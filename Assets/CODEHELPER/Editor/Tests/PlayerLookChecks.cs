#if UNITY_EDITOR
using System;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerLookChecks
{
    [MenuItem("Tools/CodeHelper/Run All Character Checks")]
    public static void RunAll()
    {
        ProceduralAnimationChecks.Run();
        HandIKChecks.Run();
        WeaponPoseChecks.Run();
        CameraAimChecks.Run();
        EquipmentChecks.Run();
        Run();
    }

    [MenuItem("Tools/CodeHelper/Run Player Look Checks")]
    public static void Run()
    {
        GameObject root = new GameObject("PlayerLook checks (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);
        SpineLookProfile profile = ScriptableObject.CreateInstance<SpineLookProfile>();
        try
        {
            root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            Transform model = Child(root.transform, "Character");
            Transform spineBone = Child(model, "Spine");
            Transform head = Child(spineBone, "Head");
            head.localPosition = Vector3.up;
            Transform holder = Child(head, "CameraHolder");
            Transform camera = Child(holder, "MainCamera");
            camera.gameObject.AddComponent<Camera>();

            ProceduralSpine spine = root.AddComponent<ProceduralSpine>();
            spine.animationRoot = root.transform;
            spine.profile = profile;
            spine.inputSmoothTime = 0f;
            spine.stabilizationWeight = 0f;
            profile.pitchElements.Add(new SpineLookElement { customBone = "Spine" });
            Require((bool)Invoke(spine, "ResolveRuntimeBones"), "resolve spine");
            Invoke(spine, "CaptureBindPose");
            typeof(ProceduralSpine).GetField("_ready", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spine, true);

            CameraRigController rig = holder.gameObject.AddComponent<CameraRigController>();
            rig.characterRoot = root.transform;
            rig.headBone = head;
            rig.cameraHolder = holder;
            rig.realCamera = camera;
            PlayerLookController look = root.AddComponent<PlayerLookController>();
            look.inputMode = PlayerLookInputMode.External;
            look.lockCursorOnEnable = false;
            look.mouseSensitivity = 1f;
            look.inputSmoothness = 0f;
            look.blockBodyRotation = true;
            Invoke(look, "Awake");
            look.ConnectCamera();
            Require(rig.lookController == look, "camera source connected");
            Require(!rig.yawRelativeToRoot && !rig.useSpineLean, "no double yaw or converted lean");
            Near(look.CameraYaw, 35f, "initial heading from Player, not animated head");

            Invoke(look, "ProcessLookFrame", new Vector2(10f, 20f), 1f, true, 1f / 60f);
            Near(look.CameraYaw, 45f, "mouse horizontal delta");
            Near(look.CameraPitch, 20f, "mouse vertical delta positive up");
            Near(root.transform.eulerAngles.y, 35f, "blockBodyRotation respected");
            Near(spine.inputVertical, -20f, "spine pitch axis conversion");
            Near(spine.inputHorizontal, 10f, "relative spine yaw, not world yaw");
            Near(spine.inputTurnOffset, 10f, "turn-offset yaw also supplied");
            Near(spine.inputLean, -look.CameraLean, "spine lean axis conversion");
            Require(look.IsAiming, "aim state published");
            Invoke(spine, "LateUpdate");
            Require(spineBone.forward.y > 0f, "body looks UP with positive camera pitch");
            Invoke(rig, "LateUpdate");
            Same(camera.rotation, Quaternion.Euler(-20f, 45f, -look.CameraLean), "camera consumes interface without doubling root yaw");

            look.isTPS = true;
            Require(((ICameraLookSource)look).IsThirdPerson, "TPS switch reaches camera interface");
            look.isTPS = false;
            look.overrideSpineControl = true;
            look.externalSpinePitch = 12f;
            look.externalSpineYaw = -15f;
            look.isTPS = true;
            Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
            Near(spine.inputVertical, -12f, "TPS override pitch converted");
            Near(spine.inputHorizontal, -15f, "TPS override uses relative yaw");
            look.overrideSpineControl = false;
            look.isTPS = false;

            look.SetLookAngles(0f, 179f);
            Invoke(look, "ProcessLookFrame", new Vector2(5f, 0f), 0f, false, 1f / 60f);
            Near(look.CameraYaw, -176f, "yaw wraps across 180");
            look.SetLookAngles(0f, 0f);
            Invoke(look, "ProcessLookFrame", new Vector2(10f, 0f), 0f, false, 1f / 30f);
            float yawAt30 = look.CameraYaw;
            look.SetLookAngles(0f, 0f);
            Invoke(look, "ProcessLookFrame", new Vector2(10f, 0f), 0f, false, 1f / 120f);
            Near(look.CameraYaw, yawAt30, "mouse pixels not multiplied by deltaTime");

            look.SetLookAngles(500f, 0f);
            Near(look.CameraPitch, 90f, "pitch limit");
            look.AddRecoilInput(20f, 5f);
            Near(look.CameraPitch, 70f, "legacy recoil vertical sign retained");
            Near(look.CameraYaw, 5f, "recoil yaw");
            look.blockBodyRotation = false;
            root.transform.rotation = Quaternion.identity;
            look.SetLookAngles(0f, 160f);
            Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
            Require(Mathf.Abs(look.BodyYawOffset) <= look.hardLimitAngle + 0.01f, "hard turn limit enforced");

            foreach (float limit in new[] { 0f, 50f })
            {
                root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                look.softLimitAngle = look.hardLimitAngle = limit;
                look.SetLookAngles(0f, 0f);
                Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
                Require(Mathf.Abs(look.BodyYawOffset) <= limit + 0.01f,
                    "equal/zero limits remain enforced after interpolation");
            }

            look.SetInput(new Vector2(7f, 2f), -1f, false);
            Invoke(look, "Update");
            Vector2 pending = (Vector2)typeof(PlayerLookController).GetField("_externalLook",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(look);
            Require(pending == Vector2.zero, "external delta consumed once");
            CheckTurnInPlaceIsolation();
            CheckStabilizedHeadFollowing();
            Debug.Log("[PlayerLookChecks] Passed: camera interface, initial yaw, pixel input, wrap/clamp, " +
                      "relative body yaw, pitch/lean signs, body hard limit, TPS override, recoil, external input consumption " +
                      "camera position/rotation isolation, and stabilized-head pitch/posture following. " +
                      "Physical mouse, cursor focus and real Animator transitions still need Play Mode verification.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
        }
    }

    private static void CheckTurnInPlaceIsolation()
    {
        GameObject root = new GameObject("Turn-in-place isolation checks (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);
        try
        {
            Transform model = Child(root.transform, "Character");
            Transform head = Child(model, "Head");
            head.localPosition = new Vector3(0.08f, 1.65f, 0.25f); // Deliberate off-axis eye position.
            Transform holder = Child(head, "CameraHolder");
            Transform camera = Child(holder, "MainCamera");
            camera.gameObject.AddComponent<Camera>();
            CameraRigController rig = holder.gameObject.AddComponent<CameraRigController>();
            rig.characterRoot = root.transform;
            rig.headBone = head;
            rig.cameraHolder = holder;
            rig.realCamera = camera;
            rig.positionSource = CameraPositionSource.PlayerRoot;
            rig.captureHeadPosition = true;
            rig.smoothHeadPosition = true; // Must NOT delay actual PlayerRoot translation in this mode.
            PlayerLookController look = root.AddComponent<PlayerLookController>();
            look.inputMode = PlayerLookInputMode.External;
            look.lockCursorOnEnable = false;
            look.inputSmoothness = 0f;
            Invoke(look, "Awake");
            look.ConnectCamera();
            look.SetLookAngles(20f, 120f);
            Invoke(rig, "LateUpdate"); // Calibrate BEFORE body turn.
            Vector3 initialPosition = camera.position;
            Quaternion initialRotation = camera.rotation;

            for (int i = 0; i < 120; i++)
            {
                Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
                // Simulate a turn animation that bobs/translates/rotates the head in addition to root yaw.
                head.localPosition = new Vector3(Mathf.Sin(i * 0.2f) * 0.2f,
                    1.65f + Mathf.Cos(i * 0.3f) * 0.1f, 0.25f + Mathf.Sin(i * 0.4f) * 0.15f);
                head.localRotation = Quaternion.Euler(i * 3f, i * 7f, i * 2f);
                Invoke(rig, "LateUpdate");
                NearPosition(camera.position, initialPosition, "turn/head animation cannot translate stable camera");
                Same(camera.rotation, initialRotation, "body catch-up cannot rotate stable camera");
            }
            Require(Mathf.Abs(root.transform.eulerAngles.y) > 1f, "body actually turned during stability check");
            // Edit-mode checks explicitly exercise the enable callback (objects are kept inactive
            // to avoid unrelated game lifecycle callbacks). Re-enabling must NOT recalibrate.
            Invoke(rig, "OnEnable");
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, initialPosition, "enable callback retains calibrated eye position");
            Same(camera.rotation, initialRotation, "enable callback retains view direction");
            look.isTPS = true;
            rig.collisionLayers = 0;
            Invoke(rig, "LateUpdate");
            Vector3 thirdPersonPosition = camera.position;
            root.transform.rotation = Quaternion.Euler(0f, -80f, 0f);
            head.localPosition += Vector3.one * 0.3f;
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, thirdPersonPosition, "TPS orbit does not shift with body turn");
            Same(camera.rotation, initialRotation, "TPS rotation independent of body");

            Vector3 displacement = new Vector3(2f, 0.5f, -3f);
            root.transform.position += displacement;
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, thirdPersonPosition + displacement, "real player movement followed immediately");
            rig.ResetPositionSmoothing(); // Teleport reset must not recapture animated eye offset.
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, thirdPersonPosition + displacement, "filter reset does not recapture body motion");

            look.isTPS = false;
            look.SetLookAngles(-15f, -40f);
            Invoke(rig, "LateUpdate");
            Same(camera.rotation, Quaternion.Euler(15f, -40f, 0f), "free look remains immediate after body turns");
            NearPosition(camera.position, initialPosition + displacement, "mouse look does not orbit eye anchor");

            rig.captureHeadPosition = false;
            rig.rootAnchorOffset = new Vector3(0f, 1.1f, 0f);
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, root.transform.position + rig.rootAnchorOffset, "explicit stance height");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void NearPosition(Vector3 actual, Vector3 expected, string label) =>
        Require(Vector3.Distance(actual, expected) < 0.0001f, label);

    private static void CheckStabilizedHeadFollowing()
    {
        GameObject root = new GameObject("Stabilized-head checks (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);
        SpineLookProfile profile = ScriptableObject.CreateInstance<SpineLookProfile>();
        GameObject tiltHost = null;
        try
        {
            Transform model = Child(root.transform, "Character");
            Transform hips = Child(model, "Hips");
            hips.localPosition = Vector3.up;
            Transform spineBone = Child(hips, "Spine");
            spineBone.localPosition = new Vector3(0.02f, 0.2f, 0.04f);
            Transform chest = Child(spineBone, "Chest");
            chest.localPosition = new Vector3(-0.01f, 0.22f, 0.03f);
            Transform neck = Child(chest, "Neck");
            neck.localPosition = new Vector3(0f, 0.15f, 0f);
            Transform head = Child(neck, "Head");
            head.localPosition = new Vector3(0f, 0.1f, 0.08f);
            Transform holder = Child(head, "CameraHolder");
            Transform camera = Child(holder, "Camera");
            camera.gameObject.AddComponent<Camera>();

            foreach (string name in new[] { "Spine", "Chest", "Neck", "Head" })
                profile.pitchElements.Add(new SpineLookElement
                    { customBone = name, clampedAngle = new Vector2(22.5f, 22.5f) });
            profile.yawElements.Add(new SpineLookElement { customBone = "Spine", clampedAngle = new Vector2(30f, 30f) });
            profile.yawElements.Add(new SpineLookElement { customBone = "Chest", clampedAngle = new Vector2(60f, 60f) });
            profile.rollElements.Add(new SpineLookElement { customBone = "Chest", clampedAngle = new Vector2(30f, 30f) });
            ProceduralSpine spine = root.AddComponent<ProceduralSpine>();
            spine.animationRoot = root.transform;
            spine.profile = profile;
            Require((bool)Invoke(spine, "ResolveRuntimeBones"), "stabilized-head spine setup");
            Invoke(spine, "CaptureBindPose");
            typeof(ProceduralSpine).GetField("_ready", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spine, true);

            CameraRigController rig = holder.gameObject.AddComponent<CameraRigController>();
            rig.characterRoot = root.transform;
            rig.headBone = head;
            rig.cameraHolder = holder;
            rig.realCamera = camera;
            rig.positionSource = CameraPositionSource.HeadStabilized;
            typeof(CameraRigController).GetField("proceduralSpine", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rig, spine);
            Invoke(rig, "Awake");
            PlayerLookController look = root.AddComponent<PlayerLookController>();
            look.inputMode = PlayerLookInputMode.External;
            look.lockCursorOnEnable = false;
            look.inputSmoothness = 0f;
            look.blockBodyRotation = true;
            Invoke(look, "Awake");
            look.ConnectCamera();
            StepLookSpineCamera(look, spine, rig);
            NearPosition(camera.position, head.position, "neutral stabilized camera matches head");
            Vector3 neutral = camera.position;
            foreach (float pitch in new[] { 50f, -50f })
            {
                look.SetLookAngles(pitch, 0f);
                StepLookSpineCamera(look, spine, rig);
                NearPosition(camera.position, head.position, "stabilized camera follows head when looking up/down");
                Require(Vector3.Distance(camera.position, neutral) > 0.05f,
                    "stabilized head is not a fixed PlayerRoot anchor");
            }

            look.SetLookAngles(35f, 70f);
            StepLookSpineCamera(look, spine, rig);
            Vector3 fixedPitchPosition = camera.position;
            Quaternion fixedView = camera.rotation;
            look.blockBodyRotation = false;
            look.softLimitAngle = 20f;
            for (int i = 0; i < 120; i++)
            {
                StepLookSpineCamera(look, spine, rig);
                NearPosition(camera.position, fixedPitchPosition, "body turn isolated while retaining head pitch");
                Same(camera.rotation, fixedView, "stabilized view does not rotate with body catch-up");
            }
            Require(Mathf.Abs(root.transform.eulerAngles.y) > 1f, "body turned with stabilized head");
            Invoke(rig, "OnEnable");
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, fixedPitchPosition, "stabilized-head calibration persists on enable");

            look.blockBodyRotation = true;
            root.transform.rotation = Quaternion.Euler(0f, 70f, 0f);
            look.SetLookAngles(-25f, 70f);
            StepLookSpineCamera(look, spine, rig);
            NearPosition(camera.position, head.position, "head pitch following after turn completion");
            Vector3 standing = camera.position;
            hips.localPosition -= Vector3.up * 0.3f;
            StepLookSpineCamera(look, spine, rig);
            NearPosition(camera.position, standing - Vector3.up * 0.3f, "animated posture height still followed");
            Vector3 displacement = new Vector3(2f, 0.5f, -1f);
            root.transform.position += displacement;
            StepLookSpineCamera(look, spine, rig);
            NearPosition(camera.position, standing - Vector3.up * 0.3f + displacement,
                "stabilized-head follows real player translation");

            // Unmapped animated hips must carry posture, not the body's changing heading.
            Invoke(spine, "Update");
            hips.localRotation = Quaternion.Euler(20f, 0f, 8f);
            spine.stabilizationWeight = 0f;
            CheckTiltedCatchUp(root.transform, look, spine, rig, camera, "animated tilted hips");

            // Exercise the same separation with actual procedural pelvis compensation and roll.
            tiltHost = new GameObject("Active pelvis driver (temporary)");
            tiltHost.hideFlags = HideFlags.HideAndDontSave;
            PelvisTilt tilt = tiltHost.AddComponent<PelvisTilt>();
            tilt.pelvisBone = hips;
            tilt.leftThigh = Child(hips, "LeftThigh");
            tilt.rightThigh = Child(hips, "RightThigh");
            tilt.maxAngle = 15f;
            spine.pelvisTilt = tilt;
            CheckTiltedCatchUp(root.transform, look, spine, rig, camera, "pelvis tilt");
            CheckTiltedCatchUp(root.transform, look, spine, rig, camera, "pelvis tilt and procedural roll", 20f);

            // Rebuild references without evaluating the camera during the not-ready interval.
            Invoke(spine, "Update");
            tilt.Restore();
            int generation = spine.ReferenceGeneration;
            chest.localRotation = Quaternion.Euler(14f, 7f, 5f);
            Invoke(spine, "CaptureBindPose");
            Require(spine.ReferenceGeneration != generation, "reference rebuild increments generation");
            Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
            Invoke(spine, "LateUpdate");
            var freshAnchor = new HeadPositionStabilizer();
            Require(freshAnchor.TryGetPosition(root.transform, head, spine, look.CameraYaw, out Vector3 freshPosition),
                "fresh reference calibration");
            Invoke(rig, "OnEnable");
            Invoke(rig, "LateUpdate");
            NearPosition(camera.position, freshPosition, "rebuild missed while disabled refreshes camera calibration");
        }
        finally
        {
            if (tiltHost != null) Object.DestroyImmediate(tiltHost);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
        }
    }

    private static void CheckTiltedCatchUp(Transform root, PlayerLookController look, ProceduralSpine spine,
        CameraRigController rig, Transform camera, string label, float spineLean = 0f)
    {
        Vector3 expected = Vector3.zero;
        look.blockBodyRotation = true;
        look.SetLookAngles(35f, 70f);
        for (int i = 0; i <= 70; i++)
        {
            Invoke(spine, "Update");
            root.rotation = Quaternion.Euler(0f, i, 0f);
            Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
            spine.inputLean = spineLean;
            Invoke(spine, "LateUpdate");
            Invoke(rig, "LateUpdate");
            if (i == 0) expected = camera.position;
            else NearPosition(camera.position, expected, label + " does not orbit during catch-up");
        }
    }

    private static void StepLookSpineCamera(PlayerLookController look, ProceduralSpine spine, CameraRigController rig)
    {
        Invoke(look, "ProcessLookFrame", Vector2.zero, 0f, false, 1f / 60f);
        Invoke(spine, "Update");
        Invoke(spine, "LateUpdate");
        Invoke(rig, "LateUpdate");
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
    private static object Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Near(float actual, float expected, string label) => Require(Mathf.Abs(actual - expected) < 0.001f, label);
    private static void Same(Quaternion actual, Quaternion expected, string label) => Require(Quaternion.Angle(actual, expected) < 0.1f, label);
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[PlayerLookChecks] Failed: " + label);
    }
}
#endif
