using UnityEngine;

namespace DawnGarden
{
    public enum GardenInteractionKind { Water, Chest }

    public sealed class GardenInteraction : MonoBehaviour
    {
        public string stableId;
        public string displayName;
        public GardenInteractionKind kind;
        public Transform approach;
        public Transform visual;
        public Transform lid;
        public float activationDistance = 1.8f;
    }
}
