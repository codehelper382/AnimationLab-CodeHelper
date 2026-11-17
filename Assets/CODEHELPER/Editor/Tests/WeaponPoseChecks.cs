#if UNITY_EDITOR
using System;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Editor;
using CodeHelper.ProceduralShooterFramework.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class WeaponPoseChecks
{
    [MenuItem("Tools/CodeHelper/Run Weapon Pose Checks")]
    public static void Run()
    {
        Scene userScene = SceneManager.GetActiveScene();
        bool userSceneWasDirty = userScene.isDirty;
        Scene testScene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("Weapon pose checks (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, testScene);
        root.SetActive(false);
        WeaponPoseProfile profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        try
        {
            Transform cameraHolder = Child(root.transform, "CameraHolder");
            Transform weapon = Child(cameraHolder, "WeaponHolder");
            Transform grip = Child(weapon, "RightGrip");
            grip.localPosition = new Vector3(0.03f, -0.08f, -0.2f);
            Vector3 gripLocal = grip.localPosition;
            Vector3 aimPosition = new Vector3(0f, -0.05f, 0.3f);
            Quaternion aimRotation = Quaternion.Euler(3f, 2f, 1f);
            weapon.SetLocalPositionAndRotation(aimPosition, aimRotation);
            var controller = weapon.gameObject.AddComponent<WeaponPoseController>();
            controller.poseRoot = weapon;
            controller.profile = profile;
            controller.useLookController = false;

            Step(controller, 1f);
            Pose(weapon, aimPosition, aimRotation, "empty profile preserves current placement");
            Require(profile.StorePose(WeaponPoseState.Aim, WeaponPose.FromLocalTransform(weapon)), "store Aim");
            Require(profile.HasAim && !profile.HasIdle, "Aim capture does not invent Idle");
            Step(controller, 1f);
            Pose(weapon, aimPosition, aimRotation, "missing Idle preserves current Aim");

            Vector3 idlePosition = new Vector3(0.15f, -0.2f, 0.4f);
            Quaternion idleRotation = Quaternion.Euler(12f, 8f, 15f);
            profile.StorePose(WeaponPoseState.Idle, new WeaponPose
                { localPosition = idlePosition, localEulerAngles = idleRotation.eulerAngles });
            profile.transitionDuration = 0.2f;
            Step(controller, 0f);
            Pose(weapon, aimPosition, aimRotation, "starting transition does not jump");
            Step(controller, 0.1f);
            Near(weapon.localPosition, Vector3.Lerp(aimPosition, idlePosition, 0.5f), "half transition");
            Vector3 interruptedPosition = weapon.localPosition;
            Quaternion interruptedRotation = weapon.localRotation;
            controller.SetAiming(true);
            Step(controller, 0f);
            Pose(weapon, interruptedPosition, interruptedRotation, "reversal starts from displayed pose");
            Step(controller, 0.2f);
            Pose(weapon, aimPosition, aimRotation, "Aim endpoint");
            for (int i = 0; i < 120; i++) Step(controller, 1f / 60f);
            Pose(weapon, aimPosition, aimRotation, "no accumulated offsets");
            Near(grip.localPosition, gripLocal, "grip local placement untouched");

            // The driven pose is local: camera/root movement carries the weapon without changing its data.
            cameraHolder.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(20f, 70f, 15f));
            Step(controller, 0.1f);
            Near(weapon.position, cameraHolder.TransformPoint(aimPosition), "camera motion carried in same frame");
            profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose storedAim);
            Near(storedAim.localPosition, aimPosition, "runtime does not change profile");

            Invoke(controller, "OnDisable");
            Invoke(controller, "OnEnable");
            Step(controller, 0f);
            Pose(weapon, aimPosition, aimRotation, "reenable preserves current pose");
            controller.SetAiming(false);
            profile.transitionDuration = 0f;
            Step(controller, 0f);
            Pose(weapon, idlePosition, idleRotation, "zero duration snaps to saved pose");

            var look = root.AddComponent<PlayerLookController>();
            look.lockCursorOnEnable = false;
            controller.lookController = look;
            controller.useLookController = true;
            typeof(PlayerLookController).GetMethod("ProcessLookFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(look, new object[] { Vector2.zero, 0f, true, 0f });
            Step(controller, 0f);
            Pose(weapon, aimPosition, aimRotation, "consumes PlayerLook IsAiming");
            look.enabled = false;
            Step(controller, 0f);
            Pose(weapon, idlePosition, idleRotation, "disabled look source does not leave stale Aim");

            Require(!profile.StorePose(WeaponPoseState.Aim, new WeaponPose
                { localPosition = new Vector3(float.NaN, 0f, 0f) }), "reject nonfinite capture");
            Require(!profile.TryGetPose((WeaponPoseState)42, out _), "reject invalid state");
            Transform camera = Child(weapon, "UnsafeCamera");
            camera.gameObject.AddComponent<Camera>();
            Require(!controller.TryValidateRoot(out _), "reject anchors containing camera");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
            EditorSceneManager.ClosePreviewScene(testScene);
        }
        CheckEditorCapture();
        Require(!userScene.IsValid() || userScene.isDirty == userSceneWasDirty, "editor capture checks preserve scene dirty state");
        CheckSpringMath();
        CheckAimMotion();
        Require(!userScene.IsValid() || userScene.isDirty == userSceneWasDirty, "aim motion checks preserve scene dirty state");
        CheckMotionPresets();
        Require(!userScene.IsValid() || userScene.isDirty == userSceneWasDirty, "checks preserve user's scene dirty state");
        Debug.Log("[WeaponPoseChecks] Passed: uncaptured pose safety, local captures, Idle/Aim transition, reversal, " +
                  "zero duration, no drift, camera-parent movement, look input, unchanged grips/profile, reenable, " +
                  "invalid data/hierarchy, editor preview/Undo, asset persistence and bounded weapon-only Aim bounce. " +
                  "Real weapon alignment and bounce feel still need Play Mode.");
    }

    private static void CheckEditorCapture()
    {
        // Explicit unique asset, owned and removed only by this check. Never touch user assets/scenes.
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CodeHelperWeaponPoseCheck.asset");
        Scene testScene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("Weapon authoring check (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, testScene);
        root.SetActive(false);
        WeaponPoseProfile profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        try
        {
            AssetDatabase.CreateAsset(profile, path);
            Transform weapon = Child(root.transform, "WeaponHolder");
            var controller = weapon.gameObject.AddComponent<WeaponPoseController>();
            controller.profile = profile;
            Vector3 aim = new Vector3(0.01f, -0.05f, 0.3f);
            Quaternion rotation = Quaternion.Euler(2f, 5f, 10f);
            weapon.SetLocalPositionAndRotation(aim, rotation);
            Require(WeaponPoseAuthoring.Capture(controller, WeaponPoseState.Aim), "editor captures Aim");
            Require(!profile.HasIdle, "editor capture leaves Idle pending");
            weapon.localPosition = new Vector3(0.15f, -0.2f, 0.4f);
            Vector3 idle = weapon.localPosition;
            Require(WeaponPoseAuthoring.Capture(controller, WeaponPoseState.Idle), "editor captures Idle");

            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            WeaponPoseAuthoring.ApplyMotionPreset(profile, true);
            Undo.FlushUndoRecordObjects();
            Require(profile.aimMotion.enabled && profile.aimMotion.frequency == 2f, "diagnostic preset applied");
            Undo.PerformUndo();
            Require(!profile.aimMotion.enabled, "preset Undo restores prior disabled motion");
            Undo.PerformRedo();
            Require(profile.aimMotion.enabled && profile.aimMotion.frequency == 2f, "preset Redo restores diagnostic motion");
            WeaponPoseAuthoring.ApplyMotionPreset(profile, false);

            controller.profile = null;
            Resources.UnloadAsset(profile);
            profile = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(path);
            controller.profile = profile;
            Require(profile.HasAim && profile.HasIdle, "capture flags persist on disk");
            Require(profile.aimMotion.enabled && profile.aimMotion.frequency == 4.5f && profile.aimMotion.damping == 0.6f,
                "soft preset remains enabled and persists after asset reload");
            profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose stored);
            Near(stored.localPosition, aim, "saved Aim survives asset reload");

            Undo.IncrementCurrentGroup();
            Require(WeaponPoseAuthoring.Preview(controller, WeaponPoseState.Aim), "preview saved Aim");
            Undo.FlushUndoRecordObjects();
            Pose(weapon, aim, rotation, "preview uses local pose");
            Undo.PerformUndo();
            Near(weapon.localPosition, idle, "preview supports Undo");
        }
        finally
        {
            if (profile != null) Undo.ClearUndo(profile);
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(path);
            EditorSceneManager.ClosePreviewScene(testScene);
        }
    }

    private static void CheckSpringMath()
    {
        foreach (float damping in new[] { 0.05f, 0.6f, 1f })
        {
            var once = new WeaponAimSpring();
            var split = new WeaponAimSpring();
            Vector3 kick = new Vector3(0.006f, -0.012f, 0.003f);
            once.Kick(kick, 4.5f, 0.1f);
            split.Kick(kick, 4.5f, 0.1f);
            once.Step(0.1f, 4.5f, damping, 0.1f);
            for (int i = 0; i < 10; i++) split.Step(0.01f, 4.5f, damping, 0.1f);
            Near(split.Offset, once.Offset, "analytical spring frame-rate independence");
            Near(split.Velocity, once.Velocity, "analytical velocity frame-rate independence");
            for (int i = 0; i < 1000; i++)
            {
                once.Kick(kick * 1000f, 20f, 0.025f);
                once.Step(1f / 120f, 20f, damping, 0.025f);
                Require(once.Offset.magnitude <= 0.02501f, "repeated kicks remain bounded");
            }
            once.Step(60f, 20f, damping, 0.025f);
            Near(once.Offset, Vector3.zero, "spring settles after long frame");
            once.Kick(new Vector3(float.NaN, 0f, 0f), float.NaN, float.NaN);
            once.Step(float.NaN, float.NaN, float.NaN, float.NaN);
            Near(once.Offset, Vector3.zero, "invalid spring values stay finite");
        }
    }

    private static void CheckAimMotion()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject host = new GameObject("Weapon bounce check (temporary)");
        host.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(host, scene);
        host.SetActive(false);
        WeaponPoseProfile profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        WeaponPoseProfile replacement = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        try
        {
            Transform camera = Child(host.transform, "Camera");
            camera.gameObject.AddComponent<Camera>();
            Vector3 cameraPosition = camera.position;
            Quaternion cameraRotation = camera.rotation;
            Transform weapon = Child(host.transform, "Weapon");
            Transform grip = Child(weapon, "Grip");
            grip.localPosition = new Vector3(0.02f, -0.04f, 0.1f);
            Vector3 gripPosition = grip.localPosition;
            WeaponPose idle = new WeaponPose { localPosition = new Vector3(0.15f, -0.15f, 0.4f) };
            WeaponPose aim = new WeaponPose { localPosition = new Vector3(0f, -0.05f, 0.3f) };
            profile.StorePose(WeaponPoseState.Idle, idle);
            profile.StorePose(WeaponPoseState.Aim, aim);
            profile.aimMotion.enabled = true;
            profile.transitionDuration = 0.18f;
            var controller = weapon.gameObject.AddComponent<WeaponPoseController>();
            controller.profile = profile;
            controller.useLookController = false;
            weapon.localPosition = idle.localPosition;
            Step(controller, 1f);
            Near(controller.ProceduralPositionOffset, Vector3.zero, "no startup impulse");
            controller.SetAiming(true);
            Step(controller, 1f / 60f);
            Require(controller.ProceduralPositionOffset.magnitude > 0.0001f, "Aim starts weapon position bounce");
            Require(controller.ProceduralRotationOffset.magnitude > 0.01f, "Aim starts angular bounce");
            Vector3 displayed = weapon.localPosition;
            Quaternion displayedRotation = weapon.localRotation;
            controller.SetAiming(false);
            Step(controller, 0f);
            Pose(weapon, displayed, displayedRotation, "bounce reversal has no instantaneous pose jump");
            for (int i = 0; i < 300; i++)
            {
                controller.SetAiming((i & 1) == 0);
                Step(controller, 1f / 120f);
                Require(controller.ProceduralPositionOffset.magnitude <= profile.aimMotion.maxPositionOffset + 0.00001f,
                    "rapid toggles respect position limit");
                Require(controller.ProceduralRotationOffset.magnitude <= profile.aimMotion.maxRotationOffset + 0.00001f,
                    "rapid toggles respect rotation limit");
            }
            controller.SetAiming(true);
            for (int i = 0; i < 240; i++) Step(controller, 1f / 60f);
            Pose(weapon, aim.localPosition, aim.Rotation, "bounce settles exactly on saved Aim");
            Near(controller.ProceduralPositionOffset, Vector3.zero, "no residual position offset");
            Near(controller.ProceduralRotationOffset, Vector3.zero, "no residual angular offset");
            Near(camera.position, cameraPosition, "bounce does not translate camera");
            Require(Quaternion.Angle(camera.rotation, cameraRotation) < 0.001f, "bounce does not rotate camera");
            Near(grip.localPosition, gripPosition, "bounce does not modify grip-local pose");
            profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose stored);
            Near(stored.localPosition, aim.localPosition, "bounce never writes saved Aim");

            // Use an instantaneous base transition to make capture contamination unambiguous.
            profile.transitionDuration = 0f;
            controller.SetAiming(false);
            Step(controller, 0.016f);
            Require(controller.ProceduralPositionOffset.magnitude > 0.0001f, "exit Aim also bounces");
            Near(controller.GetPoseForCapture().localPosition, idle.localPosition, "capture excludes transient bounce");
            Require(WeaponPoseAuthoring.Capture(controller, WeaponPoseState.Idle), "editor can capture base during bounce");
            profile.TryGetPose(WeaponPoseState.Idle, out stored);
            Near(stored.localPosition, idle.localPosition, "captured Idle has no procedural contamination");
            Require(Quaternion.Angle(stored.Rotation, idle.Rotation) < 0.01f, "captured rotation excludes bounce");

            profile.aimMotion.enabled = false;
            Step(controller, 0f);
            Pose(weapon, idle.localPosition, idle.Rotation, "disabling effect removes its residue");
            profile.aimMotion.enabled = true;
            controller.SetAiming(true);
            Step(controller, 0.016f);
            Invoke(controller, "OnDisable");
            Pose(weapon, aim.localPosition, aim.Rotation, "component disable restores base only");
            Invoke(controller, "OnEnable");
            Step(controller, 0f);
            Pose(weapon, aim.localPosition, aim.Rotation, "reenable does not kick or bake old effect");

            controller.SetAiming(false);
            Step(controller, 0.016f);
            controller.profile = replacement; // Empty replacement: must remove old bounce, not move to origin.
            Step(controller, 0f);
            Pose(weapon, idle.localPosition, idle.Rotation, "missing replacement pose clears old effect");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(replacement);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckMotionPresets()
    {
        // Use a persistent test asset for editor Undo/save operations, not an unsaved scene object.
        string fixturePath = AssetDatabase.GenerateUniqueAssetPath("Assets/CodeHelperMotionPresetCheck.asset");
        WeaponPoseProfile profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
        try
        {
            AssetDatabase.CreateAsset(profile, fixturePath);
            profile.StorePose(WeaponPoseState.Idle, new WeaponPose { localPosition = new Vector3(0.03f, -0.1f, 0.4f) });
            profile.StorePose(WeaponPoseState.Aim, new WeaponPose { localPosition = new Vector3(0f, -0.05f, 0.3f) });
            profile.transitionDuration = 0.18f;
            foreach (bool exaggerated in new[] { false, true })
            {
                profile.TryGetPose(WeaponPoseState.Idle, out WeaponPose idle);
                profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose aim);
                WeaponPoseAuthoring.ApplyMotionPreset(profile, exaggerated);
                profile.TryGetPose(WeaponPoseState.Idle, out WeaponPose afterIdle);
                profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose afterAim);
                Near(afterIdle.localPosition, idle.localPosition, "preset preserves Idle");
                Near(afterAim.localPosition, aim.localPosition, "preset preserves Aim");
                Require(Mathf.Abs(profile.transitionDuration - 0.18f) < 0.0001f, "preset preserves transition duration");
                MeasureConfiguredMotion(profile, exaggerated, exaggerated ? "exaggerated preset" : "soft preset");
            }

            // Optional snapshot exported from the real user's YAML by the external validation command.
            // Load only data into this temporary profile: never import or modify the actual user asset.
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-weaponPoseFixture");
            if (argument >= 0 && argument + 1 < args.Length)
            {
                LoadProfileFixture(profile, args[argument + 1]);
                Require(profile.HasIdle && profile.HasAim && profile.aimMotion != null && profile.aimMotion.enabled,
                    "real AK12 snapshot contains both poses and enabled motion");
                bool diagnostic = profile.aimMotion.aimRotationKick.magnitude > 8f;
                MeasureConfiguredMotion(profile, diagnostic, "actual AK 12.asset snapshot");
            }
        }
        finally
        {
            Undo.ClearUndo(profile);
            AssetDatabase.DeleteAsset(fixturePath);
        }
    }

    [Serializable]
    private class ProfileFixture
    {
        public float transitionDuration = 0.18f;
        public bool synchronizeAimEffects = false;
        public float transitionOscillations = 1f;
        public WeaponPose idle = new WeaponPose();
        public WeaponPose aim = new WeaponPose();
        public bool idleCaptured = false;
        public bool aimCaptured = false;
        public WeaponAimMotionSettings aimMotion = new WeaponAimMotionSettings();
        public WeaponAimCameraSettings aimCamera = new WeaponAimCameraSettings();
    }

    internal static void LoadProfileFixture(WeaponPoseProfile profile, string path)
    {
        // This is plain exported data, not EditorJsonUtility's native-object JSON format.
        ProfileFixture data = JsonUtility.FromJson<ProfileFixture>(System.IO.File.ReadAllText(path));
        Require(data != null && data.idleCaptured && data.aimCaptured, "snapshot has captured poses");
        Require(profile.StorePose(WeaponPoseState.Idle, data.idle) && profile.StorePose(WeaponPoseState.Aim, data.aim),
            "snapshot has finite pose data");
        profile.transitionDuration = data.transitionDuration;
        profile.synchronizeAimEffects = data.synchronizeAimEffects;
        profile.transitionOscillations = data.transitionOscillations;
        profile.aimMotion = data.aimMotion;
        profile.aimCamera = data.aimCamera;
    }

    private static void MeasureConfiguredMotion(WeaponPoseProfile profile, bool exaggerated, string label)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("Configured weapon motion check (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        string originalData = EditorJsonUtility.ToJson(profile);
        try
        {
            Transform camera = Child(root.transform, "Camera");
            Camera cameraComponent = camera.gameObject.AddComponent<Camera>();
            cameraComponent.fieldOfView = 60f;
            Transform weapon = Child(root.transform, "WeaponHolder");
            Transform grip = Child(weapon, "Grip");
            grip.localPosition = new Vector3(0.01f, -0.08f, -0.22f);
            Vector3 gripLocal = grip.localPosition;
            PlayerLookController look = root.AddComponent<PlayerLookController>();
            look.lockCursorOnEnable = false;
            var controller = weapon.gameObject.AddComponent<WeaponPoseController>();
            controller.profile = profile;
            controller.lookController = look;
            controller.useLookController = true;
            profile.TryGetPose(WeaponPoseState.Idle, out WeaponPose idle);
            profile.TryGetPose(WeaponPoseState.Aim, out WeaponPose aim);
            weapon.SetLocalPositionAndRotation(idle.localPosition, idle.Rotation);
            FeedAiming(look, false);
            Step(controller, 1f);
            float enterPosition = 0f, enterRotation = 0f, exitPosition = 0f, exitRotation = 0f;
            FeedAiming(look, true);
            for (int i = 0; i < 960; i++)
            {
                Step(controller, 1f / 120f);
                enterPosition = Mathf.Max(enterPosition, controller.ProceduralPositionOffset.magnitude);
                enterRotation = Mathf.Max(enterRotation, controller.ProceduralRotationOffset.magnitude);
            }
            Pose(weapon, aim.localPosition, aim.Rotation, label + " returns exactly to Aim");
            FeedAiming(look, false);
            for (int i = 0; i < 960; i++)
            {
                Step(controller, 1f / 120f);
                exitPosition = Mathf.Max(exitPosition, controller.ProceduralPositionOffset.magnitude);
                exitRotation = Mathf.Max(exitRotation, controller.ProceduralRotationOffset.magnitude);
            }
            Pose(weapon, idle.localPosition, idle.Rotation, label + " returns exactly to Idle");
            Require(enterPosition > (exaggerated ? 0.04f : 0.001f), label + " measurable positional bounce");
            Require(enterRotation > (exaggerated ? 4f : 0.1f), label + " measurable angular bounce");
            Require(exitPosition > 0.001f && exitRotation > 0.1f, label + " measurable exit bounce");
            Near(camera.localPosition, Vector3.zero, label + " camera unchanged");
            Require(Quaternion.Angle(camera.localRotation, Quaternion.identity) < 0.001f && cameraComponent.fieldOfView == 60f,
                label + " camera rotation and FOV unchanged");
            Near(grip.localPosition, gripLocal, label + " grip-local pose unchanged");
            Require(EditorJsonUtility.ToJson(profile) == originalData, label + " saved profile data unchanged");
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[WeaponPoseChecks] {0}: Aim peak {1:F2} cm / {2:F2} deg; Idle peak {3:F2} cm / {4:F2} deg. Camera/FOV unchanged; both endpoints restored.",
                label, enterPosition * 100f, enterRotation, exitPosition * 100f, exitRotation));
        }
        finally
        {
            Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void FeedAiming(PlayerLookController look, bool aiming) => typeof(PlayerLookController)
        .GetMethod("ProcessLookFrame", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(look, new object[] { Vector2.zero, 0f, aiming, 0f });

    private static Transform Child(Transform parent, string name)
    {
        Transform result = new GameObject(name).transform;
        result.SetParent(parent, false);
        return result;
    }
    private static void Step(WeaponPoseController controller, float dt) => Invoke(controller, "ApplyPose", dt);
    private static void Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static void Pose(Transform root, Vector3 position, Quaternion rotation, string label)
    {
        Near(root.localPosition, position, label + " position");
        Require(Quaternion.Angle(root.localRotation, rotation) < 0.1f, label + " rotation");
    }
    private static void Near(Vector3 actual, Vector3 expected, string label) =>
        Require(Vector3.Distance(actual, expected) < 0.0001f, label);
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[WeaponPoseChecks] Failed: " + label);
    }
}
#endif
