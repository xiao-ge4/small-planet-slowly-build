using UnityEngine;

namespace DawnGarden
{
    // Immutable provenance travels with every instance of the reusable prefab.
    public sealed class PreparedHomeAsset : MonoBehaviour
    {
        public string assetId;
        public string runId;
        public int sourceRevision;
        public string sourceSha256;
        public string designSha256;
        public string providerTaskId;
        public string preparationSha256;
        public string interactionKind = "None";
        public bool validatedForAssembly;
    }
}
