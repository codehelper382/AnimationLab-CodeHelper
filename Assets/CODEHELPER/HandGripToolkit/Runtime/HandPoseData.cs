using System.Collections.Generic;
using UnityEngine;

namespace HandGripToolkit
{
    /// <summary>
    /// Serializable finger pose for one hand of a Humanoid character.
    ///
    /// Stores the local rotation of every mapped finger bone (15 per hand on a
    /// fully mapped avatar) plus an optional wrist offset relative to a GripPoint.
    ///
    /// - Capture() reads the current finger rotations of a live Animator.
    /// - Apply() writes the pose back onto an Animator, blending from the
    ///   current rotations by a 0..1 weight (Slerp). Call it AFTER the Animator
    ///   has evaluated (LateUpdate or later); anything written earlier in the
    ///   frame is overwritten by the Animator.
    /// - Mirror() returns an in-memory copy for the opposite hand.
    ///
    /// Unity 6000.3 note: HumanBodyBones finger members are named
    /// Left/Right*Proximal|Intermediate|Distal here (the legacy "Thumb1/2/3"
    /// spelling no longer exists). See HandBoneMap for the mapping tables.
    /// </summary>
    [CreateAssetMenu(fileName = "HandPose", menuName = "HandGrip Toolkit/Hand Pose")]
    public class HandPoseData : ScriptableObject
    {
        [Header("Hand")]
        [Tooltip("Which hand this pose belongs to.")]
        [SerializeField] private HandSide side = HandSide.Right;

        [Header("Finger Bones")]
        [Tooltip("Local rotation per finger bone (15 entries on a fully mapped hand).")]
        [SerializeField] private List<FingerBonePose> bones = new List<FingerBonePose>();

        [Header("Optional Wrist Offset (relative to the GripPoint transform)")]
        [Tooltip("When enabled, grip tools use this offset to fine-tune how the wrist sits on the grip.")]
        [SerializeField] private bool hasWristOffset = false;

        [Tooltip("Wrist position relative to the GripPoint transform.")]
        [SerializeField] private Vector3 wristLocalPosition = Vector3.zero;

        [Tooltip("Wrist rotation relative to the GripPoint transform.")]
        [SerializeField] private Quaternion wristLocalRotation = Quaternion.identity;

        // ──────────────────────────────────────────────
        //  Accessors
        // ──────────────────────────────────────────────

        /// <summary>Hand this pose belongs to.</summary>
        public HandSide Side { get { return side; } set { side = value; } }

        /// <summary>How many finger bone entries this pose stores.</summary>
        public int BoneCount { get { return bones.Count; } }

        /// <summary>True when the optional wrist offset should be used at grips.</summary>
        public bool HasWristOffset { get { return hasWristOffset; } set { hasWristOffset = value; } }

        /// <summary>Wrist position relative to the GripPoint transform.</summary>
        public Vector3 WristLocalPosition { get { return wristLocalPosition; } set { wristLocalPosition = value; } }

        /// <summary>Wrist rotation relative to the GripPoint transform.</summary>
        public Quaternion WristLocalRotation { get { return wristLocalRotation; } set { wristLocalRotation = value; } }

        // ──────────────────────────────────────────────
        //  Entry access (used by editor tooling)
        // ──────────────────────────────────────────────

        /// <summary>Entry for a finger joint of this pose's hand, or null if absent.</summary>
        public FingerBonePose GetEntry(FingerId finger, int joint)
        {
            return GetEntry(HandBoneMap.GetBone(side, finger, joint));
        }

        /// <summary>Entry that stores the given humanoid bone, or null if absent.</summary>
        public FingerBonePose GetEntry(HumanBodyBones bone)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                FingerBonePose entry = bones[i];
                if (entry != null && entry.bone == bone)
                    return entry;
            }
            return null;
        }

        // ──────────────────────────────────────────────
        //  Capture / Apply / Mirror
        // ──────────────────────────────────────────────

        /// <summary>
        /// Reads the current local rotations of this hand's finger bones from a
        /// Humanoid Animator into this asset. Bones missing from the avatar are
        /// skipped. The wrist offset is left untouched (it needs grip context).
        /// Returns true when at least one bone was captured.
        /// </summary>
        public bool Capture(Animator animator)
        {
            if (!IsHumanoidAnimator(animator, out string reason))
            {
                Debug.LogWarning("[HandGrip Toolkit] Capture failed: " + reason, this);
                return false;
            }

            bones.Clear();
            for (int finger = 0; finger < HandBoneMap.FingersPerHand; finger++)
            {
                for (int joint = 0; joint < HandBoneMap.JointsPerFinger; joint++)
                {
                    HumanBodyBones id = HandBoneMap.GetBone(side, (FingerId)finger, joint);
                    Transform bone = animator.GetBoneTransform(id);
                    if (bone != null)
                        bones.Add(new FingerBonePose(id, bone.localRotation));
                }
            }

            if (bones.Count == 0)
            {
                Debug.LogWarning(
                    $"[HandGrip Toolkit] Capture: '{animator.name}' has no finger bones mapped for the {side} hand. " +
                    "Map the fingers in the avatar's Rig settings (Configure Avatar) to author hand poses.", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Writes the stored finger rotations onto an Animator, blending from
        /// the current rotation by 'weight' (0 = no change, 1 = full pose).
        ///
        /// IMPORTANT: call this AFTER the Animator has evaluated (e.g. from
        /// LateUpdate); the Animator rewrites bone transforms every frame, so
        /// anything written earlier is discarded. Missing bones are skipped
        /// silently so this stays safe to call every frame without log spam.
        /// </summary>
        public bool Apply(Animator animator, float weight = 1f)
        {
            if (animator == null || !animator.isHuman)
                return false;

            weight = Mathf.Clamp01(weight);
            if (weight <= 0f)
                return true; // Nothing to do.

            for (int i = 0; i < bones.Count; i++)
            {
                FingerBonePose entry = bones[i];
                if (entry == null)
                    continue;

                Transform bone = animator.GetBoneTransform(entry.bone);
                if (bone == null)
                    continue; // Bone not mapped on this avatar.

                bone.localRotation = weight >= 1f
                    ? entry.localRotation
                    : Quaternion.Slerp(bone.localRotation, entry.localRotation, weight);
            }
            return true;
        }

        /// <summary>
        /// Returns a new in-memory pose for the opposite hand (not saved as an
        /// asset; use the editor tools to save it). See MirrorRotation for the
        /// mirroring assumptions.
        /// </summary>
        public HandPoseData Mirror()
        {
            HandPoseData mirror = CreateInstance<HandPoseData>();
            mirror.name = string.IsNullOrEmpty(name) ? "Mirrored Hand Pose" : name + " (Mirrored)";
            mirror.side = HandBoneMap.GetOppositeSide(side);
            mirror.hasWristOffset = hasWristOffset;

            // Convention: grip frames are expected to be axis-aligned with the
            // item (Z forward, Y up), so the mirror plane is the grip's local
            // YZ plane -> negate the local X of the wrist position.
            mirror.wristLocalPosition = new Vector3(-wristLocalPosition.x, wristLocalPosition.y, wristLocalPosition.z);
            mirror.wristLocalRotation = MirrorRotation(wristLocalRotation);

            for (int i = 0; i < bones.Count; i++)
            {
                FingerBonePose entry = bones[i];
                if (entry == null)
                    continue;

                mirror.bones.Add(new FingerBonePose(
                    HandBoneMap.GetMirroredBone(entry.bone),
                    MirrorRotation(entry.localRotation)));
            }
            return mirror;
        }

        /// <summary>
        /// Mirrors a local rotation across the character's sagittal plane
        /// (X -> -X, i.e. reflection in the YZ plane), mapping a left-bone
        /// rotation to the equivalent right-bone rotation.
        ///
        /// Math: conjugating a rotation with a reflection keeps it a proper
        /// rotation but reverses its turning sense around the reflected axis,
        /// which for quaternions simplifies to negating the components on the
        /// mirror plane's tangent axes:
        ///     q' = (x, -y, -z, w)
        /// (equivalently (-x, y, z, -w), since q and -q rotate identically).
        ///
        /// ASSUMPTION: the rig is left/right symmetric at bind pose (true for
        /// most humanoid rigs, e.g. Mixamo): every parent/bind frame of the
        /// right chain is the exact X-reflection of the left one. Rigs authored
        /// with identical (non-mirrored) left/right frames must instead mirror
        /// relative to the bind pose, which the Pose Editor does when a
        /// character is available.
        /// </summary>
        public static Quaternion MirrorRotation(Quaternion q)
        {
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }

        /// <summary>
        /// Validates that an Animator can drive humanoid hand posing.
        /// 'reason' describes the problem when returning false.
        /// </summary>
        public static bool IsHumanoidAnimator(Animator animator, out string reason)
        {
            if (animator == null)
            {
                reason = "No Animator assigned.";
                return false;
            }
            if (animator.avatar == null)
            {
                reason = "Animator '" + animator.name + "' has no Avatar assigned.";
                return false;
            }
            if (!animator.isHuman)
            {
                reason = "Animator '" + animator.name + "' does not use a Humanoid avatar (check the model's Rig tab).";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
