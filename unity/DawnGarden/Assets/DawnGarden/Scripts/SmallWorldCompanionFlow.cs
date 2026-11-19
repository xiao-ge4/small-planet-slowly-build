using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        [Serializable] public class HomeIdea {public string id,title,description,idea,why;public string[] assetIds;}
        HomeIdea[] inspirationIdeas=new HomeIdea[0];bool inspirationBusy,applyingGuideAction,inspirationDirty;
        string inspirationHash="",guideActionError="",expandedIdeaId="";GuideTurn queuedGuideAction;
        void QueueGuideAction(GuideTurn turn)
        {
            if(turn==null||turn.residentId!=null||turn.intentIsInspiration()||turn.action=="chat"||turn.action=="create_home"||string.IsNullOrEmpty(turn.id))return;
            if((Profile.appliedGuideTurns??new string[0]).Contains(turn.id))return;
            queuedGuideAction=turn;Profile.pendingGuideActionJson=JsonUtility.ToJson(turn);Profile.Save();
        }
        void UpdateCompanionFlow()
        {
            if(GuardianBusy||restoringOwnedLayout)return;
            if(queuedGuideAction==null&&!applyingGuideAction&&!string.IsNullOrEmpty(Profile?.pendingGuideActionJson)&&string.IsNullOrEmpty(guideActionError))
                try{queuedGuideAction=JsonUtility.FromJson<GuideTurn>(Profile.pendingGuideActionJson);}catch(Exception){guideActionError="上一条操作未能读取，请重新告诉我你的想法。";}
            if(queuedGuideAction!=null&&!chatPending&&!applyingGuideAction&&!creationBusy&&!editorBusy&&!loading&&!editorOpen&&!ritualOpen&&!WorldOpen&&!showMemories&&Game!=null&&!Game.Busy)
            {var turn=queuedGuideAction;queuedGuideAction=null;StartCoroutine(ApplyGuideAction(turn));}
        }
        void RetryGuideAction(){guideActionError="";if(!string.IsNullOrEmpty(Profile.pendingGuideActionJson))queuedGuideAction=JsonUtility.FromJson<GuideTurn>(Profile.pendingGuideActionJson);}
        IEnumerator ApplyGuideAction(GuideTurn turn)
        {
            if((Profile.appliedGuideTurns??new string[0]).Contains(turn.id)){Profile.pendingGuideActionJson="";Profile.Save();yield break;}
            applyingGuideAction=true;guideActionError="";bool ok=true;
            if(turn.action.EndsWith("_creation")||turn.action=="show_budget")
            {
                if(creation==null&&!string.IsNullOrEmpty(Profile.creationRunId))yield return RefreshCreation();
                if(creation==null)guideActionError="现在没有进行中的创作，可以直接说想建什么。";
                else if(turn.action=="revise_creation"){creationRevision=turn.instruction;yield return ReviseCreation(turn.id);guideActionError=lastCreationError;}
                else if(turn.action=="pause_creation"||turn.action=="resume_creation"||turn.action=="cancel_creation")
                {yield return ControlCreation(turn.action.Split('_')[0],turn.id);guideActionError=lastCreationError;}
                else {showCreationBudget=turn.action=="show_budget";yield return RefreshCreation();}
            }
            else if(turn.action=="show_memory"){ChatOpen=false;showMemories=true;UpdateControls();yield return LoadMemories();}
            else if(turn.action=="world"){OpenWorld();}
            else if(turn.action=="water"||turn.action=="chest")
            {
                var kind=turn.action=="water"?GardenInteractionKind.Water:GardenInteractionKind.Chest;var item=Game.FindInteraction(kind);
                ChatOpen=false;UpdateControls();
                if(Game.IsDone(item))Toast("这一项已经完成了。");else if(!Game.GoToInteraction(item,true))guideActionError="这里暂时走不到，可以靠近一点后再试。";
            }
            else if(turn.action=="edit_home"||turn.action=="smooth_seam")
            {
                if(turn.hasLayoutProposal&&(turn.layoutBaseVersion!=committedLayout.version||!string.IsNullOrEmpty(turn.layoutContextId)&&turn.layoutContextId!=DraftContextId()))guideActionError="家园刚刚发生了变化，请让我根据现状重新安排。";
                else if(!OpenPlacement(false))guideActionError="先回自己的新土地，再一起布置。";
                else
                {
                    string previous = DraftJson();
                    RememberUndo();
                    if (turn.hasLayoutProposal && turn.layoutProposal != null)
                    {
                        if (turn.message == HomecomingRequest)
                        {
                            var keptIds = editItems.Select(item => item.instanceId).ToHashSet();
                            editItems.AddRange(turn.layoutProposal.Where(item => !keptIds.Contains(item.instanceId)));
                        }
                        else editItems = turn.layoutProposal.ToList();
                    }
                    if(turn.action=="smooth_seam")editSeamSoftened=true;
                    yield return PreviewPlacement();
                    if (!previewValid)
                    {
                        guideActionError = placementError;
                        ReadDraft(previous);
                        yield return PreviewPlacement(false);
                    }
                    else if (turn.message == HomecomingRequest) FrameHomecomingPreview();
                }
            }
            ok=string.IsNullOrEmpty(guideActionError);
            if(ok){Profile.appliedGuideTurns=(Profile.appliedGuideTurns??new string[0]).Concat(new[]{turn.id}).TakeLast(60).ToArray();Profile.pendingGuideActionJson="";Profile.Save();}
            else Toast(guideActionError);
            applyingGuideAction=false;
        }
        IEnumerator RefreshInspiration()
        {
            if(storyActive||showcaseActive||inspirationBusy||!Connected||IsVisitor||Game?.Progress?.firstExpansionUnlocked!=true||(Environment.GetCommandLineArgs().Contains("-smallWorldSelfTest")||Environment.GetCommandLineArgs().Contains("-smallWorldGuardianTest")))yield break;
            if(JourneyAvailable && Journey==JourneyStep.Arrange)yield break;
            inspirationBusy=true;inspirationDirty=false;GuideTurn turn=null;
            yield return SyncState();yield return SmallWorldApi.Request("inspiration",new ClientRequest {clientId=Profile.clientId},j=>turn=JsonUtility.FromJson<GuideTurn>(j),_=>{});
            if(turn!=null&&!string.IsNullOrEmpty(turn.contextHash)&&turn.contextHash!=inspirationHash)inspirationIdeas=new HomeIdea[0];
            var end=Time.realtimeSinceStartup+65;
            while(turn!=null&&(turn.state=="queued"||turn.state=="running")&&Time.realtimeSinceStartup<end)
            {yield return new WaitForSecondsRealtime(.7f);yield return SmallWorldApi.Request("turn/"+Profile.clientId+"/"+turn.id,null,j=>turn=JsonUtility.FromJson<GuideTurn>(j),_=>{});}
            if(turn?.state=="failed")yield return SmallWorldApi.Request("inspiration",new ClientRequest {clientId=Profile.clientId},j=>turn=JsonUtility.FromJson<GuideTurn>(j),_=>{});
            if(!inspirationDirty&&turn?.state=="complete"&&turn.ideas?.Length==3){inspirationIdeas=turn.ideas;inspirationHash=turn.contextHash;guideBudget=turn.budget??guideBudget;}
            inspirationBusy=false;if(inspirationDirty)StartCoroutine(RefreshInspiration());
        }
        void InvalidateInspiration(){inspirationIdeas=new HomeIdea[0];inspirationHash="";inspirationDirty=true;}
        void DrawInspirationCards()
        {
            if(IsVisitor||Game?.Progress?.firstExpansionUnlocked!=true||creation!=null&&!creation.runtimeFinalized&&!creation.isQuickProject&&!creation.cancelled)return;
            if(inspirationBusy)GUILayout.Label("正在结合你的偏好与家园，整理三个方向…",muted);
            if(inspirationIdeas.Length==0){if(!inspirationBusy&&GUILayout.Button("帮我想三个布置方向",secondaryButton))StartCoroutine(RefreshInspiration());return;}
            GUILayout.Space(10);GUILayout.Label("给这片土地的一点灵感",label);
            foreach(var idea in inspirationIdeas)
            {
                GUILayout.Label(idea.title,label);GUILayout.Label(idea.description.Length>55?idea.description.Substring(0,55)+"…":idea.description,muted);
                GUI.enabled=!chatPending&&!inspirationBusy&&!applyingGuideAction;
                GUILayout.BeginHorizontal();if(GUILayout.Button("聊这个方向",button))StartCoroutine(SendChat("我选「"+idea.title+"」。"+idea.idea+"先讨论方向，不要立即生成图片。"));
                if(GUILayout.Button(expandedIdeaId==idea.id?"收起思路":"看看依据",button))expandedIdeaId=expandedIdeaId==idea.id?"":idea.id;GUILayout.EndHorizontal();
                if(expandedIdeaId==idea.id){GUILayout.Label(idea.description,muted);GUILayout.Label(idea.why,small);}
                GUI.enabled=true;GUILayout.Space(8);
            }
        }
    }
    static class SmallWorldGuideTurnExtensions
    {
        public static bool intentIsInspiration(this SmallWorldApp.GuideTurn turn)=>turn.ideas!=null&&turn.ideas.Length>0;
    }
}
