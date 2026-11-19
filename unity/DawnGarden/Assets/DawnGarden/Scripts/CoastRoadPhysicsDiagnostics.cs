using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    // Read-only diagnostics for the real generated scene; this never changes a
    // collider, route, agent limit or navigation result to make a test pass.
    public static class CoastRoadPhysicsDiagnostics
    {
        public static void Write(string directory,Vector3 origin,float radius,float height)
        {
            var csv=new StringBuilder("route,segment,t,side,x,z,ground,slope_degrees,nav,nav_y,connected,capsule_contacts,upward_hits\n");
            var routes=new[]{DawnCoast.ReferenceRidgeApproach,DawnCoast.ReferenceRidgePath,DawnCoast.ReferenceBridgeCourtDescent,
                new[]{DawnCoast.ReferenceRidgePath[DawnCoast.ReferenceRidgePath.Length-2],DawnCoast.ReferenceObservatoryApproach},
                DawnCoast.ReferenceForeshoreWalk,DawnCoast.ReferenceForeshoreLegacyArc,DawnCoast.ReferenceObservatoryStairs,
                DawnCoast.ReferencePierDescent,
                new[]{DawnCoast.ReferenceWorkshopCourt,new Vector3(27.6f,-.4f,-9.55f),new Vector3(28.8109f,-.4f,-9.1674f),new Vector3(28.2204f,.992f,-6.9623f),new Vector3(27.5367f,1.0269f,-4.4103f)}};
            var names=new[]{"bridge_to_s","s_ridge","bridge_to_court","star_entry_seam","west_foreshore","west_legacy_branch","upper_court_star_steps","workshop_pier_descent","workshop_entry"};
            for(int route=0;route<routes.Length;route++)
            for(int i=0;i<routes[route].Length-1;i++)
            {
                var a=routes[route][i];var b=routes[route][i+1];var forward=b-a;forward.y=0;
                int samples=Mathf.CeilToInt(forward.magnitude/.20f);var side=Vector3.Cross(Vector3.up,forward.normalized);
                for(int j=0;j<=samples;j++)foreach(float lateral in new[]{-.45f,0,.45f})
                {
                    float t=j/(float)samples;var p=Vector3.Lerp(a,b,t)+side*lateral;
                    p.y=DawnCoast.ReferenceHeight(p.x,p.z);
                    float dx=(DawnCoast.ReferenceHeight(p.x+.12f,p.z)-DawnCoast.ReferenceHeight(p.x-.12f,p.z))/.24f;
                    float dz=(DawnCoast.ReferenceHeight(p.x,p.z+.12f)-DawnCoast.ReferenceHeight(p.x,p.z-.12f))/.24f;
                    float slope=Mathf.Atan(Mathf.Sqrt(dx*dx+dz*dz))*Mathf.Rad2Deg;
                    bool found=NavMesh.SamplePosition(p,out var nav,.3f,NavMesh.AllAreas);
                    var path=new NavMeshPath();bool connected=found&&NavMesh.CalculatePath(origin,nav.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                    var low=p+Vector3.up*(radius+.06f);var high=p+Vector3.up*(height-radius+.02f);
                    var hits=Physics.OverlapCapsule(low,high,radius,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore);
                    string contacts=string.Join(" | ",hits.Select(c=>$"{c.name} / {c.transform.parent?.name} [{c.bounds.min.y:F2}..{c.bounds.max.y:F2}]"));
                    var upward=Physics.RaycastAll(p+Vector3.up*.10f,Vector3.up,height+.4f,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore);
                    string ceilings=string.Join(" | ",upward.OrderBy(h=>h.distance).Select(h=>$"{h.collider.name} / {h.collider.transform.parent?.name} y={h.point.y:F3} normal={h.normal}"));
                    csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2:F3},{3:F2},{4:F3},{5:F3},{6:F3},{7:F2},{8},{9:F3},{10},{11},{12}\n",
                        names[route],i,t,lateral,p.x,p.z,p.y,slope,found?1:0,found?nav.position.y:999,connected?1:0,Quote(contacts),Quote(ceilings));
                }
            }
            File.WriteAllText(Path.Combine(directory,"bridge-road-physics.csv"),csv.ToString());
        }
        static string Quote(string text)=>"\""+text.Replace("\"","\"\"")+"\"";
    }
}
