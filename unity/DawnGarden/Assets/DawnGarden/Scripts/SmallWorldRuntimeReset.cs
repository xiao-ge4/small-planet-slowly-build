using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        int worldSceneEpoch;
        const string TransitionSceneName = "SmallWorldMemoryTransition";

        bool SaveBeforeWorldTransition()
        {
            if (editorOpen) SaveDraft();
            Game?.CancelMovement();
            if (Game != null && !Game.SaveNow())
            {
                Toast("家园还没有成功保存，请稍后再出发。");
                return false;
            }
            Profile?.Save();
            return true;
        }

        // A Single load keeps the old world alive while the next one loads. On
        // WebGL that can require two complete coastlines in the same Wasm heap.
        // Keep the persistent app/atlas, unload into an empty runtime scene, and
        // finish destruction/asset reclamation before allocating the next world.
        IEnumerator UnloadWorldScene(string destination = "title")
        {
            Debug.Log("[SmallWorldMemory] Unloading for " + destination + "; allocated MiB=" +
                (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)));
            worldSceneEpoch++; neighborSceneEpoch++;
            Game?.CancelMovement();
            if (Game != null) Game.GameEvent -= OnGameEvent;
            if (guardianExpansion != null) guardianExpansion.GuardianChallengeRequested -= BeginGuardianBattle;
            guardianExpansion = null; guardianBattle?.Dispose(); guardianBattle = null;
            // Story land can live on the persistent app. Release its cropped
            // meshes and scene references while the old coast is still valid.
            storyLand?.Dispose();
            playableLand?.Dispose();
            Game = null; coast = null; playableLand = null;
            gardenSurface = null; placedRoot = null; placementOutline = null;
            guardianPreview = guardianSentinel = residentActor = null;
            lampAnchor = null; homecomingLantern = null;
            pendingWorldTarget = ritualTarget = null; worldTargets.Clear();
            residentReady = neighborPreparing = neighborBusy = restoringOwnedLayout = false;
            editorOpen = creatorOpen = ChatOpen = WorldOpen = ritualOpen = homecomingJournal = false;
            SetWorldVisible(false);

            var transition = SceneManager.GetSceneByName(TransitionSceneName);
            if (!transition.IsValid() || !transition.isLoaded)
                transition = SceneManager.CreateScene(TransitionSceneName);
            SceneManager.SetActiveScene(transition);
            var oldScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != transition && scene.isLoaded) oldScenes.Add(scene);
            }
            foreach (var scene in oldScenes)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
            oldScenes.Clear();
            // OnDestroy owns procedural meshes, materials, textures and navmesh
            // data. Its Destroy calls need a frame before the assets sweep.
            yield return null;
            GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
            yield return null;
            Debug.Log("[SmallWorldMemory] Ready for " + destination + "; allocated MiB=" +
                (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)));
        }

        IEnumerator LoadWorldScene(string sceneName)
        {
            yield return UnloadWorldScene(sceneName);
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        IEnumerator UnloadWorldForTitle()
        {
            loading = true;
            yield return UnloadWorldScene();
            loading = false;
        }

        // The caller saves the old profile and stops its coroutines first, then
        // selects the target Profile before invoking this in-memory reset.
        void ResetSubmissionRuntimeContext()
        {
            Game?.CancelMovement();
            if(Game!=null){Game.externalControlsBlocked=true;Game.showHud=false;}
            Connected=false;loading=false;connectionError="";
            notice=bubble="";noticeUntil=bubbleUntil=0;
            nextSync=nextReminder=0;ordinaryReminders=0;reminders.Clear();
            planetIndex=0;continentIndex=Profile?.ownContinent??0;
            visitTheme=string.IsNullOrEmpty(Profile?.theme)?"dawn":Profile.theme;
            ChatOpen=WorldOpen=showMemories=gameSettingsOpen=false;
            SetWorldVisible(false);

            committedLayout=new SmallWorldLayout();
            editItems.Clear();renderedItems.Clear();placementUndo.Clear();placementRedo.Clear();
            editorOpen=editorBusy=restoringOwnedLayout=previewValid=editSeamSoftened=false;
            editSurfaceStyle=placementError=savedDraft=dragBefore="";
            selectedItem=-1;nextDraftSave=0;SuccessfulPointerDrags=0;
            draggingItem=dragMoved=false;dragCandidateValid=true;dragBeginFrame=0;
            dragStart=dragOffset=dragCandidate=Vector3.zero;paletteScroll=dragPointer=Vector2.zero;
            availableAssets=null;
            if(placedRoot!=null)Destroy(placedRoot.gameObject);placedRoot=null;
            if(placementOutline!=null)Destroy(placementOutline.gameObject);placementOutline=null;
            gardenSurface=null;

            visualReviewBusy=visualReviewPassed=visualReviewChanged=visualOverrideAccepted=visualReviewFinished=false;
            visualReviewMessage=reviewedDraftId="";reviewedHour=-1;
            creation=null;creatorOpen=creationBusy=allowNewModel=unknownModelConfirmed=showCreationBudget=false;
            creationIdea=Profile?.creationIdeaDraft??"";
            creationRevision=lastCreationPhase=lastCreationError="";nextCreationPoll=0;creationScroll=Vector2.zero;
            enlargedDesign=null;
            foreach(var texture in creatorImages.Values)if(texture!=null)Destroy(texture);
            creatorImages.Clear();imageLoads.Clear();

            queuedGuideAction=null;applyingGuideAction=inspirationBusy=inspirationDirty=false;
            guideActionError=inspirationHash=expandedIdeaId="";inspirationIdeas=new HomeIdea[0];
            chatPending=false;chatInput="";chatResidentId=null;lastTurn=null;memoryReply=null;
            memoryEditing=null;memoryText="";chatScroll=memoryScroll=Vector2.zero;showOptions=false;
            chatLines=new List<GuideLine>();guideLines=chatLines;
            guide=new GuideSettings {name="小禾",dailyCnyCap=2};guideBudget=new Budget {capCny=2};

            neighborWorld=new WorldReply {residents=new Resident[0],gifts=new Gift[0]};
            neighborBusy=residentReady=neighborPreparing=false;lastNeighborError="";neighborSceneEpoch++;
            pendingWorldTarget=null;worldTargets.Clear();residentAnchor=Vector3.zero;
            if(residentActor!=null)Destroy(residentActor);residentActor=null;
            ritualOpen=ritualSubmitting=ritualWasHeld=ritualPointerHeld=ritualHints=false;
            ritualHeat=ritualStarted=ritualDemoUntil=ritualHeatStarted=0;ritualInput=0;
            ritualTuning=new[]{0,0,0};ritualMessage="";ritualTarget=null;ritualAnchor=Vector3.zero;

            if(guardianExpansion!=null)guardianExpansion.GuardianChallengeRequested-=BeginGuardianBattle;
            guardianExpansion=null;guardianBattle?.Dispose();guardianBattle=null;
            if(guardianPreview!=null)Destroy(guardianPreview);
            guardianPreview=guardianSentinel=null;guardianEncounter=null;
            guardianReplay=guardianSavePending=guardianSavedVictory=guardianCoCreateOffer=walkingToGuardian=guardianVictoryObserved=false;
            homecomingJournal=homecomingNight=walkingToLamp=stagingHomecoming=false;
            lampAnchor=null;homecomingLantern=null;coast=null;playableLand=null;coastDestination=-1;
        }
    }
}
