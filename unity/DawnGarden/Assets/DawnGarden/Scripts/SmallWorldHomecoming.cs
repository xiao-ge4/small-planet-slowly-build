using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        const string HomecomingGift = "ajin-lantern";
        const string HomecomingRequest = "请用现有物件围绕草稿里的阿烬暖焰灯布置一个小小的归家纪念角，可添一张矮凳和一丛白花。保留礼物和其他物件的位置、标识，不挡连接小路。只修改摆放，直接给我预览。";
        enum JourneyStep { Supply, Expand, Visit, Meet, Care, Find, Warm, Gift, Home, Arrange, Light, Complete }
        bool titleOpen, titleThemes, titleHelp, homecomingJournal, homecomingNight, walkingToLamp, stagingHomecoming;
        Texture2D coverTexture, coverShade;
        readonly List<Texture2D> homecomingTextures = new();
        GUIStyle coverTitle, coverSubtitle, coverPill, hudPill, hudTitle, hudNote;
        Transform lampAnchor;
        DawnCoast coast;
        SmallWorldWarmLantern homecomingLantern;
        Light homecomingGlow => homecomingLantern != null ? homecomingLantern.Glow : null;

        bool JourneyAvailable => Profile.theme == "dawn" && Game != null;
        Gift AjinGift => neighborWorld.gifts?.FirstOrDefault(g => g.id == HomecomingGift);
        SmallWorldItem CommittedLamp => committedLayout.instances?.FirstOrDefault(i => i.giftId == HomecomingGift);

        JourneyStep Journey
        {
            get
            {
                if (IsVisitor)
                {
                    var resident = CurrentResident;
                    if (resident?.id != "ajin" || resident.giftClaimed) return JourneyStep.Home;
                    if (!residentReady || !resident.questAccepted) return JourneyStep.Meet;
                    if (resident.wateringDays == 0) return JourneyStep.Care;
                    if (resident.collected.Length < 3) return JourneyStep.Find;
                    return resident.ritualComplete ? JourneyStep.Gift : JourneyStep.Warm;
                }
                if (!Game.Progress.chestOpened) return JourneyStep.Supply;
                if (!Game.Progress.firstExpansionUnlocked) return JourneyStep.Expand;
                if (AjinGift == null) return JourneyStep.Visit;
                if (CommittedLamp == null) return JourneyStep.Arrange;
                return Profile.homecomingLampLit ? JourneyStep.Complete : JourneyStep.Light;
            }
        }

        static string JourneyHint(JourneyStep step) => step switch
        {
            JourneyStep.Supply => "打开补给箱，拿到第一张开拓许可。",
            JourneyStep.Expand => "挑战苔岩守卫，获胜后使用许可占领新土地。",
            JourneyStep.Visit => "打开地图，拜访同星球邻居阿烬。",
            JourneyStep.Meet => "走近阿烬，聊聊温泉边的委托。",
            JourneyStep.Care => "给暖焰芽浇一回水。",
            JourneyStep.Find => "找齐散在小路边的三颗温润石。",
            JourneyStep.Warm => "去炉边和阿烬一起温热暖石。",
            JourneyStep.Gift => "和阿烬分享找到的暖石，收下礼物。",
            JourneyStep.Home => "回晨曦花园，把这次旅行留在家里。",
            JourneyStep.Arrange => "请小禾把暖焰灯布置到新土地，再应用预览。",
            JourneyStep.Light => "入夜后，走近暖焰灯，把朋友送来的光点亮。",
            _ => "回家的人，远远就能看到这盏灯。"
        };

        void ResetHomecomingScene(DawnGardenGame game)
        {
            homecomingJournal = homecomingNight = walkingToLamp = false;
            lampAnchor = null;
            homecomingLantern = null;
            coast = null;
            if (IsVisitor || game.homeId != "dawn-garden-starter-v1") return;
            coast = new GameObject("晨曦花园 · 海岸六区").AddComponent<DawnCoast>();
            coast.Build(game, library);
        }

        void UpdateHomecoming()
        {
            if (!JourneyAvailable || IsVisitor || loading || restoringOwnedLayout) return;
            if (editorOpen || editorBusy) return;
            var lamp = CommittedLamp;
            var layoutAnchor = lamp == null || placedRoot == null ? null : placedRoot.Find(lamp.instanceId);
            if (layoutAnchor == null)
            {
                coast?.RemoveGift();
                lampAnchor = null;
                homecomingLantern = null;
            }
            else if (homecomingLantern == null)
            {
                homecomingLantern = coast != null && !storyActive && playableLand == null ? coast.InstallGift(lamp) : layoutAnchor.GetComponent<SmallWorldWarmLantern>();
                lampAnchor = homecomingLantern != null ? homecomingLantern.transform : null;
            }
            homecomingLantern?.UpdateLight(Profile.homecomingLampLit && Game.atmosphere.Daylight < .55f);
            if (walkingToLamp && !Game.Navigating)
            {
                walkingToLamp = false;
                if (NearLamp()) TryNearbyHomecomingLamp();
                else Toast("沿着小路靠近暖焰灯，再按 E。");
            }
        }

        bool NearLamp()
        {
            if (lampAnchor == null) return false;
            var offset = Game.actor.position - lampAnchor.position;
            if (Mathf.Abs(offset.y) > .82f) return false;
            offset.y = 0;
            return offset.sqrMagnitude <= 2.1f * 2.1f;
        }

        public bool TryNearbyHomecomingLamp()
        {
            if (!JourneyAvailable || IsVisitor || Journey != JourneyStep.Light || !NearLamp()) return false;
            if (Game.atmosphere.Daylight >= .4f) Toast("暖焰灯入夜后可以点亮。在手记里可以等到入夜。");
            else StartCoroutine(LightHomecomingLamp());
            return true;
        }

        void ApproachLamp()
        {
            if (lampAnchor == null) { Toast("等暖焰灯载入后再试。"); return; }
            if (NearLamp()) { TryNearbyHomecomingLamp(); return; }
            var path = new NavMeshPath();
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                var goal = lampAnchor.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.45f;
                if (!NavMesh.SamplePosition(goal, out var sample, .32f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(Game.actor.position, sample.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (!Game.WalkTo(sample.position)) continue;
                Game.orbit.FocusPlayer();
                if (coast != null && !storyActive && playableLand == null) FrameGrottoEntrance();
                walkingToLamp = true;
                return;
            }
            Toast("请从小路靠近暖焰灯，再按 E。");
        }

        IEnumerator LightHomecomingLamp()
        {
            if (Game.Busy || !NearLamp() || Profile.homecomingLampLit || Game.atmosphere.Daylight >= .4f) yield break;
            if (coast != null && !storyActive && playableLand == null) FrameGrottoEntrance();
            yield return Game.SmallGesture(() =>
            {
                Profile.homecomingLampLit = true;
                try { Profile.Save(); }
                catch (IOException)
                {
                    Profile.homecomingLampLit = false;
                    Toast("点灯进度还没有保存，请再试一次。");
                    return;
                }
                Game.actor.GetComponent<SmallWorldPetResponse>()?.Respond("gift");
                SmallWorldPetResponse.Chime(Game, "dawn", "gift");
                Toast("暖焰灯亮了。阿烬送来的这点光，今天留在家里。");
                StartCoroutine(SyncState());
            });
        }

        void FrameGrottoEntrance()
        {
            Game.orbit.overview = false;
            Game.orbit.yaw = 181;
            Game.orbit.pitch = 24;
            Game.orbit.distance = 8.2f;
        }

        IEnumerator WaitForHomecomingNight()
        {
            homecomingNight = true;
            var current = Game.atmosphere.CurrentHour;
            var end = current > 21 ? 44.0f : 20.4f;
            var started = Time.unscaledTime;
            while (Time.unscaledTime < started + 1.75f)
            {
                if (!JourneyAvailable || IsVisitor) { homecomingNight = false; yield break; }
                Game.atmosphere.Preview(Mathf.Lerp(current, end, Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / 1.75f)));
                yield return null;
            }
            Game.atmosphere.Preview(20.4f);
            homecomingNight = false;
            ApproachLamp();
        }

        IEnumerator ArrangeHomecoming()
        {
            if (stagingHomecoming || chatPending || applyingGuideAction || AjinGift == null) yield break;
            if (restoringOwnedLayout || !OpenPlacement(false)) { Toast("等新土地准备好，再一起布置。"); yield break; }
            stagingHomecoming = true;
            var gift = AjinGift;
            var asset = (availableAssets ?? library.assets).FirstOrDefault(a => a.id == gift.assetId);
            string original = DraftJson();
            if (asset != null && !editItems.Any(i => i.giftId == gift.id))
            {
                editItems.Add(new SmallWorldItem
                {
                    instanceId = "homecoming-ajin-light", giftId = gift.id, assetId = asset.id,
                    assetHash = asset.hash, assetVersion = asset.version, tint = gift.tint,
                    localPosition = new Vector3(1.55f, 0, 1.45f), localScale = Vector3.one * gift.scale
                });
            }
            editSurfaceStyle = SmallWorldGardenSurface.Style;
            editSeamSoftened = true;
            yield return PreviewPlacement(false);
            if (asset == null || !previewValid)
            {
                ReadDraft(original);
                yield return PreviewPlacement(false);
                Toast("这个位置不合适，请在布置面板里给礼物选一个空位。");
                stagingHomecoming = false;
                yield break;
            }
            SaveDraft();
            yield return ClosePlacement();
            stagingHomecoming = false;
            yield return SendChat(HomecomingRequest);
        }

        void FrameHomecomingPreview()
        {
            var gift = editItems.FirstOrDefault(item => item.giftId == HomecomingGift);
            if (gift == null) return;
            var orbit = Game.orbit;
            orbit.overview = true;
            orbit.yaw = -28;
            orbit.pitch = 42;
            orbit.distance = 12;
            var focus = Game.expansion.land.TransformPoint(gift.localPosition) + Vector3.up * .35f;
            orbit.expandedOverviewCenter = focus + Quaternion.Euler(0, orbit.yaw, 0) * Vector3.right * 1.35f;
        }

        void PrepareHomecomingStyles(Font font)
        {
            coverTitle = new GUIStyle(title) { fontSize = 63 };
            coverTitle.normal.textColor = new Color(.99f, .94f, .81f);
            coverSubtitle = new GUIStyle(label) { fontSize = 19 };
            coverSubtitle.normal.textColor = new Color(.91f, .91f, .81f);
            hudTitle = new GUIStyle(title) { fontSize = 20 };
            hudTitle.normal.textColor = new Color(.99f, .95f, .84f);
            hudNote = new GUIStyle(muted) { fontSize = 14 };
            hudNote.normal.textColor = new Color(.90f, .94f, .86f);
            hudPill = new GUIStyle(panel) { padding = new RectOffset(14, 14, 8, 8) };
            hudPill.normal.background = RoundedTexture(new Color(.075f, .14f, .125f, .91f));
            coverPill = new GUIStyle(button) { fontSize = 19, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(19, 16, 10, 10) };
            coverPill.normal.background = RoundedTexture(new Color(.95f, .82f, .57f));
            coverPill.normal.textColor = new Color(.15f, .23f, .17f);
            coverPill.hover.background = RoundedTexture(new Color(1f, .91f, .69f));
            coverPill.hover.textColor = coverPill.normal.textColor;
            coverPill.active.background = coverPill.hover.background;
            coverPill.active.textColor = coverPill.normal.textColor;
            coverPill.border = new RectOffset(12, 12, 12, 12);
            hudPill.border = coverPill.border;
            button.border = coverPill.border;
            button.normal.background = RoundedTexture(new Color(.19f, .32f, .24f));
            button.hover.background = RoundedTexture(new Color(.25f, .40f, .30f));
            button.active.background = button.hover.background;
            button.focused.background = button.hover.background;
            coverShade = new Texture2D(128, 2);
            coverShade.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < 128; x++)
                for (int y = 0; y < 2; y++)
                    coverShade.SetPixel(x, y, new Color(.035f, .103f, .093f, Mathf.Lerp(.89f, 0, Mathf.SmoothStep(0, 1, x / 102f))));
            coverShade.Apply();
            coverTexture = Resources.Load<Texture2D>("Homecoming/Cover");
        }

        Texture2D RoundedTexture(Color color)
        {
            var texture = new Texture2D(32, 32) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            homecomingTextures.Add(texture);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 6);
                    float dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 6);
                    var pixel = color;
                    pixel.a *= Mathf.Clamp01(9.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, pixel);
                }
            texture.Apply();
            return texture;
        }

        IEnumerator EnterFromTitle(string theme)
        {
            yield return BeginJourney(theme);
            if (Game != null && Game.orbit != null) Game.orbit.FocusPlayer();
            titleHelp = false;
            titleOpen = false;
        }

        void DrawHomecomingTitle()
        {
            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var canvas = new Rect(0, 0, Screen.width / scale, Screen.height / scale);
            GUI.color = new Color(.09f, .20f, .17f);
            GUI.DrawTexture(canvas, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (coverTexture != null) GUI.DrawTexture(canvas, coverTexture, ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(canvas, coverShade, ScaleMode.StretchToFill);
            GUI.Label(new Rect(74, 123, 510, 38), "AI 对话共创 · 探索、建造与陪伴", coverSubtitle);
            GUI.Label(new Rect(66, 163, 820, 101), "捡个星球慢慢造", coverTitle);
            GUI.Label(new Rect(76, 278, 360, 62), "探索海岸、挑战守卫，和小禾一起建造自己的小世界。", coverSubtitle);
            GUI.enabled = !loading;
            if (titleThemes)
            {
                if (GUI.Button(new Rect(76, 372, 276, 54), "从晨曦花园出发", coverPill)) StartCoroutine(EnterFromTitle("dawn"));
                if (GUI.Button(new Rect(76, 440, 276, 54), "从暖焰营地出发", coverPill)) StartCoroutine(EnterFromTitle("ember"));
                if (GUI.Button(new Rect(76, 510, 120, 42), "返回", button)) titleThemes = false;
            }
            else
            {
                bool continuing = !string.IsNullOrEmpty(Profile.theme);
                if (GUI.Button(new Rect(76, 372, 276, 58), continuing ? "继续旅程     →" : "开始旅程     →", coverPill))
                {
                    if (continuing) StartCoroutine(EnterFromTitle(Profile.theme));
                    else titleThemes = true;
                }
                if (GUI.Button(new Rect(76, 446, 276, 48), "完整家园展示", button)) StartSubmissionGallery();
#if UNITY_WEBGL && !UNITY_EDITOR
                if (GUI.Button(new Rect(76, 507, 180, 42), titleHelp ? "收起说明" : "玩法说明", button)) OpenJourneyFilm();
#else
                if (GUI.Button(new Rect(76, 507, 180, 42), "观看演示", button)) OpenJourneyFilm();
#endif
            }
            GUI.enabled = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (titleHelp)
            {
                GUILayout.BeginArea(new Rect(660, 350, 540, 265), hudPill);
                GUILayout.BeginHorizontal();
                GUILayout.Label("玩法说明", hudTitle);
                if (GUILayout.Button("收起", button, GUILayout.Width(67))) titleHelp = false;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("WASD 移动 · E 互动 · 鼠标旋转和缩放。", hudNote);
                GUILayout.Space(8);
                GUILayout.Label("开补给箱、挑战守卫后，可以使用现有资产布置一块相邻新地。", hudNote);
                GUILayout.Space(8);
                GUILayout.Label("与小禾的真实 Agent 对话需在服务端配置 QWEN_AGENT_API_KEY。", hudNote);
                GUILayout.Space(8);
                GUILayout.Label("“完整家园展示”使用独立存档，可直接查看完成后的家园。", hudNote);
                GUILayout.EndArea();
            }
#endif
            GUI.Label(new Rect(76, 642, 425, 34), "WASD 移动 · E 互动 · 鼠标旋转和缩放", coverSubtitle);
        }

        void OpenJourneyFilm()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            titleHelp = !titleHelp;
#else
            var moviePath = Path.Combine(Application.streamingAssetsPath, "homecoming-preview.mp4");
            if (File.Exists(moviePath)) Application.OpenURL(new Uri(moviePath).AbsoluteUri);
            else Toast("这份构建没有附带演示影片。");
#endif
        }

        void ToggleHomecomingJournal()
        {
            Game.CancelMovement();
            homecomingJournal = !homecomingJournal;
            UpdateControls();
        }

        void DrawHomecomingJournal()
        {
            var step = Journey;
            GUILayout.BeginArea(new Rect(24, 88, 338, 520), panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("旅行手记", title);
            if (GUILayout.Button("收起", button, GUILayout.Width(67))) ToggleHomecomingJournal();
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("给回家的路留一盏灯", label);
            GUILayout.Label(JourneyHint(step), muted);
            GUILayout.Space(16);
            GUILayout.Label((Game.Progress.firstExpansionUnlocked ? "✓" : "○") + "  挑战守卫 · 占领土地", label);
            GUILayout.Label((AjinGift == null ? "○" : "✓") + "  拜访阿烬 · 带回暖焰灯", label);
            GUILayout.Label((CommittedLamp == null ? "○" : "✓") + "  和小禾布置纪念角", label);
            GUILayout.Label((Profile.homecomingLampLit ? "✓" : "○") + "  入夜后点亮暖焰灯", label);
            GUILayout.Space(19);
            GUI.enabled = !Game.Busy && !loading && !homecomingNight && !stagingHomecoming;
            DrawJourneyAction(step);
            if (coast != null && playableLand == null)
            {
                GUILayout.Space(12);
                for (int row = 0; row < 3; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int i = row * 2 + 1; i <= row * 2 + 2; i++)
                        if (GUILayout.Button(DawnCoast.ZoneNames[i], button)) GoToCoastDistrict(i);
                    GUILayout.EndHorizontal();
                }
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        void DrawJourneyAction(JourneyStep step)
        {
            switch (step)
            {
                case JourneyStep.Supply:
                    if (GUILayout.Button("去开补给箱", button)) { ToggleHomecomingJournal(); Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Chest), true); }
                    break;
                case JourneyStep.Expand:
                    if (GUILayout.Button("去挑战土地守卫", button)) ApproachGuardian();
                    break;
                case JourneyStep.Visit:
                    if (GUILayout.Button("打开地图 · 拜访阿烬", button))
                    { if(playableLand != null){ToggleHomecomingJournal();OpenWorld();}else GoToCoastDistrict(4); }
                    break;
                case JourneyStep.Meet:
                case JourneyStep.Gift:
                    if (GUILayout.Button("找阿烬聊聊", button))
                    {
                        ToggleHomecomingJournal();
                        var target = worldTargets.FirstOrDefault(t => t != null && t.kind == "resident");
                        if (target != null) QueueWorldTarget(target);
                    }
                    break;
                case JourneyStep.Care:
                    if (GUILayout.Button("去给暖焰芽浇水", button)) { ToggleHomecomingJournal(); Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Water), true); }
                    break;
                case JourneyStep.Warm:
                    if (GUILayout.Button("去炉边", button)) { ToggleHomecomingJournal(); if (ritualTarget != null) QueueWorldTarget(ritualTarget); }
                    break;
                case JourneyStep.Home:
                    if (GUILayout.Button(AjinGift == null ? "回自己的家园" : "带着礼物回家", button)) { ToggleHomecomingJournal(); StartCoroutine(Visit(0, Profile.ownContinent)); }
                    break;
                case JourneyStep.Arrange:
                    if (GUILayout.Button("和小禾布置礼物", button))
                    { if(playableLand != null){ToggleHomecomingJournal();StartCoroutine(ArrangeHomecoming());}else GoToCoastDistrict(2); }
                    break;
                case JourneyStep.Light:
                    if (GUILayout.Button(Game.atmosphere.Daylight < .4f ? "去点灯" : "等到入夜", button))
                    {
                        ToggleHomecomingJournal();
                        if (Game.atmosphere.Daylight < .4f) ApproachLamp();
                        else StartCoroutine(WaitForHomecomingNight());
                    }
                    break;
            }
        }
    }
}
