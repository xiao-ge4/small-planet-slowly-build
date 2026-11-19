using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // These rocks describe outcrops of the shared ground, with the road cut out of them.
        // No saved layout coordinates or expansion collider is replaced by this art layer.
        void BuildCoastLandformArt()
        {
            var root=Group("连续海崖 · 地质层与风化露头",details);
            var pale=Paint("海崖受光砂岩",new Color(.80f,.76f,.65f),.11f,.15f);
            var grey=Paint("海崖背光板岩",new Color(.63f,.67f,.65f),.13f,.13f);
            var warm=Paint("含铁风化断面",new Color(.81f,.70f,.55f),.10f,.17f);
            var wet=Paint("潮位浸水礁岩",new Color(.43f,.55f,.53f),.26f,.15f);
            var palette=new[]{pale,grey,warm};
            var rng=new System.Random(281977);
            float Rand(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());

            // A broken, two-level rock coast hides the old smooth sand skirt. Gaps deliberately
            // remain at the jetty, the estuary and the accessible beach, with small rocks only.
            int seed=31;
            for(int edge=0;edge<CoastOutline.Length;edge++)
            {
                Vector2 a=CoastOutline[edge],b=CoastOutline[(edge+1)%CoastOutline.Length];
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/1.75f));
                for(int j=0;j<count;j++)
                {
                    // Alternate headlands with real sandy bays; a continuous necklace of equal
                    // boulders reads as a defensive wall rather than a natural shoreline.
                    if((edge%9>=3&&edge%9<=6)||j%3==2)continue;
                    var p=Vector2.Lerp(a,b,(j+.25f+(float)rng.NextDouble()*.4f)/count);
                    var inward=(new Vector2(10,-15)-p).normalized;
                    if(p.x>43&&p.y>8&&p.y<17 || p.x>1&&p.x<11&&p.y>18) continue;
                    float width=Rand(1.5f,3.5f),height=Rand(.65f,1.8f);
                    var inland=p+inward*Rand(.6f,1.6f);
                    float landY=CoastHeight(inland.x,inland.y);
                    if(!LandformClear(inland.x,inland.y,width*.35f)) continue;
                    float baseY=Mathf.Max(-1.35f,landY-height*.59f);
                    CoastCrag(root,new Vector3(inland.x,baseY+height*.50f,inland.y),
                        new Vector3(width,height,width*Rand(.65f,.91f)),seed++,palette[seed%3],Rand(-.35f,.35f));
                    var sea=p-inward*Rand(.4f,1.3f);
                    CoastCrag(root,new Vector3(sea.x,-.91f,sea.y),new Vector3(width*.82f,Rand(.65f,1.30f),width*.61f),
                        seed++,wet,Rand(-.4f,.4f));
                    if(j%4==0)
                        CoastCrag(root,new Vector3(p.x-inward.x*2.1f,-1.11f,p.y-inward.y*2.1f),
                            new Vector3(width*.51f,.62f,width*.43f),seed++,grey,0);
                }
            }

            // Articulate the faces of the large massifs instead of stretching one material over
            // their heightfield. Every cluster has a big parent rock and smaller broken strata.
            var ridges=new[]{
                new Vector4(-1.5f,-36.5f,15,11),new Vector4(12,-28.5f,9.5f,12),
                new Vector4(-30.2f,-35.6f,7.6f,9.4f),new Vector4(-8.8f,-19.9f,6.6f,9.4f),
                new Vector4(41,-15,7.7f,10),new Vector4(33.5f,5.1f,6.7f,4.7f)
            };
            foreach(var mound in ridges)
            for(int ring=0;ring<2;ring++)
            {
                float radius=.52f+ring*.27f;
                // Long exposed bands are joined into the hill. Small boulders only punctuate
                // the ends of this bedrock, avoiding a hillside covered in separate marbles.
                CoastCliffBand(root,mound,radius,ring,pale,grey);
                int count=9;
                for(int j=0;j<count;j++)
                {
                    if(j%3!=1)continue;
                    float angle=j*Mathf.PI*2/count+ring*.41f;
                    float x=mound.x+Mathf.Cos(angle)*mound.z*radius;
                    float z=mound.y+Mathf.Sin(angle)*mound.w*radius;
                    float width=Rand(1.3f,2.5f);
                    if(!LandformClear(x,z,width*.48f)) continue;
                    float groundY=CoastHeight(x,z);
                    float height=Rand(.6f,1.5f);
                    CoastCrag(root,new Vector3(x,groundY+height*.19f-.15f,z),
                        new Vector3(width,height,width*Rand(.67f,.91f)),seed++,palette[seed%3],Rand(-.20f,.20f));
                    if(j%3==1)
                    {
                        float dx=Mathf.Cos(angle)*width*.53f,dz=Mathf.Sin(angle)*width*.53f;
                        if(LandformClear(x+dx,z+dz,width*.22f))
                            CoastCrag(root,new Vector3(x+dx,CoastHeight(x+dx,z+dz)+.21f,z+dz),
                                new Vector3(width*.70f,.86f,width*.55f),seed++,pale,Rand(-.25f,.25f));
                    }
                }
            }

            // The garden and observatory sit on stone shoulders. Avoid the flat usable core and
            // stair access; their outside face is a stack of geology rather than a green mound.
            foreach(int index in new[]{1,2,5})
            {
                var pad=CoastPads[index];
                for(int j=0;j<32;j++)
                {
                    float a=j*Mathf.PI*2/32;
                    float x=pad.Center.x+Mathf.Cos(a)*(pad.HalfSize.x+1.48f);
                    float z=pad.Center.z+Mathf.Sin(a)*(pad.HalfSize.y+1.48f);
                    if(!LandformClear(x,z,1.1f))continue;
                    float top=pad.Center.y-.2f,bottom=CoastHeight(x,z)-.55f;
                    float height=Mathf.Max(.9f,top-bottom);
                    CoastCrag(root,new Vector3(x,bottom+height*.43f,z),new Vector3(Rand(2.2f,3.6f),height+Rand(.2f,.4f),Rand(1.4f,2.0f)),
                        seed++,palette[j%3],Rand(-.12f,.12f));
                }
            }
            CombineLandformRocks(root);
            var hero=Group("近岸带苔风化主岩",details);
            var points=new[]{new Vector2(-14,5),new Vector2(-26,-15),new Vector2(-33,-28),
                new Vector2(-28,-39),new Vector2(46,-32),new Vector2(48,-17),new Vector2(46,3),
                new Vector2(36,20),new Vector2(24,22),new Vector2(16,22),new Vector2(-4,22)};
            for(int i=0;i<points.Length;i++)
            {
                var p=points[i];
                if(!LandformClear(p.x,p.y,.85f))continue;
                var model=Asset("moss-boulder",hero,new Vector3(p.x,CoastHeight(p.x,p.y)-.35f,p.y),.87f+(i%3)*.19f,i*47,false);
                MakeCoastRockMatte(model);
                if(model!=null)model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(1.6f,.87f,1.14f));
            }
            BuildGrottoCliffShoulders(pale,grey);
            BuildCoastGeologyFinish();
        }

        void BuildGrottoCliffShoulders(Material pale,Material grey)
        {
            var root=Group("潮洞两翼连续岸肩",details);
            var shoulders=new[]{
                new[]{new Vector3(-1,0,14),new Vector3(4,0,14),new Vector3(8,0,12.6f),new Vector3(10.4f,0,10.7f)},
                new[]{new Vector3(28,0,9.6f),new Vector3(32,0,8),new Vector3(35,0,4.9f),new Vector3(36.4f,0,3.1f)}
            };
            foreach(var points in shoulders)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();var mapping=new List<Vector2>();
                var offsets=new[]{1.72f,2.12f,2.8f,3.65f,4.6f,5.7f};
                int count=0;
                for(int segment=0;segment<points.Length-1;segment++)
                {
                    int steps=Mathf.CeilToInt(Vector3.Distance(points[segment],points[segment+1])/.44f);
                    for(int i=0;i<=steps;i++)
                    {
                        if(segment>0&&i==0)continue;
                        var crest=Vector3.Lerp(points[segment],points[segment+1],i/(float)steps);
                        for(int row=0;row<offsets.Length;row++)
                        {
                            var p=crest+Vector3.forward*(offsets[row]+.075f*Mathf.Sin(crest.x*1.7f+row));
                            p.y=CoastHeight(p.x,p.z)+.22f+.022f*Mathf.Sin(crest.x*3.7f+row*1.3f);
                            vertices.Add(p);mapping.Add(new Vector2(crest.x*.27f,p.y*.34f+row*.06f));
                            if(count==0||row==0)continue;
                            int at=count*offsets.Length+row;
                            triangles.AddRange(new[]{at-offsets.Length-1,at,at-1,at-offsets.Length-1,at-offsets.Length,at});
                        }
                        count++;
                    }
                }
                // Seal all four sides of the replacement shell. The lower toe extends beyond
                // the removed source triangles and is buried in intact ground, so an oblique
                // camera can never see the ocean through a floating lip at the rock's foot.
                var perimeter=new List<int>();
                for(int column=0;column<count;column++)perimeter.Add(column*offsets.Length);
                for(int row=1;row<offsets.Length;row++)perimeter.Add((count-1)*offsets.Length+row);
                for(int column=count-2;column>=0;column--)perimeter.Add(column*offsets.Length+offsets.Length-1);
                for(int row=offsets.Length-2;row>0;row--)perimeter.Add(row);
                var toes=new List<Vector3>();
                float baseY=float.PositiveInfinity;
                for(int edge=0;edge<perimeter.Count;edge++)
                {
                    var previous=vertices[perimeter[(edge+perimeter.Count-1)%perimeter.Count]];
                    var next=vertices[perimeter[(edge+1)%perimeter.Count]];
                    var tangent=next-previous;tangent.y=0;tangent.Normalize();
                    var toe=vertices[perimeter[edge]]+Vector3.Cross(Vector3.up,tangent)*.62f;
                    toe.y=CoastHeight(toe.x,toe.z)-.20f;
                    baseY=Mathf.Min(baseY,toe.y-.75f);toes.Add(toe);
                }
                void RockFace(Vector3 a,Vector3 b,Vector3 c)
                {
                    int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                    triangles.AddRange(new[]{first,first+1,first+2});
                    foreach(var p in new[]{a,b,c})mapping.Add(new Vector2(p.x*.27f+p.z*.17f,p.y*.34f));
                }
                Vector3 bottomCenter=Vector3.zero;
                foreach(var p in toes)bottomCenter+=p;
                bottomCenter/=toes.Count;bottomCenter.y=baseY;
                for(int edge=0;edge<perimeter.Count;edge++)
                {
                    int next=(edge+1)%perimeter.Count;
                    var a=vertices[perimeter[edge]];var b=vertices[perimeter[next]];
                    var ta=toes[edge];var tb=toes[next];
                    var ba=ta;var bb=tb;ba.y=bb.y=baseY;
                    RockFace(a,b,ta);RockFace(b,tb,ta);
                    RockFace(ta,tb,ba);RockFace(tb,bb,ba);
                    RockFace(bottomCenter,ba,bb);
                }
                var face=MeshObject("接入陆地的分层岩壁",root,vertices,triangles,pale,false,mapping);
                // Both normals are authored independently so the backlit rock does not turn black.
                face.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.TwoSided;
            }
            foreach(var p in new[]{new Vector2(4.2f,17.3f),new Vector2(8.0f,16.0f),new Vector2(29.1f,13.6f),new Vector2(34.2f,8.2f)})
            {
                var model=Asset("moss-boulder",root,new Vector3(p.x,CoastHeight(p.x,p.y)-.25f,p.y),1.04f,p.x*9,false);
                MakeCoastRockMatte(model);
                if(model!=null)model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(1.8f,1.15f,.9f));
            }
        }

        static readonly Vector2[][] CliffShoulderCoverage =
        {
            new[]{new Vector2(-1,14),new Vector2(4,14),new Vector2(8,12.6f),new Vector2(10.4f,10.7f)},
            new[]{new Vector2(28,9.6f),new Vector2(32,8),new Vector2(35,4.9f),new Vector2(36.4f,3.1f)}
        };

        static bool ReplacedByCoastCliffShoulder(Vector3 point)
        {
            // This is the interior of the exact two authored rock strips above. Their source
            // heightfield render faces are removed (collision is retained), so differently
            // sampled meshes cannot cut through each other as brown triangular patches.
            foreach(var line in CliffShoulderCoverage)
            {
                if(point.x<line[0].x+.20f||point.x>line[line.Length-1].x-.20f)continue;
                for(int segment=0;segment<line.Length-1;segment++)
                {
                    var a=line[segment];var b=line[segment+1];
                    if(point.x<a.x||point.x>b.x)continue;
                    float t=(point.x-a.x)/(b.x-a.x);
                    float offset=point.z-Mathf.Lerp(a.y,b.y,t);
                    if(offset>1.90f&&offset<5.48f)return true;
                }
            }
            return false;
        }

        static bool LandformClear(float x,float z,float clearance)
        {
            // Keep the original small garden, the complete expansion plot and its seam intact.
            if(x>-7.6f-clearance&&x<19.0f+clearance&&z>-6.1f-clearance&&z<7.1f+clearance) return false;
            var p=new Vector2(x,z);
            foreach(var road in CoastTracks)
            for(int j=0;j<road.Points.Length-1;j++)
                if(SegmentDistanceSquared(p,road.Points[j],road.Points[j+1],out _) <
                   Mathf.Pow(road.HalfWidth+clearance+.70f,2))return false;
            // No artificial boulders on top of the authored horticultural terraces.
            foreach(var pad in CoastPads)
                if(Mathf.Abs(x-pad.Center.x)<pad.HalfSize.x+.20f&&Mathf.Abs(z-pad.Center.z)<pad.HalfSize.y+.20f)return false;
            for(int j=0;j<RiverProfile.Length-1;j++)
            {
                float d=SegmentDistanceSquared(p,RiverProfile[j],RiverProfile[j+1],out float t);
                float width=Mathf.Lerp(RiverHalfWidths[j],RiverHalfWidths[j+1],t);
                if(d<Mathf.Pow(width+clearance+.3f,2))return false;
            }
            return true;
        }

        void CoastCliffBand(Transform parent,Vector4 mound,float radius,int stratum,Material face,Material shadow)
        {
            const int steps=96,rows=4;
            var vertices=new List<Vector3>();var triangles=new List<int>();var mapping=new List<Vector2>();
            var valid=new bool[steps+1];
            for(int i=0;i<=steps;i++)
            {
                float a=i*Mathf.PI*2/steps;
                var direction=new Vector2(Mathf.Cos(a)*mound.z,Mathf.Sin(a)*mound.w);
                float drift=.025f*Mathf.Sin(a*7+stratum)+.014f*Mathf.Sin(a*13+.7f);
                var inner=new Vector2(mound.x,mound.y)+direction*(radius-.15f+drift);
                var lip=new Vector2(mound.x,mound.y)+direction*(radius+.025f+drift);
                var outer=new Vector2(mound.x,mound.y)+direction*(radius+.10f+drift);
                float top=CoastHeight(inner.x,inner.y)+.12f;
                float bottom=CoastHeight(outer.x,outer.y)-.27f;
                valid[i]=top-bottom>.50f&&LandformClear(lip.x,lip.y,.8f)&&LandformClear(inner.x,inner.y,.5f);
                float notches=.13f*Mathf.Sin(a*21+stratum)+.08f*Mathf.Sin(a*39);
                vertices.Add(new Vector3(outer.x,bottom,outer.y));
                vertices.Add(new Vector3(lip.x,Mathf.Lerp(bottom,top,.67f)+notches,lip.y));
                vertices.Add(new Vector3(lip.x-direction.x*.023f,top-.04f+notches*.45f,lip.y-direction.y*.023f));
                vertices.Add(new Vector3(inner.x,top-.08f,inner.y));
                for(int row=0;row<rows;row++)mapping.Add(new Vector2(i*.35f,row*.57f));
                if(i==0||!valid[i]||!valid[i-1])continue;
                for(int row=0;row<rows-1;row++)
                {
                    int n=(i-1)*rows+row;
                    triangles.AddRange(new[]{n,n+1,n+rows,n+rows,n+1,n+rows+1});
                }
            }
            MeshObject("与山体连成一体的层状岩面",parent,vertices,triangles,stratum%2==0?face:shadow,false,mapping);
        }

        void BuildCoastStairFlight()
        {
            var route=SampleCaveSteps;
            var root=Group("桥头至潮洞 · 石砌踏步与挡墙",ground);
            var stone=Paint("下洞阶梯 · 暖白凿石",new Color(.78f,.72f,.59f),.13f,.16f);
            var wall=Paint("阶梯挡墙 · 石灰岩",new Color(.60f,.60f,.52f),.10f,.17f);
            for(int segment=0;segment<route.Length-1;segment++)
            {
                var a=route[segment];var b=route[segment+1];
                var delta=b-a;delta.y=0;
                var direction=delta.normalized;var side=Vector3.Cross(Vector3.up,direction);
                float length=delta.magnitude;
                int count=Mathf.CeilToInt(Mathf.Abs(a.y-b.y)/.17f);
                for(int step=0;step<count;step++)
                {
                    float t=(step+.5f)/count;
                    var p=Vector3.Lerp(a,b,t);
                    float top=Mathf.Lerp(a.y,b.y,step/(float)count)+.055f;
                    p.y=top-.13f;
                    var tread=Box(root,"完整水平石踏步",p,new Vector3(2.82f,.26f,length/count+.035f),stone);
                    tread.transform.localRotation=Quaternion.LookRotation(direction);
                    foreach(float sign in new[]{-1f,1f})
                    {
                        var rim=p+side*(sign*1.48f);rim.y=top+.61f;
                        var coping=Box(root,"踏步侧缘厚压石",rim,new Vector3(.22f,.15f,length/count*.98f),stone);
                        coping.transform.localRotation=tread.transform.localRotation;
                    }
                }
                var rampA=a+Vector3.up*.075f;var rampB=b+Vector3.up*.075f;
                var support=new List<Vector3>{rampA-side*1.40f,rampB-side*1.40f,rampB+side*1.40f,rampA+side*1.40f};
                var slab=MeshObject("阶梯内部连续行走支撑",root,support,new List<int>{0,1,2,0,2,3},stone,true);
                slab.GetComponent<MeshRenderer>().enabled=false;
                foreach(float sign in new[]{-1f,1f})
                {
                    var start=a+side*(sign*1.49f);var end=b+side*(sign*1.49f);
                    start.y+=.62f;end.y+=.62f;
                    var baseStart=start;var baseEnd=end;
                    baseStart.y=Mathf.Min(CoastHeight(start.x,start.z),start.y)-.5f;
                    baseEnd.y=Mathf.Min(CoastHeight(end.x,end.z),end.y)-.5f;
                    var width=side*.13f;
                    var vertices=new List<Vector3>{start-width,end-width,baseEnd-width,baseStart-width,
                        start+width,end+width,baseEnd+width,baseStart+width};
                    MeshObject("阻止斜切入阶的实体栏墙",root,vertices,new List<int>{0,1,2,0,2,3,6,5,4,7,6,4,
                        0,4,5,0,5,1,3,2,6,3,6,7,0,3,7,0,7,4,1,5,6,1,6,2},wall,false,null,true);
                }
            }
        }

        void CoastCrag(Transform root,Vector3 center,Vector3 size,int seed,Material material,float lean)
        {
            // Shared bevel normals keep worn corners without turning every triangle into a dark facet.
            const int sides=10,bands=4;
            var rings=new Vector3[sides*bands];
            for(int band=0;band<bands;band++)
            for(int j=0;j<sides;j++)
            {
                float angle=j*Mathf.PI*2/sides+seed*.618f;
                float radius=(band==0?.79f:band==1?1.0f:band==2?.97f:.70f)*(.94f+.07f*Mathf.Sin(j*8.1f+seed));
                float y=(band==0?-.5f:band==1?-.11f:band==2?.29f:.5f)+Mathf.Sin(seed+j*4.91f)*.07f;
                rings[band*sides+j]=center+new Vector3((Mathf.Cos(angle)*radius*.5f+lean*y)*size.x,
                    y*size.y,Mathf.Sin(angle)*radius*.5f*size.z);
            }
            var vertices=new List<Vector3>(rings);var tris=new List<int>();var uv=new List<Vector2>();
            foreach(var p in rings)uv.Add(new Vector2(p.x*.37f+p.z*.23f,p.y*.43f+p.z*.17f));
            for(int band=0;band<bands-1;band++)
            for(int j=0;j<sides;j++)
            {
                int a=band*sides+j,b=band*sides+(j+1)%sides;
                tris.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});
            }
            int cap=(bands-1)*sides;
            for(int j=1;j<sides-1;j++)tris.AddRange(new[]{cap,cap+j+1,cap+j});
            MeshObject("断层基岩",root,vertices,tris,material,false,uv);
        }

        void CombineLandformRocks(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null)continue;
                var mat=renderer.sharedMaterial;
                if(!groups.TryGetValue(mat,out var list))groups[mat]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh{name="合批海岸岩层",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                var combined=Group(pair.Key.name,root).gameObject;
                combined.AddComponent<MeshFilter>().sharedMesh=mesh;
                combined.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
    }
}
