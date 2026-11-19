using UnityEngine;

namespace DawnGarden
{
    public sealed class SmallWorldAjinActor : MonoBehaviour
    {
        public Animator rig;
        int reactionState;
        float reactionDeadline;

        public void Respond(string action)
        {
            if (action != "greet" && action != "receive_gift" && action != "ritual" && action != "care") return;
            var state = action == "care" ? "Water" : "Greet";
            reactionState = Animator.StringToHash(state);
            rig.CrossFadeInFixedTime(state, .12f);
            reactionDeadline = Time.time + 6f;
        }

        void Update()
        {
            if (reactionState == 0) return;
            var active = rig.GetCurrentAnimatorStateInfo(0);
            if (Time.time < reactionDeadline && (active.shortNameHash != reactionState || active.normalizedTime < 1f)) return;
            reactionState = 0;
            rig.CrossFadeInFixedTime("Idle", .18f);
        }
    }
}
