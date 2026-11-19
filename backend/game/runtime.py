"""Game-runtime asset preparation and player-approved, versioned home snapshots."""
import base64
import copy
import io
import json
from pathlib import Path
import subprocess
from uuid import uuid4

from PIL import Image
from backend.home_creation import prompts
from backend.home_creation.catalog import validate_instances
from backend.home_creation.storage import canonical, fingerprint
from backend.store import Problem, now
from backend.game.garden_recipes import resolve_plan,checked_initial_layout,surface_style
from backend.game.visual_review import binding_for,policy_for


class RuntimeCreation:
    def __init__(self,game):self.game=game;self.creation=game.creation;self.repo=game.repo;self.root=game.root;self.catalog=game.catalog

    def prepare(self,client,rid):
        with self.repo.lock:
            self.game.require_home_land(client)
            r=self.game.own_run(client,rid)
            if r.get('runtimePreparation',{}).get('state')=='running':return self.game.creation_view(client,rid)
            if r['phase'] not in {'assets_ready','runtime_editing'}:raise Problem('请先完成制作批次和模型下载。',409)
            if r.get('paused') or r.get('cancelled'):raise Problem('创作已暂停或取消。',409)
            r['runtimePreparation']={'state':'running','startedAt':now()};self.repo.save(r)
            self.game.pool.submit(self.run_prepare,client,rid,r['revision'])
        return self.game.creation_view(client,rid)

    def run_prepare(self,client,rid,revision):
        try:
            r=self.repo.get(rid)
            for aid,model in r.get('models',{}).items():
                if aid in r.get('runtimeAssets',{}):continue
                source=self.repo.path(model['modelRef'],rid);design=r['designs'][aid];height=design['asset']['heightMetres']
                output=self.root/'artifacts/game-v1/generated-assets'/rid/str(revision)/aid;output.mkdir(parents=True,exist_ok=True)
                result=output/'preparation.json'
                with (output/'prepare.log').open('w') as log:
                    process=subprocess.run([str(self.root/'tools/blender'),'--background','--python',str(self.root/'scripts/prepare_home_asset_blender.py'),'--','--source',str(source),'--out',str(output),'--height',str(height),'--result',str(result)],cwd=self.root,stdout=log,stderr=subprocess.STDOUT,timeout=240)
                if process.returncode or not result.is_file():raise Problem('本地模型准备未完成，原生成结果已保留，可以重试。',409)
                prep=json.loads(result.read_text())
                if not prep.get('success'):raise Problem('模型归一与 LOD 检查未通过。',409)
                with self.repo.lock:
                    r=self.repo.get(rid)
                    if r['revision']!=revision:raise Problem('方案已变化，旧模型准备结果未覆盖新方案。',409)
                    lods=[{'level':i,'triangles':a['triangles'],'modelRef':self.repo.put(rid,(output/a['file']).read_bytes(),'glb',design['asset']['name']+' LOD'+str(i),[model['modelRef']])} for i,a in enumerate(prep['lods'])]
                    preview=self.repo.put(rid,(output/'preview.png').read_bytes(),'png',design['asset']['name']+'预览',[model['sourceDesignRef']])
                    catalog_id=aid+'-'+rid[:8]
                    data={'id':catalog_id,'name':design['asset']['name'],'version':1,'prefabPath':'runtime://'+catalog_id,
                        'size':dict(zip(('x','y','z'),prep['normalization']['dimensionsMetres'])),'center':{'x':0,'y':height/2,'z':0},'hasCollider':False,'hasLod':True,'placeable':True,
                        'tags':['自制',design['asset']['name']],'materials':['生成模型原材质'],'dependencies':[],'interactionKind':None,'pivot':'bottom-centred',
                        'runtimeModelRef':lods[0]['modelRef'],'runtimeCollider':prep['collider'],'previewRef':preview,
                        'source':{'kind':'runtime-glb','runId':rid,'modelRef':lods[0]['modelRef'],'sourceModelRef':model['modelRef'],'designRef':model['sourceDesignRef'],'lods':lods}}
                    digest=fingerprint({k:v for k,v in data.items() if k not in {'version','previewRef'}})
                    old=self.catalog.get(catalog_id,required=False)
                    if old and old['contentHash']==digest:data=old
                    else:
                        data.update(contentHash=digest,version=old['version']+1 if old else 1,indexedAt=now())
                        with self.repo.db:self.repo.db.execute('INSERT INTO catalog VALUES (?,?,?)',(catalog_id,data['version'],canonical(data)))
                    r.setdefault('runtimeAssets',{})[aid]={'catalogId':data['id'],'version':data['version'],'hash':data['contentHash'],'modelRef':lods[0]['modelRef']};self.repo.save(r)
            r=self.repo.get(rid);allowed,coverage,style=resolve_plan(r.get('plan',{}),self.catalog)
            for a in r.get('plan',{}).get('assets',[]):
                if a['route']!='generated_mesh':continue
                identity=r.get('runtimeAssets',{}).get(a['id'],{}).get('catalogId')
                item=self.catalog.get(identity,required=False) if identity else None
                if item and item['placeable']:allowed.append(item);coverage.append({'planAssetId':a['id'],'recipe':identity,'status':'resolved'})
                else:raise Problem('新增模型尚未进入资产库，不能略过后继续装配。',409)
            # Ask the existing metered planning tool to place only selected production assets.
            initial=[];repairs=[]
            if allowed:
                selected=[{k:a[k] for k in ('id','version','contentHash','name','size','center')} for a in allowed]
                context={'brief':r.get('brief'),'catalog':selected,'currentLayout':[],'productionCoverage':coverage,'surfaceStyle':style,'instruction':'按已确认概念图安排完整场景。每种已规划资产至少使用一次；灌木、地被、花丛是可以重复放置的组合，形成疏密层次和围合，不是每种只放一件。保留西侧入口和中央通路，所有物件使用同一地块局部坐标；按尺寸检查旋转包围盒，宁可适度缩小，不要漏项。'}
                cfg=r['settingsSnapshot'];payload={'purpose':'layout','agentConfig':cfg['agent'],'systemPrompt':cfg.get('layoutPrompt',prompts.LAYOUT),'context':context,'inputRefs':[r['conceptRef']] if r.get('conceptRef') else []}
                key='runtime-layout:'+str(revision)
                with self.repo.lock:
                    latest=self.repo.get(rid);task=next((t for t in latest['tasks'] if t['key']==key),None)
                    if task is None:task=self.creation.new_task(latest,key,'agent',payload,payload['inputRefs'],[{'unit':'CNY','amount':cfg['agentReserveCny']}]);self.repo.save(latest)
                if task:
                    for _ in range(3):
                        current=self.creation.task(self.repo.get(rid),task['id'])
                        if current['state']=='verified':break
                        if current['state'] not in {'planned','responded'}:break
                        self.creation.execute_task(rid,task['id'])
                    current=self.creation.task(self.repo.get(rid),task['id'])
                    if current['state']=='verified':
                        initial,repairs=checked_initial_layout(self.creation.json_result(current).get('instances'),self.catalog,allowed)
                if not initial:raise Problem('自动布局未完成，资产已保留，可重试或手动布置；不会用固定位置代替概念图。',409)
            with self.repo.lock:
                r=self.repo.get(rid)
                if r['revision']!=revision:raise Problem('方案版本已变化。',409)
                r['runtimeInitialLayout']=self.game.native_items(initial);r['runtimeSurfaceStyle']=style;r['productionCoverage']=coverage;r['layoutRepairs']=repairs;r['visualReviewRequired']=bool(r.get('conceptRef'));r['visualStatus']='needs_review';r['runtimePreparation'].update(state='complete',finishedAt=now(),visualStatus='needs_review');r['phase']='runtime_editing';r['status']='awaiting_player';r['stages']['assets']='complete';self.repo.save(r)
        except Exception as exc:
            with self.repo.lock:
                r=self.repo.get(rid);r['runtimePreparation'].update(state='failed',message=str(exc) if isinstance(exc,Problem) else '资产准备遇到问题，已保留结果，请重试或先用已有物件布置。');self.repo.save(r)

    def ensure_project(self,client):
        with self.repo.lock:return self._ensure_project(client)

    def _ensure_project(self,client):
        self.game.require_home_land(client)
        p=self.game.get_profile(client)
        if p.get('creationRunId'):
            previous=self.repo.get(p['creationRunId'])
            if not previous.get('cancelled'):return previous
            self.game.new_edition(p)
        grant=self.game.grant(client);rid=uuid4().hex
        r={'id':rid,'schemaVersion':1,'mode':self.creation.tools.mode,'purpose':'game-runtime','gameProfileId':client,'projectKind':'runtime-only',
            'idea':'使用物件库共同布置新土地','plotGrant':{'purpose':'game-issued','profile':'starter','token':grant['token']},'revision':1,'phase':'runtime_editing','status':'awaiting_player','paused':False,'cancelled':False,
            'settingsSnapshot':self.creation.settings(),'boundarySnapshot':json.loads((self.root/'contracts/home-creation-boundaries.v1.json').read_text()),'budgetCaps':{'USD':0,'CNY':2,'AHOLO_CREDITS':None},'maxGeneratedAssets':0,
            'createdAt':now(),'references':[],'tasks':[],'confirmations':[],'events':[],'artifactHistory':[],'_requests':{},'stages':{k:'not_started' for k in ('brief','concept','breakdown','asset_design','assets','layout','validation','finalize')},'memoryContext':self.creation.workspace.memory_context()}
        r['brief']={'theme':('晨曦花园' if p['theme']=='dawn' else '暖焰营地')+' · 新土地'}
        r['stages'].update(brief='complete',concept='not_required',breakdown='complete',asset_design='not_required',assets='complete')
        with self.repo.lock:
            self.repo.save(r);self.creation.workspace.claim_grant(grant['token'],rid)
            p=self.game.get_profile(client);p['creationRunId']=rid;self.game.save_profile(p)
        return r

    def preview(self,body):
        client=body.get('clientId');p=self.game.get_profile(client);state=p['states'].get(p.get('lastPlot'),{})
        self.game.require_home_land(client)
        r=self.ensure_project(client)
        if body.get('runId') and body['runId']!=r['id']:raise Problem('当前创作已变化，请重新打开家园预览。',409)
        if r['paused'] or r['cancelled'] or any(t['state'] in {'planned','submitting','submitted','responded'} for t in r['tasks']):raise Problem('请先等待已提交的制作任务结束。',409)
        raw=body.get('instances')
        if not isinstance(raw,list) or len(raw)>50:raise Problem('布局实例列表无效。')
        items=validate_instances(raw,self.catalog) if raw else []
        for source,item in zip(raw,items):
            if source.get('giftId'):
                self.game.neighbors.require_gift(client,source['giftId'],item['assetId']);item['giftId']=source['giftId']
        old=self.game.layout(client)
        if body.get('expectedVersion')!=old['version']:raise Problem('保存的家园已经变化，请重新读取。',409)
        for i in items:self.catalog.check_dependencies(self.catalog.get(i['assetId'],i['assetVersion']))
        checks=body.get('checks');required={'groundConnected','westRoute','boundsValid','materialsValid','collisionsValid','actorGrounded'}
        if not isinstance(checks,dict) or any(checks.get(k) is not True for k in required):raise Problem('当前预览未通过地面、通路、材质或碰撞检查。',409)
        build_id=body.get('buildId');manifest=self.root/'artifacts/game-v1/build-manifest.json'
        if type(body.get('seamSoftened',False))!=bool:raise Problem('连接处预览参数不合法。')
        if not isinstance(build_id,str) or not build_id or not manifest.is_file() or json.loads(manifest.read_text()).get('buildId')!=build_id:raise Problem('游戏版本与制作服务不一致，请使用最新构建。',409)
        try:
            image=base64.b64decode(body.get('image',''),validate=True)
            if len(image)>8*1024*1024:raise ValueError()
            with Image.open(io.BytesIO(image)) as im:
                if im.format!='PNG' or im.width<320 or im.height<180 or im.width*im.height>4_000_000:raise ValueError()
                im.verify()
        except Exception:raise Problem('需要保存当前 Unity 预览截图。') from None
        style=surface_style(body.get('surfaceStyle',old.get('surfaceStyle','')))
        layout={'schemaVersion':1,'scope':'native-runtime-home','profileId':client,'runId':r['id'],'revision':r['revision'],'version':old['version']+1,'surfaceStyle':style,
            'instances':self.game.native_items(items),'seamNatural':body.get('seamNatural',True) is True,'seamSoftened':body.get('seamSoftened',False),'buildId':build_id,'createdAt':now()}
        layout_ref=self.repo.put_json(r['id'],layout,'游戏内布局预览');picture=self.repo.put(r['id'],image,'png','Unity 游戏内预览',[layout_ref]);report=self.repo.put_json(r['id'],{'scope':'native-runtime-home','buildId':build_id,'layoutHash':layout_ref['sha256'],'checks':checks,'passed':True},'游戏运行时布局检查',[layout_ref])
        token=uuid4().hex
        with self.repo.lock:
            self.game.require_home_land(client)
            r=self.game.own_run(client,r['id'])
            if r['revision']!=layout['revision'] or r['paused'] or r['cancelled'] or self.game.layout(client)['version']!=old['version']:
                raise Problem('创作或家园在预览期间发生变化，请重新查看后再确认。',409)
            r['runtimePreview']={'tokenHash':fingerprint(token),'layoutRef':layout_ref,'pictureRef':picture,'reportRef':report,'expectedVersion':old['version'],'revision':r['revision'],'visualBinding':binding_for(layout,r['revision'],r.get('conceptRef'),body.get('lightingHour',12),policy_for(r))};r['status']='awaiting_player';self.repo.save(r)
        return {'runId':r['id'],'previewToken':token,'version':layout['version']}

    def finalize(self,body):
        client=body.get('clientId')
        with self.repo.lock:
            r=self.game.own_run(client,body.get('runId'))
            self.game.require_home_land(client)
            if r['paused'] or r['cancelled']:raise Problem('这次创作已暂停或取消，家园与草稿仍已保留。',409)
            preview=r.get('runtimePreview',{})
            visual=r.get('runtimeVisualReviews',{}).get(preview.get('visualBinding'),{})
            if r.get('visualReviewRequired') and not (visual.get('state')=='complete' and visual.get('action')=='accept') and body.get('visualOverrideAccepted') is not True:raise Problem('请先完成当前布局的画面检查；仍可手动调整草稿。',409)
            if body.get('visualAccepted') is not True or fingerprint(body.get('previewToken'))!=preview.get('tokenHash') or preview.get('revision')!=r['revision']:raise Problem('预览已变化，请重新查看后确认。',409)
            if r.get('runtimeFinalized') and r.get('runtimeFinalToken')==preview['tokenHash']:return self.game.layout(client)
            old=self.game.layout(client)
            if preview['expectedVersion']!=old['version']:raise Problem('旧预览不能覆盖最新家园。',409)
            layout=json.loads(self.repo.path(preview['layoutRef'],r['id']).read_text());self.repo.path(preview['pictureRef'],r['id']);self.repo.path(preview['reportRef'],r['id'])
            for i in layout['instances']:self.catalog.check_dependencies(self.catalog.get(i['assetId'],i['assetVersion']))
            test=self.game.get_profile(client).get('testMode',False)
            c={'id':uuid4().hex,'gate':'final_layout','actor':'test-driver' if test else 'player','at':now(),'bindingHash':preview['layoutRef']['sha256'],'visualAccepted':True,'visualOverrideAccepted':body.get('visualOverrideAccepted') is True,'visualReviewBinding':preview.get('visualBinding')}
            r['runtimeAssetManifest']={i['assetId']:{k:a[k] for k in ('id','name','version','contentHash','size','source')} for i in layout['instances'] for a in [self.catalog.get(i['assetId'],i['assetVersion'])]}
            snapshot={'schemaVersion':1,'releaseId':uuid4().hex,'runId':r['id'],'revision':r['revision'],'scope':'native-runtime-home','assemblyVersion':'runtime-'+str(layout['version']),
                'layoutRef':preview['layoutRef'],'sceneRef':preview['layoutRef'],'validationRef':preview['reportRef'],'previewRefs':[preview['pictureRef']],'playerApproval':c,'confirmedAt':c['at'],'buildId':layout['buildId']}
            snapshot_ref=self.repo.put_json(r['id'],snapshot,'已确认的游戏内家园',[preview['layoutRef'],preview['reportRef']])
            if r.get('finalize') and isinstance(r['finalize'],dict):r.setdefault('finalizationHistory',[]).append(r['finalize'])
            r['layout']={'layoutRef':preview['layoutRef'],'layoutSceneRef':preview['layoutRef'],'layoutPreviewRefs':[preview['pictureRef']],'scenePath':'runtime://'+client+'/'+str(layout['version']),
                'sceneSha256':preview['layoutRef']['sha256'],'assemblyVersion':snapshot['assemblyVersion'],'instances':layout['instances'],'instanceCount':len(layout['instances'])}
            r['validation']={'bindingVersion':1,'scope':'native-runtime-home','passed':True,'revision':r['revision'],'layoutRefId':preview['layoutRef']['id'],'sceneSha256':preview['layoutRef']['sha256'],'reportRef':preview['reportRef'],'screenshotRefs':[preview['pictureRef']]}
            r['finalize']={'snapshotRef':snapshot_ref,'releaseId':snapshot['releaseId'],'confirmedAt':c['at'],'scope':'native-runtime-home'}
            r['confirmations'].append(c);r['phase']='finalized';r['status']='completed';r['runtimeFinalized']=True;r['runtimeFinalToken']=preview['tokenHash'];r['stages'].update(layout='complete',validation='complete',finalize='complete')
            memory=self.creation.workspace.remember_scene(r,snapshot,persist=False)
            memory['profileId']=client
            if test:memory.update(kind='validation_scene',actor='test-driver')
            # One database transaction commits the home, the finalization, and its memory.
            r['updatedAt']=now()
            with self.repo.db:
                self.repo.db.execute('INSERT OR REPLACE INTO game_layouts VALUES (?,?,?)',(client,client,canonical(layout)))
                self.repo.db.execute('INSERT OR IGNORE INTO memories VALUES (?,?)',(memory['id'],canonical(memory)))
                self.repo.db.execute('INSERT OR REPLACE INTO runs VALUES (?,?)',(r['id'],canonical(r)))
        return layout
