using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed partial class DawnCoast : MonoBehaviour
    {
        readonly List<Material> materials = new();
        readonly List<Mesh> meshes = new();
        readonly List<Texture2D> textures = new();
        readonly List<NavMeshData> navigation = new();
        readonly List<Renderer> falls = new();
        readonly List<Light> nightLamps = new();
        readonly System.Random scatter = new(16521);
        DawnGardenGame game;
        Transform ground, details;
        SmallWorldLibrary library;
        Material grass, cliff, slate, limestone, sandstone, brick, path, wetStone, darkWood, wood, roof, azure, water, flow, foam, brass, soil, leaf, carrots, glass, warmGlass;
        Transform wheel;
        Light fill;
        MaterialPropertyBlock flowBlock;
        SmallWorldWarmLantern giftLamp;

        public Vector3 GiftLampPoint => Grotto + new Vector3(1.8f, 0, -.8f);

        public void Build(DawnGardenGame target, SmallWorldLibrary assets) => BuildReferenceWorld(target, assets);

        void BuildPreviousLayout(DawnGardenGame target, SmallWorldLibrary assets)
        {
            game = target;
            library = assets;
            var world = game.expansion.surface.transform;
            transform.SetParent(world, false);
            ground = Group("可行走地形与路径", transform);
            details = Group("场景美术", transform);
            CreateMaterials();

            foreach (var item in world.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == "Walkable Island" || item.name == "Central Boulder" || item.name == "North Rocks" ||
                    item.name == "West Boulder" || item.name == "Starter landscape details" || item.name == "Garden Polish Decorations" ||
                    item.name == "Shore rim stone" || item.name == "Stepping Stone" || item.name == "Travel Marker" ||
                    item.name == "Travel Marker Base" || item.name == "Tree Crown" || item.name == "Tree Crown Tip" ||
                    item.name == "Tree Trunk" || item.name.StartsWith("Formal Tree ") ||
                    item.name.StartsWith("Formal Travel") || item.name == "Travel Crystal")
                {
                    if (!item.IsChildOf(game.expansion.land)) item.gameObject.SetActive(false);
                }
            }
            var seed = game.FindInteraction(GardenInteractionKind.Water);
            var seedMove = new Vector3(-16.1f, 2.61f, -20.6f) - seed.transform.position;
            if (!seed.approach.IsChildOf(seed.transform)) seed.approach.position += seedMove;
            seed.transform.position += seedMove;
            seed.displayName = "给苗圃浇水";
            foreach (var child in world.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Seed bed border")))
                if (!child.IsChildOf(seed.transform)) child.gameObject.SetActive(false);

            BuildTerrain();
            BuildHome();
            BuildNursery();
            BuildWorkshop();
            BuildGrotto();
            BuildFerry();
            BuildStars();
            BuildLivingGarden();
            BuildDistantShore();
            BuildExpansionArt();
            ConfigureSampleCamera();

            game.atmosphere.daylightExposure = .33f;
            game.atmosphere.nightExposure = .79f;
            game.walkSpeed = 4.1f;
            game.worldCamera.fieldOfView = 43;
            game.worldCamera.farClipPlane = 650;
            var skyShader=Resources.Load<Shader>("Coast/GardenSky");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "海岸天空" };
                RenderSettings.skybox=sky;
                game.worldCamera.clearFlags=CameraClearFlags.Skybox;
                materials.Add(sky);
            }
            if(game.atmosphere.sunDisc != null)game.atmosphere.sunDisc.gameObject.SetActive(false);
            if(game.atmosphere.moonDisc != null)game.atmosphere.moonDisc.gameObject.SetActive(false);
            fill = Group("沿岸柔光", details).gameObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.79f,.9f,1f);
            fill.transform.rotation = Quaternion.Euler(44,205,0);
            flowBlock = new MaterialPropertyBlock();
            game.orbit.largeWorld = true;
            game.orbit.expandedMaxDistance = 145;
            game.orbit.expandedOverviewDistance = 112;
            game.orbit.expandedOverviewCenter = Overview;
            game.orbit.overview = false;
            game.orbit.yaw = 169;
            game.orbit.pitch = 30;
            game.orbit.distance = 12.5f;
            var ocean = GameObject.Find("Ocean")?.GetComponent<Renderer>();
            if (ocean != null)
            {
                var mat = new Material(ocean.sharedMaterial);
                materials.Add(mat);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(.15f, .38f, .47f));
                if (mat.HasProperty("_ShallowColor")) mat.SetColor("_ShallowColor", new Color(.26f, .63f, .64f));
                ocean.sharedMaterial = mat;
                ocean.transform.localScale = new Vector3(1450, ocean.transform.localScale.y, 1450);
            }
            BuildNavigation();
        }

        void BuildNavigation()
        {
            var expansion = game.expansion;
            var agent = game.navigator;
            var position = game.actor.position;
            var heading = game.actor.eulerAngles.y;
            agent.enabled = false;
            expansion.surface.RemoveData();
            expansion.land.gameObject.SetActive(false);
            Physics.SyncTransforms();
            RebuildPlayableNavigation();
            expansion.originalNavigation = expansion.surface.navMeshData;
            expansion.land.gameObject.SetActive(true);
            expansion.landCollider.enabled = true;
            foreach (var seam in expansion.seamStones) if (seam != null) seam.SetActive(false);
            Physics.SyncTransforms();
            RebuildPlayableNavigation();
            expansion.expandedNavigation = expansion.surface.navMeshData;
            bool unlocked = game.Progress.firstExpansionUnlocked;
            expansion.land.gameObject.SetActive(unlocked);
            expansion.landCollider.enabled = unlocked;
            foreach (var seam in expansion.seamStones) if (seam != null) seam.SetActive(!unlocked);
            expansion.surface.RemoveData();
            expansion.surface.navMeshData = unlocked ? expansion.expandedNavigation : expansion.originalNavigation;
            expansion.surface.AddData();
            if (!NavMesh.SamplePosition(position, out var hit, .75f, NavMesh.AllAreas)) position = game.spawn.position;
            else position = hit.position;
            game.PlaceActor(position, heading);
        }

        // The project's historical bake allowed 75 cm ledges, but this pet's controller can
        // climb only 25 cm. Use its real limits for this landscape and for subsequent edits.
        public void RebuildPlayableNavigation()
        {
            var surface=game.expansion.surface;
            var settings=surface.GetBuildSettings();
            settings.agentClimb=Mathf.Min(.20f,game.body.stepOffset*.8f);
            settings.agentSlope=Mathf.Min(38,game.body.slopeLimit-2);
            // Resolve the narrow garden crossings without rounding a 20cm tread
            // into a whole default bake voxel. Radius and physical limits stay intact.
            settings.overrideVoxelSize=true;settings.voxelSize=.08f;
            var sources=new List<NavMeshBuildSource>();
            var marks=new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(surface.transform,surface.layerMask,
                NavMeshCollectGeometry.PhysicsColliders,0,marks,sources);
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,
                new Bounds(new Vector3(10,6,-12),new Vector3(150,56,150)),surface.transform.position,surface.transform.rotation);
            if(data==null)throw new InvalidOperationException("The coast navigation bake failed.");
            navigation.Add(data);
            surface.RemoveData();surface.navMeshData=data;surface.AddData();
        }

        // Call after committing surface.navMeshData to expandedNavigation. The
        // original surface remains valid for locked land and guardian reveals;
        // only superseded runtime bakes owned by this coast may be destroyed.
        internal void ReleaseUnusedNavigation()
        {
            if (game == null || game.expansion == null) return;
            var expansion = game.expansion;
            for (int i = navigation.Count - 1; i >= 0; i--)
            {
                var data = navigation[i];
                if (data == expansion.originalNavigation || data == expansion.expandedNavigation ||
                    data == expansion.surface.navMeshData) continue;
                navigation.RemoveAt(i);
                if (data != null) Destroy(data);
            }
        }

        internal SmallWorldWarmLantern InstallGift(SmallWorldItem gift)
        {
            if (giftLamp != null) return giftLamp;
            var holder = Group("阿烬的暖焰灯", details);
            holder.position = GiftLampPoint;
            Cylinder(holder, "暖灯底座", new Vector3(0,.23f,0), new Vector3(.6f,.23f,.6f), limestone);
            var asset = library.assets.FirstOrDefault(a => a.id == gift.assetId)?.prefab;
            if (asset != null)
            {
                var model = Instantiate(asset, holder, false);
                model.transform.localPosition = Vector3.up * .38f;
                model.transform.localScale *= .57f;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
                SmallWorldAssetLoader.Tint(model, gift.tint);
            }
            giftLamp = holder.gameObject.AddComponent<SmallWorldWarmLantern>();
            giftLamp.peakIntensity = 4.8f;
            giftLamp.Build();
            return giftLamp;
        }

        public void RemoveGift()
        {
            if (giftLamp == null) return;
            Destroy(giftLamp.gameObject);
            giftLamp = null;
        }

        void Update()
        {
            if (game == null || game.atmosphere == null) return;
            float now = Time.time;
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0, 0, now * 19);
            fill.intensity = Mathf.Lerp(.045f,.09f,game.atmosphere.Daylight);
            flowBlock.SetVector("_BaseMap_ST", new Vector4(1, 1.5f, 0, -now * .55f));
            foreach (var renderer in falls)
            {
                renderer.SetPropertyBlock(flowBlock);
            }
            foreach (var lamp in nightLamps)
            {
                lamp.intensity = Mathf.Lerp(1.25f, 1.85f, 1 - game.atmosphere.Daylight);
            }
        }

        void OnDestroy()
        {
            DisposeExpansionArt();
            RestoreSampleAtmosphere();
            RestoreReferenceOceanMesh();
            foreach (var item in meshes) if (item != null) Destroy(item);
            foreach (var item in materials) if (item != null) Destroy(item);
            foreach (var item in textures) if (item != null) Destroy(item);
            foreach (var item in navigation) if (item != null) Destroy(item);
        }
    }
}
