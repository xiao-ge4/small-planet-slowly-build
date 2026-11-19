using UnityEngine;

namespace DawnGarden
{
    public sealed class EmberLavaEffects : MonoBehaviour
    {
        public GardenAtmosphere atmosphere;
        public Light[] shoreGlow;
        public Transform[] cinders;
        void Awake()
        {
            // Keep old serialized scenes safe: disable legacy lava fixtures
            // instead of allowing them to add light or emissive particles.
            if(shoreGlow!=null) foreach(var light in shoreGlow) if(light!=null) light.intensity=0;
            if(cinders!=null) foreach(var cinder in cinders) if(cinder!=null) cinder.gameObject.SetActive(false);
        }
    }
}
