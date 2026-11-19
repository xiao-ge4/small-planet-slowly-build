using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void BuildHome()
        {
            var court = Group("01 · 回家小院",details);
            BuildSampleBridgeAbutments();
            var grout = Paint("小院浅砂浆",new Color(.69f,.59f,.43f),.12f,.21f);
            var cobbles = new[]
            {
                Paint("小院旧石板 · 米色",new Color(.77f,.67f,.50f),.16f,.20f),
                Paint("小院旧石板 · 金砂色",new Color(.70f,.58f,.40f),.12f,.25f),
                Paint("小院旧石板 · 灰砂色",new Color(.64f,.56f,.44f),.13f,.22f)
            };

            for (int row=0;row<11;row++)
            for (int col=0;col<9;col++)
            {
                var x=-4.1f+col*.78f+(row%2)*.39f;
                var z=-.56f+row*.56f;
                var dx=(x+.45f)/3.99f;
                var dz=(z-2.19f)/3.07f;
                if(dx*dx+dz*dz>1 + .08f*Mathf.Sin(row*2+col)) continue;
                if((new Vector2(x-2.7f,z-1.65f)).sqrMagnitude<1.07f) continue;
                int seed=row*57+col*11;
                var y=CoastHeight(x,z);
                SampleStone(court,"手凿庭院铺石",new Vector3(x,y-.037f,z),new Vector3(.755f,.105f,.53f),
                    seed,cobbles[(seed%7)/3]);
            }

            var wallStart = new Vector3(-5.75f,0,.17f);
            var wallEnd = new Vector3(-6.7f,0,3.29f);
            SampleRetaining(court,wallStart,wallEnd,.53f,2,grout);
            Asset("garden-tree",court,new Vector3(-6.5f,CoastHeight(-6.5f,.84f),.84f),1.35f,68,true);
            Asset("moss-boulder",court,new Vector3(-3.55f,CoastHeight(-3.55f,5.92f),5.92f),.9f,62,true);
            for (int i=0;i<11;i++)
            {
                var a=i*.60f;
                var p=new Vector3(-.6f+Mathf.Cos(a)*4.8f,0,1.5f+Mathf.Sin(a)*3.8f);
                p.y=CoastHeight(p.x,p.z)+.02f;
                if(p.y<-.33f) continue;
                Asset("meadow-flower",court,p,.47f+Mathf.Abs(Mathf.Sin(i*12))*.28f,i*53);
            }

            var pondCenter = new Vector3(2.7f,.032f,1.65f);
            foreach (float a in new[]{.22f,1.04f,1.93f,2.76f,4.09f,4.63f,5.55f})
            {
                var p=pondCenter+new Vector3(Mathf.Cos(a)*1.11f,.044f,Mathf.Sin(a)*1.05f);
                SampleStone(court,"水池磨圆压边",p,new Vector3(.32f,.18f,.29f),Mathf.RoundToInt(a*47),limestone);
            }
            var sitting = Asset("mossy-stone-bench",court,new Vector3(-5.03f,CoastHeight(-5.03f,2.65f),2.65f),.71f,83,false);
            if (sitting != null) return;
            var seat = Group("低石座",court);
            seat.localPosition=new Vector3(-5.03f,CoastHeight(-5.03f,2.65f),2.65f);
            SampleStone(seat,"靠院石座",new Vector3(0,.47f,0),new Vector3(.62f,.16f,1.65f),177,limestone);
            foreach (var z in new[]{-.50f,.51f})
                SampleStone(seat,"矮座支石",new Vector3(0,.17f,z),new Vector3(.49f,.36f,.26f),88,slate);
        }

        void BuildSampleBridgeAbutments(bool includeEast=true)
        {
            var pair = Group("溪桥 · 嵌岸石桥台",details);
            var facing = HorizontalDirection(SampleBridgeEnd-SampleBridgeStart);
            foreach (var p in new[]{SampleBridgeStart,SampleBridgeEnd})
            {
                if(!includeEast && p==SampleBridgeEnd)continue;
                var intoBridge = p == SampleBridgeStart ? facing : -facing;
                var rotation=Quaternion.LookRotation(intoBridge);
                var deckSide=Vector3.Cross(Vector3.up,intoBridge);
                var slab = p+intoBridge*.19f+Vector3.down*.145f;
                SampleStone(pair,"对接桥板的厚压顶",slab,new Vector3(3.42f,.25f,.80f),126,limestone,rotation);
                var below=p+intoBridge*.51f;
                float rockFoundation=CoastHeight(below.x,below.z);
                int courses=Mathf.Clamp(Mathf.CeilToInt((slab.y-rockFoundation+.24f)/.28f),3,10);
                for (int tier=0;tier<courses;tier++)
                for (int i=0;i<5;i++)
                {
                    var position=slab+deckSide*((i-2)*.62f+(tier%2)*.048f)
                        +intoBridge*(.05f-tier*.049f)-Vector3.up*(.21f+tier*.28f);
                    SampleStone(pair,"退进河岸的桥台砌层",position,
                        new Vector3(.65f,.30f,.73f),i+32*tier,(i+tier)%5==0?slate:limestone,rotation);
                }
            }
        }

        void BuildNursery()
        {
            var terrace = Group("02 · 水渠苗圃",details);
            var mortar=Paint("苗圃挡土墙 · 砂缝",new Color(.55f,.49f,.37f),.10f,.22f);
            BuildSampleGlasshouse(terrace,new Vector3(-22.8f,4.12f,-32.4f));

            // Varied low terraces leave a clear approach to the interactable seedling.
            var beds = new[]
            {
                new Vector3(-22.6f,2.6f,-17.20f),
                new Vector3(-22.9f,2.6f,-21.31f),
                new Vector3(-22.5f,2.6f,-26.07f),
                new Vector3(-17.35f,2.6f,-25.47f),
                new Vector3(-17.30f,2.6f,-16.63f),
                new Vector3(-29.12f,2.64f,-25.50f)
            };
            for(int i=0;i<beds.Length;i++)
            {
                var at=beds[i];
                at.y=CoastHeight(at.x,at.z);
                BuildSampleBed(terrace,at,i);
            }
            SampleStone(terrace,"玩家苗床湿土",new Vector3(-16.1f,2.636f,-20.6f),new Vector3(1.48f,.075f,1.51f),5,soil);
            SampleRetaining(terrace,new Vector3(-24.3f,2.58f,-28.87f),new Vector3(-15.2f,2.58f,-28.87f),.49f,3,mortar);
            SampleRetaining(terrace,new Vector3(-20.1f,3.54f,-30.6f),new Vector3(-15.2f,3.54f,-30.6f),.37f,2,mortar);
            SampleRetaining(terrace,new Vector3(-24.3f,2.52f,-15.46f),new Vector3(-17.8f,2.52f,-15.46f),.42f,2,mortar);
            // These dry-stone faces retain the terrace shoulders and stop short of the player lanes.
            SampleRetaining(terrace,new Vector3(-16.7f,1.63f,-15.9f),new Vector3(-11.8f,1.63f,-18.8f),.42f,2,mortar,false);
            SampleRetaining(terrace,new Vector3(-15.2f,1.69f,-25.2f),new Vector3(-14.5f,1.69f,-28.81f),.43f,2,mortar,false);
            SampleRetaining(terrace,new Vector3(-27.9f,2.73f,-28.33f),new Vector3(-24.4f,2.73f,-28.78f),.47f,2,mortar,false);
            for(int i=0;i<6;i++)
            {
                float x=-25.07f+i*1.91f;
                var p=new Vector3(x,CoastHeight(x,-15.36f),-15.36f);
                if(p.y>1.9f) Asset("meadow-flower",terrace,p,.72f,i*52);
            }

            // The upper headrace feeds the main cut, and the lower offtake follows the nursery rows.
            SampleTrough(terrace,.64f,
                new Vector3(-18.7f,4.35f,-32.91f),
                new Vector3(-17.3f,4.32f,-32.91f),
                new Vector3(-15.0f,4.30f,-32.91f),
                new Vector3(-12.5f,4.29f,-32.90f));
            SampleTrough(terrace,.41f,
                new Vector3(-19.25f,4.34f,-32.80f),
                new Vector3(-19.28f,4.31f,-31.15f),
                new Vector3(-19.37f,4.29f,-29.72f),
                new Vector3(-19.37f,3.56f,-28.94f),
                new Vector3(-19.49f,3.54f,-27.86f),
                new Vector3(-19.72f,3.14f,-26.96f),
                new Vector3(-19.89f,3.13f,-25.48f));
            SampleTrough(terrace,.55f,
                new Vector3(-19.89f,3.13f,-25.48f),
                new Vector3(-19.79f,3.11f,-24.63f),
                new Vector3(-19.42f,2.97f,-23.80f),
                new Vector3(-18.63f,2.95f,-23.73f),
                new Vector3(-17.43f,2.94f,-23.83f),
                new Vector3(-16.25f,2.93f,-23.76f),
                new Vector3(-15.41f,2.91f,-24.08f),
                new Vector3(-14.65f,2.86f,-24.38f),
                new Vector3(-13.58f,2.83f,-24.48f),
                new Vector3(-12.43f,2.80f,-24.18f),
                new Vector3(-11.25f,2.78f,-23.89f),
                new Vector3(-10.27f,2.76f,-23.36f),RiverProfile[2]);

            var head = Group("引水池与可见出水口",terrace);
            head.localPosition=new Vector3(-18.87f,4.18f,-32.94f);
            SampleStone(head,"水池暗底",new Vector3(-.24f,.035f,0),new Vector3(1.83f,.20f,1.66f),40,wetStone);
            foreach(var sign in new[] {-1,1})
                SampleStone(head,"引水池砌壁",new Vector3(-.23f,.15f,sign*.73f),new Vector3(1.94f,.34f,.26f),41+sign,limestone);
            SampleStone(head,"引水池止水壁",new Vector3(-1.1f,.15f,0),new Vector3(.25f,.35f,1.54f),48,limestone);
            Box(head,"引水池蓄水",new Vector3(-.25f,.17f,0),new Vector3(1.52f,.025f,1.20f),water);

            BuildSampleWheel(terrace);
            DressNurseryPaths(terrace);
            BuildSteppedNurseryGarden(terrace);
            var seedling=game.FindInteraction(GardenInteractionKind.Water);
            BuildNurseryWorkCourt(terrace,seedling.transform.position,seedling.approach.position);
            var crates=Group("育秧物料",terrace);
            crates.localPosition=new Vector3(-25.47f,CoastHeight(-25.47f,-16.25f),-16.25f);
            for(int i=0;i<3;i++)
            {
                var p = new Vector3(i*.56f,0,.05f*Mathf.Sin(i));
                Box(crates,"旧木育种箱",p+Vector3.up*.19f,new Vector3(.57f,.38f,.53f),wood);
                foreach(float z in new[] {-.246f,.246f})
                    foreach(float y in new[] {.06f,.22f,.32f})
                        Box(crates,"料箱横木",p+new Vector3(0,y,z),new Vector3(.61f,.063f,.035f),darkWood);
            }
        }

        void BuildSampleBed(Transform terrace,Vector3 at,int variety)
        {
            var bed=Group("花圃 · 分层石墙和苗土",terrace);
            bed.localPosition=at;
            const float width=4.20f,depth=2.52f;
            Box(bed,"菜畦实土",new Vector3(0,.29f,0),new Vector3(width-.19f,.39f,depth-.19f),soil,ObstacleLayer);
            for(int row=0;row<2;row++)
            {
                float y=.114f+row*.215f;
                foreach(float sign in new[]{-1f,1f})
                {
                    for(int i=0;i<8;i++)
                    {
                        float x=(i-3.5f)*.507f+(row-1)*.052f*sign;
                        SampleStone(bed,"露出砂缝的苗床块石",new Vector3(x,y,sign*(depth*.5f-.06f)),
                            new Vector3(.49f,.212f,.32f),i+row*21+(int)(sign*7),i%5==0?slate:limestone);
                    }
                    for(int i=0;i<4;i++)
                    {
                        float z=(i-1.5f)*.527f;
                        SampleStone(bed,"苗床侧向块石",new Vector3(sign*(width*.5f-.061f),y,z),
                            new Vector3(.32f,.21f,.50f),18+i+row*19,limestone);
                    }
                    Box(bed,"苗床顶边",new Vector3(0,.479f,sign*(depth*.5f-.055f)),new Vector3(4.28f,.095f,.30f),limestone);
                }
            }
            PlantNurseryBed(bed,variety);
        }

        void BuildSampleWheel(Transform terrace)
        {
            var pivot=Group("苗圃木水轮 · 位于跌水槽上",terrace);
            pivot.localPosition=new Vector3(-9.5f,3.53f,-23f);
            var riverHeading=HorizontalDirection(new Vector3(4.7f,0,4.6f));
            pivot.localRotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,riverHeading))*Quaternion.Euler(0,-13,0);
            wheel=Group("可旋转轮体",pivot);
            var axle=Cylinder(pivot,"锻铜轮轴",Vector3.zero,new Vector3(.219f,.69f,.219f),brass);
            axle.transform.localRotation=Quaternion.Euler(90,0,0);
            foreach(var sign in new[] {-1f,1f})
            {
                var bank=Group("石砌轮轴基",pivot);
                bank.localPosition=new Vector3(0,0,sign*.92f);
                var world=bank.position;
                world.y=CoastHeight(world.x,world.z)+.106f;
                bank.position=world;
                SampleStone(bank,"浸水轴墩",new Vector3(0,.12f,0),new Vector3(.71f,.54f,.65f),78,wetStone);
                Beam(pivot,"轮轴支木",new Vector3(-.30f,-1.00f,sign*.92f),new Vector3(0,.03f,sign*.64f),.21f,darkWood);
                Beam(pivot,"轮轴支木",new Vector3(.30f,-1.00f,sign*.92f),new Vector3(0,.03f,sign*.64f),.21f,darkWood);
            }
            const int divisions=18;
            FitWheelRing(wheel, radius: 1.044f, band: .147f, depth: .072f);
            const float radius=1.04f;
            for(int i=0;i<divisions;i++)
            {
                float top=i*360f/divisions;
                var a=Quaternion.Euler(0,0,top)*Vector3.up;
                foreach (var sign in new[] {-1f,1f})
                {
                    var side=Vector3.forward*(.285f*sign);
                    Beam(wheel,"铜钉辐梁",side+a*.15f,side+radius*a,.103f,wood);
                }
                var paddle=Box(wheel,"宽木叶板",a*.923f,new Vector3(.262f,.055f,.648f),wood);
                paddle.transform.localRotation=Quaternion.Euler(0,0,top);
            }
            foreach(var sign in new[] {-1f,1f})
            {
                var hub=Cylinder(wheel,"带黄铜环的轮毂",Vector3.forward*sign*.326f,new Vector3(.32f,.062f,.32f),brass);
                hub.transform.localRotation=Quaternion.Euler(90,0,0);
            }
        }

        void BuildSampleGlasshouse(Transform terrace, Vector3 origin)
        {
            var greenhouse=Group("暖铜弧肋玻璃温室",terrace);
            greenhouse.localPosition=origin;
            const float halfWidth=3.24f,halfLength=4.34f,spring=1.15f,rise=2.53f;
            const int ribCount=6,arcSegments=12;
            var verdigris=Paint("温室铜木框 · 青金微锈",new Color(.55f,.40f,.22f),.34f,.24f);
            var glassShader = Resources.Load<Shader>("Coast/SampleGlass");
            var pane=glassShader != null ? new Material(glassShader) : new Material(glass);
            pane.name = "温室淡青玻璃";
            pane.color=new Color(.22f,.62f,.52f,.78f);
            materials.Add(pane);
            var tinted=new Material(pane) { name = "温室有深浅的淡青玻璃" };
            tinted.color=new Color(.18f,.55f,.47f,.73f);
            materials.Add(tinted);
            var measure=new List<Vector3>();
            for(int i=0;i<=arcSegments;i++)
            {
                float a=i*Mathf.PI/arcSegments;
                measure.Add(new Vector3(Mathf.Cos(a)*halfWidth,spring+Mathf.Sin(a)*rise,0));
            }
            Box(greenhouse,"温室温暖砖地",new Vector3(0,.023f,0),new Vector3(6.88f,.068f,8.93f),path);
            for(int i=0;i<=ribCount;i++)
            {
                float z=Mathf.Lerp(-halfLength,halfLength,i/(float)ribCount);
                float next=Mathf.Lerp(-halfLength,halfLength,(i+1f)/ribCount);
                foreach (int sign in new[] {-1,1})
                {
                    var a=new Vector3(sign*halfWidth,.31f,z);
                    Beam(greenhouse,"立式铜木窗柱",a,a+Vector3.up*(spring-.31f),.114f,verdigris);
                    if(i<ribCount)
                    {
                        var b=new Vector3(a.x,a.y,next);
                        SampleGlassPane("低墙透明侧窗",greenhouse,a,b,b+Vector3.up*(spring-.31f),a+Vector3.up*(spring-.31f),i%2==0?pane:tinted);
                    }
                }
                for(int j=0;j<arcSegments;j++)
                {
                    var a=measure[j]+Vector3.forward*z;
                    var b=measure[j+1]+Vector3.forward*z;
                    Beam(greenhouse,"旧铜拱肋",a,b,.125f,brass);
                    if(i==ribCount) continue;
                    var c=measure[j+1]+Vector3.forward*next;
                    var d=measure[j]+Vector3.forward*next;
                    SampleGlassPane("均分的弧形玻璃窗",greenhouse,a,b,c,d,(j+i)%5==0?tinted:pane);
                    if(j%2==0) Beam(greenhouse,"顺屋铜檩",a,d,.076f,verdigris);
                }
            }
            foreach(int sign in new[]{-1,1})
            {
                var x=sign*halfWidth;
                Box(greenhouse,"矮石地梁",new Vector3(x,.145f,0),new Vector3(.32f,.29f,8.8f),limestone,ObstacleLayer);
                foreach(float y in new[]{.37f,1.13f})
                    Beam(greenhouse,"侧窗铜横梃",new Vector3(x,y,-halfLength),new Vector3(x,y,halfLength),.089f,verdigris);
                for(int i=0;i<4;i++)
                {
                    var pos=new Vector3(sign*2.08f,.42f,-2.83f+i*1.70f);
                    Box(greenhouse,"温室两侧种植木箱",pos,new Vector3(1.32f,.79f,1.34f),darkWood,ObstacleLayer);
                    Box(greenhouse,"箱缘宽木",pos+Vector3.up*.39f,new Vector3(1.44f,.072f,1.47f),wood);
                    for(int j=0;j<3;j++)
                        Asset("meadow-flower",greenhouse,pos+new Vector3(.23f*(j-1),.52f,.28f*(j-1)),.58f+((i+j)%4)*.06f,j*57);
                }

                var door=new Vector3(sign*.96f,0,halfLength+.03f);
                Beam(greenhouse,"门框铜木侧梃",door,door+Vector3.up*2.36f,.16f,verdigris);
                SampleGlassPane("门边固定玻璃",greenhouse,door+new Vector3(0,.3f,0),new Vector3(x,.30f,door.z),
                    new Vector3(x,spring,door.z),door+Vector3.up*2.33f,pane);
                for(int panel=1;panel<=3;panel++)
                {
                    float a=panel*halfWidth/3;
                    Beam(greenhouse,"后窗竖梃",new Vector3(sign*a,.25f,-halfLength),
                        new Vector3(sign*a,spring+rise*Mathf.Sqrt(1-a*a/(halfWidth*halfWidth)),-halfLength),.079f,verdigris);
                }
                for(int i=0;i<arcSegments;i++)
                {
                    var a=measure[i]+Vector3.back*halfLength;
                    var b=measure[i+1]+Vector3.back*halfLength;
                    if(sign>0) continue;
                    SampleGlassPane("后侧分片玻璃",greenhouse,a,b,new Vector3(b.x,.28f,b.z),new Vector3(a.x,.28f,a.z),pane);
                }
            }
            Beam(greenhouse,"两侧透光门楣",new Vector3(-1.05f,2.34f,halfLength+.03f),new Vector3(1.05f,2.34f,halfLength+.03f),.14f,brass);
            var fanBase=new Vector3(0,2.39f,halfLength+.037f);
            var fan=new List<Vector3>{fanBase};
            for(int i=0;i<=10;i++)
            {
                float t=-1+i/5f;
                var point=fanBase+new Vector3(t*1.26f,(1-.72f*t*t)*1.21f,0);
                fan.Add(point);
                if(i%2==0) Beam(greenhouse,"门顶铜木辐格",fanBase,point,.048f,brass);
            }
            var fanFaces=new List<int>();
            for(int i=1;i<fan.Count-1;i++) fanFaces.AddRange(new[]{0,i+1,i});
            MeshObject("门顶弧扇青玻璃",greenhouse,fan,fanFaces,tinted);
            for(int i=1;i<fan.Count-1;i++)
                Beam(greenhouse,"扇窗弧圈",fan[i],fan[i+1],.062f,verdigris);
            var airStrip = Group("玻璃温室天窗与顶缘",greenhouse);
            foreach(float s in new[]{-1f,1f})
                Beam(airStrip,"天窗长边",new Vector3(s*.61f,3.712f,-3.55f),new Vector3(s*.61f,3.712f,2.42f),.083f,brass);
            for(int i=0;i<5;i++)
            {
                float z=-3.55f+i*1.4925f;
                Beam(airStrip,"天窗短边",new Vector3(-.63f,3.712f,z),new Vector3(.63f,3.712f,z),.071f,verdigris);
            }
            for(int i=0;i<4;i++)
            {
                float z=-3.55f+i*1.4925f;
                SampleGlassPane("有错角的绿铜天窗",airStrip,new Vector3(-.61f,3.719f,z),new Vector3(.61f,3.719f,z),
                    new Vector3(.61f,3.78f,z+1.4925f),new Vector3(-.61f,3.78f,z+1.4925f),tinted);
            }
            DressSampleGlasshouse(greenhouse,halfWidth,halfLength);
            SampleStone(greenhouse,"平齐门槛",new Vector3(0,.059f,halfLength+.12f),new Vector3(2.14f,.10f,.55f),31,limestone);
        }

        void SampleGlassPane(string name,Transform parent,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material mat)
        {
            MeshObject(name,parent,new List<Vector3>{a,b,c,d},new List<int>{0,1,2,0,2,3},mat);
        }

        void FitWheelRing(Transform root,float radius,float band,float depth)
        {
            var gold=new Material(brass) { name="水轮铜衬与铆头" };
            gold.color=new Color(.64f,.42f,.207f);
            materials.Add(gold);
            const int rimSections=72;
            foreach(float side in new[]{-1f,1f})
            {
                var z=.288f*side;
                var points=new List<Vector3>();
                var outer=new List<int>();
                var inside=new List<int>();
                var section=new[]
                {
                    new Vector2(radius-band, z-.5f*depth),
                    new Vector2(radius-band*.31f,z-.59f*depth),
                    new Vector2(radius+.016f,z-.24f*depth),
                    new Vector2(radius+.018f,z+.28f*depth),
                    new Vector2(radius-band*.38f,z+.57f*depth),
                    new Vector2(radius-band,z+.50f*depth),
                };
                for(int i=0;i<rimSections;i++)
                for(int j=0;j<section.Length;j++)
                {
                    float a=i*360f/rimSections;
                    var p=Quaternion.Euler(0,0,a)*Vector3.up*section[j].x;
                    points.Add(p+Vector3.forward*section[j].y);
                    int that=i*section.Length+j;
                    int next=(i+1)%rimSections*section.Length+j;
                    int cut=(j+1)%section.Length-j;
                    var faces=j==1||j==3?inside:outer;
                    faces.AddRange(new[]{that,next,that+cut,next,next+cut,that+cut});
                }
                MeshObject("圆润水轮木箍",root,points,outer,wood);
                MeshObject("铜饰内外圈",root,points,inside,gold);
                for(int i=0;i<12;i++)
                {
                    var pin=Quaternion.Euler(0,0,i*30+6)*Vector3.up*(radius-.065f)+Vector3.forward*(z+.033f*side);
                    var nail=Cylinder(root,"水轮铜色端铆",pin,new Vector3(.062f,.016f,.062f),gold);
                    nail.transform.localRotation=Quaternion.Euler(90,0,0);
                }
            }
        }

        void SampleRetaining(Transform parent, Vector3 start, Vector3 end, float courseHeight, int rows, Material mortar, bool blocksMovement = true)
        {
            var wall=Group("分层错缝挡土砌石",parent);
            var direction=HorizontalDirection(end-start);
            var orientation=Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up));
            var distance=Vector3.Distance(start,end);
            float unit=distance/Mathf.Ceil(distance/.74f);
            var backing=Box(wall,"挡墙砂浆", (start+end)*.5f+Vector3.up*(courseHeight*rows*.5f-.025f),
                new Vector3(distance*.998f,courseHeight*rows-.055f,.41f),mortar,blocksMovement?ObstacleLayer:0);
            backing.transform.localRotation=orientation;
            for(int row=0;row<rows;row++)
            {
                float length=0;
                int index=0;
                while (length<distance-.08f)
                {
                    float blockLength=Mathf.Min(distance-length,index==0 && row%2==1?unit*.57f:unit);
                    var at=start+direction*(length+blockLength*.5f)+Vector3.up*(row*courseHeight+courseHeight*.48f);
                    var offset=Vector3.Cross(direction,Vector3.up)*(.022f*Mathf.Sin(index*7+row*2));
                    SampleStone(wall,"手凿层理石灰岩",at+offset,new Vector3(blockLength-.022f,courseHeight*.925f,.57f+.032f*Mathf.Sin(row+index*5)),
                        row*121+index*7,(index+row*3)%9==0?slate:limestone,orientation);
                    length+=blockLength;
                    index++;
                }
            }
            var cap=Box(wall,"厚石压顶",(start+end)*.5f+Vector3.up*(courseHeight*rows+.016f),
                new Vector3(distance+.09f,.125f,.68f),limestone);
            cap.transform.localRotation=orientation;
        }

        void SampleStone(Transform parent,string name,Vector3 point,Vector3 size,int seed,Material mat,
            Quaternion? orientation=null)
        {
            const int sides=8;
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            var q=orientation??Quaternion.identity;
            var bevel=new[]
            {
                new Vector2(-.5f,-.31f), new Vector2(-.34f,-.5f),new Vector2(.34f,-.5f),new Vector2(.5f,-.31f),
                new Vector2(.5f,.31f),new Vector2(.34f,.5f),new Vector2(-.34f,.5f),new Vector2(-.5f,.31f)
            };
            for(int row=0;row<3;row++)
            for(int i=0;i<sides;i++)
            {
                float scale=row==1?1f:.88f;
                float wobble=1+.066f*Mathf.Sin(seed*.79f+i*6.6f+row);
                var p=new Vector3(bevel[i].x*size.x*scale*wobble,
                    (row==0?-.5f:row==1?.25f:.5f)*size.y+Mathf.Sin(i*14+seed)*size.y*.06f,
                    bevel[i].y*size.z*scale*wobble);
                vertices.Add(point+q*p);
            }
            for(int row=0;row<2;row++)
            for(int i=0;i<sides;i++)
            {
                int a=row*sides+i,b=row*sides+(i+1)%sides;
                triangles.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});
            }
            for(int i=1;i<sides-1;i++)
                triangles.AddRange(new[]{2*sides,2*sides+i+1,2*sides+i});
            MeshObject(name,parent,vertices,triangles,mat);
        }
    }
}
