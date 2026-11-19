using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DawnGarden.Editor
{
    /// <summary>Imports only the approved guardian; no existing character paths are reused.</summary>
    public static class MossstoneGuardianImport
    {
        const string ModelPath = "Assets/DawnGarden/Art/Characters/Guardian/MossstoneGuardian.glb";
        const string AnimationFolder = "Assets/DawnGarden/Animations/Guardian";
        const string PrefabPath = "Assets/SmallWorldV1/Resources/Guardian/MossstoneGuardian.prefab";
        static readonly Dictionary<string, string> Motions = new()
        {
            { "Idle", "Idle_Loop" }, { "Walk", "Walk_Loop" }, { "Attack", "Punch_Jab" },
            { "Guard", "Shield_OneShot" }, { "Skill", "Spell_Simple_Shoot" },
            { "Hit", "Hit_Chest" }, { "Defeat", "Death01" }
        };

        [Serializable] sealed class ImportReport
        {
            public string sourceSha256, model, prefab, groundingPose;
            public int skins, minimumJoints;
            public float height;
            public float idleHeight, idleFootYBefore, idleFootYAfter, visualVerticalCorrection;
            public string[] states;
            public float[] seconds;
        }

        public static string ImportDefault() => Import(Path.GetFullPath(Path.Combine(
            Application.dataPath, "../../../artifacts/guardian-design/v1/a-pose-rig/guardian-motion.glb")));

        public static string Import(string source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before importing the guardian.");
            source = Path.GetFullPath(source);
            if (!File.Exists(source)) throw new FileNotFoundException("Guardian motion GLB is missing.", source);
            EnsureFolder(Path.GetDirectoryName(ModelPath));
            EnsureFolder(AnimationFolder);
            EnsureFolder(Path.GetDirectoryName(PrefabPath));
            File.Copy(source, ModelPath, true);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ModelPath);
            using (var serialized = new SerializedObject(importer))
            {
                var animationMethod = serialized.FindProperty("importSettings.animationMethod");
                if (animationMethod == null) throw new InvalidOperationException("The guardian requires the glTFast Mecanim importer.");
                animationMethod.intValue = (int)GLTFast.AnimationMethod.Mecanim;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Guardian GLB did not import.");
            var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var matched = Motions.ToDictionary(pair => pair.Key,
                pair => imported.SingleOrDefault(clip => clip.name == pair.Value || clip.name.EndsWith("|" + pair.Value)));
            if (matched.Values.Any(clip => clip == null || clip.length <= 0))
                throw new InvalidOperationException("Missing guardian motions. Imported: " + string.Join(", ", imported.Select(c => c.name)));

            var controllerPath = AnimationFolder + "/MossstoneGuardian.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ??
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            foreach (var pair in matched)
            {
                var state = machine.AddState(pair.Key);
                state.motion = SaveClip(pair.Key, pair.Value);
                if (pair.Key == "Idle") machine.defaultState = state;
            }

            var root = new GameObject("MossstoneGuardian");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Visual";
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length == 0 || skins.Any(s => s.bones.Length < 12))
                    throw new InvalidOperationException("Guardian is missing a usable skinned mesh.");
                var bounds = skins[0].bounds;
                foreach (var skin in skins.Skip(1)) bounds.Encapsulate(skin.bounds);
                if (!float.IsFinite(bounds.size.y) || bounds.size.y < .001f)
                    throw new InvalidOperationException("Guardian skin has invalid bounds.");
                var scale = 2.15f / bounds.size.y;
                visual.transform.localScale *= scale;
                visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                foreach (var skin in skins)
                {
                    skin.updateWhenOffscreen = true;
                    if (skin.sharedMaterials.Any(material => material == null || material.shader == null || !material.shader.isSupported))
                        throw new InvalidOperationException("Guardian has a missing or unsupported material.");
                }
                var rig = visual.GetComponent<Animator>() ?? visual.GetComponentInChildren<Animator>();
                if (rig == null) throw new InvalidOperationException("Guardian has no Animator.");
                rig.applyRootMotion = false;
                rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.runtimeAnimatorController = controller;
                // Imported skin bounds include motion padding and are not the visible
                // feet. Sample the actual Mecanim Idle skin before aligning the prefab.
                rig.Rebind();
                rig.Play("Idle", 0, 0);
                rig.Update(0);
                var idleBefore = BakedWorldBounds(skins);
                var verticalCorrection = root.transform.position.y - idleBefore.min.y;
                visual.transform.position += Vector3.up * verticalCorrection;
                rig.Update(0);
                var idleAfter = BakedWorldBounds(skins);
                if (Mathf.Abs(idleAfter.min.y - root.transform.position.y) > .001f)
                    throw new InvalidOperationException("Guardian Idle feet could not be aligned to the prefab ground.");
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Guardian prefab could not be saved.");
                AssetDatabase.SaveAssets();
                using var hash = SHA256.Create();
                var report = new ImportReport
                {
                    sourceSha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(source))).Replace("-", "").ToLowerInvariant(),
                    model = ModelPath, prefab = PrefabPath, skins = skins.Length,
                    minimumJoints = skins.Min(s => s.bones.Length), height = 2.15f,
                    groundingPose = "Mecanim Idle at normalized time 0; actual baked skin vertices",
                    idleHeight = idleAfter.size.y, idleFootYBefore = idleBefore.min.y,
                    idleFootYAfter = idleAfter.min.y, visualVerticalCorrection = verticalCorrection,
                    states = matched.Keys.ToArray(), seconds = matched.Values.Select(c => c.length).ToArray()
                };
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(source), "unity-import-report.json"), JsonUtility.ToJson(report, true));
                return "Guardian prefab ready: " + PrefabPath + " · " + string.Join(", ", matched.Keys) + " · joints " + report.minimumJoints;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Bounds BakedWorldBounds(IEnumerable<SkinnedMeshRenderer> skins)
        {
            var baked = new Mesh();
            var vertices = new List<Vector3>();
            Bounds result = default;
            bool found = false;
            try
            {
                foreach (var skin in skins)
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                    skin.BakeMesh(baked, false);
                    baked.GetVertices(vertices);
                    var toWorld = skin.localToWorldMatrix;
                    foreach (var vertex in vertices)
                    {
                        var point = toWorld.MultiplyPoint3x4(vertex);
                        if (!float.IsFinite(point.sqrMagnitude))
                            throw new InvalidOperationException("Guardian Idle contains non-finite skinned vertices.");
                        if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                        else result.Encapsulate(point);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            if (!found || result.size.y < .001f)
                throw new InvalidOperationException("Guardian Idle has no visible baked skin.");
            return result;
        }

        static AnimationClip SaveClip(string name, AnimationClip source)
        {
            var clip = UnityEngine.Object.Instantiate(source);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Walk";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var path = AnimationFolder + "/Guardian-" + name + ".anim";
            var previous = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (previous == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, previous);
            UnityEngine.Object.DestroyImmediate(clip);
            return previous;
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
