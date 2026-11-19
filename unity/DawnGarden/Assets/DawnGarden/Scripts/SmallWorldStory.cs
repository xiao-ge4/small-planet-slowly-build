using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGarden
{
    // This director exists only behind an explicit flag plus an isolated save root.
    // Dialogue and reveals are authored footage; gameplay uses the ordinary game APIs.
    public sealed partial class SmallWorldApp
    {
        [Serializable] public class StoryConfig
        {
            public string serviceBase, ffmpeg, conceptImage, path, buildId, sourceBuildId;
            public float holdScale = 1;
            public bool smoke, rehearsal;
        }
        [Serializable] public class StoryReport
        {
            public bool passed;
            public string buildId, clientId, utc, error, agentMode = "preset-demo", controlMode = "scripted-real-gameplay";
            public double duration;
            public int frames;
            public MovieEvent[] events;
            public string[] checks;
        }
        [Serializable] class StoryMemory
        {
            public string provenance = "preset";
            public string source = "approved-demo-script; authored dialogue, not model inference; event and layout facts verified against the game";
            public string explicitPreference, clientId;
            public bool guardianDefeated, lampLit;
            public string[] gifts, ownedTerritoryIds;
            public int layoutVersion, revealedStage;
            public MovieEvent[] transcript;
        }
        bool storyActive, storyGuardianVisible, storyDialogueVisible, storyConceptVisible, storyTitleVisible;
        string storyReplyLayout="";
        string storyUser = "", storyReply = "", storySpeaker = "小禾", storyTitle = "", storySubtitle = "";
        StoryConfig storyConfig;
        SmallWorldStoryLand storyLand;
        Texture2D storyConcept;
        GUIStyle storyHeadingStyle, storyBodyStyle, storyEyebrowStyle;
        readonly List<MovieEvent> storyEvents = new();
        readonly List<string> storyChecks = new();
        int storyStage;
        double storyChapterTime;

        void ConfigureStory()
        {
            storyActive = Environment.GetCommandLineArgs().Contains("-smallWorldStory");
            if (!storyActive) return;
            if (string.IsNullOrEmpty(SmallWorldProfile.TestRoot)) throw new InvalidOperationException("Story requires a separate recording profile.");
            storyConfig = JsonUtility.FromJson<StoryConfig>(File.ReadAllText(Path.Combine(SmallWorldProfile.TestRoot, "story-config.json")));
            if (!Uri.TryCreate(storyConfig.serviceBase, UriKind.Absolute, out var uri) || !uri.IsLoopback)
                throw new InvalidOperationException("Story service must be loopback and isolated.");
            SmallWorldApi.UseRecordingService(storyConfig.serviceBase);
            showcaseConfig = new ShowcaseConfig {holdScale = storyConfig.holdScale};
        }

        void StoryEvent(string kind, string text)
        {
            storyEvents.Add(new MovieEvent {time = movie?.Elapsed ?? 0, kind = kind, text = text});
            Debug.Log("STORY " + kind + " " + text);
        }
        void StoryChapter(string titleText, string caption)
        {
            storyChapterTime = movie?.Elapsed ?? 0;
            StoryEvent("chapter", titleText); StoryNote(caption);
        }
        void StoryNote(string text) => StoryEvent("caption", text);
        void StoryCheck(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("Story check: " + message);
            storyChecks.Add(message); Debug.Log("STORY check " + message);
        }
        IEnumerator StoryHold(float seconds) {yield return new WaitForSecondsRealtime(seconds * storyConfig.holdScale);}
        IEnumerator StoryChapterEnd(float seconds)
        {
            float remaining = seconds * storyConfig.holdScale - (float)((movie?.Elapsed ?? 0) - storyChapterTime);
            if (remaining > 0) yield return new WaitForSecondsRealtime(remaining);
        }
        void StoryClear()
        {
            StoryWorkshopClose();
            storyDialogueVisible = storyConceptVisible = storyTitleVisible = false;
            ClearMoviePanels(); creatorOpen = homecomingJournal = false; guardianCoCreateOffer = false;
            noticeUntil = bubbleUntil = 0; UpdateControls();
        }
        void StoryCamera(Vector3 target, float distance = 18, float yaw = 180, float pitch = 40)
        {
            var o = Game.orbit; o.enabled = true; o.placementView = false;
            o.largeWorld = true; o.overview = true; o.overviewPerspective = true; o.overviewOrthographic = false;
            o.overviewFieldOfView = 43; o.expandedMaxDistance = 150;
            o.expandedOverviewCenter = target; o.distance = distance; o.yaw = yaw; o.pitch = pitch;
            Game.worldCamera.rect = new Rect(0,0,1,1);
        }
        IEnumerator StorySay(string player, string reply, float hold = 5)
        {
            ChatOpen = false; storyUser = player; storyReply = "";storyReplyLayout=reply;
            storySpeaker = IsVisitor ? CurrentResident?.name ?? "朋友" : "小禾";
            storyDialogueVisible = true;
            StoryEvent("player", player);
            yield return StoryHold(.8f);
            StoryEvent("preset-dialogue", storySpeaker + "：" + reply);
            float elapsed = 0, duration = Mathf.Max(.12f, Mathf.Min(2.1f, reply.Length * .025f) * storyConfig.holdScale);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                storyReply = reply.Substring(0, Mathf.Min(reply.Length, Mathf.CeilToInt(reply.Length * elapsed / duration)));
                yield return null;
            }
            storyReply = reply;
            yield return StoryShot("conversation-"+storyEvents.Count.ToString("D3"));
            yield return StoryHold(hold);
            storyDialogueVisible = false; UpdateControls();
        }
        IEnumerator StoryShot(string name)
        {
            yield return new WaitForEndOfFrame();
            string dir = Path.Combine(SmallWorldProfile.TestRoot, "shots"); Directory.CreateDirectory(dir);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
            StoryEvent("screenshot", name);
        }
        IEnumerator StoryWalk(Vector3 destination)
        {
            StoryClear();
            StoryCheck(Game.WalkTo(destination), "path requested: " + destination.ToString("F1"));
            yield return UntilV1(() => !Game.Navigating, 65);
            Vector3 delta = Game.actor.position - destination; delta.y = 0;
            StoryCheck(!Game.Navigating && delta.magnitude < 1.9f, "real walking reached destination");
        }
        void SaveStoryMemory()
        {
            var memory = new StoryMemory {clientId = Profile.clientId,
                explicitPreference = "我喜欢自然一点、能坐下来休息的家；小路不要被花挡住。",
                guardianDefeated = Game.Progress.firstExpansionUnlocked, lampLit = Profile.homecomingLampLit,
                gifts = (neighborWorld.gifts ?? Array.Empty<Gift>()).Select(g=>g.name).ToArray(),
                layoutVersion = committedLayout.version, revealedStage = storyStage, ownedTerritoryIds = storyLand.OwnedIds,
                transcript = storyEvents.Where(e=>e.kind=="player" || e.kind=="preset-dialogue").ToArray()};
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot, "story-memory.json"), JsonUtility.ToJson(memory, true));
        }
        IEnumerator RunStoryGuarded()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(StoryRoutine()); string error = null;
            while (stack.Count > 0)
            {
                bool next = false; object current = null;
                try {next = stack.Peek().MoveNext(); if(next) current=stack.Peek().Current;}
                catch(Exception e) {error=e.ToString(); Debug.LogException(e); break;}
                if(!next){stack.Pop();continue;}
                if(current is IEnumerator inner){stack.Push(inner);continue;} yield return current;
            }
            if(movie!=null)
            {
                var ending=movie.Finish();
                while(true)
                {
                    bool next=false;object current=null;
                    try{next=ending.MoveNext();if(next)current=ending.Current;}
                    catch(Exception e){error=(error??"")+"\n"+e;break;}
                    if(!next)break;yield return current;
                }
            }
            var report = new StoryReport {passed=error==null,buildId=storyConfig.buildId,clientId=Profile.clientId,
                utc=DateTime.UtcNow.ToString("o"),error=error??"",duration=movie?.Elapsed??0,frames=movie?.Frames??0,
                events=storyEvents.ToArray(),checks=storyChecks.ToArray()};
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"story-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(error==null?0:2);
        }

        IEnumerator StoryRoutine()
        {
            yield return UntilV1(()=>Game!=null && coast!=null && Connected && !loading && !restoringOwnedLayout,90);
            StoryCheck(Game!=null && coast!=null && Connected, "isolated complete coast loaded");
            StoryCheck(!Game.Progress.chestOpened && !Game.Progress.firstExpansionUnlocked, "fresh story begins without rewards or owned expansion");
            Application.targetFrameRate=30;Profile.remindersMuted=true;Profile.Save();Game.atmosphere.Preview(8.2f);
            SmallWorldStoryLand.ResetSession();storyLand=gameObject.AddComponent<SmallWorldStoryLand>();storyLand.Attach(Game,coast);
            yield return null;yield return null;
            StoryCamera(DawnCoast.ReferenceHome+Vector3.up*.5f,21,10,44);
            if(File.Exists(storyConfig.conceptImage))storyConcept=ReadMovieImage(storyConfig.conceptImage);
            yield return new WaitForSecondsRealtime(1.5f);
            movie=gameObject.AddComponent<SmallWorldMovieRecorder>();movie.Begin(SmallWorldProfile.TestRoot,storyConfig.ffmpeg);
            var listener=FindFirstObjectByType<AudioListener>();StoryCheck(listener!=null,"game audio listener present");movie.AttachAudio(listener.gameObject);
            StoryEvent("construction-demo-v2","每块新增土地先空置，再确认与施工");
            StoryChapter("序章 · 一块小院，一个愿望","真实游戏实录 · 独立演示存档；对话与共创施工使用预设流程。");
            storyTitle="从一块小院\n到自己的小世界";storySubtitle="今晚，让朋友送来的光留在家里。";storyTitleVisible=true;
            yield return StoryHold(5);storyTitleVisible=false;yield return StoryShot("01-initial-courtyard");
            if(storyConfig.smoke){yield return StoryHold(3);yield break;}
            yield return StoryWalk(DawnCoast.ReferenceHome+new Vector3(-1.6f,0,-1.7f));
            yield return StorySay("这里还很空。我想做一个自然一点、能坐下来休息的家。","我记住啦。先照顾这株小芽，再给以后的故事留一点地方。",6);
            SaveStoryMemory();
            StoryChapter("一 · 第一口水","照料、成长与补给，是开拓小世界的第一步。");
            yield return StorySay("带我去浇水吧。","好，水壶准备好了。沿着小路过去，让新芽喝到今天的第一口水。",4);
            StoryClear();
            var water=Game.FindInteraction(GardenInteractionKind.Water);
            StoryCamera(water.transform.position+Vector3.up*.5f,13,15,43);
            StoryCheck(Game.GoToInteraction(water,true),"watering action requested through normal interaction");
            yield return UntilV1(()=>GardenSave.WateredToday(Game.Progress)&&!Game.Busy,45);
            StoryCheck(GardenSave.WateredToday(Game.Progress),"watering saved");yield return StoryShot("02-watered-seedling");yield return StoryHold(3);
            yield return StorySay("接下来做什么？","那只补给箱还没打开。里面有一张开拓许可，能让我们去挑战附近土地上的守卫。",4);
            StoryClear();var chest=Game.FindInteraction(GardenInteractionKind.Chest);
            StoryCamera(chest.transform.position,15,15,44);
            StoryCheck(Game.GoToInteraction(chest,true),"chest route requested");
            yield return UntilV1(()=>Game.Progress.chestOpened&&!Game.Busy,45);
            StoryCheck(Game.Progress.expansionPermits==1&&!Game.Progress.firstExpansionUnlocked,"chest grants eligibility, not ownership");
            yield return StoryHold(3);StoryCheck(Game.expansion.Defer(),"permit deferred into pouch");
            StoryNote("先收进锦囊，准备好了再使用。许可不会因离开而丢失。");yield return StoryHold(2);
            Game.expansion.OpenBag();yield return StoryShot("03-permit-pouch");yield return StoryHold(3);Game.expansion.Defer();
            yield return StoryBattle();
            yield return StoryFirstCreation();
            yield return StoryFullWorldMontage();
            yield return StoryNeighbors();
            yield return StoryBringGiftsHome();
            yield return StoryNightAndMemory();
            StoryCheck(File.Exists(LayoutFile)&&committedLayout.instances.Length>=8,"layout_saved: actual finalized assets persist");
            StoryCheck(neighborWorld.gifts.Length==3,"gifts_saved: three earned gifts retained");
            SaveStoryMemory();StoryEvent("end","Approved scripted dialogue and staged existing geometry; real combat, interactions, gifts, placement and save/reload. No generation APIs.");
        }

        IEnumerator StoryBattle()
        {
            StoryChapter("二 · 苔岩守卫的试炼","许可让我们前往新土地；战胜守卫，土地才真正属于我们。");
            StoryClear();storyGuardianVisible=true;storyLand.OpenGuardianRoute();
            yield return new WaitForSecondsRealtime(1);
            StoryCamera((DawnCoast.ReferenceHome+Game.expansion.finalPosition)*.5f,39,0,48);
            StoryNote("沿着自然土路过去，守卫看守的是一块尚未布置的空地。");yield return StoryHold(4);
            StoryEvent("montage-start","guardian-approach");yield return StoryWalk(GuardianApproach());StoryEvent("montage-end","guardian-approach");
            StoryCamera(Game.expansion.finalPosition+Vector3.left*2,20,0,46);yield return StoryHold(3);yield return StoryShot("04-guardian-land");
            StoryCheck(Game.expansion.StartGuardianChallenge(),"normal guardian challenge entry");yield return new WaitForSecondsRealtime(.9f);
            StoryCheck(guardianBattle.UsesGuardianPrefab,"approved guardian rig used");yield return StoryShot("05-battle-opening");
            bool defended=false,interrupted=false,weak=false;int safety=0;
            while(guardianBattle.State.Result==GuardianBattleResult.None && safety++<22)
            {
                var s=guardianBattle.State;
                var action=s.Intent==GuardianBattleIntent.HeavyStrike&&(!defended||s.Energy<2)?GuardianBattleAction.Guard:
                    s.CanUseResonance&&(s.Intent==GuardianBattleIntent.Exposed||s.CanInterrupt)?GuardianBattleAction.Resonance:GuardianBattleAction.Attack;
                StoryNote(action==GuardianBattleAction.Guard?"它要重击了——这回合选择守护。":action==GuardianBattleAction.Resonance?
                    s.Intent==GuardianBattleIntent.Exposed?"石甲露出破绽，使用共鸣击。":"趁它蓄力，用共鸣击打断。":"普通攻击积攒能量，留意守卫的下一步动作。");
                yield return StoryHold(1.7f);
                StoryCheck(guardianBattle.SubmitAction(action),"battle action "+action);
                defended|=action==GuardianBattleAction.Guard;interrupted|=guardianBattle.State.Interrupted;weak|=guardianBattle.State.HitWeakPoint;
                yield return UntilV1(()=>guardianBattle.State.CanAct||guardianBattle.State.Result!=GuardianBattleResult.None,8);
            }
            StoryCheck(guardianBattle.State.Result==GuardianBattleResult.Victory&&defended&&interrupted&&weak,"tactical battle completed with defense, interrupt and weak point");
            yield return UntilV1(()=>guardianBattle.ResultReady,6);yield return StoryShot("06-battle-victory");yield return StoryHold(3);
            guardianBattle.DismissResult();yield return null;
            StoryCheck(Game.Progress.firstExpansionUnlocked&&Game.Progress.expansionPermits==0,"victory consumed exactly one permit and persisted ownership");
            StoryCheck(storyLand.UnlockTerritory("rest-garden","guardian-victory:"+Game.Progress.guardianOccupationId),"guardian victory adds exactly one rest garden");
            StoryCheck(storyLand.OwnedIds.Length==2&&!storyLand.HasTerritory("nursery-lower"),"first victory leaves all later districts locked");
            StoryEvent("territory-granted","rest-garden | guardian-victory:"+Game.Progress.guardianOccupationId);
            StoryNote("一张许可，一片属于自己的新土地。现在，可以开始创造了。");
            yield return UntilV1(()=>!Game.expansion.IsRising,15);guardianCoCreateOffer=false;
            StoryCamera(Game.expansion.finalPosition,22,0,48);yield return StoryHold(3);
            yield return StoryWalk(Game.expansion.WalkTarget);
            StoryRecordEmpty("rest-garden");yield return StoryShot("07-owned-land");yield return StoryHold(3);yield return SyncState();
        }

        SmallWorldItem StoryItem(string id,string instance,Vector3 pos,float scale=1,float yaw=0)
        {
            var a=(availableAssets??library.assets).First(x=>x.id==id);
            return new SmallWorldItem {instanceId=instance,assetId=id,assetHash=a.hash,assetVersion=a.version,localPosition=pos,localScale=Vector3.one*scale,localEulerAngles=new Vector3(0,Mathf.Repeat(yaw,360f),0)};
        }
        IEnumerator StoryPreviewStage(string labelText)
        {
            var previous=placedRoot==null?new Dictionary<string,Vector3>():placedRoot.Cast<Transform>().ToDictionary(t=>t.name,t=>t.localPosition);
            StoryNote(labelText);yield return PreviewPlacement(false);
            StoryCheck(previewValid,"creation preview valid: "+labelText+" / "+placementError);
            editorOpen=false;
            StoryBuildCamera(Game.expansion.finalPosition,22);
            yield return StoryGrowPlacedItems();
            yield return StoryMovePlacedItems(previous);
            yield return StoryHold(2.5f);
        }
        IEnumerator StoryFirstCreation()
        {
            const string id="rest-garden";
            StoryChapter("三 · 告诉小禾，这片空地想做什么","预设共创演示 · 玩家确认方案，小禾按计划逐步施工。");
            StoryClear();StoryCamera(Game.expansion.finalPosition,22,-15,48);
            yield return StorySay("这块空地想做成休憩花园。浅色长凳，花沿着两侧，中间留路。",
                "先安排空地和通路，再准备长凳与花草。你确认布局后，我再开始施工。",5);
            storyConceptVisible=true;yield return StoryHold(5);yield return StoryShot("08-design-reference");storyConceptVisible=false;
            yield return StoryPlanConstruction(id,"休憩花园","浅色石凳、两侧花草，中间通路保持宽松。",
                new[]{"整理草土与通路","准备并摆放长凳","种花与点缀苔石","按你的意见调整并定稿"});
            StoryCheck(OpenPlacement(false),"creation editor opens on owned land");editItems.Clear();editorOpen=false;
            editSeamSoftened=true;editSurfaceStyle=SmallWorldGardenSurface.Style;
            StoryWorkshopTask(0,"场景设计","整理地面，留出入口和中央通路");
            storyLand.SetConstructionStage(id,1);StoryEvent("construction-step",id+" | 1 | 草土与通路");
            yield return StoryPreviewStage("施工 1 / 3 · 草土地面与入口通路");yield return StoryShot("09-creation-ground");
            StoryWorkshopDone(0,"地面与通路已铺好");
            StoryWorkshopTask(1,"资产准备","新建长凳资产 · 设计图 → 3D 模型 → 尺寸整理（预设演示）");
            yield return StoryHold(2.5f);
            StoryWorkshopTask(1,"场景装配","把浅色长凳放到右后方");
            editItems.Add(StoryItem("mossy-stone-bench","story-bench",new Vector3(1.7f,0,1.35f),1.65f,220));
            storyLand.SetConstructionStage(id,2);StoryEvent("construction-step",id+" | 2 | 长凳落位");
            yield return StoryPreviewStage("施工 2 / 3 · 长凳落位");yield return StoryShot("10-creation-bench");
            StoryWorkshopDone(1,"长凳已落位，入口保持畅通");
            StoryWorkshopTask(2,"资产准备","复用花簇与苔石，沿两侧成组摆放");
            editItems.Add(StoryItem("garden-flower-cluster","story-flowers-a",new Vector3(3.4f,0,2),.8f));
            editItems.Add(StoryItem("garden-flower-cluster","story-flowers-b",new Vector3(3.3f,0,-2.2f),.8f,80));
            editItems.Add(StoryItem("garden-understory-cluster","story-leaves",new Vector3(-1.8f,0,2.55f),.75f,35));
            editItems.Add(StoryItem("moss-boulder","story-rock",new Vector3(-2.7f,0,-2.15f),.5f));
            var flowerEdges=new[]{new Vector3(3.6f,0,2.5f),new Vector3(4f,0,1.8f),new Vector3(2.5f,0,3f),new Vector3(-3.4f,0,-2.4f),new Vector3(-2.6f,0,-2.7f),new Vector3(-1.9f,0,-3f),new Vector3(3.4f,0,3.2f)};
            for(int n=0;n<flowerEdges.Length;n++)editItems.Add(StoryItem("garden-flower-cluster","story-border-"+n,flowerEdges[n],.62f+(n%3)*.07f,n*47));
            // Leave room for each rotated rock's full bounds and the three patrol stops.
            var stoneEdges=new[]{new Vector3(3.3f,0,2.9f),new Vector3(3.8f,0,2.3f),new Vector3(4.3f,0,.4f),new Vector3(4.1f,0,-1.8f),new Vector3(-2.5f,0,-3.3f),new Vector3(-3.5f,0,-2.2f),new Vector3(-3.2f,0,2.4f)};
            for(int n=0;n<stoneEdges.Length;n++)editItems.Add(StoryItem("moss-boulder","story-edge-stone-"+n,stoneEdges[n],.26f+(n%3)*.06f,n*61));
            storyLand.SetConstructionStage(id,3);StoryEvent("construction-step",id+" | 3 | 花草与苔石");
            yield return StoryPreviewStage("施工 3 / 3 · 花草、苔石与礼物角");yield return StoryShot("11-creation-plants");
            StoryWorkshopDone(2,"两侧花草已经种好");
            StoryWorkshopTask(3,"小禾","等待你检查长凳与通路");
            StoryWorkshopConfirmation("长凳靠岸一点，右侧花往外移，中央再宽一些。",false);
            StoryEvent("player","长凳靠岸一点，右侧花往外移，中央再宽一些。");yield return StoryHold(3);
            StoryWorkshopConfirmation("保留其余布置，只调整长凳和这一丛花。",true);
            var bench=editItems.First(i=>i.instanceId=="story-bench");bench.localPosition.x-=.4f;
            editItems.First(i=>i.instanceId=="story-flowers-a").localPosition.x+=.25f;
            yield return StoryPreviewStage("根据你的意见 · 调整长凳与花的位置");
            yield return StoryShot("workshop-garden-revision");
            StoryWorkshopDone(3,"已调整，通路检查通过");
            StoryWorkshopConfirmation("确认当前花园，应用到家园。",false);yield return StoryHold(2);
            StoryWorkshopConfirmation("玩家已确认 · 应用到家园",true);
            yield return FinalizePlacement();StoryCheck(committedLayout.version>0&&!editorOpen,"first local scene genuinely finalized");
            StoryCompleteConstruction(id);yield return StoryHold(2);StoryWorkshopClose();
            storyStage=storyLand.Stage;SaveStoryMemory();StoryCamera(Game.expansion.finalPosition,22,5,48);
            yield return StoryWalk(Game.expansion.WalkTarget);yield return StoryShot("12-first-garden-saved");yield return StoryHold(3);
        }
        IEnumerator StoryFullWorldMontage()
        {
            StoryChapter("四 · 另一片空地，新的想法","园丁巡查带来一块空地。先获得土地，再决定把它建成什么。");
            StoryClear();StoryCamera(Game.expansion.finalPosition,24,0,43);
            yield return StorySay("花园建好了。我还能怎样开拓？","先做一次园丁巡查：看看入口、走到新栽的花旁，再检查休憩处。完成后可以领取小院北侧的一块空地。",5);
            StoryCheck(committedLayout.version>0&&committedLayout.instances.Any(i=>i.instanceId=="story-bench")&&committedLayout.instances.Any(i=>i.instanceId=="story-flowers-a"),"garden patrol requires the genuinely saved first garden");
            var patrol=new[]{new Vector3(-3.7f,.03f,-.5f),new Vector3(3.5f,.03f,-2.9f),new Vector3(.1f,.03f,2.6f)};
            var tasks=new[]{"检查入口 · 小路可以通行","检查花丛 · 新花已经种下","检查休憩处 · 长凳旁留有空地"};
            for(int i=0;i<patrol.Length;i++)
            {
                yield return StoryWalk(Game.expansion.finalPosition+patrol[i]);
                StoryEvent("territory-objective","garden-patrol:"+(i+1));
                StoryNote("园丁巡查 "+(i+1)+" / 3 · "+tasks[i]);yield return StoryHold(1.6f);
            }
            StoryNote("园丁巡查完成 · 获得空地开拓许可 × 1");yield return StoryHold(3);
            yield return StorySay("领取这张许可，就开发小院旁边这一块。","好，先得到北侧这块空地。等你想好用途，我们再安排施工。",4);
            yield return StoryGrantTerritory("nursery-lower","garden-patrol:"+committedLayout.version,"北侧空地",new Vector3(-12,2.8f,-10),"园丁巡查奖励",30);
            StoryCheck(storyLand.OwnedIds.Length==3&&!storyLand.HasTerritory("nursery-upper")&&!storyLand.HasTerritory("workshop-shore"),"patrol grants only one adjacent nursery plot");
            yield return StorySay("这块空地就做苗圃吧。先留出路，再放育苗床，花和蔬菜分开。",
                "我来规划种植台、育苗床和两侧植物。先看布局，确认后分三步搭建。",4);
            yield return StoryBuildDistrict("nursery-lower","北侧苗圃","在刚获得的空地建苗圃，留中央通道，花和蔬菜分开。",
                new[]{"整理地形与通道","制作育苗床与种植土","种下花苗和蔬菜"});
            StoryCamera(new Vector3(-1,0,-13),58,0,46);yield return StoryHold(4);yield return StoryShot("13-first-two-expansions");
            SaveStoryMemory();
        }
        IEnumerator StoryGrantTerritory(string id,string evidence,string name,Vector3 focus,string reason,float distance)
        {
            StoryClear();StoryCamera(focus,distance,0,43);
            StoryNote("待领取 · "+name+" · "+reason);yield return StoryHold(2);
            int before=storyLand.OwnedIds.Length;
            StoryCheck(storyLand.UnlockTerritory(id,evidence),"claim territory: "+id+" from "+evidence);
            StoryCheck(storyLand.OwnedIds.Length==before+1,"territory reward reveals exactly one district: "+id);
            storyStage=storyLand.Stage;
            yield return UntilV1(()=>storyLand.NavigationReady,5);
            StoryEvent("territory-granted",id+" | "+evidence);
            StoryRecordEmpty(id);
            StoryNote("已领取 · "+name+" · 空地等待你和小禾规划");
            yield return StoryHold(4);yield return StoryShot("territory-"+id);SaveStoryMemory();
        }
        IEnumerator StoryClaimNeighborTerritories()
        {
            // Each gift was genuinely earned from an independent neighbor quest;
            // this demo's additional land reward is authored and recorded explicitly.
            StoryChapter("七 · 三片空地，一起规划","邻居委托带来空地许可。土地的用途，继续由你和小禾决定。");
            StoryCheck(neighborWorld.gifts.Length==3,"neighbor territory rewards require all three actual quest gifts");
            yield return StorySay("朋友送来的礼物收好了，还有哪些开拓奖励？","有三张空地许可。我们先一块一块领取，再讨论用途；你可以把几片土地放在一起规划。",6);
            var claims=new[]{
                new[]{"workshop-shore","ajin-lantern","东侧空地","阿烬 · 暖石委托完成"},
                new[]{"nursery-upper","lumi-flowers","苗圃后方空地","露米 · 星光委托完成"},
                new[]{"ridge-garden","lanlan-stone","高处空地","岚岚 · 风铃委托完成"}};
            var focuses=new[]{new Vector3(26,0,-4),new Vector3(-13,4,7),new Vector3(14,2,14)};
            for(int i=0;i<claims.Length;i++)
            {
                var claim=claims[i];
                StoryCheck(neighborWorld.gifts.Any(g=>g.id==claim[1]),"earned gift authorizes territory: "+claim[1]);
                yield return StoryGrantTerritory(claim[0],"neighbor-gift:"+claim[1],claim[2],focuses[i],claim[3],36);
            }
            yield return StorySay("这三块一起安排吧。东边做工坊，苗圃后面做温室，高处留一片花园。",
                "收到。先规划各区的通路，再准备建筑和植物。每片都按确认后的方案逐步完成。",5);
            yield return StoryBuildDistrict("workshop-shore","岸边工坊","东侧空地安排一间工坊，门前留出工作和休息空间。",
                new[]{"铺设工坊通路","准备房屋与工作陈列","补齐门前植物"});
            yield return StoryBuildDistrict("nursery-upper","温室苗园","苗圃后方添一座温室，与已有苗床连接。",
                new[]{"连接种植台与道路","装配温室与育苗设施","补种花苗与边缘植物"});
            yield return StoryBuildDistrict("ridge-garden","高处花园","保留高处空间，先搭通路和观景台边界，再补植物。",
                new[]{"整理上坡与平台","安装平台边界与休憩设施","种下高处绿植"});
            StoryCheck(!storyLand.PositionOpen(DawnCoast.ReferenceObservatory),"summit remains locked after the visible demo rewards");
            StoryCamera(new Vector3(5,0,-6),76,0,44);yield return StoryHold(4);yield return StoryShot("13-earned-home-world");
        }
        IEnumerator StoryBringGiftsHome()
        {
            StoryClear();StoryEvent("montage-start","return-home");storyTransition="带着礼物回家";
            yield return Visit(0,Profile.ownContinent);yield return UntilV1(()=>!loading&&!restoringOwnedLayout&&coast!=null,60);
            storyLand.Attach(Game,coast);storyStage=storyLand.Stage;Game.atmosphere.Preview(17.2f);
            StoryCheck(storyLand.ReloadTerritories()&&storyLand.OwnedIds.Length==3,"returning home retains exactly the already earned three territories");
            StoryCamera(new Vector3(-1,0,-13),58,0,46);
            yield return null;
            storyTransition=null;StoryEvent("montage-end","return-home");
            yield return StoryClaimNeighborTerritories();
            StoryChapter("八 · 把旅途布置成家","礼物保留朋友和经历的来源；拥有后可以反复用于布置。");
            yield return SyncState();StoryCamera(Game.expansion.finalPosition+Vector3.right*2,22,-15,48);
            yield return StorySay("把今天收到的礼物放进花园，别挡住小路。","我记得你想留出宽松的通路。阿烬的灯、露米的花和岚岚的风铃石，都有自己的位置。",6);
            StoryCheck(OpenPlacement(false),"gift arrangement opens existing saved garden");
            foreach(var gift in neighborWorld.gifts)
            {
                Vector3 pos=gift.id=="ajin-lantern"?new Vector3(3.25f,0,.6f):gift.id=="lumi-flowers"?new Vector3(2.1f,0,-1.8f):new Vector3(-3.8f,0,-2.5f);
                var item=StoryItem(gift.assetId,"story-gift-"+gift.id,pos,gift.scale);item.giftId=gift.id;item.tint=gift.tint;editItems.Add(item);
            }
            yield return PreviewPlacement(false);StoryCheck(previewValid,"earned gifts placed using their real provenance");yield return StoryShot("17-gifts-and-asset-library");yield return StoryHold(5);
            StoryNote("喜欢的花可以复用。也能直接移动、旋转，或者撤销刚才的调整。");
            RememberUndo();var flower=StoryItem("meadow-flower","story-reused-flower",new Vector3(1.65f,0,-2.4f),1.7f);editItems.Add(flower);
            yield return PreviewPlacement(false);StoryCheck(previewValid,"reused owned asset preview valid");
            selectedItem=editItems.FindIndex(i=>i.instanceId==flower.instanceId);ChangeSelected(.25f,0,15,0);
            yield return UntilV1(()=>!editorBusy,15);yield return StoryHold(3);
            placementRedo.Add(DraftJson());ReadDraft(placementUndo.Last());placementUndo.RemoveAt(placementUndo.Count-1);yield return PreviewPlacement(false);
            StoryCheck(previewValid&&editItems.Last().localPosition==flower.localPosition-new Vector3(.25f,0,0),"manual placement undo restores exact position");
            yield return StoryHold(3);yield return FinalizePlacement();StoryCheck(!editorOpen&&committedLayout.instances.Count(i=>!string.IsNullOrEmpty(i.giftId))==3,"gift layout_saved through normal finalization");
            StoryCamera(Game.expansion.finalPosition,23,5,45);yield return StoryShot("18-gifts-at-home");yield return StoryHold(5);
            yield return StorySay("之前留出的纪念角，终于有故事了。","是啊。这里有你打败守卫的勇气，也有今天新认识的三位朋友。",5);
        }
        IEnumerator StoryNightAndMemory()
        {
            StoryChapter("九 · 夜里，把朋友送来的光点亮","日夜流转。家园保存布局，也留下照料和相遇的记录。");
            StoryClear();StoryCamera(Game.expansion.finalPosition,25,0,38);
            foreach(float hour in new[]{18f,19.5f,22f}){Game.atmosphere.Preview(hour);yield return StoryHold(3);}
            yield return UntilV1(()=>lampAnchor!=null,12);StoryCheck(lampAnchor!=null,"gift lantern has a real nighttime interaction");
            StoryNote("把长凳旁的暖焰灯点亮。阿烬送来的这点光，会陪我们等到夜深。");
            StoryEvent("montage-start","lantern-approach");
            ApproachLamp();yield return UntilV1(()=>!walkingToLamp&&!Game.Navigating&&!Game.Busy,65);
            StoryEvent("montage-end","lantern-approach");
            if(!Profile.homecomingLampLit&&NearLamp()){TryNearbyHomecomingLamp();yield return UntilV1(()=>Profile.homecomingLampLit&&!Game.Busy,12);}
            StoryCheck(Profile.homecomingLampLit,"night lamp lit through actual proximity interaction");
            StoryCamera(lampAnchor.position+Vector3.up*.7f,13,0,28);yield return StoryHold(4);yield return StoryShot("19-night-lantern");
            yield return SyncState();yield return LoadMemories();StoryCheck(memoryReply?.scenes?.Length>0,"scene creation memory saved by the actual service");
            yield return StorySay("小禾，今天我们都经历了什么？","你照顾了小芽，战胜了苔岩守卫，还和阿烬、露米、岚岚交换了心意。我记得：你喜欢自然的小院，也要让小路一直畅通。",8);
            SaveStoryMemory();var before=JsonUtility.ToJson(committedLayout);Game.SaveNow();Profile.Save();
            StoryNote("再次回到家园，礼物仍在，今天的故事也还在。");
            StoryClear();StoryEvent("montage-start","reload-home");storyTransition="重新打开家园";
            loading=true;yield return LoadWorldScene("DawnHomeV1");loading=false;
            yield return UntilV1(()=>!restoringOwnedLayout&&coast!=null,60);
            storyLand.Attach(Game,coast);StoryCheck(storyLand.ReloadTerritories()&&storyLand.OwnedIds.Length==6&&!storyLand.PositionOpen(DawnCoast.ReferenceObservatory),"disk reload retains six earned territories and keeps summit locked");StoryEvent("territory-restored",string.Join("|",storyLand.OwnedIds));Game.atmosphere.Preview(22);
            StoryCheck(JsonUtility.ToJson(committedLayout)==before&&File.Exists(LayoutFile)&&previewValid&&placedRoot!=null&&placedRoot.GetComponentsInChildren<HomeLayoutInstance>().Length==committedLayout.instances.Length,"save and scene reload retain exact final layout and rendered instances");
            StoryCheck(SmallWorldProfile.Load().homecomingLampLit&&GardenSave.Load(Game.SavePath,Game.homeId).firstExpansionUnlocked,"ownership and lamp state survive reload from disk");
            yield return LoadNeighbors();StoryCheck(neighborWorld.gifts.Length==3,"neighbor gifts survive re-entry");
            StoryCamera(new Vector3(5,0,-10),83,0,44);
            yield return null;
            storyTransition=null;StoryEvent("montage-end","reload-home");
            yield return StoryHold(5);yield return StoryShot("20-night-world");
            StoryClear();yield return StoryHold(2);
            storyTitle="自己的小世界";storySubtitle="把遇见的朋友、说过的愿望，\n慢慢留在自己的世界里。";storyTitleVisible=true;
            yield return StoryHold(7);yield return StoryShot("21-world-ending");storyTitleVisible=false;
        }

        void DrawStorySeedMarker()
        {
            if(!storyActive||IsVisitor||Game==null||Game.Progress==null||GardenSave.WateredToday(Game.Progress)||WorldOpen||GuardianBusy||storyTitleVisible)return;
            var target=Game.FindInteraction(GardenInteractionKind.Water);
            if(target?.visual==null)return;
            // Anchor to the actual relocated plant, never the old nursery coordinates.
            var screen=Game.worldCamera.WorldToScreenPoint(target.visual.position+Vector3.up*.72f);
            if(screen.z<=0)return;
            var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            float x=screen.x/scale,y=(Screen.height-screen.y)/scale;
            GUI.Box(new Rect(x-60,y-32,120,32),GUIContent.none,hudPill);
            GUI.DrawTexture(new Rect(x-51,y-29,24,24),SmallWorldUiArt.Icon("leaf"));
            GUI.Label(new Rect(x-23,y-30,80,28),"今日小芽",hudNote);
        }
        string storyTransition;
        void DrawStoryOverlay()
        {
            if(storyTransition!=null)
            {
                var oldColor=GUI.color;
                GUI.color=new Color(.09f,.17f,.16f);
                GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
                GUI.color=oldColor;
                var caption=new GUIStyle(hudTitle){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(Screen.height*.037f)};
                GUI.Label(new Rect(0,Screen.height*.43f,Screen.width,Screen.height*.14f),storyTransition,caption);
                return;
            }
            if(!storyDialogueVisible&&!storyConceptVisible&&!storyTitleVisible)return;
            GUI.depth=-180;var previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*Mathf.Min(Screen.width/1280f,Screen.height/720f));
            if(storyHeadingStyle==null)
            {
                storyHeadingStyle=new GUIStyle(title){fontSize=24,wordWrap=true};
                storyBodyStyle=new GUIStyle(label){fontSize=18,wordWrap=true};
                storyEyebrowStyle=new GUIStyle(muted){fontSize=13};
            }
            if(storyTitleVisible)
            {
                GUI.Box(new Rect(58,451,444,212),GUIContent.none,panel);
                GUI.DrawTexture(new Rect(86,473,44,44),SmallWorldUiArt.Icon("leaf"));
                GUI.Label(new Rect(145,478,335,30),"LITTLE WORLD  /  把旅途带回家",storyEyebrowStyle);
                var font=storyHeadingStyle.fontSize;storyHeadingStyle.fontSize=37;
                GUI.Label(new Rect(88,517,385,60),storyTitle,storyHeadingStyle);storyHeadingStyle.fontSize=font;
                GUI.Label(new Rect(90,586,380,65),storySubtitle,storyBodyStyle);
            }
            if(storyDialogueVisible)
            {
                var id=storySpeaker=="阿烬"?"ajin":storySpeaker=="露米"?"lumi":storySpeaker=="岚岚"?"lanlan":"xiaohe";
                var promptStyle=new GUIStyle(storyBodyStyle){fontSize=16};
                float replyHeight=Mathf.Clamp(storyBodyStyle.CalcHeight(new GUIContent(storyReplyLayout),736)+64,116,164);
                float replyY=694-replyHeight;
                float promptHeight=Mathf.Clamp(promptStyle.CalcHeight(new GUIContent(storyUser),579)+24,52,86);
                float promptY=replyY-promptHeight-10;
                GUI.Box(new Rect(424,promptY,666,promptHeight),GUIContent.none,panel);
                GUI.Label(new Rect(445,promptY+13,42,22),"你",storyEyebrowStyle);
                GUI.Label(new Rect(489,promptY+12,579,promptHeight-20),storyUser,promptStyle);
                GUI.Box(new Rect(190,replyY,900,replyHeight),GUIContent.none,panel);
                SmallWorldUiArt.Portrait(new Rect(212,replyY+18,80,80),id);
                GUI.Label(new Rect(326,replyY+13,350,32),storySpeaker,storyHeadingStyle);
                GUI.Label(new Rect(326,replyY+56,736,replyHeight-60),storyReply,storyBodyStyle);
                GUI.Label(new Rect(1110,22,150,26),"预设剧情演示",storyEyebrowStyle);
            }
            if(storyConceptVisible)
            {
                GUI.Box(new Rect(160,70,960,580),GUIContent.none,panel);
                GUI.Label(new Rect(190,89,900,42),"一起规划未来的家",storyHeadingStyle);
                if(storyConcept!=null)GUI.DrawTexture(new Rect(190,140,900,425),storyConcept,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(190,590,870,38),"现有概念图 · 先建这一块，其余土地在后续旅程中逐一获得",storyEyebrowStyle);
            }
            GUI.matrix=previous;
        }

    }
}
