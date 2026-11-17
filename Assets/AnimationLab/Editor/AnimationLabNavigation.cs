using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private void OnLostFocus() { CancelFrameDrag(); CancelTransitionBlockDrag(); CancelPathDrag(); CancelViewportDrag(); navigating = false; navigationKeys.Clear(); }

        private void AdvanceNavigation(float dt)
        {
            if (!navigating || firstPerson || preview.Camera == null) return;
            Vector3 move = Vector3.zero;
            if (navigationKeys.Contains(KeyCode.W)) move += preview.Camera.transform.forward;
            if (navigationKeys.Contains(KeyCode.S)) move -= preview.Camera.transform.forward;
            if (navigationKeys.Contains(KeyCode.D)) move += preview.Camera.transform.right;
            if (navigationKeys.Contains(KeyCode.A)) move -= preview.Camera.transform.right;
            if (navigationKeys.Contains(KeyCode.E)) move += Vector3.up;
            if (navigationKeys.Contains(KeyCode.Q)) move -= Vector3.up;
            float speed = navigationKeys.Contains(KeyCode.LeftShift) || navigationKeys.Contains(KeyCode.RightShift) ? 2 : 0.5f;
            preview.pan += move.normalized * dt * speed;
        }

        private void DrawCameraNavigation(Rect rect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseUp && e.button == 1) { navigating = false; navigationKeys.Clear(); }
            if (firstPerson) return;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown && e.button == 1)
                { navigating = true; EditorGUIUtility.editingTextField = false; GUI.FocusControl(null); e.Use(); }
                if (e.type == EventType.MouseDrag && (e.button == 2 || (e.button == 0 && e.shift)))
                {
                    float scale = 2 * preview.distance * Mathf.Tan(preview.Camera.fieldOfView * Mathf.Deg2Rad * 0.5f) / Mathf.Max(rect.height, 1);
                    preview.pan += (-preview.Camera.transform.right * e.delta.x + preview.Camera.transform.up * e.delta.y) * scale;
                    e.Use();
                }
                if (e.type == EventType.MouseDrag && e.button == 1 && navigating)
                { preview.orbit += new Vector2(e.delta.x, -e.delta.y) * 0.4f; e.Use(); }
            }
            if (navigating && (e.type == EventType.KeyDown || e.type == EventType.KeyUp))
            {
                if (e.keyCode == KeyCode.Escape) { navigating = false; navigationKeys.Clear(); e.Use(); return; }
                if (e.keyCode == KeyCode.W || e.keyCode == KeyCode.A || e.keyCode == KeyCode.S || e.keyCode == KeyCode.D || e.keyCode == KeyCode.Q || e.keyCode == KeyCode.E || e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift)
                { if (e.type == EventType.KeyDown) navigationKeys.Add(e.keyCode); else navigationKeys.Remove(e.keyCode); e.Use(); }
            }
        }

        private void FocusSelection()
        {
            firstPerson = false;
            Vector3 center = preview.Equipment.spine.animationRoot.position + Vector3.up * 1.35f;
            preview.pan = HandleWorld() - center;
            preview.distance = 0.45f;
        }
    }
}
