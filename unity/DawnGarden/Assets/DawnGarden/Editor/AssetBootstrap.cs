using System;
using System.IO;
using System.Linq;
using DawnGarden;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden.Editor
{
    [InitializeOnLoad]
    public static class AssetBootstrap
    {
        const string Root = "Assets/DawnGarden";
        const string ScenePath = Root + "/Scenes/AssetValidation.unity";
        const string Pending = "DawnGarden.AssetVerification";
        static double deadline;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.tooling/verification"));
        static AssetBootstrap()
        {
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Pending, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    var demo = UnityEngine.Object.FindFirstObjectByType<AssetDemo>();
                    if (demo == null) throw new InvalidOperationException("Asset demo not found");
                    demo.BeginVerification(Output);
                }
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Pending, false);
            };
            EditorApplication.update += () => {
                if (!SessionState.GetBool(Pending, false)) return;
                if (File.Exists(Path.Combine(Output, "assets-runtime.json")))
                {
                    SessionState.SetBool(Pending, false);
                    EditorApplication.ExitPlaymode();
                }
                else if (EditorApplication.timeSinceStartup > deadline)
                {
                    SessionState.SetBool(Pending, false);
                    Debug.LogError("Asset verification timed out");
                    EditorApplication.ExitPlaymode();
                }
            };
        }
        [MenuItem("Dawn Garden/Setup Asset Validation")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode first");
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Build();
        }
        // Explicitly rebuilding is separate from opening, so manual scene edits are preserved.
        [MenuItem("Dawn Garden/Rebuild Asset Validation")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode first");
            Directory.CreateDirectory(Root + "/Animations");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.72f, .79f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.55f, .63f, .64f);
            RenderSettings.ambientGroundColor = new Color(.34f, .4f, .31f);
            RenderSettings.fog = false;

            var ground = Primitive(PrimitiveType.Cylinder, "Test Platform", new Vector3(0, -.14f, 0), new Vector3(9, .12f, 8), new Color(.49f, .65f, .48f));
            var actor = new GameObject("ActorRoot");
            var pet = InstantiateAsset("Characters/Pet.glb", actor.transform, 1.8f);
            var animator = pet.GetComponentInChildren<Animator>();
            if (animator == null) throw new InvalidOperationException("Imported character has no Animator");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var hips = Find(pet.transform, "mixamorig:Hips");
            var hand = Find(pet.transform, "mixamorig:RightHand");
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(Root + "/Art/Characters/Pet.glb").OfType<AnimationClip>()
                .OrderBy(c => Array.IndexOf(new[] { "Idle", "Jog", "Watering", "Chest" }, c.name)).ToArray();
            if (sourceClips.Length != 4) throw new InvalidOperationException("Expected four imported clips");
            var clips = sourceClips.Select(c => SaveClip(c, hips.localPosition)).ToArray();
            var controllerPath = Root + "/Animations/Pet.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name); state.motion = clip;
                if (clip.name == "Idle") machine.defaultState = state;
            }
            animator.runtimeAnimatorController = controller;
            actor.transform.position = new Vector3(0, 0, .9f);

            var chestRoot = new GameObject("ChestRoot"); chestRoot.transform.position = new Vector3(1.45f, 0, -1.03f);
            var chest = InstantiateAsset("Props/InteractiveChest.glb", chestRoot.transform, .68f);
            var lid = Find(chest.transform, "LidHinge");
            // Make stand-alone reusable wrappers; render bounds are deliberately not used for collisions.
            var chestCollider = chestRoot.AddComponent<BoxCollider>(); chestCollider.center = new Vector3(0, .24f, 0); chestCollider.size = new Vector3(.85f, .48f, .55f);
            PrefabUtility.SaveAsPrefabAsset(chestRoot, Root + "/Prefabs/InteractiveChest.prefab");
            var canRoot = new GameObject("WateringCanDisplay"); canRoot.transform.position = new Vector3(-2.4f, 0, .9f);
            InstantiateAsset("Props/WateringCan.glb", canRoot.transform, .46f);
            PrefabUtility.SaveAsPrefabAsset(canRoot, Root + "/Prefabs/WateringCan.prefab");

            // Calibrate the grip at the source clip's authored hold pose. glTF import mirrors X.
            clips.First(c => c.name == "Watering").SampleAnimation(pet, .8f);
            var socket = new GameObject("WateringGripSocket").transform;
            socket.SetParent(hand, false);
            socket.localScale = Vector3.one / pet.transform.localScale.x;
            socket.rotation = Quaternion.Euler(0, -90f, 0);
            var heldCan = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Props/WateringCan.glb"));
            heldCan.transform.SetParent(socket, false);
            var canScale = .46f / Bounds(heldCan).size.y;
            heldCan.transform.localScale = Vector3.one * canScale;
            heldCan.transform.localPosition = new Vector3(.073f, -.023f, 0) * canScale;
            var nozzle = new GameObject("Spout").transform; nozzle.SetParent(heldCan.transform, false); nozzle.localPosition = new Vector3(.098f, .038f, 0);
            socket.gameObject.SetActive(false);
            clips.First(c => c.name == "Idle").SampleAnimation(pet, 0);
            PrefabUtility.SaveAsPrefabAsset(actor, Root + "/Prefabs/Pet.prefab");

            var plant = new GameObject("Test Plant").transform; plant.position = new Vector3(-1.1f, 0, -1.05f);
            var soil = Primitive(PrimitiveType.Cylinder, "Soil", new Vector3(0, .035f, 0), new Vector3(.65f, .035f, .65f), new Color(.35f, .24f, .15f)); soil.transform.SetParent(plant, false);
            for (var i = 0; i < 3; i++)
            {
                var leaf = Primitive(PrimitiveType.Sphere, "Leaf", new Vector3((i-1)*.16f, .22f, 0), new Vector3(.17f, .36f, .1f), new Color(.28f, .61f, .3f));
                leaf.transform.SetParent(plant, false); leaf.transform.localRotation = Quaternion.Euler(0, 0, (i-1)*-25);
            }
            var drops = new Transform[24];
            for (var i = 0; i < drops.Length; i++)
            {
                drops[i] = Primitive(PrimitiveType.Sphere, "Water Drop", Vector3.zero, new Vector3(.025f, .045f, .025f), new Color(.3f, .73f, .95f)).transform;
                drops[i].gameObject.SetActive(false);
            }
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.6f; light.color = new Color(1, .96f, .9f); light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48, -28, 0); RenderSettings.sun = light;
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.78f, .85f, .83f);
            camera.transform.position = new Vector3(3.5f, 3.4f, 6.2f); camera.transform.LookAt(new Vector3(0, .75f, 0));
            camera.fieldOfView = 36; camera.nearClipPlane = .05f; camera.farClipPlane = 50;
            camera.gameObject.AddComponent<AudioListener>(); camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var demo = new GameObject("Asset Demo").AddComponent<AssetDemo>();
            demo.animator = animator; demo.actorRoot = actor.transform; demo.hips = hips; demo.rightHand = hand; demo.lid = lid;
            demo.plant = plant; demo.gripSocket = socket; demo.nozzle = nozzle; demo.drops = drops; demo.clips = clips; demo.demoCamera = camera;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(Root + "/Scenes/EnvironmentCheck.unity", true) };
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "assets-import.json"), "{\"character\":\"Pet.glb\",\"boneCount\":" + pet.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r=>r.bones.Length) + ",\"clips\":4,\"humanoidRetargetVerified\":false}");
            Debug.Log("Dawn Garden: imported assets and created AssetValidation scene");
        }
        static AnimationClip SaveClip(AnimationClip source, Vector3 rest)
        {
            var clip = UnityEngine.Object.Instantiate(source); clip.name = source.name;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!binding.path.EndsWith("mixamorig:Hips")) continue;
                if (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z")
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, clip.length, binding.propertyName.EndsWith("x") ? rest.x : rest.z));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = clip.name == "Idle" || clip.name == "Jog";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var path = Root + "/Animations/" + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
            return clip;
        }
        static GameObject InstantiateAsset(string path, Transform parent, float height)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/" + path);
            if (source == null) throw new InvalidOperationException("Asset not imported: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source); go.transform.SetParent(parent, false);
            var bounds = Bounds(go); var scale = height / bounds.size.y;
            go.transform.localScale = Vector3.one * scale;
            var center = bounds.center - parent.position;
            go.transform.localPosition = new Vector3(-center.x, -(bounds.min.y - parent.position.y), -center.z) * scale;
            return go;
        }
        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers in " + go.name);
            var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var path = Root + "/Materials/Asset" + name.Replace(" ", "") + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color; AssetDatabase.CreateAsset(mat, path); }
            go.GetComponent<Renderer>().sharedMaterial = mat; return go;
        }
        [MenuItem("Dawn Garden/Run Asset Verification")]
        public static void Verify()
        {
            Create(); Directory.CreateDirectory(Output);
            var report = Path.Combine(Output, "assets-runtime.json"); if (File.Exists(report)) File.Delete(report);
            deadline = EditorApplication.timeSinceStartup + 180;
            SessionState.SetBool(Pending, true);
            var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.EnterPlaymode();
        }
    }
}
