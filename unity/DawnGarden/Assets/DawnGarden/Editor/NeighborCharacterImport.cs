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
    /// <summary>Imports one approved neighbor into its own character and animation paths.</summary>
    public static class NeighborCharacterImport
    {
        static string ModelPath, AnimationFolder, PrefabPath;
        static readonly Dictionary<string,string> Motions = new()
        {
            {"Idle", "Idle_Loop"}, {"Walk", "Walk_Loop"}, {"Jog", "Jog_Fwd_Loop"},
            {"Greet", "Idle_Rail_Call"}, {"Water", "Farm_Watering"}
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

        public static string Import(string character, string source)
        {
            if (character != "lumi" && character != "lanlan") throw new ArgumentException("Expected lumi or lanlan.");
            var name = character == "lumi" ? "Lumi" : "Lanlan";
            ModelPath = "Assets/DawnGarden/Art/Characters/Neighbors/" + name + ".glb";
            AnimationFolder = "Assets/DawnGarden/Animations/Neighbors/" + name;
            PrefabPath = "Assets/SmallWorldV1/Resources/Neighbors/" + name + ".prefab";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before importing the neighbor.");
            source = Path.GetFullPath(source);
            if (!File.Exists(source)) throw new FileNotFoundException("Neighbor motion GLB is missing.", source);
            EnsureFolder(Path.GetDirectoryName(ModelPath));
            EnsureFolder(AnimationFolder);
            EnsureFolder(Path.GetDirectoryName(PrefabPath));
            File.Copy(source, ModelPath, true);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ModelPath);
            using (var serialized = new SerializedObject(importer))
            {
                var animationMethod = serialized.FindProperty("importSettings.animationMethod");
                if (animationMethod == null) throw new InvalidOperationException("The neighbor requires the glTFast Mecanim importer.");
                animationMethod.intValue = (int)GLTFast.AnimationMethod.Mecanim;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Neighbor GLB did not import.");
            var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var matched = Motions.ToDictionary(pair => pair.Key,
                pair => imported.SingleOrDefault(clip => clip.name == pair.Value || clip.name.EndsWith("|" + pair.Value)));
            if (matched.Values.Any(clip => clip == null || clip.length <= 0))
                throw new InvalidOperationException("Missing neighbor motions. Imported: " + string.Join(", ", imported.Select(c => c.name)));

            var controllerPath = AnimationFolder + "/Neighbor.controller";
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

            var root = new GameObject("Neighbor");
            try
            {
                // Scale an unanimated parent so Mecanim owns only the imported rig.
                var fitting = new GameObject("Visual fitting").transform;
                fitting.SetParent(root.transform, false);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, fitting);
                visual.name = "Visual";
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length == 0 || skins.Any(s => s.bones.Length < 12))
                    throw new InvalidOperationException("Neighbor is missing a usable skinned mesh.");
                foreach (var skin in skins)
                {
                    skin.updateWhenOffscreen = true;
                    if (skin.sharedMaterials.Any(material => material == null || material.shader == null || !material.shader.isSupported))
                        throw new InvalidOperationException("Neighbor has a missing or unsupported material.");
                }
                var rig = visual.GetComponent<Animator>() ?? visual.GetComponentInChildren<Animator>();
                if (rig == null) throw new InvalidOperationException("Neighbor has no Animator.");
                rig.applyRootMotion = false;
                rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.runtimeAnimatorController = controller;
                // Imported skin bounds include motion padding and are not the visible
                // feet. Sample the actual Mecanim Idle skin before aligning the prefab.
                rig.Rebind();
                rig.Play("Idle", 0, 0);
                rig.Update(0);
                var idleBefore = BakedWorldBounds(skins);
                fitting.localScale = Vector3.one * (1.45f / idleBefore.size.y);
                rig.Update(0);
                idleBefore = BakedWorldBounds(skins);
                var verticalCorrection = root.transform.position.y - idleBefore.min.y;
                fitting.localPosition += new Vector3(-idleBefore.center.x, verticalCorrection, -idleBefore.center.z);
                rig.Update(0);
                RefineMotionCurves(rig, skins, controller);
                rig.Rebind();
                rig.Play("Idle", 0, 0);
                rig.Update(0);
                var idleAfter = BakedWorldBounds(skins);
                if (Mathf.Abs(idleAfter.min.y - root.transform.position.y) > .001f || Mathf.Abs(idleAfter.size.y - 1.45f) > .01f)
                    throw new InvalidOperationException($"Neighbor fitting failed: height {idleAfter.size.y:F3}, foot Y {idleAfter.min.y:F3}, parent scale {fitting.localScale.x:F3}.");
                root.AddComponent<SmallWorldAjinActor>().rig = rig;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Neighbor prefab could not be saved.");
                AssetDatabase.SaveAssets();
                using var hash = SHA256.Create();
                var report = new ImportReport
                {
                    sourceSha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(source))).Replace("-", "").ToLowerInvariant(),
                    model = ModelPath, prefab = PrefabPath, skins = skins.Length,
                    minimumJoints = skins.Min(s => s.bones.Length), height = 1.45f,
                    groundingPose = "Mecanim Idle at normalized time 0; actual baked skin vertices",
                    idleHeight = idleAfter.size.y, idleFootYBefore = idleBefore.min.y,
                    idleFootYAfter = idleAfter.min.y, visualVerticalCorrection = verticalCorrection,
                    states = matched.Keys.ToArray(), seconds = matched.Values.Select(c => c.length).ToArray()
                };
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(source), "unity-import-report.json"), JsonUtility.ToJson(report, true));
                return "Neighbor prefab ready: " + PrefabPath + " · " + string.Join(", ", matched.Keys) + " · joints " + report.minimumJoints;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static Bounds BakedWorldBounds(IEnumerable<SkinnedMeshRenderer> skins)
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
                    // Keep local-space vertices here. On the imported glTF rig
                    // BakeMesh(false) already includes renderer scale.
                    skin.BakeMesh(baked, true);
                    baked.GetVertices(vertices);
                    var toWorld = skin.localToWorldMatrix;
                    foreach (var vertex in vertices)
                    {
                        var point = toWorld.MultiplyPoint3x4(vertex);
                        if (!float.IsFinite(point.sqrMagnitude))
                            throw new InvalidOperationException("Neighbor Idle contains non-finite skinned vertices.");
                        if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                        else result.Encapsulate(point);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            if (!found || result.size.y < .001f)
                throw new InvalidOperationException("Neighbor Idle has no visible baked skin.");
            return result;
        }

        static void RefineMotionCurves(Animator animator, SkinnedMeshRenderer[] skins, AnimatorController controller)
        {
            var bones = skins.SelectMany(s => s.bones).Distinct().ToDictionary(t => t.name);
            var hips = bones["mixamorig:Hips"];
            var hipsPath = AnimationUtility.CalculateTransformPath(hips, animator.transform);
            var arms = new[] { bones["mixamorig:LeftArm"], bones["mixamorig:RightArm"] };
            var elbows = new[] { bones["mixamorig:LeftForeArm"], bones["mixamorig:RightForeArm"] };
            var armPaths = arms.Select(t => AnimationUtility.CalculateTransformPath(t, animator.transform)).ToArray();
            // Infer left and right from the actual bind positions, including the
            // glTF coordinate conversion. Both arms retain their original swing.
            var side = arms.Select(t => Mathf.Sign(Vector3.Dot(t.position - hips.position, animator.transform.right))).ToArray();
            var armProperties = new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            var hipProperties = new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            foreach (var state in controller.layers[0].stateMachine.states.Select(s => s.state))
            {
                var clip = (AnimationClip)state.motion;
                if (state.name == "Greet")
                {
                    BuildWave(animator, clip, hips, hipsPath, bones.Values.ToArray(), arms[1], elbows[1],
                        bones["mixamorig:RightHand"], side[1], armProperties, hipProperties);
                    animator.Rebind();
                    continue;
                }
                var duration = clip.length;
                int frames = Mathf.CeilToInt(duration * 30);
                var hipKeys = NewCurves(3);
                var armKeys = new[] { NewCurves(4), NewCurves(4) };
                var previous = new[] { Quaternion.identity, Quaternion.identity };
                for (int i = 0; i <= frames; ++i)
                {
                    float phase = (float)i / frames;
                    float seconds = phase * duration;
                    animator.Play(state.name, 0, phase);
                    animator.Update(0);
                    for (int arm = 0; arm < arms.Length; ++arm)
                    {
                        var upper = arms[arm];
                        var direction = (elbows[arm].position - upper.position).normalized;
                        var outward = animator.transform.right * side[arm];
                        // Give close-hanging elbows clearance from the belly.
                        float clearance = Mathf.Clamp01((.32f - Vector3.Dot(direction, outward)) / .42f);
                        if (clearance > 0)
                            upper.rotation = Quaternion.RotateTowards(Quaternion.identity,
                                Quaternion.FromToRotation(direction, direction + outward * .45f), 13f * clearance) * upper.rotation;
                        var local = upper.localRotation;
                        if (i > 0 && Quaternion.Dot(previous[arm], local) < 0)
                            local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
                        previous[arm] = local;
                        for (int c = 0; c < 4; ++c) armKeys[arm][c].Add(new Keyframe(seconds, local[c]));
                    }
                    var position = hips.localPosition;
                    if (state.name == "Walk" || state.name == "Jog")
                    {
                        float floor = BakedWorldBounds(skins).min.y;
                        float correction = state.name == "Walk" ? -floor : Mathf.Max(0f, -floor);
                        position += hips.parent.InverseTransformVector(Vector3.up * correction);
                    }
                    for (int c = 0; c < 3; ++c) hipKeys[c].Add(new Keyframe(seconds, position[c]));
                }
                for (int arm = 0; arm < arms.Length; ++arm)
                    WriteCurves(clip, armPaths[arm], armProperties, armKeys[arm]);
                if (state.name == "Walk" || state.name == "Jog")
                    WriteCurves(clip, hipsPath, hipProperties, hipKeys);
                clip.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(clip);
                animator.Rebind();
            }
        }

        static void BuildWave(Animator animator, AnimationClip clip, Transform hips, string hipsPath,
            Transform[] bones, Transform arm, Transform elbow, Transform hand, float side,
            string[] rotationProperties, string[] positionProperties)
        {
            // The source rail-call leans forward as though resting on a bar.
            // Use the neighbor's fitted standing pose for a clear in-game wave.
            var paths = bones.Select(t => AnimationUtility.CalculateTransformPath(t, animator.transform)).ToArray();
            var rotations = bones.Select(_ => NewCurves(4)).ToArray();
            var positions = NewCurves(3);
            var previous = new Quaternion[bones.Length];
            float duration = clip.length;
            int frames = Mathf.CeilToInt(duration * 30);
            var outward = animator.transform.right * side;
            var front = animator.transform.forward;
            for (int frame = 0; frame <= frames; ++frame)
            {
                float phase = (float)frame / frames;
                float seconds = phase * duration;
                animator.Play("Idle", 0, 0);
                animator.Update(0);
                float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase / .22f)) *
                    Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - phase) / .23f));
                float wave = Mathf.Sin(phase * Mathf.PI * 6);
                AimChild(arm, elbow, outward + Vector3.up * .20f + front * .32f, reach);
                AimChild(elbow, hand, outward * (.78f + wave * .18f) + Vector3.up + front * .38f, reach);
                for (int i = 0; i < bones.Length; ++i)
                {
                    var q = bones[i].localRotation;
                    if (frame > 0 && Quaternion.Dot(q, previous[i]) < 0)
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous[i] = q;
                    for (int axis = 0; axis < 4; ++axis) rotations[i][axis].Add(new Keyframe(seconds, q[axis]));
                }
                for (int axis = 0; axis < 3; ++axis) positions[axis].Add(new Keyframe(seconds, hips.localPosition[axis]));
            }
            clip.ClearCurves();
            for (int i = 0; i < bones.Length; ++i) WriteCurves(clip, paths[i], rotationProperties, rotations[i]);
            WriteCurves(clip, hipsPath, positionProperties, positions);
            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
        }

        static void AimChild(Transform joint, Transform child, Vector3 direction, float amount)
        {
            var delta = Quaternion.FromToRotation(child.position - joint.position, direction);
            joint.rotation = Quaternion.Slerp(Quaternion.identity, delta, amount) * joint.rotation;
        }

        static List<Keyframe>[] NewCurves(int count) =>
            Enumerable.Range(0, count).Select(_ => new List<Keyframe>()).ToArray();

        static void WriteCurves(AnimationClip clip, string path, string[] properties, List<Keyframe>[] keys)
        {
            for (int axis = 0; axis < properties.Length; ++axis)
            {
                var curve = new AnimationCurve(keys[axis].ToArray());
                for (int i = 0; i < curve.length; ++i)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[axis]), curve);
            }
        }

        static AnimationClip SaveClip(string name, AnimationClip source)
        {
            var clip = UnityEngine.Object.Instantiate(source);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Walk" || name == "Jog";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var path = AnimationFolder + "/Neighbor-" + name + ".anim";
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
