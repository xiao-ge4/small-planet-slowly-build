using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    // Generic animation clips retain the source skeleton; movement belongs to ActorRoot.
    public sealed class AssetDemo : MonoBehaviour
    {
        public Animator animator;
        public Transform actorRoot, hips, rightHand, lid, plant, gripSocket, nozzle;
        public AnimationClip[] clips;
        public Transform[] drops;
        public Camera demoCamera;
        private string action = "Idle";
        private float elapsed;
        private bool chestOpen, watered, verifying;
        private int errors;
        private Vector3 initialActorPosition;
        private Quaternion initialActorRotation;
        private float groundY;

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void Start()
        {
            initialActorPosition = actorRoot.position;
            initialActorRotation = actorRoot.rotation;
            groundY = actorRoot.position.y;
            SetAction("Idle");
        }
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        public void SetAction(string name)
        {
            if (!clips.Any(c => c.name == name)) throw new ArgumentException("Unknown clip " + name);
            action = name;
            elapsed = 0;
            if (name == "Watering")
            {
                actorRoot.SetPositionAndRotation(new Vector3(-1.1f, groundY, .1f), Quaternion.Euler(0, 180, 0));
            }
            else if (name == "Chest")
            {
                actorRoot.SetPositionAndRotation(new Vector3(1.45f, groundY, .1f), Quaternion.Euler(0, 180, 0));
                chestOpen = false;
            }
            animator.speed = 1;
            animator.Play(name, 0, 0);
            animator.Update(0);
            gripSocket.gameObject.SetActive(name == "Watering");
            if (name == "Watering" || name == "Chest")
            {
                demoCamera.transform.position = actorRoot.position + new Vector3(2.8f, 2.5f, -4.3f);
                demoCamera.transform.LookAt(actorRoot.position + new Vector3(0, .8f, -.35f));
            }
            else
            {
                demoCamera.transform.position = new Vector3(3.5f, 3.4f, 6.2f);
                demoCamera.transform.LookAt(new Vector3(0, .75f, 0));
            }
        }
        public void ResetDemo()
        {
            actorRoot.SetPositionAndRotation(initialActorPosition, initialActorRotation);
            watered = chestOpen = false;
            plant.localScale = Vector3.one;
            lid.localRotation = Quaternion.identity;
            SetAction("Idle");
        }
        void Update()
        {
            if (verifying) return;
            elapsed += Time.deltaTime;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetAction("Idle");
                if (keyboard.digit2Key.wasPressedThisFrame) SetAction("Jog");
                if (keyboard.digit3Key.wasPressedThisFrame) SetAction("Watering");
                if (keyboard.digit4Key.wasPressedThisFrame) SetAction("Chest");
                if (keyboard.rKey.wasPressedThisFrame) ResetDemo();
                if (action == "Idle" || action == "Jog")
                {
                    var dir = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0,
                        (keyboard.sKey.isPressed ? 1 : 0) - (keyboard.wKey.isPressed ? 1 : 0));
                    if (dir.sqrMagnitude > 0)
                    {
                        if (action != "Jog") SetAction("Jog");
                        MoveActor(dir, Time.deltaTime);
                    }
                    else if (keyboard.wKey.wasReleasedThisFrame || keyboard.aKey.wasReleasedThisFrame || keyboard.sKey.wasReleasedThisFrame || keyboard.dKey.wasReleasedThisFrame)
                        SetAction("Idle");
                }
            }
            if ((action == "Watering" || action == "Chest") && elapsed >= clips.First(c => c.name == action).length)
            {
                if (action == "Watering") watered = true;
                else chestOpen = true;
                SetAction("Idle");
            }
        }
        public void MoveActor(Vector3 direction, float deltaTime)
        {
            direction.y = 0;
            if (direction.sqrMagnitude == 0) return;
            actorRoot.position += direction.normalized * (1.4f * deltaTime);
            var p = actorRoot.position; p.x = Mathf.Clamp(p.x, -3.3f, 3.3f); p.z = Mathf.Clamp(p.z, -.2f, 2.7f); actorRoot.position = p;
            actorRoot.rotation = Quaternion.LookRotation(direction);
        }
        void LateUpdate()
        {
            var t = elapsed;
            if (action == "Chest") lid.localRotation = Quaternion.Euler(-77.35f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, 1.05f, t)), 0, 0);
            else if (chestOpen) lid.localRotation = Quaternion.Euler(-77.35f, 0, 0);
            if (watered) plant.localScale = Vector3.one * 1.18f;
            for (var i = 0; i < drops.Length; i++)
            {
                var on = action == "Watering" && t > .72f && t < 3.35f;
                drops[i].gameObject.SetActive(on);
                if (!on) continue;
                var u = Mathf.Repeat(t * 1.8f + (float)i / drops.Length, 1);
                var end = plant.position + new Vector3(Mathf.Sin(i * 2.4f) * .18f, .1f, Mathf.Cos(i * 2.4f) * .18f);
                var p = Vector3.Lerp(nozzle.position, end, u); p.y = Mathf.Lerp(nozzle.position.y, end.y, u * u);
                drops[i].position = p;
            }
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 174, 238), GUI.skin.box);
            GUILayout.Label("DAWN GARDEN / Asset lab");
            GUILayout.Label("22 joints / 4 clips");
            foreach (var name in new[] { "Idle", "Jog", "Watering", "Chest" })
                if (GUILayout.Button(name, GUILayout.Height(25)) && !verifying) SetAction(name);
            if (GUILayout.Button("Reset [R]") && !verifying) ResetDemo();
            GUILayout.Label("WASD: move | 1-4: clips");
            GUILayout.Label(action + (verifying ? " / verifying" : ""));
            GUILayout.EndArea();
        }
        public void BeginVerification(string output)
        {
            if (verifying) return;
            verifying = true;
            StartCoroutine(Verify(output));
        }
        IEnumerator Verify(string output)
        {
            Directory.CreateDirectory(output);
            yield return null;
            var checks = new List<ClipCheck>();
            foreach (var clip in clips)
            {
                SetAction(clip.name);
                animator.speed = 0;
                var maxDrift = 0f;
                var rest = hips.localPosition;
                var first = rightHand.localToWorldMatrix;
                var changed = false;
                for (var i = 0; i <= 24; i++)
                {
                    elapsed = clip.length * i / 24f;
                    animator.Play(clip.name, 0, Mathf.Min(.999f, (float)i / 24));
                    animator.Update(0);
                    var p = hips.localPosition - rest;
                    maxDrift = Mathf.Max(maxDrift, new Vector2(p.x, p.z).magnitude);
                    changed |= (rightHand.position - first.GetColumn(3).ToVector3()).sqrMagnitude > .000001f;
                    yield return new WaitForEndOfFrame();
                    if (i == 14)
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(output, "assets-" + clip.name + ".png"), tex.EncodeToPNG());
                        Destroy(tex);
                    }
                }
                checks.Add(new ClipCheck { name = clip.name, seconds = clip.length, looping = clip.isLooping,
                    poseChanged = changed, hipXZDrift = maxDrift });
            }
            var renderers = actorRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            var materials = FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var missing = materials.Count(m => m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader");
            var textured = materials.Where(m => m.shader.name.Contains("glTF")).Count(m => m.GetTexturePropertyNames().Any(p => m.GetTexture(p) != null));
            var lidAngle = Mathf.DeltaAngle(0, lid.localEulerAngles.x);
            ResetDemo();
            var start = actorRoot.position;
            MoveActor(Vector3.right, .5f);
            var movementDistance = Vector3.Distance(start, actorRoot.position);
            var report = new Report { unity = Application.unityVersion, errors = errors, boneCount = renderers.Sum(r => r.bones.Length),
                materialCount = materials.Length, texturedImportedMaterials = textured, invalidMaterials = missing,
                lidAngle = lidAngle, movementDistance = movementDistance, clips = checks.ToArray(),
                // Pet, can and chest shell carry textures; chest lining is an authored solid material.
                passed = errors == 0 && renderers.Sum(r => r.bones.Length) == 22 && missing == 0 && textured == 3
                    && checks.Count == 4 && checks.All(c => c.poseChanged && c.hipXZDrift < .0001f)
                    && Mathf.Abs(lidAngle) > 70 && Mathf.Abs(movementDistance - .7f) < .0001f };
            File.WriteAllText(Path.Combine(output, "assets-runtime.json"), JsonUtility.ToJson(report, true));
            animator.speed = 1;
        }
        [Serializable] class ClipCheck { public string name; public float seconds; public bool looping, poseChanged; public float hipXZDrift; }
        [Serializable] class Report
        {
            public string unity; public int errors, boneCount, materialCount, texturedImportedMaterials, invalidMaterials;
            public float lidAngle, movementDistance; public ClipCheck[] clips; public bool passed;
        }
    }
    static class VectorConversion { public static Vector3 ToVector3(this Vector4 v) => new Vector3(v.x, v.y, v.z); }
}
