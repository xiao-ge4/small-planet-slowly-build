using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceBankConnectionsBuilt;

        // Call after BuildReferenceRockFaces, before planting/navigation. These are
        // continuous closed bank bodies: the back is buried 1.1m into the hill and
        // the foot is buried .45m. They do not duplicate the terrain as a thin skin.
        void BuildReferenceBankConnections()
        {
            if(referenceBankConnectionsBuilt || details==null)return;
            referenceBankConnectionsBuilt=true;
            var root=Group("参考岸坡 · Geology/道路与花园连续岩肩",details);
            var palette=new[]{
                Paint("连续岩肩 · 上部暖灰石",new Color(.61f,.60f,.54f),.065f,.13f),
                Paint("连续岩肩 · 中部灰石",new Color(.55f,.56f,.53f),.055f,.14f),
                Paint("连续岩肩 · 下部湿灰石",new Color(.46f,.49f,.46f),.05f,.12f),
                Paint("连续岩肩 · 嵌入土帽",new Color(.45f,.43f,.31f),.025f,.11f)
            };
            var vertices=new List<Vector3>();var uv=new List<Vector2>();
            var faces=new[]{new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            int bodies=0;int omitted=0;
            // Explicit visible seams, not a whole-island rock scattering pass.
            var seams=new (string name,Vector2[] line,Vector2 outward,bool bridge)[]{
                ("西桥头南肩",new[]{new Vector2(-10.0f,-22.65f),new(-7.8f,-22.35f),new(-5.4f,-21.65f),new(-3.0f,-21.15f)},new(0,-1),true),
                ("东桥头南肩",new[]{new Vector2(7.0f,-20.85f),new(8.6f,-20.0f),new(10.6f,-19.1f),new(12.1f,-18.4f)},new(.2f,-.98f),true),
                ("苗圃下沿西肩",new[]{new Vector2(-18.6f,-13.2f),new(-16.7f,-13.9f),new(-14.5f,-14.1f),new(-12.2f,-14.5f)},new(-.12f,-.99f),false),
                ("洞口阶外侧岩脚",new[]{new Vector2(16.8f,-20.1f),new(18.0f,-22.7f),new(17.8f,-25.0f),new(16.6f,-26.9f)},new(1,-.1f),false),
                ("洞口阶内侧接肩",new[]{new Vector2(13.5f,-20.2f),new(13.9f,-22.1f),new(13.4f,-24.2f)},new(-1,-.1f),false)
            };
            foreach(var seam in seams)
            {
                var rings=new List<Vector3[]>();var distances=new List<float>();
                var outward=seam.outward.normalized;float along=0;int installed=0;
                void Flush()
                {
                    if(rings.Count<2){rings.Clear();distances.Clear();return;}
                    int start=vertices.Count;const int sides=12;
                    var faceStart=new int[4];for(int m=0;m<4;m++)faceStart[m]=faces[m].Count;
                    for(int k=0;k<rings.Count;k++)for(int j=0;j<sides;j++)
                    {vertices.Add(rings[k][j]);uv.Add(new Vector2(distances[k]*.43f,rings[k][j].y*.43f));}
                    for(int k=1;k<rings.Count;k++)for(int j=0;j<sides;j++)
                    {
                        int next=(j+1)%sides;
                        int material=j<=1?3:j<=4?0:j<=7?1:2;
                        int a=start+(k-1)*sides+j,b=start+k*sides+j;
                        int c=start+k*sides+next,d=start+(k-1)*sides+next;
                        // Outward winding is chosen from the section's signed orientation.
                        var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                        var center=(rings[k][0]+rings[k][9])*.5f;
                        var wanted=(vertices[a]+vertices[b]+vertices[c]+vertices[d])*.25f-center;
                        if(Vector3.Dot(normal,wanted)<0)faces[material].AddRange(new[]{a,c,b,a,d,c});
                        else faces[material].AddRange(new[]{a,b,c,a,c,d});
                    }
                    // End caps share their boundary vertices, so the complete volume
                    // remains closed even where a surveyed seam ends at a junction.
                    for(int end=0;end<2;end++)
                    {
                        int ring=end==0?0:rings.Count-1;var center=Vector3.zero;
                        foreach(var p in rings[ring])center+=p;center/=sides;
                        int middle=vertices.Count;vertices.Add(center);uv.Add(Vector2.zero);
                        var away=rings[ring][0]-rings[end==0?1:rings.Count-2][0];
                        for(int j=0;j<sides;j++)
                        {
                            int a=start+ring*sides+j,b=start+ring*sides+(j+1)%sides;
                            if(Vector3.Dot(Vector3.Cross(vertices[a]-center,vertices[b]-center),away)>0)
                                faces[1].AddRange(new[]{middle,a,b});
                            else faces[1].AddRange(new[]{middle,b,a});
                        }
                    }
                    bool valid=true;
                    for(int m=0;m<4&&valid;m++)for(int f=faceStart[m];f<faces[m].Count;f+=3)
                    {
                        var a=vertices[faces[m][f]];var b=vertices[faces[m][f+1]];var c=vertices[faces[m][f+2]];
                        foreach(var p in new[]{a,b,c,(a+b)*.5f,(a+c)*.5f,(b+c)*.5f,(a+b+c)/3})
                            if(!ReferenceBankConnectionAllowed(p,seam.bridge)){valid=false;break;}
                    }
                    if(!valid)
                    {
                        vertices.RemoveRange(start,vertices.Count-start);uv.RemoveRange(start,uv.Count-start);
                        for(int m=0;m<4;m++)faces[m].RemoveRange(faceStart[m],faces[m].Count-faceStart[m]);
                        omitted++;
                    }
                    else
                    {
                        int exposed=0;float maxRelief=0;
                        for(int v=start;v<vertices.Count;v++)
                        {
                            var p=vertices[v];float relief=p.y-ReferenceHeight(p.x,p.z);
                            if(relief>.06f)exposed++;maxRelief=Mathf.Max(maxRelief,relief);
                        }
                        bodies++;installed++;
                        Debug.Log($"Bank visible body {seam.name}: {exposed}/{vertices.Count-start} exposed vertices, max {maxRelief:F2}m above existing soil, full buried back retained.");
                    }
                    rings.Clear();distances.Clear();
                }
                for(int segment=0;segment<seam.line.Length-1;segment++)
                {
                    var a=seam.line[segment];var b=seam.line[segment+1];
                    int count=Mathf.CeilToInt(Vector2.Distance(a,b)/.28f);
                    for(int k=0;k<=count;k++)
                    {
                        if(segment>0&&k==0)continue;
                        var seed=Vector2.Lerp(a,b,k/(float)count);
                        // Locate the surveyed cut within a bounded 1.2m strip. Broad
                        // shoulders follow the actual existing land, never a box wall.
                        Vector2 cut=seed;float best=-100;
                        for(int search=-2;search<=2;search++)
                        {
                            var q=seed+outward*(search*.30f);
                            float upper=ReferenceHeight(q.x-outward.x*.70f,q.y-outward.y*.70f);
                            float lower=ReferenceHeight(q.x+outward.x*1.10f,q.y+outward.y*1.10f);
                            float drop=upper-lower;
                            if(drop>best){best=drop;cut=q;}
                        }
                        var crest=cut-outward*.72f;var foot=cut+outward*1.24f;
                        // The top must come from the soil cap behind the cut. Sampling
                        // only the crest of the descending blend buried the whole bank.
                        var cap=crest-outward*.65f;
                        float top=Mathf.Max(ReferenceHeight(crest.x,crest.y),ReferenceHeight(cap.x,cap.y))-.09f;
                        float bottom=ReferenceHeight(foot.x,foot.y)-.18f;
                        bool overCave=crest.x>-4.5f&&crest.x<10.5f&&crest.y< -16.9f&&crest.y> -29;
                        if(overCave&&seam.bridge)bottom=Mathf.Max(bottom,1.68f);
                        // Less than 45cm is a soil transition, not a new rock wall.
                        if(top-bottom<.45f||top<ReferenceSeaLevel+.15f)
                        {Flush();omitted++;continue;}
                        float h=top-bottom;
                        float wave=.10f*Mathf.Sin(along*1.8f)+.055f*Mathf.Sin(along*4.7f);
                        // A broad shoulder projects beyond the terrain cut before the
                        // lower face descends. The former high points were all uphill
                        // of the cut, so even a closed volume was almost invisible.
                        var offsets=new[]{-1.10f,-.22f,.16f,.90f,1.16f,1.28f,1.62f,1.78f,2.12f,2.40f,2.40f,-1.10f};
                        var heights=new[]{Mathf.Min(top-.05f,ReferenceHeight(cap.x,cap.y)-.15f),top,top-.10f,top-.20f,top-h*.23f,top-h*.34f,top-h*.46f,top-h*.66f,top-h*.84f,bottom-.10f,bottom-.48f,bottom-.48f};
                        var ring=new Vector3[12];bool clear=true;
                        for(int j=0;j<12;j++)
                        {
                            float relief=j>=3&&j<=8?wave*(j%2==0?1:.55f):0;
                            var p=crest+outward*(offsets[j]+relief);
                            float y=ReferenceBankConnectionCeiling(p,heights[j],.76f);
                            if(overCave&&seam.bridge)y=Mathf.Max(1.68f,y);
                            ring[j]=new Vector3(p.x,y,p.y);
                            if(!ReferenceBankConnectionAllowed(ring[j],seam.bridge))clear=false;
                        }
                        // The .76m construction buffer covers each .28m section;
                        // final triangles are also sampled at the required +.60m.
                        if(!clear){Flush();omitted++;continue;}
                        if(rings.Count>0)
                        {
                            var previous=rings[rings.Count-1];
                            for(int j=0;j<12&&clear;j++)
                            {
                                int next=(j+1)%12;
                                foreach(var p in new[]{(previous[j]+ring[j])*.5f,(previous[j]+ring[next])*.5f,
                                    (previous[j]+ring[j]+ring[next])/3,(previous[j]+previous[next]+ring[next])/3})
                                    if(!ReferenceBankConnectionAllowed(p,seam.bridge)){clear=false;break;}
                            }
                        }
                        if(!clear){Flush();omitted++;continue;}
                        rings.Add(ring);distances.Add(along);along+=Vector2.Distance(a,b)/count;
                    }
                }
                Flush();Debug.Log($"Bank connection {seam.name}: {installed} closed continuous bodies.");
            }
            if(vertices.Count==0){Destroy(root.gameObject);return;}
            int triangles=0;foreach(var f in faces)triangles+=f.Count/3;
            if(triangles>50000)throw new System.InvalidOperationException("Bank connection geometry budget exceeded.");
            var mesh=new Mesh{name="连续岩肩 · 土帽至埋地岩脚闭合体",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=4;
            for(int i=0;i<4;i++)mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var solid=Group("连续闭合岩体 · 4 材质合批",root).gameObject;solid.layer=ObstacleLayer;
            solid.AddComponent<MeshFilter>().sharedMesh=mesh;
            solid.AddComponent<MeshRenderer>().sharedMaterials=palette;
            solid.AddComponent<MeshCollider>().sharedMesh=mesh;
            Debug.Log($"Bank connections: {bodies} closed bodies, {triangles}/50000 triangles, 4 material batches, {omitted} excluded cross sections; road width + .60m and open water protected.");
        }

        static float ReferenceBankConnectionCeiling(Vector2 point,float y,float buffer)
        {
            foreach(var road in ReferenceRoads)for(int i=0;i<road.points.Length-1;i++)
            {
                var a=road.points[i];var b=road.points[i+1];
                if(SegmentDistanceSquared(point,a,b,out float t)>Mathf.Pow(road.width+buffer,2))continue;
                // Also reserve the vertical grade change across one half section.
                float horizontal=new Vector2(b.x-a.x,b.z-a.z).magnitude;
                float slope=Mathf.Abs(b.y-a.y)/Mathf.Max(.1f,horizontal);
                y=Mathf.Min(y,Mathf.Lerp(a.y,b.y,t)-.40f-slope*.16f);
            }
            return y;
        }

        static bool ReferenceBankConnectionAllowed(Vector3 p,bool bridge)
        {
            if(p.x>3.7f&&p.x<19.7f&&Mathf.Abs(p.z)<6.7f)return false;
            if(p.x>-4.5f&&p.x<10.5f&&p.z> -29&&p.z< -16.9f&&(!bridge||p.y<1.66f))return false;
            var point=new Vector2(p.x,p.z);
            foreach(var road in ReferenceRoads)for(int i=0;i<road.points.Length-1;i++)
                if(SegmentDistanceSquared(point,road.points[i],road.points[i+1],out float t)<Mathf.Pow(road.width+.60f,2)
                    &&p.y>Mathf.Lerp(road.points[i].y,road.points[i+1].y,t)-.377f)return false;
            for(int i=0;i<ReferenceStream.Length-1;i++)
                if(SegmentDistanceSquared(point,ReferenceStream[i],ReferenceStream[i+1],out float t)<
                    Mathf.Pow(Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t)+.48f,2))return false;
            return true;
        }

        // Optional: call immediately after ReferenceAsset("Geology/central_ridge")
        // and before ReferenceSolidModel. Only the high eastern crown is lowered;
        // footprint, channel, road cuts and all lower vertices remain exact.
        void SoftenReferenceRidgeCrown(GameObject ridge)
        {
            if(ridge==null)return;int changed=0;
            foreach(var filter in ridge.GetComponentsInChildren<MeshFilter>())
            {
                var source=filter.sharedMesh;if(source==null||!source.isReadable)continue;
                var points=source.vertices;bool altered=false;
                for(int i=0;i<points.Length;i++)
                {
                    var p=details.InverseTransformPoint(filter.transform.TransformPoint(points[i]));
                    if(p.y<=5.25f||p.z<8||p.z>23.5f||p.x< -2.7f||p.x>4.5f)continue;
                    float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.25f,8.2f,p.y));
                    mask*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-2.7f,-1.0f,p.x));
                    for(int k=0;k<ReferenceStream.Length-1;k++)
                    {
                        float d=Mathf.Sqrt(SegmentDistanceSquared(new Vector2(p.x,p.z),ReferenceStream[k],ReferenceStream[k+1],out float t));
                        float w=Mathf.Lerp(ReferenceStreamHalfWidths[k],ReferenceStreamHalfWidths[k+1],t);
                        mask*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(w+1.0f,w+2.0f,d));
                    }
                    if(ReferenceRidgePathContains(p.x,p.z,1.25f))continue;
                    // Smooth bounded compression of upper facets, not asset scale.
                    float lower=Mathf.Min(.95f,(p.y-5.25f)*.29f)*mask;
                    if(lower<.001f)continue;p.y-=lower;
                    points[i]=filter.transform.InverseTransformPoint(details.TransformPoint(p));altered=true;changed++;
                }
                if(!altered)continue;
                var mesh=Instantiate(source);mesh.name=source.name+" · 高处岩冠缓坡";
                mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                meshes.Add(mesh);filter.sharedMesh=mesh;
            }
            Debug.Log($"Ridge crown: {changed} high vertices lowered by <= .95m; water, route and low rock footprint unchanged.");
        }
    }
}
