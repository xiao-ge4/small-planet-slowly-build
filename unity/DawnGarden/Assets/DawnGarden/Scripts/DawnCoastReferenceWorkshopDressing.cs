using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // Surveyed in world space from the imported mesh. The original building's
        // floor is Y0.986875, above its Y0.7 placement pivot; this missing last rise
        // must be a visible staircase rather than a larger character climb setting.
        static readonly Vector3[] ReferenceWorkshopAccess =
        {
            new(24,-.4f,-9.55f),new(27.6f,-.4f,-9.55f),
            new(28.8109f,-.4f,-9.1674f),new(28.2204f,.992f,-6.9623f),
            new(28.1756f,.986875f,-6.7946f)
        };

        static Vector3 ReferenceWorkshopFloorLocal(float x,float z)
        {
            return Quaternion.Inverse(Quaternion.Euler(0,165,0))*
                (new Vector3(x,ReferenceWorkshop.y,z)-ReferenceWorkshop)/.9605f;
        }

        static void ApplyReferenceWorkshopAccess(float x,float z,ref float y,ref int surface,
            ref float paving,ref float wet,ref bool underWater)
        {
            var point=new Vector2(x,z);float nearest=float.PositiveInfinity,grade=0;
            for(int i=0;i<ReferenceWorkshopAccess.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceWorkshopAccess[i],ReferenceWorkshopAccess[i+1],out float t);
                if(square>=nearest)continue;
                nearest=square;grade=Mathf.Lerp(ReferenceWorkshopAccess[i].y,ReferenceWorkshopAccess[i+1].y,t);
            }
            float influence=1-Ease(1.06f,1.40f,Mathf.Sqrt(nearest));
            // Keep the pre-existing pier descent south of the facade untouched.
            // The stair's foot is inside the yard, clear of that crossing route.
            influence*=Ease(-9.85f,-9.50f,z);
            if(influence<=0)return;
            y=Mathf.Lerp(y,grade,influence);paving=Mathf.Max(paving,influence);
            // The approach's support lies beneath the imported floor. Keeping
            // two coplanar surfaces here caused peach fragments to flicker through it.
            var interior=ReferenceWorkshopFloorLocal(x,z);
            if(Mathf.Abs(interior.x)<5.14f&&Mathf.Abs(interior.z)<3.42f)y=Mathf.Min(y,.89f);
            if(influence>.97f){surface=1;wet=0;underWater=false;}
        }

        // Local coordinates were measured from the actual Unity mesh export: +Z is
        // the open facade, -Z the back wall. The original model is never rotated here.
        void BuildReferenceWorkshopDressing()
        {
            var root=Group("工坊 · 可见陈列与暖灯",details);
            root.localPosition=ReferenceWorkshop;root.localRotation=Quaternion.Euler(0,165,0);
            root.localScale=new Vector3(.9605f,.85f,.9605f);
            var palette=new[]{
                Paint("工坊陈列 · 深色橡木",new Color(.25f,.13f,.075f),.12f,.12f),
                Paint("工坊陈列 · 蜜色端木",new Color(.63f,.39f,.20f),.13f,.13f),
                Paint("工坊陈列 · 陶土红",new Color(.77f,.37f,.23f),.17f,.08f),
                Paint("工坊陈列 · 蜜釉黄",new Color(.86f,.57f,.23f),.32f,.035f),
                Paint("工坊陈列 · 青釉",new Color(.30f,.60f,.57f),.32f,.025f),
                Paint("工坊陈列 · 亚麻包裹",new Color(.86f,.78f,.60f),.08f,.06f),
                Paint("工坊陈列 · 锻铁",new Color(.29f,.30f,.27f),.28f,.025f),
                Paint("工坊陈列 · 琥珀灯芯",new Color(1,.70f,.27f),.35f,0)
            };
            palette[7].EnableKeyword("_EMISSION");palette[7].SetColor("_EmissionColor",new Color(.90f,.37f,.09f));
            var batch=new ReferenceWorkshopBatch(this,root,palette);
            ReferenceForeshoreStair(root,ReferenceWorkshopAccess[3],ReferenceWorkshopAccess[2],2.1f,
                Paint("工坊入口完整石阶",new Color(.82f,.79f,.69f),.10f,.10f),"工坊门廊 · 连接实际地板的完整石阶");
            // A18 ray tests place the legible shelf band below local Y2.2; higher
            // contents are hidden by the unchanged deep eave. Bring the display faces
            // forward within the back wall instead of filling an invisible roof void.
            for(int bay=0;bay<3;bay++)
            {
                float x=(bay-1)*2.92f;
                for(int board=0;board<8;board++)
                    batch.Box(0,new Vector3(x-1.27f+board*.36f,1.52f,-2.39f),new Vector3(.34f,2.34f,.10f));
                for(int level=0;level<3;level++)
                {
                    float y=.68f+level*.55f;
                    batch.Box(1,new Vector3(x,y,-1.91f),new Vector3(2.62f,.10f,.67f));
                    batch.Box(0,new Vector3(x,y-.055f,-1.56f),new Vector3(2.66f,.12f,.095f));
                    // Each bay has a purpose and some empty shelf: timber stock,
                    // packed orders, then a small set of finished ceramic pieces.
                    if(bay==0)
                    {
                        for(int plank=0;plank<(level==1?1:3);plank++)
                            batch.Box(1,new Vector3(x-.18f+plank*.10f,y+.10f+plank*.085f,-1.88f),
                                new Vector3(1.63f-plank*.21f,.08f,.34f),Quaternion.Euler(0,plank*3-3,0));
                        if(level==1)
                        {
                            WorkshopVessel(batch,new Vector3(x-.61f,y+.14f,-1.69f),.22f,.37f,4,true);
                            WorkshopVessel(batch,new Vector3(x+.03f,y+.14f,-1.70f),.16f,.27f,2,false);
                        }
                        else WorkshopVessel(batch,new Vector3(x+.97f,y+.05f,-1.73f),.18f,.34f,level==0?2:3,true);
                    }
                    else if(bay==1)
                    {
                        if(level!=1)WorkshopParcel(batch,new Vector3(x-.70f,y+.05f,-1.85f),new Vector3(.62f,.32f,.43f),level);
                        WorkshopParcel(batch,new Vector3(x+.19f,y+.05f,-1.84f),new Vector3(.45f,.26f,.38f),level+1);
                        WorkshopVessel(batch,new Vector3(x+.92f,y+.05f,-1.70f),.21f,level==1?.44f:.33f,level==1?4:2,level==1);
                    }
                    else
                    {
                        WorkshopVessel(batch,new Vector3(x-.81f,y+.05f,-1.83f),.22f,.40f,2+level%3,true);
                        if(level!=1)WorkshopVessel(batch,new Vector3(x-.24f,y+.05f,-1.78f),.17f,.31f,3,false);
                    }
                }
                // The visible shelves occupy a real cabinet envelope; its collider
                // stays entirely behind the current centre aisle and destination.
                WorkshopBoxCollider(root,"后陈列柜实体边界",new Vector3(x,1.35f,-2.04f),new Vector3(2.62f,2.04f,.78f));
            }
            // Existing tabletop tops are local Y1.325. Dress them without enlarging
            // either bench into the narrow but already validated centre passage.
            // The left bench holds a clamped frame in progress. Tool, offcuts and
            // shavings explain the operation while every piece stays on the old top.
            var work=new Vector3(-2.24f,1.38f,1.10f);
            batch.Box(5,new Vector3(-2.20f,1.332f,1.12f),new Vector3(1.60f,.012f,.84f));
            foreach(float side in new[]{-1f,1f})
            {
                batch.Box(1,work+new Vector3(side*.57f,0,0),new Vector3(.16f,.10f,.71f));
                batch.Box(1,work+new Vector3(0,0,side*.30f),new Vector3(1.27f,.10f,.12f));
            }
            batch.Beam(6,work+new Vector3(-.78f,.06f,-.25f),work+new Vector3(.78f,.06f,-.25f),.035f);
            foreach(float side in new[]{-1f,1f})
                batch.Box(6,work+new Vector3(side*.69f,.07f,-.25f),new Vector3(.09f,.22f,.17f));
            batch.Beam(1,new Vector3(-3.36f,1.365f,1.28f),new Vector3(-2.91f,1.375f,1.52f),.07f);
            batch.Box(1,new Vector3(-2.91f,1.405f,1.52f),new Vector3(.19f,.15f,.30f),Quaternion.Euler(0,-25,0));
            batch.Box(6,new Vector3(-1.47f,1.36f,1.37f),new Vector3(.20f,.055f,.42f),Quaternion.Euler(0,18,0));
            batch.Box(1,new Vector3(-1.49f,1.437f,1.37f),new Vector3(.09f,.10f,.23f),Quaternion.Euler(0,18,0));
            for(int i=0;i<7;i++)
                batch.Box(1,new Vector3(-2.75f+i*.17f,1.337f,1.57f+.025f*(i%2)),
                    new Vector3(.12f,.013f,.035f),Quaternion.Euler(0,i*37,0));
            // Right bench is the next job: short boards, wrapped hardware and two
            // brushes beside one open cup, separated from the finished goods.
            for(int i=0;i<3;i++)
                batch.Box(1,new Vector3(2.75f+i*.07f,1.363f+i*.065f,1.12f),new Vector3(1.07f-i*.12f,.065f,.38f),Quaternion.Euler(0,4-i*4,0));
            WorkshopParcel(batch,new Vector3(1.31f,1.33f,1.16f),new Vector3(.39f,.24f,.37f),3);
            WorkshopVessel(batch,new Vector3(2.02f,1.33f,1.39f),.15f,.29f,6,false);
            for(int i=0;i<2;i++)
            {
                var from=new Vector3(1.99f+i*.065f,1.49f,1.39f);
                var to=from+new Vector3((i-.5f)*.07f,.44f+i*.07f,.015f);
                batch.Beam(1,from,to,.032f);
                batch.Box(5,to+Vector3.up*.045f,new Vector3(.07f,.13f,.055f));
            }
            // Large grouped stock stays beside the facade posts. The route from the
            // court enters near local X0..1.1, so no new solid enters that corridor.
            WorkshopCrate(batch,root,new Vector3(-3.79f,.33f,2.19f),new Vector3(.90f,.70f,.76f),true);
            WorkshopCrate(batch,root,new Vector3(3.69f,.33f,2.10f),new Vector3(.97f,.64f,.78f),true);
            WorkshopCrate(batch,root,new Vector3(3.72f,.97f,2.08f),new Vector3(.72f,.48f,.64f),false);
            for(int i=0;i<4;i++)
                batch.Box(1,new Vector3(-4.18f+i*.18f,1.30f,1.60f),new Vector3(.15f,1.77f,.09f),
                    Quaternion.Euler(-8-i*2,0,5));
            // Tool boards sit on the two near side returns; their silhouettes remain
            // visible in the fixed camera instead of disappearing behind the rear eave.
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*4.29f;
                batch.Box(0,new Vector3(x,1.96f,.54f),new Vector3(.09f,1.20f,1.25f));
                for(int i=0;i<3;i++)
                {
                    float z=.17f+i*.37f;
                    batch.Beam(1,new Vector3(x-side*.07f,1.52f,z),new Vector3(x-side*.07f,2.28f,z),.045f);
                    batch.Box(6,new Vector3(x-side*.08f,2.27f,z),new Vector3(.09f,.12f,.29f));
                }
                WorkshopLantern(batch,root,new Vector3(side*4.42f,2.53f,2.94f));
            }
            batch.Build();
        }

        void WorkshopBoxCollider(Transform parent,string name,Vector3 center,Vector3 size)
        {
            var node=Group(name,parent);node.gameObject.layer=ObstacleLayer;
            var collider=node.gameObject.AddComponent<BoxCollider>();collider.center=center;collider.size=size;
        }
        void WorkshopCrate(ReferenceWorkshopBatch batch,Transform parent,Vector3 bottom,Vector3 size,bool collision)
        {
            var center=bottom+Vector3.up*(size.y*.5f);
            batch.Box(0,center,size-new Vector3(.045f,.035f,.045f));
            for(int plank=0;plank<4;plank++)
            {
                float y=bottom.y+(plank+.5f)*size.y/4;
                batch.Box(1,new Vector3(center.x,y,center.z+size.z*.5f),new Vector3(size.x,size.y/4-.025f,.055f));
                batch.Box(1,new Vector3(center.x,y,center.z-size.z*.5f),new Vector3(size.x,size.y/4-.025f,.055f));
            }
            foreach(float sign in new[]{-1f,1f})
            {
                batch.Box(1,new Vector3(center.x+sign*size.x*.43f,center.y,center.z+size.z*.54f),new Vector3(.095f,size.y,.065f));
                batch.Box(1,new Vector3(center.x+sign*size.x*.50f,center.y,center.z),new Vector3(.065f,size.y,size.z));
            }
            batch.Beam(0,bottom+new Vector3(-size.x*.36f,.10f,size.z*.57f),bottom+new Vector3(size.x*.36f,size.y-.10f,size.z*.57f),.065f);
            if(collision)WorkshopBoxCollider(parent,"靠墙材料箱实体边界",center,size);
        }
        static void WorkshopParcel(ReferenceWorkshopBatch batch,Vector3 bottom,Vector3 size,int seed)
        {
            var center=bottom+Vector3.up*size.y*.5f;
            batch.Box(5,center,size);
            batch.Box(1,center,new Vector3(size.x+.014f,.025f,size.z+.014f));
            batch.Box(1,center,new Vector3(.035f,size.y+.014f,size.z+.014f));
            if(seed%2==0)batch.Box(0,bottom+new Vector3(0,size.y+.02f,0),new Vector3(.12f,.035f,.09f));
        }
        static void WorkshopVessel(ReferenceWorkshopBatch batch,Vector3 bottom,float radius,float height,int material,bool handle)
        {
            var profile=new[]{new Vector2(0,0),new Vector2(radius*.62f,0),new Vector2(radius*.90f,height*.10f),
                new Vector2(radius,height*.44f),new Vector2(radius*.90f,height*.72f),new Vector2(radius*.61f,height*.88f),
                new Vector2(radius*.64f,height),new Vector2(radius*.47f,height),new Vector2(radius*.47f,height*.87f)};
            batch.Lathe(material,bottom,profile);
            batch.Lathe(0,bottom,new[]{new Vector2(radius*.47f,height*.87f),new Vector2(0,height*.87f)});
            if(handle)batch.Loop(material,bottom+new Vector3(radius*.96f,height*.58f,0),radius*.48f,height*.28f,.035f);
        }
        void WorkshopLantern(ReferenceWorkshopBatch batch,Transform parent,Vector3 center)
        {
            batch.Box(6,center,new Vector3(.40f,.62f,.30f));
            batch.Box(7,center+Vector3.forward*.166f,new Vector3(.29f,.43f,.035f));
            foreach(float side in new[]{-1f,1f})
                batch.Box(6,center+new Vector3(side*.155f,0,.195f),new Vector3(.035f,.48f,.035f));
            batch.Box(6,center+new Vector3(0,.35f,.03f),new Vector3(.48f,.10f,.38f));
            batch.Box(6,center+new Vector3(0,-.35f,.03f),new Vector3(.45f,.09f,.36f));
            var glow=Group("檐口琥珀暖灯",parent);glow.localPosition=center+new Vector3(0,0,.38f);
            var light=glow.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.61f,.24f);
            light.range=3.2f;light.intensity=.8f;light.shadows=LightShadows.None;nightLamps.Add(light);
        }

        sealed class ReferenceWorkshopBatch
        {
            readonly DawnCoast owner;readonly Transform root;readonly Material[] palette;
            readonly List<Vector3>[] vertices;readonly List<int>[] faces;
            public ReferenceWorkshopBatch(DawnCoast owner,Transform root,Material[] palette)
            {
                this.owner=owner;this.root=root;this.palette=palette;
                vertices=new List<Vector3>[palette.Length];faces=new List<int>[palette.Length];
                for(int i=0;i<palette.Length;i++){vertices[i]=new List<Vector3>();faces[i]=new List<int>();}
            }
            public void Box(int material,Vector3 center,Vector3 size,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                var half=size*.5f;var corners=new Vector3[8];
                for(int i=0;i<8;i++)corners[i]=center+rotation*new Vector3((i&1)==0?-half.x:half.x,(i&2)==0?-half.y:half.y,(i&4)==0?-half.z:half.z);
                var sides=new[]{new[]{0,4,6,2},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,6,7,3},new[]{0,2,3,1},new[]{4,5,7,6}};
                foreach(var side in sides)
                {
                    int at=vertices[material].Count;
                    foreach(int corner in side)vertices[material].Add(corners[corner]);
                    faces[material].AddRange(new[]{at,at+1,at+2,at,at+2,at+3});
                }
            }
            public void Beam(int material,Vector3 a,Vector3 b,float width)
                =>Box(material,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),Quaternion.FromToRotation(Vector3.up,b-a));
            public void Lathe(int material,Vector3 bottom,Vector2[] profile)
            {
                const int sides=20;int at=vertices[material].Count;
                for(int row=0;row<profile.Length;row++)for(int i=0;i<=sides;i++)
                {
                    float angle=i*Mathf.PI*2/sides;
                    vertices[material].Add(bottom+new Vector3(Mathf.Cos(angle)*profile[row].x,profile[row].y,Mathf.Sin(angle)*profile[row].x));
                }
                for(int row=1;row<profile.Length;row++)for(int i=0;i<sides;i++)
                {
                    int a=at+(row-1)*(sides+1)+i,b=a+1,c=a+sides+1,d=c+1;
                    faces[material].AddRange(new[]{a,c,b,b,c,d});
                }
            }
            public void Loop(int material,Vector3 center,float horizontal,float vertical,float tube)
            {
                const int sections=16,sides=8;int at=vertices[material].Count;
                for(int section=0;section<=sections;section++)for(int j=0;j<sides;j++)
                {
                    float a=section*Mathf.PI*2/sections,b=j*Mathf.PI*2/sides;
                    vertices[material].Add(center+new Vector3(Mathf.Cos(a)*(horizontal+Mathf.Cos(b)*tube),
                        Mathf.Sin(a)*(vertical+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube));
                }
                for(int section=1;section<=sections;section++)for(int j=0;j<sides;j++)
                {
                    int a=at+(section-1)*sides+j,b=at+(section-1)*sides+(j+1)%sides,c=a+sides,d=b+sides;
                    faces[material].AddRange(new[]{a,c,b,b,c,d});
                }
            }
            public void Build()
            {
                for(int i=0;i<palette.Length;i++)if(vertices[i].Count>0)
                    owner.MeshObject("工坊成组陈列 · "+palette[i].name,root,vertices[i],faces[i],palette[i]);
            }
        }
    }
}
