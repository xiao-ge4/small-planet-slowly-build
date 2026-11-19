"""Local authoring tools: immutable catalog selection, editable drafts, and grounded memory."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import shutil
from uuid import uuid4

from backend.home_creation import prompts
from backend.home_creation.catalog import AssetCatalog, validate_instances, collision_spec
from backend.home_creation.storage import canonical, fingerprint
from backend.store import Problem, now


class Workspace:
    def __init__(self, service, root):
        self.service=service;self.repo=service.repo;self.root=Path(root)
        self.catalog=AssetCatalog(self.repo,self.root)
        with self.repo.lock,self.repo.db:
            self.repo.db.execute('CREATE TABLE IF NOT EXISTS memories (id TEXT PRIMARY KEY,data TEXT)')
            self.repo.db.execute('CREATE TABLE IF NOT EXISTS game_plots (id TEXT PRIMARY KEY,data TEXT)')

    def unity(self, code):
        result=subprocess.run([str(self.root/'tools/unity'),'command','script','eval','--project-path',str(self.root/'unity/DawnGarden'),'--timeout','90','--json','--','--code',code],cwd=self.root,capture_output=True,text=True,timeout=120)
        if result.returncode or '"success": false' in result.stdout:raise Problem('Unity 操作失败，请保存场景、退出 Play Mode 并检查编译状态后重试。',409)
        return result.stdout

    def refresh_catalog(self):
        if not self.service.runtime_lock.acquire(blocking=False):raise Problem('Unity 正在执行任务，请稍后刷新目录。',409)
        try:
            directory=self.root/'artifacts/home-creation/catalog'
            self.unity('return DawnGarden.Editor.HomeAssetCatalog.Export('+json.dumps(str(directory))+');')
            entries=self.catalog.import_manifest(json.loads((directory/'index.json').read_text()))
            return {'count':len(entries),'assets':entries}
        finally:self.service.runtime_lock.release()

    def editor(self, rid):
        r=self.repo.get(rid)
        draft=r.get('layoutDraft')
        if not draft:
            items=[]
            for item in (r.get('layout') or {}).get('instances',[]):
                asset=next((a for a in self.catalog.search() if a['prefabPath']==item.get('sourceAssetRef')),None)
                if asset:
                    vector=lambda key: [item[key][k] for k in ('x','y','z')] if isinstance(item[key],dict) else item[key]
                    items.append(dict(instanceId=item['instanceId'],assetId=asset['id'],assetVersion=asset['version'],assetHash=asset['contentHash'],
                        localPosition=vector('localPosition'),localEulerAngles=vector('localEulerAngles'),localScale=vector('localScale'),tint='#ffffff'))
            draft={'version':0,'instances':items,'baseLayoutRefId':(r.get('layout') or {}).get('layoutRef',{}).get('id'),'updatedAt':None}
        catalog=self.catalog.search(placeable=True)
        for item in draft['instances']:
            old=self.catalog.get(item['assetId'],item.get('assetVersion'),required=False)
            if old and not any(a['id']==old['id'] and a['version']==old['version'] for a in catalog):catalog.append(old)
        return {'runId':rid,'draft':draft,'assemblyJob':r.get('assemblyJob'),'plotSize':[12.3,10.3],
                'phase':r['phase'],'conceptRef':r.get('conceptRef'),'layout':r.get('layout'),'catalog':catalog,'feedback':r.get('layoutFeedback',[]),'layoutProposal':r.get('layoutProposal'),
                'preparationJob':r.get('preparationJob'),'canBootstrap':r['phase']=='assets_ready'}

    def prepare_assets(self,rid):
        with self.repo.lock:
            r=self.repo.get(rid)
            if r['phase']!='assets_ready' or r['paused'] or r['cancelled']:raise Problem('请先完成模型下载。',409)
            pending=[aid for aid in r.get('models',{}) if aid not in r.get('preparedAssets',{})]
            if not pending:
                r['stages']['assets']='complete';self.repo.save(r);return self.editor(rid)
            if not self.service.runtime_lock.acquire(blocking=False):raise Problem('Unity 正在执行任务，请稍后。',409)
            jid=uuid4().hex;r['preparationJob']={'id':jid,'state':'running','startedAt':now()};self.repo.save(r)
            try:self.service.pool.submit(self.run_preparation,rid,jid,pending,r['revision'])
            except Exception:self.service.runtime_lock.release();raise
        return self.editor(rid)

    def run_preparation(self,rid,jid,pending,revision):
        try:
            from PIL import Image
            for aid in pending:
                r=self.repo.get(rid);model=r['models'][aid];design=r['designs'][aid]
                source=self.repo.path(model['modelRef'],rid);digest=model['modelRef']['sha256'];height=design['asset']['heightMetres']
                output=self.root/'artifacts/home-creation/asset-preparation'/rid/('r'+str(revision))/aid/digest[:10];output.mkdir(parents=True,exist_ok=True)
                result=output/'preparation.json'
                command=[str(self.root/'tools/blender'),'--background','--python',str(self.root/'scripts/prepare_home_asset_blender.py'),'--','--source',str(source),'--out',str(output),'--height',str(height),'--result',str(result)]
                with (output/'blender.log').open('w') as log:
                    process=subprocess.run(command,cwd=self.root,stdout=log,stderr=subprocess.STDOUT,timeout=240)
                if process.returncode or not result.is_file():raise Problem('模型归一与 LOD 准备失败，原始模型已保留。',409)
                prep=json.loads(result.read_text())
                if not prep.get('success'):raise Problem('模型准备报告未通过。',409)
                folder='Assets/DawnGarden/PreparedHomes/'+rid+'/r'+str(revision)+'/'+aid+'-'+digest[:10]
                project=self.root/'unity/DawnGarden';models=project/folder/'Models';models.mkdir(parents=True,exist_ok=True)
                for lod in prep['lods']:shutil.copyfile(output/lod['file'],models/lod['file'])
                vector=lambda v:dict(zip(('x','y','z'),v))
                task=self.service.task(r,model['taskId'])
                request={'runId':rid,'assetId':aid,'revision':revision,'sourceSha256':digest,'designSha256':model['sourceDesignRef']['sha256'],
                    'providerTaskId':task['providerTaskId'],'preparationSha256':hashlib.sha256(result.read_bytes()).hexdigest(),
                    'height':height,'colliderSize':vector(prep['collider']['size']),'colliderCenter':vector(prep['collider']['center']),
                    'modelDirectory':folder+'/Models','outputDirectory':str(output),'prefabPath':folder+'/'+aid+'.prefab','previewScenePath':folder+'/Review.unity'}
                request_path=output/'unity-request.json';request_path.write_text(canonical(request))
                self.unity('DawnGarden.Editor.HomeAssetPreparation.Build('+json.dumps(str(request_path))+'); return "prepared";')
                report=json.loads((output/'unity-report.json').read_text())
                if not report.get('passed'):raise Problem('Unity 资产检查未通过。',409)
                sheet=Image.new('RGB',(960,720),'#eef0e7')
                for index,name in enumerate(report['previews']):
                    with Image.open(output/name) as im:im=im.copy();im.thumbnail((320,240));sheet.paste(im,((index%3)*320,(index//3)*240))
                sheet_data=__import__('io').BytesIO();sheet.save(sheet_data,'PNG')
                with self.repo.lock:
                    latest=self.repo.get(rid)
                    if latest['revision']!=revision or latest['phase']!='assets_ready':raise Problem('方案版本已变化，准备产物保留但未覆盖新方案。',409)
                    parents=[model['modelRef'],model['sourceDesignRef']]
                    asset={'assetId':aid,'name':design['asset']['name'],'category':'static-decoration','revision':revision,'sourceModelRef':model['modelRef'],
                        'sourceDesignRef':model['sourceDesignRef'],'dimensionsMetres':prep['normalization']['dimensionsMetres'],'collider':prep['collider'],
                        'lods':[{'level':i,'triangles':lod['triangles'],'modelRef':self.repo.put(rid,(output/lod['file']).read_bytes(),'glb',aid+' LOD'+str(i),parents)} for i,lod in enumerate(prep['lods'])],
                        'contactSheetRef':self.repo.put(rid,sheet_data.getvalue(),'png','模型三视角与 LOD 检查',parents),'previewRef':self.repo.put(rid,(output/'preview.png').read_bytes(),'png',aid+'预览',parents),
                        'unityReportRef':self.repo.put_json(rid,report,'Unity 资产检查',parents),'unitScale':1,'upAxis':'Y','pivot':[0,0,0],
                        'unity':{'projectPath':str(project),'prefabPath':request['prefabPath'],'previewScenePath':request['previewScenePath']},
                        'visualReview':{'passed':False,'method':'awaiting final player visual review'},'checks':report['checks']}
                    latest.setdefault('preparedAssets',{})[aid]=asset;self.repo.save(latest)
            directory=self.root/'artifacts/home-creation/catalog';self.unity('return DawnGarden.Editor.HomeAssetCatalog.Export('+json.dumps(str(directory))+');')
            self.catalog.import_manifest(json.loads((directory/'index.json').read_text()))
            with self.repo.lock:
                r=self.repo.get(rid);r['preparationJob'].update(state='complete',finishedAt=now());r['stages']['assets']='complete';self.repo.save(r)
        except Exception as exc:
            with self.repo.lock:
                r=self.repo.get(rid);r['preparationJob'].update(state='failed',finishedAt=now(),message=str(exc) if isinstance(exc,Problem) else '本地模型准备未完成，请查看 Blender 与 Unity 检查报告后重试。');self.repo.save(r)
        finally:self.service.runtime_lock.release()

    def bootstrap(self,rid):
        """Start a layout from an existing, versioned terrain template; never overwrite it."""
        with self.repo.lock:
            r=self.repo.get(rid)
            if r.get('layout'):return self.editor(rid)
            if r['phase']!='assets_ready' or r['paused'] or r['cancelled']:raise Problem('请先完成设计与资产制作批次。',409)
            if any(aid not in r.get('preparedAssets',{}) for aid in r.get('models',{})):raise Problem('请先准备新模型的 LOD 与碰撞。',409)
            candidates=[x for x in self.repo.all() if x.get('layout') and x['id']!=rid]
            if not candidates:raise Problem('尚无可用土地模板，请先登记一个已有家园装配。',409)
            template=candidates[-1]['layout'];available=self.catalog.search(placeable=True)
            requested=[a for a in r.get('plan',{}).get('assets',[]) if a['route']=='reuse']
            chosen=[self.catalog.get(a['sourceId'],required=False) for a in requested]
            chosen=[a for a in chosen if a and a['placeable']]
            for a in r.get('preparedAssets',{}).values():
                chosen.extend(x for x in available if x['prefabPath']==a['unity']['prefabPath'])
            if not chosen:raise Problem('方案没有可摆放静态资源，请在方案中至少选择一件装饰。',409)
            items=[]
            positions=[[1.7,0,1.45],[2.5,0,-1.8],[-1.8,0,2.7],[-2,0,-2.7],[0,0,3.2]]
            for index,a in enumerate(chosen[:5]):
                maxdim=max(a['size']['x'],a['size']['z']);scale=min(1,1.5/maxdim)
                items.append({'instanceId':'initial-'+str(index),'assetId':a['id'],'assetVersion':a['version'],'assetHash':a['contentHash'],'localPosition':positions[index],'localEulerAngles':[0,0,0],'localScale':[scale]*3,'tint':'#ffffff'})
            validate_instances(items,self.catalog)
            # Temporary template association is a draft, not a completed assembly or acceptance.
            r['layout']={'scenePath':template['scenePath'],'layoutRef':template['layoutRef'],'instances':[],'assemblyVersion':'template','instanceCount':0,'layoutPreviewRefs':template.get('layoutPreviewRefs',[])}
            r['layoutDraft']={'version':0,'instances':items,'baseLayoutRefId':template['layoutRef']['id'],'updatedAt':now()};r['stages']['assets']='complete';self.repo.save(r)
        return self.assemble(rid)

    def propose_layout(self,rid,body):
        """One metered planning request; output can only become a checked draft."""
        with self.repo.lock:
            r=self.repo.get(rid)
            if r['paused'] or r['cancelled'] or not r.get('layout'):raise Problem('请先准备初始布局，并保持任务可编辑。',409)
            self.service.request_id(body.get('requestId'))
            key='layout-plan:'+body['requestId']
            prior=next((t for t in r['tasks'] if t['key']==key),None)
            if prior:
                if prior.get('workspaceRequestHash') and prior['workspaceRequestHash']!=fingerprint(body):raise Problem('布局规划请求标识冲突。',409)
                return self.editor(rid)
            if r.get('layoutProposal',{}).get('state')=='running':raise Problem('Agent 正在规划摆放。',409)
            catalog=self.catalog.context()

            payload={'purpose':'layout','systemPrompt':r['settingsSnapshot'].get('layoutPrompt',prompts.LAYOUT),'agentConfig':r['settingsSnapshot']['agent'],
                'context':{'idea':r['idea'],'plan':r.get('plan'),'catalog':catalog,'current':self.editor(rid)['draft'],'instruction':str(body.get('instruction',''))[:2000],'memory':r.get('memoryContext',{})},'inputRefs':[r['conceptRef']] if r.get('conceptRef') else []}
            task=self.service.new_task(r,key,'agent',payload,payload['inputRefs'],[{'unit':'CNY','amount':r['settingsSnapshot']['agentReserveCny']}])
            if not task:self.repo.save(r);raise Problem('规划预算不足，未调用 Agent。',409)
            task['workspaceRequestHash']=fingerprint(body)
            r['layoutProposal']={'state':'running','taskId':task['id'],'baseDraftVersion':self.editor(rid)['draft']['version']};r['status']='awaiting_next_stage';self.repo.save(r)
            self.service.pool.submit(self.run_proposal,rid,task['id'])
        return self.editor(rid)

    def run_proposal(self,rid,tid):
        try:
            self.service.execute_task(rid,tid);self.service.execute_task(rid,tid)
            with self.repo.lock:
                r=self.repo.get(rid);task=self.service.task(r,tid)
                if task['state']!='verified':raise Problem('规划未完成，原摆放未改变。请查看请求记录。',409)
                result=self.service.json_result(task);items=validate_instances(result.get('instances'),self.catalog)
                r['layoutProposal'].update(state='ready',instances=items,finishedAt=now());r['status']='awaiting_player' if r['phase']=='validation_ready' else 'awaiting_next_stage';self.repo.save(r)
        except Exception as exc:
            with self.repo.lock:
                r=self.repo.get(rid);r['layoutProposal'].update(state='failed',message=str(exc) if isinstance(exc,Problem) else 'Agent 布局格式不符合约定，未改变原摆放。');self.repo.save(r)

    def save_draft(self,rid,body):
        with self.repo.lock:
            r=self.repo.get(rid)
            if r['paused'] or r['cancelled'] or r.get('assemblyJob',{}).get('state')=='running' or r.get('runtimeJob',{}).get('state')=='running':raise Problem('任务正在执行或已暂停，暂不能修改布局。',409)
            if not r.get('layout'):raise Problem('请先创建初始布局。',409)
            old=self.editor(rid)['draft']
            if body.get('expectedVersion')!=old['version'] or body.get('baseLayoutRefId')!=r['layout']['layoutRef']['id']:raise Problem('布局已变化，请重新打开后编辑。',409,'STALE_DRAFT')
            items=validate_instances(body.get('instances'),self.catalog)
            r.setdefault('layoutDraftHistory',[]).append(old)
            r['layoutDraft']={'version':old['version']+1,'instances':items,'baseLayoutRefId':r['layout']['layoutRef']['id'],'updatedAt':now()}
            self.service.invalidate_final(r)
            r['validation']='not_started';r['stages']['validation']='not_started';r['phase']='layout_changes_requested';r['status']='needs_layout_changes'
            self.service.event(r,'DRAFT_SAVED','摆放草稿已保存，原运行验收失效；应用到 Unity 后重新验收。')
            self.repo.save(r)
        return self.editor(rid)

    def assemble(self,rid):
        with self.repo.lock:
            r=self.repo.get(rid)
            if r['paused'] or r['cancelled']:raise Problem('任务已暂停或取消。',409)
            draft=self.editor(rid)['draft'];items=validate_instances(draft['instances'],self.catalog)
            for item in items:self.catalog.check_dependencies(self.catalog.get(item['assetId'],item['assetVersion']))
            if not self.service.runtime_lock.acquire(blocking=False):raise Problem('Unity 正在执行任务，请稍后。',409)
            job_id=uuid4().hex
            r['assemblyJob']={'id':job_id,'state':'running','draftVersion':draft['version'],'startedAt':now()}
            self.repo.save(r)
            try:self.service.pool.submit(self.run_assembly,rid,job_id,copy.deepcopy(draft),items)
            except Exception:self.service.runtime_lock.release();raise
        return self.editor(rid)

    def run_assembly(self,rid,job_id,draft,items):
        try:
            r=self.repo.get(rid);version='edit-'+job_id[:12]
            output=self.root/'artifacts/home-creation/layout'/rid/version;output.mkdir(parents=True,exist_ok=True)
            scene='Assets/DawnGarden/PreparedHomes/'+rid+'/'+version+'/Home.unity'
            def vec(v):return dict(zip(('x','y','z'),v))
            unity_items=[{**i,**{k:vec(i[k]) for k in ('localPosition','localEulerAngles','localScale')}} for i in items]
            for item in unity_items:
                spec=collision_spec(self.catalog.get(item['assetId'],item['assetVersion']))
                if spec['kind']=='box':item.update(createCollider=True,collisionCenter=spec['center'],collisionSize=spec['size'])
            request={'runId':rid,'revision':r['revision'],'assemblyVersion':version,'sourceScenePath':r['layout']['scenePath'],
                'outputScenePath':scene,'outputDirectory':str(output),'instances':unity_items,'homeId':self.claim_grant(r['plotGrant']['token'],rid)['homeId'] if r['plotGrant'].get('purpose')=='game-issued' else ''}
            request_path=output/'request.json';request_path.write_text(canonical(request))
            self.unity('return DawnGarden.Editor.HomeLayoutEditorBridge.Apply('+json.dumps(str(request_path))+');')
            layout=json.loads((output/'layout.json').read_text())
            latest=self.repo.get(rid)
            if self.editor(rid)['draft']['version']!=draft['version'] or latest['revision']!=r['revision']:raise Problem('装配期间方案变化，结果未登记。',409)
            self.service.register_layout(rid,{'requestId':'assembly-'+job_id,'revision':r['revision'],'scenePath':scene,'sceneSha256':layout['sceneSha256'],'layoutPath':str(output/'layout.json'),'previewPaths':[str(output/'preview.png')]})
            with self.repo.lock:
                latest=self.repo.get(rid)
                latest['layoutDraft']={**draft,'instances':items,'baseLayoutRefId':latest['layout']['layoutRef']['id']}
                latest['assemblyJob'].update(state='complete',finishedAt=now());self.repo.save(latest)
        except Exception as exc:
            with self.repo.lock:
                r=self.repo.get(rid);r['assemblyJob'].update(state='failed',finishedAt=now(),message=str(exc) if isinstance(exc,Problem) else 'Unity 装配未完成，请检查编辑器后重试。');self.repo.save(r)
        finally:self.service.runtime_lock.release()

    def memories(self,query=''):
        with self.repo.lock:rows=self.repo.db.execute('SELECT data FROM memories ORDER BY rowid DESC').fetchall()
        result=[json.loads(row[0]) for row in rows]
        if query:result=[m for m in result if any(w.lower() in canonical(m).lower() for w in str(query).split())]
        return result[:100]

    def preference(self,body):
        text=body.get('text','');kind=body.get('kind','explicit_preference')
        if kind not in {'explicit_preference','agent_hypothesis'} or not isinstance(text,str) or not 1<=len(text.strip())<=1000:raise Problem('请填写偏好内容，并标明是玩家确认还是推测。')
        # Agent hypotheses can be stored, but are never injected as confirmed preferences.
        rid=body.get('runId')
        if rid:self.repo.get(rid)
        entry={'id':uuid4().hex,'kind':kind,'actor':'player' if kind=='explicit_preference' else 'agent','text':text.strip(),'runId':rid,'createdAt':now(),'active':True}
        with self.repo.lock,self.repo.db:self.repo.db.execute('INSERT INTO memories VALUES (?,?)',(entry['id'],canonical(entry)))
        return entry

    def retire_memory(self,mid):
        with self.repo.lock,self.repo.db:
            row=self.repo.db.execute('SELECT data FROM memories WHERE id=?',(mid,)).fetchone()
            if not row:raise Problem('记忆不存在。',404)
            data=json.loads(row[0]);data['active']=False;data['retiredAt']=now()
            self.repo.db.execute('UPDATE memories SET data=? WHERE id=?',(canonical(data),mid))
        return data

    def remember_scene(self,r,snapshot,persist=True):
        entry={'id':'scene-'+snapshot['releaseId'],'kind':'confirmed_scene','actor':'player','active':True,'createdAt':now(),
            'runId':r['id'],'revision':r['revision'],'text':r['idea'],'theme':r.get('brief',{}).get('theme'),
            'decisions':[{k:c[k] for k in ('gate','at','bindingHash')} for c in r['confirmations'] if not c.get('stale')],
            'conceptRef':r.get('conceptRef'),'imageHistory':copy.deepcopy(r.get('artifactHistory',[])),
            'assetRefs':copy.deepcopy(r.get('runtimeAssetManifest') or r.get('preparedAssets',{})),'layoutRef':snapshot['layoutRef'],'sceneRef':snapshot['sceneRef'],
            'validationRef':snapshot['validationRef'],'layoutInstances':copy.deepcopy(r['layout']['instances']),
            'snapshot':copy.deepcopy(snapshot),'costs':self.service.budget(r),'metrics':{'startedAt':r['createdAt'],'confirmedAt':snapshot['confirmedAt'],
                'playerConfirmations':len([c for c in r['confirmations'] if not c.get('stale')]),'playerRevisions':r['revision']-1,'layoutEdits':len(r.get('layoutDraftHistory',[])),
                'manualInterventions':len(r.get('layoutFeedback',[]))}}
        if persist:
            with self.repo.lock,self.repo.db:self.repo.db.execute('INSERT OR IGNORE INTO memories VALUES (?,?)',(entry['id'],canonical(entry)))
        return entry

    def memory_context(self,ids=None):
        active=[m for m in self.memories() if m.get('active')]
        if ids is not None:active=[m for m in active if m['id'] in ids]
        return {'explicitPreferences':[{'id':m['id'],'text':m['text']} for m in active if m['kind']=='explicit_preference'],
            'confirmedScenes':[{'id':m['id'],'theme':m.get('theme'),'summary':m['text'],'assets':list(m.get('assetRefs',{})),'layout':m.get('layoutInstances',[])} for m in active if m['kind']=='confirmed_scene'][:5]}

    def memory_source(self,mid):
        return next((m for m in self.memories() if m['id']==mid and m.get('active') and m['kind']=='confirmed_scene'),None)

    def game_grant(self,body):
        token=body.get('token','')
        import re
        if not isinstance(token,str) or not re.fullmatch(r'[a-f0-9]{32}',token):raise Problem('土地凭证无效。')
        path=self.root/'artifacts/home-creation/game-grants'/(token+'.json')
        if not path.is_file():raise Problem('没有找到游戏发出的土地凭证，请在扩建后从游戏进入创作。',404)
        grant=json.loads(path.read_text())
        if grant.get('token')!=token or grant.get('homeId') not in {'dawn-garden-starter-v1','ember-camp-starter-v1'} or grant.get('plotId')!='first-expansion' or not grant.get('unlocked'):raise Problem('土地尚未获得。',409)
        profile_id=grant.get('profileId')
        if profile_id is not None and not re.fullmatch(r'[a-f0-9]{32}',str(profile_id)):raise Problem('游戏存档标识无效。')
        # A cached file or pre-guardian token cannot bypass a newer profile gate.
        # Profile-less editor grants remain compatible with the authoring pipeline.
        if profile_id is not None:
            profile=self.service.game.get_profile(profile_id)
            if profile.get('guardianLandVersion')==1:
                state=self.service.game.require_home_land(profile_id)
                if grant.get('guardianLandVersion')==1 and grant.get('guardianOccupationId')!=state.get('guardianOccupationId'):
                    raise Problem('土地凭据与当前占领记录不匹配。',409)
        edition=grant.get('edition',1)
        if type(edition)!=int or not 1<=edition<=10000:raise Problem('土地创作版本无效。')
        key=(profile_id+':' if profile_id else '')+grant['homeId']+':'+grant['plotId']+(':'+str(edition) if profile_id else '')
        with self.repo.lock,self.repo.db:
            row=self.repo.db.execute('SELECT data FROM game_plots WHERE id=?',(key,)).fetchone()
            stored=json.loads(row[0]) if row else {**grant,'id':key,'runId':None}
            self.repo.db.execute('INSERT OR REPLACE INTO game_plots VALUES (?,?)',(key,canonical(stored)))
        return stored

    def claim_grant(self,token,rid=None):
        grant=self.game_grant({'token':token})
        if grant['runId'] and grant['runId']!=rid:raise Problem('该土地已关联创作，请继续原有记录。',409,'PLOT_CLAIMED')
        if rid:
            grant['runId']=rid
            with self.repo.lock,self.repo.db:self.repo.db.execute('UPDATE game_plots SET data=? WHERE id=?',(canonical(grant),grant['id']))
        return grant

    def publish(self,rid):
        r=self.repo.get(rid)
        if r['phase']!='finalized' or not self.service.validation_current(r):raise Problem('请先通过运行验收并由玩家确认定稿。',409)
        report=json.loads(self.repo.path(r['validation']['reportRef'],rid).read_text());self.service.check_runtime_sources(report)
        if not self.service.runtime_lock.acquire(blocking=False):raise Problem('Unity 正在执行任务，请稍后。',409)
        try:
            # Local demo installation opens the exact accepted scene. No public hosting or build is implied.
            self.unity('return DawnGarden.Editor.HomeCreationLocalRelease.Open('+json.dumps(r['layout']['scenePath'])+','+json.dumps(r['layout']['sceneSha256'])+');')
            release={'scope':'local-unity-home','runId':rid,'releaseId':r['finalize']['releaseId'],'scenePath':r['layout']['scenePath'],'installedAt':now(),
                'plotGrant':r.get('plotGrant'),'snapshotRef':r['finalize']['snapshotRef']}
            saved,_=self.repo.conditional_update(rid,{'revision':r['revision'],'phase':'finalized','finalize':r['finalize'],'layout':r['layout']},lambda current:current.update(localRelease=release))
            if not saved:raise Problem('加载期间方案已变化，未覆盖最新记录。请重新查看当前版本。',409)
            return release
        finally:self.service.runtime_lock.release()
