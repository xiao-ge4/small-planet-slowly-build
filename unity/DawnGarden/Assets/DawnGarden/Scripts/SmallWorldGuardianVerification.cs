using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        IEnumerator VerifyGuardianDemo() => RunHomecomingChecks(WalkGuardianDemo(), Environment.GetCommandLineArgs().Contains("-smallWorldGuardianRehearsal") ? "guardian-rehearsal-report.json" : "guardian-report.json");

        IEnumerator WalkGuardianDemo()
        {
            var dir = SmallWorldProfile.TestRoot;
            bool rehearsal = Environment.GetCommandLineArgs().Contains("-smallWorldGuardianRehearsal");
            bool legacy = File.Exists(Path.Combine(dir, "expected-layout.json"));
            foreach(var check in GuardianBattleVerification.RunAll()) CheckJourney(true, check);
            yield return EnterFromTitle("dawn");
            yield return UntilV1(() => Connected && Game != null && coast != null && !restoringOwnedLayout && !loading, 60);
            CheckJourney(Connected && Game != null && coast != null, "guardian demo uses isolated connected coast scene");
            Game.atmosphere.Preview(9.5f);
            CheckJourney(Game.Progress.firstExpansionUnlocked == legacy, "prior ownership preserved; fresh land remains guarded");
            CheckJourney(Game.Progress.guardianLandVersion == 1, "save upgraded to guardian protocol");
            if (legacy) CheckJourney(File.ReadAllText(LayoutFile) == File.ReadAllText(Path.Combine(dir, "expected-layout.json")), "legacy committed layout is byte-identical");
            else
            {
                CheckJourney(!OpenPlacement(false), "cannot build on guarded land");
                CheckJourney(!Game.expansion.StartGuardianChallenge(), "no permit cannot begin a challenge");
                var chest = Game.FindInteraction(GardenInteractionKind.Chest);
                CheckJourney(Game.GoToInteraction(chest, true), "walk to starter chest");
                yield return UntilV1(() => Game.Progress.chestOpened && !Game.Busy, 65);
                CheckJourney(Game.Progress.chestOpened && Game.Progress.expansionPermits == 1 && !Game.Progress.firstExpansionUnlocked, "opening chest grants permit but no ownership");
                CheckJourney(Game.expansion.Defer(), "permit can be retained in pouch");
                yield return SyncState();
                string denied = null;
                yield return SmallWorldApi.Request("create", new CreateRequest {clientId = Profile.clientId, idea = "提前在守卫地建花园", maxGeneratedAssets = 0}, _ => {}, e => denied = e);
                CheckJourney(!string.IsNullOrEmpty(denied), "backend rejects creation before occupation");
                CheckJourney(Game.WalkTo(GuardianApproach()), "guardian boundary reachable through real navmesh");
                yield return UntilV1(() => !Game.Navigating, 65);
                CheckJourney(NearGuardian, "physical controller reaches guardian approach");
                yield return JourneyScreenshot("01-guarded-land");
            }

            var actorBefore = Game.actor.position;
            var originalRenderers = Game.actor.GetComponentsInChildren<Renderer>(true).Concat(Game.expansion.land.GetComponentsInChildren<Renderer>(true)).ToArray();
            var renderEnabled = originalRenderers.Select(r => r.enabled).ToArray();
            int permitsBefore = Game.Progress.expansionPermits;
            CheckJourney(Game.expansion.StartGuardianChallenge(legacy), "challenge starts through production permit entry");
            yield return new WaitForSecondsRealtime(.8f);
            CheckJourney(GuardianBusy && guardianBattle.IsActive && Game.ControlsBlocked, "battle owns movement and modal input");
            CheckJourney(guardianBattle.PlayerPresentationHeight > 1.5f && guardianBattle.PlayerPresentationHeight < 2.1f,
                "player presentation retains readable human-scale height after guardian fitting");
            CheckJourney(rehearsal || guardianBattle.UsesGuardianPrefab, rehearsal ? "explicit rehearsal uses temporary actor; excludes formal model approval" : "approved guardian rig is loaded instead of geometric stand-in");
            OpenWorld(); OpenChat();
            CheckJourney(!WorldOpen && !ChatOpen && !OpenPlacement(false), "battle blocks map chat and editor entry");
            CheckJourney(!Game.expansion.StartGuardianChallenge(legacy), "duplicate encounter rejected");
            CheckJourney(!guardianBattle.TryAct(GuardianBattleAction.Resonance), "insufficient energy rejects special skill");
            yield return JourneyScreenshot("02-battle-start");
            CheckJourney(guardianBattle.TryAct(GuardianBattleAction.Retreat), "retreat accepted");
            yield return UntilV1(() => guardianBattle.ResultReady, 5);
            guardianBattle.Close(); yield return null;
            CheckJourney(!GuardianBusy && Game.Progress.expansionPermits == permitsBefore && Game.Progress.firstExpansionUnlocked == legacy, "retreat leaves inventory and ownership unchanged");
            CheckJourney(Vector3.Distance(actorBefore, Game.actor.position) < .05f && Game.orbit.enabled, "battle restores camera and original actor position");
            CheckJourney(originalRenderers.Select((r, i) => r != null && r.enabled == renderEnabled[i]).All(v => v), "battle restores source actor and land renderer states");
            yield return SyncState();

            CheckJourney(Game.expansion.StartGuardianChallenge(legacy), "retry creates a fresh encounter");
            yield return new WaitForSecondsRealtime(.8f);
            int safety = 0;
            while (guardianBattle.State.Result == GuardianBattleResult.None && safety++ < 20)
            {
                if (guardianBattle.State.CanAct)
                {
                    int hpBeforeContact = guardianBattle.State.GuardianHealth;
                    CheckJourney(guardianBattle.TryAct(GuardianBattleAction.Attack), "attack-only rehearsal accepted");
                    CheckJourney(guardianBattle.State.GuardianHealth == hpBeforeContact && !guardianBattle.ImpactVisible,
                        "attack windup does not announce or apply a hit before contact");
                    CheckJourney(!guardianBattle.TryAct(GuardianBattleAction.Attack), "double click does not apply damage twice");
                    if (safety == 1)
                    {
                        yield return UntilV1(() => guardianBattle.State.Phase == GuardianBattlePhase.GuardianAction, 3);
                        yield return new WaitForSecondsRealtime(.45f);
                        File.WriteAllBytes(Path.Combine(dir, "guardian-contact.png"), CaptureHome(Game.worldCamera, 1280, 720));
                    }
                }
                yield return UntilV1(() => guardianBattle.State.CanAct || guardianBattle.State.Result != GuardianBattleResult.None, 6);
                yield return null;
            }
            CheckJourney(guardianBattle.State.Result == GuardianBattleResult.Defeat, "ignoring telegraphed heavy strikes can lose");
            yield return UntilV1(() => guardianBattle.ResultReady, 5);
            yield return JourneyScreenshot("03-defeat");
            string retryId = Game.Progress.guardianEncounterId;
            CheckJourney(guardianBattle.Retry(), "in-battle retry accepted from defeat screen");
            yield return new WaitForSecondsRealtime(.8f);
            CheckJourney(guardianBattle.State.PlayerHealth == 36 && guardianBattle.State.GuardianHealth == 72 && guardianBattle.State.Energy == 0 && Game.Progress.guardianEncounterId == retryId, "retry resets combat without minting another encounter");
            CheckJourney(guardianBattle.SubmitAction(GuardianBattleAction.Retreat), "retry can still safely retreat");
            yield return UntilV1(() => guardianBattle.ResultReady, 5);
            guardianBattle.DismissResult();
            CheckJourney(Game.Progress.expansionPermits == permitsBefore && Game.Progress.firstExpansionUnlocked == legacy, "defeat costs no permit and changes no owned land");
            yield return SyncState();

            SmallWorldMovieRecorder battleMovie = null;
            var encoderPath = Path.Combine(dir, "guardian-ffmpeg.txt");
            if (File.Exists(encoderPath))
            {
                battleMovie = gameObject.AddComponent<SmallWorldMovieRecorder>();
                battleMovie.Begin(Path.Combine(dir, "battle-film"), File.ReadAllText(encoderPath).Trim());
                var listener = FindFirstObjectByType<AudioListener>();
                if (listener != null) battleMovie.AttachAudio(listener.gameObject);
            }
            CheckJourney(Game.expansion.StartGuardianChallenge(legacy), "new attempt after defeat");
            string winningId = Game.Progress.guardianEncounterId;
            yield return new WaitForSecondsRealtime(.8f);
            safety = 0; bool guarded = false, interrupted = false, weakHit = false;
            while (guardianBattle.State.Result == GuardianBattleResult.None && safety++ < 22)
            {
                var state = guardianBattle.State;
                GuardianBattleAction action;
                if (state.Intent == GuardianBattleIntent.HeavyStrike && (!guarded || state.Energy < 2)) action = GuardianBattleAction.Guard;
                else if (state.CanUseResonance && (state.Intent == GuardianBattleIntent.Exposed || state.CanInterrupt)) action = GuardianBattleAction.Resonance;
                else action = GuardianBattleAction.Attack;
                if (battleMovie != null) yield return new WaitForSecondsRealtime(.8f);
                if (state.Intent != GuardianBattleIntent.Probe)
                    yield return JourneyScreenshot("intent-" + state.Intent.ToString().ToLowerInvariant());
                CheckJourney(guardianBattle.TryAct(action), "tactical action accepted: " + action);
                guarded |= action == GuardianBattleAction.Guard;
                interrupted |= guardianBattle.State.Interrupted;
                weakHit |= guardianBattle.State.HitWeakPoint;
                yield return new WaitForSecondsRealtime(.46f);
                if (action == GuardianBattleAction.Guard)
                {
                    yield return UntilV1(() => guardianBattle.State.Phase == GuardianBattlePhase.GuardianAction, 3);
                    yield return new WaitForSecondsRealtime(.46f);
                    yield return JourneyScreenshot("04-guard-heavy-strike");
                }
                else if (guardianBattle.State.Interrupted) yield return JourneyScreenshot("05-interrupt");
                else if (guardianBattle.State.HitWeakPoint) yield return JourneyScreenshot("06-weak-point");
                yield return UntilV1(() => guardianBattle.State.CanAct || guardianBattle.State.Result != GuardianBattleResult.None, 6);
                yield return null;
            }
            CheckJourney(guardianBattle.State.Result == GuardianBattleResult.Victory && guarded && interrupted && weakHit, "telegraphs guard interrupt and weakness produce a real victory");
            CheckJourney(!guardianBattle.ResultReady, "victory plays its ending before exposing the continue action");
            guardianBattle.Close();
            CheckJourney(guardianBattle.IsActive, "early result input cannot skip the outcome performance");
            yield return JourneyScreenshot("victory-performance");
            yield return UntilV1(() => guardianBattle.ResultReady, 5);
            yield return JourneyScreenshot("07-victory");
            File.WriteAllBytes(Path.Combine(dir, "guardian-defeat-ground.png"), CaptureHome(Game.worldCamera, 1280, 720));
            if (!rehearsal) CheckJourney(float.IsFinite(guardianBattle.GuardianDefeatMinY) &&
                Mathf.Abs(guardianBattle.GuardianDefeatMinY - guardianBattle.GuardianGroundY) < .015f,
                "defeated guardian baked skin rests on the battle ground");
            CheckJourney(Game.Progress.firstExpansionUnlocked && !Game.expansion.IsRising && GardenSave.Load(Game.SavePath, Game.homeId).firstExpansionUnlocked, "victory already saved on result screen before reveal or close");
            guardianBattle.Close(); yield return null;
            CheckJourney(!guardianSavePending && Game.Progress.firstExpansionUnlocked, "victory persists ownership");
            CheckJourney(Game.Progress.expansionPermits == permitsBefore - (legacy ? 0 : 1), "only first occupation consumes exactly one permit");
            CheckJourney(Game.expansion.CompleteGuardianChallenge(winningId, true), "repeated victory receipt is idempotent");
            int after = Game.Progress.expansionPermits;
            yield return UntilV1(() => !Game.expansion.IsRising, 12);
            CheckJourney(Game.Progress.expansionPermits == after && Game.expansion.landCollider.enabled, "land reveal finished with no duplicate debit");
            yield return SyncState();
            CheckJourney(Game.WalkTo(Game.expansion.WalkTarget), "newly owned land is walkable");
            yield return UntilV1(() => !Game.Navigating, 35);
            CheckJourney(Vector3.Distance(Game.actor.position, Game.expansion.WalkTarget) < .85f, "physical player steps onto owned land");
            yield return JourneyScreenshot("08-occupied-land");

            guardianCoCreateOffer = false;
            OpenCreator();
            CheckJourney(ChatOpen, "occupation connects to resident companion co-creation");
            ChatOpen = false; UpdateControls();
            CheckJourney(OpenPlacement(false), "owned land opens existing reusable asset editor");
            yield return new WaitForEndOfFrame();
            var plotOnScreen = Game.worldCamera.WorldToViewportPoint(Game.expansion.finalPosition);
            CheckJourney(plotOnScreen.z > 0 && plotOnScreen.x > .16f && plotOnScreen.x < .58f &&
                plotOnScreen.y > .2f && plotOnScreen.y < .8f && Game.worldCamera.fieldOfView >= 40,
                "creation camera frames the owned plot in the unobstructed editing area");
            if (!legacy)
            {
                var asset = (availableAssets ?? library.assets).First(a => a.id == "meadow-flower");
                editItems.Add(new SmallWorldItem {instanceId = "guardian-first-flowers", assetId = asset.id, assetHash = asset.hash, assetVersion = asset.version, localPosition = new Vector3(1.7f, 0, -1.8f), localScale = Vector3.one});
                yield return PreviewPlacement(false);
                CheckJourney(previewValid, "existing asset preview on conquered plot passes collision and road checks");
                yield return JourneyScreenshot("09-co-creation-preview");
                yield return FinalizePlacement();
                CheckJourney(File.Exists(LayoutFile) && committedLayout.instances.Any(i => i.instanceId == "guardian-first-flowers"), "isolated test layout saved through normal creation service");
            }
            else yield return ClosePlacement();
            CheckJourney(!Game.orbit.placementView, "leaving placement restores normal world camera projection");
            Game.SaveNow();
            var saved = GardenSave.Load(Game.SavePath, Game.homeId);
            CheckJourney(saved.firstExpansionUnlocked && GardenGuardianLand.CanCreate(saved) && saved.expansionPermits == after, "reload preserves ownership and creation eligibility");
            if (legacy) CheckJourney(File.ReadAllText(LayoutFile) == File.ReadAllText(Path.Combine(dir, "expected-layout.json")), "legacy replay and editor preview never rewrite committed layout");
            if (battleMovie != null)
            {
                yield return new WaitForSecondsRealtime(1.3f);
                yield return battleMovie.Finish();
                CheckJourney(battleMovie.Frames > 24 * 10, "actual battle movie contains the combat and occupation sequence");
            }
            if (!rehearsal) yield return VerifyGuardianRigPoses();
        }

        IEnumerator VerifyGuardianRigPoses()
        {
            var prefab = Resources.Load<GameObject>("Guardian/MossstoneGuardian");
            CheckJourney(prefab != null, "guardian asset present in built player");
            var model = Instantiate(prefab);
            model.transform.position = new Vector3(0, 600, 0);
            var rig = model.GetComponentInChildren<Animator>();
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            CheckJourney(rig != null && skins.Length > 0 && skins.All(s => s.bones.Length > 20 && s.bones.All(b => b != null)), "guardian skin references intact skeleton");
            var camera = new GameObject("Guardian pose inspector").AddComponent<Camera>();
            camera.CopyFrom(Game.worldCamera); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 1.65f; camera.aspect = 1; camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.34f, .39f, .37f);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            var output = Path.Combine(SmallWorldProfile.TestRoot, "guardian-poses"); Directory.CreateDirectory(output);
            var mesh = new Mesh(); rig.speed = 0;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Guardian QA ground contact";
            floor.layer = 30; floor.transform.position = model.transform.position; floor.transform.localScale = Vector3.one * .6f;
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); floorMaterial.color = new Color(.53f, .57f, .49f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            floor.GetComponent<Collider>().enabled = false;
            try
            {
                foreach (var state in new[] {"Idle", "Walk", "Attack", "Guard", "Skill", "Hit", "Defeat"})
                {
                    CheckJourney(rig.HasState(0, Animator.StringToHash(state)), "guardian animation available: " + state);
                    for (int i = 0; i < 4; i++)
                    {
                        rig.Play(state, 0, .05f + .23f * i); rig.Update(0);
                        yield return new WaitForEndOfFrame();
                        Bounds bounds = default;
                        bool measured = false;
                        foreach (var skin in skins)
                        {
                            skin.BakeMesh(mesh, false);
                            var vertices = mesh.vertices;
                            CheckJourney(mesh.vertexCount > 0 && vertices.All(v => float.IsFinite(v.sqrMagnitude)), "finite skinned vertices: " + state + " " + i);
                            foreach (var vertex in vertices)
                            {
                                var point = skin.localToWorldMatrix.MultiplyPoint3x4(vertex);
                                if (!measured) { bounds = new Bounds(point, Vector3.zero); measured = true; }
                                else bounds.Encapsulate(point);
                            }
                        }
                        CheckJourney(bounds.size.magnitude < 12 && bounds.size.magnitude > .1f, "bounded animated mesh: " + state + " " + i);
                        if (state == "Idle" && i == 0)
                            CheckJourney(Mathf.Abs(bounds.min.y - floor.transform.position.y) < .015f,
                                "imported guardian actual Idle feet align with prefab ground");
                        camera.transform.position = model.transform.position + (i % 2 == 0 ? new Vector3(2.8f, 1.6f, 4.8f) : new Vector3(-4.5f, 1.5f, 2.3f));
                        camera.transform.LookAt(model.transform.position + Vector3.up * 1.05f);
                        File.WriteAllBytes(Path.Combine(output, state + "-" + (i + 1) + ".png"), CaptureHome(camera, 448, 448));
                    }
                }
            }
            finally { Destroy(camera.gameObject); Destroy(model); Destroy(mesh); Destroy(floor); Destroy(floorMaterial); }
        }
    }
}
