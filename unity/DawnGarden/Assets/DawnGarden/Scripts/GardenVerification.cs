using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed class GardenVerification : MonoBehaviour
    {
        DawnGardenGame game;
        string output;
        readonly List<Check> checks = new List<Check>();
        int errors;
        VerificationContext context;
        public void Begin(DawnGardenGame value, string directory, string bindingJson = null)
        {
            game = value; output = directory; game.inputEnabled = false;
            if (!string.IsNullOrEmpty(bindingJson)) context = JsonUtility.FromJson<VerificationContext>(bindingJson);
            Directory.CreateDirectory(output);
            var path = Path.Combine(output, "garden-state-test.json");
            if (File.Exists(path)) File.Delete(path);
            game.Initialize(path);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void Record(string name, bool passed, string detail = "") { checks.Add(new Check { name = name, passed = passed, detail = detail }); }
        IEnumerator Run()
        {
            for (var i = 0; i < 8; i++) yield return new WaitForEndOfFrame();
            yield return InspectAtmosphere();
            yield return InspectPolish();
            Record("baked_navigation", NavMesh.CalculateTriangulation().vertices.Length > 0);
            Record("spawn_grounded", game.body.isGrounded && game.actor.position.y > -.1f);
            yield return Capture("garden-overview.png");
            var water = game.FindInteraction(GardenInteractionKind.Water);
            var chest = game.FindInteraction(GardenInteractionKind.Chest);
            var start = game.actor.position;
            Record("reject_far_interaction", !game.TryInteract(water) && !game.Busy && !game.Navigating && !game.Progress.watered);
            Record("no_far_teleport", Vector3.Distance(start, game.actor.position) < .01f);

            // Exercise the actual CharacterController independently of the NavMesh guard.
            game.PlaceActor(new Vector3(-2.6f, .05f, .65f), 270);
            game.navigator.enabled = false;
            for (var i = 0; i < 60; i++) { game.body.Move(new Vector3(-.04f, -.01f, 0)); yield return null; }
            Record("physical_rock_collision", game.actor.position.x > -3.10f && game.actor.position.x < -2.8f, game.actor.position.ToString("F3"));

            game.PlaceActor(new Vector3(.8f, .05f, 1.65f), 90);
            for (var i = 0; i < 75; i++) { game.MoveStep(Vector3.right, 1f / 30); yield return null; }
            Record("pond_not_walkable", game.actor.position.x < 1.6f, game.actor.position.ToString("F3"));

            game.PlaceActor(new Vector3(0, .05f, 3.6f), 0);
            for (var i = 0; i < 100; i++) { game.MoveStep(Vector3.forward, 1f / 30); yield return null; }
            Record("shore_boundary", game.actor.position.z < 5.3f && game.actor.position.y > -.1f
                && NavMesh.SamplePosition(game.actor.position, out _, .3f, NavMesh.AllAreas), game.actor.position.ToString("F3"));

            game.PlaceActor(game.spawn.position, 180);
            var route = game.GoToInteraction(water);
            var pathCorners = game.LastPathCornerCount;
            yield return Await(() => !game.Navigating, 18);
            Record("walk_to_seedling", route && !game.Navigating && game.InRange(water), "corners=" + pathCorners + "; " + game.actor.position.ToString("F3"));
            Record("arrival_does_not_auto_water", !game.Progress.watered && !game.Busy);
            var before = game.actor.position;
            var began = game.TryInteract(water);
            Record("interaction_no_teleport", Vector3.Distance(before, game.actor.position) < .01f);
            yield return Await(() => game.Busy && game.ActionTime > 1.6f, 6);
            yield return Capture("garden-watering.png");
            yield return Await(() => game.Progress.watered && !game.Busy, 8);
            Record("watering_complete", began && game.Progress.watered && !game.Busy && !game.gripSocket.gameObject.activeSelf);
            Record("water_only_no_reward", game.Progress.expansionPermits == 0 && !game.Progress.starterRewardGranted);
            yield return Await(() => water.visual.localScale.x > 1.49f, 3);
            Record("seedling_growth", water.visual.localScale.x > 1.49f, water.visual.localScale.ToString("F2"));

            route = game.GoToInteraction(chest, true);
            yield return Await(() => game.Busy && game.Motion == "Chest" && game.ActionTime > .8f, 18);
            yield return Capture("garden-chest.png");
            yield return Await(() => game.Progress.chestOpened && !game.Busy, 6);
            Record("open_chest_complete", route && game.Progress.chestOpened && !game.Busy);
            Record("lid_stays_open", Mathf.Abs(Mathf.DeltaAngle(0, chest.lid.localEulerAngles.x)) > 70);
            Record("starter_reward_once", game.Progress.starterRewardGranted && game.Progress.expansionPermits == 1);
            var denied = !game.TryInteract(chest) && !game.GoToInteraction(chest, true) && !game.TryInteract(water);
            GardenSave.GrantStarterReward(game.Progress);
            Record("duplicate_reward_prevented", denied && game.Progress.expansionPermits == 1);
            game.SaveNow();
            var savedPosition = game.actor.position;
            game.ReloadSaved();
            yield return null;
            Record("save_reload_progress", game.Progress.watered && game.Progress.chestOpened && game.Progress.starterRewardGranted && game.Progress.expansionPermits == 1);
            Record("save_reload_position", Vector3.Distance(game.actor.position, savedPosition) < .35f);
            Record("save_reload_visuals", water.visual.localScale.x > 1.49f && Mathf.Abs(Mathf.DeltaAngle(0, chest.lid.localEulerAngles.x)) > 70);
            game.orbit.overview = true; game.orbit.distance = 15.5f;
            yield return Capture("garden-complete.png");
            yield return InspectExpansion();
            if (context != null)
            {
                Record("scene_source_unchanged", FileHash(context.scenePath) == context.sceneSha256
                    && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == context.scenePath);
                var layoutPath = Path.Combine(output, "layout.json");
                Record("layout_source_unchanged", File.Exists(layoutPath) && File.ReadAllText(layoutPath) == context.layoutJson);
                Record("dependencies_unchanged", context.dependencyFiles != null && context.dependencyFiles.Length > 0
                    && context.dependencyFiles.All(d => FileHash(d.path) == d.sha256));
            }
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                errors = errors, checks = checks.ToArray(), passed = errors == 0 && checks.All(c => c.passed),
                scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                isolatedSave = game.SavePath, navMeshVertices = NavMesh.CalculateTriangulation().vertices.Length,
                bindingVersion = context == null ? 0 : 1, runId = context?.runId, revision = context?.revision,
                assemblyVersion = context?.assemblyVersion, sceneSha256 = context?.sceneSha256,
                layoutJson = context?.layoutJson, dependencyFiles = context?.dependencyFiles };
            var reportPath = Path.Combine(output, "garden-runtime.json");
            File.WriteAllText(reportPath + ".tmp", JsonUtility.ToJson(report, true));
            File.Move(reportPath + ".tmp", reportPath);
        }
        IEnumerator InspectExpansion()
        {
            var expansion=game.expansion;
            Record("expansion_installed", expansion != null && expansion.originalNavigation != null && expansion.expandedNavigation != null);
            if (expansion == null) yield break;
            var path=Path.Combine(output,"garden-expansion-test.json");
            if (File.Exists(path)) File.Delete(path);
            game.Initialize(path); game.atmosphere.Preview(7.5f);
            yield return new WaitForSeconds(.3f);
            Record("new_land_locked_initially", !expansion.land.gameObject.activeSelf && !NavMesh.SamplePosition(expansion.WalkTarget,out _,.3f,NavMesh.AllAreas));
            var chest=game.FindInteraction(GardenInteractionKind.Chest);
            // Start at the authored chest approach point here. Navigation from the
            // spawn is covered above; this section verifies the permit/offer flow
            // without making the reward test depend on an incidental rock edge.
            game.PlaceActor(chest.approach.position, 180);
            var route=game.TryInteract(chest);
            yield return Await(()=>game.Progress.chestOpened && !game.Busy,20);
            Record("chest_alone_grants_permit",route && !game.Progress.watered && game.Progress.expansionPermits==1 && game.Progress.starterRewardGranted);
            Record("chest_opens_expansion_offer",expansion.PromptOpen && !expansion.IsRising);
            Record("offer_blocks_world_input",!game.WalkTo(game.spawn.position) && !game.GoToInteraction(game.FindInteraction(GardenInteractionKind.Water)));
            yield return Capture("garden-expansion-offer.png");
            var deferred=expansion.Defer(); game.ReloadSaved(); yield return null;
            Record("defer_saved_in_bag",deferred && !expansion.PromptOpen && !expansion.BagOpen && game.Progress.expansionPermits==1 && !game.Progress.expansionOfferPending);
            expansion.OpenBag();
            Record("bag_reopens_permit",expansion.BagOpen && game.Progress.expansionPermits==1);
            yield return Capture("garden-expansion-bag.png");
            expansion.Defer(); expansion.ShowOffer();
            Record("offer_again_from_saved_permit",expansion.PromptOpen);
            var accepted=expansion.Accept();
            Record("permit_consumed_once",accepted && game.Progress.expansionPermits==0 && game.Progress.firstExpansionUnlocked && !expansion.Accept());
            var committed=GardenSave.Load(game.SavePath);
            Record("expansion_committed_before_animation",committed.firstExpansionUnlocked && committed.expansionPermits==0);
            Record("rising_blocks_navigation",expansion.IsRising && !game.WalkTo(expansion.WalkTarget) && !expansion.landCollider.enabled && !NavMesh.SamplePosition(expansion.WalkTarget,out _,.3f,NavMesh.AllAreas));
            yield return new WaitForSeconds(.85f);
            yield return Capture("garden-expansion-awakening.png");
            yield return new WaitForSeconds(2.35f);
            Record("land_rises_from_sea", expansion.land.position.y > -3.3f && expansion.land.position.y < -.1f);
            yield return Capture("garden-expansion-rising.png");
            yield return Await(()=>!expansion.IsRising,8);
            Record("expansion_finishes_with_collision", !expansion.IsRising && expansion.landCollider.enabled && Vector3.Distance(expansion.land.position,expansion.finalPosition)<.001f);
            Record("old_shore_seam_removed",expansion.seamStones.Length>0 && expansion.seamStones.All(o=>!o.activeSelf));
            var navPath=new NavMeshPath();
            Record("new_land_navigation_connected",NavMesh.CalculatePath(game.actor.position,expansion.WalkTarget,NavMesh.AllAreas,navPath) && navPath.status==NavMeshPathStatus.PathComplete);
            yield return InspectPreparedLayout();
            yield return new WaitForSeconds(.8f);
            yield return Capture("garden-expansion-complete.png");
            // Validate the actual traversal from the authored home entrance after
            // the reveal, where the west connection is unobstructed.
            game.PlaceActor(game.spawn.position, 180);
            var walking=game.WalkTo(expansion.WalkTarget);
            yield return Await(()=>!game.Navigating,18);
            Record("player_walks_onto_new_land",walking && Vector3.Distance(game.actor.position,expansion.WalkTarget)<.3f && game.body.isGrounded,game.actor.position.ToString("F3"));
            game.SaveNow(); var position=game.actor.position; game.ReloadSaved(); yield return new WaitForSeconds(.2f);
            Record("expanded_save_restores_land_and_player",game.Progress.firstExpansionUnlocked && game.Progress.expansionPermits==0 && expansion.land.gameObject.activeSelf && !expansion.PromptOpen && Vector3.Distance(game.actor.position,position)<.35f);
            Record("expanded_save_cannot_repeat",!expansion.Accept() && !game.GoToInteraction(chest,true) && game.Progress.expansionPermits==0);
            yield return Capture("garden-expansion-walkable.png");
            // A crash during the reveal restores the committed final state, never consumes twice.
            var interrupted=Path.Combine(output,"garden-expansion-interrupted.json");
            GardenSave.Write(interrupted,new GardenProgress{chestOpened=true,starterRewardGranted=true,expansionPermits=1});
            game.Initialize(interrupted); expansion.ShowOffer(); expansion.Accept();
            yield return new WaitForSeconds(1.4f); game.ReloadSaved(); yield return null;
            Record("mid_animation_reload_recovers",game.Progress.firstExpansionUnlocked && !expansion.IsRising && expansion.landCollider.enabled && game.Progress.expansionPermits==0 && Vector3.Distance(expansion.land.position,expansion.finalPosition)<.001f);
            // Fail the atomic save intentionally by using an existing directory as the target file.
            var blocked=Path.Combine(output,"blocked-expansion-save"); Directory.CreateDirectory(blocked);
            game.Initialize(blocked); game.Progress.chestOpened=true; GardenSave.GrantStarterReward(game.Progress); expansion.ShowOffer();
            var rejected=!expansion.Accept();
            Record("save_failure_keeps_permit_and_land_locked",rejected && game.Progress.expansionPermits==1 && !game.Progress.firstExpansionUnlocked && !expansion.IsRising && !expansion.land.gameObject.activeSelf);
            game.Initialize(path);
            game.orbit.overview=true; game.orbit.distance=22;
            yield return new WaitForSeconds(.5f);
        }
        IEnumerator InspectAtmosphere()
        {
            var a = game.atmosphere;
            Record("atmosphere_installed", a != null);
            if (a == null) yield break;
            var real = a.followRealTime; var hour = a.previewHour; var volume = a.MusicVolume; var muted = a.Muted;
            var oldHud = game.showHud; game.showHud = false;
            Record("clock_utc8_midnight_rollover", Mathf.Abs(GardenAtmosphere.HourAt(new DateTimeOffset(2026,10,29,18,30,0,TimeSpan.Zero),8)-2.5f)<.001f);
            a.Preview(7.5f); yield return new WaitForSeconds(.3f);
            var morningDirection = a.sun.transform.rotation;
            yield return Capture("garden-atmosphere-morning.png");
            a.Preview(12); yield return new WaitForSeconds(.15f);
            Record("solar_direction_moves", Quaternion.Angle(morningDirection,a.sun.transform.rotation)>30);
            Record("day_sun_and_water", a.sun.enabled && a.sun.intensity>1 && a.Daylight>.99f);
            yield return Capture("garden-atmosphere-noon.png");
            a.Preview(17.7f); yield return new WaitForSeconds(.15f);
            yield return Capture("garden-atmosphere-sunset.png");
            a.Preview(22); yield return new WaitForSeconds(.15f);
            Record("night_moon_and_water", !a.sun.enabled && a.moon.enabled && a.moon.intensity>.3f && a.Daylight<.01f);
            var block = new MaterialPropertyBlock(); a.water[0].GetPropertyBlock(block);
            Record("night_water_follows_clock", block.GetFloat("_Daylight")<.01f);
            yield return Capture("garden-atmosphere-night.png");
            Record("music_looping", a.morningMusic.isPlaying && a.nightMusic.isPlaying && a.morningMusic.loop && a.nightMusic.loop
                && a.morningMusic.clip.length>90 && Mathf.Abs(a.morningMusic.clip.length-a.nightMusic.clip.length)<.01f);
            a.SetMuted(true,false); yield return null; yield return null;
            Record("music_mute", a.morningMusic.volume==0 && a.nightMusic.volume==0);
            a.SetMuted(false,false); a.SetVolume(.35f,false); yield return new WaitForSeconds(.15f);
            Record("music_night_crossfade", a.morningMusic.volume<.001f && a.nightMusic.volume>0);
            // Seek both long clips to just before the seam and actually let their looping playback wrap.
            a.morningMusic.timeSamples=a.morningMusic.clip.samples-2205;
            a.nightMusic.timeSamples=a.nightMusic.clip.samples-2205;
            yield return new WaitForSeconds(.35f);
            Record("music_playback_wraps", a.morningMusic.isPlaying && a.morningMusic.time<1 && a.nightMusic.time<1);
            a.Preview(31.5f); Record("preview_24h_wrap", Mathf.Abs(a.CurrentHour-7.5f)<.001f);
            a.UseRealTime();
            Record("clock_return_to_real", a.followRealTime && Mathf.Abs(a.CurrentHour-GardenAtmosphere.HourAt(DateTimeOffset.UtcNow,a.utcOffsetHours))<.002f);
            a.SetVolume(volume,false); a.SetMuted(muted,false);
            a.previewHour=hour; a.followRealTime=real; a.RefreshNow(); game.showHud=oldHud;
        }
        IEnumerator InspectPolish()
        {
            var mesh = GameObject.Find("Walkable Island").GetComponent<MeshFilter>().sharedMesh;
            Record("ground_has_uv", mesh.uv.Length == mesh.vertexCount && mesh.uv.Distinct().Count() > 50);
            Record("valid_materials", FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials)
                .All(m => m != null && m.shader != null && m.shader.isSupported));
            var monumentBounds = GameObject.Find("Formal Travel Monument").GetComponentsInChildren<Renderer>().Select(r => r.bounds.min.y).Min();
            Record("monument_grounded", monumentBounds >= -.03f && monumentBounds < .08f, "base y=" + monumentBounds);
            var orbit = game.orbit;
            var yaw = orbit.yaw; var pitch = orbit.pitch; var distance = orbit.distance;
            game.showHud = false;
            var angles = new[] { 180f, 270f, 0f, 90f };
            for (var i = 0; i < angles.Length; i++)
            {
                orbit.yaw = angles[i]; orbit.pitch = 39; orbit.distance = 15.8f;
                for (var f = 0; f < 8; f++) yield return new WaitForEndOfFrame();
                yield return Capture("garden-polished-" + i + ".png");
            }
            orbit.yaw = 180; orbit.overview = false; orbit.distance = 8.4f; orbit.pitch = 28;
            yield return new WaitForSeconds(.7f);
            yield return Capture("garden-polished-close.png");
            // Exercise obstruction handling with a target behind a known tree, without moving the player.
            var cutaway = game.worldCamera.GetComponent<GardenTreeCutaway>();
            if (cutaway != null)
            {
                var originalTarget = cutaway.target;
                var cameraPosition = cutaway.transform.position;
                var tree = GameObject.Find("Formal Tree 1");
                var probe = new GameObject("Temporary cutaway probe");
                probe.transform.position = tree.transform.position + new Vector3(0, .3f, 2);
                cutaway.target = probe.transform;
                cutaway.transform.position = tree.transform.position + new Vector3(0,1.2f,-4);
                cutaway.SendMessage("LateUpdate");
                Record("tree_occlusion_cutaway", cutaway.HiddenCount > 0, "hidden renderers=" + cutaway.HiddenCount);
                cutaway.enabled = false;
                Record("tree_cutaway_restores_renderers", cutaway.foliage.All(r => r == null || !r.forceRenderingOff));
                cutaway.target = originalTarget; cutaway.transform.position = cameraPosition; cutaway.enabled = true;
                Destroy(probe);
            }
            else Record("tree_occlusion_cutaway", false, "Missing component");
            orbit.yaw = yaw; orbit.pitch = pitch; orbit.distance = distance; orbit.overview = true;
            game.showHud = true;
            yield return new WaitForSeconds(.5f);
        }
        IEnumerator InspectPreparedLayout()
        {
            var markers=FindObjectsByType<HomeLayoutInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var bench = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Mossy Stone Bench (layout)")
                ?? markers.Where(t=>t.GetComponentInChildren<Collider>()!=null).Select(t=>t.transform).FirstOrDefault()
                ?? markers.Select(t=>t.transform).FirstOrDefault();
            Record("prepared_layout_present", bench != null);
            if (bench == null) yield break;
            var expansion = game.expansion;
            Record("prepared_layout_on_new_continent", bench.IsChildOf(expansion.land) && bench.gameObject.activeInHierarchy && markers.All(t=>t.transform.IsChildOf(expansion.land) && t.gameObject.activeInHierarchy),
                bench.position.ToString("F3"));
            var ground = expansion.land.GetComponent<Collider>();
            Record("prepared_starter_sized_plot", ground.bounds.size.x >= 12 && ground.bounds.size.z >= 10, ground.bounds.size.ToString("F3"));
            var collider = bench.GetComponentInChildren<Collider>();
            var westTarget = expansion.finalPosition + new Vector3(-4.6f, .03f, 0);
            var westPath = new NavMeshPath();
            Record("prepared_west_route", NavMesh.CalculatePath(game.spawn.position, westTarget, NavMesh.AllAreas, westPath)
                && westPath.status == NavMeshPathStatus.PathComplete, westPath.status.ToString());
            foreach(var item in markers)
            {
                var solid=item.GetComponentInChildren<Collider>();
                var nonblocking=item.assetId=="meadow-flower" || item.assetId=="stepping-stone";
                var hit=false;
                if(solid!=null && solid.enabled && !solid.isTrigger)
                {
                    var b=solid.bounds;hit=solid.Raycast(new Ray(new Vector3(b.min.x-.5f,b.center.y,b.center.z),Vector3.right),out _,b.size.x+1);
                }
                Record("prepared_instance_collision_"+item.instanceId,hit || (solid==null && nonblocking));
            }
            if(collider==null)
            {
                var intentionallyNonblocking=markers.Length>0 && markers.All(t=>t.assetId=="meadow-flower" || t.assetId=="stepping-stone");
                Record("prepared_asset_collider",intentionallyNonblocking,"Nonblocking floor decoration policy");
                Record("prepared_asset_collision",intentionallyNonblocking,"No solid decoration requires a collision probe");yield break;
            }
            Record("prepared_asset_collider",!collider.isTrigger && collider.enabled);
            var original = game.actor.position;
            var bounds = collider.bounds;
            var start = new Vector3(bounds.min.x - .8f, .05f, bounds.center.z);
            game.PlaceActor(start, 90);
            if (game.navigator != null) game.navigator.enabled = false;
            for (var i = 0; i < 50; i++) { game.body.Move(new Vector3(.045f, -.01f, 0)); yield return null; }
            Record("prepared_asset_collision", game.actor.position.x < start.x + 2.25f - .25f && game.actor.position.x > start.x + .1f
                && Vector3.Distance(collider.ClosestPoint(game.actor.position + Vector3.up * .4f), game.actor.position + Vector3.up * .4f) >= game.body.radius * .75f,
                "actor=" + game.actor.position.ToString("F3") + "; bench=" + bounds.ToString());
            game.PlaceActor(original, 180);
            yield return null;
        }
        IEnumerator Await(Func<bool> predicate, float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null) { File.WriteAllBytes(Path.Combine(output, name), tex.EncodeToPNG()); Destroy(tex); }
        }
        public static string FileHash(string path)
        {
            if (!File.Exists(path)) return "";
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        [Serializable] public sealed class DependencyFile { public string path, sha256; }
        [Serializable] public sealed class VerificationContext
        {
            public string runId, revision, assemblyVersion, scenePath, sceneSha256, layoutJson;
            public DependencyFile[] dependencyFiles;
        }
        [Serializable] sealed class Check { public string name; public bool passed; public string detail; }
        [Serializable] sealed class Report
        {
            public string utc, unity, isolatedSave, scenePath, runId, revision, assemblyVersion, sceneSha256, layoutJson;
            public DependencyFile[] dependencyFiles; public int bindingVersion, errors, navMeshVertices; public Check[] checks; public bool passed;
        }
    }
}
