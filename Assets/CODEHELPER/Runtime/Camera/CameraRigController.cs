using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Weapons;
using UnityEngine;

/// <summary>
/// Optional bridge implemented by the game's look controller. Pitch is positive UP;
/// yaw is an absolute world heading unless yawRelativeToRoot is enabled.
/// Publish values before this component's LateUpdate. Do not read the driven camera
/// rotation back as input (that creates a feedback loop).
/// </summary>
public interface ICameraLookSource
{
    float CameraPitch { get; }
    float CameraYaw { get; }
    float CameraLean { get; }
    bool IsThirdPerson { get; }
}

public enum CameraPositionSource
{
    Head = 0,
    PlayerRoot = 1,
    HeadStabilized = 2
}

/// <summary>
/// Stable world-space view with an optional head-parented rig. PlayerRoot mode follows
/// player translation only; Head follows animation exactly; HeadStabilized follows a virtual
/// head with body yaw compensated while retaining pitch/posture.
/// Does not reparent objects or animate bones.
/// </summary>
[AddComponentMenu("CodeHelper/Shooter Framework/Camera/Camera Rig Controller")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(ShooterExecutionOrder.Camera)]
public class CameraRigController : MonoBehaviour
{
    [Header("1. Input and character")]
    [Tooltip("Assign PlayerLookController (implements ICameraLookSource), or another compatible source. " +
             "Leave empty to use SetLookInputs directly.")]
    public MonoBehaviour lookController;
    [SerializeField] private ProceduralSpine proceduralSpine;
    [Tooltip("Unanimated character heading reference, never Head or an object below this rig.")]
    public Transform characterRoot;
    [Tooltip("Optional position fallback when Head is not assigned.")]
    public Transform stabilizationBone;
    public Transform headBone;
    [Tooltip("Off: yaw is world-space. On: yaw is relative to characterRoot, like a local look offset.")]
    public bool yawRelativeToRoot;
    [Tooltip("Compatibility option: read lean from ProceduralSpine. Pitch always comes from look input.")]
    public bool useSpineLean = true;

    [Header("2. Rig structure")]
    [Tooltip("Camera transform. Can be this transform for a camera directly below Head.")]
    public Transform realCamera;
    [Tooltip("Optional child of this rig for hands/weapons. If empty, this transform is the holder.")]
    public Transform cameraHolder;

    [Header("3. Position")]
    [Tooltip("Head: exact animated position. PlayerRoot: fixed eye offset. HeadStabilized: follow pitch/posture " +
             "with body yaw replaced by view yaw (recommended with ProceduralSpine).")]
    public CameraPositionSource positionSource = CameraPositionSource.PlayerRoot;
    [Tooltip("In PlayerRoot mode, capture Head's current world offset once. It will NOT rotate with the body.")]
    public bool captureHeadPosition = true;
    [Tooltip("World-space offset from Character Root when Capture Head Position is off (or no head is assigned). " +
             "For crouching, disable capture and drive this height explicitly.")]
    public Vector3 rootAnchorOffset = new Vector3(0f, 1.65f, 0f);
    [Tooltip("Additional offset in stable view space, not animated head space. Zero avoids look-orbit translation.")]
    public Vector3 offset;
    [Tooltip("Head/HeadStabilized: filter anchor translation. PlayerRoot movement is always immediate.")]
    public bool smoothHeadPosition;
    [Min(0f)] public float positionSmoothTime = 0.02f;
    public bool useUnscaledTime;

    [Header("4. TPS")]
    public bool isTPS;
    public Vector3 tpsOffset = new Vector3(0.5f, 0f, -2.5f);
    public AnimationCurve distanceCurve = new AnimationCurve(
        new Keyframe(-90f, 0.4f), new Keyframe(0f, 1f), new Keyframe(90f, 0.4f));
    [Tooltip("Exclude the player's colliders. Triggers are ignored.")]
    public LayerMask collisionLayers;
    [Min(0.001f)] public float collisionRadius = 0.2f;
    [Min(0f)] public float collisionPadding = 0.05f;

    [Header("5. Camera-only effects")]
    [Min(0f)] public float recoilReturnSpeed = 16f;
    [Min(0f)] public float fovSpeed = 10f;
    [Tooltip("Optional weapon profile/input source for Aim camera effects. Auto-found below this rig in Awake.")]
    public WeaponPoseController aimEffectsSource;

    private ICameraLookSource _lookSource;
    private MonoBehaviour _boundSource;
    private Transform _boundRealCamera;
    private UnityEngine.Camera _camera;
    private Vector3 _anchorPosition, _positionVelocity;
    private Vector3 _recoilEuler, _additivePosition;
    private Quaternion _additiveRotation = Quaternion.identity;
    private float _pitch, _yaw, _lean, _targetFov;
    private bool _anchorInitialized, _hasFovTarget, _reportedError;
    private bool _rootAnchorCaptured;
    private Vector3 _capturedRootOffset;
    private Transform _capturedRoot, _capturedHead;
    private CameraPositionSource _lastPositionSource;
    private readonly HeadPositionStabilizer _headStabilizer = new HeadPositionStabilizer();
    private WeaponAimSpring _aimCameraSpring;
    private WeaponPoseController _aimSource;
    private WeaponPoseProfile _aimProfile;
    private bool _aimInputInitialized, _lastAiming;
    private float _aimFovOffset;
    private UnityEngine.Camera _aimFovCamera;
    private float _fovWithoutAim, _fovWithAim;
    private bool _aimFovApplied, _aimRotationApplied;
    private Transform _aimRotationCamera;
    private Quaternion _rotationWithoutAim, _rotationWithAim;
    private Vector3 _aimRotationOffset, _aimTimedStart, _aimTimedKick;
    private float _aimTimedFovStart;
    private uint _aimTransitionVersion;
    private bool _aimTimedValid, _aimSyncMode;

    public Quaternion ViewRotation { get; private set; } = Quaternion.identity;
    public Vector3 ViewAnchor { get; private set; }
    public float AimFovOffset => _aimFovOffset;
    public Vector3 AimCameraRotationOffset => _aimRotationApplied ? _aimRotationOffset : Vector3.zero;

    private void Awake()
    {
        if (realCamera == null)
        {
            var found = GetComponentInChildren<UnityEngine.Camera>();
            if (found != null) realCamera = found.transform;
        }
        if (proceduralSpine == null) proceduralSpine = GetComponentInParent<ProceduralSpine>();
        if (aimEffectsSource == null) aimEffectsSource = GetComponentInChildren<WeaponPoseController>(true);
        if (characterRoot == null)
            characterRoot = proceduralSpine != null && proceduralSpine.animationRoot != null
                ? proceduralSpine.animationRoot : transform.root;
    }

    private void OnEnable()
    {
        // Re-enabling must not capture a head currently displaced by an animation/turn.
        // A fresh component captures lazily; an existing one keeps its calibrated eye offset.
        ResetPositionSmoothing();
        ValidateSetup();
    }

    private void OnValidate()
    {
        positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
        collisionRadius = Mathf.Max(0.001f, collisionRadius);
        collisionPadding = Mathf.Max(0f, collisionPadding);
        recoilReturnSpeed = Mathf.Max(0f, recoilReturnSpeed);
        fovSpeed = Mathf.Max(0f, fovSpeed);
        _anchorInitialized = false;
    }

    private void OnDisable() { ResetAimCameraEffects(); }

    /// <summary>Call after replacing references or changing the hierarchy at runtime.</summary>
    [ContextMenu("Validate Setup")]
    public bool ValidateSetup()
    {
        _reportedError = false;
        _boundSource = lookController;
        _boundRealCamera = realCamera;
        _lookSource = lookController as ICameraLookSource;
        _camera = realCamera != null ? realCamera.GetComponent<UnityEngine.Camera>() : null;
        string error = GetSetupError();
        if (error == null) return true;
        Debug.LogWarning("[CameraRigController] " + error, this);
        _reportedError = true;
        return false;
    }

    private string GetSetupError()
    {
        Transform holder = cameraHolder != null ? cameraHolder : transform;
        if (realCamera == null) return "Assign Real Camera.";
        if (positionSource != CameraPositionSource.PlayerRoot && headBone == null && stabilizationBone == null)
            return "Head/HeadStabilized mode needs Head Bone or Stabilization Bone.";
        if (positionSource != CameraPositionSource.Head && characterRoot == null)
            return "PlayerRoot/HeadStabilized mode needs Character Root (Player, not the animated model).";
        Transform anchorBone = headBone != null ? headBone : stabilizationBone;
        if (positionSource == CameraPositionSource.HeadStabilized && anchorBone != null && characterRoot != null &&
            (anchorBone == characterRoot || !anchorBone.IsChildOf(characterRoot)))
            return "HeadStabilized needs the head to be below Character Root.";
        if (lookController != null && _lookSource == null)
            return "Look Controller must implement ICameraLookSource. Alternatively clear it and call SetLookInputs.";
        if (headBone != null && headBone.IsChildOf(transform))
            return "The rig cannot contain Head. Place it below Head or on a separate branch, not on the character root.";
        if (stabilizationBone != null && stabilizationBone.IsChildOf(transform))
            return "Stabilization Bone cannot be inside the driven rig.";
        if (characterRoot != null && characterRoot.IsChildOf(transform))
            return "Character Root must be outside/above the driven rig, never the rig itself.";
        if (!holder.IsChildOf(transform)) return "Camera Holder must be this rig or a child of it.";
        if (!realCamera.IsChildOf(holder)) return "Real Camera must be the holder or a child of it.";
        return null;
    }

    private void LateUpdate()
    {
        ApplyCamera(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    private void ApplyCamera(float dt)
    {
        // Remove only our previous FOV overlay before the existing base-FOV transition.
        // Never feed an Aim-modified FOV back into that baseline.
        RemoveOwnAimFov();
        if (_aimRotationCamera != null && _aimRotationCamera != realCamera)
        {
            RemoveOwnAimRotation();
            _aimInputInitialized = false;
            _aimCameraSpring.Reset();
            _aimTimedValid = false;
            _aimRotationOffset = Vector3.zero;
        }
        if (_boundSource != lookController || _boundRealCamera != realCamera) ValidateSetup();
        if (realCamera == null || _reportedError) return;
        if (positionSource != CameraPositionSource.PlayerRoot && headBone == null && stabilizationBone == null) return;
        if (positionSource != CameraPositionSource.Head && characterRoot == null) return;
        if (_lastPositionSource != positionSource)
        {
            RecalibrateAnchor();
            _lastPositionSource = positionSource;
        }

        float pitch = _pitch, yaw = _yaw, lean = _lean;
        bool thirdPerson = isTPS;
        // Unity null check above prevents invoking a destroyed MonoBehaviour through an interface.
        if (lookController != null && _lookSource != null)
        {
            pitch = _lookSource.CameraPitch;
            yaw = _lookSource.CameraYaw;
            lean = _lookSource.CameraLean;
            thirdPerson = _lookSource.IsThirdPerson;
        }
        if (useSpineLean && proceduralSpine != null) lean = proceduralSpine.inputLean;
        pitch = Mathf.Clamp(pitch, -90f, 90f);
        Quaternion view = Quaternion.Euler(-pitch, yaw, -lean);
        if (yawRelativeToRoot && characterRoot != null) view = characterRoot.rotation * view;

        dt = WeaponAimSpring.Safe(dt, 0f, 60f, 0f);
        // Keep independent world-space state: inherited head rotation must not rotate the filter history.
        Vector3 targetAnchor = GetTargetAnchor(yaw);
        if (!_anchorInitialized || positionSource == CameraPositionSource.PlayerRoot ||
            !smoothHeadPosition || positionSmoothTime <= 0f)
        {
            _anchorPosition = targetAnchor;
            _positionVelocity = Vector3.zero;
            _anchorInitialized = true;
        }
        else if (dt > 0f)
            _anchorPosition = Vector3.SmoothDamp(_anchorPosition, targetAnchor, ref _positionVelocity,
                positionSmoothTime, Mathf.Infinity, dt);

        Vector3 anchor = _anchorPosition + view * offset;
        ViewAnchor = anchor;
        ViewRotation = view;
        transform.SetPositionAndRotation(anchor, view);
        Transform holder = cameraHolder != null ? cameraHolder : transform;
        if (holder != transform) holder.SetPositionAndRotation(anchor, view);

        Vector3 cameraPosition = thirdPerson ? GetThirdPersonPosition(anchor, view, -pitch) : anchor;
        _recoilEuler *= Mathf.Exp(-recoilReturnSpeed * dt);
        if (_recoilEuler.sqrMagnitude < 1e-8f) _recoilEuler = Vector3.zero;
        UpdateAimCamera(dt, thirdPerson);
        Quaternion finalRotation = view * Quaternion.Euler(_recoilEuler) * _additiveRotation;
        _rotationWithoutAim = finalRotation;
        _aimRotationApplied = CanShakeCamera() && _aimRotationOffset.sqrMagnitude > 0f;
        if (_aimRotationApplied) finalRotation *= Quaternion.Euler(_aimRotationOffset);
        _rotationWithAim = finalRotation;
        _aimRotationCamera = realCamera;
        realCamera.SetPositionAndRotation(cameraPosition + view * _additivePosition, finalRotation);
        UpdateFov(dt);
        ApplyAimFov();
    }

    private bool CanShakeCamera()
    {
        Transform holder = cameraHolder != null ? cameraHolder : transform;
        // Rotating a shared holder would also rotate the weapon and break camera-only isolation.
        return realCamera != null && realCamera != transform && realCamera != holder;
    }

    private void UpdateAimCamera(float dt, bool thirdPerson)
    {
        WeaponPoseController source = aimEffectsSource;
        WeaponPoseProfile profile = source != null ? source.profile : null;
        WeaponAimCameraSettings settings = profile != null ? profile.aimCamera : null;
        bool active = source != null && source.isActiveAndEnabled && profile != null && profile.HasIdle && profile.HasAim &&
            settings != null && settings.enabled && (!thirdPerson || settings.applyInThirdPerson) &&
            (!profile.synchronizeAimEffects || source.HasPreparedPose);
        bool synchronized = active && profile.synchronizeAimEffects;
        bool aiming = active && (synchronized ? source.TransitionIsAiming : source.IsAiming);
        float targetFovOffset = aiming ? WeaponAimSpring.Safe(settings.aimFovOffset, -30f, 30f, 0f) : 0f;
        bool sameContext = _aimInputInitialized && _aimSource == source && _aimProfile == profile;

        if (synchronized)
        {
            if (!sameContext || !_aimTimedValid || !_aimSyncMode || _aimTransitionVersion != source.TransitionVersion)
            {
                _aimTimedFovStart = _aimFovOffset;
                _aimTimedStart = sameContext ? _aimRotationOffset : Vector3.zero;
                _aimTimedKick = sameContext && source.TransitionIsStateChange
                    ? aiming ? settings.aimRotationKick : settings.idleRotationKick : Vector3.zero;
                _aimTransitionVersion = source.TransitionVersion;
                _aimTimedValid = true;
            }
            _aimFovOffset = Mathf.Lerp(_aimTimedFovStart, targetFovOffset, source.TransitionBlend);
            float limit = WeaponAimSpring.Safe(settings.maxRotationOffset, 0f, 2f, 0f);
            _aimRotationOffset = settings.shakeEnabled && CanShakeCamera()
                ? AimTransitionMath.Motion(_aimTimedStart, _aimTimedKick, source.TransitionProgress,
                    profile.transitionOscillations, settings.damping, limit) : Vector3.zero;
            _aimCameraSpring.Reset();
            _aimSource = source; _aimProfile = profile; _lastAiming = aiming;
            _aimInputInitialized = _aimSyncMode = true;
            return;
        }

        _aimTimedValid = false;
        float speed = settings != null ? WeaponAimSpring.Safe(settings.fovBlendSpeed, 0f, 100f, 12f) : 12f;
        float blend = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * dt);
        _aimFovOffset = Mathf.Lerp(_aimFovOffset, targetFovOffset, blend);
        if (Mathf.Abs(_aimFovOffset - targetFovOffset) < 0.001f) _aimFovOffset = targetFovOffset;

        if (!active)
        {
            _aimInputInitialized = false;
            _aimCameraSpring.Reset();
            _aimRotationOffset = Vector3.zero;
            _aimSyncMode = false;
            return;
        }
        if (!sameContext || _aimSyncMode) _aimCameraSpring.Reset();
        if (settings.shakeEnabled && CanShakeCamera())
        {
            float limit = WeaponAimSpring.Safe(settings.maxRotationOffset, 0f, 2f, 0f);
            if (sameContext && _lastAiming != aiming)
                _aimCameraSpring.Kick(aiming ? settings.aimRotationKick : settings.idleRotationKick, settings.frequency, limit);
            _aimCameraSpring.Step(dt, settings.frequency, settings.damping, limit);
        }
        else _aimCameraSpring.Reset();
        _aimRotationOffset = _aimCameraSpring.Offset;
        _aimSource = source;
        _aimProfile = profile;
        _lastAiming = aiming;
        _aimInputInitialized = true;
        _aimSyncMode = false;
    }

    private void RemoveOwnAimFov()
    {
        if (_aimFovApplied && _aimFovCamera != null && Mathf.Abs(_aimFovCamera.fieldOfView - _fovWithAim) < 0.001f)
            _aimFovCamera.fieldOfView = _fovWithoutAim;
        _aimFovApplied = false;
    }

    private void ApplyAimFov()
    {
        if (_camera == null || _aimFovOffset == 0f) return;
        _fovWithoutAim = _camera.fieldOfView;
        _fovWithAim = Mathf.Clamp(_fovWithoutAim + _aimFovOffset, 1f, 179f);
        _camera.fieldOfView = _fovWithAim;
        _aimFovCamera = _camera;
        _aimFovApplied = true;
    }

    private void ResetAimCameraEffects()
    {
        RemoveOwnAimFov();
        RemoveOwnAimRotation();
        _aimInputInitialized = false;
        _aimFovOffset = 0f;
        _aimCameraSpring.Reset();
        _aimRotationOffset = Vector3.zero;
        _aimTimedValid = _aimSyncMode = false;
    }

    private void RemoveOwnAimRotation()
    {
        if (_aimRotationApplied && _aimRotationCamera != null &&
            Quaternion.Angle(_aimRotationCamera.rotation, _rotationWithAim) < 0.05f)
            _aimRotationCamera.rotation = _rotationWithoutAim;
        _aimRotationApplied = false;
    }

    private Vector3 GetTargetAnchor(float yaw)
    {
        Transform head = headBone != null ? headBone : stabilizationBone;
        if (positionSource == CameraPositionSource.Head) return head.position;
        if (positionSource == CameraPositionSource.HeadStabilized)
        {
            float worldYaw = yawRelativeToRoot ? characterRoot.eulerAngles.y + yaw : yaw;
            return _headStabilizer.TryGetPosition(characterRoot, head, proceduralSpine, worldYaw, out Vector3 position)
                ? position : head.position;
        }
        if (!captureHeadPosition)
        {
            _rootAnchorCaptured = false;
            return characterRoot.position + rootAnchorOffset;
        }
        if (!_rootAnchorCaptured || _capturedRoot != characterRoot || _capturedHead != head)
        {
            // Do NOT use TransformPoint/localRotation each frame: turning the Player would rotate
            // the eye offset around the body again. Cache a WORLD offset, then follow translation only.
            _capturedRootOffset = head != null ? head.position - characterRoot.position : rootAnchorOffset;
            _capturedRoot = characterRoot;
            _capturedHead = head;
            _rootAnchorCaptured = true;
        }
        return characterRoot.position + _capturedRootOffset;
    }

    private Vector3 GetThirdPersonPosition(Vector3 origin, Quaternion view, float pitch)
    {
        Vector3 displacement = tpsOffset;
        displacement.z *= distanceCurve != null ? Mathf.Max(0f, distanceCurve.Evaluate(pitch)) : 1f;
        displacement = view * displacement;
        float distance = displacement.magnitude;
        if (distance < 1e-5f) return origin;
        Vector3 direction = displacement / distance;
        float radius = Mathf.Max(0.001f, collisionRadius);
        // SphereCast alone does not handle a sphere already overlapping at the origin.
        if (Physics.CheckSphere(origin, radius, collisionLayers, QueryTriggerInteraction.Ignore)) return origin;
        if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, distance,
                collisionLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - Mathf.Max(0f, collisionPadding));
        return origin + direction * distance;
    }

    /// <summary>Absolute view input: pitch positive UP, yaw WORLD degrees, lean positive left.
    /// With yawRelativeToRoot, yaw is relative to characterRoot instead. Does not drive the spine.</summary>
    public void SetLookInputs(float pitchUp, float yaw, float lean = 0f, bool thirdPerson = false)
    {
        _pitch = Mathf.Clamp(pitchUp, -90f, 90f);
        _yaw = yaw;
        _lean = lean;
        isTPS = thirdPerson;
    }

    /// <summary>Call on teleports/respawn so the filter does not drag across the scene.</summary>
    public void ResetPositionSmoothing()
    {
        _anchorInitialized = false;
        _positionVelocity = Vector3.zero;
    }

    /// <summary>Call only when intentionally changing eye placement (new avatar/pose).
    /// Unlike ResetPositionSmoothing, this captures a NEW head offset on the next camera update.</summary>
    [ContextMenu("Recalibrate Camera Anchor")]
    public void RecalibrateAnchor()
    {
        _rootAnchorCaptured = false;
        _headStabilizer.Reset();
        ResetPositionSmoothing();
    }

    public void AddCameraRecoil(Vector3 eulerDegrees) { _recoilEuler += eulerDegrees; }

    /// <summary>Absolute camera-only effect pose in stable view space. Reset with zero/identity.
    /// Position effects are not collision-tested; keep them small or zero in TPS.</summary>
    public void SetAdditivePose(Vector3 position, Quaternion rotation)
    {
        _additivePosition = position;
        _additiveRotation = rotation;
    }

    public void ResetCameraEffects()
    {
        ResetAimCameraEffects();
        _recoilEuler = Vector3.zero;
        _additivePosition = Vector3.zero;
        _additiveRotation = Quaternion.identity;
    }

    public void UpdateTargetFOV(float fieldOfView)
    {
        _targetFov = Mathf.Clamp(fieldOfView, 1f, 179f);
        _hasFovTarget = true;
    }

    private void UpdateFov(float dt)
    {
        if (_camera == null || !_hasFovTarget) return;
        float blend = fovSpeed <= 0f ? 1f : 1f - Mathf.Exp(-fovSpeed * dt);
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, blend);
        if (Mathf.Abs(_camera.fieldOfView - _targetFov) < 0.01f)
        {
            _camera.fieldOfView = _targetFov;
            _hasFovTarget = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (realCamera == null) return;
        Vector3 anchor = Application.isPlaying ? ViewAnchor
            : headBone != null ? headBone.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(anchor, Vector3.one * 0.1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(realCamera.position, collisionRadius);
        Gizmos.DrawLine(anchor, realCamera.position);
    }
#endif
}
