using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace DawnGarden.Editor
{
    public static class SmallWorldShowcaseBuilder
    {
        [Serializable] class Manifest {public string buildId,appPath,createdAt,assemblySha256;public bool success,recordingOnly=true;public string[] scenes;}
        public static string Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Exit Play Mode and resolve compilation errors first");
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));var folder=Path.Combine(root,"artifacts/showcase-game");Directory.CreateDirectory(folder);
            var source=JsonUtility.FromJson<SmallWorldV1Builder.BuildManifest>(File.ReadAllText(Path.Combine(root,"artifacts/game-v5/build-manifest.json")));if(!source.success)throw new InvalidOperationException("A verified gameplay build is required");
            string product=PlayerSettings.productName,appId=PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone);var path=Path.Combine(folder,"Mac/SmallWorldShowcase.app");Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                PlayerSettings.productName="Small World Showcase";PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"local.littleworld.showcase");
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=source.scenes,locationPathName=path,target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
                if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Recording build failed: "+result.summary.result);
                var assembly=Path.Combine(path,"Contents/Resources/Data/Managed/Assembly-CSharp.dll");string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(assembly))).Replace("-","").ToLowerInvariant();
                File.WriteAllText(Path.Combine(folder,"build-manifest.json"),JsonUtility.ToJson(new Manifest {buildId=source.buildId,appPath=path,createdAt=DateTime.UtcNow.ToString("o"),success=true,scenes=source.scenes,assemblySha256=digest},true));return path;
            }
            finally{PlayerSettings.productName=product;PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,appId);}
        }
    }
}
