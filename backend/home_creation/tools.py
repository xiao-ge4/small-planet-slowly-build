import base64
import io
import json
import mimetypes
import struct

import httpx
from PIL import Image

from backend.provider import AholoProvider, ProviderRejected, download_https, setting
from backend.review import review_cost
from backend.home_creation import prompts


class LiveTools:
    mode = "live"

    def configured(self):
        return {"agent": bool(setting("QWEN_AGENT_API_KEY") or setting("QWEN_API_KEY")),
                "image": bool(setting("TOAPIS_API_KEY")), "model": bool(setting("AHOLO_API_KEY"))}

    def request(self, method, route, **kwargs):
        base = setting("TOAPIS_BASE_URL", "https://toapis.cn").rstrip("/")
        response = httpx.request(method, base + route, headers={"Authorization": "Bearer " + setting("TOAPIS_API_KEY")},
                                 timeout=httpx.Timeout(150, connect=20), **kwargs)
        if response.is_error:
            raise ProviderRejected(response.status_code, "TOAPIS_REJECTED", response.headers.get("x-request-id"))
        return response.json()

    def submit(self, kind, payload, paths):
        if kind == "agent":
            content = [{"type": "text", "text": json.dumps(payload["context"], ensure_ascii=False)}]
            for ref, path in zip(payload["inputRefs"], paths):
                # Bound image/context size to make the configured per-call reserve useful.
                with Image.open(path) as im:
                    im = im.convert("RGB"); im.thumbnail((768, 768))
                    buf = io.BytesIO(); im.save(buf, "JPEG", quality=85)
                content.extend([{"type": "text", "text": "图片ID：" + ref["id"]},
                                {"type": "image_url", "image_url": {"url": "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()}}])
            cfg = payload["agentConfig"]
            text = payload["systemPrompt"]
            response = httpx.post(cfg["apiUrl"], headers={"Authorization": "Bearer " + (setting("QWEN_AGENT_API_KEY") or setting("QWEN_API_KEY"))},
                json={"model": cfg["model"], "messages": [{"role": "system", "content": cfg["instruction"] + "\n" + text}, {"role": "user", "content": content}],
                      "enable_thinking": False, "response_format": {"type": "json_object"}, "max_tokens": 2000, "stream": False}, timeout=cfg["timeoutSeconds"])
            if response.is_error:
                raise ProviderRejected(response.status_code, "AGENT_REJECTED", response.headers.get("x-request-id"))
            result = response.json()
            message = result["choices"][0]
            # Discard reasoning_content; keep only final JSON and metered usage.
            cost = review_cost(result.get("usage", {}), cfg)
            return {"state": "done", "content": message["message"]["content"], "finishReason": message.get("finish_reason"),
                    "costs": [{"unit": "CNY", "amount": cost["amount"] if cost else None, "basis": "usage_estimate" if cost else "unknown"}],
                    "usage": result.get("usage", {}),
                    "requestId": response.headers.get("x-request-id") or result.get("id")}
        if kind == "image":
            urls = []
            for path in paths:
                with path.open("rb") as f:
                    uploaded = self.request("POST", "/v1/uploads/images", files={"file": (path.name, f, mimetypes.guess_type(path.name)[0])}, data={"purpose": "generation"})
                urls.append(uploaded["data"]["url"])
            request = {"model": "gpt-image-2", "prompt": payload["prompt"], "n": 1,
                       "size": payload["size"], "resolution": payload["resolution"], "response_format": "url"}
            if urls:
                request["reference_images"] = urls
            response = self.request("POST", "/v1/images/generations", json=request)
            return {"state": "running", "taskId": response["id"]}
        if kind == "model":
            provider = AholoProvider()
            urls = [provider.upload(p) for p in paths]
            _, lux = provider.clients()
            cfg = payload["modelSettings"]
            task_id = lux.img_to_3d.create(imgs=urls, version=cfg["model"], face_count=cfg["faceCount"], enable_pbr=cfg["enablePbr"], output_format=["glb"])
            return {"state": "running", "taskId": str(task_id)}
        raise ValueError("Tool not permitted")

    def poll(self, kind, task_id):
        if kind == "image":
            response = self.request("GET", "/v1/images/generations/" + str(task_id))
            state = response.get("status")
            if state == "completed":
                cost = response.get("billing", {}).get("cost_usd")
                return {"state": "done", "url": response["result"]["data"][0]["url"],
                        "costs": [{"unit": "USD", "amount": float(cost) if cost is not None else None, "basis": "settled" if cost is not None else "unknown"}]}
            return {"state": "failed" if state in {"failed", "cancelled"} else "running"}
        provider = AholoProvider()
        response = provider.query(task_id)
        if response["status"] == 3:
            return {"state": "done", "outputs": response.get("outputs", []), "costs": [{"unit": "AHOLO_CREDITS", "amount": None, "basis": "unknown"}]}
        return {"state": "failed" if response["status"] in (4, 6) else "running"}

    def download(self, kind, response):
        if kind == "image":
            raw = download_https(response["url"], limit=45 * 1024 * 1024)
            with Image.open(io.BytesIO(raw)) as im:
                im.load(); out = io.BytesIO(); im.convert("RGB").save(out, "PNG")
            return out.getvalue(), "png"
        return AholoProvider().download(response, "img-to-3d")


class MockTools:
    """Clearly labelled deterministic fixtures; never access the network."""
    mode = "mock"

    def __init__(self):
        self.calls = []; self.review_script = []; self.pending_polls = 0

    def configured(self):
        return {"agent": True, "image": True, "model": True}

    def submit(self, kind, payload, paths):
        self.calls.append({"kind": kind, "payload": payload})
        if kind != "agent":
            return {"state": "running", "taskId": "mock-" + str(len(self.calls))}
        purpose = payload["purpose"]
        if purpose == "brief":
            result = {"theme": "晨露石灯小院", "mustHave": ["石灯", "开阔空地"], "mustAvoid": ["发光激光", "堵住通道"], "clarifications": [],
                      "conceptPrompt": payload["context"]["idea"] + "，温暖卡通小院，中心留空、连接口留空，无文字和人物。"}
        elif purpose == "plan":
            result = {"summary": "先验证一件主题石灯；地形由游戏提供。", "assets": [{"id": "dew-lantern", "name": "晨露石灯", "route": "generated_mesh", "description": "一盏无发光的奶油灰石质小庭院灯，圆钝底座、绿叶形装饰与琥珀色实体灯窗，不带植物地台。", "heightMetres": 0.9, "sourceId": None, "interactionKind": None}], "keepInBase": ["地形"], "reservedConnection": "保持入口净空"}
            if payload['context']['maxGeneratedAssets']==0:
                available=[a for a in payload['context']['knownResources'] if isinstance(a,dict) and a.get('placeable')]
                chosen=available[0] if available else None
                result['assets']=[{'id':'reused-decoration','name':'复用装饰','route':'reuse' if chosen else 'procedural','description':'复用现有目录资源，保留通路','heightMetres':.5,'sourceId':chosen['id'] if chosen else None,'interactionKind':None}]
        elif purpose == 'layout':
            context=payload['context']
            result = {'instances':context.get('current',{}).get('instances',context.get('currentLayout',[]))}
        elif purpose == 'companion':
            text=payload['context']['message'];state=payload['context']['gameState']
            result={'reply':'我在。'+('今天的新芽还没有浇水，我们可以先去看看。' if not state.get('wateredToday') else '今天已经照料过新芽了，可以看看地图或一起布置新土地。'),
                'action':'water' if '浇水' in text else 'chat','instruction':'','rememberQuote':text if '记住' in text else '', 'hasLayoutProposal':False,'layoutProposal':[]}
        else:
            result = self.review_script.pop(0) if self.review_script else {"action": "accept", "issues": [], "instruction": "", "referenceImageIds": []}
            if callable(result): result = result(payload)
        return {"state": "done", "content": json.dumps(result, ensure_ascii=False), "finishReason": "stop", "costs": [{"unit": "CNY", "amount": 0, "basis": "mock"}]}

    def poll(self, kind, task_id):
        if self.pending_polls > 0:
            self.pending_polls -= 1; return {"state": "running"}
        return {"state": "done", "mock": True, "costs": [{"unit": "USD" if kind == "image" else "AHOLO_CREDITS", "amount": 0, "basis": "mock"}]}

    def download(self, kind, response):
        if kind == "image":
            from PIL import ImageDraw
            im = Image.new("RGB", (1200, 600), "white"); d = ImageDraw.Draw(im)
            for x in (200, 600, 1000):
                d.rounded_rectangle((x-75, 110, x+75, 470), 25, fill="#8d9a74")
                d.rectangle((x-55, 270, x+55, 340), fill="#dfb979")
            d.text((12, 12), "MOCK - NOT GENERATED ART", fill="black")
            out = io.BytesIO(); im.save(out, "PNG"); return out.getvalue(), "png"
        vertices = struct.pack("<9f", -.5, 0, 0, .5, 0, 0, 0, 1, 0)
        doc = {"asset": {"version": "2.0", "generator": "HomeCreation MOCK fixture"}, "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0}],
               "buffers": [{"byteLength": len(vertices)}], "bufferViews": [{"buffer": 0, "byteLength": len(vertices)}],
               "accessors": [{"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [-.5,0,0], "max": [.5,1,0]}],
               "meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}]}
        raw = json.dumps(doc).encode(); raw += b" " * (-len(raw) % 4)
        return struct.pack("<4sII", b"glTF", 2, 28 + len(raw) + len(vertices)) + struct.pack("<II",len(raw),0x4E4F534A)+raw+struct.pack("<II",len(vertices),0x004E4942)+vertices, "glb"
