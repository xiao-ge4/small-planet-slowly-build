using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        [Serializable] public class ShowcaseConfig {public string serviceBase,ffmpeg,photo,design,objectDesign;public bool smoke,navigationCheck,resumeHome;public float holdScale=1;}
        [Serializable] public class MovieEvent {public double time;public string kind,text;}
        [Serializable] public class MovieReport
        {public bool passed,smoke;public string buildId,clientId,utc,error,controlMode="scripted-demo",agentMode="live";public double duration;public int frames;public MovieEvent[] events;public GuideTurn[] agentTurns;public string[] checks;}
        bool showcaseActive,showcaseIntro,showcaseHeatHeld;ShowcaseConfig showcaseConfig;SmallWorldMovieRecorder movie;
        readonly List<MovieEvent> movieEvents=new List<MovieEvent>();readonly List<GuideTurn> movieTurns=new List<GuideTurn>();readonly List<string> movieChecks=new List<string>();
        Texture2D showcasePhoto,showcaseDesign,showcaseObject;double chapterTime;
        void RecordMovieEvent(string kind,string text){movieEvents.Add(new MovieEvent {kind=kind,text=text,time=movie?.Elapsed??0});Debug.Log("SHOWCASE "+kind+" "+text);}
        void Chapter(string title,string caption){chapterTime=movie.Elapsed;RecordMovieEvent("chapter",title);RecordMovieEvent("caption",caption);}
        void Note(string text)=>RecordMovieEvent("caption",text);
        void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);movieChecks.Add(message);}
        IEnumerator Hold(float seconds){yield return new WaitForSecondsRealtime(seconds*showcaseConfig.holdScale);}
        IEnumerator ChapterEnd(float seconds){float left=seconds*showcaseConfig.holdScale-(float)(movie.Elapsed-chapterTime);if(left>0)yield return new WaitForSecondsRealtime(left);}
        void ClearMoviePanels(){ChatOpen=false;showMemories=false;editorOpen=false;ritualOpen=false;WorldOpen=false;SetWorldVisible(false);UpdateControls();}
        void MovieCamera(bool close=false,float distance=16)
        {Game.orbit.overview=!close;Game.orbit.distance=close?10f:distance;Game.orbit.yaw=180;Game.orbit.pitch=close?48:40;Game.orbit.expandedOverviewCenter=new Vector3(0,.5f,0);}
        IEnumerator MovieWalk(Vector3 destination)
        {
            ClearMoviePanels();Require(Game.WalkTo(destination),"walk requested");yield return UntilV1(()=>!Game.Navigating,30);
            var delta=Game.actor.position-destination;delta.y=0;
            if(delta.magnitude>1.8f)
            {
                Debug.Log("SHOWCASE route retry via central path: "+Game.actor.position.ToString("F3")+" -> "+destination.ToString("F3")+"; "+Game.Status);
                Require(Game.WalkTo(Game.spawn.position),"central waypoint requested");yield return UntilV1(()=>!Game.Navigating,25);
                Require(Game.WalkTo(destination),"destination route retried");yield return UntilV1(()=>!Game.Navigating,25);
                delta=Game.actor.position-destination;delta.y=0;
            }
            Require(!Game.Navigating&&delta.magnitude<1.8f,"walk completed near target: "+delta.magnitude.ToString("F2"));
        }
        IEnumerator MovieChat(string message)
        {
            for(int attempt=0;attempt<2;attempt++)
            {
                string previous=lastTurn?.id;
                yield return SendChat(attempt==0?message:"刚才没有完成，请再试一次。必须使用目录中准确的资产ID、版本与哈希，并检查物件尺寸和地块边界。原要求："+message);
                if(lastTurn?.state=="complete"&&lastTurn.id!=previous){movieTurns.Add(lastTurn);RecordMovieEvent("agent",lastTurn.reply);yield return Hold(7);yield break;}
                if(attempt==0){Note("小禾这次没有完成请求。现有草稿仍在，请它再整理一次。");yield return Hold(3);}
            }
            Require(false,"live agent response after one retry");
        }
        IEnumerator MovieNpcTalk()
        {MovieCamera(true);yield return MovieWalk(Reachable(residentAnchor+Vector3.back*.95f));yield return UseWorldTarget(worldTargets.First(t=>t.kind=="resident"));yield return UntilV1(()=>!neighborBusy,12);Require(ChatOpen&&chatResidentId==CurrentResident.id,"resident dialogue opened");yield return Hold(6);}
        IEnumerator MovieCare()
        {
            ClearMoviePanels();MovieCamera(true);Require(Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Water),true),"watering route requested");
            yield return UntilV1(()=>GardenSave.WateredToday(Game.Progress)&&!Game.Busy,35);Require(GardenSave.WateredToday(Game.Progress),"real watering completed");
            if(IsVisitor){yield return UntilV1(()=>!neighborBusy&&CurrentResident.wateringDays>0,12);Require(CurrentResident.wateringDays>0,"neighbor care persisted");}yield return Hold(4);
        }
        IEnumerator MovieCollect()
        {
            ClearMoviePanels();Require(worldTargets.Where(t=>t.kind=="search"&&t.gameObject.activeSelf).All(t=>Physics.GetIgnoreCollision(Game.body,t.GetComponent<Collider>())),"search hit areas do not block navigation");
            for(int i=0;i<3;i++)
            {var target=worldTargets.First(t=>t.kind=="search"&&t.index==i);MovieCamera(true);yield return MovieWalk(target.transform.position);var delta=Game.actor.position-target.transform.position;delta.y=0;Require(delta.magnitude<2.3f,"pickup proximity verified");yield return UseWorldTarget(target);int index=i;yield return UntilV1(()=>CurrentResident.collected.Contains(index)&&!neighborBusy,12);Require(CurrentResident.collected.Contains(i),"quest item "+i+" persisted");yield return Hold(2);}
        }
        IEnumerator RunShowcaseGuarded()
        {
            var stack=new Stack<IEnumerator>();stack.Push(ShowcaseRoutine());string error=null;
            while(stack.Count>0)
            {
                bool next=false;object current=null;
                try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}
                catch(Exception e){error=e.ToString();Debug.LogException(e);break;}
                if(!next){stack.Pop();continue;}if(current is IEnumerator inner){stack.Push(inner);continue;}yield return current;
            }
            if(movie!=null)
            {
                var end=movie.Finish();while(true){bool next=false;object current=null;try{next=end.MoveNext();if(next)current=end.Current;}catch(Exception e){error=(error??"")+"\n"+e;break;}if(!next)break;yield return current;}
            }
            var report=new MovieReport {passed=error==null,smoke=showcaseConfig.smoke,buildId=library.buildId,clientId=Profile.clientId,utc=DateTime.UtcNow.ToString("o"),error=error??"",duration=movie?.Elapsed??0,frames=movie?.Frames??0,events=movieEvents.ToArray(),agentTurns=movieTurns.ToArray(),checks=movieChecks.ToArray()};
            File.WriteAllText(Path.Combine(SmallWorldProfile.Root,"movie-report.json"),JsonUtility.ToJson(report,true));Application.Quit(error==null?0:2);
        }
        Texture2D ReadMovieImage(string path){var image=new Texture2D(2,2);if(!image.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException("Historical image failed to load");return image;}
        IEnumerator ShowcaseRoutine()
        {
            yield return UntilV1(()=>Game!=null&&!loading&&!restoringOwnedLayout&&Connected,60);Require(Game!=null&&Connected,"isolated game connected");
            Application.targetFrameRate=30;Profile.remindersMuted=true;Profile.Save();Game.atmosphere.Preview(8.2f);MovieCamera();
            showcasePhoto=ReadMovieImage(showcaseConfig.photo);showcaseDesign=ReadMovieImage(showcaseConfig.design);showcaseObject=ReadMovieImage(showcaseConfig.objectDesign);
            yield return new WaitForSecondsRealtime(.8f);movie=gameObject.AddComponent<SmallWorldMovieRecorder>();movie.Begin(SmallWorldProfile.Root,showcaseConfig.ffmpeg);var listener=FindFirstObjectByType<AudioListener>();Require(listener!=null,"game audio listener exists");movie.AttachAudio(listener.gameObject);
            if(showcaseConfig.resumeHome){Require(neighborWorld.gifts.Length==3&&Game.Progress.firstExpansionUnlocked,"same demo checkpoint restored");goto HomeChapter;}
            if(showcaseConfig.navigationCheck)
            {
                foreach(var location in new[]{new Vector2Int(1,0),new Vector2Int(2,1)})
                {
                    yield return Visit(location.x,location.y);yield return UntilV1(()=>!loading&&residentReady,30);MovieCamera(true);
                    Chapter("寻路校验 · "+CurrentResident.name,"隔离存档，仅验证真实行走和互动距离，不调用付费生成。");
                    yield return MovieNpcTalk();yield return NeighborAction("accept_quest",-1,true);yield return MovieCare();yield return MovieCollect();yield return MovieWalk(ritualAnchor);
                }
                yield break;
            }
            Chapter("把旅途带回家","真实游戏录制 · 独立演示存档 · 脚本操作，Agent 对话使用真实服务");showcaseIntro=true;yield return Hold(showcaseConfig.smoke?3:6);
            Note("制作记录：从一只玻璃杯，逐步设计出自己的幼年精灵。图像为已经完成的历史版本。");yield return Hold(showcaseConfig.smoke?3:16);showcaseIntro=false;
            Note("现在，让它走进属于自己的小世界。");yield return Hold(showcaseConfig.smoke?4:6);
            if(showcaseConfig.smoke)yield break;
            Chapter("一 · 和小禾一起回家","小禾是常驻向导。它能聊天，也能记住你明确说过的偏好。");OpenChat();
            yield return MovieChat("小禾，记住我喜欢米白色的石灯和自然的小花，想把旅途中的礼物留在自己的家里。先陪我逛逛，不要生成图片或模型。");Require(lastTurn.memorySaved,"explicit preference saved");yield return ChapterEnd(28);
            Chapter("二 · 第一口水","告诉小禾想做什么，它会把对话连接到实际的照料动作。");
            yield return MovieChat("带我去给新芽浇水吧，现在就出发。");Require(lastTurn.action=="water","agent selected real watering action");
            yield return UntilV1(()=>GardenSave.WateredToday(Game.Progress)&&!Game.Busy,40);Require(GardenSave.WateredToday(Game.Progress),"agent watering completed");ClearMoviePanels();MovieCamera();Note("精灵会走到幼苗旁，拿起固定水壶完成浇水。照料进度持续保存。");yield return ChapterEnd(28);
            Chapter("三 · 给新的生活留一点空间","补给箱里是一张开拓许可。现在扩建，或者把它留在锦囊里，等准备好了再用。");ClearMoviePanels();MovieCamera(true);
            Require(Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Chest),true),"chest route requested");yield return UntilV1(()=>Game.Progress.chestOpened&&!Game.Busy,35);Require(Game.Progress.chestOpened,"real chest opened");yield return Hold(5);
            Require(Game.expansion.Defer(),"permit deferred into pouch");Note("许可先收进锦囊，进度不会丢失。");yield return Hold(4);Game.expansion.OpenBag();yield return Hold(3);Require(Game.expansion.Accept(),"expansion accepted");
            Note("一片与家园相邻的新土地缓缓升起。它的主题与摆放，将由你和小禾一起决定。");yield return UntilV1(()=>!Game.expansion.IsRising,15);Require(Game.Progress.firstExpansionUnlocked,"new land unlocked");MovieCamera(false,25);Game.orbit.expandedOverviewCenter=new Vector3(5.8f,.5f,0);yield return ChapterEnd(42);
            Chapter("四 · 出门认识朋友","同一颗星球上，四块大陆隔海相望。可以旋转、拉近，再访问邻居的家园。");OpenWorld();float until=Time.realtimeSinceStartup+12*showcaseConfig.holdScale;
            while(Time.realtimeSinceStartup<until){mapYaw+=Time.unscaledDeltaTime*12;yield return null;}FocusMapContinent((Profile.ownContinent+1)%4);yield return Hold(5);yield return ChapterEnd(22);
            yield return Visit(0,(Profile.ownContinent+1)%4);yield return UntilV1(()=>!loading&&residentReady&&!neighborPreparing,30);Require(CurrentResident?.id=="ajin"&&residentReady,"same-planet resident ready");Game.atmosphere.Preview(9.25f);MovieCamera();
            Chapter("五 · 阿烬的暖焰营地","阿烬喜欢温泉与炉火。帮助它照料新芽，再找齐路边的三颗温润石。");yield return Hold(5);yield return MovieNpcTalk();yield return MovieChat("这里的温泉和篝火真温暖。你今天有什么想和我一起做的事？");yield return NeighborAction("accept_quest",-1,true);yield return Hold(5);
            yield return MovieCare();yield return MovieCollect();Note("找齐暖石后，一起把它们加热到刚刚好的温度。");yield return MovieWalk(ritualAnchor);OpenRitual();yield return Hold(3);showcaseHeatHeld=true;yield return UntilV1(()=>ritualHeat>=.67f,6);showcaseHeatHeld=false;
            yield return UntilV1(()=>!ritualSubmitting&&!ritualOpen,12);Require(CurrentResident.ritualComplete,"warm-stone minigame completed");yield return MovieNpcTalk();yield return NeighborAction("receive_gift",-1,true);Require(CurrentResident.giftClaimed,"Ajin gift received");Note("这份暖焰纪念物可以带回家，反复用来布置自己的土地。");yield return ChapterEnd(92);
            Chapter("六 · 星露小筑的三点星光","跨越星球，认识喜欢观察星光的露米。旅行与各家的生活记录都会保留。");OpenWorld();stars=true;planetRoot.SetActive(false);starRoot.SetActive(true);yield return Hold(7);yield return Visit(1,0);yield return UntilV1(()=>!loading&&residentReady,30);Require(CurrentResident?.id=="lumi","cross-planet resident ready");Game.atmosphere.Preview(18.5f);MovieCamera();yield return Hold(4);
            yield return MovieNpcTalk();yield return MovieChat("露米，我想把今天的星光留作纪念。我们一起找找它们吧。");yield return NeighborAction("accept_quest",-1,true);yield return MovieCare();yield return MovieCollect();yield return MovieWalk(ritualAnchor);OpenRitual();
            Note("先观察三点星光的顺序，再依次唤醒它们。选错时可以重试。");yield return UntilV1(()=>Time.unscaledTime>ritualDemoUntil,5);foreach(int n in CurrentResident.ritualPattern.ToArray()){yield return Hold(1);ChooseStar(n);}yield return UntilV1(()=>!ritualSubmitting&&!ritualOpen,12);Require(CurrentResident.ritualComplete,"star-sequence minigame completed");yield return MovieNpcTalk();yield return NeighborAction("receive_gift",-1,true);Require(CurrentResident.giftClaimed,"Lumi gift received");yield return ChapterEnd(82);
            Chapter("七 · 听一段风铃","岚岚的石庭有另一种节奏：听示范、调三枚音符，再合奏一段和弦。");ClearMoviePanels();yield return Visit(2,1);yield return UntilV1(()=>!loading&&residentReady,30);Require(CurrentResident?.id=="lanlan","wind resident ready");Game.atmosphere.Preview(15);MovieCamera();yield return MovieNpcTalk();yield return NeighborAction("accept_quest",-1,true);yield return MovieCare();yield return MovieCollect();yield return MovieWalk(ritualAnchor);OpenRitual();yield return Hold(4);
            for(int i=0;i<3;i++){ritualTuning[i]=CurrentResident.ritualPattern[i];yield return Hold(1.5f);}PlayRitualNotes(ritualTuning);yield return Hold(2);Require(TryRitualTuning(),"wind tuning submitted");yield return UntilV1(()=>!ritualSubmitting&&!ritualOpen,12);Require(CurrentResident.ritualComplete,"wind minigame completed");yield return MovieNpcTalk();yield return NeighborAction("receive_gift",-1,true);Require(CurrentResident.giftClaimed,"Lanlan gift received");yield return ChapterEnd(64);
            HomeChapter:
            Chapter("八 · 把旅途布置成家","带着朋友们的礼物回家，在新土地上留一个旅途纪念角。");ClearMoviePanels();yield return Visit(0,Profile.ownContinent);yield return UntilV1(()=>!loading&&!restoringOwnedLayout,30);Game.atmosphere.Preview(9.25f);yield return SyncState();Require(OpenPlacement(false),"home editor opened");
            editSurfaceStyle=SmallWorldGardenSurface.Style;editSeamSoftened=true;editItems.Clear();
            for(int k=0;k<neighborWorld.gifts.Length;k++){var g=neighborWorld.gifts[k];var a=availableAssets.First(x=>x.id==g.assetId);editItems.Add(new SmallWorldItem {instanceId="trip-gift-"+k,giftId=g.id,assetId=a.id,assetHash=a.hash,assetVersion=a.version,localPosition=new Vector3(2.1f+k*.7f,0,-2.5f+k*.45f),localScale=Vector3.one*g.scale,tint=g.tint});}
            yield return PreviewPlacement(false);Require(previewValid,"earned gifts can be placed");SaveDraft();yield return Hold(5);yield return ClosePlacement();
            Note("先把已经获得的礼物放进草稿，再请小禾使用现有物件补充配景。");yield return MovieChat("请在当前这三件礼物草稿旁，添加一盏米白矮石灯、一丛晨露混色花丛、一丛晨露阔叶灌木丛，总共六件。集中在新土地右侧，保留原有三件礼物的标识和来源，入口与中央通路留空。只用已有物件，不要生图或生成新模型，现在直接给出摆放预览。");
            yield return UntilV1(()=>editorOpen&&!editorBusy&&!applyingGuideAction,35);Require(editorOpen&&previewValid&&editItems.Count>=6,"live agent layout preview");yield return Hold(7);SaveDraft();yield return ClosePlacement();
            Note("继续用一句话修改：保留整体，只把石灯向左挪一点。");var lampBefore=editItems.FirstOrDefault(i=>i.assetId.Contains("stone-lantern"));
            yield return MovieChat("继续修改刚才未应用的六件物件草稿：只把米白矮石灯向左移动0.4米，其他所有物件的位置、尺寸、标识和来源保持不变，不生成图片或模型。请直接更新预览。");yield return UntilV1(()=>editorOpen&&!editorBusy&&!applyingGuideAction,35);Require(editorOpen&&previewValid,"live agent revised preview");yield return Hold(7);
            Note("布置支持继续调整、撤销和保存。这次仅复用已有资产，生成模型需要另行确认。");selectedItem=editItems.FindIndex(i=>i.assetId.Contains("stone-lantern"));if(selectedItem>=0){ChangeSelected(0,0,15,0);yield return UntilV1(()=>!editorBusy,15);yield return Hold(3);placementRedo.Add(DraftJson());ReadDraft(placementUndo.Last());placementUndo.RemoveAt(placementUndo.Count-1);yield return PreviewPlacement(false);}
            yield return FinalizePlacement();Require(committedLayout.version>0&&!editorOpen,"demo layout saved through normal finalization");yield return ChapterEnd(92);
            Chapter("九 · 家记住了这段旅途","重新进入家园，礼物与布局仍在原处。小禾也记得你的偏好与今天的相遇。");Game.SaveNow();yield return SyncState();loading=true;yield return LoadWorldScene("DawnHomeV1");loading=false;yield return UntilV1(()=>!restoringOwnedLayout,30);Require(committedLayout.instances.Length>=6,"saved layout restored");Game.atmosphere.Preview(9.25f);OpenChat();
            yield return MovieChat("我们今天拜访了谁、一起做了什么？你还记得我喜欢怎样的家园吗？请结合今天的真实记录简短说说。");ChatOpen=false;showMemories=true;UpdateControls();yield return LoadMemories();Require(memoryReply?.scenes?.Length>0&&memoryReply?.preferences?.Length>0,"preferences experiences and scene memory persisted");yield return Hold(10);ClearMoviePanels();yield return ChapterEnd(38);
            Chapter("十 · 明天还会有新的故事","家园有真实的日夜、自己的音乐，也有一点点由你决定的变化。");Game.orbit.overview=true;Game.orbit.yaw=160;Game.orbit.pitch=43;Game.orbit.distance=24;Game.orbit.expandedOverviewCenter=new Vector3(5.8f,.5f,0);
            foreach(float hour in new[]{8.0f,13f,18.5f,22f}){Game.atmosphere.Preview(hour);yield return Hold(6);}Game.atmosphere.Preview(8.2f);
            Note("自己的小世界：从一件熟悉的物品出发，把照料、相遇和创作，慢慢留在家里。");yield return Hold(10);RecordMovieEvent("end","local single-player demo with persistent NPCs; all shown agent turns used live service");
        }
        void DrawShowcaseIntro()
        {
            if(showcasePhoto==null)return;Styles();GUI.depth=-120;var previous=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            var previousColor=GUI.color;GUI.color=new Color(.97f,.975f,.95f,1);GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture);GUI.color=previousColor;
            GUI.Box(new Rect(0,0,1280,720),GUIContent.none,panel);GUI.Label(new Rect(65,110,1100,48),"自己的小世界 · 把旅途带回家",title);
            foreach(var p in new[]{new Vector2(65,210),new Vector2(455,210),new Vector2(845,210)})GUI.Box(new Rect(p.x-8,p.y-8,340,340),GUIContent.none,panel);
            GUI.DrawTexture(new Rect(65,210,320,320),showcasePhoto,ScaleMode.ScaleToFit);GUI.DrawTexture(new Rect(455,210,320,320),showcaseObject,ScaleMode.ScaleToFit);GUI.DrawTexture(new Rect(845,210,320,320),showcaseDesign,ScaleMode.ScaleToFit);
            GUI.Label(new Rect(65,555,330,38),"物件照片",label);GUI.Label(new Rect(455,555,330,38),"原物件设计",label);GUI.Label(new Rect(845,555,330,38),"幼年精灵设计",label);
            GUI.Label(new Rect(65,610,1100,45),"历史制作记录 · 图片已生成并经过版本选择，游戏使用已经绑骨的三维角色。",muted);GUI.matrix=previous;
        }
    }
}
