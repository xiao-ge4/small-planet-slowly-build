using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void BuildTerrain()
        {
            BuildConnectedTerrain();
            Bridge(SampleBridgeStart, SampleBridgeEnd, 3.25f);
            BuildSampleWaterCourse(RiverProfile, RiverHalfWidths);
        }

        void TileBorder(Transform parent, Vector3 a, Vector3 b, float nominal)
        {
            int count = Mathf.CeilToInt(Vector3.Distance(a,b)/nominal);
            for (int i=0;i<count;i++)
            {
                var start = Vector3.Lerp(a,b,i/(float)count);
                var end = Vector3.Lerp(a,b,(i+1f)/count);
                var stone = Box(parent,"带斜面的砌石",(start+end)*.5f+Vector3.up*.25f,new Vector3(.47f,.5f,(end-start).magnitude*.96f),
                    i%3==1?slate:limestone,ObstacleLayer);
                stone.transform.localRotation=Quaternion.LookRotation(end-start);
                var cap=Box(parent,"压顶",(start+end)*.5f+Vector3.up*.52f,new Vector3(.53f,.10f,(end-start).magnitude*.98f),limestone);
                cap.transform.localRotation=stone.transform.localRotation;
            }
        }

        void Bridge(Vector3 a,Vector3 b,float width)
        {
            var root=Group("弧形木桥",details);
            var side=Vector3.Cross(Vector3.up,(b-a).normalized);
            var points=new List<Vector3>();
            int sections=Mathf.Max(16,Mathf.RoundToInt(Vector3.Distance(a,b)/.34f));
            for (int i=0;i<=sections;i++)
            {
                float t=i/(float)sections;
                var p=Vector3.Lerp(a,b,t)+Vector3.up*(Mathf.Sin(Mathf.PI*t)*ReferenceBridgeRise);
                points.Add(p);
                if(i==0)continue;
                var from=points[i-1];
                var center=(from+p)*.5f+Vector3.down*.048f;
                SampleStone(root,"桥面的刨边木板",center,new Vector3(width+.055f,.137f,
                    Vector3.Distance(from,p)*1.035f),i*17,i%5==0?darkWood:wood,Quaternion.LookRotation(p-from));
            }
            Ribbon("桥面通行坡",width,wood,true,points.ToArray());
            FinishSampleBridge(root,points,side,width);
        }
    }
}
