using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceRockFacesBuilt;

        // Closed source volumes embedded in the existing terrain. This pass never removes
        // terrain, edits a saved plot or creates a rock veneer hovering over a soil face.
        // Install after the other reference rock assets, before planting and NavMesh baking.
        void BuildReferenceRockFaces()
        {
            if(referenceRockFacesBuilt || details==null)return;
            referenceRockFacesBuilt=true;
            var bedding=Resources.Load<GameObject>("CoastReference/Geology/cliff_bedding");
            var toeSource=Resources.Load<GameObject>("CoastReference/Geology/coast_rock");
            if(bedding==null)return;
            var geology=ReferenceForeshoreGeometry.Read(bedding);
            var toeGeometry=toeSource==null?null:ReferenceForeshoreGeometry.Read(toeSource);
            if(geology==null)return;
            var root=Group("参考资产 · Geology/接入陆地的台下岩层",details);
            var batches=new Dictionary<Material,List<CombineInstance>>();
            var faceMaterials=new Dictionary<Material,Material>();
            var toeMaterials=new Dictionary<Material,Material>();
            var occupied=new List<(Vector2 point,float span)>();
            long triangleCount=0;
            int mothers=0,caps=0,toes=0;
            long CountTriangles(GameObject model)
            {
                long count=0;
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                    if(filter.sharedMesh!=null)for(int s=0;s<filter.sharedMesh.subMeshCount;s++)count+=(long)filter.sharedMesh.GetIndexCount(s)/3;
                return count;
            }
            long bedTriangles=CountTriangles(bedding),toeTriangles=toeSource==null?0:CountTriangles(toeSource);
            const long triangleBudget=240000;
            var zones=new (Vector4 rect,int quota,long budget,bool southFront)[]{
                (new(-9.4f,-4.7f,-28.0f,-14.0f),2,45000,false), // back of west cave shoulder; front is composed below
                (new(10.5f,15.4f,-26.2f,-14.4f),6,45000,false), // east cave shoulder, outside stairs
                (new(19.5f,34.5f,-15.0f,2.0f),8,45000,false),  // workshop toe and east shoreline
                (new(-24.0f,-18.6f,-13.4f,14.0f),10,45000,false) // exposed nursery coast
            };
            int serial=181;
            foreach(var zone in zones)
            {
                long zoneStart=triangleCount;
                var placementNotes=new List<string>();
                var candidates=new List<(Vector2 point,Vector2 outward,float slope)>();
                for(float x=zone.rect.x+.28f;x<zone.rect.y;x+=.68f)
                for(float z=zone.rect.z+.31f;z<zone.rect.w;z+=.81f)
                {
                    // The former two generic outcrops here emerged through the
                    // composed pond/cave beds as small unrelated shards. The full
                    // shoulder below owns this foreground volume instead.
                    if(x>-10.4f && x< -4.7f && z< -23.3f)continue;
                    float h=ReferenceHeight(x,z);
                    if(h<ReferenceSeaLevel+.12f)continue;
                    float dx=(ReferenceHeight(x+.34f,z)-ReferenceHeight(x-.34f,z))/.68f;
                    float dz=(ReferenceHeight(x,z+.34f)-ReferenceHeight(x,z-.34f))/.68f;
                    var gradient=new Vector2(dx,dz);
                    if(gradient.magnitude<(zone.southFront?.62f:.92f))continue;
                    var point=new Vector2(x,z);var outward=-gradient.normalized;
                    float drop=ReferenceHeight(x-outward.x*.78f,z-outward.y*.78f)-
                        ReferenceHeight(x+outward.x*.78f,z+outward.y*.78f);
                    if(drop<(zone.southFront?.62f:.78f))continue;
                    // Prefer a clear wall section over an isolated narrow spike at a junction.
                    candidates.Add((point,outward,Mathf.Min(5,drop)+.12f*Mathf.Sin(x*2.3f+z)));
                }
                candidates.Sort((a,b)=>b.slope.CompareTo(a.slope));
                int made=0;
                foreach(var candidate in candidates)
                {
                    if(made>=zone.quota || triangleCount+bedTriangles>triangleBudget || triangleCount-zoneStart+bedTriangles>zone.budget)break;
                    bool overlaps=false;
                    foreach(var other in occupied)
                        if(Vector2.Distance(other.point,candidate.point)<other.span*.62f){overlaps=true;break;}
                    if(overlaps)continue;
                    float originalWidth=(zone.southFront?3.90f:3.45f)+.35f*Mathf.Sin(serial*1.27f);
                    float originalDepth=(zone.southFront?2.0f:1.70f)+.15f*Mathf.Cos(serial*.83f);
                    var outward=candidate.outward;
                    float facing=Mathf.Atan2(outward.x,outward.y)*Mathf.Rad2Deg;
                    var rotation=Quaternion.Euler(0,facing-geology.nativeFacing,0);
                    var along3=rotation*Vector3.right;
                    var along=new Vector2(along3.x,along3.z);
                    var forward3=rotation*Vector3.forward;
                    var forward=new Vector2(forward3.x,forward3.z);
                    bool installed=false;
                    for(int fit=0;fit<3;fit++)
                    {
                        float factor=fit==0?1:fit==1?.84f:.70f;
                        float width=originalWidth*factor,depth=originalDepth*factor;
                        // The broad back sits well inside the hill; the broken front exposes
                        // a real thickness rather than duplicating the terrain's exact surface.
                        var center=candidate.point+outward*(zone.southFront?.08f:-.12f);
                        var front=center+outward*(depth*.46f);
                        var back=center-outward*(depth*.44f);
                        float frontY=ReferenceHeight(front.x,front.y);
                        float backY=ReferenceHeight(back.x,back.y);
                        float floor=frontY;
                        for(int sign=-1;sign<=1;sign+=2)
                        {
                            var edge=front+along*(width*.34f*sign);
                            floor=Mathf.Min(floor,ReferenceHeight(edge.x,edge.y));
                        }
                        float bottom=floor-.30f;
                        // A wide, low bed reads as geological layering. Stretching this source
                        // vertically into the whole cliff height produced isolated spear faces.
                        float exposedTop=Mathf.Min(backY-.12f,bottom+Mathf.Min(zone.southFront?1.65f:2.15f,width*.66f));
                        float top=exposedTop;
                        if(zone.southFront)top=Mathf.Min(top,-.12f); // below the pond-side pavement
                        top=ReferenceRockTopBelowRoads(center,along,forward,width*.5f,depth*.5f,top,zone.southFront);
                        if(!ReferenceRockFaceClear(center,along,forward,width*.5f,depth*.5f,top,zone.southFront))continue;
                        float height=top-bottom;
                        if(height<(zone.southFront?.72f:.90f) || top<ReferenceSeaLevel+.40f)continue;
                        var scale=new Vector3(width/geology.bounds.size.x,height/geology.bounds.size.y,depth/geology.bounds.size.z);
                        var pivot=new Vector3(geology.bounds.center.x,geology.bounds.min.y,geology.bounds.center.z);
                        var position=new Vector3(center.x,bottom,center.y)-rotation*Vector3.Scale(scale,pivot);
                        ReferenceRockFaceMerge(batches,faceMaterials,bedding,Matrix4x4.TRS(position,rotation,scale),"cliff_bedding");
                        triangleCount+=bedTriangles;mothers++;made++;
                        placementNotes.Add($"({center.x:F1},{center.y:F1}) y{bottom:F1}..{top:F1}");
                        occupied.Add((candidate.point,width));installed=true;

                        // Where the escarpment is higher than one low bed, put a second,
                        // narrower volume behind it. It overlaps the first by .32m and is
                        // embedded uphill, rather than increasing a single rock's aspect ratio.
                        if(backY-.12f>top+.58f && triangleCount+bedTriangles<=triangleBudget && triangleCount-zoneStart+bedTriangles<=zone.budget)
                        {
                            var upperCenter=center-outward*(depth*.24f)+along*(serial%2==0?.16f:-.16f);
                            float upperWidth=width*.86f,upperDepth=depth*.72f;
                            float upperBottom=top-.32f;
                            float upperTop=Mathf.Min(backY-.12f,upperBottom+1.24f);
                            if(zone.southFront)upperTop=Mathf.Min(upperTop,-.12f);
                            upperTop=ReferenceRockTopBelowRoads(upperCenter,along,forward,upperWidth*.5f,upperDepth*.5f,upperTop,zone.southFront);
                            if(upperTop-upperBottom>.48f && ReferenceRockFaceClear(upperCenter,along,forward,upperWidth*.5f,upperDepth*.5f,upperTop,zone.southFront))
                            {
                                var upperScale=new Vector3(upperWidth/geology.bounds.size.x,(upperTop-upperBottom)/geology.bounds.size.y,upperDepth/geology.bounds.size.z);
                                var upperPosition=new Vector3(upperCenter.x,upperBottom,upperCenter.y)-rotation*Vector3.Scale(upperScale,pivot);
                                ReferenceRockFaceMerge(batches,faceMaterials,bedding,Matrix4x4.TRS(upperPosition,rotation,upperScale),"cliff_bedding");
                                triangleCount+=bedTriangles;caps++;
                            }
                        }

                        // A few asymmetrical foot rocks tie the bed into the lower ledge.
                        // Every foot has its own ground survey and full-footprint clearance.
                        if(toeSource!=null && toeGeometry!=null && (zone.southFront?serial%5==0:serial%3!=1) && triangleCount+toeTriangles<=triangleBudget && triangleCount-zoneStart+toeTriangles<=zone.budget)
                        {
                            var foot=center+outward*(depth*.50f+.22f)+along*(width*.22f*(serial%2==0?1:-1));
                            float yaw=facing-35+(serial%3)*23;
                            var toeRotation=Quaternion.Euler(0,yaw,0);
                            var tx=toeRotation*Vector3.right;var tz=toeRotation*Vector3.forward;
                            float toeWidth=.95f+.15f*(serial%3),toeDepth=.78f,toeHeight=.66f;
                            float y=ReferenceHeight(foot.x,foot.y);
                            if(ReferenceRockFaceClear(foot,new Vector2(tx.x,tx.z),new Vector2(tz.x,tz.z),toeWidth*.5f,toeDepth*.5f,y-.28f+toeHeight,zone.southFront))
                            {
                                if(y>ReferenceSeaLevel-.15f && y<top-.30f)
                                {
                                    var ts=new Vector3(toeWidth/toeGeometry.bounds.size.x,toeHeight/toeGeometry.bounds.size.y,toeDepth/toeGeometry.bounds.size.z);
                                    var tp=new Vector3(toeGeometry.bounds.center.x,toeGeometry.bounds.min.y,toeGeometry.bounds.center.z);
                                    var at=new Vector3(foot.x,y-.28f,foot.y)-toeRotation*Vector3.Scale(ts,tp);
                                    ReferenceRockFaceMerge(batches,toeMaterials,toeSource,Matrix4x4.TRS(at,toeRotation,ts),"coast_rock");
                                    triangleCount+=toeTriangles;toes++;
                                }
                            }
                        }
                        break;
                    }
                    serial++;
                    if(!installed)continue;
                }
                Debug.Log($"Reference rock face zone {zone.rect}: {made} groups, {triangleCount-zoneStart}/{zone.budget} triangles; "+string.Join("; ",placementNotes));
            }

            // The pond-to-cave shoulder is a broad 0 -> -3.3m slope, not the steep
            // coastal edge picked by the generic gradient search. The earlier search
            // spent its budget at the waterline and left this whole upper face bare.
            // The source's fractured wide face is local +Z, with 8 x 4.5 x 2.5m
            // actual bounds. Facing it east showed only its narrow plain end in the
            // south camera. Three broad south-east-facing beds now expose their
            // real bedding, with the upper back extending to z=-25.26 instead of
            // stopping at -27.2. A two-level return joins the cave shoulder.
            // All bounds and heights were queried against the actual Unity source
            // and current terrain. The unchanged west-coast feet remain below.
            // No flattening, triangle removal, material-only veneer or save edits.
            var pondBeds=new (string label,Vector2 center,Vector2 outward,float width,float depth,float bottom,float top)[]{
                ("池洞北伸上岩肩",new(-7.25f,-27.40f),new(.30f,-.954f),3.8f,3.3f,-1.65f,.18f),
                ("池东横向中层",new(-7.00f,-29.45f),new(.30f,-.954f),4.0f,2.85f,-2.90f,-.80f),
                ("池东宽低岩脚",new(-6.70f,-30.90f),new(.28f,-.96f),4.0f,2.65f,-3.65f,-1.20f),
                ("洞西北嵌上层",new(-6.20f,-25.90f),new(.20f,-.98f),2.8f,1.9f,-1.30f,.16f),
                ("洞西低位转折层",new(-6.02f,-26.30f),new(.25f,-.968f),2.6f,2.1f,-3.20f,-1.00f),
                ("池西海岸下层",new(-18.45f,-29.50f),new(-1,-.10f),3.7f,1.75f,-3.45f,-1.35f),
                ("池西海岸上层",new(-18.25f,-29.30f),new(-1,-.08f),3.3f,1.65f,-1.70f,-.12f),
                ("斜阶西侧下层",new(-17.85f,-33.15f),new(-.94f,-.34f),2.7f,2.0f,-3.25f,-1.30f),
                ("斜阶西侧上层",new(-17.95f,-32.95f),new(-.94f,-.34f),2.6f,1.9f,-1.65f,-.12f),
                ("斜阶外侧低岩肩",new(-15,-35.70f),new(-.6f,-.8f),3.3f,2.2f,-3.25f,-.65f)
            };
            const long pondBedBudget=60000;
            long pondTriangles=0;int pondInstalled=0;
            foreach(var bed in pondBeds)
            {
                if(pondInstalled>=11||pondTriangles+bedTriangles>pondBedBudget||triangleCount+bedTriangles>triangleBudget)break;
                var outward=bed.outward.normalized;
                float facing=Mathf.Atan2(outward.x,outward.y)*Mathf.Rad2Deg;
                var rotation=Quaternion.Euler(0,facing-geology.nativeFacing,0);
                var xAxis=rotation*Vector3.right;var zAxis=rotation*Vector3.forward;
                var right=new Vector2(xAxis.x,xAxis.z);var forward=new Vector2(zAxis.x,zAxis.z);
                float top=ReferenceRockTopBelowRoads(bed.center,right,forward,bed.width*.5f,bed.depth*.5f,bed.top,true);
                if(top-bed.bottom<.65f||!ReferenceRockFaceClear(bed.center,right,forward,bed.width*.5f,bed.depth*.5f,top,true))
                {
                    Debug.LogWarning($"Surveyed pond bed rejected by complete route/pond/shore bounds: {bed.label}, top {bed.top:F2}->{top:F2}");
                    continue;
                }
                var scale=new Vector3(bed.width/geology.bounds.size.x,(top-bed.bottom)/geology.bounds.size.y,bed.depth/geology.bounds.size.z);
                var pivot=new Vector3(geology.bounds.center.x,geology.bounds.min.y,geology.bounds.center.z);
                var position=new Vector3(bed.center.x,bed.bottom,bed.center.y)-rotation*Vector3.Scale(scale,pivot);
                ReferenceRockFaceMerge(batches,faceMaterials,bedding,Matrix4x4.TRS(position,rotation,scale),"cliff_bedding");
                triangleCount+=bedTriangles;pondTriangles+=bedTriangles;pondInstalled++;mothers++;
                var front=bed.center+outward*(bed.depth*.48f);
                Debug.Log($"Surveyed pond bed {bed.label}: ({bed.center.x:F2},{bed.center.y:F2}), y {bed.bottom:F2}..{top:F2}; front soil {ReferenceHeight(front.x,front.y):F2}");
            }
            Debug.Log($"Surveyed pond shoulder: {pondInstalled}/{pondBeds.Length} closed beds, {pondTriangles}/{pondBedBudget} triangles; shared material batches.");
            foreach(var pair in batches)
            {
                var mesh=new Mesh{name="闭合层裂岩岸合批",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                var go=Group(pair.Key.name,root).gameObject;go.layer=ObstacleLayer;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
                // Surface queries for later planting see these actual closed rock volumes.
                // Full-volume route checks above keep the new colliders out of walk corridors.
                go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            Debug.Log($"Reference rock faces: {mothers} embedded parent beds, {caps} overlapping upper beds, {toes} grounded foot rocks, {triangleCount} triangles, {root.childCount} batched renderers; original terrain and saved plot unchanged.");
        }

        static bool ReferenceRockFaceClear(Vector2 center,Vector2 right,Vector2 forward,float halfWidth,float halfDepth,float top,bool southFront=false)
        {
            bool HitsRectangle(float minX,float maxX,float minZ,float maxZ)
            {
                var target=new Vector2((minX+maxX)*.5f,(minZ+maxZ)*.5f);
                var extent=new Vector2((maxX-minX)*.5f,(maxZ-minZ)*.5f);
                var delta=target-center;
                foreach(var axis in new[]{Vector2.right,Vector2.up,right,forward})
                {
                    float ours=Mathf.Abs(Vector2.Dot(right,axis))*halfWidth+Mathf.Abs(Vector2.Dot(forward,axis))*halfDepth;
                    float theirs=Mathf.Abs(axis.x)*extent.x+Mathf.Abs(axis.y)*extent.y;
                    if(Mathf.Abs(Vector2.Dot(delta,axis))>ours+theirs)return false;
                }
                return true;
            }
            // Only the explicitly budgeted skirt pass may enter the old foreshore
            // reserve. Its complete source bounds, water basin and both paths are
            // checked below; the former blanket exclusion left all its soil bare.
            if(!southFront && HitsRectangle(-100,-4,-100,-30))return false;
            if(HitsRectangle(4.25f,19.2f,-6.15f,6.15f))return false;
            // Include the submerged bowl and stone coping. A support volume below
            // the bowl is harmless; nothing may protrude into its floor or rim.
            if(top>-.73f && HitsRectangle(-14.6f,-9.8f,-32.5f,-28.7f))return false;
            // The cave interior and its mouth stay open all the way to the low tide pool.
            if(HitsRectangle(-4.35f,10.15f,-29,-16.9f))return false;
            // The expanded workshop court is now one continuous paved precinct. Outcrops
            // may support its skirt below, but cannot emerge as isolated rocks on the paving.
            if(top>ReferenceBrickCourt.y-.28f && HitsRectangle(
                ReferenceBrickCourt.x-ReferenceBrickCourtHalf.x+.35f,ReferenceBrickCourt.x+ReferenceBrickCourtHalf.x-.35f,
                ReferenceBrickCourt.z-ReferenceBrickCourtHalf.y+.35f,ReferenceBrickCourt.z+ReferenceBrickCourtHalf.y-.35f))return false;
            if(HitsRectangle(22.0f,32.85f,-8.4f,.55f))return false; // workshop building
            if(HitsRectangle(-21.2f,-8.8f,4.6f,13.2f))return false; // greenhouse
            foreach(var road in ReferenceRoads)
            for(int i=0;i<road.points.Length-1;i++)
            {
                var a=road.points[i];var b=road.points[i+1];
                if(!ReferenceRockSegmentInterval(center,right,forward,halfWidth+road.width+.48f,halfDepth+road.width+.48f,
                    new Vector2(a.x,a.z),new Vector2(b.x,b.z),out float enter,out float exit))continue;
                float lowest=southFront?Mathf.Min(Mathf.Lerp(a.y,b.y,enter),Mathf.Lerp(a.y,b.y,exit)):Mathf.Min(a.y,b.y);
                if(top>lowest-.35f)return false;
            }
            for(int i=0;i<ReferenceStream.Length-1;i++)
            {
                float water=Mathf.Max(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1])+.42f;
                if(ReferenceRockSegmentHits(center,right,forward,halfWidth+water,halfDepth+water,
                    new Vector2(ReferenceStream[i].x,ReferenceStream[i].z),new Vector2(ReferenceStream[i+1].x,ReferenceStream[i+1].z)))return false;
            }
            foreach(var corner in new[]{center+right*halfWidth+forward*halfDepth,center+right*halfWidth-forward*halfDepth,
                center-right*halfWidth+forward*halfDepth,center-right*halfWidth-forward*halfDepth})
                if(ReferenceShoreDistance(corner)<-.15f)return false;
            return true;
        }

        static float ReferenceRockTopBelowRoads(Vector2 center,Vector2 right,Vector2 forward,float halfWidth,float halfDepth,float top,bool southFront=false)
        {
            // A real cliff supports a path above it. A purely horizontal exclusion would
            // leave that entire cliff as exposed soil. If their projected footprints meet,
            // keep the whole closed volume below even the lower end of the path segment.
            // Nothing protrudes through the tread; character/step settings are unchanged.
            foreach(var road in ReferenceRoads)
            for(int i=0;i<road.points.Length-1;i++)
            {
                var a=road.points[i];var b=road.points[i+1];
                if(!ReferenceRockSegmentInterval(center,right,forward,halfWidth+road.width+.48f,halfDepth+road.width+.48f,
                    new Vector2(a.x,a.z),new Vector2(b.x,b.z),out float enter,out float exit))continue;
                // The entire closed OBB is below the lowest *overlapping* part of the
                // sloped stair. Using the remote downhill endpoint erases the rock
                // under its upper half even when those volumes never meet.
                float lowest=southFront?Mathf.Min(Mathf.Lerp(a.y,b.y,enter),Mathf.Lerp(a.y,b.y,exit)):Mathf.Min(a.y,b.y);
                top=Mathf.Min(top,lowest-.38f);
            }
            return top;
        }

        static bool ReferenceRockSegmentHits(Vector2 center,Vector2 right,Vector2 forward,float halfWidth,float halfDepth,Vector2 a,Vector2 b)
            => ReferenceRockSegmentInterval(center,right,forward,halfWidth,halfDepth,a,b,out _,out _);

        static bool ReferenceRockSegmentInterval(Vector2 center,Vector2 right,Vector2 forward,float halfWidth,float halfDepth,
            Vector2 a,Vector2 b,out float enter,out float exit)
        {
            var start=new Vector2(Vector2.Dot(a-center,right),Vector2.Dot(a-center,forward));
            var delta=new Vector2(Vector2.Dot(b-a,right),Vector2.Dot(b-a,forward));
            enter=0;exit=1;
            for(int axis=0;axis<2;axis++)
            {
                float half=axis==0?halfWidth:halfDepth;
                if(Mathf.Abs(delta[axis])<.00001f)
                {if(Mathf.Abs(start[axis])>half)return false;continue;}
                float lo=(-half-start[axis])/delta[axis],hi=(half-start[axis])/delta[axis];
                if(lo>hi){float swap=lo;lo=hi;hi=swap;}
                enter=Mathf.Max(enter,lo);exit=Mathf.Min(exit,hi);
                if(enter>exit)return false;
            }
            return true;
        }

        void ReferenceRockFaceMerge(Dictionary<Material,List<CombineInstance>> batches,Dictionary<Material,Material> overrides,
            GameObject source,Matrix4x4 placement,string textureId)
        {
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();var mesh=filter.sharedMesh;
                if(renderer==null||mesh==null||!mesh.isReadable)continue;
                var sourceMaterials=renderer.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount&&sub<sourceMaterials.Length;sub++)
                {
                    var original=sourceMaterials[sub];if(original==null)continue;
                    if(!overrides.TryGetValue(original,out var material))
                    {
                        material=new Material(ReferenceMaterial(original)){name="海岸母岩 · "+textureId,color=new Color(1,.985f,.95f),enableInstancing=true};
                        material.SetFloat("_Smoothness",.07f);material.SetFloat("_Metallic",0);
                        var map=Resources.Load<Texture2D>("CoastReference/Geology/"+textureId+"_basecolor");
                        var normal=Resources.Load<Texture2D>("CoastReference/Geology/"+textureId+"_normal");
                        if(map!=null)material.SetTexture("_BaseMap",map);
                        if(normal!=null){material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.38f);material.EnableKeyword("_NORMALMAP");}
                        materials.Add(material);overrides[original]=material;
                    }
                    if(!batches.TryGetValue(material,out var instances))batches[material]=instances=new List<CombineInstance>();
                    instances.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,
                        transform=placement*source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
        }
    }
}
