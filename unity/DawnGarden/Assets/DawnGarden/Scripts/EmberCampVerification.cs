using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed class EmberCampVerification:MonoBehaviour
    {
        DawnGardenGame game;
        string output;
        int errors;
        readonly List<Check> checks=new List<Check>();
        public void Begin(DawnGardenGame value,string folder)
        {
            game=value;output=folder;Directory.CreateDirectory(output);
            Application.logMessageReceived+=OnLog;
            var path=Path.Combine(output,"ember-state-test.json");if(File.Exists(path))File.Delete(path);
            game.inputEnabled=false;game.Initialize(path);StartCoroutine(Run());
        }
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void Record(string name,bool pass,string detail=""){checks.Add(new Check{name=name,passed=pass,detail=detail});}
        IEnumerator Run()
        {
            yield return new WaitForSeconds(.5f);
            var a=game.atmosphere;var effects=FindFirstObjectByType<EmberCampEffects>();var exp=game.expansion;
            Record("camp_identity",game.homeId=="ember-camp-starter-v1"&&game.homeTitle=="暖焰营地"&&game.Progress.homeId==game.homeId);
            Record("independent_save_path",GardenSave.PathFor(game.homeId)!=GardenSave.DefaultPath);
            var skin=game.actor.GetComponentInChildren<SkinnedMeshRenderer>();
            Record("pet_animated",skin!=null&&skin.bones.Length>10&&game.animator.runtimeAnimatorController!=null&&game.clips.Any(c=>c.name=="Watering")&&game.clips.Any(c=>c.name=="Chest"),"Reused generic skeleton; Humanoid Avatar is not required.");
            Record("capture_resolution",Screen.width>=1280&&Screen.height>=720,Screen.width+"x"+Screen.height);
            Record("valid_render_materials",FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.shader!=null&&m.shader.isSupported));
            Record("five_formal_assets",new[]{"Camp Cream Tent","Camp Amber Monument","Ember Tree 1","Ember Tree 2","Ember Tree 3"}.All(n=>GameObject.Find(n)!=null)&&game.FindInteraction(GardenInteractionKind.Water).visual.name=="ember-seedling");
            Record("starter_navigation",NavMesh.CalculateTriangulation().vertices.Length>100);
            Record("camp_spawn_grounded",game.body.isGrounded&&game.actor.position.y>-.1f);
            Record("locked_extension_hidden",!exp.land.gameObject.activeSelf&&!NavMesh.SamplePosition(exp.WalkTarget,out _,.3f,NavMesh.AllAreas));
            game.showHud=false;game.orbit.overview=true;game.orbit.distance=16.0f;game.orbit.yaw=180;game.orbit.pitch=39;
            a.Preview(7.5f);yield return new WaitForSeconds(.6f);yield return Capture("ember-morning.png");
            yield return InspectVisibleSteam(effects.springSteam);
            yield return InspectRedSea();
            var island=GameObject.Find("Walkable Island");
            Record("sand_surface_matches_collision",island.GetComponent<MeshFilter>().sharedMesh==island.GetComponent<MeshCollider>().sharedMesh);
            game.orbit.enabled=false;
            game.worldCamera.transform.position=new Vector3(6.3f,5.0f,10.3f);game.worldCamera.transform.LookAt(new Vector3(1.7f,0,2.4f));
            yield return Capture("ember-coast-detail.png");
            game.worldCamera.transform.position=new Vector3(-.2f,3.6f,6.3f);game.worldCamera.transform.LookAt(new Vector3(-.1f,0,1.4f));
            yield return Capture("ember-sand-detail.png");
            game.worldCamera.transform.position=new Vector3(1.4f,.90f,3.6f);game.worldCamera.transform.LookAt(new Vector3(1.4f,.01f,2.5f));
            yield return Capture("ember-soil-grains-close.png");
            game.worldCamera.transform.position=new Vector3(6.4f,1.65f,5.7f);game.worldCamera.transform.LookAt(new Vector3(4.1f,-.25f,2.9f));
            yield return Capture("ember-shore-join.png");
            game.orbit.enabled=true;yield return null;
            var angles=new[]{90f,0f,270f};
            for(var i=0;i<angles.Length;i++){game.orbit.yaw=angles[i];yield return new WaitForSeconds(.4f);yield return Capture("ember-angle-"+i+".png");}
            game.orbit.yaw=180;a.Preview(12);yield return new WaitForSeconds(.3f);yield return Capture("ember-noon.png");
            Record("daylight_and_water",a.sun.enabled&&a.Daylight>.99f);
            a.Preview(17.7f);yield return new WaitForSeconds(.3f);yield return Capture("ember-sunset.png");
            a.Preview(22);yield return new WaitForSeconds(.5f);yield return Capture("ember-night.png");
            Record("night_moon_and_fire",a.moon.enabled&&!a.sun.enabled&&effects.firelight.intensity>1.5f);
            Record("steam_is_live",effects.springSteam.isPlaying&&effects.springSteam.particleCount>0,"particles="+effects.springSteam.particleCount);
            var flameScale=effects.flameTongues[0].localScale;yield return new WaitForSeconds(.3f);
            Record("fire_moves",Vector3.Distance(effects.flameTongues[0].localScale,flameScale)>.01f);
            Record("camp_music_loops",a.morningMusic.isPlaying&&a.nightMusic.isPlaying&&a.morningMusic.clip.name=="EmberMorning"&&a.nightMusic.clip.name=="EmberMoonlight"&&a.morningMusic.loop&&a.morningMusic.clip.length>90);
            var muted=a.Muted;a.SetMuted(true,false);yield return null;yield return null;Record("camp_music_mutes",a.morningMusic.volume==0&&a.nightMusic.volume==0);a.SetMuted(muted,false);
            a.Preview(7.5f);game.showHud=true;game.PlaceActor(game.spawn.position,180);
            var water=game.FindInteraction(GardenInteractionKind.Water);var chest=game.FindInteraction(GardenInteractionKind.Chest);
            Record("far_interaction_rejected",!game.TryInteract(water));
            // NavMesh must reject the spring and solid tent footprint.
            var pond=GameObject.Find("Pond No Walk Volume").transform.position;
            Record("hot_spring_not_walkable",!NavMesh.SamplePosition(new Vector3(pond.x,0,pond.z),out _,.20f,NavMesh.AllAreas));
            var tent=GameObject.Find("Camp Cream Tent");
            Record("tent_not_walkable",!NavMesh.SamplePosition(tent.transform.position,out _,.2f,NavMesh.AllAreas));
            // Walk into the actual tent collision volume with the CharacterController.
            var box=tent.GetComponent<BoxCollider>();var localEdge=box.center.z+box.size.z*.5f;
            game.PlaceActor(tent.transform.TransformPoint(new Vector3(box.center.x,.03f,localEdge+.6f)),180);game.navigator.enabled=false;
            for(var i=0;i<45;i++){game.body.Move(-tent.transform.forward*.035f+Vector3.down*.015f);yield return null;}
            Record("physical_tent_collision",tent.transform.InverseTransformPoint(game.actor.position).z>localEdge+game.body.radius-.08f,game.actor.position.ToString("F3"));
            game.PlaceActor(game.spawn.position,180);
            var route=game.GoToInteraction(water,true);
            var trees=FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.transform.GetComponentsInParent<Transform>().Any(t=>t.name.StartsWith("Ember Tree "))).ToArray();
            var treesVisible=true;var deadline=Time.realtimeSinceStartup+20;
            while(!(game.Busy&&game.Motion=="Watering"&&game.ActionTime>1.6f)&&Time.realtimeSinceStartup<deadline)
            {treesVisible&=trees.All(r=>!r.forceRenderingOff);yield return null;}
            Record("trees_remain_visible_on_seedling_route",trees.Length>=3&&treesVisible&&trees.All(r=>!r.forceRenderingOff));
            var rotations=skin.bones.Select(b=>b.localRotation).ToArray();yield return new WaitForSeconds(.35f);
            var angle=skin.bones.Select((b,i)=>Quaternion.Angle(rotations[i],b.localRotation)).Max();
            Record("pet_bones_actually_move",angle>1,"Watering max joint delta="+angle.ToString("F3"));
            yield return Capture("ember-watering.png");
            game.orbit.overview=false;game.orbit.distance=7;game.orbit.yaw=110;game.showHud=false;
            yield return new WaitForSeconds(.35f);yield return Capture("ember-watering-close.png");
            game.orbit.overview=true;game.orbit.distance=16;game.orbit.yaw=180;game.showHud=true;
            yield return Await(()=>game.Progress.watered&&!game.Busy,8);
            Record("watering_with_existing_motion",route&&game.Progress.watered&&!game.gripSocket.gameObject.activeSelf);
            yield return Await(()=>water.visual.localScale.x>1.49f,3);
            Record("ember_bud_grows",water.visual.localScale.x>1.49f);
            Record("water_does_not_grant_permit",game.Progress.expansionPermits==0);
            route=game.GoToInteraction(chest,true);yield return Await(()=>game.Busy&&game.Motion=="Chest"&&game.ActionTime>.8f,20);
            yield return Capture("ember-opening-chest.png");
            yield return Await(()=>game.Progress.chestOpened&&!game.Busy,7);
            Record("chest_grants_one_permit",route&&game.Progress.starterRewardGranted&&game.Progress.expansionPermits==1);
            Record("chest_lid_stays_open",Mathf.Abs(Mathf.DeltaAngle(0,chest.lid.localEulerAngles.x))>70);
            Record("expansion_offer_visible",exp.PromptOpen);
            yield return Capture("ember-expansion-offer.png");
            var deferred=exp.Defer();game.ReloadSaved();yield return null;exp.OpenBag();
            Record("permit_stored_and_reopened",deferred&&exp.BagOpen&&game.Progress.expansionPermits==1&&!game.Progress.expansionOfferPending);
            yield return Capture("ember-bag.png");
            var accepted=exp.Accept();Record("permit_consumed_once",accepted&&game.Progress.expansionPermits==0&&!exp.Accept());
            StartCoroutine(RecordExpansion());
            Record("reveal_locks_movement",exp.IsRising&&!game.WalkTo(exp.WalkTarget)&&!exp.landCollider.enabled);
            yield return new WaitForSeconds(4.0f);yield return Capture("ember-expansion-rising.png");
            yield return Await(()=>!exp.IsRising,7);
            Record("expanded_ground_ready",exp.landCollider.enabled&&exp.land.gameObject.activeSelf);
            var path=new NavMeshPath();Record("expansion_connected",NavMesh.CalculatePath(game.actor.position,exp.WalkTarget,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            yield return new WaitForSeconds(.5f);yield return Capture("ember-expanded.png");
            route=game.WalkTo(exp.WalkTarget);yield return Await(()=>!game.Navigating,20);
            Record("walk_to_new_camp_land",route&&Vector3.Distance(game.actor.position,exp.WalkTarget)<.3f&&game.body.isGrounded,game.actor.position.ToString("F3"));
            game.SaveNow();var position=game.actor.position;game.ReloadSaved();yield return new WaitForSeconds(.2f);
            Record("camp_reload_progress",game.Progress.watered&&game.Progress.chestOpened&&game.Progress.firstExpansionUnlocked&&game.Progress.expansionPermits==0);
            Record("camp_reload_position",Vector3.Distance(position,game.actor.position)<.35f);
            Record("reward_and_expansion_idempotent",!game.GoToInteraction(chest,true)&&!exp.Accept());
            bool wrongHomeRejected=false;try{GardenSave.Load(game.SavePath);}catch(InvalidDataException){wrongHomeRejected=true;}
            Record("dawn_cannot_load_camp_save",wrongHomeRejected);
            var interrupted=Path.Combine(output,"ember-interrupted-test.json");GardenSave.Write(interrupted,new GardenProgress{homeId=game.homeId,chestOpened=true,starterRewardGranted=true,expansionPermits=1});
            game.Initialize(interrupted);exp.ShowOffer();exp.Accept();yield return new WaitForSeconds(1.4f);game.ReloadSaved();yield return null;
            Record("reveal_interruption_restores_final_land",game.Progress.firstExpansionUnlocked&&game.Progress.expansionPermits==0&&!exp.IsRising&&exp.landCollider.enabled);
            a.UseRealTime();Record("camp_real_24h_clock",a.followRealTime&&Mathf.Abs(a.CurrentHour-GardenAtmosphere.HourAt(DateTimeOffset.UtcNow,8))<.01f);
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,errors=errors,checks=checks.ToArray(),passed=errors==0&&checks.All(c=>c.passed),isolatedSave=game.SavePath};
            File.WriteAllText(Path.Combine(output,"ember-runtime.json"),JsonUtility.ToJson(report,true));
        }
        IEnumerator Await(Func<bool> condition,float seconds){var end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator InspectVisibleSteam(ParticleSystem ps)
        {
            yield return new WaitForEndOfFrame();
            var camera=game.worldCamera;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1440,810,24);var tex=new Texture2D(1440,810,TextureFormat.RGB24,false);
            var renderer=ps.GetComponent<Renderer>();var wasHidden=renderer.forceRenderingOff;
            try
            {
                camera.targetTexture=target;RenderTexture.active=target;
                renderer.forceRenderingOff=true;camera.Render();tex.ReadPixels(new Rect(0,0,1440,810),0,0);tex.Apply();var before=tex.GetPixels32();
                renderer.forceRenderingOff=false;camera.Render();tex.ReadPixels(new Rect(0,0,1440,810),0,0);tex.Apply();var after=tex.GetPixels32();
                var changed=0;for(var i=0;i<before.Length;i++)if(Mathf.Abs(after[i].r-before[i].r)+Mathf.Abs(after[i].g-before[i].g)+Mathf.Abs(after[i].b-before[i].b)>18)changed++;
                Record("steam_visible_in_overview",changed>1000,"visible changed pixels="+changed);
            }
            finally{renderer.forceRenderingOff=wasHidden;camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(tex);}
        }
        IEnumerator InspectRedSea()
        {
            var ocean=GameObject.Find("Ocean").GetComponent<Renderer>();var mat=ocean.sharedMaterial;
            Record("red_sea_uses_existing_water",mat.shader.name=="Dawn Garden/Water"&&game.expansion.surroundingLabel=="海面");
            var deep=mat.GetColor("_BaseColor");var shallow=mat.GetColor("_ShallowColor");
            Record("red_sea_has_uneven_water_tones",deep.r>deep.g*4&&shallow.r>shallow.g*4&&mat.GetFloat("_WaveScale")>0&&Vector4.Distance(deep,shallow)>.25f,
                $"deep={deep}, shallow={shallow}");
            Record("spring_remains_water",GameObject.Find("Pond Water").GetComponent<Renderer>().sharedMaterial.shader.name=="Dawn Garden/Water");
            var grains=FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(m=>m.name=="Batched 3D soil grains").ToArray();
            Record("real_soil_grains_in_both_lands",grains.Length==2&&grains.All(m=>m.sharedMesh.vertexCount>24000),"vertices="+string.Join(",",grains.Select(m=>m.sharedMesh.vertexCount)));
            var decor=GameObject.Find("Ember Camp Decorations").transform;
            var legacyEffects=decor.Find("Lava shore glow and cinders");
            var lavaLights=decor.GetComponentsInChildren<Light>(true).Where(l=>l.name=="Reflected lava warmth").ToArray();
            var lavaCinders=decor.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Lava drifting cinder").ToArray();
            Record("lava_has_no_added_lighting_or_embers",legacyEffects==null&&lavaLights.Length==0&&lavaCinders.Length==0,
                $"root={(legacyEffects!=null)}, lights={lavaLights.Length}, embers={lavaCinders.Length}");
            yield break;
        }
        IEnumerator RecordExpansion()
        {
            var folder=Path.Combine(output,"ember-expansion-frames");Directory.CreateDirectory(folder);
            foreach(var file in Directory.GetFiles(folder,"*.png"))File.Delete(file);
            int index=0;var end=Time.time+7.3f;
            while(Time.time<end)
            {
                yield return new WaitForEndOfFrame();var tex=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder,(index++).ToString("D4")+".png"),tex.EncodeToPNG());Destroy(tex);
                yield return new WaitForSeconds(.12f);
            }
        }
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var tex=ScreenCapture.CaptureScreenshotAsTexture();if(tex!=null){File.WriteAllBytes(Path.Combine(output,name),tex.EncodeToPNG());Destroy(tex);}}
        [Serializable]sealed class Check{public string name;public bool passed;public string detail;}
        [Serializable]sealed class Report{public string utc,unity,isolatedSave;public int errors;public Check[] checks;public bool passed;}
    }
}
