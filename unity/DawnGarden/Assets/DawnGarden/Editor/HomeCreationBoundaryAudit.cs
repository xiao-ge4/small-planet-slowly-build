using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DawnGarden.Editor
{
    // Inspect saved scenes in preview scenes; never play, rebuild or save a user's home.
    public static class HomeCreationBoundaryAudit
    {
        [Serializable] public class Contract
        {
            public int schemaVersion;
            public string id;
            public Actor actor;
            public Navigation navigation;
            public Terrain terrain;
            public Interactions interactions;
        }
        [Serializable] public class Actor { public float radius, height, stepOffset, slopeLimit; public string[] locomotionClips, interactionClips; }
        [Serializable] public class Navigation { public int agentTypeId; public float bakedRadius, bakedHeight, sampleTolerance; }
        [Serializable] public class Terrain { public int walkableLayer, solidLayer; public float maxAuthoredStep, maxAuthoredSlope; }
        [Serializable] public class Interactions { public string[] allowedKinds; public float activationDistance; }
        [Serializable] public class AuditCheck { public string name, detail; public bool passed; }
        [Serializable] public class Home
        {
            public string scene, homeId, title, sceneSha256;
            public Vector3 spawn, mainLandSize, extensionSize, extensionCenter;
            public float bakedRadius, bakedHeight, bakedClimb, bakedSlope;
            public List<AuditCheck> checks = new List<AuditCheck>();
        }
        [Serializable] public class Report
        {
            public string utc, contractId, contractSha256, unity;
            public bool passed, activeScenePreserved, sceneFilesUnchanged;
            public int checks, failures;
            public List<Home> homes = new List<Home>();
            public string[] limitations = {
                "Saved scene and isolated baked-NavMesh audit; not a new full Play Mode interaction regression.",
                "Corridor width 1.4 m and headroom 2.2 m are creation targets, not measured clearance results.",
                "Existing NavMesh climb/slope exceed CharacterController limits; new terrain must use the stricter controller limits.",
                "Optional interactions, multiple authored extensions and generic Agent assembly are later work."
            };
        }
        static readonly string[] Paths = { "Assets/DawnGarden/Scenes/DawnGarden.unity", "Assets/DawnGarden/Scenes/EmberCamp.unity" };
        // Isolate each navigation query from the player's currently loaded scene.
        static readonly Vector3 Offset = new Vector3(10000, 1000, 10000);
        static string Workspace => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        static void Check(Home h, string name, bool passed, string detail = "") => h.checks.Add(new AuditCheck { name = name, passed = passed, detail = detail });
        static bool Near(float a, float b) => Mathf.Abs(a-b) < .001f;

        [MenuItem("Dawn Garden/Creation/Validate Creation Boundaries")]
        public static void ValidateFromMenu() { Run(); }

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run the boundary audit in Edit Mode after compilation.");
            var contractPath = Path.Combine(Workspace, "contracts/home-creation-boundaries.v1.json");
            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(contractPath));
            if (contract.schemaVersion != 1 || contract.actor == null || contract.navigation == null || contract.terrain == null || contract.interactions == null
                || contract.navigation.sampleTolerance <= 0 || contract.navigation.bakedRadius < contract.actor.radius
                || contract.navigation.bakedHeight < contract.actor.height)
                throw new InvalidDataException("Invalid creation boundary contract.");
            var active = SceneManager.GetActiveScene(); var activeDirty = active.isDirty;
            var before = Paths.Select(Hash).ToArray();
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), contractId = contract.id, contractSha256 = Hash(contractPath), unity = Application.unityVersion };
            for (var i = 0; i < Paths.Length; i++) report.homes.Add(Inspect(Paths[i], before[i], contract));
            if (report.homes.Select(h => h.homeId).Distinct().Count() != report.homes.Count)
                throw new InvalidDataException("Source homes have conflicting home IDs.");
            report.activeScenePreserved = active == SceneManager.GetActiveScene() && active.isDirty == activeDirty;
            report.sceneFilesUnchanged = Paths.Select(Hash).SequenceEqual(before);
            report.checks = report.homes.Sum(h => h.checks.Count);
            report.failures = report.homes.Sum(h => h.checks.Count(c => !c.passed));
            report.passed = report.failures == 0 && report.activeScenePreserved && report.sceneFilesUnchanged;
            var output = Path.Combine(Workspace, "artifacts/home-creation/boundary-audit.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("Creation boundary audit: " + report.checks + " checks, " + report.failures + " failures. " + output);
            return output;
        }
        static Home Inspect(string path, string hash, Contract c)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var game = roots.SelectMany(r => r.GetComponentsInChildren<DawnGardenGame>(true)).Single();
                var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var ground = transforms.Single(t => t.name == "Walkable Island").gameObject;
                var exp = game.expansion;
                var home = new Home { scene = path, sceneSha256 = hash, homeId = game.homeId, title = game.homeTitle, spawn = game.spawn.position,
                    mainLandSize = ground.GetComponent<MeshFilter>().sharedMesh.bounds.size,
                    extensionSize = exp.land.GetComponent<MeshFilter>().sharedMesh.bounds.size, extensionCenter = exp.finalPosition };
                Check(home, "authored_ground_collider", ground.layer == c.terrain.walkableLayer && ground.GetComponent<MeshCollider>()?.sharedMesh != null);
                Check(home, "extension_has_separate_collider", exp.landCollider != null && exp.landCollider.sharedMesh != null && exp.land.gameObject.layer == c.terrain.walkableLayer);
                var body = game.body;
                Check(home, "current_pet_collision_profile", Near(body.radius,c.actor.radius) && Near(body.height,c.actor.height) && Near(body.stepOffset,c.actor.stepOffset) && Near(body.slopeLimit,c.actor.slopeLimit));
                var clips = c.actor.locomotionClips.Concat(c.actor.interactionClips);
                Check(home, "reuse_four_existing_clips", clips.All(n => game.clips.Any(a => a != null && a.name == n)));
                var skin = game.actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Check(home, "reuse_current_skeleton", skin != null && skin.bones.Length > 10 && game.animator != null && game.animator.runtimeAnimatorController != null);
                var surface = exp.surface;
                Check(home, "collider_based_navigation", surface != null && surface.agentTypeID == c.navigation.agentTypeId && surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders
                    && (surface.layerMask.value & (1 << c.terrain.walkableLayer)) != 0 && (surface.layerMask.value & (1 << c.terrain.solidLayer)) != 0);
                var settings = NavMesh.GetSettingsByID(c.navigation.agentTypeId);
                home.bakedRadius=settings.agentRadius; home.bakedHeight=settings.agentHeight; home.bakedClimb=settings.agentClimb; home.bakedSlope=settings.agentSlope;
                Check(home, "baked_agent_profile", Near(settings.agentRadius,c.navigation.bakedRadius) && Near(settings.agentHeight,c.navigation.bakedHeight));
                Check(home, "supported_interaction_kinds", game.interactions.All(i => c.interactions.allowedKinds.Contains(i.kind.ToString())));
                Check(home, "stable_interaction_ids", game.interactions.All(i => !string.IsNullOrWhiteSpace(i.stableId)) && game.interactions.Select(i=>i.stableId).Distinct().Count()==game.interactions.Length);
                foreach (var item in game.interactions)
                {
                    Check(home, item.kind + "_separate_stateful_visual", item.visual != null && !item.transform.IsChildOf(ground.transform) && item.visual.IsChildOf(item.transform));
                    Check(home, item.kind + "_approach_in_activation_range", item.approach != null && Near(item.activationDistance,c.interactions.activationDistance)
                        && Vector2.Distance(new Vector2(item.approach.position.x,item.approach.position.z),new Vector2(item.transform.position.x,item.transform.position.z)) <= item.activationDistance
                        && Mathf.Abs(item.approach.position.y-item.transform.position.y) < .8f);
                }
                var pond = transforms.Single(t => t.name == "Pond No Walk Volume");
                AuditNavigation(home, game, exp.originalNavigation, false, pond.position, c.navigation.sampleTolerance);
                AuditNavigation(home, game, exp.expandedNavigation, true, pond.position, c.navigation.sampleTolerance);
                return home;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void AuditNavigation(Home home, DawnGardenGame game, NavMeshData data, bool expanded, Vector3 pond, float tolerance)
        {
            var prefix = expanded ? "expanded_" : "starter_";
            Check(home, prefix + "saved_navigation_exists", data != null);
            if (data == null) return;
            var instance = NavMesh.AddNavMeshData(data, Offset, Quaternion.identity);
            try
            {
                var source = game.spawn.position + Offset;
                var onMesh = NavMesh.SamplePosition(source, out var spawn, tolerance, NavMesh.AllAreas);
                Check(home, prefix + "spawn_reachable", onMesh);
                foreach (var item in game.interactions)
                {
                    var valid = onMesh && item.approach != null && CompletePath(spawn.position,item.approach.position+Offset,tolerance);
                    Check(home,prefix + item.kind + "_approach_reachable",valid);
                }
                var target=game.expansion.WalkTarget+Offset;
                Check(home,prefix + "land_access_matches_unlock",expanded
                    ? onMesh && CompletePath(spawn.position,target,tolerance)
                    : !NavMesh.SamplePosition(target,out _,tolerance,NavMesh.AllAreas));
                var water = new Vector3(pond.x,0,pond.z)+Offset;
                Check(home,prefix + "pond_or_spring_excluded",!NavMesh.SamplePosition(water,out _,.2f,NavMesh.AllAreas));
                Check(home,prefix + "outer_sea_excluded",!NavMesh.SamplePosition(Offset+new Vector3(0,0,7),out _,.2f,NavMesh.AllAreas));
            }
            finally { instance.Remove(); }
        }
        static bool CompletePath(Vector3 from, Vector3 target, float tolerance)
        {
            if (!NavMesh.SamplePosition(target,out var to,tolerance,NavMesh.AllAreas)) return false;
            var path=new NavMeshPath();
            return NavMesh.CalculatePath(from,to.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
        }
    }
}
