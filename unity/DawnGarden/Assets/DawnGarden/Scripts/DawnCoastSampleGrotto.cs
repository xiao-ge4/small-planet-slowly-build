using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void BuildGrotto()
        {
            var grotto=Group("04 · 潮洞 · 岩层与灯龛",details);
            grotto.localPosition=SampleGrottoOffset;
            var innerStone=SampleCaveStone("潮洞潮湿内壁",new Color(.43f,.48f,.46f),new Color(.68f,.70f,.63f));
            var outerStone=SampleCaveStone("潮洞风化外壁",new Color(.63f,.64f,.56f),new Color(.88f,.81f,.68f));
            var bandsStone=SampleCaveStone("外壁错层基岩",new Color(.58f,.60f,.54f),new Color(.80f,.76f,.63f));
            const int arc=36;
            var wallSlices=new[]
            {
                new Vector4(10.65f,10.22f,3.54f,3.0f),
                new Vector4(10.36f,11.44f,4.02f,3.1f),
                new Vector4(10.30f,13.11f,4.52f,3.19f),
                new Vector4(10.00f,14.88f,4.48f,3.53f),
                new Vector4(10.28f,16.15f,5.4f,3.19f),
                new Vector4(10.51f,17.31f,6.0f,3.09f)
            };
            const float floor=-.94f;
            var inner=new List<Vector3>();
            var outer=new List<Vector3>();
            var insideFaces=new List<int>();
            var outsideFaces=new List<int>();
            var seamFaces=new List<int>();
            var mapping=new List<Vector2>();
            for(int slice=0;slice<wallSlices.Length;slice++)
            for(int vertex=0;vertex<=arc;vertex++)
            {
                var s=wallSlices[slice];
                float a=Mathf.PI*vertex/arc;
                float wave=1 + .022f*Mathf.Sin(a*5+slice*.43f)+.012f*Mathf.Cos(a*9+slice*.31f);
                float sin=Mathf.Pow(Mathf.Max(0,Mathf.Sin(a)),.76f);
                float cos=Mathf.Sign(Mathf.Cos(a))*Mathf.Pow(Mathf.Abs(Mathf.Cos(a)),.87f);
                float up = Mathf.Lerp(floor,-.43f,Mathf.SmoothStep(0,1,sin*3.3f));
                float zig=.12f*Mathf.Sin(a*5+slice*.56f)*sin;
                var p=new Vector3(s.x+cos*s.z*wave+zig,up+sin*s.w*wave,s.y+.14f*sin*Mathf.Cos(a*3+slice*.48f));
                inner.Add(p);
                float faceThickness=.89f+.34f*Mathf.Sin(a*2.7f+slice*.49f)*Mathf.Sin(a);
                float outerY=p.y+faceThickness*sin;
                float shelfWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.42f,.77f,sin));
                float shelfTop=3.88f+.12f*Mathf.Sin(slice*.91f+a*2.7f)+.05f*Mathf.Cos(a*9);
                outerY=Mathf.Lerp(outerY,Mathf.Max(outerY,shelfTop),shelfWeight);
                outer.Add(new Vector3(s.x+cos*(s.z+1.52f)*wave,outerY,p.z+.22f*sin));
                mapping.Add(new Vector2(p.x*.31f+p.z*.16f,p.y*.73f+p.z*.037f));
                if(slice==0 || vertex==0) continue;
                int count=arc+1;
                int b=slice*count+vertex-1;
                int aPrev=b-count;
                insideFaces.AddRange(new[]{aPrev,b,aPrev+1,aPrev+1,b,b+1});
                outsideFaces.AddRange(new[]{aPrev,aPrev+1,b,aPrev+1,b+1,b});
            }

            // A real inlet opening at the back lets the elevated stream enter below the bridge.
            // Its bottom is below the bed and its roof is above the water; the stream never clips
            // through the solid back wall. A regular mesh around the opening also keeps collision.
            var rear=new List<Vector3>();var rearFaces=new List<int>();
            const int rearCols=36,rearRows=24;
            var allowed=new bool[(rearCols+1)*(rearRows+1)];
            for(int row=0;row<=rearRows;row++)
            for(int col=0;col<=rearCols;col++)
            {
                float x=Mathf.Lerp(5.4f,15.9f,col/(float)rearCols);
                float y=Mathf.Lerp(-1.10f,3.64f,row/(float)rearRows);
                float relative=(x-10.65f)/5.2f;
                float top=-.44f+4.0f*Mathf.Pow(Mathf.Max(0,1-relative*relative),.40f);
                bool inlet=x>8.65f&&x<12.40f&&y>.72f&&y<2.85f;
                int at=rear.Count;rear.Add(new Vector3(x,y,10.16f));
                allowed[at]=y<=top&&!inlet;
                if(row==0||col==0)continue;
                int n=at-rearCols-2;
                if(allowed[n]&&allowed[n+1]&&allowed[at]&&allowed[at-1])
                    rearFaces.AddRange(new[]{n,n+1,at,n,at,at-1});
            }

            var rim=new List<Vector3>();
            var last=(wallSlices.Length-1)*(arc+1);
            for(int i=0;i<=arc;i++)
            {
                rim.Add(inner[last+i]);
                rim.Add(outer[last+i]);
                if(i==0) continue;
                int n=i*2;
                seamFaces.AddRange(new[]{n-2,n-1,n,n,n-1,n+1});
            }
            var innerShell=MeshObject("完整洞顶和内侧岩壁",grotto,inner,insideFaces,innerStone,false,mapping,true);
            // Retain the complete collision vault; its exposed high strip is visually superseded
            // by the real closed rock meshes above it, which have a readable solid underside.
            var visibleWalls=new List<int>();
            for(int i=0;i<insideFaces.Count;i+=3)
                if(Mathf.Max(inner[insideFaces[i]].y,inner[insideFaces[i+1]].y,inner[insideFaces[i+2]].y)<1.68f)
                    visibleWalls.AddRange(new[]{insideFaces[i],insideFaces[i+1],insideFaces[i+2]});
            var wallMesh=new Mesh{name="洞内低壁 · 母岩下方"};
            wallMesh.SetVertices(inner);wallMesh.SetTriangles(visibleWalls,0);wallMesh.SetUVs(0,mapping);
            wallMesh.RecalculateNormals();wallMesh.RecalculateBounds();meshes.Add(wallMesh);
            innerShell.GetComponent<MeshFilter>().sharedMesh=wallMesh;
            MeshObject("完整洞顶的地面外露岩体",grotto,outer,outsideFaces,outerStone,false,mapping,true);
            MeshObject("闭合背墙与后侧封岩",grotto,rear,rearFaces,innerStone,false,null,true);
            MeshObject("有进深的洞口截面",grotto,rim,seamFaces,bandsStone,false,null,true);
            // Seal the complete rear ring and both ground edges using the identical shared
            // vertices. Earlier offsets left a daylight slit above the left cave shoulder.
            var closure=new List<Vector3>();var closedFaces=new List<int>();
            void CloseStrip(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=closure.Count;closure.Add(a);closure.Add(b);closure.Add(c);closure.Add(d);
                closedFaces.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for(int i=0;i<arc;i++) CloseStrip(inner[i],outer[i],outer[i+1],inner[i+1]);
            for(int s=0;s<wallSlices.Length-1;s++)
            foreach(int end in new[]{0,arc})
            {
                int a=s*(arc+1)+end,b=(s+1)*(arc+1)+end;
                CloseStrip(inner[a],inner[b],outer[b],outer[a]);
            }
            MeshObject("闭合洞顶后缘与两侧基脚",grotto,closure,closedFaces,outerStone,false,null,true);

            BuildGrottoGeology(grotto,wallSlices,outerStone,bandsStone);
            BuildGrottoRiverBanks(grotto,outerStone);

            // Keep the arch skin exposed. Rocks brace the opening at water level.
            SampleStone(grotto,"洞口沉积岬基",new Vector3(5.24f,-.61f,16.54f),new Vector3(2.29f,.90f,1.35f),
                68,outerStone,Quaternion.Euler(2,20,-8));
            SampleStone(grotto,"右岸坡脚风化基岩",new Vector3(15.08f,-.65f,16.44f),new Vector3(2.11f,.98f,1.52f),
                109,bandsStone,Quaternion.Euler(7,-26,9));
            SampleStone(grotto,"内壁墩脚",new Vector3(13.78f,-.69f,11.20f),new Vector3(1.60f,.70f,1.57f),
                89,outerStone,Quaternion.Euler(0,17,0));
            for(int i=0;i<4;i++)
            {
                var p=new Vector3(14.76f+i*.48f,-.48f,14.39f+.63f*i);
                SampleStone(grotto,"洞缘出露的板层",p,new Vector3(1.08f,.32f,.79f),74+i,outerStone,Quaternion.Euler(4,i*18,-8));
            }

            var seat=Group("洞内凿岩长座",grotto);
            seat.localPosition=new Vector3(13f,-.36f,13.67f);
            SampleStone(seat,"高于潮水的长座基台",new Vector3(0,-.183f,-.07f),new Vector3(3.17f,.56f,.96f),
                92,outerStone,Quaternion.Euler(0,3,0));
            for(int i=0;i<6;i++)
            {
                float angle=(i-2.5f)*.107f;
                var at=new Vector3(Mathf.Sin(angle)*4.26f,0,Mathf.Cos(angle)*4.26f-4.26f);
                SampleStone(seat,"弧面湿岩靠凳",at+Vector3.up*.47f,new Vector3(.54f,.22f,.69f),i*17,
                    limestone,Quaternion.Euler(0,(i-2)*6,0));
                if(i%2==0)
                    SampleStone(seat,"靠凳石墩",at+Vector3.up*.18f,new Vector3(.47f,.39f,.52f),78+i,wetStone);
            }

            // Large irregular paving stops at the open tidal floor instead of stretching beneath water.
            for(int row=0;row<5;row++)
            for(int col=0;col<7;col++)
            {
                float x=7.53f+col*.71f+(row%2)*.15f;
                float z=13.33f+row*.54f;
                float r=new Vector2((x-9.64f)/3.06f,(z-14.57f)/1.75f).sqrMagnitude;
                var y=GrottoFloor(x,z);
                if(r>1 || y<-.69f || y>-.15f) continue;
                bool dryFootprint=true;
                foreach(float dx in new[]{-.37f,.37f})
                foreach(float dz in new[]{-.28f,.28f})
                {
                    float corner=GrottoFloor(x+dx,z+dz);
                    if(corner<-.60f||corner>-.15f||Mathf.Abs(corner-y)>.10f)dryFootprint=false;
                }
                if(!dryFootprint)continue;
                SampleStone(grotto,"洞内残存石板",new Vector3(x,y-.014f,z),new Vector3(.69f,.071f,.511f),
                    row*131+col*97,(row+col)%3==0?slate:bandsStone);
            }
            SampleWallLight(grotto,new Vector3(14.19f,.53f,14.88f),Quaternion.Euler(0,-53,0));
            SampleWallLight(grotto,new Vector3(7.27f,.38f,13.12f),Quaternion.Euler(0,49,0));
            Lantern(grotto,new Vector3(15.59f,GrottoFloor(15.59f,17.22f),17.22f),true);
            for(int i=0;i<6;i++)
            {
                var x=6.96f+i*.35f;
                var z=17.08f+Mathf.Sin(i*4)*.24f;
                var y=GrottoFloor(x,z);
                if(y>-.68f)
                    SampleStone(grotto,"出洞口碎岩",new Vector3(x,y+.014f,z),new Vector3(.29f,.20f,.23f),54+i,bandsStone);
            }
        }

        void BuildGrottoRiverBanks(Transform grotto,Material parentStone)
        {
            var stone=Paint("洞中跌水的连层湿岩",new Color(.55f,.60f,.57f),.22f,.16f);
            var holder=Group("替代土尖柱的连续岩岸",grotto);
            foreach(float sign in new[]{-1f,1f})
            {
                var vertices=new List<Vector3>();var faces=new List<int>();var uv=new List<Vector2>();
                bool previous=false;int previousStart=0;float distance=0;
                for(int segment=12;segment<=14;segment++)
                {
                    int count=Mathf.CeilToInt(Vector3.Distance(RiverProfile[segment],RiverProfile[segment+1])*6);
                    for(int step=0;step<=count;step++)
                    {
                        if(step==0&&segment>12)continue;
                        float t=step/(float)count;var center=RiverWaterline(segment,t);
                        if(center.z<8.5f||center.z>16.5f){previous=false;continue;}
                        var tangent=HorizontalDirection(RiverProfile[segment+1]-RiverProfile[segment]);
                        var normal=Vector3.Cross(Vector3.up,tangent)*sign;
                        float width=Mathf.Lerp(RiverHalfWidths[segment],RiverHalfWidths[segment+1],t)*1.035f;
                        float seam=.035f*Mathf.Sin(center.z*4.2f+sign);
                        var top=center+normal*(width+.045f);top.y+=.05f;
                        var lip=center+normal*(width+.21f+seam);lip.y+=.09f;
                        var heel=center+normal*(width+.33f+seam);heel.y=-.48f;
                        var toe=center+normal*(width+.61f);toe.y=-.48f;
                        int first=vertices.Count;
                        foreach(var p in new[]{top,lip,heel,toe})
                        {
                            vertices.Add(p-SampleGrottoOffset);
                            uv.Add(new Vector2(distance*.42f,p.y*.43f));
                        }
                        if(previous)
                        for(int row=0;row<3;row++)
                        {
                            int a=previousStart+row,b=first+row;
                            if(sign>0)faces.AddRange(new[]{a,b,a+1,a+1,b,b+1});
                            else faces.AddRange(new[]{a,a+1,b,a+1,b+1,b});
                        }
                        previous=true;previousStart=first;distance+=.17f;
                    }
                }
                MeshObject("带厚石檐与平整基脚的跌水岩岸",holder,vertices,faces,stone,false,uv);
            }
        }

        void BuildGrottoGeology(Transform grotto,Vector4[] slices,Material outerStone,Material bandsStone)
        {
            var mantle=Group("连入河岸的洞顶岩层与落石",grotto);
            int seed=700;
            // The roof is one thick rock shelf authored into the shared outer shell above.
            // Detached pillow-shaped rocks on top made it look like a decorated thin awning.
            BuildGrottoHeroRocks(grotto);
            var green=Paint("洞口小圆叶藤",new Color(.53f,.68f,.29f),.12f,.11f);
            var foliage=new List<Vector3>();var leafFaces=new List<int>();
            void OvalLeaf(Vector3 center,float angle,float scale)
            {
                var rotation=Quaternion.Euler(4,angle,Mathf.Sin(angle)*12);
                foreach(int face in new[]{1,-1})
                {
                    int first=foliage.Count;
                    foliage.Add(center+rotation*new Vector3(0,0,face*.023f*scale));
                    for(int i=0;i<10;i++)
                    {
                        float t=i*Mathf.PI*2/10;
                        foliage.Add(center+rotation*new Vector3(Mathf.Cos(t)*.064f*scale,Mathf.Sin(t)*.089f*scale,0));
                    }
                    for(int i=0;i<10;i++)
                    {
                        int x=first+1+i,y=first+1+(i+1)%10;
                        if(face>0)leafFaces.AddRange(new[]{first,x,y});else leafFaces.AddRange(new[]{first,y,x});
                    }
                }
            }
            for(int strand=0;strand<9;strand++)
            {
                float x=6.0f+strand*.96f;
                float y=3.64f-.66f*Mathf.Pow(Mathf.Abs(x-10.2f)/4.2f,1.3f);
                var before=new Vector3(x,y,18.02f);
                int count=5+strand%5;
                for(int n=0;n<count;n++)
                {
                    var center=new Vector3(x+Mathf.Sin(n*.83f+strand)*.075f,y-n*.125f,18.02f+n*.012f);
                    if(n>0)Beam(mantle,"细藤茎",before,center,.013f,green);
                    OvalLeaf(center+new Vector3(n%2==0?-.048f:.048f,0,0),strand*17+n*23,.71f+(strand%3)*.10f);
                    before=center;
                }
            }
            MeshObject("微凸双面小圆叶串",mantle,foliage,leafFaces,green);
            CombineLandformRocks(mantle);
        }

        void BuildGrottoHeroRocks(Transform grotto)
        {
            var pieces=new[]{
                (new Vector3(4.9f,1.30f,15.8f),new Vector3(3.15f,4.45f,4.0f),new Vector3(3,26,-12)),
                (new Vector3(16.15f,1.40f,15.4f),new Vector3(3.65f,4.65f,4.1f),new Vector3(-4,-19,10)),
                (new Vector3(6.7f,2.65f,16.0f),new Vector3(4.3f,2.05f,3.7f),new Vector3(5,61,-17)),
                (new Vector3(10.6f,3.68f,15.9f),new Vector3(5.35f,1.35f,3.8f),new Vector3(-5,-38,4)),
                (new Vector3(14.15f,2.75f,15.7f),new Vector3(4.10f,2.05f,3.8f),new Vector3(-3,112,14)),
                (new Vector3(5.55f,1.35f,11.7f),new Vector3(3.90f,4.50f,4.8f),new Vector3(7,151,-11)),
                (new Vector3(15.35f,1.45f,11.6f),new Vector3(4.00f,4.65f,4.8f),new Vector3(-6,207,9))
            };
            int built=0;
            foreach(var piece in pieces)
            {
                var model=Asset("moss-boulder",grotto,Vector3.zero,1,0,false);
                if(model==null)continue;
                MakeCoastRockMatte(model);
                model.name="洞檐母岩 · 错层嵌合";
                var renderers=model.GetComponentsInChildren<Renderer>();
                if(renderers.Length==0)continue;
                var bounds=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
                var size=bounds.size;
                model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(
                    piece.Item2.x/Mathf.Max(.01f,size.x),piece.Item2.y/Mathf.Max(.01f,size.y),piece.Item2.z/Mathf.Max(.01f,size.z)));
                model.transform.localRotation=Quaternion.Euler(piece.Item3);
                bounds=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
                model.transform.position+=grotto.TransformPoint(piece.Item1)-bounds.center;
                built++;
            }
            if(built==pieces.Length)
            {
                // Keep the supporting/collision vault, replacing only its conspicuous smooth
                // exterior with interlocking authored rock meshes and their actual surface detail.
                foreach(string name in new[]{"完整洞顶的地面外露岩体","有进深的洞口截面"})
                {
                    var original=grotto.Find(name);
                    if(original!=null&&original.TryGetComponent<MeshRenderer>(out var renderer))renderer.enabled=false;
                }
            }
        }

        static float GrottoFloor(float x,float z) => CoastHeight(x+SampleGrottoOffset.x,z+SampleGrottoOffset.z)-SampleGrottoOffset.y;

        void SampleWallLight(Transform parent,Vector3 position,Quaternion facing)
        {
            var root=Group("嵌墙暖光灯龛",parent);
            root.localPosition=position;
            root.localRotation=facing;
            SampleStone(root,"灯龛檐石",new Vector3(0,.36f,0),new Vector3(.65f,.14f,.43f),47,limestone);
            SampleStone(root,"灯龛深色背板",new Vector3(0,.133f,-.134f),new Vector3(.59f,.47f,.22f),87,wetStone);
            Cylinder(root,"龛灯底托",new Vector3(0,.05f,.135f),new Vector3(.35f,.08f,.31f),brass);
            Cylinder(root,"半透琥珀灯芯",new Vector3(0,.204f,.153f),new Vector3(.24f,.102f,.24f),warmGlass);
            var light=Group("石壁回光",root).gameObject.AddComponent<Light>();
            light.transform.localPosition=new Vector3(0,.204f,.31f);
            light.type=LightType.Point;
            light.color=new Color(1,.63f,.38f);
            light.range=2.4f;
            light.intensity=.34f;
            nightLamps.Add(light);
        }

        Material SampleCaveStone(string name,Color dark,Color pale)
        {
            var shader=Resources.Load<Shader>("Coast/WornStone");
            var mat=new Material(shader!=null?shader:Shader.Find("Universal Render Pipeline/Lit")) { name=name,color=Color.white };
            mat.SetFloat("_Smoothness",.23f);
            mat.SetFloat("_Cull",0);
            const int size=256;
            var tex=new Texture2D(size,size,TextureFormat.RGB24,true) { name=name+" · 层理", wrapMode=TextureWrapMode.Repeat };
            var pixels=new Color32[size*size];
            for (int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float granular=Mathf.PerlinNoise(u*43+3,v*47+7);
                float rock=Mathf.PerlinNoise(u*8+14,v*9+2);
                float hollow=Mathf.PerlinNoise(u*11+2,v*4+4);
                float strata=v*3 + .58f*Mathf.PerlinNoise(u*4+9,v*4+2)+.31f*Mathf.Sin(u*2*Mathf.PI);
                float strip = Mathf.Repeat(strata,1);
                float seam=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.01f,.08f,strip))) * (.36f+rock*.40f);
                float shade=Mathf.Clamp01(.28f+.26f*rock+.16f*granular+.29f*hollow-seam*.13f);
                pixels[y*size+x]=Color.Lerp(dark,pale,shade);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true,true);
            mat.SetTexture("_BaseMap",tex);
            textures.Add(tex);
            materials.Add(mat);
            return mat;
        }
    }
}
