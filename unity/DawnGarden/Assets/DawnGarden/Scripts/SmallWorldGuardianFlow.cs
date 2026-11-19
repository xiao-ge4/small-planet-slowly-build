using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        SmallWorldGuardianBattle guardianBattle;
        GardenExpansion guardianExpansion;
        GameObject guardianPreview, guardianSentinel;
        string guardianEncounter;
        bool guardianReplay, guardianSavePending, guardianSavedVictory, guardianCoCreateOffer;
        bool walkingToGuardian, guardianVictoryObserved;
        public bool GuardianBusy => guardianBattle?.IsActive == true || guardianSavePending;

        IEnumerator PreviewGuardianGame()
        {
            if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot))throw new InvalidOperationException("Guardian preview requires an isolated profile.");
            yield return EnterFromTitle("dawn");
            yield return UntilV1(() => coast != null && Connected && !restoringOwnedLayout && !loading, 60);
            Game.atmosphere.Preview(9.5f); Game.orbit.FocusPlayer();
            Toast("先开补给箱领取许可，再沿旅行手记挑战守卫。已有土地可在锦囊重玩试炼。");
        }

        void ResetGuardianScene()
        {
            if (guardianExpansion != null) guardianExpansion.GuardianChallengeRequested -= BeginGuardianBattle;
            guardianBattle?.Dispose(); guardianBattle = null;
            if (guardianPreview != null) Destroy(guardianPreview);
            guardianPreview = guardianSentinel = null;
            guardianEncounter = null; guardianSavePending = guardianCoCreateOffer = walkingToGuardian = false;
            guardianExpansion = Game?.expansion;
            if (IsVisitor || guardianExpansion == null) return;
            guardianExpansion.GuardianChallengeRequested += BeginGuardianBattle;
            if (!Game.Progress.firstExpansionUnlocked)
            {
                // A render-only preview shows the occupied plot without adding walkable land.
                guardianPreview = Instantiate(guardianExpansion.land.gameObject);
                guardianPreview.name = "苔岩守卫 · 尚未占领的土地预览";
                guardianPreview.transform.SetParent(Game.transform.parent, true);
                guardianPreview.transform.position = guardianExpansion.finalPosition + Vector3.down * .45f;
                foreach (var c in guardianPreview.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var b in guardianPreview.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                guardianPreview.SetActive(true);
                var prefab = Resources.Load<GameObject>("Guardian/MossstoneGuardian");
                if (prefab != null)
                {
                    guardianSentinel = Instantiate(prefab, guardianPreview.transform, false);
                    guardianSentinel.transform.localPosition = new Vector3(-.5f, .05f, .3f);
                    guardianSentinel.transform.localRotation = Quaternion.Euler(0, 220, 0);
                    foreach (var c in guardianSentinel.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    foreach (var a in guardianSentinel.GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;
                }
            }
        }

        Vector3 GuardianApproach()
        {
            var p = Game.expansion.finalPosition + Vector3.left * (Game.expansion.revealHalfSize.x + 1.6f);
            if (NavMesh.SamplePosition(p, out var at, 2, NavMesh.AllAreas)) return at.position;
            return Game.spawn.position;
        }

        bool NearGuardian => Game != null && !IsVisitor && (!storyActive || storyGuardianVisible) && Vector3.Distance(Game.actor.position, GuardianApproach()) < 2.7f;

        public bool TryNearbyGuardian()
        {
            if (GuardianBusy || !NearGuardian || Game.Progress.firstExpansionUnlocked || Game.Busy) return false;
            if (Game.Progress.expansionPermits < 1) Toast("苔岩守卫守着这块地。先到补给箱领取开拓许可，再来挑战。");
            else Game.expansion.ShowOffer();
            return true;
        }

        void ApproachGuardian()
        {
            if (GuardianBusy || Game == null || IsVisitor) return;
            if (homecomingJournal) { homecomingJournal = false; UpdateControls(); }
            if (NearGuardian) { TryNearbyGuardian(); return; }
            if (Game.WalkTo(GuardianApproach()))
            { walkingToGuardian = true; Game.orbit.FocusPlayer(); Toast("前往守卫土地。击败守卫后，才会使用许可并开放布置。"); }
            else Toast("请沿中央小路靠近守卫土地，再按 E 挑战。");
        }

        void BeginGuardianBattle(string encounterId, bool replay)
        {
            if (Game == null || IsVisitor || loading || editorOpen || editorBusy || chatPending || applyingGuideAction || restoringOwnedLayout || homecomingNight || stagingHomecoming || GuardianBusy)
            { guardianEncounter=encounterId; guardianReplay=replay; guardianSavedVictory=false; guardianSavePending=true; SaveGuardianResult(); Toast("先结束当前操作，再来挑战守卫。许可仍保留。"); return; }
            guardianEncounter = encounterId; guardianReplay = replay; guardianVictoryObserved = false;
            coastDestination = -1; pendingWorldTarget = null; walkingToGuardian = walkingToLamp = false;
            Game.CancelMovement();
            ChatOpen = creatorOpen = homecomingJournal = showMemories = WorldOpen = false;
            SetWorldVisible(false);
            guardianBattle?.Dispose(); guardianBattle = new SmallWorldGuardianBattle();
            guardianBattle.Finished += FinishGuardianBattle;
            if (!guardianBattle.Begin(Game, library?.font))
            { guardianSavedVictory=false; guardianSavePending=true; SaveGuardianResult(); Toast("守卫试炼未能打开，许可没有消耗。"); }
            UpdateControls(); StartCoroutine(SyncState());
        }

        void FinishGuardianBattle(GuardianBattleResult result)
        {
            guardianSavedVictory = result == GuardianBattleResult.Victory;
            guardianSavePending = true;
            SaveGuardianResult();
        }

        void SaveGuardianResult()
        {
            if (!guardianSavePending || Game == null) return;
            if (!Game.expansion.CompleteGuardianChallenge(guardianEncounter, guardianSavedVictory, guardianBattle?.IsActive == true))
            { Toast("挑战结果还没保存成功。请重试保存，许可与土地尚未改变。"); UpdateControls(); return; }
            guardianSavePending = false;
            if (guardianSavedVictory && !guardianReplay)
            {
                if (guardianPreview != null) guardianPreview.SetActive(false);
                guardianCoCreateOffer = true;
                bubble = "你做到了！等这片土地稳固，就来聊聊想把它建成什么。";
            }
            else bubble = guardianSavedVictory ? "试炼完成！这是重玩，已有土地保持原样。" : "没关系，许可还在。观察它的蓄力，下次可以守护，或用共鸣击打断。";
            bubbleUntil = Time.unscaledTime + 16;
            UpdateControls(); StartCoroutine(SyncState());
        }

        void UpdateGuardianFlow()
        {
            guardianBattle?.Update(Time.unscaledDeltaTime);
            if (guardianBattle?.IsActive == true && guardianBattle.State.Result == GuardianBattleResult.Victory && !guardianVictoryObserved)
            {
                guardianVictoryObserved = true; guardianSavedVictory = true; guardianSavePending = true;
                SaveGuardianResult(); // Persist the win now; reveal only after leaving the result screen.
            }
            if (guardianPreview != null) guardianPreview.SetActive(Game != null && !GuardianBusy && !Game.Progress.firstExpansionUnlocked && (!storyActive || storyGuardianVisible));
            if (walkingToGuardian && !GuardianBusy && Game != null && !Game.Navigating)
            { walkingToGuardian = false; if (NearGuardian) TryNearbyGuardian(); }
        }

        void DrawGuardianWorldPrompt()
        {
            if (IsVisitor || Game == null || homecomingJournal || Game.Busy || Game.expansion.BlocksInput) return;
            if (!Game.Progress.firstExpansionUnlocked && NearGuardian)
            {
                if (GUI.Button(new Rect(460, 605, 350, 44), "苔岩守卫 · 查看土地挑战 [E]", button)) TryNearbyGuardian();
            }
            if (guardianCoCreateOffer && !Game.expansion.IsRising)
            {
                GUI.Box(new Rect(350, 100, 580, 135), GUIContent.none, panel);
                GUI.Label(new Rect(375, 106, 530, 48), "新土地已经属于你", title);
                GUI.Label(new Rect(375, 150, 530, 36), "告诉小禾想建什么：已有物件可以复用，也可以先看新设计图。", muted);
                if (GUI.Button(new Rect(375, 186, 240, 35), "和小禾布置这片地", button))
                { guardianCoCreateOffer = false; OpenCreator(); }
                if (GUI.Button(new Rect(635, 186, 180, 35), "稍后再聊", button)) guardianCoCreateOffer = false;
            }
        }

        void DrawGuardianInterface()
        {
            if (guardianBattle?.IsActive == true) guardianBattle.Draw();
            if (!guardianSavePending) return;
            GUI.Box(new Rect(365, 225, 550, 225), GUIContent.none, panel);
            GUI.Label(new Rect(390, 240, 500, 50), "保存挑战结果", title);
            GUI.Label(new Rect(390, 293, 480, 65), "保存尚未成功，许可和土地保持原样。请重试；直接退出会按中断挑战处理。", label);
            if (GUI.Button(new Rect(390, 378, 480, 46), "重试保存", button)) SaveGuardianResult();
        }
    }
}
