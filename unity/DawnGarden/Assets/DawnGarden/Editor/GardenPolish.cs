using System;
using System.IO;
using System.Linq;
using DawnGarden;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden.Editor
{
    public static class GardenPolish
    {
        const string Root = "Assets/DawnGarden";
        const string Folder = Root + "/Art/Polish";
        const string Prefabs = Root + "/Prefabs/Garden/";
        static GameObject Find(string name) => UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(o => o.scene == EditorSceneManager.GetActiveScene() && o.name == name);

        [MenuItem("Dawn Garden/Art/Polish Dawn Garden")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first");
            GardenBootstrap.Open();
            Directory.CreateDirectory(Folder);
            var world = Find("Garden World");
            if (world == null || Find("Formal Tree 1") == null) throw new InvalidOperationException("Apply formal art before polishing");
            var old = Find("Garden Polish Decorations");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var decorations = new GameObject("Garden Polish Decorations").transform;
            decorations.SetParent(world.transform, false);
            FixGround();
            Lighting();
            var monument = Find("Formal Travel Monument");
            if (monument != null) { var position = monument.transform.position; position.y = .02f; monument.transform.position = position; }
            SmallProps(decorations);
            PondAndGardenBed(decorations);
            Shore(decorations);
            var camera = Camera.main;
            var game = UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            var cutaway = camera.GetComponent<GardenTreeCutaway>();
            if (cutaway == null) cutaway = camera.gameObject.AddComponent<GardenTreeCutaway>();
            cutaway.target = game.actor;
            cutaway.foliage = Enumerable.Range(1, 5).Select(i => Find("Formal Tree " + i))
                .Where(t => t != null).SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).ToArray();
            game.orbit.yaw = 180; game.orbit.pitch = 39; game.orbit.distance = 15.8f;
            game.orbit.overview = true;
            camera.transform.position = new Vector3(0, .5f, 0) + Quaternion.Euler(39, 180, 0) * Vector3.back * 15.8f;
            camera.transform.LookAt(new Vector3(0,.5f,0));
            // Tree trunks changed during formal art assembly: rebake from the actual colliders.
            var surface = world.GetComponent<NavMeshSurface>();
            var nav = surface.navMeshData;
            surface.BuildNavMesh();
            if (nav != null && nav != surface.navMeshData)
            {
                var generated = surface.navMeshData;
                surface.RemoveData(); EditorUtility.CopySerialized(generated, nav);
                surface.navMeshData = nav; surface.AddData(); UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(nav);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Dawn Garden polish saved: ground UV, morning light, water, reusable props, garden borders, navigation and tree cutaway.");
        }
        static Material Lit(string name, Color color)
        {
            var path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0);
            mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
        }
        static void FixGround()
        {
            var island = Find("Walkable Island");
            var source = island.GetComponent<MeshCollider>().sharedMesh;
            // Duplicate vertices per submesh to keep the flat meadow normal independent of the cliff.
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var indices = new int[source.subMeshCount][];
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var old = source.GetTriangles(sub); indices[sub] = new int[old.Length];
                for (var i = 0; i < old.Length; i++)
                {
                    var v = source.vertices[old[i]]; indices[sub][i] = vertices.Count;
                    vertices.Add(v); uv.Add(new Vector2(v.x / 3.1f, v.z / 3.1f));
                }
            }
            var mesh = new Mesh { name = "Meadow visual mesh with planar UV" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = indices.Length;
            for (var i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var path = Folder + "/IslandVisual.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            island.GetComponent<MeshFilter>().sharedMesh = saved;
            var meadow = Lit("Soft Meadow", new Color(.80f,.90f,.76f));
            meadow.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Garden/Meadow.png"));
            var cliff = Lit("Warm Limestone Cliff", new Color(.74f,.69f,.55f));
            island.GetComponent<Renderer>().sharedMaterials = new[] { meadow, cliff };
        }
        static void Lighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.63f,.75f,.85f);
            RenderSettings.ambientEquatorColor = new Color(.53f,.61f,.53f);
            RenderSettings.ambientGroundColor = new Color(.34f,.39f,.33f);
            RenderSettings.fog = false;
            var sun = Find("Morning Sun").GetComponent<Light>();
            sun.intensity = 1.15f; sun.color = new Color(1,.95f,.85f);
            sun.transform.rotation = Quaternion.Euler(48,-38,0);
            sun.shadowStrength = .72f; sun.shadowBias = .04f; sun.shadowNormalBias = .22f;
            sun.shadows = LightShadows.Soft;
            var profilePath = Folder + "/MorningVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            var tonemap = Get<Tonemapping>(profile); tonemap.mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>(profile); color.postExposure.Override(.15f); color.contrast.Override(-7); color.saturation.Override(-7);
            var bloom = Get<Bloom>(profile); bloom.intensity.Override(.09f); bloom.threshold.Override(1.2f); bloom.scatter.Override(.55f);
            var volumeObject = Find("Garden Morning Volume") ?? new GameObject("Garden Morning Volume");
            var volume = volumeObject.GetComponent<Volume>() ?? volumeObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
            var camera = Camera.main;
            camera.allowHDR = true; camera.backgroundColor = new Color(.51f,.72f,.80f);
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            WaterMaterial("Ocean", "Ocean Water", new Color(.12f,.39f,.49f), new Color(.31f,.64f,.70f), .9f);
            WaterMaterial("Pond Water", "Pond Water", new Color(.12f,.42f,.46f), new Color(.36f,.70f,.66f), 3);
        }
        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            p.components.RemoveAll(c => c == null);
            if (!p.TryGet<T>(out var component) || component == null)
            {
                component = p.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, p);
            }
            EditorUtility.SetDirty(component); EditorUtility.SetDirty(p); return component;
        }
        static void WaterMaterial(string objectName, string name, Color deep, Color light, float scale)
        {
            var path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Dawn Garden/Water")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", deep); mat.SetColor("_ShallowColor", light); mat.SetFloat("_WaveScale", scale); EditorUtility.SetDirty(mat);
            Find(objectName).GetComponent<Renderer>().sharedMaterial = mat;
        }
        static GameObject Prop(string id, Vector3 position, Vector3 scale, float heading, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + id + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing garden prop: " + id);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Polished " + id; go.transform.position = position; go.transform.localScale = Vector3.Scale(go.transform.localScale, scale);
            go.transform.rotation = Quaternion.Euler(0, heading, 0) * go.transform.rotation;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = id == "meadow-flower" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return go;
        }
        static void SmallProps(Transform parent)
        {
            foreach (var stone in ObjectsNamed("Stepping Stone"))
            {
                stone.GetComponent<Renderer>().enabled = false;
                Prop("stepping-stone", stone.transform.position, new Vector3(.95f,1,.95f), stone.transform.position.z * 75, parent);
            }
            var stump = Find("Tool Stump"); stump.GetComponent<Renderer>().enabled = false;
            Prop("tool-stump", stump.transform.position - Vector3.up*.25f, Vector3.one, 20, parent);
            foreach (var flower in ObjectsNamed("Flower").Concat(ObjectsNamed("Flower Stem"))) flower.GetComponent<Renderer>().enabled = false;
            var random = new System.Random(28);
            // Keep large flowers at the edge and leave approved interaction approaches clear.
            for (var i = 0; i < 68; i++)
            {
                float a = i * 2.39996f, r = 3.8f + (float)random.NextDouble()*.6f;
                var position = new Vector3(Mathf.Cos(a)*r*1.12f, .015f, Mathf.Sin(a)*r*.86f);
                if (position.x > 1.1f && position.z > .4f) continue;
                float s = .65f + (float)random.NextDouble()*.55f;
                Prop("meadow-flower", position, Vector3.one*s, i*37, parent);
            }
        }
        static GameObject[] ObjectsNamed(string name) => UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(o => o.scene == EditorSceneManager.GetActiveScene() && o.name == name).ToArray();
        static void Pebble(string name, Vector3 position, Vector3 scale, Material material, Transform parent, float rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name;
            go.transform.SetParent(parent, false); go.transform.position = position;
            go.transform.localScale = Vector3.Scale(go.transform.localScale, scale); go.transform.rotation = Quaternion.Euler(0,rotation,8);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static void PondAndGardenBed(Transform parent)
        {
            var stone = Lit("Ivory Border Stone", new Color(.73f,.74f,.61f));
            var moss = Lit("Soft Green Moss", new Color(.35f,.51f,.24f));
            foreach (var p in ObjectsNamed("Pond Pebble")) p.GetComponent<Renderer>().enabled = false;
            var bank = Find("Pond Bank"); bank.GetComponent<Renderer>().sharedMaterial = moss;
            for (var i = 0; i < 22; i++)
            {
                float a = i * Mathf.PI*2/22;
                Pebble("Pond edge stone", new Vector3(2.7f+Mathf.Cos(a)*1.34f,.045f,1.65f+Mathf.Sin(a)*1.06f),
                    new Vector3(.29f,.17f,.24f), stone,parent,i*29);
            }
            var plant = Find("Waterable Seedling").transform.position;
            for (var i = 0; i < 14; i++)
            {
                float a = i*Mathf.PI*2/14;
                Pebble("Seed bed border",plant+new Vector3(Mathf.Cos(a)*.62f,.07f,Mathf.Sin(a)*.57f),new Vector3(.22f,.16f,.19f),stone,parent,i*21);
            }
            var soil = Find("Seed Bed").GetComponent<Renderer>(); soil.sharedMaterial = Lit("Garden Soil", new Color(.35f,.24f,.14f));
        }
        static void Shore(Transform parent)
        {
            var sand = Lit("Shore Limestone",new Color(.67f,.64f,.48f));
            var moss = Lit("Shore Moss",new Color(.36f,.48f,.23f));
            for (var i=0;i<66;i++)
            {
                float a=i*Mathf.PI*2/66;
                float r=1+.035f*Mathf.Sin(3*a+.3f)+.025f*Mathf.Cos(7*a);
                Pebble("Shore rim stone",new Vector3(Mathf.Cos(a)*6.01f*r,-.08f,Mathf.Sin(a)*5.03f*r),
                    new Vector3(.49f,.25f,.38f),i%6==0?moss:sand,parent,i*23);
            }
        }
        public static void Validate()
        {
            var island = Find("Walkable Island");
            var visual = island.GetComponent<MeshFilter>().sharedMesh;
            var collider = island.GetComponent<MeshCollider>().sharedMesh;
            var report = new {
                utc=DateTime.UtcNow.ToString("O"), groundUv=visual.uv.Length == visual.vertexCount,
                separateVisualAndPhysics = visual != collider, uvTilesAcross=12.3f/3.1f,
                formalTrees=Enumerable.Range(1,5).Count(i=>Find("Formal Tree "+i)!=null),
                navigationTriangles=UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length/3,
                materialErrors=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials)
                    .Count(m=>m==null || m.shader==null || !m.shader.isSupported),
                postProcessing=Camera.main.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing,
                cutaway=Camera.main.GetComponent<GardenTreeCutaway>()!=null
            };
            File.WriteAllText(Path.Combine(GardenBootstrap.Output,"garden-polish-structure.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        }
    }
}
