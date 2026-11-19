using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    // One authored guardian plot. Victory, permit debit and terrain ownership commit before animation.
    public sealed class GardenExpansion : MonoBehaviour
    {
        public DawnGardenGame game;
        public Transform land;
        public MeshCollider landCollider;
        public NavMeshSurface surface;
        public NavMeshData originalNavigation, expandedNavigation;
        public GameObject[] seamStones;
        public Material effectMaterial;
        public Color revealTint = new Color(.68f,1,.91f);
        public Vector3 finalPosition = new Vector3(7.5f, .012f, 0);
        public float duration = 6.6f;
        public float waterLevel = -.94f;
        public Vector2 revealHalfSize = new Vector2(3.25f, 2.75f);
        public string surroundingLabel = "海面";
        public bool IsRising { get; private set; }
        public bool IsGuardianRevealPending { get; private set; }
        public bool PromptOpen { get; private set; }
        public bool BagOpen { get; private set; }
        public bool IsGuardianChallengeActive => game != null && GardenGuardianLand.IsChallenging(game.Progress);
        public bool BlocksInput => IsRising || IsGuardianRevealPending || IsGuardianChallengeActive || PromptOpen || BagOpen;
        public event Action<string, bool> GuardianChallengeRequested;
        public float EffectTime { get; private set; }
        public Vector3 WalkTarget => finalPosition + new Vector3(.5f, .03f, 0);
        LineRenderer[] rings;
        Transform[] sparks;
        GameObject effects;
        string error;

        public void Restore(GardenProgress state)
        {
            GardenGuardianLand.Restore(state);
            IsRising = IsGuardianRevealPending = false; EffectTime = 0; BagOpen = false; error = null;
            game.orbit.EndExpansionView();
            // Restoring a normal save keeps the player's camera preference.
            game.orbit.distance = state.firstExpansionUnlocked ? game.orbit.expandedOverviewDistance : 15.5f;
            land.position = finalPosition; land.gameObject.SetActive(state.firstExpansionUnlocked);
            landCollider.enabled = state.firstExpansionUnlocked;
            SetSeam(!state.firstExpansionUnlocked);
            SwitchNavigation(state.firstExpansionUnlocked);
            if (effects != null) effects.SetActive(false);
            PromptOpen = state.expansionOfferPending && state.expansionPermits > 0 && !state.firstExpansionUnlocked;
        }
        public void ShowOffer()
        {
            if (game.isVisitor || game.Progress.firstExpansionUnlocked || game.Progress.expansionPermits < 1 || IsRising || IsGuardianChallengeActive || game.Busy) return;
            game.CancelMovement(); PromptOpen = true; BagOpen = false; error = null;
        }
        public void OpenBag()
        {
            if (game.isVisitor || game.Busy || IsGuardianRevealPending || IsGuardianChallengeActive) return;
            game.CancelMovement(); BagOpen = true; PromptOpen = false; error = null;
        }
        public bool Defer()
        {
            if (IsRising || IsGuardianRevealPending || IsGuardianChallengeActive) return false;
            var pending = game.Progress.expansionOfferPending;
            game.Progress.expansionOfferPending = false;
            if (!game.SaveNow()) { game.Progress.expansionOfferPending = pending; error = "暂存失败，请重试。许可仍然保留。"; return false; }
            PromptOpen = BagOpen = false;
            game.SetStatus("土地扩建许可已放入锦囊。在家园点击「锦囊」即可取出使用。");
            return true;
        }
        public bool Accept()
        {
            return StartGuardianChallenge(false);
        }
        public bool StartGuardianChallenge(bool replay = false)
        {
            if (game.isVisitor || IsRising || IsGuardianRevealPending || game.Busy || IsGuardianChallengeActive) return false;
            if (GuardianChallengeRequested == null)
            {
                error = "守卫试炼尚未准备好，许可仍保留。"; game.SetStatus(error); return false;
            }
            var before = new GardenGuardianLand.Checkpoint(game.Progress);
            if (!GardenGuardianLand.Begin(game.Progress, replay, out var encounterId)) return false;
            if (!game.SaveNow())
            {
                before.Apply(game.Progress); error = "保存失败，挑战尚未开始，许可没有扣除。请重试。"; return false;
            }
            game.CancelMovement(); PromptOpen = BagOpen = false; error = null;
            game.SetStatus(replay ? "守卫试炼：已有土地保持开放，本次不重复发放奖励。" : "挑战土地守卫；胜利后才消耗许可并正式占领。撤退或失败不会扣除许可。");
            GuardianChallengeRequested.Invoke(encounterId, replay);
            return true;
        }
        public bool CompleteGuardianChallenge(string encounterId, bool victory, bool deferReveal = false)
        {
            if (game.isVisitor) return false;
            var before = new GardenGuardianLand.Checkpoint(game.Progress);
            var result = GardenGuardianLand.Resolve(game.Progress, encounterId, victory);
            if (result == GardenGuardianLand.Resolution.Rejected) return false;
            if (result == GardenGuardianLand.Resolution.AlreadyResolved)
            {
                if (!deferReveal && victory && game.Progress.guardianOccupationId == encounterId) BeginGuardianReveal();
                return true;
            }
            if (!game.SaveNow())
            {
                before.Apply(game.Progress); error = "挑战结果尚未保存，许可和土地没有改变。请重试保存。"; game.SetStatus(error); return false;
            }
            PromptOpen = BagOpen = false; error = null;
            if (result != GardenGuardianLand.Resolution.Occupied)
            {
                game.SetStatus(result == GardenGuardianLand.Resolution.ReplayVictory ? "守卫试炼获胜！已有土地保持开放，不重复发放奖励。" : "已离开守卫试炼。已有土地和许可保持不变，可重新挑战。");
                return true;
            }
            // The win is durable before the result screen is dismissed. Only the
            // animation waits for its camera to be returned; reload restores owned land.
            IsGuardianRevealPending = true;
            if (deferReveal)
            {
                game.SetStatus("胜利与土地占领已保存。返回家园后，新土地将升起。");
                return true;
            }
            BeginGuardianReveal();
            return true;
        }
        void BeginGuardianReveal()
        {
            if (!IsGuardianRevealPending || IsRising) return;
            IsGuardianRevealPending = false;
            game.CancelMovement();
            IsRising = true; EffectTime = 0;
            land.position = finalPosition + Vector3.down * 3.4f;
            land.gameObject.SetActive(true); landCollider.enabled = false;
            EnsureEffects(); effects.SetActive(true);
            game.SetStatus($"守卫已击退，土地正式归你所有。许可化作微光…一片新的{game.terrainLabel}正在升起。");
        }
        void Update()
        {
            if (!IsRising)
            {
                if ((PromptOpen || BagOpen) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Defer();
                return;
            }
            EffectTime += Time.deltaTime;
            var u = Mathf.Clamp01((EffectTime - 1.1f) / 3.6f);
            var ease = u*u*u*(u*(u*6-15)+10);
            land.position = finalPosition + Vector3.down * (3.4f * (1-ease));
            if (u > .96f) SetSeam(false);
            var swell = Mathf.Sin(Mathf.PI * u);
            game.orbit.FrameExpansion(game.orbit.largeWorld ? finalPosition + Vector3.left * 2f : game.orbit.expandedOverviewCenter, .022f*swell);
            for (var r = 0; r < rings.Length; r++)
            {
                var phase = Mathf.Repeat(EffectTime*.43f + r*.31f, 1);
                var radius = 1 + phase*.37f;
                var alpha = (1-phase)*Mathf.Clamp01((duration-EffectTime)*1.4f);
                var tint=revealTint;tint.a=alpha*.8f;rings[r].startColor = rings[r].endColor = tint;
                rings[r].widthMultiplier = .025f + (1-phase)*.045f;
                for (var i=0;i<rings[r].positionCount;i++)
                {
                    var a=i*Mathf.PI*2/(rings[r].positionCount-1);
                    // Elliptical foam around the emerging land, always above the water.
                    rings[r].SetPosition(i,finalPosition + new Vector3(Mathf.Cos(a)*revealHalfSize.x*radius,waterLevel,Mathf.Sin(a)*revealHalfSize.y*radius));
                }
            }
            for (var i=0;i<sparks.Length;i++)
            {
                float a=i*2.39996f, t=Mathf.Repeat(EffectTime*.28f+i*.097f,1);
                sparks[i].position = finalPosition + new Vector3(Mathf.Cos(a)*(revealHalfSize.x*.75f+.45f*Mathf.Sin(i)), -1 + t*3.1f, Mathf.Sin(a)*revealHalfSize.y*.8f);
                var size=Mathf.Sin(t*Mathf.PI)*.06f*Mathf.Clamp01((duration-EffectTime)*2);
                sparks[i].localScale=Vector3.one*size;
            }
            if (EffectTime >= duration)
            {
                land.position = finalPosition; landCollider.enabled = true;
                Physics.SyncTransforms(); SwitchNavigation(true);
                IsRising = false; effects.SetActive(false); game.orbit.EndExpansionView();
                game.SetStatus($"扩建完成！新的{game.terrainLabel}已与家园相连，可以走上去看看。进度已保存。");
            }
        }
        void SetSeam(bool visible) { foreach (var stone in seamStones) if (stone != null) stone.SetActive(visible); }
        void SwitchNavigation(bool expanded)
        {
            var data=expanded ? expandedNavigation : originalNavigation;
            if (surface.navMeshData == data) return;
            var wasEnabled=game.navigator != null && game.navigator.enabled;
            if (wasEnabled) game.navigator.enabled=false;
            surface.RemoveData(); surface.navMeshData=data; surface.AddData();
            if (wasEnabled) { game.navigator.enabled=true; game.navigator.Warp(game.actor.position); }
        }
        void EnsureEffects()
        {
            if (effects != null) return;
            effects = new GameObject("Expansion seafoam and dawn motes"); effects.transform.SetParent(transform,false);
            rings = new LineRenderer[3];
            for (var i=0;i<rings.Length;i++)
            {
                var go=new GameObject("Sea ripple "+i); go.transform.SetParent(effects.transform,false);
                var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=effectMaterial; line.useWorldSpace=true;
                line.positionCount=97; line.numCapVertices=3; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[i]=line;
            }
            sparks=new Transform[42];
            for (var i=0;i<sparks.Length;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name="Dawn mote "+i;
                Destroy(go.GetComponent<Collider>()); go.transform.SetParent(effects.transform,false);
                go.GetComponent<Renderer>().sharedMaterial=effectMaterial;
                go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                sparks[i]=go.transform;
            }
        }
        public void DrawInterface(GUIStyle box, GUIStyle title, GUIStyle text, GUIStyle muted, GUIStyle button)
        {
            GUI.enabled = !game.Busy && !IsGuardianRevealPending && !IsGuardianChallengeActive && !PromptOpen && !BagOpen;
            if (SmallWorldApp.Instance==null && GUI.Button(new Rect(22,365,320,50), "锦囊  ·  土地扩建许可 × " + game.Progress.expansionPermits, button)) OpenBag();
            GUI.enabled = true;
            if (IsRising)
            {
                GUI.Box(new Rect(455,537,570,77), GUIContent.none, box);
                GUI.Label(new Rect(477,551,530,29), EffectTime < 1.1f ? $"微光正在唤醒这片{surroundingLabel}…" : "新的土地，正在升起", title);
                GUI.Label(new Rect(477,585,530,23), "土地稳固后即可踏入  ·  " + Mathf.RoundToInt(Mathf.Clamp01(EffectTime/duration)*100) + "%", muted);
                return;
            }
            if (!PromptOpen && !BagOpen) return;
            var previous=GUI.color; GUI.color=new Color(0,.05f,.06f,.68f);
            GUI.DrawTexture(new Rect(0,0,Screen.width/GUI.matrix.m00,Screen.height/GUI.matrix.m11),Texture2D.whiteTexture); GUI.color=previous;
            GUILayout.BeginArea(new Rect(385,214,510,338),box);
            GUILayout.BeginHorizontal();GUILayout.Label(SmallWorldUiArt.Icon("permit"),GUILayout.Width(54),GUILayout.Height(54));GUILayout.BeginVertical();GUILayout.Label(PromptOpen ? "发现新的可能" : "家园锦囊",title);GUILayout.Label("开拓许可 · 一张，一片新土地",muted);GUILayout.EndVertical();GUILayout.EndHorizontal();
            GUILayout.Space(6);
            var unlocked=game.Progress.firstExpansionUnlocked;
            GUILayout.Label(unlocked ? "第一片新土地已经开放。" : "土地扩建许可  × "+game.Progress.expansionPermits,text);
            GUILayout.Space(14);
            GUILayout.Label(unlocked ? $"你可以走上新{game.terrainLabel}，继续照料自己的小世界。" : game.Progress.expansionPermits > 0 ? "带着许可挑战相邻土地的守卫。\n获胜占领；失败或撤退，许可仍会保留。" : "开启家园补给箱，即可获得第一张土地扩建许可。",text);
            GUILayout.Space(18);
            if (!unlocked && game.Progress.expansionPermits > 0)
            {
                if (GUILayout.Button("出发，挑战守卫",button,GUILayout.Height(48))) Accept();
                var secondary=new GUIStyle(button);secondary.normal.background=SmallWorldUiArt.Plate(new Color(.88f,.90f,.80f),new Color(.65f,.72f,.54f));secondary.normal.textColor=SmallWorldUiArt.Ink;
                if (GUILayout.Button("先收好，稍后再出发",secondary,GUILayout.Height(43))) Defer();
            }
            else
            {
                if(unlocked && GUILayout.Button("设计这片新土地",button,GUILayout.Height(44))) HomeCreationPortal.Open(game);
                if(unlocked && GUILayout.Button("守卫试炼  ·  不重复奖励",button,GUILayout.Height(44))) StartGuardianChallenge(true);
                if (GUILayout.Button("收起锦囊",button,GUILayout.Height(44))) { BagOpen=false; PromptOpen=false; }
            }
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error,muted);
            GUILayout.EndArea();
        }
    }
}
