using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        [Serializable] public class ChatRequest {public string clientId,requestId,message,residentId,layoutContextId,draftSurfaceStyle;public bool hasLocalDraft,draftSeamSoftened;public int layoutBaseVersion;public SmallWorldItem[] previewLayout;}
        [Serializable] public class GuideTurn
        {
            public string id,state,message,reply,action,instruction,error,residentId,contextHash,layoutContextId;public HomeIdea[] ideas;
            public bool memorySaved,hasLayoutProposal,unknownCharge;
            public int layoutBaseVersion;
            public SmallWorldItem[] layoutProposal;
            public Budget budget;
        }
        [Serializable] public class MemoryEntry {public string id,text,day,runId;public bool editable;}
        [Serializable] public class MemoryReply {public MemoryEntry[] diary,preferences,scenes;}
        List<GuideLine> chatLines=new List<GuideLine>();string chatInput="";bool chatPending;
        Vector2 chatScroll,memoryScroll;GuideTurn lastTurn;MemoryReply memoryReply;
        IEnumerator RefreshGuideSettings(){if(Connected&&chatResidentId==null)yield return SmallWorldApi.Request("status/"+Profile.clientId,null,json=>{var c=JsonUtility.FromJson<ConnectReply>(json);guide=c.settings??guide;guide.name=c.name;guideBudget=c.budget??guideBudget;if(!chatPending)lastTurn=c.lastTurn;},_=>{});}
        void DrawChat()
        {
            string name=chatResidentId==null?guide.name:CurrentResident?.name??"邻居";
            GUILayout.BeginArea(new Rect(784,38,474,630),panel);
            GUILayout.BeginHorizontal();GUILayout.Label(SmallWorldUiArt.PortraitTexture(chatResidentId??"xiaohe"),GUILayout.Width(50),GUILayout.Height(50));GUILayout.Label(name+"\n"+(chatResidentId==null?"一起照料小世界":CurrentResident?.homeName),new GUIStyle(title){fontSize=20,wordWrap=true});
            if(chatResidentId==null&&GUILayout.Button("更多",secondaryButton,GUILayout.Width(68)))showOptions=!showOptions;
            if(GUILayout.Button("收起",button,GUILayout.Width(70))){ChatOpen=false;UpdateControls();}GUILayout.EndHorizontal();
            if(showOptions&&chatResidentId==null)
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button("一起的记忆",button)){ChatOpen=false;showMemories=true;StartCoroutine(LoadMemories());UpdateControls();}
                if(GUILayout.Button(Profile.remindersMuted?"开启提醒":"安静陪伴",button)){Profile.remindersMuted=!Profile.remindersMuted;Profile.Save();}GUILayout.EndHorizontal();
                GUILayout.Label("对话今日估算 ¥"+guideBudget.usedOrReservedCny.ToString("F3")+" / ¥"+guideBudget.capCny.ToString("F2"),small);
            }
            chatScroll=GUILayout.BeginScrollView(chatScroll,GUILayout.Height(showOptions?309:371));
            if(chatLines.Count==0)GUILayout.Label(chatResidentId==null?"今天想做些什么？也可以直接告诉我，想把新土地布置成什么样。":"欢迎来做客。",label);
            foreach(var line in chatLines){GUILayout.BeginVertical(line.role=="user"?new GUIStyle(panel){normal={background=secondaryButton.normal.background},padding=new RectOffset(12,12,10,10)}:new GUIStyle(GUIStyle.none){padding=new RectOffset(12,12,8,8)});GUILayout.Label(line.role=="user"?"你":name,small);GUILayout.Label(line.text,label);GUILayout.EndVertical();GUILayout.Space(9);}
            if(chatPending)GUILayout.Label(name+"正在想…",muted);
            if(chatResidentId!=null)DrawNeighborCard();
            else
            {
                if(JourneyAvailable&&!IsVisitor&&!restoringOwnedLayout&&Journey==JourneyStep.Arrange&&!chatPending&&!applyingGuideAction&&!stagingHomecoming)
                {
                    GUILayout.Label("旅行手记 · 给回家的路留一盏灯",small);
                    if(GUILayout.Button("用阿烬的礼物布置纪念角",coverPill))StartCoroutine(ArrangeHomecoming());
                    if(HasUnappliedDraft()&&GUILayout.Button("查看已保留的布置草稿",button))OpenPlacement();
                }
                if(!string.IsNullOrEmpty(guideActionError)){GUILayout.Label(guideActionError,muted);if(GUILayout.Button("重试未完成的操作",button))RetryGuideAction();}
                if(chatLines.Count==0||showOptions)DrawInspirationCards();
                DrawCreationCard();
            }
            GUILayout.EndScrollView();
            GUILayout.Label("直接告诉我想法、修改意见或想做的事",small);GUI.SetNextControlName("guide-chat-input");chatInput=GUILayout.TextArea(chatInput,2000,inputStyle,GUILayout.Height(62));
            var resuming=!string.IsNullOrEmpty(Profile.pendingTurnId);GUI.enabled=!chatPending&&!inspirationBusy&&!applyingGuideAction&&(resuming||!string.IsNullOrWhiteSpace(chatInput));
            if(GUILayout.Button(resuming?"查看上一条结果":"发送",button,GUILayout.Height(42))){if(resuming)StartCoroutine(ResumePendingChat());else{var message=chatInput;chatInput="";StartCoroutine(SendChat(message));}}GUI.enabled=true;
            GUILayout.EndArea();
            if(enlargedDesign!=null){GUI.Box(new Rect(105,20,1070,660),GUIContent.none,panel);GUI.DrawTexture(new Rect(125,40,1030,575),enlargedDesign,ScaleMode.ScaleToFit);if(GUI.Button(new Rect(515,620,250,42),"返回对话",button))enlargedDesign=null;}
        }
        ChatRequest ChatRequestFor(string id,string text)
        {
            var request=new ChatRequest {clientId=Profile.clientId,requestId=id,message=text,residentId=chatResidentId};
            if(!IsVisitor&&chatResidentId==null&&HasUnappliedDraft())
            {var draft=JsonUtility.FromJson<SmallWorldLayout>(System.IO.File.ReadAllText(Profile.HomeSavePath("layout-draft")));request.hasLocalDraft=true;request.previewLayout=draft.instances;request.draftSeamSoftened=draft.seamSoftened;request.draftSurfaceStyle=draft.surfaceStyle;request.layoutBaseVersion=committedLayout.version;request.layoutContextId=DraftContextId();}
            return request;
        }
        string DraftContextId()
        {
            string value=HasUnappliedDraft()?System.IO.File.ReadAllText(Profile.HomeSavePath("layout-draft")):JsonUtility.ToJson(committedLayout);
            using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
        }
        IEnumerator SendChat(string text)
        {
            if(chatPending)yield break;
            if(!string.IsNullOrEmpty(Profile.pendingTurnId)){Toast("先查看上一条消息的结果，不会重复提交。");yield return ResumePendingChat();yield break;}
            chatPending=true;chatLines.Add(new GuideLine {role="user",text=text});chatScroll.y=100000;
            yield return SyncState();
            if(!Connected){chatPending=false;ShowChatError(connectionError);yield break;}
            var id=Guid.NewGuid().ToString("N");Profile.pendingTurnId=id;Profile.pendingResidentId=chatResidentId;Profile.Save();GuideTurn turn=null;bool failed=false;string submissionError="";
            yield return SmallWorldApi.Request("chat",ChatRequestFor(id,text),json=>turn=JsonUtility.FromJson<GuideTurn>(json),error=>{submissionError=error;failed=true;});
            if(failed)
            {
                // A timed-out POST may already have reached the server. Resolve
                // its existing ID through a read; never submit a paid turn twice.
                yield return SmallWorldApi.Request("turn/"+Profile.clientId+"/"+id,null,json=>turn=JsonUtility.FromJson<GuideTurn>(json),error=>{
                    if(error.Contains("不存在")){Profile.pendingTurnId=null;Profile.pendingResidentId=null;Profile.Save();}
                });
                if(turn==null)ShowChatError(submissionError);
            }
            if(turn!=null)
            {
                yield return AwaitGuideTurn(turn,id,false);
            }
            chatPending=false;
        }
        IEnumerator ResumePendingChat()
        {
            if(chatPending||string.IsNullOrEmpty(Profile.pendingTurnId))yield break;chatPending=true;var id=Profile.pendingTurnId;GuideTurn turn=null;
            yield return SmallWorldApi.Request("turn/"+Profile.clientId+"/"+id,null,json=>turn=JsonUtility.FromJson<GuideTurn>(json),error=>{Toast(error);if(error.Contains("不存在")){Profile.pendingTurnId=null;Profile.pendingResidentId=null;Profile.Save();}});
            if(turn!=null)yield return AwaitGuideTurn(turn,id,true);chatPending=false;
        }
        IEnumerator AwaitGuideTurn(GuideTurn turn,string id,bool resumed)
        {
            var deadline=Time.realtimeSinceStartup+115;
            while(turn!=null&&(turn.state=="queued"||turn.state=="running")&&Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForSecondsRealtime(1);
                yield return SmallWorldApi.Request("turn/"+Profile.clientId+"/"+id,null,json=>turn=JsonUtility.FromJson<GuideTurn>(json),Toast);
            }
            if(turn?.state=="complete")
            {
                if((turn.residentId??"")!=(chatResidentId??"")){Toast("上一条消息已完成，下次和对方聊天时可以继续查看。");Profile.pendingTurnId=null;Profile.pendingResidentId=null;Profile.Save();yield break;}
                lastTurn=turn;
                if(!resumed||chatLines.Count==0||chatLines[chatLines.Count-1].role!="assistant"||chatLines[chatLines.Count-1].text!=turn.reply)chatLines.Add(new GuideLine {role="assistant",text=turn.reply});
                chatScroll.y=100000;guideBudget=turn.budget??guideBudget;
                if(turn.residentId==null&&turn.action=="create_home"){creationIdea=turn.instruction;Profile.creationIdeaDraft=creationIdea;Profile.Save();allowNewModel=false;}
                if(turn.residentId==null)QueueGuideAction(turn);
                if(turn.memorySaved)Toast("这条明确偏好已经记住，可以在「一起的记忆」中查看。");
            }
            else ShowChatError(turn?.error??"这条消息还在处理中。稍后可继续查看，不会自动重复提交。");
            if(turn?.state=="complete"||turn?.state=="failed"){Profile.pendingTurnId=null;Profile.pendingResidentId=null;Profile.Save();}
        }
        void ShowChatError(string error)
        {
            if(string.IsNullOrEmpty(error))error="小禾暂时没有连上真实对话服务，进度已保留。";
            if(chatLines.Count==0||chatLines[chatLines.Count-1].text!=error)chatLines.Add(new GuideLine {role="assistant",text=error});
            chatScroll.y=100000;Toast(error);
        }
        string ActionLabel(string action)
        {
            switch(action){case "water":return "前往幼苗";case "chest":return "前往补给箱";case "world":return "打开星球地图";case "create_home":return "一起设计新土地";case "edit_home":return "查看摆放建议";case "smooth_seam":return "预览自然连接";case "show_memory":return "看看我们的记忆";case "revise_creation":return "按这条意见修改设计";case "pause_creation":return "暂停制作";case "resume_creation":return "继续制作";case "cancel_creation":return "停止本次制作";case "show_creation":return "查看进度";case "show_budget":return "查看创作预算";default:return "继续聊聊";}
        }
        void PerformGuideAction(GuideTurn turn){QueueGuideAction(turn);}
        string memoryEditing,memoryText;
        [Serializable] class MemoryChange {public string clientId,id,action,text;}
        IEnumerator ChangeMemory(string id,string action,string text)
        {yield return SmallWorldApi.Request("memory-change",new MemoryChange {clientId=Profile.clientId,id=id,action=action,text=text},j=>{memoryReply=JsonUtility.FromJson<MemoryReply>(j);memoryEditing=null;InvalidateInspiration();},Toast);}
        IEnumerator LoadMemories(){yield return SmallWorldApi.Request("memories/"+Profile.clientId,null,json=>memoryReply=JsonUtility.FromJson<MemoryReply>(json),Toast);}
        void DrawMemories()
        {
            GUILayout.BeginArea(new Rect(320,65,640,570),panel);GUILayout.BeginHorizontal();GUILayout.Label("一起的记忆",title);if(GUILayout.Button("关闭",button,GUILayout.Width(80))){showMemories=false;ChatOpen=true;UpdateControls();}GUILayout.EndHorizontal();
            GUILayout.Label("游戏偏好和共同经历会持续整理。奖励、土地和植物状态以游戏存档为准。",muted);
            memoryScroll=GUILayout.BeginScrollView(memoryScroll);
            GUILayout.Label("你明确说过的偏好",title);
            foreach(var m in memoryReply?.preferences??new MemoryEntry[0])
            {
                GUILayout.Label("· "+m.text,label);
                if(m.editable){GUILayout.BeginHorizontal();if(GUILayout.Button("更正",button)){memoryEditing=m.id;memoryText=m.text;}if(GUILayout.Button("忘记",button))StartCoroutine(ChangeMemory(m.id,"forget",""));GUILayout.EndHorizontal();}
                if(memoryEditing==m.id){memoryText=GUILayout.TextArea(memoryText,500,inputStyle);if(GUILayout.Button("保存更正",button))StartCoroutine(ChangeMemory(m.id,"replace",memoryText));}
            }
            GUILayout.Space(12);GUILayout.Label("共同经历",title);
            foreach(var m in memoryReply?.diary??new MemoryEntry[0]){GUILayout.Label(m.day,small);GUILayout.Label(m.text,label);GUILayout.Space(10);}
            GUILayout.Space(12);GUILayout.Label("已经定稿的地方",title);
            foreach(var m in memoryReply?.scenes??new MemoryEntry[0])GUILayout.Label("· "+m.text,label);
            GUILayout.EndScrollView();GUILayout.Label("共同经历会自动保存；更正偏好不会改写奖励与家园进度。",muted);GUILayout.EndArea();
        }
    }
}
