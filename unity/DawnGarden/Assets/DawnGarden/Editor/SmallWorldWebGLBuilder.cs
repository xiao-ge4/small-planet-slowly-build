using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGarden.Editor
{
    public static class SmallWorldWebGLBuilder
    {
        [Serializable] public sealed class Manifest
        {
            public string buildId, sourceBuildId, createdAt, appPath;
            public bool success, recordingOnly;
            public string platform = "WebGL", apiMode = "same-origin";
            public string[] scenes;
        }

        [MenuItem("Small World/Build Submission WebGL")]
        public static void BuildWebGL() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Exit Play Mode and resolve compilation errors first.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install WebGL Build Support for Unity " + Application.unityVersion + " first.");

            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var source = JsonUtility.FromJson<SmallWorldV1Builder.BuildManifest>(
                File.ReadAllText(Path.Combine(root, "artifacts/game-v5/build-manifest.json")));
            if (!source.success || source.scenes == null || source.scenes.Length < 5
                || source.scenes[0] != "Assets/SmallWorldV1/SmallWorldBoot.unity"
                || source.scenes.Any(scene => !File.Exists(scene)))
                throw new InvalidOperationException("The complete prepared game-v5 scene manifest is required.");

            var output = Environment.GetEnvironmentVariable("SMALL_WORLD_WEBGL_OUTPUT");
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-buildOutput") output = args[i + 1];
            output = string.IsNullOrWhiteSpace(output)
                ? Path.Combine(root, "artifacts/submission/WebGL") : Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            var manifestPath = Path.Combine(Path.GetDirectoryName(output), "webgl-build-manifest.json");
            var manifest = new Manifest { buildId = Guid.NewGuid().ToString("N"), sourceBuildId = source.buildId,
                createdAt = DateTime.UtcNow.ToString("o"), appPath = output, scenes = source.scenes };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

            var product = PlayerSettings.productName;
            var width = PlayerSettings.defaultWebScreenWidth;
            var height = PlayerSettings.defaultWebScreenHeight;
            var http = PlayerSettings.insecureHttpOption;
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var threads = PlayerSettings.WebGL.threadsSupport;
            var initialMemory = PlayerSettings.WebGL.initialMemorySize;
            var maximumMemory = PlayerSettings.WebGL.maximumMemorySize;
            var memoryGrowth = PlayerSettings.WebGL.memoryGrowthMode;
            var template = PlayerSettings.WebGL.template;
            var stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            Action restoreOptimization = null;
            try
            {
                PlayerSettings.productName = "捡个星球慢慢造";
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                // Uncompressed output works with an ordinary HTTP server and avoids
                // the extra Brotli build step and Content-Encoding requirements.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.threadsSupport = false;
                PlayerSettings.WebGL.initialMemorySize = 256;
                PlayerSettings.WebGL.maximumMemorySize = 2048;
                PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
                PlayerSettings.WebGL.template = "APPLICATION:Default";
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
                restoreOptimization = TryUseBuildTimeOptimization();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = source.scenes,
                    locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.CompressWithLz4 });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Small World WebGL build failed: " + report.summary.result);
                manifest.success = true;
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                return output;
            }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.defaultWebScreenWidth = width;
                PlayerSettings.defaultWebScreenHeight = height;
                PlayerSettings.insecureHttpOption = http;
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.threadsSupport = threads;
                PlayerSettings.WebGL.initialMemorySize = initialMemory;
                PlayerSettings.WebGL.maximumMemorySize = maximumMemory;
                PlayerSettings.WebGL.memoryGrowthMode = memoryGrowth;
                PlayerSettings.WebGL.template = template;
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, stripping);
                restoreOptimization?.Invoke();
            }
        }

        static Action TryUseBuildTimeOptimization()
        {
            // A newly installed Web module may be buildable before its editor
            // extension assembly is loaded. Optimization must remain optional.
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("UnityEditor.WebGL.UserBuildSettings", false))
                    .FirstOrDefault(candidate => candidate != null);
                var property = type?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property == null || !property.CanRead || !property.CanWrite || !property.PropertyType.IsEnum)
                    return null;
                var previous = property.GetValue(null);
                property.SetValue(null, Enum.Parse(property.PropertyType, "BuildTimes"));
                return () =>
                {
                    try { property.SetValue(null, previous); }
                    catch (Exception error) { Debug.LogWarning("Could not restore optional Web code optimization: " + error.GetType().Name); }
                };
            }
            catch (Exception error)
            {
                Debug.LogWarning("Optional Web code optimization was skipped: " + error.GetType().Name);
                return null;
            }
        }
    }
}
