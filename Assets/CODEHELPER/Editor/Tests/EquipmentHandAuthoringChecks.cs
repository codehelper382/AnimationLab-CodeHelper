#if UNITY_EDITOR
using System;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using CodeHelper.ProceduralShooterFramework.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EquipmentHandAuthoringChecks
{
    public static void Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Isolated hand authoring check");
            SceneManager.MoveGameObjectToScene(root, scene);
            var source = root.AddComponent<EquipmentSceneController>();
            source.handIK = root.AddComponent<HandIKSolver>();
            source.look = root.AddComponent<PlayerLookController>();
            var view = new GameObject("View").transform;
            view.SetParent(root.transform, false);
            source.look.cameraRig = view.gameObject.AddComponent<CameraRigController>();
            source.look.cameraRig.cameraHolder = view;
            source.slots = new[] { new EquipmentSceneSlot() };
            var left = source.slots[0].leftHandPose;
            var right = source.slots[0].rightHandPose;
            left.enabled = false; left.weight = 0.35f;
            left.actionTarget = Point(view, "Existing end");
            left.elbowHint = Point(view, "Existing elbow");
            right.guard = Point(view, "Existing guard");
            Transform originalEnd = left.actionTarget, originalHint = left.elbowHint, originalGuard = right.guard;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            EquipmentHandTargetsEditor.CreateMissing(source, 0);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            Require(left.guard != null && right.actionTarget != null, "creator fills missing guard and endpoint independently");
            Require(left.actionTarget == originalEnd && left.elbowHint == originalHint && right.guard == originalGuard &&
                !left.enabled && Mathf.Abs(left.weight - 0.35f) < 0.0001f, "creator preserves existing bindings and settings");
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            EquipmentHandTargetsEditor.CreateMissing(source, 0);
            Require(root.GetComponentsInChildren<Transform>(true).Length == count, "creator is idempotent for complete points");
            Undo.PerformUndo();
            Require(source.slots[0].leftHandPose.guard == null && source.slots[0].rightHandPose.actionTarget == null &&
                source.slots[0].leftHandPose.actionTarget == originalEnd && source.slots[0].rightHandPose.guard == originalGuard,
                "Undo restores missing references without deleting original points");
            Undo.PerformRedo();
            Require(source.slots[0].leftHandPose.guard != null && source.slots[0].rightHandPose.actionTarget != null,
                "Redo recreates points and bindings together");
            source.handIK = null;
            EquipmentHandTargetsEditor.CreateMissing(source, 0); // Safe no-op for incomplete component dependencies.
            Require(root.GetComponentsInChildren<Transform>(true).Length == count, "missing solver does not create partial objects");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("[EquipmentHandAuthoringChecks] Passed: fill missing points, preserve references/settings, repeat-safe creation, Undo/Redo and missing-solver safety.");
    }

    private static Transform Point(Transform parent, string name)
    {
        var item = new GameObject(name).transform;
        item.SetParent(parent, false);
        return item;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("[EquipmentHandAuthoringChecks] " + message); }
}
#endif
