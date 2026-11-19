using UnityEngine;

namespace DawnGarden
{
    /// <summary>Restored every frame, then layered after Mecanim on the disposable guardian only.</summary>
    [DefaultExecutionOrder(1110)]
    public sealed class GuardianBattleIntentPose : MonoBehaviour
    {
        Transform spine, head;
        Quaternion beforeSpine, beforeHead;
        bool exposed, applied;

        public void Bind(Transform actor)
        {
            foreach (var bone in actor.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "mixamorig:Spine1") spine = bone;
                else if (bone.name == "mixamorig:Head") head = bone;
            }
        }

        public void SetExposed(bool value)
        {
            if (exposed == value) return;
            Restore(); exposed = value;
        }

        void Update() { Restore(); }

        void LateUpdate()
        {
            if (!exposed) return;
            if (spine != null) { beforeSpine = spine.localRotation; spine.localRotation *= Quaternion.Euler(14, 0, 0); }
            if (head != null) { beforeHead = head.localRotation; head.localRotation *= Quaternion.Euler(19, 0, 0); }
            applied = true;
        }

        void Restore()
        {
            if (!applied) return;
            if (spine != null) spine.localRotation = beforeSpine;
            if (head != null) head.localRotation = beforeHead;
            applied = false;
        }

        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }
    }
}
