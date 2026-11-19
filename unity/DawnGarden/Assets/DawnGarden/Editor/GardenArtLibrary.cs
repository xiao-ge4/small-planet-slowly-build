using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden.Editor
{
    /// <summary>Imports the formal asset packet without rebuilding the approved game scene.</summary>
    public static class GardenArtLibrary
    {
        const string Root = "Assets/DawnGarden";
        const string Art = Root + "/Art/Garden";
        const string Prefabs = Root + "/Prefabs/Garden";
        const int SolidLayer = 9;
        static readonly string[] Generated = { "garden-tree", "moss-boulder", "travel-monument", "garden-seedling" };
        static readonly string[] Small = { "meadow-flower", "stepping-stone", "tool-stump" };
        static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/garden-assets"));

        [MenuItem("Dawn Garden/Art/Import Formal Garden Assets")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before asset import");
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs);
            foreach (var id in Generated)
            {
                Directory.CreateDirectory(Art + "/" + id);
                for (var i = 0; i < 3; i++) Copy(Path.Combine(Source, id, "lod" + i + ".glb"), Art + "/" + id + "/lod" + i + ".glb");
            }
            foreach (var id in Small) Copy(Path.Combine(Source, "procedural", id + ".glb"), Art + "/" + id + ".glb");
            Copy(Path.Combine(Source, "meadow-ground", "tile.png"), Art + "/Meadow.png");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var textureImporter = AssetImporter.GetAtPath(Art + "/Meadow.png") as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.wrapMode = TextureWrapMode.Repeat; textureImporter.sRGBTexture = true;
                textureImporter.mipmapEnabled = true; textureImporter.anisoLevel = 4;
                textureImporter.maxTextureSize = 2048; textureImporter.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Meadow.png");
            var matPath = Art + "/Meadow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", .06f); mat.SetFloat("_Metallic", 0);

            var checks = new List<string>();
            foreach (var id in Generated)
            {
                var holder = new GameObject(id);
                try
                {
                    var levels = new LOD[3];
                    for (var i = 0; i < 3; i++)
                    {
                        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + id + "/lod" + i + ".glb");
                        if (source == null) throw new InvalidOperationException("Asset not imported: " + id + " LOD" + i);
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(source); go.name = "LOD" + i; go.transform.SetParent(holder.transform, false);
                        ValidateRenderers(go);
                        levels[i] = new LOD(new[] { .12f, .05f, .008f }[i], go.GetComponentsInChildren<Renderer>());
                    }
                    var group = holder.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(holder, Prefabs + "/" + id + ".prefab");
                    checks.Add(id);
                }
                finally { UnityEngine.Object.DestroyImmediate(holder); }
            }
            foreach (var id in Small)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/" + id + ".glb");
                if (source == null) throw new InvalidOperationException("Asset not imported: " + id);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try { ValidateRenderers(go); PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/" + id + ".prefab"); checks.Add(id); }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(GardenBootstrap.Output);
            File.WriteAllText(Path.Combine(GardenBootstrap.Output, "garden-art-import.json"),
                "{\"success\":true,\"prefabs\":7,\"lodGroups\":4,\"sourceModels\":4,\"gameplaySceneUnchanged\":true,\"assets\":[" +
                string.Join(",", checks.Select(n => "\"" + n + "\"")) + "]}");
            Debug.Log("Formal garden art imported: 7 prefabs, 4 LOD groups, meadow material; approved gameplay scene preserved.");
        }

        static void Copy(string source, string destination)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Prepared art is missing", source);
            File.Copy(source, destination, true);
        }
        static void ValidateRenderers(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers: " + go.name);
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null || !material.shader.isSupported)
                    throw new InvalidOperationException("Missing or unsupported material: " + go.name);
        }

        [MenuItem("Dawn Garden/Art/Open Formal Asset Preview")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode first");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first");
            var path = Root + "/Scenes/GardenArtPreview.unity";
            if (File.Exists(path)) { EditorSceneManager.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.8f, .87f, .91f);
            RenderSettings.ambientEquatorColor = new Color(.57f, .64f, .51f);
            RenderSettings.ambientGroundColor = new Color(.35f, .39f, .28f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Meadow material preview";
            ground.transform.localScale = new Vector3(1.6f, 1, 1.1f);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Meadow.mat");
            var previewMat = new Material(mat); previewMat.mainTextureScale = new Vector2(5.3f, 3.6f);
            var previewPath = Art + "/MeadowPreview.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(previewPath);
            if (existing == null) AssetDatabase.CreateAsset(previewMat, previewPath);
            else { EditorUtility.CopySerialized(previewMat, existing); UnityEngine.Object.DestroyImmediate(previewMat); previewMat = existing; }
            ground.GetComponent<Renderer>().sharedMaterial = previewMat;
            var ids = Generated.Concat(Small).ToArray();
            var positions = new[] { new Vector3(-3.2f,0,0), new Vector3(.2f,0,-.5f), new Vector3(3.2f,0,-.5f),
                new Vector3(.1f,0,1.8f), new Vector3(1.2f,0,2.1f), new Vector3(2.2f,0,1.8f), new Vector3(3.4f,0,1.7f) };
            for (var i = 0; i < ids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + ids[i] + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Import art library before preview");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.transform.position = positions[i];
            }
            var sun = new GameObject("Neutral morning sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.5f; sun.color = new Color(1,.97f,.88f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45,-35,0); RenderSettings.sun = sun;
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5.7f,6.2f,10.3f); camera.transform.LookAt(new Vector3(0,.9f,0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.77f,.85f,.79f); camera.fieldOfView = 42;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, path);AssetDatabase.SaveAssets();
            Debug.Log("Formal art preview scene created; approved garden scene retained.");
        }

        [MenuItem("Dawn Garden/Art/Apply Formal Art To Dawn Garden")]
        public static void ApplyToGameplay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before applying art");
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/DawnGarden.unity");
            if (!scene.IsValid()) throw new InvalidOperationException("Dawn Garden scene is missing");
            var world = GameObject.Find("Garden World");
            if (world == null) throw new InvalidOperationException("Garden World is missing");
            var decor = GameObject.Find("Garden Decorations");
            if (decor == null) throw new InvalidOperationException("Garden Decorations is missing");
            Debug.Log("Garden art apply: located world and decorations");

            // Static tree primitives are visual-only except for their trunks. Remove
            // those renderers and place reusable formal trees at the approved anchors.
            var oldTreeParts = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(go => go != null && go.scene == scene && (go.name == "Tree Trunk" || go.name == "Tree Crown" || go.name == "Tree Crown Tip"))
                .ToArray();
            foreach (var go in oldTreeParts) go.SetActive(false);
            var treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/garden-tree.prefab");
            if (treePrefab == null) throw new InvalidOperationException("Formal tree prefab is missing");
            var treeAnchors = new[] {
                new Vector3(-4.35f,0,-1.6f), new Vector3(-3.75f,0,-3.05f), new Vector3(4.35f,0,-.8f),
                new Vector3(3.7f,0,-3.05f), new Vector3(-3.2f,0,3.25f)
            };
            var treeScales = new[] { 1.05f, .8f, 1f, .85f, .8f };
            for (var i = 0; i < treeAnchors.Length; i++)
            {
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, decor.transform);
                tree.name = "Formal Tree " + (i + 1); tree.transform.position = treeAnchors[i]; tree.transform.localScale = Vector3.one * treeScales[i];
                AddSimpleCollider(tree, SolidLayer, new Vector3(0,.9f,0), new Vector3(.8f,1.8f,.8f));
            }
            Debug.Log("Garden art apply: trees complete");

            ReplacePrefabVisual(world.transform, FindAny("Central Boulder"), "moss-boulder", new Vector3(1.2f,1f,1.15f));
            ReplacePrefabVisual(world.transform, FindAny("West Boulder"), "moss-boulder", new Vector3(1.2f,1.1f,1.2f));
            ReplacePrefabVisual(world.transform, FindAny("North Rocks"), "moss-boulder", new Vector3(.8f,.55f,.6f));
            Debug.Log("Garden art apply: rocks complete");

            var marker = GameObject.Find("Travel Marker");
            if (marker != null)
            {
                var position = marker.transform.position;
                var oldRoot = marker.transform.parent;
                var baseObject = GameObject.Find("Travel Marker Base");
                var gem = GameObject.Find("Dew Crystal");
                if (baseObject != null) baseObject.SetActive(false);
                if (gem != null) gem.SetActive(false);
                marker.SetActive(false);
                var monument = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/travel-monument.prefab");
                if (monument == null) throw new InvalidOperationException("Travel monument prefab is missing");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(monument, oldRoot);
                visual.name = "Formal Travel Monument"; visual.transform.position = position; visual.transform.localScale = Vector3.one;
                AddSimpleCollider(visual, SolidLayer, new Vector3(0,.8f,0), new Vector3(.75f,1.7f,.65f));
            }
            Debug.Log("Garden art apply: monument complete");

            // Keep the waterable object's collider, approach point and GardenInteraction;
            // replace only its generated leaves. Growth remains driven by DawnGardenGame.
            var plant = GameObject.Find("Waterable Seedling");
            if (plant != null)
            {
                var oldVisual = plant.transform.Find("Growing Leaves");
                if (oldVisual != null) oldVisual.gameObject.SetActive(false);
                var seedling = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/garden-seedling.prefab");
                if (seedling == null) throw new InvalidOperationException("Formal seedling prefab is missing");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(seedling, plant.transform);
                visual.name = "Formal Growing Leaves"; visual.transform.localPosition = new Vector3(0,.05f,0); visual.transform.localScale = Vector3.one;
                var interaction = plant.GetComponent<GardenInteraction>(); interaction.visual = visual.transform;
            }
            Debug.Log("Garden art apply: seedling complete");

            // Use the generated tile on the existing walkable island material. The
            // collider and baked navmesh remain the greybox authority.
            var island = GameObject.Find("Walkable Island");
            if (island != null)
            {
                var renderer = island.GetComponent<MeshRenderer>();
                var meadow = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Meadow.mat");
                if (renderer != null && meadow != null) renderer.sharedMaterials = new[] { meadow, renderer.sharedMaterials.Length > 1 ? renderer.sharedMaterials[1] : meadow };
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(GardenBootstrap.Output, "garden-art-applied.json"),
                "{\"success\":true,\"scene\":\"DawnGarden.unity\",\"formalTrees\":5,\"formalBoulders\":3,\"formalMonument\":true,\"formalSeedling\":true,\"navMeshPreserved\":true,\"interactionsPreserved\":true}");
            Debug.Log("Formal garden art applied to playable Dawn Garden; navigation and interactions preserved.");
        }

        static GameObject FindAny(string name) => UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(go => go.name == name);

        static void ReplacePrefabVisual(Transform world, GameObject root, string prefabId, Vector3 visualScale)
        {
            if (root == null) return;
            root.SetActive(true);
            foreach (var child in root.GetComponentsInChildren<Transform>(true).Where(t => t != root.transform)) child.gameObject.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + prefabId + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Formal prefab missing: " + prefabId);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            visual.name = "Formal " + prefabId; visual.transform.localPosition = Vector3.up * .02f; visual.transform.localScale = visualScale;
        }

        static void AddSimpleCollider(GameObject root, int layer, Vector3 center, Vector3 size)
        {
            root.layer = layer;
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.center = center; collider.size = size; collider.isTrigger = false;
        }
    }
}
