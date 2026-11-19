"""Finite, executable scene recipes. Unsupported production routes stay explicit."""
import copy
import math
from backend.home_creation.catalog import validate_instances
from backend.store import Problem

SURFACE='garden-meadow-v1'

def surface_style(value):
    if value not in ('',SURFACE):raise Problem('地表样式不受支持。')
    return value

def resolve_plan(plan,catalog):
    selected={};coverage=[];style=''
    for asset in plan.get('assets',[]):
        route=asset['route'];source=asset.get('sourceId');label=(asset.get('id','')+' '+asset.get('name','')+' '+asset.get('description','')).lower()
        if route=='generated_mesh':continue
        if route=='procedural':
            if any(s in label for s in ('terrain','地形','场景底座')):
                style=SURFACE;coverage.append({'planAssetId':asset['id'],'recipe':SURFACE,'status':'resolved'});continue
            mapping=[(('flower','花丛','花簇'),'garden-flower-cluster'),(('shrub','bush','灌木','阔叶'),'garden-understory-cluster'),(('grass','草簇','草丛'),'garden-grass-tuft'),(('pebble','rock','碎石','景石'),'garden-rock-cluster')]
            source=next((aid for words,aid in mapping if any(w in label for w in words)),None)
            if not source:raise Problem('「'+asset['name']+'」尚没有可执行的场景配方，已保留其他资产，请调整拆分方案。',409)
            style=SURFACE
        elif route=='material_variant':
            raise Problem('「'+asset['name']+'」的材质修改尚未落实为可执行配方，请先明确现有材质方案。',409)
        item=catalog.get(source,required=False) if source else None
        if not item or not item['placeable']:raise Problem('「'+asset['name']+'」的复用资源或场景配方尚未准备好。',409)
        if item.get('source',{}).get('kind')=='procedural-kit':style=SURFACE
        selected[item['id']]=item;coverage.append({'planAssetId':asset['id'],'recipe':item['id'],'status':'resolved'})
    return list(selected.values()),coverage,style

def checked_initial_layout(raw,catalog,allowed):
    """Repair nearby footprints, preserving the proposed composition; never discard it."""
    if not isinstance(raw,list) or not raw:raise Problem('布局建议没有可摆放内容，模型已保留，请重新安排位置。',409)
    allowed={a['id'] for a in allowed};result=[];repairs=[]
    for item in raw:
        if item.get('assetId') not in allowed:raise Problem('布局用了本批次未准备的资源，请重新安排。',409)
        try:result.extend(validate_instances([item],catalog));continue
        except Problem as original:
            a=catalog.get(item['assetId']);candidate=copy.deepcopy(item)
            if item.get('assetHash')!=a['contentHash']:raise original
            def vec(v):return [v[k] for k in ('x','y','z')] if isinstance(v,dict) else list(v)
            try:p=vec(item['localPosition']);scale=vec(item['localScale'])
            except (KeyError,TypeError):raise original
            fixed=None
            for shrink in (1,.85,.7,.55,.4):
                for dx,dz in ((0,0),(0,.4),(0,-.4),(.4,0),(-.4,0),(0,.8),(0,-.8),(.8,0),(-.8,0)):
                    candidate['localPosition']=[p[0]+dx,0,p[2]+dz];candidate['localScale']=[max(.1,s*shrink) for s in scale]
                    try:fixed=validate_instances([candidate],catalog)[0];break
                    except Problem:pass
                if fixed:break
            if fixed is None:raise original
            result.append(fixed);repairs.append({'instanceId':item['instanceId'],'reason':str(original),'from':item['localPosition'],'to':fixed['localPosition'],'scale':fixed['localScale']})
    # Validate identities across the whole set, not just each individual footprint.
    result=validate_instances(result,catalog)
    missing=allowed-{i['assetId'] for i in result}
    if missing:raise Problem('布局遗漏了已规划的资源：'+', '.join(sorted(missing))+'；请重新安排，不能以空地代替。',409)
    return result,repairs
