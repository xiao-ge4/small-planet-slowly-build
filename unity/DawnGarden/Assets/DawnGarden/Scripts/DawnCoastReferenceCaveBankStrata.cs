using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // The final shoulder is one authored, closed, southern-facing solid.
        // No contour wrapping, vertex stretching or runtime rebuilding happens here.
        void BuildReferenceCaveBankStrata(Transform parent,GameObject unusedSource,
            ReferenceForeshoreGeometry unusedGeometry,Dictionary<Material,Material> unusedOverrides)
        {
            const string id="CoastReference/Geology/cave_east_shoulder";
            var source=Resources.Load<GameObject>(id);
            if(source==null)throw new System.InvalidOperationException("Missing authored cave east shoulder: "+id);
            var model=Instantiate(source,parent,false);
            model.name="洞东完整岩肩 · Geology/cave_east_shoulder";
            model.transform.localPosition=new Vector3(11.9f,-2.6f,-23.0f);
            // This FBX carries Blender-to-Unity axis conversion on its root.
            // Clearing that rotation swaps height and depth and buries the rock.
            model.transform.localRotation=source.transform.localRotation;
            model.transform.localScale=source.transform.localScale;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){
                name="完整南向岩肩 · 干燥暖灰砂岩",color=Color.white,enableInstancing=true
            };
            material.SetFloat("_Smoothness",.07f);material.SetFloat("_Metallic",0);
            var color=Resources.Load<Texture2D>(id+"_basecolor");
            var normal=Resources.Load<Texture2D>(id+"_normal");
            if(color==null||normal==null)throw new System.InvalidOperationException("Cave shoulder baked maps are missing.");
            material.SetTexture("_BaseMap",color);material.SetTexture("_BumpMap",normal);
            material.SetFloat("_BumpScale",.45f);material.EnableKeyword("_NORMALMAP");materials.Add(material);
            long triangles=0;int samples=0,conflicts=0,renderers=0;
            bool first=true;Bounds localBounds=default;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                if(mesh==null||renderer==null)continue;
                if(!mesh.isReadable)throw new System.InvalidOperationException("Enable Read/Write for cave_east_shoulder.fbx before building.");
                if(mesh.subMeshCount==0||mesh.GetIndexCount(0)==0)
                    throw new System.InvalidOperationException("The authored cave shoulder has no final render triangles.");
                var points=mesh.vertices;
                for(int i=0;i<points.Length;i++)
                {
                    points[i]=details.InverseTransformPoint(filter.transform.TransformPoint(points[i]));
                    if(first){localBounds=new Bounds(points[i],Vector3.zero);first=false;}else localBounds.Encapsulate(points[i]);
                }
                void Check(Vector3 p)
                {
                    samples++;
                    foreach(var road in ReferenceRoads)
                    for(int i=0;i<road.points.Length-1;i++)
                    {
                        float distance=SegmentDistanceSquared(new Vector2(p.x,p.z),road.points[i],road.points[i+1],out float t);
                        if(distance>=Mathf.Pow(road.width+.60f,2))continue;
                        float grade=Mathf.Lerp(road.points[i].y,road.points[i+1].y,t);
                        if(p.y>grade-.377f){conflicts++;return;}
                    }
                }
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var faces=mesh.GetTriangles(sub);triangles+=faces.Length/3;
                    for(int k=0;k<faces.Length;k+=3)
                    {
                        var a=points[faces[k]];var b=points[faces[k+1]];var c=points[faces[k+2]];
                        Check(a);Check(b);Check(c);Check((a+b)*.5f);Check((a+c)*.5f);Check((b+c)*.5f);Check((a+b+c)/3);
                    }
                }
                renderer.sharedMaterials=new[]{material};renderers++;
                filter.gameObject.layer=ObstacleLayer;
                var collider=filter.GetComponent<MeshCollider>()??filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=mesh;collider.convex=false;
            }
            if(renderers==0||triangles==0||conflicts>0)
                throw new System.InvalidOperationException($"Authored cave shoulder failed final geometry guard: {triangles} triangles, {conflicts}/{samples} walk-corridor conflicts.");
            if(localBounds.min.y < -2.85f || localBounds.max.y < 2.0f || localBounds.max.z > -20.3f || localBounds.min.z > -25.3f)
                throw new System.InvalidOperationException("Authored cave shoulder lost its upright imported orientation: "+localBounds.ToString("F3"));
            Debug.Log($"Cave authored shoulder: {triangles} final triangles, {renderers} renderers, {samples} actual imported-geometry samples, {conflicts} conflicts at full road width + .60m; bounds {localBounds.min}..{localBounds.max}. No runtime deformation.");
        }
    }
}
