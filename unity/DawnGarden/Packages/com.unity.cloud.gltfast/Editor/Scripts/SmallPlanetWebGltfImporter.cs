// Project-local Web texture compression. Keep GltfImporter.cs unchanged.
#if !GLTFAST_EDITOR_IMPORT_OFF
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace GLTFast.Editor
{
    [ScriptedImporter(1, null, overrideExts: new[] { "glb", "gltf" })]
    class SmallPlanetWebGltfImporter : GltfImporter
    {
        static readonly FieldInfo SettingsField = typeof(GltfImporter).GetField("importSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo ImportField = typeof(GltfImporter).GetField("m_Gltf", BindingFlags.Instance | BindingFlags.NonPublic);

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (ctx.selectedBuildTarget != BuildTarget.WebGL)
            {
                base.OnImportAsset(ctx);
                return;
            }
            if (SettingsField == null || ImportField == null)
                throw new InvalidOperationException("The glTFast importer fields changed; Web texture compression cannot continue.");

            var originalSettings = (ImportSettings)SettingsField.GetValue(this);
            var readableSettings = originalSettings == null
                ? new ImportSettings { NodeNameMethod = NameImportMethod.OriginalUnique, GenerateMipMaps = true, AnimationMethod = AnimationMethod.Mecanim }
                : JsonUtility.FromJson<ImportSettings>(JsonUtility.ToJson(originalSettings));
            readableSettings.TexturesReadable = true;
            SettingsField.SetValue(this, readableSettings);
            try
            {
                base.OnImportAsset(ctx);
                var gltf = ImportField.GetValue(this) as GltfImport;
                if (gltf == null || gltf.LoadingError)
                    throw new InvalidOperationException("glTF import failed before Web texture compression: " + ctx.assetPath);

                var compressed = new HashSet<Texture2D>();
                for (var i = 0; i < gltf.TextureCount; i++)
                {
                    var texture = gltf.GetTexture(i);
                    if (texture == null || !compressed.Add(texture)) continue;
                    var path = AssetDatabase.GetAssetPath(texture);
                    // External PNG/JPEG assets retain their TextureImporter settings.
                    if (!string.IsNullOrEmpty(path) && path != ctx.assetPath) continue;
                    if (!texture.isReadable)
                        throw new InvalidOperationException("Embedded Web texture is not readable: " + texture.name);
                    EditorUtility.CompressTexture(texture, TextureFormat.DXT5, TextureCompressionQuality.Normal);
                    if (texture.format != TextureFormat.DXT5)
                        throw new InvalidOperationException("Web texture compression did not produce DXT5: " + texture.name);
                    // Preserve the generated mip chain and discard the CPU pixel copy.
                    texture.Apply(false, true);
                }
                Debug.Log("Small World Web textures: " + compressed.Count + " texture(s) inspected in " + ctx.assetPath);
            }
            finally
            {
                // The temporary readable setting must not become an authored import setting.
                SettingsField.SetValue(this, originalSettings);
            }
        }
    }
}
#endif
