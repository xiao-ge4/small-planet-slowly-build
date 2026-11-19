using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        const int FloorLayer = 8, ObstacleLayer = 9;
        readonly List<(Vector3 Center, List<Vector3> Rim)> shores = new();

        void CreateMaterials()
        {
            grass = Paint("沿海草地", new Color(.43f,.57f,.24f), .08f, .24f);
            cliff = Paint("层理砂岩", new Color(.70f,.67f,.56f), .1f, .21f);
            cliff.EnableKeyword("_EMISSION");
            cliff.SetColor("_EmissionColor",new Color(.054f,.052f,.038f));
            slate = Paint("蓝灰板岩", new Color(.31f,.42f,.47f), .16f, .14f);
            limestone = Paint("暖白石灰岩", new Color(.84f,.78f,.63f), .13f, .13f);
            sandstone = Paint("浅潮滩", new Color(.81f,.68f,.48f), .09f, .14f);
            brick = Paint("工坊陶砖", new Color(.73f,.43f,.32f), .1f, .13f);
            path = Paint("小院石板与曲径", new Color(.78f,.66f,.49f), .13f, .16f, true);
            wetStone = Paint("潮湿洞壁", new Color(.32f,.35f,.36f), .24f, .23f);
            darkWood = Paint("老橡木深纹", new Color(.40f,.24f,.12f), .12f, .21f);
            wood = Paint("蜂蜜色木板", new Color(.66f,.42f,.23f), .14f, .18f);
            roof = Paint("烧制瓦片", new Color(.60f,.30f,.19f), .17f, .15f);
            azure = Paint("星图台釉面", new Color(.24f,.39f,.55f), .26f, .10f);
            water = Paint("浅渠青水", new Color(.12f,.51f,.57f), .82f, .19f);
            flow = Paint("瀑布纹理", new Color(.54f,.82f,.88f), .62f, .34f);
            foam = Paint("浪沫", new Color(.86f,.96f,.92f), .34f, .12f);
            brass = Paint("旧黄铜", new Color(.79f,.54f,.24f), .50f, .10f);
            brass.SetFloat("_Metallic", .38f);
            soil = Paint("湿润菜畦土", new Color(.37f,.25f,.14f), .04f, .24f);
            leaf = Paint("园圃嫩叶", new Color(.51f,.69f,.25f), .16f, .16f);
            carrots = Paint("橙花", new Color(.94f,.55f,.24f), .18f, .1f);
            glass = Paint("青釉温室拱板", new Color(.45f,.83f,.76f,.43f), .90f, 0);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetFloat("_Surface", 1);
            glass.SetFloat("_ZWrite", 0);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_Cull", (float)CullMode.Off);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            glass.SetShaderPassEnabled("ShadowCaster", false);
            warmGlass = Paint("琥珀壁灯", new Color(1f,.66f,.28f), .38f, 0);
            warmGlass.EnableKeyword("_EMISSION");
            warmGlass.SetColor("_EmissionColor", new Color(2.2f,.76f,.12f));
        }

        Material Paint(string name, Color color, float shine, float variation, bool paving = false)
        {
            var surfaceShader = (name.Contains("岩") || name.Contains("石")) && !paving ? Resources.Load<Shader>("Coast/WornStone") : null;
            var mat = new Material(surfaceShader != null ? surfaceShader : Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color, enableInstancing = true };
            mat.SetFloat("_Smoothness", shine);
            if (variation > 0)
            {
                const int resolution = 256;
                var map = new Texture2D(resolution, resolution, TextureFormat.RGB24, true) { name = name + " · 纹理", wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color32[resolution * resolution];
                for (int j = 0; j < resolution; j++)
                for (int i = 0; i < resolution; i++)
                {
                    float u = i / (float)resolution, v = j / (float)resolution;
                    float n = Mathf.PerlinNoise(u * 6 + 4.7f, v * 6 + 1.2f) * .74f + Mathf.PerlinNoise(u * 34, v * 34) * .26f;
                    float k = .88f + variation * (n - .35f);
                    if (paving)
                    {
                        int strip = Mathf.FloorToInt(v * 4);
                        float gx = Mathf.Repeat(u * 4 + (strip % 2) * .5f + (n - .5f) * .065f, 1);
                        float gy = Mathf.Repeat(v * 4 + (n - .5f) * .033f, 1);
                        float rim = Mathf.Min(gx,1-gx,gy,1-gy);
                        k *= Mathf.Lerp(.70f, 1.06f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.007f,.061f,rim)));
                    }
                    pixels[j * resolution + i] = new Color(k,k,k,1);
                }
                map.SetPixels32(pixels);
                map.Apply(true, true);
                textures.Add(map);
                mat.SetTexture("_BaseMap",map);
            }
            materials.Add(mat);
            return mat;
        }

        Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        GameObject MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> indices,
            Material material, bool walkable = false, List<Vector2> mapping = null, bool obstacle = false)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            if (mapping == null)
            {
                mapping = new List<Vector2>(vertices.Count);
                foreach (var v in vertices) mapping.Add(new Vector2(v.x * .34f + v.z * .18f, v.z * .34f + v.y * .38f));
            }
            mesh.SetUVs(0,mapping);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var go = Group(name,parent).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (walkable || obstacle)
            {
                go.layer = walkable ? FloorLayer : ObstacleLayer;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return go;
        }

        GameObject Piece(PrimitiveType shape, Transform parent, string name, Vector3 local, Vector3 size, Material mat, int physicsLayer = 0)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = local;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            obj.layer = physicsLayer;
            if (physicsLayer == 0)
            {
                var component = obj.GetComponent<Collider>();
                component.enabled = false;
                Destroy(component);
            }
            return obj;
        }

        GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material mat, int physicsLayer = 0)
            => Piece(PrimitiveType.Cube, parent, name, local, size, mat, physicsLayer);

        GameObject Cylinder(Transform parent, string name, Vector3 local, Vector3 size, Material mat, int physicsLayer = 0)
            => Piece(PrimitiveType.Cylinder, parent, name, local, size, mat, physicsLayer);

        void Beam(Transform parent, string name, Vector3 a, Vector3 b, float width, Material mat, int layer = 0)
        {
            var beam = Box(parent,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,layer);
            beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up,b-a);
        }

        void Rail(Transform parent, Vector3 start, Vector3 end, float gap, Material mat)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(start,end) / gap));
            for (int i=0; i<=count; i++)
            {
                var p = Vector3.Lerp(start,end,i/(float)count);
                Box(parent,"栏杆立柱",p + Vector3.up*.47f,new Vector3(.14f,.94f,.14f),mat,ObstacleLayer);
                if (i > 0)
                {
                    var before = Vector3.Lerp(start,end,(i-1)/(float)count);
                    Beam(parent,"扶手",before + Vector3.up*.72f,p + Vector3.up*.72f,.11f,mat);
                }
            }
        }

        void Plate(string name, Vector3 center, Vector2 radius, Material surface, Material edge, int seed, bool angular = false)
        {
            var perimeter = new List<Vector3>();
            int segments = angular ? 16 : 37;
            for (int i = 0; i < segments; i++)
            {
                float a = i * (2 * Mathf.PI / segments);
                float r = 1 + .075f * Mathf.Sin(a*5 + seed) + .05f * Mathf.Cos(a*9 + seed*.63f);
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                if (angular)
                {
                    float f = 1 / Mathf.Pow(Mathf.Pow(Mathf.Abs(x),5)+Mathf.Pow(Mathf.Abs(z),5),.2f);
                    x *= f; z *= f; r = 1;
                }
                perimeter.Add(center + new Vector3(x * r * radius.x,0,z * r * radius.y));
            }
            if (center.x < 100) shores.Add((center, perimeter));
            var top = new List<Vector3>{center}; top.AddRange(perimeter);
            var cap = new List<int>();
            for (int i=0; i<segments; i++) cap.AddRange(new[]{0,(i+1)%segments+1,i+1});
            MeshObject(name,ground,top,cap,surface,true);

            var sides = new List<Vector3>();
            var walls = new List<int>();
            for (int i=0; i<segments; i++)
            {
                var a = perimeter[i]; var b = perimeter[(i+1)%segments];
                var footA= center+(a-center) * (1.13f + .06f * Mathf.Sin(i * 2.7f));
                var footB= center+(b-center) * (1.13f + .06f * Mathf.Sin(((i+1)%segments) * 2.7f));
                footA.y = footB.y = Mathf.Min(-1.75f,center.y-1.1f);
                int n=sides.Count;
                var shoulderA = Vector3.Lerp(a,footA,.54f); var shoulderB = Vector3.Lerp(b,footB,.54f);
                shoulderA += (a-center).normalized * (.22f * Mathf.Sin(i*1.7f));
                shoulderB += (b-center).normalized * (.22f * Mathf.Sin(((i+1)%segments)*1.7f));
                sides.AddRange(new[]{a,b,shoulderA,shoulderB,footA,footB});
                walls.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2,n+2,n+3,n+4,n+3,n+5,n+4});
            }
            MeshObject(name+" · 分层岩壁",details,sides,walls,edge);
        }

        void Ribbon(string name, float width, Material mat, bool walkable, params Vector3[] knots)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int i=0; i<knots.Length; i++)
            {
                var dir = (knots[Mathf.Min(i+1,knots.Length-1)] - knots[Mathf.Max(0,i-1)]); dir.y=0;
                var normal = Vector3.Cross(Vector3.up,dir.normalized) * (width*.5f);
                vertices.Add(knots[i]+normal);
                vertices.Add(knots[i]-normal);
                if (i == 0) continue;
                int n=2*i;
                indices.AddRange(new[]{n-2,n-1,n,n,n-1,n+1});
            }
            MeshObject(name,walkable?ground:details,vertices,indices,mat,walkable);
            if (!walkable || mat == wood) return;
            var walls = new List<Vector3>();
            var faces = new List<int>();
            for (int i = 0; i < knots.Length; i++)
            {
                var a = vertices[i*2]; var b = vertices[i*2+1];
                var outward = (a-b).normalized * .37f;
                var baseA = a + outward; var baseB = b - outward;
                baseA.y = baseB.y = -1.53f;
                walls.AddRange(new[]{a,b,baseA,baseB});
                if (i == 0) continue;
                int n = i * 4, p = n - 4;
                faces.AddRange(new[]{p,n,p+2,n,n+2,p+2,p+1,p+3,n+1,n+1,p+3,n+3});
            }
            MeshObject(name+" · 岩坡",details,walls,faces,cliff);
        }

        void Quad(string name, Transform parent, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material mat)
        {
            MeshObject(name,parent,new List<Vector3>{a,b,c,d},new List<int>{0,1,2,0,2,3,2,1,0,3,2,0},mat,false,
                new List<Vector2>{new(0,0),new(1,0),new(1,1),new(0,1)});
        }

        GameObject Asset(string id, Transform parent, Vector3 point, float scale, float yaw = 0, bool blocking = false)
        {
            var source = System.Array.Find(library.assets, x => x.id == id)?.prefab;
            if (source == null) return null;
            var go = Instantiate(source,parent,false);
            go.name = source.name;
            go.transform.localPosition = point;
            go.transform.localScale *= scale;
            go.transform.localRotation = Quaternion.Euler(0,yaw,0);
            foreach (var collider in go.GetComponentsInChildren<Collider>())
            {
                collider.enabled = blocking;
                if (blocking) collider.gameObject.layer = ObstacleLayer;
            }
            return go;
        }

        void Rocks(Transform parent, Vector3 at, float size, int count)
        {
            for (int i=0; i<count; i++)
            {
                var p = at+new Vector3(i * size*.36f,0,Mathf.Sin(i*2.3f)*size*.38f);
                var rock = Asset("moss-boulder",parent,p,size*(.38f + .19f*(float)scatter.NextDouble()),i*51, false);
                if (rock != null) rock.transform.localRotation *= Quaternion.Euler(0,0,Mathf.Sin(i*2.7f)*14);
            }
        }
    }
}
