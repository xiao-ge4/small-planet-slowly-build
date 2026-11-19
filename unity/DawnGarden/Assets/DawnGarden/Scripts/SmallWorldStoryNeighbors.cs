using System.Collections;
using System.Linq;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        // The story runner supplies captions/cameras and preset conversation. Every
        // quest mutation below still goes through the ordinary neighbor service.
        IEnumerator StoryNeighbors()
        {
            StoryCheck(!string.IsNullOrEmpty(SmallWorldProfile.TestRoot), "neighbor story uses an isolated save");
            StoryCheck(Game != null && Connected && !IsVisitor, "neighbor journey starts at the player's home");
            yield return LoadNeighbors();
            var ids = new[] { "ajin", "lumi", "lanlan" };
            StoryCheck(ids.All(id => neighborWorld.residents.Any(r => r.id == id && !r.giftClaimed &&
                r.wateringDays == 0 && r.collected.Length == 0 && !r.ritualComplete)),
                "three untouched neighbor quests are ready for genuine first visits");
            var travelEnergyBefore = Profile.travelEnergy;
            var ajin = neighborWorld.residents.First(r => r.id == "ajin");
            StoryCheck(ajin.planet == 0 && ajin.continent != Profile.ownContinent,
                "Ajin lives on another continent of the home planet");

            StoryChapter("出门认识朋友", "同一颗星球上，四块大陆隔海相望。转一转，看看朋友住在哪里。");
            yield return StoryNeighborAtlas(ajin.continent);
            yield return StoryNeighborVisit(0, ajin.continent, "ajin", 9.3f);

            StoryChapter("阿烬 · 暖焰营地", "先给暖焰芽浇水，再找齐路边的三颗温润石。");
            yield return StoryHold(2);
            yield return StoryNeighborMeet();
            yield return StorySay("阿烬，我来串门啦。今天一起做点什么？",
                "欢迎。陪我给新芽浇一回水，再找三颗温润石吧。炉边给你留了位置。", 4.5f);
            yield return StoryNeighborAccept();
            yield return StoryNeighborCare();
            yield return StoryNeighborCollect();
            StoryNote("把找到的暖石放在炉边：按住添火，温度进入暖色区域后松开。");
            yield return StoryNeighborOpenRitual();
            yield return StoryHold(1.2f);
            try
            {
                showcaseHeatHeld = true;
                yield return UntilV1(() => ritualHeat >= .67f, 6);
                StoryCheck(ritualHeat >= .55f && ritualHeat <= .8f, "warm stones reach the real minigame's warm zone");
            }
            finally { showcaseHeatHeld = false; }
            yield return StoryNeighborRitualComplete("heat");
            yield return StoryNeighborGift("ajin-lantern", "三颗暖石都温好了。我们一起分享吧。",
                "温温的，刚刚好。这盏暖焰灯送给你；放在家里，也想起今天的炉火。");

            StoryChapter("露米 · 星露小筑", "打开星际航线，去另一颗星球拜访喜欢观察星光的露米。");
            yield return StoryNeighborStarRoute(1, 0);
            yield return StoryNeighborVisit(1, 0, "lumi", 17.2f);
            yield return StoryHold(1.5f);
            yield return StoryNeighborMeet();
            yield return StorySay("露米，听说昨晚有星光落进花园？",
                "嗯，三颗星露石就藏在小路旁。我们先照料新芽，再把它们找回来。", 4.5f);
            yield return StoryNeighborAccept();
            StoryEvent("montage-start","lumi-care-collect");
            yield return StoryNeighborCare();
            yield return StoryNeighborCollect();
            StoryEvent("montage-end","lumi-care-collect");
            StoryNote("先看三点星光依次亮起，再按相同顺序唤醒它们。");
            yield return StoryNeighborOpenRitual();
            yield return UntilV1(() => Time.unscaledTime > ritualDemoUntil, 5);
            yield return StoryShot("neighbors-lumi-star-sequence");
            foreach (var star in CurrentResident.ritualPattern.ToArray())
            {
                yield return StoryHold(.85f);
                ChooseStar(star);
            }
            yield return StoryNeighborRitualComplete("stars");
            yield return StoryNeighborGift("lumi-flowers", "我记住了，它们的光接在一起了。",
                "谢谢你。把这簇星露花带回去，让今天的小小星光也住进你的花园。"
            );

            StoryChapter("岚岚 · 风铃石庭", "再去风铃之星。这里的礼物，是一起听过的一段声音。");
            yield return StoryNeighborStarRoute(2, 1);
            yield return StoryNeighborVisit(2, 1, "lanlan", 15);
            yield return StoryHold(1.5f);
            yield return StoryNeighborMeet();
            yield return StorySay("岚岚，石庭里的风声很好听。",
                "那就慢慢听。帮新芽喝一口水，找回三颗风铃石，我们一起调一段和弦。", 4.5f);
            yield return StoryNeighborAccept();
            StoryEvent("montage-start","lanlan-care-collect");
            yield return StoryNeighborCare();
            yield return StoryNeighborCollect();
            StoryEvent("montage-end","lanlan-care-collect");
            StoryNote("听一遍风铃示范，调整三枚音符，再一起奏响。");
            yield return StoryNeighborOpenRitual();
            yield return StoryHold(2.8f);
            ritualHints = true;
            for (var i = 0; i < 3; i++)
            {
                ritualTuning[i] = CurrentResident.ritualPattern[i];
                yield return StoryHold(.8f);
            }
            yield return StoryShot("neighbors-lanlan-wind-chimes");
            PlayRitualNotes(ritualTuning);
            yield return StoryHold(1.4f);
            StoryCheck(TryRitualTuning(), "wind chime tuning submitted through the normal minigame");
            yield return StoryNeighborRitualComplete("tune");
            yield return StoryNeighborGift("lanlan-stone", "合上了。谢谢你陪我听这一段风。",
                "也谢谢你。这块风铃景石送给你，放在家里时，就当风又来打招呼了。"
            );

            yield return LoadNeighbors();
            StoryCheck(neighborWorld.gifts.Length == 3 && ids.All(id =>
                neighborWorld.gifts.Count(g => g.residentId == id) == 1),
                "three distinct gifts are persisted by their actual neighbors");
            StoryCheck(ids.All(id => neighborWorld.residents.Any(r => r.id == id && r.giftClaimed &&
                r.wateringDays > 0 && r.collected.Distinct().Count() == 3 && r.ritualComplete)),
                "all three neighbors remember care, three finds, minigame and gift");
            StoryCheck(Profile.travelEnergy == travelEnergyBefore, "same-planet and interplanetary visits remain free");
            StoryNeighborClear();
            StoryNote("暖焰灯、星露花簇、风铃景石：三份亲手获得的纪念，现在可以一起带回家。");
            yield return StoryHold(2);
        }

        void StoryNeighborClear()
        {
            ClearMoviePanels();
            creatorOpen = homecomingJournal = false;
            pendingWorldTarget = null;
            UpdateControls();
        }

        IEnumerator StoryNeighborAtlas(int destination)
        {
            StoryNeighborClear();
            OpenWorld();
            StoryCheck(WorldOpen && planetRoot.activeSelf && continentRoots.Length == 4,
                "four-continent world map is open");
            for (var step = 0; step < 4; step++)
            {
                var index = (Profile.ownContinent + step) % 4;
                var fromYaw = mapYaw;
                var fromPitch = mapPitch;
                FocusMapContinent(index);
                var toYaw = mapYaw;
                var toPitch = mapPitch;
                var started = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - started < 1.15f)
                {
                    var u = Mathf.SmoothStep(0, 1, (Time.realtimeSinceStartup - started) / 1.15f);
                    mapYaw = Mathf.LerpAngle(fromYaw, toYaw, u);
                    mapPitch = Mathf.LerpAngle(fromPitch, toPitch, u);
                    yield return null;
                }
                mapYaw = toYaw;
                mapPitch = toPitch;
                yield return StoryHold(.45f);
            }
            FocusMapContinent(destination);
            yield return StoryHold(1.2f);
            yield return StoryShot("neighbors-four-continents");
        }

        IEnumerator StoryNeighborStarRoute(int planet, int continent)
        {
            StoryNeighborClear();
            OpenWorld();
            stars = true;
            planetRoot.SetActive(false);
            starRoot.SetActive(true);
            StoryCheck(WorldOpen && starRoot.activeSelf, "star routes visible before planet " + planet);
            yield return StoryHold(3);
            yield return StoryShot("neighbors-star-route-" + planet);
            mapPlanet = planet;
            stars = false;
            starRoot.SetActive(false);
            planetRoot.SetActive(true);
            FocusMapContinent(continent);
            yield return StoryHold(1.3f);
        }

        IEnumerator StoryNeighborVisit(int planet, int continent, string id, float hour)
        {
            yield return Visit(planet, continent);
            yield return UntilV1(() => !loading && residentReady && !neighborPreparing && !neighborBusy, 35);
            StoryCheck(IsVisitor && CurrentResident?.id == id && residentReady && !neighborPreparing,
                id + " household loaded through normal travel");
            VerifyStoryResidentCharacter(id);
            StoryNeighborClear();
            Game.atmosphere.Preview(hour);
            StoryCamera(new Vector3(0, .6f, 0), 17, 160, 43);
        }

        void VerifyStoryResidentCharacter(string id)
        {
            // A successful visit must not silently record the player fallback
            // when a newly imported neighbor is missing from the built game.
            var resource=id=="ajin"?"Homecoming/Ajin":id=="lumi"?"Neighbors/Lumi":"Neighbors/Lanlan";
            var prefab=Resources.Load<GameObject>(resource);
            var rig=residentActor!=null?residentActor.GetComponentInChildren<Animator>():null;
            var expected=prefab!=null?prefab.GetComponentInChildren<Animator>():null;
            StoryCheck(expected!=null&&rig!=null&&rig.runtimeAnimatorController==expected.runtimeAnimatorController,
                id+" uses its dedicated imported character, not the player fallback");
            var skins=residentActor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var playerMeshes=Game.actor.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s=>s.sharedMesh).ToArray();
            var expectedMeshes=prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s=>s.sharedMesh).ToArray();
            StoryCheck(skins.Length>0&&skins.All(s=>s.sharedMesh!=null&&s.bones.Length>=12&&
                expectedMeshes.Contains(s.sharedMesh)&&!playerMeshes.Contains(s.sharedMesh)),
                id+" has its own skinned model and usable bones");
            StoryCheck(new[]{"Idle","Walk","Jog","Greet","Water"}.All(s=>rig.HasState(0,Animator.StringToHash(s)))&&
                Resources.Load<Texture2D>("CharacterPortraits/"+id)!=null,
                id+" has five playable motions and its actual model portrait");
            StoryEvent("character-asset",id+" | "+resource+" | "+string.Join(",",skins.Select(s=>s.sharedMesh.name)));
        }

        IEnumerator StoryNeighborWalk(Vector3 destination)
        {
            StoryNeighborClear();
            StoryCheck(Game.WalkTo(destination), CurrentResident.id + " walking route requested");
            yield return UntilV1(() => !Game.Navigating && !Game.Busy, 30);
            var delta = Game.actor.position - destination;
            delta.y = 0;
            if (delta.magnitude > 1.6f)
            {
                StoryCheck(Game.WalkTo(Game.spawn.position), CurrentResident.id + " central path retry requested");
                yield return UntilV1(() => !Game.Navigating && !Game.Busy, 25);
                StoryCheck(Game.WalkTo(destination), CurrentResident.id + " destination route retried");
                yield return UntilV1(() => !Game.Navigating && !Game.Busy, 25);
                delta = Game.actor.position - destination;
                delta.y = 0;
            }
            StoryCheck(!Game.Navigating && delta.magnitude < 1.6f,
                CurrentResident.id + " reached interaction by walking (" + delta.magnitude.ToString("F2") + "m)");
        }

        IEnumerator StoryNeighborMeet()
        {
            var meetingPoint = Reachable(residentAnchor + Vector3.back * .85f);
            // Keep both silhouettes apart, with the resident's face toward the
            // camera so its eyes and crest are visible during the introduction.
            const float meetYaw = 62f;
            var cameraRight = Quaternion.Euler(0, meetYaw, 0) * Vector3.right;
            StoryCamera((residentAnchor + meetingPoint) * .5f + Vector3.up * .65f + cameraRight * 1.05f,
                8.8f, meetYaw, 29);
            yield return StoryNeighborWalk(meetingPoint);
            StoryCheck(Vector2.Distance(new Vector2(Game.actor.position.x, Game.actor.position.z),
                new Vector2(residentAnchor.x, residentAnchor.z)) < 2.3f,
                CurrentResident.id + " conversation happens beside the resident");
            yield return UseWorldTarget(worldTargets.First(t => t.kind == "resident"));
            yield return UntilV1(() => !neighborBusy, 12);
            StoryCheck(ChatOpen && chatResidentId == CurrentResident.id && CurrentResident.greeted,
                CurrentResident.id + " greeting persisted through real resident interaction");
        }

        IEnumerator StoryNeighborAccept()
        {
            yield return NeighborAction("accept_quest", -1, true);
            StoryCheck(CurrentResident.questAccepted, CurrentResident.id + " quest accepted");
            yield return StoryHold(.7f);
        }

        IEnumerator StoryNeighborCare()
        {
            StoryNeighborClear();
            var water = Game.FindInteraction(GardenInteractionKind.Water);
            StoryCamera(water.approach.position + Vector3.up * .6f, 11, 165, 42);
            StoryNote("先照料" + CurrentResident.name + "的新芽。精灵会走过去，拿起水壶完成浇水。");
            StoryCheck(Game.GoToInteraction(water, true), CurrentResident.id + " real watering route requested");
            yield return UntilV1(() => GardenSave.WateredToday(Game.Progress) && !Game.Busy &&
                !neighborBusy && CurrentResident.wateringDays > 0, 40);
            StoryCheck(GardenSave.WateredToday(Game.Progress) && CurrentResident.wateringDays > 0 && !neighborBusy,
                CurrentResident.id + " actual watering saved both locally and by neighbor service");
            yield return StoryHold(1.2f);
        }

        IEnumerator StoryNeighborCollect()
        {
            var targets = worldTargets.Where(t => t.kind == "search").OrderBy(t => t.index).ToArray();
            StoryCheck(targets.Length == 3 && targets.All(t => t.gameObject.activeSelf &&
                Physics.GetIgnoreCollision(Game.body, t.GetComponent<Collider>())),
                CurrentResident.id + " has three distinct reachable, nonblocking search targets");
            foreach (var target in targets)
            {
                StoryNote(CurrentResident.questItemName + " · " + (CurrentResident.collected.Length + 1) + " / 3");
                StoryCamera((Game.actor.position + target.transform.position) * .5f + Vector3.up * .6f, 12, 170, 43);
                yield return StoryNeighborWalk(target.transform.position);
                yield return UseWorldTarget(target);
                yield return UntilV1(() => CurrentResident.collected.Contains(target.index) && !neighborBusy, 12);
                StoryCheck(CurrentResident.collected.Contains(target.index) && !target.gameObject.activeSelf,
                    CurrentResident.id + " search item " + target.index + " collected by real proximity interaction");
                yield return StoryHold(.65f);
            }
            StoryCheck(CurrentResident.collected.Distinct().Count() == 3,
                CurrentResident.id + " all three quest objects saved");
            yield return StoryShot("neighbors-" + CurrentResident.id + "-three-found");
        }

        IEnumerator StoryNeighborOpenRitual()
        {
            StoryCamera(ritualAnchor + Vector3.up * .6f, 12, 155, 42);
            yield return StoryNeighborWalk(ritualAnchor);
            yield return UseWorldTarget(ritualTarget);
            StoryCheck(ritualOpen, CurrentResident.id + " minigame opened at its actual world station");
            if (CurrentResident.ritualKind == "heat") yield return StoryShot("neighbors-ajin-warm-stones");
        }

        IEnumerator StoryNeighborRitualComplete(string kind)
        {
            yield return UntilV1(() => !ritualSubmitting && !ritualOpen && CurrentResident.ritualComplete, 15);
            StoryCheck(CurrentResident.ritualKind == kind && CurrentResident.ritualComplete && !ritualOpen,
                CurrentResident.id + " " + kind + " minigame validated and persisted");
            yield return StoryHold(1.2f);
        }

        IEnumerator StoryNeighborGift(string giftId, string player, string reply)
        {
            yield return StoryNeighborMeet();
            StoryCheck(CurrentResident.canReceiveGift, CurrentResident.id + " sharing unlocked only after all real tasks");
            yield return NeighborAction("receive_gift", -1, true);
            StoryCheck(CurrentResident.giftClaimed && neighborWorld.gifts.Any(g =>
                g.id == giftId && g.residentId == CurrentResident.id),
                CurrentResident.id + " earned gift " + giftId + " received through normal sharing");
            StoryNote("收到「" + CurrentResident.giftName + "」· 回家后可以反复用来布置自己的土地。");
            yield return StorySay(player, reply, 4.3f);
            yield return StoryShot("neighbors-" + CurrentResident.id + "-gift");
        }
    }
}
