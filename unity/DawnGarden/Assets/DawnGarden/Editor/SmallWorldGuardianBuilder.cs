using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DawnGarden.Editor
{
    public static class SmallWorldGuardianBuilder
    {
        public static string BuildMac()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var source = Path.Combine(root, "artifacts/game-v5/build-manifest.json");
            var manifest = JsonUtility.FromJson<SmallWorldV1Builder.BuildManifest>(File.ReadAllText(source));
            var folder = Path.Combine(root, "artifacts/guardian-demo");
            var path = Path.Combine(folder, "Mac/SmallWorld.app");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previousHttp = PlayerSettings.insecureHttpOption;
            var previousVersion = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.companyName = "LittleWorld";
                PlayerSettings.productName = "Small World";
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "local.littleworld.smallworld");
                // The companion API is loopback HTTP in the native game.
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                PlayerSettings.bundleVersion = "0.10.0";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = manifest.scenes, locationPathName = path,
                    target = BuildTarget.StandaloneOSX, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Small World build failed: " + report.summary.result);
                manifest.appPath = path;
                // The scene list comes from v5, but this is a new executable.
                // Do not reuse v5's build identifier in later visual evidence.
                manifest.buildId = Guid.NewGuid().ToString("N");
                manifest.createdAt = DateTime.UtcNow.ToString("o");
                manifest.success = true;
                File.WriteAllText(Path.Combine(folder, "build-manifest.json"), JsonUtility.ToJson(manifest, true));
                return path;
            }
            finally
            {
                PlayerSettings.insecureHttpOption = previousHttp;
                PlayerSettings.bundleVersion = previousVersion;
            }
        }
    }
}
