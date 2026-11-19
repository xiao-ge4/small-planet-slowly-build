using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DawnGarden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace DawnGarden.Editor
{
    public static class HomeAssetPreparation
    {
        [Serializable] public class Request
        {
            public string assetId, runId, sourceSha256, designSha256, providerTaskId, preparationSha256;
            public int revision;
            public string modelDirectory, outputDirectory, prefabPath, previewScenePath;
            public float height;
            public Vector3 colliderSize, colliderCenter;
        }
        [Serializable] public class Check { public string name; public bool passed; }
        [Serializable] public class Report
        {
            public bool passed, activeScenePreserved;
            public string utc, assetId, sourceSha256, preparationSha256, prefabPath, previewScenePath, activeSceneBefore, activeSceneAfter;
            public Vector3 boundsMin, boundsMax, dimensions;
            public int[] lodTriangles;
            public Check[] checks;
            public string[] previews;
        }
        public static void Build(string requestPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before preparing assets");
            var req = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if (!System.Text.RegularExpressions.Regex.IsMatch(req.assetId, "^[a-z][a-z0-9-]{1,49}$")) throw new InvalidOperationException("Invalid asset ID");
            if (!req.prefabPath.StartsWith("Assets/DawnGarden/PreparedHomes/") || !req.previewScenePath.StartsWith("Assets/DawnGarden/PreparedHomes/")) throw new InvalidOperationException("Output outside prepared asset area");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var previous = SceneManager.GetActiveScene();
            var previousPath = previous.path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var reviewMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
            EditorSceneManager.SetSceneCullingMask(scene, reviewMask);
            var checks = new List<Check>();
            var images = new List<string>();
            var report = new Report { assetId=req.assetId, sourceSha256=req.sourceSha256, preparationSha256=req.preparationSha256, utc=DateTime.UtcNow.ToString("o"), activeSceneBefore=previousPath, prefabPath=req.prefabPath, previewScenePath=req.previewScenePath };
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject(req.assetId);
                root.layer = 9;
                var lods = new LOD[3];
                report.lodTriangles = new int[3];
                var thresholds = new[] { .18f, .07f, .006f };
                for (var i=0;i<3;i++)
                {
                    var path = req.modelDirectory + "/lod" + i + ".glb";
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) throw new InvalidOperationException("Missing imported model: "+path);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                    go.name = "LOD"+i; go.transform.SetParent(root.transform, false);
                    foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=9;
                    var renderers = go.GetComponentsInChildren<Renderer>();
                    if (renderers.Length==0) throw new InvalidOperationException("No model renderers");
                    checks.Add(new Check { name="LOD"+i+" has textured materials", passed=renderers.All(r=>r.sharedMaterials.Length>0 && r.sharedMaterials.All(m=>m!=null && m.shader!=null && m.shader.isSupported && m.mainTexture!=null)) });
                    report.lodTriangles[i] = go.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3);
                    lods[i] = new LOD(thresholds[i], renderers);
                }
                var group=root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                var collider=root.AddComponent<BoxCollider>(); collider.center=req.colliderCenter; collider.size=req.colliderSize; collider.isTrigger=false;
                var meta=root.AddComponent<PreparedHomeAsset>();
                meta.assetId=req.assetId; meta.runId=req.runId; meta.sourceRevision=req.revision; meta.sourceSha256=req.sourceSha256; meta.designSha256=req.designSha256; meta.providerTaskId=req.providerTaskId; meta.preparationSha256=req.preparationSha256;
                var bounds=Bounds(lods[0].renderers);
                report.boundsMin=bounds.min; report.boundsMax=bounds.max; report.dimensions=bounds.size;
                checks.Add(new Check { name="Height and bottom-centred pivot", passed=Mathf.Abs(bounds.size.y-req.height)<.001f && Mathf.Abs(bounds.min.y)<.001f && Mathf.Abs(bounds.center.x)<.001f && Mathf.Abs(bounds.center.z)<.001f });
                checks.Add(new Check { name="LOD triangle counts strictly decrease", passed=report.lodTriangles[0]>report.lodTriangles[1] && report.lodTriangles[1]>report.lodTriangles[2] && report.lodTriangles[2]>0 });
                Physics.SyncTransforms();
                var ray=new Ray(new Vector3(0,req.height*.5f,-2),Vector3.forward);
                checks.Add(new Check { name="Solid collision ray hits asset, clear side misses", passed=collider.Raycast(ray,out _,4) && !collider.Raycast(new Ray(new Vector3(req.colliderSize.x,req.height*.5f,-2),Vector3.forward),out _,4) });
                var probe=new GameObject("Collision probe"); var capsule=probe.AddComponent<CapsuleCollider>(); capsule.height=1.7f;capsule.radius=.24f;capsule.center=new Vector3(0,.85f,0);
                var blocked=Physics.ComputePenetration(capsule,Vector3.zero,Quaternion.identity,collider,root.transform.position,root.transform.rotation,out _,out _);
                var clear=!Physics.ComputePenetration(capsule,new Vector3(req.colliderSize.x+1,0,0),Quaternion.identity,collider,root.transform.position,root.transform.rotation,out _,out _);
                checks.Add(new Check { name="Pet-sized capsule blocked by prop, side passage clear", passed=blocked&&clear });
                UnityEngine.Object.DestroyImmediate(probe);
                meta.validatedForAssembly=checks.All(c=>c.passed);
                Directory.CreateDirectory(Path.GetDirectoryName(req.prefabPath));
                PrefabUtility.SaveAsPrefabAsset(root,req.prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                // Reload from disk instead of validating only the unsaved object.
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(req.prefabPath);
                root=(GameObject)PrefabUtility.InstantiatePrefab(saved,scene);
                group=root.GetComponent<LODGroup>();
                checks.Add(new Check { name="Reloaded prefab retains LOD, collider and provenance", passed=group.GetLODs().Length==3 && root.GetComponent<BoxCollider>()!=null && root.GetComponent<PreparedHomeAsset>().sourceSha256==req.sourceSha256 });
                RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.8f,.83f,.78f); RenderSettings.ambientEquatorColor=new Color(.6f,.62f,.56f); RenderSettings.ambientGroundColor=new Color(.4f,.43f,.38f);RenderSettings.fog=false;
                var sun=new GameObject("Review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.color=new Color(1,.97f,.9f);sun.transform.rotation=Quaternion.Euler(50,-25,0);RenderSettings.sun=sun;
                var camera=new GameObject("Asset review camera").AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=reviewMask;camera.cullingMask=1<<9;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.26f,.24f);camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.z)*.60f;camera.nearClipPlane=.05f;camera.farClipPlane=20;
                Directory.CreateDirectory(req.outputDirectory);
                for(var lod=0;lod<3;lod++)
                {
                    group.ForceLOD(lod);
                    foreach(var view in new[]{"front","right","back"})
                    {
                        var offset=view=="front"?new Vector3(1.1f,.7f,-2.4f):view=="right"?new Vector3(2.6f,.6f,.1f):new Vector3(-1,.7f,2.5f);
                        camera.transform.position=bounds.center+offset;camera.transform.LookAt(bounds.center);
                        var file="unity-lod"+lod+"-"+view+".png";Capture(camera,Path.Combine(req.outputDirectory,file));images.Add(file);
                    }
                }
                group.ForceLOD(-1);camera.transform.position=bounds.center+new Vector3(1.1f,.7f,-2.4f);camera.transform.LookAt(bounds.center);
                Directory.CreateDirectory(Path.GetDirectoryName(req.previewScenePath));
                EditorSceneManager.SaveScene(scene,req.previewScenePath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
                report.activeSceneAfter=SceneManager.GetActiveScene().path;report.activeScenePreserved=report.activeSceneAfter==previousPath;
            }
            checks.Add(new Check { name="Previous active scene preserved",passed=report.activeScenePreserved });
            report.checks=checks.ToArray();report.previews=images.ToArray();report.passed=checks.All(c=>c.passed);
            File.WriteAllText(Path.Combine(req.outputDirectory,"unity-report.json"),JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Prepared asset validation failed; inspect unity-report.json");
        }
        static Bounds Bounds(Renderer[] renderers) { var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b; }
        static void Capture(Camera camera,string path)
        {
            var previous=RenderTexture.active;var rt=new RenderTexture(960,720,24);var image=new Texture2D(960,720,TextureFormat.RGB24,false);
            try { camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { camera.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
