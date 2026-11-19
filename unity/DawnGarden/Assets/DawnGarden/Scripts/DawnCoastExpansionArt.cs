using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        readonly List<(MeshFilter filter, Renderer renderer, Mesh originalMesh, Material[] originalMaterials)> expansionArtSwaps = new();
        Transform expansionCoastRim;

        void BuildExpansionArt()
        {
            var land = game.expansion.land;
            if (land == null) return;
            var filter = land.GetComponent<MeshFilter>();
            var renderer = land.GetComponent<Renderer>();
            var original = filter != null ? filter.sharedMesh : null;
            var surfaceShader = Resources.Load<Shader>("Coast/SampleGround");
            if (surfaceShader == null || renderer == null || original == null ||
                original.name != "Starter-sized new continent" || !original.isReadable) return;

            // Read the actual top outline. The visual skin follows the old collider exactly.
            var vertices = original.vertices;
            var top = original.GetTriangles(0);
            var outline = new Vector3[top.Length / 3];
            for (int i = 0; i < outline.Length; i++) outline[i] = vertices[top[i * 3 + 2]];

            var surface = new Material(surfaceShader) { name = "地块 · 海岸草地、土径与岩脚", color = grass.color };
            surface.SetColor("_StoneColor", cliff.color);
            surface.SetColor("_PathColor", path.color);
            materials.Add(surface);
            SetExpansionRenderMesh(filter, renderer, MakeExpansionSurface(vertices[0], outline), surface);

            var waist = land.Find("Natural continent connection");
            if (waist != null)
            {
                var waistFilter = waist.GetComponent<MeshFilter>();
                var waistRenderer = waist.GetComponent<Renderer>();
                if (waistFilter != null && waistRenderer != null && waistFilter.sharedMesh != null && waistFilter.sharedMesh.isReadable)
                    SetExpansionRenderMesh(waistFilter, waistRenderer,
                        MakeExpansionWaist(waistFilter.sharedMesh, game.expansion.finalPosition), surface);
            }

            expansionCoastRim = Group("扩建地随土地升起的岩脚", land);
            var root = Group("沿岩脚分层露出的石岸", expansionCoastRim);
            var rimMesh = MakeExpansionRim(outline, root);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = rimMesh;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = surface;
        }

        void SetExpansionRenderMesh(MeshFilter filter, Renderer renderer, Mesh replacement, Material surface)
        {
            expansionArtSwaps.Add((filter, renderer, filter.sharedMesh, renderer.sharedMaterials));
            filter.sharedMesh = replacement;
            renderer.sharedMaterials = new[] { surface };
        }

        void DisposeExpansionArt()
        {
            foreach (var original in expansionArtSwaps)
            {
                if (original.filter != null) original.filter.sharedMesh = original.originalMesh;
                if (original.renderer != null) original.renderer.sharedMaterials = original.originalMaterials;
            }
            expansionArtSwaps.Clear();
            if (expansionCoastRim != null) Destroy(expansionCoastRim.gameObject);
            expansionCoastRim = null;
        }

        Mesh MakeExpansionSurface(Vector3 center, Vector3[] perimeter)
        {
            const int rings = 24;
            var points = new List<Vector3> { center };
            var colors = new List<Color> { grass.color };
            var weights = new List<Vector2> { Vector2.zero };
            var faces = new List<int>();
            var core = colors[0];
            core.a = 0;
            colors[0] = core;
            int count = perimeter.Length;
            for (int ring = 1; ring <= rings; ring++)
            for (int i = 0; i < count; i++)
            {
                float fraction = ring / (float)rings;
                var position = Vector3.Lerp(center, perimeter[i], fraction);
                points.Add(position);
                float turf = Mathf.PerlinNoise(position.x * .61f + 13.4f, position.z * .61f + 7.8f);
                float edge = Ease(.79f, 1, fraction) * (.15f + .23f * turf);
                colors.Add(ExpansionSurfaceColor(position, edge));
                weights.Add(Vector2.zero);

                int outer = 1 + (ring - 1) * count;
                int next = (i + 1) % count;
                if (ring == 1) faces.AddRange(new[] { 0, outer + next, outer + i });
                else
                {
                    int inner = outer - count;
                    faces.AddRange(new[] { inner + i, inner + next, outer + next, inner + i, outer + next, outer + i });
                }
            }
            return MakeExpansionArtMesh("随扩建升起的海岸草地", points, faces, colors, weights);
        }

        Color ExpansionSurfaceColor(Vector3 position, float rim)
        {
            float wornSoil = Mathf.PerlinNoise(position.x * .38f + 3, position.z * .47f + 18);
            var tint = Color.Lerp(grass.color, sandstone.color, rim + wornSoil * .076f);
            float curve = .16f * Mathf.Sin(position.x * 1.27f + 2);
            float approach = 1 - Ease(-4.88f, -3.08f, position.x);
            tint.a = approach * (1 - Ease(.38f, .91f, Mathf.Abs(position.z - curve)));
            return tint;
        }

        Mesh MakeExpansionWaist(Mesh original, Vector3 landOrigin)
        {
            var outline = original.vertices;
            var oldTop = original.GetTriangles(0);
            var points = new List<Vector3>();
            var colors = new List<Color>();
            var weights = new List<Vector2>();
            var faces = new List<int>();
            // The existing waist consists of pairs along its length. Split only the render top
            // across its width so the pavers can wash out into the surrounding turf.
            int stations = oldTop.Length / 6 + 1;
            const int columns = 12;
            for (int station = 0; station < stations; station++)
            for (int column = 0; column <= columns; column++)
            {
                float across = column / (float)columns;
                var p = Vector3.Lerp(outline[station * 2], outline[station * 2 + 1], across);
                var pigment = ExpansionSurfaceColor(p, .17f * Ease(.76f, 1, Mathf.Abs(across * 2 - 1)));
                points.Add(p);
                colors.Add(pigment);
                weights.Add(Vector2.zero);
                if (column == 0 || station == 0) continue;
                int b = points.Count - 1, a = b - 1, c = a - columns - 1, d = b - columns - 1;
                faces.AddRange(new[] { c, d, b, c, b, a });
            }

            // All four faces close around the original waist. Its feet lie beneath the static
            // shore and travel with the waist without affecting the original walkable surface.
            var waistRim = new List<(Vector3 point, Vector3 outward)>();
            void AddWaistRim(int station, int column)
            {
                float x = station == 0 ? -1 : station == stations - 1 ? 1 : 0;
                float z = column == 0 ? -1 : column == columns ? 1 : 0;
                waistRim.Add((points[station * (columns + 1) + column], new Vector3(x, 0, z).normalized));
            }
            for (int station = 0; station < stations; station++) AddWaistRim(station, 0);
            for (int column = 1; column <= columns; column++) AddWaistRim(stations - 1, column);
            for (int station = stations - 2; station >= 0; station--) AddWaistRim(station, columns);
            for (int column = columns - 1; column > 0; column--) AddWaistRim(0, column);

            int start = points.Count;
            int rimCount = waistRim.Count;
            float[] spreads = { 0, .16f, .43f, .67f };
            float[] depths = { 0, .24f, .68f, 1.28f };
            for (int row = 0; row < spreads.Length; row++)
            for (int i = 0; i < rimCount; i++)
            {
                var edge = waistRim[i];
                float irregular = 1 + .12f * Mathf.Sin(i * .41f) + .08f * Mathf.Cos(i * 1.86f);
                var foot = edge.point + edge.outward * (spreads[row] * irregular) - Vector3.up * depths[row];
                if (row == spreads.Length - 1)
                {
                    float shore = CoastHeight(foot.x + landOrigin.x, foot.z + landOrigin.z) - landOrigin.y;
                    foot.y = Mathf.Min(foot.y, shore - .20f, CoastSeaLevel - landOrigin.y - .14f);
                }
                points.Add(foot);
                float rock = Ease(0, .36f, depths[row]);
                var pigment = Color.Lerp(ExpansionSurfaceColor(edge.point, .16f), cliff.color, rock);
                pigment.a = 0;
                colors.Add(pigment);
                weights.Add(new Vector2(Ease(.38f, 1.1f, depths[row]) * .72f, rock));
                if (row == 0) continue;
                int next = (i + 1) % rimCount;
                int a = start + (row - 1) * rimCount + i, b = start + (row - 1) * rimCount + next;
                int c = start + row * rimCount + i, d = start + row * rimCount + next;
                faces.AddRange(new[] { a, b, d, a, d, c });
            }
            return MakeExpansionArtMesh("连接旧岸的原腰带表层", points, faces, colors, weights);
        }

        Mesh MakeExpansionRim(Vector3[] perimeter, Transform stones)
        {
            float[] depths = { 0, .13f, .31f, .58f, .84f, 1.08f, 1.36f, 1.68f };
            float[] spreads = { 0, .062f, .26f, .54f, .87f, 1.17f, 1.40f, 1.43f };
            var points = new List<Vector3>();
            var colors = new List<Color>();
            var weights = new List<Vector2>();
            var faces = new List<int>();
            int count = perimeter.Length;
            for (int row = 0; row < depths.Length; row++)
            for (int i = 0; i < count; i++)
            {
                float angle = 2 * Mathf.PI * i / count;
                var edge = perimeter[i];
                var radial = new Vector3(edge.x, 0, edge.z).normalized;
                float headland = .79f + .23f * Mathf.Sin(angle * 3 + .9f) + .16f * Mathf.Cos(angle * 7 + .6f);
                float approach = Ease(.68f, .99f, -radial.x) * (1 - Ease(.36f, .63f, Mathf.Abs(radial.z)));
                float reach = spreads[row] * headland * (1 - approach * .84f);
                var p = edge + radial * reach - Vector3.up * depths[row];
                if (row > 0) p.y += .04f * Mathf.Sin(angle * 11 + row * 6.17f);
                points.Add(p);
                float rock = Ease(.10f, .55f, depths[row]);
                var pigment = Color.Lerp(grass.color, cliff.color, rock);
                pigment.a = 0;
                colors.Add(pigment);
                weights.Add(new Vector2(Ease(.57f, 1.45f, depths[row]) * .72f, rock));
                if (row == 0) continue;
                int next = (i + 1) % count;
                int a = (row - 1) * count + i, b = (row - 1) * count + next;
                int c = row * count + i, d = row * count + next;
                faces.AddRange(new[] { a, b, d, a, d, c });

                if (row == 4 && i % 4 == 1 && approach < .44f)
                {
                    float variant = .5f + .5f * Mathf.Cos(angle * 11.7f + 1.56f);
                    Asset("moss-boulder", stones, p - Vector3.up * .095f, .28f + .34f * variant, i * 39);
                }
            }
            return MakeExpansionArtMesh("岩脚由草皮下倾向潮线", points, faces, colors, weights);
        }

        Mesh MakeExpansionArtMesh(string name, List<Vector3> points, List<int> faces,
            List<Color> colors, List<Vector2> weights)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points);
            mesh.SetTriangles(faces, 0);
            mesh.SetColors(colors);
            mesh.SetUVs(1, weights);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }
    }
}
