using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp : MonoBehaviour
    {
        public static SmallWorldApp Instance {get;private set;}
        public SmallWorldLibrary library;
        public SmallWorldProfile Profile {get;private set;}
        public DawnGardenGame Game {get;private set;}
        public bool Connected {get;private set;}
        public bool IsVisitor=>planetIndex!=0 || continentIndex!=Profile.ownContinent;
        public string PlotId=>IsVisitor?"resident-"+(CurrentResident?.id??"unknown"):"home";
        public int planetIndex,continentIndex;
        public bool WorldOpen {get;private set;}
        public bool ChatOpen {get;private set;}
        bool loading,focused=true,showMemories;
        string notice="",bubble="",connectionError="",visitTheme="dawn";
        float noticeUntil,nextSync,nextReminder;
        int ordinaryReminders;
        HashSet<string> reminders=new HashSet<string>();
        GUIStyle panel,title,label,muted,button,small,inputStyle,toggleStyle;
        Texture2D panelTexture,softTexture,buttonHoverTexture,inputTexture;
        [Serializable] public class ConnectRequest {public string clientId,theme;public bool testMode;public int ownContinent;}
        [Serializable] public class GuideLine {public string role,text;}
        [Serializable] public class GuideSettings {public string name;public float dailyCnyCap,reminderCooldownSeconds=180;public int maxOrdinaryReminders=2;}
        [Serializable] public class Budget {public float usedOrReservedCny,capCny;}
        [Serializable] public class ConnectReply {public string clientId,name,creationRunId,serviceMode;public bool agentConfigured;public GuideLine[] history;public GuideSettings settings;public Budget budget;public GuideTurn lastTurn;}
        [Serializable] public class ClientRequest {public string clientId,commandId;}
        [Serializable] public class GameState
        {
            public string plotId,homeId,lastWateredDay;public Vector3 actorPosition;
            public bool isVisitor,wateredToday,chestOpened,expanded;
            public int wateringDays,growthStage,permits,travelEnergy,planetIndex,continentIndex;
            public int guardianLandVersion;
            public string guardianLandState,guardianEncounterId,guardianOccupationId,guardianLastEncounterId,guardianLastOutcome;
            public bool guardianLegacyOwnership,guardianReplay;
        }
        [Serializable] public class StateRequest {public string clientId;public int sequence;public GameState state;}
        GuideSettings guide=new GuideSettings {name="小禾",dailyCnyCap=2};Budget guideBudget=new Budget {capCny=2};
        void Awake()
        {
            if(Instance!=null){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
            foreach(var arg in Environment.GetCommandLineArgs())if(arg.StartsWith("-smallWorldTestRoot="))SmallWorldProfile.TestRoot=arg.Substring("-smallWorldTestRoot=".Length);
            var testMarker=Path.GetFullPath(Path.Combine(Application.dataPath,"../../v1-test-root.txt"));if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot)&&File.Exists(testMarker))SmallWorldProfile.TestRoot=File.ReadAllText(testMarker).Trim();
            showcaseActive=Environment.GetCommandLineArgs().Contains("-smallWorldShowcase");
            if(showcaseActive){if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot))throw new InvalidOperationException("Showcase recording requires an isolated profile");showcaseConfig=JsonUtility.FromJson<ShowcaseConfig>(File.ReadAllText(Path.Combine(SmallWorldProfile.TestRoot,"showcase-config.json")));SmallWorldApi.UseRecordingService(showcaseConfig.serviceBase);}
            ConfigureStory();
            const string testService = "-smallWorldTestService=";
            if (!string.IsNullOrEmpty(SmallWorldProfile.TestRoot))
                foreach (var arg in Environment.GetCommandLineArgs())
                    if (arg.StartsWith(testService, StringComparison.Ordinal))
                        SmallWorldApi.UseRecordingService(arg.Substring(testService.Length));
            Profile=SmallWorldProfile.Load();creationIdea=Profile.creationIdeaDraft??"";continentIndex=Profile.ownContinent;
            SceneManager.sceneLoaded+=SceneLoaded;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
        }
        void Start()
        {
            CreateWorldMap();
            var args=Environment.GetCommandLineArgs();
            if(SubmissionGalleryRequested())StartSubmissionGallery();
            else if(args.Contains("-smallWorldSelfTest")){StartCoroutine(BeginJourney("dawn"));StartCoroutine(RunV1Verification());}
            else if(args.Contains("-smallWorldGuardianTest"))StartCoroutine(VerifyGuardianDemo());
            else if(args.Contains("-smallWorldHomecomingTest"))StartCoroutine(VerifyHomecoming());
            else if(args.Contains("-smallWorldCoastSampleTest"))StartCoroutine(VerifyCoastSample());
            else if(args.Contains("-smallWorldGuardianPreview"))StartCoroutine(PreviewGuardianGame());
            else if(args.Contains("-smallWorldCoastPreview"))StartCoroutine(PreviewCoastGallery());
            else if(storyActive||showcaseActive||args.Any(a=>a=="-smallWorldDetailPreview"||a=="-smallWorldOpenDraft"||a=="-smallWorldStarterDetailPreview"))
                StartCoroutine(BeginJourney(string.IsNullOrEmpty(Profile.theme)?"dawn":Profile.theme));
            else titleOpen=true;
            if(args.Contains("-smallWorldDetailPreview"))StartCoroutine(VerifyGardenDetail());
            if(args.Contains("-smallWorldOpenDraft"))StartCoroutine(OpenSavedDraft());
            if(args.Contains("-smallWorldStarterDetailPreview"))StartCoroutine(VerifyStarterHomeDetails());
            if(showcaseActive)StartCoroutine(RunShowcaseGuarded());
            if(storyActive)StartCoroutine(RunStoryGuarded());
        }
        IEnumerator BeginJourney(string theme)
        {
            if(loading||!SaveBeforeWorldTransition())yield break;loading=true;Profile.theme=theme;Profile.Save();
            if(continentRoots[Profile.ownContinent]!=null)continentRoots[Profile.ownContinent].GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor",theme=="ember"?new Color(.76f,.44f,.20f):new Color(.43f,.64f,.34f));
            yield return SmallWorldApi.Request("connect",new ConnectRequest {clientId=Profile.clientId,theme=theme,testMode=!string.IsNullOrEmpty(SmallWorldProfile.TestRoot),ownContinent=Profile.ownContinent},json=>{
                var result=JsonUtility.FromJson<ConnectReply>(json);Connected=true;guide=result.settings??guide;guide.name=result.name;guideBudget=result.budget??guideBudget;
                chatLines=(result.history??new GuideLine[0]).ToList();guideLines=chatLines;lastTurn=result.lastTurn;Profile.creationRunId=result.creationRunId;Profile.Save();connectionError="";
            },error=>{Connected=false;connectionError=error;});
            yield return LoadNeighbors();
            planetIndex=0;continentIndex=Profile.ownContinent;visitTheme=theme;
            yield return LoadWorldScene(theme=="ember"?"EmberHomeV1":"DawnHomeV1");
            loading=false;
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();if(game==null||game.gameObject.scene!=scene)return;
            Game=game;game.dailyCare=true;game.isVisitor=IsVisitor;
            if((showcaseActive||storyActive)&&movie!=null){var listener=UnityEngine.Object.FindFirstObjectByType<AudioListener>();if(listener!=null)movie.AttachAudio(listener.gameObject);}
            var path=Profile.HomeSavePath(PlotId);
            if(IsVisitor)GardenSave.Write(path,new GardenProgress {homeId=game.homeId,chestOpened=true,starterRewardGranted=true,lastWateredDay=CurrentResident?.lastWateredDay??"",wateringDays=CurrentResident?.wateringDays??0,watered=(CurrentResident?.wateringDays??0)>0});
            game.FindInteraction(GardenInteractionKind.Water).displayName=game.homeId.Contains("ember")?"照料暖焰芽":game.homeId.Contains("star-dew")?"照料星露芽":game.homeId.Contains("wind")?"照料风铃草":"照料新芽";
            game.Initialize(path);game.GameEvent+=OnGameEvent;game.gameObject.AddComponent<SmallWorldGrowth>().Bind(game,library);game.actor.gameObject.AddComponent<SmallWorldPetResponse>().Bind(game,game.animator);
            ResetHomecomingScene(game);
            ResetGuardianScene();
            SetupSubmissionScene();
            if(storyActive&&!IsVisitor&&storyLand!=null&&coast!=null)
                storyLand.Attach(game,coast);
            if(IsVisitor){residentReady=false;game.homeTitle=CurrentResident?.homeName??"邻居家园";game.homeSubtitle=(CurrentResident?.name??"邻居")+"的家园 · 欢迎做客";StartCoroutine(SetupResident());}
            WorldOpen=false;SetWorldVisible(false);nextSync=Time.unscaledTime+2;nextReminder=Time.unscaledTime+12;
            bubble=IsVisitor?"走近"+CurrentResident?.name+"聊聊，也可以先帮新芽浇水。":"欢迎回家。想聊天或布置土地，随时找我。";bubbleUntil=Time.unscaledTime+12;chatResidentId=null;pendingWorldTarget=null;
            editorOpen=false;creatorOpen=false;ritualOpen=false;ChatOpen=false;showMemories=false;UpdateControls();
            if(!IsVisitor)StartCoroutine(RestoreOwnedLayout());
            if(!chatPending&&!string.IsNullOrEmpty(Profile.pendingTurnId))StartCoroutine(ResumePendingChat());
        }
        void Update()
        {
            if(titleOpen||loading){UpdateControls();return;}
            if(!titleOpen)playableLand?.SyncPlayableProgress();
            UpdateGuardianFlow();
            if (GuardianBusy) { UpdateControls(); return; }
            UpdateWorldMap();UpdatePlacementInput();UpdateNeighborInput();UpdateDraftAutosave();UpdateRitual();
            if(Game!=null && Game.Progress!=null)
            {
                UpdateControls();
                if(Time.unscaledTime>nextSync){nextSync=Time.unscaledTime+15;StartCoroutine(SyncState());}
                if(focused&&!Profile.remindersMuted && !WorldOpen&&!ChatOpen&&!editorOpen&&!creatorOpen&&!Game.Busy&&!Game.expansion.BlocksInput && Time.unscaledTime>nextReminder)
                {
                    nextReminder=Time.unscaledTime+guide.reminderCooldownSeconds;
                    if(ordinaryReminders<guide.maxOrdinaryReminders)
                    {
                        string key=null,text=null;
                        if(!GardenSave.WateredToday(Game.Progress)){key=PlotId+":water:"+GardenSave.Today;text="今天的新芽还没喝到水。要不要先去看看？";}
                        else if(!IsVisitor&&!Game.Progress.chestOpened){key="first-chest";text="那只补给箱还没打开，里面有一张开拓许可。";}
                        else if(!IsVisitor&&Game.Progress.expansionPermits>0&&!Game.Progress.firstExpansionUnlocked){key="permit";text="锦囊里有开拓许可。挑战相邻土地的苔岩守卫，胜利后就能占领。";}
                        if(key!=null&&reminders.Add(key)){bubble=text;bubbleUntil=Time.unscaledTime+12;ordinaryReminders++;}
                    }
                }
            }
            PollCreationIfNeeded();UpdateCompanionFlow();UpdateLandResponse();UpdateHomecoming();UpdateCoastDestination();
        }
        void OnGameEvent(string kind)
        {
            if(kind=="water"||kind=="chest")Game.actor.GetComponent<SmallWorldPetResponse>()?.Respond(kind=="chest"?"gift":"grow");
            if(kind=="water") {Toast(IsVisitor?"新芽已喝到水":"今天的照料完成了");if(IsVisitor)StartCoroutine(NeighborCare());}
            if(kind=="chest")bubble="许可已经放进锦囊了。先挑战苔岩守卫，胜利后再使用许可占领土地。";
            if(kind=="water")bubble=Game.homeId.Contains("ember")?"暖焰芽喝到了温泉水，慢慢暖起来了。":Game.homeId.Contains("star-dew")?"星露芽在夜色里精神起来了。":Game.homeId.Contains("wind")?"风铃草轻轻晃了晃，像在向你道谢。":"今天的照料完成了。新芽会记住这份耐心，明天还会继续长大。";
            bubbleUntil=Time.unscaledTime+12;StartCoroutine(SyncState());
        }
        public IEnumerator SyncState()
        {
            if(Game?.Progress==null || string.IsNullOrEmpty(Profile.theme))yield break;
            if(!Connected)
            {
                yield return SmallWorldApi.Request("connect",new ConnectRequest {clientId=Profile.clientId,theme=Profile.theme,testMode=!string.IsNullOrEmpty(SmallWorldProfile.TestRoot),ownContinent=Profile.ownContinent},json=>{Connected=true;connectionError="";var c=JsonUtility.FromJson<ConnectReply>(json);guide=c.settings??guide;},error=>connectionError=error);
                if(!Connected)yield break;
            }
            var p=Game.Progress;Profile.sequence++;Profile.Save();
            var state=new GameState {plotId=PlotId,homeId=Game.homeId,isVisitor=IsVisitor,wateredToday=GardenSave.WateredToday(p),lastWateredDay=p.lastWateredDay??"",wateringDays=p.wateringDays,growthStage=Mathf.Min(3,p.wateringDays),chestOpened=p.chestOpened,expanded=p.firstExpansionUnlocked,permits=p.expansionPermits,travelEnergy=Profile.travelEnergy,planetIndex=planetIndex,continentIndex=continentIndex,actorPosition=Game.actor.position,guardianLandVersion=p.guardianLandVersion,guardianLandState=p.guardianLandState??"",guardianEncounterId=p.guardianEncounterId??"",guardianOccupationId=p.guardianOccupationId??"",guardianLastEncounterId=p.guardianLastEncounterId??"",guardianLastOutcome=p.guardianLastOutcome??"",guardianLegacyOwnership=p.guardianLegacyOwnership,guardianReplay=p.guardianReplay};
            yield return SmallWorldApi.Request("snapshot",new StateRequest {clientId=Profile.clientId,sequence=Profile.sequence,state=state},_=>{},error=>{Connected=false;connectionError=error;});
        }
        void UpdateControls(){if(Game!=null){Game.externalControlsBlocked=titleOpen||gameSettingsOpen||GuardianBusy||WorldOpen||ChatOpen||showMemories||editorOpen||editorBusy||ritualOpen||loading||homecomingJournal||storyWorkshopOpen;Game.showHud=!WorldOpen&&!GuardianBusy&&!titleOpen;}}
        public void OpenChat(){if(GuardianBusy)return;gameSettingsOpen=false;chatResidentId=null;chatLines=guideLines;ChatOpen=true;WorldOpen=false;SetWorldVisible(false);UpdateControls();chatScroll.y=100000;StartCoroutine(RefreshGuideSettings());if(!string.IsNullOrEmpty(Profile.creationRunId)){nextCreationPoll=Time.unscaledTime+3;StartCoroutine(RefreshCreation());}if(!chatPending&&!string.IsNullOrEmpty(Profile.pendingTurnId))StartCoroutine(ResumePendingChat());}
        public void OpenCreator()
        {
            if(GuardianBusy)return;
            if(IsVisitor){Toast("先回自己的家园，再一起布置。");return;}
            if(Game?.Progress==null||!GardenGuardianLand.CanCreate(Game.Progress)||Game.expansion.IsRising){Toast("等新的土地升起后，再一起创造。");return;}
            OpenChat();chatResidentId=null;if(!string.IsNullOrEmpty(Profile.creationRunId))StartCoroutine(RefreshCreation());
        }
        public void Toast(string text){notice=text;noticeUntil=Time.unscaledTime+6;}
        public bool PointerOverUI(Vector2 p)
        {
            if(storyWorkshopOpen&&StoryWorkshopBounds.Contains(p))return true;
            if(gameSettingsOpen||GuardianBusy||guardianCoCreateOffer||ritualOpen||titleOpen||homecomingJournal||string.IsNullOrEmpty(Profile?.theme)||WorldOpen||ChatOpen||creatorOpen||showMemories)return true;
            if(editorOpen)return new Rect(910,60,350,580).Contains(p)||new Rect(350,615,910,90).Contains(p);
            return new Rect(890,510,370,158).Contains(p)||new Rect(20,20,298,68).Contains(p)||new Rect(20,91,140,40).Contains(p)
                ||new Rect(982,20,276,65).Contains(p)||new Rect(20,600,140,88).Contains(p)||new Rect(490,605,300,44).Contains(p);
        }
        void Styles()
        {
            if(panel!=null)return;
            var font=library?.font??Font.CreateDynamicFontFromOSFont(new[]{"PingFang SC","Arial Unicode MS"},18);
            panelTexture=new Texture2D(1,1);panelTexture.SetPixel(0,0,new Color(.98f,.97f,.92f,.97f));panelTexture.Apply();
            softTexture=new Texture2D(1,1);softTexture.SetPixel(0,0,new Color(.035f,.09f,.055f));softTexture.Apply();
            buttonHoverTexture=new Texture2D(1,1);buttonHoverTexture.SetPixel(0,0,new Color(.065f,.15f,.09f));buttonHoverTexture.Apply();
            inputTexture=new Texture2D(1,1);inputTexture.SetPixel(0,0,new Color(.84f,.88f,.82f));inputTexture.Apply();
            panel=new GUIStyle(GUI.skin.box){normal={background=panelTexture},padding=new RectOffset(22,22,20,20)};
            panel.normal.textColor=new Color(.20f,.34f,.25f);
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=25,fontStyle=FontStyle.Bold,normal={textColor=new Color(.20f,.34f,.25f)}};
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true,normal={textColor=new Color(.20f,.29f,.23f)}};
            muted=new GUIStyle(label){fontSize=14,normal={textColor=new Color(.43f,.49f,.39f)}};
            small=new GUIStyle(muted){fontSize=12};button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,padding=new RectOffset(12,12,10,10)};
            inputStyle=new GUIStyle(GUI.skin.textArea){font=font,fontSize=18,wordWrap=true,padding=new RectOffset(10,10,8,8)};
            toggleStyle=new GUIStyle(GUI.skin.toggle){font=font,fontSize=16,wordWrap=true,padding=new RectOffset(24,4,3,3)};
            toggleStyle.normal.textColor=toggleStyle.hover.textColor=toggleStyle.active.textColor=toggleStyle.onNormal.textColor=toggleStyle.onHover.textColor=toggleStyle.onActive.textColor=new Color(.20f,.29f,.23f);
            button.normal.background=softTexture;button.normal.textColor=Color.white;button.hover.background=buttonHoverTexture;button.hover.textColor=Color.white;button.active.background=buttonHoverTexture;button.active.textColor=Color.white;button.focused.background=buttonHoverTexture;button.focused.textColor=Color.white;
            inputStyle.normal.background=inputTexture;inputStyle.normal.textColor=new Color(.16f,.25f,.19f);inputStyle.focused.background=inputTexture;inputStyle.focused.textColor=inputStyle.normal.textColor;
            PrepareHomecomingStyles(font);
            PrepareIllustratedSkin(font);
        }
        void OnGUI()
        {
            Styles();GUI.depth=-30;var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUI.skin=storySkin;
            if(loading){GUI.Box(new Rect(440,300,400,95),"正在前往…",panel);GUI.matrix=Matrix4x4.identity;return;}
            if(storyActive&&(storyTitleVisible||storyTransition!=null)){GUI.matrix=Matrix4x4.identity;DrawStoryOverlay();return;}
            if(titleOpen||string.IsNullOrEmpty(Profile.theme))DrawHomecomingTitle();
            else if(storyActive&&storyWorkshopOpen){}
            else if(GuardianBusy)DrawGuardianInterface();
            else if(WorldOpen)DrawWorldInterface();
            else
            {
                if(!editorOpen&&!ChatOpen&&!showMemories&&!ritualOpen&&Game!=null&&!Game.expansion.BlocksInput)
                {if(!storyDialogueVisible){DrawCompactHud();DrawNeighborLabels();}}
                if(Game!=null&&!ChatOpen&&!showMemories&&!editorOpen&&!ritualOpen)Game.expansion.DrawInterface(panel,title,label,muted,button);
                if(ritualOpen)DrawRitual();if(ChatOpen)DrawChat();if(showMemories)DrawMemories();if(editorOpen)DrawPlacement();
                if(homecomingJournal)DrawHomecomingJournal();
                if(gameSettingsOpen&&!storyDialogueVisible&&!editorOpen&&!ChatOpen&&!ritualOpen)DrawGameSettings();
            }
            if(storyActive)DrawStorySeedMarker();
            if(Time.unscaledTime<noticeUntil&&!ritualOpen&&!storyDialogueVisible){GUI.Box(new Rect(350,675,900,36),GUIContent.none,panel);GUI.Label(new Rect(365,680,870,32),notice,muted);}
            GUI.matrix=Matrix4x4.identity;
            if(showcaseActive&&showcaseIntro)DrawShowcaseIntro();
            if(storyActive){DrawStoryOverlay();DrawStoryWorkshop();}
        }
        void OnApplicationFocus(bool value){focused=value;Application.targetFrameRate=(showcaseActive||storyActive)?30:value?60:20;if(!value && Connected)StartCoroutine(SmallWorldApi.Request("summary",new ClientRequest {clientId=Profile.clientId},_=>{}));}
        void OnApplicationQuit(){if(editorOpen)SaveDraft();Game?.SaveNow();Profile?.Save();}
        void OnDestroy(){guardianBattle?.Dispose();if(guardianExpansion!=null)guardianExpansion.GuardianChallengeRequested-=BeginGuardianBattle;if(guardianPreview!=null)Destroy(guardianPreview);if(Instance==this){Instance=null;SceneManager.sceneLoaded-=SceneLoaded;}if(panelTexture!=null)Destroy(panelTexture);if(softTexture!=null)Destroy(softTexture);if(buttonHoverTexture!=null)Destroy(buttonHoverTexture);if(inputTexture!=null)Destroy(inputTexture);if(coverShade!=null)Destroy(coverShade);foreach(var texture in homecomingTextures)if(texture!=null)Destroy(texture);}
    }
}
