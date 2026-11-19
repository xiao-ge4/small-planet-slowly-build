import base64
import json
import os
from pathlib import Path
import time
from urllib.parse import urlparse

import httpx
from manycore.aholo_sdk_core import AholoClientConfig
from manycore.aholo_sdk_asset import create_asset_client
from manycore.aholo_sdk_lux3d import create_lux3d_client

from backend.store import Problem

ROOT = Path(__file__).resolve().parent.parent


def env_file():
    values = {}
    path = ROOT / ".env.local"
    if path.exists():
        for line in path.read_text().splitlines():
            if line.strip() and not line.lstrip().startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                values[key.strip()] = value.strip().strip("\"'")
    return values


def setting(name, default=""):
    return os.environ.get(name, env_file().get(name, default))


class ProviderRejected(Exception):
    """A definite HTTP rejection, without retaining credentials or response URLs."""
    def __init__(self, status, code="PROVIDER_REJECTED", request_id=None):
        self.status, self.code, self.request_id = status, str(code)[:100], request_id
        super().__init__("Provider rejected request (HTTP %d)." % status)


def download_https(url, limit=120 * 1024 * 1024):
    if urlparse(url).scheme != "https":
        raise Problem("平台返回了无效的结果链接。", code="INVALID_OUTPUT_URL")
    data = bytearray()
    # Download is read-only: transient connection failures may safely be retried.
    for attempt in range(3):
        try:
            data.clear()
            with httpx.stream("GET", url, timeout=120, follow_redirects=True) as response:
                response.raise_for_status()
                for chunk in response.iter_bytes():
                    data.extend(chunk)
                    if len(data) > limit:
                        raise Problem("生成文件过大，已停止下载。")
            return bytes(data)
        except (httpx.TransportError, httpx.TimeoutException):
            if attempt == 2:
                raise
            time.sleep(1 + attempt)


class QwenProvider:
    mode = "live"
    name = "qwen-image"

    def __init__(self, client=None):
        self.key = setting("QWEN_API_KEY")
        self.model = setting("QWEN_MODEL", "qwen-image-3.0")
        self.api_url = setting("QWEN_API_URL", "https://maas.qianwenaiapi.com/compatible-mode/v1/images/generations")
        self.client = client

    @property
    def configured(self):
        return bool(self.key)

    def generate(self, images, prompt, negative_prompt=""):
        if len(images) > 3:
            raise Problem("设计图最多使用 3 张参考图。")
        payload = {"model": self.model, "prompt": prompt, "n": 1,
                   "size": "1024x1024", "prompt_extend": False, "watermark": False}
        if negative_prompt:
            payload["negative_prompt"] = negative_prompt
        if images:
            encoded = []
            for path in images:
                from PIL import Image
                with Image.open(path) as im:
                    mime = Image.MIME[im.format]
                encoded.append("data:%s;base64,%s" % (mime, base64.b64encode(path.read_bytes()).decode()))
            payload["image"] = encoded[0] if len(encoded) == 1 else encoded
        # This synchronous, potentially billable POST is never automatically retried.
        kwargs = {"json": payload, "headers": {"Authorization": "Bearer " + self.key},
                  "timeout": httpx.Timeout(600, connect=30)}
        response = self.client.post(self.api_url, **kwargs) if self.client else httpx.post(self.api_url, **kwargs)
        request_id = response.headers.get("x-request-id")
        if response.is_error:
            try:
                code = response.json().get("error", {}).get("code", "PROVIDER_REJECTED")
            except (ValueError, AttributeError):
                code = "PROVIDER_REJECTED"
            raise ProviderRejected(response.status_code, code, request_id)
        result = response.json()
        if not isinstance(result, dict):
            raise ValueError("Unexpected Qwen response")
        return {**result, "requestId": request_id, "responseStatus": response.status_code}

    def download(self, result):
        outputs = result.get("data", [])
        if not outputs or not outputs[0].get("url"):
            raise Problem("Qwen 已响应，但没有可下载的设计图。", code="MISSING_OUTPUT")
        return download_https(outputs[0]["url"], limit=20 * 1024 * 1024), "png"


class AholoProvider:
    mode = "live"
    name = "aholo-lux3d"

    def __init__(self):
        config = env_file()
        self.key = os.environ.get("AHOLO_API_KEY") or config.get("AHOLO_API_KEY", "")
        self.region = os.environ.get("AHOLO_REGION") or config.get("AHOLO_REGION", "cn")
        self.model = setting("AHOLO_MODEL", "G1-Turbo")
        self.api_url = "https://api.aholo3d.cn" if self.region == "cn" else "https://api.aholo3d.com"
        if self.region not in {"cn", "com"}:
            raise Problem("AHOLO_REGION 需为 cn 或 com。")

    @property
    def configured(self):
        return bool(self.key)

    def clients(self):
        config = AholoClientConfig(api_key=self.key, region=self.region)
        return create_asset_client(config), create_lux3d_client(config)

    def upload(self, path):
        asset, _ = self.clients()
        return asset.upload_file(path).url

    def create(self, kind, urls, prompt=None):
        if kind != "img-to-3d" or len(urls) != 1:
            raise Problem("Aholo 仅用于将一张已确认设计图生成 3D。")
        _, lux3d = self.clients()
        return str(lux3d.img_to_3d.create(img=urls[0], version=self.model, face_count=100000,
            output_format=["glb"], enable_pbr=True))

    def query(self, task_id):
        _, lux3d = self.clients()
        return lux3d.tasks.retrieve(task_id)

    def download(self, result, kind):
        if kind != "img-to-3d":
            raise Problem("Aholo 只处理模型文件，设计图请使用 Qwen。")
        wanted = {".glb"}
        for item in result.get("outputs", []):
            url = item.get("content", "")
            parsed = urlparse(url)
            if parsed.scheme != "https":
                continue
            suffix = Path(parsed.path).suffix.lower()
            if suffix not in wanted:
                continue
            return download_https(url), suffix.lstrip(".")
        raise Problem("平台未返回预期格式的文件。", code="MISSING_OUTPUT")


class MockProvider:
    """Deterministic offline adapter for tests. It never calls a paid API."""
    mode = "mock"
    region = "cn"
    configured = True
    name = "aholo-lux3d"
    model = "G1-Turbo"
    api_url = "mock://aholo"

    def __init__(self):
        self.created = []

    def upload(self, path):
        return "mock://" + Path(path).name

    def create(self, kind, urls, prompt=None):
        task_id = str(time.time_ns())
        self.created.append(task_id)
        return task_id

    def query(self, task_id):
        return {"taskId": task_id, "status": 3, "outputs": []}

    def download(self, result, kind):
        if kind == "img-to-3d":
            # Reuse a real fixture locally, or render a valid triangle in fresh checkouts.
            candidates = sorted((ROOT / "validation-runs").glob("*/model.glb"))
            if candidates:
                return candidates[0].read_bytes(), "glb"
            import struct
            vertices = struct.pack("<9f", -.5, 0, 0, .5, 0, 0, 0, 1, 0)
            doc = {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}],
                   "nodes": [{"mesh": 0}], "buffers": [{"byteLength": len(vertices)}],
                   "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": len(vertices), "target": 34962}],
                   "accessors": [{"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3",
                                  "min": [-.5, 0, 0], "max": [.5, 1, 0]}],
                   "materials": [{"doubleSided": True, "pbrMetallicRoughness": {"baseColorFactor": [.3, .6, .4, 1], "metallicFactor": 0}}],
                   "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "material": 0}]}]}
            raw = json.dumps(doc).encode()
            raw += b" " * ((-len(raw)) % 4)
            return (struct.pack("<4sIIII", b"glTF", 2, 28 + len(raw) + len(vertices), len(raw), 0x4E4F534A)
                    + raw + struct.pack("<II", len(vertices), 0x004E4942) + vertices), "glb"
        raise Problem("模拟 Aholo 只处理模型文件。")


class MockImageProvider:
    mode = "mock"
    configured = True
    name = "qwen-image"
    model = "qwen-image-3.0"
    api_url = "mock://qwen"

    def __init__(self):
        self.created = []

    def generate(self, images, prompt, negative_prompt=""):
        self.created.append({"images": [str(p) for p in images], "prompt": prompt, "negative_prompt": negative_prompt})
        return {"requestId": "mock-qwen-" + str(time.time_ns()), "responseStatus": 200,
                "data": [{"url": "mock://design"}], "usage": {
                    "input_image_count": len(images), "output_image_count": 1}}

    def download(self, result):
        from PIL import Image, ImageDraw
        import io
        out = io.BytesIO()
        image = Image.new("RGB", (768, 768), "#f1eee1")
        draw = ImageDraw.Draw(image)
        color = ["#9bcaba", "#82a6de", "#dfb77b"][max(0, len(self.created) - 1) % 3]
        draw.ellipse((130, 575, 630, 670), fill="#dedfce")
        draw.rounded_rectangle((475, 265, 650, 505), radius=70, outline=color, width=44)
        draw.rounded_rectangle((180, 190, 540, 610), radius=100, fill=color)
        draw.ellipse((180, 165, 540, 300), fill="#c6e5d6", outline="#647f7c", width=8)
        draw.ellipse((205, 189, 515, 274), fill="#718b88")
        draw.rounded_rectangle((215, 310, 248, 520), radius=16, fill="#c7e4d8")
        image.save(out, "PNG")
        return out.getvalue(), "png"
