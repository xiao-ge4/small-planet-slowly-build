using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // The local sea stays level for existing shores and interactions. Beyond it,
        // a real spherical surface produces a horizon for the long-lens overview.
        const float ReferenceOceanRadius = 1133.48f;
        const float ReferenceOceanFlatRadius = 60f;
        const float ReferenceOceanBlendRadius = 120f;
        const float ReferenceOceanOuterRadius = 900f;
        const int ReferenceOceanSegments = 256;
        static readonly Vector2 ReferenceOceanCenter = new(7.5f, -10.4f);

        MeshFilter referenceOceanMeshFilter;
        Mesh referenceOceanMeshSource;
        Mesh referenceOceanMesh;
        Texture2D referenceOceanBathymetry;

        // Returns an offset from ReferenceSeaLevel, not an absolute world height.
        // Backdrop foundations can use ReferenceSeaLevel + ReferenceOceanOffset(x,z).
        public static float ReferenceOceanOffset(float worldX, float worldZ)
        {
            float x = worldX - ReferenceOceanCenter.x;
            float z = worldZ - ReferenceOceanCenter.y;
            float radius = Mathf.Sqrt(x * x + z * z);
            return SampleReferenceOceanProfile(radius, out _);
        }

        static float SampleReferenceOceanProfile(float radius, out float slope)
        {
            slope = 0;
            if (radius <= ReferenceOceanFlatRadius) return 0;

            // The generated cap ends at 900 m, well before the sphere's equator.
            // Clamp external height queries so a distant placement cannot yield NaN.
            float r = Mathf.Min(radius, ReferenceOceanRadius - .1f);
            float root = Mathf.Sqrt(ReferenceOceanRadius * ReferenceOceanRadius - r * r);
            // Equivalent to sqrt(R^2-r^2)-R, without near-origin cancellation.
            float sphere = -(r * r) / (ReferenceOceanRadius + root);
            float sphereSlope = -r / root;
            if (radius >= ReferenceOceanBlendRadius)
            {
                slope = radius == r ? sphereSlope : 0;
                return sphere;
            }

            float span = ReferenceOceanBlendRadius - ReferenceOceanFlatRadius;
            float t = (radius - ReferenceOceanFlatRadius) / span;
            float weight = t * t * (3 - 2 * t);
            float weightSlope = 6 * t * (1 - t) / span;
            slope = sphereSlope * weight + sphere * weightSlope;
            return sphere * weight;
        }

        void BuildReferenceOceanMesh(Renderer ocean)
        {
            if (ocean == null) return;
            var filter = ocean.GetComponent<MeshFilter>();
            if (filter == null)
                throw new InvalidOperationException("Reference ocean requires a MeshFilter on its renderer.");

            // Rebuilding is safe if the host changes its scale or world transform.
            // Do not mutate the shared plane asset or its physics/collision mesh.
            RestoreReferenceOceanMesh();
            referenceOceanMeshFilter = filter;
            referenceOceanMeshSource = filter.sharedMesh;

            var rings = new List<float>(160);
            for (float r = 4; r <= ReferenceOceanFlatRadius; r += 4) rings.Add(r);
            for (float r = 62; r <= ReferenceOceanBlendRadius; r += 2) rings.Add(r);
            for (float r = 124; r <= 320; r += 4) rings.Add(r);
            for (float r = 330; r <= ReferenceOceanOuterRadius; r += 10) rings.Add(r);

            int vertexCount = 1 + rings.Count * ReferenceOceanSegments;
            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var uv = new List<Vector2>(vertexCount);
            var triangles = new List<int>((ReferenceOceanSegments +
                (rings.Count - 1) * ReferenceOceanSegments * 2) * 3);
            var toLocal = ocean.transform.worldToLocalMatrix;
            // Normals transform by inverse transpose. Applying this transpose here
            // supplies local normals that become the intended world normals even
            // when the existing Ocean has a very large or nonuniform scale.
            var worldNormalToLocal = ocean.transform.localToWorldMatrix.transpose;

            void AddVertex(float x, float z, float offset, Vector3 normal)
            {
                vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(x, ReferenceSeaLevel + offset, z)));
                normals.Add(worldNormalToLocal.MultiplyVector(normal).normalized);
                uv.Add(new Vector2(x, z) * .01f);
            }

            AddVertex(ReferenceOceanCenter.x, ReferenceOceanCenter.y, 0, Vector3.up);
            foreach (float radius in rings)
            {
                float offset = SampleReferenceOceanProfile(radius, out float slope);
                for (int segment = 0; segment < ReferenceOceanSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2 / ReferenceOceanSegments;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    var normal = new Vector3(-slope * x, 1, -slope * z).normalized;
                    AddVertex(ReferenceOceanCenter.x + radius * x,
                        ReferenceOceanCenter.y + radius * z, offset, normal);
                }
            }

            for (int segment = 0; segment < ReferenceOceanSegments; segment++)
            {
                int next = (segment + 1) % ReferenceOceanSegments;
                // Viewed from above, these triangles face the sky (+world Y).
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + segment);
            }
            for (int ring = 1; ring < rings.Count; ring++)
            {
                int inner = 1 + (ring - 1) * ReferenceOceanSegments;
                int outer = 1 + ring * ReferenceOceanSegments;
                for (int segment = 0; segment < ReferenceOceanSegments; segment++)
                {
                    int next = (segment + 1) % ReferenceOceanSegments;
                    triangles.Add(inner + segment);
                    triangles.Add(inner + next);
                    triangles.Add(outer + segment);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                    triangles.Add(outer + segment);
                }
            }

            referenceOceanMesh = new Mesh
            {
                name = "海岸远海 · 近岸平面与真实球冠",
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            referenceOceanMesh.SetVertices(vertices);
            referenceOceanMesh.SetNormals(normals);
            referenceOceanMesh.SetUVs(0, uv);
            referenceOceanMesh.SetTriangles(triangles, 0);
            referenceOceanMesh.RecalculateBounds();
            referenceOceanMesh.RecalculateTangents();
            filter.sharedMesh = referenceOceanMesh;
            meshes.Add(referenceOceanMesh);
            BuildReferenceOceanBathymetry(ocean.sharedMaterial);
        }

        void BuildReferenceOceanBathymetry(Material material)
        {
            if(material==null || !material.HasProperty("_UseBathymetry"))return;
            material.SetFloat("_UseBathymetry",0);
            var terrain=ground.Find("参考地图 · 连续地表与地块承接")?.GetComponent<MeshFilter>();
            var source=terrain!=null?terrain.sharedMesh:null;
            if(source==null || !source.isReadable)return;
            // Read the already-built survey, rather than sampling or changing any
            // walking surface. Vertices are row-major in the terrain's own XZ frame.
            var points=source.vertices;
            if(points.Length<4)return;
            int columns=1;
            while(columns<points.Length && Mathf.Abs(points[columns].z-points[0].z)<.001f)columns++;
            if(columns<2 || points.Length%columns!=0)return;
            int rows=points.Length/columns;if(rows<2)return;
            var first=terrain.transform.TransformPoint(points[0]);
            var last=terrain.transform.TransformPoint(points[points.Length-1]);
            var east=terrain.transform.TransformPoint(points[1])-first;
            var north=terrain.transform.TransformPoint(points[columns])-first;
            if(east.x<=0 || north.z<=0 || Mathf.Abs(east.z)>.001f || Mathf.Abs(north.x)>.001f)
            {
                Debug.LogWarning("Reference ocean bathymetry requires an axis-aligned terrain survey; retaining optical-depth fallback.");
                return;
            }
            var heights=new float[points.Length];
            for(int i=0;i<points.Length;i++)heights[i]=terrain.transform.TransformPoint(points[i]).y;
            referenceOceanBathymetry=new Texture2D(columns,rows,TextureFormat.RFloat,false,true)
            {
                name="海岸 · 实际海床世界高度",
                wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
            };
            referenceOceanBathymetry.SetPixelData(heights,0);
            referenceOceanBathymetry.Apply(false,true);
            textures.Add(referenceOceanBathymetry);
            material.SetTexture("_Bathymetry",referenceOceanBathymetry);
            material.SetVector("_BathymetryRect",new Vector4(first.x,first.z,last.x-first.x,last.z-first.z));
            material.SetFloat("_UseBathymetry",1);
            Debug.Log($"Reference ocean bathymetry: {columns}x{rows} existing terrain vertices, XZ ({first.x:F2},{first.z:F2}) to ({last.x:F2},{last.z:F2}); visual depth only, no geometry or collider changes.");
        }

        void RestoreReferenceOceanMesh()
        {
            if(referenceOceanBathymetry!=null)
            {
                textures.Remove(referenceOceanBathymetry);
                if(Application.isPlaying)Destroy(referenceOceanBathymetry);
                else DestroyImmediate(referenceOceanBathymetry);
                referenceOceanBathymetry=null;
            }
            if (referenceOceanMeshFilter != null &&
                referenceOceanMeshFilter.sharedMesh == referenceOceanMesh)
                referenceOceanMeshFilter.sharedMesh = referenceOceanMeshSource;
            if (referenceOceanMesh != null)
            {
                meshes.Remove(referenceOceanMesh);
                if (Application.isPlaying) Destroy(referenceOceanMesh);
                else DestroyImmediate(referenceOceanMesh);
            }
            referenceOceanMeshFilter = null;
            referenceOceanMeshSource = null;
            referenceOceanMesh = null;
        }
    }
}
