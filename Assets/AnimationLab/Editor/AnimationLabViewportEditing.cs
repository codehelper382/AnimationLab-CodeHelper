using UnityEditor;
using UnityEngine;
using CodeHelper.ProceduralShooterFramework.Equipment;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private int viewportDragControl, viewportAxis = -1;
        private Vector3 viewportWorldStart, viewportDirection, viewportOldPosition, viewportOldRotation;
        private Vector2 viewportScreenDirection;
        private Vector2 viewportGizmoWindowPoint;
        private Vector2[] viewportAxisWindowEnds=new Vector2[3];
        private bool viewportOldWrist, viewportOldWeapon, viewportOldObject, viewportWristApproved, viewportWeaponApproved, viewportObjectApproved;
        private EquipmentHandBinding viewportLeftBinding,viewportRightBinding;
        private bool BeginViewportPositionEdit()
        {
            viewportOldPosition = keyPosition; viewportOldRotation = keyRotation;
            viewportOldWrist = previewingKey; viewportOldWeapon = weaponPending; viewportOldObject = objectPending;
            viewportWristApproved = wristReplacementApproved; viewportWeaponApproved = weaponReplacementApproved; viewportObjectApproved = objectReplacementApproved;
            viewportLeftBinding=preview.Slot.leftHandPose;viewportRightBinding=preview.Slot.rightHandPose;
            return BeginPositionEdit();
        }
        private void CancelViewportDrag()
        {
            if (viewportDragControl == 0) return;
            if (GUIUtility.hotControl == viewportDragControl) GUIUtility.hotControl = 0;
            keyPosition = viewportOldPosition; keyRotation = viewportOldRotation;
            previewingKey = viewportOldWrist; weaponPending = viewportOldWeapon; objectPending = viewportOldObject;
            wristReplacementApproved = viewportWristApproved; weaponReplacementApproved = viewportWeaponApproved; objectReplacementApproved = viewportObjectApproved;
            viewportDragControl = 0;
            if(preview!=null&&draft!=null)
            {
                preview.Slot.leftHandPose=viewportLeftBinding;preview.Slot.rightHandPose=viewportRightBinding;
                if(!quarantinedRecovery)Safe(()=>EvaluateNow(Pending));
            }
        }
        private Vector2 ViewPoint(Rect rect, Vector3 world)
        {
            var view = preview.Camera.WorldToViewportPoint(world);
            return new Vector2(rect.x + view.x * rect.width, rect.y + (1 - view.y) * rect.height);
        }
        private void DrawPoseGizmo(Rect rect)
        {
            int id = GUIUtility.GetControlID("LabWristHandle".GetHashCode(), FocusType.Passive);
            var evt = Event.current;
            if (viewportDragControl != 0 && GUIUtility.hotControl != viewportDragControl) CancelViewportDrag();
            if (viewportDragControl != 0 && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            { CancelViewportDrag(); evt.Use(); return; }
            if (viewportDragControl != 0 && evt.type == EventType.MouseUp)
            { viewportDragControl = 0; GUIUtility.hotControl = 0; evt.Use(); return; }
            if (viewportDragControl != 0 && evt.type == EventType.MouseDrag)
            {
                Vector2 delta = evt.mousePosition - dragMouse;
                Vector3 displacement;
                if (viewportAxis >= 0)
                    displacement = viewportDirection * (Vector2.Dot(delta, viewportScreenDirection) / viewportScreenDirection.sqrMagnitude) * .1f;
                else
                {
                    var view = new Vector3(dragViewport.x + delta.x / rect.width, dragViewport.y - delta.y / rect.height, handleDepth);
                    displacement = preview.Camera.ViewportToWorldPoint(view) - viewportWorldStart;
                }
                var reference = LocalReference;
                keyPosition = dragPosition + (reference != null ? reference.InverseTransformVector(displacement) : displacement);
                evt.Use();
            }
            if (preview.Camera == null || editObject && ObjectTarget == null) return;
            var world = HandleWorld();
            var viewPoint = preview.Camera.WorldToViewportPoint(world);
            if (viewPoint.z <= 0) return;
            Vector2 point = ViewPoint(rect, world);
            if(evt.type==EventType.Repaint)
            {
                viewportGizmoWindowPoint=GUIUtility.GUIToScreenPoint(point)-position.position;
                for(int i=0;i<3;i++)viewportAxisWindowEnds[i]=Vector2.zero;
            }
            if (!rect.Contains(point)) return;
            var referenceFrame = LocalReference;
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            Color[] colors = { new Color(1,.3f,.3f), new Color(.3f,1,.4f), new Color(.35f,.65f,1) };
            string[] names = { "X", "Y", "Z" };
            int hit = -2; float best = 9;
            Vector3 chosenDirection = Vector3.zero; Vector2 chosenProjection = Vector2.zero;
            Handles.BeginGUI();
            for (int i = 0; i < 3; i++)
            {
                Vector3 direction = worldSpace || referenceFrame == null ? axes[i] : referenceFrame.TransformVector(axes[i]).normalized;
                Vector2 projected = ViewPoint(rect, world + direction * .1f) - point;
                if (projected.sqrMagnitude < 4) continue; // Axis nearly faces the camera; use fields or another view.
                // Different lengths keep coincident projected axes individually selectable.
                Vector2 end = point + projected.normalized * (48+i*9);
                if (!rect.Contains(end)) continue;
                if(evt.type==EventType.Repaint)viewportAxisWindowEnds[i]=GUIUtility.GUIToScreenPoint(end)-position.position;
                Handles.color = colors[i]; Handles.DrawAAPolyLine(3, point, end);
                Handles.DrawSolidDisc(end, Vector3.forward, 4);
                GUI.Label(new Rect(end.x + 5, end.y - 9, 25, 20), names[i], EditorStyles.whiteMiniLabel);
                float distance = HandleUtility.DistancePointToLineSegment(evt.mousePosition, point + projected.normalized * 14, end);
                if (distance < best)
                { best = distance; hit = i; chosenDirection = direction; chosenProjection = projected; }
            }
            Handles.color = editObject ? Color.cyan : editWeapon ? new Color(1,.55f,.15f) : Color.yellow;
            Handles.DrawSolidDisc(point, Vector3.forward, 7);
            Handles.EndGUI();
            if (Vector2.Distance(evt.mousePosition, point) < 11) hit = -1;
            GUI.Label(new Rect(point.x + 10, point.y + 48, 250, 22), worldSpace ? "Ejes globales · Esc cancela" : "Ejes locales · Esc cancela", EditorStyles.whiteMiniLabel);
            if (evt.type != EventType.MouseDown || evt.button != 0 || evt.shift || hit == -2) return;
            Safe(() =>
            {
                if (!BeginViewportPositionEdit()) return;
                EvaluateNow(true);
                viewportAxis = hit; viewportDirection = chosenDirection; viewportScreenDirection = chosenProjection;
                viewportWorldStart = HandleWorld(); dragViewport = preview.Camera.WorldToViewportPoint(viewportWorldStart);
                handleDepth = dragViewport.z; dragMouse = evt.mousePosition; dragPosition = keyPosition;
                viewportDragControl = id; GUIUtility.hotControl = id;
            });
            evt.Use();
        }
    }
}
