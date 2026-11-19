"""Context-bound, metered home ideas with safe local fallback; never generates art."""
import copy
from backend.home_creation.storage import fingerprint
from backend.store import Problem


def context_for(game,client):
    p=game.get_profile(client);memory=game.memories(client);layout=game.layout(client)
    prefs=[{'id':m['id'],'text':m['text']} for m in memory['preferences']]
    assets=[{'id':a['id'],'name':a['name']} for a in game.catalog.search(placeable=True)]
    gifts=game.neighbors.gifts(client)
    context={'profileId':client,'theme':p['theme'],'preferences':prefs,'layout':layout.get('instances',[]),'gifts':gifts,'assets':assets,
             'pastScenes':[{'idea':m.get('idea',''),'assets':m.get('assets',[])} for m in memory['scenes'][:3]]}
    # Ignore timestamps and dialogue recency: reopening the same idea does not spend again.
    context['bindingHash']=fingerprint(context)
    return context


def fallback_ideas(context):
    available={a['id']:a['name'] for a in context['assets']};theme=context['theme'];prefs=context['preferences']
    quote='；'.join(p['text'] for p in prefs)[:240]
    existing=context['layout'];by_id={i['assetId'] for i in existing};gifts=context['gifts']
    def pick(ids):return [i for i in ids if i in available][:4]
    warm=theme=='ember'
    stone=any(k in quote for k in ('石','浅色','米白','简洁'))
    flowers=any(k in quote for k in ('花','绿','植物','自然'))
    main=pick((['mossy-stone-bench','moss-boulder','meadow-flower'] if stone else ['meadow-flower','garden-tree','moss-boulder']))
    if not main:main=list(available)[:3]
    anchor=('保留现在的'+ '、'.join(available.get(x,x) for x in list(by_id)[:3])+'，只补充空白处。') if existing else '保留入口和中央的活动空间。'
    preference=('依据你明确说过的「'+quote+'」。') if quote else ('延续暖色温泉营地的氛围。' if warm else '延续晨光、草地和海风的氛围。')
    recipes=[
      {'id':'continue','title':('花间休憩角' if flowers else '苔石休憩角'),'description':anchor+'用'+ '、'.join(available[i] for i in main)+'形成小小停留区。','idea':anchor+'布置一个安静休憩角，'+preference+'优先复用已有物件，不生成新模型。','assetIds':main,'why':preference},
      {'id':'contrast','title':'清凉绿洲' if warm else '暖色会客角','description':('在暖色家园旁做一处绿意留白，能从温泉区慢慢走过去。' if warm else '在绿意旁加入暖色石景，留出一起聊天的位置。')+anchor,'idea':anchor+('新土地做清凉绿洲，采用绿植和浅色石头。' if warm else '新土地做暖色会客角，以暖色石头和一小片花丛组成。')+preference+'仅复用资产库，可改色。','assetIds':pick(['meadow-flower','moss-boulder','ember-monument']),'why':'根据当前家园主题提供互补方向；'+('保留现有摆放。' if existing else '适合尚未布置的地块。')},
      {'id':'keepsakes','title':(gifts[0]['fromName']+'的纪念角' if gifts else '旅途纪念角'),'description':('围绕已经收到的「'+gifts[0]['name']+'」布置，不消耗这份礼物。' if gifts else '为以后从邻居家带回的纪念物留一个位置。')+anchor,'idea':anchor+('以「'+gifts[0]['name']+'」为中心，' if gifts else '为邻居的纪念物预留一个角落，')+'用已有花草和景石做旅途纪念角。'+preference+'不生成新模型。','assetIds':pick([g['assetId'] for g in gifts]+['meadow-flower','moss-boulder']),'why':('你已经拥有邻居的纪念物，可重复摆放。' if gifts else '你可以通过访问邻居获得纪念物，再补进这里。')}
    ]
    return recipes


def validate_ideas(value,context):
    if not isinstance(value,list) or len(value)!=3:return fallback_ideas(context)
    ids={a['id'] for a in context['assets']};result=[]
    for i,idea in enumerate(value):
        if not isinstance(idea,dict) or any(not isinstance(idea.get(k),str) or not 1<=len(idea[k])<=limit for k,limit in [('title',40),('description',280),('idea',1800),('why',300)]):return fallback_ideas(context)
        selected=idea.get('assetIds')
        if not isinstance(selected,list) or len(selected)>5 or any(not isinstance(a,str) or a not in ids for a in selected):return fallback_ideas(context)
        result.append({**{k:idea[k] for k in ('title','description','idea','why','assetIds')},'id':'idea-'+str(i)})
    return result
