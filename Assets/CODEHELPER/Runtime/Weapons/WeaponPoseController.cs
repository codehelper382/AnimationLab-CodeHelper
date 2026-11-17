using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Weapons
{
    /// <summary>Absolute local Idle/Aim placement. Leaves bones, camera and grips untouched.</summary>
    [AddComponentMenu("CodeHelper/Shooter Framework/Weapons/Weapon Pose Controller")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ShooterExecutionOrder.WeaponPose)]
    public class WeaponPoseController : MonoBehaviour
    {
        public WeaponPoseProfile profile;
        [Tooltip("Dedicated weapon anchor (Weapon Holder), not a camera or a skeleton bone. Defaults to this object.")]
        public Transform poseRoot;
        public bool useLookController = true;
        public PlayerLookController lookController;
        public bool useUnscaledTime;

        private bool _externalAiming, _initialized;
        private WeaponPoseState _state;
        private WeaponPoseProfile _profile;
        private Transform _root, _parent;
        private Transform _validatedRoot, _validatedParent;
        private bool _rootValid;
        private Vector3 _fromPosition, _targetPosition;
        private Quaternion _fromRotation, _targetRotation;
        private float _elapsed, _duration;
        private WeaponAimSpring _positionSpring, _rotationSpring;
        private bool _effectApplied;
        private Transform _effectRoot, _effectParent;
        private Vector3 _basePosition, _outputPosition;
        private Quaternion _baseRotation, _outputRotation;
        private Vector3 _motionPosition, _motionRotation, _timedStartPosition, _timedStartRotation;
        private Vector3 _timedPositionKick, _timedRotationKick;
        private uint _motionVersion;
        private bool _motionVersionValid, _clockSynchronized;

        public Transform PoseRoot => poseRoot != null ? poseRoot : transform;
        public bool IsAiming => useLookController
            ? lookController != null && lookController.enabled && lookController.IsAiming
            : _externalAiming;
        public Vector3 ProceduralPositionOffset => _motionPosition;
        public Vector3 ProceduralRotationOffset => _motionRotation;
        public float TransitionProgress { get; private set; }
        public float TransitionBlend => AimTransitionMath.Blend(TransitionProgress);
        public uint TransitionVersion { get; private set; }
        public bool TransitionIsStateChange { get; private set; }
        public bool TransitionIsAiming => _state == WeaponPoseState.Aim;
        public bool HasPreparedPose => _initialized && _profile == profile && _root == PoseRoot && _parent == _root.parent;

        private void Reset()
        {
            poseRoot = transform;
            lookController = GetComponentInParent<PlayerLookController>(true);
        }

        private void Awake()
        {
            if (poseRoot == null) poseRoot = transform;
            if (lookController == null) lookController = GetComponentInParent<PlayerLookController>(true);
        }

        private void OnEnable() { _initialized = false; _validatedRoot = null; }
        private void OnValidate() { _initialized = false; _validatedRoot = null; }
        private void OnDisable()
        {
            RemoveOwnMotion();
            ResetMotion();
            _initialized = false; // Leave the last BASE pose in place, without procedural residue.
        }

        private bool OwnOutputIsUnchanged() => _effectRoot != null && _effectRoot.parent == _effectParent &&
            Vector3.Distance(_effectRoot.localPosition, _outputPosition) < 0.00001f &&
            Quaternion.Angle(_effectRoot.localRotation, _outputRotation) < 0.05f;

        /// <summary>Explicit editor captures omit our transient bounce, not other systems' effects.
        /// Capturing mid-transition still captures the CURRENT interpolated base, not an endpoint.</summary>
        public WeaponPose GetPoseForCapture()
        {
            if (_effectApplied && _effectRoot == PoseRoot && OwnOutputIsUnchanged())
                return new WeaponPose { localPosition = _basePosition, localEulerAngles = _baseRotation.eulerAngles };
            return WeaponPose.FromLocalTransform(PoseRoot);
        }

        private void RemoveOwnMotion()
        {
            // Never bake the last bounce into the base on reversal, disable or profile/root replacement.
            // Preserve an external writer's new pose instead of restoring over it.
            if (_effectApplied && OwnOutputIsUnchanged())
                _effectRoot.SetLocalPositionAndRotation(_basePosition, _baseRotation);
            _effectApplied = false;
        }

        private void ResetMotion()
        {
            _positionSpring.Reset(); _rotationSpring.Reset();
            _motionPosition = _motionRotation = Vector3.zero;
            _timedStartPosition = _timedStartRotation = _timedPositionKick = _timedRotationKick = Vector3.zero;
            _motionVersionValid = false;
        }

        /// <summary>External input. Set Use Look Controller off to consume this value.</summary>
        public void SetAiming(bool aiming) { _externalAiming = aiming; }

        public bool TryValidateRoot(out string error)
        {
            Transform root = PoseRoot;
            error = null;
            if (root.parent == null) error = "Weapon pose root needs a parent defining its local reference space.";
            else if (root.GetComponentInChildren<Camera>(true) != null)
                error = "Weapon pose root must not contain a camera. Assign Weapon Holder, not Camera Holder or Player.";
            return error == null;
        }

        [ContextMenu("Validate Setup")]
        public bool ValidateSetup()
        {
            _validatedRoot = null; // Also recheck a hierarchy changed explicitly at runtime.
            string error;
            if (!TryValidateRoot(out error)) { Debug.LogWarning("[WeaponPoseController] " + error, this); return false; }
            if (profile == null) error = "Assign a Weapon Pose Profile, or create one using the Inspector.";
            else if (useLookController && lookController == null) error = "Assign Player Look Controller or use external aiming.";
            else if (!profile.HasIdle || !profile.HasAim) error = "Capture both Idle and Aim before testing the full transition.";
            else return true;
            Debug.LogWarning("[WeaponPoseController] " + error, this);
            return false;
        }

        // Update runs after PlayerLook input, before camera LateUpdate. It publishes one clock;
        // camera reads it at 500, then the weapon applies its prepared result at 550.
        private void Update() => PreparePose(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        private void LateUpdate() => ApplyPreparedPose(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        // Deterministic combined evaluation used by isolated checks. Runtime uses Update + LateUpdate.
        private void ApplyPose(float deltaTime)
        {
            PreparePose(deltaTime);
            ApplyPreparedPose(deltaTime);
        }

        private void PreparePose(float deltaTime)
        {
            RemoveOwnMotion();
            bool aiming = IsAiming;
            WeaponPoseState state = aiming ? WeaponPoseState.Aim : WeaponPoseState.Idle;
            // An uncaptured pose is NOT an implicit (0,0,0). In particular, creating an Aim-only
            // profile must never erase the user's current placement on the first Idle frame.
            if (profile == null || !profile.TryGetPose(state, out WeaponPose pose) || !RuntimeRootIsValid())
            {
                _initialized = false;
                ResetMotion();
                return;
            }
            Transform root = PoseRoot;
            Quaternion rotation = pose.Rotation;
            bool sameContext = _initialized && _profile == profile && _root == root && _parent == root.parent;
            bool stateChanged = sameContext && _state != state;
            bool timingChanged = _clockSynchronized != profile.synchronizeAimEffects;
            if (!sameContext || timingChanged) ResetMotion();
            float duration = WeaponAimSpring.Safe(profile.transitionDuration, 0f, float.MaxValue, 0f);
            if (!_initialized || _profile != profile || _root != root || _parent != root.parent ||
                _state != state || _targetPosition != pose.localPosition || Quaternion.Angle(_targetRotation, rotation) > 0.001f ||
                timingChanged || _duration != duration)
            {
                _fromPosition = root.localPosition;
                _fromRotation = root.localRotation;
                _targetPosition = pose.localPosition;
                _targetRotation = rotation;
                _elapsed = 0f;
                _duration = duration;
                _profile = profile; _root = root; _parent = root.parent; _state = state;
                _clockSynchronized = profile.synchronizeAimEffects;
                TransitionIsStateChange = stateChanged;
                unchecked { TransitionVersion++; }
                _initialized = true;
            }
            if (!float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime)) _elapsed += Mathf.Max(0f, deltaTime);
            TransitionProgress = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            float t = TransitionBlend;
            _basePosition = Vector3.Lerp(_fromPosition, _targetPosition, t);
            _baseRotation = Quaternion.Slerp(_fromRotation, _targetRotation, t);
        }

        private void ApplyPreparedPose(float deltaTime)
        {
            if (!HasPreparedPose) return;
            bool newTransition = !_motionVersionValid || _motionVersion != TransitionVersion;
            UpdateMotion(profile.aimMotion, newTransition, deltaTime);
            _motionVersion = TransitionVersion;
            _motionVersionValid = true;
            _outputPosition = _basePosition + _motionPosition;
            _outputRotation = _baseRotation * Quaternion.Euler(_motionRotation);
            Transform root = PoseRoot;
            root.SetLocalPositionAndRotation(_outputPosition, _outputRotation);
            _effectRoot = root;
            _effectParent = root.parent;
            _effectApplied = _motionPosition.sqrMagnitude > 0f || _motionRotation.sqrMagnitude > 0f;
        }

        private void UpdateMotion(WeaponAimMotionSettings motion, bool newTransition, float dt)
        {
            if (motion == null || !motion.enabled) { ResetMotion(); return; }
            float positionLimit = WeaponAimSpring.Safe(motion.maxPositionOffset, 0f, 0.1f, 0f);
            float rotationLimit = WeaponAimSpring.Safe(motion.maxRotationOffset, 0f, 15f, 0f);
            bool kick = newTransition && TransitionIsStateChange;
            if (profile.synchronizeAimEffects)
            {
                if (newTransition)
                {
                    _timedStartPosition = _motionPosition;
                    _timedStartRotation = _motionRotation;
                    _timedPositionKick = kick ? TransitionIsAiming ? motion.aimPositionKick : motion.idlePositionKick : Vector3.zero;
                    _timedRotationKick = kick ? TransitionIsAiming ? motion.aimRotationKick : motion.idleRotationKick : Vector3.zero;
                }
                _motionPosition = AimTransitionMath.Motion(_timedStartPosition, _timedPositionKick, TransitionProgress,
                    profile.transitionOscillations, motion.damping, positionLimit);
                _motionRotation = AimTransitionMath.Motion(_timedStartRotation, _timedRotationKick, TransitionProgress,
                    profile.transitionOscillations, motion.damping, rotationLimit);
                return;
            }
            if (kick)
            {
                _positionSpring.Kick(TransitionIsAiming ? motion.aimPositionKick : motion.idlePositionKick, motion.frequency, positionLimit);
                _rotationSpring.Kick(TransitionIsAiming ? motion.aimRotationKick : motion.idleRotationKick, motion.frequency, rotationLimit);
            }
            _positionSpring.Step(dt, motion.frequency, motion.damping, positionLimit);
            _rotationSpring.Step(dt, motion.frequency, motion.damping, rotationLimit);
            _motionPosition = _positionSpring.Offset;
            _motionRotation = _rotationSpring.Offset;
        }

        private bool RuntimeRootIsValid()
        {
            Transform root = PoseRoot;
            if (_validatedRoot != root || _validatedParent != root.parent)
            {
                _rootValid = TryValidateRoot(out _);
                _validatedRoot = root;
                _validatedParent = root.parent;
            }
            return _rootValid;
        }
    }
}
