using UnityEngine;

namespace DawnGarden
{
    // Hide tree visuals that lies between the camera and the pet; collision stays intact.
    [DefaultExecutionOrder(100)]
    public sealed class GardenTreeCutaway : MonoBehaviour
    {
        public Transform target;
        public Renderer[] foliage;
        public int HiddenCount { get; private set; }
        void LateUpdate()
        {
            HiddenCount = 0;
            if (target == null || foliage == null) return;
            var end = target.position + Vector3.up * .95f;
            var delta = end - transform.position;
            var ray = new Ray(transform.position, delta.normalized);
            foreach (var renderer in foliage)
            {
                if (renderer == null) continue;
                var bounds = renderer.bounds;
                bounds.Expand(.15f);
                var blocked = bounds.IntersectRay(ray, out var distance) && distance < delta.magnitude - .4f;
                renderer.forceRenderingOff = blocked;
                if (blocked) HiddenCount++;
            }
        }
        void OnDisable()
        {
            if (foliage == null) return;
            foreach (var renderer in foliage) if (renderer != null) renderer.forceRenderingOff = false;
        }
    }
}
