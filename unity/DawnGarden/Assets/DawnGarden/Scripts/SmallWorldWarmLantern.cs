using System.Linq;
using UnityEngine;

namespace DawnGarden
{
    sealed class SmallWorldWarmLantern : MonoBehaviour
    {
        Light glow;
        Material frame, glass;
        float brightness;
        public float peakIntensity = 2.2f;

        public Light Glow => glow;

        public void Build()
        {
            var bounds = GetComponentsInChildren<Renderer>();
            var top = bounds.Length == 0 ? 1.25f :
                transform.InverseTransformPoint(transform.position + Vector3.up *
                    (bounds.Max(renderer => renderer.bounds.max.y) - transform.position.y)).y;
            var center = Vector3.up * (top + .21f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            frame = new Material(shader) { name = "暖焰灯 · 黑铜" };
            frame.color = new Color(.30f, .16f, .075f);
            frame.SetFloat("_Smoothness", .33f);
            glass = new Material(shader) { name = "暖焰灯 · 琥珀纸" };
            glass.color = new Color(.98f, .61f, .24f);
            glass.EnableKeyword("_EMISSION");
            AddPiece(PrimitiveType.Cylinder, center, new Vector3(.33f, .18f, .33f), glass);
            AddPiece(PrimitiveType.Cylinder, center + Vector3.up * .20f, new Vector3(.47f, .025f, .47f), frame);
            AddPiece(PrimitiveType.Cylinder, center - Vector3.up * .19f, new Vector3(.43f, .031f, .43f), frame);
            for (int i = 0; i < 4; ++i)
            {
                var edge = Quaternion.Euler(0, 45 + 90 * i, 0) * Vector3.forward * .18f;
                AddPiece(PrimitiveType.Cube, center + edge, new Vector3(.035f, .39f, .035f), frame);
            }
            var lightObject = new GameObject("暖焰灯 · 火芯");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = center + Vector3.up * .24f;
            glow = lightObject.AddComponent<Light>();
            glow.color = new Color(1f, .78f, .53f);
            glow.type = LightType.Point;
            glow.range = 5.0f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;
            glass.SetColor("_EmissionColor", Color.black);
        }

        public void UpdateLight(bool lit)
        {
            brightness = Mathf.MoveTowards(brightness, lit ? 1 : 0, Time.unscaledDeltaTime * 1.65f);
            glow.intensity = brightness * peakIntensity;
            glow.enabled = brightness > .005f;
            glass.SetColor("_EmissionColor", new Color(1f, .63f, .27f) * (brightness * .65f));
        }

        void AddPiece(PrimitiveType shape, Vector3 point, Vector3 scale, Material material)
        {
            var piece = GameObject.CreatePrimitive(shape);
            piece.name = material.name;
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point;
            piece.transform.localScale = scale;
            var collider = piece.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        void OnDestroy()
        {
            if (frame != null) Destroy(frame);
            if (glass != null) Destroy(glass);
        }
    }
}
