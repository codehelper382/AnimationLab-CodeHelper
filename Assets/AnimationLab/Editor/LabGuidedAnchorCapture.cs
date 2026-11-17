using System;
using System.Linq;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    /// <summary>
    /// Converts a preview-only hand pose into portable local values. Transform references are never retained.
    /// </summary>
    internal static class LabGuidedAnchorCapture
    {
        internal static LabGuidedAnchor CaptureHand(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase, HandSide side,
            Transform guard, Transform wrist, float normalizedTime)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (wrist == null) throw new ArgumentNullException(nameof(wrist));
            if (!Finite(normalizedTime) || normalizedTime < 0f || normalizedTime > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedTime), "El tiempo debe estar normalizado entre 0 y 1.");
            if ((phase.channels & LabGuidedChannels.Wrist) == 0)
                throw new InvalidOperationException("El canal de muñeca no está seleccionado en esta fase.");

            var hand = side == HandSide.Left ? LabGuidedHands.Left : LabGuidedHands.Right;
            if ((phase.hands & hand) == 0)
                throw new InvalidOperationException("La mano que se intenta capturar no está asignada a esta fase.");
            if (plan.calibration == null)
                throw new InvalidOperationException("La secuencia necesita una calibración antes de capturar anclajes.");
            if (plan.calibration.anchors == null)
                plan.calibration.anchors = new System.Collections.Generic.List<LabGuidedAnchor>();

            var anchor = plan.calibration.anchors.FirstOrDefault(a => a != null
                && a.phaseId == phase.id && LabGuidedAnchorTools.EffectiveKind(a) == LabGuidedAnchorKind.Wrist
                && a.hand == hand && a.space == LabGuidedAnchorSpace.Guard
                && Mathf.Abs(a.time - normalizedTime) <= LabTimeline.TimeEpsilon);
            if (anchor == null)
            {
                anchor = new LabGuidedAnchor();
                plan.calibration.anchors.Add(anchor);
            }

            anchor.phaseId = phase.id;
            anchor.role = phase.role.ToString();
            anchor.kind = LabGuidedAnchorKind.Wrist;
            anchor.pieceId = phase.pieceId;
            anchor.hand = hand;
            anchor.objectId = null;
            anchor.space = LabGuidedAnchorSpace.Guard;
            anchor.time = normalizedTime;
            anchor.position = guard.InverseTransformPoint(wrist.position);
            anchor.rotation = Quaternion.Inverse(guard.rotation) * wrist.rotation;
            return anchor;
        }

        internal static LabGuidedAnchor CapturePiece(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase,
            Transform piece, string objectId, float normalizedTime) =>
            CaptureBoundObject(plan, phase, piece, objectId, normalizedTime, LabGuidedAnchorKind.Piece);

        internal static LabGuidedAnchor CaptureMechanism(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase,
            Transform mechanism, string objectId, float normalizedTime) =>
            CaptureBoundObject(plan, phase, mechanism, objectId, normalizedTime, LabGuidedAnchorKind.Mechanism);

        private static LabGuidedAnchor CaptureBoundObject(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase,
            Transform target, string objectId, float normalizedTime, LabGuidedAnchorKind kind)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(phase.pieceId))
                throw new InvalidOperationException("La fase necesita una pieza o mecanismo objetivo.");
            LabGuidedChannels requiredChannel = kind == LabGuidedAnchorKind.Mechanism
                ? LabGuidedChannels.Mechanism : LabGuidedChannels.Piece;
            if ((phase.channels & requiredChannel) == 0)
                throw new InvalidOperationException("El canal del anclaje no está seleccionado en esta fase.");
            if (!ValidObjectId(objectId))
                throw new InvalidOperationException("El objetivo necesita un binding part: o prop: resuelto.");
            if (target.parent == null)
                throw new InvalidOperationException("La pose necesita un padre local estable.");
            if (!Finite(normalizedTime) || normalizedTime < 0f || normalizedTime > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedTime), "El tiempo debe estar normalizado entre 0 y 1.");
            if (plan.calibration == null)
                throw new InvalidOperationException("La secuencia necesita una calibración antes de capturar anclajes.");
            var binding = plan.calibration.bindings?.FirstOrDefault(b => b != null && b.pieceId == phase.pieceId);
            if (binding == null || !string.Equals(binding.objectId, objectId, StringComparison.Ordinal))
                throw new InvalidOperationException("La referencia capturada debe coincidir con el binding actual de la pieza.");
            if (plan.calibration.anchors == null)
                plan.calibration.anchors = new System.Collections.Generic.List<LabGuidedAnchor>();

            var anchor = plan.calibration.anchors.FirstOrDefault(a => a != null
                && a.phaseId == phase.id && LabGuidedAnchorTools.EffectiveKind(a) == kind
                && a.space == LabGuidedAnchorSpace.Parent && a.objectId == objectId
                && Mathf.Abs(a.time - normalizedTime) <= LabTimeline.TimeEpsilon);
            if (anchor == null)
            {
                anchor = new LabGuidedAnchor();
                plan.calibration.anchors.Add(anchor);
            }

            anchor.phaseId = phase.id;
            anchor.role = phase.role.ToString();
            anchor.kind = kind;
            anchor.pieceId = phase.pieceId;
            anchor.hand = LabGuidedHands.None;
            anchor.objectId = objectId;
            anchor.space = LabGuidedAnchorSpace.Parent;
            anchor.time = normalizedTime;
            anchor.position = target.localPosition;
            anchor.rotation = target.localRotation;
            return anchor;
        }

        internal static LabGuidedAnchor CaptureWeapon(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase,
            Transform weaponPoseRoot, float normalizedTime)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            if (weaponPoseRoot == null) throw new ArgumentNullException(nameof(weaponPoseRoot));
            if ((phase.channels & LabGuidedChannels.Weapon) == 0)
                throw new InvalidOperationException("El canal de arma no está seleccionado en esta fase.");
            if (weaponPoseRoot.parent == null)
                throw new InvalidOperationException("La pose del holder del arma necesita un padre local estable.");
            if (!Finite(normalizedTime) || normalizedTime < 0f || normalizedTime > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedTime), "El tiempo debe estar normalizado entre 0 y 1.");
            if (plan.calibration == null)
                throw new InvalidOperationException("La secuencia necesita una calibración antes de capturar anclajes.");
            if (plan.calibration.anchors == null)
                plan.calibration.anchors = new System.Collections.Generic.List<LabGuidedAnchor>();

            var anchor = plan.calibration.anchors.FirstOrDefault(a => a != null
                && a.phaseId == phase.id && LabGuidedAnchorTools.EffectiveKind(a) == LabGuidedAnchorKind.Weapon
                && a.space == LabGuidedAnchorSpace.Parent
                && Mathf.Abs(a.time - normalizedTime) <= LabTimeline.TimeEpsilon);
            if (anchor == null)
            {
                anchor = new LabGuidedAnchor();
                plan.calibration.anchors.Add(anchor);
            }

            anchor.phaseId = phase.id;
            anchor.role = phase.role.ToString();
            anchor.kind = LabGuidedAnchorKind.Weapon;
            anchor.pieceId = null;
            anchor.hand = LabGuidedHands.None;
            anchor.objectId = null;
            anchor.space = LabGuidedAnchorSpace.Parent;
            anchor.time = normalizedTime;
            anchor.position = weaponPoseRoot.localPosition;
            anchor.rotation = weaponPoseRoot.localRotation;
            return anchor;
        }

        internal static void CorrectLocalPose(LabGuidedAnchor anchor, Vector3 position, Vector3 euler)
        {
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            LabTakeValidation.RequireFinite(position, "La posición local del anclaje");
            LabTakeValidation.RequireFinite(euler, "El giro local del anclaje");
            anchor.position = position;
            anchor.rotation = Quaternion.Euler(euler);
        }

        private static bool ValidObjectId(string objectId) =>
            !string.IsNullOrWhiteSpace(objectId)
            && ((objectId.StartsWith("part:", StringComparison.Ordinal) && objectId.Length > 5)
                || (objectId.StartsWith("prop:", StringComparison.Ordinal) && objectId.Length > 5));

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
