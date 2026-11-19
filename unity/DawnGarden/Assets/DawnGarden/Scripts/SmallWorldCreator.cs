using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        [Serializable] public class CreateRequest
        {public string clientId,idea;public float imageBudgetUsd=.3f,planningBudgetCny=2;public int maxGeneratedAssets;public bool autoFlow=true,replaceExisting;}
        [Serializable] public class CreationDesign {public string assetId,name,url;}
        [Serializable] public class CreationPreparation {public string state,message;}
        [Serializable] public class CreationState
        {
            public string id,phase,status,title,idea,conceptUrl,conceptHash,batchHash;
            public string[] questions;
            public CreationDesign[] designs;
            public float usedUsd,usedCny;
            public int modelCount,revision;
            public bool runtimeFinalized,isQuickProject,paused,cancelled,autoFlow,replaceExisting,visualReviewRequired;
            public CreationPreparation runtimePreparation;
            public SmallWorldItem[] initialLayout;
            public string surfaceStyle;
        }
        [Serializable] public class CreationConfirm {public string gate,bindingHash,requestId;public int unknownModelTasks;}
        [Serializable] public class CreationInstruction {public string instruction,commandId;}
        [Serializable] public class RunRequest {public string clientId,runId;}
        bool creatorOpen,creationBusy,allowNewModel,unknownModelConfirmed;
        string creationIdea="",creationRevision="",lastCreationPhase="",lastCreationError="";
        CreationState creation;Vector2 creationScroll;float nextCreationPoll;
        Dictionary<string,Texture2D> creatorImages=new Dictionary<string,Texture2D>();HashSet<string> imageLoads=new HashSet<string>();
        void PollCreationIfNeeded()
        {
            if(!string.IsNullOrEmpty(Profile?.creationRunId)&&Time.unscaledTime>nextCreationPoll)
            {nextCreationPoll=Time.unscaledTime+(ChatOpen?3:12);StartCoroutine(RefreshCreation());}
        }
        IEnumerator RefreshCreation()
        {
            if(string.IsNullOrEmpty(Profile.creationRunId)||!Connected)yield break;
            yield return SmallWorldApi.Request("creation/"+Profile.clientId+"/"+Profile.creationRunId,null,json=>{
                var next=JsonUtility.FromJson<CreationState>(json);if(creation?.id!=next.id||creation?.batchHash!=next.batchHash)unknownModelConfirmed=false;creation=next;
                if(lastCreationPhase!=creation.phase && (creation.phase=="awaiting_concept"||creation.phase=="awaiting_batch"||creation.phase=="runtime_editing"))bubble=creation.phase=="runtime_editing"?"物件已经准备好了，随时可以来摆放预览。":"新一版设计准备好了。方便时一起看看，再决定下一步。";
                if(lastCreationPhase!=creation.phase){bubbleUntil=Time.unscaledTime+15;chatScroll.y=100000;}lastCreationPhase=creation.phase;
                QueueCreatorImage(creation.conceptUrl);foreach(var d in creation.designs??new CreationDesign[0])QueueCreatorImage(d.url);
            },_=>{});
            if(!creationBusy&&creation!=null&&!creation.paused&&!creation.cancelled)
            {
                if(creation.phase=="awaiting_batch"&&creation.modelCount==0){if(creation.autoFlow)yield return ControlCreation("advance");else yield return ConfirmCreation("production_batch",creation.batchHash,0);}
                else if(creation.phase=="assets_ready"&&creation.runtimePreparation?.state!="running"&&creation.runtimePreparation?.state!="failed"){if(creation.autoFlow)yield return ControlCreation("advance");else yield return PrepareRuntimeCreation();}
            }
            yield return AutoEnterPreparedPreview();
        }
        void QueueCreatorImage(string url){if(!string.IsNullOrEmpty(url)&&!creatorImages.ContainsKey(url)&&imageLoads.Add(url))StartCoroutine(LoadCreatorImage(url));}
        IEnumerator LoadCreatorImage(string url)
        {
            if(!url.StartsWith("/api/home-creation/files/"))yield break;
            using(var request=UnityWebRequest.Get(SmallWorldApi.Base+url))
            {yield return request.SendWebRequest();if(request.result==UnityWebRequest.Result.Success){var image=new Texture2D(2,2);if(image.LoadImage(request.downloadHandler.data))creatorImages[url]=image;else Destroy(image);}}
            imageLoads.Remove(url);
        }
        void CreatorImage(string url,float height)
        {
            if(string.IsNullOrEmpty(url))return;
            if(creatorImages.TryGetValue(url,out var image)){var rect=GUILayoutUtility.GetRect(0,510,height,height,GUILayout.ExpandWidth(true));GUI.DrawTexture(rect,image,ScaleMode.ScaleToFit);if(Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition)){enlargedDesign=image;Event.current.Use();}}
            else GUILayout.Label("正在载入设计图…",muted);
        }
        bool showCreationBudget;Texture2D enlargedDesign;
        void DrawCreationCard()
        {
            if(IsVisitor)return;
            if(Game?.Progress?.firstExpansionUnlocked!=true){GUILayout.Label("等新土地升起后，就能在这里一起设计和布置。",muted);return;}
            GUI.enabled=!creationBusy&&!chatPending;
            if(creation==null||creation.runtimeFinalized||creation.isQuickProject||creation.cancelled)
            {
                if(!string.IsNullOrWhiteSpace(creationIdea))
                {
                    GUILayout.Space(10);GUILayout.Label("一起设计新土地",label);GUILayout.Label(creationIdea,muted);
                    allowNewModel=IllustratedToggle(allowNewModel,"允许新增 1 件物件（也可以只复用现有物件）");
                    GUILayout.Label("本次上限：设计图 $0.30 · 规划与检查估算 ¥2。新增模型会另请你确认费用。",muted);
                    if(GUILayout.Button("确认预算，开始设计",button))StartCoroutine(BeginCreation());
                }
                if(Game?.Progress?.firstExpansionUnlocked==true&&GUILayout.Button("用已有物件自由布置",button))OpenPlacement();
            }
            else
            {
                GUILayout.Space(12);GUILayout.Label(creation.title,label);GUILayout.Label(CreationPhase(creation.phase),muted);
                CreatorImage(creation.conceptUrl,205);
                if(creation.phase=="awaiting_concept"&&GUILayout.Button("认可设计，继续制作",button))StartCoroutine(ConfirmCreation("concept",creation.conceptHash,0));
                foreach(var design in creation.designs??new CreationDesign[0]){GUILayout.Label(design.name,label);CreatorImage(design.url,160);}
                if(creation.phase=="awaiting_batch"&&creation.modelCount>0)
                {
                    unknownModelConfirmed=IllustratedToggle(unknownModelConfirmed,"确认这 "+creation.modelCount+" 件物件的设计，并接受群核本批积分未知");
                    GUI.enabled=!creationBusy&&unknownModelConfirmed;if(GUILayout.Button("确认并生成 3D",button))StartCoroutine(ConfirmCreation("production_batch",creation.batchHash,creation.modelCount));GUI.enabled=!creationBusy;
                }
                if(creation.phase=="runtime_editing")
                {
                    if(HasUnappliedDraft()&&Profile.previewHandledKey!=PreviewKey()){GUILayout.Label("新方案准备好了，你还有一份未应用的草稿。",muted);if(GUILayout.Button("先继续原草稿",button)){Profile.previewHandledKey=PreviewKey();Profile.Save();OpenPlacement();}if(GUILayout.Button("把新方案加入预览",button))StartCoroutine(UsePreparedLayout(true));}
                    else if(GUILayout.Button("继续布置",button))StartCoroutine(UsePreparedLayout());
                }
                if(creation.runtimePreparation?.state=="failed") {GUILayout.Label(creation.runtimePreparation.message,muted);if(GUILayout.Button("重试准备物件",button))StartCoroutine(PrepareRuntimeCreation());}
                foreach(var q in creation.questions??new string[0])GUILayout.Label(q,label);
                if(creation.paused)GUILayout.Label("已暂停后续请求；你可以在对话中让我继续。",muted);
                if(creation.status=="budget_blocked")GUILayout.Label("预算已用完，后续提交被拦截。可以先用已有物件布置。",muted);
                GUILayout.Label("想修改、暂停或继续，直接在下面告诉我。",small);
                if(showCreationBudget)GUILayout.Label("设计图 $"+creation.usedUsd.ToString("F3")+" / $0.30 · 规划估算 ¥"+creation.usedCny.ToString("F3")+" / ¥2",muted);
            }
            GUI.enabled=true;
        }
        string CreationPhase(string phase)
        {
            switch(phase){case "brief":return "正在整理你的想法";case "concept_image":return "正在生成概念图";case "concept_review":return "正在检查画面";case "awaiting_concept":return "等待你确认概念图";case "plan":return "正在拆分场景资源";case "asset_image":return "正在生成物件三视图";case "asset_review":return "正在检查三视图一致性";case "awaiting_batch":return "等待你确认本次制作批次";case "models":return "正在生成并保存 3D";case "assets_ready":return "模型已下载，等待本地准备";case "runtime_editing":return "已准备好，可以摆放预览";default:return "创作记录已保存";}
        }
        IEnumerator BeginCreation()
        {
            creationBusy=true;unknownModelConfirmed=false;yield return SyncState();
            if(Connected)yield return SmallWorldApi.Request("create",new CreateRequest {clientId=Profile.clientId,idea=creationIdea,maxGeneratedAssets=allowNewModel?1:0,autoFlow=true},json=>{creation=JsonUtility.FromJson<CreationState>(json);Profile.creationRunId=creation.id;creationIdea="";Profile.creationIdeaDraft="";Profile.Save();},Toast);
            creationBusy=false;
        }
        IEnumerator ConfirmCreation(string gate,string hash,int count)
        {
            creationBusy=true;yield return SmallWorldApi.Request("creation/"+Profile.clientId+"/"+creation.id+"/confirm",new CreationConfirm {gate=gate,bindingHash=hash,requestId=Guid.NewGuid().ToString("N"),unknownModelTasks=count},json=>creation=JsonUtility.FromJson<CreationState>(json),Toast);creationBusy=false;
        }
        IEnumerator ControlCreation(string action,string commandId=null)
        {
            creationBusy=true;lastCreationError="";yield return SmallWorldApi.Request("creation/"+Profile.clientId+"/"+creation.id+"/"+action,new ClientRequest {clientId=Profile.clientId,commandId=commandId},json=>creation=JsonUtility.FromJson<CreationState>(json),CreationError);creationBusy=false;
        }
        void CreationError(string error){lastCreationError=error;Toast(error);}
        IEnumerator ReviseCreation(string commandId=null)
        {
            creationBusy=true;lastCreationError="";yield return SmallWorldApi.Request("creation/"+Profile.clientId+"/"+creation.id+"/revise",new CreationInstruction {instruction=creationRevision,commandId=commandId},json=>{creation=JsonUtility.FromJson<CreationState>(json);creationRevision="";unknownModelConfirmed=false;},CreationError);creationBusy=false;
        }
        IEnumerator PrepareRuntimeCreation()
        {
            creationBusy=true;yield return SmallWorldApi.Request("prepare-runtime",new RunRequest {clientId=Profile.clientId,runId=creation.id},json=>creation=JsonUtility.FromJson<CreationState>(json),Toast);creationBusy=false;
        }
        string PreviewKey()=>creation==null?"":creation.id+":"+creation.revision;
        IEnumerator AutoEnterPreparedPreview()
        {
            if(creation?.phase!="runtime_editing"||creation.paused||creation.cancelled||creation.isQuickProject||IsVisitor||!ChatOpen||chatResidentId!=null||chatPending||applyingGuideAction||editorOpen||editorBusy||Game==null||Game.Busy||Profile.previewHandledKey==PreviewKey()||HasUnappliedDraft())yield break;
            yield return UsePreparedLayout();
        }
        IEnumerator UsePreparedLayout(bool forceNew=false)
        {
            yield return SmallWorldApi.Request("assets",null,json=>availableAssets=JsonUtility.FromJson<SmallWorldAssetList>(json).assets,Toast);
            var newVersion=Profile.previewHandledKey!=PreviewKey()||forceNew;
            if(!OpenPlacement(false))yield break;
            if(newVersion&&creation.initialLayout!=null)
            {
                RememberUndo();var initial=JsonUtility.FromJson<SmallWorldLayout>(JsonUtility.ToJson(new SmallWorldLayout {instances=creation.initialLayout})).instances;
                if(!string.IsNullOrEmpty(creation.surfaceStyle))editSurfaceStyle=creation.surfaceStyle;
                foreach(var item in initial)if(!item.instanceId.StartsWith(creation.id.Substring(0,8)+"-"))item.instanceId=creation.id.Substring(0,8)+"-"+item.instanceId;
                if(creation.replaceExisting)editItems=new List<SmallWorldItem>(initial);else foreach(var i in initial)if(!editItems.Exists(x=>x.instanceId==i.instanceId))editItems.Add(i);
            }
            Profile.previewHandledKey=PreviewKey();Profile.Save();yield return PreviewPlacement();
            Toast(previewValid?"这是可撤销的预览，应用后才改变家园。":"预览需要调整，草稿已保留。");
        }
    }
}
