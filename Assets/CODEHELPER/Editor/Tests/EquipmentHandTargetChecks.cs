#if UNITY_EDITOR
using System;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Runs in the parent scene fixture with cloned action definitions; no saved asset edits.</summary>
public static class EquipmentHandTargetChecks
{
    public static void Check(EquipmentSceneController equipment, PlayableGraph graph)
    {
        var slot = equipment.slots[1];
        var definition = (MeleeDefinition)slot.definition;
        var left = slot.leftHandPose; var right = slot.rightHandPose;
        var recipe = definition.attacks[0];
        EquipmentHandMotion originalMotion = recipe.rightHandMotion;
        float pitch = equipment.look.CameraPitch, yaw = equipment.look.CameraYaw;
        Vector3 guardPosition = right.guard.localPosition;
        Quaternion guardRotation = right.guard.localRotation;
        Transform end = right.actionTarget;
        Quaternion endRotation = end.localRotation;
        AnimationCurve originalIK = recipe.rightIK;
        AnimationClip clip = null;
        float maxResidual = 0f;
        int reachLimited = 0;
        try
        {
            Require(definition.characterMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), "authored mask remains intact");
            SetLook(equipment, graph, 0f, yaw);
            RequireGuard(equipment.handIK.leftHand, left); RequireGuard(equipment.handIK.rightHand, right);
            CheckMask(equipment, false, false);
            right.guard.localPosition += new Vector3(0.01f, -0.01f, 0f);
            right.guard.localRotation *= Quaternion.Euler(0f, 5f, 0f);
            Frame(equipment, graph, 0f);
            RequireGuard(equipment.handIK.rightHand, right);
            right.guard.SetLocalPositionAndRotation(guardPosition, guardRotation);
            foreach (Vector2 angles in new[] { Vector2.zero, new Vector2(45f, 50f), new Vector2(90f, 0f),
                new Vector2(-45f, -50f), new Vector2(-90f, 0f) })
            {
                SetLook(equipment, graph, angles.x, yaw + angles.y);
                RequireGuard(equipment.handIK.leftHand, left); RequireGuard(equipment.handIK.rightHand, right);
                maxResidual = Mathf.Max(maxResidual, CheckWrist(equipment.handIK.leftHand, ref reachLimited), CheckWrist(equipment.handIK.rightHand, ref reachLimited));
            }
            SetLook(equipment, graph, 0f, yaw);
            int contacts = equipment.Runtime.Current.MeleeContactCount;
            Require(equipment.Runtime.TryMeleeAttack(), "start authored straight punch");
            float previous = 0f;
            foreach (float progress in new[] { 0f, 0.1f, 0.25f, 0.4f, 0.6f, 0.9f, 0.98f })
            {
                Frame(equipment, graph, recipe.duration * (progress - previous)); previous = progress;
                var action = equipment.Runtime.Current.Action;
                float amount = Mathf.Clamp01(recipe.rightHandMotion.reach.Evaluate(action.Progress)) *
                    EquipmentSceneController.ActionWeight(recipe, action.Progress);
                Vector3 expected = Vector3.Lerp(right.guard.position, right.actionTarget.position, amount);
                Require(Vector3.Distance(expected, equipment.handIK.rightHand.target.position) < 0.00001f, "punch follows exact editable straight path");
                RequireGuard(equipment.handIK.leftHand, left);
                maxResidual = Mathf.Max(maxResidual, CheckWrist(equipment.handIK.rightHand, ref reachLimited));
                Require(equipment.Runtime.Current.MeleeContactCount == contacts + (action.Progress >= recipe.commitTime ? 1 : 0),
                    "contact uses the same action clock as the hand path");
            }
            Frame(equipment, graph, recipe.duration);
            RequireGuard(equipment.handIK.rightHand, right);
            Require(equipment.Runtime.Current.MeleeContactCount == contacts + 1, "punch returns to guard with one contact");

            // Same infrastructure for an arc/rotation, plus optional local Generic target animation.
            recipe.rightHandMotion = new EquipmentHandMotion { enabled = true };
            recipe.rightHandMotion.position.x = AnimationCurve.Constant(0f, 1f, 0.04f);
            recipe.rightHandMotion.rotation.y = AnimationCurve.Constant(0f, 1f, 20f);
            clip = new AnimationClip { name = "Temporary hand arc clip" };
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"),
                AnimationCurve.Constant(0f, 1f, 0.02f));
            recipe.rightHandMotion.motionClip = clip;
            Require(equipment.Runtime.TryMeleeAttack(), "start independent curve/clip hand path");
            Frame(equipment, graph, recipe.duration * 0.5f);
            Require(Vector3.Distance(equipment.handIK.rightHand.target.position, right.guard.TransformPoint(new Vector3(0.04f, 0.02f, 0f))) < 0.00001f,
                "position curves and Generic motion combine in guard space");
            Require(Quaternion.Angle(equipment.handIK.rightHand.target.rotation, right.guard.rotation * Quaternion.Euler(0f, 20f, 0f)) < 0.1f,
                "wrist rotation curve is independent from position");
            end.localRotation *= Quaternion.Euler(30f, 0f, 10f);
            recipe.rightHandMotion.reach = AnimationCurve.Constant(0f, 1f, 1f);
            Frame(equipment, graph, 0f);
            Quaternion rotatedGoal = right.guard.rotation * Quaternion.Euler(0f, 20f, 0f) *
                Quaternion.Inverse(right.guard.rotation) * end.rotation;
            Require(Quaternion.Angle(equipment.handIK.rightHand.target.rotation, rotatedGoal) < 0.1f,
                "rotation offsets use guard axes even with a differently rotated endpoint");
            Vector3 frozen = equipment.handIK.rightHand.hand.position;
            Vector3 head = equipment.spine.animator.GetBoneTransform(HumanBodyBones.Head).position;
            for (int repeat = 0; repeat < 30; repeat++)
            {
                Frame(equipment, graph, 0f);
                Require(Vector3.Distance(frozen, equipment.handIK.rightHand.hand.position) < 0.00001f &&
                    Vector3.Distance(head, equipment.spine.animator.GetBoneTransform(HumanBodyBones.Head).position) < 0.00001f,
                    "owned targets and fixed animation clocks do not drift");
            }
            Require(equipment.Runtime.CancelAction(), "cancel hand action"); Frame(equipment, graph, 0f);
            RequireGuard(equipment.handIK.rightHand, right);
            recipe.rightHandMotion = originalMotion;
            right.actionTarget = null;
            Frame(equipment, graph, 1f); // Contact already happened; cancelling does not erase attack cooldown.
            Require(equipment.Runtime.TryMeleeAttack(), "missing endpoint can still hold guard");
            Frame(equipment, graph, recipe.duration * 0.3f); RequireGuard(equipment.handIK.rightHand, right);
            equipment.Runtime.CancelAction(); right.actionTarget = end;

            right.enabled = false; Frame(equipment, graph, 0f);
            Require(equipment.handIK.rightHand.target == null && equipment.handIK.rightHand.weight == 0f, "one-hand override leaves other hand in original FK mode");
            CheckMask(equipment, false, true);
            Vector3 fkHand = equipment.handIK.rightHand.hand.position;
            right.enabled = true; right.weight = 0f; Frame(equipment, graph, 0f);
            CheckMask(equipment, false, true);
            Require(equipment.handIK.rightHand.weight == 0f && Vector3.Distance(fkHand, equipment.handIK.rightHand.hand.position) < 0.0001f,
                "zero binding weight retains equipment FK, not the underlying controller arm");
            right.weight = 0.5f; Frame(equipment, graph, 0f); CheckMask(equipment, false, true);
            Require(equipment.handIK.rightHand.weight == 0.5f, "partial weight preserves FK blend source");
            right.weight = 1f;
            recipe.rightIK = AnimationCurve.Constant(0f, 1f, 0f);
            Require(equipment.Runtime.TryMeleeAttack(), "start action with released hand IK");
            Frame(equipment, graph, recipe.duration * 0.5f); CheckMask(equipment, false, true);
            Require(equipment.handIK.rightHand.weight == 0f, "action IK curve can release an editable hand");
            equipment.Runtime.CancelAction(); recipe.rightIK = originalIK;
            slot.leftHandPose = new EquipmentHandBinding { guard = equipment.handIK.leftHand.hand };
            Frame(equipment, graph, 0f);
            Require(equipment.handIK.leftHand.target == null && equipment.handIK.leftHand.weight == 0f, "arm-parented targets are rejected without cycles");
            slot.leftHandPose = left; Frame(equipment, graph, 0f); CheckMask(equipment, false, false);
            RequireGuard(equipment.handIK.leftHand, left);
            end.localRotation = endRotation;
            CheckContinuous(equipment, graph, recipe, yaw);
        }
        finally
        {
            equipment.Runtime.CancelAction(); recipe.rightHandMotion = originalMotion;
            slot.leftHandPose = left; slot.rightHandPose = right;
            right.enabled = true; right.actionTarget = end;
            right.weight = 1f; end.localRotation = endRotation; recipe.rightIK = originalIK;
            right.guard.SetLocalPositionAndRotation(guardPosition, guardRotation);
            SetLook(equipment, graph, pitch, yaw);
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
        }
        Debug.Log($"[EquipmentHandTargetChecks] Passed editable guard/rotation, exact straight punch and return, shared contact clock, " +
            $"0/±45/±90 view, local curves/Generic clip, single-hand fallback, safe masks/targets, cancellation and frozen-clock stability; " +
            $"max wrist error beyond physical reach={maxResidual * 1000f:F3}mm; reach-limited samples={reachLimited}.");
    }

    private static void RequireGuard(HandIKChain hand, EquipmentHandBinding binding) => Require(hand.weight == 1f &&
        Vector3.Distance(hand.target.position, binding.guard.position) < 0.00001f && Quaternion.Angle(hand.target.rotation, binding.guard.rotation) < 0.1f,
        "hand goal matches editable guard pose");

    private static void CheckContinuous(EquipmentSceneController equipment, PlayableGraph graph, EquipmentActionDefinition recipe, float yaw)
    {
        var slot = equipment.slots[1];
        var right = slot.rightHandPose;
        Vector3 guard = right.guard.localPosition, end = right.actionTarget.localPosition;
        foreach (int rate in new[] { 30, 60, 144 })
        {
            Frame(equipment, graph, 1f, true);
            int contacts = equipment.Runtime.Current.MeleeContactCount;
            Require(equipment.Runtime.TryMeleeAttack(), "start continuous hand test at " + rate);
            for (int frame = 0; frame < Mathf.CeilToInt(recipe.duration * rate) + 2; frame++)
            {
                float phase = frame / (recipe.duration * rate);
                equipment.look.SetLookAngles(Mathf.Sin(phase * Mathf.PI * 2f) * 85f, yaw + Mathf.Sin(phase * Mathf.PI) * 55f);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Frame(equipment, graph, 1f / rate, true);
                var action = equipment.Runtime.Current.Action;
                float amount = action.IsActive ? Mathf.Clamp01(recipe.rightHandMotion.reach.Evaluate(action.Progress)) *
                    EquipmentSceneController.ActionWeight(recipe, action.Progress) : 0f;
                Require(Vector3.Distance(equipment.handIK.rightHand.target.position,
                    Vector3.Lerp(right.guard.position, right.actionTarget.position, amount)) < 0.00002f,
                    "continuous target uses current camera frame and shared action clock at " + rate);
                RequireGuard(equipment.handIK.leftHand, slot.leftHandPose);
                Require(Vector3.Distance(guard, right.guard.localPosition) < 0.00001f &&
                    Vector3.Distance(end, right.actionTarget.localPosition) < 0.00001f, "playback never overwrites saved authoring points");
            }
            Require(!equipment.Runtime.Current.IsBusy && equipment.Runtime.Current.MeleeContactCount == contacts + 1,
                "continuous punch ends and commits once at " + rate);
        }
        Debug.Log("[EquipmentHandTargetChecks] Continuous native graph passed at 30/60/144 Hz with moving camera: " +
            "exact targets, stationary off hand, saved points intact, one contact and return to guard.");
    }

    private static float CheckWrist(HandIKChain hand, ref int limited)
    {
        Vector3 goal = hand.target.TransformPoint(hand.positionOffset);
        float upper = Vector3.Distance(hand.upperArm.position, hand.forearm.position);
        float lower = Vector3.Distance(hand.forearm.position, hand.hand.position);
        float distance = Vector3.Distance(hand.upperArm.position, goal);
        float unavoidable = Mathf.Max(0f, distance - upper - lower, Mathf.Abs(upper - lower) - distance);
        if (unavoidable > 0.003f) limited++;
        float residual = Mathf.Max(0f, Vector3.Distance(hand.hand.position, goal) - unavoidable);
        Require(residual < 0.003f, "hand follows target within physical arm reach");
        return residual;
    }

    private static void CheckMask(EquipmentSceneController equipment, bool left, bool right)
    {
        var layer = Get(equipment.spine, "_equipmentLayer");
        var mask = (AvatarMask)Get(layer, "_poseMask");
        Require(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm) == left &&
            mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm) == right &&
            mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers) && mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers),
            "runtime masks exclude only IK-owned arms and preserve fingers");
    }

    private static void SetLook(EquipmentSceneController equipment, PlayableGraph graph, float pitch, float yaw)
    {
        equipment.look.SetLookAngles(pitch, yaw);
        Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
        Frame(equipment, graph, 0f);
    }

    private static void Frame(EquipmentSceneController equipment, PlayableGraph graph, float dt, bool advanceGraph = false)
    {
        equipment.Step(dt); Invoke(equipment.spine, "Update");
        Invoke(equipment.weaponPose, "PreparePose", dt); Invoke(equipment.handIK, "Update");
        graph.Evaluate(advanceGraph ? dt : 0f); Invoke(equipment.spine, "LateUpdate");
        Invoke(equipment.look.cameraRig, "ApplyCamera", dt);
        Invoke(equipment.weaponPose, "ApplyPreparedPose", dt);
        equipment.ApplyMotion(); Invoke(equipment.handIK, "LateUpdate");
    }
    private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
    private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("[EquipmentHandTargetChecks] " + message); }
}
#endif
