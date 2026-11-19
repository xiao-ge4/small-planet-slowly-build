using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace DawnGarden
{
    /// <summary>
    /// Owns only disposable presentation objects. The app owns modal input blocking and reward persistence.
    /// Call Update and Draw while IsActive; Finished fires once, after leaving the result screen and cleanup.
    /// </summary>
    public sealed partial class SmallWorldGuardianBattle : IDisposable
    {
        public bool IsActive { get; private set; }
        public GuardianBattleModel State { get; private set; }
        public bool UsesGuardianPrefab { get; private set; }
        public float PlayerPresentationHeight => VisibleRendererHeight(player);
        public float GuardianPresentationHeight => VisibleRendererHeight(guardian);
        public float GuardianGroundY { get; private set; }
        public float GuardianDefeatRawMinY { get; private set; } = float.NaN;
        public float GuardianDefeatMinY { get; private set; } = float.NaN;
        public float GuardianDefeatGroundCorrection { get; private set; }
        public event Action<GuardianBattleResult> Finished;
        public const string GuardianResourcePath = "Guardian/MossstoneGuardian";
        const float PlayerBeat = 1f, GuardianBeat = 1.15f, EntranceDuration = .5f;
        const float PlayerImpactTime = .34f, GuardianImpactTime = .40f;
        public float DisplayPlayerHealth { get; private set; }
        public float DisplayGuardianHealth { get; private set; }
        public int DisplayEnergy => State?.Energy ?? 0;
        public bool ImpactVisible => impactPresented && impactAge < .75f;
        public float ImpactAge => impactAge;
        public bool ResultReady => State != null && State.Result != GuardianBattleResult.None &&
            resultAge >= (State.Result == GuardianBattleResult.Victory ? 2.65f : State.Result == GuardianBattleResult.Defeat ? 1.65f : .55f);
        public string PresentationMessage => State == null ? "" : !impactPresented && State.Phase == GuardianBattlePhase.PlayerAction
            ? (State.LastAction == GuardianBattleAction.Guard ? "展开守护" : State.LastAction == GuardianBattleAction.Resonance ? "凝聚共鸣" : "向前出击")
            : !impactPresented && State.Phase == GuardianBattlePhase.GuardianAction && State.GuardianDamageDealt > 0 ? "守卫出招" : State.Message;
        bool impactPresented;
        float impactAge;
        const float PlayerAttackLunge = 1.7f, PlayerSkillLunge = .28f, GuardianAttackLunge = 2.1f;
        DawnGardenGame game;
        Camera camera;
        GameObject stage;
        Transform player, guardian, guardRing, resonanceRing;
        Animator playerAnimator, guardianAnimator;
        GuardianBattlePlayerPose playerPose;
        AudioSource feedback;
        AudioClip tapTone, guardTone, resonanceTone;
        Vector3 playerRest, guardianRest, cameraPosition;
        Quaternion cameraRotation;
        float fieldOfView, orthographicSize, nearClip, farClip, phaseAge, entranceAge, resultAge;
        bool cameraOrthographic, orbitEnabled, disposed;
        int lastInputFrame = -1;
        GuardianBattlePhase presentedPhase;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        readonly List<AudioClip> ownedSounds = new List<AudioClip>();
        readonly Dictionary<Renderer, bool> originalRendererVisibility = new Dictionary<Renderer, bool>();
        SkinnedMeshRenderer[] defeatSkins;
        Mesh[] defeatMeshes;
        List<Vector3>[] defeatVertices;
        bool defeatGroundingSubscribed, defeatGroundingSettled;
        int defeatGroundingFrame = -1;
        GUIStyle title, label, muted, button, panel, enemyTitle, largeNumber, command, resultTitle;
        GUIStyle floatingDamage;
        Font font;

        public bool Begin(DawnGardenGame owner, Font interfaceFont = null)
        {
            if (disposed || IsActive || owner == null || owner.actor == null || owner.worldCamera == null || owner.Busy) return false;
            game = owner;
            font = interfaceFont != null ? interfaceFont : owner.interfaceFont;
            camera = owner.worldCamera;
            cameraPosition = camera.transform.position;
            cameraRotation = camera.transform.rotation;
            fieldOfView = camera.fieldOfView; orthographicSize = camera.orthographicSize;
            cameraOrthographic = camera.orthographic; nearClip = camera.nearClipPlane; farClip = camera.farClipPlane;
            orbitEnabled = owner.orbit != null && owner.orbit.enabled;
            try
            {
                owner.CancelMovement();
                if (owner.orbit != null) owner.orbit.enabled = false;
                BuildStage();
                camera.orthographic = false; camera.fieldOfView = 38; camera.nearClipPlane = .1f; camera.farClipPlane = Mathf.Max(100, farClip);
                camera.transform.position = stage.transform.position + new Vector3(.1f, 5.6f, -11.4f);
                // Keep both full silhouettes inside the open strip between the intent card and command bar.
                camera.transform.LookAt(stage.transform.position + new Vector3(0, 1f, 0));
                State = new GuardianBattleModel();
                IsActive = true; phaseAge = resultAge = entranceAge = 0;
                presentedPhase = State.Phase; lastInputFrame = Time.frameCount;
                DisplayPlayerHealth = State.PlayerHealth; DisplayGuardianHealth = State.GuardianHealth;
                InitDirection(); PresentPhase();
                return true;
            }
            catch (Exception exception)
            {
                Cleanup();
                Debug.LogException(exception);
                return false;
            }
        }

        /// <summary>Public integration seam; duplicate same-frame input and animation-time input are ignored.</summary>
        public bool TryAct(GuardianBattleAction action)
        {
            if (!IsActive || entranceAge < EntranceDuration || lastInputFrame == Time.frameCount) return false;
            bool accepted = action == GuardianBattleAction.Retreat ? State.Retreat() : State.TryAct(action, State.Turn);
            if (!accepted) return false;
            lastInputFrame = Time.frameCount; phaseAge = resultAge = 0;
            PresentPhase();
            return true;
        }

        public bool SubmitAction(GuardianBattleAction action) => TryAct(action);

        public bool DismissResult()
        {
            if (!IsActive || State.Result == GuardianBattleResult.None || !ResultReady) return false;
            Close();
            return true;
        }

        public bool Retry() => Restart();

        public void Update(float unscaledDeltaTime)
        {
            if (!IsActive) return;
            if (game == null || camera == null || stage == null) { Close(); return; }
            float dt = float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) ? 0 : Mathf.Clamp(unscaledDeltaTime, 0, .15f);
            entranceAge += dt;
            phaseAge += dt;
            if (impactPresented) impactAge += dt;
            bool playerPhase = State.Phase == GuardianBattlePhase.PlayerAction;
            bool enemyPhase = State.Phase == GuardianBattlePhase.GuardianAction;
            if ((playerPhase || enemyPhase) && !State.ImpactResolved && phaseAge >= (playerPhase ? PlayerImpactTime : GuardianImpactTime))
            {
                if (State.ResolveImpact()) PresentImpact(playerPhase);
            }
            DisplayPlayerHealth = Mathf.MoveTowards(DisplayPlayerHealth, State.PlayerHealth, dt * 60);
            DisplayGuardianHealth = Mathf.MoveTowards(DisplayGuardianHealth, State.GuardianHealth, dt * 80);
            if (State.Result != GuardianBattleResult.None) resultAge += dt;
            else if ((State.Phase == GuardianBattlePhase.PlayerAction && phaseAge >= PlayerBeat)
                || (State.Phase == GuardianBattlePhase.GuardianAction && phaseAge >= GuardianBeat))
            {
                State.Advance(); phaseAge = 0;
                // Never consume a second decision from input that arrived on this transition frame.
                lastInputFrame = Time.frameCount;
            }
            if (presentedPhase != State.Phase) PresentPhase();
            UpdatePoses();
            TickDirection(dt);
            if (feedback != null) feedback.volume = game.atmosphere?.Muted == true ? 0 : (game.atmosphere?.MusicVolume ?? .35f) * .55f;
            var keyboard = Keyboard.current;
            if (keyboard == null || entranceAge < EntranceDuration) return;
            if (State.Result != GuardianBattleResult.None)
            {
                if (ResultReady && (keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)) Close();
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame) TryAct(GuardianBattleAction.Retreat);
            else if (keyboard.digit1Key.wasPressedThisFrame) TryAct(GuardianBattleAction.Attack);
            else if (keyboard.digit2Key.wasPressedThisFrame) TryAct(GuardianBattleAction.Guard);
            else if (keyboard.digit3Key.wasPressedThisFrame) TryAct(GuardianBattleAction.Resonance);
        }

        public bool Restart()
        {
            if (!IsActive || State.Result == GuardianBattleResult.None || State.Result == GuardianBattleResult.Victory || !ResultReady) return false;
            State = new GuardianBattleModel(); phaseAge = resultAge = entranceAge = 0;
            lastInputFrame = Time.frameCount; presentedPhase = State.Phase;
            DisplayPlayerHealth = State.PlayerHealth; DisplayGuardianHealth = State.GuardianHealth;
            PresentPhase(); UpdatePoses();
            return true;
        }

        public void Close()
        {
            if (!IsActive || (State.Result != GuardianBattleResult.None && !ResultReady)) return;
            if (State.Result == GuardianBattleResult.None) State.Retreat();
            var result = State.Result;
            Cleanup();
            Finished?.Invoke(result);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Cleanup();
            foreach (var texture in ownedTextures) if (texture != null) Object.Destroy(texture);
            ownedTextures.Clear(); Finished = null;
        }

        void Cleanup()
        {
            IsActive = false;
            DisposeDirection();
            ReleaseDefeatGrounding();
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                camera.orthographic = cameraOrthographic; camera.fieldOfView = fieldOfView; camera.orthographicSize = orthographicSize;
                camera.nearClipPlane = nearClip; camera.farClipPlane = farClip;
            }
            if (game != null && game.orbit != null) game.orbit.enabled = orbitEnabled;
            // Disable first: deferred Destroy must never render an abandoned stage for another frame.
            if (stage != null) { stage.SetActive(false); Object.Destroy(stage); }
            RestoreOriginalRenderers();
            foreach (var material in ownedMaterials) if (material != null) Object.Destroy(material);
            foreach (var sound in ownedSounds) if (sound != null) Object.Destroy(sound);
            ownedSounds.Clear(); feedback = null; tapTone = guardTone = resonanceTone = null; playerPose = null;
            ownedMaterials.Clear(); stage = null; player = guardian = guardRing = resonanceRing = null;
            playerAnimator = guardianAnimator = null; camera = null; game = null;
        }

        void BuildStage()
        {
            if (game.expansion == null || game.expansion.land == null)
                throw new InvalidOperationException("Guardian battle needs the authored expansion land for its stage.");
            stage = new GameObject("Guardian battle · temporary presentation");
            stage.SetActive(false);
            stage.transform.position = game.expansion.finalPosition;
            // Match the actor feet established by the one-time Idle fit; the coast skin sits just above navigation.
            GuardianGroundY = stage.transform.position.y + .04f;
            GuardianDefeatRawMinY = GuardianDefeatMinY = float.NaN;
            GuardianDefeatGroundCorrection = 0;
            // Copy only transform/mesh/renderer data. No navigation, physics, scene scripts or saved terrain state.
            var land = CopyScenery(game.expansion.land, stage.transform, null, true);
            land.name = "Existing meadow · presentation copy";
            land.localPosition = Vector3.zero; land.localRotation = Quaternion.identity;
            land.gameObject.SetActive(true);
            DecorateStage();
            player = CopyActor(game.actor.gameObject, "Player · presentation copy");
            player.localPosition = new Vector3(-1.65f, .04f, -.3f); player.localRotation = Quaternion.Euler(0, 72, 0);
            playerAnimator = player.GetComponentInChildren<Animator>(true);
            var guardianPrefab = Resources.Load<GameObject>(GuardianResourcePath);
            UsesGuardianPrefab = guardianPrefab != null;
            guardian = guardianPrefab != null ? CopyActor(guardianPrefab, "Mossstone Guardian · presentation copy") : BuildTemporaryGuardian();
            guardian.localPosition = new Vector3(1.65f, .04f, .25f); guardian.localRotation = Quaternion.Euler(0, -108, 0);
            guardianAnimator = guardian.GetComponentInChildren<Animator>(true);
            guardRing = MakeRing("Guard", new Color(.25f, .68f, .82f), .7f);
            resonanceRing = MakeRing("Resonance", new Color(.9f, .69f, .3f), .85f);
            guardRing.gameObject.SetActive(false); resonanceRing.gameObject.SetActive(false);
            feedback = stage.AddComponent<AudioSource>(); feedback.playOnAwake = false; feedback.spatialBlend = 0;
            tapTone = MakeTone("Guardian contact", 246.94f, .18f);
            guardTone = MakeTone("Guardian shield", 392f, .24f);
            resonanceTone = MakeTone("Guardian resonance", 659.25f, .38f);
            stage.SetActive(true);
            InitializePose(playerAnimator); InitializePose(guardianAnimator);
            // The established player has a different scaled Generic import hierarchy. Keep its verified
            // renderer-bounds sizing; current-pose baking is a guardian-specific calibration.
            FitActor(player, 1.8f);
            FitActor(guardian, 2.2f, UsesGuardianPrefab);
            playerRest = player.localPosition; guardianRest = guardian.localPosition;
            if (playerAnimator != null)
            {
                playerPose = player.gameObject.AddComponent<GuardianBattlePlayerPose>();
                playerPose.Bind(playerAnimator);
            }
            // Capture only after copying, so visible source meshes produce visible presentation copies.
            // Inactive tools and already-hidden layout parts retain their individual enabled states.
            HideOriginalRenderers(game.expansion.land);
            HideOriginalRenderers(game.actor);
        }

        void HideOriginalRenderers(Transform root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || originalRendererVisibility.ContainsKey(renderer)) continue;
                originalRendererVisibility.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }
        }

        void RestoreOriginalRenderers()
        {
            foreach (var entry in originalRendererVisibility)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            originalRendererVisibility.Clear();
        }

        static Transform CopyScenery(Transform source, Transform parent, Dictionary<Transform, Transform> links = null, bool skipSavedLayout = false)
        {
            var copy = new GameObject(source.name).transform;
            if (links != null) links.Add(source, copy);
            copy.SetParent(parent, false); copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation; copy.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var target = copy.gameObject.AddComponent<MeshRenderer>();
                target.sharedMaterials = renderer.sharedMaterials; target.enabled = renderer.enabled;
                target.shadowCastingMode = renderer.shadowCastingMode; target.receiveShadows = renderer.receiveShadows;
            }
            foreach (Transform child in source)
            {
                // A replay uses the same coastal surroundings while keeping saved furniture out of the arena.
                if (skipSavedLayout && (child.name == "Editable Home Layout" || child.name == "Layout preview staging")) continue;
                CopyScenery(child, copy, links, skipSavedLayout);
            }
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            return copy;
        }

        Transform CopyActor(GameObject source, string name)
        {
            // Rebuild only transforms/renderers/rigs. Never instantiate lifecycle scripts that might own
            // a shared glTF import, save data, navigation or audio, even for a single deferred-Destroy frame.
            var links = new Dictionary<Transform, Transform>();
            var copy = CopyScenery(source.transform, stage.transform, links); copy.name = name;
            foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var target = links[skin.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                target.sharedMesh = skin.sharedMesh; target.sharedMaterials = skin.sharedMaterials;
                target.localBounds = skin.localBounds; target.enabled = skin.enabled; target.updateWhenOffscreen = true;
                target.shadowCastingMode = skin.shadowCastingMode; target.receiveShadows = skin.receiveShadows;
                var bones = skin.bones; var mapped = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++) if (bones[i] != null && links.TryGetValue(bones[i], out var bone)) mapped[i] = bone;
                target.bones = mapped;
                if (skin.rootBone != null && links.TryGetValue(skin.rootBone, out var rootBone)) target.rootBone = rootBone;
                if (skin.sharedMesh != null)
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) target.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            }
            foreach (var sourceRig in source.GetComponentsInChildren<Animator>(true))
            {
                var rig = links[sourceRig.transform].gameObject.AddComponent<Animator>();
                rig.avatar = sourceRig.avatar; rig.runtimeAnimatorController = sourceRig.runtimeAnimatorController;
                rig.applyRootMotion = false; rig.cullingMode = AnimatorCullingMode.AlwaysAnimate; rig.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                if (t.name == "WateringGripSocket" || t.name == "WateringCanDisplay") t.gameObject.SetActive(false);
            copy.gameObject.SetActive(true);
            return copy;
        }

        static void InitializePose(Animator rig)
        {
            if (rig == null || rig.runtimeAnimatorController == null) return;
            if (rig.HasState(0, Animator.StringToHash("Idle"))) { rig.Play("Idle", 0, 0); rig.Update(0); }
        }

        void DecorateStage()
        {
            var assets = SmallWorldApp.Instance?.library?.assets;
            if (assets == null) return;
            AddScenery(assets, "moss-boulder", new Vector3(-2.5f, .03f, 1.35f), .56f, 22);
            AddScenery(assets, "moss-boulder", new Vector3(4.4f, .03f, 3.3f), .65f, 142);
            var flowers = new[] { new Vector3(-2.8f, .03f, -.85f), new Vector3(-.55f, .03f, 2.15f), new Vector3(.3f, .03f, 2.25f), new Vector3(2.8f, .03f, -.65f), new Vector3(1.9f, .03f, -1.8f) };
            for (int i = 0; i < flowers.Length; i++) AddScenery(assets, "meadow-flower", flowers[i], .28f + .025f * (i % 2), 37 * i);
        }

        void AddScenery(SmallWorldAsset[] assets, string id, Vector3 position, float height, float yaw)
        {
            var asset = Array.Find(assets, candidate => candidate != null && candidate.id == id && candidate.prefab != null);
            if (asset == null) return;
            var prop = CopyScenery(asset.prefab.transform, stage.transform);
            prop.localPosition = position; prop.localRotation = Quaternion.Euler(0, yaw, 0); prop.gameObject.SetActive(true);
            FitActor(prop, height);
        }

        AudioClip MakeTone(string name, float frequency, float duration)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate, u = time / duration;
                samples[i] = (Mathf.Sin(2 * Mathf.PI * frequency * time) + .18f * Mathf.Sin(4 * Mathf.PI * frequency * time))
                    * Mathf.Sin(Mathf.PI * u) * Mathf.Exp(-u * 5) * .32f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); ownedSounds.Add(clip); return clip;
        }

        void PlayFeedback(AudioClip clip)
        {
            if (feedback == null || clip == null || game?.atmosphere?.Muted == true) return;
            feedback.volume = (game?.atmosphere?.MusicVolume ?? .35f) * .55f;
            feedback.PlayOneShot(clip);
        }

        static bool RendererVisibleUnder(Renderer renderer, Transform root)
        {
            if (renderer == null || !renderer.enabled) return false;
            for (var t = renderer.transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == root) return true;
            }
            return false;
        }

        static float VisibleRendererHeight(Transform root)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return 0;
            bool found = false; Bounds bounds = default;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!RendererVisibleUnder(renderer, root)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found ? bounds.size.y : 0;
        }

        static void FitActor(Transform actor, float targetHeight, bool bakeGuardianPose = false)
        {
            var renderers = actor.GetComponentsInChildren<Renderer>(true);
            bool found = false; Bounds bounds = default;
            Mesh baked = null;
            try
            {
                foreach (var renderer in renderers)
                {
                    if (!RendererVisibleUnder(renderer, actor)) continue;
                    if (bakeGuardianPose && renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        // Guardian initialization only: its sampled Idle supplies actual feet, instead
                        // of padded bounds. Never apply this import-specific sizing to the existing player.
                        if (baked == null) baked = new Mesh();
                        skin.BakeMesh(baked, false);
                        var vertices = baked.vertices;
                        var toWorld = skin.localToWorldMatrix;
                        foreach (var vertex in vertices)
                        {
                            var point = toWorld.MultiplyPoint3x4(vertex);
                            if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                            else bounds.Encapsulate(point);
                        }
                    }
                    else if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            finally { if (baked != null) Object.Destroy(baked); }
            if (!found || bounds.size.y < .01f) return;
            float factor = targetHeight / bounds.size.y;
            actor.localScale *= factor;
            // Scaling is around the actor pivot, which can sit below or above the visible feet.
            float feet = (bounds.min.y - actor.position.y) * factor;
            actor.localPosition -= Vector3.up * feet;
        }

        Transform BuildTemporaryGuardian()
        {
            var root = new GameObject("Guardian · geometric rehearsal actor").transform;
            root.SetParent(stage.transform, false);
            var body = MakeMaterial(new Color(.39f, .47f, .39f));
            var seam = MakeMaterial(new Color(.62f, .73f, .42f));
            Primitive(root, "Rehearsal body", PrimitiveType.Cube, new Vector3(0, .95f, 0), new Vector3(1.25f, 1.4f, .85f), body);
            Primitive(root, "Rehearsal head", PrimitiveType.Cube, new Vector3(0, 1.9f, .08f), new Vector3(.9f, .6f, .8f), body);
            Primitive(root, "Left foot", PrimitiveType.Cube, new Vector3(-.37f, .18f, .05f), new Vector3(.5f, .36f, .7f), body);
            Primitive(root, "Right foot", PrimitiveType.Cube, new Vector3(.37f, .18f, .05f), new Vector3(.5f, .36f, .7f), body);
            Primitive(root, "Core", PrimitiveType.Sphere, new Vector3(0, 1.1f, .47f), Vector3.one * .35f, seam);
            return root;
        }

        static void Primitive(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(shape);
            primitive.name = name; primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position; primitive.transform.localScale = scale;
            var collider = primitive.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            primitive.GetComponent<Renderer>().sharedMaterial = material;
        }

        Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported material shader for guardian presentation.");
            var material = new Material(shader); material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            ownedMaterials.Add(material); return material;
        }

        Transform MakeRing(string name, Color color, float radius)
        {
            var root = new GameObject(name).transform; root.SetParent(stage.transform, false);
            var line = root.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = MakeMaterial(color); line.useWorldSpace = false; line.loop = true;
            line.positionCount = 48; line.widthMultiplier = .065f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(a), .08f, Mathf.Sin(a)) * radius); }
            return root;
        }

        static void Play(Animator rig, string state)
        {
            if (rig == null || rig.runtimeAnimatorController == null) return;
            if (!rig.HasState(0, Animator.StringToHash(state))) state = "Idle";
            if (rig.HasState(0, Animator.StringToHash(state))) rig.CrossFadeInFixedTime(state, .12f, 0, 0);
        }

        void PresentPhase()
        {
            presentedPhase = State.Phase; impactPresented = false; impactAge = 0;
            OnDirectionPhase();
            if (State.Phase == GuardianBattlePhase.PlayerAction)
            {
                Play(playerAnimator, State.LastAction == GuardianBattleAction.Guard ? "Guard" : State.LastAction == GuardianBattleAction.Resonance ? "Skill" : "Attack");
                Play(guardianAnimator, "Idle");
                playerPose?.Pose(State.LastAction == GuardianBattleAction.Guard ? "Guard" : State.LastAction == GuardianBattleAction.Resonance ? "Skill" : "Attack", PlayerBeat);
            }
            else if (State.Phase == GuardianBattlePhase.GuardianAction)
            {
                Play(guardianAnimator, State.Interrupted ? "Hit" : State.GuardianDamageDealt > 0 ? "Attack" : State.Intent == GuardianBattleIntent.Windup ? "Skill" : "Idle");
                Play(playerAnimator, State.LastAction == GuardianBattleAction.Guard ? "Guard" : "Idle");
                playerPose?.Pose(State.LastAction == GuardianBattleAction.Guard ? "Guard" : "Idle", GuardianBeat);
            }
            else
            {
                Play(playerAnimator, State.Result == GuardianBattleResult.Defeat ? "Defeat" : "Idle");
                Play(guardianAnimator, State.Result == GuardianBattleResult.Victory ? "Defeat" : "Idle");
                playerPose?.Pose(State.Result == GuardianBattleResult.Defeat ? "Defeat" : State.Result == GuardianBattleResult.Victory ? "Victory" : "Idle", 2.6f);
                if (State.Result == GuardianBattleResult.Victory) { BeginDefeatGrounding(); PlayFeedback(resonanceTone); }
            }
        }

        void PresentImpact(bool playerAttacks)
        {
            impactPresented = true; impactAge = 0;
            if (playerAttacks)
            {
                if (State.PlayerDamageDealt > 0) Play(guardianAnimator, "Hit");
                PlayFeedback(State.LastAction == GuardianBattleAction.Guard ? guardTone : State.LastAction == GuardianBattleAction.Resonance ? resonanceTone : tapTone);
            }
            else if (State.GuardianDamageDealt > 0)
            {
                bool guarded = State.LastAction == GuardianBattleAction.Guard;
                Play(playerAnimator, guarded ? "Guard" : "Hit");
                playerPose?.Pose(guarded ? "Guard" : "Hit", .55f);
                PlayFeedback(guarded ? guardTone : tapTone);
            }
            OnDirectionImpact(playerAttacks);
        }

        void BeginDefeatGrounding()
        {
            if (defeatGroundingSubscribed || guardian == null) return;
            defeatSkins = guardian.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            defeatMeshes = new Mesh[defeatSkins.Length];
            defeatVertices = new List<Vector3>[defeatSkins.Length];
            for (int i = 0; i < defeatSkins.Length; i++)
            {
                var skin = defeatSkins[i];
                if (skin.sharedMesh == null) continue;
                defeatMeshes[i] = new Mesh { name = "Guardian defeat grounding buffer" };
                defeatMeshes[i].MarkDynamic();
                defeatVertices[i] = new List<Vector3>(skin.sharedMesh.vertexCount);
            }
            defeatGroundingSettled = false; defeatGroundingFrame = -1;
            // URP invokes this after animation/LateUpdate and before culling, including Camera.Render captures.
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += GroundGuardianDefeat;
            defeatGroundingSubscribed = true;
        }

        void GroundGuardianDefeat(UnityEngine.Rendering.ScriptableRenderContext context, Camera renderCamera)
        {
            if (!IsActive || renderCamera != camera || guardian == null || State.Result != GuardianBattleResult.Victory) return;
            // Reconstruct from the immutable encounter rest pose every time. A second camera render or
            // the app's next Update can therefore never compound last frame's upward correction.
            guardian.localPosition = guardianRest;
            if (!defeatGroundingSettled && defeatGroundingFrame != Time.frameCount)
            {
                float minY = float.PositiveInfinity;
                for (int i = 0; i < defeatSkins.Length; i++)
                {
                    var skin = defeatSkins[i]; var mesh = defeatMeshes[i];
                    if (skin == null || mesh == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                    skin.BakeMesh(mesh, false);
                    var vertices = defeatVertices[i];
                    mesh.GetVertices(vertices);
                    var matrix = skin.localToWorldMatrix;
                    for (int j = 0; j < vertices.Count; j++)
                        minY = Mathf.Min(minY, matrix.MultiplyPoint3x4(vertices[j]).y);
                }
                if (!float.IsInfinity(minY) && !float.IsNaN(minY))
                {
                    GuardianDefeatRawMinY = minY;
                    GuardianDefeatGroundCorrection = Mathf.Max(0, GuardianGroundY - minY);
                    GuardianDefeatMinY = minY + GuardianDefeatGroundCorrection;
                }
                defeatGroundingFrame = Time.frameCount;
                if (guardianAnimator != null && !guardianAnimator.IsInTransition(0))
                {
                    var state = guardianAnimator.GetCurrentAnimatorStateInfo(0);
                    // Defeat is a one-shot clip. Its final pose is stable while the result UI remains open.
                    defeatGroundingSettled = state.shortNameHash == Animator.StringToHash("Defeat") && !state.loop && state.normalizedTime >= 1;
                }
            }
            guardian.position += Vector3.up * GuardianDefeatGroundCorrection;
        }

        void ReleaseDefeatGrounding()
        {
            if (defeatGroundingSubscribed)
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= GroundGuardianDefeat;
            defeatGroundingSubscribed = false;
            if (defeatMeshes != null)
                foreach (var mesh in defeatMeshes) if (mesh != null) Object.Destroy(mesh);
            defeatSkins = null; defeatMeshes = null; defeatVertices = null;
            defeatGroundingSettled = false; defeatGroundingFrame = -1;
        }

        void UpdatePoses()
        {
            if (player == null || guardian == null) return;
            player.localPosition = playerRest; guardian.localPosition = guardianRest;
            guardRing.gameObject.SetActive(State.Result == GuardianBattleResult.None && !State.CanAct && State.LastAction == GuardianBattleAction.Guard);
            resonanceRing.gameObject.SetActive(State.Phase == GuardianBattlePhase.PlayerAction && State.LastAction == GuardianBattleAction.Resonance);
            guardRing.localPosition = playerRest; resonanceRing.localPosition = guardianRest;
            resonanceRing.localScale = Vector3.one * (1 + .35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(phaseAge / PlayerBeat)));
            if (State.Phase == GuardianBattlePhase.PlayerAction && State.LastAction != GuardianBattleAction.Guard)
            {
                float movement = LungeEnvelope(phaseAge / PlayerBeat);
                var towardGuardian = guardianRest - playerRest; towardGuardian.y = 0; towardGuardian.Normalize();
                float distance = State.LastAction == GuardianBattleAction.Attack ? PlayerAttackLunge : PlayerSkillLunge;
                player.localPosition += towardGuardian * (movement * distance);
                guardian.localPosition += Vector3.right * ((impactPresented ? Mathf.Sin(impactAge * 31) : 0) * movement * .045f);
            }
            else if (State.Phase == GuardianBattlePhase.GuardianAction && State.GuardianDamageDealt > 0)
            {
                float movement = LungeEnvelope(phaseAge / GuardianBeat);
                var towardPlayer = playerRest - guardianRest; towardPlayer.y = 0; towardPlayer.Normalize();
                guardian.localPosition += towardPlayer * (movement * GuardianAttackLunge);
                player.localPosition += Vector3.left * ((impactPresented ? Mathf.Sin(impactAge * 28) : 0) * movement * .055f);
            }
        }

        static float LungeEnvelope(float normalizedTime)
        {
            float u = Mathf.Clamp01(normalizedTime);
            if (u < .35f) return Mathf.SmoothStep(0, 1, u / .35f);
            if (u < .55f) return 1;
            return Mathf.SmoothStep(1, 0, (u - .55f) / .45f);
        }

    }
}
