using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    public sealed class DawnGardenGame : MonoBehaviour
    {
        public Transform actor, gripSocket, nozzle, spawn;
        public CharacterController body;
        public NavMeshAgent navigator;
        public Animator animator;
        public AnimationClip[] clips;
        public GardenInteraction[] interactions;
        public Transform[] drops;
        public GardenOrbitCamera orbit;
        public GardenAtmosphere atmosphere;
        public GardenExpansion expansion;
        public Camera worldCamera;
        public Font interfaceFont;
        public LayerMask groundMask;
        public bool inputEnabled = true;
        public bool showHud = true;
        [NonSerialized] public bool externalControlsBlocked, dailyCare, isVisitor;
        public event Action<string> GameEvent;
        public float walkSpeed = 2.15f;
        [Header("Home identity")]
        public string homeId = "dawn-garden-starter-v1";
        public string homeTitle = "晨曦花园";
        public string homeSubtitle = "我的第一块土地 · 晨光与新芽";
        public string terrainLabel = "草地";

        public GardenProgress Progress { get; private set; }
        public string SavePath { get; private set; }
        public bool Busy => worldGesture || active != null || (expansion != null && expansion.IsRising);
        public bool ControlsBlocked => externalControlsBlocked || Busy || (expansion != null && expansion.BlocksInput);
        public bool Navigating => corners != null;
        public int LastPathCornerCount { get; private set; }
        public string Motion => motion;
        public float ActionTime => actionTime;
        public string Status { get; private set; } = "欢迎回家。先看看幼苗和补给箱吧。";
        GardenInteraction active, onArrival;
        Vector3[] corners;
        int cornerIndex;
        float actionTime, verticalSpeed, autosaveAt, stuckTime;
        string motion;
        bool initialized, saveBlocked, dirty;
        Vector3 lastPathPosition;
        Vector3 plantBaseScale;
        float growth;
        GUIStyle titleStyle, textStyle, mutedStyle, buttonStyle, smallStyle, boxStyle;
        Texture2D panelTexture;
        Font uiFont;

        void Start() { if (!initialized) Initialize(); }
        public void Initialize(string storagePath = null)
        {
            SavePath = storagePath ?? GardenSave.PathFor(homeId);
            saveBlocked = false;
            dirty = false;
            Status = "欢迎回家。先看看幼苗和补给箱吧。";
            try { Progress = GardenSave.Load(SavePath, homeId); }
            catch (Exception e)
            {
                Progress = new GardenProgress { homeId = homeId }; saveBlocked = true;
                Status = "存档读取失败，已保留原文件：" + e.Message;
                Debug.LogWarning(Status);
            }
            if (!initialized) plantBaseScale = interactions.First(i => i.kind == GardenInteractionKind.Water).visual.localScale;
            initialized = true;
            corners = null; active = onArrival = null;
            actionTime = 0; verticalSpeed = 0;
            gripSocket.gameObject.SetActive(false);
            if (navigator != null) navigator.enabled = false;
            if (expansion != null) expansion.Restore(Progress);
            var position = spawn.position;
            if (Progress.hasPosition && NavMesh.SamplePosition(Progress.position, out var saved, .7f, NavMesh.AllAreas)) position = saved.position;
            PlaceActor(position, Progress.hasPosition ? Progress.heading : 180f);
            SetMotion("Idle", true);
            growth = dailyCare ? Mathf.Min(3,Progress.wateringDays) : Progress.watered ? 1 : 0;
            ApplyVisuals(0);
            autosaveAt = Time.unscaledTime + 3;
        }
        public GardenInteraction FindInteraction(GardenInteractionKind kind) => interactions.First(i => i.kind == kind);
        public bool IsDone(GardenInteraction item) => item.kind == GardenInteractionKind.Water ? dailyCare ? GardenSave.WateredToday(Progress) : Progress.watered : Progress.chestOpened || isVisitor;
        public bool InRange(GardenInteraction item)
        {
            return FlatDistance(actor.position, item.transform.position) <= item.activationDistance
                && Mathf.Abs(actor.position.y - item.transform.position.y) < .8f;
        }
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        public bool GoToInteraction(GardenInteraction item, bool interactOnArrival = false)
        {
            if (ControlsBlocked) return false;
            if (interactOnArrival && IsDone(item)) { Status = "今天已经完成了。"; return false; }
            if (!WalkTo(item.approach.position)) return false;
            onArrival = interactOnArrival ? item : null;
            Status = "正在走向" + item.displayName + "…";
            return true;
        }
        public bool WalkTo(Vector3 destination)
        {
            if (ControlsBlocked) return false;
            if (!NavMesh.SamplePosition(actor.position, out var from, .8f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(destination, out var to, .65f, NavMesh.AllAreas))
            { Status = "那里暂时无法到达。"; return false; }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            { Status = "那里暂时没有可行走的路线。"; return false; }
            corners = path.corners; LastPathCornerCount = corners.Length;
            cornerIndex = corners.Length > 1 ? 1 : 0;
            onArrival = null; stuckTime = 0; lastPathPosition = actor.position;
            return true;
        }
        public bool TryInteract(GardenInteraction item)
        {
            if (ControlsBlocked || IsDone(item)) return false;
            if (!InRange(item)) { Status = "先走近" + item.displayName + "，再按 E。"; return false; }
            // Walk the last few steps to the authored contact point; never teleport into a pose.
            return GoToInteraction(item, true);
        }
        void BeginInteraction(GardenInteraction item)
        {
            if (!InRange(item) || IsDone(item)) return;
            active = item; actionTime = 0; corners = null; onArrival = null;
            var facing = item.transform.position - actor.position; facing.y = 0;
            if (facing.sqrMagnitude > .001f) actor.rotation = Quaternion.LookRotation(facing);
            SetMotion(item.kind == GardenInteractionKind.Water ? "Watering" : "Chest", true);
            gripSocket.gameObject.SetActive(item.kind == GardenInteractionKind.Water);
            Status = item.kind == GardenInteractionKind.Water ? "慢慢浇水，让新芽长大。" : "打开来自星球的第一份补给。";
        }
        void CompleteInteraction()
        {
            var wasChest = active.kind == GardenInteractionKind.Chest;
            var rewardedBefore = Progress.starterRewardGranted;
            var newWater=false;
            if (active.kind == GardenInteractionKind.Water) { if(dailyCare)newWater=GardenSave.WaterToday(Progress);else Progress.watered=true; }
            else Progress.chestOpened = true;
            GardenSave.GrantStarterReward(Progress);
            active = null; gripSocket.gameObject.SetActive(false);
            SetMotion("Idle", true);
            Status = wasChest && !rewardedBefore ? "获得土地扩建许可 × 1！可以立即使用，也可以放入锦囊。" : "完成了，家园又多了一点生机。";
            if (SaveNow() && expansion != null && Progress.expansionOfferPending) expansion.ShowOffer();
            GameEvent?.Invoke(wasChest?"chest":newWater?"water":"care");
        }
        void Update()
        {
            if (!initialized) return;
            var dt = Mathf.Min(Time.deltaTime, .05f);
            if (inputEnabled && !ControlsBlocked && Keyboard.current != null)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame) orbit.FocusPlayer();
                if (Keyboard.current.tabKey.wasPressedThisFrame) orbit.ToggleOverview();
            }
            if (active != null)
            {
                actionTime += dt;
                body.Move(Vector3.down * (2f * dt));
                if (actionTime >= clips.First(c => c.name == motion).length) CompleteInteraction();
            }
            else if (!ControlsBlocked)
            {
                var input = inputEnabled ? ReadInput() : Vector3.zero;
                if (input.sqrMagnitude > .001f)
                {
                    corners = null; onArrival = null;
                    MoveStep(input, dt);
                }
                else if (corners != null) FollowPath(dt);
                else { MoveStep(Vector3.zero, dt); SetMotion("Idle"); }
                if (inputEnabled) ReadActions();
            }
            if (Time.unscaledTime > autosaveAt) { autosaveAt = Time.unscaledTime + 3; if (dirty) SaveNow(); }
            // Homes can contain playable shorelines below world zero. Recover
            // below this home's sea, rather than teleporting out of its cave.
            float recoveryLevel = expansion == null ? -2 : Mathf.Min(-2, expansion.waterLevel - 2);
            if (actor.position.y < recoveryLevel)
            {
                CancelMovement();
                PlaceActor(spawn.position, 180);
                Status = "已回到家园入口。";
            }
        }
        Vector3 ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector3.zero;
            var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            var y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            var right = worldCamera.transform.right; right.y = 0; right.Normalize();
            var forward = worldCamera.transform.forward; forward.y = 0; forward.Normalize();
            return (right * x + forward * y).normalized;
        }
        void ReadActions()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    if(SmallWorldApp.Instance!=null&&SmallWorldApp.Instance.TryNearbyGuardian())return;
                    if(SmallWorldApp.Instance!=null&&SmallWorldApp.Instance.TryNearbyHomecomingLamp())return;
                    if(SmallWorldApp.Instance!=null&&SmallWorldApp.Instance.TryNearbyCoastStation())return;
                    if(SmallWorldApp.Instance!=null&&SmallWorldApp.Instance.TryNearbyWorldTarget())return;
                    var item = interactions.Where(InRange).OrderBy(i => FlatDistance(actor.position, i.transform.position)).FirstOrDefault(i => !IsDone(i));
                    if (item != null) TryInteract(item); else Status = "走近幼苗或补给箱，按 E 互动。";
                }
                if (keyboard.escapeKey.wasPressedThisFrame) { corners = null; onArrival = null; }
            }
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || PointerOverHud(mouse.position.ReadValue())) return;
            if (!Physics.Raycast(worldCamera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, worldCamera.farClipPlane, ~0, QueryTriggerInteraction.Ignore)) return;
            if(SmallWorldApp.Instance!=null&&SmallWorldApp.Instance.TryWorldTarget(hit.collider))return;
            var itemHit = hit.collider.GetComponentInParent<GardenInteraction>();
            if (itemHit != null) { GoToInteraction(itemHit, true); return; }
            if ((groundMask.value & (1 << hit.collider.gameObject.layer)) != 0) WalkTo(hit.point);
            else Status = $"点击{terrainLabel}移动，水域和石头不能通行。";
        }
        void FollowPath(float dt)
        {
            // Reach each corner before turning; cutting a corner early can cross an obstacle's nav boundary.
            var closeEnough = orbit.largeWorld ? .085f : .005f;
            while (cornerIndex < corners.Length && FlatDistance(actor.position, corners[cornerIndex]) < closeEnough) cornerIndex++;
            if (cornerIndex >= corners.Length)
            {
                var item = onArrival; corners = null; onArrival = null;
                SetMotion("Idle"); if (item != null) BeginInteraction(item); else Status = "到了。走近物件后按 E 互动。";
                return;
            }
            var delta = corners[cornerIndex] - actor.position; delta.y = 0;
            MoveStep(delta.normalized, Mathf.Min(dt, delta.magnitude / walkSpeed));
            if (FlatDistance(lastPathPosition, actor.position) < .002f) stuckTime += dt; else stuckTime = 0;
            lastPathPosition = actor.position;
            if (stuckTime > 1.8f) { corners = null; onArrival = null; SetMotion("Idle"); Status = "路线被挡住了，换个位置试试。"; }
        }
        public void MoveStep(Vector3 direction, float dt)
        {
            if (!body.enabled) return;
            direction.y = 0;
            var before = actor.position;
            var horizontal = direction.normalized * (walkSpeed * dt);
            if (horizontal.sqrMagnitude > 0 && navigator != null && navigator.isOnNavMesh)
            {
                // Agent.Move handles polygon edges and sliding around corners robustly;
                // CharacterController remains the authority for the visible root and collisions.
                navigator.nextPosition = before;
                navigator.Move(horizontal);
                horizontal = navigator.nextPosition - before; horizontal.y = 0;
            }
            else horizontal = Vector3.zero;
            verticalSpeed = body.isGrounded ? -2 : Mathf.Max(verticalSpeed - 18 * dt, -20);
            body.Move(horizontal + Vector3.up * (verticalSpeed * dt));
            if (navigator != null && navigator.isOnNavMesh) navigator.nextPosition = actor.position;
            var moved = actor.position - before; moved.y = 0;
            if (moved.sqrMagnitude > .000001f)
            {
                actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(moved), 1 - Mathf.Exp(-14 * dt));
                dirty = true; SetMotion("Jog");
            }
            else SetMotion("Idle");
        }
        void SetMotion(string name, bool restart = false)
        {
            if (motion == name && !restart) return;
            motion = name; animator.CrossFadeInFixedTime(name, .12f, 0, 0);
        }
        void LateUpdate() { if (initialized) ApplyVisuals(Time.deltaTime); }
        void ApplyVisuals(float dt)
        {
            var plant = FindInteraction(GardenInteractionKind.Water);
            var targetGrowth=dailyCare?Mathf.Min(3,Progress.wateringDays):Progress.watered?1:0;
            growth = Mathf.MoveTowards(growth, targetGrowth, dt * 1.3f);
            plant.visual.localScale = plantBaseScale * (dailyCare ? 1+.5f*growth : Mathf.Lerp(1, 1.5f, Mathf.SmoothStep(0, 1, growth)));
            var chest = FindInteraction(GardenInteractionKind.Chest);
            var opened = Progress.chestOpened ? 1 : active == chest ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, 1.05f, actionTime)) : 0;
            chest.lid.localRotation = Quaternion.Euler(-77.35f * opened, 0, 0);
            var pouring = active == plant && actionTime > .72f && actionTime < 3.35f;
            for (var i = 0; i < drops.Length; i++)
            {
                drops[i].gameObject.SetActive(pouring);
                if (!pouring) continue;
                var u = Mathf.Repeat(actionTime * 1.8f + (float)i / drops.Length, 1);
                var end = plant.transform.position + new Vector3(Mathf.Sin(i * 2.4f) * .2f, .18f, Mathf.Cos(i * 2.4f) * .2f);
                var p = Vector3.Lerp(nozzle.position, end, u); p.y = Mathf.Lerp(nozzle.position.y, end.y, u * u); drops[i].position = p;
            }
        }
        public bool SaveNow()
        {
            if (!initialized || saveBlocked) return false;
            Progress.hasPosition = true; Progress.position = actor.position; Progress.heading = actor.eulerAngles.y;
            try { GardenSave.Write(SavePath, Progress); dirty = false; return true; }
            catch (Exception e) { Status = "保存失败：" + e.Message; Debug.LogWarning(Status); return false; }
        }
        bool worldGesture;
        public System.Collections.IEnumerator SmallGesture(Action complete)
        {
            if(Busy)yield break;worldGesture=true;CancelMovement();SetMotion("Chest",true);
            yield return new WaitForSeconds(1.1f);SetMotion("Idle",true);worldGesture=false;complete?.Invoke();
        }
        public void CancelMovement() { corners = null; onArrival = null; SetMotion("Idle", true); }
        public void SetStatus(string message) { Status = message; }
        public void ReloadSaved() { Initialize(SavePath); }
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() { SaveNow(); }
        // Used by load/spawn and automated verification, never by normal interaction.
        public void PlaceActor(Vector3 position, float heading)
        {
            if (navigator != null) navigator.enabled = false;
            body.enabled = false; actor.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0)); body.enabled = true;
            if (navigator != null) { navigator.enabled = true; navigator.Warp(position); }
            Physics.SyncTransforms();
            // CharacterController reports grounded after its first Move. Nudge
            // a restored/spawned actor down once so the initial state is stable
            // for both gameplay and saved-position verification.
            body.Move(Vector3.down * .08f);
            verticalSpeed = 0;
        }
        public bool PointerOverHud(Vector2 screenPosition)
        {
            if (expansion != null && expansion.BlocksInput) return true;
            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var p = new Vector2(screenPosition.x / scale, (Screen.height - screenPosition.y) / scale);
            if(SmallWorldApp.Instance!=null)return SmallWorldApp.Instance.PointerOverUI(p)||(atmosphere!=null&&atmosphere.PointerOverHud(p));
            return new Rect(22, 20, 320, 330).Contains(p) || new Rect(22, 648, 1236, 56).Contains(p)
                || new Rect(1050, 20, 208, 125).Contains(p) || new Rect(22, 365, 320, 55).Contains(p) || (atmosphere != null && atmosphere.PointerOverHud(p));
        }
        void PrepareStyles()
        {
            if (titleStyle != null) return;
            uiFont = interfaceFont != null ? interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelTexture = new Texture2D(1, 1); panelTexture.SetPixel(0, 0, new Color(.09f, .17f, .16f, .92f)); panelTexture.Apply();
            boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(20, 20, 18, 16) };
            titleStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.96f, .91f, .73f) } };
            textStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 18, wordWrap = true, normal = { textColor = new Color(.93f, .95f, .89f) } };
            mutedStyle = new GUIStyle(textStyle) { fontSize = 16, normal = { textColor = new Color(.68f, .8f, .73f) } };
            smallStyle = new GUIStyle(textStyle) { fontSize = 15 };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 17, padding = new RectOffset(10, 10, 9, 9) };
            buttonStyle.normal.textColor=buttonStyle.hover.textColor=buttonStyle.active.textColor=buttonStyle.focused.textColor=new Color(.13f,.23f,.18f);
        }
        void OnGUI()
        {
            if (!initialized || !showHud || SmallWorldApp.Instance!=null) return;
            PrepareStyles();
            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(22, 20, 320, 330), boxStyle);
            GUILayout.Label(homeTitle, titleStyle);
            GUILayout.Label(homeSubtitle, mutedStyle);
            GUILayout.Space(15);
            GUILayout.Label(((dailyCare?GardenSave.WateredToday(Progress):Progress.watered) ? "✓" : "○") + "  为新芽浇一次水", textStyle);
            GUILayout.Label((Progress.chestOpened ? "✓" : "○") + "  打开第一份补给", textStyle);
            GUILayout.Label(isVisitor?"演示邻居 · 可帮助照料":"开拓许可  " + Progress.expansionPermits, textStyle);
            GUILayout.Space(8);
            GUI.enabled = !ControlsBlocked;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("前往幼苗", buttonStyle)) GoToInteraction(FindInteraction(GardenInteractionKind.Water));
            if (GUILayout.Button("前往补给箱", buttonStyle)) GoToInteraction(FindInteraction(GardenInteractionKind.Chest));
            GUILayout.EndHorizontal();
            var near = interactions.Where(InRange).FirstOrDefault(i => !IsDone(i));
            GUI.enabled = !ControlsBlocked && near != null;
            if (GUILayout.Button(near == null ? "走近物件后互动 [E]" : (near.kind == GardenInteractionKind.Water ? "浇水 [E]" : "开启补给箱 [E]"), buttonStyle) && near != null) TryInteract(near);
            GUI.enabled = true;
            GUILayout.EndArea();
            GUI.enabled = !ControlsBlocked;
            GUILayout.BeginArea(new Rect(1050, 20, 208, 125), boxStyle);
            if (GUILayout.Button(orbit.overview ? "跟随精灵 [Tab]" : "家园总览 [Tab]", buttonStyle)) orbit.ToggleOverview();
            if (GUILayout.Button("拉近精灵 [F]", buttonStyle)) orbit.FocusPlayer();
            GUILayout.EndArea();
            GUI.enabled = true;
            GUILayout.BeginArea(new Rect(22, 648, 1236, 56), boxStyle);
            GUILayout.Label(Status, textStyle);
            GUILayout.EndArea();
            GUI.Label(new Rect(365, 24, 650, 25), $"WASD 移动 · 点击{terrainLabel}寻路 · 右键旋转 · 滚轮缩放 · Esc 停止寻路", smallStyle);
            foreach (var item in interactions)
            {
                var p = worldCamera.WorldToScreenPoint(item.transform.position + Vector3.up * (item.kind == GardenInteractionKind.Water ? .8f : 1.05f));
                if (p.z <= 0) continue;
                GUI.Label(new Rect(p.x / scale - 65, (Screen.height - p.y) / scale - 12, 160, 30), IsDone(item) ? "✓ " + item.displayName : item.displayName, textStyle);
            }
            if (expansion != null) expansion.DrawInterface(boxStyle, titleStyle, textStyle, mutedStyle, buttonStyle);
            GUI.matrix = Matrix4x4.identity;
        }
        void OnDestroy() { if (panelTexture != null) Destroy(panelTexture); }
    }
}
