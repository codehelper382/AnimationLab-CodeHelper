using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HandGripToolkit
{
    /// <summary>
    /// Attachment point on a GrippableItem where a hand should go.
    /// Add it as a child transform of the weapon/prop and move/rotate it with
    /// the standard transform tools. Scene gizmos: red = Left hand grip,
    /// blue = Right hand grip; selecting it also shows the local axes and a
    /// label.
    /// </summary>
    [AddComponentMenu("HandGrip Toolkit/Grip Point")]
    [DisallowMultipleComponent]
    public class GripPoint : MonoBehaviour
    {
        [Tooltip("Which hand grips here.")]
        public HandSide side = HandSide.Right;

        [Tooltip("Finger pose applied when a hand grips here (optional).")]
        public HandPoseData pose;

        [Tooltip("Primary hands (trigger hand) drive the item; secondary hands only support it.")]
        public bool isPrimary = true;

        // ──────────────────────────────────────────────
        //  Gizmos
        // ──────────────────────────────────────────────

        private static readonly Color LeftColor = new Color(1f, 0.25f, 0.25f, 1f);  // red = left
        private static readonly Color RightColor = new Color(0.2f, 0.55f, 1f, 1f);  // blue = right

        private const float SmallRadius = 0.02f;     // always-visible marker size
        private const float SelectedRadius = 0.045f; // marker size while selected
        private const float AxisLength = 0.08f;      // local axis handle length

        private Color SideColor
        {
            get { return side == HandSide.Left ? LeftColor : RightColor; }
        }

        // Small marker so grips stay visible on the item at all times.
        private void OnDrawGizmos()
        {
            Gizmos.color = SideColor;
            Gizmos.DrawWireSphere(transform.position, SmallRadius);
        }

        // Bigger marker plus local axes and a label while selected.
        private void OnDrawGizmosSelected()
        {
            Vector3 position = transform.position;

            Gizmos.color = SideColor;
            Gizmos.DrawWireSphere(position, SelectedRadius);

            // Local axes (dimmed so they don't fight with the side color).
            DrawAxis(Vector3.right,    new Color(1f, 0.45f, 0.45f));
            DrawAxis(Vector3.up,      new Color(0.45f, 1f, 0.45f));
            DrawAxis(Vector3.forward, new Color(0.45f, 0.6f, 1f));

            // Tiny white core marking the exact anchor point.
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(position, SelectedRadius * 0.12f);

#if UNITY_EDITOR
            Handles.Label(position + Vector3.up * (SelectedRadius + 0.01f), BuildLabelText());
#endif
        }

        private void DrawAxis(Vector3 localDirection, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(transform.position, transform.TransformPoint(localDirection * AxisLength));
        }

#if UNITY_EDITOR
        private string BuildLabelText()
        {
            string role = isPrimary ? "Primary" : "Secondary";
            string poseName = pose != null ? pose.name : "no pose";
            return side + " Grip (" + role + ")\nPose: " + poseName;
        }
#endif
    }
}
