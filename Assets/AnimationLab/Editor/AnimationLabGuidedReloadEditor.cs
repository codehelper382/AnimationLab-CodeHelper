using System;
using System.Collections.Generic;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool guidedReloadEditorOpen = true;
        private AnimationLabTake guidedReloadCopySource;
        private int guidedAnchorPhaseIndex;
        private bool guidedReloadWizardMode = true;
        private int guidedReloadWizardStep = 0;

        private void DrawGuidedReloadEditor()
        {
            guidedReloadEditorOpen = EditorGUILayout.Foldout(guidedReloadEditorOpen,
                "Recarga guiada · fases, piezas y canales", true);
            if (!guidedReloadEditorOpen) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "Configura una secuencia propia o copia el patrón de otra toma. Esta etapa solo edita datos: todavía no genera movimiento ni cambia gameplay.",
                    MessageType.Info);
                if (HasTakeTrial)
                    EditorGUILayout.HelpBox("Acepta o cancela la prueba temporal antes de editar la secuencia.", MessageType.Warning);

                using (new EditorGUI.DisabledScope(HasTakeTrial || quarantinedRecovery))
                {
                    var plan = draft.guidedReload;
                    if (plan == null || !plan.configured)
                    {
                        DrawGuidedReloadStart();
                        return;
                    }

                    // Mode toolbar: Asistente vs Editor avanzado
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        bool wizard = GUILayout.Toggle(guidedReloadWizardMode, "🧙 Asistente guiado", EditorStyles.toolbarButton);
                        bool advanced = GUILayout.Toggle(!guidedReloadWizardMode, "🛠 Editor avanzado (fases/piezas)", EditorStyles.toolbarButton);
                        if (wizard != guidedReloadWizardMode) guidedReloadWizardMode = wizard;
                        else if (advanced == guidedReloadWizardMode) guidedReloadWizardMode = !advanced;
                    }
                    EditorGUILayout.Space(3);

                    if (guidedReloadWizardMode)
                    {
                        DrawGuidedReloadWizard(plan);
                    }
                    else
                    {
                        DrawGuidedReloadHeader(plan);
                        DrawGuidedReloadPieces(plan);
                        DrawGuidedReloadPhases(plan);
                        DrawGuidedReloadCalibration(plan);
                        DrawGuidedReloadDiagnostics(plan);
                    }
                }
            }
        }

        private void DrawGuidedReloadStart()
        {
            EditorGUILayout.LabelField("La toma activa es un borrador independiente; crear una secuencia no modifica su asset fuente.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Elige una plantilla para comenzar o diseña una secuencia desde cero:", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("📦 Recarga táctica con cargador (6 fases, Snappy)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Presentar, alcanzar, extraer, insertar cargador nuevo, golpe de palma y recuperar. Configura perfil Snappy y cargador extraíble.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Usar plantilla táctica con cargador", GUILayout.Height(26)))
                {
                    Safe(() => ApplyGuidedChange("Crear recarga táctica con cargador", () =>
                    {
                        draft.guidedReload = LabGuidedReloadEditing.CreateTacticalMagazinePlan(draft.equipmentId);
                        int bound = LabGuidedReloadEditing.AutoDetectBindings(draft.guidedReload, ObjectIds());
                        guidedReloadWizardStep = 0;
                        if (bound > 0) notice = $"Plantilla creada y {bound} pieza(s) vinculadas automáticamente.";
                    }));
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("⚡ Recarga vacía con cargador y cerrojo (7 fases, Snappy)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Incluye extracción e inserción de cargador más fase dedicada de tirón de cerrojo (Bolt Hook). Detecta automáticamente cargador y cerrojo del equipo.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Usar plantilla vacía con cerrojo", GUILayout.Height(26)))
                {
                    Safe(() => ApplyGuidedChange("Crear recarga vacía con cerrojo", () =>
                    {
                        draft.guidedReload = LabGuidedReloadEditing.CreateEmptyReloadPlan(draft.equipmentId);
                        int bound = LabGuidedReloadEditing.AutoDetectBindings(draft.guidedReload, ObjectIds());
                        guidedReloadWizardStep = 0;
                        if (bound > 0) notice = $"Plantilla creada y {bound} pieza(s) vinculadas automáticamente.";
                    }));
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🖐 Recarga procedimental solo manos (5 fases, Fluid)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Genera movimiento orgánico de muñecas y dedos sin exigir piezas 3D ni modelos de cargador. Ideal para prototipado rápido.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Usar plantilla solo manos", GUILayout.Height(26)))
                {
                    Safe(() => ApplyGuidedChange("Crear recarga solo manos", () =>
                    {
                        draft.guidedReload = LabGuidedReloadEditing.CreateProceduralHandsPlan(draft.equipmentId);
                        guidedReloadWizardStep = 0;
                    }));
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🔴 Escopeta de tubo · cartucho a cartucho (6 fases, Controlled)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Presentar ventana, alcanzar cartuchos, insertar en tubo con repeticiones, asentar retenida, accionar bombeo y recuperar.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Usar plantilla escopeta de tubo", GUILayout.Height(26)))
                {
                    Safe(() => ApplyGuidedChange("Crear recarga de escopeta de tubo", () =>
                    {
                        draft.guidedReload = LabGuidedReloadEditing.CreateShotgunTubePlan(draft.equipmentId);
                        int bound = LabGuidedReloadEditing.AutoDetectBindings(draft.guidedReload, ObjectIds());
                        guidedReloadWizardStep = 0;
                        if (bound > 0) notice = $"Plantilla de escopeta creada y {bound} pieza(s) vinculadas.";
                    }));
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🔄 Revólver · tambor oscilante (6 fases, Controlled)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Bascular/abrir tambor, expulsar vainas con varilla, insertar munición/speedloader, cierre seco y recuperar empuñadura.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Usar plantilla revólver", GUILayout.Height(26)))
                {
                    Safe(() => ApplyGuidedChange("Crear recarga de revólver", () =>
                    {
                        draft.guidedReload = LabGuidedReloadEditing.CreateRevolverPlan(draft.equipmentId);
                        int bound = LabGuidedReloadEditing.AutoDetectBindings(draft.guidedReload, ObjectIds());
                        guidedReloadWizardStep = 0;
                        if (bound > 0) notice = $"Plantilla de revólver creada y {bound} pieza(s) vinculadas.";
                    }));
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("📄 Secuencia personalizada desde cero", EditorStyles.boldLabel);
                if (GUILayout.Button("Crear secuencia nueva (vacía)", GUILayout.Height(24)))
                {
                    Safe(() => ApplyGuidedChange("Crear recarga guiada", () =>
                    {
                        draft.guidedReload = LabGuidedReloadEditing.NewPlan(draft.equipmentId);
                        guidedReloadWizardStep = 0;
                    }));
                }
            }

            EditorGUILayout.Space(3);
            guidedReloadCopySource = (AnimationLabTake)EditorGUILayout.ObjectField(
                "Copiar patrón de otra toma", guidedReloadCopySource, typeof(AnimationLabTake), false);
            bool hasPattern = guidedReloadCopySource != null && guidedReloadCopySource.guidedReload != null
                && guidedReloadCopySource.guidedReload.configured;
            using (new EditorGUI.DisabledScope(!hasPattern))
                if (GUILayout.Button("Copiar secuencia y recalibrar para este equipo"))
                    Safe(() => ApplyGuidedChange("Copiar patrón de recarga",
                        () =>
                        {
                            draft.guidedReload = LabGuidedReloadEditing.CopyPattern(
                                guidedReloadCopySource.guidedReload, draft.equipmentId);
                            LabGuidedReloadEditing.AutoDetectBindings(draft.guidedReload, ObjectIds());
                            guidedReloadWizardStep = 0;
                        }));
            if (guidedReloadCopySource != null && !hasPattern)
                EditorGUILayout.HelpBox("Esta toma no contiene un patrón guiado. Puedes abrirla como copia desde Inicio y crear aquí una secuencia, sin inferir fases de sus claves existentes.", MessageType.None);
        }

        private void DrawGuidedReloadWizard(LabGuidedReloadPlan plan)
        {
            string[] steps = new[]
            {
                "1. Estilo y Fases",
                "2. Piezas y Vínculos",
                "3. Calibración guiada",
                "4. Generar y Probar"
            };
            guidedReloadWizardStep = GUILayout.Toolbar(guidedReloadWizardStep, steps, GUILayout.Height(28));
            EditorGUILayout.Space(4);

            switch (guidedReloadWizardStep)
            {
                case 0:
                    DrawWizardStep0_StyleAndPhases(plan);
                    break;
                case 1:
                    DrawWizardStep1_PiecesAndBindings(plan);
                    break;
                case 2:
                    DrawWizardStep2_GuidedCalibration(plan);
                    break;
                case 3:
                    DrawWizardStep3_GenerateAndTest(plan);
                    break;
            }
        }

        private void DrawWizardStep0_StyleAndPhases(LabGuidedReloadPlan plan)
        {
            DrawGuidedReloadHeader(plan);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Fases de la secuencia ({plan.phases.Count})", EditorStyles.boldLabel);

            int lastFrame = LabTimeline.LastFrame(draft.action.duration);
            for (int i = 0; i < plan.phases.Count; i++)
            {
                var phase = plan.phases[i];
                if (phase == null) continue;
                int startFrame = GuidedTimeToFrame(phase.start, lastFrame);
                int endFrame = GuidedTimeToFrame(phase.end, lastFrame);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"#{i + 1} [F{startFrame}-F{endFrame}]", EditorStyles.boldLabel, GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{phase.displayName} ({phase.role})", GUILayout.MinWidth(140));
                    EditorGUILayout.LabelField($"Mano: {phase.hands}", GUILayout.Width(90));
                    string presetName = phase.gripPreset >= 0 && phase.gripPreset < LabGripTools.Names.Length
                        ? LabGripTools.Names[phase.gripPreset] : "Auto";
                    EditorGUILayout.LabelField($"Agarre: {presetName}", GUILayout.Width(130));

                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22)))
                            Safe(() => ApplyGuidedChange("Mover fase arriba", () => LabGuidedReloadEditing.MovePhase(plan, i, -1)));
                    using (new EditorGUI.DisabledScope(i == plan.phases.Count - 1))
                        if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22)))
                            Safe(() => ApplyGuidedChange("Mover fase abajo", () => LabGuidedReloadEditing.MovePhase(plan, i, 1)));
                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                        Safe(() => ApplyGuidedChange("Eliminar fase", () => LabGuidedReloadEditing.RemovePhase(plan, phase.id)));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Añadir fase", GUILayout.Height(24)))
                    Safe(() => ApplyGuidedChange("Añadir fase", () => LabGuidedReloadEditing.AddPhase(plan)));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Siguiente: Piezas y Vínculos ➔", GUILayout.Width(220), GUILayout.Height(26)))
                    guidedReloadWizardStep = 1;
            }
        }

        private void DrawWizardStep1_PiecesAndBindings(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.LabelField("Piezas abstractas y vínculos del equipo", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Asocia cada pieza del plan a un objeto real del equipo (part:...) o a un diseño de referencia (prop:...).",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🔍 Auto-detectar vínculos del equipo", GUILayout.Height(26)))
                {
                    Safe(() =>
                    {
                        int bound = LabGuidedReloadEditing.AutoDetectBindings(plan, ObjectIds());
                        if (bound > 0)
                        {
                            EditorUtility.SetDirty(draft);
                            dirty = true;
                            notice = $"Se vincularon automáticamente {bound} pieza(s) según la jerarquía del equipo.";
                        }
                        else
                        {
                            notice = "No se encontraron nuevas piezas coincidentes en el equipo activo.";
                        }
                    });
                }
                if (GUILayout.Button("+ Añadir pieza", GUILayout.Width(120), GUILayout.Height(26)))
                    Safe(() => ApplyGuidedChange("Añadir pieza abstracta", () => LabGuidedReloadEditing.AddPiece(plan)));
            }
            EditorGUILayout.Space(4);

            DrawGuidedReloadPieces(plan);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("⬅ Anterior: Estilo y Fases", GUILayout.Width(200), GUILayout.Height(26)))
                    guidedReloadWizardStep = 0;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Siguiente: Calibración guiada ➔", GUILayout.Width(220), GUILayout.Height(26)))
                    guidedReloadWizardStep = 2;
            }
        }

        private void DrawWizardStep2_GuidedCalibration(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.LabelField("Calibración guiada de anclajes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Cada canal activo requiere anclajes calibrados al inicio y al final de su fase. "
                + "Navega a cada fotograma con '⏱ Ir', ajusta la pose si es necesario, y presiona '📷 Capturar'. "
                + "Usa '🔗 Enlazar anterior' para copiar con continuidad la pose final de la fase previa.",
                MessageType.Info);

            var requirements = LabGuidedReloadEditing.GetRequirements(plan);
            int total = requirements.Count;
            int satisfied = requirements.Count(r => r.isSatisfied);

            float pct = total > 0 ? (float)satisfied / total : 1f;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (total == 0)
                {
                    EditorGUILayout.HelpBox("No hay anclajes obligatorios en la configuración actual.", MessageType.Info);
                }
                else if (satisfied == total)
                {
                    EditorGUILayout.HelpBox($"✔ Calibración completa ({satisfied}/{total} anclajes capturados). ¡Listo para generar!", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"⏳ Progreso de calibración: {satisfied} de {total} anclajes capturados ({pct * 100f:0}%). Completa los pendientes para generar.", MessageType.Warning);
                }
            }
            EditorGUILayout.Space(4);

            int lastFrame = LabTimeline.LastFrame(draft.action.duration);
            for (int i = 0; i < plan.phases.Count; i++)
            {
                var phase = plan.phases[i];
                if (phase == null) continue;
                var phaseReqs = requirements.Where(r => r.phaseId == phase.id).ToList();
                if (phaseReqs.Count == 0) continue;

                int startFrame = GuidedTimeToFrame(phase.start, lastFrame);
                int endFrame = GuidedTimeToFrame(phase.end, lastFrame);
                bool phaseComplete = phaseReqs.All(r => r.isSatisfied);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var prevColor = GUI.color;
                        GUI.color = phaseComplete ? Color.green : new Color(1f, 0.7f, 0.2f);
                        GUILayout.Label(phaseComplete ? "✔" : "⏳", GUILayout.Width(18));
                        GUI.color = prevColor;

                        EditorGUILayout.LabelField($"Fase #{i + 1}: {phase.displayName} [F{startFrame}-F{endFrame}]", EditorStyles.boldLabel);

                        if (i > 0)
                        {
                            if (GUILayout.Button("🔗 Enlazar todo con anterior", EditorStyles.miniButton, GUILayout.Width(170)))
                            {
                                Safe(() =>
                                {
                                    ApplyGuidedChange("Enlazar anclajes con fase anterior", () =>
                                    {
                                        int copied = LabGuidedReloadEditing.CopyPreviousPhaseAnchors(plan, i);
                                        notice = $"Se enlazaron {copied} anclaje(s) desde la fase anterior.";
                                    });
                                });
                            }
                        }
                    }

                    foreach (var req in phaseReqs)
                    {
                        int reqFrame = GuidedTimeToFrame(req.time, lastFrame);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var prevColor = GUI.color;
                            GUI.color = req.isSatisfied ? Color.green : new Color(1f, 0.5f, 0.3f);
                            GUILayout.Label(req.isSatisfied ? "✔" : "⚠", GUILayout.Width(16));
                            GUI.color = prevColor;

                            EditorGUILayout.LabelField($"[F{reqFrame}] {req.label}", GUILayout.MinWidth(180));

                            if (GUILayout.Button($"⏱ Ir a F{reqFrame}", GUILayout.Width(75)))
                                Safe(() => Seek(req.time));

                            if (GUILayout.Button("📷 Capturar", GUILayout.Width(80)))
                            {
                                Safe(() =>
                                {
                                    Seek(req.time);
                                    ApplyGuidedChange("Capturar anclaje guiado", () =>
                                    {
                                        if (req.kind == LabGuidedAnchorKind.Wrist)
                                            CaptureGuidedHandAnchor(plan, phase, req.hand);
                                        else if (req.kind == LabGuidedAnchorKind.Weapon)
                                            CaptureGuidedWeaponAnchor(plan, phase);
                                        else if (req.kind == LabGuidedAnchorKind.Piece || req.kind == LabGuidedAnchorKind.Mechanism)
                                        {
                                            var binding = plan.calibration?.bindings?.FirstOrDefault(b => b != null && b.pieceId == req.pieceId);
                                            if (binding == null || string.IsNullOrEmpty(binding.objectId))
                                                throw new InvalidOperationException($"La pieza '{req.pieceDisplayName}' no tiene un objeto vinculado. Asígnalo en el Paso 2.");
                                            CaptureGuidedBoundObject(plan, phase, binding.objectId, req.kind);
                                        }
                                    });
                                });
                            }

                            if (req.isStart && i > 0)
                            {
                                if (GUILayout.Button("🔗 Enlazar", GUILayout.Width(70)))
                                {
                                    Safe(() =>
                                    {
                                        ApplyGuidedChange("Enlazar anclaje", () =>
                                        {
                                            int copied = LabGuidedReloadEditing.CopyPreviousPhaseAnchors(plan, i);
                                            notice = $"Se enlazaron anclajes desde el final de la fase anterior.";
                                        });
                                    });
                                }
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("⬅ Anterior: Piezas y Vínculos", GUILayout.Width(200), GUILayout.Height(26)))
                    guidedReloadWizardStep = 1;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Siguiente: Generar y Probar ➔", GUILayout.Width(220), GUILayout.Height(26)))
                    guidedReloadWizardStep = 3;
            }
        }

        private void DrawWizardStep3_GenerateAndTest(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.LabelField("Generación y prueba reversible", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "La generación compila las fases a un candidato temporal reversible. "
                + "Puedes evaluar el movimiento en el visor, alternar ANTES/DESPUÉS y cancelar sin modificar el borrador.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Resumen de configuración:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"• Patrón: {plan.patternName}");
                EditorGUILayout.LabelField($"• Perfil de movimiento: {plan.motionProfile}");
                EditorGUILayout.LabelField($"• Alcance: {plan.generationMode} ({plan.phases.Count} fases)");
                EditorGUILayout.LabelField($"• Canales soportados (Hito 3 ampliado): Muñeca, Arma, Dedos");
                EditorGUILayout.LabelField($"• Canales bloqueados: Piezas, Mecanismos (requieren ampliación posterior)");
            }

            if (HasTakeTrial)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Prueba reversible activa", EditorStyles.boldLabel);
                    DrawGuidedReloadTrialActions();
                }
            }

            DrawGuidedReloadDiagnostics(plan);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("⬅ Anterior: Calibración guiada", GUILayout.Width(200), GUILayout.Height(26)))
                    guidedReloadWizardStep = 2;
            }
        }

        private void DrawGuidedReloadHeader(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.LabelField("Patrón editable · no es una máquina de estados del juego", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.DelayedTextField("Nombre", plan.patternName);
            var mode = (LabGuidedGenerationMode)EditorGUILayout.EnumPopup("Alcance previsto", plan.generationMode);
            var profile = (LabGuidedMotionProfile)EditorGUILayout.EnumPopup("Perfil de movimiento", plan.motionProfile);
            var magPolicy = (LabGuidedMagazinePolicy)EditorGUILayout.EnumPopup("Política de cargador", plan.magazinePolicy);
            if (EditorGUI.EndChangeCheck())
                Safe(() => ApplyGuidedChange("Editar patrón de recarga", () =>
                {
                    plan.patternName = name;
                    plan.generationMode = mode;
                    plan.motionProfile = profile;
                    plan.magazinePolicy = magPolicy;
                }));
            string profileDesc = profile == LabGuidedMotionProfile.Linear
                ? "Perfil Lineal: interpolación directa entre anclas, sin arcos automáticos (base Hito 3)."
                : profile == LabGuidedMotionProfile.Controlled
                ? "Perfil Controlado: curvas suaves (Ease) con aceleración/frenado amortiguado y elevación natural (2 cm) en tránsitos."
                : profile == LabGuidedMotionProfile.Fluid
                ? "Perfil Fluido: curvas continuas orgánicas (Smooth / Catmull-Rom) con arco amplio (3.5 cm) sin paradas bruscas."
                : "Perfil Snappy (Táctico Moderno): estilo MW 2019 con curvas cinemáticas, salida explosiva (1.35x), arco tenso y parada seca de impacto.";
            EditorGUILayout.HelpBox(profileDesc, MessageType.None);
        }

        private void DrawGuidedReloadPieces(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Piezas abstractas", EditorStyles.boldLabel);
            if (plan.pieces == null)
            {
                EditorGUILayout.HelpBox("La lista de piezas está dañada o incompleta; no se reparará automáticamente.", MessageType.Error);
                return;
            }
            if (GUILayout.Button("＋ Añadir pieza")) Safe(() => ApplyGuidedChange("Añadir pieza", () => LabGuidedReloadEditing.AddPiece(plan)));

            for (int i = 0; i < plan.pieces.Count; i++)
            {
                var piece = plan.pieces[i];
                if (piece == null)
                {
                    EditorGUILayout.HelpBox("Hay una entrada de pieza vacía. Revisa los datos antes de continuar.", MessageType.Error);
                    continue;
                }
                bool canRemove = LabGuidedReloadEditing.CanRemovePiece(plan, piece.id);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(piece.displayName, EditorStyles.boldLabel);
                        using (new EditorGUI.DisabledScope(!canRemove))
                            if (GUILayout.Button("Quitar", GUILayout.Width(58)))
                                Safe(() => ApplyGuidedChange("Quitar pieza", () => LabGuidedReloadEditing.RemovePiece(plan, piece.id)));
                    }

                    EditorGUI.BeginChangeCheck();
                    string name = EditorGUILayout.DelayedTextField("Nombre", piece.displayName);
                    var kind = (LabGuidedPieceKind)EditorGUILayout.EnumPopup("Clase", piece.kind);
                    int count = EditorGUILayout.IntField("Cantidad sugerida", piece.suggestedCount);
                    if (EditorGUI.EndChangeCheck())
                        Safe(() => ApplyGuidedChange("Editar pieza", () =>
                        {
                            piece.displayName = name;
                            piece.kind = kind;
                            piece.suggestedCount = count;
                        }));

                    DrawGuidedPieceBinding(plan, piece);
                    if (!canRemove)
                        EditorGUILayout.LabelField("No se puede quitar mientras una fase, binding o anclaje use esta pieza.", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawGuidedPieceBinding(LabGuidedReloadPlan plan, LabGuidedPieceDefinition piece)
        {
            var binding = plan.calibration?.bindings?.FirstOrDefault(b => b != null && b.pieceId == piece.id);
            string[] ids = ObjectIds();
            var labels = new List<string> { "Sin referencia calibrada" };
            labels.AddRange(ids.Select(ObjectLabel));
            int objectIndex = string.IsNullOrEmpty(binding?.objectId) ? -1 : Array.IndexOf(ids, binding.objectId);
            int selected = objectIndex >= 0 ? objectIndex + 1 : 0;
            if (binding != null && !string.IsNullOrEmpty(binding.objectId) && objectIndex < 0)
            {
                labels.Add("No encontrada · " + binding.objectId);
                selected = labels.Count - 1;
            }
            int next = EditorGUILayout.Popup("Referencia de pieza", selected, labels.ToArray());
            if (next != selected)
            {
                string objectId = next <= 0 || next > ids.Length ? null : ids[next - 1];
                Safe(() => ApplyGuidedChange("Vincular pieza de recarga",
                    () =>
                    {
                        if (plan.calibration == null)
                            plan.calibration = new LabGuidedReloadCalibration { equipmentId = draft.equipmentId };
                        LabGuidedReloadEditing.SetBinding(plan, piece.id, objectId);
                    }));
            }
            if (binding != null && binding.objectId != null && binding.objectId.StartsWith("prop:", StringComparison.Ordinal))
                EditorGUILayout.LabelField("Diseño de laboratorio: referencia visual; no representa munición ni una pieza real del equipo.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawGuidedReloadPhases(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Fases ordenables", EditorStyles.boldLabel);
            if (plan.phases == null)
            {
                EditorGUILayout.HelpBox("La lista de fases está dañada o incompleta; no se reparará automáticamente.", MessageType.Error);
                return;
            }
            if (GUILayout.Button("＋ Añadir fase")) Safe(() =>
                ApplyGuidedChange("Añadir fase", () => LabGuidedReloadEditing.AddPhase(plan)));
            if (plan.phases.Count == 0)
                EditorGUILayout.HelpBox("No hay fases. Añádelas solo si forman parte del mecanismo real; puedes omitir cargador, extracción o cualquier otra acción.", MessageType.None);

            int lastFrame = LabTimeline.LastFrame(draft.action.duration);
            for (int i = 0; i < plan.phases.Count; i++)
            {
                var phase = plan.phases[i];
                if (phase == null)
                {
                    EditorGUILayout.HelpBox("Hay una fase vacía. Revisa los datos antes de continuar.", MessageType.Error);
                    continue;
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Fase " + (i + 1), EditorStyles.boldLabel);
                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button("↑", GUILayout.Width(30)))
                                Safe(() => ApplyGuidedChange("Reordenar fases", () => LabGuidedReloadEditing.MovePhase(plan, i, -1)));
                        using (new EditorGUI.DisabledScope(i == plan.phases.Count - 1))
                            if (GUILayout.Button("↓", GUILayout.Width(30)))
                                Safe(() => ApplyGuidedChange("Reordenar fases", () => LabGuidedReloadEditing.MovePhase(plan, i, 1)));
                        if (GUILayout.Button("Quitar", GUILayout.Width(58)))
                            Safe(() => ApplyGuidedChange("Quitar fase", () => LabGuidedReloadEditing.RemovePhase(plan, phase.id)));
                    }

                    EditorGUI.BeginChangeCheck();
                    string name = EditorGUILayout.DelayedTextField("Nombre", phase.displayName);
                    var role = (LabGuidedPhaseRole)EditorGUILayout.EnumPopup("Rol orientativo", phase.role);
                    int originalStartFrame = GuidedTimeToFrame(phase.start, lastFrame);
                    int originalEndFrame = GuidedTimeToFrame(phase.end, lastFrame);
                    int start = EditorGUILayout.DelayedIntField("Desde F", originalStartFrame);
                    int end = EditorGUILayout.DelayedIntField("Hasta F", originalEndFrame);
                    int repetitions = EditorGUILayout.IntField("Repeticiones", phase.repetitions);
                    DrawGuidedPhasePiece(plan, phase, out string selectedPiece);
                    var pieceAction = (LabGuidedPieceAction)EditorGUILayout.EnumPopup("Acción visual de pieza", phase.pieceAction);
                    var hands = (LabGuidedHands)EditorGUILayout.EnumPopup("Mano(s)", phase.hands);
                    var channels = (LabGuidedChannels)EditorGUILayout.EnumFlagsField("Canales", phase.channels);
                    bool allowOverlap = EditorGUILayout.Toggle("Permitir solapamiento", phase.allowOverlap);
                    var phaseCurve = (LabPathMode)EditorGUILayout.Popup("Curva de fase", (int)phase.pathMode,
                        new[] { "Auto (según perfil)", "Lineal", "Suave", "Arco", "Frenado suave (Ease)", "Cinemática", "Bézier", "Flujo" });
                    float extraArc = EditorGUILayout.FloatField("Arco extra (cm)", phase.arc);

                    int gripIndex = phase.gripPreset + 1;
                    var gripOptions = new List<string> { "Auto · según rol (" + LabGripTools.Names[LabGripTools.PresetForRole(role)] + ")" };
                    gripOptions.AddRange(LabGripTools.Names);
                    int newGripIndex = EditorGUILayout.Popup("Agarre de dedos", gripIndex, gripOptions.ToArray());
                    int newGripPreset = newGripIndex - 1;

                    var blendMode = (LabFingerBlend)EditorGUILayout.Popup("Llegada dedos", (int)phase.fingerBlend,
                        new[] { "Original (compatible)", "Lineal", "Suave", "Instantánea" });
                    float blendDuration = EditorGUILayout.Slider("Duración transición dedos", phase.fingerBlendDuration, 0f, 0.5f);
                    string customLabel = EditorGUILayout.TextField("Etiqueta dedos", phase.fingerLabel);

                    // Quick Jump to phase start / end
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("⏱ Ir a F " + originalStartFrame, EditorStyles.miniButtonLeft))
                            Safe(() => Seek(phase.start));
                        if (GUILayout.Button("⏱ Ir a F " + originalEndFrame, EditorStyles.miniButtonRight))
                            Safe(() => Seek(phase.end));
                    }

                    int suggestedGrip = LabGripTools.PresetForRole(phase.role);
                    if (GUILayout.Button("✋ Agarre táctico sugerido: " + LabGripTools.Names[suggestedGrip], EditorStyles.miniButton))
                    {
                        Safe(() =>
                        {
                            phase.gripPreset = suggestedGrip;
                            phase.fingerLabel = LabGripTools.Names[suggestedGrip];
                            if (preview != null && preview.Animator != null)
                            {
                                SetFingerPose(LabGripTools.Preset(preview.Animator, side, suggestedGrip));
                                fingerPoseLabel = LabGripTools.Names[suggestedGrip];
                            }
                        });
                    }

                    if (phase.role == LabGuidedPhaseRole.PreparePresent)
                    {
                        if (GUILayout.Button("📐 Inclinación táctica arma (Canting -15°)", EditorStyles.miniButton))
                            Safe(() => ApplyGuidedWeaponCant(plan, phase, -15f));
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        Safe(() => ApplyGuidedChange("Editar fase de recarga", () =>
                        {
                            phase.displayName = name;
                            phase.role = role;
                            ApplyGuidedPhaseTiming(phase, start, end,
                                start != originalStartFrame, end != originalEndFrame, lastFrame);
                            phase.repetitions = repetitions;
                            phase.pieceId = selectedPiece;
                            phase.pieceAction = pieceAction;
                            phase.hands = hands;
                            phase.channels = channels;
                            phase.allowOverlap = allowOverlap;
                            phase.pathMode = phaseCurve;
                            phase.arc = extraArc;
                            phase.gripPreset = newGripPreset;
                            phase.fingerBlend = blendMode;
                            phase.fingerBlendDuration = blendDuration;
                            phase.fingerLabel = customLabel;
                        }));
                    }
                }
            }
        }

        private void DrawGuidedPhasePiece(LabGuidedReloadPlan plan, LabGuidedReloadPhase phase, out string selectedPiece)
        {
            var pieces = plan.pieces ?? new List<LabGuidedPieceDefinition>();
            var labels = new List<string> { "Ninguna" };
            labels.AddRange(pieces.Select(p => p != null ? p.displayName : "(pieza vacía)"));
            int pieceIndex = pieces.FindIndex(p => p != null && p.id == phase.pieceId);
            int selected = pieceIndex >= 0 ? pieceIndex + 1 : 0;
            if (!string.IsNullOrEmpty(phase.pieceId) && pieceIndex < 0)
            {
                labels.Add("No encontrada · " + phase.pieceId);
                selected = labels.Count - 1;
            }
            int next = EditorGUILayout.Popup("Pieza objetivo", selected, labels.ToArray());
            selectedPiece = next == selected ? phase.pieceId
                : next <= 0 || next > pieces.Count ? null : pieces[next - 1]?.id;
        }

        private void DrawGuidedReloadCalibration(LabGuidedReloadPlan plan)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Calibración de este equipo / rig", EditorStyles.boldLabel);
            if (plan.calibration == null)
            {
                EditorGUILayout.HelpBox("Falta la calibración; el patrón se conserva, pero no está preparado para resolver anclajes.", MessageType.Warning);
                if (GUILayout.Button("Crear calibración vacía"))
                    Safe(() => ApplyGuidedChange("Crear calibración de recarga",
                        () => plan.calibration = new LabGuidedReloadCalibration { equipmentId = draft.equipmentId }));
                return;
            }

            EditorGUILayout.LabelField("Equipo", string.IsNullOrEmpty(plan.calibration.equipmentId) ? "Sin asignar" : plan.calibration.equipmentId);
            if (!string.Equals(plan.calibration.equipmentId, draft.equipmentId, StringComparison.Ordinal)
                && GUILayout.Button("Usar el equipo de esta toma"))
                Safe(() => ApplyGuidedChange("Asignar equipo de calibración",
                    () => plan.calibration.equipmentId = draft.equipmentId));

            EditorGUI.BeginChangeCheck();
            string rigId = EditorGUILayout.DelayedTextField(new GUIContent("ID de rig/avatar", "Identificador descriptivo opcional. No se infiere por el nombre del modelo."),
                plan.calibration.rigId);
            if (EditorGUI.EndChangeCheck())
                Safe(() => ApplyGuidedChange("Editar identificación del rig", () => plan.calibration.rigId = rigId));

            if (plan.calibration.anchors == null)
            {
                EditorGUILayout.HelpBox("Falta la lista de anclajes; no se reconstruirá automáticamente.", MessageType.Warning);
                if (GUILayout.Button("Crear lista de anclajes"))
                    Safe(() => ApplyGuidedChange("Crear lista de anclajes",
                        () => plan.calibration.anchors = new List<LabGuidedAnchor>()));
                return;
            }

            if (plan.phases == null || plan.phases.Count == 0)
                EditorGUILayout.HelpBox("Añade una fase y asígnale una mano para capturar su pose local de muñeca.", MessageType.Info);
            else
            {
                guidedAnchorPhaseIndex = Mathf.Clamp(guidedAnchorPhaseIndex, 0, plan.phases.Count - 1);
                var phaseLabels = plan.phases.Select((p, i) => p == null
                    ? (i + 1) + " · fase vacía"
                    : (i + 1) + " · " + p.displayName).ToArray();
                guidedAnchorPhaseIndex = EditorGUILayout.Popup("Fase para calibrar", guidedAnchorPhaseIndex, phaseLabels);
                var selectedPhase = plan.phases[guidedAnchorPhaseIndex];
                if (selectedPhase == null)
                    EditorGUILayout.HelpBox("La fase está vacía; no se puede capturar una pose asociada.", MessageType.Warning);
                else
                {
                    int lastFrame = LabTimeline.LastFrame(draft.action.duration);
                    int phaseStartFrame = GuidedTimeToFrame(selectedPhase.start, lastFrame);
                    int phaseEndFrame = GuidedTimeToFrame(selectedPhase.end, lastFrame);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("⏱ Ir a inicio · F " + phaseStartFrame))
                            Safe(() => Seek(selectedPhase.start));
                        if (GUILayout.Button("⏱ Ir a fin · F " + phaseEndFrame))
                            Safe(() => Seek(selectedPhase.end));
                        if (guidedAnchorPhaseIndex > 0)
                        {
                            if (GUILayout.Button("⎘ Enlazar con fin de fase anterior"))
                            {
                                Safe(() => ApplyGuidedChange("Enlazar anclaje de inicio con fase anterior", () =>
                                {
                                    int copied = LabGuidedReloadEditing.CopyPreviousPhaseAnchors(plan, guidedAnchorPhaseIndex);
                                    if (copied > 0)
                                        notice = $"Se enlazaron {copied} anclaje(s) con la salida de la fase anterior en F {phaseStartFrame}.";
                                }));
                            }
                        }
                    }

                    EditorGUILayout.HelpBox("Mueve el cabezal al fotograma que quieras calibrar. Muñecas: local a guardia; piezas/mecanismos: local al padre vinculado; arma: pose evaluada del holder local a su padre. Solo se guardan valores e IDs, nunca referencias Transform.", MessageType.None);
                    bool hasAssignedHand = (selectedPhase.hands & LabGuidedHands.Both) != 0;
                    bool capturesWrist = (selectedPhase.channels & LabGuidedChannels.Wrist) != 0;
                    using (new EditorGUI.DisabledScope(!hasAssignedHand || !capturesWrist || preview == null))
                        if (GUILayout.Button("Capturar muñeca asignada · F " + CurrentFrame))
                            Safe(() => ApplyGuidedChange("Capturar anclaje de muñeca", () =>
                                CaptureGuidedPhaseAnchors(plan, selectedPhase)));
                    if (!hasAssignedHand)
                        EditorGUILayout.HelpBox("Asigna izquierda, derecha o ambas en esta fase antes de capturar.", MessageType.Warning);
                    else if (!capturesWrist)
                        EditorGUILayout.HelpBox("Activa el canal Muñeca en esta fase para capturar una pose de muñeca.", MessageType.Info);

                    if (!string.IsNullOrEmpty(selectedPhase.pieceId))
                    {
                        var pieceBinding = plan.calibration.bindings?
                            .FirstOrDefault(b => b != null && b.pieceId == selectedPhase.pieceId);
                        Transform pieceTransform = pieceBinding != null
                            ? ResolveGuidedObject(pieceBinding.objectId) : null;

                        // Hito 3 ampliado: muñeca, arma y dedos. Pieza y Mecanismo se bloquean con diagnóstico.
                        if ((selectedPhase.channels & LabGuidedChannels.Piece) != 0)
                        {
                            EditorGUILayout.HelpBox(
                                "Canal 'Pieza' no soportado en Hito 3 ampliado. Se generan muñeca, arma y dedos. " +
                                "Este canal se habilitará en una ampliación posterior (Hito 3 ampliado - Piezas).",
                                MessageType.Warning);
                        }
                        if ((selectedPhase.channels & LabGuidedChannels.Mechanism) != 0)
                        {
                            EditorGUILayout.HelpBox(
                                "Canal 'Mecanismo' no soportado en Hito 3 ampliado. Se generan muñeca, arma y dedos. " +
                                "Este canal se habilitará en una ampliación posterior (Hito 3 ampliado - Mecanismo).",
                                MessageType.Warning);
                        }
                        if ((selectedPhase.channels & (LabGuidedChannels.Piece | LabGuidedChannels.Mechanism)) != 0
                            && pieceBinding == null)
                            EditorGUILayout.HelpBox("Vincula la pieza objetivo a una referencia part: o prop: antes de capturar su pose.", MessageType.Warning);
                        else if ((selectedPhase.channels & (LabGuidedChannels.Piece | LabGuidedChannels.Mechanism)) != 0
                            && pieceTransform == null)
                            EditorGUILayout.HelpBox("La referencia de pieza/mecanismo no está resuelta en el equipo/preview actual.", MessageType.Warning);
                        if (pieceBinding != null && pieceTransform != null)
                        {
                            EditorGUILayout.LabelField("La pose se guarda respecto al padre actual; solo se conserva el ID estable y los valores locales, no el Transform.", EditorStyles.wordWrappedMiniLabel);
                        }
                    }

                    if ((selectedPhase.channels & LabGuidedChannels.Weapon) != 0)
                    {
                        Transform weaponRoot = preview != null ? preview.Equipment.weaponPose.PoseRoot : null;
                        using (new EditorGUI.DisabledScope(weaponRoot == null || weaponRoot.parent == null))
                            if (GUILayout.Button("Capturar pose del holder del arma · F " + CurrentFrame))
                                Safe(() => ApplyGuidedChange("Capturar anclaje del holder del arma", () =>
                                    CaptureGuidedWeaponAnchor(plan, selectedPhase)));
                        EditorGUILayout.LabelField("Se registra la pose evaluada del holder en este fotograma, local a su padre; es solo referencia, no se anima ni hornea la raíz visual calibrada.", EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }

            if (plan.calibration.anchors.Count == 0)
                EditorGUILayout.HelpBox("Sin anclajes capturados. La secuencia sigue siendo editable; la generación deberá esperar a una calibración suficiente.", MessageType.Warning);
            else
            {
                EditorGUILayout.LabelField("Anclajes locales capturados: " + plan.calibration.anchors.Count, EditorStyles.boldLabel);
                int lastFrame = LabTimeline.LastFrame(draft.action.duration);
                foreach (var anchor in plan.calibration.anchors.Where(a => a != null).ToArray())
                {
                    string phaseName = plan.phases?.FirstOrDefault(p => p != null && p.id == anchor.phaseId)?.displayName
                        ?? anchor.role ?? "Anclaje";
                    var anchorKind = LabGuidedAnchorTools.EffectiveKind(anchor);
                    string targetName = anchorKind == LabGuidedAnchorKind.Wrist
                        ? anchor.hand == LabGuidedHands.Left ? "muñeca izquierda"
                            : anchor.hand == LabGuidedHands.Right ? "muñeca derecha" : "muñeca sin mano"
                        : anchorKind == LabGuidedAnchorKind.Weapon ? "holder del arma"
                        : anchorKind == LabGuidedAnchorKind.Mechanism ? "mecanismo"
                        : "pieza";
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                phaseName + " · " + targetName + " · F " + GuidedTimeToFrame(anchor.time, lastFrame),
                                EditorStyles.boldLabel);
                            if (GUILayout.Button("Quitar", GUILayout.Width(56)))
                                Safe(() => ApplyGuidedChange("Quitar anclaje", () =>
                                    plan.calibration.anchors.RemoveAll(a => a != null && a.id == anchor.id)));
                        }
                        EditorGUI.BeginChangeCheck();
                        Vector3 localPositionCm = EditorGUILayout.Vector3Field("Posición local · cm", anchor.position * 100f);
                        Vector3 localEuler = EditorGUILayout.Vector3Field("Giro local · °", anchor.rotation.eulerAngles);
                        if (EditorGUI.EndChangeCheck())
                            Safe(() => ApplyGuidedChange("Corregir anclaje local", () =>
                                LabGuidedAnchorCapture.CorrectLocalPose(anchor, localPositionCm * 0.01f, localEuler)));
                    }
                }
            }
        }

        private void CaptureGuidedPhaseAnchors(LabGuidedReloadPlan plan, LabGuidedReloadPhase phase)
        {
            CaptureGuidedHandAnchor(plan, phase, phase.hands);
        }

        private void CaptureGuidedHandAnchor(LabGuidedReloadPlan plan, LabGuidedReloadPhase phase, LabGuidedHands hand)
        {
            preview.Evaluate(draft, progress, pitch, yaw, aim);
            if ((hand & LabGuidedHands.Left) != 0)
            {
                var chain = preview.Equipment.handIK.leftHand;
                LabGuidedAnchorCapture.CaptureHand(plan, phase, HandGripToolkit.HandSide.Left,
                    preview.Guard(HandGripToolkit.HandSide.Left), chain.target != null ? chain.target : chain.hand, progress);
            }
            if ((hand & LabGuidedHands.Right) != 0)
            {
                var chain = preview.Equipment.handIK.rightHand;
                LabGuidedAnchorCapture.CaptureHand(plan, phase, HandGripToolkit.HandSide.Right,
                    preview.Guard(HandGripToolkit.HandSide.Right), chain.target != null ? chain.target : chain.hand, progress);
            }
        }

        private void CaptureGuidedBoundObject(
            LabGuidedReloadPlan plan, LabGuidedReloadPhase phase, string objectId, LabGuidedAnchorKind kind)
        {
            preview.Evaluate(draft, progress, pitch, yaw, aim);
            var piece = ResolveGuidedObject(objectId);
            if (piece == null)
                throw new InvalidOperationException("La referencia de pieza/mecanismo dejó de estar disponible en el preview.");
            if (kind == LabGuidedAnchorKind.Mechanism)
                LabGuidedAnchorCapture.CaptureMechanism(plan, phase, piece, objectId, progress);
            else
                LabGuidedAnchorCapture.CapturePiece(plan, phase, piece, objectId, progress);
        }

        private void CaptureGuidedWeaponAnchor(LabGuidedReloadPlan plan, LabGuidedReloadPhase phase)
        {
            preview.Evaluate(draft, progress, pitch, yaw, aim);
            LabGuidedAnchorCapture.CaptureWeapon(plan, phase, preview.Equipment.weaponPose.PoseRoot, progress);
        }

        private Transform ResolveGuidedObject(string objectId)
        {
            if (string.IsNullOrEmpty(objectId) || preview == null) return null;
            if (objectId.StartsWith("part:", StringComparison.Ordinal))
                return preview.Objects.ResolvePart(objectId.Substring(5));
            if (objectId.StartsWith("prop:", StringComparison.Ordinal))
                return preview.Objects.ResolveProp(objectId.Substring(5));
            return null;
        }

        private void DrawGuidedReloadDiagnostics(LabGuidedReloadPlan plan)
        {
            var ids = ObjectIds();
            var availableHands = LabGuidedHands.None;
            if (preview.Animator.GetBoneTransform(HumanBodyBones.LeftHand) != null)
                availableHands |= LabGuidedHands.Left;
            if (preview.Animator.GetBoneTransform(HumanBodyBones.RightHand) != null)
                availableHands |= LabGuidedHands.Right;
            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = draft.equipmentId,
                availableHands = availableHands,
                availableObjectIds = new HashSet<string>(ids, StringComparer.Ordinal),
                requireExportablePartMotion = plan.generationMode == LabGuidedGenerationMode.SelectedChannels
            };
            var issues = LabGuidedReloadValidation.Inspect(plan, context);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("La estructura del patrón es válida. La compatibilidad visual de manos/anclajes aún requiere revisión en el visor.", MessageType.Info);
                return;
            }
            foreach (var issue in issues.Take(8))
                EditorGUILayout.HelpBox((issue.itemId != null ? issue.itemId + ": " : "") + issue.message,
                    issue.severity == LabGuidedIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            if (issues.Count > 8)
                EditorGUILayout.LabelField("Y " + (issues.Count - 8) + " diagnóstico(s) más…", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Hito 3 ampliado: la generación compila muñecas, arma y dedos a un candidato reversible. " +
                "Piezas y mecanismos bloquean la generación con diagnóstico; no producen resultado parcial. " +
                "No altera el borrador hasta presionar Aceptar candidato.",
                MessageType.None);
            using (new EditorGUI.DisabledScope(HasTakeTrial || quarantinedRecovery || preview == null))
                if (GUILayout.Button("▶ Generar propuesta de recarga (Candidato)", GUILayout.Height(32)))
                    Safe(StartGuidedReloadTrial);
        }

        private bool ApplyGuidedChange(string undoLabel, Action change)
        {
            if (HasTakeTrial || quarantinedRecovery)
                throw new InvalidOperationException("Termina la prueba o reparación antes de editar la recarga guiada.");
            if (!CanLeaveFrame()) return false;
            Undo.RecordObject(draft, undoLabel);
            change();
            EditorUtility.SetDirty(draft);
            dirty = true;
            RebuildReviewCache();
            return true;
        }

        internal static int GuidedTimeToFrame(float time, int lastFrame)
        {
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(time) * lastFrame), 0, lastFrame);
        }

        internal static float GuidedFrameToTime(int frame, int lastFrame) =>
            lastFrame <= 0 ? 0f : frame >= lastFrame ? 1f : Mathf.Clamp(frame, 0, lastFrame) / (float)lastFrame;

        internal static void ApplyGuidedPhaseTiming(
            LabGuidedReloadPhase phase, int startFrame, int endFrame,
            bool startChanged, bool endChanged, int lastFrame)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            if (startChanged) phase.start = GuidedFrameToTime(startFrame, lastFrame);
            if (endChanged) phase.end = GuidedFrameToTime(endFrame, lastFrame);
        }

        private void ApplyGuidedWeaponCant(LabGuidedReloadPlan plan, LabGuidedReloadPhase phase, float cantAngle)
        {
            var anchors = plan.calibration.anchors.Where(a => a.phaseId == phase.id
                && LabGuidedAnchorTools.EffectiveKind(a) == LabGuidedAnchorKind.Weapon).ToArray();
            if (anchors.Length == 0)
                throw new InvalidOperationException("Captura primero el ancla del arma en esta fase.");
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(draft, "Aplicar rotación táctica de arma");
            foreach (var anchor in anchors)
                anchor.rotation = anchor.rotation * Quaternion.Euler(cantAngle * 0.25f, 0, cantAngle);
            dirty = true;
            notice = "Inclinación táctica (" + cantAngle + "°) aplicada al ancla del arma en '" + phase.displayName + "'.";
        }
    }
}
