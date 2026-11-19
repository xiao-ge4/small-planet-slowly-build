using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceTransitionsBuilt;

        // Call once after the terrain, solid rocks and existing planting have been
        // assembled. The supplied parent should be the world-space details group.
        void BuildReferencePlantTransitions(Transform parent)
        {
            if(referenceTransitionsBuilt||parent==null)return;
            referenceTransitionsBuilt=true;
            var root=Group("墙根—溪岸—岩帽 · 低层种植连接",parent);
            var batches=new Dictionary<Material,NurseryGeometry>();
            var leaves=new[]{
                NurseryMat("连接层 · 阴处小叶",new Color(.27f,.40f,.14f),.035f),
                NurseryMat("连接层 · 青绿细叶",new Color(.39f,.53f,.19f),.035f),
                NurseryMat("连接层 · 嫩梢",new Color(.53f,.65f,.27f),.035f)
            };
            var moss=Paint("连接层 · 低伏苔草",new Color(.30f,.43f,.16f),.02f,.08f);
            moss.SetFloat("_Cull",0);
            var stem=Paint("连接层 · 细藤茎",new Color(.24f,.31f,.12f),.025f,.04f);
            var support=new List<Collider>();var blockers=new List<Bounds>();
            foreach(var collider in transform.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                bool apronContainer=false;
                for(var ancestor=collider.transform;ancestor!=null&&ancestor!=transform;ancestor=ancestor.parent)
                    if(ancestor.name.Contains("温室东缘")){apronContainer=true;break;}
                if(apronContainer){blockers.Add(collider.bounds);continue;}
                if(collider is BoxCollider && collider.gameObject.layer==ObstacleLayer)blockers.Add(collider.bounds);
                if(collider is MeshCollider && (collider.gameObject.layer==FloorLayer||collider.gameObject.layer==ObstacleLayer))support.Add(collider);
            }
            Physics.SyncTransforms();
            int roots=0,grass=0,tendrils=0,cushions=0,carpetFaces=0;

            bool Clear(Vector3 p,float radius,float height=.28f)
            {
                if(p.x>4.4f-radius&&p.x<19.05f+radius&&Mathf.Abs(p.z)<5.95f+radius)return false;
                if(ReferenceUpperGardenEntryClear(p.x,p.z,radius+.6f))return false;
                var house=Quaternion.Inverse(Quaternion.Euler(0,-22,0))*(p-ReferenceGreenhouse);
                if(Mathf.Abs(house.x)<4.054f+radius+.60f&&Mathf.Abs(house.z)<2.846f+radius+.60f)return false;
                foreach(var road in ReferenceRoads)for(int i=1;i<road.points.Length;i++)
                {
                    float d=SegmentDistanceSquared(new Vector2(p.x,p.z),road.points[i-1],road.points[i],out float t);
                    float grade=Mathf.Lerp(road.points[i-1].y,road.points[i].y,t);
                    // A roof can carry leaves above a cave route, but vines cannot
                    // enter its real 2.2 m headroom or the extra 60 cm side clearance.
                    if(p.y+height>grade-.30f&&p.y<grade+2.2f&&d<Mathf.Pow(road.width+radius+.60f,2))return false;
                }
                if(SegmentDistanceSquared(new Vector2(p.x,p.z),ReferenceBridgeStart,ReferenceBridgeEnd,out _) <
                    Mathf.Pow(1.625f+radius+.60f,2))return false;
                for(int i=1;i<ReferenceStream.Length;i++)
                {
                    float d=SegmentDistanceSquared(new Vector2(p.x,p.z),ReferenceStream[i-1],ReferenceStream[i],out float t);
                    if(d<Mathf.Pow(Mathf.Lerp(ReferenceStreamHalfWidths[i-1],ReferenceStreamHalfWidths[i],t)+radius+.22f,2))return false;
                }
                if(p.z>-13.9f&&p.z<4.5f&&Mathf.Abs(p.x+17.1f)<.55f+radius+.20f)return false;
                foreach(float z in ReferenceNurseryCanalZ)
                    if(p.x>-17.7f&&p.x<-.5f&&Mathf.Abs(p.z-z)<.46f+radius+.20f)return false;
                var world=parent.TransformPoint(p);
                foreach(var b in blockers)
                    if(world.x+radius>b.min.x&&world.x-radius<b.max.x&&world.z+radius>b.min.z&&world.z-radius<b.max.z&&
                        world.y+height>b.min.y&&world.y<b.max.y+.10f)return false;
                return true;
            }

            bool Surface(Vector2 p,float footprint,out Vector3 at,out Vector3 normal,out Collider owner)
            {
                at=normal=default;owner=null;
                var ray=new Ray(parent.TransformPoint(new Vector3(p.x,22,p.y)),-parent.up);
                float best=float.NegativeInfinity;
                foreach(var collider in support)
                {
                    if(!collider.Raycast(ray,out var hit,30))continue;
                    var n=parent.InverseTransformDirection(hit.normal).normalized;
                    var local=parent.InverseTransformPoint(hit.point);
                    if(n.y<.48f||local.y<best||local.y<ReferenceSeaLevel+.38f)continue;
                    at=local;normal=n;owner=collider;best=local.y;
                }
                return owner!=null && Clear(at,footprint);
            }

            void Cushion(Vector3 at,Vector3 normal,int seed)
            {
                var mesh=PlantMaterial(batches,moss);
                var points=new Vector3[8];var valid=new bool[8];
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI*.25f;float radius=.295f+.025f*Mathf.Sin(seed+i*1.9f);
                    var p=new Vector2(at.x+Mathf.Cos(angle)*radius,at.z+Mathf.Sin(angle)*radius);
                    if(!Surface(p,.02f,out var edge,out var n,out _)||Mathf.Abs(edge.y-at.y)>.50f)continue;
                    points[i]=edge+n*.018f;valid[i]=true;
                }
                int made=0;
                for(int i=0;i<8;i++)
                {
                    int next=(i+1)%8;if(!valid[i]||!valid[next])continue;
                    var a=at+normal*.025f;var b=points[next];var c=points[i];var mid=(a+b+c)/3;
                    // Each real slope patch survives independently. A missing
                    // ledge no longer discards all moss, while a rock gap cannot
                    // be bridged by an unsupported green triangle.
                    if(!Surface(new Vector2(mid.x,mid.z),.02f,out var supportAt,out _,out _)||Mathf.Abs(supportAt.y-mid.y)>.11f)continue;
                    int first=mesh.Points.Count;mesh.Points.AddRange(new[]{a,b,c});mesh.Faces.AddRange(new[]{first,first+1,first+2});made++;
                }
                if(made>0)cushions++;carpetFaces+=made;
            }

            void Tendril(Vector3 cap,Collider rock,int seed)
            {
                var last=cap;int made=0;
                for(int j=0;j<11;j++)
                {
                    float depth=(j+1)*.085f;
                    var sample=cap+new Vector3(.035f*Mathf.Sin(j*.6f+seed),-depth,0);
                    var ray=new Ray(parent.TransformPoint(sample+Vector3.back*2.4f),parent.forward);
                    if(!rock.Raycast(ray,out var hit,4.8f))break;
                    var p=parent.InverseTransformPoint(hit.point)+parent.InverseTransformDirection(hit.normal)*.024f;
                    if(Mathf.Abs(p.z-cap.z)>1.05f||!Clear(p,.14f,.16f)||(j>0&&Vector3.Distance(last,p)>.50f))break;
                    // Begin in the actual exposed rock crevice. Never stretch a
                    // floating stem from an inland cap point to the cliff face.
                    if(j>0)BloomStem(PlantMaterial(batches,stem),last,p,.006f);
                    for(int side=-1;side<=1;side+=2)
                        ReferenceRoundedLeaf(PlantMaterial(batches,leaves[(seed+j)%3]),p,180+side*(48+j*3),-.16f,.145f,.047f,-.012f);
                    last=p;made++;
                }
                if(made>3)tendrils++;
            }

            // Broad and narrow lobes are deliberately separated by small bare
            // stone intervals. No upper reserve, playable plot or whole-map fill.
            var bands=new (string name,float width,bool trail,Vector2[] line)[]{
                ("苗圃南缘左下岩肩",.60f,false,new[]{new Vector2(-18.0f,-15.35f),new(-16.6f,-16.1f),new(-15.0f,-16.7f),new(-13.0f,-16.4f)}),
                ("苗圃南缘右",.48f,false,new[]{new Vector2(-8.5f,-15.25f),new(-7.2f,-15.55f),new(-5.7f,-15.7f),new(-4.7f,-15.6f)}),
                ("温室外台沿底层",.38f,false,new[]{new Vector2(-8.0f,5.8f),new(-7.5f,7.3f),new(-7.6f,9.4f),new(-7.6f,10.65f),new(-7.45f,12.6f),new(-7.85f,14.4f)}),
                ("溪轮下游西岸",.43f,false,new[]{new Vector2(-3.5f,-11.9f),new(-2.6f,-13.7f),new(-1.2f,-16.3f)}),
                ("溪轮下游东岸",.45f,false,new[]{new Vector2(1.15f,-10.5f),new(2.2f,-12.5f),new(3.1f,-14.8f)}),
                ("洞顶左草帽",.78f,true,new[]{new Vector2(-4.5f,-23.4f),new(-3.8f,-24.1f),new(-2.7f,-24.45f),new(-1.35f,-24.2f)}),
                ("洞顶右草帽",.84f,true,new[]{new Vector2(2.4f,-24.1f),new(3.8f,-24.3f),new(5.3f,-24.05f),new(7.2f,-23.85f)}),
                ("洞侧岩帽回折",.37f,true,new[]{new Vector2(7.3f,-23.85f),new(8.45f,-22.9f),new(9.1f,-21.9f)})
            };
            var occupied=new HashSet<Vector2Int>();
            for(int band=0;band<bands.Length;band++)
            {
                var b=bands[band];int accepted=0;float distance=0;
                for(int segment=1;segment<b.line.Length;segment++)
                {
                    var a=b.line[segment-1];var end=b.line[segment];var dir=(end-a).normalized;var side=new Vector2(-dir.y,dir.x);
                    float length=Vector2.Distance(a,end);int samples=Mathf.CeilToInt(length/.245f);
                    for(int step=0;step<samples;step++)
                    {
                        float t=(step+.5f)/samples,along=distance+t*length;
                        float pulse=Mathf.Sin(along*1.18f+band*.9f);
                        // All lanes pause together through the bare-stone gap.
                        // Surviving communities swell into a broad centre and
                        // taper to tiny satellite leaves instead of a green ribbon.
                        if(pulse<-.53f)continue;
                        float lobe=.38f+.57f*Mathf.Clamp01((pulse+.35f)/1.35f)+.08f*Mathf.Cos(along*2.1f);
                        float width=b.width*lobe;
                        for(int lane=-2;lane<=2;lane++)
                        {
                            int seed=1043+band*311+segment*79+step*17+lane+2;
                            float lateral=lane*width*.43f+.025f*Mathf.Sin(seed*.8f);
                            if(Mathf.Abs(lateral)>width)continue;
                            if(lane!=0&&Mathf.Sin(along*1.62f+band)<-.70f)continue;
                            var p=Vector2.Lerp(a,end,t)+side*lateral;
                            var cell=new Vector2Int(Mathf.RoundToInt(p.x/.18f),Mathf.RoundToInt(p.y/.18f));
                            if(occupied.Contains(cell)||!Surface(p,.34f,out var at,out var normal,out var owner))continue;
                            occupied.Add(cell);at+=normal*.022f;Cushion(at,normal,seed);
                            float leafScale=.70f+.42f*Mathf.Clamp01((pulse+.3f)/1.3f);
                            for(int leaf=0;leaf<5;leaf++)
                                ReferenceRoundedLeaf(PlantMaterial(batches,leaves[(seed+leaf)%3]),at,
                                    seed*17+leaf*137.5f,.11f+(leaf%2)*.04f,(.17f+.015f*(leaf%3))*leafScale,.045f*leafScale,.018f);
                            if(seed%3==0)
                            {
                                for(int blade=0;blade<4;blade++)BloomBlade(PlantMaterial(batches,leaves[(blade+band)%3]),at,
                                    seed*13+blade*101,.58f,.22f+.035f*(blade%2),.013f,.022f);
                                grass++;
                            }
                            if(b.trail&&seed%23==0)Tendril(at,owner,seed);
                            roots++;accepted++;
                        }
                    }
                    distance+=length;
                }
                Debug.Log($"Plant transition {b.name}: {accepted} low roots");
            }
            foreach(var pair in batches)
            {
                if(pair.Value.Points.Count==0)continue;
                var mesh=new Mesh{name=pair.Key.name,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(pair.Value.Points);mesh.SetTriangles(pair.Value.Faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var item=Group(pair.Key.name,root).gameObject;item.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            Debug.Log($"Plant transitions: {roots} connected low roots, {cushions} conforming moss cushions / {carpetFaces} supported faces, {grass} fine grass accents, {tendrils} supported hanging trails; {root.childCount} meshes; no colliders.");
        }
    }
}
