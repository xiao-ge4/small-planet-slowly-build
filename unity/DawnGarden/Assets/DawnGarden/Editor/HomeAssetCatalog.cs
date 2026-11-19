using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGarden.Editor
{
    public static class HomeAssetCatalog
    {
        [Serializable] public class Dependency { public string path, sha256; }
        [Serializable] public class Entry
        {
            public string id, prefabPath, previewPath;
            public Vector3 size, center;
            public bool hasCollider, hasLod;
            public string[] materials;
            public Dependency[] dependencies;
        }
        [Serializable] public class Catalog { public int schemaVersion = 1; public Entry[] assets; }
        public static string Export(string directory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DawnGarden/Prefabs", "Assets/DawnGarden/PreparedHomes" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.EndsWith("/Pet.prefab") && !p.Contains("/WorktableAcceptance/")).OrderBy(p => p).ToArray();
            var active = SceneManager.GetActiveScene();
            var entries = paths.Select(p => Inspect(p, directory)).ToArray();
            if (active.IsValid()) SceneManager.SetActiveScene(active);
            File.WriteAllText(Path.Combine(directory, "index.json"), JsonUtility.ToJson(new Catalog { assets = entries }, true));
            return "Exported " + entries.Length + " reusable prefabs";
        }
        static Entry Inspect(string path, string directory)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null; Texture2D image = null;
            var previous = RenderTexture.active;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0) throw new InvalidDataException("No renderers: " + path);
                var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                var id = Path.GetFileNameWithoutExtension(path);
                var provenance=go.GetComponent<PreparedHomeAsset>();
                if(provenance!=null && provenance.runId!="3ea386b9e1f54c399406cc67a44c4430" && !string.IsNullOrEmpty(provenance.sourceSha256))
                    id += "-"+provenance.sourceSha256.Substring(0,10);
                if (id == "WateringCan") id = "watering-can"; if (id == "InteractiveChest") id = "hinged-chest";
                var cameraGo = new GameObject("Catalog Camera"); SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>(); camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.91f,.93f,.87f);
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z)) * 1.6f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                camera.transform.position = bounds.center + new Vector3(1, .8f, -1).normalized * Mathf.Max(5, bounds.size.magnitude * 2);
                camera.transform.LookAt(bounds.center);
                var lightGo = new GameObject("Catalog Light"); SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(40,-30,0);
                rt = new RenderTexture(384,384,24); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(384,384,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,384,384),0,0); image.Apply();
                var preview = Path.Combine(directory, id + ".png"); File.WriteAllBytes(preview,image.EncodeToPNG());
                return new Entry { id=id, prefabPath=path, previewPath=preview, size=bounds.size, center=bounds.center,
                    hasCollider=go.GetComponentsInChildren<Collider>(true).Length>0, hasLod=go.GetComponentInChildren<LODGroup>()!=null,
                    materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.name).Distinct().ToArray(),
                    dependencies=AssetDatabase.GetDependencies(path,true).Where(File.Exists).OrderBy(p=>p).Select(p=>new Dependency { path=p, sha256=Hash(p) }).ToArray() };
            }
            finally { RenderTexture.active=previous; if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);} if(image!=null)UnityEngine.Object.DestroyImmediate(image);EditorSceneManager.ClosePreviewScene(scene); }
        }
        static string Hash(string p) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant(); }
    }
}
