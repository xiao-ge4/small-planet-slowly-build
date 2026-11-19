"""Persistent NPC households, quests and keepsakes. Dialogue cannot mint rewards."""
import copy
import json
import math
from uuid import uuid4
from backend.store import Problem, now
from backend.home_creation.storage import canonical, fingerprint

RESIDENTS = [
    dict(id='ajin',ritualKind='heat',ritualTitle='炉边温热暖石',ritualHint='按住添火，松开时让温度停在暖色区域。',name='阿烬',homeName='暖焰营地',theme='ember',homeId='ember-camp-starter-v1',scene='EmberHomeV1',planet=0,continent=-1,
         personality='热情但不吵闹，喜欢温泉、篝火和培育暖焰花，说话朴实，不使用说教式口吻。',
         greeting='欢迎来营地。暖焰花正等一口温泉水，炉边也给你留了位置。',
         questTitle='温泉边的三颗暖石',questItemName='温润石',questDescription='三颗温润石散落在营地的小路旁。找到它们，再来找我，一起把它们做成纪念。',
         giftId='ajin-lantern',giftName='阿烬的暖焰灯',giftAssetId='ember-monument',giftTint='#e7b579',giftScale=.55),
    dict(id='lumi',ritualKind='stars',ritualTitle='记住落星的顺序',ritualHint='先看三颗星依次亮起，再按相同顺序唤醒它们。',name='露米',homeName='星露小筑',theme='dew',homeId='star-dew-neighbor-v2',scene='StarDewHomeV2',planet=1,continent=0,
         personality='安静、好奇，喜欢看星光和记录花开，不神秘兮兮，不催促玩家完成任务。',
         greeting='你来啦。昨夜有三点星光落进花园，我们可以边找边聊。',
         questTitle='落在花园的星光',questItemName='星露石',questDescription='帮花园浇一次水，再找到小路旁的三颗星露石。它们会变成一份可以带回家的纪念。',
         giftId='lumi-flowers',giftName='露米的星露花簇',giftAssetId='meadow-flower',giftTint='#9fc5ed',giftScale=.85),
    dict(id='lanlan',ritualKind='tune',ritualTitle='调出风铃的和弦',ritualHint='听一遍风铃示范，调整三枚音符，再一起奏响。',name='岚岚',homeName='风铃石庭',theme='wind',homeId='wind-court-neighbor-v2',scene='WindCourtHomeV2',planet=2,continent=1,
         personality='温和、爱观察微小事物，喜欢石头、风声和慢慢散步，允许安静相处。',
         greeting='欢迎到石庭来。风把三颗小石子藏在路边了，找累了就歇一会儿。',
         questTitle='风捎来的三颗小石子',questItemName='风铃石',questDescription='照料石庭的新芽，找齐三颗风铃石，再带给我。我想送你一块适合放在家里的小景石。',
         giftId='lanlan-stone',giftName='岚岚的风铃景石',giftAssetId='moss-boulder',giftTint='#d3bce2',giftScale=.4),
]


class NeighborWorld:
    def __init__(self,game):
        self.game=game;self.repo=game.repo
        with self.repo.lock,self.repo.db:
            self.repo.db.execute('CREATE TABLE IF NOT EXISTS game_residents (profile TEXT,id TEXT,data TEXT,PRIMARY KEY(profile,id))')
            self.repo.db.execute('CREATE TABLE IF NOT EXISTS game_neighbor_actions (profile TEXT,id TEXT,hash TEXT,data TEXT,PRIMARY KEY(profile,id))')

    def definition(self,client,npc):
        p=self.game.get_profile(client);item=next((r for r in RESIDENTS if r['id']==npc),None)
        if not item:raise Problem('这里还没有住户。',404)
        result=copy.deepcopy(item)
        if result['planet']==0:result['continent']=(p.get('ownContinent',p.get('states',{}).get('home',{}).get('continentIndex',0))+1)%4
        return result

    def state(self,client,npc):
        self.definition(client,npc)
        with self.repo.lock:
            row=self.repo.db.execute('SELECT data FROM game_residents WHERE profile=? AND id=?',(client,npc)).fetchone()
            if row:
                state=json.loads(row[0]);state.setdefault('ritualComplete',bool(state.get('giftClaimed')));return state
            result={'id':npc,'visits':0,'lastVisitDay':'','lastWateredDay':'','wateringDays':0,'greeted':False,'ritualComplete':False,'questAccepted':False,'collected':[],'giftClaimed':False,'friendship':0,'chatHistory':[],'createdAt':now()}
            self.save(client,result);return result

    def save(self,client,state):
        with self.repo.lock,self.repo.db:self.repo.db.execute('INSERT OR REPLACE INTO game_residents VALUES (?,?,?)',(client,state['id'],canonical(state)))

    def public(self,client,npc):
        from backend.game.service import day_key
        d=self.definition(client,npc);s=self.state(client,npc)
        seed=int(fingerprint({'profile':client,'npc':npc})[:6],16);pattern=[(seed+i)%3 for i in range(3)] if d['ritualKind']=='stars' else [(seed+i*2)%5 for i in range(3)]
        return {k:v for k,v in d.items() if k!='personality'} | {k:copy.deepcopy(s[k]) for k in ['visits','lastWateredDay','wateringDays','greeted','questAccepted','collected','giftClaimed','friendship','chatHistory','ritualComplete']} | {'ritualPattern':pattern,'chatHistory':self.game.history(client,npc),'wateredToday':s['lastWateredDay']==day_key(),'canReceiveGift':s['greeted'] and s['wateringDays']>0 and len(s['collected'])==3 and s['ritualComplete'] and not s['giftClaimed']}

    def view(self,client):
        self.game.get_profile(client)
        return {'residents':[self.public(client,r['id']) for r in RESIDENTS],'gifts':self.gifts(client),'travelFree':True}

    def at(self,client,planet,continent):
        return next((self.definition(client,r['id']) for r in RESIDENTS if self.definition(client,r['id'])['planet']==planet and self.definition(client,r['id'])['continent']==continent),None)

    def gifts(self,client):
        return [{'id':d['giftId'],'name':d['giftName'],'assetId':d['giftAssetId'],'tint':d['giftTint'],'scale':d['giftScale'],'fromName':d['name'],'residentId':d['id']} for d in RESIDENTS if self.state(client,d['id'])['giftClaimed']]

    def require_gift(self,client,gift_id,asset_id):
        gift=next((g for g in self.gifts(client) if g['id']==gift_id and g['assetId']==asset_id),None)
        if not gift:raise Problem('这份纪念物还没有收到。',409)
        return gift

    def present(self,client,npc,near=None):
        p=self.game.get_profile(client);d=self.definition(client,npc);s=p['states'].get(p.get('lastPlot'),{})
        if not s.get('isVisitor') or s.get('planetIndex')!=d['planet'] or s.get('continentIndex')!=d['continent']:raise Problem('先到这位邻居的家园，再一起互动。',409)
        if near is not None:
            pos=s.get('actorPosition');target=near
            if not isinstance(pos,dict) or any(type(pos.get(k)) not in (int,float) or not math.isfinite(pos[k]) for k in ('x','y','z')):raise Problem('还没同步精灵的位置，稍后再试。',409)
            if math.dist((pos['x'],pos['z']),(target[0],target[2]))>2.4:raise Problem('先走近一点。',409)
        return d,s

    def register_geometry(self,client,npc,body):
        """Only the bundled scene may register reachable anchors once for its build."""
        d,_=self.present(client,npc);manifest=self.game.root/'artifacts/game-v1/build-manifest.json'
        if not manifest.is_file() or body.get('buildId')!=json.loads(manifest.read_text()).get('buildId'):raise Problem('场景版本已更新，请使用新版游戏。',409)
        def vector(v):
            if not isinstance(v,dict) or any(type(v.get(k)) not in (float,int) or not math.isfinite(v[k]) for k in ('x','y','z')):raise Problem('场景锚点无效。')
            if abs(v['x'])>5.6 or abs(v['z'])>4.7 or abs(v['y'])>1:raise Problem('场景锚点超出家园。')
            return [v[k] for k in ('x','y','z')]
        geometry={'resident':vector(body.get('resident')),'plant':vector(body.get('plant')),'items':[vector(v) for v in body.get('items',[])]}
        geometry['ritual']=vector(body['ritual']) if body.get('ritual') is not None else geometry['resident']
        if len(geometry['items'])!=3:raise Problem('寻物点需要三个。')
        with self.repo.lock:
            s=self.state(client,npc)
            if s.get('geometryBuild')==body['buildId']:
                if s['geometry']!=geometry:raise Problem('本版家园的锚点已经确定。',409)
            else:s.update(geometry=geometry,geometryBuild=body['buildId']);self.save(client,s)
        return self.public(client,npc)

    def record_visit(self,client,npc):
        from backend.game.service import day_key
        self.present(client,npc)
        with self.repo.lock:
            s=self.state(client,npc)
            if s['lastVisitDay']!=day_key():s.update(lastVisitDay=day_key(),visits=s['visits']+1);self.save(client,s)
        return self.public(client,npc)

    def action(self,body):
        from backend.game.service import day_key
        client=body.get('clientId');npc=body.get('residentId');rid=body.get('requestId');kind=body.get('action');self.game.client_id(client)
        if not isinstance(rid,str) or not 8<=len(rid)<=100:raise Problem('互动请求标识无效。')
        fp=fingerprint(body)
        with self.repo.lock:
            old=self.repo.db.execute('SELECT hash,data FROM game_neighbor_actions WHERE profile=? AND id=?',(client,rid)).fetchone()
            if old:
                if old[0]!=fp:raise Problem('互动请求标识冲突。',409)
                result=json.loads(old[1]);result['resident']=self.public(client,npc);result['gifts']=self.gifts(client);return result
            d,current=self.present(client,npc);s=self.state(client,npc);geo=s.get('geometry')
            if not geo:raise Problem('家园还在准备中。',409)
            reply='';event=None
            if kind in ('greet','accept_quest','receive_gift'):
                self.present(client,npc,geo['resident'])
            if kind=='greet':
                first=not s['greeted'];s['greeted']=True
                reply=d['greeting'] if first else ('今天的新芽已经喝过水了，陪我坐一会儿、聊聊天也很好。' if s['lastWateredDay']==day_key() else f"又见面了。我们已经一起照料过{s['wateringDays']}次新芽。今天也可以慢慢来。")
                event=('greet',f"和{d['name']}在{d['homeName']}打了招呼")
            elif kind=='accept_quest':
                s['questAccepted']=True;reply=d['questDescription'];event=('quest',f"接下{d['name']}的「{d['questTitle']}」委托")
            elif kind=='care':
                self.present(client,npc,geo['plant'])
                if not current.get('wateredToday') or current.get('lastWateredDay')!=day_key():raise Problem('先完成这次照料。',409)
                if s['lastWateredDay']!=day_key():
                    s.update(lastWateredDay=day_key(),wateringDays=s['wateringDays']+1,friendship=s['friendship']+1)
                    event=('care',f"帮助{d['name']}照料了{d['homeName']}的新芽")
                reply='谢谢你，新芽精神多了。'+('想不想顺便找找散落的小物？' if not s['questAccepted'] else '我们可以继续找路边的小物。')
            elif kind=='collect':
                index=body.get('itemIndex')
                if not s['questAccepted'] or type(index)!=int or index not in range(3):raise Problem('先和邻居聊聊这份委托。',409)
                self.present(client,npc,geo['items'][index])
                if index not in s['collected']:s['collected'].append(index);event=('collect-'+str(index),f"找到{d['name']}委托中的一颗{d['questItemName']}")
                reply=f"找到{len(s['collected'])}/3。"+('可以回去和邻居分享了。' if len(s['collected'])==3 else '')
            elif kind=='ritual':
                if not s['questAccepted'] or len(s['collected'])!=3:raise Problem('先找齐三件小物，再一起完成这一步。',409)
                self.present(client,npc,geo.get('ritual',geo['resident']))
                public=self.public(client,npc)
                if d['ritualKind']=='heat':
                    value=body.get('value');duration=body.get('duration')
                    if type(value) not in (int,float) or not math.isfinite(value) or not .55<=value<=.8 or type(duration) not in (int,float) or not math.isfinite(duration) or not 1.5<=duration<=15:raise Problem('暖石还没到合适的温度，再试一次吧。',409)
                elif body.get('pattern')!=public['ritualPattern']:raise Problem('还差一点，再听听、看看示范吧。',409)
                if not s['ritualComplete']:s['ritualComplete']=True;event=('ritual',f"和{d['name']}完成了「{d['ritualTitle']}」")
                reply={'heat':'暖石温温的，刚刚好。带回来和我分享吧。','stars':'三颗星接上了彼此的光，谢谢你记住它们。','tune':'风铃合上了和弦。带上这段声音，来和我聊聊吧。'}[d['ritualKind']]
            elif kind=='receive_gift':
                if not s['greeted'] or not s['wateringDays'] or len(s['collected'])!=3 or not s['ritualComplete']:raise Problem('照料新芽、找齐小物并完成这次合作后，再来分享吧。',409)
                if not s['giftClaimed']:s.update(giftClaimed=True,friendship=s['friendship']+2);event=('gift',f"收到{d['name']}赠送的「{d['giftName']}」，可在自己的家园反复摆放")
                reply=f"这份「{d['giftName']}」送给你。把它放在家里时，也想起我们一起度过的这段时间吧。"
            else:raise Problem('这项互动还不能进行。')
            self.save(client,s)
            if event:self.game.record_shared_event(client,'neighbor:'+npc+':'+event[0]+(':'+day_key() if kind in ('care','greet') else ''),event[1],kind='neighbor')
            result={'resident':self.public(client,npc),'reply':reply,'gifts':self.gifts(client)}
            with self.repo.db:self.repo.db.execute('INSERT INTO game_neighbor_actions VALUES (?,?,?,?)',(client,rid,fp,canonical(result)))
            return result
