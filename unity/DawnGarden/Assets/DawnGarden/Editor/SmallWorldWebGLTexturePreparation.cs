using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace DawnGarden.Editor
{
    public static class SmallWorldWebGLTexturePreparation
    {
        const string ImporterName = "GLTFast.Editor.SmallPlanetWebGltfImporter";
        const string Pet = "Assets/DawnGarden/Art/Characters/Pet.glb";
        const string PetPrefab = "Assets/DawnGarden/Prefabs/Pet.prefab";

        [Serializable] public sealed class TextureInfo
        {
            public string name, format, graphicsFormat;
            public int width, height, mipmaps;
            public long runtimeBytes;
            public bool readable;
        }
        [Serializable] public sealed class Inspection
        {
            public string path, importer;
            public TextureInfo[] textures;
            public SubAssetInfo[] subAssets;
        }
        [Serializable] public sealed class SubAssetInfo { public string name, type, guid; public long localFileId; }
        [Serializable] public sealed class SettingSnapshot { public string name, json; public bool wasNull; }
        [Serializable] public sealed class PreparationReport
        {
            public Inspection before, after;
            public SettingSnapshot[] settingsBefore, settingsAfter;
            public bool localFileIdsPreserved, importSettingsPreserved, prefabMeshesValid = true, prefabMaterialsValid = true;
            public string prefabPath;
            public int prefabSkinnedRenderers;
        }

        public static string PreparePet() => PreparePilot();
        public static string PreparePilot() => Prepare(Pet);
        public static string InspectPet() => Inspect(Pet);

        public static string PrepareAll()
        {
            RequireWebGL();
            var paths = Directory.EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                .Select(path => "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/'))
                .OrderBy(path => path).ToArray();
            foreach (var path in paths) Prepare(path);
            return "Prepared Web texture compression for " + paths.Length + " glTF assets.";
        }

        public static string Prepare(string path)
        {
            RequireWebGL();
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path))
                throw new ArgumentException("An existing project glTF asset path is required.", nameof(path));
            var before = Capture(path);
            var settings = CaptureSettings(AssetImporter.GetAtPath(path));
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(ImporterName, false)).FirstOrDefault(candidate => candidate != null);
            if (type == null) throw new InvalidOperationException("Refresh the embedded glTFast package and resolve compilation errors first.");
            var setter = typeof(AssetDatabase).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "SetImporterOverride" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1);
            setter.MakeGenericMethod(type).Invoke(null, new object[] { path });
            var overriddenImporter = AssetImporter.GetAtPath(path);
            foreach (var snapshot in settings)
            {
                var field = FindSettingField(overriddenImporter.GetType(), snapshot.name);
                field.SetValue(overriddenImporter, snapshot.wasNull ? null : JsonUtility.FromJson(snapshot.json, field.FieldType));
            }
            EditorUtility.SetDirty(overriddenImporter);
            overriddenImporter.SaveAndReimport();
            var after = Capture(path);
            var settingsAfter = CaptureSettings(AssetImporter.GetAtPath(path));
            var report = new PreparationReport { before = before, after = after, settingsBefore = settings, settingsAfter = settingsAfter,
                importSettingsPreserved = settings.Select(SettingIdentity).SequenceEqual(settingsAfter.Select(SettingIdentity)),
                localFileIdsPreserved = before.subAssets.Select(Identity).OrderBy(value => value)
                    .SequenceEqual(after.subAssets.Select(Identity).OrderBy(value => value)) };
            if (path == Pet)
            {
                report.prefabPath = PetPrefab;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PetPrefab);
                var renderers = prefab == null ? new SkinnedMeshRenderer[0] : prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                report.prefabSkinnedRenderers = renderers.Length;
                report.prefabMeshesValid = renderers.Length > 0 && renderers.All(renderer => renderer.sharedMesh != null);
                report.prefabMaterialsValid = renderers.Length > 0 && renderers.All(renderer =>
                    renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(material => material != null));
                var reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/submission/webgl-texture-pilot.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            if (!report.importSettingsPreserved || !report.localFileIdsPreserved || !report.prefabMeshesValid || !report.prefabMaterialsValid)
                throw new InvalidOperationException("The importer override changed import settings, subasset identities, or Pet prefab references; stop before preparing more assets. See webgl-texture-pilot.json.");
            var textures = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().ToArray();
            if (textures.Any(texture => texture.format != TextureFormat.DXT5 || texture.isReadable))
                throw new InvalidOperationException("The imported embedded textures did not retain non-readable DXT5 data: " + path);
            return JsonUtility.ToJson(report, true);
        }

        static string Identity(SubAssetInfo asset) => asset.guid + ":" + asset.localFileId + ":" + asset.type + ":" + asset.name;

        static string SettingIdentity(SettingSnapshot snapshot) => snapshot.name + ":" + snapshot.wasNull + ":" + snapshot.json;

        static SettingSnapshot[] CaptureSettings(AssetImporter importer)
        {
            if (importer == null) throw new InvalidOperationException("No glTF importer was found for the asset.");
            return new[] { "importSettings", "editorImportSettings", "instantiationSettings" }.Select(name => {
                var field = FindSettingField(importer.GetType(), name);
                var value = field.GetValue(importer);
                return new SettingSnapshot { name = name, wasNull = value == null, json = value == null ? null : JsonUtility.ToJson(value) };
            }).ToArray();
        }

        static FieldInfo FindSettingField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new InvalidOperationException("The glTF importer setting field was not found: " + name);
        }

        public static string Inspect(string path) => JsonUtility.ToJson(Capture(path), true);

        static Inspection Capture(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            var objects = AssetDatabase.LoadAllAssetsAtPath(path);
            return new Inspection { path = path, importer = importer?.GetType().FullName,
                subAssets = objects.Select(asset => {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id);
                    return new SubAssetInfo { name = asset.name, type = asset.GetType().FullName, guid = guid, localFileId = id };
                }).ToArray(),
                textures = objects.OfType<Texture2D>().Select(texture => new TextureInfo {
                    name = texture.name, format = texture.format.ToString(), graphicsFormat = texture.graphicsFormat.ToString(),
                    width = texture.width, height = texture.height, mipmaps = texture.mipmapCount,
                    runtimeBytes = Profiler.GetRuntimeMemorySizeLong(texture), readable = texture.isReadable }).ToArray() };
        }

        static void RequireWebGL()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Exit Play Mode and select the WebGL build target before preparing textures.");
        }
    }
}
