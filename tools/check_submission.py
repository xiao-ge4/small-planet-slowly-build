#!/usr/bin/env python3
"""Verify a clean extracted submission without paid services or existing saves."""
import argparse
import hashlib
import json
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def request(base, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base + path, data=data,
                                 headers={"Content-Type": "application/json"} if data else {})
    with urllib.request.urlopen(req, timeout=5) as response:
        raw = response.read()
        return json.loads(raw) if response.headers.get_content_type() == "application/json" else raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-only", action="store_true", help="skip WebGL HTTP checks before a build exists")
    args = parser.parse_args()
    sys.path.insert(0, str(ROOT))
    from tools.package_submission import rejected, known_credentials, scan_secrets
    from tools.init_submission_data import initialize
    from backend.home_creation.catalog import AssetCatalog
    from backend.home_creation.storage import Repository

    manifest_path = ROOT / "submission-manifest.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.is_file() else None
    if manifest:
        expected = {item["path"] for item in manifest["files"]} | {"submission-manifest.json"}
        for item in manifest["files"]:
            name = item["path"]
            if rejected(name):
                raise SystemExit("包内出现排除文件：" + name)
            path = ROOT / name
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != item["sha256"]:
                raise SystemExit("文件缺失或校验失败：" + name)
        # Verify the real directory too, so unlisted local databases/keys cannot pass as clean.
        for path in ROOT.rglob("*"):
            relative = path.relative_to(ROOT)
            if path.is_file() and not {"__pycache__", ".venv"}.intersection(relative.parts):
                if rejected(relative) or relative.as_posix() not in expected:
                    raise SystemExit("解压目录不干净：" + str(relative))
    credentials = known_credentials()
    for name in (item["path"] for item in manifest["files"]) if manifest else (".env.example",):
        scan_secrets(ROOT / name, credentials)
    with tempfile.TemporaryDirectory(prefix="small-planet-clean-") as temporary:
        data_dir = Path(temporary) / "data"
        initialize(data_dir)
        repo = Repository(data_dir)
        try:
            catalog = AssetCatalog(repo, ROOT)
            assets = catalog.search(limit=100)
            if len(assets) != 19:
                raise SystemExit("干净资源库应包含 19 项资产。")
            for asset in assets:
                catalog.check_dependencies(asset)
                repo.path(asset["previewRef"])
                if asset.get("runtimeModelRef"):
                    repo.path(asset["runtimeModelRef"])
            if repo.db.execute("SELECT COUNT(*) FROM runs").fetchone()[0] != 0:
                raise SystemExit("初始化混入了历史任务。")
        finally:
            repo.close()
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            port = probe.getsockname()[1]
        command = [sys.executable, str(ROOT / "home_server.py"), "--mode", "mock", "--port", str(port),
                   "--data-dir", str(data_dir)]
        if not args.source_only:
            command += ["--web-root", str(ROOT / "artifacts/submission/WebGL")]
        with (Path(temporary) / "server.log").open("w+") as log:
            process = subprocess.Popen(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
            base = "http://127.0.0.1:%d" % port
            try:
                deadline = time.monotonic() + 20
                while time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise SystemExit("模拟服务未能启动。")
                    try:
                        config = request(base, "/api/home-creation/config")
                        break
                    except (OSError, urllib.error.URLError):
                        time.sleep(0.2)
                else:
                    raise SystemExit("模拟服务启动超时。")
                if config["mode"] != "mock":
                    raise SystemExit("验收必须使用 mock。")
                if len(request(base, "/api/home-creation/catalog")["assets"]) != 19:
                    raise SystemExit("HTTP 资源目录不完整。")
                request(base, "/api/game-v1/connect", {"clientId": "0123456789abcdef0123456789abcdef", "theme": "dawn"})
                if not isinstance(request(base, "/api/game-v1/settings").get("name"), str):
                    raise SystemExit("小禾设置接口异常。")
                if not args.source_only:
                    if b"<html" not in request(base, "/").lower():
                        raise SystemExit("提交入口缺失。")
                    playable = request(base, "/play/")
                    if b"<html" not in playable.lower() or b"/api/submission/build" not in playable:
                        raise SystemExit("WebGL 入口缺失。")
                    status = request(base, "/api/submission/status")
                    build = request(base, "/api/submission/build")
                    if status.get("mode") != "mock" or not status.get("webReady") or not build.get("ready"):
                        raise SystemExit("提交状态或构建元数据未就绪。")
                    for field in ("loaderUrl", "dataUrl", "frameworkUrl", "codeUrl"):
                        resource = build.get(field, "")
                        if not resource.startswith("/play/Build/"):
                            raise SystemExit("WebGL 资源地址异常：" + field)
                        head = urllib.request.Request(base + resource, method="HEAD")
                        with urllib.request.urlopen(head, timeout=5) as response:
                            if int(response.headers.get("Content-Length", "0")) <= 0:
                                raise SystemExit("WebGL 资源为空：" + field)
                            if field == "codeUrl" and response.headers.get_content_type() != "application/wasm":
                                raise SystemExit("WebAssembly MIME 不正确。")
                    cover = request(base, "/submission/assets/cover.jpg")
                    if not cover.startswith(b"\xff\xd8"):
                        raise SystemExit("封面缺失或不是 JPEG。")
                print(json.dumps({"passed": True, "assetCount": 19, "freshDatabase": True,
                                  "mockHttp": True, "webglHttp": not args.source_only,
                                  "manifestVerified": bool(manifest), "paidCalls": 0}, ensure_ascii=False))
            finally:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


if __name__ == "__main__":
    main()
