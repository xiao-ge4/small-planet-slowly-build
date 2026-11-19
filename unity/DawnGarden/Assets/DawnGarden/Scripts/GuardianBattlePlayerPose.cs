using UnityEngine;

namespace DawnGarden
{
    /// <summary>Small additive rotations on the disposable player copy; no bone positions or source clips change.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class GuardianBattlePlayerPose : MonoBehaviour
    {
        Transform[] bones;
        Quaternion[] before;
        Animator rig;
        string action = "Idle";
        float age, duration;
        bool applied;

        public void Bind(Animator animator)
        {
            rig = animator;
            var hierarchy = animator.GetComponentsInChildren<Transform>(true);
            string[] names = { "mixamorig:LeftArm", "mixamorig:RightArm", "mixamorig:LeftForeArm", "mixamorig:RightForeArm", "mixamorig:Spine1", "mixamorig:Head" };
            bones = new Transform[names.Length]; before = new Quaternion[names.Length];
            for (int i = 0; i < names.Length; i++)
                foreach (var bone in hierarchy) if (bone.name == names[i]) { bones[i] = bone; break; }
        }

        public void Pose(string state, float seconds)
        {
            Restore(); action = state; duration = seconds; age = 0;
            // A future authored player combat animation takes precedence over this temporary pose layer.
            if (rig != null && rig.runtimeAnimatorController != null && rig.HasState(0, Animator.StringToHash(state))) action = "Idle";
        }

        void Restore()
        {
            if (!applied || bones == null) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = before[i];
            applied = false;
        }

        void Update() { Restore(); age += Time.unscaledDeltaTime; }

        void LateUpdate()
        {
            if (bones == null || duration <= 0 || (age >= duration && action != "Defeat") || action == "Idle") return;
            float u = Mathf.Clamp01(age / duration);
            float weight = action == "Guard" ? Mathf.SmoothStep(0, 1, Mathf.Min(u * 5, (1 - u) * 5)) : Mathf.Sin(Mathf.PI * u);
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) before[i] = bones[i].localRotation;
            if (action == "Attack")
            {
                Rotate(0, new Vector3(0, 8, -13), weight);
                Rotate(1, new Vector3(-18, -20, 38), weight);
                Rotate(3, new Vector3(-15, -8, 13), weight);
                Rotate(4, new Vector3(0, -8, 0), weight);
            }
            else if (action == "Guard")
            {
                Rotate(0, new Vector3(-9, 18, -32), weight); Rotate(1, new Vector3(-9, -18, 32), weight);
                Rotate(2, new Vector3(0, 14, -19), weight); Rotate(3, new Vector3(0, -14, 19), weight);
                Rotate(5, new Vector3(5, 0, 0), weight);
            }
            else if (action == "Skill")
            {
                Rotate(0, new Vector3(-14, 23, -42), weight); Rotate(1, new Vector3(-14, -23, 42), weight);
                Rotate(2, new Vector3(-8, 0, -8), weight); Rotate(3, new Vector3(-8, 0, 8), weight);
                Rotate(4, new Vector3(5, 0, 0), weight);
            }
            else if (action == "Hit") { Rotate(4, new Vector3(-9, 0, 0), weight); Rotate(5, new Vector3(-5, 0, 0), weight); }
            else if (action == "Victory")
            {
                Rotate(0, new Vector3(-10, 12, -28), weight);
                Rotate(1, new Vector3(-10, -12, 28), weight);
                Rotate(5, new Vector3(-7, 0, 0), weight);
            }
            else if (action == "Defeat")
            {
                float bow = Mathf.SmoothStep(0, 1, age / .55f);
                Rotate(4, new Vector3(12, 0, 0), bow);
                Rotate(5, new Vector3(16, 0, 0), bow);
            }
            applied = true;
        }

        void Rotate(int index, Vector3 angles, float weight)
        {
            if (bones[index] != null) bones[index].localRotation *= Quaternion.Euler(angles * weight);
        }

        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }
    }
}
