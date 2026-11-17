using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal enum LabGuidedIssueSeverity { Warning, Error }
    internal enum LabGuidedIssueSection { Pattern, Calibration, Compatibility }

    internal sealed class LabGuidedReloadIssue
    {
        internal LabGuidedIssueSeverity severity;
        internal LabGuidedIssueSection section;
        internal string code, itemId, message;
    }

    // Optional read-only scene/rig facts. The validator never resolves or changes Unity objects itself.
    internal sealed class LabGuidedReloadValidationContext
    {
        internal string equipmentId, rigId;
        internal LabGuidedHands availableHands = LabGuidedHands.Both;
        internal ISet<string> availableObjectIds;
        internal bool requireExportablePartMotion;
    }

    /// <summary>Pure data and compatibility inspection; never repairs, sorts, samples, or saves.</summary>
    internal static class LabGuidedReloadValidation
    {
        private const float TimeEpsilon = 0.00001f;
        private const int KnownChannels = (int)LabGuidedChannels.All;
        private const int KnownHands = (int)LabGuidedHands.Both;

        internal static List<LabGuidedReloadIssue> Inspect(
            LabGuidedReloadPlan plan, LabGuidedReloadValidationContext context = null)
        {
            var issues = new List<LabGuidedReloadIssue>();
            void Add(LabGuidedIssueSeverity severity, LabGuidedIssueSection section,
                string code, string itemId, string message)
            {
                issues.Add(new LabGuidedReloadIssue
                {
                    severity = severity, section = section, code = code,
                    itemId = itemId, message = message
                });
            }

            if (plan == null)
            {
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.missing", null, "Falta la ficha de recarga guiada.");
                return issues;
            }
            if (!plan.configured) return issues;
            if (plan.version != LabGuidedReloadPlan.CurrentVersion)
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.version", null, "La versión de la ficha no es compatible.");
            if (string.IsNullOrWhiteSpace(plan.patternId))
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.id", null, "El patrón necesita un identificador estable.");
            if (string.IsNullOrWhiteSpace(plan.patternName))
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.name", null, "Escribe un nombre para el patrón.");
            if (!Enum.IsDefined(typeof(LabGuidedGenerationMode), plan.generationMode))
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.generationMode", null, "El alcance de generación no es válido.");
            if (!Enum.IsDefined(typeof(LabGuidedMotionProfile), plan.motionProfile))
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.motionProfile", null, "El perfil de movimiento no es válido.");
            if (!Enum.IsDefined(typeof(LabGuidedMagazinePolicy), plan.magazinePolicy))
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "plan.magazinePolicy", null, "La política de cargador no es válida.");
            if (plan.pieces == null)
            {
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "pieces.missing", null, "Falta la lista de piezas.");
                return issues;
            }
            if (plan.phases == null)
            {
                Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                    "phases.missing", null, "Falta la lista de fases.");
                return issues;
            }

            var pieceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var piece in plan.pieces)
            {
                if (piece == null)
                {
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "piece.null", null, "Hay una pieza vacía.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(piece.id) || !pieceIds.Add(piece.id))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "piece.id", piece.id, "La pieza necesita un ID único.");
                if (string.IsNullOrWhiteSpace(piece.displayName))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "piece.name", piece.id, "La pieza necesita un nombre.");
                if (!Enum.IsDefined(typeof(LabGuidedPieceKind), piece.kind))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "piece.kind", piece.id, "La clase de pieza no es válida.");
                if (piece.suggestedCount < 1 || piece.suggestedCount > LabGuidedReloadPlan.MaxRepetitions)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "piece.count", piece.id, "La cantidad sugerida debe estar entre 1 y " + LabGuidedReloadPlan.MaxRepetitions + ".");
            }

            var phaseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var phase in plan.phases)
            {
                if (phase == null)
                {
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.null", null, "Hay una fase vacía.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(phase.id) || !phaseIds.Add(phase.id))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.id", phase.id, "La fase necesita un ID único.");
                if (string.IsNullOrWhiteSpace(phase.displayName))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.name", phase.id, "La fase necesita un nombre.");
                if (!Enum.IsDefined(typeof(LabGuidedPhaseRole), phase.role))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.role", phase.id, "El rol de fase no es válido.");
                if (!Enum.IsDefined(typeof(LabGuidedPieceAction), phase.pieceAction))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.pieceAction", phase.id, "La acción de pieza no es válida.");
                if (!Finite(phase.start) || !Finite(phase.end) || phase.start < 0f || phase.end > 1f || phase.start >= phase.end)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.time", phase.id, "El intervalo debe cumplir 0 ≤ inicio < fin ≤ 1.");
                if (phase.repetitions < 1 || phase.repetitions > LabGuidedReloadPlan.MaxRepetitions)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.repetitions", phase.id, "Las repeticiones deben estar entre 1 y " + LabGuidedReloadPlan.MaxRepetitions + ".");

                int hands = (int)phase.hands;
                if ((hands & ~KnownHands) != 0)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.hands", phase.id, "La asignación de manos contiene valores desconocidos.");
                int channels = (int)phase.channels;
                if ((channels & ~KnownChannels) != 0)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.channels", phase.id, "La selección de canales contiene valores desconocidos.");
                if (plan.generationMode == LabGuidedGenerationMode.VisualMotionOnly
                    && (phase.channels & ~(LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers)) != 0)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.visualOnly", phase.id, "El modo de movimiento visual (Hito 3 ampliado) permite muñecas, arma y dedos; piezas y mecanismos no se generan en esta versión.");
                if (phase.hands == LabGuidedHands.None
                    && (phase.channels & (LabGuidedChannels.Wrist | LabGuidedChannels.Fingers)) != 0)
                    Add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Pattern,
                        "phase.handRequired", phase.id, "La fase selecciona muñeca o dedos, pero no tiene una mano asignada.");
                if ((phase.channels & LabGuidedChannels.Fingers) != 0)
                {
                    if (!Enum.IsDefined(typeof(LabFingerBlend), phase.fingerBlend))
                        Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                            "phase.fingerBlend", phase.id, "El modo de llegada de dedos no es válido.");
                    if (!Finite(phase.fingerBlendDuration) || phase.fingerBlendDuration < 0f)
                        Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                            "phase.fingerBlendDuration", phase.id, "La duración de transición de dedos debe ser finita y mayor o igual que cero.");
                }

                bool objectMotion = (phase.channels & (LabGuidedChannels.Piece | LabGuidedChannels.Mechanism)) != 0;
                bool hasPiece = !string.IsNullOrWhiteSpace(phase.pieceId);
                if ((objectMotion || phase.pieceAction != LabGuidedPieceAction.NoChange) && !hasPiece)
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.pieceRequired", phase.id, "La acción/canal de pieza necesita una pieza objetivo.");
                if (hasPiece && !pieceIds.Contains(phase.pieceId))
                    Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                        "phase.pieceMissing", phase.id, "La fase apunta a una pieza que no existe en el patrón.");
            }

            for (int i = 0; i < plan.phases.Count; i++)
            {
                var first = plan.phases[i];
                if (first == null || !Finite(first.start) || !Finite(first.end) || first.start >= first.end) continue;
                for (int j = i + 1; j < plan.phases.Count; j++)
                {
                    var second = plan.phases[j];
                    if (second == null || !Finite(second.start) || !Finite(second.end) || second.start >= second.end) continue;
                    bool overlaps = Mathf.Min(first.end, second.end) - Mathf.Max(first.start, second.start) > TimeEpsilon;
                    if (overlaps && !(first.allowOverlap && second.allowOverlap))
                        Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Pattern,
                            "phase.overlap", second.id, "Las fases '" + first.displayName + "' y '" + second.displayName
                                + "' se solapan sin autorización explícita en ambas.");
                }
            }

            InspectCalibration(plan, pieceIds, phaseIds, context, Add);
            if (context != null)
                foreach (var phase in plan.phases.Where(p => p != null && p.hands != LabGuidedHands.None))
                    if ((((int)phase.hands) & ~((int)context.availableHands)) != 0)
                        Add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Compatibility,
                            "phase.handUnavailable", phase.id, "El rig/equipo actual no tiene disponible la mano asignada.");
            return issues;
        }

        private static void InspectCalibration(LabGuidedReloadPlan plan, ISet<string> pieceIds,
            ISet<string> phaseIds,
            LabGuidedReloadValidationContext context,
            Action<LabGuidedIssueSeverity, LabGuidedIssueSection, string, string, string> add)
        {
            var calibration = plan.calibration;
            if (calibration == null)
            {
                add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                    "calibration.missing", null, "El patrón aún no tiene calibración para un equipo/rig.");
                return;
            }
            if (string.IsNullOrWhiteSpace(calibration.calibrationId))
                add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                    "calibration.id", null, "La calibración necesita un identificador estable.");
            if (calibration.bindings == null)
                add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                    "bindings.missing", null, "Falta la lista de referencias de piezas.");
            if (calibration.anchors == null)
                add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                    "anchors.missing", null, "Falta la lista de anclajes.");

            var bindingIds = new HashSet<string>(StringComparer.Ordinal);
            var objectOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            if (calibration.bindings != null)
            {
                foreach (var binding in calibration.bindings)
                {
                    if (binding == null)
                    {
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "binding.null", null, "Hay una referencia de pieza vacía.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(binding.pieceId) || !pieceIds.Contains(binding.pieceId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "binding.piece", binding.pieceId, "La referencia apunta a una pieza inexistente.");
                    if (!string.IsNullOrWhiteSpace(binding.pieceId) && !bindingIds.Add(binding.pieceId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "binding.duplicate", binding.pieceId, "Una pieza tiene más de una referencia asignada.");
                    if (!ValidObjectId(binding.objectId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "binding.objectId", binding.pieceId, "La referencia debe ser part:<ruta> o prop:<id>.");
                    else
                    {
                        if (objectOwners.TryGetValue(binding.objectId, out string otherPiece)
                            && !string.Equals(otherPiece, binding.pieceId, StringComparison.Ordinal))
                            add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                                "binding.reused", binding.pieceId, "La misma referencia " + binding.objectId
                                    + " está asignada también a '" + otherPiece + "'; no representa dos piezas independientes.");
                        else objectOwners[binding.objectId] = binding.pieceId;
                        if (context?.availableObjectIds != null && !context.availableObjectIds.Contains(binding.objectId))
                            add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Compatibility,
                                "binding.unresolved", binding.pieceId, "No se encontró la referencia " + binding.objectId + " en el equipo actual.");
                    }
                    if (context != null && context.requireExportablePartMotion
                        && binding.objectId != null && binding.objectId.StartsWith("prop:", StringComparison.Ordinal)
                        && plan.phases.Any(p => p != null && p.pieceId == binding.pieceId
                            && (p.channels & (LabGuidedChannels.Piece | LabGuidedChannels.Mechanism)) != 0))
                        add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Compatibility,
                            "binding.referenceOnly", binding.pieceId, "El diseño prop: es solo una referencia del laboratorio; no representa una pieza real para bake/exportación.");
                }
            }

            foreach (var phase in plan.phases.Where(p => p != null && !string.IsNullOrWhiteSpace(p.pieceId)))
            {
                if ((phase.channels & (LabGuidedChannels.Piece | LabGuidedChannels.Mechanism)) != 0
                    && !bindingIds.Contains(phase.pieceId))
                    add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                        "binding.required", phase.pieceId, "La pieza de esta fase todavía no está vinculada a una referencia calibrada.");
            }

            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            if (calibration.anchors != null)
            {
                foreach (var anchor in calibration.anchors)
                {
                    if (anchor == null)
                    {
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.null", null, "Hay un anclaje vacío.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(anchor.id) || !anchorIds.Add(anchor.id))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.id", anchor.id, "El anclaje necesita un ID único.");
                    if (string.IsNullOrWhiteSpace(anchor.role))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.role", anchor.id, "El anclaje necesita un rol.");
                    var anchorKind = LabGuidedAnchorTools.EffectiveKind(anchor);
                    if (anchorKind == LabGuidedAnchorKind.Unspecified
                        || !Enum.IsDefined(typeof(LabGuidedAnchorKind), anchor.kind))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.kind", anchor.id, "El tipo de anclaje no es válido o no puede inferirse de forma segura.");
                    if (!Finite(anchor.time) || anchor.time < 0f || anchor.time > 1f)
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.time", anchor.id, "El tiempo del anclaje debe estar entre 0 y 1.");
                    if (string.IsNullOrWhiteSpace(anchor.phaseId) || !phaseIds.Contains(anchor.phaseId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.phase", anchor.id, "El anclaje necesita una fase existente.");
                    else
                    {
                        var phase = plan.phases.FirstOrDefault(p => p != null && p.id == anchor.phaseId);
                        if (phase != null && (anchor.time < phase.start - TimeEpsilon || anchor.time > phase.end + TimeEpsilon))
                            add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                                "anchor.outsidePhase", anchor.id, "El anclaje se capturó fuera del intervalo de la fase asociada.");
                        if (phase != null && anchor.hand != LabGuidedHands.None
                            && (((int)anchor.hand & (int)phase.hands) != (int)anchor.hand))
                            add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                                "anchor.handUnassigned", anchor.id, "La mano del anclaje no está asignada a su fase.");
                        if (phase != null && anchorKind == LabGuidedAnchorKind.Wrist
                            && (phase.channels & LabGuidedChannels.Wrist) == 0)
                            add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                                "anchor.channelDisabled", anchor.id, "La fase ya no tiene activo el canal Muñeca para este anclaje.");
                    }
                    int anchorHands = (int)anchor.hand;
                    if ((anchorHands & ~KnownHands) != 0 || anchor.hand == LabGuidedHands.Both
                        || (anchorKind != LabGuidedAnchorKind.Wrist && anchor.hand != LabGuidedHands.None))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.hand", anchor.id, "Solo un anclaje de muñeca identifica una mano; los demás usan None.");
                    if (anchorKind == LabGuidedAnchorKind.Wrist && anchor.space != LabGuidedAnchorSpace.Guard)
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.wristSpace", anchor.id, "La muñeca debe expresarse respecto a la guardia.");
                    if (anchorKind == LabGuidedAnchorKind.Wrist && anchor.hand == LabGuidedHands.None)
                        add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                            "anchor.guardHand", anchor.id, "Un anclaje local a guardia necesita identificar la mano correspondiente.");
                    if (!Enum.IsDefined(typeof(LabGuidedAnchorSpace), anchor.space))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.space", anchor.id, "El espacio local del anclaje no es válido.");
                    if (anchorKind == LabGuidedAnchorKind.Weapon
                        && (anchor.space != LabGuidedAnchorSpace.Parent || !string.IsNullOrEmpty(anchor.pieceId)
                            || !string.IsNullOrEmpty(anchor.objectId)))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.weaponShape", anchor.id, "El anclaje de arma guarda solo pose local al padre, sin binding de escena/pieza.");
                    if (anchorKind != LabGuidedAnchorKind.Wrist && anchorKind != LabGuidedAnchorKind.Weapon
                        && anchor.space != LabGuidedAnchorSpace.Parent)
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.objectSpace", anchor.id, "La pose de pieza/mecanismo debe expresarse respecto a su padre actual.");
                    if (anchorKind != LabGuidedAnchorKind.Wrist && anchorKind != LabGuidedAnchorKind.Weapon
                        && string.IsNullOrEmpty(anchor.objectId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.referenceRequired", anchor.id, "Un anclaje de pieza/mecanismo necesita una referencia de objeto estable.");
                    bool boundAnchor = anchorKind == LabGuidedAnchorKind.Piece
                        || anchorKind == LabGuidedAnchorKind.Mechanism;
                    if (boundAnchor && string.IsNullOrWhiteSpace(anchor.pieceId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.pieceRequired", anchor.id, "El anclaje de pieza/mecanismo necesita una pieza abstracta.");
                    if (!Finite(anchor.position) || !Finite(anchor.rotation))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.pose", anchor.id, "La pose local del anclaje debe ser finita y tener una rotación válida.");
                    if (!string.IsNullOrEmpty(anchor.pieceId) && !pieceIds.Contains(anchor.pieceId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.piece", anchor.id, "El anclaje apunta a una pieza inexistente.");
                    if (boundAnchor && !string.IsNullOrEmpty(anchor.pieceId)
                        && anchor.space == LabGuidedAnchorSpace.Parent && calibration.bindings != null)
                    {
                        var binding = calibration.bindings.FirstOrDefault(b => b != null && b.pieceId == anchor.pieceId);
                        if (binding == null)
                            add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                                "anchor.bindingMissing", anchor.id, "El anclaje local a padre necesita el binding actual de su pieza.");
                        else if (!string.Equals(binding.objectId, anchor.objectId, StringComparison.Ordinal))
                            add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                                "anchor.bindingMismatch", anchor.id, "El anclaje local a padre debe usar el binding actual de su pieza.");
                    }
                    else if (boundAnchor && !string.IsNullOrEmpty(anchor.pieceId)
                        && anchor.space == LabGuidedAnchorSpace.Parent)
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.bindingMissing", anchor.id, "El anclaje local a padre necesita el binding actual de su pieza.");
                    if (!string.IsNullOrEmpty(anchor.objectId) && !ValidObjectId(anchor.objectId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Calibration,
                            "anchor.objectId", anchor.id, "La referencia debe ser part:<ruta> o prop:<id>.");
                    else if (!string.IsNullOrEmpty(anchor.objectId) && context?.availableObjectIds != null
                        && !context.availableObjectIds.Contains(anchor.objectId))
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Compatibility,
                            "anchor.unresolved", anchor.id, "No se encontró la referencia " + anchor.objectId + " en el equipo actual.");
                    if (context != null && anchor.hand != LabGuidedHands.None
                        && (((int)anchor.hand) & ~((int)context.availableHands)) != 0)
                        add(LabGuidedIssueSeverity.Error, LabGuidedIssueSection.Compatibility,
                            "anchor.handUnavailable", anchor.id, "El rig/equipo actual no tiene disponible la mano del anclaje.");
                }
            }

            foreach (var phase in plan.phases.Where(p => p != null))
            {
                if ((phase.channels & LabGuidedChannels.Wrist) != 0 && phase.hands != LabGuidedHands.None)
                {
                    foreach (LabGuidedHands hand in new[] { LabGuidedHands.Left, LabGuidedHands.Right })
                    {
                        if ((phase.hands & hand) == 0) continue;
                        bool calibrated = HasAnchor(calibration, phase, LabGuidedAnchorKind.Wrist, hand);
                        if (!calibrated)
                            add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                                "anchor.required", phase.id, "La fase todavía no tiene una pose local de muñeca capturada para la mano "
                                    + (hand == LabGuidedHands.Left ? "izquierda" : "derecha") + ".");
                    }
                }
                if ((phase.channels & LabGuidedChannels.Weapon) != 0
                    && !HasAnchor(calibration, phase, LabGuidedAnchorKind.Weapon, LabGuidedHands.None))
                    add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                        "anchor.weaponRequired", phase.id, "La fase de arma todavía no tiene una pose local del holder capturada.");
                if ((phase.channels & LabGuidedChannels.Piece) != 0 && !string.IsNullOrEmpty(phase.pieceId)
                    && !HasAnchor(calibration, phase, LabGuidedAnchorKind.Piece, LabGuidedHands.None))
                    add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                        "anchor.pieceRequired", phase.id, "La fase todavía no tiene una pose local de pieza capturada.");
                if ((phase.channels & LabGuidedChannels.Mechanism) != 0 && !string.IsNullOrEmpty(phase.pieceId)
                    && !HasAnchor(calibration, phase, LabGuidedAnchorKind.Mechanism, LabGuidedHands.None))
                    add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Calibration,
                        "anchor.mechanismRequired", phase.id, "La fase todavía no tiene una pose local del mecanismo capturada.");
            }

            if (context == null) return;
            if (!string.IsNullOrEmpty(context.equipmentId)
                && !string.Equals(calibration.equipmentId, context.equipmentId, StringComparison.Ordinal))
                add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Compatibility,
                    "calibration.equipment", null, "La calibración se guardó para otro equipo o aún no tiene equipmentId.");
            if (!string.IsNullOrEmpty(context.rigId) && !string.IsNullOrEmpty(calibration.rigId)
                && !string.Equals(calibration.rigId, context.rigId, StringComparison.Ordinal))
                add(LabGuidedIssueSeverity.Warning, LabGuidedIssueSection.Compatibility,
                    "calibration.rig", null, "La calibración se guardó para otro rig/avatar.");
        }

        private static bool HasAnchor(LabGuidedReloadCalibration calibration,
            LabGuidedReloadPhase phase, LabGuidedAnchorKind kind, LabGuidedHands hand) =>
            calibration.anchors != null && calibration.anchors.Any(a => a != null
                && a.phaseId == phase.id && LabGuidedAnchorTools.EffectiveKind(a) == kind && a.hand == hand
                && a.time >= phase.start - TimeEpsilon && a.time <= phase.end + TimeEpsilon);

        private static bool ValidObjectId(string objectId) =>
            !string.IsNullOrWhiteSpace(objectId)
            && ((objectId.StartsWith("part:", StringComparison.Ordinal) && objectId.Length > 5)
                || (objectId.StartsWith("prop:", StringComparison.Ordinal) && objectId.Length > 5));

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) =>
            Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 0.00000001f;
    }
}
