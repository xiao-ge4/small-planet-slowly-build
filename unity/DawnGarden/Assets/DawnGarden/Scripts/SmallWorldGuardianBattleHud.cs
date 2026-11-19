using UnityEngine;

namespace DawnGarden
{
    public sealed partial class SmallWorldGuardianBattle
    {
        static readonly Color HudCream = new Color(.97f, .94f, .81f);
        static readonly Color HudMuted = new Color(.90f, .91f, .82f);
        static readonly Color HudGold = new Color(.94f, .75f, .40f);
        static readonly Color HudLeaf = new Color(.57f, .84f, .57f);
        static readonly Color HudWater = new Color(.51f, .80f, .83f);
        GUIStyle hudShape, hudSmall, hudCenter, hudKey, hudCardTitle, hudCardMeta, hudTooltip, hudResultLabel;
        Texture2D hudCircle;

        void PrepareStyles()
        {
            if (title != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // CJK glyphs need a generous line box. All text below uses at least 1.8x its font size.
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), normal = { textColor = HudCream } };
            label = new GUIStyle(title) { fontSize = 16, fontStyle = FontStyle.Normal, wordWrap = true };
            muted = new GUIStyle(label) { fontSize = 13, normal = { textColor = HudMuted } };
            enemyTitle = new GUIStyle(title) { fontSize = 17 };
            largeNumber = new GUIStyle(label) { fontSize = 14, alignment = TextAnchor.MiddleRight };
            resultTitle = new GUIStyle(title) { fontSize = 29, alignment = TextAnchor.MiddleCenter };
            floatingDamage = new GUIStyle(title) { fontSize = 32, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            hudSmall = new GUIStyle(muted) { fontSize = 13, wordWrap = false };
            hudCenter = new GUIStyle(muted) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            hudKey = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            hudCardTitle = new GUIStyle(title) { fontSize = 20 };
            hudCardMeta = new GUIStyle(muted) { fontSize = 14, wordWrap = false };
            hudResultLabel = new GUIStyle(enemyTitle) { alignment = TextAnchor.MiddleCenter };
            hudTooltip = new GUIStyle(muted) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            hudShape = new GUIStyle(GUI.skin.box) { normal = { background = HudMask(false) }, border = new RectOffset(12, 12, 12, 12), margin = new RectOffset(), padding = new RectOffset() };
            hudCircle = HudMask(true);
            // Invisible controls retain Unity's mouse/keyboard event handling; the visible treatment is drawn below.
            button = new GUIStyle(GUIStyle.none);
            command = new GUIStyle(GUIStyle.none);
        }

        Texture2D HudMask(bool circle)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = circle ? "Battle HUD · circle" : "Battle HUD · rounded frame" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + .5f - size * .5f), dy = Mathf.Abs(y + .5f - size * .5f);
                    float distance = circle ? Mathf.Sqrt(dx * dx + dy * dy) - 31f : new Vector2(Mathf.Max(0, dx - 20), Mathf.Max(0, dy - 20)).magnitude - 11f;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - distance));
                }
            texture.SetPixels(pixels); texture.Apply(); ownedTextures.Add(texture);
            return texture;
        }

        /// <summary>Compact 1280x720 HUD; the scene stays open for actor silhouettes and impact framing.</summary>
        public void Draw()
        {
            if (!IsActive) return;
            PrepareStyles();
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; var oldBackground = GUI.backgroundColor;
            var oldContent = GUI.contentColor; int oldDepth = GUI.depth; bool oldEnabled = GUI.enabled;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 720 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
            GUI.depth = -100; GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white; GUI.enabled = true;
            try
            {
                HealthPanel(new Rect(30, 24, 284, 62), "我的精灵", DisplayPlayerHealth, GuardianBattleModel.PlayerMaxHealth, HudLeaf, false);
                HealthPanel(new Rect(966, 24, 284, 62), "苔岩守卫", DisplayGuardianHealth, GuardianBattleModel.GuardianMaxHealth, HudGold, true);
                DrawIntent();
                DrawCombatFeedback(scale);
                if (State.Result == GuardianBattleResult.None) DrawCommands();
                else DrawResult();
                if (entranceAge < EntranceDuration)
                    HudFill(new Rect(-1280, -720, 3840, 2160), new Color(.08f, .13f, .10f, 1 - Mathf.Clamp01(entranceAge / EntranceDuration)));
            }
            finally
            {
                GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.backgroundColor = oldBackground;
                GUI.contentColor = oldContent; GUI.depth = oldDepth; GUI.enabled = oldEnabled;
            }
        }

        void HealthPanel(Rect rect, string name, float health, int maximum, Color tint, bool isGuardian)
        {
            HudPlate(rect, new Color(.07f, .13f, .105f, .91f), new Color(.78f, .79f, .59f, .43f));
            Vector2 crest = new Vector2(rect.x + 29, rect.y + 30);
            HudDisc(crest, 19, new Color(tint.r, tint.g, tint.b, .12f));
            SmallWorldUiArt.Portrait(new Rect(crest.x-20,crest.y-20,40,40),isGuardian?"guardian":"player");
            GUI.Label(new Rect(rect.x + 57, rect.y + 1, 135, 35), name, enemyTitle);
            GUI.Label(new Rect(rect.x + 187, rect.y + 2, 79, 33), Mathf.CeilToInt(Mathf.Max(0, health)) + " / " + maximum, largeNumber);
            Rect track = new Rect(rect.x + 58, rect.y + 39, 207, 8);
            HudRound(track, new Color(.025f, .055f, .04f, .9f));
            float filled = Mathf.Clamp01(health / maximum) * (track.width - 2);
            if (filled > 0)
            {
                Color bar = health / maximum < .25f ? new Color(.89f, .54f, .37f) : tint;
                HudFill(new Rect(track.x + 1, track.y + 1, filled, 6), bar);
                HudFill(new Rect(track.x + 1, track.y + 1, filled, 2), new Color(1, 1, .85f, .30f));
            }
            // Small milestones give the bar a crafted game-HUD rhythm without extra text.
            for (int i = 1; i < 4; i++) HudFill(new Rect(track.x + track.width * i / 4, track.y, 1, track.height), new Color(.06f, .12f, .08f, .65f));
        }

        void DrawIntent()
        {
            if (State.Result != GuardianBattleResult.None)
            {
                bool victory = State.Result == GuardianBattleResult.Victory;
                Color resultAccent = victory ? HudGold : HudWater;
                HudPlate(new Rect(540, 30, 200, 48), new Color(.08f, .13f, .10f, .90f), new Color(resultAccent.r, resultAccent.g, resultAccent.b, .48f));
                HudIcon(new Vector2(565, 54), victory ? GuardianBattleAction.Resonance : GuardianBattleAction.Guard, 22, resultAccent);
                GUI.Label(new Rect(590, 34, 134, 38), victory ? "试炼完成" : State.Result == GuardianBattleResult.Defeat ? "暂作休整" : "安全撤退", enemyTitle);
                return;
            }
            Rect rect = new Rect(482, 24, 316, 62);
            bool danger = State.Intent == GuardianBattleIntent.HeavyStrike;
            bool exposed = State.Intent == GuardianBattleIntent.Exposed;
            Color accent = danger ? new Color(.98f, .61f, .37f) : exposed ? HudLeaf : HudGold;
            HudPlate(rect, new Color(.08f, .13f, .10f, .90f), new Color(accent.r, accent.g, accent.b, .48f));
            bool phaseTwo = State.PhaseTwo && DisplayGuardianHealth <= GuardianBattleModel.GuardianMaxHealth * .5f;
            GUI.Label(new Rect(495, phaseTwo ? 26 : 37, 76, 32), "第 " + State.Turn + " 回合", hudCenter);
            if (phaseTwo) HudText(new Rect(495, 54, 76, 27), "石甲松动", hudCenter, HudGold);
            HudFill(new Rect(583, 39, 1, 31), new Color(.9f, .83f, .60f, .22f));
            Vector2 icon = new Vector2(608, 55);
            if (State.Intent == GuardianBattleIntent.Windup)
            {
                float pulse = .7f + .3f * Mathf.Sin(Time.unscaledTime * 3);
                HudDisc(icon, 17, new Color(accent.r, accent.g, accent.b, .08f + pulse * .10f));
                HudIcon(icon, GuardianBattleAction.Resonance, 23, accent);
            }
            else HudIcon(icon, exposed ? GuardianBattleAction.Resonance : GuardianBattleAction.Attack, 23, accent);
            string detail = State.Intent == GuardianBattleIntent.Probe ? "8 伤害" : State.Intent == GuardianBattleIntent.Windup ? "下回合重击" : danger ? "24 伤害 · 可打断" : "共鸣伤害提升";
            GUI.Label(new Rect(630, 25, 156, 34), State.IntentTitle, enemyTitle);
            HudText(new Rect(631, 54, 156, 27), detail, hudSmall, accent);
            if (State.Result == GuardianBattleResult.None && rect.Contains(Event.current.mousePosition))
            {
                HudPlate(new Rect(451, 96, 378, 72), new Color(.07f, .13f, .10f, .97f), new Color(accent.r, accent.g, accent.b, .38f));
                GUI.Label(new Rect(469, 100, 342, 63), State.IntentDescription, hudTooltip);
            }
        }

        void DrawCombatFeedback(float scale)
        {
            if (!ImpactVisible || camera == null || player == null || guardian == null) return;
            bool playersTurn = State.Phase == GuardianBattlePhase.PlayerAction;
            if (!playersTurn && State.Phase != GuardianBattlePhase.GuardianAction) return;
            var target = playersTurn ? guardian : player;
            int damage = playersTurn ? State.PlayerDamageDealt : State.GuardianDamageDealt;
            string text = damage > 0 ? "−" + damage : "";
            string detail = "";
            if (playersTurn && State.Interrupted) detail = "打断蓄力";
            else if (playersTurn && State.HitWeakPoint) detail = "弱点命中";
            else if (State.LastAction == GuardianBattleAction.Guard)
            {
                if (playersTurn) { text = "守护"; target = player; }
                else if (damage > 0) detail = "抵挡";
            }
            if (string.IsNullOrEmpty(text)) return;
            float age = Mathf.Max(0, ImpactAge);
            var point = camera.WorldToScreenPoint(target.position + Vector3.up * (2f + age * .55f));
            if (point.z <= 0) return;
            float x = (point.x - (Screen.width - 1280 * scale) * .5f) / scale;
            float y = Mathf.Clamp((Screen.height - point.y - (Screen.height - 720 * scale) * .5f) / scale, 112, 490);
            float alpha = 1 - Mathf.Clamp01((age - .48f) / .40f);
            Color accent = playersTurn && (State.Interrupted || State.HitWeakPoint) ? HudGold : State.LastAction == GuardianBattleAction.Guard ? HudWater : HudCream;
            HudText(new Rect(x - 128 + 2, y - 24 + 3, 256, 65), text, floatingDamage, new Color(.035f, .07f, .045f, alpha * .9f));
            HudText(new Rect(x - 128, y - 24, 256, 65), text, floatingDamage, new Color(accent.r, accent.g, accent.b, alpha));
            if (detail.Length > 0)
            {
                HudText(new Rect(x - 127, y + 29, 256, 31), detail, hudCenter, new Color(.025f, .05f, .03f, alpha));
                HudText(new Rect(x - 128, y + 28, 256, 31), detail, hudCenter, new Color(accent.r, accent.g, accent.b, alpha));
            }
        }

        void DrawCommands()
        {
            bool ready = State.CanAct && entranceAge >= EntranceDuration;
            string hoverText = null;
            Rect attackRect = new Rect(296, 606, 216, 86);
            Rect guardRect = new Rect(528, 606, 216, 86);
            Rect resonanceRect = new Rect(760, 606, 224, 86);
            if (DrawSkill(attackRect, GuardianBattleAction.Attack, "1", "普攻", "共鸣能量 +1", HudLeaf, ready)) TryAct(GuardianBattleAction.Attack);
            if (DrawSkill(guardRect, GuardianBattleAction.Guard, "2", "守护", "减伤 75%", HudWater, ready)) TryAct(GuardianBattleAction.Guard);
            bool charged = DisplayEnergy >= GuardianBattleModel.ResonanceCost;
            if (DrawSkill(resonanceRect, GuardianBattleAction.Resonance, "3", "共鸣击", charged ? "消耗 2 共鸣能量" : "需要 2 共鸣能量", HudGold, ready && charged)) TryAct(GuardianBattleAction.Resonance);
            Vector2 mouse = Event.current.mousePosition;
            if (attackRect.Contains(mouse)) hoverText = "普攻 · 造成 8 伤害，命中后积攒 1 共鸣能量";
            if (guardRect.Contains(mouse)) hoverText = "守护 · 本回合减伤 75%，重击也能稳稳接住";
            if (resonanceRect.Contains(mouse)) hoverText = State.Intent == GuardianBattleIntent.Exposed ? "共鸣击 · 命中弱点，造成 28 伤害" : State.CanInterrupt ? "共鸣击 · 造成 16 伤害，并打断守卫蓄力" : "共鸣击 · 消耗 2 共鸣能量，造成 16 伤害";

            // Energy has its own jewel setting, away from actor silhouettes and readable while cards animate.
            HudPlate(new Rect(141, 618, 132, 64), new Color(.06f, .12f, .09f, .83f), new Color(.76f, .82f, .57f, .25f));
            GUI.Label(new Rect(149, 616, 116, 28), "共鸣能量 "+DisplayEnergy+" / "+GuardianBattleModel.MaxEnergy, hudCenter);
            for (int i = 0; i < GuardianBattleModel.MaxEnergy; i++)
                { var old=GUI.color;GUI.color=DisplayEnergy>i?Color.white:new Color(.46f,.53f,.45f,.65f);GUI.DrawTexture(new Rect(160+i*31,645,27,29),SmallWorldUiArt.Icon("star"));GUI.color=old; }

            string status = hoverText ?? (ready ? "选择行动" : PresentationMessage);
            if (!string.IsNullOrEmpty(status))
            {
                float width = Mathf.Clamp(hudCenter.CalcSize(new GUIContent(status)).x + 46, 136, 840);
                HudPlate(new Rect(640 - width / 2, 565, width, 32), new Color(.06f, .12f, .09f, .86f), new Color(.81f, .83f, .61f, .18f));
                GUI.Label(new Rect(652 - width / 2, 564, width - 24, 32), status, hudCenter);
            }
            Rect retreat = new Rect(1121, 650, 127, 37);
            bool retreatHover = retreat.Contains(mouse);
            HudRound(retreat, new Color(.055f, .11f, .08f, retreatHover ? .88f : .68f));
            HudText(retreat, "撤退  Esc", hudCenter, retreatHover ? HudCream : HudMuted);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = entranceAge >= EntranceDuration;
            if (GUI.Button(retreat, GUIContent.none, button)) TryAct(GuardianBattleAction.Retreat);
            GUI.enabled = oldEnabled;
        }

        bool DrawSkill(Rect rect, GuardianBattleAction action, string key, string name, string cost, Color accent, bool available)
        {
            bool selected = !State.CanAct && State.LastAction == action;
            bool hover = rect.Contains(Event.current.mousePosition) && State.CanAct;
            bool resonance = action == GuardianBattleAction.Resonance;
            Color fill = resonance ? new Color(.18f, .145f, .085f, .96f) : action == GuardianBattleAction.Guard ? new Color(.065f, .16f, .17f, .96f) : new Color(.09f, .17f, .115f, .96f);
            if (hover && available) fill = Color.Lerp(fill, new Color(accent.r, accent.g, accent.b, .98f), .14f);
            Color edge = new Color(accent.r, accent.g, accent.b, selected || (hover && available) ? .95f : available ? .58f : .25f);
            HudPlate(rect, fill, edge);
            HudFill(new Rect(rect.x + 14, rect.y + 1, rect.width - 28, selected || (hover && available) ? 3 : 1), edge);
            // Decorative corner cut and a restrained glint make the controls feel like equipment, not forms.
            HudLine(new Vector2(rect.xMax - 17, rect.y + 10), new Vector2(rect.xMax - 10, rect.y + 17), 1, edge);
            HudRound(new Rect(rect.x + 10, rect.y + 10, 23, 23), new Color(.025f, .05f, .035f, .60f));
            HudText(new Rect(rect.x + 10, rect.y + 6, 23, 30), key, hudKey, HudCream);
            Vector2 icon = new Vector2(rect.x + 49, rect.y + 50);
            HudDisc(icon, 23, new Color(accent.r, accent.g, accent.b, available || selected ? .12f : .055f));
            HudIcon(icon, action, 29, new Color(accent.r, accent.g, accent.b, available || selected ? 1 : .46f));
            HudText(new Rect(rect.x + 82, rect.y + 9, rect.width - 92, 40), name, hudCardTitle, HudCream);
            HudText(new Rect(rect.x + 83, rect.y + 47, rect.width - 93, 30), cost, hudCardMeta, HudMuted);
            bool previous = GUI.enabled; GUI.enabled = available;
            bool clicked = GUI.Button(rect, GUIContent.none, command);
            GUI.enabled = previous;
            return clicked;
        }

        void DrawResult()
        {
            bool victory = State.Result == GuardianBattleResult.Victory;
            bool defeat = State.Result == GuardianBattleResult.Defeat;
            Color accent = victory ? HudGold : HudWater;
            // Let the ending performance play in the unobscured scene; the controls unlock after its hold.
            string heading = victory ? "守卫已平静下来" : defeat ? "稍作休息，再来一次" : "已安全撤退";
            Rect banner = new Rect(277, 581, 726, 111);
            HudPlate(banner, new Color(.065f, .12f, .095f, .94f), new Color(accent.r, accent.g, accent.b, .53f));
            HudIcon(new Vector2(314, 618), victory ? GuardianBattleAction.Resonance : GuardianBattleAction.Guard, 30, accent);
            HudText(new Rect(346, 588, 440, 43), heading, title, HudCream);
            GUI.Label(new Rect(346, 632, 406, 34), victory ? "返回家园，继续这次土地开拓。" : "体力会恢复，家园与已有进度都保留。", muted);
            if (!ResultReady)
            {
                HudText(new Rect(793, 618, 185, 37), victory ? "共鸣正在平息…" : "稍候片刻…", hudCenter, accent);
                return;
            }
            if (victory)
            {
                if (HudResultButton(new Rect(793, 615, 180, 48), "返回家园", accent)) Close();
            }
            else
            {
                if (HudResultButton(new Rect(793, 594, 180, 44), "再试一次", accent)) Restart();
                Rect home = new Rect(793, 646, 180, 32);
                bool hovered = home.Contains(Event.current.mousePosition);
                HudText(home, "返回家园", hudCenter, hovered ? HudCream : HudMuted);
                if (GUI.Button(home, GUIContent.none, button)) Close();
            }
        }

        bool HudResultButton(Rect rect, string text, Color accent)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            HudPlate(rect, hover ? new Color(.21f, .30f, .22f, 1) : new Color(.14f, .23f, .17f, 1), new Color(accent.r, accent.g, accent.b, hover ? 1 : .65f));
            HudText(rect, text, hudResultLabel, HudCream);
            return GUI.Button(rect, GUIContent.none, button);
        }

        void HudPlate(Rect rect, Color fill, Color edge)
        {
            HudRound(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(.025f, .045f, .025f, .30f));
            HudRound(rect, edge);
            HudRound(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill);
        }

        void HudRound(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.Box(rect, GUIContent.none, hudShape); GUI.color = previous;
        }

        static void HudFill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        static void HudText(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previousText = style.normal.textColor, previousContent = GUI.contentColor, previousTint = GUI.color;
            bool previousEnabled = GUI.enabled;
            try
            {
                // Use one final ink color. Multiplying a colored style by another tint muddied small text.
                // Labels stay readable even when their associated invisible button is disabled.
                style.normal.textColor = color;
                GUI.contentColor = GUI.color = Color.white;
                GUI.enabled = true;
                GUI.Label(rect, text, style);
            }
            finally
            {
                style.normal.textColor = previousText; GUI.contentColor = previousContent;
                GUI.color = previousTint; GUI.enabled = previousEnabled;
            }
        }

        void HudDisc(Vector2 center, float radius, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), hudCircle);
            GUI.color = previous;
        }

        static void HudLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            Vector2 delta = end - start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            HudFill(new Rect(start.x, start.y - width * .5f, delta.magnitude, width), color);
            GUI.matrix = previous;
        }

        void HudCrystal(Vector2 center, float radius, bool filled, Color accent)
        {
            Color edge = filled ? accent : new Color(.53f, .61f, .47f, .65f);
            Vector2 top = center + Vector2.up * -radius, right = center + Vector2.right * radius * .65f;
            Vector2 bottom = center + Vector2.up * radius, left = center + Vector2.left * radius * .65f;
            if (filled)
            {
                HudDisc(center, radius + 6, new Color(accent.r, accent.g, accent.b, .10f));
                for (int i = -(int)radius + 1; i < (int)radius; i++)
                {
                    float halfWidth = (radius - Mathf.Abs(i)) * .65f;
                    HudFill(new Rect(center.x - halfWidth, center.y + i, halfWidth * 2, 1), new Color(accent.r, accent.g, accent.b, .78f));
                }
                HudLine(top, bottom, 1, new Color(1, .96f, .70f, .7f));
            }
            HudLine(top, right, 1.3f, edge); HudLine(right, bottom, 1.3f, edge);
            HudLine(bottom, left, 1.3f, edge); HudLine(left, top, 1.3f, edge);
        }

        void HudIcon(Vector2 center, GuardianBattleAction action, float size, Color color)
        {
            var old=GUI.color;GUI.color=new Color(1,1,1,color.a);
            GUI.DrawTexture(new Rect(center.x-size*.7f,center.y-size*.7f,size*1.4f,size*1.4f),SmallWorldUiArt.Icon(action==GuardianBattleAction.Guard?"shield":action==GuardianBattleAction.Resonance?"star":"leaf"),ScaleMode.ScaleToFit);GUI.color=old;
        }

    }
}
