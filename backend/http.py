import base64
import binascii
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
from pathlib import Path
from urllib.parse import unquote, urlsplit

from backend.store import Problem, public
from backend.art import prices
from backend.prompts import compose, compose_anthro, defaults, get_settings, save_settings, validate_fields

ROOT = Path(__file__).resolve().parent.parent


def image_data(body):
    try:
        return base64.b64decode(body.get("data", ""), validate=True)
    except (binascii.Error, TypeError):
        raise Problem("图片数据不合法。", code="INVALID_IMAGE")


def make_handler(studio):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def json(self, value, status=200):
            raw = json.dumps(public(value), ensure_ascii=False).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(raw)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            self.wfile.write(raw)

        def body(self):
            try:
                size = int(self.headers.get("Content-Length", "0"))
            except ValueError:
                raise Problem("请求长度不合法。")
            if size < 0 or size > 15 * 1024 * 1024:
                raise Problem("请求过大，每张图片最多 10 MB。", 413)
            if self.headers.get_content_type() != "application/json":
                raise Problem("请使用 JSON 请求。", 415)
            try:
                result = json.loads(self.rfile.read(size))
            except (ValueError, UnicodeError):
                raise Problem("请求内容不是合法 JSON。")
            if not isinstance(result, dict):
                raise Problem("请求内容应为 JSON 对象。")
            return result

        def handle_request(self):
            route = unquote(urlsplit(self.path).path).strip("/")
            bits = route.split("/")
            method = self.command
            if method != "GET":
                origin = self.headers.get("Origin")
                host = self.headers.get("Host")
                if origin and origin != "http://" + host:
                    raise Problem("拒绝跨站请求。", 403)
            if method == "GET":
                if route == "api/prompt-settings":
                    return self.json({"active": get_settings(studio.store), "defaults": defaults(),
                                      "agentConfigured": studio.review_provider.configured})
                if route == "api/config":
                    return self.json({"provider": studio.provider.mode, "configured": studio.provider.configured,
                        "imageService": {"provider": "qwen-image", "configured": studio.image_provider.configured,
                                         "model": studio.image_provider.model},
                        "modelService": {"provider": "aholo-lux3d", "configured": studio.provider.configured,
                                         "model": studio.provider.model, "region": studio.provider.region},
                        "artRulesVersion": get_settings(studio.store)["baseVersion"],
                        "promptSettingsRevision": get_settings(studio.store)["revision"], "prices": prices(),
                        "region": studio.provider.region, "maxImages": 3, "maxImageBytes": 10485760})
                if route == "api/projects":
                    return self.json([{"id": p["id"], "title": p["title"], "status": p["status"],
                                       "updatedAt": p["updatedAt"], "versionCount": len(p["versions"])} for p in studio.store.all()])
                if len(bits) == 4 and bits[:2] == ["api", "projects"] and bits[3] == "anthro":
                    return self.json(studio.anthro_context(bits[2]))
                if len(bits) == 3 and bits[:2] == ["api", "projects"]:
                    return self.json(studio.store.get(bits[2]))
                if len(bits) == 2 and bits[0] == "files":
                    entry = studio.store.file(bits[1])
                    return self.send_file(studio.store.file_path(bits[1]), immutable=True)
                if route == "api/validation":
                    from scripts.serve_preview import results
                    return self.json(results())
                return self.static(route)
            if method == "PATCH" and route == "api/prompt-settings":
                return self.json(save_settings(studio.store, self.body()))
            if method == "POST" and route == "api/prompt-settings/preview":
                body = self.body()
                fields = validate_fields(body.get("fields"))
                stage = body.get("stage")
                if stage not in {"textDesign", "referenceDesign", "revision", "anthro"}:
                    raise Problem("请选择有效的生成步骤。")
                idea = body.get("instruction", "")
                if not isinstance(idea, str) or len(idea) > 1500:
                    raise Problem("想法最多 1500 字。")
                purposes = body.get("purposes", [])
                if (not isinstance(purposes, list) or len(purposes) > 3
                    or any(not isinstance(p, str) or not p.strip() or len(p) > 80 for p in purposes)):
                    raise Problem("最多 3 张参考图，每张用途需为 1–80 字。")
                if stage == "anthro":
                    anthro = body.get("anthro") or get_settings(studio.store)["anthro"]
                    refs = [{"purpose": p} for p in purposes] or [{"purpose": "原物件确认设计图，保留材质与关键结构"}]
                    return self.json({"prompt": compose_anthro(idea.strip(), refs, body.get("conceptOption", "handle-ear"), fields=anthro),
                                      "negativePrompt": anthro["negative"]})
                if stage == "textDesign":
                    purposes = []
                elif not purposes:
                    raise Problem("请填写参考图用途。")
                if stage == "revision":
                    purposes[0] = "当前选中的设计图，作为本轮修改的主体基础"
                return self.json({"prompt": compose(idea.strip(), [{"purpose": p} for p in purposes],
                    revision=stage == "revision", fields=fields), "negativePrompt": fields["negative"]})
            if method == "POST" and route == "api/projects":
                body = self.body()
                title = body.get("title", "未命名物件")
                if not isinstance(title, str) or not title.strip() or len(title) > 80:
                    raise Problem("项目名称需为 1–80 个字。")
                return self.json(studio.store.create(title.strip()), 201)
            if len(bits) >= 3 and bits[:2] == ["api", "projects"]:
                project_id = bits[2]
                if method == "PATCH" and len(bits) == 3:
                    return self.json(studio.patch(project_id, self.body()))
                if method == "POST" and bits[3:] == ["images"]:
                    body = self.body()
                    return self.json(studio.upload(project_id, image_data(body), str(body.get("fileName", "image")),
                                                  activate=body.get("scope") != "auxiliary", purpose=body.get("purpose")))
                if method == "POST" and bits[3:] == ["images", "order"]:
                    return self.json(studio.order_images(project_id, self.body().get("imageIds")))
                if len(bits) in {5, 6} and bits[3] == "images":
                    if method == "PATCH" and len(bits) == 5:
                        return self.json(studio.edit_image(project_id, bits[4], self.body()))
                    if method == "DELETE" and len(bits) == 5:
                        return self.json(studio.change_images(project_id, bits[4], True))
                    if method == "POST" and len(bits) == 6 and bits[5] == "primary":
                        self.body()
                        return self.json(studio.change_images(project_id, bits[4]))
                if method == "POST":
                    body = self.body()
                    if bits[3:] == ["generate"]:
                        return self.json(studio.start(project_id, "generate", body), 202)
                    if bits[3:] == ["revisions"]:
                        return self.json(studio.start(project_id, "revise", body), 202)
                    if bits[3:] == ["anthro", "generate"]:
                        return self.json(studio.start_anthro(project_id, "generate", body), 202)
                    if bits[3:] == ["anthro", "revisions"]:
                        return self.json(studio.start_anthro(project_id, "revise", body), 202)
                    if len(bits) == 6 and bits[3] == "versions":
                        version_id, action = bits[4], bits[5]
                        if action == "generate":
                            return self.json(studio.start(project_id, "approve-design", {**body, "versionId": version_id}), 202)
                        if action == "confirm":
                            return self.json(studio.confirm(project_id, version_id))
                        if action == "discard":
                            return self.json(studio.discard(project_id, version_id))
                        if action == "preview":
                            return self.json(studio.preview(project_id, version_id, image_data(body)))
                        if action == "confirm-anthro":
                            return self.json(studio.confirm_anthro(project_id, version_id))
                        if action == "inspect-rig":
                            return self.json(studio.inspect_anthro_rig(project_id, version_id))
                        if action == "generate-anthro":
                            return self.json(studio.start_anthro(project_id, "approve", {**body, "versionId": version_id}), 202)
                    if len(bits) == 6 and bits[3] == "tasks" and bits[5] == "retry":
                        return self.json(studio.retry(project_id, bits[4], body.get("requestId")), 202)
            raise Problem("接口不存在。", 404, "NOT_FOUND")

        def static(self, route):
            route = route or "index.html"
            path = (ROOT / route).resolve()
            fixed = {"index.html", "styles.css", "app.js", "settings.html", "settings.js", "anthro.html", "anthro.js", "validation.html", "validation.js",
                     "vendor/model-viewer.min.js", "test-inputs/test.jpeg"}
            allowed_output = path.is_relative_to(ROOT / "validation-runs") and path.suffix in {".glb", ".png", ".jpg", ".jpeg", ".webp"}
            if not path.is_relative_to(ROOT) or not (route in fixed or allowed_output) or not path.is_file():
                raise Problem("页面不存在。", 404, "NOT_FOUND")
            return self.send_file(path)

        def send_file(self, path, immutable=False):
            if not path.is_file():
                raise Problem("文件不存在。", 404, "NOT_FOUND")
            self.send_response(200)
            self.send_header("Content-Type", mimetypes.guess_type(path.name)[0] or "application/octet-stream")
            self.send_header("Content-Length", str(path.stat().st_size))
            self.send_header("Cache-Control", "private, max-age=31536000, immutable" if immutable else "no-cache")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            with path.open("rb") as f:
                while chunk := f.read(256 * 1024):
                    self.wfile.write(chunk)

        def dispatch(self):
            try:
                self.handle_request()
            except Problem as exc:
                self.json({"error": {"code": exc.code, "message": str(exc)}}, exc.status)
            except (BrokenPipeError, ConnectionResetError):
                pass
            except Exception as exc:
                print("HTTP error: " + type(exc).__name__, flush=True)
                self.json({"error": {"code": "INTERNAL", "message": "服务处理失败，请稍后重试。"}}, 500)

        do_GET = do_POST = do_PATCH = do_DELETE = dispatch

    return Handler


def serve(studio, port=8765):
    server = ThreadingHTTPServer(("127.0.0.1", port), make_handler(studio))
    studio.recover()
    print("物件工作台：http://127.0.0.1:%d/，模式：%s" % (port, studio.provider.mode), flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        studio.shutdown()
