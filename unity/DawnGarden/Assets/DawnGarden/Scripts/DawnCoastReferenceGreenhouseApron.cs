using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // A few authored planting pockets on the greenhouse's east apron. The
        // whole pot/bed footprint is tested against the rotated house and paths.
        void BuildReferenceGreenhouseApron()
        {
            var root=Group("温室东缘 · 陶盆与窄花槽",details);
            var pale=Paint("暖房浅旧石槽",new Color(.78f,.75f,.63f),.07f,.10f);
            var ochre=Paint("暖房蜂蜜陶盆",new Color(.67f,.38f,.16f),.10f,.09f);
            var clay=Paint("暖房淡陶盆",new Color(.78f,.53f,.30f),.10f,.09f);
            var earth=Paint("暖房盆内湿土",new Color(.27f,.24f,.12f),.02f,.07f);
            var seedlings=new Dictionary<Material,NurseryGeometry>();

            var beds=new[]{new Vector3(-8.65f,0,9.80f),new Vector3(-8.85f,0,13.20f)};
            for(int i=0;i<beds.Length;i++)
            {
                var p=beds[i];const float halfWidth=.46f,halfDepth=1.05f;
                if(!ReferenceGreenhouseApronClear(p,halfWidth,halfDepth))continue;
                float h=ReferenceHeight(p.x,p.z);
                if(i==0)
                {
                    // Reuse the first planter's surveyed envelope for a compact
                    // potting station. The whole working corner stays outside the
                    // rotated greenhouse and every route's extra 65 cm margin.
                    BuildReferenceApronWorkCorner(root,new Vector3(p.x,h,p.z),ochre,clay,earth,seedlings);
                    continue;
                }
                var outline=new[]{new Vector2(p.x-halfWidth+.13f,p.z-halfDepth),
                    new(p.x+halfWidth-.13f,p.z-halfDepth),new(p.x+halfWidth,p.z-halfDepth+.18f),
                    new(p.x+halfWidth,p.z+halfDepth-.18f),new(p.x+halfWidth-.13f,p.z+halfDepth),
                    new(p.x-halfWidth+.13f,p.z+halfDepth),new(p.x-halfWidth,p.z+halfDepth-.18f),
                    new(p.x-halfWidth,p.z-halfDepth+.18f)};
                UpperGardenVolume(root,"温室边缘完整窄石槽 "+i,outline,h+.30f,pale,false,h-.035f);
                // Soil is inside the continuous rim; the existing closed bed owns
                // the collision, so these surfaces cannot create hidden barriers.
                var inner=new List<Vector3>();foreach(var q in outline)
                    inner.Add(new Vector3(Mathf.Lerp(p.x,q.x,.74f),h+.312f,Mathf.Lerp(p.z,q.y,.86f)));
                var triangles=new List<int>();for(int n=1;n<inner.Count-1;n++)triangles.AddRange(new[]{0,n+1,n});
                MeshObject("花槽内土",root,inner,triangles,earth);
                ReferenceApronFlower(root,new Vector3(p.x-.035f,h+.32f,p.z+.48f),"lavender",.48f,153);
                ReferenceApronFlower(root,new Vector3(p.x+.045f,h+.32f,p.z-.48f),"cream",.27f,227);
                ReferenceNurseryCrop(seedlings,new Vector3(p.x,h+.32f,p.z-.02f),2,.24f,311);
            }
            var pots=new[]{new Vector4(-9.10f,7.30f,.29f,.61f),new Vector4(-8.02f,6.88f,.25f,.43f),new Vector4(-7.98f,11.55f,.27f,.75f)};
            for(int i=0;i<pots.Length;i++)
            {
                var p=pots[i];var at=new Vector3(p.x,ReferenceHeight(p.x,p.y),p.y);
                if(!ReferenceGreenhouseApronClear(at,p.z+.04f,p.z+.04f))continue;
                ReferenceApronPot(root,at,p.z,p.w,i%2==0?ochre:clay,earth);
                if(i==2)ReferenceApronFlower(root,at+Vector3.up*(p.w-.045f),"lavender",.38f,155);
                else ReferenceNurseryCrop(seedlings,at+Vector3.up*(p.w-.045f),2,.25f,61+i*47);
            }
            PublishReferenceNurseryCrops(root,seedlings);
        }

        void BuildReferenceApronWorkCorner(Transform root,Vector3 at,Material ochre,Material clay,
            Material earth,Dictionary<Material,NurseryGeometry> seedlings)
        {
            var bench=Group("温室东缘 · 正在换盆的工作角",root);bench.localPosition=at;
            Box(bench,"窄换盆桌面",new Vector3(0,.87f,0),new Vector3(.79f,.10f,1.78f),wood);
            foreach(float x in new[]{-.30f,.30f})foreach(float z in new[]{-.72f,.72f})
                Box(bench,"换盆桌实木腿",new Vector3(x,.41f,z),new Vector3(.095f,.82f,.095f),darkWood);
            Box(bench,"桌下收纳板",new Vector3(0,.17f,0),new Vector3(.67f,.065f,1.57f),wood);
            ReferenceObstacle(bench,new Vector3(0,.46f,0),new Vector3(.79f,.92f,1.78f));

            ReferenceApronPot(bench,new Vector3(.12f,.92f,.53f),.18f,.27f,ochre,earth);
            ReferenceNurseryCrop(seedlings,at+new Vector3(.12f,1.16f,.53f),2,.23f,683);
            ReferenceApronPot(bench,new Vector3(-.13f,.92f,-.57f),.135f,.20f,clay,earth);
            ReferenceApronPot(bench,new Vector3(.12f,.205f,-.48f),.22f,.34f,ochre,earth);
            ReferenceApronPot(bench,new Vector3(-.13f,.205f,.42f),.17f,.26f,clay,earth);

            // A low soil tray, loose soil and a laid-down trowel explain the task
            // in progress. These use the existing wood, metal and pot geometry.
            Box(bench,"待移栽木苗盘",new Vector3(-.055f,.95f,-.055f),new Vector3(.49f,.065f,.58f),darkWood);
            Box(bench,"苗盘湿土",new Vector3(-.055f,.987f,-.055f),new Vector3(.42f,.018f,.51f),earth);
            foreach(float z in new[]{-.17f,.06f})
                ReferenceNurseryCrop(seedlings,at+new Vector3(-.055f,1.00f,z),2,.16f,733+(z>0?13:0));
            Beam(bench,"手铲木柄",new Vector3(.22f,.95f,-.34f),new Vector3(.25f,.96f,-.06f),.045f,wood);
            Box(bench,"手铲旧金属铲面",new Vector3(.26f,.95f,.025f),new Vector3(.12f,.022f,.18f),slate);
            SampleStone(bench,"换盆留下的小撮湿土",new Vector3(-.24f,.937f,.35f),new Vector3(.17f,.032f,.12f),907,earth);
        }

        static bool ReferenceGreenhouseApronClear(Vector3 at,float halfX,float halfZ)
        {
            float h=ReferenceHeight(at.x,at.z);
            foreach(float dx in new[]{-halfX,0,halfX})foreach(float dz in new[]{-halfZ,0,halfZ})
            {
                var p=new Vector3(at.x+dx,h,at.z+dz);
                if(Mathf.Abs(ReferenceHeight(p.x,p.z)-h)>.07f)return false;
                var local=Quaternion.Inverse(Quaternion.Euler(0,-22,0))*(p-ReferenceGreenhouse);
                if(Mathf.Abs(local.x)<4.054f+.65f&&Mathf.Abs(local.z)<2.846f+.65f)return false;
                if(ReferenceUpperGardenEntryClear(p.x,p.z,.65f))return false;
                foreach(var road in ReferenceRoads)for(int k=1;k<road.points.Length;k++)
                    if(SegmentDistanceSquared(new Vector2(p.x,p.z),road.points[k-1],road.points[k],out _) <
                        Mathf.Pow(road.width+.65f,2))return false;
            }
            return true;
        }

        void ReferenceApronFlower(Transform parent,Vector3 at,string kind,float scale,float yaw)
        {
            var flower=ReferenceAsset("Geology/flowers_"+kind,at,yaw);
            if(flower==null)return;
            flower.transform.SetParent(parent,true);flower.transform.localScale=Vector3.one*scale;
        }

        void ReferenceApronPot(Transform parent,Vector3 at,float radius,float height,Material clay,Material soilMaterial)
        {
            const int sides=16;
            var profile=new[]{new Vector2(radius*.68f,0),new(radius*.72f,height*.12f),new(radius*.92f,height*.80f),
                new(radius,height*.86f),new(radius,height*.97f),new(radius*.83f,height),
                new(radius*.79f,height*.90f),new(radius*.78f,height*.84f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var ring in profile)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;
                vertices.Add(at+new Vector3(Mathf.Cos(a)*ring.x,ring.y,Mathf.Sin(a)*ring.x));
            }
            for(int row=0;row<profile.Length-1;row++)for(int i=0;i<sides;i++)
            {
                int a=row*sides+i,b=row*sides+(i+1)%sides,c=a+sides,d=b+sides;
                triangles.AddRange(new[]{a,c,d,a,d,b});
            }
            int bottom=vertices.Count;vertices.Add(at);int soilCenter=vertices.Count;vertices.Add(at+Vector3.up*(height*.84f));
            for(int i=0;i<sides;i++)
            {
                triangles.AddRange(new[]{bottom,i,(i+1)%sides});
                int a=(profile.Length-1)*sides+i,b=(profile.Length-1)*sides+(i+1)%sides;
                triangles.AddRange(new[]{soilCenter,b,a});
            }
            MeshObject("有真实厚壁和碰撞的陶盆",parent,vertices,triangles,clay,false,null,true);
            var soil=new List<Vector3>{at+Vector3.up*(height*.845f)};var faces=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;soil.Add(at+new Vector3(Mathf.Cos(a)*radius*.78f,height*.845f,Mathf.Sin(a)*radius*.78f));
                faces.AddRange(new[]{0,(i+1)%sides+1,i+1});
            }
            MeshObject("陶盆内土",parent,soil,faces,soilMaterial);
        }
    }
}
