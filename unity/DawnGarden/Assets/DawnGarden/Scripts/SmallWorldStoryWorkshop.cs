using System;
using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    // Authored recording UI only. These states never create generation jobs or backend tasks.
    public sealed partial class SmallWorldApp
    {
        static readonly Rect StoryWorkshopBounds = new Rect(890, 92, 368, 565);
        bool storyWorkshopOpen, storyWorkshopHasConfirmation, storyWorkshopConfirmed;
        string storyWorkshopTitle = "", storyWorkshopRequest = "", storyWorkshopConfirmation = "";
        string[] storyWorkshopSteps = Array.Empty<string>();
        int[] storyWorkshopStepStates = Array.Empty<int>(); // 0 planned, 1 working, 2 complete.
        readonly List<string> storyWorkshopLog = new List<string>();
        readonly string[] storyWorkshopAgentMessages = { "等待规划分工", "等待素材清单" };
        readonly int[] storyWorkshopAgentSteps = { -1, -1 };
        readonly int[] storyWorkshopAgentStates = { 0, 0 };
        GUIStyle workshopHeading, workshopBody, workshopNote, workshopStatus, workshopNumber;
        GUIStyle workshopCard, workshopBubble, workshopActive, workshopConfirm, workshopTag, workshopClose;

        void StoryWorkshopOpen(string title, string request, string[] steps)
        {
            storyWorkshopOpen = true;
            storyWorkshopTitle = title ?? "一起搭起小世界";
            storyWorkshopRequest = request ?? "";
            storyWorkshopSteps = steps == null ? Array.Empty<string>() : (string[])steps.Clone();
            storyWorkshopStepStates = new int[storyWorkshopSteps.Length];
            storyWorkshopHasConfirmation = storyWorkshopConfirmed = false;
            storyWorkshopConfirmation = "方案就绪后，小禾会请你确认";
            storyWorkshopLog.Clear();
            for (int i = 0; i < 2; i++)
            {
                storyWorkshopAgentSteps[i] = -1;
                storyWorkshopAgentStates[i] = 0;
                storyWorkshopAgentMessages[i] = i == 0 ? "等待规划分工" : "等待素材清单";
            }

            ChatOpen = WorldOpen = false;
            editorOpen = creatorOpen = ritualOpen = showMemories = false;
            gameSettingsOpen = homecomingJournal = guardianCoCreateOffer = false;
            storyDialogueVisible = storyConceptVisible = storyTitleVisible = false;
            SetWorldVisible(false);
            UpdateControls();
            StoryEvent("workshop-open", "provenance=preset-demo | title=" + storyWorkshopTitle
                + " | request=" + storyWorkshopRequest + " | plan=" + string.Join(" / ", storyWorkshopSteps));
        }

        void StoryWorkshopTask(int index, string agent, string message)
        {
            if (!storyWorkshopOpen) return;
            int card = StoryWorkshopAgentCard(agent);
            if (index >= 0 && index < storyWorkshopStepStates.Length) storyWorkshopStepStates[index] = 1;
            storyWorkshopAgentSteps[card] = index;
            storyWorkshopAgentStates[card] = 1;
            storyWorkshopAgentMessages[card] = message ?? "正在准备这一小步";
            StoryEvent("workshop-task", "provenance=preset-demo | step=" + index + " | agent=" + agent
                + " | " + storyWorkshopAgentMessages[card] + " | authored task display; no backend task created");
        }

        void StoryWorkshopDone(int index, string message)
        {
            if (!storyWorkshopOpen) return;
            if (index >= 0 && index < storyWorkshopStepStates.Length) storyWorkshopStepStates[index] = 2;
            for (int i = 0; i < 2; i++)
                if (storyWorkshopAgentSteps[i] == index)
                {
                    storyWorkshopAgentStates[i] = 2;
                    storyWorkshopAgentMessages[i] = message ?? "这一步已完成";
                }
            storyWorkshopLog.Add(message ?? "这一步已完成");
            if (storyWorkshopLog.Count > 2) storyWorkshopLog.RemoveAt(0);
            StoryEvent("workshop-done", "provenance=preset-demo | step=" + index + " | " + message
                + " | staged local scene; no model generation claimed");
        }

        void StoryWorkshopConfirmation(string text, bool confirmed)
        {
            if (!storyWorkshopOpen) return;
            storyWorkshopHasConfirmation = true;
            storyWorkshopConfirmed = confirmed;
            storyWorkshopConfirmation = text ?? "按这个方案继续搭建";
            StoryEvent(confirmed ? "workshop-confirmed" : "workshop-confirmation-needed",
                "provenance=preset-demo | " + storyWorkshopConfirmation);
        }

        void StoryWorkshopClose()
        {
            if (!storyWorkshopOpen) return;
            storyWorkshopOpen = false;
            UpdateControls();
            StoryEvent("workshop-close", "provenance=preset-demo | " + storyWorkshopTitle);
        }

        static int StoryWorkshopAgentCard(string agent)
        {
            string value = (agent ?? "").ToLowerInvariant();
            return value.Contains("资产") || value.Contains("素材") || value.Contains("模型")
                || value.Contains("asset") ? 1 : 0;
        }

        void PrepareStoryWorkshopStyles()
        {
            if (workshopBody != null) return;
            workshopHeading = new GUIStyle(title) { fontSize = 22 };
            workshopBody = new GUIStyle(label) { fontSize = 17 };
            workshopNote = new GUIStyle(hudNote) { fontSize = 15 };
            workshopNote.normal.textColor = new Color(.40f, .49f, .38f);
            workshopStatus = new GUIStyle(workshopNote) { alignment = TextAnchor.MiddleRight, fontSize = 14 };
            workshopNumber = new GUIStyle(workshopBody) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            workshopCard = new GUIStyle(panel) { padding = new RectOffset(10, 10, 8, 8) };
            workshopCard.normal.background = SmallWorldUiArt.Plate(new Color(.93f, .94f, .85f), new Color(.72f, .78f, .63f));
            workshopBubble = new GUIStyle(workshopCard);
            workshopBubble.normal.background = SmallWorldUiArt.Plate(new Color(.89f, .92f, .81f), new Color(.71f, .78f, .62f));
            workshopActive = new GUIStyle(workshopCard);
            workshopActive.normal.background = SmallWorldUiArt.Plate(new Color(.85f, .90f, .74f), new Color(.61f, .72f, .48f));
            workshopConfirm = new GUIStyle(button) { fontSize = 16, padding = new RectOffset(7, 7, 4, 4) };
            workshopTag = new GUIStyle(workshopNote) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            workshopTag.normal.background = SmallWorldUiArt.Plate(new Color(.90f, .87f, .73f), Color.clear);
            workshopTag.border = new RectOffset(16, 16, 16, 16);
            workshopClose = new GUIStyle(secondaryButton) { fontSize = 18, padding = new RectOffset(0, 0, 0, 0) };
            // Do not inherit skin line sizing: short recording labels must never be reduced to an ellipsis by height.
            foreach (var style in new[] { workshopHeading, workshopBody, workshopNote, workshopStatus,
                workshopNumber, workshopCard, workshopBubble, workshopActive, workshopConfirm, workshopTag, workshopClose })
            {
                style.padding = new RectOffset(0, 0, 0, 0);
                style.margin = new RectOffset(0, 0, 0, 0);
                style.fixedHeight = style.fixedWidth = 0;
                style.contentOffset = Vector2.zero;
                style.wordWrap = false;
                style.clipping = TextClipping.Clip;
            }
            workshopHeading.alignment = workshopBody.alignment = workshopNote.alignment = TextAnchor.MiddleLeft;
            workshopStatus.alignment = TextAnchor.MiddleRight;
            workshopNumber.alignment = workshopTag.alignment = workshopConfirm.alignment = workshopClose.alignment = TextAnchor.MiddleCenter;
        }

        // Owns its virtual-canvas transform, so the caller may draw this after DrawStoryOverlay().
        void DrawStoryWorkshop()
        {
            if (!storyWorkshopOpen) return;
            Styles();
            PrepareStoryWorkshopStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            GUI.depth = -185;
            GUI.BeginGroup(StoryWorkshopBounds);
            GUI.Box(new Rect(0, 0, 368, 565), GUIContent.none, panel);
            SmallWorldUiArt.Portrait(new Rect(18, 17, 46, 46), "xiaohe");
            GUI.Label(new Rect(75, 15, 155, 28), "小禾 · 共创", workshopHeading);
            GUI.Label(new Rect(233, 20, 80, 23), "预设演示", workshopTag);
            StoryWorkshopLabel(new Rect(75, 45, 239, 24), storyWorkshopTitle, workshopBody);
            if (GUI.Button(new Rect(324, 17, 27, 28), "×", workshopClose)) StoryWorkshopClose();

            GUI.Box(new Rect(17, 79, 334, 81), GUIContent.none, workshopBubble);
            GUI.Label(new Rect(29, 87, 306, 22), "你的要求", workshopNote);
            StoryWorkshopMultiline(new Rect(29, 110, 306, 43), storyWorkshopRequest, workshopBody);

            int complete = 0, firstActive = -1;
            for (int i = 0; i < storyWorkshopStepStates.Length; i++)
            {
                if (storyWorkshopStepStates[i] == 2) complete++;
                if (firstActive < 0 && storyWorkshopStepStates[i] == 1) firstActive = i;
            }
            GUI.Label(new Rect(19, 172, 180, 25), "计划 · 一步步搭起来", workshopBody);
            GUI.Label(new Rect(250, 173, 98, 24), complete + " / " + storyWorkshopSteps.Length + " 完成", workshopStatus);
            // Keep the current work visible even when an authored plan contains more than four steps.
            int first = Mathf.Clamp(firstActive >= 0 ? firstActive - 2 : complete - 3, 0,
                Mathf.Max(0, storyWorkshopSteps.Length - 4));
            for (int row = 0; row < Mathf.Min(4, storyWorkshopSteps.Length); row++)
            {
                int i = first + row, state = storyWorkshopStepStates[i];
                float y = 203 + row * 27;
                if (state == 1) GUI.Box(new Rect(18, y - 1, 332, 27), GUIContent.none, workshopActive);
                GUI.Label(new Rect(22, y, 27, 25), state == 2 ? "✓" : (i + 1).ToString(), workshopNumber);
                StoryWorkshopLabel(new Rect(53, y, 221, 25), storyWorkshopSteps[i] ?? "", workshopBody);
                GUI.Label(new Rect(275, y, 64, 25), state == 2 ? "已完成" : state == 1 ? "进行中" : "待开始", workshopStatus);
            }

            GUI.Label(new Rect(19, 316, 260, 24), "协作 Agent", workshopBody);
            DrawStoryWorkshopAgent(0, new Rect(17, 344, 162, 87), "场景规划");
            DrawStoryWorkshopAgent(1, new Rect(189, 344, 162, 87), "资产制作");

            GUI.Box(new Rect(17, 444, 334, 60), GUIContent.none,
                storyWorkshopHasConfirmation && !storyWorkshopConfirmed ? workshopActive : workshopCard);
            GUI.Label(new Rect(29, 450, 201, 25), storyWorkshopHasConfirmation
                ? storyWorkshopConfirmed ? "✓ 你已确认" : "需要你的确认" : "一起搭建中", workshopBody);
            if (storyWorkshopHasConfirmation && !storyWorkshopConfirmed
                && GUI.Button(new Rect(239, 451, 99, 27), "确认方案", workshopConfirm))
                StoryWorkshopConfirmation(storyWorkshopConfirmation, true);
            StoryWorkshopLabel(new Rect(29, 478, 309, 22), storyWorkshopConfirmation, workshopNote);

            if (storyWorkshopLog.Count == 0)
                StoryWorkshopMultiline(new Rect(20, 516, 329, 39), "预设协作 · 新物件按脚本逐步搭建\n场景设计与资产准备均为演示状态", workshopNote);
            else
                for (int i = 0; i < storyWorkshopLog.Count; i++)
                    StoryWorkshopLabel(new Rect(21, 516 + i * 20, 326, 22), "✓ " + storyWorkshopLog[i], workshopNote);
            GUI.EndGroup();
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
        }

        void DrawStoryWorkshopAgent(int index, Rect rect, string role)
        {
            int state = storyWorkshopAgentStates[index];
            GUI.Box(rect, GUIContent.none, state == 1 ? workshopActive : workshopCard);
            GUI.Label(new Rect(rect.x + 10, rect.y + 8, 85, 23), role, workshopBody);
            GUI.Label(new Rect(rect.x + 94, rect.y + 7, 58, 24), state == 2 ? "已完成" : state == 1 ? "进行中" : "待开始", workshopStatus);
            StoryWorkshopMultiline(new Rect(rect.x + 10, rect.y + 34, rect.width - 20, 45),
                storyWorkshopAgentMessages[index], workshopNote);
        }

        // Fit by measured width only. Skin font line metrics can exceed a short label's Rect height.
        static void StoryWorkshopLabel(Rect rect, string value, GUIStyle style)
        {
            string original = value ?? "";
            string visible = StoryWorkshopFitLine(original.Replace('\n', ' '), rect.width, style);
            GUI.Label(rect, new GUIContent(visible, original), style);
        }

        // Draw the two lines explicitly so inherited font height cannot silently discard the second line.
        static void StoryWorkshopMultiline(Rect rect, string value, GUIStyle style)
        {
            string original = value ?? "", remaining = original.Replace("\r", "");
            float rowHeight = rect.height * .5f;
            for (int row = 0; row < 2 && remaining.Length > 0; row++)
            {
                string visible;
                if (row == 1)
                    visible = StoryWorkshopFitLine(remaining.Replace('\n', ' '), rect.width, style);
                else
                {
                    int newline = remaining.IndexOf('\n');
                    string paragraph = newline < 0 ? remaining : remaining.Substring(0, newline);
                    int count = StoryWorkshopPrefixLength(paragraph, rect.width, style);
                    visible = paragraph.Substring(0, count);
                    int consumed = count;
                    if (count == paragraph.Length && newline >= 0) consumed++;
                    remaining = remaining.Substring(consumed).TrimStart();
                }
                GUI.Label(new Rect(rect.x, rect.y + row * rowHeight, rect.width, rowHeight),
                    new GUIContent(visible, original), style);
            }
        }

        static string StoryWorkshopFitLine(string value, float width, GUIStyle style)
        {
            if (style.CalcSize(new GUIContent(value)).x <= width) return value;
            float ellipsisWidth = style.CalcSize(new GUIContent("…")).x;
            int count = StoryWorkshopPrefixLength(value, Mathf.Max(0, width - ellipsisWidth), style);
            return value.Substring(0, count) + "…";
        }

        static int StoryWorkshopPrefixLength(string value, float width, GUIStyle style)
        {
            int low = 0, high = value.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (style.CalcSize(new GUIContent(value.Substring(0, middle))).x <= width) low = middle;
                else high = middle - 1;
            }
            return low;
        }
    }
}
