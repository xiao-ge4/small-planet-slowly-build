using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DawnGarden.Editor
{
    public static class SmallWorldUiArtBuilder
    {
        public static string Build()
        {
            const string folder="Assets/DawnGarden/Resources/UIAssetThumbs";Directory.CreateDirectory(folder);int count=0;
            var assets=AssetDatabase.FindAssets("t:SmallWorldLibrary").Select(g=>AssetDatabase.LoadAssetAtPath<SmallWorldLibrary>(AssetDatabase.GUIDToAssetPath(g))).Where(l=>l!=null&&l.assets!=null).SelectMany(l=>l.assets).Where(a=>a.prefab!=null).GroupBy(a=>a.id).Select(g=>g.First());
            foreach(var asset in assets)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    var go=UnityEngine.Object.Instantiate(asset.prefab);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.Euler(0,25,0);preview.AddSingleGO(go);
                    var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                    var bounds=renderers[0].bounds;foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.92f,.92f,.83f,0);preview.camera.fieldOfView=32;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10000;
                    var distance=Mathf.Max(.3f,bounds.extents.magnitude*3.8f);preview.camera.transform.position=bounds.center+new Vector3(1,.75f,-1.6f).normalized*distance;preview.camera.transform.LookAt(bounds.center);
                    preview.lights[0].intensity=1.15f;preview.lights[0].transform.rotation=Quaternion.Euler(35,35,0);preview.lights[1].intensity=.65f;preview.ambientColor=new Color(.65f,.67f,.64f);
                    preview.BeginPreview(new Rect(0,0,160,160),GUIStyle.none);preview.Render(true);var rendered=preview.EndPreview();
                    var old=RenderTexture.active;var target=RenderTexture.GetTemporary(160,160,24);Graphics.Blit(rendered,target);RenderTexture.active=target;
                    var image=new Texture2D(160,160,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,160,160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,asset.id+".png"),image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);count++;
                }
                finally {preview.Cleanup();}
            }
            AssetDatabase.Refresh();return "UI model thumbnails: "+count;
        }
    }
}
