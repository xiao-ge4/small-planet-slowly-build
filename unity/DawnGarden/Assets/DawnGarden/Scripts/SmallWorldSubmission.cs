using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        bool submissionGallery;
        string submissionRegularRoot;
        SmallWorldProfile submissionRegularProfile;
        SmallWorldStoryLand playableLand;
        bool SubmissionSwitchBusy => loading || chatPending || editorBusy || GuardianBusy || restoringOwnedLayout
            || applyingGuideAction || creationBusy || neighborBusy || neighborPreparing || homecomingNight || stagingHomecoming;

        void StartSubmissionGallery()
        {
            if (SubmissionSwitchBusy) { Toast("请先完成当前操作，再切换存档。"); return; }
            if (!SaveBeforeWorldTransition()) return;
            StopAllCoroutines();
            StartCoroutine(BeginSubmissionGallery());
        }

        bool SubmissionGalleryRequested()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var page))
                return page.Query.TrimStart('?').Split('&').Any(part => part == "gallery=1");
#endif
            return false;
        }

        IEnumerator BeginSubmissionGallery()
        {
            if (loading) yield break;
            if (!submissionGallery)
            {
                submissionRegularRoot = SmallWorldProfile.TestRoot;
                submissionRegularProfile = Profile;
                Game?.SaveNow(); Profile.Save();
                SmallWorldProfile.TestRoot = Path.Combine(Application.persistentDataPath, "SmallPlanet-Gallery-v1");
                Profile = SmallWorldProfile.Load();
                submissionGallery = true;
            }
            ResetSubmissionRuntimeContext();
            var path = Profile.HomeSavePath("home");
            if (!File.Exists(path))
            {
                var state = new GardenProgress { homeId = "dawn-garden-starter-v1", chestOpened = true,
                    starterRewardGranted = true, firstExpansionUnlocked = true };
                GardenGuardianLand.Restore(state);
                GardenSave.Write(path, state);
            }
            yield return BeginJourney("dawn");
            titleOpen = false;
            Game.orbit.ToggleOverview();
            Toast("完整家园展示 · 独立存档，观星台已开放。相邻空地可真实布置并保存。");
        }

        void SetupSubmissionScene()
        {
            playableLand = null;
            if (storyActive || showcaseActive || IsVisitor || coast == null) return;
            if (submissionGallery)
            {
                Game.homeTitle = "晨曦花园 · 完整展示";
                return;
            }
            playableLand = coast.gameObject.AddComponent<SmallWorldStoryLand>();
            playableLand.AttachPlayable(Game, coast);
        }

        void ReturnSubmissionTitle()
        {
            if (SubmissionSwitchBusy) { Toast("请先完成当前操作，再返回标题。"); return; }
            if (!SaveBeforeWorldTransition()) return;
            StopAllCoroutines();
            if (submissionGallery)
            {
                SmallWorldProfile.TestRoot = submissionRegularRoot;
                Profile = submissionRegularProfile ?? SmallWorldProfile.Load();
                submissionGallery = false;
                submissionRegularProfile = null;
            }
            ResetSubmissionRuntimeContext();
            titleOpen = true; titleThemes = false;
            ChatOpen = WorldOpen = gameSettingsOpen = homecomingJournal = editorOpen = ritualOpen = false;
            SetWorldVisible(false); UpdateControls();
            StartCoroutine(UnloadWorldForTitle());
        }
    }
}
