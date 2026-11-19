using System;
using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // This is a finish over the surveyed terrain. It deliberately owns no colliders:
        // walking, saved land and path clearances remain in the underlying land system.
        void BuildReferenceTerraceArt()
        {
            var root=Group("参考地图 · 阶地砌筑与土层",details);
            var stone=new[]{
                Paint("阶地暖白石",new Color(.83f,.78f,.66f),.09f,.16f),
                Paint("阶地旧石灰岩",new Color(.74f,.73f,.64f),.10f,.18f),
                Paint("阶地淡蜜石",new Color(.89f,.81f,.66f),.10f,.15f)
            };
            var mortar=Paint("阶地灰土石缝",new Color(.48f,.49f,.42f),.03f,.12f);
            var dirt=Paint("阶地土壤断面",new Color(.35f,.27f,.17f),.04f,.24f);
            var terra=new[]{
                Paint("庭院旧砖浅",new Color(.86f,.61f,.43f),.09f,.15f),
                Paint("庭院旧砖暖",new Color(.91f,.66f,.47f),.10f,.16f),
                Paint("庭院旧砖灰",new Color(.82f,.58f,.43f),.08f,.17f)
            };
            var batches=new ReferenceMasonryBatches(this,root,stone);

            // Four connected retaining levels. The terrace outline follows the survey;
            // staggered joints vary by course, while the long masonry face remains continuous.
            ReferenceTerraceWall(root,batches,mortar,dirt,new(-11,2.55f,-11),new(7.8f,2.4f),1.05f,111);
            ReferenceTerraceWall(root,batches,mortar,dirt,new(-11,3.45f,-6),new(7.8f,2.6f),1.12f,117);
            ReferenceTerraceWall(root,batches,mortar,dirt,new(-11,4.25f,-.65f),new(7.5f,2.6f),1.02f,123);
            ReferenceTerraceWall(root,batches,mortar,dirt,ReferenceGreenhousePad,ReferenceGreenhousePadHalf,1.17f,129);

            // Low returns support the brick forecourt and the sand reserve. They share
            // the terrain elevation and stop at every real path and editable-land boundary.
            ReferenceTerraceWall(root,batches,mortar,dirt,ReferenceBrickCourt,ReferenceBrickCourtHalf,1.35f,211,false);
            ReferenceTerraceWall(root,batches,mortar,dirt,ReferenceWhiteCourt,ReferenceWhiteCourtHalf,.55f,219,false);
            var sandRim=new Vector3[11];
            for(int i=0;i<sandRim.Length;i++)sandRim[i]=new Vector3(ReferenceReserveEdge[i].x,ReferenceReservedPad.y,ReferenceReserveEdge[i].y);
            ReferenceTerraceOutlineWall(root,batches,mortar,dirt,sandRim,ReferenceReservedPad.y,2.95f,227,false);
            BuildReferenceObservatoryPlinth(root,mortar,dirt);
            ReferenceStairFlight(batches,new(-17.8f,2.55f,-11.5f),new(-19.8f,3.45f,-6.5f),1.95f,301);
            ReferenceStairFlight(batches,new(-19.8f,3.45f,-6.5f),new(-20,4.25f,-.5f),1.95f,307);
            ReferenceStairFlight(batches,new(-20,4.25f,-.5f),new(-21.6f,5.2f,5.1f),1.95f,313);
            ReferenceStairFlight(batches,new(24,-.4f,-9),new(27.4f,.7f,-8.1f),2.1f,331);
            ReferenceStairFlight(batches,ReferenceReserveStairs[1],ReferenceReserveStairs[2],2.5f,337);
            ReferenceStairFlight(batches,ReferenceReserveStairs[2],ReferenceReserveStairs[3],2.5f,343);
            ReferenceStairFlight(batches,new(14.8f,.7f,-19.9f),new(16,-.8f,-23.5f),2.3f,361);
            ReferenceStairFlight(batches,new(16,-.8f,-23.5f),new(14.5f,-2.6f,-27.8f),2.3f,367);
            ReferenceStairFlight(batches,new(28,-.9f,-11.9f),new(26,-1.9f,-13.3f),2.4f,373);
            ReferenceStairFlight(batches,new(26,-1.9f,-13.3f),ReferencePierLanding,2.4f,379);
            batches.Build();
            ReferenceCourtPavers(root,terra);
            // The original editable plot and its stone apron now use the same
            // world-space surface layer, supplied by the world assembler.
            BuildReferenceBankStrata(root,stone);
            ReferenceForeshoreStair(root,ReferenceForeshoreWalk[4],ReferenceForeshoreWalk[5],2.0f,stone[0]);
            ReferenceForeshoreStair(root,ReferenceForeshoreWalk[5],ReferenceForeshoreWalk[6],2.0f,stone[1]);
            ReferenceForeshoreStair(root,ReferenceObservatoryStairs[3],ReferenceObservatoryStairs[2],2.4f,stone[0],"上院通往观星台的实体石阶");
            BuildReferenceRidgePathArt(root,stone);
            BuildReferenceReserveBoundary(root,stone);
        }

        void BuildReferenceReserveBoundary(Transform parent,Material[] stone)
        {
            var root=Group("沙地上院 · 低石栏与开放入口",parent);
            var masonry=new ReferenceMasonryBatches(this,root,stone);
            int serial=2910,posts=0;
            bool Clear(Vector3 p,float extra)
            {
                foreach(var road in ReferenceRoads)for(int k=0;k<road.points.Length-1;k++)
                    if(SegmentDistanceSquared(new Vector2(p.x,p.z),road.points[k],road.points[k+1],out _) <
                        Mathf.Pow(road.width+.46f+extra,2))return false;
                return Mathf.Abs(ReferenceHeight(p.x,p.z)-ReferenceReservedPad.y)<.15f;
            }
            for(int edge=0;edge<ReferenceReserveEdge.Length;edge++)
            {
                var a=ReferenceReserveEdge[edge];var b=ReferenceReserveEdge[(edge+1)%ReferenceReserveEdge.Length];
                var direction=(b-a).normalized;
                var inward=(new Vector2(ReferenceReservedPad.x,ReferenceReservedPad.z)-(a+b)*.5f).normalized*.25f;
                int intervals=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/2.15f));
                Vector3? previous=null;
                for(int j=0;j<=intervals;j++)
                {
                    var q=Vector2.Lerp(a,b,j/(float)intervals)+inward;
                    var p=new Vector3(q.x,ReferenceReservedPad.y,q.y);
                    if(!Clear(p,.23f)){previous=null;continue;}
                    var along=new Vector3(direction.x,0,direction.y);
                    masonry.Stone(p+Vector3.up*.11f,along,.45f,.22f,.45f,serial++);
                    masonry.Stone(p+Vector3.up*.40f,along,.29f,.46f,.29f,serial++);
                    masonry.Stone(p+Vector3.up*.68f,along,.44f,.12f,.44f,serial++);
                    ReferenceObstacle(root,p+Vector3.up*.37f,new Vector3(.42f,.74f,.42f));posts++;
                    if(previous.HasValue)
                    {
                        var from=previous.Value;var middle=(from+p)*.5f;
                        if(Clear(middle,.16f)&&Clear(Vector3.Lerp(from,p,.25f),.16f)&&Clear(Vector3.Lerp(from,p,.75f),.16f))
                        {
                            var run=(p-from);float length=run.magnitude-.32f;
                            masonry.Stone(middle+Vector3.up*.37f,run,length,.18f,.20f,serial++);
                            var boundary=Group("低石栏实体边界",root);boundary.position=middle+Vector3.up*.37f;
                            boundary.rotation=Quaternion.LookRotation(run.normalized);boundary.gameObject.layer=ObstacleLayer;
                            boundary.gameObject.AddComponent<BoxCollider>().size=new Vector3(.20f,.18f,length);
                        }
                    }
                    previous=p;
                }
            }
            masonry.Build();
            Debug.Log($"Reference upper court boundary: {posts} stone posts; stairs and route mouths kept open.");
        }

        void BuildReferenceObservatoryPlinth(Transform parent,Material joints,Material earth)
        {
            var root=Group("观星圆台 · 厚石基与低岩花园",parent);
            var palette=new[]{
                Paint("观星承台浅灰石",new Color(.73f,.74f,.70f),.09f,.16f),
                Paint("观星承台暖灰石",new Color(.80f,.78f,.71f),.09f,.17f),
                Paint("观星承台风化石",new Color(.65f,.69f,.68f),.08f,.18f)
            };
            var masonry=new ReferenceMasonryBatches(this,root,palette);
            // The masonry follows the real ellipse and closes around both visible
            // shoulders. Its outside ground is low, so all five lower courses remain
            // exposed; circulation openings still use the exact road exclusion rule.
            var rim=new Vector3[43];
            for(int i=0;i<rim.Length;i++)
            {
                float angle=Mathf.Lerp(Mathf.PI-.20f,Mathf.PI*2+.28f,i/(float)(rim.Length-1));
                rim[i]=ReferenceObservatory+new Vector3(Mathf.Cos(angle)*ReferenceObservatoryRimHalf.x,0,
                    Mathf.Sin(angle)*ReferenceObservatoryRimHalf.y);
            }
            ReferenceTerraceOutlineWall(root,masonry,joints,earth,rim,ReferenceObservatory.y,
                ReferenceObservatory.y-ReferenceObservatoryPlinthFoot,241,false);
            masonry.Build();

            // Low interlocking rock feet break the wide approach into a planted
            // garden, not an empty green ramp. Every solid rock owns its real mesh
            // collision and stays clear of both complete-width approach corridors.
            var toes=new[]{new Vector2(21.5f,29.8f),new Vector2(26.8f,30.6f),
                new Vector2(30.2f,34.4f),new Vector2(29.7f,39.5f),new Vector2(23.4f,26.0f)};
            for(int i=0;i<toes.Length;i++)
            {
                var p=toes[i];
                if(!ReferenceObservatoryGardenClear(p,2.15f))continue;
                float groundY=ReferenceHeight(p.x,p.y);
                var rock=ReferenceAsset("Geology/cliff_bedding",new Vector3(p.x,groundY-.52f,p.y),158+i*23);
                if(rock==null)continue;
                rock.transform.SetParent(root,true);
                rock.transform.localScale=new Vector3(.46f+.035f*(i%3),.20f+.02f*(i%2),.64f);
                SetReferenceRockMap(rock,"cliff_bedding");ReferenceSolidModel(rock,false);
                rock.name="圆台山脚 · 嵌入低岩层 "+i;
            }
            // Flower planting belongs to the world planting pass; these solid toes
            // expose their real bounds so the adjacent flower bands can avoid them.

        }

        static bool ReferenceObservatoryGardenClear(Vector2 point,float radius)
        {
            foreach(var road in ReferenceRoads)for(int i=0;i<road.points.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,road.points[i],road.points[i+1],out _);
                float clearance=road.width+.38f+radius;
                if(square<clearance*clearance)return false;
            }
            return true;
        }

        void BuildReferenceRidgePathArt(Transform parent,Material[] stone)
        {
            // The earthy stone paving is part of the terrain shader. These scattered
            // shallow curb stones clarify the S silhouette without another retaining
            // wall, raised road ribbon or independent collision above the actual land.
            var edging=new ReferenceMasonryBatches(this,parent,stone);
            int seed=1701;
            for(int segment=0;segment<ReferenceRidgePath.Length-1;segment++)
            {
                var from=ReferenceRidgePath[segment];var to=ReferenceRidgePath[segment+1];
                var forward=to-from;forward.y=0;float run=forward.magnitude;forward.Normalize();
                var side=Vector3.Cross(Vector3.up,forward);
                for(float distance=.45f;distance<run-.35f;distance+=1.17f)
                {
                    var at=Vector3.Lerp(from,to,distance/run);
                    if(at.z>ReferenceObservatory.z-5.8f)continue;
                    // Leave the saved west entrance and natural crossing unobstructed.
                    if(Mathf.Abs(at.z)<1.60f)continue;
                    foreach(float sign in new[]{-1f,1f})
                    {
                        if((seed+segment)%5==0){seed++;continue;}
                        float inset=1.34f+.065f*Mathf.Sin(seed*1.71f);
                        var p=at+side*(sign*inset);
                        if(p.x>=5.25f&&p.x<=18.35f&&p.z>=-5.65f&&p.z<=5.65f)continue;
                        float h=ReferenceHeight(p.x,p.z);
                        if(Mathf.Abs(h-at.y)>.5f)continue;
                        p.y=h+.045f;
                        edging.Stone(p,forward,.67f+.16f*Mathf.Sin(seed*2.13f),.13f,.25f,seed++);
                    }
                }
                // The outer (eastern) edge meets existing earth and the reserve court.
                // Avoid the inner rock shoulder so foliage cannot be buried in the FBX.
                for(float distance=.75f;distance<run-.4f;distance+=2.25f)
                {
                    var at=Vector3.Lerp(from,to,distance/run)+side*1.9f;
                    if(at.z<8||at.x<7.45f||ReferenceRidgePathContains(at.x,at.z,.42f))continue;
                    float y=ReferenceHeight(at.x,at.z);
                    var plant=ReferenceAsset("Geology/flowers_"+(seed%3==0?"lavender":seed%2==0?"cream":"gold"),
                        new Vector3(at.x,y-.04f,at.z),seed*29);
                    if(plant!=null)plant.transform.localScale=Vector3.one*(.61f+.04f*(seed%3));
                    seed++;
                }
            }
            edging.Build();
        }

        void ReferenceForeshoreStair(Transform parent,Vector3 from,Vector3 to,float width,Material material,string label="小院通往低岸的实体石阶")
        {
            // One closed stair volume, embedded into the surveyed ramp. Every tread
            // is real visible collision geometry; risers stay below the existing step
            // allowance instead of increasing character or nav settings to get across.
            var forward=to-from;forward.y=0;forward.Normalize();
            var side=Vector3.Cross(Vector3.up,forward)*(width*.5f);
            int count=Mathf.CeilToInt(Mathf.Abs(to.y-from.y)/.135f);
            var vertices=new List<Vector3>();var faces=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {int k=vertices.Count;vertices.AddRange(new[]{a,b,c,d});faces.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
            for(int i=0;i<count;i++)
            {
                var a=Vector3.Lerp(from,to,i/(float)count);var b=Vector3.Lerp(from,to,(i+1)/(float)count);
                float top=a.y+.025f;var al=new Vector3(a.x,top,a.z)-side;var ar=al+side*2;
                var bl=new Vector3(b.x,top,b.z)-side;var br=bl+side*2;
                float footA=a.y-.40f,footB=b.y-.40f;
                var dal=new Vector3(al.x,footA,al.z);var dar=new Vector3(ar.x,footA,ar.z);
                var dbl=new Vector3(bl.x,footB,bl.z);var dbr=new Vector3(br.x,footB,br.z);
                Quad(al,bl,br,ar);Quad(dal,dar,dbr,dbl);
                Quad(al,dal,dbl,bl);Quad(ar,br,dbr,dar);
                float nextTop=b.y+.025f;
                if(i<count-1)Quad(bl,new Vector3(bl.x,nextTop,bl.z),new Vector3(br.x,nextTop,br.z),br);
                else Quad(bl,dbl,dbr,br);
                if(i==0)Quad(al,ar,dar,dal);
            }
            MeshObject(label,ground,vertices,faces,material,true);
        }

        sealed class ReferenceMasonryBatches
        {
            readonly DawnCoast owner;
            readonly Transform parent;
            readonly Material[] palette;
            readonly List<Vector3>[] vertices;
            readonly List<int>[] indices;
            public ReferenceMasonryBatches(DawnCoast o,Transform p,Material[] m)
            {
                owner=o;parent=p;palette=m;
                vertices=new List<Vector3>[m.Length];indices=new List<int>[m.Length];
                for(int i=0;i<m.Length;i++){vertices[i]=new List<Vector3>();indices[i]=new List<int>();}
            }
            public void Stone(Vector3 center,Vector3 along,float length,float height,float depth,int seed)
            {
                int bucket=Mathf.Abs(seed)%palette.Length;
                var v=vertices[bucket];var f=indices[bucket];
                along.y=0;along.Normalize();var front=Vector3.Cross(Vector3.up,along);
                float wx=length*.5f,hy=height*.5f;
                float bevel=Mathf.Min(.075f,height*.15f);
                var corners=new[]{new Vector2(wx-bevel,hy),new Vector2(wx,hy-bevel),new Vector2(wx,-hy+bevel),
                    new Vector2(wx-bevel,-hy),new Vector2(-wx+bevel,-hy),new Vector2(-wx,-hy+bevel),
                    new Vector2(-wx,hy-bevel),new Vector2(-wx+bevel,hy)};
                int first=v.Count;
                // A modest bevel, slight face bow and unequal ends read as dressed stone.
                // Each course is flush with the next; this is not a row of detached rocks.
                for(int ring=0;ring<3;ring++)for(int j=0;j<8;j++)
                {
                    var q=corners[j];
                    float wear=.008f*Mathf.Sin(seed*2.17f+j*1.79f);
                    float scale=ring==0?.92f:1;
                    float d=ring==0?depth*.5f:ring==1?depth*.5f-bevel:-depth*.5f;
                    v.Add(center+along*(q.x*scale)+Vector3.up*(q.y*scale+wear)+front*d);
                }
                for(int ring=0;ring<2;ring++)for(int j=0;j<8;j++)
                {
                    int a=first+ring*8+j,b=first+ring*8+(j+1)%8,c=a+8,d=b+8;
                    f.AddRange(new[]{a,d,b,a,c,d});
                }
                int middle=v.Count;v.Add(center+front*(depth*.5f+.005f));
                for(int j=0;j<8;j++)f.AddRange(new[]{middle,first+j,first+(j+1)%8});
                int back=v.Count;v.Add(center-front*depth*.5f);
                for(int j=0;j<8;j++)f.AddRange(new[]{back,first+16+(j+1)%8,first+16+j});
            }
            public void Build()
            {
                for(int i=0;i<palette.Length;i++)if(vertices[i].Count>0)
                    owner.MeshObject("倒角错缝石砌 · "+i,parent,vertices[i],indices[i],palette[i]);
            }
        }

        static bool ReferenceFinishClear(Vector3 center,float halfLength,Vector3 along,bool paving=false)
        {
            // Do not write any decoration into the stored local placement region.
            float px=(center.x-11.7f)/6.15f,pz=center.z/5.15f;
            if(px*px+pz*pz<1.035f)return false;
            var point=new Vector2(center.x,center.z);
            for(int i=0;i<ReferenceStream.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceStream[i],ReferenceStream[i+1],out float t);
                float clearance=Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t)+.34f;
                if(square<clearance*clearance)return false;
            }
            if(paving)return true;
            foreach(var road in ReferenceRoads)for(int i=0;i<road.points.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,road.points[i],road.points[i+1],out _);
                float clearance=road.width+.28f;
                if(square<clearance*clearance)return false;
                // A long block must not overhang an otherwise open path entrance.
                foreach(float direction in new[]{-1f,1f})
                {
                    var end=center+along*halfLength*direction;
                    if(SegmentDistanceSquared(new Vector2(end.x,end.z),road.points[i],road.points[i+1],out _)<clearance*clearance)return false;
                }
            }
            return true;
        }

        void ReferenceTerraceWall(Transform parent,ReferenceMasonryBatches batch,Material joints,Material earth,
            Vector3 center,Vector2 half,float drop,int seed,bool soilBand=true)
        {
            // Only the front and two short returns are exposed. There is no artificial
            // enclosing wall along the back of a terrace embedded into the hillside.
            float left=center.x-half.x-.19f,right=center.x+half.x+.19f;
            float front=center.z-half.y-.24f,back=center.z+half.y-.24f;
            var outline=new[]{new Vector3(left,center.y,back),new Vector3(left,center.y,front+.55f),
                new Vector3(left+.55f,center.y,front),new Vector3(right-.55f,center.y,front),
                new Vector3(right,center.y,front+.55f),new Vector3(right,center.y,back)};
            ReferenceTerraceOutlineWall(parent,batch,joints,earth,outline,center.y,drop,seed,soilBand);
        }

        void ReferenceTerraceOutlineWall(Transform parent,ReferenceMasonryBatches batch,Material joints,Material earth,
            Vector3[] outline,float topY,float drop,int seed,bool soilBand)
        {
            var mortarVertices=new List<Vector3>();var mortarFaces=new List<int>();
            var soilVertices=new List<Vector3>();var soilFaces=new List<int>();
            var rng=new System.Random(seed);
            int courses=Mathf.Max(2,Mathf.CeilToInt(drop/.37f));float rowHeight=drop/courses;
            for(int edge=0;edge<outline.Length-1;edge++)
            {
                var a=outline[edge];var b=outline[edge+1];var along=(b-a).normalized;
                float run=Vector3.Distance(a,b);var outward=Vector3.Cross(Vector3.up,along);
                // Backing is broken only at actual openings and at hidden/buried sections.
                int strips=Mathf.CeilToInt(run/.38f);
                for(int s=0;s<strips;s++)
                {
                    var p=Vector3.Lerp(a,b,s/(float)strips);var q=Vector3.Lerp(a,b,(s+1)/(float)strips);
                    var middle=(p+q)*.5f;
                    if(!ReferenceFinishClear(middle,.22f,along))continue;
                    float baseY=Mathf.Max(topY-drop,Mathf.Min(ReferenceHeight((middle+outward*.6f).x,(middle+outward*.6f).z)-.25f,topY-.30f));
                    AddReferenceQuad(mortarVertices,mortarFaces,p-outward*.08f,q-outward*.08f,
                        new Vector3(q.x,baseY,q.z)-outward*.08f,new Vector3(p.x,baseY,p.z)-outward*.08f);
                    if(soilBand)
                    {
                        var inside=-outward*.23f;
                        var topP=p+inside+Vector3.up*.035f;var topQ=q+inside+Vector3.up*.035f;
                        AddReferenceQuad(soilVertices,soilFaces,topP,topQ,topQ-Vector3.up*.16f,topP-Vector3.up*.16f);
                    }
                }
                for(int row=0;row<courses;row++)
                {
                    float traveled=0,phase=(row%2)*.48f;
                    while(traveled<run-.04f)
                    {
                        float blockWidth=Mathf.Min(run-traveled,.87f+(float)rng.NextDouble()*.53f-(traveled==0?phase:0));
                        if(blockWidth<.13f)break;
                        var at=a+along*(traveled+blockWidth*.5f);
                        at.y=topY-drop+rowHeight*(row+.5f);
                        if(ReferenceFinishClear(at,blockWidth*.5f,along))
                        {
                            float visibleGround=ReferenceHeight((at+outward*.44f).x,(at+outward*.44f).z);
                            if(at.y+rowHeight*.5f>visibleGround-.18f)
                                batch.Stone(at+outward*(.025f*(courses-row)),along,blockWidth-.025f,rowHeight-.018f,.50f,seed++);
                        }
                        traveled+=blockWidth;
                    }
                }
                // A quiet coping course visually links the steps without a bead-like rim.
                float capAt=0;
                while(capAt<run-.08f)
                {
                    float length=Mathf.Min(run-capAt,1.05f+(float)rng.NextDouble()*.45f);
                    var at=a+along*(capAt+length*.5f)+Vector3.up*.065f;
                    if(ReferenceFinishClear(at,length*.5f,along))batch.Stone(at,along,length-.025f,.17f,.67f,seed++);
                    capAt+=length;
                }
            }
            if(mortarVertices.Count>0)MeshObject("嵌入阶地的连续灰土墙芯",parent,mortarVertices,mortarFaces,joints);
            if(soilVertices.Count>0)MeshObject("石帽后方薄土壤断面",parent,soilVertices,soilFaces,earth);
        }

        static void AddReferenceQuad(List<Vector3> vertices,List<int> faces,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d,d,c,b,a});
            // Separate front/back vertices preserve opposite normals. Sharing them
            // cancels RecalculateNormals and produces black courtyard tiles.
            faces.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+4,n+5,n+6,n+4,n+6,n+7});
        }

        void ReferenceStairFlight(ReferenceMasonryBatches batch,Vector3 from,Vector3 to,float width,int seed)
        {
            var forward=to-from;forward.y=0;float run=forward.magnitude;forward.Normalize();
            var across=Vector3.Cross(Vector3.up,forward);
            int count=Mathf.Max(1,Mathf.CeilToInt(run/.43f));
            for(int i=0;i<count;i++)
            {
                var a=Vector3.Lerp(from,to,i/(float)count);var b=Vector3.Lerp(from,to,(i+1)/(float)count);
                float ya=ReferenceHeight(a.x,a.z),yb=ReferenceHeight(b.x,b.z);
                float top=Mathf.Max(ya,yb)+.025f,baseY=Mathf.Min(ya,yb)-.065f;
                var at=(a+b)*.5f;at.y=(top+baseY)*.5f;
                batch.Stone(at,across,width,top-baseY,run/count+.015f,seed+i);
            }
        }

        void ReferenceCourtPavers(Transform parent,Material[] material)
        {
            var vertices=new[]{new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
            var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            float x0=5.32f,x1=32.98f;
            float z0=-12.98f,z1=11.05f;
            int row=0;var random=new System.Random(421);
            // Slender clay bricks deliberately contrast with the rounded path stones
            // and the large pale courtyard slabs. Staggered courses have narrow joints;
            // fixed random material choice avoids the previous repeating colour stripe.
            const float course=.48f,brickLength=1.08f,joint=.012f;
            for(float z=z0;z<z1;z+=course,row++)
            for(float x=x0-(row%2)*brickLength*.5f;x<x1;x+=brickLength)
            {
                float a=Mathf.Max(x0,x+joint),b=Mathf.Min(x1,x+brickLength-joint);
                float c=z+joint,d=Mathf.Min(z1,z+course-joint);
                if(b-a<.18f||d-c<.18f)continue;
                var center=new Vector3((a+b)*.5f,0,(c+d)*.5f);
                if(center.x>=5.3f&&center.x<=18.3f&&center.z>=-5.6f&&center.z<=5.6f)continue;
                if(!ReferenceFinishClear(center,.5f,Vector3.right,true))continue;
                float ya=ReferenceHeight(a,c),yb=ReferenceHeight(b,c),yc=ReferenceHeight(b,d),yd=ReferenceHeight(a,d);
                float high=Mathf.Max(ya,yb,yc,yd),low=Mathf.Min(ya,yb,yc,yd);
                if(high-low>.28f||Mathf.Abs((high+low)*.5f-ReferenceBrickCourt.y)>.15f)continue;
                int bucket=random.Next(3);
                ReferenceCourtBrick(vertices[bucket],faces[bucket],a,b,c,d);
            }
            for(int i=0;i<3;i++)if(vertices[i].Count>0)MeshObject("工坊庭院陶砖铺装 · "+i,parent,vertices[i],faces[i],material[i]);
        }

        static void ReferenceCourtBrick(List<Vector3> vertices,List<int> faces,float left,float right,float front,float back)
        {
            const float corner=.035f;
            var outline=new[]{new Vector2(left+corner,front),new Vector2(right-corner,front),
                new Vector2(right,front+corner),new Vector2(right,back-corner),new Vector2(right-corner,back),
                new Vector2(left+corner,back),new Vector2(left,back-corner),new Vector2(left,front+corner)};
            var center=new Vector2((left+right)*.5f,(front+back)*.5f);int start=vertices.Count;
            foreach(var p in outline)vertices.Add(new Vector3(p.x,ReferenceHeight(p.x,p.y)+.012f,p.y));
            foreach(var cornerPoint in outline)
            {
                var p=Vector2.Lerp(center,cornerPoint,.94f);
                vertices.Add(new Vector3(p.x,ReferenceHeight(p.x,p.y)+.026f,p.y));
            }
            int top=vertices.Count;vertices.Add(new Vector3(center.x,ReferenceHeight(center.x,center.y)+.026f,center.y));
            for(int cornerIndex=0;cornerIndex<8;cornerIndex++)
            {
                int next=(cornerIndex+1)%8;
                faces.AddRange(new[]{start+cornerIndex,start+8+next,start+next,
                    start+cornerIndex,start+8+cornerIndex,start+8+next,top,start+8+next,start+8+cornerIndex});
            }
        }

        void ReferenceWhiteCourtPavers(Transform parent,Material[] material)
        {
            var vertices=new[]{new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
            var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            int row=0;
            for(float z=-3.98f;z<6.98f;z+=.91f,row++)
            for(float x=4.02f-(row%2)*.65f;x<19.98f;x+=1.31f)
            {
                float a=Mathf.Max(4.02f,x+.015f),b=Mathf.Min(19.98f,x+1.295f),c=z+.015f,d=Mathf.Min(6.98f,z+.895f);
                if(b-a<.2f||d-c<.2f)continue;
                var center=new Vector3((a+b)*.5f,0,(c+d)*.5f);
                if(!ReferenceFinishClear(center,.65f,Vector3.right,true))continue;
                // No tile may bridge into the editable ellipse, even at one corner.
                bool clear=true;
                foreach(var point in new[]{new Vector2(a,c),new Vector2(b,c),new Vector2(a,d),new Vector2(b,d)})
                    if(Mathf.Pow((point.x-11.7f)/6.15f,2)+Mathf.Pow(point.y/5.15f,2)<1.10f)clear=false;
                if(!clear)continue;
                int bucket=(row+Mathf.FloorToInt(x))%3;if(bucket<0)bucket+=3;
                AddReferenceQuad(vertices[bucket],faces[bucket],new(a,.024f,c),new(a,.024f,d),new(b,.024f,d),new(b,.024f,c));
            }
            for(int i=0;i<3;i++)if(vertices[i].Count>0)MeshObject("既有地块外侧白石庭台 · "+i,parent,vertices[i],faces[i],material[i]);
        }

        void BuildReferenceBankStrata(Transform parent,Material[] stone)
        {
            // Long outcrops follow the surveyed slopes. The entire perimeter is buried
            // back into the terrain, giving a rock face rather than a line of boulders.
            var bands=new[]{
                new[]{new Vector2(-21.4f,-13),new Vector2(-22,-6),new Vector2(-22,1),new Vector2(-20.4f,8)},
                new[]{new Vector2(-18,-14),new Vector2(-13,-14.4f),new Vector2(-8,-14.2f),new Vector2(-4.5f,-13.8f)},
                new[]{new Vector2(-20,-29),new Vector2(-17,-32),new Vector2(-12,-33.3f),new Vector2(-7,-32.5f)},
                new[]{new Vector2(10,-14.3f),new Vector2(16,-14.7f),new Vector2(22,-15.0f),new Vector2(28,-14.7f)},
                new[]{new Vector2(31,2),new Vector2(31,7),new Vector2(27.8f,15.5f),new Vector2(26,22)},
                new[]{new Vector2(-3.8f,-18.2f),new Vector2(-5.1f,-20.6f),new Vector2(-5.2f,-24.3f)}
            };
            var vertices=new[]{new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
            var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            int bandIndex=0;
            foreach(var line in bands)
            {
                var spine=new List<Vector2>();
                for(int segment=0;segment<line.Length-1;segment++)
                {
                    int pieces=Mathf.CeilToInt(Vector2.Distance(line[segment],line[segment+1])/.55f);
                    for(int i=0;i<pieces;i++)spine.Add(Vector2.Lerp(line[segment],line[segment+1],i/(float)pieces));
                }
                spine.Add(line[line.Length-1]);
                const int rows=9;
                var grid=new Vector3[spine.Count,rows];var clear=new bool[spine.Count,rows];
                for(int column=0;column<spine.Count;column++)
                {
                    var tangent=spine[Mathf.Min(column+1,spine.Count-1)]-spine[Mathf.Max(0,column-1)];tangent.Normalize();
                    var cross=new Vector2(-tangent.y,tangent.x);
                    for(int row=0;row<rows;row++)
                    {
                        float across=(row/(float)(rows-1)-.5f)*3.6f;
                        var p=spine[column]+cross*across;
                        float y=ReferenceHeight(p.x,p.y);
                        float dx=(ReferenceHeight(p.x+.15f,p.y)-ReferenceHeight(p.x-.15f,p.y))/.3f;
                        float dz=(ReferenceHeight(p.x,p.y+.15f)-ReferenceHeight(p.x,p.y-.15f))/.3f;
                        float slope=Mathf.Sqrt(dx*dx+dz*dz);
                        float interior=Mathf.Sin(row*Mathf.PI/(rows-1))*Ease(0,2,column)*Ease(0,2,spine.Count-1-column);
                        float strata=.13f+.08f*Mathf.Sin(y*7.1f+p.x*.42f)+.05f*Mathf.PerlinNoise(p.x*1.2f+5,p.y*1.2f+7);
                        var normal=new Vector3(-dx,1,-dz).normalized;
                        grid[column,row]=new Vector3(p.x,y,p.y)+normal*(strata*interior-.045f);
                        clear[column,row]=slope>.52f&&y>ReferenceSeaLevel-.12f&&ReferenceFinishClear(new Vector3(p.x,y,p.y),.22f,Vector3.right);
                    }
                }
                for(int column=1;column<spine.Count;column++)for(int row=1;row<rows;row++)
                {
                    if(!clear[column,row]||!clear[column-1,row]||!clear[column,row-1]||!clear[column-1,row-1])continue;
                    int bucket=(bandIndex+row/3)%3;
                    var a=grid[column-1,row-1];var b=grid[column,row-1];var c=grid[column,row];var d=grid[column-1,row];
                    // Separate broad faces retain the uneven rock planes under grazing light.
                    AddReferenceQuad(vertices[bucket],faces[bucket],a,b,c,d);
                }
                bandIndex++;
            }
            for(int i=0;i<3;i++)if(vertices[i].Count>0)MeshObject("嵌入土坡的连续岩层 · "+i,parent,vertices[i],faces[i],stone[i]);
        }
    }
}
