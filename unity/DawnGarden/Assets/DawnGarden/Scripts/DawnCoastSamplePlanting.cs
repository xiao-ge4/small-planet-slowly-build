using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        sealed class NurseryGeometry
        {
            internal readonly List<Vector3> Points = new();
            internal readonly List<int> Faces = new();
        }

        Material herbStem, herbMoss, herbFern, herbGold, herbCoral, herbCream, herbBlue, clayCotta, clayRim;

        void MakeNurseryMaterials()
        {
            if (herbStem!=null) return;
            herbStem=NurseryMat("墨绿叶脉",new Color(.28f,.45f,.16f),.12f);
            herbMoss=NurseryMat("嫩青卷叶",new Color(.47f,.72f,.27f),.11f);
            herbFern=NurseryMat("深林蕨叶",new Color(.30f,.56f,.255f),.13f);
            herbGold=NurseryMat("暖金万寿菊",new Color(1.0f,.66f,.14f),.19f);
            herbCoral=NurseryMat("朱红与珊瑚",new Color(.92f,.30f,.20f),.21f);
            herbCream=NurseryMat("淡奶黄衬花",new Color(.96f,.90f,.70f),.23f);
            herbBlue=NurseryMat("蓝紫鼠尾草",new Color(.43f,.56f,.83f),.19f);
            clayCotta=Paint("育苗陶壶 · 氧化红泥",new Color(.74f,.40f,.246f),.18f,.20f);
            clayRim=Paint("育苗陶壶 · 抛光暖红陶沿",new Color(.79f,.48f,.28f),.25f,.16f);
        }

        Material NurseryMat(string name,Color pigment,float gloss)
        {
            var material=Paint(name,pigment,gloss,.09f);
            material.SetFloat("_Cull",0);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor",pigment*.075f);
            return material;
        }

        NurseryGeometry PlantMaterial(Dictionary<Material,NurseryGeometry> batch, Material mat)
        {
            if(!batch.TryGetValue(mat,out var part))
            {
                part=new NurseryGeometry();
                batch.Add(mat,part);
            }
            return part;
        }

        void PublishPlants(Transform parent, string label, Dictionary<Material,NurseryGeometry> batch)
        {
            foreach(var layer in batch)
                if(layer.Value.Points.Count>0)
                    MeshObject(label + " · " + layer.Key.name,parent,layer.Value.Points,layer.Value.Faces,layer.Key);
        }

        void BloomBlade(NurseryGeometry batch,Vector3 root,float heading,float pitch,float length,float width,float curl)
        {
            var yaw=Quaternion.Euler(0,heading,0)*Vector3.forward;
            var side=Vector3.Cross(Vector3.up,yaw);
            var spine=Vector3.LerpUnclamped(yaw,Vector3.up,pitch);
            var initial=batch.Points.Count;
            const int steps=5;
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps;
                float swell=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.74f);
                var mid=root+spine*(length*t)+Vector3.up*(curl*t*t);
                float cup=width*.32f*swell;
                batch.Points.Add(mid-side*(swell*width)+Vector3.up*cup);
                batch.Points.Add(mid);
                batch.Points.Add(mid+side*(swell*width)+Vector3.up*cup);
                if(i==0) continue;
                int c=initial+i*3,p=c-3;
                batch.Faces.AddRange(new[]{p,c,p+1,p+1,c,c+1,p+1,c+1,p+2,p+2,c+1,c+2});
            }
        }

        void BloomStem(NurseryGeometry batch,Vector3 root,Vector3 tip,float rootRadius)
        {
            var tangent=(tip-root).normalized;
            var sid=Vector3.Cross(Mathf.Abs(tangent.y)>.97f?Vector3.left:Vector3.up,tangent).normalized;
            var depth=Vector3.Cross(sid,tangent);
            const int edge=5, coursing=3;
            var offset=batch.Points.Count;
            for(int slice=0;slice<=coursing;slice++)
            for(int i=0;i<edge;i++)
            {
                var t=slice/(float)coursing;
                float a=i*2*Mathf.PI/edge;
                var rim=(sid*Mathf.Cos(a)+depth*Mathf.Sin(a))*rootRadius*Mathf.Lerp(1,.56f,t);
                batch.Points.Add(Vector3.Lerp(root,tip,t)+rim);
                if(slice<=0) continue;
                int b=offset+slice*edge+i,c=offset+slice*edge+(i+1)%edge;
                batch.Faces.AddRange(new[]{b-edge,b,c-edge,c-edge,b,c});
            }
        }

        void BloomRosette(Dictionary<Material,NurseryGeometry> plants,Vector3 root,float scale,int seed,bool tall,int color)
        {
            MakeNurseryMaterials();
            var green=PlantMaterial(plants,seed%3==0?herbMoss:herbFern);
            var stems=PlantMaterial(plants,herbStem);
            var angle=Mathf.Repeat(seed*73.6f,360);
            const int outerLeaves=6;
            for(int i=0;i<outerLeaves;i++)
            {
                float azimuth=angle+i*360f/outerLeaves;
                float jitter=.82f+.16f*Mathf.Sin(seed*.71f+i*4);
                BloomBlade(green,root+Vector3.up*(.015f*i/outerLeaves),azimuth,.26f,.27f*scale*jitter,
                    .095f*scale*jitter,.067f*scale);
            }
            for(int i=0;i<4;i++)
                BloomBlade(green,root+Vector3.up*.033f*scale,angle+i*90+29,.57f,.21f*scale,.066f*scale,.071f*scale);
            if(!tall) return;

            var blossom=(color%4) switch { 1=>herbCoral, 2=>herbCream, 3=>herbBlue, _=>herbGold };
            var petals=PlantMaterial(plants,blossom);
            var middle=PlantMaterial(plants,color%2==0?herbGold:herbCream);
            int stemsCount=color%4==3?3:2;
            for(int stem=0;stem<stemsCount;stem++)
            {
                var direction=Quaternion.Euler(0,angle+stem*138,0)*Vector3.forward;
                float height=(.35f+Mathf.Sin(stem*4.4f+seed)*.082f)*scale;
                var top=root+direction*(.089f+stem*.018f)*scale+Vector3.up*height;
                BloomStem(stems,root,top,.016f*scale);
                BloomBlade(green,Vector3.Lerp(root,top,.5f),angle+stem*81,.60f,.15f*scale,.049f*scale,.022f*scale);
                for(int i=0;i<6;i++)
                    BloomBlade(petals,top,angle+i*60,(color%4==3?.66f:.14f),.106f*scale,.043f*scale,.033f*scale);
                for(int i=0;i<3;i++)
                    BloomBlade(middle,top+Vector3.up*.019f*scale,angle+i*120,.72f,.048f*scale,.028f*scale,.027f*scale);
            }
        }

        void PlantNurseryBed(Transform bed,int variety)
        {
            var flowers=new Dictionary<Material,NurseryGeometry>();
            for(int row=0;row<4;row++)
            for(int col=0;col<7;col++)
            {
                int offset=row*7+col;
                float variation=Mathf.Sin(row*7.7f+col*9.8f+variety*12.3f);
                float x=(col-3)*.527f+variation*.061f;
                float z=(row-1.5f)*.51f+Mathf.Sin(offset*4.1f)*.077f;
                float scale=.89f+.24f*Mathf.Abs(variation);
                bool stands=variety%6!=2 || offset%4==0;
                int variant=variety%6==1||variety%6==4?1:variety%6==5?3:variety%6==2?2:0;
                BloomRosette(flowers,new Vector3(x,.51f,z),scale,offset+variety*77,stands,variant);
            }
            PublishPlants(bed,"分株花畦",flowers);
        }

        void DressNurseryPaths(Transform terrace)
        {
            MakeNurseryMaterials();
            var foliage=new Dictionary<Material,NurseryGeometry>();
            var islands=new Vector3[]
            {
                new(-25.61f,0,-15.66f),new(-24.44f,0,-15.68f),new(-23.90f,0,-16.76f),
                new(-22.36f,0,-15.61f),new(-21.24f,0,-15.48f),new(-19.81f,0,-15.56f),
                new(-14.88f,0,-17.09f),new(-13.52f,0,-19.44f),new(-13.60f,0,-21.78f),
                new(-15.20f,0,-24.79f),new(-15.65f,0,-26.45f),new(-16.63f,0,-27.57f),
                new(-24.89f,0,-30.08f),new(-22.21f,0,-28.55f),new(-18.62f,0,-30.40f)
            };
            for(int i=0;i<islands.Length;i++)
            {
                var sample=islands[i];
                if(CoastHeight(sample.x,sample.z)<1.4f) continue;
                for(int k=0;k<5;k++)
                {
                    float yaw=i*91+k*137.5f;
                    float radius=k==0?0:.27f+Mathf.Sin(i*6f+k)*.10f;
                    var p=sample+Quaternion.Euler(0,yaw,0)*Vector3.forward*radius;
                    p.y=CoastHeight(p.x,p.z)+.021f;
                    bool nearWater=i>=6&&i<=11;
                    bool tall=nearWater?k==0:i%4!=2||k%3==0;
                    float scale=(nearWater?.69f:.88f)+Mathf.Sin(i+k)*.14f;
                    BloomRosette(foliage,p,scale,i*29+k,tall,i%4);
                }
            }
            PublishPlants(terrace,"沿坡脚和渠道成片栽种的护边花草",foliage);

            var planters=new Vector3[]
            {
                new(-17.51f,0,-31.95f),new(-18.60f,0,-30.48f),new(-20.27f,0,-28.97f),
                new(-25.96f,0,-27.64f),new(-25.78f,0,-18.49f)
            };
            for(int i=0;i<planters.Length;i++)
            {
                var p=planters[i];
                p.y=CoastHeight(p.x,p.z);
                BuildNurseryPot(terrace,p,.63f+.12f*Mathf.Sin(i*2.3f),i*44,i%4);
            }
        }

        void BuildNurseryWorkCourt(Transform parent,Vector3 seed,Vector3 approach)
        {
            var workCourt=Group("育苗照料区 · 暖石踏坪与盆栽角",parent);
            var toward=HorizontalDirection(approach-seed);
            var across=Vector3.Cross(Vector3.up,toward);
            var leaves=new Dictionary<Material,NurseryGeometry>();

            // Existing bare interaction approach remains open between the low, planted edges.
            for (int row=0;row<6;row++)
            for (int column=-2;column<=2;column++)
            {
                float bend=(row*row*.013f-.05f)*Mathf.Sin(row*.64f);
                float shift=column*.43f+.17f*(row%2)+bend;
                var p=seed+toward*(1.03f+row*.43f)+across*shift;
                if (Vector2.Distance(new Vector2(p.x,p.z),new Vector2(seed.x,seed.z))<1.02f) continue;
                if (Mathf.Abs(column)==2&&(row+column)%3!=0) continue;
                p.y=CoastHeight(p.x,p.z)-.016f;
                SampleStone(workCourt,"埋进泥面的不规则育苗踏石",p,
                    new Vector3(.49f+Mathf.Sin(column*7+row)*.066f,.077f,.41f),76+row*23+column,
                    (row+column+3)%6==0?sandstone:limestone,
                    Quaternion.LookRotation(toward)*Quaternion.Euler(0,7f*Mathf.Cos(row+column*1.8f),0));
            }

            for (int i=0;i<15;i++)
            {
                float angle=i*2*Mathf.PI/15f;
                if (Mathf.Cos(angle)>.23f) continue;
                var offset=toward*Mathf.Cos(angle)+across*Mathf.Sin(angle);
                var p=seed+offset*(.84f+Mathf.Sin(i*2.4f)*.065f);
                p.y=CoastHeight(p.x,p.z)+.035f;
                SampleStone(workCourt,"苗床边的低暖石",p,new Vector3(.34f,.143f,.31f),79+i*19,
                    i%4==0?sandstone:limestone,Quaternion.LookRotation(offset));
                if (i%3==0) BloomRosette(leaves,p+offset*.21f,.49f,i*61,i%2==0,i%4);
            }

            var display=new[] { new Vector2(-18.55f,-21.35f),new Vector2(-18.82f,-21.94f),new Vector2(-17.93f,-22.58f) };
            for(int i=0;i<display.Length;i++)
            {
                var at=display[i];
                var p=new Vector3(at.x,CoastHeight(at.x,at.y),at.y);
                BuildNurseryPot(workCourt,p,.43f+.075f*i,154+i*73,i+1);
            }
            PublishPlants(workCourt,"苗床石脚的矮花和新叶",leaves);
        }

        void BuildSteppedNurseryGarden(Transform root)
        {
            var gardens=Group("苗圃从下到上的石砌观花梯田",root);
            var apron=Paint("梯田 · 石灰岩坡脚的暖金",new Color(.77f,.70f,.55f),.13f,.25f);
            var darker=Paint("梯田 · 旧浅土石",new Color(.60f,.56f,.442f),.13f,.20f);
            var sanded=Paint("梯田 · 暖白截面",new Color(.82f,.75f,.63f),.17f,.17f);
            BuildNurseryTerraceFront(gardens,3.09f,1.55f,darker,
                new Vector2(-26.60f,-24.38f),new Vector2(-25.37f,-23.79f),new Vector2(-23.89f,-23.59f),
                new Vector2(-21.39f,-23.61f),new Vector2(-20.36f,-24.10f));
            BuildNurseryTerraceFront(gardens,3.37f,.75f,darker,
                new Vector2(-27.60f,-27.02f),new Vector2(-26.10f,-27.28f),new Vector2(-24.83f,-27.59f),
                new Vector2(-22.66f,-27.65f),new Vector2(-21.24f,-27.94f));
            BuildNurseryTerraceFront(gardens,4.12f,1.58f,darker,
                new Vector2(-26.52f,-29.62f),new Vector2(-25.86f,-29.03f),new Vector2(-23.78f,-28.77f),
                new Vector2(-20.35f,-28.72f),new Vector2(-19.77f,-29.17f));
            var shelves=new[]
            {
                new Vector4(-27.4f,-22.8f,3.65f,2.46f),
                new Vector4(-29.4f,-19.2f,3.65f,2.16f),
                new Vector4(-23.4f,-14.0f,4.80f,1.33f),
                new Vector4(-18.25f,-27.95f,4.18f,2.35f),
                new Vector4(-23.4f,-28.43f,4.62f,1.63f),
                new Vector4(-27.87f,-30.03f,3.19f,1.71f),
                new Vector4(-27.72f,-34.81f,2.75f,2.18f),
                new Vector4(-17.40f,-34.74f,2.54f,2.63f),
                new Vector4(-20.40f,-37.02f,3.40f,1.72f)
            };
            for(int i=0;i<shelves.Length;i++)
            {
                var patch=shelves[i];
                int tier=i<3?0:i<6?1:2;
                var midpoint=new Vector3(patch.x,0,patch.y);
                float bottom=CoastHeight(patch.x,patch.y);
                if(bottom<.45f) continue;
                float elevation=bottom+.28f;
                if(tier==1) elevation=Mathf.Max(3.30f,elevation);
                if(tier==2) elevation=Mathf.Max(4.22f,elevation);
                var bed=Group("阶田花台 · 第"+(tier+1)+"层",gardens);
                bed.localPosition=midpoint;
                var flowers=new Dictionary<Material,NurseryGeometry>();
                var back=patch.w*.5f;
                var across=patch.z*.5f;
                SampleStone(bed,"湿润种植土与透气下层",new Vector3(0,elevation-.16f,0),
                    new Vector3(patch.z,.31f,patch.w),107+i*42,soil);
                for(int side=-1;side<=1;side+=2)
                {
                    int n=Mathf.CeilToInt(patch.z/.48f);
                    for(int section=0;section<n;section++)
                    {
                        var x=(((section+.5f)/n)*2-1)*across;
                        var toe=CoastHeight(patch.x+x,patch.y+side*back);
                        float visible=Mathf.Max(.20f,elevation-toe+.13f);
                        int courses=Mathf.CeilToInt(visible/.235f);
                        var w=patch.z/n-.015f;
                        for(int row=0;row<courses;row++)
                        {
                            float shift=(row%2==1 && section>0 && section<n-1?.14f:0) * (side==-1?1:-1);
                            var at=new Vector3(x+shift,elevation-.108f-row*.222f,side*back);
                            SampleStone(bed,"阶田外墙错缝石料",at,new Vector3(w,.226f,.28f),137+i*7+row*13+section,
                                (row+section+i)%7==0?darker:apron,Quaternion.Euler(0,side*2f*Mathf.Sin(section*3+i),0));
                        }
                        var cap=new Vector3(x,elevation+.039f,side*back);
                        SampleStone(bed,"厚石墙帽边",cap,new Vector3(w+.022f,.13f,.36f),163+section*4+i,sanded);
                    }

                    int m=Mathf.CeilToInt(patch.w/.53f);
                    for(int j=0;j<m;j++)
                    {
                        var z=(((j+.5f)/m)*2-1)*back;
                        float toe=CoastHeight(patch.x+side*across,patch.y+z);
                        int courses=Mathf.CeilToInt(Mathf.Max(.21f,elevation-toe+.12f)/.21f);
                        for(int row=0;row<courses;row++)
                        {
                            var at=new Vector3(side*across,elevation-.115f-row*.20f,z);
                            SampleStone(bed,"阶田端墙",at,new Vector3(.28f,.209f,patch.w/m-.016f),51+j+row*29,
                                (i+j+row)%6==0?darker:apron);
                        }
                    }
                }
                for(int row=0;row<4;row++)
                for(int col=0;col<9;col++)
                {
                    float sway=Mathf.Sin(row*61+col*21+i);
                    var p=new Vector3((col-4)*patch.z*.101f+sway*.056f,elevation+.026f,(row-1.5f)*patch.w*.208f);
                    float size=.89f+(sway+1)*.14f;
                    var tone=i%3==0?0:i%3==1?1:i%2==0?2:3;
                    BloomRosette(flowers,p,size,i*47+row*31+col,col%6!=5,tone);
                }
                PublishPlants(bed,"一整层丰盛的种植带",flowers);
            }
            BuildTerraceStair(gardens,new Vector2(-20.67f,-28.34f),new Vector2(-21.3f,-31.11f),2.12f,apron,sanded);
            BuildTerraceStair(gardens,new Vector2(-26.67f,-25.4f),new Vector2(-27.46f,-28.52f),1.79f,apron,sanded);
        }

        void BuildNurseryTerraceFront(Transform parent,float elevation,float depth,Material mortar,params Vector2[] frontage)
        {
            var ledge=Group("横跨多座花畦的实体台基",parent);
            var surface=new List<Vector3>(frontage.Length*2);
            var triangles=new List<int>();
            for (int i=0;i<frontage.Length;i++)
            {
                var point=frontage[i];
                var rear=point+new Vector2(0,-depth);
                surface.Add(new Vector3(point.x,elevation-.081f,point.y));
                surface.Add(new Vector3(rear.x,Mathf.Max(elevation-.081f,CoastHeight(rear.x,rear.y)),rear.y));
                if (i==0) continue;
                var before=frontage[i-1];
                var middle=(point+before)*.5f;
                float foot=Mathf.Min(CoastHeight(point.x,point.y),CoastHeight(middle.x,middle.y),CoastHeight(before.x,before.y));
                const float course=.236f;
                int levels=Mathf.Clamp(Mathf.CeilToInt((elevation-foot+.15f)/course),2,8);
                float foundation=elevation-course*levels;
                SampleRetaining(ledge,new Vector3(before.x,foundation,before.y),new Vector3(point.x,foundation,point.y),
                    course,levels,mortar,false);
                int edge=i*2;
                triangles.AddRange(new[] { edge-2,edge,edge-1,edge-1,edge,edge+1 });
            }
            MeshObject("挡墙后的连续土台",ledge,surface,triangles,limestone);
        }

        void BuildTerraceStair(Transform parent,Vector2 low,Vector2 high,float span,Material stone,Material caps)
        {
            var from=new Vector3(low.x,CoastHeight(low.x,low.y),low.y);
            var to=new Vector3(high.x,CoastHeight(high.x,high.y),high.y);
            if(to.y<from.y+.38f) return;
            var pathTo=HorizontalDirection(to-from);
            var face=Quaternion.LookRotation(pathTo);
            float depth=(high-low).magnitude;
            var stair=Group("贴等高线的花圃石阶",parent);
            int risers=Mathf.Max(5,Mathf.CeilToInt((to.y-from.y)/.19f));
            for(int i=0;i<risers;i++)
            {
                var center=Vector3.Lerp(from,to,(i+.5f)/risers);
                float top=Mathf.Lerp(from.y,to.y,(i+1f)/risers);
                float groundY=CoastHeight(center.x,center.z);
                float rise=Mathf.Max(.23f,top-groundY+.12f);
                center.y=top-rise*.5f;
                var collision=Box(stair,"连续的石级踏面",center,new Vector3(span,rise,depth/risers+.036f),
                    stone,FloorLayer);
                collision.transform.localRotation=face;
                var at=center+Vector3.up*(rise*.5f);
                SampleStone(stair,"白石踏级压边",at-Vector3.up*.015f,new Vector3(span+.066f,.101f,depth/risers+.026f),
                    i*31+127,caps,face);
            }
        }

        void BuildNurseryPot(Transform parent,Vector3 profile,float size,int seed,int tone)
        {
            MakeNurseryMaterials();
            var urn=Group("有空腹和卷沿的苗圃赤陶盆",parent);
            urn.localPosition=profile;
            urn.localScale=Vector3.one*size;
            var cross=new[]
            {
                new Vector2(.25f,0),new Vector2(.30f,.05f),new Vector2(.35f,.24f),new Vector2(.44f,.54f),
                new Vector2(.445f,.61f),new Vector2(.49f,.64f),new Vector2(.49f,.70f),new Vector2(.45f,.73f),
                new Vector2(.388f,.69f),new Vector2(.38f,.62f),new Vector2(.29f,.38f)
            };
            var v=new List<Vector3>();
            var body=new List<int>();
            var trim=new List<int>();
            const int sides=22;
            for(int j=0;j<cross.Length;j++)
            for(int i=0;i<sides;i++)
            {
                float a=i*2*Mathf.PI/sides;
                float press=cross[j].x*(1+.026f*Mathf.Sin(i*5.2f+seed));
                v.Add(new Vector3(Mathf.Cos(a)*press,cross[j].y,Mathf.Sin(a)*press));
                if(j<=0) continue;
                int p=(j-1)*sides+i,q=(j-1)*sides+(i+1)%sides;
                var indices=j>=5&&j<=8?trim:body;
                indices.AddRange(new[]{p,p+sides,q,q,p+sides,q+sides});
            }
            MeshObject("手拉坯盆腹",urn,v,body,clayCotta);
            MeshObject("卷陶盆嘴",urn,v,trim,clayRim);
            Cylinder(urn,"盆内土面",new Vector3(0,.530f,0),new Vector3(.745f,.091f,.745f),soil);
            var flowers=new Dictionary<Material,NurseryGeometry>();
            for(int i=0;i<4;i++)
            {
                var position=Quaternion.Euler(0,i*90f+seed,0)*Vector3.forward*.128f+Vector3.up*.64f;
                BloomRosette(flowers,position,1.05f+.27f*Mathf.Sin(seed+i*4.1f),seed+37*i,true,tone);
            }
            PublishPlants(urn,"盆栽错落小花",flowers);
        }

        void DressSampleGlasshouse(Transform station,float halfWidth,float halfLength)
        {
            MakeNurseryMaterials();
            var climbers=new Dictionary<Material,NurseryGeometry>();
            var leafPart=PlantMaterial(climbers,herbFern);
            var stems=PlantMaterial(climbers,herbStem);
            var petals=PlantMaterial(climbers,herbCream);
            foreach(float sign in new[]{-1f,1f})
            {
                for(int pot=0;pot<4;pot++)
                {
                    var outside = new Vector3(sign*(pot%2==0?1.76f:2.71f),.05f,halfLength+.20f+pot*.42f);
                    BuildNurseryPot(station,outside,.63f+.11f*pot,pot*79+(int)(sign*8),pot+1);
                }
                for(int vine=0;vine<3;vine++)
                {
                    var start=new Vector3(sign*(1.16f+vine*.82f),2.53f-vine*.20f,halfLength+.082f);
                    for(int i=0;i<11;i++)
                    {
                        var p=start+new Vector3(sign*.14f*Mathf.Sin(i*.44f+vine),-.108f*i,Mathf.Sin(i*3)*.046f);
                        var next=start+new Vector3(sign*.14f*Mathf.Sin((i+1)*.44f+vine),-.108f*(i+1),Mathf.Sin((i+1)*3)*.046f);
                        BloomStem(stems,p,next,.017f);
                        BloomBlade(leafPart,p,sign*(i%2==0?72f:-61f),-.31f,.177f,.072f,-.051f);
                        if(i%4==1)
                            BloomBlade(petals,p,sign*(-34f+i*9),-.18f,.11f,.044f,-.034f);
                    }
                }
            }
            PublishPlants(station,"温室檐下攀藤",climbers);
        }

        void FinishSampleBridge(Transform root,List<Vector3> deck,Vector3 side,float width)
        {
            var endGrain=Paint("溪桥 · 蜜褐端木",new Color(.73f,.50f,.292f),.19f,.24f);
            var nut=Paint("溪桥 · 暖铜木钉",new Color(.64f,.39f,.17f),.42f,.11f);
            var span=deck[deck.Count-1]-deck[0];
            var toward=HorizontalDirection(span);
            int railIntervals=Mathf.Max(5,Mathf.RoundToInt(span.magnitude/1.13f));
            var arch=new List<Vector3>(deck.Count);
            for(int i=0;i<deck.Count;i++)
            {
                float fraction=i/(deck.Count-1f);
                // A deeper independent bearing arch meets the real masonry piers;
                // the walkable deck above keeps its gentler, separately defined rise.
                float drop=Mathf.Lerp(1.80f,.32f,Mathf.Sin(fraction*Mathf.PI));
                arch.Add(deck[i]-Vector3.up*drop);
            }
            foreach(float sign in new[]{-1f,1f})
            {
                Vector3 bank=side*(width*.469f*sign);
                for(int i=0;i<=railIntervals;i++)
                {
                    int position=Mathf.RoundToInt(i*(deck.Count-1f)/railIntervals);
                    Vector3 where=deck[position]+bank;
                    bool endPost=i==0||i==railIntervals;
                    float high=(endPost?.81f:.73f)*ReferenceBridgeRailScale;
                    var block=new Vector3(.132f,high,.133f);
                    SampleStone(root,"方材刨角桥栏柱",where+Vector3.up*(high*.5f),block,13+i*5,
                        endPost?endGrain:wood,Quaternion.LookRotation(toward));
                    var guard=Box(root,"桥栏柱体碰撞",where+Vector3.up*(high*.5f),block,wood,ObstacleLayer);
                    guard.GetComponent<Renderer>().enabled=false;
                    var head=SampleBridgeFastener(root,where+Vector3.up*high,.067f,.026f,brass);
                    head.name="榫柱铜套帽";
                    Vector3 face=sign*side;
                    for(int j=0;j<2;j++)
                    {
                        var stud=SampleBridgeFastener(root,where+Vector3.up*((.335f+j*.205f)*ReferenceBridgeRailScale)+face*.076f,.035f,.018f,nut);
                        stud.transform.rotation=Quaternion.FromToRotation(Vector3.up,face);
                    }
                }
                SweptTimber(root,"顺拱桥栏扶手",deck,bank+Vector3.up*(.688f*ReferenceBridgeRailScale),.076f,.045f,endGrain,side);
                SweptTimber(root,"细榫腰桁",deck,bank+Vector3.up*(.337f*ReferenceBridgeRailScale),.044f,.039f,wood,side);
                var frameOffset=bank-side*(sign*.142f);
                SweptTimber(root,"跟随桥拱的承压侧梁",deck,frameOffset-Vector3.up*.153f,.103f,.091f,darkWood,side);
                SweptTimber(root,"架在石岸上的弯曲木拱",arch,frameOffset,.125f,.137f,endGrain,side);
                for(int i=1;i<railIntervals;i++)
                {
                    int position=Mathf.RoundToInt(i*(deck.Count-1f)/railIntervals);
                    var top=deck[position]+frameOffset-Vector3.up*.16f;
                    var bottom=arch[position]+frameOffset;
                    Beam(root,"桥板与主拱之间的榫接竖材",bottom,top,.114f,darkWood);
                }
                for(int i=0;i<deck.Count;i+=2)
                {
                    var p=deck[i]+bank-side*(sign*.118f)+Vector3.up*.022f;
                    SampleBridgeFastener(root,p,.023f,.006f,nut);
                }
            }
            var foot=Group("溪桥两端的石岸花草",root);
            var shorePlants=new Dictionary<Material,NurseryGeometry>();
            for(int i=0;i<4;i++)
            {
                var end=i/2==0?deck[0]:deck[deck.Count-1];
                var back=i/2==0?-toward:toward;
                var right=i%2==0?side:-side;
                for(int j=0;j<11;j++)
                {
                    var p=end+back*(.38f+j*.091f)+right*(width*.61f+Mathf.Sin(j*7.7f)*.25f);
                    p.y=CoastHeight(p.x,p.z);
                    if(p.y<-.08f) continue;
                    BloomRosette(shorePlants,p,.65f+Mathf.Sin(i*7.1f+j)*.16f,i*47+j,j%3!=0,(i+j)%4);
                }
            }
            PublishPlants(foot,"桥台草坡中的花叶",shorePlants);
        }

        GameObject SampleBridgeFastener(Transform root,Vector3 p,float radius,float depth,Material material)
            => Cylinder(root,"铜色端铆",p,new Vector3(radius*2,depth*.5f,radius*2),material);

        void SweptTimber(Transform parent,string name,List<Vector3> route,Vector3 offset,float half,float rise,Material mat,
            Vector3 horizontalAxis)
        {
            var points=new List<Vector3>();
            var tris=new List<int>();
            const int edges=8;
            for(int i=0;i<route.Count;i++)
            {
                var center=route[i]+offset;
                for(int j=0;j<edges;j++)
                {
                    float a=(j+.5f)*2*Mathf.PI/edges;
                    points.Add(center + horizontalAxis*(Mathf.Cos(a)*half)+Vector3.up*(Mathf.Sin(a)*rise));
                    if(i==0) continue;
                    int current=i*edges+j,next=i*edges+(j+1)%edges;
                    tris.AddRange(new[]{current-edges,next-edges,current,current,next-edges,next});
                }
            }
            for(int i=1;i<edges-1;i++)
            {
                int start=(route.Count-1)*edges;
                tris.AddRange(new[]{0,i+1,i,start,start+i,start+i+1});
            }
            MeshObject(name,parent,points,tris,mat);
        }
    }
}
