using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden.Editor
{
    public static class HomeLayoutEditorBridge
    {
        [Serializable] public class Item
        {
            public string instanceId, assetId, sourceAssetRef, assetHash, tint;
            public int assetVersion;
            public Vector3 localPosition, localEulerAngles, localScale;
            public bool createCollider;
            public Vector3 collisionCenter, collisionSize;
        }
        [Serializable] public class Request
        {
            public string runId, assemblyVersion, sourceScenePath, outputScenePath, outputDirectory, theme, homeId;
            public int revision;
            public Item[] instances;
            public string[] previousInstanceIds;
        }
        [Serializable] public class Checks { public bool westConnectionClear=true, insidePlot=true, navigationConnected=true; }
        [Serializable] public class Layout
        {
            public int schemaVersion=1;
            public string runId, revision, assemblyVersion, scenePath, sceneSha256, plotId="first-expansion";
            public Item[] instances;
            public Checks checks=new Checks();
        }
        public static string Apply(string requestPath)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("Exit Play Mode and wait for compilation.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene first.");
            var req=JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if(!req.sourceScenePath.StartsWith("Assets/DawnGarden/PreparedHomes/") || !req.outputScenePath.StartsWith("Assets/DawnGarden/PreparedHomes/") || req.sourceScenePath==req.outputScenePath)
                throw new InvalidDataException("Only new PreparedHomes versions may be written.");
            Directory.CreateDirectory(Path.GetDirectoryName(req.outputScenePath));Directory.CreateDirectory(req.outputDirectory);
            var sourceHash=GardenVerification.FileHash(req.sourceScenePath);
            var scene=EditorSceneManager.OpenScene(req.sourceScenePath);
            if(!EditorSceneManager.SaveScene(scene,req.outputScenePath))throw new IOException("Could not create isolated scene version.");
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            if(req.homeId=="dawn-garden-starter-v1" || req.homeId=="ember-camp-starter-v1")game.homeId=req.homeId;
            var expansion=game.expansion; var land=expansion.land;
            var root=land.Find("Editable Home Layout");
            if(root!=null)UnityEngine.Object.DestroyImmediate(root.gameObject);
            // One-time migration of the previously authored prototype instance.
            foreach(var old in land.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Mossy Stone Bench (layout)").ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            root=new GameObject("Editable Home Layout").transform;root.SetParent(land,false);
            foreach(var item in req.instances)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(item.sourceAssetRef);
                if(prefab==null)throw new InvalidDataException("Catalog prefab missing: "+item.assetId);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.name="Layout: "+item.instanceId;go.transform.SetParent(root,false);
                go.transform.localPosition=item.localPosition;go.transform.localEulerAngles=item.localEulerAngles;go.transform.localScale=item.localScale;
                var mark=go.AddComponent<HomeLayoutInstance>();mark.instanceId=item.instanceId;mark.assetId=item.assetId;mark.assetVersion=item.assetVersion;
                var bounds=BoundsOf(go);go.transform.position+=Vector3.up*(land.position.y+item.localPosition.y-bounds.min.y);
                if(item.createCollider && go.GetComponentsInChildren<Collider>(true).Length==0)
                {
                    go.layer=LayerMask.NameToLayer("GardenSolid");
                    var box=go.AddComponent<BoxCollider>();box.center=item.collisionCenter;box.size=item.collisionSize;
                }
                if(item.tint!="#ffffff" && ColorUtility.TryParseHtmlString(item.tint,out var tint))
                {
                    var i=0;
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats=renderer.sharedMaterials;
                        for(var m=0;m<mats.Length;m++)if(mats[m]!=null)
                        {
                            var material=new Material(mats[m]); if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",tint);
                            var path=Path.GetDirectoryName(req.outputScenePath)+"/Tint-"+item.instanceId+"-"+(i++)+".mat";
                            AssetDatabase.CreateAsset(material,path);mats[m]=material;
                        }
                        renderer.sharedMaterials=mats;
                    }
                }
                var world=BoundsOf(go);var center=land.InverseTransformPoint(world.center);
                var min=land.InverseTransformPoint(world.min);var max=land.InverseTransformPoint(world.max);
                if(min.x < -6.3f || max.x > 6.3f || min.z < -5.3f || max.z > 5.3f)throw new InvalidDataException("Asset outside plot: "+item.instanceId);
                if(min.x<-.5f && min.z<.8f && max.z>-.8f)throw new InvalidDataException("Connection corridor blocked: "+item.instanceId);
            }
            // Navigation is a new asset per layout version; old releases remain intact.
            land.gameObject.SetActive(true);expansion.landCollider.enabled=true;
            foreach(var seam in expansion.seamStones)if(seam!=null)seam.SetActive(false);
            Physics.SyncTransforms();var surface=expansion.surface;surface.BuildNavMesh();var nav=surface.navMeshData;
            var navPath=Path.GetDirectoryName(req.outputScenePath)+"/LayoutNavigation.asset";surface.RemoveData();AssetDatabase.CreateAsset(nav,navPath);
            expansion.expandedNavigation=nav;surface.navMeshData=nav;surface.AddData();
            foreach(var target in new[]{expansion.finalPosition+Vector3.right*.5f,expansion.finalPosition+Vector3.left*4})
            {
                var route=new NavMeshPath();
                if(!NavMesh.CalculatePath(game.spawn.position,target,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)throw new InvalidDataException("New land or west route is unreachable.");
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var cameraGo=new GameObject("Layout Capture Camera");var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=7.2f;
            camera.transform.position=expansion.finalPosition+new Vector3(.3f,17,-13.5f);camera.transform.LookAt(expansion.finalPosition);camera.farClipPlane=180;
            Capture(camera,Path.Combine(req.outputDirectory,"preview.png"));UnityEngine.Object.DestroyImmediate(cameraGo);
            if(GardenVerification.FileHash(req.sourceScenePath)!=sourceHash)throw new IOException("Source scene changed.");
            File.WriteAllText(Path.Combine(req.outputDirectory,"layout.json"),JsonUtility.ToJson(new Layout {runId=req.runId,revision=req.revision.ToString(),assemblyVersion=req.assemblyVersion,scenePath=req.outputScenePath,sceneSha256=GardenVerification.FileHash(req.outputScenePath),instances=req.instances},true));
            return "Layout saved: "+req.outputScenePath;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new InvalidDataException("No renderer");
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static void Capture(Camera camera,string path)
        {
            var previous=RenderTexture.active;var rt=new RenderTexture(1280,960,24);var image=new Texture2D(1280,960,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
