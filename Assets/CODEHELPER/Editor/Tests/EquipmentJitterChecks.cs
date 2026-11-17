#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using CodeHelper.ProceduralShooterFramework.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Continuous-frame diagnostics; never changes the saved scene or its definitions.</summary>
public static class EquipmentJitterChecks
{
    private struct Measurements
    {
        public float head, camera, hips;
    }

    [MenuItem("Tools/CodeHelper/Diagnose Reload Jitter (Isolated)")]
    public static void Run()
    {
        foreach (SpineDriveMode mode in new[] { SpineDriveMode.LateUpdate, SpineDriveMode.AnimationJob })
            foreach (int rate in new[] { 30, 60, 144 })
            {
                Measurements idle = Check(mode, "idle", rate);
                Measurements reload = Check(mode, "reload", rate);
                RequireStable(idle, reload, mode + "/reload/" + rate);
                foreach (string variant in new[] { "static-target", "no-character", "fingers-only" })
                    RequireStable(idle, Check(mode, variant, rate), mode + "/" + variant + "/" + rate);
                Measurements control = Check(mode, "full-arm-control", rate);
                if (mode == SpineDriveMode.LateUpdate && rate == 60)
                    Require(control.head > idle.head + 0.0002f && control.camera > idle.camera + 0.0002f,
                        "full-arm control must still reproduce the original excess body/camera motion");
            }
        Debug.Log("[EquipmentJitterChecks] Passed: reload matches idle body/camera baseline at 30/60/144 Hz in both modes; " +
            "full-arm control reproduces original LateUpdate issue, no target-sampling side effects or frozen-clock drift.");
    }

    private static void RequireStable(Measurements idle, Measurements reload, string label)
    {
        const float tolerance = 0.00001f; // 0.01 mm above the same-rate idle baseline.
        Require(reload.head <= idle.head + tolerance && reload.camera <= idle.camera + tolerance &&
            reload.hips <= idle.hips + tolerance, label + " adds body/camera movement above idle");
    }

    private static Measurements Check(SpineDriveMode mode, string variant, int rate)
    {
        var scene = EditorSceneManager.OpenPreviewScene(EquipmentAssetPaths.Scene);
        EquipmentSceneController equipment = null;
        FirearmDefinition definition = null;
        EquipmentActionDefinition action = null;
        AvatarMask mask = null;
        try
        {
            equipment = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EquipmentSceneController>(true)).Single();
            definition = Object.Instantiate((FirearmDefinition)equipment.slots[0].definition);
            action = Object.Instantiate(definition.reload);
            definition.reload = definition.reloadEmpty = action;
            definition.equip = definition.unequip = null;
            equipment.slots[0].definition = definition;
            if (variant == "static-target") action.leftHandTargetMotion = null;
            if (variant == "no-character") action.characterClip = null;
            if (variant == "fingers-only" || variant == "full-arm-control")
            {
                mask = new AvatarMask { hideFlags = HideFlags.HideAndDontSave };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i == (int)AvatarMaskBodyPart.LeftFingers ||
                        (variant == "full-arm-control" && i == (int)AvatarMaskBodyPart.LeftArm));
                action.characterMask = mask;
            }
            var spine = equipment.spine;
            var hands = equipment.handIK;
            Invoke(spine, "Awake"); Invoke(hands, "Awake");
            Invoke(equipment.weaponPose, "Awake"); Invoke(equipment.weaponPose, "OnEnable");
            Invoke(equipment.look, "Awake");
            equipment.look.ConnectCamera();
            spine.animator.Rebind(); spine.animator.Update(0f);
            Require((bool)Invoke(spine, "ResolveRuntimeBones"), "resolve bones");
            Invoke(spine, "CaptureBindPose");
            Set(spine, "_activeMode", mode);
            Require((bool)Invoke(spine, mode == SpineDriveMode.AnimationJob ? "TryBuildJobGraph" : "TryBuildEquipmentGraph"), "build graph");
            Set(spine, "_ready", true);
            var graph = (PlayableGraph)Get(spine, "_graph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            Invoke(equipment, "OnEnable");
            Transform head = spine.animator.GetBoneTransform(HumanBodyBones.Head);
            Transform hips = spine.animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform camera = equipment.look.cameraRig.realCamera;
            float dt = 1f / rate;
            for (int i = 0; i < rate; i++) Frame(equipment, graph, dt, hips);
            if (variant != "idle") Require(equipment.Runtime.TryReload(), "start reload");
            Vector3 lastHead = head.position, lastCamera = camera.position, lastHips = hips.position;
            Quaternion lastUpper = hands.leftHand.upperArm.rotation;
            float headStep = 0f, cameraStep = 0f, hipsStep = 0f, upperStep = 0f, sampleSideEffect = 0f;
            for (int frame = 0; frame < Mathf.CeilToInt(action.duration / dt) + 5; frame++)
            {
                sampleSideEffect = Mathf.Max(sampleSideEffect, Frame(equipment, graph, dt, hips));
                headStep = Mathf.Max(headStep, Vector3.Distance(lastHead, head.position));
                cameraStep = Mathf.Max(cameraStep, Vector3.Distance(lastCamera, camera.position));
                hipsStep = Mathf.Max(hipsStep, Vector3.Distance(lastHips, hips.position));
                upperStep = Mathf.Max(upperStep, Quaternion.Angle(lastUpper, hands.leftHand.upperArm.rotation));
                lastHead = head.position; lastCamera = camera.position; lastHips = hips.position;
                lastUpper = hands.leftHand.upperArm.rotation;
            }
            // Same clocks, same input, same target: repeated evaluation must not alternate/drift.
            if (variant != "idle")
            {
                if (!equipment.Runtime.TryReload())
                {
                    Require(equipment.Runtime.TryFire(), "spend round for frozen diagnostic");
                    for (int i = 0; i < rate; i++) Frame(equipment, graph, dt, hips);
                    Require(equipment.Runtime.TryReload(), "restart frozen reload");
                }
                Frame(equipment, graph, action.duration * 0.5f, hips);
            }
            Vector3 frozenHead = head.position, frozenHips = hips.position, frozenHand = hands.leftHand.hand.position;
            float frozenBody = 0f, frozenHandError = 0f;
            for (int i = 0; i < 20; i++)
            {
                Frame(equipment, graph, 0f, hips);
                frozenBody = Mathf.Max(frozenBody, Vector3.Distance(frozenHead, head.position), Vector3.Distance(frozenHips, hips.position));
                frozenHandError = Mathf.Max(frozenHandError, Vector3.Distance(frozenHand, hands.leftHand.hand.position));
            }
            Require(sampleSideEffect < 0.00001f, "target sampling moves ancestors");
            Require(frozenBody < 0.00001f && frozenHandError < 0.00001f, "repeated identical clocks cause drift");
            Debug.Log($"[EquipmentJitterChecks] {mode}/{variant}/{rate}Hz: max frame head={headStep * 1000f:F3}mm, camera={cameraStep * 1000f:F3}mm, " +
                $"hips={hipsStep * 1000f:F3}mm, upperArm={upperStep:F3}deg; sample ancestor side effect={sampleSideEffect * 1000f:F3}mm; " +
                $"frozen body={frozenBody * 1000f:F3}mm, hand={frozenHandError * 1000f:F3}mm; applyRootMotion={spine.animator.applyRootMotion}.");
            return new Measurements { head = headStep, camera = cameraStep, hips = hipsStep };
        }
        finally
        {
            if (equipment != null) { Invoke(equipment, "OnDisable"); Invoke(equipment.spine, "TearDown"); }
            EditorSceneManager.ClosePreviewScene(scene);
            if (definition != null) Object.DestroyImmediate(definition);
            if (action != null) Object.DestroyImmediate(action);
            if (mask != null) Object.DestroyImmediate(mask);
        }
    }

    private static float Frame(EquipmentSceneController equipment, PlayableGraph graph, float dt, Transform hips)
    {
        // Match actual DefaultExecutionOrder: equipment -20, spine 200, pose 550, hands 600.
        equipment.Step(dt);
        Invoke(equipment.spine, "Update");
        Invoke(equipment.weaponPose, "PreparePose", dt);
        Invoke(equipment.handIK, "Update");
        graph.Evaluate(dt);
        Invoke(equipment.spine, "LateUpdate");
        Invoke(equipment.look.cameraRig, "ApplyCamera", dt);
        Invoke(equipment.weaponPose, "ApplyPreparedPose", dt);
        Vector3 beforeSample = hips.position;
        equipment.ApplyMotion();
        float sideEffect = Vector3.Distance(beforeSample, hips.position);
        Invoke(equipment.handIK, "LateUpdate");
        return sideEffect;
    }

    private static object Invoke(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("[EquipmentJitterChecks] " + message); }
}
#endif
