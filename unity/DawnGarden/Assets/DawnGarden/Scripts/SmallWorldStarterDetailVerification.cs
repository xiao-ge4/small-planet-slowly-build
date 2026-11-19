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
        [Serializable] class StarterDetailReport
        {public bool passed;public string theme,utc;public int botanicalGroups,grassBlades,grains;public string[] failed;}
        byte[] CaptureStarterHome(float distance=16.2f,Vector3? focus=null)
        {
            var cam=Game.worldCamera;var p=cam.transform.position;var r=cam.transform.rotation;
            try{var target=focus??new Vector3(0,.45f,0);cam.transform.position=target+Quaternion.Euler(40,180,0)*Vector3.back*distance;cam.transform.LookAt(target);return CaptureHome();}
            finally{cam.transform.SetPositionAndRotation(p,r);}
        }
        IEnumerator VerifyStarterHomeDetails()
        {
            if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot))yield break;
            yield return new WaitForSecondsRealtime(1);
            yield return UntilV1(()=>Game!=null&&!loading&&!editorBusy&&!restoringOwnedLayout,60);
            var failures=new List<string>();var detail=FindFirstObjectByType<StarterHomeDetail>();
            if(detail==null)throw new InvalidOperationException("Starter details did not load");
            if(detail.botanicalGroups<20)failures.Add("Too few botanical groups");
            if(detail.content.GetComponentsInChildren<Collider>(true).Length!=0)failures.Add("Decorations added gameplay colliders");
            var renderers=detail.content.GetComponentsInChildren<Renderer>();
            if(renderers.Any(r=>r.sharedMaterials.Any(m=>m==null||!m.shader.isSupported)))failures.Add("Unsupported detail material");
            if(detail.grains<500||detail.grassBlades<100)failures.Add("Geometric ground details missing");
            var cutaway=Game.worldCamera.GetComponent<GardenTreeCutaway>();if(cutaway!=null&&cutaway.enabled)failures.Add("Tree cutaway re-enabled");
            var ocean=GameObject.Find("Ocean").GetComponent<Renderer>().sharedMaterial;var oceanShader=ocean.shader.name;
            Game.atmosphere.Preview(9.25f);yield return new WaitForEndOfFrame();
            detail.SetVisible(false);yield return new WaitForEndOfFrame();File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"before.png"),CaptureStarterHome());
            detail.SetVisible(true);yield return new WaitForEndOfFrame();File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"after.png"),CaptureStarterHome());File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"close.png"),CaptureStarterHome(10.5f,new Vector3(0,.4f,-.7f)));
            Game.atmosphere.Preview(21.25f);yield return new WaitForEndOfFrame();File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"night.png"),CaptureStarterHome());Game.atmosphere.Preview(9.25f);
            var water=Game.FindInteraction(GardenInteractionKind.Water);var chest=Game.FindInteraction(GardenInteractionKind.Chest);
            int trees=FindObjectsByType<Transform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Count(t=>t.name.StartsWith("Formal Tree ")||t.name.StartsWith("Ember Tree "));
            if(!Game.GoToInteraction(water,true))failures.Add("Water route not reachable");
            yield return UntilV1(()=>!Game.Navigating&&!Game.Busy&&Game.Progress.wateringDays>0,30);
            if(Game.Progress.wateringDays==0)failures.Add("Real watering did not complete");
            int afterTrees=FindObjectsByType<Transform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Count(t=>t.name.StartsWith("Formal Tree ")||t.name.StartsWith("Ember Tree "));
            if(trees!=afterTrees)failures.Add("Approaching plant hides a tree");
            if(!Game.GoToInteraction(chest,true))failures.Add("Chest route not reachable");
            yield return UntilV1(()=>!Game.Navigating&&!Game.Busy&&Game.Progress.chestOpened,30);
            if(!Game.Progress.chestOpened)failures.Add("Real chest did not open");
            if(ocean.shader.name!=oceanShader)failures.Add("Water style changed during interactions");
            if(detail.theme=="ember")
            {var effects=FindFirstObjectByType<EmberCampEffects>();if(effects==null||effects.springSteam==null||!effects.springSteam.gameObject.activeInHierarchy)failures.Add("Warm spring effects missing");var legacy=FindFirstObjectByType<EmberLavaEffects>();if(legacy!=null&&((legacy.shoreGlow!=null&&legacy.shoreGlow.Any(l=>l!=null&&l.intensity>0))||(legacy.cinders!=null&&legacy.cinders.Any(t=>t!=null&&t.gameObject.activeInHierarchy))))failures.Add("Lava effects re-enabled");}
            var report=new StarterDetailReport{passed=failures.Count==0,theme=detail.theme,utc=DateTime.UtcNow.ToString("o"),botanicalGroups=detail.botanicalGroups,grassBlades=detail.grassBlades,grains=detail.grains,failed=failures.ToArray()};File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"starter-detail-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:2);
        }
    }
}
