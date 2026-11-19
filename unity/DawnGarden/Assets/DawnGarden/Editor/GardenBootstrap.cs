using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DawnGarden;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden.Editor
{
    [InitializeOnLoad]
    public static class GardenBootstrap
    {
        const string Root = "Assets/DawnGarden";
        const string ScenePath = Root + "/Scenes/DawnGarden.unity";
        const string Pending = "DawnGarden.GardenVerification";
        const int GroundLayer = 8, SolidLayer = 9;
        static double deadline;
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.tooling/verification"));
        static GardenBootstrap()
        {
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Pending, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    var game = UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
                    if (game == null) { Debug.LogError("Garden game is missing"); return; }
                    game.gameObject.AddComponent<GardenVerification>().Begin(game, Output);
                }
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Pending, false);
            };
            EditorApplication.update += () => {
                if (!SessionState.GetBool(Pending, false)) return;
                if (File.Exists(Path.Combine(Output, "garden-runtime.json")))
                {
                    SessionState.SetBool(Pending, false); EditorApplication.ExitPlaymode();
                }
                else if (EditorApplication.timeSinceStartup > deadline)
                {
                    SessionState.SetBool(Pending, false); Debug.LogError("Garden verification timed out"); EditorApplication.ExitPlaymode();
                }
            };
        }
        [MenuItem("Dawn Garden/Open Dawn Garden")]
        public static void Open()
        {
            EnsureEdit();
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); else Build();
        }
        static void EnsureEdit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before changing scenes");
            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty) throw new InvalidOperationException("Save the current scene before changing scenes");
        }
        [MenuItem("Dawn Garden/Rebuild Dawn Garden Greybox")]
        public static void Build()
        {
            EnsureEdit();
            foreach (var dir in new[] { "Garden/Materials", "Garden/Meshes", "Garden/Navigation" }) Directory.CreateDirectory(Root + "/" + dir);
            SetLayer(GroundLayer, "GardenGround"); SetLayer(SolidLayer, "GardenSolid");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.72f, .83f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.56f, .64f, .54f);
            RenderSettings.ambientGroundColor = new Color(.33f, .37f, .27f);
            RenderSettings.fog = false;
            var world = new GameObject("Garden World").transform;
            var decor = new GameObject("Garden Decorations").transform; decor.SetParent(world, false);
            var grass = Material("Meadow", new Color(.57f, .72f, .4f));
            var earth = Material("Cliff", new Color(.60f, .46f, .30f));
            var cream = Material("Warm stone", new Color(.81f, .79f, .61f));
            var wood = Material("Cedar", new Color(.46f, .31f, .18f));
            var leafLight = Material("Leaf light", new Color(.65f, .77f, .37f));
            var leafDark = Material("Leaf dark", new Color(.36f, .60f, .33f));
            var water = Material("Still water", new Color(.37f, .73f, .75f), .7f);
            var sea = Material("Sea", new Color(.36f, .60f, .66f), .55f);
            var island = IslandMesh();
            var land = new GameObject("Walkable Island"); land.layer = GroundLayer; land.transform.SetParent(world, false);
            land.AddComponent<MeshFilter>().sharedMesh = island;
            land.AddComponent<MeshRenderer>().sharedMaterials = new[] { grass, earth };
            land.AddComponent<MeshCollider>().sharedMesh = island;
            Primitive(PrimitiveType.Cylinder, "Ocean", new Vector3(0, -1.05f, 0), new Vector3(150, .02f, 150), sea, null, false);

            var pond = Primitive(PrimitiveType.Cylinder, "Pond Water", new Vector3(2.7f, .018f, 1.65f), new Vector3(2.5f, .025f, 1.9f), water, decor, false);
            var bank = Primitive(PrimitiveType.Cylinder, "Pond Bank", new Vector3(2.7f, -.015f, 1.65f), new Vector3(2.85f, .045f, 2.2f), cream, decor, false);
            var pondBarrier = Primitive(PrimitiveType.Cylinder, "Pond No Walk Volume", new Vector3(2.7f, .45f, 1.65f), new Vector3(2.55f, .65f, 2f), water, world, true);
            pondBarrier.GetComponent<Renderer>().enabled = false;
            var pondModifier = pondBarrier.AddComponent<NavMeshModifier>(); pondModifier.overrideArea = true; pondModifier.area = 1;
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2 / 12;
                var stone = Primitive(PrimitiveType.Sphere, "Pond Pebble", new Vector3(2.7f + Mathf.Cos(angle) * 1.33f, .045f, 1.65f + Mathf.Sin(angle) * 1.04f), new Vector3(.24f, .15f, .2f), cream, decor, false);
            }

            // A central obstacle deliberately exercises route planning; the west rock tests collision.
            Rock("Central Boulder", new Vector3(-1.25f, 0, 1.15f), new Vector3(1.2f, 1f, 1.15f), cream, world);
            Rock("West Boulder", new Vector3(-3.8f, 0, .65f), new Vector3(1.2f, 1.1f, 1.2f), cream, world);
            Rock("North Rocks", new Vector3(.9f, 0, -4.1f), new Vector3(.8f, .55f, .6f), cream, world);
            Tree(new Vector3(-4.35f, 0, -1.6f), 1.05f, wood, leafDark, decor);
            Tree(new Vector3(-3.75f, 0, -3.05f), .8f, wood, leafLight, decor);
            Tree(new Vector3(4.35f, 0, -.8f), 1, wood, leafLight, decor);
            Tree(new Vector3(3.7f, 0, -3.05f), .85f, wood, leafDark, decor);
            Tree(new Vector3(-3.2f, 0, 3.25f), .8f, wood, leafLight, decor);
            var stonePositions = new[] { new Vector3(0, 0, 3.2f), new Vector3(.05f, 0, 2.5f), new Vector3(.05f, 0, 1.75f), new Vector3(.15f, 0, .95f),
                new Vector3(0, 0, .2f), new Vector3(-.6f, 0, -.4f), new Vector3(-1.4f, 0, -.8f), new Vector3(-2.25f, 0, -1f),
                new Vector3(.8f, 0, -.35f), new Vector3(1.5f, 0, -.8f), new Vector3(2.2f, 0, -1f) };
            foreach (var p in stonePositions) Primitive(PrimitiveType.Cylinder, "Stepping Stone", p + Vector3.up * .025f, new Vector3(.58f, .025f, .43f), cream, decor, false);
            var flowerColors = new[] { Material("Petal peach", new Color(.98f, .70f, .54f)), Material("Petal cream", new Color(1f, .9f, .63f)), Material("Petal lilac", new Color(.74f, .68f, .87f)) };
            for (var i = 0; i < 24; i++)
            {
                var angle = i * 2.39996f; var radius = 3.9f + .35f * Mathf.Sin(i * 3.2f);
                var p = new Vector3(Mathf.Cos(angle) * radius * 1.12f, 0, Mathf.Sin(angle) * radius * .86f);
                if (p.x > 1.1f && p.z > .4f) continue;
                Primitive(PrimitiveType.Capsule, "Flower Stem", p + Vector3.up * .14f, new Vector3(.045f, .14f, .045f), leafDark, decor, false);
                Primitive(PrimitiveType.Sphere, "Flower", p + Vector3.up * .31f, new Vector3(.17f, .12f, .17f), flowerColors[i % 3], decor, false);
            }
            // A stationary marker reserves the future travel point; it has no task yet.
            Primitive(PrimitiveType.Cylinder, "Travel Marker Base", new Vector3(-.4f, .1f, -3.75f), new Vector3(1, .1f, 1), cream, decor, true);
            var marker = Primitive(PrimitiveType.Cube, "Travel Marker", new Vector3(-.4f, .75f, -3.75f), new Vector3(.55f, 1.2f, .45f), cream, decor, true);
            marker.transform.rotation = Quaternion.Euler(0, 35, 0);
            var gem = Primitive(PrimitiveType.Cube, "Dew Crystal", new Vector3(-.4f, 1.52f, -3.75f), Vector3.one * .3f, Material("Crystal", new Color(.48f, .83f, .85f)), decor, false);
            gem.transform.rotation = Quaternion.Euler(25, 35, 35);

            var plantRoot = new GameObject("Waterable Seedling"); plantRoot.transform.SetParent(world, false); plantRoot.transform.position = new Vector3(-2.3f, 0, -2.1f);
            var plant = plantRoot.AddComponent<GardenInteraction>(); plant.stableId = "starter-seedling"; plant.displayName = "晨露新芽"; plant.kind = GardenInteractionKind.Water;
            var plantCollision = plantRoot.AddComponent<CapsuleCollider>(); plantCollision.radius = .38f; plantCollision.height = .65f; plantCollision.center = new Vector3(0, .3f, 0); plantRoot.layer = SolidLayer;
            var soil = Material("Soil", new Color(.38f, .26f, .16f));
            Primitive(PrimitiveType.Cylinder, "Seed Bed", plantRoot.transform.position + Vector3.up * .05f, new Vector3(1.15f, .05f, 1.05f), soil, decor, false);
            plant.visual = new GameObject("Growing Leaves").transform; plant.visual.SetParent(plantRoot.transform, false);
            for (var i = 0; i < 4; i++)
            {
                var leaf = Primitive(PrimitiveType.Sphere, "Seedling Leaf", Vector3.zero, new Vector3(.2f, .46f + i % 2 * .08f, .13f), i % 2 == 0 ? leafDark : leafLight, null, false);
                leaf.transform.SetParent(plant.visual, false); leaf.transform.localPosition = new Vector3((i-1.5f)*.13f, .29f, i%2*.1f-.05f); leaf.transform.localRotation = Quaternion.Euler(0, i*40, (i-1.5f)*-18f);
            }
            plant.approach = Approach(plantRoot.transform, new Vector3(0, .06f, 1.08f));
            var chestRoot = Instance("InteractiveChest", world); chestRoot.name = "Starter Supply Chest"; chestRoot.transform.position = new Vector3(2.2f, 0, -2.1f); SetLayerRecursive(chestRoot, SolidLayer);
            var chest = chestRoot.AddComponent<GardenInteraction>(); chest.stableId = "starter-supply"; chest.displayName = "星球补给箱"; chest.kind = GardenInteractionKind.Chest;
            chest.lid = chestRoot.GetComponentsInChildren<Transform>(true).First(t => t.name == "LidHinge"); chest.visual = chestRoot.transform;
            chest.approach = Approach(chestRoot.transform, new Vector3(0, .06f, 1.08f));
            var toolStand = Primitive(PrimitiveType.Cylinder, "Tool Stump", new Vector3(-3.8f, .25f, -2f), new Vector3(.7f, .25f, .7f), wood, decor, true);
            var can = Instance("WateringCan", decor); can.transform.position = new Vector3(-3.8f, .5f, -2);

            var surface = world.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << GroundLayer) | (1 << SolidLayer); surface.overrideVoxelSize = true; surface.voxelSize = .08f;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake produced no data");
            var navPath = Root + "/Garden/Navigation/StarterNavMesh.asset";
            var nav = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (nav == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            else
            {
                var generated = surface.navMeshData; surface.RemoveData();
                EditorUtility.CopySerialized(generated, nav); surface.navMeshData = nav; surface.AddData(); UnityEngine.Object.DestroyImmediate(generated);
            }

            var actor = Instance("Pet", null); actor.name = "Player Pet"; actor.transform.position = new Vector3(0, .1f, 3.65f);
            var body = actor.AddComponent<CharacterController>(); body.height = 1.7f; body.radius = .24f; body.center = new Vector3(0, .85f, 0); body.stepOffset = .25f; body.slopeLimit = 40; body.skinWidth = .025f; body.minMoveDistance = 0;
            var navigator = actor.AddComponent<NavMeshAgent>(); navigator.updatePosition = false; navigator.updateRotation = false;
            navigator.radius = .24f; navigator.height = 1.7f; navigator.baseOffset = 0; navigator.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            var spawn = new GameObject("Home Spawn").transform; spawn.position = actor.transform.position;
            var light = new GameObject("Morning Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.color = new Color(1, .93f, .8f); light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(50, -35, 0); RenderSettings.sun = light;
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.fieldOfView = 42; camera.nearClipPlane = .08f; camera.farClipPlane = 180;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.70f, .83f, .84f);
            camera.gameObject.AddComponent<AudioListener>(); camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var game = new GameObject("Dawn Garden Game").AddComponent<DawnGardenGame>();
            var orbit = camera.gameObject.AddComponent<GardenOrbitCamera>(); orbit.target = actor.transform; orbit.game = game; orbit.overview = true; orbit.distance = 15.5f;
            game.actor = actor.transform; game.animator = actor.GetComponentInChildren<Animator>(); game.body = body; game.spawn = spawn; game.worldCamera = camera; game.orbit = orbit; game.groundMask = 1 << GroundLayer;
            game.navigator = navigator;
            game.interfaceFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/NotoSansSC-Regular.otf");
            game.clips = new[] { "Idle", "Jog", "Watering", "Chest" }.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/" + n + ".anim")).ToArray();
            game.gripSocket = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "WateringGripSocket");
            game.nozzle = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "Spout");
            game.interactions = new[] { plant, chest };
            var drops = new Transform[28]; var dropMat = Material("Water Drop", new Color(.33f, .77f, .96f));
            for (var i = 0; i < drops.Length; i++)
            {
                drops[i] = Primitive(PrimitiveType.Sphere, "Water Drop", Vector3.zero, new Vector3(.025f, .05f, .025f), dropMat, null, false).transform; drops[i].gameObject.SetActive(false);
            }
            game.drops = drops;
            camera.transform.position = new Vector3(-6, 9, 12); camera.transform.LookAt(Vector3.zero);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Dawn Garden: garden greybox built with baked navigation and physics colliders");
        }
        static Transform Approach(Transform parent, Vector3 localPosition)
        {
            var t = new GameObject("Interaction Approach").transform; t.SetParent(parent, false); t.localPosition = localPosition; return t;
        }
        static GameObject Instance(string name, Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");
            if (source == null) throw new InvalidOperationException("Import existing assets first: " + name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source); instance.transform.SetParent(parent, false); return instance;
        }
        static Material Material(string name, Color color, float smoothness = .1f)
        {
            var path = Root + "/Garden/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color; mat.SetFloat("_Smoothness", smoothness); AssetDatabase.CreateAsset(mat, path); }
            return mat;
        }
        static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material mat, Transform parent, bool solid)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (solid) go.layer = SolidLayer; else UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static void Rock(string name, Vector3 position, Vector3 size, Material mat, Transform parent)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.position = position; root.layer = SolidLayer;
            var box = root.AddComponent<BoxCollider>(); box.size = size; box.center = Vector3.up * size.y * .5f;
            var visual = Primitive(PrimitiveType.Sphere, name + " Visual", position + Vector3.up * size.y * .45f, size * 1.12f, mat, root.transform, false);
            visual.transform.rotation = Quaternion.Euler(5, 25, -7);
        }
        static void Tree(Vector3 p, float scale, Material trunk, Material leaves, Transform parent)
        {
            Primitive(PrimitiveType.Cylinder, "Tree Trunk", p + Vector3.up * .85f * scale, new Vector3(.35f, .85f, .35f)*scale, trunk, parent, true);
            Primitive(PrimitiveType.Sphere, "Tree Crown", p + Vector3.up * 2.05f * scale, new Vector3(1.9f, 1.9f, 1.65f)*scale, leaves, parent, false);
            Primitive(PrimitiveType.Sphere, "Tree Crown Tip", p + new Vector3(.35f, 2.6f, -.12f)*scale, new Vector3(1.3f, 1.3f, 1.25f)*scale, leaves, parent, false);
        }
        static Mesh IslandMesh()
        {
            const int count = 72;
            var verts = new List<Vector3> { Vector3.zero };
            for (var i = 0; i < count; i++)
            {
                var a = i * Mathf.PI * 2 / count; var r = 1 + .035f * Mathf.Sin(3*a+.3f) + .025f * Mathf.Cos(7*a);
                verts.Add(new Vector3(Mathf.Cos(a)*6.15f*r, 0, Mathf.Sin(a)*5.15f*r));
            }
            for (var i = 0; i < count; i++) { var v = verts[1+i] * .85f; v.y = -1.25f; verts.Add(v); }
            verts.Add(new Vector3(0, -1.25f, 0));
            var top = new List<int>(); var side = new List<int>();
            for (var i = 0; i < count; i++)
            {
                int a = 1+i, b = 1+(i+1)%count, c = a+count, d = b+count;
                top.AddRange(new[] { 0, b, a }); side.AddRange(new[] { a,b,d,a,d,c,verts.Count-1,c,d });
            }
            var mesh = new Mesh { name = "Dawn Garden Island", subMeshCount = 2 }; mesh.SetVertices(verts); mesh.SetTriangles(top, 0); mesh.SetTriangles(side, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var path = Root + "/Garden/Meshes/Island.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path); else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            return mesh;
        }
        static void SetLayer(int index, string name)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != name) throw new InvalidOperationException("Layer slot already occupied: " + index);
            layer.stringValue = name; tags.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetLayerRecursive(GameObject go, int layer) { foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
        [MenuItem("Dawn Garden/Run Garden Verification")]
        public static void Verify()
        {
            Open(); Directory.CreateDirectory(Output);
            var report = Path.Combine(Output, "garden-runtime.json"); if (File.Exists(report)) File.Delete(report);
            SessionState.SetBool(Pending, true); deadline = EditorApplication.timeSinceStartup + 180;
            var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) { var window = EditorWindow.GetWindow(gameView); window.Focus(); }
            EditorApplication.EnterPlaymode();
        }
    }
}
