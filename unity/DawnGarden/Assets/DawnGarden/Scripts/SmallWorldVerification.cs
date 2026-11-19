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
        [Serializable] class V1Check {public string name,detail;public bool passed;}
        [Serializable] class V1Report {public bool passed;public string scope="native-runtime-neighbors-and-creation",utc,home;public V1Check[] checks;}
        List<V1Check> v1Checks=new List<V1Check>();
        void CheckV1(string name,bool passed,string detail=""){v1Checks.Add(new V1Check {name=name,passed=passed,detail=detail});Debug.Log("V1 verification "+name+": "+passed+" "+detail);}
        IEnumerator UntilV1(Func<bool> predicate,float seconds=25)
        {var deadline=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<deadline)yield return null;}
        IEnumerator RunV1Verification()
        {
            if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot)){Debug.LogError("Verification requires an isolated test root");Application.Quit(2);yield break;}
            yield return UntilV1(()=>Game?.Progress!=null&&!loading,30);
            if(Game==null){CheckV1("home_loaded",false);FinishV1Verification();yield break;}
            CheckV1("home_loaded",Game.homeId=="dawn-garden-starter-v1");
            CheckV1("old_saves_isolated",Game.SavePath.StartsWith(SmallWorldProfile.TestRoot));
            CheckV1("four_separate_continents",continentRoots.Length==4&&continentRoots.All(t=>t!=null));
            CheckV1("three_demo_planets",starRoot.transform.childCount==3);
            CheckV1("one_pet_with_four_clips",Game.actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0&&new[]{"Idle","Jog","Watering","Chest"}.All(n=>Game.clips.Any(c=>c.name==n)));
            var daily=new GardenProgress();CheckV1("daily_water_once",GardenSave.WaterToday(daily,"2026-01-01")&&!GardenSave.WaterToday(daily,"2026-01-01")&&daily.wateringDays==1);
            CheckV1("next_day_growth",GardenSave.WaterToday(daily,"2026-01-02")&&daily.wateringDays==2);
            CheckV1("clock_rollback_no_duplicate",!GardenSave.WaterToday(daily,"2026-01-01")&&daily.wateringDays==2);
            var water=Game.FindInteraction(GardenInteractionKind.Water);CheckV1("walk_to_water_requested",Game.GoToInteraction(water,true));
            yield return UntilV1(()=>GardenSave.WateredToday(Game.Progress),35);
            CheckV1("real_watering_completed",GardenSave.WateredToday(Game.Progress)&&Game.Progress.wateringDays==1);
            CheckV1("water_does_not_grant_land",Game.Progress.expansionPermits==0);
            CheckV1("watering_has_no_travel_gate",Profile.travelEnergy==3);
            var chest=Game.FindInteraction(GardenInteractionKind.Chest);CheckV1("walk_to_chest_requested",Game.GoToInteraction(chest,true));
            yield return UntilV1(()=>Game.Progress.chestOpened,35);
            CheckV1("starter_chest_once",Game.Progress.chestOpened&&Game.Progress.expansionPermits==1);
            var exp=Game.expansion;CheckV1("defer_permit_saved",exp.Defer()&&Game.Progress.expansionPermits==1&&!Game.Progress.firstExpansionUnlocked);
            exp.ShowOffer();CheckV1("accept_expansion",exp.Accept());yield return UntilV1(()=>!exp.IsRising,12);
            CheckV1("one_extension_only",Game.Progress.firstExpansionUnlocked&&Game.Progress.expansionPermits==0&&!exp.Accept());
            CheckV1("starter_sized_extension",exp.landCollider.bounds.size.x>=12&&exp.landCollider.bounds.size.z>=10);
            var seam=exp.land.Find("Natural continent connection");CheckV1("continuous_seam_exists",seam!=null&&seam.GetComponent<Collider>()!=null);
            var path=new NavMeshPath();CheckV1("continuous_seam_walkable",NavMesh.CalculatePath(Game.spawn.position,exp.finalPosition,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            CheckV1("walk_onto_new_land_requested",Game.WalkTo(exp.finalPosition+Vector3.right*.5f));yield return UntilV1(()=>!Game.Navigating,20);
            CheckV1("pet_walked_onto_new_land",Game.actor.position.x>9);
            Game.SaveNow();var savedPosition=Game.actor.position;Game.ReloadSaved();CheckV1("restore_progress_and_position",Game.Progress.firstExpansionUnlocked&&Vector3.Distance(Game.actor.position,savedPosition)<.4f&&GardenSave.WateredToday(Game.Progress));
            var energy=Profile.travelEnergy;var neighbour=(Profile.ownContinent+1)%4;
            yield return Visit(0,neighbour);yield return UntilV1(()=>Game!=null&&Game.isVisitor&&!loading,12);
            CheckV1("same_planet_visit_free",IsVisitor&&Profile.travelEnergy==energy);
            CheckV1("visitor_cannot_open_chest",!Game.TryInteract(Game.FindInteraction(GardenInteractionKind.Chest))&&Game.Progress.expansionPermits==0);
            CheckV1("other_theme_demo",Game.homeId=="ember-camp-starter-v1");
            yield return UntilV1(()=>residentActor!=null&&residentReady&&!neighborBusy,12);yield return new WaitForSecondsRealtime(1);
            CheckV1("resident_is_visible_with_player",residentActor!=null&&residentActor!=Game.actor.gameObject&&residentActor.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0);
            var target=worldTargets.First(t=>t.kind=="resident");
            CheckV1("walk_to_resident",Game.WalkTo(Reachable(residentAnchor+Vector3.back*.95f)));yield return UntilV1(()=>!Game.Navigating,20);
            yield return NeighborAction("greet");yield return NeighborAction("accept_quest");
            CheckV1("resident_quest_accepted",CurrentResident.greeted&&CurrentResident.questAccepted);
            var npcWater=Game.FindInteraction(GardenInteractionKind.Water);CheckV1("neighbor_watering_route",Game.GoToInteraction(npcWater,true));
            yield return UntilV1(()=>GardenSave.WateredToday(Game.Progress),35);yield return UntilV1(()=>!neighborBusy,8);yield return new WaitForSecondsRealtime(.6f);
            CheckV1("neighbor_care_persisted",CurrentResident.wateringDays==1);
            for(int i=0;i<3;i++)
            {
                var marker=worldTargets.First(t=>t.kind=="search"&&t.index==i);
                CheckV1("quest_item_route_"+i,Game.WalkTo(Reachable(marker.transform.position+Vector3.back*.5f)));yield return UntilV1(()=>!Game.Navigating,20);
                yield return Game.SmallGesture(()=>{});yield return NeighborAction("collect",i);
            }
            CheckV1("all_three_items_persisted",CurrentResident.collected.Length==3);
            Game.WalkTo(Reachable(residentAnchor+Vector3.back*.95f));yield return UntilV1(()=>!Game.Navigating,20);
            CheckV1("gift_waits_for_cooperative_activity",!CurrentResident.canReceiveGift);
            CheckV1("walk_to_hearth",Game.WalkTo(ritualAnchor));yield return UntilV1(()=>!Game.Navigating,20);
            OpenRitual();CheckV1("fire_has_temperature_game",ritualOpen&&CurrentResident.ritualKind=="heat");ritualHeat=.95f;ritualWasHeld=true;AdvanceHeat(false,.02f);CheckV1("overheat_is_retryable",ritualOpen&&!ritualSubmitting&&ritualHeat==0);
            ritualHeat=.65f;ritualHeatStarted=Time.unscaledTime-2.6f;ritualWasHeld=true;AdvanceHeat(false,.02f);yield return UntilV1(()=>!ritualSubmitting,8);
            CheckV1("temperature_game_saved",CurrentResident.ritualComplete);
            Game.WalkTo(Reachable(residentAnchor+Vector3.back*.95f));yield return UntilV1(()=>!Game.Navigating,20);
            yield return NeighborAction("receive_gift");yield return NeighborAction("receive_gift");
            CheckV1("gift_is_unique_and_reusable",CurrentResident.giftClaimed&&neighborWorld.gifts.Count(g=>g.residentId=="ajin")==1);
            yield return Visit(1,0);yield return UntilV1(()=>!loading&&residentActor!=null,12);CheckV1("cross_planet_free_and_real_resident",Profile.travelEnergy==energy&&CurrentResident?.id=="lumi"&&Game.homeId=="star-dew-neighbor-v2");yield return VerifyNeighborRitual("stars");
            yield return Visit(2,1);yield return UntilV1(()=>!loading&&residentActor!=null,12);CheckV1("second_star_has_resident",CurrentResident?.id=="lanlan"&&Game.homeId=="wind-court-neighbor-v2");yield return VerifyNeighborRitual("tune");
            yield return Visit(0,neighbour);yield return UntilV1(()=>!loading&&residentActor!=null,12);CheckV1("neighbor_progress_survives_revisit",CurrentResident.giftClaimed&&CurrentResident.collected.Length==3&&GardenSave.WateredToday(Game.Progress));
            yield return Visit(0,Profile.ownContinent);yield return UntilV1(()=>!loading,12);
            CheckV1("return_home_free",!IsVisitor&&Profile.travelEnergy==energy);
            CheckV1("owned_progress_survives_visits",Game.Progress.firstExpansionUnlocked&&GardenSave.WateredToday(Game.Progress));
            yield return SyncState();CheckV1("real_state_bridge",Connected);
            yield return UntilV1(()=>!editorBusy,15);editItems.Clear();editSeamSoftened=true;yield return PreviewPlacement(false);
            CheckV1("runtime_layout_and_soft_connection",previewValid&&Game.expansion.land.Find("Natural continent connection").localScale.z>1.1f,placementError);
            editSeamSoftened=false;yield return PreviewPlacement(false);
            CheckV1("connection_preview_is_reversible",previewValid&&Mathf.Abs(Game.expansion.land.Find("Natural continent connection").localScale.z-1)<.01f,placementError);
            yield return SmallWorldApi.Request("assets",null,json=>availableAssets=JsonUtility.FromJson<SmallWorldAssetList>(json).assets,_=>{});
            var source=(availableAssets??new SmallWorldAsset[0]).FirstOrDefault(a=>!string.IsNullOrEmpty(a.modelUrl));
            if(source!=null)
            {
                // Exercise the actual runtime GLB route without a baked prefab or any new paid generation.
                var runtimeAsset=JsonUtility.FromJson<SmallWorldAsset>(JsonUtility.ToJson(source));runtimeAsset.id="verification-runtime-only";runtimeAsset.hash="test-only";
                var staging=new GameObject("Runtime GLB verification").transform;staging.position=Vector3.down*100;GameObject imported=null;string error=null;
                yield return SmallWorldAssetLoader.Instantiate(runtimeAsset,library,staging,value=>imported=value,message=>error=message);
                CheckV1("runtime_glb_download_and_load",imported!=null&&imported.GetComponentsInChildren<Renderer>().Length>0,error??"");
                CheckV1("runtime_glb_simple_collider",imported!=null&&imported.GetComponentInChildren<Collider>()!=null);
                if(imported!=null&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)CheckV1("runtime_glb_materials",imported.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)));
                Destroy(staging.gameObject);
            }
            else CheckV1("runtime_glb_source_available",false);
            // Explicit guide commands execute once; old answers are never replayed on opening chat.
            ChatOpen=true;var action=new GuideTurn {id="qa-auto-memory-action",state="complete",action="show_memory"};QueueGuideAction(action);yield return UntilV1(()=>Profile.appliedGuideTurns.Contains(action.id),8);
            CheckV1("guide_command_automatic_once",showMemories&&Profile.appliedGuideTurns.Count(x=>x==action.id)==1);showMemories=false;ChatOpen=false;QueueGuideAction(action);CheckV1("guide_command_not_replayed",queuedGuideAction==null);
            var reaction=Game.actor.GetComponent<SmallWorldPetResponse>();var position=Game.actor.position;reaction.Respond("gift");yield return null;CheckV1("pet_has_celebration_pose",reaction.Responding&&reaction.ResponseCount>0);yield return new WaitForSeconds(2.2f);CheckV1("pet_reaction_preserves_physics",Vector3.Distance(position,Game.actor.position)<.15f);
            CheckV1("open_unified_placement",OpenPlacement(false));
            var gift=neighborWorld.gifts.FirstOrDefault();var giftAsset=availableAssets.FirstOrDefault(a=>a.id==gift?.assetId);
            if(giftAsset!=null)
            {
                editItems=new List<SmallWorldItem> {new SmallWorldItem {instanceId="qa-gift",giftId=gift.id,assetId=giftAsset.id,assetVersion=giftAsset.version,assetHash=giftAsset.hash,tint=gift.tint,localScale=Vector3.one*gift.scale,localPosition=new Vector3(1.7f,0,-1.8f)}};
                yield return PreviewPlacement(false);CheckV1("gift_usable_in_home",previewValid,placementError);
                var originalRoot=placedRoot;editItems[0].localPosition.x+=.25f;yield return PreviewPlacement(false);
                CheckV1("small_edit_preserves_existing_models",previewValid&&placedRoot==originalRoot);
                yield return ClosePlacement();CheckV1("draft_exit_returns_to_dialogue",ChatOpen&&!editorOpen);
                OpenPlacement(false);yield return PreviewPlacement(false);
                CheckV1("draft_survives_close_and_reopen",editItems.Count==1&&Mathf.Abs(editItems[0].localPosition.x-1.95f)<.01f);
                yield return FinalizePlacement();CheckV1("gift_layout_applied",committedLayout.version==1&&committedLayout.instances[0].giftId==gift.id);
                yield return RestoreOwnedLayout();CheckV1("applied_gift_survives_reload",committedLayout.instances.Any(i=>i.giftId==gift.id));
                yield return LoadMemories();CheckV1("scene_memory_updates_automatically",memoryReply?.scenes?.Length>0);
            }
            else CheckV1("gift_palette_available",false);
            // A prepared version opens automatically only when it is safe and does not overwrite a draft.
            var realCreation=creation;var initialItem=JsonUtility.FromJson<SmallWorldLayout>(DraftJson()).instances[0];initialItem.instanceId="initial-seed";initialItem.localPosition=new Vector3(2.5f,0,2);
            var beforePreparedCount=committedLayout.instances.Length;
            creation=new CreationState {id="qa-preview-0000000000000000",revision=1,phase="runtime_editing",initialLayout=new[]{initialItem}};ChatOpen=true;Profile.previewHandledKey="";
            yield return AutoEnterPreparedPreview();CheckV1("prepared_scene_opens_without_extra_click",editorOpen&&Profile.previewHandledKey==PreviewKey());
            CheckV1("prepared_assets_seeded_exactly_once",editItems.Count==beforePreparedCount+1&&editItems.Count(i=>i.instanceId=="qa-previ-initial-seed")==1,"Initial proposal adds one instance before leaving preview.");
            yield return ClosePlacement();
            var handled=Profile.previewHandledKey;yield return AutoEnterPreparedPreview();CheckV1("preview_does_not_reopen_after_leaving",!editorOpen&&Profile.previewHandledKey==handled);
            creation=realCreation;ChatOpen=false;UpdateControls();
            FinishV1Verification();
        }
        IEnumerator VerifyNeighborRitual(string kind)
        {
            yield return UntilV1(()=>residentReady,12);CheckV1(kind+"_resident_ready",residentReady,lastNeighborError);Game.PlaceActor(Reachable(residentAnchor+Vector3.back*.9f),180);yield return NeighborAction("greet");yield return NeighborAction("accept_quest");
            for(int i=0;i<3;i++){var t=worldTargets.First(x=>x.kind=="search"&&x.index==i);Game.PlaceActor(t.transform.position,180);yield return NeighborAction("collect",i);}
            Game.PlaceActor(ritualAnchor,180);OpenRitual();CheckV1(kind+"_has_distinct_activity",ritualOpen&&CurrentResident.ritualKind==kind);
            if(kind=="stars"){ritualDemoUntil=Time.unscaledTime;ChooseStar((CurrentResident.ritualPattern[0]+1)%3);CheckV1("wrong_star_order_can_retry",ritualOpen&&ritualInput==0);foreach(var i in CurrentResident.ritualPattern)ChooseStar(i);}
            else{ritualTuning=new[]{-1,-1,-1};CheckV1("wrong_wind_tuning_can_retry",!TryRitualTuning());ritualTuning=CurrentResident.ritualPattern.ToArray();TryRitualTuning();}
            yield return UntilV1(()=>!ritualSubmitting,8);CheckV1(kind+"_activity_persisted",CurrentResident.ritualComplete);
        }
        void FinishV1Verification()
        {
            var report=new V1Report {passed=v1Checks.All(c=>c.passed),utc=DateTime.UtcNow.ToString("o"),home=Game?.homeId,checks=v1Checks.ToArray()};
            Directory.CreateDirectory(SmallWorldProfile.Root);File.WriteAllText(Path.Combine(SmallWorldProfile.Root,"runtime-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
    }
}
