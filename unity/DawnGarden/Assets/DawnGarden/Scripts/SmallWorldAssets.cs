using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace DawnGarden
{
    [Serializable] public class SmallWorldAsset
    {
        public string id,name,hash,previewUrl,modelUrl,modelSha256;
        public int version;
        public GameObject prefab;
        public Vector3 size,center,collisionCenter,collisionSize;
        public bool createCollider,decorationOnly;
    }
    [Serializable] public class SmallWorldAssetList { public SmallWorldAsset[] assets; }
    [Serializable] public class SmallWorldItem
    {
        public string instanceId,assetId,giftId,assetHash,tint="#ffffff",sourceAssetRef;
        public int assetVersion;
        public Vector3 localPosition,localEulerAngles,localScale=Vector3.one;
    }
    [Serializable] public class SmallWorldLayout
    {
        public int version;
        public string runId;
        public string surfaceStyle="";
        public bool seamNatural=true;
        public bool seamSoftened;
        public SmallWorldItem[] instances=new SmallWorldItem[0];
    }
    public sealed class SmallWorldGltfLifetime : MonoBehaviour
    {
        public GltfImport import;
        void OnDestroy(){import?.Dispose();}
    }
    public static class SmallWorldAssetLoader
    {
        public static IEnumerator Instantiate(SmallWorldAsset asset,SmallWorldLibrary library,Transform parent,Action<GameObject> done,Action<string> fail)
        {
            var compiled=library.Find(asset.id,asset.hash);GameObject go;
            if(compiled?.prefab!=null)go=UnityEngine.Object.Instantiate(compiled.prefab,parent,false);
            else
            {
                if(string.IsNullOrEmpty(asset.modelUrl)||!asset.modelUrl.StartsWith("/api/home-creation/files/")){fail("这件物件尚未准备好，请先完成模型制作。");yield break;}
                var folder=Path.Combine(SmallWorldProfile.Root,"asset-cache");Directory.CreateDirectory(folder);
                var path=Path.Combine(folder,asset.modelSha256+".glb");
                if(!File.Exists(path)||Hash(path)!=asset.modelSha256)
                {
                    using(var request=UnityWebRequest.Get(SmallWorldApi.Base+asset.modelUrl))
                    {
                        request.timeout=60;yield return request.SendWebRequest();
                        if(request.result!=UnityWebRequest.Result.Success){fail("物件下载中断，原布局仍然保留。");yield break;}
                        var temp=path+".tmp";File.WriteAllBytes(temp,request.downloadHandler.data);
                        if(Hash(temp)!=asset.modelSha256){fail("物件校验没有通过，请重新下载。");yield break;}
                        if(File.Exists(path))File.Delete(path);File.Move(temp,path);
                    }
                }
                go=new GameObject(asset.name);go.transform.SetParent(parent,false);
                var gltf=new GltfImport();var lifetime=go.AddComponent<SmallWorldGltfLifetime>();lifetime.import=gltf;
#if UNITY_WEBGL && !UNITY_EDITOR
                // A file:// URL cannot address WebGL's virtual filesystem. Reuse the
                // verified cache bytes and keep HTTP as the base for embedded references.
                var load=gltf.LoadGltfBinary(File.ReadAllBytes(path),new Uri(SmallWorldApi.Base+asset.modelUrl));
                SmallWorldBrowserStorage.Flush();
#else
                var load=gltf.Load(new Uri(path).AbsoluteUri);
#endif
                while(!load.IsCompleted)yield return null;
                if(load.IsCanceled || load.IsFaulted || !load.Result){UnityEngine.Object.Destroy(go);fail("模型无法加载，原布局已保留。");yield break;}
                var instantiate=gltf.InstantiateMainSceneAsync(go.transform);while(!instantiate.IsCompleted)yield return null;
                if(instantiate.IsCanceled || instantiate.IsFaulted || !instantiate.Result){UnityEngine.Object.Destroy(go);fail("模型结构加载未完成。");yield break;}
            }
            go.name="Layout: "+asset.name;
            var rs=go.GetComponentsInChildren<Renderer>(true);
            if(rs.Length==0){UnityEngine.Object.Destroy(go);fail("模型没有可显示的结构。");yield break;}
            var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
            var noBlock=asset.decorationOnly||asset.id=="meadow-flower"||asset.id=="stepping-stone";
            if(!noBlock && go.GetComponentsInChildren<Collider>(true).Length==0)
            {
                go.layer=LayerMask.NameToLayer("GardenSolid");var box=go.AddComponent<BoxCollider>();
                box.size=asset.collisionSize.sqrMagnitude>.001f?asset.collisionSize:bounds.size;
                box.center=asset.collisionSize.sqrMagnitude>.001f?asset.collisionCenter:go.transform.InverseTransformPoint(bounds.center);
            }
            if(noBlock && go.GetComponentsInChildren<Collider>(true).Length==0)
            {var pick=go.AddComponent<BoxCollider>();pick.isTrigger=true;pick.size=asset.size;pick.center=asset.center;}
            done(go);
        }
        public static string Hash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        public static void Tint(GameObject go,string value)
        {
            if(value=="#ffffff"||!ColorUtility.TryParseHtmlString(value,out var color))return;
            var block=new MaterialPropertyBlock();
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared=r.sharedMaterials;
                for(int index=0;index<shared.Length;index++)
                {
                    var m=shared[index];if(m==null)continue;
                    var property=m.HasProperty("_BaseColor")?"_BaseColor":m.HasProperty("baseColorFactor")?"baseColorFactor":m.HasProperty("_Color")?"_Color":null;
                    if(property==null)continue;
                    // Preserve each submesh's overrides without renderer.materials,
                    // which allocates material copies on every placement preview.
                    block.Clear();r.GetPropertyBlock(block,index);
                    block.SetColor(property,color);r.SetPropertyBlock(block,index);
                }
            }
        }
    }
}
