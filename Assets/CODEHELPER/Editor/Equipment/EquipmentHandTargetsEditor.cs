#if UNITY_EDITOR
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    [CustomEditor(typeof(EquipmentSceneController))]
    public sealed class EquipmentHandTargetsEditor : UnityEditor.Editor
    {
        private int _slot;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var source = (EquipmentSceneController)target;
            if (source.slots == null || source.slots.Length == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editable hand targets", EditorStyles.boldLabel);
            _slot = EditorGUILayout.IntSlider("Slot", _slot, 0, source.slots.Length - 1);
            var slot = source.slots[_slot];
            EditorGUILayout.HelpBox("Guard = resting wrist. Action Target = contact/end point. Move/rotate the Empty objects, not the bones. " +
                "Action hand curves control the path; finger clips remain active. Save adjustments outside Play Mode.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying || slot == null || source.handIK == null || source.look?.cameraRig == null))
                if (GUILayout.Button("Create missing guard/action points")) CreateMissing(source, _slot);
            if (slot == null) return;
            if (GUILayout.Button("Select guard points")) Select(slot.leftHandPose?.guard, slot.rightHandPose?.guard);
            if (GUILayout.Button("Select action points")) Select(slot.leftHandPose?.actionTarget, slot.rightHandPose?.actionTarget);
        }

        private static void Select(Transform left, Transform right)
        {
            if (left != null && right != null) Selection.objects = new Object[] { left.gameObject, right.gameObject };
            else if (left != null) Selection.activeGameObject = left.gameObject;
            else if (right != null) Selection.activeGameObject = right.gameObject;
        }

        public static void CreateMissing(EquipmentSceneController source, int index)
        {
            if (source == null || source.handIK == null || source.look?.cameraRig == null || source.slots == null || index < 0 || index >= source.slots.Length) return;
            var slot = source.slots[index];
            if (slot == null || (Complete(slot.leftHandPose) && Complete(slot.rightHandPose))) return;
            Transform view = source.look.cameraRig.cameraHolder != null ? source.look.cameraRig.cameraHolder : source.look.cameraRig.transform;
            if ((source.handIK.leftHand.upperArm != null && view.IsChildOf(source.handIK.leftHand.upperArm)) ||
                (source.handIK.rightHand.upperArm != null && view.IsChildOf(source.handIK.rightHand.upperArm)))
                throw new System.InvalidOperationException("View anchor must be outside the arms.");
            Undo.RecordObject(source, "Create equipment hand targets");
            Transform root = slot.leftHandPose?.guard != null ? slot.leftHandPose.guard.parent :
                slot.rightHandPose?.guard != null ? slot.rightHandPose.guard.parent : null;
            if (root == null)
            {
                root = new GameObject((slot.definition != null ? slot.definition.displayName : "Equipment") + " Hand Targets").transform;
                root.SetParent(view, false);
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create equipment hand targets");
            }
            slot.leftHandPose = CreateBinding(slot.leftHandPose, root, "Left", source.handIK.leftHand.hand, -0.15f);
            slot.rightHandPose = CreateBinding(slot.rightHandPose, root, "Right", source.handIK.rightHand.hand, 0.15f);
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
        }

        private static bool Complete(EquipmentHandBinding binding) => binding?.guard != null && binding.actionTarget != null;

        private static EquipmentHandBinding CreateBinding(EquipmentHandBinding current, Transform root, string side, Transform wrist, float x)
        {
            if (current == null) current = new EquipmentHandBinding();
            if (current.guard == null)
            {
                current.guard = new GameObject(side + " Guard").transform;
                current.guard.SetParent(root, false);
                if (wrist != null) current.guard.SetPositionAndRotation(wrist.position, wrist.rotation);
                else current.guard.localPosition = new Vector3(x, -0.18f, 0.3f);
                Undo.RegisterCreatedObjectUndo(current.guard.gameObject, "Create hand guard");
            }
            if (current.actionTarget == null)
            {
                current.actionTarget = new GameObject(side + " Action Target").transform;
                current.actionTarget.SetParent(root, false);
                current.actionTarget.SetPositionAndRotation(current.guard.position + root.forward * 0.16f, current.guard.rotation);
                Undo.RegisterCreatedObjectUndo(current.actionTarget.gameObject, "Create hand action point");
            }
            return current;
        }

        private void OnSceneGUI()
        {
            var source = (EquipmentSceneController)target;
            if (source.slots == null || _slot < 0 || _slot >= source.slots.Length || source.slots[_slot] == null) return;
            Draw(source.slots[_slot].leftHandPose, Color.cyan, "Left");
            Draw(source.slots[_slot].rightHandPose, Color.yellow, "Right");
        }

        private static void Draw(EquipmentHandBinding binding, Color color, string label)
        {
            if (binding?.guard == null) return;
            Handles.color = color;
            Handles.DrawWireDisc(binding.guard.position, binding.guard.forward, 0.025f);
            Handles.Label(binding.guard.position, label + " Guard");
            if (binding.actionTarget == null) return;
            Handles.DrawDottedLine(binding.guard.position, binding.actionTarget.position, 4f);
            Handles.DrawWireDisc(binding.actionTarget.position, binding.actionTarget.forward, 0.025f);
            Handles.Label(binding.actionTarget.position, label + " Action");
        }
    }
}
#endif
