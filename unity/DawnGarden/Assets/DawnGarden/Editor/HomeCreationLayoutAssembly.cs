using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace DawnGarden.Editor
{
    // Builds an isolated scene for one home-creation run. The source home is opened
    // as a template and saved under PreparedHomes; it is never edited in place.
    public static class HomeCreationLayoutAssembly
    {
        [Serializable]
        public class Request
        {
            public string runId, plotId, sourceScenePath, outputScenePath, outputDirectory;
            public string layoutJsonPath, previewPath, prefabPath, terrainTemplateRef;
            public int revision;
            public float corridorWidth = 1.4f;
            public Vector3 benchPosition = new Vector3(1.5f, 0, 0.8f);
            public Vector3 benchEuler = new Vector3(0, 180, 0);
            public Vector3 benchScale = Vector3.one;
            public Vector3 plotCenter = new Vector3(11.7f, 0, 0);
            public Vector2 plotSize = new Vector2(12.3f, 10.3f);
            public string plotMeshPath;
        }

        [Serializable]
        public class Anchor
        {
            public string side;
            public float corridorWidthMetres;
            public Vector3 center;
            public Vector3 direction;
            public bool reserved;
        }

        [Serializable]
        public class Instance
        {
            public string instanceId, assetId, sourceAssetRef, interactionKind;
            public Vector3 localPosition, localEulerAngles, localScale;
            public bool isStatic;
        }

        [Serializable]
        public class Layout
        {
            public int schemaVersion = 1;
            public string runId, revision, assemblyVersion, plotId, terrainTemplateRef, scenePath, sceneSha256;
            public Anchor[] connectionAnchors;
            public Instance[] instances;
            public string[] interactionBindings;
            public Environment environment;
            public string[] sourceAssetRefs;
            public Checks checks;
        }

        [Serializable]
        public class Environment
        {
            public string theme;
            public string template;
            public string dayNight;
        }

        [Serializable]
        public class Checks
        {
            public bool westConnectionClear, insidePlot, simpleCollider, sourceSceneUnchanged;
        }

        [Serializable]
        public class Report
        {
            public bool passed, sourceSceneUnchanged, activeScenePreserved;
            public string utc, runId, scenePath, sceneSha256, previewPath, layoutJsonPath;
            public Vector3 benchPosition, benchEuler, benchScale, benchBoundsMin, benchBoundsMax;
            public string[] checks;
        }

        // Batchmode entry point: Unity's executeMethod does not pass method arguments,
        // so the request file is supplied after -layoutRequest.
        public static void BuildFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            var marker = Array.IndexOf(args, "-layoutRequest");
            var requestPath = marker >= 0 && marker + 1 < args.Length
                ? args[marker + 1]
                : Path.Combine(Root, "artifacts/home-creation/layout/3ea386b9e1f54c399406cc67a44c4430/layout-request.json");
            Build(requestPath);
        }

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));

        public static void Build(string requestPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Leave Play Mode and wait for compilation before assembling a home.");

            var req = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            Require(req.runId, "runId"); Require(req.plotId, "plotId"); Require(req.sourceScenePath, "sourceScenePath");
            Require(req.outputScenePath, "outputScenePath"); Require(req.prefabPath, "prefabPath");
            Require(req.layoutJsonPath, "layoutJsonPath"); Require(req.previewPath, "previewPath");
            if (req.revision < 1) throw new InvalidDataException("revision must be positive");
            if (!req.sourceScenePath.StartsWith("Assets/DawnGarden/Scenes/", StringComparison.Ordinal))
                throw new InvalidDataException("Source scene must be a DawnGarden template.");
            if (!req.outputScenePath.StartsWith("Assets/DawnGarden/PreparedHomes/", StringComparison.Ordinal)
                || !req.prefabPath.StartsWith("Assets/DawnGarden/PreparedHomes/", StringComparison.Ordinal))
                throw new InvalidDataException("Prepared output must stay under PreparedHomes.");
            if (string.IsNullOrWhiteSpace(req.plotMeshPath)) req.plotMeshPath = Path.GetDirectoryName(req.outputScenePath).Replace('\\', '/') + "/CourtyardIsland.asset";
            if (!req.plotMeshPath.StartsWith("Assets/DawnGarden/PreparedHomes/", StringComparison.Ordinal))
                throw new InvalidDataException("Prepared terrain mesh must stay under PreparedHomes.");
            if (req.corridorWidth < 1.4f) throw new InvalidDataException("The west connection corridor is too narrow.");

            var sourceAbsolute = ProjectPath(req.sourceScenePath);
            var sourceHashBefore = Hash(sourceAbsolute);
            var activeBefore = SceneManager.GetActiveScene();
            var activePathBefore = activeBefore.path;
            var activeDirtyBefore = activeBefore.isDirty;
            var report = new Report {
                utc = DateTime.UtcNow.ToString("o"), runId = req.runId,
                scenePath = req.outputScenePath, previewPath = req.previewPath,
                layoutJsonPath = req.layoutJsonPath, benchPosition = req.benchPosition,
                benchEuler = req.benchEuler, benchScale = req.benchScale
            };
            var checks = new List<string>();
            Scene scene = default;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                scene = EditorSceneManager.OpenScene(req.sourceScenePath, OpenSceneMode.Single);
                if (!scene.IsValid()) throw new InvalidOperationException("Could not open the terrain template.");

                var sourceGround = FindRequired("Walkable Island");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(req.prefabPath);
                if (prefab == null) throw new FileNotFoundException("Prepared bench prefab is missing", req.prefabPath);

                // Replace the old small placeholder extension with a new starter-sized plot.
                var game = UnityEngine.Object.FindFirstObjectByType<DawnGarden.DawnGardenGame>();
                var expansion = game.expansion;
                if (expansion == null) throw new InvalidOperationException("Expansion runtime is missing.");
                UnityEngine.Object.DestroyImmediate(expansion.land.gameObject);
                var layoutRoot = new GameObject("Mossy Stone Courtyard - New Continent");
                layoutRoot.transform.SetParent(expansion.transform, false);
                layoutRoot.transform.position = req.plotCenter;
                var terrain = BuildCourtyard(req, layoutRoot.transform);
                var decorations = new GameObject("Mossy Stone Courtyard Decorations").transform;
                decorations.SetParent(terrain.transform, false);
                BuildCourtyardDetails(req, decorations);
                var bench = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                bench.name = "Mossy Stone Bench (layout)";
                bench.transform.SetParent(decorations.transform, false);
                bench.transform.localPosition = req.benchPosition;
                bench.transform.localEulerAngles = req.benchEuler;
                bench.transform.localScale = req.benchScale;

                expansion.land = terrain.transform;
                expansion.landCollider = terrain.GetComponent<MeshCollider>();
                expansion.finalPosition = req.plotCenter;
                expansion.revealHalfSize = req.plotSize * .5f;
                game.orbit.expandedOverviewCenter = req.plotCenter * .5f + Vector3.up * .5f;
                game.orbit.expandedOverviewDistance = 30f;
                game.orbit.expandedMaxDistance = 38f;
                expansion.seamStones = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(t => t.name == "Shore rim stone" && t.position.x > 4.8f && Mathf.Abs(t.position.z) < 1.25f)
                    .Select(t => t.gameObject).ToArray();
                foreach (var stone in expansion.seamStones) stone.SetActive(false);
                Physics.SyncTransforms();
                var surface = expansion.surface;
                surface.BuildNavMesh();
                var nav = surface.navMeshData;
                var navPath = Path.GetDirectoryName(req.outputScenePath).Replace('\\', '/') + "/CourtyardNavigation.asset";
                var savedNav = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                surface.RemoveData();
                if (savedNav == null) { AssetDatabase.CreateAsset(nav, navPath); savedNav = nav; }
                else { EditorUtility.CopySerialized(nav, savedNav); UnityEngine.Object.DestroyImmediate(nav); }
                expansion.expandedNavigation = savedNav;
                surface.navMeshData = savedNav; surface.AddData();
                var route = new NavMeshPath();
                var connected = NavMesh.CalculatePath(game.spawn.position, req.plotCenter, NavMesh.AllAreas, route)
                    && route.status == NavMeshPathStatus.PathComplete;
                checks.Add("new_continent_navigation_connected=" + connected);
                if (!connected) throw new InvalidOperationException("New continent is not connected to the starter home.");

                var collider = bench.GetComponentsInChildren<Collider>(true).FirstOrDefault();
                if (collider == null) throw new InvalidOperationException("Prepared bench has no collider.");
                var benchBounds = collider.bounds;
                report.benchBoundsMin = benchBounds.min;
                report.benchBoundsMax = benchBounds.max;

                var plotBounds = terrain.GetComponent<Collider>().bounds;
                var inside = benchBounds.min.x >= plotBounds.min.x && benchBounds.max.x <= plotBounds.max.x
                    && benchBounds.min.z >= plotBounds.min.z && benchBounds.max.z <= plotBounds.max.z
                    && benchBounds.min.y >= -0.05f;
                var westClear = !PathBlocked(terrain.transform, benchBounds, req.corridorWidth);
                var simpleCollider = bench.GetComponentsInChildren<Collider>(true).All(c => c is BoxCollider || c is CapsuleCollider || c is SphereCollider);
                var plotSize = plotBounds.size;
                checks.Add("inside_plot=" + inside);
                checks.Add("west_connection_clear=" + westClear);
                checks.Add("simple_collider=" + simpleCollider);
                if (!inside || !westClear || !simpleCollider) throw new InvalidOperationException("Layout boundary or collider check failed.");

                var sourceMesh = sourceGround.GetComponent<MeshFilter>().sharedMesh;
                checks.Add("starter_home_preserved=" + (sourceMesh != null));
                checks.Add("separate_starter_sized_plot=" + (plotSize.x >= 12f && plotSize.z >= 10f));
                checks.Add("entry_path_reaches_plot=" + (Vector3.Distance(req.plotCenter + new Vector3(-5.9f, 0, 0), req.plotCenter + new Vector3(-3.2f, 0, -.2f)) > 2f));

                var outputSceneAbsolute = ProjectPath(req.outputScenePath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputSceneAbsolute));
                EditorSceneManager.SaveScene(scene, req.outputScenePath);
                AssetDatabase.SaveAssets();

                // Capture from the same elevated perspective used by the garden previews.
                var previewCameraObject = new GameObject("__Layout Preview Camera");
                var camera = previewCameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.55f, .70f, .64f);
                camera.orthographic = true;
                camera.orthographicSize = 7.1f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 180;
                camera.transform.position = req.plotCenter + new Vector3(.3f, 17f, -13.5f);
                camera.transform.LookAt(req.plotCenter + new Vector3(0, 0, .25f));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(req.previewPath)));
                Capture(camera, Path.GetFullPath(req.previewPath));
                camera.orthographicSize = 14.2f;
                camera.transform.position = new Vector3(6, 25, -20);
                camera.transform.LookAt(new Vector3(5.5f, 0, 0));
                Capture(camera, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(req.previewPath)), "adjacency.png"));
                UnityEngine.Object.DestroyImmediate(previewCameraObject);

                var sceneHash = Hash(outputSceneAbsolute);
                report.sceneSha256 = sceneHash;
                report.sourceSceneUnchanged = sourceHashBefore == Hash(sourceAbsolute);
                report.activeScenePreserved = SceneManager.GetActiveScene().path == req.outputScenePath;
                report.passed = true;

                var layout = new Layout {
                    runId = req.runId, revision = req.revision.ToString(), plotId = req.plotId,
                    terrainTemplateRef = req.terrainTemplateRef,
                    scenePath = req.outputScenePath, sceneSha256 = sceneHash,
                    assemblyVersion = "v2",
                    connectionAnchors = new[] { new Anchor { side = "west", corridorWidthMetres = req.corridorWidth,
                        center = req.plotCenter + new Vector3(-req.plotSize.x * .5f + .25f, 0, 0), direction = Vector3.right, reserved = true } },
                    instances = new[] {
                        new Instance { instanceId = "mossy-stone-bench-01", assetId = "mossy-stone-bench",
                            sourceAssetRef = req.prefabPath, localPosition = req.benchPosition,
                            localEulerAngles = req.benchEuler, localScale = req.benchScale,
                            interactionKind = null, isStatic = true }
                    },
                    interactionBindings = new string[0],
                    environment = new Environment { theme = "苔石小院", template = "Starter-sized adjacent courtyard continent", dayNight = "reuse existing" },
                    sourceAssetRefs = new[] { req.prefabPath },
                    checks = new Checks { westConnectionClear = westClear, insidePlot = inside,
                        simpleCollider = simpleCollider, sourceSceneUnchanged = report.sourceSceneUnchanged }
                };
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(req.layoutJsonPath)));
                File.WriteAllText(Path.GetFullPath(req.layoutJsonPath), JsonUtility.ToJson(layout, true));
                report.checks = checks.ToArray();
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(req.previewPath)), "unity-layout-report.json"), JsonUtility.ToJson(report, true));
            }
            finally
            {
                // Keep the generated scene open for inspection; restore only when the
                // build fails before SaveScene or when the caller had no active scene.
                if (!string.IsNullOrEmpty(activePathBefore) && File.Exists(ProjectPath(activePathBefore)) && SceneManager.GetActiveScene().path != req.outputScenePath)
                    EditorSceneManager.OpenScene(activePathBefore, OpenSceneMode.Single);
            }
            Debug.Log("Home creation layout assembled: " + req.outputScenePath);
        }

        static GameObject BuildCourtyard(Request req, Transform parent)
        {
            var root = new GameObject("Courtyard Island Ground");
            root.layer = LayerMask.NameToLayer("GardenGround");
            root.transform.SetParent(parent, false);
            var mesh = CourtyardMesh(req.plotSize, req.plotMeshPath);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/DawnGarden/Art/Polish/Soft Meadow.mat");
            var cliff = AssetDatabase.LoadAssetAtPath<Material>("Assets/DawnGarden/Art/Polish/Warm Limestone Cliff.mat");
            if (grass == null || cliff == null) throw new FileNotFoundException("Courtyard terrain materials are missing.");
            var grassPath = Path.GetDirectoryName(req.outputScenePath).Replace('\\', '/') + "/Courtyard Meadow.mat";
            var meadow = AssetDatabase.LoadAssetAtPath<Material>(grassPath);
            if (meadow == null) { meadow = new Material(grass); AssetDatabase.CreateAsset(meadow, grassPath); }
            meadow.SetColor("_BaseColor", new Color(.65f, .69f, .43f));
            meadow.SetFloat("_Smoothness", .03f);
            EditorUtility.SetDirty(meadow);
            root.AddComponent<MeshRenderer>().sharedMaterials = new[] { meadow, cliff };
            root.AddComponent<MeshCollider>().sharedMesh = mesh;
            return root;
        }

        static Mesh CourtyardMesh(Vector2 size, string assetPath)
        {
            const int count = 96;
            var vertices = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new Vector2(size.x * .5f / 3.2f, size.y * .5f / 3.2f) };
            var top = new List<int>(); var sides = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var a = i * Mathf.PI * 2 / count;
                var r = 1 + .025f * Mathf.Sin(3 * a + .4f) + .018f * Mathf.Cos(7 * a);
                var v = new Vector3(Mathf.Cos(a) * size.x * .5f * r, .025f, Mathf.Sin(a) * size.y * .5f * r);
                vertices.Add(v); uv.Add(new Vector2((v.x + size.x * .5f) / 3.2f, (v.z + size.y * .5f) / 3.2f));
            }
            for (var i = 0; i < count; i++) top.AddRange(new[] { 0, 1 + (i + 1) % count, 1 + i });
            for (var i = 0; i < count; i++)
            {
                var a = i * Mathf.PI * 2 / count;
                var b = (i + 1) * Mathf.PI * 2 / count;
                var ra = 1 + .025f * Mathf.Sin(3 * a + .4f) + .018f * Mathf.Cos(7 * a);
                var rb = 1 + .025f * Mathf.Sin(3 * b + .4f) + .018f * Mathf.Cos(7 * b);
                var va = new Vector3(Mathf.Cos(a) * size.x * .5f * ra, .025f, Mathf.Sin(a) * size.y * .5f * ra);
                var vb = new Vector3(Mathf.Cos(b) * size.x * .5f * rb, .025f, Mathf.Sin(b) * size.y * .5f * rb);
                var baseIndex = vertices.Count;
                vertices.AddRange(new[] { va, vb, vb * .89f + Vector3.down * 1.35f, va * .89f + Vector3.down * 1.35f });
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                sides.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 });
            }
            var mesh = new Mesh { name = "Mossy Courtyard Starter Plot", subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(top, 0); mesh.SetTriangles(sides, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectPath(assetPath)));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, assetPath);
            else { UpdateMesh(existing, mesh); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            return mesh;
        }

        static void BuildCourtyardDetails(Request req, Transform parent)
        {
            var rocks = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DawnGarden/Prefabs/Garden/moss-boulder.prefab");
            var flower = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DawnGarden/Prefabs/Garden/meadow-flower.prefab");
            if (rocks == null || flower == null) throw new FileNotFoundException("Courtyard decoration prefabs are missing.");
            var ring = new[] {
                new Vector3(-4.8f,0,2.5f), new Vector3(-3.5f,0,4.0f), new Vector3(-1.6f,0,4.45f),
                new Vector3(.6f,0,4.5f), new Vector3(2.8f,0,4.15f), new Vector3(4.7f,0,3.1f),
                new Vector3(5.25f,0,1.35f), new Vector3(5.3f,0,-1.1f), new Vector3(4.35f,0,-3.25f),
                new Vector3(2.5f,0,-4.2f), new Vector3(.3f,0,-4.5f), new Vector3(-2.3f,0,-4.25f),
                new Vector3(-4.5f,0,-3.1f), new Vector3(-5.15f,0,-1.8f)
            };
            for (var i = 0; i < ring.Length; i++)
            {
                if (ring[i].x < -4.8f && Mathf.Abs(ring[i].z) < 1.6f) continue;
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(rocks, parent);
                rock.name = "Mossy edge boulder"; rock.transform.localPosition = ring[i] + Vector3.up * .02f;
                var s = .55f + (i % 4) * .13f; rock.transform.localScale = Vector3.one * s;
                rock.transform.localRotation = Quaternion.Euler((i % 3) * 3f, i * 31f, (i % 2 == 0 ? -1 : 1) * 4f);
            }
            // A compact, gently curving soil path enters from the existing home on the west.
            var pathMatPath = Path.GetDirectoryName(req.outputScenePath).Replace('\\', '/') + "/Courtyard Path.mat";
            var pathMat = AssetDatabase.LoadAssetAtPath<Material>(pathMatPath);
            if (pathMat == null)
            {
                pathMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pathMat.name = "Courtyard Path Earth"; pathMat.SetColor("_BaseColor", new Color(.56f, .40f, .24f));
                pathMat.SetFloat("_Smoothness", .08f); AssetDatabase.CreateAsset(pathMat, pathMatPath);
            }
            ConfigurePathMaterial(req, pathMat);
            BuildPath(req, parent, pathMat);
            // Smaller rocks and grouped flowers fill the gaps without obstructing the open center.
            for (var i = 0; i < ring.Length; i++)
            {
                var next = ring[(i + 1) % ring.Length];
                if (Vector3.Distance(ring[i], next) > 3f) continue; // west entrance stays open
                var midpoint = (ring[i] + next) * .5f;
                var pebble = (GameObject)PrefabUtility.InstantiatePrefab(rocks, parent);
                pebble.name = "Courtyard smaller moss stone";
                pebble.transform.localPosition = midpoint + Vector3.up * .015f;
                pebble.transform.localScale = new Vector3(.29f + .06f * (i % 3), .30f, .34f);
                pebble.transform.localRotation = Quaternion.Euler(0, i * 53, 0);
                for (var j = 0; j < 9; j++)
                {
                    var a = j * 2.39996f + i;
                    var radius = .23f + .15f * (j % 3);
                    var pos = midpoint * .94f + new Vector3(Mathf.Cos(a) * radius, .035f, Mathf.Sin(a) * radius);
                    var plant = (GameObject)PrefabUtility.InstantiatePrefab(flower, parent);
                    plant.name = "Courtyard clustered daisies";
                    plant.transform.localPosition = pos;
                    plant.transform.localScale = Vector3.one * (1.25f + .23f * (j % 4));
                    plant.transform.localRotation = Quaternion.Euler(0, i * 41 + j * 37, 0);
                    foreach (var c in plant.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                }
            }
            var edgeFlowers = new[] { new Vector3(-4.4f,0,2.9f),new Vector3(-3.4f,0,3.8f),new Vector3(-1.2f,0,4.1f),new Vector3(1.3f,0,4.05f),new Vector3(3.5f,0,3.4f),new Vector3(4.3f,0,2.0f),new Vector3(4.5f,0,-2.2f),new Vector3(3.2f,0,-3.7f),new Vector3(.8f,0,-4.0f),new Vector3(-2.9f,0,-3.5f),new Vector3(-4.2f,0,-2.4f) };
            for (var i = 0; i < edgeFlowers.Length; i++)
            {
                var plant = (GameObject)PrefabUtility.InstantiatePrefab(flower, parent); plant.name = "Courtyard edge flowers";
                plant.transform.localPosition = edgeFlowers[i] + Vector3.up * .035f; plant.transform.localScale *= 2.0f + .25f * (i % 3);
            }
        }

        static void BuildPath(Request req, Transform parent, Material material)
        {
            var points = new[] { new Vector3(-6.25f,.045f,.22f), new Vector3(-5.35f,.045f,.18f), new Vector3(-4.65f,.045f,-.02f), new Vector3(-3.95f,.045f,-.32f), new Vector3(-2.65f,.045f,-.6f) };
            const float halfWidth = .62f;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (var i = 0; i < points.Length; i++)
            {
                var tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                var side = new Vector3(-tangent.z, 0, tangent.x) * halfWidth;
                vertices.Add(points[i] - side); vertices.Add(points[i] + side);
                uv.Add(new Vector2(0, (float)i / (points.Length - 1))); uv.Add(new Vector2(1, (float)i / (points.Length - 1)));
                if (i == 0) continue;
                var v = i * 2;
                triangles.AddRange(new[] { v - 2, v + 1, v, v - 2, v - 1, v + 1 });
            }
            var mesh = new Mesh { name = "Courtyard Curved Earth Entry" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            var path = Path.GetDirectoryName(req.outputScenePath).Replace('\\', '/') + "/CourtyardPath.asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored == null) AssetDatabase.CreateAsset(mesh, path);
            else { UpdateMesh(stored, mesh); UnityEngine.Object.DestroyImmediate(mesh); mesh = stored; }
            var go = new GameObject("Courtyard Curving Earth Entry"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void ConfigurePathMaterial(Request req, Material material)
        {
            const int width = 128, height = 512;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var u = x / (float)(width - 1); var v = y / (float)(height - 1);
                var grain = Mathf.PerlinNoise(x * .24f, y * .24f);
                var mottling = Mathf.PerlinNoise(x * .043f, y * .043f);
                var left = .10f + .07f * Mathf.PerlinNoise(1, v * 13);
                var right = .86f + .07f * Mathf.PerlinNoise(8, v * 11);
                var alpha = Mathf.SmoothStep(0, 1, (u-left)/.15f) * Mathf.SmoothStep(0, 1, (right-u)/.15f)
                    * Mathf.SmoothStep(0, 1, (1-v)/.28f);
                pixels[y * width + x] = new Color(.64f + mottling*.16f + grain*.06f, .45f + mottling*.15f + grain*.06f, .26f + mottling*.11f + grain*.05f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply();
            var path = Path.GetDirectoryName(req.outputScenePath).Replace('\\', '/') + "/Courtyard Earth.png";
            File.WriteAllBytes(ProjectPath(path), texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
        }

        static void UpdateMesh(Mesh target, Mesh source)
        {
            // Assign through the mesh API so native/GPU buffers refresh as well as serialized fields.
            target.Clear(); target.vertices = source.vertices; target.uv = source.uv;
            target.subMeshCount = source.subMeshCount;
            for (var i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
            target.RecalculateNormals(); target.RecalculateBounds(); target.UploadMeshData(false);
            EditorUtility.SetDirty(target);
        }

        static bool PathBlocked(Transform terrain, Bounds benchBounds, float corridor)
        {
            var benchLocal = terrain.InverseTransformPoint(benchBounds.center);
            return benchLocal.x < -corridor || Mathf.Abs(benchLocal.z) < corridor * .5f && benchLocal.x < -3.2f;
        }

        static void Require(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException(label + " is required.");
        }

        static GameObject FindRequired(string name)
        {
            var found = GameObject.Find(name);
            if (found == null) throw new InvalidOperationException("Template object is missing: " + name);
            return found;
        }

        static string ProjectPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var rt = new RenderTexture(1280, 960, 24);
            var image = new Texture2D(1280, 960, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
