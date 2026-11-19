import copy
import hashlib
from concurrent.futures import ThreadPoolExecutor
import io
import json
import math
from pathlib import Path
import re
import subprocess
import threading
import time
from uuid import uuid4

from PIL import Image, ImageChops, ImageOps

from backend.home_creation import prompts
from backend.home_creation.storage import canonical, fingerprint
from backend.home_creation.workspace import Workspace
from backend.game.service import GameService
from backend.game.visual_review import PROMPT as VISUAL_REVIEW_PROMPT
from backend.provider import ProviderRejected
from backend.review import DEFAULT_AGENT, validate_agent
from backend.store import Problem, now, public
from scripts.verify_lux3d import inspect_glb

ROOT = Path(__file__).resolve().parents[2]
STAGES = ["brief", "concept", "breakdown", "asset_design", "assets", "layout", "validation", "finalize"]
UNITS = ("USD", "CNY", "AHOLO_CREDITS")
REQUIRED_RUNTIME_CHECKS = {
    "scene_source_unchanged", "layout_source_unchanged", "dependencies_unchanged",
    "baked_navigation", "spawn_grounded", "watering_complete", "seedling_growth",
    "open_chest_complete", "duplicate_reward_prevented", "save_reload_progress",
    "new_land_navigation_connected", "prepared_layout_on_new_continent",
    "prepared_asset_collision", "prepared_west_route", "player_walks_onto_new_land",
    "expanded_save_restores_land_and_player", "mid_animation_reload_recovers",
    "save_failure_keeps_permit_and_land_locked",
}
DEFAULTS = {
    "agent": {**DEFAULT_AGENT, "instruction": prompts.RULES, "repairInstruction": "仅修正指出的问题，保留用户确认的主题与主要形体。"},
    "style": prompts.STYLE, "briefPrompt": prompts.BRIEF, "planPrompt": prompts.PLAN, "reviewPrompt": prompts.REVIEW, "assetSheetPrompt": prompts.ASSET_SHEET, "layoutPrompt": prompts.LAYOUT,
    "visualReviewPrompt": VISUAL_REVIEW_PROMPT,
    "agentReserveCny": 0.20, "imageReserveUsd": 0.025,
    "model": {"provider": "aholo-lux3d", "model": "G1", "faceCount": 300000, "enablePbr": True, "format": "glb"}
}
KNOWN_RESOURCES = {
    "watering-can": "Assets/DawnGarden/Prefabs/WateringCan.prefab",
    "hinged-chest": "Assets/DawnGarden/Prefabs/InteractiveChest.prefab"
}


def number(value, lower, upper):
    return type(value) in (int, float) and math.isfinite(value) and lower <= value <= upper


def validate_brief(value):
    if not isinstance(value, dict) or set(value) != {"theme", "mustHave", "mustAvoid", "clarifications", "conceptPrompt"}:
        raise ValueError("Invalid brief fields")
    for k in ("theme", "conceptPrompt"):
        if not isinstance(value[k], str) or not 1 <= len(value[k]) <= 6000:
            raise ValueError("Invalid brief text")
    for k in ("mustHave", "mustAvoid", "clarifications"):
        if not isinstance(value[k], list) or len(value[k]) > 20 or any(not isinstance(s,str) or not 1 <= len(s) <= 500 for s in value[k]):
            raise ValueError("Invalid brief list")
    return value


def validate_plan(value, limit, resources=None):
    if not isinstance(value, dict) or set(value) != {"summary", "assets", "keepInBase", "reservedConnection"}:
        raise ValueError("Invalid plan fields")
    if not isinstance(value["summary"], str) or len(value["summary"]) > 2000 or not isinstance(value["reservedConnection"], str) or len(value["reservedConnection"]) > 2000:
        raise ValueError("Invalid plan text")
    if not isinstance(value["keepInBase"],list) or len(value["keepInBase"]) > 30 or any(not isinstance(x,str) or len(x)>500 for x in value["keepInBase"]):
        raise ValueError("Invalid base items")
    if not isinstance(value["assets"],list) or not 1 <= len(value["assets"]) <= 20:
        raise ValueError("Invalid asset list")
    ids = set(); generated = 0
    for a in value["assets"]:
        if not isinstance(a,dict) or set(a) != {"id","name","route","description","heightMetres","sourceId","interactionKind"}:
            raise ValueError("Invalid asset fields")
        if not isinstance(a["id"],str) or not re.fullmatch(r"[a-z][a-z0-9-]{1,49}",a["id"]) or a["id"] in ids:
            raise ValueError("Invalid or duplicate asset ID")
        ids.add(a["id"])
        if a["route"] not in {"generated_mesh","reuse","procedural","material_variant"} or a["interactionKind"] not in {None,"Water","Chest"}:
            raise ValueError("Unsupported route or behaviour")
        if not number(a["heightMetres"],.05,5) or any(not isinstance(a[k],str) or not 1<=len(a[k])<=2000 for k in ("name","description")):
            raise ValueError("Invalid asset size or description")
        if a["route"] == "reuse" and a["sourceId"] not in (resources if resources is not None else KNOWN_RESOURCES):
            raise ValueError("Unknown reusable asset")
        if a["route"] != "reuse" and a["sourceId"] is not None:
            raise ValueError("Unexpected source asset")
        generated += a["route"] == "generated_mesh"
    if generated > limit:
        raise ValueError("Generated mesh count exceeds server allowance")
    return value


def validate_review(value, candidate, originals):
    if not isinstance(value, dict) or set(value) != {"action","issues","instruction","referenceImageIds"}:
        raise ValueError("Invalid review fields")
    action, ids = value["action"], value["referenceImageIds"]
    if action not in {"accept","edit","regenerate","ask_player"} or not isinstance(ids,list) or len(ids)>3 or any(not isinstance(x,str) for x in ids) or len(set(ids))!=len(ids):
        raise ValueError("Invalid review action/references")
    if not isinstance(value["issues"],list) or len(value["issues"])>12 or any(not isinstance(x,str) or not 1<=len(x)<=500 for x in value["issues"]) or not isinstance(value["instruction"],str) or len(value["instruction"])>2500:
        raise ValueError("Invalid review evidence")
    if action == "accept":
        if ids or value["issues"] or value["instruction"]: raise ValueError("Inconsistent accept")
    elif action == "edit":
        if not ids or ids[0]!=candidate or any(x not in originals for x in ids[1:]) or not value["issues"] or not value["instruction"]: raise ValueError("Invalid edit references")
    elif action == "regenerate":
        if any(x not in originals for x in ids) or (originals and not ids) or not value["issues"] or not value["instruction"]: raise ValueError("Invalid regenerate references")
    elif ids or not value["instruction"]: raise ValueError("Invalid question")
    return value


class CreationService:
    """Only the whitelist of agent/image/model tools runs here. Unity publication is a later stage."""
    def __init__(self, repo, tools, background=True, poll_interval=None):
        self.repo, self.tools = repo, tools
        self.poll_interval = poll_interval if poll_interval is not None else (12 if tools.mode == "live" else .03)
        self.stop = threading.Event(); self.pool = ThreadPoolExecutor(max_workers=2,thread_name_prefix="home-creation")
        self.scheduled = set(); self.scheduling_lock = threading.Lock(); self.background = background
        self.runtime_lock = threading.Lock()
        self.settings_path = repo.folder / "settings.json"
        self.workspace = Workspace(self, ROOT)
        self.game = GameService(self, ROOT)

    def settings(self):
        return {**copy.deepcopy(DEFAULTS), **json.loads(self.settings_path.read_text())} if self.settings_path.exists() else copy.deepcopy(DEFAULTS)

    def update_settings(self, value):
        if not isinstance(value,dict) or set(value)!=set(DEFAULTS): raise Problem("配置字段不完整。")
        validate_agent(value["agent"])
        for k in ("style","briefPrompt","planPrompt","reviewPrompt","assetSheetPrompt","layoutPrompt","visualReviewPrompt"):
            if not isinstance(value[k],str) or not 1<=len(value[k])<=8000: raise Problem("提示词长度不合法。")
        if not number(value["agentReserveCny"],.001,10) or not number(value["imageReserveUsd"],.001,10) or value["model"]!=DEFAULTS["model"]:
            raise Problem("费用预留或模型配置不合法。首版使用标准 G1、30 万面。")
        temp=self.settings_path.with_suffix(".tmp"); temp.write_text(canonical(value));temp.replace(self.settings_path)
        return value

    def create(self, body):
        idea=body.get("idea","")
        if not isinstance(idea,str) or not 1<=len(idea.strip())<=4000: raise Problem("请填写 1–4000 字的场景想法。")
        budgets=body.get("budgetCaps",{})
        if set(budgets)!=set(UNITS) or any(v is not None and not number(v,0,10000) for v in budgets.values()): raise Problem("请分别填写三种费用预算，未授权使用 null。")
        max_models=body.get("maxGeneratedAssets",1)
        if type(max_models)!=int or not 0<=max_models<=5: raise Problem("本轮生成模型数应为 0–5，0 表示只复用资源。")
        if self.tools.mode == "live" and not all(self.tools.configured().values()): raise Problem("真实服务的密钥尚未完整配置。",503,"NOT_CONFIGURED")
        grant=body.get("plotGrant")
        memory_source=self.workspace.memory_source(body.get('memorySourceId')) if body.get('memorySourceId') else None
        if body.get('memorySourceId') and not memory_source:raise Problem('选择的旧场景尚未定稿或已停用。',409)
        # Actual game-issued grants are not yet implemented. Service acceptance must
        # be explicitly labelled; never insert a fake plot into the player's game.
        if not isinstance(grant,dict) or grant.get("purpose") not in {"service-acceptance","game-issued"} or grant.get("profile")!="starter":
            raise Problem("第三步仅接受服务验收任务；真实土地授权在场景装配阶段接入。")
        if grant['purpose']=='game-issued':self.workspace.claim_grant(grant.get('token'))
        run_id=uuid4().hex
        run={"id":run_id,"schemaVersion":1,"mode":self.tools.mode,"purpose":"service-acceptance","idea":idea.strip(),"plotGrant":grant,
             "budgetCaps":budgets,"maxGeneratedAssets":max_models,"revision":1,"phase":"brief","status":"ready","paused":bool(body.get("startPaused",False)),"cancelled":False,
             "settingsSnapshot":self.settings(),"boundarySnapshot":json.loads((ROOT/"contracts/home-creation-boundaries.v1.json").read_text()),
             "createdAt":now(),"references":[],"tasks":[],"confirmations":[],"events":[],"stages":{s:"not_started" for s in STAGES},
             "memoryContext":self.workspace.memory_context(),
             "_requests":{},"artifactHistory":[]}
        with self.repo.lock:
            if grant['purpose']=='game-issued':self.workspace.claim_grant(grant.get('token'),run_id)
            if memory_source and memory_source.get('conceptRef'):
                source=memory_source['conceptRef']
                run['references']=[self.repo.put(run_id,self.repo.path(source,memory_source['runId']).read_bytes(),source['format'],'旧场景参考图',[source])]
                run['memorySourceId']=memory_source['id']
            self.repo.save(run)
        self.schedule(run_id);return self.view(run_id)

    def add_reference(self, rid, data, name="reference"):
        with self.repo.lock:
            r=self.repo.get(rid)
            if r["phase"]!="brief" or r["tasks"]: raise Problem("参考图需在开始规划前上传。",409)
            if len(r["references"])>=3 or len(data)>10*1024*1024: raise Problem("最多三张图片，每张不超过10MB。")
            try:
                with Image.open(io.BytesIO(data)) as im:
                    if im.format not in {"JPEG","PNG","WEBP"} or im.width*im.height>40_000_000: raise ValueError()
                    im.verify()
                with Image.open(io.BytesIO(data)) as im:
                    out=io.BytesIO();im.convert("RGB").save(out,"PNG")
            except Exception: raise Problem("参考图需为可读取的 JPG、PNG 或 WebP。") from None
            ref=self.repo.put(rid,out.getvalue(),"png",name)
            r["references"].append(ref);self.repo.save(r)
        return self.view(rid)

    def event(self,r,code,message):
        r["events"].append({"at":now(),"code":code,"message":message})

    def budget(self,r):
        amounts={u:0.0 for u in UNITS}; unknown=[]
        for t in r["tasks"]:
            actual={c["unit"]:c["amount"] for c in t.get("costs",[])}
            for cost in t["reserve"]:
                value=actual.get(cost["unit"],cost["amount"])
                if value is None:
                    if t["state"] not in {"rejected"}: unknown.append(t["id"])
                    value=cost["amount"]
                if value is not None and t["state"]!="rejected": amounts[cost["unit"]]+=value
        return {"usedOrReserved":amounts,"caps":r["budgetCaps"],"unknownTaskIds":list(dict.fromkeys(unknown))}

    def gate_hash(self,r,gate):
        if gate=="concept":
            refs=[r.get("briefRef"),r.get("conceptRef")]; extras=r.get("conceptReview")
        elif gate=="production_batch":
            refs=[r.get("briefRef"),r.get("conceptRef"),r.get("planRef")]+[d["sheetRef"] for d in r.get("designs",{}).values()]
            extras={"designs":r.get("designs"),"settings":r["settingsSnapshot"]["model"],"budget":r["budgetCaps"],"count":r["maxGeneratedAssets"]}
        elif gate=="final_layout":
            if not self.validation_current(r): raise Problem("当前布局需要重新运行验收。",409,"VALIDATION_REQUIRED")
            layout,validation=r["layout"],r["validation"]
            refs=[layout["layoutRef"],layout["layoutSceneRef"],validation["reportRef"],*layout["layoutPreviewRefs"],*validation["screenshotRefs"]]
            extras={"sceneSha256":layout["sceneSha256"],"assemblyVersion":layout["assemblyVersion"]}
        else: raise Problem("不支持的确认项目。",409)
        return fingerprint({"gate":gate,"revision":r["revision"],"refs":refs,"extras":extras,"boundary":r["boundarySnapshot"]})

    @staticmethod
    def validation_current(r):
        v,l=r.get("validation"),r.get("layout")
        return bool(r["phase"] in {"validation_ready","finalized"} and isinstance(v,dict) and l and v.get("bindingVersion")==1 and v.get("passed")
                    and v.get("layoutRefId")==l["layoutRef"]["id"] and v.get("revision")==r["revision"]
                    and v.get("sceneSha256")==l["sceneSha256"])

    @staticmethod
    def project_file(path):
        project=(ROOT/"unity"/"DawnGarden").resolve()
        if not isinstance(path,str) or not path.startswith(("Assets/","Packages/","ProjectSettings/")):
            raise Problem("验收引用了无效的工程路径。")
        resolved=(project/path).resolve()
        # Unity exposes registry packages under virtual Packages/ paths;
        # their physical files live in Library/PackageCache.
        if not resolved.is_file() and path.startswith("Packages/"):
            parts=Path(path).parts
            if len(parts)>2:
                cache=(project/"Library/PackageCache").resolve()
                matches=[(p/Path(*parts[2:])).resolve() for p in cache.glob(parts[1]+"@*") if p.is_dir()]
                matches=[p for p in matches if cache in p.parents and p.is_file()]
                if len(matches)==1: resolved=matches[0]
        if project not in resolved.parents or not resolved.is_file():
            raise Problem("验收引用的工程文件缺失。",409,"STALE_ASSET")
        return resolved

    def check_runtime_sources(self,report):
        if hashlib.sha256(self.project_file(report["scenePath"]).read_bytes()).hexdigest()!=report["sceneSha256"]:
            raise Problem("Unity 场景已变化，请重新装配并验收。",409,"STALE_ASSET")
        files=report.get("dependencyFiles")
        if not isinstance(files,list) or not files or len(files)>5000:
            raise Problem("验收报告缺少有效的工程依赖清单。")
        paths=set()
        for item in files:
            if not isinstance(item,dict) or not isinstance(item.get("path"),str) or item["path"] in paths:
                raise Problem("验收依赖清单格式不合法。")
            paths.add(item["path"])
            if hashlib.sha256(self.project_file(item["path"]).read_bytes()).hexdigest()!=item.get("sha256"):
                raise Problem("工程资产或脚本已变化，请重新运行验收。",409,"STALE_ASSET")
        if report["scenePath"] not in paths: raise Problem("依赖清单缺少当前场景。")

    @staticmethod
    def invalidate_final(r):
        for c in r["confirmations"]:
            if c["gate"]=="final_layout": c["stale"]=True
        if r.get("finalize") and isinstance(r["finalize"],dict):
            r.setdefault("finalizationHistory",[]).append(r["finalize"])
        r["finalize"]="not_started";r["stages"]["finalize"]="not_started";r["canPublish"]=False

    def confirmed(self,r,gate):
        return any(c["gate"]==gate and c["bindingHash"]==self.gate_hash(r,gate) and not c.get("stale") for c in r["confirmations"])

    def confirm(self,rid,body):
        with self.repo.lock:
            r=self.repo.get(rid);gate=body.get("gate");request_id=body.get("requestId")
            self.request_id(request_id)
            fp=fingerprint(body)
            if request_id in r["_requests"]:
                if r["_requests"][request_id]!=fp: raise Problem("请求标识冲突。",409,"REQUEST_CONFLICT")
                return self.view(rid)
            expected={"concept":"awaiting_concept","production_batch":"awaiting_batch","final_layout":"validation_ready"}
            if gate not in expected or r["phase"]!=expected[gate] or r["paused"] or r["cancelled"]: raise Problem("当前不能进行这项确认。",409)
            if body.get("bindingHash")!=self.gate_hash(r,gate): raise Problem("页面中的版本已过期，请重新查看。",409,"STALE_CONFIRMATION")
            if gate=="final_layout":
                if body.get("visualAccepted") is not True: raise Problem("请先查看场景并确认画面与布局。")
                l,v=r["layout"],r["validation"]
                report=json.loads(self.repo.path(v["reportRef"],rid).read_text())
                self.check_runtime_sources(report)
                for ref in [l["layoutRef"],l["layoutSceneRef"],*l["layoutPreviewRefs"],*v["screenshotRefs"]]: self.repo.path(ref,rid)
                c={"id":uuid4().hex,"gate":gate,"actor":"player","at":now(),"bindingHash":self.gate_hash(r,gate),"visualAccepted":True}
                snapshot={"schemaVersion":1,"releaseId":uuid4().hex,"runId":rid,"revision":r["revision"],"scope":"local-scene-acceptance",
                          "assemblyVersion":l["assemblyVersion"],"layoutRef":l["layoutRef"],"sceneRef":l["layoutSceneRef"],
                          "validationRef":v["reportRef"],"previewRefs":l["layoutPreviewRefs"],"screenshotRefs":v["screenshotRefs"],
                          "assetManifestRef":r.get("preparedAssetManifestRef") or r.get("assetManifestRef"),"playerApproval":c,"confirmedAt":c["at"]}
                ref=self.repo.put_json(rid,snapshot,"已确认场景快照",[l["layoutRef"],v["reportRef"]])
                r["confirmations"].append(c);r["_requests"][request_id]=fp
                r["finalize"]={"snapshotRef":ref,"releaseId":snapshot["releaseId"],"confirmedAt":c["at"],"bindingHash":c["bindingHash"],"scope":snapshot["scope"]}
                r["phase"]="finalized";r["status"]="completed";r["stages"]["finalize"]="complete";r["canPublish"]=False
                memory=self.workspace.remember_scene(r,snapshot,persist=False)
                self.event(r,"SCENE_CONFIRMED","你已确认当前 Unity 场景，布局、验收与预览已保存为定稿快照。")
                self.repo.save_with_memory(r,memory);return self.view(rid)
            refs=[r.get("conceptRef")] if gate=="concept" else [ref for d in r.get("designs",{}).values() for ref in [d["sheetRef"],*d["views"]]]
            for ref in refs:
                if ref:self.repo.path(ref,rid)
            if gate=="concept" and r.get("conceptReview",{}).get("action")!="accept": raise Problem("当前概念图尚未通过检查，请修改后再确认。",409)
            if gate=="production_batch" and any(d["review"]["action"]!="accept" for d in r["designs"].values()): raise Problem("有单件设计尚未通过检查。",409)
            unknown=body.get("unknownModelTasks",0)
            if type(unknown)!=int or not 0<=unknown<=r["maxGeneratedAssets"]: raise Problem("未知积分授权数量不合法。")
            c={"id":uuid4().hex,"gate":gate,"actor":"player","at":now(),"bindingHash":self.gate_hash(r,gate),"unknownModelTasks":unknown}
            r["confirmations"].append(c);r["_requests"][request_id]=fp;r["status"]="ready"
            if gate=="concept":r["stages"]["concept"]="complete";r["phase"]="plan"
            else:r["stages"]["asset_design"]="complete";r["phase"]="models"
            self.event(r,"PLAYER_CONFIRMED","已确认"+("概念图" if gate=="concept" else "制作批次"));self.repo.save(r)
        self.schedule(rid);return self.view(rid)

    @staticmethod
    def request_id(value):
        if not isinstance(value,str) or not 8<=len(value)<=100: raise Problem("缺少有效的请求标识。")

    def control(self,rid,action,body=None):
        body=body or {}
        with self.repo.lock:
            r=self.repo.get(rid)
            if action=="pause":r["paused"]=True;self.event(r,"PAUSED","已暂停新请求；已提交任务仍收取结果。")
            elif action=="resume":
                if r["cancelled"]:raise Problem("已取消的创作不能再提交新任务。",409)
                r["paused"]=False
                for t in r["tasks"]:
                    if t["state"] in {"download_failed","poll_failed"}:t["state"]="responded" if t.get("_response",{}).get("state")=="done" else "submitted"
                r["status"]={"awaiting_concept":"awaiting_player","awaiting_batch":"awaiting_player","validation_ready":"awaiting_player","assets_ready":"awaiting_next_stage","layout_ready":"awaiting_next_stage","validation_failed":"invalid_output","finalized":"completed","layout_changes_requested":"needs_layout_changes"}.get(r["phase"],"ready")
                self.event(r,"RESUMED","继续已有任务，已付费结果优先下载。")
            elif action=="cancel":r["cancelled"]=True;self.event(r,"CANCELLED","停止新提交；已提交任务可能已计费，仍保存结果。")
            elif action=="revise":
                text=body.get("instruction","")
                if not isinstance(text,str) or not 1<=len(text)<=2000:raise Problem("请输入修改意见。")
                if any(t["state"] in {"submitting","submitted","responded"} for t in r["tasks"]):raise Problem("请等待已提交任务收取结果后再修改。",409)
                if r["cancelled"]:raise Problem("任务已取消。",409)
                selected=r.get("conceptRef")
                r["revision"]+=1;r["idea"]+="\n本轮修改："+text;r["phase"]="brief";r["status"]="ready";r["paused"]=False
                if selected:r["references"]=[selected]
                for c in r["confirmations"]:c["stale"]=True
                if r.get("layout"): r.setdefault("layoutHistory",[]).append({"layout":r["layout"],"validation":r.get("validation"),"archivedAt":now()})
                self.invalidate_final(r)
                for k in ("brief","briefRef","conceptRef","conceptReview","conceptAttempt","conceptInputs","plan","planRef","designs","assetIndex","resourceSelection","layoutDraft","layoutDraftHistory","models","assetManifestRef","preparedAssets","preparedAssetManifestRef","layout","validation","finalize","runtimeAssets","runtimeInitialLayout","runtimePreparation","runtimePreview","runtimeFinalized","runtimeFinalToken"):
                    r.pop(k,None)
                r["stages"]={s:"not_started" for s in STAGES};self.event(r,"REVISED","已创建新方案版本，旧确认失效，历史资产保留。")
            elif action=="budget":
                caps=body.get("budgetCaps")
                if not isinstance(caps,dict) or set(caps)!=set(UNITS) or any(v is not None and not number(v,0,10000) for v in caps.values()):raise Problem("预算不合法。")
                r["budgetCaps"]=caps;r["status"]="ready";r["paused"]=False
                for c in r["confirmations"]:
                    if c["gate"]=="production_batch":c["stale"]=True
                if r["phase"]=="models":r["phase"]="awaiting_batch"
            elif action=="prepare_views":
                if r["phase"]!="awaiting_batch" or r["cancelled"]:
                    raise Problem("只能在制作批次确认前重新准备三视图。",409)
                for design in r.get("designs",{}).values():
                    design["views"]=self.split_views(r,design["sheetRef"])
                for c in r["confirmations"]:
                    if c["gate"]=="production_batch":c["stale"]=True
                self.event(r,"VIEWS_PREPARED","三视图已按统一比例重新准备，等待确认当前批次。")
            elif action=="request_layout_changes":
                text=body.get("instruction","")
                if r["phase"] not in {"validation_ready","validation_failed","finalized","layout_changes_requested"} or not isinstance(text,str) or not 1<=len(text.strip())<=2000:
                    raise Problem("请在运行验收后填写需要调整的场景内容。",409)
                r.setdefault("layoutFeedback",[]).append({"instruction":text.strip(),"at":now(),"layoutRefId":r["layout"]["layoutRef"]["id"],"actor":"player"})
                self.invalidate_final(r)
                r["phase"]="layout_changes_requested";r["status"]="needs_layout_changes"
                self.event(r,"LAYOUT_CHANGES_REQUESTED","已记录场景调整意见，重新装配后需再次运行验收。")
            else:raise Problem("不支持的操作。")
            self.repo.save(r)
        self.schedule(rid);return self.view(rid)

    def reconcile(self,rid,tid,task_id):
        with self.repo.lock:
            r=self.repo.get(rid);t=self.task(r,tid)
            if t["state"]!="submission_unknown" or t["kind"]=="agent":raise Problem("只能关联结果未知的图片或模型任务。",409)
            if not isinstance(task_id,str) or not re.fullmatch(r"[A-Za-z0-9_-]{1,100}",task_id):raise Problem("平台任务ID不合法。")
            t["providerTaskId"]=task_id;t["state"]="submitted";t["manualReconciliation"]={"at":now(),"actor":"local-admin"}
            r["status"]="ready";self.repo.save(r)
        self.schedule(rid);return self.view(rid)

    def register_layout(self, rid, body):
        """Persist the Unity assembly output without advancing validation/finalize."""
        request_id = body.get("requestId")
        self.request_id(request_id)
        revision = body.get("revision")
        scene_path = body.get("scenePath")
        scene_sha = body.get("sceneSha256")
        preview_paths = body.get("previewPaths", [])
        layout_path = body.get("layoutPath")
        if type(revision) is not int or not isinstance(scene_path, str) or not isinstance(scene_sha, str):
            raise Problem("缺少有效的 Unity 布局信息。")
        if not isinstance(preview_paths, list) or not preview_paths or any(not isinstance(p, str) for p in preview_paths):
            raise Problem("至少需要一张 Unity 场景预览图。")
        if layout_path is not None and not isinstance(layout_path, str):
            raise Problem("布局 JSON 路径不合法。")

        with self.repo.lock:
            r = self.repo.get(rid)
            fp = fingerprint(body)
            if request_id in r["_requests"]:
                if r["_requests"][request_id] != fp:
                    raise Problem("请求标识冲突。", 409, "REQUEST_CONFLICT")
                return self.view(rid)
            replacing_validated_layout = r["phase"] in {"layout_ready","validation_ready","validation_failed","finalized","layout_changes_requested"} and bool(r.get("layout"))
            if r["phase"] != "assets_ready" and not replacing_validated_layout or r["revision"] != revision:
                raise Problem("当前运行不能登记场景装配。", 409)

            project_root = (ROOT / "unity" / "DawnGarden").resolve()
            allowed_roots = [ROOT.resolve(), project_root]
            def local_file(value, label, base=None):
                raw = Path(value).expanduser()
                if not raw.is_absolute() and base is not None:
                    raw = base / raw
                p = raw.resolve()
                if not p.is_file() or not any(p == root or root in p.parents for root in allowed_roots):
                    raise Problem(label + "不存在或不在工作区内。", 400)
                return p

            scene_file = local_file(scene_path, "Unity 场景", project_root)
            if not scene_path.startswith("Assets/DawnGarden/PreparedHomes/"):
                raise Problem("场景必须保存到 PreparedHomes。")
            layout_file = local_file(layout_path, "布局 JSON") if layout_path else None
            try:
                layout = json.loads(layout_file.read_text()) if layout_file else body.get("layout")
            except (OSError, ValueError):
                raise Problem("布局 JSON 无法读取。") from None
            if not isinstance(layout, dict) or layout.get("runId") != rid or str(layout.get("revision")) != str(revision):
                raise Problem("布局 JSON 与当前运行不匹配。")
            if layout.get("sceneSha256") != scene_sha or layout.get("scenePath") != scene_path:
                raise Problem("布局 JSON 与场景哈希不匹配。")
            instances = layout.get("instances")
            if not isinstance(instances, list) or len(instances) > 50:
                raise Problem("布局实例列表不合法。")
            instance_ids = [x.get("instanceId") for x in instances if isinstance(x, dict)]
            if len(instance_ids) != len(instances) or len(set(instance_ids)) != len(instance_ids):
                raise Problem("布局实例 ID 必须唯一。")
            if not layout.get("checks", {}).get("westConnectionClear"):
                raise Problem("西侧连接通路未通过检查。")
            if not layout.get("checks", {}).get("insidePlot"):
                raise Problem("布局存在地块边界外实例。")

            allowed_prefabs = set()
            for asset in self.workspace.catalog.search():allowed_prefabs.add(asset['prefabPath'])
            for asset in (r.get("preparedAssets") or {}).values():
                unity = asset.get("unity") or {}
                if unity.get("prefabPath"):
                    allowed_prefabs.add(unity["prefabPath"])
            for instance in instances:
                source_ref = instance.get("sourceAssetRef")
                if 'assetVersion' in instance:
                    entry=self.workspace.catalog.get(instance.get('assetId'),instance['assetVersion'])
                    if instance.get('assetHash')!=entry['contentHash'] or source_ref!=entry['prefabPath']:raise Problem('布局资产的版本与来源不一致。',409)
                    self.workspace.catalog.check_dependencies(entry)
                if source_ref not in allowed_prefabs:
                    raise Problem("布局引用了未准备的资产。")

            scene_bytes = scene_file.read_bytes()
            actual_scene_sha = fingerprint(scene_bytes.hex())
            # Unity reports a raw SHA-256; avoid trusting the request body.
            import hashlib
            actual_scene_sha = hashlib.sha256(scene_bytes).hexdigest()
            if actual_scene_sha != scene_sha:
                raise Problem("Unity 场景哈希校验失败。", 409, "STALE_ASSET")
            scene_ref = self.repo.put(rid, scene_bytes, "unity", "Unity 场景布局", [r.get("assetManifestRef")] if r.get("assetManifestRef") else [])
            layout_ref = self.repo.put_json(rid, layout, "场景布局 JSON", [r.get("assetManifestRef")] if r.get("assetManifestRef") else [])
            previews = []
            for index, value in enumerate(preview_paths):
                preview_file = local_file(value, "场景预览图")
                data = preview_file.read_bytes()
                if len(data) > 20 * 1024 * 1024:
                    raise Problem("场景预览图过大。")
                previews.append(self.repo.put(rid, data, "png", "Unity 场景预览" + (" %d" % (index + 1) if len(preview_paths) > 1 else ""), [layout_ref]))
            if replacing_validated_layout:
                r.setdefault("layoutHistory", []).append({"layout": r.get("layout"), "validation": r.get("validation"), "archivedAt": now()})
                r["validationHistory"] = r.get("validationHistory", []) + ([r.get("validation")] if r.get("validation") else [])
                r["validation"] = "not_started"
            r["layout"] = {"layoutRef": layout_ref, "layoutSceneRef": scene_ref, "layoutPreviewRefs": previews,
                           "scenePath": scene_path, "sceneSha256": scene_sha, "instanceCount": len(instances),
                           "instances": instances, "checks": layout.get("checks", {}), "environment": layout.get("environment", {}),
                           "assemblyVersion": layout.get("assemblyVersion", "v1")}
            r["_requests"][request_id] = fp
            self.invalidate_final(r)
            r["stages"]["layout"] = "complete"
            r["phase"] = "layout_ready"
            r["status"] = "awaiting_next_stage"
            r["validation"] = "not_started"
            r["finalize"] = "not_started"
            r["canPublish"] = False
            self.event(r, "LAYOUT_READY", "Unity 场景已装配并保存为独立版本；等待场景与玩法验收。")
            self.repo.save(r)
        return self.view(rid)

    def start_validation(self,rid):
        """Run only the local Unity verification command; no paid tools involved."""
        with self.repo.lock:
            r=self.repo.get(rid)
            if r["phase"] not in {"layout_ready","validation_ready","validation_failed","finalized"} or r["paused"] or r["cancelled"]:
                raise Problem("当前不能启动运行验收。",409)
            if not self.runtime_lock.acquire(blocking=False): raise Problem("Unity 正在验收另一个任务，请稍后再试。",409)
            job_id=uuid4().hex
            r["runtimeJob"]={"id":job_id,"state":"running","startedAt":now(),"layoutRefId":r["layout"]["layoutRef"]["id"]}
            self.invalidate_final(r)
            r["phase"]="layout_ready";r["status"]="awaiting_next_stage";r["stages"]["validation"]="running"
            self.repo.save(r)
            try:self.pool.submit(self.run_validation,rid,job_id,r["revision"],r["layout"])
            except Exception:self.runtime_lock.release();raise
        return self.view(rid)

    def run_validation(self,rid,job_id,revision,layout):
        output=ROOT/"artifacts/home-creation/layout"/rid/layout["assemblyVersion"]
        report=output/"garden-runtime.json"
        try:
            code="DawnGarden.Editor.PreparedHomeBootstrap.VerifyFor("+json.dumps(layout["scenePath"])+","+json.dumps(str(output))+'); return "started";'
            result=subprocess.run([str(ROOT/"tools/unity"),"command","script","eval","--project-path",str(ROOT/"unity/DawnGarden"),"--timeout","60","--json","--","--code",code],cwd=ROOT,capture_output=True,text=True,timeout=90)
            if result.returncode or '"success": false' in result.stdout:
                raise ValueError("Unity 未启动验收，请检查编辑器编译状态，并退出已有 Play Mode 后重试。")
            deadline=time.monotonic()+260
            while not report.exists():
                if self.stop.wait(.5): raise ValueError("服务已停止，运行验收需要重新启动。")
                if time.monotonic()>deadline: raise ValueError("Unity 运行验收超时，请检查编辑器后重试。")
            with self.repo.lock:
                current=self.repo.get(rid)
                if current["revision"]!=revision or current.get("layout",{}).get("layoutRef",{}).get("id")!=layout["layoutRef"]["id"]:
                    raise ValueError("验收期间布局已更改，请为新布局重新验收。")
                # Report is written atomically by Unity before this registration.
                shots=[str(p) for p in sorted(output.glob("garden-*.png"))][:12]
                self.register_validation(rid,{"requestId":"runtime-"+job_id,"revision":revision,"reportPath":str(report),"screenshotPaths":shots})
                r=self.repo.get(rid);r["runtimeJob"].update(state="complete",finishedAt=now());self.repo.save(r)
        except Exception as exc:
            with self.repo.lock:
                r=self.repo.get(rid)
                if r.get("runtimeJob",{}).get("id")==job_id:
                    r["runtimeJob"].update(state="failed",finishedAt=now(),message=str(exc) if isinstance(exc,(Problem,ValueError)) else "Unity 验收未完成，请检查编辑器后重试。")
                    if r["phase"]=="layout_ready": r["stages"]["validation"]="failed"
                    self.repo.save(r)
        finally:self.runtime_lock.release()

    def register_validation(self, rid, body):
        """Register an isolated Unity Play Mode validation report for the layout."""
        request_id = body.get("requestId")
        self.request_id(request_id)
        revision = body.get("revision")
        report_path = body.get("reportPath")
        screenshot_paths = body.get("screenshotPaths", [])
        if type(revision) is not int or not isinstance(report_path, str):
            raise Problem("缺少有效的 Unity 验收报告信息。")
        if not isinstance(screenshot_paths, list) or any(not isinstance(p, str) for p in screenshot_paths):
            raise Problem("验收截图路径不合法。")
        if len(screenshot_paths) > 12:
            raise Problem("验收截图数量过多。")

        with self.repo.lock:
            r = self.repo.get(rid)
            fp = fingerprint(body)
            if request_id in r["_requests"]:
                if r["_requests"][request_id] != fp:
                    raise Problem("请求标识冲突。", 409, "REQUEST_CONFLICT")
                return self.view(rid)
            if r["phase"] not in {"layout_ready","validation_ready","validation_failed","finalized"} or r["revision"] != revision or r["cancelled"]:
                raise Problem("当前运行不能登记场景验收。", 409)
            layout = r.get("layout") or {}
            scene_path = layout.get("scenePath")
            scene_sha = layout.get("sceneSha256")
            if not scene_path or not scene_sha:
                raise Problem("缺少已登记的 Unity 场景。", 409)

            allowed_roots = [ROOT.resolve(), (ROOT / "unity" / "DawnGarden").resolve()]
            def local_file(value, label):
                p = Path(value).expanduser()
                p = p.resolve()
                if not p.is_file() or not any(p == root or root in p.parents for root in allowed_roots):
                    raise Problem(label + "不存在或不在工作区内。", 400)
                return p

            report_file = local_file(report_path, "验收报告")
            try:
                report = json.loads(report_file.read_text())
            except (OSError, ValueError):
                raise Problem("验收报告无法读取。") from None
            if not isinstance(report, dict) or type(report.get("passed")) is not bool:
                raise Problem("验收报告格式不合法。")
            if (report.get("bindingVersion")!=1 or report.get("runId")!=rid or str(report.get("revision"))!=str(revision)
                    or report.get("scenePath")!=scene_path or report.get("sceneSha256")!=scene_sha
                    or report.get("assemblyVersion")!=layout.get("assemblyVersion")):
                raise Problem("验收报告来自其他场景或旧版，请重新运行。",409,"STALE_REPORT")
            try: report_layout=json.loads(report.get("layoutJson",""))
            except (ValueError,TypeError): raise Problem("验收报告缺少布局来源。") from None
            if fingerprint(report_layout)!=layout["layoutRef"]["sha256"]:
                raise Problem("验收报告与当前布局不匹配。",409,"STALE_REPORT")
            checks = report.get("checks")
            if not isinstance(checks, list) or not checks:
                raise Problem("验收报告缺少检查项。")
            clean_checks = []
            names = set()
            for item in checks:
                if not isinstance(item, dict) or not isinstance(item.get("name"), str) or type(item.get("passed")) is not bool:
                    raise Problem("验收检查项格式不合法。")
                if item["name"] in names:
                    raise Problem("验收检查项名称重复。")
                names.add(item["name"])
                clean_checks.append({"name": item["name"], "passed": item["passed"], "detail": str(item.get("detail", ""))[:500]})
            if not REQUIRED_RUNTIME_CHECKS.issubset(names): raise Problem("报告缺少必要的运行检查，请重新验收。")
            if type(report.get("errors")) is not int or report["errors"]<0: raise Problem("运行错误数量不合法。")
            computed_passed = not report["errors"] and all(x["passed"] for x in clean_checks)
            if computed_passed != report["passed"]:
                raise Problem("验收报告的 passed 与检查项结果不一致。")

            self.check_runtime_sources(report)
            current_sha = scene_sha

            report_ref = self.repo.put_json(rid, {
                "runId": rid, "revision": revision, "scenePath": scene_path,
                "sceneSha256": current_sha, "passed": report["passed"],
                "errors": int(report.get("errors", 0) or 0), "checks": clean_checks,
                "utc": report.get("utc"), "unity": report.get("unity"),
                "isolatedSave": report.get("isolatedSave"),
                "bindingVersion":1,"assemblyVersion":layout["assemblyVersion"],"layoutRefId":layout["layoutRef"]["id"],
                "dependencyFiles":report["dependencyFiles"],"layoutJson":report["layoutJson"]
            }, "Unity 运行验收报告", [layout.get("layoutRef")] if layout.get("layoutRef") else [])
            screenshots = []
            for path in screenshot_paths:
                f = local_file(path, "验收截图")
                data = f.read_bytes()
                if len(data) > 20 * 1024 * 1024:
                    raise Problem("验收截图过大。")
                captions={"garden-overview":"初始家园","garden-polished-close":"场景细节","garden-watering":"浇水动作","garden-chest":"开箱动作","garden-complete":"照料完成","garden-expansion-offer":"扩建询问","garden-expansion-bag":"锦囊许可","garden-expansion-rising":"新大陆升起","garden-expansion-complete":"新大陆全貌","garden-expansion-walkable":"精灵走上新大陆"}
                screenshots.append(self.repo.put(rid, data, "png", captions.get(f.stem,"Unity 验收截图"), [report_ref]))

            if isinstance(r.get("validation"),dict): r.setdefault("validationHistory",[]).append(r["validation"])
            self.invalidate_final(r)
            r["validation"] = {"reportRef": report_ref, "screenshotRefs": screenshots,"bindingVersion":1,"revision":revision,"layoutRefId":layout["layoutRef"]["id"],
                               "passed": report["passed"], "errors": int(report.get("errors", 0) or 0),
                               "checks": clean_checks, "sceneSha256": current_sha}
            r["_requests"][request_id] = fp
            r["stages"]["validation"] = "complete" if report["passed"] else "failed"
            r["phase"] = "validation_ready" if report["passed"] else "validation_failed"
            r["status"] = "awaiting_player" if report["passed"] else "invalid_output"
            r["stages"]["finalize"] = "awaiting_player" if report["passed"] else "not_started"
            r["finalize"] = "not_started"
            r["canPublish"] = False
            self.event(r, "VALIDATION_READY" if report["passed"] else "VALIDATION_FAILED",
                       "Unity 场景运行验收通过，等待最终确认。" if report["passed"] else "Unity 场景运行验收未通过，请查看失败检查项。")
            self.repo.save(r)
        return self.view(rid)

    def task(self,r,tid):
        try:return next(t for t in r["tasks"] if t["id"]==tid)
        except StopIteration:raise Problem("任务不存在。",404)

    def new_task(self,r,key,kind,payload,refs,reserve):
        # Caller holds repository lock; reservation and intent persist together.
        used=self.budget(r)["usedOrReserved"]
        if any(r["budgetCaps"][u] is not None and used[u]>r["budgetCaps"][u]+1e-9 for u in UNITS):
            r["status"]="budget_blocked";self.event(r,"BUDGET_BLOCKED","已回报费用超过预算，停止后续提交。");return None
        for c in reserve:
            if c["amount"] is None:continue
            cap=r["budgetCaps"][c["unit"]]
            if cap is None or used[c["unit"]]+c["amount"]>cap+1e-9:
                r["status"]="budget_blocked";self.event(r,"BUDGET_BLOCKED","预算不足，未提交新的付费请求。");return None
        if kind=="model":
            count=sum(t["kind"]=="model" and t["state"]!="rejected" for t in r["tasks"])
            auth=next((c for c in reversed(r["confirmations"]) if c["gate"]=="production_batch" and not c.get("stale") and c["bindingHash"]==self.gate_hash(r,"production_batch")),None)
            if count>=r["maxGeneratedAssets"] or not auth or auth["unknownModelTasks"]<=count:
                r["status"]="authorization_required";self.event(r,"MODEL_AUTH_REQUIRED","需确认本批次模型数量及未知积分。未提交模型。");return None
        for ref in refs:self.repo.path(ref,r["id"])
        model=r["settingsSnapshot"]["agent"]["model"] if kind=="agent" else "gpt-image-2" if kind=="image" else r["settingsSnapshot"]["model"]["model"]
        task={"id":uuid4().hex,"key":key,"kind":kind,"model":model,"state":"planned","revision":r["revision"],"requestHash":fingerprint({"payload":payload,"refs":refs}),
              "inputRefs":refs,"_payload":payload,"reserve":reserve,"createdAt":now(),"costs":[]}
        r["tasks"].append(task);r["status"]="running";return task

    def ensure(self,r,key,kind,payload,refs):
        full_key=str(r["revision"])+":"+key
        existing=next((t for t in r["tasks"] if t["key"]==full_key),None)
        if existing:return existing
        reserve=[{"unit":"CNY","amount":r["settingsSnapshot"]["agentReserveCny"]}] if kind=="agent" else [{"unit":"USD","amount":r["settingsSnapshot"]["imageReserveUsd"]}] if kind=="image" else [{"unit":"AHOLO_CREDITS","amount":None}]
        return self.new_task(r,full_key,kind,payload,refs,reserve)

    def agent(self,r,key,purpose,context,refs):
        config=r["settingsSnapshot"]
        payload={"purpose":purpose,"context":context,"inputRefs":refs,"agentConfig":config["agent"],
                 "systemPrompt":config[{"brief":"briefPrompt","plan":"planPrompt","review":"reviewPrompt"}[purpose]]}
        return self.ensure(r,key,"agent",payload,refs)

    def json_result(self,t):
        response=t["_response"]
        if response.get("finishReason") not in {None,"stop"}:raise ValueError("Incomplete agent response")
        return json.loads(response["content"])

    def set_artifact(self,r,key,value,label,parents=None):
        ref=self.repo.put_json(r["id"],value,label,parents);r[key]=ref;r["artifactHistory"].append(ref);return ref

    def split_views(self,r,sheet):
        with Image.open(self.repo.path(sheet,r["id"])) as source:
            im=source.convert("RGB");w,h=im.size
            minimum=ImageChops.darker(ImageChops.darker(*im.split()[:2]),im.split()[2])
            mask=minimum.point(lambda x:255 if x<235 else 0)
            scan=mask.resize((w,1),Image.Resampling.BOX)
            projection=list(scan.get_flattened_data() if hasattr(scan,"get_flattened_data") else scan.getdata());gaps=[];start=None
            for x,v in enumerate(projection+[255]):
                if v==0 and start is None:start=x
                if v!=0 and start is not None:
                    if x-start>=max(8,w//150):gaps.append((start,x))
                    start=None
            seams=[]
            for fraction in (1/3,2/3):
                valid=[g for g in gaps if abs(sum(g)/2-w*fraction)<w*.14]
                if not valid:raise ValueError("三视图间没有安全白边，不能自动切割。")
                seams.append(round(sum(min(valid,key=lambda g:abs(sum(g)/2-w*fraction)))/2))
            refs=[];bounds=[0,*seams,w];subjects=[]
            for n,label in enumerate(("front","right","back")):
                panel=im.crop((bounds[n],0,bounds[n+1],h));box=mask.crop((bounds[n],0,bounds[n+1],h)).getbbox()
                if not box:raise ValueError("三视图有空视角。")
                subjects.append(panel.crop(box))
            # Preserve the sheet's common pixel scale. Independently enlarging
            # the narrow side view would imply a different size to the 3D model.
            side=round(max(max(subject.size) for subject in subjects)*1.18)
            for label,subject in zip(("front","right","back"),subjects):
                canvas=Image.new("RGB",(side,side),"white")
                canvas.paste(subject,((side-subject.width)//2,(side-subject.height)//2));canvas=ImageOps.contain(canvas,(1536,1536))
                out=io.BytesIO();canvas.save(out,"PNG");refs.append(self.repo.put(r["id"],out.getvalue(),"png",label,[sheet]))
            return refs

    def advance(self,r):
        phase=r["phase"];cfg=r["settingsSnapshot"]
        if phase=="brief":
            r["stages"]["brief"]="running"
            t=self.agent(r,"brief","brief",{"idea":r["idea"],"constraints":r["boundarySnapshot"],"maxGeneratedAssets":r["maxGeneratedAssets"],"memory":r.get("memoryContext",{})},r["references"])
            if not t or t["state"]!="verified":return
            brief=validate_brief(self.json_result(t));r["brief"]=brief
            self.set_artifact(r,"briefRef",brief,"场景需求")
            if brief["clarifications"]:r["status"]="needs_input";r["questions"]=brief["clarifications"];return
            r["stages"]["brief"]="complete";r["phase"]="concept_image";r["conceptAttempt"]=0;r["conceptInputs"]=r["references"][:3]
        elif phase=="concept_image" or phase=="asset_image":
            concept=phase=="concept_image";stage="concept" if concept else "asset_design";r["stages"][stage]="running"
            attempt=r["conceptAttempt"] if concept else r.get("assetAttempt",0)
            asset=None if concept else r["generatedAssets"][r["assetIndex"]]
            text=r["brief"]["conceptPrompt"] if concept else prompts.sheet_prompt(asset,cfg.get("assetSheetPrompt",prompts.ASSET_SHEET))
            correction=r.get("nextConceptCorrection" if concept else "nextAssetCorrection","")
            text=cfg["style"]+"\n"+text+("\n"+cfg["agent"]["repairInstruction"]+"\n修改意见："+correction if correction else "")
            refs=r["conceptInputs"] if concept else r.get("assetInputs",[r["conceptRef"]])
            key=("concept" if concept else "sheet:"+asset["id"])+":"+str(attempt)
            t=self.ensure(r,key,"image",{"prompt":text,"size":"4:3" if concept else "2:1","resolution":"2K" if concept else "4K"},refs)
            if not t or t["state"]!="verified":return
            r["candidateRef"]=t["fileRef"];r["candidateTaskId"]=t["id"];r["phase"]="concept_review" if concept else "asset_review"
        elif phase in {"concept_review","asset_review"}:
            concept=phase=="concept_review";candidate=r["candidateRef"]
            original=r["references"][:3] if concept else [r["conceptRef"]]
            refs=list({x["id"]:x for x in original+[candidate]}.values())
            context={"stage":"concept" if concept else "asset_design","brief":r["brief"],"candidateId":candidate["id"],"originalIds":[x["id"] for x in original],"style":cfg["style"]}
            if not concept:context["asset"]=r["generatedAssets"][r["assetIndex"]]
            t=self.agent(r,"review:"+candidate["id"],"review",context,refs)
            if not t or t["state"]!="verified":return
            decision=validate_review(self.json_result(t),candidate["id"],[x["id"] for x in original])
            r["artifactHistory"].append(candidate)
            if decision["action"]!="accept":
                counter="conceptAttempt" if concept else "assetAttempt";attempt=r.get(counter,0)
                if decision["action"]=="ask_player" or attempt>=cfg["agent"]["maxCorrections"]:
                    r["status"]="needs_input";r["questions"]=decision["issues"]+[decision["instruction"]];r["reviewFailedCandidate"]=candidate;return
                allowed={x["id"]:x for x in refs};selected=[allowed[x] for x in decision["referenceImageIds"]]
                r[counter]=attempt+1;r["conceptInputs" if concept else "assetInputs"]=selected
                r["nextConceptCorrection" if concept else "nextAssetCorrection"]=decision["instruction"];r["phase"]="concept_image" if concept else "asset_image";return
            if concept:
                r["conceptRef"]=candidate;r["conceptReview"]=decision;r["phase"]="awaiting_concept";r["status"]="awaiting_player"
                r["stages"]["concept"]="awaiting_player";self.event(r,"CONCEPT_READY","概念图已通过内部检查，请确认或继续修改。")
            else:
                asset=r["generatedAssets"][r["assetIndex"]]
                views=self.split_views(r,candidate)
                r.setdefault("designs",{})[asset["id"]]={"sheetRef":candidate,"views":views,"review":decision,"asset":asset}
                r["assetIndex"]+=1;r["assetAttempt"]=0;r.pop("assetInputs",None);r.pop("nextAssetCorrection",None)
                if r["assetIndex"]<len(r["generatedAssets"]):r["phase"]="asset_image"
                else:r["phase"]="awaiting_batch";r["status"]="awaiting_player";r["stages"]["asset_design"]="awaiting_player";self.event(r,"BATCH_READY","单件三视图已准备，请确认制作批次、参数与费用。")
        elif phase=="plan":
            if not self.confirmed(r,"concept"):raise ValueError("Concept authorization stale")
            r["stages"]["breakdown"]="running"
            if 'resourceSelection' not in r:
                r['resourceSelection']=self.workspace.catalog.context() or [{'id':key} for key in KNOWN_RESOURCES]
            t=self.agent(r,"plan:"+r["conceptRef"]["id"],"plan",{"brief":r["brief"],"maxGeneratedAssets":r["maxGeneratedAssets"],"knownResources":r['resourceSelection'],"memory":r.get('memoryContext',{})},[r["conceptRef"]])
            if not t or t["state"]!="verified":return
            plan=validate_plan(self.json_result(t),r["maxGeneratedAssets"],{a['id'] for a in r['resourceSelection']});r["plan"]=plan
            self.set_artifact(r,"planRef",plan,"资产拆解方案",[r["conceptRef"]]);r["stages"]["breakdown"]="complete"
            r["generatedAssets"]=[a for a in plan["assets"] if a["route"]=="generated_mesh"];r["assetIndex"]=0;r["designs"]={}
            if r["generatedAssets"]:r["phase"]="asset_image"
            else:r["phase"]="awaiting_batch";r["status"]="awaiting_player"
        elif phase=="models":
            if not self.confirmed(r,"concept") or not self.confirmed(r,"production_batch"):raise ValueError("Production authorization stale")
            r["stages"]["assets"]="running";models=r.setdefault("models",{})
            for identity,design in r["designs"].items():
                if identity in models:continue
                t=self.ensure(r,"model:"+identity+":"+design["sheetRef"]["sha256"],"model",{"modelSettings":cfg["model"],"sourceDesignRef":design["sheetRef"]},design["views"])
                if not t or t["state"]!="verified":return
                models[identity]={"sourceDesignRef":design["sheetRef"],"inputViews":design["views"],"modelRef":t["fileRef"],"taskId":t["id"],"checks":t.get("containerCheck")}
                return
            manifest={"mode":r["mode"],"models":models,"planRef":r["planRef"],"preparationStatus":"raw-downloads-verified",
                      "remaining":["asset-library normalization/LOD/collision/socket metadata","Unity assembly","runtime and visual validation","final player confirmation"],"canPublish":False}
            self.set_artifact(r,"assetManifestRef",manifest,"待接入资产库的模型清单",[r["conceptRef"],r["planRef"]])
            r["stages"]["assets"]="awaiting_preparation";r["phase"]="assets_ready";r["status"]="awaiting_next_stage"
            self.event(r,"ASSETS_DOWNLOADED","模型已下载并校验容器，等待资产库准备和 Unity 装配；尚未发布到游戏。")

    def update_task(self,rid,tid,**values):
        def change(r):self.task(r,tid).update(values)
        self.repo.update(rid,change)

    def execute_task(self,rid,tid):
        # Persist submitting before calling outside the lock. Never save a stale
        # run snapshot after the call; pause/cancel/budget changes must survive.
        with self.repo.lock:
            r=self.repo.get(rid);t=self.task(r,tid)
            if t["state"]=="planned":
                if r["paused"] or r["cancelled"]:return
                used=self.budget(r)["usedOrReserved"]
                if any(r["budgetCaps"][u] is not None and used[u]>r["budgetCaps"][u]+1e-9 for u in UNITS):
                    r["status"]="budget_blocked";self.repo.save(r);return
                if t["kind"]=="model" and not self.confirmed(r,"production_batch"):
                    r["status"]="authorization_required";self.repo.save(r);return
                t["state"]="submitting";self.repo.save(r);submit=True
            else:submit=False
        try:
            if submit:
                paths=[self.repo.path(ref,rid) for ref in t["inputRefs"]]
                response=self.tools.submit(t["kind"],t["_payload"],paths)
                self.update_task(rid,tid,_response=response,providerTaskId=response.get("taskId"),state="responded" if response["state"]=="done" else "submitted",costs=response.get("costs",[]))
                return
            if t["state"]=="submitted":
                response=self.tools.poll(t["kind"],t["providerTaskId"])
                if response["state"]=="failed":
                    self.update_task(rid,tid,state="provider_failed",errorCode="PROVIDER_FAILED");return
                self.update_task(rid,tid,_response=response,state="responded" if response["state"]=="done" else "submitted",costs=response.get("costs",t["costs"]))
                return
            if t["state"]=="responded":
                if t["kind"]=="agent":self.update_task(rid,tid,state="verified");return
                raw,suffix=self.tools.download(t["kind"],t["_response"])
                ref=self.repo.put(rid,raw,suffix,"设计图" if t["kind"]=="image" else "标准 G1 模型",t["inputRefs"])
                check=inspect_glb(self.repo.path(ref,rid)) if suffix=="glb" else None
                self.update_task(rid,tid,fileRef=ref,containerCheck=check,state="verified");return
        except Exception as exc:
            definite=isinstance(exc,ProviderRejected) and exc.status<500 and exc.status!=408
            state=("rejected" if definite else "submission_unknown") if submit else ("poll_failed" if t["state"]=="submitted" else "download_failed")
            self.update_task(rid,tid,state=state,errorCode=type(exc).__name__)

    def tick(self,rid):
        r=self.repo.get(rid)
        active=next((t for t in r["tasks"] if t["state"] in {"planned","submitted","responded"}),None)
        if active:
            self.execute_task(rid,active["id"]);return
        if r["paused"] or r["cancelled"]:return
        blocked=[t for t in r["tasks"] if t["revision"]==r["revision"] and t["state"] in {"submission_unknown","rejected","provider_failed","poll_failed","download_failed"}]
        if blocked:
            self.repo.update(rid,lambda v:v.update(status="task_blocked"));return
        if r["status"] in {"needs_input","awaiting_player","awaiting_next_stage","budget_blocked","authorization_required","invalid_output","completed","needs_layout_changes"}:return
        with self.repo.lock:
            r=self.repo.get(rid)
            if r["paused"] or r["cancelled"]:return
            try:self.advance(r)
            except (ValueError,KeyError,TypeError) as exc:
                r["status"]="invalid_output";self.event(r,"INVALID_OUTPUT",str(exc)[:180])
            self.repo.save(r)

    def schedule(self,rid):
        if not self.background:return
        with self.scheduling_lock:
            if rid in self.scheduled:return
            self.scheduled.add(rid)
        self.pool.submit(self.worker,rid)

    def worker(self,rid):
        try:
            while not self.stop.is_set():
                try:self.tick(rid)
                except Exception:
                    self.repo.update(rid,lambda r:r.update(status="internal_error"));break
                r=self.repo.get(rid)
                active=any(t["state"] in {"submitted","responded"} for t in r["tasks"])
                if not active and (r["paused"] or r["cancelled"] or r["status"] in {"needs_input","awaiting_player","awaiting_next_stage","budget_blocked","authorization_required","invalid_output","task_blocked","completed","needs_layout_changes"}):break
                self.stop.wait(self.poll_interval if active else .03)
        finally:
            with self.scheduling_lock:self.scheduled.discard(rid)
            # Close the race with a resume/confirmation arriving just before exit.
            if not self.stop.is_set():
                r=self.repo.get(rid)
                if not r["paused"] and not r["cancelled"] and r["status"]=="ready":self.schedule(rid)

    def recover(self):
        for r in self.repo.all():
            changed=False
            if r['phase']=='validation_ready' and not r['paused'] and not r['cancelled'] and r['status']=='awaiting_next_stage':
                r['status']='awaiting_player';changed=True
            if r.get("runtimeJob",{}).get("state")=="running":
                r["runtimeJob"].update(state="failed",message="服务重启中断了验收登记，请重新运行。",finishedAt=now());changed=True
            for key in ('assemblyJob','preparationJob','layoutProposal','runtimePreparation'):
                if r.get(key,{}).get('state')=='running':
                    r[key].update(state='failed',message='服务重启中断了此工具执行，已保留原结果，请重新操作。',finishedAt=now());changed=True
            for t in r["tasks"]:
                if t["state"]=="submitting":t["state"]="submission_unknown";t["errorCode"]="PROCESS_INTERRUPTED";changed=True
            if changed:self.repo.save(r)
            self.schedule(r["id"])

    def view(self,rid,admin=False):
        r=self.repo.get(rid)
        result=public(r);result["budget"]=self.budget(r)
        result["gateBindings"]={g:self.gate_hash(r,g) for g in ("concept","production_batch")}
        result["validationCurrent"]=self.validation_current(r)
        if result["validationCurrent"]: result["gateBindings"]["final_layout"]=self.gate_hash(r,"final_layout")
        if not admin:
            result.pop("settingsSnapshot",None)
            for t in result["tasks"]:t.pop("requestHash",None)
        return result

    def shutdown(self):
        self.game.shutdown()
        self.stop.set();self.pool.shutdown(wait=True,cancel_futures=True)
