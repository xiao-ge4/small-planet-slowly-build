using UnityEngine;

namespace DawnGarden
{
    // Decorations never own gameplay state, colliders or navigation. They are
    // separately switchable for matched-camera comparisons of the real scenes.
    public sealed class StarterHomeDetail : MonoBehaviour
    {
        public string theme;
        public Transform content;
        public int botanicalGroups,grains,grassBlades;
        public void SetVisible(bool visible){if(content!=null)content.gameObject.SetActive(visible);}
    }
}
