#if UNITY_EDITOR
using System;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CameraAimChecks
{
    [MenuItem("Tools/CodeHelper/Run Camera Aim Checks")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("Camera Aim checks (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        WeaponPoseProfile profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        try
        {
            profile.StorePose(WeaponPoseState.Idle, new WeaponPose { localPosition = new Vector3(0.03f, -0.1f, 0.4f) });
            profile.StorePose(WeaponPoseState.Aim, new WeaponPose { localPosition = new Vector3(0f, -0.05f, 0.3f) });
            profile.transitionDuration = 0f;
            profile.aimCamera.enabled = true;
            string[] args = Environment.GetCommandLineArgs();
            int fixture = Array.IndexOf(args, "-weaponPoseFixture");
            if (fixture >= 0 && fixture + 1 < args.Length)
            {
                WeaponPoseChecks.LoadProfileFixture(profile, args[fixture + 1]);
                Require(profile.aimCamera != null && profile.aimCamera.enabled, "AK12 snapshot enables Aim camera");
                MeasureConfiguredCamera(profile);
            }
            // General API regressions use known camera defaults, independently of the user's FOV/TPS choices.
            profile.aimCamera = new WeaponAimCameraSettings { enabled = true };
            profile.synchronizeAimEffects = false;
            // Camera tests isolate their channel from weapon bounce; neither preset is modified on disk.
            profile.aimMotion.enabled = false;
            profile.transitionDuration = 0f;
            Transform holder = Child(root.transform, "CameraHolder");
            Transform camera = Child(holder, "Camera");
            Camera cameraComponent = camera.gameObject.AddComponent<Camera>();
            cameraComponent.fieldOfView = 60f;
            Transform weapon = Child(holder, "WeaponHolder");
            Transform grip = Child(weapon, "Grip");
            grip.localPosition = new Vector3(0.01f, -0.08f, -0.22f);
            Vector3 gripLocal = grip.localPosition;
            var source = weapon.gameObject.AddComponent<WeaponPoseController>();
            source.profile = profile;
            var look = root.AddComponent<PlayerLookController>();
            look.lockCursorOnEnable = false;
            look.SetLookAngles(15f, 50f);
            source.lookController = look;
            var rig = holder.gameObject.AddComponent<CameraRigController>();
            rig.realCamera = camera;
            rig.cameraHolder = holder;
            rig.characterRoot = root.transform;
            rig.positionSource = CameraPositionSource.PlayerRoot;
            rig.captureHeadPosition = false;
            rig.lookController = look;
            rig.useSpineLean = false;
            rig.aimEffectsSource = source;
            rig.tpsOffset = Vector3.zero;
            root.SetActive(true); // Exercise the same active-source guard used by real camera evaluation.
            look.SetLookAngles(15f, 50f);
            Require(rig.ValidateSetup(), "camera Aim setup");
            Tick(look, source, rig, false, 1f / 120f);
            Near(cameraComponent.fieldOfView, 60f, "Idle base FOV");
            Vector3 anchor = camera.position;
            Quaternion view = rig.ViewRotation;
            float peak = 0f;
            for (int i = 0; i < 240; i++)
            {
                Tick(look, source, rig, true, 1f / 120f);
                peak = Mathf.Max(peak, rig.AimCameraRotationOffset.magnitude);
                Require(Vector3.Distance(camera.position, anchor) < 0.0001f, "camera Aim never translates camera");
                Same(holder.rotation, view, "camera shake does not rotate holder/weapon anchor");
                Same(rig.ViewRotation, view, "camera shake never modifies view input");
            }
            Near(cameraComponent.fieldOfView, 63f, "Aim widens FOV from 60 to 63");
            Require(peak > 0.03f && peak <= profile.aimCamera.maxRotationOffset + 0.0001f, "brief bounded camera angular shake");
            Same(camera.rotation, view, "held Aim settles without continuous shaking");
            Near(grip.localPosition.magnitude, gripLocal.magnitude, "camera effect does not edit grips");
            profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose aim);
            Require(Vector3.Distance(weapon.localPosition, aim.localPosition) < 0.0001f, "camera effect does not displace weapon pose");

            // The existing base-FOV API and generic additive effects remain independent.
            rig.UpdateTargetFOV(70f);
            Quaternion additive = Quaternion.Euler(0f, 0f, 1f);
            rig.SetAdditivePose(Vector3.zero, additive);
            rig.AddCameraRecoil(new Vector3(0.4f, 0f, 0f));
            for (int i = 0; i < 240; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 73f, "Aim adds to base FOV without replacing it");
            Same(camera.rotation, view * additive, "generic additive rotation is preserved");
            for (int i = 0; i < 240; i++) Tick(look, source, rig, false, 1f / 120f);
            Near(cameraComponent.fieldOfView, 70f, "leaving Aim restores current base FOV");
            for (int i = 0; i < 300; i++) Tick(look, source, rig, (i & 1) == 0, 1f / 120f);
            for (int i = 0; i < 360; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 73f, "rapid toggles do not accumulate FOV");
            Same(camera.rotation, view * additive, "rapid toggles settle camera rotation");

            profile.aimCamera.aimFovOffset = -5f;
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 65f, "negative FOV offset supports zoom");
            profile.aimCamera.aimFovOffset = 3f;
            source.enabled = false;
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 70f, "disabled weapon source releases camera channel");
            source.enabled = true;
            weapon.gameObject.SetActive(false);
            Require(source.enabled && !source.isActiveAndEnabled, "inactive weapon keeps enabled flag but is not active");
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 70f, "deactivated weapon releases FOV overlay");
            Near(rig.AimCameraRotationOffset.magnitude, 0f, "deactivated weapon produces no camera impulse");
            weapon.gameObject.SetActive(true);
            look.isTPS = true;
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 70f, "camera Aim defaults to FPS only");
            profile.aimCamera.applyInThirdPerson = true;
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 73f, "optional TPS camera channel");
            look.isTPS = false;

            // Direct external FOV changes are treated as a new base, not overwritten by stale cleanup.
            cameraComponent.fieldOfView = 80f;
            Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 83f, "preserve external FOV changes");
            Tick(look, source, rig, false, 0.05f);
            Tick(look, source, rig, true, 0.016f);
            Invoke(rig, "OnDisable");
            Near(cameraComponent.fieldOfView, 80f, "rig disable removes only Aim FOV");
            Same(camera.rotation, view * additive, "rig disable removes only Aim rotation");

            Invoke(rig, "OnEnable");
            Tick(look, source, rig, true, 1f / 120f);
            Near(rig.AimCameraRotationOffset.magnitude, 0f, "no shake impulse on enable/spawn");
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            profile.aimCamera.enabled = false;
            for (int i = 0; i < 180; i++) Tick(look, source, rig, true, 1f / 120f);
            Near(cameraComponent.fieldOfView, 80f, "disabled profile releases FOV overlay");
            profile.aimCamera.enabled = true;
            profile.aimCamera.fovBlendSpeed = 0f;
            Tick(look, source, rig, true, 0f);
            Near(cameraComponent.fieldOfView, 83f, "zero blend speed applies target immediately");

            // Legacy layouts sharing real camera and holder may change FOV, but must not shake the holder.
            rig.realCamera = holder;
            Camera alias = holder.gameObject.AddComponent<Camera>();
            alias.fieldOfView = 60f;
            rig.ValidateSetup();
            Tick(look, source, rig, false, 0.016f);
            Tick(look, source, rig, true, 0.016f);
            Same(holder.rotation, view * additive, "shared camera holder receives no Aim shake");
            Near(rig.AimCameraRotationOffset.magnitude, 0f, "shared holder suppresses angular channel");
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[CameraAimChecks] Passed: FOV 60->63->60/base restoration, measured peak {0:F3} deg, zero translation, " +
                "stable holder/input, no FOV drift, additive/recoil coexistence, external FOV, rapid toggles, FPS/TPS, cleanup and camera alias safety. " +
                "Visual comfort and sight alignment still require Play Mode.", peak));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        CheckSynchronizedTiming();
    }

    private static void CheckSynchronizedTiming()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("Shared Aim timing (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        WeaponPoseProfile profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        try
        {
            WeaponPose idle = new WeaponPose { localPosition = new Vector3(0.03f, -0.1f, 0.4f) };
            WeaponPose aim = new WeaponPose { localPosition = new Vector3(0f, -0.05f, 0.3f) };
            profile.StorePose(WeaponPoseState.Idle, idle);
            profile.StorePose(WeaponPoseState.Aim, aim);
            profile.synchronizeAimEffects = true;
            profile.aimMotion.enabled = profile.aimCamera.enabled = true;
            profile.aimCamera.aimFovOffset = -10f;
            profile.aimCamera.aimRotationKick = new Vector3(-2f, 0.5f, 1f);
            profile.aimCamera.maxRotationOffset = 2f;
            Transform holder = Child(root.transform, "Holder");
            Transform camera = Child(holder, "Camera");
            Camera lens = camera.gameObject.AddComponent<Camera>();
            lens.fieldOfView = 60f;
            Transform weapon = Child(holder, "Weapon");
            var source = weapon.gameObject.AddComponent<WeaponPoseController>();
            source.profile = profile;
            var look = root.AddComponent<PlayerLookController>();
            look.lockCursorOnEnable = false;
            source.lookController = look;
            var rig = holder.gameObject.AddComponent<CameraRigController>();
            rig.realCamera = camera; rig.cameraHolder = holder; rig.characterRoot = root.transform;
            rig.positionSource = CameraPositionSource.PlayerRoot; rig.captureHeadPosition = false;
            rig.lookController = look; rig.useSpineLean = false; rig.aimEffectsSource = source;
            root.SetActive(true);
            look.SetLookAngles(0f, 0f);
            rig.ValidateSetup();
            weapon.localPosition = idle.localPosition;
            Tick(look, source, rig, false, 1f);
            Vector3 anchor = camera.position;
            foreach (float duration in new[] { 0f, 0.18f, 0.4f })
            {
                profile.transitionDuration = duration;
                foreach (float step in new[] { 1f / 30f, 1f / 60f, 1f / 144f })
                {
                    Tick(look, source, rig, false, 1f);
                    for (int i = 0; i <= Mathf.CeilToInt(duration / step); i++)
                    {
                        Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, true, 0f);
                        Invoke(source, "PreparePose", step);
                        float progress = source.TransitionProgress;
                        // Deliberately freeze the rig's dt: synced effects must use the SOURCE clock.
                        Invoke(rig, "ApplyCamera", 0f);
                        Invoke(source, "ApplyPreparedPose", step);
                        Near(source.TransitionProgress, progress, "LateUpdate does not advance shared clock twice");
                        Near(lens.fieldOfView, Mathf.Lerp(60f, 50f, source.TransitionBlend), "zoom uses exact same blend as weapon pose");
                        Require(Vector3.Distance(camera.position, anchor) < 0.0001f, "shared timing never moves camera position");
                    }
                    Near(source.TransitionProgress, 1f, "shared transition completed by deadline");
                    Require(source.ProceduralPositionOffset == Vector3.zero && source.ProceduralRotationOffset == Vector3.zero &&
                        rig.AimCameraRotationOffset == Vector3.zero, "all bounce channels end with pose, without residual tail");
                    Near(lens.fieldOfView, 50f, "zoom reaches endpoint with pose");
                    Require(Vector3.Distance(weapon.localPosition, aim.localPosition) < 0.0001f, "weapon reaches saved Aim with zoom");
                }
            }

            profile.transitionDuration = 0.2f;
            Tick(look, source, rig, false, 1f);
            Tick(look, source, rig, true, 0.07f);
            Vector3 displayedPosition = weapon.localPosition;
            Quaternion displayedRotation = weapon.localRotation;
            Quaternion displayedCamera = camera.rotation;
            float displayedFov = lens.fieldOfView;
            Tick(look, source, rig, false, 0f);
            Require(Vector3.Distance(weapon.localPosition, displayedPosition) < 0.0001f, "timed weapon reversal is continuous");
            Same(weapon.localRotation, displayedRotation, "timed angular weapon reversal is continuous");
            Same(camera.rotation, displayedCamera, "timed camera reversal is continuous");
            Near(lens.fieldOfView, displayedFov, "timed zoom reversal is continuous");
            for (int i = 0; i < 10; i++) Tick(look, source, rig, false, 0.025f);
            Near(lens.fieldOfView, 60f, "reversed zoom returns on its new deadline");
            Require(rig.AimCameraRotationOffset == Vector3.zero && source.ProceduralPositionOffset == Vector3.zero,
                "reversed effects end at zero");
            for (int i = 0; i < 100; i++) Tick(look, source, rig, (i & 1) == 0, 0.005f);
            for (int i = 0; i < 30; i++) Tick(look, source, rig, true, 0.01f);
            Near(lens.fieldOfView, 50f, "rapid timed reversals do not accumulate zoom");
            Require(rig.AimCameraRotationOffset == Vector3.zero && source.ProceduralRotationOffset == Vector3.zero,
                "rapid timed reversals finish cleanly");
            Tick(look, source, rig, false, 1f);
            Tick(look, source, rig, true, 0.04f);
            profile.aimMotion.enabled = false;
            Tick(look, source, rig, true, 0.01f);
            profile.aimMotion.enabled = true;
            Tick(look, source, rig, true, 0.01f);
            Require(source.ProceduralPositionOffset == Vector3.zero && source.ProceduralRotationOffset == Vector3.zero,
                "reenabling motion mid-transition does not resurrect a cleared kick");
            foreach (float cycles in new[] { 1f, 1.25f, 1.5f, 2.7f })
            {
                Vector3 end = AimTransitionMath.Motion(Vector3.zero, Vector3.right, 1f, cycles, 0.15f, 2f);
                Vector3 nearEnd = AimTransitionMath.Motion(Vector3.zero, Vector3.right, 0.999f, cycles, 0.15f, 2f);
                Require(end == Vector3.zero && nearEnd.magnitude / 0.001f < 0.05f,
                    "timed wave approaches zero velocity for fractional oscillations");
            }
            Debug.Log("[CameraAimChecks] Shared timing passed: 0/0.18/0.4 s at 30/60/144 Hz, one source clock, " +
                "exact FOV/weapon endpoints, zero bounce at deadline, continuous reversals and no camera translation.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void MeasureConfiguredCamera(WeaponPoseProfile profile)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("Configured camera bounce (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        string originalProfile = JsonUtility.ToJson(profile);
        try
        {
            Transform holder = Child(root.transform, "Holder");
            Transform camera = Child(holder, "Camera");
            Camera lens = camera.gameObject.AddComponent<Camera>();
            lens.fieldOfView = 60f;
            Transform weapon = Child(holder, "Weapon");
            WeaponPoseController source = weapon.gameObject.AddComponent<WeaponPoseController>();
            source.profile = profile;
            PlayerLookController look = root.AddComponent<PlayerLookController>();
            look.lockCursorOnEnable = false;
            source.lookController = look;
            CameraRigController rig = holder.gameObject.AddComponent<CameraRigController>();
            rig.realCamera = camera;
            rig.cameraHolder = holder;
            rig.characterRoot = root.transform;
            rig.positionSource = CameraPositionSource.PlayerRoot;
            rig.captureHeadPosition = false;
            rig.lookController = look;
            rig.useSpineLean = false;
            rig.aimEffectsSource = source;
            root.SetActive(true);
            look.SetLookAngles(0f, 0f);
            Require(rig.ValidateSetup(), "configured camera setup");
            Tick(look, source, rig, false, 1f / 120f);
            Vector3 anchor = camera.position;
            Quaternion view = rig.ViewRotation;
            float aimPeak = 0f, idlePeak = 0f;
            for (int i = 0; i < 960; i++)
            {
                Tick(look, source, rig, true, 1f / 120f);
                aimPeak = Mathf.Max(aimPeak, rig.AimCameraRotationOffset.magnitude);
                Require(Vector3.Distance(camera.position, anchor) < 0.0001f, "diagnostic camera never translates");
                Same(holder.rotation, view, "diagnostic camera never rotates weapon holder");
            }
            bool diagnostic = profile.aimCamera.maxRotationOffset >= 1.9f && profile.aimCamera.damping <= 0.2f;
            Require(aimPeak > (diagnostic ? 1f : 0.01f), "configured camera bounce has measurable amplitude");
            Require(aimPeak <= profile.aimCamera.maxRotationOffset + 0.001f, "configured camera respects angle limit");
            Near(lens.fieldOfView, Mathf.Clamp(60f + profile.aimCamera.aimFovOffset, 1f, 179f), "configured Aim FOV preserved");
            Require(rig.AimCameraRotationOffset.magnitude < 0.001f, "configured camera bounce settles to zero");
            Same(camera.rotation, view, "configured camera returns to base rotation");
            for (int i = 0; i < 960; i++)
            {
                Tick(look, source, rig, false, 1f / 120f);
                idlePeak = Mathf.Max(idlePeak, rig.AimCameraRotationOffset.magnitude);
            }
            Near(lens.fieldOfView, 60f, "configured camera restores base FOV");
            Require(rig.AimCameraRotationOffset.magnitude < 0.001f, "configured exit bounce settles to zero");
            Require(JsonUtility.ToJson(profile) == originalProfile, "camera diagnostics never edit profile data");
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[CameraAimChecks] Actual profile: Aim bounce {0:F3} deg; Idle bounce {1:F3} deg; Aim FOV {2:F1}. No translation; settled to zero; saved settings unchanged.",
                aimPeak, idlePeak, Mathf.Clamp(60f + profile.aimCamera.aimFovOffset, 1f, 179f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform result = new GameObject(name).transform;
        result.SetParent(parent, false);
        return result;
    }
    private static void Tick(PlayerLookController look, WeaponPoseController source, CameraRigController rig, bool aiming, float dt)
    {
        Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, aiming, 0f);
        if (source.isActiveAndEnabled) Invoke(source, "PreparePose", dt); // Shared clock in Update, before camera LateUpdate.
        Invoke(rig, "ApplyCamera", dt); // Order 500, reads current input/profile, not last frame's weapon result.
        if (source.isActiveAndEnabled) Invoke(source, "ApplyPreparedPose", dt); // Order 550: no second clock advance.
    }
    private static void Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static void Near(float actual, float expected, string label) => Require(Mathf.Abs(actual - expected) < 0.01f, label);
    private static void Same(Quaternion actual, Quaternion expected, string label) => Require(Quaternion.Angle(actual, expected) < 0.1f, label);
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[CameraAimChecks] Failed: " + label);
    }
}
#endif
