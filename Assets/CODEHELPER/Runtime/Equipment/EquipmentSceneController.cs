using System;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Weapons;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    [Serializable]
    public sealed class EquipmentSceneSlot
    {
        public EquipmentDefinition definition;
        [Tooltip("Existing scene model, not the common holder or character. Null for fists.")]
        public Transform visualRoot;
        [Tooltip("Blend this model's authored rig clip from/to its saved pose using the action's Blend In/Out. Useful for coordinated melee weapon/hand rigs.")]
        public bool blendWeaponClip;
        public Transform leftGrip, rightGrip, leftHint, rightHint;
        [Tooltip("Optional reload contact/motion anchor under Weapon Holder, outside both arm chains. Moving this object repositions the left reload for this weapon.")]
        public Transform leftReloadTarget;
        [Tooltip("Optional editable guard/action points. Unassigned hands retain their existing grip/FK behavior.")]
        public EquipmentHandBinding leftHandPose = new EquipmentHandBinding();
        public EquipmentHandBinding rightHandPose = new EquipmentHandBinding();
    }

    /// <summary>First scene binding: reuses existing visuals/grips and the spine-owned clip channels.</summary>
    [AddComponentMenu("CodeHelper/Shooter Framework/Equipment/Equipment Scene Controller")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class EquipmentSceneController : MonoBehaviour
    {
        public ProceduralSpine spine;
        public HandIKSolver handIK;
        public WeaponPoseController weaponPose;
        public PlayerLookController look;
        public EquipmentSceneSlot[] slots = new EquipmentSceneSlot[0];
        public int initialSlot;
        public bool readInput = true;
        [Tooltip("Enable R for the assigned reload action. Character, weapon and grip curves must be configured together.")]
        public bool enableReloadInput;
        public bool showControls = true;
        public bool useUnscaledTime;
        public EquipmentRuntime Runtime { get; private set; }
        public int SelectedSlot { get; private set; } = -1;

        private EquipmentInstance[] _instances;
        private bool[] _visualActive;
        private bool _captured, _oldUseLook, _oldPoseEnabled, _oldAiming, _inputArmed;
        private WeaponPoseProfile _oldProfile;
        private Transform _oldLeftGrip, _oldRightGrip, _oldLeftHint, _oldRightHint;
        private float _oldLeftWeight, _oldRightWeight;
        private double _idleTime;
        private int _burstRemaining;
        private Transform _effectRoot;
        private Vector3 _beforePosition, _afterPosition;
        private Quaternion _beforeRotation, _afterRotation;
        private bool _effectApplied;
        private readonly EquipmentWeaponClip _weaponClip = new EquipmentWeaponClip();
        private bool _queuedReload;
        private readonly EquipmentLeftHandTarget _leftReload = new EquipmentLeftHandTarget();
        private bool _usesLeftReloadTarget;
        private readonly EquipmentCameraArms _cameraArms = new EquipmentCameraArms();
        private readonly EquipmentHandTarget _leftPose = new EquipmentHandTarget(), _rightPose = new EquipmentHandTarget();
        private bool _usesLeftPose, _usesRightPose;

        private void OnEnable()
        {
            if (spine == null || handIK == null || weaponPose == null || slots == null || slots.Length == 0)
            {
                Debug.LogWarning("[Equipment] Assign spine, hands, weapon pose and scene slots.", this);
                return;
            }
            Capture();
            if (_instances == null || _instances.Length != slots.Length)
            {
                _instances = new EquipmentInstance[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && slots[i].definition != null) _instances[i] = new EquipmentInstance(slots[i].definition);
            }
            Runtime = new EquipmentRuntime();
            SelectSlot(SelectedSlot >= 0 ? SelectedSlot : initialSlot);
        }

        private void Capture()
        {
            _oldProfile = weaponPose.profile;
            _oldUseLook = weaponPose.useLookController;
            _oldAiming = weaponPose.IsAiming;
            _oldPoseEnabled = weaponPose.enabled;
            _oldLeftGrip = handIK.leftHand.target; _oldRightGrip = handIK.rightHand.target;
            _oldLeftHint = handIK.leftHand.hint; _oldRightHint = handIK.rightHand.hint;
            _oldLeftWeight = handIK.leftHand.weight; _oldRightWeight = handIK.rightHand.weight;
            _visualActive = new bool[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]?.visualRoot != null) _visualActive[i] = slots[i].visualRoot.gameObject.activeSelf;
            _captured = true;
        }

        private void OnDisable()
        {
            RemoveMotion();
            _weaponClip.Clear();
            Runtime?.Clear();
            _burstRemaining = 0;
            _queuedReload = false;
            _inputArmed = false;
            if (!_captured) { DisposeHandTargets(); return; }
            if (spine != null) spine.SetEquipmentAnimation(null, null, 0, null, null, 0, 0f);
            if (weaponPose != null)
            {
                weaponPose.profile = _oldProfile;
                weaponPose.useLookController = _oldUseLook;
                weaponPose.SetAiming(_oldAiming);
                weaponPose.enabled = _oldPoseEnabled;
            }
            if (handIK != null)
            {
                handIK.leftHand.target = _oldLeftGrip; handIK.rightHand.target = _oldRightGrip;
                handIK.leftHand.hint = _oldLeftHint; handIK.rightHand.hint = _oldRightHint;
                handIK.leftHand.weight = _oldLeftWeight; handIK.rightHand.weight = _oldRightWeight;
                handIK.ValidateSetup();
            }
            for (int i = 0; i < slots.Length && i < _visualActive.Length; i++)
                if (slots[i]?.visualRoot != null) slots[i].visualRoot.gameObject.SetActive(_visualActive[i]);
            _captured = false;
            _usesLeftReloadTarget = false;
            DisposeHandTargets();
        }

        private void DisposeHandTargets()
        {
            _leftReload.Dispose(); _leftPose.Dispose(); _rightPose.Dispose();
            _usesLeftPose = _usesRightPose = false;
        }

        public bool SelectSlot(int index)
        {
            if (Runtime == null || _instances == null || index < 0 || index >= _instances.Length || _instances[index] == null)
                return false;
            if (!Runtime.TrySelect(_instances[index])) return false;
            _queuedReload = false;
            _burstRemaining = 0;
            Synchronize(0f);
            return true;
        }

        private void Update() => Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, true);

        // Explicit step is also used by isolated scene checks; never tick Runtime separately.
        public void Step(float deltaTime, bool processInput = false)
        {
            if (!_captured || Runtime == null) return;
            RemoveMotion();
            float dt = EquipmentNumbers.NonNegative(deltaTime);
            Runtime.Tick(dt);
            if (processInput) ReadInput();
            Synchronize(dt);
        }

        private void Synchronize(float dt)
        {
            EquipmentInstance current = Runtime.Current;
            int slot = current == null ? -1 : Array.IndexOf(_instances, current);
            if (SelectedSlot != slot)
            {
                SelectedSlot = slot;
                _idleTime = 0;
            }
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]?.visualRoot != null && slots[i].visualRoot.gameObject.activeSelf != (i == slot))
                    slots[i].visualRoot.gameObject.SetActive(i == slot);
            if (current == null || slot < 0)
            {
                _usesLeftReloadTarget = false;
                _usesLeftPose = _usesRightPose = false;
                weaponPose.profile = null;
                weaponPose.useLookController = false;
                weaponPose.SetAiming(false);
                handIK.leftHand.target = handIK.rightHand.target = null;
                handIK.leftHand.hint = handIK.rightHand.hint = null;
                handIK.leftHand.weight = handIK.rightHand.weight = 0f;
                spine.SetEquipmentAnimation(null, null, 0, null, null, 0, 0f);
                return;
            }
            EquipmentDefinition definition = current.Definition;
            EquipmentSceneSlot view = slots[slot];
            weaponPose.profile = definition.weaponPoses;
            weaponPose.useLookController = false;
            weaponPose.SetAiming(Runtime.CanAim && look != null && look.IsAiming);
            EquipmentActionState action = current.Action;
            EquipmentActionDefinition recipe = action.IsActive ? action.Definition : null;
            EquipmentHandSample hands = recipe != null ? recipe.SampleHands(action.Progress)
                : new EquipmentHandSample { leftGrip = 1f, rightGrip = 1f, leftIK = 1f, rightIK = 1f };
            _usesLeftPose = _leftPose.Prepare(view.leftHandPose, handIK.leftHand.upperArm, handIK.rightHand.upperArm);
            _usesRightPose = _rightPose.Prepare(view.rightHandPose, handIK.leftHand.upperArm, handIK.rightHand.upperArm);
            _usesLeftReloadTarget = !_usesLeftPose && action.IsActive && action.Kind == EquipmentActionKind.Reload &&
                _leftReload.Prepare(weaponPose.PoseRoot, view.leftGrip, view.leftReloadTarget,
                    handIK.leftHand.upperArm, handIK.rightHand.upperArm);
            handIK.leftHand.target = _usesLeftReloadTarget ? _leftReload.Target : view.leftGrip;
            handIK.rightHand.target = view.rightGrip;
            handIK.leftHand.hint = view.leftHint; handIK.rightHand.hint = view.rightHint;
            handIK.leftHand.weight = view.leftGrip != null
                ? definition.leftGripWeight * hands.leftIK * (_usesLeftReloadTarget ? 1f : hands.leftGrip) : 0f;
            handIK.rightHand.weight = view.rightGrip != null ? definition.rightGripWeight * hands.rightGrip * hands.rightIK : 0f;
            if (_usesLeftPose)
            {
                handIK.leftHand.target = _leftPose.Target; handIK.leftHand.hint = view.leftHandPose.elbowHint;
                handIK.leftHand.weight = EquipmentNumbers.Progress(view.leftHandPose.weight) * hands.leftIK;
            }
            if (_usesRightPose)
            {
                handIK.rightHand.target = _rightPose.Target; handIK.rightHand.hint = view.rightHandPose.elbowHint;
                handIK.rightHand.weight = EquipmentNumbers.Progress(view.rightHandPose.weight) * hands.rightIK;
            }
            _idleTime += dt;
            AnimationClip pose = definition.characterPose;
            double poseTime = pose != null && pose.length > 0f ? _idleTime % pose.length : 0;
            float weight = ActionWeight(recipe, action.Progress);
            spine.SetEquipmentAnimation(pose, definition.characterMask, poseTime, recipe != null ? recipe.characterClip : null,
                recipe != null && recipe.characterMask != null ? recipe.characterMask : definition.characterMask,
                recipe != null && recipe.characterClip != null ? action.Progress * recipe.characterClip.length : 0, weight,
                _usesLeftPose && FullyOwned(handIK.leftHand), _usesRightPose && FullyOwned(handIK.rightHand));
        }

        // Partial/zero IK needs the authored FK arm as its blend source. Only full ownership may
        // remove arm muscles from the character clip (fingers always remain in that clip).
        private bool FullyOwned(HandIKChain chain) => handIK.isActiveAndEnabled && handIK.alpha >= 1f &&
            chain.weight >= 1f && chain.positionWeight >= 1f && chain.rotationWeight >= 1f;

        public static float ActionWeight(EquipmentActionDefinition action, float progress)
        {
            if (action == null) return 0f;
            float duration = EquipmentNumbers.NonNegative(action.duration);
            float time = EquipmentNumbers.Progress(progress) * duration;
            float incoming = action.blendIn > 0f ? Mathf.Clamp01(time / action.blendIn) : 1f;
            float outgoing = action.blendOut > 0f ? Mathf.Clamp01((duration - time) / action.blendOut) : 1f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(incoming, outgoing));
        }

        // Called separately at 575, after Idle/Aim 550 and before hand IK 600.
        public void ApplyMotion()
        {
            RemoveMotion();
            ApplyActionMotion();
            ApplyHandPoses();
        }

        private void ApplyHandPoses()
        {
            if (Runtime?.Current?.Definition is MeleeDefinition melee && melee.isUnarmed && melee.followCamera && handIK != null)
                _cameraArms.Apply(spine, look != null ? look.cameraRig : null,
                    _usesLeftPose && FullyOwned(handIK.leftHand) ? null : handIK.leftHand.upperArm,
                    _usesRightPose && FullyOwned(handIK.rightHand) ? null : handIK.rightHand.upperArm);
            var action = Runtime?.Current?.Action;
            var recipe = action != null && action.IsActive ? action.Definition : null;
            float progress = action != null ? action.Progress : 0f;
            float weight = ActionWeight(recipe, progress);
            if (_usesLeftPose) _leftPose.Evaluate(recipe?.leftHandMotion, progress, weight);
            if (_usesRightPose) _rightPose.Evaluate(recipe?.rightHandMotion, progress, weight);
        }

        private void ApplyActionMotion()
        {
            EquipmentActionState action = Runtime?.Current?.Action;
            if (weaponPose == null || action == null || !action.IsActive || action.Definition == null)
            {
                _weaponClip.Clear();
                return;
            }
            Transform root = weaponPose.PoseRoot;
            if (!weaponPose.TryValidateRoot(out _)) { _weaponClip.Clear(); return; }
            _effectRoot = root;
            _beforePosition = root.localPosition; _beforeRotation = root.localRotation;
            EquipmentSceneSlot view = SelectedSlot >= 0 && SelectedSlot < slots.Length ? slots[SelectedSlot] : null;
            _weaponClip.Sample(view?.visualRoot, action.Definition.weaponClip, action.Progress,
                view != null && view.blendWeaponClip ? ActionWeight(action.Definition, action.Progress) : 1f);
            if (action.Definition.followAnimatedRightHand && view?.rightGrip != null && handIK.rightHand.hand != null)
            {
                // Read the animated wrist BEFORE final hand IK. The weapon remains on its independent holder.
                Quaternion gripInRoot = Quaternion.Inverse(root.rotation) * view.rightGrip.rotation;
                Quaternion wristOffset = Quaternion.Euler(handIK.rightHand.rotationOffsetEuler);
                Quaternion gripRotation = handIK.rightHand.hand.rotation * Quaternion.Inverse(wristOffset);
                Quaternion desiredRotation = gripRotation * Quaternion.Inverse(gripInRoot);
                Vector3 gripOffset = desiredRotation * Quaternion.Inverse(root.rotation) * (view.rightGrip.position - root.position);
                Vector3 wristPositionOffset = desiredRotation * Quaternion.Inverse(root.rotation) *
                    view.rightGrip.TransformVector(handIK.rightHand.positionOffset);
                Vector3 desiredPosition = handIK.rightHand.hand.position - gripOffset - wristPositionOffset;
                float blend = ActionWeight(action.Definition, action.Progress);
                root.SetPositionAndRotation(Vector3.Lerp(root.position, desiredPosition, blend),
                    Quaternion.Slerp(root.rotation, desiredRotation, blend));
            }
            Vector3 position = action.Definition.weaponPosition?.Evaluate(action.Progress) ?? Vector3.zero;
            Vector3 rotation = action.Definition.weaponRotation?.Evaluate(action.Progress) ?? Vector3.zero;
            _afterPosition = root.localPosition + position;
            _afterRotation = root.localRotation * Quaternion.Euler(rotation);
            root.SetLocalPositionAndRotation(_afterPosition, _afterRotation);
            _effectApplied = true;
            if (_usesLeftReloadTarget && view != null)
                _leftReload.Evaluate(view.leftGrip, action.Definition.leftHandTargetMotion,
                    action.Progress, action.Definition.SampleHands(action.Progress).leftGrip);
        }

        private void RemoveMotion()
        {
            _cameraArms.Restore();
            if (_effectApplied && _effectRoot != null && Vector3.Distance(_effectRoot.localPosition, _afterPosition) < 0.00001f &&
                Quaternion.Angle(_effectRoot.localRotation, _afterRotation) < 0.01f)
                _effectRoot.SetLocalPositionAndRotation(_beforePosition, _beforeRotation);
            _effectApplied = false;
        }

        private void ReadInput()
        {
            bool locked = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            bool allowed = readInput && locked && _inputArmed && (look == null || look.inputEnabled);
            _inputArmed = locked;
            if (!allowed) { _burstRemaining = 0; _queuedReload = false; return; }
            bool first = false, second = false, third = false, pressed = false, held = false, reload = false, cycle = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                first = keyboard.digit1Key.wasPressedThisFrame; second = keyboard.digit2Key.wasPressedThisFrame;
                third = keyboard.digit3Key.wasPressedThisFrame;
                reload = keyboard.rKey.wasPressedThisFrame; cycle = keyboard.cKey.wasPressedThisFrame;
            }
            if (mouse != null) { pressed = mouse.leftButton.wasPressedThisFrame; held = mouse.leftButton.isPressed; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            first = Input.GetKeyDown(KeyCode.Alpha1); second = Input.GetKeyDown(KeyCode.Alpha2);
            third = Input.GetKeyDown(KeyCode.Alpha3);
            reload = Input.GetKeyDown(KeyCode.R); cycle = Input.GetKeyDown(KeyCode.C);
            pressed = Input.GetMouseButtonDown(0); held = Input.GetMouseButton(0);
#endif
            if (first) SelectSlot(0);
            if (second) SelectSlot(1);
            if (third) SelectSlot(2);
            if (reload && enableReloadInput && !(Runtime.Current != null && Runtime.Current.IsBusy &&
                Runtime.Current.Action.Kind == EquipmentActionKind.Reload)) { _queuedReload = true; _burstRemaining = 0; }
            if (_queuedReload && Runtime.Phase == EquipmentSwitchPhase.Ready && Runtime.Current != null && !Runtime.Current.IsBusy)
            {
                Runtime.TryReload();
                _queuedReload = false;
            }
            if (cycle) Runtime.TryCycle();
            if (Runtime.Current?.Definition is FirearmDefinition firearm)
            {
                if (firearm.fireMode == FireMode.Burst && pressed && Runtime.IsReady)
                    _burstRemaining = Mathf.Max(1, firearm.burstSize);
                bool request = firearm.fireMode == FireMode.Automatic ? held
                    : firearm.fireMode == FireMode.Burst ? _burstRemaining > 0 : pressed;
                if (!_queuedReload && request && Runtime.TryFire() && _burstRemaining > 0) _burstRemaining--;
            }
            else if (pressed) Runtime.TryMeleeAttack();
        }

        private void OnGUI()
        {
            if (!showControls || Runtime?.Current == null) return;
            EquipmentInstance item = Runtime.Current;
            string ammo = item.Definition is FirearmDefinition
                ? $" | {item.TotalLoadedRounds}/{item.ReserveRounds}" : "";
            string status = item.Action.IsActive && item.Action.Kind == EquipmentActionKind.Reload
                ? $" | Recargando {item.Action.Progress * 100f:F0}%" : "";
            GUI.Box(new Rect(12, 12, 570, 78), $"{item.Definition.displayName}{ammo}{status}\n" +
                "1: AK12   2: Puños   3: Cuchillo   Click: disparo/golpe   RMB: Aim   R: recargar\n" +
                "Prueba de animación/munición, sin daño. Esc: liberar cursor");
        }
    }
}
