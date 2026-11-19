using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        readonly struct ReferencePaverFootprint
        {
            public readonly Vector2 center,along,across;
            public readonly float halfLength,halfWidth;
            public readonly int road;
            public ReferencePaverFootprint(Vector2 c,Vector2 f,float l,float w,int r)
            {center=c;along=f;across=new Vector2(f.y,-f.x);halfLength=l;halfWidth=w;road=r;}
        }

        // The masonry follows arc length and tangent along the actual surveyed roads.
        // It is a shallow visible finish over their unchanged walking collision.
        void BuildReferencePathStones()
        {
            var root=Group("参考地图 · 随路圆磨桃石",details);
            var palettes=new[]{new Color(.88f,.69f,.51f),new Color(.92f,.73f,.55f),
                new Color(.83f,.62f,.47f),new Color(.88f,.67f,.53f)};
            var points=new List<Vector3>[4];var faces=new List<int>[4];
            for(int i=0;i<4;i++){points[i]=new List<Vector3>();faces[i]=new List<int>();}
            var random=new System.Random(901247);int stones=0,roadIndex=-1,candidates=0,duplicateRejects=0,supportRejects=0;
            var occupied=new Dictionary<Vector2Int,List<ReferencePaverFootprint>>();
            float J(float amount)=>((float)random.NextDouble()-.5f)*amount;
            // Remove the conflicting world-aligned yellow grid underneath the real
            // cobbles. The separate white-land material is deliberately untouched.
            var groundFinish=ground.Find("参考地图 · 连续地表与地块承接");
            if(groundFinish!=null)
            {
                var material=groundFinish.GetComponent<MeshRenderer>().sharedMaterial;
                if(material.HasProperty("_PaverScale"))
                {
                    material.SetVector("_PaverScale",Vector4.zero);
                    material.SetColor("_PathColor",new Color(.88f,.70f,.53f));
                    material.SetFloat("_MortarTone",.96f);
                    material.SetFloat("_GroutWidth",1);
                }
            }
            foreach(var road in ReferenceRoads)
            {
                roadIndex++;
                var cumulative=new float[road.points.Length];
                for(int i=1;i<cumulative.Length;i++)
                    cumulative[i]=cumulative[i-1]+Vector2.Distance(new Vector2(road.points[i-1].x,road.points[i-1].z),
                        new Vector2(road.points[i].x,road.points[i].z));
                float length=cumulative[cumulative.Length-1];
                int lanes=Mathf.Clamp(Mathf.RoundToInt(road.width*1.65f/.54f),2,5);
                float laneWidth=road.width*1.65f/lanes;
                for(float row=.15f;row<length-.06f;row+=.54f)
                for(int lane=0;lane<lanes;lane++)
                {
                    float distance=Mathf.Clamp(row+(lane%2)*.27f+J(.018f),.04f,length-.04f);
                    var at=ReferenceRoadPoint(road.points,cumulative,distance);
                    var before=ReferenceRoadPoint(road.points,cumulative,Mathf.Max(0,distance-.36f));
                    var after=ReferenceRoadPoint(road.points,cumulative,Mathf.Min(length,distance+.36f));
                    var forward=new Vector2(after.x-before.x,after.z-before.z).normalized;
                    if(forward.sqrMagnitude<.5f)continue;
                    var side=new Vector2(forward.y,-forward.x);
                    float cross=(lane-(lanes-1)*.5f)*laneWidth+J(.018f);
                    float x=at.x+side.x*cross,z=at.z+side.y*cross;
                    if(!ReferencePathStoneArea(x,z))continue;
                    float y=SampleReferenceHeight(x,z,out int surface,out float paving,out float wet,out bool submerged);
                    if(submerged||paving<.50f||wet>.35f||surface==2)continue;
                    candidates++;
                    float turn=J(5)*Mathf.Deg2Rad;
                    forward=new Vector2(forward.x*Mathf.Cos(turn)-forward.y*Mathf.Sin(turn),
                        forward.x*Mathf.Sin(turn)+forward.y*Mathf.Cos(turn));
                    side=new Vector2(forward.y,-forward.x);
                    float width=laneWidth*.485f*(1+J(.06f)),depth=.260f*(1+J(.045f));
                    var footprint=new ReferencePaverFootprint(new Vector2(x,z),forward,depth,width,roadIndex);
                    var cell=new Vector2Int(Mathf.FloorToInt(x),Mathf.FloorToInt(z));
                    bool overlaps=false;
                    for(int dx=-1;dx<=1&&!overlaps;dx++)for(int dz=-1;dz<=1&&!overlaps;dz++)
                        if(occupied.TryGetValue(cell+new Vector2Int(dx,dz),out var neighbors))
                            foreach(var other in neighbors)if(ReferencePaversOverlap(footprint,other)){overlaps=true;break;}
                    if(overlaps){duplicateRejects++;continue;}
                    var edge=new Vector3[16];bool supported=true;
                    float cornerRadius=Mathf.Min(width,depth)*(.36f+(float)random.NextDouble()*.10f);
                    for(int corner=0;corner<4&&supported;corner++)for(int step=0;step<4;step++)
                    {
                        float angle=(-90+corner*90+step*30)*Mathf.Deg2Rad;
                        float cx=corner<2?width-cornerRadius:-width+cornerRadius;
                        float cz=corner==0||corner==3?-depth+cornerRadius:depth-cornerRadius;
                        float u=cx+Mathf.Cos(angle)*cornerRadius,v=cz+Mathf.Sin(angle)*cornerRadius;
                        var p=new Vector2(x,z)+side*u+forward*v;
                        float h=y;bool cornerSupported=false;
                        // Clip only the boundary of a stone against the surveyed
                        // path, instead of deleting an entire row on bends/slopes.
                        for(int attempt=0;attempt<6;attempt++)
                        {
                            h=SampleReferenceHeight(p.x,p.y,out _,out float mask,out _,out bool waterCovered);
                            if(!waterCovered&&mask>=.22f&&Mathf.Abs(h-y)<.48f&&ReferencePathStoneArea(p.x,p.y))
                            {cornerSupported=true;break;}
                            p=Vector2.Lerp(new Vector2(x,z),p,.78f);
                        }
                        if(!cornerSupported){supported=false;break;}
                        edge[corner*4+step]=new Vector3(p.x,h+.012f,p.y);
                    }
                    if(!supported){supportRejects++;continue;}
                    int bucket=random.Next(4),start=points[bucket].Count;
                    var vertices=points[bucket];var triangles=faces[bucket];
                    vertices.AddRange(edge);
                    for(int corner=0;corner<16;corner++)
                    {
                        var p=Vector3.Lerp(new Vector3(x,y+.012f,z),edge[corner],.88f);
                        p.y+=.020f;vertices.Add(p);
                    }
                    int center=vertices.Count;vertices.Add(new Vector3(x,y+.036f,z));
                    for(int corner=0;corner<16;corner++)
                    {
                        int next=(corner+1)%16;
                        triangles.AddRange(new[]{start+corner,start+16+next,start+next,
                            start+corner,start+16+corner,start+16+next,center,start+16+next,start+16+corner});
                    }
                    if(!occupied.TryGetValue(cell,out var list))occupied[cell]=list=new List<ReferencePaverFootprint>();
                    list.Add(footprint);stones++;
                }
            }
            for(int i=0;i<4;i++)if(points[i].Count>0)
                MeshObject("随路圆磨桃石 · "+i,root,points[i],faces[i],Paint("步道桃砂石 · "+i,palettes[i],.10f,.12f));
            Debug.Log($"Reference path stones: {stones}/{candidates} supported road cells, duplicate junction cells={duplicateRejects}, unsupported={supportRejects}, {root.childCount} batches; maximum relief 0.036m; navigation and saved land untouched.");
        }

        static Vector3 ReferenceRoadPoint(Vector3[] route,float[] cumulative,float distance)
        {
            for(int i=1;i<route.Length;i++)
                if(distance<=cumulative[i])return Vector3.Lerp(route[i-1],route[i],
                    Mathf.InverseLerp(cumulative[i-1],cumulative[i],distance));
            return route[route.Length-1];
        }

        static bool ReferencePathStoneArea(float x,float z)
        {
            // Dedicated surfaces retain their own scale and material. This also keeps
            // the editable land entirely free of new geometry or scene-state changes.
            if(x>-4.2f&&x<10.3f&&z>-25.5f&&z<-16.9f)return false;
            var workshop=ReferenceWorkshopFloorLocal(x,z);
            if(Mathf.Abs(workshop.x)<5.28f&&Mathf.Abs(workshop.z)<3.54f)return false;
            if(x>5.2f&&x<18.4f&&z>-5.7f&&z<5.7f)return false;
            if(ReferencePolygonDistance(new Vector2(x,z),ReferenceReserveEdge)>-.10f)return false;
            float dx=(x-ReferenceObservatory.x)/ReferenceObservatoryGroundHalf.x;
            float dz=(z-ReferenceObservatory.z)/ReferenceObservatoryGroundHalf.y;
            if(dx*dx+dz*dz<1.04f)return false;
            return true;
        }

        static bool ReferencePaversOverlap(ReferencePaverFootprint a,ReferencePaverFootprint b)
        {
            // Continuous courses on the same road must never randomly reject one
            // another. Only a second road's near-identical junction cell is skipped.
            if(a.road==b.road)return false;
            float clearance=(Mathf.Min(a.halfLength,a.halfWidth)+Mathf.Min(b.halfLength,b.halfWidth))*.78f;
            return (a.center-b.center).sqrMagnitude<clearance*clearance;
        }
    }
}
