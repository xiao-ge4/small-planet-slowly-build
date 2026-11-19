using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceBorderPlantingBuilt;
        delegate bool ReferencePlantGround(Vector2 point,float footprint,out Vector3 at,out Vector3 normal);
        static readonly (string label,Vector4 area,float heading)[] ReferenceFlowerAnchors=
        {
            ("小院树侧",new(-16.2f,-21.0f,1.55f,1.10f),15),
            ("池前转角",new(-9.8f,-33.0f,1.55f,.95f),-10),
            ("苗圃上缘",new(-17.8f,6.8f,1.70f,.92f),0),
            ("星台入口台脚",new(19.0f,27.7f,1.55f,1.10f),-8)
        };
        static Vector4 ReferenceFlowerAnchorArea(int index)
        {
            var area=ReferenceFlowerAnchors[index].area;
            if(index==3)
            {
                // Follow the relocated platform and its approach. The low soil
                // pocket is east of both stairs and south-west of the solid toes.
                area.x=ReferenceObservatoryApproach.x+4.6f;
                area.y=ReferenceObservatory.z-10.9f;
            }
            return area;
        }

        // Visual-only planting. Call after the reference terrain, cliffs and heroes exist.
        // The imported flowers are merged directly from their readable source meshes;
        // there is no GameObject or Collider per plant.
        void BuildReferencePlanting()
        {
            if(referenceBorderPlantingBuilt || details==null)return;
            referenceBorderPlantingBuilt=true;
            var root=Group("参考海岸 · 成团多层花草",details);
            var foliage=new Dictionary<Material,NurseryGeometry>();
            var leaves=new[]{
                NurseryMat("护岸灌丛 · 深橄榄底叶",new Color(.29f,.43f,.145f),.055f),
                NurseryMat("护岸灌丛 · 青绿中叶",new Color(.39f,.54f,.175f),.06f),
                NurseryMat("护岸灌丛 · 迎光嫩叶",new Color(.53f,.65f,.245f),.065f)
            };
            var stems=NurseryMat("护岸灌丛 · 枝茎",new Color(.29f,.34f,.12f),.04f);
            var flowerBatches=new Dictionary<Material,List<CombineInstance>>();
            var flowerSources=new[]{
                Resources.Load<GameObject>("CoastReference/Geology/flowers_cream"),
                Resources.Load<GameObject>("CoastReference/Geology/flowers_gold"),
                Resources.Load<GameObject>("CoastReference/Geology/flowers_lavender")
            };
            var sourceBounds=new Bounds[flowerSources.Length];
            for(int i=0;i<flowerSources.Length;i++)sourceBounds[i]=ReferenceFlowerBounds(flowerSources[i]);

            // Large broken drifts, rather than evenly spaced plants along every edge.
            // Each patch has an inner shrub mass, low foreground leaves and a small
            // number of taller, readable flower clumps. The broad paths remain empty.
            var patches=new (Vector4 area,int palette)[]{
                // Courtyard: flowers embrace the tree, pond and stepped coastal rim.
                (new(-18.3f,-23.9f,2.7f,2.3f),0),(new(-17.8f,-28.3f,2.1f,1.5f),1),
                (new(-19.5f,-20.1f,1.8f,2.0f),0),(new(-15.9f,-18.1f,2.1f,1.4f),2),
                (new(-11.8f,-33.4f,2.5f,1.15f),1),(new(-7.1f,-32.0f,1.8f,1.05f),0),
                (new(-6.9f,-29.6f,1.6f,1.2f),2),(new(-5.5f,-23.5f,1.1f,.85f),0),
                // Nursery: outside the actual crop beds, central aisle and water troughs.
                (new(-21.3f,-14.1f,1.6f,1.8f),1),(new(-22.4f,-8.7f,1.0f,1.5f),0),
                (new(-22.8f,-3.5f,.9f,1.8f),2),(new(-22.4f,2.2f,1.05f,1.4f),0),
                (new(-21.3f,11.6f,1.4f,1.65f),1),(new(-17.1f,14.3f,2.5f,1.25f),0),
                (new(-10.6f,13.6f,2.5f,1.5f),2),(new(-1.9f,7.3f,1.5f,2.1f),0),
                (new(-.7f,12.0f,1.2f,1.9f),1),
                // White court and reserve: frame the corners without filling the edit plot.
                (new(3.1f,5.5f,.85f,1.65f),0),(new(6.6f,8.6f,1.8f,.70f),2),
                (new(20.9f,7.5f,1.15f,1.0f),0),(new(7.2f,14.9f,1.25f,2.1f),1),
                (new(8.3f,19.3f,1.4f,1.0f),0),(new(29.1f,17.6f,1.25f,2.1f),2),
                // Observatory skirt: a pale-blue and cream backdrop, with a sunny gold tip.
                (new(8.5f,29.5f,1.25f,2.3f),2),(new(12.4f,38.2f,2.1f,1.1f),0),
                (new(23.0f,38.3f,2.0f,1.2f),1),(new(28.4f,32.0f,1.2f,2.4f),0),
                (new(27.7f,24.1f,1.4f,1.4f),2),
                // Workshop sea edge, keeping the yard stock, entry and pier stairs clear.
                (new(32.9f,1.2f,1.15f,2.3f),1),(new(33.2f,-6.3f,1.15f,2.0f),0),
                (new(30.8f,-11.1f,1.25f,.85f),1),(new(21.3f,-14.4f,1.0f,1.0f),2),
                (new(18.9f,-21.2f,.85f,1.2f),0),
                // The upper stream has isolated banks of green, never a complete hedge.
                (new(2.2f,12.9f,1.5f,2.1f),0),(new(2.1f,18.2f,1.45f,1.5f),1),
                (new(-4.7f,14.3f,1.35f,2.0f),0),(new(-3.8f,18.4f,1.6f,1.7f),1),
                (new(3.0f,8.7f,1.15f,1.65f),2),(new(2.4f,21.4f,1.5f,1.5f),0)
            };
            var rocks=new List<Collider>();
            foreach(var collider in transform.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled || !collider.gameObject.activeInHierarchy)continue;
                for(var t=collider.transform;t!=null&&t!=transform;t=t.parent)
                {
                    if((t.name.Contains("Geology/")&&!t.name.Contains("cave")&&!t.name.Contains("flowers")) ||
                        t.name.StartsWith("完整南岸岩坎") || t.name.StartsWith("圆台山脚 · 嵌入低岩层"))
                    {rocks.Add(collider);break;}
                }
            }
            Physics.SyncTransforms();
            int roots=0,shrubs=0,flowers=0;
            var random=new System.Random(417829);
            float R(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)random.NextDouble());

            bool Surface(Vector2 p,float footprint,out Vector3 at,out Vector3 normal)
            {
                at=new Vector3(p.x,ReferenceHeight(p.x,p.y),p.y);normal=Vector3.up;
                if(ReferenceUpperGardenEntryClear(p.x,p.y,footprint))return false;
                if(!ReferenceBorderPlantingClear(p,footprint) || at.y<ReferenceSeaLevel+.32f)return false;
                float dx=(ReferenceHeight(p.x+.14f,p.y)-ReferenceHeight(p.x-.14f,p.y))/.28f;
                float dz=(ReferenceHeight(p.x,p.y+.14f)-ReferenceHeight(p.x,p.y-.14f))/.28f;
                normal=new Vector3(-dx,1,-dz).normalized;
                // Closed geological meshes can stand above the heightfield. Survey those
                // surfaces directly so a planted ledge never floats or hides below rock.
                var ray=new Ray(details.TransformPoint(new Vector3(p.x,35,p.y)),-details.up);
                foreach(var rock in rocks)
                {
                    if(!rock.Raycast(ray,out var hit,50))continue;
                    var local=details.InverseTransformPoint(hit.point);
                    var n=details.InverseTransformDirection(hit.normal).normalized;
                    if(local.y>at.y && n.y>.58f){at=local;normal=n;}
                }
                if(normal.y<.60f)return false;
                if(footprint>.30f)
                {
                    // A whole flower asset needs a broad seat. Small groundcover is allowed
                    // on steeper shoulders, but flower bouquets cannot hang off a ledge.
                    float hi=at.y,lo=at.y;
                    for(int i=0;i<4;i++)
                    {
                        var q=p+new Vector2(i==0?footprint:i==1?-footprint:0,i==2?footprint:i==3?-footprint:0);
                        float y=ReferenceHeight(q.x,q.y);
                        if(!ReferenceBorderPlantingClear(q,.08f))return false;
                        var support=new Ray(details.TransformPoint(new Vector3(q.x,35,q.y)),-details.up);
                        foreach(var rock in rocks)if(rock.Raycast(support,out var h,50))
                        {var local=details.InverseTransformPoint(h.point);if(h.normal.y>.58f)y=Mathf.Max(y,local.y);}
                        hi=Mathf.Max(hi,y);lo=Mathf.Min(lo,y);
                    }
                    if(hi-lo>.34f)return false;
                }
                at+=normal*.015f;
                return true;
            }

            for(int patch=0;patch<patches.Length;patch++)
            {
                var a=patches[patch].area;
                int attempts=Mathf.Clamp(Mathf.CeilToInt(a.z*a.w*23),32,92);
                int accepted=0;
                for(int sample=0;sample<attempts;sample++)
                {
                    float u=R(-1,1),v=R(-1,1);
                    float edge=.80f+.12f*Mathf.Sin(u*5.1f+patch)+.10f*Mathf.Cos(v*6.7f-patch);
                    if(u*u+v*v>edge)continue;
                    var p=new Vector2(a.x+u*a.z,a.y+v*a.w);
                    // These four focal banks are composed below as continuous drifts;
                    // isolated small bouquets would break their white/gold colour masses.
                    if(ReferenceInFlowerAnchor(p,1.16f))continue;
                    if(!Surface(p,.17f,out var foot,out var normal))continue;
                    float scale=R(.82f,1.26f);
                    int seed=patch*101+sample*17;
                    ReferenceBorderRosette(foliage,foot,scale,seed,leaves);roots++;
                    bool interior=u*u+v*v<.58f;
                    if(interior && accepted%3==0 && Surface(p,.42f,out var bushAt,out _))
                    {
                        ReferenceBorderShrub(foliage,bushAt,scale*(patch%4==0?1.23f:1),seed,leaves,stems);
                        shrubs++;
                    }
                    if(accepted%3==1 && Surface(p,.38f,out var flowerAt,out var flowerNormal))
                    {
                        int kind=accepted%15==1?patches[patch].palette:(patch%3==0?1:0);
                        var source=flowerSources[kind];
                        if(source!=null)
                        {
                            float spread=R(.88f,1.24f),height=(kind==2?.85f:.62f)*R(.86f,1.19f);
                            var bounds=sourceBounds[kind];
                            float sx=spread/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z));
                            float sy=height/Mathf.Max(.01f,bounds.size.y);
                            var rotation=Quaternion.FromToRotation(Vector3.up,Vector3.Slerp(Vector3.up,flowerNormal,.18f))*Quaternion.Euler(0,R(0,360),0);
                            var translation=flowerAt-rotation*new Vector3(bounds.center.x*sx,bounds.min.y*sy+.035f,bounds.center.z*sx);
                            ReferenceMergeFlower(flowerBatches,source,Matrix4x4.TRS(translation,rotation,new Vector3(sx,sy,sx)));
                            flowers++;
                        }
                    }
                    accepted++;
                }
            }

            var drifting=BuildReferenceVegetationDrifts(foliage,leaves,stems,Surface);
            roots+=drifting.roots;shrubs+=drifting.shrubs;
            var anchorPlanting=BuildReferenceFlowerAnchors(foliage,flowerBatches,flowerSources,sourceBounds,leaves,stems,Surface);
            roots+=anchorPlanting.roots;shrubs+=anchorPlanting.shrubs;flowers+=anchorPlanting.flowers;
            shrubs+=BuildReferenceHydrangeas(foliage,leaves,stems,Surface);
            int trails=BuildReferenceWallTrails(foliage,leaves,stems);
            int crowns=WidenReferenceBackgroundCrowns();
            foreach(var pair in foliage)
            {
                if(pair.Value.Points.Count==0)continue;
                var mesh=new Mesh{name="参考海岸 · 弯曲叶片合批",indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(pair.Value.Points);mesh.SetTriangles(pair.Value.Faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var go=Group(pair.Key.name,root).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var pair in flowerBatches)
            {
                var mesh=new Mesh{name="参考海岸 · 现有花束合批",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                var go=Group("成团花色 · "+pair.Key.name,root).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            Debug.Log($"Reference border planting: {roots} low rosettes, {shrubs} leafy shrubs, {flowers} source flower clumps, {trails} rooted wall trails, {crowns} wider background crowns, {root.childCount} batched renderers; no colliders.");
        }

        static bool ReferenceInFlowerAnchor(Vector2 point,float expansion)
        {
            for(int index=0;index<ReferenceFlowerAnchors.Length;index++)
            {
                var anchor=ReferenceFlowerAnchors[index];var area=ReferenceFlowerAnchorArea(index);
                var delta=point-new Vector2(area.x,area.y);
                float angle=-anchor.heading*Mathf.Deg2Rad;
                var local=new Vector2(delta.x*Mathf.Cos(angle)-delta.y*Mathf.Sin(angle),
                    delta.x*Mathf.Sin(angle)+delta.y*Mathf.Cos(angle));
                if(Mathf.Pow(local.x/(area.z*expansion),2)+Mathf.Pow(local.y/(area.w*expansion),2)<1)return true;
            }
            return false;
        }

        (int roots,int shrubs,int flowers) BuildReferenceFlowerAnchors(Dictionary<Material,NurseryGeometry> foliage,
            Dictionary<Material,List<CombineInstance>> batches,GameObject[] sources,Bounds[] sourceBounds,
            Material[] leaves,Material stems,ReferencePlantGround survey)
        {
            int roots=0,shrubs=0,flowers=0;
            for(int anchorIndex=0;anchorIndex<ReferenceFlowerAnchors.Length;anchorIndex++)
            {
                var anchor=ReferenceFlowerAnchors[anchorIndex];
                var area=ReferenceFlowerAnchorArea(anchorIndex);
                var sites=new List<Vector2>();
                float angle=anchor.heading*Mathf.Deg2Rad;
                // Overlapping bouquets are wide enough to read as a single 2–3 m bank.
                // Staggered centres avoid either a plantation grid or evenly spaced dots.
                for(int row=-4;row<=4;row++)for(int column=-4;column<=4;column++)
                {
                    var q=new Vector2(column*.45f+(row%2)*.16f,row*.43f);
                    q+=new Vector2(.035f*Mathf.Sin(row*4.7f+column),.035f*Mathf.Sin(column*3.2f+row));
                    float outline=Mathf.Pow(q.x/area.z,2)+Mathf.Pow(q.y/area.w,2);
                    if(outline>.88f+.065f*Mathf.Sin(row*1.1f+anchorIndex))continue;
                    // The greenhouse forecourt has two unequal groups and a
                    // visible stone opening toward its approach, not a solid hedge.
                    if(anchorIndex==2&&q.x>-.12f&&q.x<.83f&&q.y<.58f)continue;
                    sites.Add(q);
                }
                sites.Sort((a,b)=>a.sqrMagnitude.CompareTo(b.sqrMagnitude));
                int bankFlowers=0,bankRoots=0,bankShrubs=0;
                var plantedFlowers=new List<Vector2>();
                for(int i=0;i<sites.Count;i++)
                {
                    var q=sites[i];
                    var point=new Vector2(area.x+q.x*Mathf.Cos(angle)-q.y*Mathf.Sin(angle),
                        area.y+q.x*Mathf.Sin(angle)+q.y*Mathf.Cos(angle));
                    if(!survey(point,.21f,out var at,out _))continue;
                    int seed=2101+anchorIndex*317+i*23;
                    ReferenceBorderRosette(foliage,at,(anchorIndex==2?1.02f:1.63f)+.14f*Mathf.Sin(seed),seed,leaves);
                    bankRoots++;roots++;
                    bool greenhouseShrub=anchorIndex!=2||(q.y>.18f&&q.x<-.45f&&bankShrubs<3);
                    if(greenhouseShrub&&i%4==0 && bankShrubs<9 && survey(point,.43f,out var bush,out _))
                    {
                        ReferenceBorderShrub(foliage,bush,anchorIndex==2?.94f:1.24f,seed,leaves,stems);bankShrubs++;shrubs++;
                    }
                    if(bankFlowers>=14)continue;
                    bool near=false;
                    foreach(var existing in plantedFlowers)if((point-existing).sqrMagnitude<.47f*.47f){near=true;break;}
                    if(near)continue;
                    float spread=1.16f+.12f*(.5f+.5f*Mathf.Sin(seed*1.73f));
                    if(anchorIndex==2)spread=.64f+.28f*Mathf.Clamp01((q.y+.65f)/1.3f)+.06f*Mathf.Sin(seed);
                    if(!survey(point,spread*.43f,out var flowerAt,out var normal))continue;
                    // White is dominant, warm yellow forms adjacent islands. A single
                    // lavender bouquet at the outer end supplies the minor accent.
                    int kind=bankFlowers==12?2:((q.x+q.y*.3f)>.18f?1:0);
                    if(anchorIndex==2&&q.x<-.65f&&q.y>.10f)kind=2;
                    var source=sources[kind];if(source==null)continue;
                    var bounds=sourceBounds[kind];
                    float sx=spread/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z));
                    float sy=(kind==2?.88f:.73f+.05f*Mathf.Sin(seed))/Mathf.Max(.01f,bounds.size.y);
                    if(anchorIndex==2)sy=(kind==2?.82f:Mathf.Lerp(.30f,.57f,Mathf.Clamp01((q.y+.60f)/1.2f)))/Mathf.Max(.01f,bounds.size.y);
                    var rotation=Quaternion.FromToRotation(Vector3.up,Vector3.Slerp(Vector3.up,normal,.14f))*Quaternion.Euler(0,seed*31,0);
                    var translation=flowerAt-rotation*new Vector3(bounds.center.x*sx,bounds.min.y*sy+.045f,bounds.center.z*sx);
                    ReferenceMergeFlower(batches,source,Matrix4x4.TRS(translation,rotation,new Vector3(sx,sy,sx)));
                    plantedFlowers.Add(point);bankFlowers++;flowers++;
                }
                Debug.Log($"Reference focal flower bank {anchor.label}: {bankRoots} supported groundcover, {bankShrubs} shrubs, {bankFlowers} overlapping white/gold bouquets.");
            }
            return(roots,shrubs,flowers);
        }

        (int roots,int shrubs) BuildReferenceVegetationDrifts(Dictionary<Material,NurseryGeometry> batch,Material[] leaves,Material stems,ReferencePlantGround survey)
        {
            // Connected pockets of understory, interrupted at bends and paths. The flower
            // clumps above sit in these green masses rather than being glued to bare rock.
            var lines=new (float width,Vector2[] points)[]{
                (.95f,new[]{new Vector2(-18.8f,-29),new(-20,-25),new(-19.1f,-20.5f),new(-16.2f,-18.3f)}),
                (.73f,new[]{new Vector2(-15.7f,-33.1f),new(-12.1f,-34.1f),new(-7.2f,-32.8f)}),
                (.68f,new[]{new Vector2(-22.3f,-12),new(-22.7f,-5.4f),new(-22.6f,2.8f),new(-21.6f,11.9f)}),
                (1.05f,new[]{new Vector2(-20.1f,13.2f),new(-16.0f,14.6f),new(-11.5f,14.0f)}),
                (.65f,new[]{new Vector2(8.9f,11.7f),new(9.2f,16.1f),new(9.0f,20.6f)}),
                (.78f,new[]{new Vector2(29.0f,12.4f),new(29.5f,17.4f),new(28.0f,22.3f)}),
                (.83f,new[]{new Vector2(8.0f,24.4f),new(8.2f,30.3f),new(12.2f,38.1f)}),
                (.76f,new[]{new Vector2(27.4f,23.1f),new(29.1f,28.2f),new(27.9f,35.0f)}),
                (.65f,new[]{new Vector2(33.8f,2.5f),new(34.0f,-4.2f),new(32.7f,-10.6f)}),
                (.59f,new[]{new Vector2(21.7f,-14.4f),new(25.1f,-14.8f),new(30.1f,-13.5f)}),
                (.83f,new[]{new Vector2(.2f,9.1f),new(.8f,13.2f),new(1.8f,17.6f),new(.6f,20.8f)})
            };
            const int maxRoots=560,maxShrubs=105;
            int roots=0,shrubs=0;
            for(int line=0;line<lines.Length;line++)
            {
                int station=0;
                for(int part=0;part<lines[line].points.Length-1;part++)
                {
                    var a=lines[line].points[part];var b=lines[line].points[part+1];
                    var delta=b-a;var side=new Vector2(-delta.y,delta.x).normalized;
                    int count=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.43f));
                    for(int step=0;step<count;step++,station++)
                    {
                        if(roots>=maxRoots)return(roots,shrubs);
                        // One wider breathing gap every few metres preserves a broken natural
                        // edge, while the supported sections grow into a contiguous leaf mass.
                        if(Mathf.Sin(station*.53f+line*1.9f)<-.78f)continue;
                        float t=(step+.5f)/count;
                        var center=Vector2.Lerp(a,b,t);
                        float width=lines[line].width*(.83f+.17f*Mathf.Sin(station*.63f+line));
                        for(int lane=0;lane<3;lane++)
                        {
                            var point=center+side*((lane-1)*width*.61f+.065f*Mathf.Sin(station*3.1f+lane));
                            if(!survey(point,.18f,out var at,out _))continue;
                            int seed=line*719+station*23+lane*3;
                            ReferenceBorderRosette(batch,at,1.16f+.18f*Mathf.Sin(seed),seed,leaves);roots++;
                            if(lane==1 && station%3==0 && shrubs<maxShrubs && survey(point,.43f,out var bush,out _))
                            {ReferenceBorderShrub(batch,bush,1.13f,seed,leaves,stems);shrubs++;}
                        }
                    }
                }
            }
            return(roots,shrubs);
        }

        static bool ReferenceBorderPlantingClear(Vector2 p,float radius)
        {
            // All stored edit transforms remain available in the original plot frame.
            if(p.x>4.4f-radius&&p.x<19.05f+radius&&Mathf.Abs(p.y)<5.95f+radius)return false;
            // Cave and outfall planting is surveyed separately against the cave mesh.
            if(p.x>-5.4f-radius&&p.x<12.7f+radius&&p.y>-29.9f-radius&&p.y< -17.1f+radius)return false;
            if(ReferenceShoreDistance(p)<.55f)return false;
            foreach(var road in ReferenceRoads)
            for(int i=0;i<road.points.Length-1;i++)
                if(SegmentDistanceSquared(p,road.points[i],road.points[i+1],out _)<Mathf.Pow(road.width+radius+.24f,2))return false;
            for(int i=0;i<ReferenceStream.Length-1;i++)
            {
                float square=SegmentDistanceSquared(p,ReferenceStream[i],ReferenceStream[i+1],out float t);
                if(square<Mathf.Pow(Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t)+radius+.25f,2))return false;
            }
            // Pond, seat, chest, buildings, beds and the tier irrigation remain readable.
            if(Mathf.Pow((p.x-ReferencePondX)/(2.38f+radius),2)+Mathf.Pow((p.y-ReferencePondZ)/(1.83f+radius),2)<1)return false;
            if((p-new Vector2(-12.6f,-25)).sqrMagnitude<Mathf.Pow(1.25f+radius,2))return false;
            if((p-new Vector2(-13,-22.5f)).sqrMagnitude<Mathf.Pow(1.1f+radius,2))return false;
            // The greenhouse moved inland and is now 78% of its former footprint;
            // retain its actual building clearance, rather than blanking the front bank.
            if(Mathf.Abs(p.x-ReferenceGreenhouse.x)<5.2f+radius&&Mathf.Abs(p.y-ReferenceGreenhouse.z)<3.65f+radius)return false;
            if(Mathf.Abs(p.x-27.4f)<5.35f+radius&&Mathf.Abs(p.y+3.9f)<4.35f+radius)return false;
            if(Mathf.Pow((p.x-ReferenceObservatory.x)/(ReferenceObservatoryRimHalf.x+.015f+radius),2)+
                Mathf.Pow((p.y-ReferenceObservatory.z)/(ReferenceObservatoryRimHalf.y+.015f+radius),2)<1)return false;
            foreach(float row in new[]{-11f,-6f,-.65f})
            {
                if(Mathf.Abs(p.y-row)<1.98f+radius && Mathf.Abs(p.x+13.9f)<2.78f+radius)return false;
                if(Mathf.Abs(p.y-row-.34f)<1.98f+radius && Mathf.Abs(p.x+6.9f)<2.78f+radius)return false;
            }
            if(Mathf.Abs(p.x+17.1f)<.55f+radius&&p.y>-13.8f&&p.y<4.5f)return false;
            foreach(float z in ReferenceNurseryCanalZ)
                if(p.x>-17.7f&&p.x<-.5f&&Mathf.Abs(p.y-z)<.53f+radius)return false;
            return true;
        }

        void ReferenceBorderRosette(Dictionary<Material,NurseryGeometry> batch,Vector3 at,float scale,int seed,Material[] leaves)
        {
            for(int i=0;i<9;i++)
                ReferenceRoundedLeaf(PlantMaterial(batch,leaves[Mathf.Abs(seed+i)%3]),at,seed*31+i*137.5f,
                    .23f+(i%3)*.08f,(.18f+.025f*(i%3))*scale,.054f*scale,.034f*scale);
        }

        void ReferenceBorderShrub(Dictionary<Material,NurseryGeometry> batch,Vector3 at,float scale,int seed,Material[] leaves,Material stems)
        {
            for(int branch=0;branch<3;branch++)
            {
                var direction=Quaternion.Euler(0,seed*19+branch*120,0)*Vector3.forward;
                var root=at+direction*.08f*scale;
                var tip=root+direction*.19f*scale+Vector3.up*(.29f+branch*.045f)*scale;
                BloomStem(PlantMaterial(batch,stems),root,tip,.012f*scale);
                for(int leaf=0;leaf<10;leaf++)
                {
                    var p=Vector3.Lerp(root,tip,.20f+leaf*.080f);
                    ReferenceRoundedLeaf(PlantMaterial(batch,leaves[(leaf+branch)%3]),p,
                        seed*19+branch*120+leaf*137.5f,.42f,(.215f+.018f*(leaf%3))*scale,.075f*scale,.045f*scale);
                }
            }
        }

        // Rounded, cupped leaves make a fine canopy rather than isolated large spear tips.
        void ReferenceRoundedLeaf(NurseryGeometry batch,Vector3 root,float heading,float pitch,float length,float width,float curl)
        {
            var forward=Quaternion.Euler(0,heading,0)*Vector3.forward;
            var side=Vector3.Cross(Vector3.up,forward);
            var spine=Vector3.LerpUnclamped(forward,Vector3.up,pitch);
            int first=batch.Points.Count;
            const int sections=8;
            for(int i=0;i<=sections;i++)
            {
                float t=i/(float)sections;
                float swell=Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(2*t-1,2)));
                var middle=root+spine*(length*t)+Vector3.up*(curl*t*t);
                batch.Points.Add(middle-side*swell*width+Vector3.up*width*.16f*swell);
                batch.Points.Add(middle+Vector3.up*width*.045f*swell);
                batch.Points.Add(middle+side*swell*width+Vector3.up*width*.16f*swell);
                if(i==0)continue;
                int b=first+i*3,a=b-3;
                batch.Faces.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+1,b+1,a+2,a+2,b+1,b+2});
            }
        }

        int BuildReferenceWallTrails(Dictionary<Material,NurseryGeometry> batch,Material[] leaves,Material stems)
        {
            int count=0;
            void Trail(Vector3 cap,Vector3 outward,Vector3 along,int seed)
            {
                // The same predicate owns the masonry cap, so no ivy starts over a missing
                // block at a path or canal opening. Roots sit on its actual .15 m high coping.
                var wall=cap-Vector3.up*.15f;
                if(!ReferenceFinishClear(wall,.64f,along))return;
                if(ReferenceHeight(cap.x,cap.z)>cap.y+.12f)return;
                var root=cap+outward*.12f;
                ReferenceBorderRosette(batch,root,.68f,seed,leaves);
                for(int branch=0;branch<5;branch++)
                {
                    Vector3 last=root+along*((branch-2)*.18f);
                    int length=12+(Mathf.Abs(seed+branch)%6);
                    for(int step=0;step<length;step++)
                    {
                        float depth=(step+1)*.095f;
                        var p=root+along*((branch-2)*.18f+.055f*Mathf.Sin(step*.8f+seed+branch))+
                            outward*(.24f+.025f*Mathf.Sin(step*.7f+branch))-Vector3.up*depth;
                        if(p.y<ReferenceHeight(p.x,p.z)+.05f)break;
                        bool crossingPath=false;
                        foreach(var road in ReferenceRoads)
                        for(int i=0;i<road.points.Length-1;i++)
                        {
                            float square=SegmentDistanceSquared(new Vector2(p.x,p.z),road.points[i],road.points[i+1],out float t);
                            float grade=Mathf.Lerp(road.points[i].y,road.points[i+1].y,t);
                            if(Mathf.Abs(p.y-grade)<1.35f && square<Mathf.Pow(road.width+.24f,2))crossingPath=true;
                        }
                        if(crossingPath)break;
                        BloomStem(PlantMaterial(batch,stems),last,p,.007f);
                        for(int sign=-1;sign<=1;sign+=2)
                            ReferenceRoundedLeaf(PlantMaterial(batch,leaves[(branch+step)%3]),p,
                                Mathf.Atan2(outward.x,outward.z)*Mathf.Rad2Deg+sign*(66+step*3),-.25f,
                                .18f+.022f*(step%2),.068f,-.016f);
                        last=p;
                    }
                }
                count++;
            }
            for(int tier=0;tier<3;tier++)
            {
                float front=tier==0?-13.64f:tier==1?-8.84f:-3.49f;
                foreach(float x in new[]{-15.8f,-13.1f,-7.8f,-5.4f})
                    Trail(new Vector3(x,ReferenceNurseryLevels[tier]+.15f,front),Vector3.back,Vector3.right,71+tier*37+(int)(x*9));
            }
            foreach(float x in new[]{8.9f,13.3f,18.1f,23.0f,26.1f})
                Trail(new Vector3(x,2.55f,9.76f),Vector3.back,Vector3.right,301+(int)(x*11));
            foreach(float x in new[]{9.5f,11.8f,20.8f,24.2f,30.8f})
                Trail(new Vector3(x,-.25f,-13.24f),Vector3.back,Vector3.right,501+(int)(x*7));
            for(int i=0;i<8;i++)
            {
                float angle=Mathf.PI+(i+.5f)*Mathf.PI/8;
                var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var cap=ReferenceObservatory+new Vector3(direction.x*9.345f,.15f,direction.z*8.085f);
                Trail(cap,direction,new Vector3(-direction.z,0,direction.x),801+i*23);
            }
            return count;
        }

        int WidenReferenceBackgroundCrowns()
        {
            // The foreground tree already frames the courtyard. Only the distant trees
            // get a broader crown; their trunks, roots, transforms and physics stay intact.
            var replacements=new Dictionary<Mesh,Mesh>();
            int trees=0;
            foreach(var model in details.GetComponentsInChildren<Transform>())
            {
                if(!model.name.Contains("Buildings/courtyard_tree"))continue;
                var position=details.InverseTransformPoint(model.position);
                if(position.z<20 || position.x> -24)continue;
                var leaves=new List<MeshFilter>();
                Bounds crown=default;bool first=true;
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    string name=filter.name.ToLowerInvariant();
                    if(!name.Contains("foliage")&&!name.Contains("blossom"))continue;
                    var mesh=filter.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
                    leaves.Add(filter);
                    var matrix=model.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var bound=mesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var point=matrix.MultiplyPoint3x4(bound.center+Vector3.Scale(bound.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                        if(first){crown=new Bounds(point,Vector3.zero);first=false;}else crown.Encapsulate(point);
                    }
                }
                foreach(var filter in leaves)
                {
                    var original=filter.sharedMesh;
                    if(!replacements.TryGetValue(original,out var wider))
                    {
                        wider=Instantiate(original);wider.name=original.name+" · 宽冠";
                        var matrix=model.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        var inverse=matrix.inverse;var points=wider.vertices;
                        for(int i=0;i<points.Length;i++)
                        {
                            var p=matrix.MultiplyPoint3x4(points[i]);
                            p=crown.center+Vector3.Scale(p-crown.center,new Vector3(1.30f,1.055f,1.30f));
                            points[i]=inverse.MultiplyPoint3x4(p);
                        }
                        wider.vertices=points;wider.RecalculateNormals();wider.RecalculateBounds();
                        replacements[original]=wider;meshes.Add(wider);
                    }
                    filter.sharedMesh=wider;
                }
                if(leaves.Count>0)trees++;
            }
            return trees;
        }

        static Bounds ReferenceFlowerBounds(GameObject source)
        {
            var bounds=new Bounds(Vector3.zero,Vector3.one);bool first=true;
            if(source==null)return bounds;
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null)continue;
                var local=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=local.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        void ReferenceMergeFlower(Dictionary<Material,List<CombineInstance>> batches,GameObject source,Matrix4x4 placement)
        {
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();var mesh=filter.sharedMesh;
                if(renderer==null||mesh==null||!mesh.isReadable)continue;
                var mats=renderer.sharedMaterials;
                var transform=placement*source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int sub=0;sub<mesh.subMeshCount&&sub<mats.Length;sub++)
                {
                    var material=ReferenceMaterial(mats[sub]);
                    if(!batches.TryGetValue(material,out var list))batches[material]=list=new List<CombineInstance>();
                    list.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=transform});
                }
            }
        }
    }
}
