using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        readonly HashSet<string> storyPlacedRevealed = new HashSet<string>();

        void StoryBuildCamera(Vector3 focus, float distance)
        {
            var right = Quaternion.Euler(0, -15, 0) * Vector3.right;
            StoryCamera(focus + right * (distance * .15f), distance, -15, 48);
        }

        void StoryRecordEmpty(string id)
        {
            int facilities = storyLand.VisibleFacilityCount(id);
            StoryCheck(storyLand.ConstructionStage(id) == 0 && facilities == 0,
                "empty territory contains no finished facilities: " + id);
            StoryEvent("construction-empty", id + " | visibleFacilities=" + facilities);
        }

        IEnumerator StoryPlanConstruction(string id, string name, string request, string[] steps)
        {
            StoryCheck(storyLand.HasTerritory(id) && storyLand.IsUndeveloped(id),
                "construction planning starts on owned empty land: " + id);
            StoryWorkshopOpen(name, request, steps);
            StoryEvent("construction-plan", id + " | " + request);
            var bounds = storyLand.ConstructionBounds(id);
            float distance = Mathf.Max(22, Mathf.Max(bounds.width, bounds.height) * 1.7f);
            StoryBuildCamera(new Vector3(bounds.center.x, id == "rest-garden" ? 0 : 2, bounds.center.y), distance);
            StoryWorkshopTask(0, "场景设计", "分析空地范围，安排入口、通路和功能区");
            yield return StoryHold(2);
            StoryWorkshopTask(0, "资产准备", "检索已有物件，列出需要新建的资产");
            yield return StoryShot("workshop-plan-" + id);
            yield return StoryHold(2);
            StoryWorkshopConfirmation("方案已准备好。先看布局，再确认开始搭建。", false);
            yield return StoryHold(2);
            StoryEvent("player", "按这个方案开始，通路要留宽。");
            StoryWorkshopConfirmation("你已确认 · 按计划开始搭建", true);
            StoryEvent("construction-confirmed", id + " | 玩家确认方案");
            yield return StoryHold(1);
        }

        IEnumerator StoryGrowPlacedItems()
        {
            if (placedRoot == null) yield break;
            var fresh = placedRoot.Cast<Transform>().Where(t => storyPlacedRevealed.Add(t.name)).ToArray();
            var scales = fresh.Select(t => t.localScale).ToArray();
            for (int i = 0; i < fresh.Length; ++i) fresh[i].localScale = Vector3.zero;
            float duration = Mathf.Max(.45f, storyConfig.holdScale * 1.1f);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                for (int i = 0; i < fresh.Length; ++i)
                    if (fresh[i] != null) fresh[i].localScale = scales[i] * progress;
                yield return null;
            }
            for (int i = 0; i < fresh.Length; ++i)
                if (fresh[i] != null) fresh[i].localScale = scales[i];
        }

        IEnumerator StoryBuildDistrict(string id, string name, string request, string[] steps)
        {
            yield return StoryPlanConstruction(id, name, request, steps);
            for (int stage = 1; stage <= 3; ++stage)
            {
                int step = stage - 1;
                StoryWorkshopTask(step, stage == 2 ? "资产准备" : "场景设计", steps[step]);
                if (stage == 2)
                {
                    StoryWorkshopTask(step, "资产准备", "设计图 → 3D 物件 → 尺寸检查（预设演示）");
                    yield return StoryHold(2);
                    StoryWorkshopTask(step, "场景装配", "资产准备完成，按布局逐件落位");
                }
                var before = new HashSet<Renderer>(coast.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy));
                storyLand.SetConstructionStage(id, stage);
                var appeared = coast.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy && !before.Contains(r))
                    .OrderBy(r => r.bounds.center.z).ToArray();
                foreach (var renderer in appeared) renderer.enabled = false;
                int batch = Mathf.Max(1, Mathf.CeilToInt(appeared.Length / 12f));
                for (int index = 0; index < appeared.Length; ++index)
                {
                    if (appeared[index] != null) appeared[index].enabled = true;
                    if (index % batch == 0) yield return new WaitForSecondsRealtime(.07f);
                }
                StoryEvent("construction-step", id + " | " + stage + " | " + steps[step]);
                yield return StoryHold(2.5f);
                StoryWorkshopDone(step, steps[step] + " · 已完成");
                yield return StoryShot("workshop-" + id + "-step-" + stage);
            }
            StoryWorkshopConfirmation("检查完成，确认应用这片新区域。", false);
            yield return StoryHold(2);
            StoryWorkshopConfirmation("你已确认 · 已应用到家园", true);
            StoryCompleteConstruction(id);
            yield return StoryHold(1.5f);
            StoryWorkshopClose();
        }

        IEnumerator StoryMovePlacedItems(Dictionary<string, Vector3> previous)
        {
            if (placedRoot == null) yield break;
            var changed = placedRoot.Cast<Transform>().Where(t => previous.ContainsKey(t.name) &&
                (t.localPosition - previous[t.name]).sqrMagnitude > .0001f).ToArray();
            if (changed.Length == 0) yield break;
            var destinations = changed.Select(t => t.localPosition).ToArray();
            float duration = Mathf.Max(.5f, storyConfig.holdScale * 1.4f);
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, time / duration);
                for (int i = 0; i < changed.Length; ++i)
                    changed[i].localPosition = Vector3.Lerp(previous[changed[i].name], destinations[i], progress);
                yield return null;
            }
            for (int i = 0; i < changed.Length; ++i) changed[i].localPosition = destinations[i];
        }

        void StoryCompleteConstruction(string id)
        {
            StoryCheck(storyLand.ConstructionStage(id) == 3,
                "confirmed construction reached its final stage: " + id);
            StoryEvent("construction-complete", id + " | 方案已应用");
        }
    }
}
