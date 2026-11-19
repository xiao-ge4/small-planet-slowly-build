"""Local home creation service. The existing pet/home preview servers stay separate."""
import argparse
import base64
import fcntl
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
from pathlib import Path
from urllib.parse import urlsplit, parse_qs, unquote, quote

from backend.home_creation.service import CreationService
from backend.home_creation.storage import Repository
from backend.home_creation.tools import LiveTools, MockTools
from backend.store import Problem

ROOT=Path(__file__).resolve().parent


def make_handler(service, web_root=None):
    web_root=Path(web_root).resolve() if web_root is not None else None
    def webgl_build():
        fields={'loaderUrl':'*.loader.js','dataUrl':'*.data','frameworkUrl':'*.framework.js','codeUrl':'*.wasm'}
        config={'ready':False,'productName':'捡个星球慢慢造','companyName':'Small Planet','productVersion':'0.1',
                'streamingAssetsUrl':'/play/StreamingAssets'}
        if web_root is None:return config
        for field,pattern in fields.items():
            matches=list((web_root/'Build').glob(pattern))
            if len(matches)!=1 or not matches[0].is_file():
                config['message']='网页游戏构建尚未完成，请按 README 完成构建后重试。';return config
            config[field]='/play/'+quote(matches[0].relative_to(web_root).as_posix(),safe='/')
        config['ready']=(web_root/'index.html').is_file()
        return config
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*_): pass

        def json(self,value,status=200):
            raw=json.dumps(value,ensure_ascii=False).encode();self.send_response(status)
            self.send_header("Content-Type","application/json; charset=utf-8");self.send_header("Content-Length",str(len(raw)))
            self.send_header("Cache-Control","no-store");self.send_header("X-Content-Type-Options","nosniff");self.end_headers();self.wfile.write(raw)

        def body(self):
            origin=self.headers.get("Origin")
            if origin and origin!="http://"+self.headers.get("Host",""):raise Problem("拒绝跨站请求。",403)
            if self.headers.get_content_type()!="application/json":raise Problem("请使用 JSON。",415)
            try:size=int(self.headers.get("Content-Length",0))
            except ValueError:raise Problem("请求长度不合法。")
            if not 0<size<=15*1024*1024:raise Problem("请求过大或为空。",413)
            try:body=json.loads(self.rfile.read(size))
            except (ValueError,UnicodeError):raise Problem("JSON 不合法。")
            if not isinstance(body,dict):raise Problem("请求需为 JSON 对象。")
            return body

        def file(self,path):
            if not path.is_file():raise Problem("文件不存在。",404)
            self.send_response(200);self.send_header("Content-Type","application/wasm" if path.suffix=='.wasm' else mimetypes.guess_type(path.name)[0] or "application/octet-stream")
            self.send_header("Content-Length",str(path.stat().st_size));self.send_header("Cache-Control","no-store");self.send_header("X-Content-Type-Options","nosniff");self.end_headers()
            if self.command=='HEAD':return
            with path.open("rb") as f:
                while chunk:=f.read(256*1024):self.wfile.write(chunk)

        def dispatch(self):
            try:self.route()
            except Problem as e:self.json({"error":{"code":e.code,"message":str(e)}},e.status)
            except (BrokenPipeError,ConnectionResetError):pass
            except Exception as e:
                print("Home creation HTTP error: "+type(e).__name__,flush=True)
                self.json({"error":{"code":"INTERNAL","message":"服务处理失败，请查看任务记录。"}},500)

        def route(self):
            path=urlsplit(self.path).path
            if web_root is not None and self.command in {'GET','HEAD'}:
                if path=='/api/submission/status':
                    return self.json({'name':'捡个星球慢慢造','webReady':webgl_build()['ready'],
                        'mode':service.tools.mode,'configured':service.tools.configured(),
                        'scope':'local-single-player-with-persistent-npc-neighbors'})
                if path=='/api/submission/build':return self.json(webgl_build())
                if path in {'/','/index.html'}:return self.file(ROOT/'web/submission/index.html')
                if path=='/play/':return self.file(ROOT/'web/submission/play.html')
                if path=='/play':
                    self.send_response(302);self.send_header('Location','/play/');self.end_headers();return
                prefix='/play/' if path.startswith('/play/') else '/submission/' if path.startswith('/submission/') else None
                if prefix:
                    directory=web_root if prefix=='/play/' else (ROOT/'web/submission').resolve()
                    relative=unquote(path[len(prefix):]) or 'index.html'
                    target=(directory/relative).resolve()
                    if directory not in target.parents:raise Problem('页面不存在。',404)
                    return self.file(target)
            if path.startswith('/api/game-v1/'):
                bits=path[len('/api/game-v1/'):].strip('/').split('/');game=service.game
                if self.command=='GET':
                    if bits==['settings']:return self.json(game.settings())
                    if len(bits)==2 and bits[0]=='status':return self.json(game.connect({'clientId':bits[1],'theme':game.get_profile(bits[1])['theme']}))
                    if bits==['assets']:return self.json(game.assets())
                    if len(bits)==2 and bits[0]=='world':return self.json(game.neighbors.view(bits[1]))
                    if len(bits)==2 and bits[0]=='memories':return self.json(game.memories(bits[1]))
                    if len(bits)==2 and bits[0]=='layout':return self.json(game.layout(bits[1]))
                    if len(bits)==4 and bits[0]=='visual-review':return self.json(game.visual.get(bits[1],bits[2],bits[3]))
                    if len(bits)==3 and bits[0]=='turn':return self.json(game.turn(bits[1],bits[2]))
                    if len(bits)==3 and bits[0]=='creation':return self.json(game.creation_view(bits[1],bits[2]))
                if self.command=='POST':
                    body=self.body()
                    if bits==['settings']:return self.json(game.update_settings(body))
                    if bits==['connect']:return self.json(game.connect(body))
                    if bits==['snapshot']:return self.json(game.snapshot(body))
                    if bits==['chat']:return self.json(game.chat(body))
                    if bits==['inspiration']:return self.json(game.inspiration(body))
                    if bits==['neighbor-action']:return self.json(game.neighbors.action(body))
                    if bits==['neighbor-visit']:return self.json(game.neighbors.record_visit(body.get('clientId'),body.get('residentId')))
                    if bits==['neighbor-geometry']:return self.json(game.neighbors.register_geometry(body.get('clientId'),body.get('residentId'),body))
                    if bits==['memory-change']:return self.json(game.memory_change(body))
                    if bits==['summary']:return self.json(game.summarize(body.get('clientId')))
                    if bits==['create']:return self.json(game.create(body))
                    if bits==['preview']:return self.json(game.runtime.preview(body))
                    if bits==['review-runtime']:return self.json(game.visual.start(body))
                    if bits==['finalize']:return self.json(game.runtime.finalize(body))
                    if bits==['prepare-runtime']:return self.json(game.runtime.prepare(body.get('clientId'),body.get('runId')))
                    if len(bits)==4 and bits[0]=='creation':return self.json(game.creation_action(bits[1],bits[2],bits[3],body))
                raise Problem('游戏接口不存在。',404)
            if self.command=="GET" and path in {"/","/home-creation.html","/home-creation.js","/home-creation-settings.html","/home-workspace.html","/home-workspace.css","/home-workspace.js","/game-settings.html","/vendor/model-viewer.min.js"}:
                return self.file(ROOT/("home-creation.html" if path=="/" else path[1:]))
            prefix="/api/home-creation/"
            if not path.startswith(prefix):raise Problem("页面不存在。",404)
            bits=path[len(prefix):].strip("/").split("/")
            if self.command=="GET":
                if bits==['catalog']:
                    q=parse_qs(urlsplit(self.path).query)
                    return self.json({'assets':service.workspace.catalog.search(q.get('q',[''])[0],interaction=q.get('interaction',[None])[0],placeable=True if q.get('placeable')==['true'] else None)})
                if bits==['memories']:return self.json({'memories':service.workspace.memories(),'context':service.workspace.memory_context()})
                if bits==["config"]:return self.json({"mode":service.tools.mode,"configured":service.tools.configured(),"supports":["brief","concept","breakdown","asset_design","model_download","asset_preparation","unity_validation","final_layout_confirmation","asset_catalog","layout_editor","creative_memory","local_game_entry"],"unityAssembly":True})
                if bits==["settings"]:return self.json(service.settings())
                if bits==["runs"]:return self.json([{k:r.get(k) for k in ["id","idea","mode","status","phase","createdAt","updatedAt"]} for r in service.repo.all()])
                if len(bits)==3 and bits[0]=='runs' and bits[2]=='editor':return self.json(service.workspace.editor(bits[1]))
                if len(bits)==2 and bits[0]=="runs":return self.json(service.view(bits[1]))
                if len(bits)==2 and bits[0]=="files":
                    ref=service.repo.file(bits[1]);return self.file(service.repo.path(ref))
            if self.command=="POST":
                body=self.body()
                if bits==['catalog','refresh']:return self.json(service.workspace.refresh_catalog())
                if bits==['memories']:return self.json(service.workspace.preference(body))
                if len(bits)==3 and bits[0]=='memories' and bits[2]=='retire':return self.json(service.workspace.retire_memory(bits[1]))
                if bits==['game-grant']:return self.json(service.workspace.game_grant(body))
                if bits==["settings"]:return self.json(service.update_settings(body))
                if bits==["runs"]:return self.json(service.create(body),201)
                if len(bits)==3 and bits[0]=="runs":
                    rid,action=bits[1:]
                    if action=='draft':return self.json(service.workspace.save_draft(rid,body))
                    if action=='assemble':return self.json(service.workspace.assemble(rid))
                    if action=='propose-layout':return self.json(service.workspace.propose_layout(rid,body))
                    if action=='prepare-assets':return self.json(service.workspace.prepare_assets(rid))
                    if action=='bootstrap-layout':return self.json(service.workspace.bootstrap(rid))
                    if action=='install-local':return self.json(service.workspace.publish(rid))
                    if action=="confirm":return self.json(service.confirm(rid,body))
                    if action=="layout":return self.json(service.register_layout(rid,body))
                    if action=="validation":return self.json(service.register_validation(rid,body))
                    if action=="verify-runtime":return self.json(service.start_validation(rid))
                    if action=="reference":
                        try:raw=base64.b64decode(body.get("data",""),validate=True)
                        except Exception:raise Problem("图片数据不合法。")
                        return self.json(service.add_reference(rid,raw,body.get("name","参考图")))
                    return self.json(service.control(rid,action,body))
                if len(bits)==5 and bits[0]=="runs" and bits[2]=="tasks" and bits[4]=="reconcile":return self.json(service.reconcile(bits[1],bits[3],body.get("providerTaskId")))
            raise Problem("接口不存在。",404)

        do_GET=do_POST=do_HEAD=dispatch
    return Handler


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port",type=int,default=8767);parser.add_argument("--mode",choices=["mock","live"],default="mock")
    parser.add_argument("--data-dir",type=Path,default=ROOT/"data/home-creation")
    parser.add_argument('--web-root',type=Path,help='Serve the submission landing page and Unity WebGL build from the same origin.')
    args=parser.parse_args();args.data_dir.mkdir(parents=True,exist_ok=True)
    lock=(args.data_dir/"service.lock").open("w")
    try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    except BlockingIOError:raise SystemExit("This creation data directory is already served by another process.")
    service=CreationService(Repository(args.data_dir),LiveTools() if args.mode=="live" else MockTools())
    # Never replay mock task IDs against live providers or vice versa.
    if any(r["mode"]!=args.mode for r in service.repo.all()):raise SystemExit("Use separate data directories for live and mock runs.")
    server=ThreadingHTTPServer(("127.0.0.1",args.port),make_handler(service,args.web_root));service.recover()
    print("家园共创：http://127.0.0.1:%d/ · %s"%(args.port,args.mode),flush=True)
    try:server.serve_forever()
    except KeyboardInterrupt:pass
    finally:server.server_close();service.shutdown();service.repo.close();lock.close()


if __name__=="__main__":main()
