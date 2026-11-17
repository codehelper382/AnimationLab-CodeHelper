using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal sealed class LabGuidedGeneratedMotion
    {
        internal readonly Dictionary<HandSide, List<LabHandKey>> wrists =
            new Dictionary<HandSide, List<LabHandKey>>();
        internal readonly Dictionary<HandSide, List<LabHandKey>> fingers =
            new Dictionary<HandSide, List<LabHandKey>>();
        internal List<LabWeaponKey> weapon;
        internal readonly Dictionary<string, List<LabWeaponKey>> pieces =
            new Dictionary<string, List<LabWeaponKey>>(StringComparer.Ordinal);

        internal bool GeneratesWeapon => weapon != null;
        internal bool GeneratesPieces => pieces.Count > 0;
        internal bool GeneratesFingers => fingers.Count > 0;
    }

    /// <summary>
    /// Compiles portable phase anchors into ordinary Animation Lab keys.
    /// This is editor authoring only; it is not evaluated by gameplay/runtime code.
    /// </summary>
    internal static class LabGuidedReloadGenerator
    {
        internal const float PositionTolerance = 0.0005f;
        internal const float RotationTolerance = 0.5f;
        // Hito 3 ampliado: muñeca, arma y dedos. Piezas y mecanismos se bloquean con diagnóstico.
        private const LabGuidedChannels SupportedChannels =
            LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;

        internal static HandSide[] GeneratedHands(LabGuidedReloadPlan plan)
        {
            if (plan?.phases == null) return Array.Empty<HandSide>();
            var result = new List<HandSide>();
            // Hito 3 ampliado: muñeca y dedos.
            if (plan.phases.Any(p => p != null && (p.channels & (LabGuidedChannels.Wrist | LabGuidedChannels.Fingers)) != 0
                    && (p.hands & LabGuidedHands.Left) != 0))
                result.Add(HandSide.Left);
            if (plan.phases.Any(p => p != null && (p.channels & (LabGuidedChannels.Wrist | LabGuidedChannels.Fingers)) != 0
                    && (p.hands & LabGuidedHands.Right) != 0))
                result.Add(HandSide.Right);
            return result.ToArray();
        }

        internal static HandSide[] GeneratedWristHands(LabGuidedReloadPlan plan)
        {
            if (plan?.phases == null) return Array.Empty<HandSide>();
            var result = new List<HandSide>();
            if (plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Wrist) != 0
                    && (p.hands & LabGuidedHands.Left) != 0))
                result.Add(HandSide.Left);
            if (plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Wrist) != 0
                    && (p.hands & LabGuidedHands.Right) != 0))
                result.Add(HandSide.Right);
            return result.ToArray();
        }

        internal static HandSide[] GeneratedFingerHands(LabGuidedReloadPlan plan)
        {
            if (plan?.phases == null) return Array.Empty<HandSide>();
            var result = new List<HandSide>();
            if (plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Fingers) != 0
                    && (p.hands & LabGuidedHands.Left) != 0))
                result.Add(HandSide.Left);
            if (plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Fingers) != 0
                    && (p.hands & LabGuidedHands.Right) != 0))
                result.Add(HandSide.Right);
            return result.ToArray();
        }

        internal static bool GeneratesFingers(LabGuidedReloadPlan plan) =>
            plan?.phases != null && plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Fingers) != 0);

        internal static HashSet<(HandSide, float)> GeneratedFingerTimes(LabGuidedReloadPlan plan)
        {
            var result = new HashSet<(HandSide, float)>();
            if (plan?.phases == null) return result;
            foreach (var phase in plan.phases.Where(p => p != null && (p.channels & LabGuidedChannels.Fingers) != 0))
            {
                if ((phase.hands & LabGuidedHands.Left) != 0)
                {
                    result.Add((HandSide.Left, phase.end));
                    if (phase.start > LabTimeline.TimeEpsilon)
                        result.Add((HandSide.Left, 0f));
                }
                if ((phase.hands & LabGuidedHands.Right) != 0)
                {
                    result.Add((HandSide.Right, phase.end));
                    if (phase.start > LabTimeline.TimeEpsilon)
                        result.Add((HandSide.Right, 0f));
                }
            }
            return result;
        }

        internal static bool GeneratesWeapon(LabGuidedReloadPlan plan) =>
            plan?.phases != null && plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Weapon) != 0);

        // Hito 3 inicial: piezas y mecanismos no se generan. Métodos conservados para compatibilidad de API; devuelven falso/vacío.
        internal static bool GeneratesPieces(LabGuidedReloadPlan plan) => false;
        internal static bool GeneratesMechanisms(LabGuidedReloadPlan plan) => false;
        internal static string[] GeneratedPieceObjectIds(LabGuidedReloadPlan plan) => Array.Empty<string>();

        internal static LabGuidedGeneratedMotion Compile(
            LabGuidedReloadPlan plan,
            float duration,
            Func<LabGuidedAnchor, (Vector3 position, Quaternion rotation)> weaponOffset,
            LabGuidedReloadValidationContext context)
            => Compile(plan, duration, weaponOffset, null, null, null, context);

        internal static LabGuidedGeneratedMotion Compile(
            LabGuidedReloadPlan plan,
            float duration,
            Func<LabGuidedAnchor, (Vector3 position, Quaternion rotation)> weaponOffset,
            Func<LabGuidedReloadPhase, LabGuidedAnchor, HandSide, LabPathContact> pieceContact,
            LabGuidedReloadValidationContext context)
            => Compile(plan, duration, weaponOffset, pieceContact, null, null, context);

        internal static LabGuidedGeneratedMotion Compile(
            LabGuidedReloadPlan plan,
            float duration,
            Func<LabGuidedAnchor, (Vector3 position, Quaternion rotation)> weaponOffset = null,
            Func<LabGuidedReloadPhase, LabGuidedAnchor, HandSide, LabPathContact> pieceContact = null,
            Func<LabGuidedReloadPhase, HandSide, HandPoseData> fingerPoseProvider = null,
            Func<HandSide, HandPoseData> baselinePoseProvider = null,
            LabGuidedReloadValidationContext context = null)
        {
            if (!LabTakeValidation.Finite(duration) || duration <= 0f)
                throw new InvalidOperationException("La duración de la toma debe ser finita y mayor que cero.");
            if (plan == null || !plan.configured)
                throw new InvalidOperationException("Crea una secuencia guiada antes de generar movimiento.");

            var issues = LabGuidedReloadValidation.Inspect(plan, context);
            var invalid = issues.FirstOrDefault(i => i.severity == LabGuidedIssueSeverity.Error);
            if (invalid != null)
                throw new InvalidOperationException("Recarga guiada: " + invalid.message);

            if (context != null && !string.IsNullOrEmpty(context.equipmentId)
                && !string.Equals(plan.calibration?.equipmentId, context.equipmentId, StringComparison.Ordinal))
                throw new InvalidOperationException("La calibración pertenece a otro equipo. Recalibra para el equipo de la toma.");

            var phases = plan.phases.Where(p => p != null && p.channels != LabGuidedChannels.None).ToArray();
            if (phases.Length == 0)
                throw new InvalidOperationException("Selecciona Muñeca, Arma, Pieza o Dedos en al menos una fase.");

            foreach (var phase in phases)
            {
                var unsupported = phase.channels & ~SupportedChannels;
                if (unsupported != LabGuidedChannels.None)
                    throw new InvalidOperationException("La fase '" + phase.displayName + "' solicita "
                        + unsupported + ", canal aún no compatible. No se generó ningún canal.");
                if (phase.repetitions != 1)
                    throw new InvalidOperationException("La fase '" + phase.displayName
                        + "' indica repeticiones. Divide el gesto en fases explícitas; esta versión no inventa ciclos.");
                if ((phase.channels & LabGuidedChannels.Wrist) != 0 && phase.hands == LabGuidedHands.None)
                    throw new InvalidOperationException("La fase '" + phase.displayName
                        + "' solicita muñeca, pero no tiene una mano asignada.");
                if ((phase.channels & LabGuidedChannels.Fingers) != 0 && phase.hands == LabGuidedHands.None)
                    throw new InvalidOperationException("La fase '" + phase.displayName
                        + "' solicita dedos, pero no tiene una mano asignada.");
                if ((phase.channels & LabGuidedChannels.Piece) != 0 && string.IsNullOrEmpty(phase.pieceId))
                    throw new InvalidOperationException("La fase '" + phase.displayName
                        + "' solicita pieza, pero no tiene una pieza asignada.");
                if ((phase.channels & LabGuidedChannels.Mechanism) != 0 && string.IsNullOrEmpty(phase.pieceId))
                    throw new InvalidOperationException("La fase '" + phase.displayName
                        + "' solicita mecanismo, pero no tiene una pieza/mecanismo asignado.");
            }

            var output = new LabGuidedGeneratedMotion();
            foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
            {
                var handFlag = side == HandSide.Left ? LabGuidedHands.Left : LabGuidedHands.Right;
                var selected = phases.Where(p => (p.channels & LabGuidedChannels.Wrist) != 0
                    && (p.hands & handFlag) != 0).ToArray();
                if (selected.Length > 0)
                    output.wrists.Add(side, CompileWristTrack(plan, selected, handFlag));
            }

            var weaponPhases = phases.Where(p => (p.channels & LabGuidedChannels.Weapon) != 0).ToArray();
            if (weaponPhases.Length > 0)
            {
                if (weaponOffset == null)
                    throw new InvalidOperationException("No se pudo muestrear la referencia local del holder del arma.");
                output.weapon = CompileWeaponTrack(plan, weaponPhases, weaponOffset);
            }

            foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
            {
                var handFlag = side == HandSide.Left ? LabGuidedHands.Left : LabGuidedHands.Right;
                var selectedFingers = phases.Where(p => (p.channels & LabGuidedChannels.Fingers) != 0
                    && (p.hands & handFlag) != 0).ToArray();
                if (selectedFingers.Length > 0)
                {
                    if (fingerPoseProvider == null)
                        throw new InvalidOperationException("No se pudo muestrear la pose de dedos para la mano "
                            + (side == HandSide.Left ? "izquierda" : "derecha") + ".");
                    output.fingers.Add(side, CompileFingerTrack(plan, selectedFingers, side, fingerPoseProvider, baselinePoseProvider));
                }
            }

            if (output.wrists.Count == 0 && !output.GeneratesWeapon && !output.GeneratesFingers)
                throw new InvalidOperationException("No hay fases con canales compatibles seleccionados (Muñeca, Arma o Dedos).");
            return output;
        }

        internal static AnimationLabTake BuildCandidate(
            AnimationLabTake source, LabPreview preview, float displayTime, float pitch, float yaw, bool aim)
        {
            if (source == null || source.action == null)
                throw new InvalidOperationException("Falta la toma fuente o su acción.");
            if (preview == null)
                throw new InvalidOperationException("Prepara el preview aislado antes de generar.");
            if (preview.Equipment.SelectedSlot != source.slot
                || preview.Slot.definition == null
                || !string.Equals(preview.Slot.definition.equipmentId, source.equipmentId, StringComparison.Ordinal))
                throw new InvalidOperationException("El equipo/slot del preview no coincide con la toma.");

            var availableHands = LabGuidedHands.None;
            if (preview.Animator.GetBoneTransform(HumanBodyBones.LeftHand) != null)
                availableHands |= LabGuidedHands.Left;
            if (preview.Animator.GetBoneTransform(HumanBodyBones.RightHand) != null)
                availableHands |= LabGuidedHands.Right;
            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = source.equipmentId,
                availableHands = availableHands
            };

            // Compile once before cloning/evaluating. Any unsupported request blocks the whole result.
            var plan = source.guidedReload;
            var requestedWeapon = plan != null && plan.phases != null
                && plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Weapon) != 0);
            var requestedFingers = plan != null && plan.phases != null
                && plan.phases.Any(p => p != null && (p.channels & LabGuidedChannels.Fingers) != 0);
            if (requestedWeapon && source.weaponDynamics != null && source.weaponDynamics.applied)
                throw new InvalidOperationException("Restaura o quita primero la capa de movimiento natural del arma; no se reemplazará una receta aplicada.");

            // Hito 3 ampliado: bloquear muñecas con contactos/compensación previos.
            // El generador reemplaza toda la trayectoria de muñeca solicitada.
            var preliminary = Compile(plan, source.action.duration,
                requestedWeapon ? _ => (Vector3.zero, Quaternion.identity) : null,
                context);
            foreach (var side in preliminary.wrists.Keys)
            {
                bool compensated = side == HandSide.Left ? source.leftContactMotion : source.rightContactMotion;
                bool linked = LabCoordinatedPaths.Segments(source).Any(s => s.side == side);
                if (compensated || linked)
                    throw new InvalidOperationException("Separa o resuelve primero los contactos de la mano "
                        + (side == HandSide.Left ? "izquierda" : "derecha")
                        + "; este candidato no compila recorridos mano–pieza (Hito 3 ampliado: muñeca, arma y dedos).");
                if (side == HandSide.Right && source.action.followAnimatedRightHand)
                    throw new InvalidOperationException("Desactiva Follow Animated Right Hand antes de generar la muñeca derecha.");
            }

            LabTakeValidation.RequireValid(source);
            var candidate = LabAssetIO.Clone(source);
            try
            {
                LabGuidedGeneratedMotion compiled;
                Func<LabGuidedAnchor, (Vector3 position, Quaternion rotation)> offsetFunc = null;
                if (requestedWeapon)
                {
                    var weaponRoot = preview.Equipment.weaponPose.PoseRoot;
                    if (weaponRoot == null || weaponRoot.parent == null)
                        throw new InvalidOperationException("El holder del arma no tiene un padre local estable.");
                    var cache = new Dictionary<LabGuidedAnchor, (Vector3 position, Quaternion rotation)>();
                    (Vector3 position, Quaternion rotation) Offset(LabGuidedAnchor anchor)
                    {
                        if (cache.TryGetValue(anchor, out var value)) return value;
                        preview.Evaluate(source, anchor.time, pitch, yaw, aim);
                        weaponRoot = preview.Equipment.weaponPose.PoseRoot;
                        var basePosition = weaponRoot.localPosition - preview.lastWeaponOffset;
                        var baseRotation = weaponRoot.localRotation * Quaternion.Inverse(Quaternion.Euler(preview.lastWeaponEuler));
                        value = (anchor.position - basePosition, Quaternion.Inverse(baseRotation) * anchor.rotation);
                        cache.Add(anchor, value);
                        return value;
                    }
                    offsetFunc = Offset;
                }

                Func<LabGuidedReloadPhase, HandSide, HandPoseData> fingerPoseFunc = null;
                Func<HandSide, HandPoseData> baselinePoseFunc = null;
                if (requestedFingers)
                {
                    fingerPoseFunc = (phase, side) =>
                    {
                        int presetIdx = phase.gripPreset >= 0 ? phase.gripPreset : LabGripTools.PresetForRole(phase.role);
                        return LabGripTools.Preset(preview.Animator, side, presetIdx);
                    };
                    baselinePoseFunc = side =>
                    {
                        var existingF0 = (side == HandSide.Left ? source.left : source.right)
                            .FirstOrDefault(k => k.fingers != null && k.time <= LabTimeline.TimeEpsilon);
                        if (existingF0 != null && existingF0.fingers != null)
                            return UnityEngine.Object.Instantiate(existingF0.fingers);

                        preview.Evaluate(source, 0f, pitch, yaw, aim, rawFingers: true);
                        var baseline = ScriptableObject.CreateInstance<HandPoseData>();
                        baseline.Side = side;
                        baseline.hideFlags = HideFlags.HideAndDontSave;
                        if (baseline.Capture(preview.Animator))
                        {
                            LabAssetIO.NormalizeFingerPose(baseline, preview);
                            return baseline;
                        }
                        return LabGripTools.Open(preview.Animator, side);
                    };
                }

                compiled = Compile(plan, source.action.duration, offsetFunc, null, fingerPoseFunc, baselinePoseFunc, context);

                foreach (var pair in compiled.wrists)
                    ReplaceWrist(candidate, pair.Key, pair.Value);
                if (compiled.GeneratesWeapon)
                {
                    candidate.weapon = LabObjectAuthoring.CloneKeys(compiled.weapon);
                    LabAssetIO.BuildWeaponMotion(candidate);
                }
                if (compiled.GeneratesFingers)
                {
                    foreach (var pair in compiled.fingers)
                        ApplyFingers(candidate, pair.Key, pair.Value);
                }

                LabTakeValidation.RequireValid(candidate);
                return candidate;
            }
            catch
            {
                LabAssetIO.DestroyDraft(candidate);
                throw;
            }
            finally
            {
                // Restore only the isolated preview pose. The source take and shared scene/assets are untouched.
                if (requestedWeapon)
                    preview.Evaluate(source, displayTime, pitch, yaw, aim);
            }
        }

        private static List<LabHandKey> CompileWristTrack(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase[] phases, LabGuidedHands hand)
        {
            var segments = phases.Select(p => new Segment<LabHandKey>(
                p,
                SelectAnchors(plan, p, LabGuidedAnchorKind.Wrist, hand)
                    .Select(a => new LabHandKey
                    {
                        time = a.time,
                        position = a.position,
                        euler = a.rotation.eulerAngles,
                        pathMode = DeterminePathMode(plan.motionProfile, p.pathMode),
                        bend = DetermineWristBend(plan.motionProfile, p),
                        departureSpeed = DetermineDepartureSpeed(plan.motionProfile, p),
                        arrivalSpeed = DetermineArrivalSpeed(plan.motionProfile, p)
                    }).ToList(),
                (a, b) => Near(a.position, b.position, a.euler, b.euler),
                k => k.time, k => k.euler))
                .ToArray();
            return JoinSegments(segments, "muñeca " + (hand == LabGuidedHands.Left ? "izquierda" : "derecha"),
                k => k.euler, (k, reference) => k.euler = LabTimeline.UnwrapEuler(k.euler, reference));
        }

        private static List<LabWeaponKey> CompileWeaponTrack(
            LabGuidedReloadPlan plan,
            LabGuidedReloadPhase[] phases,
            Func<LabGuidedAnchor, (Vector3 position, Quaternion rotation)> weaponOffset)
        {
            var segments = phases.Select(p =>
            {
                var anchors = SelectAnchors(plan, p, LabGuidedAnchorKind.Weapon, LabGuidedHands.None);
                return new Segment<LabWeaponKey>(p, anchors.Select(a =>
                {
                    var offset = weaponOffset(a);
                    return new LabWeaponKey
                    {
                        time = a.time,
                        position = offset.position,
                        euler = offset.rotation.eulerAngles,
                        pathMode = DeterminePathMode(plan.motionProfile, p.pathMode),
                        bend = DetermineWeaponBend(plan.motionProfile, p),
                        departureSpeed = DetermineDepartureSpeed(plan.motionProfile, p),
                        arrivalSpeed = DetermineArrivalSpeed(plan.motionProfile, p)
                    };
                }).ToList(), (a, b) => Near(a.position, b.position, a.euler, b.euler),
                    k => k.time, k => k.euler);
            }).ToArray();
            return JoinSegments(segments, "arma", k => k.euler,
                (k, reference) => k.euler = LabTimeline.UnwrapEuler(k.euler, reference));
        }

        private static LabPathMode DeterminePathMode(LabGuidedMotionProfile profile, LabPathMode phaseMode)
        {
            if (phaseMode != LabPathMode.Legacy) return phaseMode;
            switch (profile)
            {
                case LabGuidedMotionProfile.Controlled:
                    return LabPathMode.Ease;
                case LabGuidedMotionProfile.Fluid:
                    return LabPathMode.Smooth;
                case LabGuidedMotionProfile.Snappy:
                    return LabPathMode.Cinematic;
                default:
                    return LabPathMode.Linear;
            }
        }

        private static Vector3 DetermineWristBend(LabGuidedMotionProfile profile, LabGuidedReloadPhase phase)
        {
            float extraY = phase.arc * 0.01f;
            if (profile == LabGuidedMotionProfile.Linear)
                return extraY != 0 ? new Vector3(0, extraY, 0) : Vector3.zero;

            Vector3 baseBend;
            switch (profile)
            {
                case LabGuidedMotionProfile.Controlled:
                    baseBend = new Vector3(-0.005f, 0.02f, 0f);
                    break;
                case LabGuidedMotionProfile.Fluid:
                    baseBend = new Vector3(-0.01f, 0.035f, 0.005f);
                    break;
                case LabGuidedMotionProfile.Snappy:
                    baseBend = new Vector3(-0.008f, 0.015f, 0f);
                    break;
                default:
                    baseBend = Vector3.zero;
                    break;
            }
            return baseBend + new Vector3(0, extraY, 0);
        }

        private static Vector3 DetermineWeaponBend(LabGuidedMotionProfile profile, LabGuidedReloadPhase phase)
        {
            if (profile == LabGuidedMotionProfile.Linear) return Vector3.zero;
            switch (profile)
            {
                case LabGuidedMotionProfile.Controlled:
                    return new Vector3(0, 0.003f, 0);
                case LabGuidedMotionProfile.Fluid:
                    return new Vector3(0, 0.006f, 0);
                case LabGuidedMotionProfile.Snappy:
                    return new Vector3(0, 0.004f, 0);
                default:
                    return Vector3.zero;
            }
        }

        private static float DetermineDepartureSpeed(LabGuidedMotionProfile profile, LabGuidedReloadPhase phase)
        {
            if (profile == LabGuidedMotionProfile.Linear) return 1f;
            switch (profile)
            {
                case LabGuidedMotionProfile.Controlled:
                    return 0.85f;
                case LabGuidedMotionProfile.Fluid:
                    return 1f;
                case LabGuidedMotionProfile.Snappy:
                    return 1.35f;
                default:
                    return 1f;
            }
        }

        private static float DetermineArrivalSpeed(LabGuidedMotionProfile profile, LabGuidedReloadPhase phase)
        {
            if (profile == LabGuidedMotionProfile.Linear) return 1f;
            switch (profile)
            {
                case LabGuidedMotionProfile.Controlled:
                    return 0.65f;
                case LabGuidedMotionProfile.Fluid:
                    return 1f;
                case LabGuidedMotionProfile.Snappy:
                    return 0.25f;
                default:
                    return 1f;
            }
        }

        private sealed class Segment<T>
        {
            internal readonly LabGuidedReloadPhase phase;
            internal readonly List<T> keys;
            internal readonly Func<T, T, bool> samePose;
            internal readonly Func<T, float> time;
            internal readonly Func<T, Vector3> euler;
            internal Segment(LabGuidedReloadPhase phase, List<T> keys, Func<T, T, bool> samePose,
                Func<T, float> time, Func<T, Vector3> euler)
            { this.phase = phase; this.keys = keys; this.samePose = samePose; this.time = time; this.euler = euler; }
            internal float Start => time(keys[0]);
            internal float End => time(keys[keys.Count - 1]);
        }

        private static List<T> JoinSegments<T>(
            Segment<T>[] source, string label, Func<T, Vector3> getEuler,
            Action<T, Vector3> unwrap)
        {
            var segments = source.OrderBy(s => s.Start).ThenBy(s => s.End).ToArray();
            var result = new List<T>();
            Segment<T> previous = null;
            foreach (var segment in segments)
            {
                if (segment.keys.Count < 2)
                    throw new InvalidOperationException("Faltan anclas de inicio/fin para " + label + " en la fase '"
                        + segment.phase.displayName + "'.");
                if (previous != null)
                {
                    if (segment.Start < previous.End - LabTimeline.TimeEpsilon)
                        throw new InvalidOperationException("Las fases solicitadas se solapan en " + label
                            + "; separa los intervalos antes de generar.");
                    var priorEnd = previous.keys[previous.keys.Count - 1];
                    var nextStart = segment.keys[0];
                    if (!previous.samePose(priorEnd, nextStart))
                        throw new InvalidOperationException("La salida de una fase no coincide con la entrada siguiente de "
                            + label + ". Añade/ajusta anclas para evitar un salto.");
                }

                foreach (var key in segment.keys)
                {
                    if (result.Count > 0 && Mathf.Abs(segment.time(result[result.Count - 1]) - segment.time(key)) <= LabTimeline.TimeEpsilon)
                    {
                        if (!segment.samePose(result[result.Count - 1], key))
                            throw new InvalidOperationException("Hay dos poses distintas en el mismo instante de " + label + ".");
                        continue;
                    }
                    result.Add(key);
                }
                previous = segment;
            }

            if (result.Count < 2)
                throw new InvalidOperationException("Se necesitan al menos dos anclas para generar " + label + ".");
            Vector3 reference = getEuler(result[0]);
            foreach (var key in result)
            {
                unwrap(key, reference);
                reference = getEuler(key);
            }
            return result;
        }

        private static List<LabGuidedAnchor> SelectAnchors(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase,
            LabGuidedAnchorKind kind, LabGuidedHands hand)
        {
            var anchors = (plan.calibration?.anchors ?? new List<LabGuidedAnchor>())
                .Where(a => a != null && a.phaseId == phase.id
                    && LabGuidedAnchorTools.EffectiveKind(a) == kind
                    && a.hand == hand)
                .OrderBy(a => a.time).ToList();
            if (kind == LabGuidedAnchorKind.Wrist)
            {
                bool invalidAssignment = (plan.calibration?.anchors ?? new List<LabGuidedAnchor>())
                    .Any(a => a != null && a.phaseId == phase.id
                        && LabGuidedAnchorTools.EffectiveKind(a) == kind
                        && (phase.hands & a.hand) != a.hand);
                if (invalidAssignment)
                    throw new InvalidOperationException("La fase '" + phase.displayName
                        + "' conserva un ancla de muñeca para una mano no asignada. Corrige o elimina ese ancla.");
            }
            if (anchors.Any(a => a.time < phase.start - LabTimeline.TimeEpsilon
                    || a.time > phase.end + LabTimeline.TimeEpsilon))
                throw new InvalidOperationException("Hay anclas de " + Label(kind) + " fuera de la fase '"
                    + phase.displayName + "'. Corrígelas antes de generar.");
            for (int i = 1; i < anchors.Count; i++)
                if (Mathf.Abs(anchors[i].time - anchors[i - 1].time) <= LabTimeline.TimeEpsilon)
                    throw new InvalidOperationException("Hay anclas duplicadas de " + Label(kind) + " en la fase '"
                        + phase.displayName + "'.");

            bool hasStart = anchors.Any(a => Mathf.Abs(a.time - phase.start) <= LabTimeline.TimeEpsilon);
            bool hasEnd = anchors.Any(a => Mathf.Abs(a.time - phase.end) <= LabTimeline.TimeEpsilon);
            if (!hasStart || !hasEnd)
                throw new InvalidOperationException("La fase '" + phase.displayName + "' necesita anclas de "
                    + Label(kind) + " en inicio y fin (F "
                    + LabTimeline.Frame(phase.start, 1f) + "–" + LabTimeline.Frame(phase.end, 1f) + ").");
            return anchors;
        }

        private static string Label(LabGuidedAnchorKind kind) =>
            kind == LabGuidedAnchorKind.Wrist ? "muñeca" : kind == LabGuidedAnchorKind.Weapon ? "arma" : kind.ToString();

        private static bool Near(Vector3 p1, Vector3 p2, Vector3 e1, Vector3 e2) =>
            Vector3.Distance(p1, p2) <= PositionTolerance
            && Quaternion.Angle(Quaternion.Euler(e1), Quaternion.Euler(e2)) <= RotationTolerance;

        private static void ReplaceWrist(AnimationLabTake take, HandSide side, List<LabHandKey> generated)
        {
            var keys = side == HandSide.Left ? take.left : take.right;
            var retained = keys.Where(k => k != null && (k.fingersOnly || k.fingers != null)).ToList();
            foreach (var key in retained)
                if (key.fingers != null) key.fingersOnly = true;
            foreach (var source in generated)
            {
                var key = retained.FirstOrDefault(k => Mathf.Abs(k.time - source.time) <= LabTimeline.TimeEpsilon);
                if (key == null)
                {
                    key = new LabHandKey { time = source.time };
                    retained.Add(key);
                }
                key.time = source.time;
                key.position = source.position;
                key.euler = source.euler;
                key.pathMode = source.pathMode;
                key.bend = source.bend;
                key.departureSpeed = source.departureSpeed;
                key.arrivalSpeed = source.arrivalSpeed;
                key.outgoing = source.outgoing;
                key.incoming = source.incoming;
                key.fingersOnly = false;
            }
            retained.Sort((a, b) => a.time.CompareTo(b.time));
            if (side == HandSide.Left)
            {
                take.left = retained;
                take.action.leftHandTargetMotion = null;
                if (take.action.leftHandMotion == null) take.action.leftHandMotion = new EquipmentHandMotion();
                LabAssetIO.BuildMotion(take.left, take.action.leftHandMotion);
            }
            else
            {
                take.right = retained;
                if (take.action.rightHandMotion == null) take.action.rightHandMotion = new EquipmentHandMotion();
                LabAssetIO.BuildMotion(take.right, take.action.rightHandMotion);
            }
        }

        private static List<LabHandKey> CompileFingerTrack(
            LabGuidedReloadPlan plan,
            LabGuidedReloadPhase[] phases,
            HandSide side,
            Func<LabGuidedReloadPhase, HandSide, HandPoseData> fingerPoseProvider,
            Func<HandSide, HandPoseData> baselinePoseProvider)
        {
            var ordered = phases.OrderBy(p => p.start).ThenBy(p => p.end).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                if (ordered[i].start < ordered[i - 1].end - LabTimeline.TimeEpsilon)
                    throw new InvalidOperationException("Las fases solicitadas se solapan en dedos de la mano "
                        + (side == HandSide.Left ? "izquierda" : "derecha")
                        + "; separa los intervalos antes de generar.");
            }

            var result = new List<LabHandKey>();
            if (ordered[0].end > LabTimeline.TimeEpsilon)
            {
                var basePose = baselinePoseProvider != null ? baselinePoseProvider(side) : null;
                result.Add(new LabHandKey
                {
                    time = 0f,
                    fingers = basePose,
                    fingersOnly = true,
                    fingerBlend = LabFingerBlend.Step,
                    fingerBlendDuration = 0f,
                    fingerLabel = "Base inicial"
                });
            }

            foreach (var phase in ordered)
            {
                int presetIdx = phase.gripPreset >= 0 ? phase.gripPreset : LabGripTools.PresetForRole(phase.role);
                string label = !string.IsNullOrEmpty(phase.fingerLabel)
                    ? phase.fingerLabel
                    : (presetIdx >= 0 && presetIdx < LabGripTools.Names.Length ? LabGripTools.Names[presetIdx] : "Agarre");
                var pose = fingerPoseProvider != null ? fingerPoseProvider(phase, side) : null;

                float priorTime = result.Count > 0 ? result[result.Count - 1].time : 0f;
                float maxDuration = Mathf.Max(0f, phase.end - priorTime);
                float duration = Mathf.Clamp(phase.fingerBlendDuration, 0f, maxDuration);

                result.Add(new LabHandKey
                {
                    time = phase.end,
                    fingers = pose,
                    fingersOnly = true,
                    fingerBlend = phase.fingerBlend,
                    fingerBlendDuration = duration,
                    fingerLabel = label
                });
            }

            return result;
        }

        private static void ApplyFingers(AnimationLabTake take, HandSide side, List<LabHandKey> generatedFingers)
        {
            var keys = side == HandSide.Left ? take.left : take.right;
            foreach (var source in generatedFingers)
            {
                var target = keys.FirstOrDefault(k => Mathf.Abs(k.time - source.time) <= LabTimeline.TimeEpsilon);
                if (target != null)
                {
                    target.fingers = source.fingers;
                    target.fingerBlend = source.fingerBlend;
                    target.fingerBlendDuration = source.fingerBlendDuration;
                    target.fingerLabel = source.fingerLabel;
                }
                else
                {
                    target = new LabHandKey
                    {
                        time = source.time,
                        fingers = source.fingers,
                        fingersOnly = true,
                        fingerBlend = source.fingerBlend,
                        fingerBlendDuration = source.fingerBlendDuration,
                        fingerLabel = source.fingerLabel
                    };
                    keys.Add(target);
                }
            }
            keys.Sort((a, b) => a.time.CompareTo(b.time));
        }
    }
}
