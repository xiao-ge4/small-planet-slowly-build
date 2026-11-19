using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DawnGarden.Editor
{
    public static class SmallWorldAjinImport
    {
        const string ModelPath = "Assets/DawnGarden/Art/Characters/Ajin.glb";
        const string Output = "Assets/SmallWorldV1/Resources/Homecoming";
        static readonly Dictionary<string, string> Motion = new()
        {
            { "Idle", "Idle_Loop" }, { "Walk", "Walk_Loop" }, { "Jog", "Jog_Fwd_Loop" },
            { "Greet", "Idle_Rail_Call" }, { "Water", "Farm_Watering" }
        };

        public static string Import(string source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before importing Ajin");
            if (!File.Exists(source)) throw new FileNotFoundException("Ajin's animated GLB is missing");
            File.Copy(source, ModelPath, true);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var modelImporter = AssetImporter.GetAtPath(ModelPath);
            using (var importer = new SerializedObject(modelImporter))
            {
                importer.FindProperty("importSettings.animationMethod").intValue = (int)GLTFast.AnimationMethod.Mecanim;
                importer.ApplyModifiedPropertiesWithoutUndo();
            }
            modelImporter.SaveAndReimport();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null) throw new InvalidOperationException("The Ajin GLB did not import");
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var matched = Motion.ToDictionary(pair => pair.Key, pair => clips.SingleOrDefault(clip => clip.name == pair.Value || clip.name.EndsWith("|" + pair.Value)));
            if (matched.Values.Any(clip => clip == null))
                throw new InvalidOperationException("Missing motions. Imported: " + string.Join(", ", clips.Select(c => c.name)));
            var controllerPath = "Assets/DawnGarden/Animations/Ajin.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ??
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var existing in stateMachine.states) stateMachine.RemoveState(existing.state);
            foreach (var pair in matched)
            {
                var clip = SaveMotion(pair.Key, pair.Value);
                var state = stateMachine.AddState(pair.Key);
                state.motion = clip;
                if (pair.Key == "Idle") stateMachine.defaultState = state;
            }

            var root = new GameObject("阿烬 · 暖焰营地");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                var skin = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skin.Length == 0 || skin.Any(s => s.bones.Length < 12))
                    throw new InvalidOperationException("Ajin is missing a usable skinned mesh");
                var bounds = skin[0].bounds;
                foreach (var s in skin.Skip(1)) bounds.Encapsulate(s.bounds);
                var scale = 1.62f / bounds.size.y;
                visual.transform.localScale *= scale;
                visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                var animator = visual.GetComponent<Animator>() ?? visual.GetComponentInChildren<Animator>();
                if (animator == null) throw new InvalidOperationException("Ajin has no Animator");
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.runtimeAnimatorController = controller;
                root.AddComponent<SmallWorldAjinActor>().rig = animator;
                PrefabUtility.SaveAsPrefabAsset(root, Output + "/Ajin.prefab");
                AssetDatabase.SaveAssets();
                return "Ajin prefab ready · " + string.Join(", ", matched.Keys) + " · joints " + skin[0].bones.Length;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AnimationClip SaveMotion(string name, AnimationClip source)
        {
            var clip = UnityEngine.Object.Instantiate(source);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Walk" || name == "Jog";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var path = "Assets/DawnGarden/Animations/Ajin-" + name + ".anim";
            var previous = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (previous == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
            EditorUtility.CopySerialized(clip, previous);
            UnityEngine.Object.DestroyImmediate(clip);
            return previous;
        }
    }
}
