using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DawnGarden
{
    /// <summary>Encounter-only direction. Every visual, material and sampled pose belongs to the disposable stage.</summary>
    public sealed partial class SmallWorldGuardianBattle
    {
        sealed class DirectionChip
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Vector3 velocity, spin, scale;
            public float age, life;
        }

        sealed class DirectionSurface
        {
            public Renderer renderer;
            public int index;
            public Color baseColor;
            public MaterialPropertyBlock original, flash;
        }

        Transform directionRoot, directionShield, directionCore, directionFlash;
        LineRenderer directionShieldRim, directionCoreRim, directionTrail, directionImpactRays;
        MeshRenderer directionShieldFace;
        Transform[] directionMotes;
        Transform directionChest;
        GuardianBattleIntentPose directionIntentPose;
        Quaternion directionGuardianRotation, directionPlayerRotation;
        Material directionStoneMaterial, directionLeafMaterial, directionSparkMaterial, directionShieldMaterial;
        Mesh directionChipMesh, directionShieldMesh;
        readonly List<DirectionChip> directionChips = new List<DirectionChip>(30);
        readonly List<DirectionSurface> directionPlayerSurfaces = new List<DirectionSurface>();
        readonly List<DirectionSurface> directionGuardianSurfaces = new List<DirectionSurface>();
        readonly System.Random directionRandom = new System.Random(1847);
        float directionImpactAge = 10, directionImpactStrength, directionCorePulse, directionShieldPulse;
        float directionPlayerFlash, directionGuardianFlash, directionTrailAge = 10, directionDustAge;
        float directionSavedGuardianSpeed = 1;
        int directionChipCursor;
        bool directionReady, directionHolding, directionHitPlayer;
        Vector3 directionTrailStart, directionTrailEnd;

        void InitDirection()
        {
            if (stage == null || guardian == null || player == null) return;
            directionGuardianRotation = guardian.localRotation; directionPlayerRotation = player.localRotation;
            directionRoot = new GameObject("Battle direction · temporary effects").transform;
            directionRoot.SetParent(stage.transform, false);
            directionStoneMaterial = MakeMaterial(new Color(.64f, .57f, .43f));
            directionLeafMaterial = MakeMaterial(new Color(.47f, .64f, .25f));
            directionSparkMaterial = DirectionMaterial(new Color(1f, .84f, .42f, 1));
            directionShieldMaterial = DirectionMaterial(new Color(.49f, .85f, .73f, .13f));
            directionChipMesh = DirectionOctahedron();
            for (int i = 0; i < 30; i++)
            {
                var chip = DirectionMesh("Pooled stone / leaf " + i, directionRoot, directionChipMesh, directionStoneMaterial);
                chip.gameObject.SetActive(false);
                directionChips.Add(new DirectionChip { transform = chip, renderer = chip.GetComponent<MeshRenderer>() });
            }

            directionShield = new GameObject("Front-facing leaf shield").transform;
            directionShield.SetParent(directionRoot, false);
            directionShieldMesh = new Mesh { name = "Temporary leaf shield surface" };
            var shieldPoints = new[] { new Vector3(0, .62f), new Vector3(.43f, .36f), new Vector3(.39f, -.12f),
                new Vector3(0, -.59f), new Vector3(-.39f, -.12f), new Vector3(-.43f, .36f) };
            var shieldVertices = new Vector3[7];
            var shieldTriangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                shieldVertices[i + 1] = shieldPoints[i];
                shieldTriangles[i * 3] = 0; shieldTriangles[i * 3 + 1] = i + 1; shieldTriangles[i * 3 + 2] = (i + 1) % 6 + 1;
            }
            directionShieldMesh.vertices = shieldVertices; directionShieldMesh.triangles = shieldTriangles;
            directionShieldMesh.RecalculateNormals(); directionShieldMesh.RecalculateBounds();
            directionShieldFace = DirectionMesh("Translucent shield face", directionShield, directionShieldMesh, directionShieldMaterial).GetComponent<MeshRenderer>();
            directionShieldRim = DirectionLine("Shield rim", directionShield, new Color(.59f, .94f, .78f), .037f, 6, true);
            directionShieldRim.SetPositions(shieldPoints);
            var vein = DirectionLine("Shield leaf vein", directionShield, new Color(.73f, .96f, .83f), .021f, 5, false);
            vein.SetPositions(new[] { new Vector3(-.25f, .24f, -.006f), new Vector3(0, -.05f, -.006f), new Vector3(0, .42f, -.006f),
                new Vector3(0, -.05f, -.006f), new Vector3(.25f, .24f, -.006f) });

            directionCore = new GameObject("Chest stone · weak point").transform;
            directionCore.SetParent(directionRoot, false);
            directionCoreRim = DirectionLine("Chest stone outline", directionCore, new Color(1f, .81f, .35f), .027f, 32, true);
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                directionCoreRim.SetPosition(i, new Vector3(Mathf.Cos(angle) * .21f, Mathf.Sin(angle) * .24f, 0));
            }
            directionMotes = new Transform[3];
            for (int i = 0; i < directionMotes.Length; i++)
            {
                directionMotes[i] = DirectionMesh("Chest pollen " + i, directionCore, directionChipMesh, directionSparkMaterial);
                directionMotes[i].localScale = new Vector3(.035f, .065f, .025f);
            }
            directionTrail = DirectionLine("Directional resonance streak", directionRoot, new Color(.99f, .83f, .45f), .055f, 16, false);
            directionTrail.useWorldSpace = true;
            directionFlash = new GameObject("Contact flash").transform; directionFlash.SetParent(directionRoot, false);
            directionImpactRays = DirectionLine("Contact star", directionFlash, new Color(1f, .91f, .64f), .035f, 16, false);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                directionImpactRays.SetPosition(i * 2, Vector3.zero);
                directionImpactRays.SetPosition(i * 2 + 1, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (i % 2 == 0 ? .24f : .14f));
            }
            foreach (var bone in guardian.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "mixamorig:Spine2") directionChest = bone;
            }
            directionIntentPose = guardian.gameObject.AddComponent<GuardianBattleIntentPose>();
            directionIntentPose.Bind(guardian);
            DirectionCacheSurfaces(player, directionPlayerSurfaces);
            DirectionCacheSurfaces(guardian, directionGuardianSurfaces);
            // Keep the existing AudioSource and volume/mute path; replace only the encounter's generated tones.
            tapTone = DirectionContactSound(false); guardTone = DirectionContactSound(true);
            directionReady = true;
            OnDirectionPhase(); TickDirection(0);
        }

        void OnDirectionPhase()
        {
            if (!directionReady) return;
            DirectionReleasePose();
            guardian.localRotation = directionGuardianRotation; player.localRotation = directionPlayerRotation;
            if (State.Phase == GuardianBattlePhase.AwaitingAction || State.Phase == GuardianBattlePhase.Complete)
            {
                directionTrailAge = 10; directionShieldPulse = 0;
                if (State.Phase == GuardianBattlePhase.Complete)
                {
                    directionCorePulse = 0;
                    DirectionRestoreSurfaces(directionGuardianSurfaces); DirectionRestoreSurfaces(directionPlayerSurfaces);
                    directionGuardianFlash = directionPlayerFlash = 0;
                }
            }
            if (State.Phase == GuardianBattlePhase.AwaitingAction && State.Turn == 1)
            {
                directionImpactAge = 10; directionCorePulse = 0;
                foreach (var chip in directionChips) { chip.life = 0; chip.transform.gameObject.SetActive(false); }
            }
        }

        void TickDirection(float dt)
        {
            if (!directionReady || camera == null || State == null) return;
            directionImpactAge += dt; directionTrailAge += dt;
            directionCorePulse = Mathf.MoveTowards(directionCorePulse, 0, dt * 2.8f);
            directionShieldPulse = Mathf.MoveTowards(directionShieldPulse, 0, dt * 3.8f);
            // These old markers are replaced by the shield and chest effects above the ground.
            if (guardRing != null) guardRing.gameObject.SetActive(false);
            if (resonanceRing != null) resonanceRing.gameObject.SetActive(false);
            DirectionCamera();
            DirectionIntent(dt);
            DirectionShield();
            DirectionWeakPoint();
            DirectionResonanceTrail();
            DirectionChips(dt);
            DirectionFlashSurfaces(directionPlayerSurfaces, ref directionPlayerFlash, dt);
            DirectionFlashSurfaces(directionGuardianSurfaces, ref directionGuardianFlash, dt);
            bool showContact = directionImpactAge < .16f;
            directionFlash.gameObject.SetActive(showContact);
            if (showContact)
            {
                float t = directionImpactAge / .16f;
                directionFlash.rotation = Quaternion.LookRotation(camera.transform.position - directionFlash.position);
                directionFlash.localScale = Vector3.one * Mathf.Lerp(.35f, 1.55f, t) * directionImpactStrength;
                directionImpactRays.widthMultiplier = .035f * (1 - t);
            }
            // Root recoil is brief and starts at contact; native attack and defeat clips retain their ownership.
            if (State.Result == GuardianBattleResult.None && directionImpactAge < .22f)
            {
                float kick = Mathf.Sin(Mathf.PI * directionImpactAge / .22f) * directionImpactStrength;
                var actor = directionHitPlayer ? player : guardian;
                var away = directionHitPlayer ? playerRest - guardianRest : guardianRest - playerRest;
                away.y = 0; actor.localPosition += away.normalized * (kick * .09f);
            }
        }

        void DirectionCamera()
        {
            float entrance = Mathf.SmoothStep(0, 1, Mathf.Clamp01(entranceAge / .7f));
            float punch = directionImpactAge < .28f ? Mathf.Sin(Mathf.PI * directionImpactAge / .28f) * directionImpactStrength : 0;
            Vector3 at = stage.transform.position + new Vector3(0, 1.08f, .05f);
            Vector3 position = stage.transform.position + new Vector3(.1f, 3.45f, Mathf.Lerp(-9.85f, -9.15f, entrance));
            if (directionImpactAge < .18f)
            {
                float falloff = 1 - directionImpactAge / .18f;
                position += new Vector3(Mathf.Sin(directionImpactAge * 92), Mathf.Sin(directionImpactAge * 71) * .45f, 0) * (falloff * directionImpactStrength * .024f);
            }
            camera.fieldOfView = 35.5f - punch * .7f;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(at - position, Vector3.up));
        }

        void DirectionIntent(float dt)
        {
            if (!State.CanAct) return;
            if (State.Intent == GuardianBattleIntent.Probe) return;
            if (!directionHolding && guardianAnimator != null && guardianAnimator.runtimeAnimatorController != null)
            {
                // Idle leaves the chest open. Guard's crossed forearms hide the weak-point cue.
                string pose = State.Intent == GuardianBattleIntent.Exposed ? "Idle" : "Skill";
                if (guardianAnimator.HasState(0, Animator.StringToHash(pose)))
                {
                    directionSavedGuardianSpeed = guardianAnimator.speed;
                    guardianAnimator.speed = 1;
                    guardianAnimator.Play(pose, 0, State.Intent == GuardianBattleIntent.HeavyStrike ? .42f : .56f);
                    guardianAnimator.Update(0); guardianAnimator.speed = 0; directionHolding = true;
                }
            }
            directionIntentPose.SetExposed(State.Intent == GuardianBattleIntent.Exposed);
            float lean = State.Intent == GuardianBattleIntent.Exposed ? 7 : State.Intent == GuardianBattleIntent.HeavyStrike ? -7 : -3.5f;
            bool exposed = State.Intent == GuardianBattleIntent.Exposed;
            // A small turn shows the chest stone while retaining the opponent-facing stance.
            guardian.localRotation = directionGuardianRotation * Quaternion.Euler(lean, exposed ? -28 : 0, exposed ? -3 : 0);
            bool heavy = State.Intent == GuardianBattleIntent.HeavyStrike;
            // Sparse flecks near the hands/chest make stored force legible without a full-screen aura.
            if (State.Intent == GuardianBattleIntent.Windup || heavy)
            {
                directionDustAge += dt;
                if (directionDustAge > (heavy ? .36f : .65f))
                {
                    directionDustAge = 0;
                    DirectionEmit(DirectionChestPosition() + Vector3.down * .25f, Vector3.up * .35f, heavy ? 2 : 1, .45f, false);
                }
            }
        }

        void DirectionReleasePose()
        {
            if (directionIntentPose != null) directionIntentPose.SetExposed(false);
            if (directionHolding && guardianAnimator != null) guardianAnimator.speed = directionSavedGuardianSpeed;
            directionHolding = false; directionDustAge = 0;
        }

        Vector3 DirectionChestPosition()
        {
            Vector3 center = directionChest != null ? directionChest.position : guardian.position + Vector3.up * 1.25f;
            Vector3 front = guardian.forward; front.y = 0;
            Vector3 view = camera.transform.position - center; view.y = 0;
            // Spine2 is inside the torso. Place the small cue on the visible chest surface;
            // normal depth testing still lets hands, scenery and the body occlude it naturally.
            Vector3 surface = (front.normalized * .65f + view.normalized * .35f).normalized;
            return center + surface * .43f + Vector3.up * .025f;
        }

        void DirectionShield()
        {
            bool visible = State.Result == GuardianBattleResult.None && !State.CanAct && State.LastAction == GuardianBattleAction.Guard;
            directionShield.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 toward = guardian.position - player.position; toward.y = 0; toward.Normalize();
            Vector3 normal = (toward + (camera.transform.position - player.position).normalized * .7f).normalized;
            directionShield.position = player.position + Vector3.up * 1.02f + toward * .46f;
            directionShield.rotation = Quaternion.LookRotation(normal, Vector3.up);
            float growth = State.Phase == GuardianBattlePhase.PlayerAction ? Mathf.SmoothStep(.2f, 1, Mathf.Clamp01(phaseAge / .18f)) : 1;
            directionShield.localScale = Vector3.one * growth * (1 + directionShieldPulse * .16f);
            directionShieldRim.widthMultiplier = .037f + directionShieldPulse * .035f;
            directionShieldMaterial.color = new Color(.49f, .85f, .73f, .13f + directionShieldPulse * .22f);
        }

        void DirectionWeakPoint()
        {
            bool exposed = State.Result == GuardianBattleResult.None && State.Intent == GuardianBattleIntent.Exposed;
            bool visible = exposed || directionCorePulse > 0;
            directionCore.gameObject.SetActive(visible);
            if (!visible) return;
            directionCore.position = DirectionChestPosition();
            directionCore.rotation = Quaternion.LookRotation(camera.transform.position - directionCore.position, Vector3.up);
            float pulse = 1 + Mathf.Sin(entranceAge * 3.4f) * .065f + directionCorePulse * .34f;
            directionCore.localScale = Vector3.one * pulse;
            directionCoreRim.widthMultiplier = .027f + directionCorePulse * .025f;
            for (int i = 0; i < directionMotes.Length; i++)
            {
                float angle = entranceAge * .8f + i * Mathf.PI * 2 / 3;
                directionMotes[i].localPosition = new Vector3(Mathf.Cos(angle) * .3f, Mathf.Sin(angle) * .32f, -.02f);
                directionMotes[i].localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }
        }

        void DirectionResonanceTrail()
        {
            bool launch = State.Phase == GuardianBattlePhase.PlayerAction && State.LastAction == GuardianBattleAction.Resonance && phaseAge >= .12f && phaseAge < PlayerImpactTime;
            bool after = directionTrailAge < .18f;
            directionTrail.gameObject.SetActive(launch || after);
            if (!launch && !after) return;
            Vector3 from = launch ? player.position + Vector3.up * 1.08f : directionTrailStart;
            Vector3 to = launch ? DirectionChestPosition() : directionTrailEnd;
            float progress = launch ? Mathf.Clamp01((phaseAge - .12f) / Mathf.Max(.01f, PlayerImpactTime - .12f)) : 1;
            float tail = after ? Mathf.Clamp01(directionTrailAge / .18f) : Mathf.Max(0, progress - .5f);
            for (int i = 0; i < 16; i++)
            {
                float u = Mathf.Lerp(tail, progress, i / 15f);
                Vector3 point = Vector3.Lerp(from, to, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * .17f);
                directionTrail.SetPosition(i, point);
            }
            directionTrail.widthMultiplier = after ? .08f * (1 - tail) : .055f;
        }

        void OnDirectionImpact(bool playerAttacks)
        {
            if (!directionReady || State == null) return;
            bool guard = State.LastAction == GuardianBattleAction.Guard;
            if (playerAttacks && guard) { directionShieldPulse = .65f; return; }
            if (!playerAttacks && State.GuardianDamageDealt <= 0) return;
            bool resonance = playerAttacks && State.LastAction == GuardianBattleAction.Resonance;
            bool strong = resonance || (!playerAttacks && State.Intent == GuardianBattleIntent.HeavyStrike);
            directionImpactAge = 0; directionHitPlayer = !playerAttacks;
            directionImpactStrength = strong ? (State.HitWeakPoint ? 1.45f : 1.15f) : .72f;
            Vector3 toward = (guardian.position - player.position).normalized;
            Vector3 position = playerAttacks ? DirectionChestPosition() : player.position + Vector3.up * 1.05f + toward * (guard ? .45f : .12f);
            directionFlash.position = position;
            DirectionEmit(position, (playerAttacks ? toward : -toward) * (strong ? 1.5f : 1), strong ? 13 : 7, strong ? .75f : .5f, playerAttacks);
            if (playerAttacks) directionGuardianFlash = .15f; else directionPlayerFlash = guard ? .07f : .13f;
            if (!playerAttacks && guard) directionShieldPulse = 1;
            if (resonance)
            {
                directionCorePulse = 1; directionTrailAge = 0;
                directionTrailStart = player.position + Vector3.up * 1.08f; directionTrailEnd = position;
            }
        }

        void DirectionEmit(Vector3 point, Vector3 velocity, int count, float life, bool stone)
        {
            for (int i = 0; i < count; i++)
            {
                var chip = directionChips[directionChipCursor++ % directionChips.Count];
                chip.age = 0; chip.life = life * DirectionRandom(.65f, 1);
                chip.velocity = velocity + new Vector3(DirectionRandom(-.65f, .65f), DirectionRandom(.35f, 1.3f), DirectionRandom(-.65f, .65f));
                chip.spin = new Vector3(DirectionRandom(-160, 160), DirectionRandom(-160, 160), DirectionRandom(-160, 160));
                bool leaf = i % 3 == 1;
                float size = DirectionRandom(.035f, .075f);
                chip.scale = new Vector3(size, size * (leaf ? 1.7f : 1), size * (leaf ? .22f : .75f));
                chip.renderer.sharedMaterial = leaf ? directionLeafMaterial : stone && i % 3 != 2 ? directionStoneMaterial : directionSparkMaterial;
                chip.transform.position = point + new Vector3(DirectionRandom(-.08f, .08f), DirectionRandom(-.08f, .08f), DirectionRandom(-.08f, .08f));
                chip.transform.rotation = Quaternion.Euler(chip.spin); chip.transform.localScale = chip.scale;
                chip.transform.gameObject.SetActive(true);
            }
        }

        float DirectionRandom(float min, float max) => Mathf.Lerp(min, max, (float)directionRandom.NextDouble());

        void DirectionChips(float dt)
        {
            foreach (var chip in directionChips)
            {
                if (chip.life <= 0) continue;
                chip.age += dt;
                if (chip.age >= chip.life) { chip.life = 0; chip.transform.gameObject.SetActive(false); continue; }
                chip.velocity += Vector3.down * (dt * 3.2f);
                chip.transform.position += chip.velocity * dt;
                chip.transform.Rotate(chip.spin * dt, Space.Self);
                chip.transform.localScale = chip.scale * Mathf.Clamp01((chip.life - chip.age) / .13f);
            }
        }

        static void DirectionCacheSurfaces(Transform actor, List<DirectionSurface> surfaces)
        {
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original, i);
                    var flash = new MaterialPropertyBlock(); renderer.GetPropertyBlock(flash, i);
                    Color color = materials[i].HasProperty("_BaseColor") ? materials[i].GetColor("_BaseColor")
                        : materials[i].HasProperty("_BaseColorFactor") ? materials[i].GetColor("_BaseColorFactor")
                        : materials[i].HasProperty("_Color") ? materials[i].GetColor("_Color") : Color.white;
                    surfaces.Add(new DirectionSurface { renderer = renderer, index = i, original = original, flash = flash, baseColor = color });
                }
            }
        }

        static void DirectionFlashSurfaces(List<DirectionSurface> surfaces, ref float age, float dt)
        {
            if (age <= 0) return;
            age = Mathf.Max(0, age - dt);
            if (age <= 0) { DirectionRestoreSurfaces(surfaces); return; }
            foreach (var surface in surfaces)
            {
                if (surface.renderer == null) continue;
                Color color = Color.Lerp(surface.baseColor, new Color(1f, .92f, .71f, surface.baseColor.a), Mathf.Clamp01(age / .15f) * .72f);
                surface.flash.SetColor("_BaseColor", color); surface.flash.SetColor("_Color", color); surface.flash.SetColor("_BaseColorFactor", color);
                surface.renderer.SetPropertyBlock(surface.flash, surface.index);
            }
        }

        static void DirectionRestoreSurfaces(List<DirectionSurface> surfaces)
        {
            foreach (var surface in surfaces) if (surface.renderer != null) surface.renderer.SetPropertyBlock(surface.original, surface.index);
        }

        Material DirectionMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var material = shader != null ? new Material(shader) : MakeMaterial(color);
            if (shader != null) ownedMaterials.Add(material);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (color.a < 1)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Cull", (float)CullMode.Off); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
            }
            return material;
        }

        LineRenderer DirectionLine(string name, Transform parent, Color color, float width, int count, bool loop)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = DirectionMaterial(color);
            line.useWorldSpace = false; line.positionCount = count; line.loop = loop; line.widthMultiplier = width;
            line.numCornerVertices = 2; line.numCapVertices = 2; line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }

        static Transform DirectionMesh(string name, Transform parent, Mesh mesh, Material material)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return root.transform;
        }

        static Mesh DirectionOctahedron()
        {
            var mesh = new Mesh { name = "Pooled temporary leaf / stone shard" };
            mesh.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            mesh.triangles = new[] { 0, 4, 3, 0, 2, 4, 0, 5, 2, 0, 3, 5, 1, 3, 4, 1, 4, 2, 1, 2, 5, 1, 5, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        AudioClip DirectionContactSound(bool shield)
        {
            const int rate = 22050;
            float duration = shield ? .23f : .18f;
            var samples = new float[(int)(duration * rate)];
            var noise = new System.Random(shield ? 71 : 37);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2 - 1, .22f);
                float strike = filtered * Mathf.Exp(-t * (shield ? 55 : 68)) * .66f;
                float body = Mathf.Sin(t * Mathf.PI * 2 * (shield ? 315 : 122)) * Mathf.Exp(-t * (shield ? 23 : 35)) * .23f;
                samples[i] = (strike + body) * Mathf.Min(1, t / .002f);
            }
            var clip = AudioClip.Create(shield ? "Leaf shield · short knock" : "Stone · short contact", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); ownedSounds.Add(clip); return clip;
        }

        void DisposeDirection()
        {
            if (!directionReady && directionRoot == null) return;
            DirectionReleasePose();
            if (guardian != null) guardian.localRotation = directionGuardianRotation;
            if (player != null) player.localRotation = directionPlayerRotation;
            DirectionRestoreSurfaces(directionPlayerSurfaces); DirectionRestoreSurfaces(directionGuardianSurfaces);
            directionPlayerSurfaces.Clear(); directionGuardianSurfaces.Clear(); directionChips.Clear();
            if (directionRoot != null) { directionRoot.gameObject.SetActive(false); Object.Destroy(directionRoot.gameObject); }
            if (directionChipMesh != null) Object.Destroy(directionChipMesh);
            if (directionShieldMesh != null) Object.Destroy(directionShieldMesh);
            directionRoot = directionChest = null; directionIntentPose = null;
            directionChipMesh = directionShieldMesh = null; directionReady = false;
        }
    }
}
