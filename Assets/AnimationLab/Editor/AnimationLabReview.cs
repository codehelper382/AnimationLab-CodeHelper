using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private sealed class LabReviewIssue
        {
            internal MessageType severity;
            internal string title;
            internal string detail;
            internal float? time;
            internal int? track;
            internal int? tab;
            internal string objectId;
        }

        private List<LabReviewIssue> reviewCache;
        private bool showReviewDetails;

        private void RebuildReviewCache()
        {
            if (preview == null || draft == null)
            {
                reviewCache = null;
                return;
            }
            try { reviewCache = BuildReviewIssues(); }
            catch (Exception ex)
            {
                reviewCache = new List<LabReviewIssue>
                {
                    new LabReviewIssue
                    {
                        severity = MessageType.Warning,
                        title = "No se pudo completar la revisión",
                        detail = ex.GetBaseException().Message + "\nGuarda una copia y revisa la toma manualmente."
                    }
                };
            }
        }

        private List<LabReviewIssue> BuildReviewIssues()
        {
            var issues = new List<LabReviewIssue>();
            if (draft == null || preview == null || draft.action == null || preview.Equipment == null) return issues;
            var dataProblems = LabTakeValidation.Inspect(draft, false);
            foreach (var problem in dataProblems)
                AddIssue(issues, MessageType.Error, "Datos no válidos", problem.message,
                    time: problem.time, track: problem.track, objectId: problem.objectId, tab: problem.track.HasValue ? (int?)null : 2);
            if (dataProblems.Count > 0) return issues;

            float duration = draft.action.duration;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0.0001f)
            {
                AddIssue(issues, MessageType.Error, "Duración inválida", "La toma necesita una duración finita y mayor que cero.", tab: 2);
                return issues;
            }
            if (duration > 30.0001f)
                AddIssue(issues, MessageType.Warning, "Duración superior a 30 s",
                    "El recorrido combinado, la inserción de manos, las recargas y el movimiento natural admiten hasta 30 s. Acorta la duración o divide la toma.",
                    tab: 2);
            if (draft.action.blendIn + draft.action.blendOut > duration + 0.001f)
                AddIssue(issues, MessageType.Warning, "Fundidos más largos que la toma",
                    $"Entrada {draft.action.blendIn:F2} s + salida {draft.action.blendOut:F2} s superan la duración de {duration:F2} s. El final puede quedar atenuado o sin peso completo.",
                    tab: 2);
            if (string.IsNullOrWhiteSpace(takeName))
                AddIssue(issues, MessageType.Warning, "Falta el nombre de la toma", "Escribe un nombre en Proyecto antes de guardar.", tab: 2);

            if (saved == null)
                AddIssue(issues, MessageType.Info, "Todavía no hay copia guardada", "Pulsa Guardar copia para crear un asset en " + LabAssetIO.Output + ".", tab: 2);
            else if (dirty)
                AddIssue(issues, MessageType.Info, "Cambios sin guardar", "La copia guardada no incluye los últimos cambios.", tab: 2);

            if (HasTakeTrial)
                AddIssue(issues, MessageType.Warning, "Prueba temporal pendiente", "Acepta o cancela la prueba antes de editar o guardar. La recuperación conserva el borrador, no el candidato.", tab: HasTransitionTrial ? 1 : 0);
            if (Pending)
                AddIssue(issues, MessageType.Warning, "Ajuste sin confirmar", "Hay una pose editada que todavía no se ha guardado como punto.", time: progress, track: Track);

            if (draft.slot < 0 || draft.slot >= preview.Equipment.slots.Length || preview.Equipment.slots[draft.slot].definition == null)
                AddIssue(issues, MessageType.Error, "Slot de equipo inválido", "La toma apunta a un equipo que no existe en este rig.", tab: 2);
            else if (preview.Equipment.slots[draft.slot].definition.equipmentId != draft.equipmentId)
                AddIssue(issues, MessageType.Error, "La toma pertenece a otro equipo", "Abre la toma con el mismo rig/equipmentId con el que se creó.", tab: 2);

            int wristKeys = draft.left.Count(k => !k.fingersOnly) + draft.right.Count(k => !k.fingersOnly);
            int weaponKeys = draft.weapon.Count;
            int objectKeys = draft.parts.Sum(t => t.keys.Count) + draft.props.Sum(p => p.keys.Count);
            int fingerKeys = draft.left.Count(k => k.fingers != null) + draft.right.Count(k => k.fingers != null);
            int motionKeys = wristKeys + weaponKeys + objectKeys;
            if (motionKeys == 0 && !HasAnimationRecipe() && fingerKeys == 0)
                AddIssue(issues, MessageType.Warning, "Sin puntos ni fuentes de animación",
                    "Puedes partir de una acción existente o guardar puntos de posición o dedos.",
                    time: 0f, track: 2);
            else if (motionKeys == 1)
                AddIssue(issues, MessageType.Info, "Solo hay un punto de movimiento",
                    "Solo hay un punto manual de posición. Para interpolar ese canal, guarda otro en la misma pista; los clips y otras fuentes siguen funcionando.",
                    time: FirstMotionTime(), track: FirstMotionTrack(), objectId: FirstMotionObject());

            AddDuplicateIssues(issues, draft.left, "Izquierda · posición", 0, k => !k.fingersOnly);
            AddDuplicateIssues(issues, draft.left, "Izquierda · dedos", 1, k => k.fingers != null);
            AddDuplicateIssues(issues, draft.right, "Derecha · posición", 2, k => !k.fingersOnly);
            AddDuplicateIssues(issues, draft.right, "Derecha · dedos", 3, k => k.fingers != null);
            AddDuplicateIssues(issues, draft.weapon, "Arma completa", 4);
            foreach (var part in draft.parts) AddDuplicateIssues(issues, part.keys, "Pieza · " + part.path, 5, "part:" + part.path);
            foreach (var prop in draft.props) AddDuplicateIssues(issues, prop.keys, "Diseño · " + prop.name, 5, "prop:" + prop.id);

            var segments = LabCoordinatedPaths.Segments(draft).ToArray();
            float hold = LabCoordinatedPaths.ContactHoldTime(draft);
            foreach (HandSide hand in new[] { HandSide.Left, HandSide.Right })
            {
                var sideSegments = segments.Where(s => s.side == hand).OrderBy(s => s.from.time).ToArray();
                if (sideSegments.Length == 0) continue;
                for (int i = 1; i < sideSegments.Length; i++)
                    if (sideSegments[i].from.time < sideSegments[i - 1].to.time - LabTimeline.TimeEpsilon)
                        AddIssue(issues, MessageType.Error, "Dos contactos se solapan",
                            SideName(hand) + " sigue dos piezas en el mismo intervalo. Separa o acorta uno de los tramos.",
                            time: sideSegments[i].from.time, track: 5, objectId: sideSegments[i].objectId);

                var last = sideSegments[sideSegments.Length - 1];
                if (last.to.time >= hold - LabTimeline.TimeEpsilon) continue;
                var keys = TakeObjectKeys(draft, last.objectId);
                bool later = keys != null && keys.Any(k => k.time > last.to.time + LabTimeline.TimeEpsilon);
                if (!later)
                    AddIssue(issues, MessageType.Warning, "El contacto termina antes del fundido",
                        SideName(hand) + " deja de seguir " + ObjectLabel(last.objectId) + " en F " + LabTimeline.Frame(last.to.time, duration) +
                        ". Después la muñeca deja de seguirla; los dedos y el fundido de salida son canales independientes.",
                        time: last.to.time, track: 5, objectId: last.objectId);
            }

            if (preview.Objects != null)
            {
                foreach (var part in draft.parts.Where(p => p.keys.Count > 0))
                    if (preview.Objects.ResolvePart(part.path) == null)
                        AddIssue(issues, MessageType.Warning, "Pieza no encontrada",
                            "La pista «" + part.path + "» no existe en este equipo. Sus puntos no se pueden reproducir.", tab: 1);
                foreach (var prop in draft.props)
                    if (preview.Objects.ResolveProp(prop.id) == null)
                        AddIssue(issues, MessageType.Warning, "Diseño no creado",
                            "El diseño «" + prop.name + "» no está instanciado en la vista. Reabre la toma o vuelve a preparar el equipo.", tab: 1);
            }

            return issues.OrderByDescending(i => (int)i.severity).ThenBy(i => i.time ?? -1f).ToList();
        }

        private void DrawReviewPanel()
        {
            var issues = reviewCache ?? BuildReviewIssues();
            LabStudioUI.Section("Revisar toma", "Comprobaciones de solo lectura. Corrige lo importante antes de exportar.");
            int errors = issues.Count(i => i.severity == MessageType.Error);
            int warnings = issues.Count(i => i.severity == MessageType.Warning);
            string summary = errors > 0 ? $"{errors} error(es) y {warnings} aviso(s)" : warnings > 0 ? $"{warnings} aviso(s)" : "Sin problemas detectados";
            EditorGUILayout.HelpBox(summary + ". " + (issues.Count == 0 ? "Revisa también el resultado visual antes de exportar." : "Usa los botones de cada aviso para ir al fotograma o a la sección."),
                errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.LabelField("Esta revisión no certifica contactos físicos, ausencia de penetraciones, calidad artística ni gameplay.", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pausar" : "Reproducir")) Safe(TogglePlayback);
                using (new EditorGUI.DisabledScope(HasTakeTrial))
                    if (GUILayout.Button("Guardar copia")) Safe(SaveTake);
                if (GUILayout.Button("Copiar informe"))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildReviewReport();
                    notice = "Informe de revisión copiado al portapapeles.";
                }
            }

            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue.title + "\n" + issue.detail, issue.severity);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (issue.time.HasValue && GUILayout.Button($"Ir a F {LabTimeline.Frame(issue.time.Value, draft.action.duration)}", GUILayout.Width(100)))
                        Safe(() => JumpToReviewIssue(issue));
                    if (issue.track.HasValue && !issue.time.HasValue && GUILayout.Button("Ir a la pista", GUILayout.Width(100)))
                        Safe(() => JumpToReviewIssue(issue));
                    if (issue.tab.HasValue)
                    {
                        string tabName = issue.tab.Value == 0 ? "Recargas" : issue.tab.Value == 1 ? "Animar" : issue.tab.Value == 2 ? "Proyecto" : "Revisar";
                        if (GUILayout.Button("Abrir " + tabName, GUILayout.Width(110))) Safe(() => JumpToReviewIssue(issue));
                    }
                    if (!string.IsNullOrEmpty(issue.objectId) && GUILayout.Button("Seleccionar pieza", GUILayout.Width(120)))
                        Safe(() => JumpToReviewIssue(issue));
                }
            }

            DrawReviewDetails();
        }

        private void DrawReviewDetails()
        {
            showReviewDetails = EditorGUILayout.Foldout(showReviewDetails, "Detalles de la toma", true);
            if (!showReviewDetails || draft == null) return;
            EditorGUILayout.LabelField("Duración", $"{draft.action.duration:F3} s · F 0–{LabTimeline.LastFrame(draft.action.duration)}");
            EditorGUILayout.LabelField("Equipo", draft.equipmentId + " · " + draft.kind);
            EditorGUILayout.LabelField("Puntos", $"Manos {draft.left.Count + draft.right.Count} · Arma {draft.weapon.Count} · Piezas {draft.parts.Sum(t => t.keys.Count) + draft.props.Sum(p => p.keys.Count)}");
            EditorGUILayout.LabelField("Fundidos", $"Entrada {draft.action.blendIn:F2} s · Salida {draft.action.blendOut:F2} s");
            EditorGUILayout.LabelField("Guardada en", saved != null ? AssetDatabase.GetAssetPath(saved) : "Sin guardar");
        }

        private void JumpToReviewIssue(LabReviewIssue issue)
        {
            if (issue == null || !CanLeaveFrame()) return;
            if (!string.IsNullOrEmpty(issue.objectId)) SelectObject(issue.objectId);
            if (issue.track.HasValue) SelectTrack(issue.track.Value, issue.time ?? progress);
            else if (issue.time.HasValue) Seek(issue.time.Value);
            if (issue.tab.HasValue) { studioTab = issue.tab.Value; scroll = Vector2.zero; }
            else if (issue.track.HasValue || !string.IsNullOrEmpty(issue.objectId)) { studioTab = 1; scroll = Vector2.zero; }
        }

        private void DrawNextStep()
        {
            if (draft == null || preview == null) return;
            string text = null, actionLabel = null;
            Action action = null;

            if (HasTakeTrial)
            {
                text = "Termina la prueba temporal: acepta o cancela en la barra del visor.";
                actionLabel = "Ir a la prueba";
                action = () => { if (CanLeaveFrame()) studioTab = HasTransitionTrial ? 1 : 0; };
            }
            else if (Pending)
            {
                text = "Tienes una pose editada sin confirmar. Guárdala como punto o descártala antes de cambiar de fotograma.";
                actionLabel = "Guardar punto";
                action = () => CommitEdits(true);
            }
            else if (MotionKeyCount() == 0 && !HasAnimationRecipe() && !draft.left.Concat(draft.right).Any(k => k.fingers != null))
            {
                text = "La toma todavía no tiene movimiento. Elige una mano o el arma y guarda el primer punto.";
                actionLabel = "Preparar F 0";
                action = () => { SelectTrack(2, 0f); studioTab = 1; scroll = Vector2.zero; notice = "Elige una mano o el arma y pulsa Guardar posición."; };
            }
            else if (MotionKeyCount() == 1)
            {
                float only = FirstMotionTime();
                int last = LabTimeline.LastFrame(draft.action.duration);
                int suggested = only < 0.5f ? Mathf.Max(1, last / 2) : 0;
                text = $"Solo hay un punto (F {LabTimeline.Frame(only, draft.action.duration)}). Ve a F {suggested} y guarda otro para crear movimiento.";
                actionLabel = "Ir a F " + suggested;
                action = () =>
                {
                    string id = FirstMotionObject();
                    if (id != null) SelectObject(id);
                    SelectTrack(FirstMotionTrack(), LabTimeline.Time(suggested, draft.action.duration));
                    studioTab = 1; scroll = Vector2.zero;
                };
            }
            else if (reviewCache != null && reviewCache.Any(i => i.severity == MessageType.Error))
            {
                text = "Hay errores que conviene corregir antes de guardar o exportar.";
                actionLabel = "Abrir revisión";
                action = () => { if (CanLeaveFrame()) studioTab = 3; };
            }
            else if (dirty || saved == null)
            {
                text = "La toma tiene cambios sin guardar. Guarda una copia para conservarlos.";
                actionLabel = "Guardar copia";
                action = () => SaveTake();
            }
            else
            {
                text = "Reproduce la toma y revisa contactos, dedos y primera persona.";
                actionLabel = playing ? "Pausar" : "Reproducir";
                action = () => TogglePlayback();
            }

            if (text == null) return;
            EditorGUILayout.HelpBox("Siguiente paso · " + text, MessageType.None);
            if (actionLabel != null && GUILayout.Button(actionLabel, GUILayout.Width(190))) Safe(action);
        }

        private int MotionKeyCount()
        {
            if (draft == null) return 0;
            return draft.left.Count(k => !k.fingersOnly) + draft.right.Count(k => !k.fingersOnly)
                + draft.weapon.Count + draft.parts.Sum(t => t.keys.Count) + draft.props.Sum(p => p.keys.Count);
        }

        private bool HasAnimationRecipe()
        {
            var action = draft.action;
            return action.characterClip != null || action.weaponClip != null || action.leftHandTargetMotion != null
                || HasHandRecipe(action.leftHandMotion) || HasHandRecipe(action.rightHandMotion)
                || LabTimeline.HasValues(action.weaponPosition) || LabTimeline.HasValues(action.weaponRotation);
        }

        private static bool HasHandRecipe(EquipmentHandMotion motion) => motion != null && motion.enabled
            && LabTimeline.ReplacesMotion(motion);

        private int FirstMotionTrack()
        {
            if (draft.left.Any(k => !k.fingersOnly)) return 0;
            if (draft.right.Any(k => !k.fingersOnly)) return 2;
            return draft.weapon.Count > 0 ? 4 : 5;
        }

        private string FirstMotionObject()
        {
            if (FirstMotionTrack() != 5) return null;
            var part = draft.parts.FirstOrDefault(p => p.keys.Count > 0);
            if (part != null) return "part:" + part.path;
            var prop = draft.props.FirstOrDefault(p => p.keys.Count > 0);
            return prop != null ? "prop:" + prop.id : null;
        }

        private float FirstMotionTime()
        {
            var times = new List<float>();
            if (draft == null) return 0f;
            times.AddRange(draft.left.Where(k => !k.fingersOnly).Select(k => k.time));
            times.AddRange(draft.right.Where(k => !k.fingersOnly).Select(k => k.time));
            times.AddRange(draft.weapon.Select(k => k.time));
            foreach (var part in draft.parts) times.AddRange(part.keys.Select(k => k.time));
            foreach (var prop in draft.props) times.AddRange(prop.keys.Select(k => k.time));
            return times.Count > 0 ? times.Min() : 0f;
        }

        private void AddDuplicateIssues(List<LabReviewIssue> issues, List<LabHandKey> keys, string label, int track, Func<LabHandKey, bool> filter)
        {
            var times = keys.Where(filter).Select(k => k.time).OrderBy(t => t).ToArray();
            for (int i = 1; i < times.Length; i++)
            {
                if (times[i] - times[i - 1] >= LabTimeline.TimeEpsilon) continue;
                AddIssue(issues, MessageType.Warning, "Puntos duplicados en " + label,
                    $"Hay dos puntos en F {LabTimeline.Frame(times[i], draft.action.duration)}. Conserva uno o muévelos a fotogramas distintos.",
                    time: times[i], track: track);
            }
        }

        private void AddDuplicateIssues(List<LabReviewIssue> issues, List<LabWeaponKey> keys, string label, int track, string objectId = null)
        {
            var times = keys.Select(k => k.time).OrderBy(t => t).ToArray();
            for (int i = 1; i < times.Length; i++)
            {
                if (times[i] - times[i - 1] >= LabTimeline.TimeEpsilon) continue;
                AddIssue(issues, MessageType.Warning, "Puntos duplicados en " + label,
                    $"Hay dos puntos en F {LabTimeline.Frame(times[i], draft.action.duration)}. Conserva uno o muévelos a fotogramas distintos.",
                    time: times[i], track: track, objectId: objectId);
            }
        }

        private static void AddIssue(List<LabReviewIssue> issues, MessageType severity, string title, string detail,
            float? time = null, int? track = null, int? tab = null, string objectId = null)
        {
            issues.Add(new LabReviewIssue { severity = severity, title = title, detail = detail, time = time, track = track, tab = tab, objectId = objectId });
        }

        private static string SideName(HandSide side) => side == HandSide.Left ? "La mano izquierda" : "La mano derecha";

        private string BuildReviewReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Animation Lab · informe de revisión");
            sb.AppendLine("Toma: " + takeName);
            if (draft != null && draft.action != null)
            {
                sb.AppendLine("Duración: " + draft.action.duration.ToString("F3") + " s");
                sb.AppendLine("Equipo: " + draft.equipmentId);
                sb.AppendLine("Tipo: " + draft.kind);
                sb.AppendLine("Guardada en: " + (saved != null ? AssetDatabase.GetAssetPath(saved) : "sin guardar"));
            }
            sb.AppendLine();
            var issues = reviewCache ?? BuildReviewIssues();
            if (issues.Count == 0) sb.AppendLine("Sin problemas detectados.");
            else foreach (var issue in issues) sb.AppendLine("[" + issue.severity + "] " + issue.title + ": " + issue.detail);
            return sb.ToString();
        }
    }
}
