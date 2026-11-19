using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceNaturalBanksBuilt;

        // A few surveyed bank compositions, not another island-wide scattering pass.
        // Run after the closed geological assets, before general planting and navigation.
        // The terrain, existing colliders and saved editable plot are never modified.
        void BuildReferenceNaturalBanks()
        {
            if(referenceNaturalBanksBuilt || details==null)return;
            referenceNaturalBanksBuilt=true;
            var root=Group("参考岸坡 · Geology/低岩层与连续地被",details);
            var source=Resources.Load<GameObject>("CoastReference/Geology/cliff_bedding");
            var geometry=source==null?null:ReferenceForeshoreGeometry.Read(source);
            var rocks=new Dictionary<Material,List<CombineInstance>>();
            var rockMaterials=new Dictionary<Material,Material>();
            int beds=0;

            // Ground/road heights were surveyed in the editor. The nursery's broad
            // apparent slope is mostly low land: only the actual raised path flank
            // receives rocks. Other parts receive a low plant drift, not a rock pile.
            var seams=new (string name,Vector2 center,Vector2 outward,float width,float depth)[]{
                ("苗圃坡脚下层",new(-12.35f,-17.60f),new(-.7f,-.7f),2.65f,1.65f),
                ("苗圃坡脚中层",new(-12.35f,-15.80f),new(-.8f,-.6f),2.90f,1.70f),
                ("苗圃嵌入上层",new(-11.95f,-14.90f),new(-.8f,-.6f),2.50f,1.50f),
                ("海口东岸内层",new(31.70f,-12.90f),new(.5f,-.87f),2.80f,1.70f),
                ("海口东岸横层",new(29.80f,-13.50f),new(.2f,-.98f),2.60f,1.50f),
                ("海口东岸低脚",new(30.80f,-14.10f),new(.2f,-.98f),2.30f,1.50f)
            };
            if(geometry!=null)
            {
                foreach(var seam in seams)
                {
                    var outward=seam.outward.normalized;
                    float heading=Mathf.Atan2(outward.x,outward.y)*Mathf.Rad2Deg;
                    var rotation=Quaternion.Euler(0,heading-geometry.nativeFacing,0);
                    var rx=rotation*Vector3.right;var rz=rotation*Vector3.forward;
                    var right=new Vector2(rx.x,rx.z);var forward=new Vector2(rz.x,rz.z);
                    var front=seam.center+outward*(seam.depth*.45f);
                    var back=seam.center-outward*(seam.depth*.45f);
                    float frontY=ReferenceHeight(front.x,front.y);
                    float floor=frontY;
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        var foot=front+right*(seam.width*.34f*sign);
                        floor=Mathf.Min(floor,ReferenceHeight(foot.x,foot.y));
                    }
                    float bottom=floor-.24f;
                    float top=Mathf.Min(ReferenceHeight(back.x,back.y)-.08f,bottom+seam.width*.62f);
                    top=ReferenceRockTopBelowRoads(seam.center,right,forward,seam.width*.5f,seam.depth*.5f,top,true);
                    if(top-bottom<.48f || top<ReferenceSeaLevel+.23f ||
                        !ReferenceRockFaceClear(seam.center,right,forward,seam.width*.5f,seam.depth*.5f,top,true))
                    {Debug.Log($"Natural bank omitted by real footprint: {seam.name}");continue;}
                    var scale=new Vector3(seam.width/geometry.bounds.size.x,(top-bottom)/geometry.bounds.size.y,seam.depth/geometry.bounds.size.z);
                    var pivot=new Vector3(geometry.bounds.center.x,geometry.bounds.min.y,geometry.bounds.center.z);
                    var position=new Vector3(seam.center.x,bottom,seam.center.y)-rotation*Vector3.Scale(scale,pivot);
                    var placement=Matrix4x4.TRS(position,rotation,scale);
                    // A bounded placement can still be completely buried. Check the
                    // actual source vertices, rather than claiming an installed count
                    // proves visible coverage. Most of the back must remain embedded.
                    int visible=0,total=0,embedded=0;
                    foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh=filter.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
                        var matrix=placement*source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        var points=mesh.vertices;
                        for(int i=0;i<points.Length;i+=5)
                        {
                            var p=matrix.MultiplyPoint3x4(points[i]);
                            float relief=p.y-ReferenceHeight(p.x,p.z);
                            if(relief>.045f)visible++;if(relief<-.06f)embedded++;total++;
                        }
                    }
                    if(total==0 || visible<total*.045f || embedded<total*.20f)
                    {Debug.Log($"Natural bank omitted: insufficient exposed/embedded source volume, {seam.name}, {visible}/{embedded}/{total}");continue;}
                    ReferenceRockFaceMerge(rocks,rockMaterials,source,placement,"cliff_bedding");beds++;
                    Debug.Log($"Natural bank {seam.name}: ({seam.center.x:F2},{seam.center.y:F2}) y{bottom:F2}..{top:F2}; visible vertices {visible}/{total}; source back embedded {embedded}/{total}");
                }
            }
            foreach(var pair in rocks)
            {
                var mesh=new Mesh{name="天然低岩岸 · 闭合源模型合批",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                var go=Group(pair.Key.name,root).gameObject;go.layer=ObstacleLayer;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
                go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            if(geometry!=null)BuildReferenceCaveBankStrata(root,source,geometry,rockMaterials);

            // Plant support is the true heightfield or the upward-facing surface of
            // a real geological mesh. No flowers suspended against a vertical wall.
            var supports=new List<Collider>();
            foreach(var collider in transform.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                for(var t=collider.transform;t!=null&&t!=transform;t=t.parent)
                {
                    if(t.name.Contains("Geology/")&&!t.name.Contains("flowers")&&!t.name.Contains("cave"))
                    {supports.Add(collider);break;}
                }
            }
            Physics.SyncTransforms();
            bool Support(Vector2 p,float footprint,out Vector3 at,out Vector3 normal)
            {
                at=new Vector3(p.x,ReferenceHeight(p.x,p.y),p.y);normal=Vector3.up;
                if(!ReferenceNaturalBankPlantClear(p,footprint)||at.y<ReferenceSeaLevel+.20f)return false;
                float dx=(ReferenceHeight(p.x+.12f,p.y)-ReferenceHeight(p.x-.12f,p.y))/.24f;
                float dz=(ReferenceHeight(p.x,p.y+.12f)-ReferenceHeight(p.x,p.y-.12f))/.24f;
                normal=new Vector3(-dx,1,-dz).normalized;
                var ray=new Ray(details.TransformPoint(new Vector3(p.x,25,p.y)),-details.up);
                foreach(var collider in supports)
                {
                    if(!collider.Raycast(ray,out var hit,40))continue;
                    var position=details.InverseTransformPoint(hit.point);
                    var n=details.InverseTransformDirection(hit.normal).normalized;
                    if(position.y>at.y&&n.y>.57f){at=position;normal=n;}
                }
                if(normal.y<.58f)return false;
                if(footprint>.23f)
                {
                    float hi=at.y,lo=at.y;
                    for(int side=0;side<4;side++)
                    {
                        var q=p+new Vector2(side==0?footprint:side==1?-footprint:0,side==2?footprint:side==3?-footprint:0);
                        if(!ReferenceNaturalBankPlantClear(q,.07f))return false;
                        float y=ReferenceHeight(q.x,q.y);
                        var supportRay=new Ray(details.TransformPoint(new Vector3(q.x,25,q.y)),-details.up);
                        foreach(var collider in supports)
                            if(collider.Raycast(supportRay,out var hit,40)&&details.InverseTransformDirection(hit.normal).y>.57f)
                                y=Mathf.Max(y,details.InverseTransformPoint(hit.point).y);
                        hi=Mathf.Max(hi,y);lo=Mathf.Min(lo,y);
                    }
                    if(hi-lo>.31f)return false;
                }
                at+=normal*.012f;return true;
            }
            var foliage=new Dictionary<Material,NurseryGeometry>();
            var flowerBatches=new Dictionary<Material,List<CombineInstance>>();
            var leaves=new[]{
                NurseryMat("天然岸坡 · 基部青绿",new Color(.31f,.47f,.16f),.045f),
                NurseryMat("天然岸坡 · 中层黄绿",new Color(.43f,.59f,.20f),.05f),
                NurseryMat("天然岸坡 · 新叶亮绿",new Color(.59f,.71f,.28f),.055f)
            };
            var stems=NurseryMat("天然岸坡 · 植物枝茎",new Color(.30f,.38f,.14f),.04f);
            var flowerSources=new[]{Resources.Load<GameObject>("CoastReference/Geology/flowers_cream"),Resources.Load<GameObject>("CoastReference/Geology/flowers_gold")};
            var flowerBounds=new[]{ReferenceFlowerBounds(flowerSources[0]),ReferenceFlowerBounds(flowerSources[1])};
            // Adjacent, overlapping small lobes form seven legible green masses.
            // A staggered grid with a scalloped boundary provides continuous coverage;
            // no uniform line of bouquets or global random placement is used.
            var drifts=new (string name,Vector4 area,float yaw)[]{
                ("苗圃西坡叶丛",new(-13.55f,-16.40f,1.48f,2.12f),-12),
                ("苗圃南缘叶丛",new(-15.80f,-15.10f,1.18f,.87f),15),
                ("溪桥前绿色肩",new(-6.65f,-15.70f,1.20f,1.35f),-15),
                ("洞东低岸缝",new(12.70f,-23.30f,.57f,1.30f),-8),
                ("海口内湾草丛",new(21.65f,-13.10f,1.20f,.55f),4),
                ("码头东侧岸草",new(30.80f,-13.90f,1.25f,.80f),12),
                ("工坊外岸低叶",new(32.45f,-11.85f,.68f,1.0f),-15)
            };
            int roots=0,shrubs=0,flowers=0;
            for(int drift=0;drift<drifts.Length;drift++)
            {
                var spec=drifts[drift];int driftRoots=0,driftFlowers=0;
                var rotation=Quaternion.Euler(0,spec.yaw,0);
                int rows=Mathf.CeilToInt(spec.area.w*2/.28f),cols=Mathf.CeilToInt(spec.area.z*2/.30f);
                for(int row=0;row<=rows;row++)
                for(int col=0;col<=cols;col++)
                {
                    float u=-1+2f*col/cols+(row%2==0?.038f:-.038f);
                    float v=-1+2f*row/rows;
                    float edge=.88f+.10f*Mathf.Sin(3.2f*u+drift)+.06f*Mathf.Cos(v*7.1f);
                    if(u*u+v*v>edge)continue;
                    int seed=701+drift*317+row*31+col*17;
                    var offset=rotation*new Vector3(u*spec.area.z+.045f*Mathf.Sin(seed),0,v*spec.area.w+.035f*Mathf.Cos(seed*.7f));
                    var p=new Vector2(spec.area.x+offset.x,spec.area.y+offset.z);
                    if(!Support(p,.17f,out var at,out _))continue;
                    ReferenceBorderRosette(foliage,at,1.35f+.16f*Mathf.Sin(seed*.3f),seed,leaves);
                    roots++;driftRoots++;
                    if((row+col*2)%7==0&&u*u+v*v<.55f&&Support(p,.29f,out var bush,out _))
                    {ReferenceBorderShrub(foliage,bush,1.20f,seed,leaves,stems);shrubs++;}
                    if((row*3+col)%11!=0||flowers>=38||!Support(p,.29f,out var flowerAt,out var flowerNormal))continue;
                    int kind=(drift+row)%3==0?1:0;var flower=flowerSources[kind];if(flower==null)continue;
                    var bounds=flowerBounds[kind];
                    float sx=.70f/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z));
                    float sy=(kind==1?.44f:.38f)/Mathf.Max(.01f,bounds.size.y);
                    var turn=Quaternion.FromToRotation(Vector3.up,Vector3.Slerp(Vector3.up,flowerNormal,.12f))*Quaternion.Euler(0,seed*17,0);
                    var atFlower=flowerAt-turn*new Vector3(bounds.center.x*sx,bounds.min.y*sy+.022f,bounds.center.z*sx);
                    ReferenceMergeFlower(flowerBatches,flower,Matrix4x4.TRS(atFlower,turn,new Vector3(sx,sy,sx)));
                    flowers++;driftFlowers++;
                }
                Debug.Log($"Natural bank planting {spec.name}: {driftRoots} connected ground rosettes, {driftFlowers} accent flower clumps");
            }
            foreach(var pair in foliage)
            {
                if(pair.Value.Points.Count==0)continue;
                var mesh=new Mesh{name="天然岸坡 · 连片叶丛合批",indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(pair.Value.Points);mesh.SetTriangles(pair.Value.Faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var go=Group(pair.Key.name,root).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var pair in flowerBatches)
            {
                var mesh=new Mesh{name="天然岸坡 · 真实花簇合批",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                var go=Group(pair.Key.name,root).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            Debug.Log($"Natural banks complete: {beds}/{seams.Length} surveyed closed beds, {roots} ground plants, {shrubs} low shrubs, {flowers}/38 flowers; {root.childCount} batches. Existing terrain, routes and saved plot unchanged.");
        }

        static bool ReferenceNaturalBankPlantClear(Vector2 p,float radius)
        {
            // The compact east bank lies outside the actual cave shell, but the
            // general planting pass reserves a wider area for its own roof trails.
            // Use the true mouth clearance here while keeping all other exclusions.
            if(p.x<10.4f+radius&&p.x>-5.4f-radius&&p.y>-29.9f-radius&&p.y< -17.1f+radius)return false;
            if(p.x>4.25f-radius&&p.x<19.2f+radius&&Mathf.Abs(p.y)<6.15f+radius)return false;
            foreach(var road in ReferenceRoads)
            for(int i=0;i<road.points.Length-1;i++)
                if(SegmentDistanceSquared(p,road.points[i],road.points[i+1],out _)<Mathf.Pow(road.width+radius+.28f,2))return false;
            for(int i=0;i<ReferenceStream.Length-1;i++)
            {
                float d=SegmentDistanceSquared(p,ReferenceStream[i],ReferenceStream[i+1],out float t);
                if(d<Mathf.Pow(Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t)+radius+.32f,2))return false;
            }
            if(ReferenceShoreDistance(p)<.42f+radius)return false;
            if(Mathf.Pow((p.x-ReferencePondX)/(2.38f+radius),2)+Mathf.Pow((p.y-ReferencePondZ)/(1.83f+radius),2)<1)return false;
            // All seven authored banks are below the crop boxes, greenhouse platform,
            // workshop frontage and the immutable expansion. This also rejects a
            // changed anchor if a later layout revision accidentally moves it there.
            if(p.y>-13.8f&&p.x<0)return false;
            if(p.y>-10.6f&&p.x>19)return false;
            return true;
        }
    }
}
