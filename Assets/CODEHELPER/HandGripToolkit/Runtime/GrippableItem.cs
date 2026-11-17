using System.Collections.Generic;
using UnityEngine;

namespace HandGripToolkit
{
    /// <summary>
    /// Root component for a weapon/prop that characters can grip.
    /// Auto-collects the GripPoints found under this object (refreshed on
    /// validate and on Awake). Call CollectGrips() if you add grips at runtime.
    /// </summary>
    [AddComponentMenu("HandGrip Toolkit/Grippable Item")]
    [DisallowMultipleComponent]
    public class GrippableItem : MonoBehaviour
    {
        [Tooltip("Grip points found under this object (auto-collected).")]
        [SerializeField] private List<GripPoint> grips = new List<GripPoint>();

        // ──────────────────────────────────────────────
        //  Accessors
        // ──────────────────────────────────────────────

        /// <summary>Grip points under this item (auto-collected when still empty).</summary>
        public IReadOnlyList<GripPoint> Grips
        {
            get { EnsureCollected(); return grips; }
        }

        /// <summary>True when the item has at least one grip for each hand.</summary>
        public bool IsTwoHanded
        {
            get { return GetGrip(HandSide.Left) != null && GetGrip(HandSide.Right) != null; }
        }

        // ──────────────────────────────────────────────
        //  Queries
        // ──────────────────────────────────────────────

        /// <summary>
        /// First grip for the given hand side, preferring primary grips.
        /// Returns null when this item has no grip for that side.
        /// </summary>
        public GripPoint GetGrip(HandSide side)
        {
            EnsureCollected();

            GripPoint firstOfSide = null;
            for (int i = 0; i < grips.Count; i++)
            {
                GripPoint grip = grips[i];
                if (grip == null || grip.side != side)
                    continue;
                if (grip.isPrimary)
                    return grip; // Prefer the primary grip.
                if (firstOfSide == null)
                    firstOfSide = grip;
            }
            return firstOfSide;
        }

        /// <summary>First primary grip, falling back to the first grip of any side.</summary>
        public GripPoint GetPrimaryGrip()
        {
            EnsureCollected();

            GripPoint fallback = null;
            for (int i = 0; i < grips.Count; i++)
            {
                GripPoint grip = grips[i];
                if (grip == null)
                    continue;
                if (grip.isPrimary)
                    return grip;
                if (fallback == null)
                    fallback = grip;
            }
            return fallback;
        }

        /// <summary>Re-scans children for GripPoints (inactive children included).</summary>
        public void CollectGrips()
        {
            grips = new List<GripPoint>(GetComponentsInChildren<GripPoint>(true));
        }

        // Queries may run before Awake (e.g. from another component's Awake),
        // so they collect lazily while the list is still empty.
        private void EnsureCollected()
        {
            if (grips.Count == 0)
                CollectGrips();
        }

        // Keep the list in sync while authoring.
        private void Reset() { CollectGrips(); }
        private void OnValidate() { CollectGrips(); }

        // Refresh at runtime (prefabs may assemble their hierarchy after this).
        private void Awake() { CollectGrips(); }
    }
}
