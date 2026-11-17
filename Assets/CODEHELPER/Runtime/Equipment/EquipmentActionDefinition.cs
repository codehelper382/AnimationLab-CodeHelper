using System;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    [Serializable]
    public class EquipmentVectorCurves
    {
        public AnimationCurve x = AnimationCurve.Constant(0f, 1f, 0f);
        public AnimationCurve y = AnimationCurve.Constant(0f, 1f, 0f);
        public AnimationCurve z = AnimationCurve.Constant(0f, 1f, 0f);

        public Vector3 Evaluate(float progress) => new Vector3(
            EquipmentNumbers.Curve(x, progress, 0f), EquipmentNumbers.Curve(y, progress, 0f),
            EquipmentNumbers.Curve(z, progress, 0f));
    }

    /// <summary>Presentation recipe plus a single gameplay milestone; no playing state or scene references.</summary>
    [CreateAssetMenu(menuName = "CodeHelper/Equipment/Action", fileName = "EquipmentAction")]
    public sealed class EquipmentActionDefinition : ScriptableObject
    {
        public AnimationClip characterClip;
        public AvatarMask characterMask;
        [Tooltip("Transform-only Generic clip for the separate weapon skeleton, without events. Never targets the common Weapon Holder or character.")]
        public AnimationClip weaponClip;
        [Tooltip("Optional Generic root-transform clip for the left reload target, in the per-weapon anchor's local space. Fingers still use Character Clip. Null holds the assigned target position.")]
        public AnimationClip leftHandTargetMotion;
        [Tooltip("Optional editable-target action, independent of weapon type and of the character/finger clip.")]
        public EquipmentHandMotion leftHandMotion = new EquipmentHandMotion();
        public EquipmentHandMotion rightHandMotion = new EquipmentHandMotion();
        [Tooltip("During a manipulation clip, drive the independent weapon anchor from the animated right wrist. Release its grip IK in the action curves; never parent the weapon under the solved hand.")]
        public bool followAnimatedRightHand;
        [Tooltip("Authoritative runtime duration in seconds, independent of clip length. Zero commits immediately.")]
        [Min(0f)] public float duration = 0.4f;
        [Min(0f)] public float blendIn = 0.08f;
        [Min(0f)] public float blendOut = 0.08f;
        [Tooltip("Normalized gameplay milestone: shot, ammo transfer, chamber operation or melee contact.")]
        [Range(0f, 1f)] public float commitTime = 0.5f;
        public bool canCancel = true;
        [Tooltip("Multiplier of the item's grip attachment, not necessarily total hand IK weight.")]
        public AnimationCurve leftGrip = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve rightGrip = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve leftIK = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve rightIK = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("Additive offset in Weapon Holder parent-local axes, metres. Evaluated, not applied by the data layer.")]
        public EquipmentVectorCurves weaponPosition = new EquipmentVectorCurves();
        [Tooltip("Additive rotation in Weapon Holder local axes, degrees.")]
        public EquipmentVectorCurves weaponRotation = new EquipmentVectorCurves();

        public EquipmentHandSample SampleHands(float progress) => new EquipmentHandSample
        {
            leftGrip = Mathf.Clamp01(EquipmentNumbers.Curve(leftGrip, progress, 1f)),
            rightGrip = Mathf.Clamp01(EquipmentNumbers.Curve(rightGrip, progress, 1f)),
            leftIK = Mathf.Clamp01(EquipmentNumbers.Curve(leftIK, progress, 1f)),
            rightIK = Mathf.Clamp01(EquipmentNumbers.Curve(rightIK, progress, 1f))
        };
    }

    public struct EquipmentHandSample
    {
        public float leftGrip, rightGrip, leftIK, rightIK;
    }

    internal static class EquipmentNumbers
    {
        public static float NonNegative(float value) => Finite(value) ? Mathf.Max(0f, value) : 0f;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Progress(float value) => Finite(value) ? Mathf.Clamp01(value) : 0f;
        public static float Curve(AnimationCurve curve, float progress, float fallback)
        {
            float value = curve == null || curve.length == 0 ? fallback : curve.Evaluate(Progress(progress));
            return Finite(value) ? value : fallback;
        }
    }
}
