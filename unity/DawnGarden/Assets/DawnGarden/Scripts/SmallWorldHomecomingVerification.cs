using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        [Serializable]
        sealed class HomecomingCheck
        {
            public bool passed;
            public string failure;
            public string[] checks;
        }
        readonly List<string> journeyChecks = new();

        void CheckJourney(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            journeyChecks.Add(description);
            Debug.Log("HOMECOMING " + description);
        }

        IEnumerator VerifyHomecoming() => RunHomecomingChecks(WalkHomecoming(), "homecoming-report.json");

        IEnumerator RunHomecomingChecks(IEnumerator scenario, string reportName)
        {
            if (string.IsNullOrEmpty(SmallWorldProfile.TestRoot)) yield break;
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            string failure = "";
            while (stack.Count > 0)
            {
                bool next = false;
                object current = null;
                try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
                catch (Exception e) { failure = e.ToString(); Debug.LogError(failure); break; }
                if (!next) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            var result = new HomecomingCheck { passed = failure.Length == 0, failure = failure, checks = journeyChecks.ToArray() };
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot, reportName), JsonUtility.ToJson(result, true));
            Application.Quit(result.passed ? 0 : 2);
        }

        IEnumerator JourneyScreenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(SmallWorldProfile.TestRoot, name + ".png"));
            yield return new WaitForSecondsRealtime(.35f);
        }

        IEnumerator WalkHomecoming()
        {
            titleOpen = true;
            yield return new WaitForSecondsRealtime(.6f);
            CheckJourney(Profile.theme == "", "fresh profile starts at title");
            yield return JourneyScreenshot("01-title");
            yield return EnterFromTitle("dawn");
            yield return UntilV1(() => Game != null && Connected && !restoringOwnedLayout);
            CheckJourney(!titleOpen && Game != null && Connected && Journey == JourneyStep.Supply, "playable garden connected from title");
            Profile.remindersMuted = true;
            Game.atmosphere.Preview(8.2f);
            if (coast != null) yield return VerifyCoast();
            yield return JourneyScreenshot("02-garden");
            ToggleHomecomingJournal();
            yield return JourneyScreenshot("03-journal");
            ToggleHomecomingJournal();
            CheckJourney(Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Water), true), "watering route starts");
            yield return UntilV1(() => GardenSave.WateredToday(Game.Progress) && !Game.Busy);
            CheckJourney(GardenSave.WateredToday(Game.Progress), "real watering completed");

            var oldYaw = Game.orbit.yaw;
            Game.orbit.yaw = 125;
            Game.orbit.distance = 10.5f;
            yield return new WaitForSecondsRealtime(.7f);
            File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot, "cover-candidate.png"), CaptureHome());
            Game.orbit.yaw = oldYaw;
            CheckJourney(Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Chest), true), "chest route starts");
            yield return UntilV1(() => Game.Progress.chestOpened && !Game.Busy);
            CheckJourney(Game.Progress.chestOpened && Game.expansion.Accept(), "chest permit opens adjacent land");
            yield return UntilV1(() => !Game.expansion.IsRising);
            CheckJourney(Journey == JourneyStep.Visit, "journal directs player to Ajin");
            if (coast != null)
            {
                GoToCoastDistrict(4);
                yield return UntilV1(() => IsVisitor, 45);
                CheckJourney(IsVisitor, "walked to the ferry and traveled by its station");
            }
            else yield return Visit(0, (Profile.ownContinent + 1) % 4);
            yield return UntilV1(() => residentReady && !neighborPreparing);
            CheckJourney(CurrentResident?.id == "ajin" && residentReady, "to Ajin's home through normal travel");
            Game.atmosphere.Preview(9.4f);
            yield return VerifyAjinMotions();
            yield return WalkJourneyTarget(worldTargets.First(t => t.kind == "resident"));
            CheckJourney(ChatOpen, "resident interaction opens dialogue");
            yield return JourneyScreenshot("04-ajin");
            yield return NeighborAction("accept_quest", -1, true);
            ClearMoviePanels();
            CheckJourney(Journey == JourneyStep.Care && Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Water), true), "neighbor asks for care");
            yield return UntilV1(() => CurrentResident.wateringDays > 0 && !Game.Busy && !neighborBusy);
            CheckJourney(Journey == JourneyStep.Find, "care results persisted");
            foreach (var target in worldTargets.Where(t => t.kind == "search").ToArray())
                yield return WalkJourneyTarget(target);
            CheckJourney(CurrentResident.collected.Length == 3 && Journey == JourneyStep.Warm, "collected all three warm stones through normal interaction");
            yield return WalkJourneyTarget(ritualTarget);
            CheckJourney(ritualOpen, "hearth minigame opens at the hearth");
            showcaseActive = true;
            showcaseHeatHeld = true;
            yield return UntilV1(() => ritualHeat >= .67f, 6);
            showcaseHeatHeld = showcaseActive = false;
            yield return UntilV1(() => CurrentResident.ritualComplete && !ritualSubmitting, 20);
            CheckJourney(CurrentResident.ritualComplete && Journey == JourneyStep.Gift, "hearth task completed");
            yield return WalkJourneyTarget(worldTargets.First(t => t.kind == "resident"));
            yield return NeighborAction("receive_gift", -1, true);
            CheckJourney(AjinGift != null && Journey == JourneyStep.Home, "earned gift in inventory");
            yield return Visit(0, Profile.ownContinent);
            yield return UntilV1(() => !restoringOwnedLayout);
            CheckJourney(Journey == JourneyStep.Arrange, "gift returned home");
            Game.atmosphere.Preview(9);
            if (coast != null)
            {
                GoToCoastDistrict(2);
                yield return UntilV1(() => ChatOpen && !Game.Navigating, 35);
                CheckJourney(ChatOpen && NearbyCoastStation() == 2, "walked into the workshop and opened companion chat");
            }
            else OpenChat();
            yield return ArrangeHomecoming();
            yield return UntilV1(() => editorOpen && !editorBusy && !applyingGuideAction, 45);
            CheckJourney(lastTurn?.state == "complete" && lastTurn.hasLayoutProposal, "live companion returned a layout");
            CheckJourney(editorOpen && previewValid && editItems.Any(i => i.giftId == HomecomingGift) && CommittedLamp == null, "preview preserves earned gift before approval");
            yield return new WaitForSecondsRealtime(.7f);
            yield return JourneyScreenshot("05-preview");
            yield return FinalizePlacement();
            yield return UntilV1(() => Journey == JourneyStep.Light && lampAnchor != null);
            CheckJourney(CommittedLamp != null && !Profile.homecomingLampLit, "layout saved; lamp stays unlit until interaction");
            yield return WaitForHomecomingNight();
            yield return UntilV1(() => Profile.homecomingLampLit && homecomingGlow != null && homecomingGlow.enabled, 32);
            CheckJourney(Journey == JourneyStep.Complete && homecomingGlow.enabled, "walked to lamp and lit it at night");
            if (coast != null)
                CheckJourney(Vector3.Distance(lampAnchor.position, coast.GiftLampPoint) < .1f, "gift is lit at the coastal grotto");
            yield return new WaitForSecondsRealtime(.8f);
            yield return JourneyScreenshot("06-night-lamp");
            yield return Visit(0, (Profile.ownContinent + 1) % 4);
            yield return Visit(0, Profile.ownContinent);
            yield return UntilV1(() => !restoringOwnedLayout && lampAnchor != null);
            Game.atmosphere.Preview(20.4f);
            yield return UntilV1(() => homecomingGlow != null && homecomingGlow.enabled);
            CheckJourney(Journey == JourneyStep.Complete && SmallWorldProfile.Load().homecomingLampLit && homecomingGlow.enabled, "returning home restores task and actual night light");
            ToggleHomecomingJournal();
            yield return JourneyScreenshot("07-complete");
        }

        IEnumerator VerifyCoast()
        {
            var start = Game.actor.position;
            var camera = new GameObject("海岸取景").AddComponent<Camera>();
            camera.CopyFrom(Game.worldCamera);
            camera.enabled = false;
            camera.fieldOfView = 42f;
            camera.transform.position = new Vector3(14,48,57);
            camera.transform.LookAt(new Vector3(11,1,-9));
            try
            {
                File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"coast-overview.png"),CaptureHome(camera,1600,900));
                camera.transform.position = start + new Vector3(-.6f,2.6f,5.6f);
                camera.transform.LookAt(new Vector3(7,1.6f,-12));
                File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"coast-path.png"),CaptureHome(camera,1600,900));
            }
            finally { Destroy(camera.gameObject); }
            for (int i = 0; i < DawnCoast.ZonePoints.Length; i++)
            {
                var point = DawnCoast.ZonePoints[i];
                var route = new NavMeshPath();
                var found = NavMesh.SamplePosition(point,out var target,.60f,NavMesh.AllAreas);
                if (found) NavMesh.CalculatePath(start,target.position,NavMesh.AllAreas,route);
                CheckJourney(found && route.status == NavMeshPathStatus.PathComplete,
                    $"reachable district: {DawnCoast.ZoneNames[i]} ({route.status}, sampled {found}, last {route.corners.LastOrDefault()})");
            }
            GoToCoastDistrict(5);
            yield return UntilV1(() => WorldOpen, 55);
            CheckJourney(WorldOpen && stars && NearbyCoastStation() == 5, "walked up to the observatory and opened interplanetary map");
            WorldOpen = false;
            SetWorldVisible(false);
            UpdateControls();
            CheckJourney(Game.WalkTo(DawnCoast.Nursery), "walked down toward the irrigation nursery");
            yield return UntilV1(() => !Game.Navigating && !Game.Busy, 55);
            CheckJourney(Vector3.Distance(Game.actor.position,DawnCoast.Nursery) < .8f,
                $"physical character reached the nursery ({Game.actor.position}, {Game.Status})");
        }

        IEnumerator VerifyAjinMotions()
        {
            var actor = residentActor.GetComponent<SmallWorldAjinActor>();
            var skin = residentActor.GetComponentInChildren<SkinnedMeshRenderer>();
            CheckJourney(actor != null && actor.rig != null && skin != null && skin.bones.Length == 52,
                "Ajin uses his own mesh and 52-joint rig");
            var rig = actor.rig;
            var referenceHeight = skin.bounds.size.y;
            var camera = new GameObject("Ajin pose inspection").AddComponent<Camera>();
            camera.CopyFrom(Game.worldCamera);
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.13f;
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.24f, .27f, .28f);
            const int inspectionLayer = 31;
            camera.cullingMask = 1 << inspectionLayer;
            var transforms = residentActor.GetComponentsInChildren<Transform>(true);
            var originalLayers = transforms.Select(t => t.gameObject.layer).ToArray();
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var poses = Path.Combine(SmallWorldProfile.TestRoot, "ajin-poses");
            Directory.CreateDirectory(poses);
            rig.speed = 0;
            try
            {
                foreach (var child in transforms) child.gameObject.layer = inspectionLayer;
                foreach (string state in new[] { "Idle", "Walk", "Jog", "Greet", "Water" })
                {
                    for (int i = 0; i < 4; i++)
                    {
                        rig.Play(state, 0, .05f + i * .25f);
                        rig.Update(0);
                        yield return new WaitForEndOfFrame();
                        skin.BakeMesh(mesh);
                        mesh.GetVertices(vertices);
                        var height = skin.transform.TransformVector(Vector3.up * mesh.bounds.size.y).magnitude;
                        if (vertices.Count == 0 || vertices.Any(v => !float.IsFinite(v.sqrMagnitude)) ||
                            height < referenceHeight * .55f || height > referenceHeight * 1.6f)
                            throw new InvalidOperationException($"Invalid skinned pose in {state} at sample {i + 1}, height={height}");
                        Vector3 offset = i % 2 == 0 ? new Vector3(.95f, .2f, 3.7f) : new Vector3(-3.1f, .2f, 2.3f);
                        var focus = residentActor.transform.position + Vector3.up * .83f;
                        camera.transform.position = focus + residentActor.transform.rotation * offset;
                        camera.transform.LookAt(focus);
                        File.WriteAllBytes(Path.Combine(poses, $"{state}-{i + 1}.png"), CaptureHome(camera, 448, 448));
                    }
                    CheckJourney(rig.GetCurrentAnimatorStateInfo(0).IsName(state), $"Ajin {state}: four poses play with intact skin bounds");
                }
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = originalLayers[i];
                rig.Play("Idle", 0, 0);
                rig.speed = 1;
                Destroy(mesh);
                Destroy(camera.gameObject);
            }
        }

        IEnumerator WalkJourneyTarget(SmallWorldWorldTarget target)
        {
            ClearMoviePanels();
            QueueWorldTarget(target);
            yield return UntilV1(() => !Game.Navigating && !Game.Busy && !neighborBusy && pendingWorldTarget == null, 30);
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
