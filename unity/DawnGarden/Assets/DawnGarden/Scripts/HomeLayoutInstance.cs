using UnityEngine;
namespace DawnGarden
{
    // IDs belong to a layout instance, never to a shared prefab.
    public sealed class HomeLayoutInstance : MonoBehaviour
    {
        public string instanceId, assetId;
        public int assetVersion;
    }
}
