"""Versioned local-player library. Metadata never substitutes for dependency checks."""
import copy
import hashlib
import json
import math
from pathlib import Path

from backend.home_creation.storage import canonical, fingerprint
from backend.store import Problem, now

LABELS = {
    'watering-can': ('固定浇水壶', ['工具','浇水','金属'], False, 'tool'),
    'hinged-chest': ('可开盖宝箱', ['宝箱','木材','开箱'], False, 'Chest'),
    'garden-tree': ('晨曦阔叶树', ['树木','绿','晨曦','花园'], True, None),
    'garden-seedling': ('晨曦幼苗', ['植物','幼苗','绿','浇水'], False, 'Water'),
    'moss-boulder': ('苔藓岩石', ['岩石','苔藓','灰','绿','花园'], True, None),
    'meadow-flower': ('草地小白花', ['花','白','草地','晨曦'], True, None),
    'stepping-stone': ('石板路', ['路','石头','灰','花园'], True, None),
    'tool-stump': ('工具树桩', ['树桩','木材','棕'], True, None),
    'travel-monument': ('晨曦水晶石碑', ['石碑','水晶','花园'], True, None),
    'ember-tree': ('暖焰树木', ['树木','橙','红','暖焰','火'], True, None),
    'ember-boulder': ('暖焰砂岩', ['岩石','砂岩','橙','暖焰'], True, None),
    'ember-seedling': ('暖焰幼苗', ['植物','幼苗','暖焰','浇水'], False, 'Water'),
    'ember-monument': ('暖焰石碑', ['石碑','暖焰','火'], True, None),
    'ember-tent-v2': ('暖焰营帐', ['帐篷','营地','暖焰','橙'], True, None),
    'mossy-stone-bench': ('苔石长凳', ['长凳','石凳','苔藓','奶油灰','花园'], True, None),
}


def collision_spec(asset):
    if asset.get('decorationOnly'):return {'kind':'none'}
    if asset.get('runtimeCollider'):return {'kind':'box','size':dict(zip(('x','y','z'),asset['runtimeCollider']['size'])),'center':dict(zip(('x','y','z'),asset['runtimeCollider']['center']))}
    if asset['hasCollider']:return {'kind':'prefab'}
    if not asset['placeable'] or asset['id'] in {'meadow-flower','stepping-stone'}:return {'kind':'none'}
    s,c=asset['size'],asset['center'];tree='tree' in asset['id']
    size={'x':s['x']*(.25 if tree else .85),'y':s['y']*(.5 if tree else .9),'z':s['z']*(.25 if tree else .85)}
    center={'x':c['x'],'y':c['y']-s['y']*.5+size['y']*.5,'z':c['z']}
    return {'kind':'box','size':size,'center':center}


class AssetCatalog:
    def __init__(self, repo, root):
        self.repo, self.root = repo, Path(root)
        with repo.lock, repo.db:
            repo.db.execute('CREATE TABLE IF NOT EXISTS catalog (id TEXT,version INTEGER,data TEXT,PRIMARY KEY(id,version))')

    def import_manifest(self, manifest):
        if not isinstance(manifest, dict) or manifest.get('schemaVersion') != 1 or not isinstance(manifest.get('assets'),list):
            raise Problem('资产目录格式无效。')
        result=[]
        for raw in manifest['assets']:
            aid=raw['id']; name,tags,placeable,interaction=LABELS.get(aid,(aid,['自制'],True,None))
            if not raw.get('dependencies') or not raw.get('prefabPath','').startswith('Assets/DawnGarden/'):
                raise Problem('资产缺少来源或工程依赖。')
            self.check_dependencies(raw)
            p=Path(raw['previewPath']).resolve()
            if self.root.resolve() not in p.parents or not p.is_file():raise Problem('资产预览缺失。')
            data={k:copy.deepcopy(raw[k]) for k in ['id','prefabPath','size','center','hasCollider','hasLod','materials','dependencies']}
            data.update(name=name,tags=tags,placeable=placeable,interactionKind=interaction,
                pivot='prefab-native',permissions=['reuse','transform','material-copy'],source={'kind':'unity-prefab','path':raw['prefabPath']})
            # Source identity is based on imported data, not timestamps or preview file IDs.
            digest=fingerprint(data)
            with self.repo.lock:
                old=self.get(aid,required=False)
                if old and old['contentHash']==digest:result.append(old);continue
                data.update(version=(old['version']+1 if old else 1),contentHash=digest,indexedAt=now(),
                    previewRef=self.repo.put('_library',p.read_bytes(),'png',name+'预览'))
                for run in self.repo.all():
                    prepared=next((a for a in (run.get('preparedAssets') or {}).values() if a.get('unity',{}).get('prefabPath')==raw['prefabPath']),None)
                    if prepared and prepared.get('unity',{}).get('prefabPath')==raw['prefabPath']:
                        data['source'].update(runId=run['id'],revision=run['revision'],preparation=prepared)
                        data['name']=prepared.get('name',data['name'])
                with self.repo.db:self.repo.db.execute('INSERT INTO catalog VALUES (?,?,?)',(aid,data['version'],canonical(data)))
                result.append(data)
        return result

    def get(self, aid, version=None, required=True):
        with self.repo.lock:
            row=self.repo.db.execute('SELECT data FROM catalog WHERE id=?'+(' AND version=?' if version is not None else '')+' ORDER BY version DESC LIMIT 1',
                (aid,version) if version is not None else (aid,)).fetchone()
        if not row:
            if required:raise Problem('资产不在目录中。',404)
            return None
        return json.loads(row[0])

    def check_dependencies(self, asset):
        if asset.get('source',{}).get('kind')=='runtime-glb':
            self.repo.path(asset['source']['modelRef'],asset['source']['runId']);return
        project=(self.root/'unity/DawnGarden').resolve()
        for dep in asset['dependencies']:
            path=dep.get('path','')
            if not path.startswith(('Assets/','Packages/')):raise Problem('非法资产依赖。')
            p=(project/path).resolve()
            if not p.is_file() and path.startswith('Packages/'):
                parts=Path(path).parts; cache=project/'Library/PackageCache'
                candidates=[(x/Path(*parts[2:])).resolve() for x in cache.glob(parts[1]+'@*')]
                candidates=[x for x in candidates if cache.resolve() in x.parents and x.is_file()]
                if len(candidates)==1:p=candidates[0]
            if project not in p.parents or not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest()!=dep.get('sha256'):
                raise Problem('资产已变化，请刷新资产目录后重新选用。',409,'STALE_ASSET')

    def search(self, query='', interaction=None, max_width=None, placeable=None, limit=50):
        with self.repo.lock:
            rows=self.repo.db.execute('SELECT c.data FROM catalog c JOIN (SELECT id,MAX(version) v FROM catalog GROUP BY id) m ON c.id=m.id AND c.version=m.v').fetchall()
        words=str(query).lower().split(); result=[]
        for row in rows:
            a=json.loads(row[0]); hay=' '.join([a['id'],a['name'],*a['tags'],*a['materials']]).lower()
            score=sum(1 for w in words if w in hay)
            if words and not score:continue
            if interaction and a['interactionKind']!=interaction:continue
            if placeable is not None and a['placeable']!=placeable:continue
            if max_width is not None and a['size']['x']>float(max_width):continue
            a['placementCollision']=collision_spec(a)
            result.append((score,a))
        return [a for _,a in sorted(result,key=lambda x:(-x[0],x[1]['id']))[:min(int(limit),100)]]

    def context(self, query=''):
        return [{k:a[k] for k in ['id','version','name','tags','size','interactionKind','placeable','hasCollider','hasLod','contentHash','placementCollision']} for a in self.search(query)]


def validate_instances(items, catalog):
    if not isinstance(items,list) or not 1<=len(items)<=50:raise Problem('请摆放 1–50 件物件。')
    result=[]; ids=set()
    for item in items:
        import re
        if not isinstance(item,dict) or not isinstance(item.get('instanceId'),str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,100}',item['instanceId']) or item['instanceId'] in ids:
            raise Problem('摆放实例 ID 无效或重复。')
        ids.add(item['instanceId']); a=catalog.get(item.get('assetId'),item.get('assetVersion'))
        if not a['placeable']:raise Problem('此资源是互动模板或工具，不能作为静态装饰摆放。')
        if item.get('assetHash')!=a['contentHash']:raise Problem('资产版本已变化，请重新选择。',409)
        def vector(key,lo,hi):
            v=item.get(key)
            if isinstance(v,dict):v=[v.get(k) for k in ('x','y','z')]
            if not isinstance(v,list) or len(v)!=3 or any(type(n) not in (float,int) or not math.isfinite(n) or not lo<=n<=hi for n in v):raise Problem('位置、旋转或缩放数值不合法。')
            return v
        p=vector('localPosition',-20,20); rot=vector('localEulerAngles',-360,360); scale=vector('localScale',.1,4)
        if abs(rot[0])>.01 or abs(rot[2])>.01 or not -.05<=p[1]<=.15:raise Problem('首版装饰落地摆放，只调整水平朝向。')
        # Conservative rotated footprint of the imported renderer bounds.
        sx,sz=a['size']['x']*scale[0]*.5,a['size']['z']*scale[2]*.5
        ang=math.radians(rot[1]); ex=abs(math.cos(ang))*sx+abs(math.sin(ang))*sz; ez=abs(math.sin(ang))*sx+abs(math.cos(ang))*sz
        cx=a['center']['x']*scale[0]; cz=a['center']['z']*scale[2]
        x=p[0]+cx*math.cos(ang)+cz*math.sin(ang); z=p[2]-cx*math.sin(ang)+cz*math.cos(ang)
        if any((xx/6.0)**2+(zz/5.0)**2>1 for xx in (x-ex,x+ex) for zz in (z-ez,z+ez)):
            raise Problem('物件超出土地轮廓，请向内移动或缩小。')
        if x-ex < -.5 and z-ez < .8 and z+ez > -.8:raise Problem('西侧入口与中央连接通路需要留空。')
        tint=item.get('tint','#ffffff')
        import re
        if not isinstance(tint,str) or not re.fullmatch(r'#[0-9a-fA-F]{6}',tint):raise Problem('材质颜色格式无效。')
        result.append(dict(instanceId=item['instanceId'],assetId=a['id'],assetVersion=a['version'],assetHash=a['contentHash'],
            sourceAssetRef=a['prefabPath'],localPosition=p,localEulerAngles=rot,localScale=scale,tint=tint))
    return result
