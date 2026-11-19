using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed class SmallWorldDawnDressing : MonoBehaviour
    {
        readonly List<Material> materials = new();
        Light pavilionLight;
        GardenAtmosphere atmosphere;

        public void Build(DawnGardenGame game, SmallWorldLibrary library)
        {
            atmosphere = game.atmosphere;
            var root = new GameObject("晨曦 · 池畔木亭").transform;
            root.SetParent(transform, false);
            root.position = new Vector3(.85f, .04f, -2.5f);
            root.rotation = Quaternion.Euler(0, -14, 0);
            root.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var wood = Finish("蜜色老橡木", new Color(.48f, .31f, .21f), .14f);
            var woodLight = Finish("橡木端面", new Color(.63f, .42f, .27f), .13f);
            var stone = Finish("磨圆池岸石", new Color(.52f, .55f, .43f), .09f);
            var brass = Finish("旧铜", new Color(.45f, .31f, .14f), .39f);
            var paper = Finish("灯笼纸", new Color(.96f, .78f, .42f), .20f, .11f);
            var canopy = Finish("灰绿色蓬布", new Color(.61f, .70f, .48f), .09f);
            BuildPavilion(root, wood, woodLight, stone, brass, paper, canopy);

            var decor = new GameObject("池岸与路缘").transform;
            decor.SetParent(transform, false);
            decor.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var flower = library.assets.FirstOrDefault(x => x.id == "meadow-flower")?.prefab;
            var boulder = library.assets.FirstOrDefault(x => x.id == "moss-boulder")?.prefab;
            Dress(decor, flower, new Vector3(-.3f, .06f, -3.12f), .62f, 18);
            Dress(decor, flower, new Vector3(1.58f, .055f, -3.05f), .75f, 62);
            Dress(decor, flower, new Vector3(3.52f, .04f, .06f), .70f, 20);
            Dress(decor, boulder, new Vector3(4.34f, -.12f, -.95f), .31f, 36);
            Dress(decor, boulder, new Vector3(4.87f, -.18f, -1.51f), .42f, 61);
            Dress(decor, boulder, new Vector3(-4.72f, -.21f, .08f), .43f, 125);
            Dress(decor, boulder, new Vector3(-5.0f, -.20f, .72f), .47f, 142);
            VaryShore();
            SoftenOcean();
            if (game.worldCamera != null) game.worldCamera.fieldOfView = 37;
            atmosphere.daylightExposure = .26f;
            atmosphere.nightExposure = .69f;
        }

        void BuildPavilion(Transform root, Material wood, Material woodLight, Material stone, Material brass, Material paper, Material canopy)
        {
            for (int i = 0; i < 11; i++)
            {
                float x = (i - 5) * .204f;
                Box(root, "木平台", new Vector3(x, -.006f, 0), new Vector3(.199f, .061f, 1.70f), i % 3 == 0 ? wood : woodLight);
            }
            foreach (var x in new[] { -1.02f, 1.02f })
            {
                foreach (var z in new[] { -.68f, .66f })
                {
                    Box(root, "石柱脚", new Vector3(x, .085f, z), new Vector3(.25f, .17f, .26f), stone);
                    Box(root, "木立柱", new Vector3(x, 1.07f, z), new Vector3(.12f, 1.98f, .13f), wood);
                    Box(root, "柱头", new Vector3(x, 1.97f, z), new Vector3(.19f, .09f, .19f), woodLight);
                    Beam(root, new Vector3(x, 1.62f, z), new Vector3(x * .71f, 1.94f, z), .065f, wood);
                }
                Box(root, "纵梁", new Vector3(x, 2.025f, 0), new Vector3(.15f, .15f, 1.82f), wood);
            }
            for (int i = 0; i < 8; i++)
            {
                float z = (i - 3.5f) * .24f;
                Beam(root, new Vector3(-1.23f, 2.03f, z), new Vector3(0, 2.29f, z), .075f, woodLight);
                Beam(root, new Vector3(0, 2.29f, z), new Vector3(1.23f, 2.03f, z), .075f, woodLight);
            }
            var left = Box(root, "左侧蓬布", new Vector3(-.61f, 2.215f, -.05f), new Vector3(1.25f, .045f, 1.25f), canopy);
            var right = Box(root, "右侧蓬布", new Vector3(.61f, 2.215f, -.05f), new Vector3(1.25f, .045f, 1.25f), canopy);
            left.transform.localRotation = Quaternion.Euler(0, 0, 12);
            right.transform.localRotation = Quaternion.Euler(0, 0, -12);
            Box(root, "檐下背梁", new Vector3(0, 1.91f, -.68f), new Vector3(2.1f, .10f, .12f), wood);
            Box(root, "长凳座板", new Vector3(-.10f, .38f, -.46f), new Vector3(1.34f, .10f, .37f), woodLight);
            Box(root, "长凳背板", new Vector3(-.10f, .61f, -.63f), new Vector3(1.34f, .14f, .07f), wood);
            foreach (float x in new[] { -.59f, .38f })
                Box(root, "凳脚", new Vector3(x, .19f, -.46f), new Vector3(.10f, .37f, .26f), wood);

            var lamp = new Vector3(-.80f, 1.45f, .41f);
            Beam(root, lamp + Vector3.up * .13f, new Vector3(lamp.x, 2.10f, lamp.z), .018f, brass);
            Box(root, "纸灯", lamp, new Vector3(.19f, .25f, .19f), paper);
            Box(root, "灯顶", lamp + Vector3.up * .14f, new Vector3(.25f, .045f, .25f), brass);
            Box(root, "灯座", lamp - Vector3.up * .14f, new Vector3(.22f, .045f, .22f), brass);
            var glow = new GameObject("池边灯笼");
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = lamp + new Vector3(0, -.03f, .12f);
            pavilionLight = glow.AddComponent<Light>();
            pavilionLight.type = LightType.Point;
            pavilionLight.range = 3.5f;
            pavilionLight.color = new Color(1f, .69f, .37f);
        }

        void VaryShore()
        {
            var stones = FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "Shore rim stone").ToArray();
            foreach (var t in stones)
            {
                float angle = Mathf.Atan2(t.position.z, t.position.x);
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                float cluster = Mathf.Sin(angle * 3 + .3f) + .38f * Mathf.Cos(angle * 8);
                r.enabled = cluster > -.60f;
                t.localScale *= Mathf.Lerp(.71f, 1.12f, Mathf.InverseLerp(-1.4f, 1.4f, cluster));
            }
        }

        void SoftenOcean()
        {
            var ocean = GameObject.Find("Ocean")?.GetComponent<Renderer>();
            if (ocean == null) return;
            var material = new Material(ocean.sharedMaterial);
            materials.Add(material);
            material.SetColor("_BaseColor", new Color(.19f, .42f, .46f));
            material.SetColor("_ShallowColor", new Color(.40f, .65f, .65f));
            ocean.sharedMaterial = material;
        }

        static void Dress(Transform root, GameObject prefab, Vector3 position, float scale, float yaw)
        {
            if (prefab == null) return;
            var instance = Instantiate(prefab, root, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            instance.transform.localScale *= scale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        Material Finish(string name, Color color, float smoothness, float emission = 0)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission > 0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * emission); }
            materials.Add(mat);
            return mat;
        }

        static GameObject Box(Transform root, string name, Vector3 where, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = where;
            go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void Beam(Transform root, Vector3 from, Vector3 to, float width, Material material)
        {
            var delta = to - from;
            var beam = Box(root, "木构件", (from + to) * .5f, new Vector3(width, delta.magnitude, width), material);
            beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
        }

        void Update()
        {
            if (pavilionLight != null)
                pavilionLight.intensity = (1 - atmosphere.Daylight) * 1.8f;
        }

        void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
