#if UNITY_EDITOR
using System;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated regression checks of our standalone IK. No third-party assemblies required.</summary>
public static class HandIKChecks
{
    [MenuItem("Tools/CodeHelper/Run Hand IK Checks")]
    public static void Run()
    {
        CheckSolveAndRestore();
        CheckValidationAndSingleHand();
        CheckCameraHolderTargets();
        CheckEdgeCases();
        CheckBendContinuity();
        Debug.Log("[HandIKChecks] Passed: reachable grips, wrist rotation, no-hint solving, hints, " +
                  "partial/zero weights, no drift, external writes, target replacement, unreachable grips, " +
                  "invalid hierarchy/cycles, single-hand weapon, camera-holder frame ordering, " +
                  "straight/folded chains, no-hint bend continuity, already-folded poses, " +
                  "offsets, rotated roots and positive uniform scale. " +
                  "Real character animation and reloads still require Play Mode verification.");
    }

    private static void CheckSolveAndRestore()
    {
        GameObject root = CreateRoot();
        try
        {
            HandIKSolver solver = root.AddComponent<HandIKSolver>();
            HandIKChain arm = CreateArm(root.transform, "Right", Vector3.zero);
            solver.rightHand = arm;
            Quaternion upperBefore = arm.upperArm.localRotation;
            Quaternion midBefore = arm.forearm.localRotation;
            Quaternion tipBefore = arm.hand.localRotation;
            Require(solver.ValidateSetup() && solver.IsRightHandReady, "valid generic arm");
            arm.target.rotation = Quaternion.Euler(12f, 35f, 9f);
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, arm.target.position, "reachable grip position");
            Same(arm.hand.rotation, arm.target.rotation, "wrist rotation");
            for (int i = 0; i < 100; i++)
            {
                Invoke(solver, "Update");
                Invoke(solver, "LateUpdate");
                Near(arm.hand.position, arm.target.position, "no accumulated error without Animator");
            }
            solver.alpha = 0f;
            Invoke(solver, "Update");
            Invoke(solver, "LateUpdate");
            Same(arm.upperArm.localRotation, upperBefore, "zero alpha restores upper arm");
            Same(arm.forearm.localRotation, midBefore, "zero alpha restores forearm");
            Same(arm.hand.localRotation, tipBefore, "zero alpha restores wrist");

            solver.alpha = 1f;
            arm.positionWeight = 0f;
            Vector3 originalPosition = arm.hand.position;
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, originalPosition, "rotation-only leaves position unchanged");
            Same(arm.hand.rotation, arm.target.rotation, "rotation-only aligns wrist");
            solver.RestorePose();

            arm.positionWeight = 0.5f;
            Vector3 partialTarget = Vector3.Lerp(arm.hand.position, arm.target.position, 0.5f);
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, partialTarget, "partial position weight");
            solver.RestorePose();

            arm.positionWeight = 1f;
            Transform newGrip = Child(root.transform, "NewGrip");
            newGrip.position = arm.upperArm.position + new Vector3(0.5f, 0.15f, -0.25f);
            arm.target = newGrip;
            arm.hint = Child(root.transform, "Hint");
            arm.hint.position = arm.upperArm.position + new Vector3(0f, -1f, 0.5f);
            Invoke(solver, "LateUpdate"); // Changes detected, no explicit ValidateSetup needed.
            Near(arm.hand.position, newGrip.position, "target replacement and hinted solve");
            Quaternion external = Quaternion.Euler(15f, 20f, 25f);
            arm.forearm.localRotation = external;
            solver.RestorePose();
            Same(arm.forearm.localRotation, external, "external forearm write is not overwritten");
            arm.forearm.localRotation = midBefore;

            float reach = Vector3.Distance(arm.upperArm.position, arm.forearm.position) +
                          Vector3.Distance(arm.forearm.position, arm.hand.position);
            newGrip.position = arm.upperArm.position + new Vector3(20f, 4f, 5f);
            Invoke(solver, "LateUpdate");
            Require(Vector3.Distance(arm.upperArm.position, arm.hand.position) <= reach + 0.001f,
                "unreachable target does not stretch bones");
            Require(!float.IsNaN(arm.hand.rotation.w), "unreachable solve is finite");
            solver.RestorePose();
            arm.forearm.localPosition = Vector3.zero;
            Invoke(solver, "LateUpdate");
            Require(!float.IsNaN(arm.hand.rotation.w), "zero-length segment is safely ignored");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CheckValidationAndSingleHand()
    {
        GameObject root = CreateRoot();
        try
        {
            var solver = root.AddComponent<HandIKSolver>();
            solver.rightHand = CreateArm(root.transform, "Right", Vector3.zero);
            solver.leftHand = CreateArm(root.transform, "Left", new Vector3(0f, 0f, 0.3f));
            solver.rightHand.target.SetParent(solver.rightHand.hand, true);
            solver.ValidateSetup(); // Expected warning: target under its solved arm.
            Require(!solver.IsRightHandReady && solver.IsLeftHandReady, "reject self-driven grip without disabling other arm");

            Transform animatedWeaponGrip = solver.rightHand.target;
            solver.rightHand.target = null; // Animation owns right arm/weapon; only left is solved.
            animatedWeaponGrip.position = solver.leftHand.upperArm.position + new Vector3(0.6f, 0.1f, 0f);
            solver.leftHand.target = animatedWeaponGrip;
            Require(solver.ValidateSetup() && solver.IsLeftHandReady, "single left-hand IK with right hand driven by animation");
            Invoke(solver, "LateUpdate");
            Near(solver.leftHand.hand.position, animatedWeaponGrip.position, "left follows weapon on unsolved right hand");
            solver.RestorePose();

            solver.leftHand.forearm = solver.leftHand.upperArm;
            Require(!solver.ValidateSetup(), "reject invalid bone chain"); // Expected warning.
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CheckCameraHolderTargets()
    {
        GameObject root = CreateRoot();
        try
        {
            Transform head = Child(root.transform, "Head");
            Transform rig = Child(head, "CameraRig");
            Transform holder = Child(rig, "Holder");
            Transform camera = Child(holder, "Camera");
            camera.gameObject.AddComponent<Camera>();
            CameraRigController view = rig.gameObject.AddComponent<CameraRigController>();
            view.characterRoot = root.transform;
            view.headBone = head;
            view.cameraHolder = holder;
            view.realCamera = camera;
            view.useSpineLean = false;
            Require(view.ValidateSetup(), "camera setup");

            var solver = root.AddComponent<HandIKSolver>();
            HandIKChain arm = CreateArm(root.transform, "Right", Vector3.zero);
            solver.rightHand = arm;
            arm.target.SetParent(holder, false);
            arm.target.localPosition = new Vector3(0.6f, 0.15f, 0.15f);
            Require(solver.ValidateSetup(), "camera-child grip setup");
            Require(ShooterExecutionOrder.FinalHandIK > ShooterExecutionOrder.Camera, "hands execute after camera");
            for (int i = 0; i < 20; i++)
            {
                Invoke(solver, "Update");
                view.SetLookInputs(i * 0.5f, i * 2f);
                Invoke(view, "LateUpdate");
                Invoke(solver, "LateUpdate");
                Near(arm.hand.position, arm.target.position, "same-frame grip under moving camera holder");
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static HandIKChain CreateArm(Transform parent, string prefix, Vector3 origin)
    {
        Transform upper = Child(parent, prefix + "Upper");
        upper.localPosition = origin;
        Transform mid = Child(upper, prefix + "Forearm");
        mid.localPosition = new Vector3(0.55f, -0.15f, 0f);
        Transform tip = Child(mid, prefix + "Hand");
        tip.localPosition = new Vector3(0.4f, 0.1f, 0f);
        Transform target = Child(parent, prefix + "Grip");
        target.position = upper.position + new Vector3(0.6f, 0.25f, 0.2f);
        return new HandIKChain { upperArm = upper, forearm = mid, hand = tip, target = target };
    }

    private static void CheckEdgeCases()
    {
        GameObject root = CreateRoot();
        try
        {
            var solver = root.AddComponent<HandIKSolver>();
            HandIKChain arm = CreateArm(root.transform, "Right", Vector3.zero);
            solver.rightHand = arm;
            arm.forearm.localPosition = Vector3.right * 0.5f;
            arm.hand.localPosition = Vector3.right * 0.5f; // Start completely straight.
            arm.hint = Child(root.transform, "ElbowHint");
            arm.hint.position = Vector3.up;
            Require(solver.ValidateSetup(), "straight arm setup");
            arm.target.position = new Vector3(0.5f, 0f, 0f);
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, arm.target.position, "straight arm bends to reachable grip");
            Require(arm.forearm.position.y > 0f, "hint selects bend side");
            solver.RestorePose();

            arm.hint = null;
            arm.target.position = arm.upperArm.position; // Fully folded equal-length chain.
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, arm.target.position, "target at shoulder does not divide by zero");
            Require(IsFinite(arm.hand.rotation), "fully folded wrist rotation finite");
            solver.RestorePose();

            arm.target.position = new Vector3(-0.5f, 0f, 0f);
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, arm.target.position, "target opposite original direction");
            solver.RestorePose();

            // Exercise transform composition and target-local offsets in a rotated/scaled hierarchy.
            root.transform.rotation = Quaternion.Euler(20f, 50f, 10f);
            root.transform.localScale = Vector3.one * 1.5f;
            arm.forearm.localPosition = new Vector3(0.55f, -0.15f, 0f);
            arm.hand.localPosition = new Vector3(0.4f, 0.1f, 0f);
            arm.target.localPosition = new Vector3(0.5f, 0.2f, 0.1f);
            arm.target.localRotation = Quaternion.Euler(7f, 12f, 30f);
            arm.positionOffset = new Vector3(0.02f, 0.03f, 0.01f);
            arm.rotationOffsetEuler = new Vector3(10f, 0f, 15f);
            Require(solver.ValidateSetup(), "rotated root and uniform scale setup");
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, arm.target.TransformPoint(arm.positionOffset), "target-local position offset");
            Same(arm.hand.rotation, arm.target.rotation * Quaternion.Euler(arm.rotationOffsetEuler),
                "target-local rotation offset");
            solver.RestorePose();

            Quaternion animatedWrist = arm.hand.rotation;
            arm.rotationWeight = 0f;
            Invoke(solver, "LateUpdate");
            Same(arm.hand.rotation, animatedWrist, "zero rotation weight retains original world wrist orientation");
            solver.RestorePose();

            // The pure solver rejects nonfinite geometry instead of writing bad rotations to the scene.
            Require(!HandIKMath.TrySolve(Vector3.zero, Vector3.right, Vector3.right * 2f,
                Quaternion.identity, Quaternion.identity, new Vector3(float.NaN, 0f, 0f),
                false, Vector3.zero, 0f, out _, out _), "nonfinite goal is rejected");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static bool IsFinite(Quaternion rotation) => !float.IsNaN(rotation.x) && !float.IsNaN(rotation.y) &&
        !float.IsNaN(rotation.z) && !float.IsNaN(rotation.w) && !float.IsInfinity(rotation.x) &&
        !float.IsInfinity(rotation.y) && !float.IsInfinity(rotation.z) && !float.IsInfinity(rotation.w);

    private static void CheckBendContinuity()
    {
        GameObject root = CreateRoot();
        try
        {
            var solver = root.AddComponent<HandIKSolver>();
            HandIKChain arm = CreateArm(root.transform, "Right", Vector3.zero);
            solver.rightHand = arm;
            arm.forearm.localPosition = new Vector3(0.5f, 0f, 0f);
            arm.hand.localPosition = new Vector3(0f, 0.5f, 0f);
            Require(solver.ValidateSetup(), "bend continuity setup");
            arm.target.position = new Vector3(0.6f, 0.001f, 0f);
            Invoke(solver, "LateUpdate");
            Vector3 firstElbow = arm.forearm.position;
            solver.RestorePose();
            arm.target.position = new Vector3(0.6f, -0.001f, 0f);
            Invoke(solver, "LateUpdate");
            Near(arm.hand.position, arm.target.position, "crossing goal still reached");
            Require(Vector3.Distance(firstElbow, arm.forearm.position) < 0.01f,
                "no-hint elbow does not flip across shoulder-elbow direction");
            solver.RestorePose();

            // Same geometry scaled down: directional degeneracy must not depend on bone length.
            root.transform.localScale = Vector3.one * 0.004f;
            Require(solver.ValidateSetup(), "small uniformly scaled arm setup");
            arm.target.localPosition = new Vector3(0.6f, 0.006f, 0f);
            Invoke(solver, "LateUpdate");
            Vector3 smallElbow = arm.forearm.position;
            solver.RestorePose();
            arm.target.localPosition = new Vector3(0.6f, -0.006f, 0f);
            Invoke(solver, "LateUpdate");
            Require(Vector3.Distance(smallElbow, arm.forearm.position) < 0.0002f,
                "small arm retains bend continuity across target path");
            Require(Vector3.Distance(arm.hand.position, arm.target.position) < 0.00001f,
                "small arm still reaches target accurately");
            solver.RestorePose();
            root.transform.localScale = Vector3.one;

            arm.hand.localPosition = new Vector3(-0.5f, 0f, 0f); // Already folded, wrist at shoulder.
            arm.target.position = arm.upperArm.position;
            Vector3 foldedElbow = arm.forearm.position;
            Quaternion upperBefore = arm.upperArm.rotation;
            Quaternion forearmBefore = arm.forearm.rotation;
            Invoke(solver, "LateUpdate");
            Near(arm.forearm.position, foldedElbow, "already-folded elbow stays still");
            Near(arm.hand.position, arm.target.position, "already-folded wrist stays at shoulder");
            Same(arm.upperArm.rotation, upperBefore, "already-folded upper rotation unchanged");
            Same(arm.forearm.rotation, forearmBefore, "already-folded forearm rotation unchanged");
            solver.RestorePose();

            arm.hint = Child(root.transform, "FoldedHint");
            arm.hint.position = foldedElbow;
            Invoke(solver, "LateUpdate");
            Near(arm.forearm.position, foldedElbow, "hinted full fold preserves aligned elbow direction");
            Near(arm.hand.position, arm.target.position, "hinted folded wrist near shoulder");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static GameObject CreateRoot()
    {
        var root = new GameObject("Hand IK check (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);
        return root;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void Invoke(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Near(Vector3 actual, Vector3 expected, string label) =>
        Require(Vector3.Distance(actual, expected) < 0.002f, label);
    private static void Same(Quaternion actual, Quaternion expected, string label) =>
        Require(Quaternion.Angle(actual, expected) < 0.1f, label);
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[HandIKChecks] Failed: " + label);
    }
}
#endif
