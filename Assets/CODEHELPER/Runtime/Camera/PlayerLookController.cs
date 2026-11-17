using UnityEngine;
using CodeHelper.ProceduralShooterFramework.Animation;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PlayerLookInputMode { MouseAndKeyboard, External }

/// <summary>
/// Owns look INPUT and upright body yaw, never the camera/head transforms.
/// CameraRigController consumes ICameraLookSource after animation; the spine receives relative angles.
/// </summary>
[AddComponentMenu("CodeHelper/Shooter Framework/Camera/Player Look Controller")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class PlayerLookController : MonoBehaviour, ICameraLookSource
{
    [Header("1. References")]
    [SerializeField] private ProceduralSpine spineController;
    [Tooltip("Player root to rotate, not Character's head or the camera holder. Defaults to this transform.")]
    public Transform bodyRoot;
    [Tooltip("CameraRigController on CameraHolder. Auto-found in children if empty.")]
    public CameraRigController cameraRig;
    private Animator _animator;

    [Header("2. Input and camera")]
    public PlayerLookInputMode inputMode = PlayerLookInputMode.MouseAndKeyboard;
    public bool inputEnabled = true;
    public bool lockCursorOnEnable = true;
    public bool useUnscaledTime;
    [Tooltip("Degrees per mouse pixel with the new Input System. Start around 0.1. No deltaTime multiplier.")]
    [Min(0f)] public float mouseSensitivity = 0.1f;
    [Tooltip("Mouse delta smoothing in seconds. Zero gives direct response. Avoid adding more camera smoothing.")]
    [Range(0f, 0.5f)] public float inputSmoothness;
    public bool invertY;
    [Tooltip("Camera pitch limits in degrees, positive UP.")]
    public Vector2 pitchLimit = new Vector2(-90f, 90f);
    public bool isTPS;

    [Header("3. Body turn in place")]
    [Range(0f, 90f)] public float softLimitAngle = 50f;
    [Range(0f, 90f)] public float hardLimitAngle = 80f;
    [Min(0f)] public float bodyTurnSpeed = 5f;
    [Tooltip("Optional float parameter. If absent, turn animation is skipped without Console spam.")]
    public string turnAngleParameter = "TurnAngle";

    [Header("4. Lean")]
    [Range(0f, 45f)] public float maxLeanAngle = 20f;
    [Min(0f)] public float leanSpeed = 5f;

    [Header("5. Debug")]
    public bool showDebug;
    public float debugHeight = 1.5f;

    public float CameraYaw { get; private set; }
    public float CameraPitch { get; private set; } // positive UP
    public float CameraLean => _currentLean; // Q negative, E positive; camera applies -lean around forward
    public bool IsThirdPerson => isTPS;
    public bool IsAiming { get; private set; }
    public float BodyYawOffset => bodyRoot != null ? Mathf.DeltaAngle(bodyRoot.eulerAngles.y, CameraYaw) : 0f;

    [HideInInspector] public bool blockBodyRotation;
    [HideInInspector] public bool overrideSpineControl;
    [HideInInspector] public float externalSpinePitch; // positive UP (same convention as CameraPitch)
    [HideInInspector] public float externalSpineYaw;   // relative to bodyRoot, not world yaw

    private float _bodyYawTarget, _currentLean, _turnAngle;
    private Vector2 _smoothMouseDelta, _externalLook;
    private float _externalLean;
    private bool _externalAiming;
    private RuntimeAnimatorController _cachedController;
    private string _cachedTurnParameter;
    private int _turnAngleId;
    private bool _hasTurnParameter;
    private enum TurnState { Quieto, RotandoSuave, RotandoLimiteDuro }
    private TurnState _currentState;

    private void Awake()
    {
        if (bodyRoot == null) bodyRoot = transform;
        if (spineController == null) spineController = GetComponentInChildren<ProceduralSpine>(true);
        if (cameraRig == null) cameraRig = GetComponentInChildren<CameraRigController>(true);
        _animator = GetComponentInChildren<Animator>(true);
        SetLookAngles(0f, bodyRoot.eulerAngles.y);
        CacheTurnParameter();
    }

    private void Start() { ConnectCamera(); }

    private void OnEnable()
    {
        _smoothMouseDelta = Vector2.zero;
        _externalLook = Vector2.zero;
        if (Application.isPlaying && lockCursorOnEnable && inputMode == PlayerLookInputMode.MouseAndKeyboard)
            SetCursorLocked(true);
    }

    private void OnDisable()
    {
        _smoothMouseDelta = Vector2.zero;
        _externalLook = Vector2.zero;
        if (Application.isPlaying && inputMode == PlayerLookInputMode.MouseAndKeyboard) SetCursorLocked(false);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && inputMode == PlayerLookInputMode.MouseAndKeyboard) SetCursorLocked(false);
    }

    private void OnValidate()
    {
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        inputSmoothness = Mathf.Clamp(inputSmoothness, 0f, 0.5f);
        pitchLimit.x = Mathf.Clamp(pitchLimit.x, -90f, 90f);
        pitchLimit.y = Mathf.Clamp(pitchLimit.y, pitchLimit.x, 90f);
        hardLimitAngle = Mathf.Clamp(hardLimitAngle, 0f, 90f);
        softLimitAngle = Mathf.Clamp(softLimitAngle, 0f, hardLimitAngle);
        bodyTurnSpeed = Mathf.Max(0f, bodyTurnSpeed);
        leanSpeed = Mathf.Max(0f, leanSpeed);
    }

    /// <summary>Call after replacing CameraRig at runtime. Does not take over a different look source.</summary>
    [ContextMenu("Connect Camera")]
    public void ConnectCamera()
    {
        if (cameraRig == null) cameraRig = GetComponentInChildren<CameraRigController>(true);
        if (cameraRig == null) return;
        if (cameraRig.lookController != null && cameraRig.lookController != this)
        {
            Debug.LogWarning("[PlayerLookController] CameraRig has another Look Controller. Assign this component explicitly.", this);
            return;
        }
        cameraRig.lookController = this;
        if (cameraRig.characterRoot == null) cameraRig.characterRoot = bodyRoot != null ? bodyRoot : transform;
        // This source publishes WORLD yaw and its own lean, not the spine's axis-converted lean.
        cameraRig.yawRelativeToRoot = false;
        cameraRig.useSpineLean = false;
        cameraRig.ValidateSetup();
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector2 look = Vector2.zero;
        float lean = 0f;
        bool aiming = false;
        if (inputMode == PlayerLookInputMode.External)
        {
            if (inputEnabled) { look = _externalLook; lean = _externalLean; aiming = _externalAiming; }
            _externalLook = Vector2.zero; // A mouse delta is consumed once, not replayed each frame.
        }
        else
            ReadMouseAndKeyboard(out look, out lean, out aiming);

        if (!inputEnabled) { look = Vector2.zero; lean = 0f; aiming = false; _smoothMouseDelta = Vector2.zero; }
        ProcessLookFrame(look, lean, aiming, dt);
        if (showDebug) DrawWorldDebugLines();
    }

    private void ReadMouseAndKeyboard(out Vector2 look, out float lean, out bool aiming)
    {
        look = Vector2.zero; lean = 0f; aiming = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { SetCursorLocked(false); return; }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (inputEnabled && mouse != null && mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            return;
        }
        if (!inputEnabled) return;
        if (keyboard != null)
        {
            lean = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            if (keyboard.vKey.wasPressedThisFrame) isTPS = !isTPS;
        }
        if (mouse != null) { look = mouse.delta.ReadValue(); aiming = mouse.rightButton.isPressed; }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) { SetCursorLocked(false); return; }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (inputEnabled && Input.GetMouseButtonDown(0)) SetCursorLocked(true);
            return;
        }
        if (!inputEnabled) return;
        look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        lean = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        aiming = Input.GetMouseButton(1);
        if (Input.GetKeyDown(KeyCode.V)) isTPS = !isTPS;
#endif
    }

    private void ProcessLookFrame(Vector2 look, float lean, bool aiming, float dt)
    {
        IsAiming = aiming;
        if (dt > 0f)
        {
            float blend = inputSmoothness > 0f ? 1f - Mathf.Exp(-dt / inputSmoothness) : 1f;
            _smoothMouseDelta = Vector2.Lerp(_smoothMouseDelta, look, blend);
            CameraYaw = Mathf.DeltaAngle(0f, CameraYaw + _smoothMouseDelta.x * mouseSensitivity);
            CameraPitch = ClampPitch(CameraPitch + _smoothMouseDelta.y * mouseSensitivity * (invertY ? -1f : 1f));
            _currentLean = Mathf.Lerp(_currentLean, Mathf.Clamp(lean, -1f, 1f) * maxLeanAngle,
                1f - Mathf.Exp(-Mathf.Max(0f, leanSpeed) * dt));
            HandleBodyOrientation(dt);
        }
        UpdateTurnAnimator(dt);
        SendToSpine();
    }

    private void HandleBodyOrientation(float dt)
    {
        if (bodyRoot == null) return;
        float bodyYaw = bodyRoot.eulerAngles.y;
        if (blockBodyRotation)
        {
            _bodyYawTarget = bodyYaw;
            _currentState = TurnState.Quieto;
            return;
        }
        float hard = Mathf.Clamp(hardLimitAngle, 0f, 90f);
        float soft = Mathf.Clamp(softLimitAngle, 0f, hard);
        float delta = Mathf.DeltaAngle(bodyYaw, CameraYaw);
        bool forced = Mathf.Abs(delta) > hard;
        if (forced)
        {
            bodyYaw = CameraYaw - hard * Mathf.Sign(delta);
            _bodyYawTarget = bodyYaw; // Do not interpolate back toward a stale heading after enforcing the limit.
            bodyRoot.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            delta = Mathf.DeltaAngle(bodyYaw, CameraYaw); // Recompute after hard-limit correction.
        }
        bool pastSoft = Mathf.Abs(delta) > soft;
        if (pastSoft) _bodyYawTarget = CameraYaw;
        if (pastSoft || Mathf.Abs(Mathf.DeltaAngle(bodyYaw, _bodyYawTarget)) > 0.1f)
        {
            float speed = Mathf.Max(0f, bodyTurnSpeed) * (IsAiming ? 1.5f : 1f);
            float yaw = Mathf.LerpAngle(bodyYaw, _bodyYawTarget, 1f - Mathf.Exp(-speed * dt));
            bodyRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            _currentState = forced ? TurnState.RotandoLimiteDuro : TurnState.RotandoSuave;
        }
        else _currentState = forced ? TurnState.RotandoLimiteDuro : TurnState.Quieto;
    }

    private void SendToSpine()
    {
        if (spineController == null) return;
        float pitch = overrideSpineControl && isTPS ? externalSpinePitch : CameraPitch;
        float yaw = overrideSpineControl && isTPS ? externalSpineYaw : BodyYawOffset;
        yaw = Mathf.Clamp(yaw, -Mathf.Clamp(hardLimitAngle, 0f, 90f), Mathf.Clamp(hardLimitAngle, 0f, 90f));
        // SpineLookMath's positive X looks DOWN; camera pitch is positive UP. Its roll has the
        // opposite sign to the camera lean convention. Convert once here, not in the rig or job.
        // Pass the same relative yaw as turnOffset too, so either spine yaw mode can consume it.
        spineController.SetLookInputs(-pitch, yaw, -_currentLean, yaw);
    }

    private void CacheTurnParameter()
    {
        _cachedController = _animator != null ? _animator.runtimeAnimatorController : null;
        _cachedTurnParameter = turnAngleParameter;
        _hasTurnParameter = false;
        if (_cachedController == null || string.IsNullOrEmpty(turnAngleParameter)) return;
        _turnAngleId = Animator.StringToHash(turnAngleParameter);
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
            if (parameter.nameHash == _turnAngleId && parameter.type == AnimatorControllerParameterType.Float)
            { _hasTurnParameter = true; break; }
    }

    private void UpdateTurnAnimator(float dt)
    {
        if (_animator == null) return;
        if (_cachedController != _animator.runtimeAnimatorController || _cachedTurnParameter != turnAngleParameter)
            CacheTurnParameter();
        if (!_hasTurnParameter) return;
        float target = _currentState == TurnState.Quieto ? 0f : BodyYawOffset;
        _turnAngle = Mathf.Lerp(_turnAngle, target, 1f - Mathf.Exp(-10f * dt));
        if (spineController != null && spineController.animator == _animator)
            spineController.SetFloat(_turnAngleId, _turnAngle);
        else _animator.SetFloat(_turnAngleId, _turnAngle);
    }

    /// <summary>External input: publish before this component's Update (-50). Delta is consumed once.
    /// Use External input mode; lean is -1 left / +1 right. Supply raw mouse-like deltas, not absolute angles.</summary>
    public void SetInput(Vector2 lookDelta, float lean, bool aiming)
    {
        _externalLook = lookDelta;
        _externalLean = Mathf.Clamp(lean, -1f, 1f);
        _externalAiming = aiming;
    }

    public void SetLookAngles(float pitchUp, float worldYaw, bool alignBody = false)
    {
        CameraPitch = ClampPitch(pitchUp);
        CameraYaw = Mathf.DeltaAngle(0f, worldYaw);
        _smoothMouseDelta = Vector2.zero;
        _bodyYawTarget = bodyRoot != null ? bodyRoot.eulerAngles.y : CameraYaw;
        if (alignBody && bodyRoot != null)
        {
            bodyRoot.rotation = Quaternion.Euler(0f, CameraYaw, 0f);
            _bodyYawTarget = CameraYaw;
        }
    }

    /// <summary>Legacy API convention retained: positive vertical REDUCES pitch (looks down).
    /// For an upward kick pass a negative vertical value. Horizontal positive turns right.</summary>
    public void AddRecoilInput(float vertical, float horizontal)
    {
        CameraPitch = ClampPitch(CameraPitch - vertical);
        CameraYaw = Mathf.DeltaAngle(0f, CameraYaw + horizontal);
    }

    public Vector3 GetCameraRotation() => new Vector3(CameraPitch, CameraYaw, 0f);

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        _smoothMouseDelta = Vector2.zero;
    }

    private float ClampPitch(float pitch)
    {
        float min = Mathf.Clamp(pitchLimit.x, -90f, 90f);
        float max = Mathf.Clamp(pitchLimit.y, min, 90f);
        return Mathf.Clamp(pitch, min, max);
    }

    private void OnGUI()
    {
        if (!showDebug) return;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height - 50f);
        Color saved = GUI.color;
        DrawLineGUI(center, 0f, 100f, Color.white, 2f);
        DrawLineGUI(center, -softLimitAngle, 100f, Color.green);
        DrawLineGUI(center, softLimitAngle, 100f, Color.green);
        DrawLineGUI(center, -hardLimitAngle, 100f, Color.red);
        DrawLineGUI(center, hardLimitAngle, 100f, Color.red);
        DrawLineGUI(center, BodyYawOffset, 90f, Color.cyan, 3f);
        GUI.color = Color.white;
        GUI.Label(new Rect(center.x - 50f, center.y + 10f, 200f, 20f), $"Delta: {BodyYawOffset:F1}°");
        GUI.Label(new Rect(center.x - 50f, center.y + 28f, 200f, 20f), $"State: {_currentState}");
        GUI.color = saved;
    }

    private static void DrawLineGUI(Vector2 start, float angle, float length, Color color, float thickness = 1f)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.color = color;
        GUI.DrawTexture(new Rect(start.x - thickness * 0.5f, start.y - length, thickness, length), Texture2D.whiteTexture);
        GUI.matrix = saved;
    }

    private void DrawWorldDebugLines()
    {
        Transform root = bodyRoot != null ? bodyRoot : transform;
        Vector3 origin = root.position + Vector3.up * debugHeight;
        Debug.DrawRay(origin, root.forward * 2f, Color.white);
        Debug.DrawRay(origin, Quaternion.Euler(0f, -softLimitAngle, 0f) * root.forward * 3f, Color.green);
        Debug.DrawRay(origin, Quaternion.Euler(0f, softLimitAngle, 0f) * root.forward * 3f, Color.green);
        Debug.DrawRay(origin, Quaternion.Euler(0f, -hardLimitAngle, 0f) * root.forward * 3f, Color.red);
        Debug.DrawRay(origin, Quaternion.Euler(0f, hardLimitAngle, 0f) * root.forward * 3f, Color.red);
        Debug.DrawRay(origin, Quaternion.Euler(0f, BodyYawOffset, 0f) * root.forward * 3f, Color.cyan);
    }
}
