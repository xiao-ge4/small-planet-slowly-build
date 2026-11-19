using System;
using System.IO;
using System.Linq;
using DawnGarden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGarden.Editor
{
    /// Runs the same isolated gameplay checks as the starter garden against a generated PreparedHomes scene.
    [InitializeOnLoad]
    public static class PreparedHomeBootstrap
    {
        const string RunId = "3ea386b9e1f54c399406cc67a44c4430";
        const string ScenePath = "Assets/DawnGarden/PreparedHomes/3ea386b9e1f54c399406cc67a44c4430/v2/MossyStoneCourtyard.unity";
        const string Pending = "DawnGarden.PreparedHomeVerification";
        const string DeadlineKey = "DawnGarden.PreparedHomeVerificationDeadline";
        const string OutputKey = "DawnGarden.PreparedHomeOutput";
        static string Output => SessionState.GetString(OutputKey, Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/home-creation/layout/" + RunId + "/v2")));

        static PreparedHomeBootstrap()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Pending, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    // Entering Play Mode reloads the editor domain, so the static deadline is reset.
                    SessionState.SetFloat(DeadlineKey, (float)(EditorApplication.timeSinceStartup + 240));
                    var game = UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
                    if (game == null) { Debug.LogError("Prepared home game is missing"); return; }
                    game.gameObject.AddComponent<GardenVerification>().Begin(game, Output, File.ReadAllText(Path.Combine(Output, "verification-context.json")));
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Pending, false);
                    SessionState.EraseFloat(DeadlineKey);
                }
            };
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(Pending, false)) return;
                if (File.Exists(Path.Combine(Output, "garden-runtime.json")))
                {
                    SessionState.SetBool(Pending, false);
                    EditorApplication.ExitPlaymode();
                }
                else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0))
                {
                    SessionState.SetBool(Pending, false);
                    Debug.LogError("Prepared home verification timed out");
                    EditorApplication.ExitPlaymode();
                }
            };
        }

        [MenuItem("Dawn Garden/Run Prepared Home Verification")]
        public static void Verify() => VerifyFor(ScenePath, Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/home-creation/layout/" + RunId + "/v2")));

        public static void VerifyFor(string scenePath, string directory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting prepared home verification");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save current scene changes before verification");
            if (!scenePath.StartsWith("Assets/DawnGarden/PreparedHomes/", StringComparison.Ordinal) || !File.Exists(scenePath))
                throw new InvalidOperationException("Prepared home scene not found: " + scenePath);
            SessionState.SetString(OutputKey, Path.GetFullPath(directory));
            Directory.CreateDirectory(Output);
            var report = Path.Combine(Output, "garden-runtime.json");
            if (File.Exists(report)) File.Delete(report);
            foreach (var screenshot in Directory.GetFiles(Output, "garden-*.png")) File.Delete(screenshot);
            EditorSceneManager.OpenScene(scenePath);
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the assembled scene before verification");
            var layoutJson = File.ReadAllText(Path.Combine(Output, "layout.json"));
            var context = JsonUtility.FromJson<GardenVerification.VerificationContext>(layoutJson);
            if (context.scenePath != scenePath || context.sceneSha256 != GardenVerification.FileHash(scenePath))
                throw new InvalidOperationException("Scene and layout differ; re-export the layout before verification");
            context.layoutJson = layoutJson;
            var dependencies = AssetDatabase.GetDependencies(scenePath, true)
                .Concat(new [] { "Assets/DawnGarden/Scripts/GardenVerification.cs", "Assets/DawnGarden/Editor/PreparedHomeBootstrap.cs", "Packages/manifest.json", "Packages/packages-lock.json" })
                .Concat(Directory.GetFiles("ProjectSettings", "*.asset"))
                .SelectMany(p => File.Exists(p + ".meta") ? new [] { p, p + ".meta" } : new [] { p })
                .Where(File.Exists).Distinct().OrderBy(p => p);
            context.dependencyFiles = dependencies.Select(p => new GardenVerification.DependencyFile { path=p, sha256=GardenVerification.FileHash(p) }).ToArray();
            File.WriteAllText(Path.Combine(Output, "verification-context.json"), JsonUtility.ToJson(context, true));
            SessionState.SetBool(Pending, true);
            SessionState.SetFloat(DeadlineKey, (float)(EditorApplication.timeSinceStartup + 240));
            var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.EnterPlaymode();
        }
    }
}
