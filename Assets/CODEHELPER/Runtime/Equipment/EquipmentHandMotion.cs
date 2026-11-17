using System;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    [Serializable]
    public sealed class EquipmentHandBinding
    {
        public bool enabled = true;
        [Tooltip("Editable resting wrist pose. Parent under a stable view/weapon anchor, never under either arm.")]
        public Transform guard;
        [Tooltip("Optional end/contact point for this hand. Each action controls when it is reached.")]
        public Transform actionTarget;
        [Tooltip("Optional elbow pole outside both arms. Recommended for a stable bend plane when blending between FK and IK weights.")]
        public Transform elbowHint;
        [Range(0f, 1f)] public float weight = 1f;
    }

    /// <summary>Shared local-space action recipe. Scene points and playback state belong to the scene binding.</summary>
    [Serializable]
    public sealed class EquipmentHandMotion
    {
        public bool enabled;
        [Tooltip("0 = guard, 1 = assigned action target. Normalized action time; use 0 at both ends to return to guard.")]
        public AnimationCurve reach = AnimationCurve.Constant(0f, 1f, 0f);
        [Tooltip("Extra position in guard-local axes, metres. Use curves for arcs, slashes or drawing a bow.")]
        public EquipmentVectorCurves position = new EquipmentVectorCurves();
        [Tooltip("Extra wrist rotation in guard-local axes, degrees.")]
        public EquipmentVectorCurves rotation = new EquipmentVectorCurves();
        [Tooltip("Optional Generic root-transform clip, starting at local zero/identity. Sampled only on an owned Empty, never on saved targets or bones.")]
        public AnimationClip motionClip;
    }
}
