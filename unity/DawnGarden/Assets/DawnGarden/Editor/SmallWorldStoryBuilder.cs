using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DawnGarden.Editor
{
    public static class SmallWorldStoryBuilder
    {
        [Serializable] public sealed class Manifest
        {
            public string buildId, sourceBuildId, appPath, createdAt, assemblySha256;
            public bool success, recordingOnly = true;
            public string[] scenes;
            public string agentMode = "preset-demo", controlMode = "scripted-real-gameplay";
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Exit Play Mode and resolve compilation errors first.");
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var source = JsonUtility.FromJson<SmallWorldV1Builder.BuildManifest>(
                File.ReadAllText(Path.Combine(root, "artifacts/game-v5/build-manifest.json")));
            if (!source.success || source.scenes == null || source.scenes.Length == 0)
                throw new InvalidOperationException("A verified game-v5 scene manifest is required.");
            var folder = Path.Combine(root, "artifacts/story-demo");
            var path = Path.Combine(folder, "Mac/SmallWorldStory.app");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var manifest = new Manifest { buildId = Guid.NewGuid().ToString("N"), sourceBuildId = source.buildId,
                appPath = path, createdAt = DateTime.UtcNow.ToString("o"), scenes = source.scenes };
            // Invalidate a previous manifest before replacing the executable.
            var manifestPath = Path.Combine(folder, "build-manifest.json");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            var product = PlayerSettings.productName;
            var identifier = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone);
            var http = PlayerSettings.insecureHttpOption;
            try
            {
                PlayerSettings.productName = "Small World Story";
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "local.littleworld.story");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = source.scenes,
                    locationPathName = path, target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
                if (result.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Story recording build failed: " + result.summary.result);
                var assembly = Path.Combine(path, "Contents/Resources/Data/Managed/Assembly-CSharp.dll");
                using (var sha = SHA256.Create())
                    manifest.assemblySha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(assembly))).Replace("-", "").ToLowerInvariant();
                manifest.success = true;
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                return path;
            }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, identifier);
                PlayerSettings.insecureHttpOption = http;
            }
        }

        public static string BuildMac() => Build();
    }
}
