using System;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    [Serializable]
    public class HandIKChain
    {
        public Transform upperArm;
        public Transform forearm;
        public Transform hand;
        [Tooltip("Grip on the weapon or an independent IK anchor, never under a solved arm.")]
        public Transform target;
        [Tooltip("Optional elbow pole, outside both solved arms. Without it the solver uses the animated bend.")]
        public Transform hint;
        [Range(0f, 1f)] public float weight = 1f;
        [Range(0f, 1f)] public float positionWeight = 1f;
        [Range(0f, 1f)] public float rotationWeight = 1f;
        [Range(0f, 1f)] public float hintWeight = 1f;
        [Tooltip("Position offset in grip-local space.")]
        public Vector3 positionOffset;
        [Tooltip("Rotation offset in grip-local space, degrees. Adjust to the rig's wrist axes.")]
        public Vector3 rotationOffsetEuler;
    }

    /// <summary>
    /// Standalone two-bone arm solving with HandIKMath; no third-party dependencies.
    /// Does not create a PlayableGraph. Targets are weapon-local
    /// grip transforms: moving the weapon moves the targets and then the hands.
    /// </summary>
    [AddComponentMenu("CodeHelper/Shooter Framework/Animation/Hand IK Solver")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ShooterExecutionOrder.FinalHandIK)]
    public class HandIKSolver : MonoBehaviour
    {
        public Animator animator;
        [Range(0f, 1f)] public float alpha = 1f;
        public HandIKChain rightHand = new HandIKChain();
        public HandIKChain leftHand = new HandIKChain();

        // All per-frame state is allocated once. No hierarchy searches, graph rebuilds or collections in Solve.
        private sealed class ChainState
        {
            public HandIKChain settings;
            public Transform upper, mid, tip, target, hint;
            public bool valid, applied;
            public Quaternion upperBefore, midBefore, tipBefore;
            public Quaternion upperAfter, midAfter, tipAfter;

            public bool Matches(HandIKChain chain) => settings == chain &&
                (chain == null || (upper == chain.upperArm && mid == chain.forearm && tip == chain.hand &&
                                   target == chain.target && hint == chain.hint));

            public void Bind(HandIKChain chain)
            {
                settings = chain;
                upper = chain?.upperArm; mid = chain?.forearm; tip = chain?.hand;
                target = chain?.target; hint = chain?.hint;
                valid = false;
                applied = false;
            }
        }

        private const float WeightThreshold = 0.001f;
        private readonly ChainState _right = new ChainState();
        private readonly ChainState _left = new ChainState();
        private bool _needsValidation = true;

        public bool IsRightHandReady => _right.valid && _right.upper != null && _right.mid != null &&
            _right.tip != null && _right.target != null;
        public bool IsLeftHandReady => _left.valid && _left.upper != null && _left.mid != null &&
            _left.tip != null && _left.target != null;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable() { _needsValidation = true; }
        private void OnValidate() { alpha = Mathf.Clamp01(alpha); _needsValidation = true; }
        private void OnDisable() { RestorePose(); }
        private void OnDestroy() { RestorePose(); }

        private void Update()
        {
            // Undo only our previous local result before Animator evaluation, never an external new pose.
            RestorePose();
        }

        private void LateUpdate()
        {
            if (_needsValidation || !_right.Matches(rightHand) || !_left.Matches(leftHand)) ValidateSetup();
            // Also makes manual/repeated calls safe when no Update or Animator writes a new pose.
            RestorePose();
            if (alpha <= WeightThreshold) return;
            Solve(_right, Mathf.Clamp01(alpha));
            Solve(_left, Mathf.Clamp01(alpha));
        }

        [ContextMenu("Auto-Configure Humanoid (missing bones only)")]
        public void AutoConfigureHumanoid()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[HandIKSolver] Assign a Humanoid Animator, or set arm bones manually for a Generic rig.", this);
                return;
            }
            if (rightHand == null) rightHand = new HandIKChain();
            if (leftHand == null) leftHand = new HandIKChain();
            FillMissingBones(rightHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            FillMissingBones(leftHand, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            _needsValidation = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void FillMissingBones(HandIKChain chain, HumanBodyBones upper, HumanBodyBones mid, HumanBodyBones tip)
        {
            if (chain.upperArm == null) chain.upperArm = animator.GetBoneTransform(upper);
            if (chain.forearm == null) chain.forearm = animator.GetBoneTransform(mid);
            if (chain.hand == null) chain.hand = animator.GetBoneTransform(tip);
        }

        /// <summary>Call after reparenting targets/bones or changing scale at runtime.
        /// Replacing target/bone references is detected automatically. An unassigned target disables that hand.</summary>
        [ContextMenu("Validate Setup")]
        public bool ValidateSetup()
        {
            RestorePose();
            _right.Bind(rightHand);
            _left.Bind(leftHand);
            ValidateChain(_right, "Right");
            ValidateChain(_left, "Left");
            if (_right.valid && _left.valid &&
                (_right.upper.IsChildOf(_left.upper) || _left.upper.IsChildOf(_right.upper)))
            {
                Debug.LogWarning("[HandIKSolver] Arms overlap. Use separate upper-arm branches, not the chest as Upper Arm.", this);
                _right.valid = _left.valid = false;
            }
            _needsValidation = false;
            return _right.valid || _left.valid;
        }

        private void ValidateChain(ChainState state, string label)
        {
            if (state.settings == null || state.target == null) return; // Intentionally inactive hand.
            string error = null;
            if (state.upper == null || state.mid == null || state.tip == null)
                error = "assign Upper Arm, Forearm and Hand.";
            else if (state.upper == state.mid || state.mid == state.tip || state.upper == state.tip ||
                     !state.mid.IsChildOf(state.upper) || !state.tip.IsChildOf(state.mid))
                error = "expected an Upper Arm -> Forearm -> Hand chain.";
            else if (IsUnderArm(state.target, rightHand) || IsUnderArm(state.target, leftHand) ||
                     IsUnderArm(state.hint, rightHand) || IsUnderArm(state.hint, leftHand))
                error = "Target/Hint must be outside BOTH arm branches. Put grips on an independently driven weapon.";
            else if (!UniformPositiveScale(state.upper.lossyScale) || !UniformPositiveScale(state.mid.lossyScale) ||
                     !UniformPositiveScale(state.tip.lossyScale))
                error = "non-uniform/negative scale is unsupported. Use uniform positive skeleton scale.";

            if (error != null) Debug.LogWarning("[HandIKSolver] " + label + ": " + error, this);
            else state.valid = true;
        }

        private static bool IsUnderArm(Transform candidate, HandIKChain chain) =>
            candidate != null && chain != null && chain.target != null && chain.upperArm != null &&
            candidate.IsChildOf(chain.upperArm);

        private static bool UniformPositiveScale(Vector3 scale)
        {
            float tolerance = Mathf.Max(scale.x, 1f) * 0.001f;
            return scale.x > 0f && scale.y > 0f && scale.z > 0f &&
                   Mathf.Abs(scale.x - scale.y) <= tolerance && Mathf.Abs(scale.x - scale.z) <= tolerance;
        }

        private static void Solve(ChainState state, float globalWeight)
        {
            if (!state.valid || state.upper == null || state.mid == null || state.tip == null || state.target == null) return;
            HandIKChain settings = state.settings;
            float weight = globalWeight * Mathf.Clamp01(settings.weight);
            float positionWeight = weight * Mathf.Clamp01(settings.positionWeight);
            float rotationWeight = weight * Mathf.Clamp01(settings.rotationWeight);
            if (positionWeight <= WeightThreshold && rotationWeight <= WeightThreshold) return;

            Quaternion targetRotation = state.target.rotation * Quaternion.Euler(settings.rotationOffsetEuler);
            Quaternion wristRotation = state.tip.rotation;
            state.upperBefore = state.upper.localRotation;
            state.midBefore = state.mid.localRotation;
            state.tipBefore = state.tip.localRotation;

            if (positionWeight > WeightThreshold)
            {
                Vector3 goal = Vector3.Lerp(state.tip.position,
                    state.target.TransformPoint(settings.positionOffset), positionWeight);
                if (!HandIKMath.TrySolve(state.upper.position, state.mid.position, state.tip.position,
                        state.upper.rotation, state.mid.rotation, goal, state.hint != null,
                        state.hint != null ? state.hint.position : Vector3.zero,
                        Mathf.Clamp01(settings.hintWeight) * positionWeight,
                        out Quaternion upperRotation, out Quaternion forearmRotation)) return;
                state.upper.rotation = upperRotation;
                state.mid.rotation = forearmRotation;
            }

            // Position solving rotates the parent hierarchy. Set the wrist from its ORIGINAL world
            // rotation so rotationWeight=0 preserves the animated wrist orientation, not the bend delta.
            state.tip.rotation = Quaternion.Lerp(wristRotation, targetRotation, rotationWeight);

            state.upperAfter = state.upper.localRotation;
            state.midAfter = state.mid.localRotation;
            state.tipAfter = state.tip.localRotation;
            state.applied = true;
        }

        public void RestorePose()
        {
            Restore(_right);
            Restore(_left);
        }

        private static void Restore(ChainState state)
        {
            if (!state.applied) return;
            state.applied = false;
            RestoreBone(state.upper, state.upperBefore, state.upperAfter);
            RestoreBone(state.mid, state.midBefore, state.midAfter);
            RestoreBone(state.tip, state.tipBefore, state.tipAfter);
        }

        private static void RestoreBone(Transform bone, Quaternion before, Quaternion after)
        {
            if (bone != null && Quaternion.Angle(bone.localRotation, after) <= 0.05f) bone.localRotation = before;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawChain(rightHand, Color.cyan);
            DrawChain(leftHand, Color.magenta);
        }

        private static void DrawChain(HandIKChain chain, Color color)
        {
            if (chain == null) return;
            Gizmos.color = color;
            if (chain.upperArm != null && chain.forearm != null) Gizmos.DrawLine(chain.upperArm.position, chain.forearm.position);
            if (chain.forearm != null && chain.hand != null) Gizmos.DrawLine(chain.forearm.position, chain.hand.position);
            if (chain.target != null)
            {
                Vector3 grip = chain.target.TransformPoint(chain.positionOffset);
                Gizmos.DrawWireSphere(grip, 0.025f);
                if (chain.hand != null) Gizmos.DrawLine(chain.hand.position, grip);
            }
            if (chain.hint != null && chain.forearm != null) Gizmos.DrawLine(chain.forearm.position, chain.hint.position);
        }
#endif
    }
}
