using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        static readonly Vector2[] ReferenceUpperGardenRim =
        {
            new(-22.2f,14.3f),new(-20.4f,14.0f),new(-18.4f,14.5f),new(-15.7f,15.65f),
            new(-14.1f,16.4f),new(-14.3f,17.0f),new(-21.9f,17.0f)
        };
        static readonly Vector3 ReferenceUpperGardenStairFoot=new(-21.35f,5.2f,8.8f);
        static readonly Vector3 ReferenceUpperGardenStairTop=new(-21.35f,7,14.45f);
        static readonly Vector2[] ReferenceUpperGardenLanding =
        {
            new(-22.30f,7.55f),new(-21.82f,7.20f),new(-20.15f,7.20f),new(-19.95f,7.72f),
            new(-20.18f,8.52f),new(-20.45f,9.15f),new(-22.25f,9.15f)
        };

        // Shared only with visual planting. The landing and stairs own their real
        // support; this clearance never substitutes for a walkable collider.
        public static bool ReferenceUpperGardenEntryClear(float x,float z,float radius=0)
            => x>-22.35f-radius && x<-19.85f+radius && z>7.05f-radius && z<14.60f+radius;

        // A small rear garden, entirely behind the unchanged greenhouse footprint.
        // Its height is carried by real closed masonry and a walkable staircase;
        // the older terrain, crop beds, saved land and established routes stay intact.
        void BuildReferenceUpperGarden()
        {
            var root=Group("温室后园 · 小型错台与石栏",details);
            var stone=new[]{
                Paint("后园旧石灰岩",new Color(.77f,.75f,.65f),.08f,.11f),
                Paint("后园暖白石",new Color(.85f,.81f,.70f),.08f,.10f),
                Paint("后园浅灰石",new Color(.72f,.74f,.68f),.07f,.11f)
            };
            var earth=Paint("后园花槽湿土",new Color(.30f,.27f,.15f),.03f,.12f);
            var joints=Paint("后园嵌缝",new Color(.48f,.48f,.40f),.03f,.07f);
            var masonry=new ReferenceMasonryBatches(this,root,stone);
            UpperGardenVolume(root,"后园实体承台",ReferenceUpperGardenRim,7,stone[0],true);

            // Only the exposed front and short side are dressed. The rear foundation
            // sinks below its real ground samples, never becoming a floating slab.
            for(int edge=0;edge<ReferenceUpperGardenRim.Length;edge++)
            {
                var aa=ReferenceUpperGardenRim[edge];var bb=ReferenceUpperGardenRim[(edge+1)%ReferenceUpperGardenRim.Length];
                var a=new Vector3(aa.x,7,aa.y);var b=new Vector3(bb.x,7,bb.y);
                var along=(b-a).normalized;float length=Vector3.Distance(a,b);
                int strips=Mathf.CeilToInt(length/.90f);
                for(int strip=0;strip<strips;strip++)
                {
                    var p=Vector3.Lerp(a,b,(strip+.5f)/strips);
                    // The staircase has an uninterrupted doorway into the garden.
                    if(edge==0&&p.x<-20.25f)continue;
                    float bottom=ReferenceHeight(p.x,p.z)-.14f;
                    int rows=Mathf.CeilToInt((7-bottom)/.36f);
                    for(int row=0;row<rows;row++)
                    {
                        float y=7-.18f-row*.36f;if(y<bottom-.05f)continue;
                        var at=new Vector3(p.x,y,p.z);
                        masonry.Stone(at,along,length/strips-.025f,.34f,.38f,2111+strip+row*13+edge*97);
                    }
                    masonry.Stone(p+Vector3.up*.045f,along,length/strips-.02f,.14f,.49f,2311+strip+edge*23);
                }
            }
            // The old pad ends at x=-21.5. Its eroded navigation edge therefore
            // could only join the staircase sideways. Extend a small supported
            // stone landing from the pad to the whole stair width, at the exact
            // existing floor elevation, before the first 13 cm riser.
            UpperGardenVolume(root,"后园阶前接地石坪",ReferenceUpperGardenLanding,5.205f,stone[1],true);
            UpperGardenStairs(root,stone[1]);
            // Two irregular low growing pockets keep most of the small platform
            // planted while a clear 1.3 m walking strip meets the top of the stairs.
            var backBed=new[]{new Vector2(-20.25f,15.82f),new(-18.9f,15.84f),new(-17.4f,16.02f),
                new(-16.35f,16.34f),new(-16.30f,16.72f),new(-20.45f,16.72f),new(-20.62f,16.38f)};
            var rightBed=new[]{new Vector2(-15.7f,16.02f),new(-14.60f,16.45f),new(-14.64f,16.76f),
                new(-15.85f,16.77f),new(-16.0f,16.42f)};
            UpperGardenVolume(root,"后园低弧形花槽",backBed,7.36f,earth,false,6.96f);
            UpperGardenVolume(root,"后园端部花槽",rightBed,7.44f,earth,false,6.96f);
            UpperGardenBedRim(masonry,backBed,7.36f,2501);
            UpperGardenBedRim(masonry,rightBed,7.44f,2601);
            var rail=new[]{new Vector3(-21.85f,7,16.96f),new Vector3(-18.05f,7,16.96f),new Vector3(-14.35f,7,16.96f)};
            for(int segment=0;segment<rail.Length-1;segment++)
            {
                var a=rail[segment];var b=rail[segment+1];int posts=Mathf.CeilToInt(Vector3.Distance(a,b)/1.9f);
                for(int i=0;i<=posts;i++)
                {
                    var p=Vector3.Lerp(a,b,i/(float)posts);
                    masonry.Stone(p+Vector3.up*.37f,Vector3.right,.25f,.74f,.27f,2701+i);
                    masonry.Stone(p+Vector3.up*.77f,Vector3.right,.37f,.12f,.37f,2711+i);
                    ReferenceObstacle(root,p+Vector3.up*.43f,new Vector3(.30f,.86f,.32f));
                    if(i==0)continue;
                    var previous=Vector3.Lerp(a,b,(i-1)/(float)posts);var along=(p-previous).normalized;
                    var center=(previous+p)*.5f+Vector3.up*.53f;
                    masonry.Stone(center,along,Vector3.Distance(previous,p)-.20f,.14f,.15f,2801+i);
                    var bar=Group("后园石栏真实边界",root);bar.position=center;
                    bar.rotation=Quaternion.LookRotation(along);bar.gameObject.layer=ObstacleLayer;
                    var collider=bar.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.17f,.16f,Vector3.Distance(previous,p));
                }
            }
            masonry.Build();
            var flowers=new[]{new Vector3(-20.20f,7.36f,16.1f),new(-19.58f,7.36f,16.28f),new(-18.77f,7.36f,16.35f),
                new(-17.80f,7.36f,16.37f),new(-16.85f,7.36f,16.48f),new(-15.35f,7.44f,16.51f)};
            for(int i=0;i<flowers.Length;i++)
            {
                var plant=ReferenceAsset("Geology/flowers_"+(i%3==0?"cream":i%3==1?"gold":"lavender"),flowers[i],17+i*37);
                if(plant==null)continue;plant.transform.SetParent(root,true);plant.transform.localScale=Vector3.one*(i<5?.66f:.55f);
            }
            var tree=ReferenceAsset("Buildings/courtyard_tree_hero",new Vector3(-18.7f,7.36f,16.30f),63);
            if(tree!=null)
            {
                tree.transform.SetParent(root,true);tree.transform.localScale=Vector3.one*.42f;
                ReferenceObstacle(root,new Vector3(-18.7f,7.95f,16.3f),new Vector3(.42f,1.2f,.42f));
            }
        }

        void UpperGardenVolume(Transform parent,string name,Vector2[] outline,float top,Material material,bool floor,float bottom=float.NaN)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var centre=Vector2.zero;
            foreach(var point in outline)centre+=point;centre/=outline.Length;
            vertices.Add(new Vector3(centre.x,top,centre.y));
            foreach(var point in outline)vertices.Add(new Vector3(point.x,top,point.y));
            int lower=vertices.Count;vertices.Add(new Vector3(centre.x,float.IsNaN(bottom)?ReferenceHeight(centre.x,centre.y)-.25f:bottom,centre.y));
            foreach(var point in outline)vertices.Add(new Vector3(point.x,float.IsNaN(bottom)?ReferenceHeight(point.x,point.y)-.25f:bottom,point.y));
            for(int i=0;i<outline.Length;i++)
            {
                int a=1+i,b=1+(i+1)%outline.Length,c=lower+a,d=lower+b;
                triangles.AddRange(new[]{0,b,a,lower,c,d,a,b,d,a,d,c});
            }
            MeshObject(name,parent,vertices,triangles,material,floor,null,!floor);
        }
        void UpperGardenBedRim(ReferenceMasonryBatches batch,Vector2[] outline,float top,int seed)
        {
            for(int edge=0;edge<outline.Length;edge++)
            {
                var a=new Vector3(outline[edge].x,top-.14f,outline[edge].y);
                var b=new Vector3(outline[(edge+1)%outline.Length].x,top-.14f,outline[(edge+1)%outline.Length].y);
                float length=Vector3.Distance(a,b);int parts=Mathf.Max(1,Mathf.CeilToInt(length/.64f));
                for(int i=0;i<parts;i++)batch.Stone(Vector3.Lerp(a,b,(i+.5f)/parts),(b-a).normalized,length/parts-.02f,.28f,.24f,seed+i+edge*17);
            }
        }
        void UpperGardenStairs(Transform parent,Material material)
        {
            const int count=14;const float width=1.8f;
            var from=ReferenceUpperGardenStairTop;var to=ReferenceUpperGardenStairFoot;
            var forward=to-from;forward.y=0;forward.Normalize();var side=Vector3.Cross(Vector3.up,forward)*(width*.5f);
            var vertices=new List<Vector3>();var faces=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {int k=vertices.Count;vertices.AddRange(new[]{a,b,c,d});faces.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
            for(int i=0;i<count;i++)
            {
                var a=Vector3.Lerp(from,to,i/(float)count);var b=Vector3.Lerp(from,to,(i+1)/(float)count);
                var al=new Vector3(a.x,a.y+.015f,a.z)-side;var ar=al+side*2;
                var bl=new Vector3(b.x,a.y+.015f,b.z)-side;var br=bl+side*2;
                float underA=Mathf.Min(ReferenceHeight(al.x,al.z),ReferenceHeight(ar.x,ar.z))-.20f;
                float underB=Mathf.Min(ReferenceHeight(bl.x,bl.z),ReferenceHeight(br.x,br.z))-.20f;
                var dal=new Vector3(al.x,underA,al.z);var dar=new Vector3(ar.x,underA,ar.z);
                var dbl=new Vector3(bl.x,underB,bl.z);var dbr=new Vector3(br.x,underB,br.z);
                Quad(al,bl,br,ar);Quad(dal,dar,dbr,dbl);Quad(al,dal,dbl,bl);Quad(ar,br,dbr,dar);
                if(i<count-1)Quad(bl,new Vector3(bl.x,b.y+.015f,bl.z),new Vector3(br.x,b.y+.015f,br.z),br);
                else Quad(bl,dbl,dbr,br);
                if(i==0)Quad(al,ar,dar,dal);
            }
            MeshObject("温室左后方 · 十四级完整矮石阶",parent,vertices,faces,material,true);
        }
    }
}
