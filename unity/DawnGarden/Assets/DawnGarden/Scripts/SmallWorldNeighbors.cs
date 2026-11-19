using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    public sealed class SmallWorldWorldTarget:MonoBehaviour {public string kind;public int index;public Vector3 approach;}
    public sealed partial class SmallWorldApp
    {
        [Serializable] public class Resident
        {
            public string id,name,homeName,theme,scene,greeting,questTitle,questDescription,questItemName,lastWateredDay,giftName,ritualKind,ritualTitle,ritualHint;public int[] ritualPattern;public bool ritualComplete;
            public int planet,continent,wateringDays,visits,friendship;public bool greeted,questAccepted,giftClaimed,wateredToday,canReceiveGift;
            public int[] collected;public GuideLine[] chatHistory;
        }
        [Serializable] public class Gift {public string id,name,assetId,tint,fromName,residentId;public float scale;}
        [Serializable] public class NeighborReply {public Resident resident;public Gift[] gifts;public string reply;}
        [Serializable] public class WorldReply {public Resident[] residents;public Gift[] gifts;public bool travelFree;}
        [Serializable] public class NeighborRequest {public string clientId,residentId,requestId,action;public int itemIndex;public float value,duration;public int[] pattern;}
        [Serializable] public class GeometryRequest {public string clientId,residentId,buildId;public Vector3 resident,plant,ritual;public Vector3[] items;}
        WorldReply neighborWorld=new WorldReply {residents=new Resident[0],gifts=new Gift[0]};
        Resident CurrentResident=>neighborWorld.residents?.FirstOrDefault(r=>r.planet==planetIndex&&r.continent==continentIndex);
        Resident AtResident(int p,int c)=>neighborWorld.residents?.FirstOrDefault(r=>r.planet==p&&r.continent==c);
        GameObject residentActor;Vector3 residentAnchor;SmallWorldWorldTarget pendingWorldTarget;
        List<SmallWorldWorldTarget> worldTargets=new List<SmallWorldWorldTarget>();
        bool neighborBusy,residentReady,neighborPreparing;int neighborSceneEpoch;string lastNeighborError="";string chatResidentId;List<GuideLine> guideLines=new List<GuideLine>();float bubbleUntil;bool showOptions;
        IEnumerator LoadNeighbors(){yield return SmallWorldApi.Request("world/"+Profile.clientId,null,j=>neighborWorld=JsonUtility.FromJson<WorldReply>(j),Toast);}
        void UpdateResident(Resident r)
        {
            if(r==null)return;var index=Array.FindIndex(neighborWorld.residents,x=>x.id==r.id);if(index>=0)neighborWorld.residents[index]=r;
            foreach(var target in worldTargets)if(target!=null&&target.kind=="search")
            {
                target.gameObject.SetActive(r.questAccepted&&!r.collected.Contains(target.index));
                if(target.gameObject.activeInHierarchy)foreach(var hit in target.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(Game.body,hit);
            }
        }
        Vector3 Reachable(Vector3 target)
        {
            var path=new NavMeshPath();
            for(float radius=.2f;radius<2.1f;radius+=.3f)if(NavMesh.SamplePosition(target,out var sample,radius,NavMesh.AllAreas)&&NavMesh.CalculatePath(Game.spawn.position,sample.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return sample.position;
            return Game.spawn.position;
        }
        IEnumerator SetupResident()
        {
            var epoch=++neighborSceneEpoch;residentReady=false;neighborPreparing=true;neighborBusy=false;lastNeighborError="";
            foreach(var old in worldTargets.Where(t=>t!=null)){if(old.kind=="search")Destroy(old.gameObject);else if(old.kind=="ritual")Destroy(old);}
            worldTargets.Clear();if(residentActor!=null)Destroy(residentActor);residentActor=null;yield return null;
            if(epoch!=neighborSceneEpoch||Game==null)yield break;
            var r=CurrentResident;if(r==null){neighborPreparing=false;yield break;}
            // All anchors are deterministic for this bundled scene and selected on its navigation surface.
            residentAnchor=Reachable(new Vector3(1.6f,.05f,1.8f));
            var residentPrefab = r.id == "ajin" ? Resources.Load<GameObject>("Homecoming/Ajin") :
                r.id == "lumi" ? Resources.Load<GameObject>("Neighbors/Lumi") :
                r.id == "lanlan" ? Resources.Load<GameObject>("Neighbors/Lanlan") : null;
            residentActor=Instantiate(residentPrefab != null ? residentPrefab : Game.actor.gameObject,residentAnchor,Quaternion.Euler(0,180,0));residentActor.name=r.name+" · 驻场精灵";
            foreach(var c in residentActor.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var agent in residentActor.GetComponentsInChildren<NavMeshAgent>())agent.enabled=false;
            var animator=residentActor.GetComponentInChildren<Animator>();if(animator!=null){animator.applyRootMotion=false;animator.Play("Idle");}
            if (residentPrefab == null)
            {
                var response=residentActor.GetComponent<SmallWorldPetResponse>()??residentActor.AddComponent<SmallWorldPetResponse>();response.Bind(Game,animator,true);
            }
            var hit=residentActor.AddComponent<CapsuleCollider>();hit.center=new Vector3(0,.55f,0);hit.height=1.1f;hit.radius=.3f;
            // These dynamic hit areas remain clickable without blocking a route baked before they existed.
            Physics.IgnoreCollision(Game.body,hit);
            var npc=residentActor.AddComponent<SmallWorldWorldTarget>();npc.kind="resident";worldTargets.Add(npc);
            var spots=new[]{new Vector3(-.8f,.05f,2.1f),new Vector3(2.6f,.05f,-.4f),new Vector3(-2.7f,.05f,.8f)};
            var items=spots.Select(Reachable).ToArray();
            for(int i=0;i<3;i++)
            {
                var a=library.assets.FirstOrDefault(x=>x.id=="moss-boulder");var go=a!=null&&a.prefab!=null?Instantiate(a.prefab):GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name=r.questItemName+" "+(i+1);go.transform.position=items[i]+Vector3.up*.04f;
                var bounds=go.GetComponentsInChildren<Renderer>().First().bounds;go.transform.localScale*=.34f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                foreach(var c in go.GetComponentsInChildren<Collider>())Destroy(c);
                var cnew=go.AddComponent<SphereCollider>();cnew.radius=.26f/go.transform.lossyScale.x;cnew.center=Vector3.up*.12f/go.transform.lossyScale.y;
                Physics.IgnoreCollision(Game.body,cnew);
                SmallWorldAssetLoader.Tint(go,r.theme=="ember"?"#f5b78d":r.theme=="dew"?"#aacff2":"#d6c2e5");
                var t=go.AddComponent<SmallWorldWorldTarget>();t.kind="search";t.index=i;worldTargets.Add(t);go.SetActive(r.questAccepted&&!r.collected.Contains(i));
            }
            CreateRitualTarget();
            yield return SyncState();if(epoch!=neighborSceneEpoch)yield break;bool geometryReady=false;
            yield return SmallWorldApi.Request("neighbor-geometry",new GeometryRequest {clientId=Profile.clientId,residentId=r.id,buildId=library.buildId,resident=residentAnchor,plant=Game.FindInteraction(GardenInteractionKind.Water).approach.position,items=items,ritual=ritualAnchor},_=>geometryReady=true,NeighborError);
            if(epoch!=neighborSceneEpoch)yield break;
            yield return SmallWorldApi.Request("neighbor-visit",new NeighborRequest {clientId=Profile.clientId,residentId=r.id},j=>{if(epoch==neighborSceneEpoch)UpdateResident(JsonUtility.FromJson<Resident>(j));},NeighborError);
            if(epoch==neighborSceneEpoch){residentReady=geometryReady;neighborPreparing=false;}
        }
        public bool TryWorldTarget(Collider collider)
        {
            var target=collider.GetComponentInParent<SmallWorldWorldTarget>();if(target==null)return false;
            QueueWorldTarget(target);return true;
        }
        public bool TryNearbyWorldTarget()
        {
            var near=worldTargets.Where(t=>t!=null&&t.gameObject.activeSelf&&Vector3.Distance(Game.actor.position,t.transform.position)<1.7f).OrderBy(t=>Vector3.Distance(Game.actor.position,t.transform.position)).FirstOrDefault();
            if(near==null)return false;QueueWorldTarget(near);return true;
        }
        void QueueWorldTarget(SmallWorldWorldTarget target)
        {
            if(!residentReady){Toast("邻居家园还在准备，稍等一下。");return;}if(neighborBusy||Game.Busy||ritualOpen)return;
            if(Vector3.Distance(Game.actor.position,target.kind=="ritual"?target.approach:target.transform.position)<1.8f){StartCoroutine(UseWorldTarget(target));return;}
            var direction=(Game.actor.position-target.transform.position).normalized;var point=target.kind=="ritual"?target.approach:Reachable(target.transform.position+direction*.9f);
            if(Game.WalkTo(point)){pendingWorldTarget=target;Toast(target.kind=="resident"?"走过去和"+CurrentResident.name+"聊聊":"走过去看看这颗小石子");}
        }
        void UpdateNeighborInput()
        {
            if(!IsVisitor||Game==null)return;
            if(residentActor!=null&&ChatOpen&&chatResidentId!=null){var v=Game.actor.position-residentActor.transform.position;v.y=0;if(v.sqrMagnitude>.01f)residentActor.transform.rotation=Quaternion.Slerp(residentActor.transform.rotation,Quaternion.LookRotation(v),Time.deltaTime*3);}
            if(pendingWorldTarget==null)return;
            if(Keyboard.current!=null&&(Keyboard.current.wKey.isPressed||Keyboard.current.aKey.isPressed||Keyboard.current.sKey.isPressed||Keyboard.current.dKey.isPressed||Keyboard.current.escapeKey.wasPressedThisFrame)){pendingWorldTarget=null;return;}
            if(!Game.Navigating&&!Game.Busy){var t=pendingWorldTarget;pendingWorldTarget=null;if(Vector3.Distance(Game.actor.position,t.kind=="ritual"?t.approach:t.transform.position)<2.4f)StartCoroutine(UseWorldTarget(t));}
        }
        IEnumerator UseWorldTarget(SmallWorldWorldTarget target)
        {
            if(target.kind=="resident")
            {
                chatResidentId=CurrentResident.id;chatLines=(CurrentResident.chatHistory??new GuideLine[0]).ToList();lastTurn=null;ChatOpen=true;UpdateControls();
                yield return NeighborAction("greet",-1,true);
            }
            else if(target.kind=="ritual")OpenRitual();
            else if(target.kind=="search")
                yield return Game.SmallGesture(()=>StartCoroutine(NeighborAction("collect",target.index)));
        }
        IEnumerator NeighborCare(){yield return SyncState();yield return NeighborAction("care");}
        IEnumerator NeighborAction(string action,int item=-1,bool append=false)
        {
            if(neighborBusy||CurrentResident==null||!residentReady)yield break;neighborBusy=true;lastNeighborError="";var epoch=neighborSceneEpoch;var residentId=CurrentResident.id;var id=Guid.NewGuid().ToString("N");
            yield return SyncState();
            yield return SmallWorldApi.Request("neighbor-action",new NeighborRequest {clientId=Profile.clientId,residentId=residentId,requestId=id,action=action,itemIndex=item},j=>{
                if(epoch!=neighborSceneEpoch)return;var reply=JsonUtility.FromJson<NeighborReply>(j);UpdateResident(reply.resident);neighborWorld.gifts=reply.gifts;
                if(append||ChatOpen&&chatResidentId!=null){chatLines.Add(new GuideLine {role="assistant",text=reply.reply});chatScroll.y=100000;}else Toast(reply.reply);
                if(action=="greet"||action=="care"||action=="ritual"||action=="receive_gift"){residentActor?.GetComponent<SmallWorldAjinActor>()?.Respond(action);residentActor?.GetComponent<SmallWorldPetResponse>()?.Respond(action=="greet"?"greet":"thanks");Game.actor.GetComponent<SmallWorldPetResponse>()?.Respond(action=="receive_gift"?"gift":action=="greet"?"greet":"thanks");if(action!="care")SmallWorldPetResponse.Chime(Game,CurrentResident.theme,action);}
                if(action=="receive_gift")Toast("收到 "+CurrentResident.giftName+" · 回家布置时可无限复用");
            },NeighborError);if(epoch==neighborSceneEpoch)neighborBusy=false;
        }
        void NeighborError(string error){lastNeighborError=error;Toast(error);Debug.LogWarning("Neighbor interaction: "+error);}
        void DrawNeighborCard()
        {
            var r=CurrentResident;if(r==null)return;
            GUILayout.Label(r.homeName+" · 共同经历 "+r.friendship,label);
            if(r.giftClaimed){GUILayout.Label("纪念物已收下。每天仍可以来照料、聊天。",muted);return;}
            GUILayout.Label(r.questTitle,label);GUILayout.Label("照料 "+(r.wateringDays>0?"✓":"○")+"  ·  寻物 "+r.collected.Length+" / 3",muted);
            GUI.enabled=residentReady&&!neighborBusy&&!chatPending;
            if(!r.questAccepted&&GUILayout.Button("有什么需要我帮忙的？",button))StartCoroutine(NeighborAction("accept_quest",-1,true));
            if(r.questAccepted&&r.collected.Length==3&&!r.ritualComplete){GUILayout.Label("接下来一起完成："+r.ritualTitle,muted);if(GUILayout.Button("去"+r.ritualTitle,button)){ChatOpen=false;UpdateControls();QueueWorldTarget(ritualTarget);}}
            if(r.canReceiveGift&&GUILayout.Button("分享找到的小物",button))StartCoroutine(NeighborAction("receive_gift",-1,true));
            GUI.enabled=true;
        }
        void DrawNeighborLabels()
        {
            var resident = CurrentResident;
            if (loading || !residentReady || resident == null || Game?.worldCamera == null) return;
            foreach(var t in worldTargets.Where(t=>t!=null&&t.gameObject.activeSelf&&(t.kind!="ritual"||resident.questAccepted&&resident.collected.Length==3&&!resident.ritualComplete)))
            {
                var p=Game.worldCamera.WorldToScreenPoint(t.transform.position+Vector3.up*(t.kind=="resident"?1.85f:.45f));if(p.z<0)continue;var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
                var text=t.kind=="resident"?resident.name:t.kind=="ritual"?resident.ritualTitle:resident.questItemName;
                var width=Mathf.Clamp(Mathf.Ceil(button.CalcSize(new GUIContent(text)).x)+16,116,320);
                var x=Mathf.Clamp(p.x/scale-width*.5f,8,1272-width);
                if(GUI.Button(new Rect(x,(Screen.height-p.y)/scale,width,36),text,button))QueueWorldTarget(t);
            }
        }
        void DrawCompactHud()
        {
            var pin = new Rect(22,20,JourneyAvailable ? 294 : 235,64);
            GUI.Box(pin,GUIContent.none,hudPill);
            GUI.Label(new Rect(36,24,266,29),Game.homeTitle+(IsVisitor?" · "+CurrentResident?.name:""),hudTitle);
            GUI.Label(new Rect(37,52,260,22),Game.atmosphere.ClockLabel+"  ·  "+Game.atmosphere.Phase,hudNote);
            GUILayout.BeginArea(new Rect(982,20,276,65),hudPill);GUILayout.BeginHorizontal();GUI.enabled=!loading&&!Game.Busy&&!homecomingJournal;
            if(GUILayout.Button("地图",button))OpenWorld();if(GUILayout.Button("锦囊",button)){gameSettingsOpen=false;Game.expansion.OpenBag();UpdateControls();}if(GUILayout.Button("设置",secondaryButton,GUILayout.Width(68))){gameSettingsOpen=!gameSettingsOpen;if(gameSettingsOpen)Game.CancelMovement();UpdateControls();}GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.EndArea();
            if(JourneyAvailable&&!homecomingJournal&&GUI.Button(new Rect(22,92,135,38),Journey==JourneyStep.Complete?"旅行手记 ✓":"旅行手记",button))ToggleHomecomingJournal();
            if(IsVisitor&&!residentReady){GUI.Label(new Rect(25,90,500,36),neighborPreparing?"正在准备邻居家园…":lastNeighborError,label);if(!neighborPreparing&&GUI.Button(new Rect(25,130,170,38),"重试连接",button))StartCoroutine(SetupResident());}
            if(IsVisitor&&CurrentResident?.questAccepted==true&&!CurrentResident.giftClaimed)
            {
                var questY=JourneyAvailable?140:90;var found=CurrentResident.collected.Length;
                GUI.Box(new Rect(22,questY,470,42),GUIContent.none,hudPill);
                GUI.Label(new Rect(36,questY+8,442,27),CurrentResident.questItemName+" "+found+" / 3 · "+(found==3?"已找齐，去找"+CurrentResident.name:"点击路边小石收集"),hudNote);
            }
            if(!homecomingJournal&&(Game.atmosphere==null||!Game.atmosphere.ShowControls))
            {
                if(Time.unscaledTime<bubbleUntil&&!string.IsNullOrEmpty(bubble)){GUI.Box(new Rect(890,523,368,65),GUIContent.none,hudPill);GUI.Label(new Rect(907,532,332,51),bubble,hudNote);}
                if(GUI.Button(new Rect(1110,611,148,43),guide.name+" · 聊聊",coverPill))OpenChat();
            }
            if(GUI.Button(new Rect(22,605,125,42),Game.orbit.overview?"跟随精灵":"看看家园",button))Game.orbit.ToggleOverview();
            if(GUI.Button(new Rect(22,652,125,30),"操作帮助",secondaryButton))Toast("WASD 移动 · 点击地面寻路、点击物件互动 · E 附近互动 · 右键旋转 · 滚轮缩放");
            var near=Game.interactions.FirstOrDefault(i=>Game.InRange(i)&&!Game.IsDone(i));
            if(near!=null&&GUI.Button(new Rect(490,605,300,44),near.displayName+" [E]",button))Game.TryInteract(near);
            else if (near == null) DrawCoastPrompt();
            if(JourneyAvailable&&!IsVisitor&&!homecomingJournal&&Journey==JourneyStep.Light&&NearLamp()&&Game.atmosphere.Daylight<.4f
                && GUI.Button(new Rect(490,605,300,44),"点亮暖焰灯 [E]",coverPill))TryNearbyHomecomingLamp();

            DrawGuardianWorldPrompt();
            DrawPetResponseCaption();
        }
        bool landWasRising;
        void UpdateLandResponse()
        {
            if(Game==null)return;
            if(landWasRising&&!Game.expansion.IsRising){Game.actor.GetComponent<SmallWorldPetResponse>()?.Respond("newland");bubble="新土地准备好了。来聊聊你想留给它的模样，我会结合我们的记忆想三个方向。";bubbleUntil=Time.unscaledTime+18;}
            landWasRising=Game.expansion.IsRising;
        }
        void DrawPetResponseCaption()
        {
            var response=Game.actor.GetComponent<SmallWorldPetResponse>();if(response==null||string.IsNullOrEmpty(response.Caption))return;
            var point=Game.worldCamera.WorldToScreenPoint(Game.actor.position+Vector3.up*2.25f);var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            if(point.z>0)
            {
                var caption=new GUIStyle(hudNote){alignment=TextAnchor.MiddleCenter,wordWrap=false};
                var width=Mathf.Clamp(Mathf.Ceil(caption.CalcSize(new GUIContent(response.Caption)).x)+24,110,310);
                var card=new Rect(Mathf.Clamp(point.x/scale-width*.5f,8,1272-width),(Screen.height-point.y)/scale-12,width,32);
                GUI.Box(card,GUIContent.none,hudPill);
                GUI.Label(new Rect(card.x+10,card.y+3,card.width-20,26),response.Caption,caption);
            }
        }
    }
}
