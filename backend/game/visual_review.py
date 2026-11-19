"""Metered comparison of a real Unity capture against the confirmed concept."""
import copy
import json
from backend.home_creation.catalog import validate_instances
from backend.home_creation.storage import fingerprint
from backend.store import Problem,now
from backend.game.garden_recipes import surface_style,checked_initial_layout

PROMPT='''检查实际 Unity 场景，不要把概念图的细节当作已经实现。
图片顺序为：已确认概念图（若有），当前 Unity 真实渲染。
比较地面草土层次、花丛/灌木密度、主体周边组合、景石位置、边缘厚度、配色和接触阴影。不能因为模型存在且可运行就accept；也不要求生成图逐像素相同。目录是工具能力，不是需求清单，不能把目录里有而概念图没有的物件说成缺失；不得擅自添加火系或其他主题物件。光照和着色器等工具不能修改的差异，只在issues说明，不用乱加植物冒充解决。
protectedInstanceIds中的主体已被玩家认可，不能移动、缩放或删除。比例是否失衡应根据实际画面中与周边物件的关系判断，不能仅凭scale数字；通路是否堵塞以已通过的运行检查为依据，不凭画面臆测。只优化配景，保持主体。比较不同机位时，不把远景中物件在屏幕上较小当作模型缺陷。
返回JSON {"action":"accept|revise","summary":"简短结论","issues":["可观察问题"],"edits":[{"op":"transform|add|remove","instanceId":"英文数字ID","assetId":"仅add时填写目录id","localPosition":[1,0,2],"localEulerAngles":[0,0,0],"localScale":[1,1,1]}],"surfaceStyle":"原值或garden-meadow-v1"}。
最多6条调整，用目录里的花丛、灌木、草簇和苔石组合补足层次，不生成模型、代码或图片。单个组合包含大量叶片/花朵，无需每朵单独摆放。位置米，X半径6、Z半径5的椭圆，地面Y=0；西侧x<-.5且z在-.8到.8的走廊不能摆放，注意物件尺寸。总实例不超过50。
transform只修改指定实例的变换，不删除原有主体。add仅使用目录中placeable的资源。可修正轻微缺陷时给出具体调整；工具不能解决的缺陷如缺新模型，用issues说明且edits为空，不得冒充已修复。accept必须issues/edits为空。'''

def policy_for(run):
    cfg=run['settingsSnapshot'];agent=cfg['agent']
    return {'version':2,'prompt':cfg.get('visualReviewPrompt',PROMPT),'model':agent['model'],'instruction':agent['instruction'],'maxCorrections':agent.get('maxCorrections',2)}

def binding_for(layout,revision,concept,hour,policy=None):
    return fingerprint({'revision':revision,'concept':concept.get('sha256') if concept else None,'instances':layout['instances'],'surfaceStyle':layout.get('surfaceStyle',''),'buildId':layout.get('buildId'),'version':layout['version'],'hour':hour,'policy':policy})

def apply_edits(layout,edits,catalog,style,allowed=None,protected=()):
    if not isinstance(edits,list) or len(edits)>6:raise Problem('画面修正指令超出范围。')
    items=copy.deepcopy(layout['instances']);by={i['instanceId']:i for i in items}
    for edit in edits:
        if not isinstance(edit,dict) or edit.get('op') not in {'add','remove','transform'}:raise Problem('画面修正指令无效。')
        identity=edit.get('instanceId');op=edit['op']
        if identity in protected:raise Problem('画面检查不能改变已认可的主体，请只调整配景。')
        if op=='add':
            if identity in by:raise Problem('新增装饰的标识重复。')
            if allowed is not None and edit.get('assetId') not in allowed:raise Problem('画面修正不能引入无关主题资源。')
            a=catalog.get(edit.get('assetId'));item={'instanceId':identity,'assetId':a['id'],'assetVersion':a['version'],'assetHash':a['contentHash'],'tint':'#ffffff'}
            item.update({k:edit[k] for k in ('localPosition','localEulerAngles','localScale')});items.append(item);by[identity]=item
        else:
            if identity not in by:raise Problem('要调整的物件已变化。')
            if op=='remove':items.remove(by.pop(identity))
            else:by[identity].update({k:edit[k] for k in ('localPosition','localEulerAngles','localScale')})
    checked=checked_initial_layout(items,catalog,[catalog.get(a) for a in {i['assetId'] for i in items}])[0] if items else []
    for item,source in zip(checked,items):
        if source.get('giftId'):item['giftId']=source['giftId']
    return checked,surface_style(style)

class RuntimeVisualReview:
    def __init__(self,game):self.game=game;self.repo=game.repo
    def start(self,body):
        client=body.get('clientId');preview=self.game.runtime.preview(body)
        with self.repo.lock:
            r=self.game.own_run(client,preview['runId']);layout=self.repo.file(r['runtimePreview']['layoutRef']['id'],r['id'])
            value=json.loads(self.repo.path(layout,r['id']).read_text());hour=body.get('lightingHour',12)
            if type(hour) is not int or not 0<=hour<=23:raise Problem('光照时间无效。')
            binding=binding_for(value,r['revision'],r.get('conceptRef'),hour,policy_for(r));old=r.get('runtimeVisualReviews',{}).get(binding)
            if old:return old
            refs=([r['conceptRef']] if r.get('conceptRef') else [])+[r['runtimePreview']['pictureRef']]
            cfg=r['settingsSnapshot'];allowed={i['assetId'] for i in value['instances']}|{'garden-flower-cluster','garden-understory-cluster','garden-grass-tuft','garden-rock-cluster','meadow-flower','moss-boulder','garden-tree'}
            catalog=[a for a in self.game.catalog.context() if a['id'] in allowed]
            protected=[i['instanceId'] for i in value['instances'] if self.game.catalog.get(i['assetId'],i['assetVersion']).get('source',{}).get('kind')=='runtime-glb']
            payload={'purpose':'runtime_visual_review','agentConfig':cfg['agent'],'systemPrompt':cfg.get('visualReviewPrompt',PROMPT),'inputRefs':refs,
                     'context':{'layout':value,'catalog':catalog,'protectedInstanceIds':protected,'runtimeChecks':body['checks'],'brief':r.get('brief'),'referenceAvailable':bool(r.get('conceptRef')),'productionCoverage':r.get('productionCoverage',[]),'hour':hour}}
            task=self.game.creation.new_task(r,'runtime-visual:'+binding,'agent',payload,refs,[{'unit':'CNY','amount':cfg['agentReserveCny']}])
            if task is None:self.repo.save(r);raise Problem('画面检查预算不足，草稿仍保留，可以手动查看和调整。',409)
            entry={'runId':r['id'],'binding':binding,'state':'running','taskId':task['id'],'maxCorrections':cfg['agent'].get('maxCorrections',2),'startedAt':now()}
            r.setdefault('runtimeVisualReviews',{})[binding]=entry;self.repo.save(r)
        self.game.pool.submit(self.run,client,r['id'],binding,task['id'],value)
        return entry
    def get(self,client,rid,binding):
        r=self.game.own_run(client,rid);entry=r.get('runtimeVisualReviews',{}).get(binding)
        if not entry:raise Problem('画面检查记录不存在。',404)
        return entry
    def run(self,client,rid,binding,tid,layout):
        try:
            for _ in range(3):
                t=self.game.creation.task(self.repo.get(rid),tid)
                if t['state']=='verified':break
                if t['state'] not in {'planned','responded'}:raise Problem('画面检查未完成，未自动重复付费请求。',409)
                self.game.creation.execute_task(rid,tid)
            t=self.game.creation.task(self.repo.get(rid),tid)
            if t['state']!='verified':raise Problem('画面检查中断，草稿已保留。',409)
            value=self.game.creation.json_result(t);action=value.get('action');issues=value.get('issues');summary=value.get('summary')
            if action not in {'accept','revise'} or not isinstance(summary,str) or not 1<=len(summary)<=500 or not isinstance(issues,list) or len(issues)>8 or any(not isinstance(i,str) or len(i)>500 for i in issues):raise Problem('画面检查结果格式不合法。')
            edits=value.get('edits',[]);proposal=[];style=layout.get('surfaceStyle','')
            if action=='accept' and (issues or edits):raise Problem('检查结果不能在仍有问题时声明通过。')
            if action=='revise' and (edits or value.get('surfaceStyle',style)!=style):proposal,style=apply_edits(layout,edits,self.game.catalog,value.get('surfaceStyle',style),{a['id'] for a in t['_payload']['context']['catalog']},t['_payload']['context'].get('protectedInstanceIds',()))
            result={'state':'complete','action':action,'summary':summary,'issues':issues,'hasProposal':bool(proposal),'proposal':self.game.native_items(proposal),'surfaceStyle':style,'finishedAt':now()}
        except Exception as exc:result={'state':'failed','summary':str(exc) if isinstance(exc,Problem) else '画面检查遇到问题，草稿和截图已保留。','finishedAt':now()}
        with self.repo.lock:
            r=self.repo.get(rid);r['runtimeVisualReviews'][binding].update(result)
            if r.get('runtimePreview',{}).get('visualBinding')==binding:r['visualStatus']='reviewed' if result.get('action')=='accept' else 'needs_adjustment'
            r['status']='awaiting_player' if r['phase']!='finalized' else 'completed';self.repo.save(r)
