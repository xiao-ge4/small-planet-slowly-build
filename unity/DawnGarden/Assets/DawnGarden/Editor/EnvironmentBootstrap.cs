using System;
using System.IO;
using DawnGarden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden.Editor
{
    [InitializeOnLoad]
    public static class EnvironmentBootstrap
    {
        private const string Root = "Assets/DawnGarden";
        private const string ScenePath = Root + "/Scenes/EnvironmentCheck.unity";
        private const string PendingKey = "DawnGarden.EnvironmentVerification.Pending";
        private const string StartedKey = "DawnGarden.EnvironmentVerification.Started";
        private static double deadline;

        static EnvironmentBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += WatchVerification;
            deadline = EditorApplication.timeSinceStartup + 120;
        }

        [MenuItem("Dawn Garden/Setup Environment Check")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before setting up the environment check.");

            var pipeline = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (!(pipeline is UniversalRenderPipelineAsset))
            {
                const string templatePipelinePath = "Assets/Settings/PC_RPAsset.asset";
                var templatePipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(templatePipelinePath);
                if (templatePipeline == null)
                    throw new InvalidOperationException("No URP pipeline is configured, and the template pipeline could not be loaded at " + templatePipelinePath + ". Import the URP template assets before creating the environment check.");
                GraphicsSettings.defaultRenderPipeline = templatePipeline;
                QualitySettings.renderPipeline = templatePipeline;
                AssetDatabase.SaveAssets();
                Debug.Log("Dawn Garden: configured the existing URP template pipeline from " + templatePipelinePath);
            }

            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                SetBuildScene();
                Debug.Log("Dawn Garden: opened the existing environment check; its contents were preserved.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.61f, 0.69f);
            RenderSettings.fog = false;

            var groundMaterial = Material("CheckGround", new Color(0.39f, 0.58f, 0.51f));
            var warmMaterial = Material("CheckWarm", new Color(0.92f, 0.58f, 0.28f));
            var coolMaterial = Material("CheckCool", new Color(0.29f, 0.62f, 0.83f));
            var creamMaterial = Material("CheckCream", new Color(0.94f, 0.86f, 0.61f));

            Primitive(PrimitiveType.Plane, "Environment Check Ground", Vector3.zero, new Vector3(1.2f, 1f, 1.2f), groundMaterial);
            Primitive(PrimitiveType.Cube, "Warm Cube", new Vector3(-2f, 0.6f, 0.3f), Vector3.one * 1.2f, warmMaterial);
            Primitive(PrimitiveType.Sphere, "Cool Sphere", new Vector3(0f, 0.75f, 0f), Vector3.one * 1.5f, coolMaterial);
            Primitive(PrimitiveType.Cube, "Tall Cream Cube", new Vector3(2f, 1f, 0.5f), new Vector3(1f, 2f, 1f), creamMaterial);

            var lightObject = new GameObject("Environment Check Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.sun = light;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.83f, 0.9f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            cameraObject.transform.position = new Vector3(8f, 6.7f, -10f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

            new GameObject("Environment Runtime Probe").AddComponent<EnvironmentProbe>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Dawn Garden: URP environment check created at " + ScenePath);
        }

        [MenuItem("Dawn Garden/Run Environment Verification")]
        public static void RunVerification()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before starting verification.");

            Create();
            var output = VerificationDirectory();
            Directory.CreateDirectory(output);
            var reportPath = Path.Combine(output, "environment-runtime.json");
            if (File.Exists(reportPath)) File.Delete(reportPath);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(StartedKey, false);
            deadline = EditorApplication.timeSinceStartup + 120;
            var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var probe = UnityEngine.Object.FindFirstObjectByType<EnvironmentProbe>();
                if (probe == null)
                {
                    FinishVerification("Dawn Garden verification failed: runtime probe is missing.");
                    return;
                }
                SessionState.SetBool(StartedKey, true);
                probe.BeginVerification(VerificationDirectory());
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool(StartedKey, false);
            }
        }

        private static void WatchVerification()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (SessionState.GetBool(StartedKey, false)
                && File.Exists(Path.Combine(VerificationDirectory(), "environment-runtime.json")))
            {
                FinishVerification(null);
            }
            else if (EditorApplication.timeSinceStartup > deadline)
            {
                FinishVerification("Dawn Garden verification timed out; inspect the Console and Game view.");
            }
        }

        private static void FinishVerification(string error)
        {
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(StartedKey, false);
            if (error == null) Debug.Log("Dawn Garden: runtime verification saved to " + VerificationDirectory());
            else Debug.LogError(error);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        private static string VerificationDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.tooling/verification"));
        }

        private static void SetBuildScene()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader could not be found.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.22f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
