using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden.Editor
{
    /// <summary>Continuous, isolated Mecanim frames for an external silent review film.</summary>
    public static class NeighborMotionFilm
    {
        const int Fps = 24, Width = 640, Height = 800;
        const float Step = 1f / Fps, ExpectedHeight = 1.45f, TargetSeconds = 24;
        static readonly string[] States = { "Idle", "Walk", "Jog", "Greet", "Water" };

        [Serializable] public sealed class GeometrySample
        {
            public int frame, vertices;
            public float seconds, normalizedTime;
            public bool geometryValid;
            public Vector3 min, max, size;
            public string error;
            public string[] warnings;
        }

        [Serializable] public sealed class Motion
        {
            public string state, clip, clipAsset, frames;
            public float clipLengthSeconds, playbackSpeed, plannedSeconds, simulatedSeconds, videoSeconds;
            public float firstNormalizedTime, lastNormalizedTime;
            public int frameCount, writtenFrames;
            public bool loop, stateMatched = true, fullClipPlayed;
            public GeometrySample[] geometry;
        }

        [Serializable] public sealed class Report
        {
            public int schemaVersion = 1, framesPerSecond = Fps, width = Width, height = Height;
            public string character, resource, prefab, prefabSha256, createdUtc, error;
            public string[] geometrySources;
            public bool complete, geometryChecksPassed, stateChecksPassed, visualReviewRequired = true;
            public float totalVideoSeconds, groundReferenceY = 0;
            public string method = "Isolated PreviewRenderUtility; Animator.Play once per state, then continuous Animator.Update(1/24) and CPU-baked visible skin. Original prefab materials.";
            public string geometryMethod = "BakeMesh only at first/middle/last frame of each state; bounds, finite vertices/normals and ground-height warnings. Self-intersection and skin weights require visual review.";
            public string timing = "Frame 0 is t=0; subsequent frames advance 1/24 s. The final pose is included, so video duration is simulated duration plus one frame. Loops play at least twice; other clips once; extra time goes to loops to target 24 s without truncating clips.";
            public Motion[] motions;
        }

        /// <summary>Writes idle/walk/jog/greet/water/frame-00000.png and unity-motion-film.json.</summary>
        public static string Render(string character, string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before recording a neighbor.");
            if (character != "lumi" && character != "lanlan")
                throw new ArgumentException("Expected lumi or lanlan.", nameof(character));
            if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("An output folder is required.", nameof(output));
            var resource = "Neighbors/" + (character == "lumi" ? "Lumi" : "Lanlan");
            var prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) throw new FileNotFoundException("Missing neighbor prefab: " + resource);
            output = Path.GetFullPath(output);
            var reportPath = Path.Combine(output, "unity-motion-film.json");
            // A fresh destination prevents an older, longer sequence leaking into ffmpeg input.
            if (File.Exists(reportPath) || States.Any(state =>
                Directory.Exists(Path.Combine(output, state.ToLowerInvariant())) &&
                Directory.EnumerateFiles(Path.Combine(output, state.ToLowerInvariant()), "frame-*.png").Any()))
                throw new IOException("Use a fresh output folder; this destination already contains motion-film results.");
            Directory.CreateDirectory(output);
            var report = new Report
            {
                character = character, resource = resource, prefab = AssetDatabase.GetAssetPath(prefab),
                createdUtc = DateTime.UtcNow.ToString("O")
            };
            using (var hash = SHA256.Create())
                report.prefabSha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(report.prefab))).Replace("-", "").ToLowerInvariant();
            var preview = new PreviewRenderUtility();
            Material referenceMaterial = null;
            Texture2D image = null;
            try
            {
                var actor = UnityEngine.Object.Instantiate(prefab);
                preview.AddSingleGO(actor);
                actor.name = character + " continuous motion review";
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null) behaviour.enabled = false;
                actor.SetActive(true);
                var animator = actor.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Prefab has no Mecanim controller.");
                animator.enabled = true;
                animator.fireEvents = false;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1;
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0 || skins.Any(s => s.sharedMesh == null || s.bones.Length < 12 || s.bones.Any(b => b == null)))
                    throw new InvalidOperationException("Missing skin mesh or usable bone hierarchy.");
                if (skins.SelectMany(s => s.sharedMaterials).Any(m => m == null || m.shader == null || !m.shader.isSupported))
                    throw new InvalidOperationException("Missing or unsupported character material.");
                foreach (var skin in skins) skin.updateWhenOffscreen = true;
                report.geometrySources = skins.Select(s => AssetDatabase.GetAssetPath(s.sharedMesh)).Distinct().ToArray();
                report.motions = States.Select(state => DescribeMotion(animator, state)).ToArray();
                var loops = report.motions.Where(m => m.loop).ToArray();
                float extra = Mathf.Max(0, TargetSeconds - report.motions.Sum(m => m.plannedSeconds));
                foreach (var motion in loops) motion.plannedSeconds += extra / loops.Length;
                SetupView(preview);
                referenceMaterial = AddGroundReference(preview);
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                using var visiblePose = new NeighborCharacterReview.VisibleSkinPose(skins);

                foreach (var motion in report.motions)
                {
                    string folder = Path.Combine(output, motion.state.ToLowerInvariant());
                    Directory.CreateDirectory(folder);
                    motion.frameCount = Mathf.CeilToInt(motion.plannedSeconds * Fps) + 1;
                    motion.simulatedSeconds = (motion.frameCount - 1) * Step;
                    motion.videoSeconds = motion.frameCount * Step;
                    var samples = new List<GeometrySample>();
                    var center = new Vector3(0, ExpectedHeight * .52f, 0);
                    var direction = motion.state == "Walk" || motion.state == "Jog" || motion.state == "Water"
                        ? new Vector3(1, .10f, .38f) : new Vector3(.28f, .10f, 1);
                    preview.camera.transform.position = center + direction.normalized * 4.5f;
                    preview.camera.transform.LookAt(center);
                    StartState(animator, motion.state);
                    // Only this loop advances the recorded animation. No normalized-time
                    // seeking or repeated Rebind is used here.
                    for (int frame = 0; frame < motion.frameCount; frame++)
                    {
                        if (frame > 0) animator.Update(Step);
                        var actual = animator.GetCurrentAnimatorStateInfo(0);
                        motion.stateMatched &= actual.shortNameHash == Animator.StringToHash(motion.state) && !animator.IsInTransition(0);
                        if (frame == 0) motion.firstNormalizedTime = actual.normalizedTime;
                        motion.lastNormalizedTime = actual.normalizedTime;
                        if (frame == 0 || frame == (motion.frameCount - 1) / 2 || frame == motion.frameCount - 1)
                            samples.Add(CheckGeometry(skins, frame, actual.normalizedTime));
                        visiblePose.Refresh();
                        WriteFrame(preview, image, Path.Combine(folder, "frame-" + frame.ToString("D5") + ".png"));
                        motion.writtenFrames++;
                    }
                    motion.geometry = samples.ToArray();
                    motion.fullClipPlayed = motion.stateMatched &&
                        motion.lastNormalizedTime - motion.firstNormalizedTime >= (motion.loop ? 2 : 1) - .001f;
                    report.totalVideoSeconds += motion.videoSeconds;
                }
                report.geometryChecksPassed = report.motions.All(m => m.geometry.All(g => g.geometryValid));
                report.stateChecksPassed = report.motions.All(m => m.stateMatched && m.fullClipPlayed);
                report.complete = true;
                return character + ": " + report.motions.Sum(m => m.writtenFrames) + " continuous frames at 24 fps; " +
                    report.totalVideoSeconds.ToString("F2") + " s; geometry=" + report.geometryChecksPassed +
                    ", states=" + report.stateChecksPassed + "; visual review required. " + reportPath;
            }
            catch (Exception error) { report.error = error.Message; throw; }
            finally
            {
                preview.Cleanup();
                if (referenceMaterial != null) UnityEngine.Object.DestroyImmediate(referenceMaterial);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
        }

        static void StartState(Animator animator, string state)
        {
            animator.Rebind();
            if (!animator.HasState(0, Animator.StringToHash(state)))
                throw new InvalidOperationException("Missing Mecanim state " + state);
            animator.Play(state, 0, 0);
            animator.Update(0);
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Animator.StringToHash(state))
                throw new InvalidOperationException("Mecanim did not enter requested state " + state);
        }

        static Motion DescribeMotion(Animator animator, string state)
        {
            StartState(animator, state);
            var info = animator.GetCurrentAnimatorStateInfo(0);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length != 1 || clips[0].clip == null)
                throw new InvalidOperationException(state + " must play one real clip; a blend cannot be timed as a single motion.");
            var clip = clips[0].clip;
            float speed = info.speed * info.speedMultiplier * animator.speed;
            if (!float.IsFinite(clip.length) || clip.length <= 0 || !float.IsFinite(speed) || speed <= 0)
                throw new InvalidOperationException(state + " has an invalid clip length or playback speed.");
            return new Motion
            {
                state = state, clip = clip.name, clipAsset = AssetDatabase.GetAssetPath(clip),
                frames = state.ToLowerInvariant() + "/frame-%05d.png", clipLengthSeconds = clip.length,
                playbackSpeed = speed, loop = info.loop, plannedSeconds = clip.length / speed * (info.loop ? 2 : 1)
            };
        }

        static GeometrySample CheckGeometry(SkinnedMeshRenderer[] skins, int frame, float normalizedTime)
        {
            var sample = new GeometrySample { frame = frame, seconds = frame * Step, normalizedTime = normalizedTime };
            var warnings = new List<string>();
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            Bounds bounds = default;
            bool found = false;
            try
            {
                foreach (var skin in skins)
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                    mesh.Clear();
                    skin.BakeMesh(mesh, true);
                    mesh.GetVertices(vertices);
                    mesh.GetNormals(normals);
                    foreach (var normal in normals)
                        if (!Finite(normal)) throw new InvalidOperationException("Non-finite baked normal.");
                    foreach (var vertex in vertices)
                    {
                        var world = skin.localToWorldMatrix.MultiplyPoint3x4(vertex);
                        if (!Finite(world)) throw new InvalidOperationException("Non-finite baked vertex.");
                        if (!found) { bounds = new Bounds(world, Vector3.zero); found = true; }
                        else bounds.Encapsulate(world);
                        sample.vertices++;
                    }
                }
                if (!found || sample.vertices < 3) throw new InvalidOperationException("No visible skinned vertices.");
                sample.min = bounds.min; sample.max = bounds.max; sample.size = bounds.size;
                if (bounds.size.y < ExpectedHeight * .22f || bounds.size.y > ExpectedHeight * 2.5f ||
                    bounds.size.x > ExpectedHeight * 3 || bounds.size.z > ExpectedHeight * 3 || bounds.center.magnitude > ExpectedHeight * 3)
                    throw new InvalidOperationException("Abnormal sampled geometry bounds; inspect deformation.");
                if (bounds.min.y < -.10f) warnings.Add("Visible skin extends below y=0; inspect feet/tool contact.");
                if (bounds.min.y > .12f) warnings.Add("Visible skin is above y=0; inspect intended motion versus floating.");
                sample.geometryValid = true;
            }
            catch (Exception error) { sample.error = error.Message; }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            sample.warnings = warnings.ToArray();
            return sample;
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static void SetupView(PreviewRenderUtility preview)
        {
            var camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.88f, .90f, .86f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.allowHDR = false; camera.allowMSAA = true;
            camera.orthographic = true; camera.orthographicSize = ExpectedHeight * .76f;
            var center = new Vector3(0, ExpectedHeight * .52f, 0);
            camera.transform.position = center + new Vector3(.28f, .10f, 1).normalized * 4.5f;
            camera.transform.LookAt(center);
            preview.lights[0].intensity = 1.05f; preview.lights[0].color = Color.white;
            preview.lights[0].transform.rotation = Quaternion.Euler(32, 210, 0);
            preview.lights[1].intensity = .55f; preview.lights[1].color = Color.white;
            preview.lights[1].transform.rotation = Quaternion.Euler(22, 35, 0);
            preview.ambientColor = new Color(.45f, .45f, .45f);
        }

        static Material AddGroundReference(PreviewRenderUtility preview)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("No shader for the y=0 reference line.");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var color = new Color(.30f, .34f, .31f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            var reference = new GameObject("y=0 foot-height reference");
            preview.AddSingleGO(reference);
            var line = reference.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 2;
            line.SetPositions(new[] { new Vector3(-1.2f, 0, 0), new Vector3(1.2f, 0, 0) });
            line.startWidth = line.endWidth = .006f;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return material;
        }

        static void WriteFrame(PreviewRenderUtility preview, Texture2D image, string path)
        {
            preview.BeginPreview(new Rect(0, 0, Width, Height), GUIStyle.none);
            preview.Render(true);
            var texture = preview.EndPreview();
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
    }
}
