using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        [Serializable] class DetailReport {public bool passed;public string scope="garden-detail-runtime",utc;public int instances,groundVertices,zeroNormals;public float whitePixelFraction;public string[] failed;}
        IEnumerator VerifyGardenDetail()
        {
            if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot))yield break;
            yield return new WaitForSecondsRealtime(.8f);
            float until=Time.realtimeSinceStartup+60;while((Game==null||loading||editorBusy||restoringOwnedLayout)&&Time.realtimeSinceStartup<until)yield return null;
            var failures=new List<string>();
            if(!OpenPlacement()){failures.Add("Preview did not open");yield break;}
            until=Time.realtimeSinceStartup+90;while(editorBusy&&Time.realtimeSinceStartup<until)yield return null;
            if(!previewValid)failures.Add("Placement / navigation validation: "+placementError);
            if(gardenSurface==null||!gardenSurface.Active)failures.Add("Detailed surface is missing");
            if(editItems.Count<30)failures.Add("Botanical composition is incomplete");
            var proposal=DraftJson();Game.atmosphere?.Preview(8.8f);
            ReadDraft(JsonUtility.ToJson(committedLayout));yield return PreviewPlacement(false);yield return new WaitForEndOfFrame();
            File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"detail-before.png"),CaptureVisualHome());
            ReadDraft(proposal);yield return PreviewPlacement(false);
            var before=DraftJson();ReadDraft(JsonUtility.ToJson(new SmallWorldLayout {version=committedLayout.version,instances=editItems.ToArray(),surfaceStyle=""}));yield return PreviewPlacement(false);
            if(gardenSurface.Active)failures.Add("Original surface cannot be restored");
            ReadDraft(before);yield return PreviewPlacement(false);SaveDraft();
            Game.atmosphere?.Preview(8.8f);Game.orbit.yaw=-30;Game.orbit.pitch=43;Game.orbit.distance=12.8f;
            Game.orbit.expandedOverviewCenter=Game.expansion.land.position+new Vector3(.3f,.35f,.3f)+Quaternion.Euler(0,-30,0)*Vector3.right*2.6f;
            yield return new WaitForSecondsRealtime(1);
            var path=Path.Combine(SmallWorldProfile.TestRoot,"detail-actual.png");var png=CaptureVisualHome();File.WriteAllBytes(path,png);
            File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,"detail-close.png"),CaptureVisualHome(6.8f,new Vector3(2.1f,.45f,1.1f)));
            var image=new Texture2D(2,2);image.LoadImage(png);var pixels=image.GetPixels();float white=pixels.Count(c=>c.r>.98f&&c.g>.98f&&c.b>.98f)/(float)pixels.Length;Destroy(image);
            if(white>.15f)failures.Add("Abnormal white/bloom area");
            int zero=0,total=0;
            foreach(var mesh in placedRoot.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Concat(gardenSurface.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh)))
            {if(!mesh.isReadable)continue;foreach(var n in mesh.normals){total++;if(float.IsNaN(n.x)||float.IsNaN(n.y)||float.IsNaN(n.z)||n.sqrMagnitude<.1f)zero++;}}
            if(total>0&&zero/(float)total>.02f)failures.Add("Invalid botanical / coast normals");
            if(!PlacementChecks().westRoute||!PlacementChecks().groundConnected)failures.Add("Garden blocks traversal");
            var report=new DetailReport {passed=failures.Count==0,utc=DateTime.UtcNow.ToString("o"),instances=editItems.Count,groundVertices=gardenSurface.GroundVertices,zeroNormals=zero,whitePixelFraction=white,failed=failures.ToArray()};
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"detail-report.json"),JsonUtility.ToJson(report,true));
            visualReviewMessage="细节样板 · 已保留入口和中央通路，可继续旋转、近看与调整。";
        }
    }
}
