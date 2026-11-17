using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabGuidedReloadChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar esquema de recargas guiadas")]
        public static void Run()
        {
            AnimationLabTake source = null, clone = null, restored = null;
            string savedPath = null;
            AnimationLabTake saved = null;
            try
            {
                source = ScriptableObject.CreateInstance<AnimationLabTake>();
                source.action = ScriptableObject.CreateInstance<EquipmentActionDefinition>();
                source.guidedReload = CreatePlan();

                CheckSerializableRoundTrip(source.guidedReload);
                CheckPureValidation(source.guidedReload);
                CheckEditingOperations(source.guidedReload);
                CheckGeneratorCompiler();
                CheckMotionProfiles();
                CheckPieceCoordination();
                CheckFingerCoordination();
                CheckMechanismCoordination();
                CheckGuidedReloadWizard();
                CheckAK12FixtureTest();

                clone = LabAssetIO.Clone(source);
                Require(clone.guidedReload != source.guidedReload
                    && clone.guidedReload.phases[0] != source.guidedReload.phases[0]
                    && clone.guidedReload.pieces[0] != source.guidedReload.pieces[0]
                    && clone.guidedReload.calibration != source.guidedReload.calibration
                    && clone.guidedReload.calibration.bindings[0] != source.guidedReload.calibration.bindings[0]
                    && clone.guidedReload.calibration.anchors[0] != source.guidedReload.calibration.anchors[0],
                    "take clone deep-copies every guided-reload data layer");
                clone.guidedReload.phases[0].displayName = "Mutación del clon";
                Require(source.guidedReload.phases[0].displayName == "Extraer usado",
                    "clone mutation never reaches the source plan");
                clone.guidedReload.phases[0].displayName = "Extraer usado";

                using (new LabAuthoringTransaction(clone))
                {
                    clone.guidedReload.phases[0].displayName = "Cambio temporal";
                    clone.guidedReload.calibration.anchors[0].time = 0.35f;
                }
                Require(clone.guidedReload.phases[0].displayName == "Extraer usado"
                    && clone.guidedReload.calibration.anchors[0].time == 0.2f,
                    "authoring transaction rollback restores guided-reload metadata and calibration");

                var envelope = LabEmergencyRecovery.Capture(source, "Guided schema check", "", "", null);
                var envelopeCopy = JsonUtility.FromJson<LabEmergencyRecovery.Envelope>(JsonUtility.ToJson(envelope));
                restored = LabEmergencyRecovery.Restore(envelopeCopy);
                Require(JsonUtility.ToJson(restored.guidedReload) == JsonUtility.ToJson(source.guidedReload),
                    "emergency recovery round-trip retains the portable guided-reload snapshot");

                var invalidTake = LabAssetIO.Clone(source);
                try
                {
                    invalidTake.guidedReload.phases[0].end = float.NaN;
                    Require(LabTakeValidation.Inspect(invalidTake).Any(p => p.message.StartsWith("Recarga guiada:", StringComparison.Ordinal)),
                        "take save validation rejects an invalid configured guided-reload plan");
                }
                finally { LabAssetIO.DestroyDraft(invalidTake); }

                CheckAssetRoundTrip(source, out saved, out savedPath);

                Debug.Log("[AnimationLabGuidedReloadChecks] PASS: portable schema/asset round-trip, pattern editing, legacy inference, guard/parent-local wrist/piece/mechanism/weapon capture and correction, linear generator bounds/continuity/blockers, deep clone, rollback, recovery and non-mutating validation.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(savedPath) && AssetDatabase.LoadAssetAtPath<AnimationLabTake>(savedPath) != null)
                    AssetDatabase.DeleteAsset(savedPath);
                if (saved != null && !AssetDatabase.Contains(saved)) LabAssetIO.DestroyDraft(saved);
                LabAssetIO.DestroyDraft(restored);
                LabAssetIO.DestroyDraft(clone);
                LabAssetIO.DestroyDraft(source);
            }
        }

        private static LabGuidedReloadPlan CreatePlan()
        {
            var plan = new LabGuidedReloadPlan
            {
                configured = true,
                patternId = "pattern-test",
                patternName = "Recarga de prueba",
                generationMode = LabGuidedGenerationMode.SelectedChannels,
                calibration = new LabGuidedReloadCalibration
                {
                    calibrationId = "calibration-test",
                    equipmentId = "AK12",
                    rigId = "fixture-test"
                }
            };
            plan.pieces.Add(new LabGuidedPieceDefinition
            {
                id = "spent-mag",
                displayName = "Cargador usado",
                kind = LabGuidedPieceKind.DetachableMagazine,
                suggestedCount = 1
            });
            plan.pieces.Add(new LabGuidedPieceDefinition
            {
                id = "replacement-mag",
                displayName = "Cargador de reemplazo",
                kind = LabGuidedPieceKind.DetachableMagazine,
                suggestedCount = 1
            });
            plan.phases.Add(new LabGuidedReloadPhase
            {
                id = "extract",
                displayName = "Extraer usado",
                role = LabGuidedPhaseRole.Extract,
                start = 0f,
                end = 0.4f,
                pieceId = "spent-mag",
                pieceAction = LabGuidedPieceAction.Extract,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece
            });
            plan.phases.Add(new LabGuidedReloadPhase
            {
                id = "insert",
                displayName = "Insertar reemplazo",
                role = LabGuidedPhaseRole.Insert,
                start = 0.4f,
                end = 1f,
                pieceId = "replacement-mag",
                pieceAction = LabGuidedPieceAction.Insert,
                hands = LabGuidedHands.Both,
                channels = LabGuidedChannels.Fingers | LabGuidedChannels.Piece
            });
            plan.calibration.bindings.Add(new LabGuidedPieceBinding
            {
                pieceId = "spent-mag",
                objectId = "part:AK12Rig/Root/Mag"
            });
            plan.calibration.bindings.Add(new LabGuidedPieceBinding
            {
                pieceId = "replacement-mag",
                objectId = "prop:replacement-mag"
            });
            plan.calibration.anchors.Add(new LabGuidedAnchor
            {
                id = "left-guard",
                phaseId = "extract",
                role = "Extract",
                hand = LabGuidedHands.Left,
                space = LabGuidedAnchorSpace.Guard,
                time = 0.2f,
                position = Vector3.zero,
                rotation = Quaternion.identity
            });
            plan.calibration.anchors.Add(new LabGuidedAnchor
            {
                id = "mag-parent",
                phaseId = "extract",
                role = "Extract",
                kind = LabGuidedAnchorKind.Piece,
                pieceId = "spent-mag",
                objectId = "part:AK12Rig/Root/Mag",
                space = LabGuidedAnchorSpace.Parent,
                time = 0.3f,
                position = new Vector3(0.01f, -0.02f, 0.03f),
                rotation = Quaternion.Euler(2f, 0f, -1f)
            });
            return plan;
        }

        private static void CheckGeneratorCompiler()
        {
            var plan = new LabGuidedReloadPlan
            {
                configured = true,
                generationMode = LabGuidedGenerationMode.SelectedChannels,
                calibration = new LabGuidedReloadCalibration
                {
                    calibrationId = "generator-calibration",
                    equipmentId = "AK12"
                }
            };
            var first = new LabGuidedReloadPhase
            {
                id = "travel-a", displayName = "Viajar 1", start = 0.1f, end = 0.4f,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon
            };
            var second = new LabGuidedReloadPhase
            {
                id = "travel-b", displayName = "Viajar 2", start = 0.6f, end = 0.9f,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon
            };
            plan.phases.Add(first);
            plan.phases.Add(second);
            Vector3 wristMiddle = new Vector3(0.1f, 0.02f, 0.03f);
            Vector3 weaponMiddle = new Vector3(0.025f, -0.01f, 0.015f);
            Quaternion wristTurn = Quaternion.Euler(4f, 22f, -3f);
            Quaternion weaponTurn = Quaternion.Euler(2f, -14f, 1f);
            AddAnchor(plan, first, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.1f, Vector3.zero, Quaternion.identity);
            AddAnchor(plan, first, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.4f, wristMiddle, wristTurn);
            AddAnchor(plan, second, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.6f, wristMiddle, wristTurn);
            AddAnchor(plan, second, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.9f, Vector3.zero, Quaternion.identity);
            AddAnchor(plan, first, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, 0.1f, Vector3.zero, Quaternion.identity);
            AddAnchor(plan, first, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, 0.4f, weaponMiddle, weaponTurn);
            AddAnchor(plan, second, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, 0.6f, weaponMiddle, weaponTurn);
            AddAnchor(plan, second, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, 0.9f, Vector3.zero, Quaternion.identity);

            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = "AK12",
                availableHands = LabGuidedHands.Both
            };
            (Vector3 position, Quaternion rotation) Offset(LabGuidedAnchor anchor) => (anchor.position, anchor.rotation);
            var compiled = LabGuidedReloadGenerator.Compile(plan, 2f, Offset, context);
            Require(compiled.wrists.Count == 1 && compiled.wrists.ContainsKey(HandGripToolkit.HandSide.Left)
                && compiled.wrists[HandGripToolkit.HandSide.Left].Count == 4
                && compiled.weapon != null && compiled.weapon.Count == 4,
                "generator compiles only requested wrist/weapon tracks and includes phase endpoints");
            var wrist = LabPathCurves.Build(compiled.wrists[HandGripToolkit.HandSide.Left],
                k => k.time, k => k.position, k => k.pathMode, k => k.bend, false);
            var weapon = LabPathCurves.Objects(compiled.weapon);
            Require(Vector3.Distance(wrist.Evaluate(0.25f), wristMiddle * 0.5f) < 0.00001f
                && Vector3.Distance(wrist.Evaluate(0.5f), wristMiddle) < 0.00001f
                && Vector3.Distance(wrist.Evaluate(0.99f), Vector3.zero) < 0.00001f,
                "wrist path is linear in phases and holds its matching pose through gaps/outside");
            Require(Vector3.Distance(weapon.Evaluate(0.5f), weaponMiddle) < 0.00001f,
                "weapon path holds a continuous pose through an unanimated gap");
            var repeated = LabGuidedReloadGenerator.Compile(plan, 2f, Offset, context);
            Require(repeated.wrists[HandGripToolkit.HandSide.Left].Select(k => k.time)
                    .SequenceEqual(compiled.wrists[HandGripToolkit.HandSide.Left].Select(k => k.time))
                && repeated.weapon.Select(k => k.position).SequenceEqual(compiled.weapon.Select(k => k.position)),
                "same guided-reload snapshot compiles deterministically");

            var missing = LabGuidedReloadPlan.Clone(plan);
            missing.calibration.anchors.RemoveAll(a => a.phaseId == "travel-a"
                && LabGuidedAnchorTools.EffectiveKind(a) == LabGuidedAnchorKind.Wrist
                && Mathf.Abs(a.time - 0.4f) < LabTimeline.TimeEpsilon);
            Require(Throws(() => LabGuidedReloadGenerator.Compile(missing, 2f, Offset, context), "necesita anclas"),
                "missing selected-channel endpoint blocks generation");

            var unsupported = LabGuidedReloadPlan.Clone(plan);
            unsupported.pieces.Add(new LabGuidedPieceDefinition { id = "dummy-piece", displayName = "Dummy", kind = LabGuidedPieceKind.Custom });
            unsupported.calibration.bindings.Add(new LabGuidedPieceBinding { pieceId = "dummy-piece", objectId = "prop:dummy" });
            unsupported.phases[0].channels |= LabGuidedChannels.Piece;
            unsupported.phases[0].pieceId = "dummy-piece";
            Require(Throws(() => LabGuidedReloadGenerator.Compile(unsupported, 2f, Offset, context), "canal aún no compatible"),
                "unsupported channel (Piece) blocks the whole candidate with generator diagnostic");

            var overlap = LabGuidedReloadPlan.Clone(plan);
            overlap.phases[0].allowOverlap = overlap.phases[1].allowOverlap = true;
            overlap.phases[1].start = 0.3f;
            overlap.phases[1].end = 0.8f;
            foreach (var anchor in overlap.calibration.anchors.Where(a => a.phaseId == "travel-b"))
                anchor.time = Mathf.Abs(anchor.time - 0.6f) < LabTimeline.TimeEpsilon ? 0.3f : 0.8f;
            Require(Throws(() => LabGuidedReloadGenerator.Compile(overlap, 2f, Offset, context), "solapan"),
                "overlapping requests on the same output track are rejected even when pattern overlap is allowed");

            var jump = LabGuidedReloadPlan.Clone(plan);
            var nextStart = jump.calibration.anchors.First(a => a.phaseId == "travel-b"
                && LabGuidedAnchorTools.EffectiveKind(a) == LabGuidedAnchorKind.Wrist
                && Mathf.Abs(a.time - 0.6f) < LabTimeline.TimeEpsilon);
            nextStart.position += Vector3.right * 0.01f;
            Require(Throws(() => LabGuidedReloadGenerator.Compile(jump, 2f, Offset, context), "no coincide"),
                "discontinuous phase endpoints are rejected instead of creating a jump");

            var repetitions = LabGuidedReloadPlan.Clone(plan);
            repetitions.phases[0].repetitions = 2;
            Require(Throws(() => LabGuidedReloadGenerator.Compile(repetitions, 2f, Offset, context), "repeticiones"),
                "unsupported repetition semantics are blocked rather than silently ignored");
        }

        private static void CheckMotionProfiles()
        {
            var plan = new LabGuidedReloadPlan
            {
                patternName = "Prueba de perfiles",
                configured = true,
                generationMode = LabGuidedGenerationMode.VisualMotionOnly,
                motionProfile = LabGuidedMotionProfile.Linear,
                calibration = new LabGuidedReloadCalibration
                {
                    equipmentId = "AK12"
                }
            };
            var phase = new LabGuidedReloadPhase
            {
                id = "reach-phase",
                displayName = "Alcanzar",
                role = LabGuidedPhaseRole.Reach,
                start = 0f,
                end = 0.5f,
                repetitions = 1,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Wrist
            };
            plan.phases.Add(phase);
            AddAnchor(plan, phase, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0f, Vector3.zero, Quaternion.identity);
            AddAnchor(plan, phase, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.5f, new Vector3(0.1f, 0.05f, 0.2f), Quaternion.Euler(0, 30, 0));

            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = "AK12",
                availableHands = LabGuidedHands.Both
            };

            // 1. Perfil Linear
            plan.motionProfile = LabGuidedMotionProfile.Linear;
            var linearCompiled = LabGuidedReloadGenerator.Compile(plan, 2f, null, context);
            var linearKeys = linearCompiled.wrists[HandGripToolkit.HandSide.Left];
            Require(linearKeys.All(k => k.pathMode == LabPathMode.Linear), "linear profile produces linear pathMode");
            Require(linearKeys.All(k => k.bend == Vector3.zero), "linear profile produces zero bend");
            Require(linearKeys.All(k => Mathf.Abs(k.departureSpeed - 1f) < 0.001f && Mathf.Abs(k.arrivalSpeed - 1f) < 0.001f), "linear profile uses 1.0 speeds");
            var linearCurve = LabPathCurves.Build(linearKeys, k => k.time, k => k.position, k => k.pathMode, k => k.bend, false);

            // 2. Perfil Controlled
            plan.motionProfile = LabGuidedMotionProfile.Controlled;
            var controlledCompiled = LabGuidedReloadGenerator.Compile(plan, 2f, null, context);
            var controlledKeys = controlledCompiled.wrists[HandGripToolkit.HandSide.Left];
            Require(controlledKeys.All(k => k.pathMode == LabPathMode.Ease), "controlled profile produces Ease pathMode");
            Require(controlledKeys.Any(k => k.bend.y > 0.01f), "controlled profile includes elevation arc in transit");
            Require(controlledKeys.All(k => k.arrivalSpeed < 0.9f), "controlled profile applies cushioned arrival");
            var controlledCurve = LabPathCurves.Build(controlledKeys, k => k.time, k => k.position, k => k.pathMode, k => k.bend, false);

            // 3. Perfil Snappy
            plan.motionProfile = LabGuidedMotionProfile.Snappy;
            var snappyCompiled = LabGuidedReloadGenerator.Compile(plan, 2f, null, context);
            var snappyKeys = snappyCompiled.wrists[HandGripToolkit.HandSide.Left];
            Require(snappyKeys.All(k => k.pathMode == LabPathMode.Cinematic), "snappy profile produces Cinematic pathMode");
            Require(snappyKeys.All(k => k.departureSpeed > 1.2f && k.arrivalSpeed <= 0.3f), "snappy profile applies explosive departure and crisp arrival");
            var snappyCurve = LabPathCurves.Build(snappyKeys, k => k.time, k => k.position, k => k.pathMode, k => k.bend, false);

            // 4. Perfil Fluid
            plan.motionProfile = LabGuidedMotionProfile.Fluid;
            var fluidCompiled = LabGuidedReloadGenerator.Compile(plan, 2f, null, context);
            var fluidKeys = fluidCompiled.wrists[HandGripToolkit.HandSide.Left];
            Require(fluidKeys.All(k => k.pathMode == LabPathMode.Smooth), "fluid profile produces Smooth pathMode");
            Require(fluidKeys.Any(k => k.bend.y > 0.02f), "fluid profile produces broad sweeping arc");

            // 5. Comparación matemática de curvas: deben ser claramente distintas
            float sampleTime = 0.125f; // Earlier in phase where bend difference is more pronounced
            Vector3 linearPos = linearCurve.Evaluate(sampleTime);
            Vector3 controlledPos = controlledCurve.Evaluate(sampleTime);
            Vector3 snappyPos = snappyCurve.Evaluate(sampleTime);
            Require(Vector3.Distance(linearPos, controlledPos) > 0.001f, "controlled profile produces measurably distinct curve from linear");
            Require(Vector3.Distance(controlledPos, snappyPos) > 0.001f, "snappy profile produces measurably distinct curve from controlled");

            // 6. Volver a Linear restaura la base sin acumulación
            plan.motionProfile = LabGuidedMotionProfile.Linear;
            var restoredCompiled = LabGuidedReloadGenerator.Compile(plan, 2f, null, context);
            var restoredKeys = restoredCompiled.wrists[HandGripToolkit.HandSide.Left];
            Require(restoredKeys.All(k => k.pathMode == LabPathMode.Linear && k.bend == Vector3.zero),
                "switching back to linear profile cleanly restores base values without accumulation");
        }

        private static void CheckPieceCoordination()
        {
            // Hito 3 inicial: canal Piece bloqueado. Verificar diagnóstico claro.
            var plan = new LabGuidedReloadPlan
            {
                patternName = "Prueba de bloqueo de pieza",
                configured = true,
                generationMode = LabGuidedGenerationMode.SelectedChannels,
                motionProfile = LabGuidedMotionProfile.Controlled,
                magazinePolicy = LabGuidedMagazinePolicy.ExtractAndReplace,
                calibration = new LabGuidedReloadCalibration
                {
                    calibrationId = "piece-test-calibration",
                    equipmentId = "AK12"
                }
            };
            var magPiece = new LabGuidedPieceDefinition
            {
                id = "spent-mag",
                displayName = "Cargador gastado",
                kind = LabGuidedPieceKind.DetachableMagazine
            };
            plan.pieces.Add(magPiece);
            plan.calibration.bindings.Add(new LabGuidedPieceBinding
            {
                pieceId = "spent-mag",
                objectId = "part:AK12Rig/Root/Mag"
            });

            var extractPhase = new LabGuidedReloadPhase
            {
                id = "phase-extract",
                displayName = "Extraer cargador",
                role = LabGuidedPhaseRole.Extract,
                start = 0.2f,
                end = 0.5f,
                pieceId = "spent-mag",
                pieceAction = LabGuidedPieceAction.Extract,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece
            };
            plan.phases.Add(extractPhase);

            AddAnchor(plan, extractPhase, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.2f, Vector3.zero, Quaternion.identity);
            AddAnchor(plan, extractPhase, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.5f, new Vector3(0.02f, -0.18f, 0.05f), Quaternion.identity);

            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = "AK12",
                availableHands = LabGuidedHands.Both
            };
            (Vector3 position, Quaternion rotation) Offset(LabGuidedAnchor anchor) => (anchor.position, anchor.rotation);

            // 1. GeneratedPieceObjectIds devuelve vacío en Hito 3 inicial
            var pieceObjects = LabGuidedReloadGenerator.GeneratedPieceObjectIds(plan);
            Require(pieceObjects.Length == 0, "GeneratedPieceObjectIds returns empty for unsupported Piece channel in Hito 3 inicial");

            // 2. GeneratesPieces devuelve false
            Require(!LabGuidedReloadGenerator.GeneratesPieces(plan), "GeneratesPieces returns false for Piece channel in Hito 3 inicial");

            // 3. Compilación bloquea canal Piece con diagnóstico claro
            Require(Throws(() => LabGuidedReloadGenerator.Compile(plan, 2.5f, Offset, context), "canal aún no compatible"),
                "Piece channel blocks generation with clear diagnostic 'canal aún no compatible'");

            // 4. Validación también reporta error en modo VisualMotionOnly
            var visualPlan = LabGuidedReloadPlan.Clone(plan);
            visualPlan.generationMode = LabGuidedGenerationMode.VisualMotionOnly;
            var issues = LabGuidedReloadValidation.Inspect(visualPlan, context);
            Require(issues.Any(i => i.code == "phase.visualOnly" && i.severity == LabGuidedIssueSeverity.Error),
                "validation rejects Piece channel in VisualMotionOnly mode");
        }

        private static void CheckFingerCoordination()
        {
            // Hito 3 ampliado: canal Fingers ahora se compila. Verificar generación correcta.
            var plan = new LabGuidedReloadPlan
            {
                configured = true,
                generationMode = LabGuidedGenerationMode.SelectedChannels
            };
            plan.calibration.equipmentId = "AK12";

            var phase1 = new LabGuidedReloadPhase
            {
                id = "reach-phase",
                displayName = "Alcanzar cargador",
                role = LabGuidedPhaseRole.Reach,
                start = 0f,
                end = 0.35f,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Fingers,
                gripPreset = 12, // Tránsito relajado
                fingerBlend = LabFingerBlend.Smooth,
                fingerBlendDuration = 0.12f,
                fingerLabel = "Tránsito relajado"
            };
            var phase2 = new LabGuidedReloadPhase
            {
                id = "acquire-phase",
                displayName = "Asir cargador",
                role = LabGuidedPhaseRole.Acquire,
                start = 0.35f,
                end = 0.7f,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Fingers,
                gripPreset = 5, // Cargador AK · agarre curvo (Rock-and-Lock)
                fingerBlend = LabFingerBlend.Step,
                fingerBlendDuration = 0f,
                fingerLabel = "Cargador AK · agarre curvo"
            };
            plan.phases.Add(phase1);
            plan.phases.Add(phase2);

            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = "AK12",
                availableHands = LabGuidedHands.Both
            };
            (Vector3 position, Quaternion rotation) Offset(LabGuidedAnchor anchor) => (anchor.position, anchor.rotation);

            var createdPoses = new List<HandPoseData>();
            HandPoseData MakePose(string name)
            {
                var p = ScriptableObject.CreateInstance<HandPoseData>();
                p.name = name;
                createdPoses.Add(p);
                return p;
            }

            try
            {
                HandPoseData FingerProvider(LabGuidedReloadPhase p, HandSide s) => MakePose("Pose_" + p.id);
                HandPoseData BaseProvider(HandSide s) => MakePose("Base_F0");

                var compiled = LabGuidedReloadGenerator.Compile(plan, 2f, null, null, FingerProvider, BaseProvider, context);
                Require(compiled.fingers.Count == 1 && compiled.fingers.ContainsKey(HandSide.Left),
                    "finger generator compiles requested left hand finger track");
                Require(compiled.wrists.Count == 0 && !compiled.GeneratesWeapon && !compiled.GeneratesPieces,
                    "requesting only fingers does not generate wrist, weapon or piece tracks");

                var keys = compiled.fingers[HandSide.Left];
                Require(keys.Count == 3, "finger track includes baseline F0 and arrival key for each phase");
                Require(Mathf.Abs(keys[0].time - 0f) < LabTimeline.TimeEpsilon
                    && keys[0].fingerBlend == LabFingerBlend.Step
                    && keys[0].fingerBlendDuration == 0f
                    && keys[0].fingerLabel == "Base inicial",
                    "baseline F0 key preserves initial hand state with step transition");

                Require(Mathf.Abs(keys[1].time - 0.35f) < LabTimeline.TimeEpsilon
                    && keys[1].fingerBlend == LabFingerBlend.Smooth
                    && Mathf.Abs(keys[1].fingerBlendDuration - 0.12f) < 0.001f
                    && keys[1].fingerLabel == "Tránsito relajado",
                    "arrival key 1 matches phase 1 end time, smooth transition mode and duration");

                Require(Mathf.Abs(keys[2].time - 0.7f) < LabTimeline.TimeEpsilon
                    && keys[2].fingerBlend == LabFingerBlend.Step
                    && keys[2].fingerBlendDuration == 0f
                    && keys[2].fingerLabel == "Cargador AK · agarre curvo",
                    "arrival key 2 matches phase 2 end time, step mode and label");

                // Verify finger hand queries
                var changedHands = LabGuidedReloadGenerator.GeneratedHands(plan);
                Require(changedHands.Length == 1 && changedHands[0] == HandSide.Left,
                    "GeneratedHands recognizes hands with finger requests");
                var wristHands = LabGuidedReloadGenerator.GeneratedWristHands(plan);
                Require(wristHands.Length == 0, "GeneratedWristHands is empty when only fingers are selected");
                var fingerHands = LabGuidedReloadGenerator.GeneratedFingerHands(plan);
                Require(fingerHands.Length == 1 && fingerHands[0] == HandSide.Left,
                    "GeneratedFingerHands returns left hand");
                var fingerTimes = LabGuidedReloadGenerator.GeneratedFingerTimes(plan);
                Require(fingerTimes.Contains((HandSide.Left, 0.35f)) && fingerTimes.Contains((HandSide.Left, 0.7f)),
                    "GeneratedFingerTimes tracks phase arrival timestamps");

                // Overlap rejection on finger tracks
                var overlapPlan = LabGuidedReloadPlan.Clone(plan);
                overlapPlan.phases[1].start = 0.2f;
                Require(Throws(() => LabGuidedReloadGenerator.Compile(overlapPlan, 2f, null, null, FingerProvider, BaseProvider, context), "solapan"),
                    "overlapping finger phases on the same hand are rejected");

                // Missing hand assignment rejection
                var missingHand = LabGuidedReloadPlan.Clone(plan);
                missingHand.phases[0].hands = LabGuidedHands.None;
                Require(Throws(() => LabGuidedReloadGenerator.Compile(missingHand, 2f, null, null, FingerProvider, BaseProvider, context), "mano asignada"),
                    "finger phase without hand assignment is rejected");

                // Unsupported channel rejection (Piece/Mechanism still blocked)
                var unknownPlan = LabGuidedReloadPlan.Clone(plan);
                unknownPlan.phases[0].channels |= (LabGuidedChannels)64;
                Require(Throws(() => LabGuidedReloadGenerator.Compile(unknownPlan, 2f, null, null, FingerProvider, BaseProvider, context), "valores desconocidos"),
                    "unsupported channel blocks the candidate with diagnostic (validation catches unknown enum values)");

                // 3. VisualMotionOnly now ALLOWS Fingers (Hito 3 ampliado)
                var visualPlan = LabGuidedReloadPlan.Clone(plan);
                visualPlan.generationMode = LabGuidedGenerationMode.VisualMotionOnly;
                var issues = LabGuidedReloadValidation.Inspect(visualPlan, context);
                Require(!issues.Any(i => i.code == "phase.visualOnly" && i.severity == LabGuidedIssueSeverity.Error),
                    "validation now allows Fingers channel in VisualMotionOnly mode (Hito 3 ampliado)");
            }
            finally
            {
                foreach (var p in createdPoses)
                    if (p != null) UnityEngine.Object.DestroyImmediate(p);
            }
        }

        private static void CheckMechanismCoordination()
        {
            // Hito 3 inicial: canal Mechanism bloqueado. Verificar diagnóstico claro.
            var plan = new LabGuidedReloadPlan
            {
                configured = true,
                generationMode = LabGuidedGenerationMode.SelectedChannels
            };
            plan.calibration.equipmentId = "AK12";

            var handlePiece = new LabGuidedPieceDefinition
            {
                id = "charging-handle",
                displayName = "Palanca de carga",
                kind = LabGuidedPieceKind.ChargingHandle,
                suggestedCount = 1
            };
            plan.pieces.Add(handlePiece);
            plan.calibration.bindings.Add(new LabGuidedPieceBinding
            {
                pieceId = "charging-handle",
                objectId = "part:AK12Rig/Root/Charge"
            });

            var operatePhase = new LabGuidedReloadPhase
            {
                id = "operate-bolt",
                displayName = "Montar cerrojo",
                role = LabGuidedPhaseRole.OperateMechanism,
                start = 0.7f,
                end = 0.95f,
                pieceId = "charging-handle",
                pieceAction = LabGuidedPieceAction.Operate,
                hands = LabGuidedHands.Left,
                channels = LabGuidedChannels.Wrist | LabGuidedChannels.Mechanism,
                gripPreset = -1,
                fingerBlend = LabFingerBlend.Smooth,
                fingerBlendDuration = 0.1f
            };
            plan.phases.Add(operatePhase);

            AddAnchor(plan, operatePhase, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.7f, new Vector3(0.01f, 0.04f, 0.02f), Quaternion.identity);
            AddAnchor(plan, operatePhase, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, 0.95f, new Vector3(0.02f, 0.03f, -0.04f), Quaternion.identity);

            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = "AK12",
                availableHands = LabGuidedHands.Both
            };
            (Vector3 position, Quaternion rotation) Offset(LabGuidedAnchor anchor) => (anchor.position, anchor.rotation);

            // 1. GeneratesMechanisms y GeneratesPieces devuelven false en Hito 3 inicial
            Require(!LabGuidedReloadGenerator.GeneratesMechanisms(plan), "GeneratesMechanisms returns false for Mechanism channel in Hito 3 inicial");
            Require(!LabGuidedReloadGenerator.GeneratesPieces(plan), "GeneratesPieces returns false for Mechanism channel in Hito 3 inicial");
            Require(LabGuidedReloadGenerator.GeneratedPieceObjectIds(plan).Length == 0, "GeneratedPieceObjectIds returns empty for unsupported Mechanism channel");

            // 2. Compilación bloquea canal Mechanism con diagnóstico claro
            Require(Throws(() => LabGuidedReloadGenerator.Compile(plan, 2f, Offset, context), "canal aún no compatible"),
                "Mechanism channel blocks generation with clear diagnostic 'canal aún no compatible'");

            // 3. Validación también reporta error en modo VisualMotionOnly
            var visualPlan = LabGuidedReloadPlan.Clone(plan);
            visualPlan.generationMode = LabGuidedGenerationMode.VisualMotionOnly;
            var issues = LabGuidedReloadValidation.Inspect(visualPlan, context);
            Require(issues.Any(i => i.code == "phase.visualOnly" && i.severity == LabGuidedIssueSeverity.Error),
                "validation rejects Mechanism channel in VisualMotionOnly mode");
        }

        private static void AddAnchor(LabGuidedReloadPlan plan, LabGuidedReloadPhase phase,
            LabGuidedAnchorKind kind, LabGuidedHands hand, float time, Vector3 position, Quaternion rotation)
        {
            plan.calibration.anchors.Add(new LabGuidedAnchor
            {
                id = Guid.NewGuid().ToString("N"),
                phaseId = phase.id,
                role = phase.role.ToString(),
                kind = kind,
                hand = hand,
                space = kind == LabGuidedAnchorKind.Wrist
                    ? LabGuidedAnchorSpace.Guard : LabGuidedAnchorSpace.Parent,
                time = time,
                position = position,
                rotation = rotation
            });
        }

        private static bool Throws(Action action, string messagePart)
        {
            try { action(); }
            catch (InvalidOperationException ex)
            { return ex.Message.IndexOf(messagePart, StringComparison.OrdinalIgnoreCase) >= 0; }
            return false;
        }

        private static void CheckSerializableRoundTrip(LabGuidedReloadPlan source)
        {
            string json = JsonUtility.ToJson(source);
            var restored = JsonUtility.FromJson<LabGuidedReloadPlan>(json);
            Require(restored != null && restored.version == LabGuidedReloadPlan.CurrentVersion
                && restored.patternId == source.patternId && restored.patternName == source.patternName
                && restored.generationMode == source.generationMode
                && restored.pieces.Count == 2 && restored.phases.Count == 2
                && restored.phases[1].hands == LabGuidedHands.Both
                && restored.phases[1].channels == (LabGuidedChannels.Fingers | LabGuidedChannels.Piece)
                && restored.calibration.bindings[1].objectId == "prop:replacement-mag"
                && restored.calibration.anchors[0].phaseId == "extract"
                && restored.calibration.anchors[0].hand == LabGuidedHands.Left
                && Mathf.Abs(restored.calibration.anchors[0].time - 0.2f) < 0.000001f
                && restored.calibration.anchors.Count == 2
                && restored.calibration.anchors[1].kind == LabGuidedAnchorKind.Piece
                && restored.calibration.anchors[1].space == LabGuidedAnchorSpace.Parent
                && restored.calibration.anchors[1].objectId == "part:AK12Rig/Root/Mag",
                "JsonUtility preserves the versioned pattern/calibration snapshot");
        }

        private static void CheckAssetRoundTrip(AnimationLabTake source, out AnimationLabTake saved, out string path)
        {
            saved = LabAssetIO.Clone(source);
            saved.hideFlags = HideFlags.None;
            saved.action.hideFlags = HideFlags.None;
            path = AssetDatabase.GenerateUniqueAssetPath(LabAssetIO.Output + "/__GuidedReloadSchemaCheck.asset");
            AssetDatabase.CreateAsset(saved, path);
            AssetDatabase.AddObjectToAsset(saved.action, saved);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(saved.action);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path);
            Require(loaded != null && loaded.guidedReload != null
                && JsonUtility.ToJson(loaded.guidedReload) == JsonUtility.ToJson(source.guidedReload),
                "saved take asset retains its guided-reload pattern and calibration snapshot");
        }

        private static void CheckPureValidation(LabGuidedReloadPlan source)
        {
            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = "AK12",
                rigId = "fixture-test",
                availableHands = LabGuidedHands.Both,
                availableObjectIds = new HashSet<string>(new[]
                {
                    "part:AK12Rig/Root/Mag",
                    "prop:replacement-mag"
                }, StringComparer.Ordinal)
            };
            string before = JsonUtility.ToJson(source);
            var validIssues = LabGuidedReloadValidation.Inspect(source, context);
            Require(!validIssues.Any(i => i.severity == LabGuidedIssueSeverity.Error),
                "valid user-defined pattern and matching rig have no errors");
            Require(before == JsonUtility.ToJson(source), "successful validation does not mutate source data");
            context.requireExportablePartMotion = true;
            Require(LabGuidedReloadValidation.Inspect(source, context).Any(i => i.code == "binding.referenceOnly"),
                "reference-only props are distinguished from exportable equipment pieces");
            context.requireExportablePartMotion = false;

            var earlyV1 = LabGuidedReloadPlan.Clone(source);
            earlyV1.calibration.anchors[1].kind = LabGuidedAnchorKind.Unspecified;
            var earlyV1Issues = LabGuidedReloadValidation.Inspect(earlyV1, context);
            Require(LabGuidedAnchorTools.EffectiveKind(earlyV1.calibration.anchors[0]) == LabGuidedAnchorKind.Wrist
                && LabGuidedAnchorTools.EffectiveKind(earlyV1.calibration.anchors[1]) == LabGuidedAnchorKind.Piece
                && !earlyV1Issues.Any(i => i.code == "anchor.kind"),
                "early v1 anchors infer unambiguous wrist/piece kinds without mutating serialized data");
            var ambiguousWrist = LabGuidedReloadPlan.Clone(earlyV1);
            ambiguousWrist.calibration.anchors[0].objectId = "part:Unexpected";
            var ambiguousPiece = LabGuidedReloadPlan.Clone(earlyV1);
            ambiguousPiece.calibration.anchors[1].hand = LabGuidedHands.Left;
            Require(LabGuidedAnchorTools.EffectiveKind(ambiguousWrist.calibration.anchors[0])
                    == LabGuidedAnchorKind.Unspecified
                && LabGuidedAnchorTools.EffectiveKind(ambiguousPiece.calibration.anchors[1])
                    == LabGuidedAnchorKind.Unspecified
                && LabGuidedReloadValidation.Inspect(ambiguousWrist).Any(i => i.code == "anchor.kind")
                && LabGuidedReloadValidation.Inspect(ambiguousPiece).Any(i => i.code == "anchor.kind"),
                "ambiguous early v1 anchors are not guessed into a type and remain validation errors");

            var orphanedAnchor = LabGuidedReloadPlan.Clone(source);
            orphanedAnchor.calibration.anchors[1].phaseId = null;
            orphanedAnchor.calibration.anchors[1].pieceId = null;
            var orphanIssues = LabGuidedReloadValidation.Inspect(orphanedAnchor);
            Require(orphanIssues.Any(i => i.code == "anchor.phase")
                && orphanIssues.Any(i => i.code == "anchor.pieceRequired"),
                "piece/mechanism anchors cannot remain orphaned from their phase or abstract piece");

            var missingChannelAnchors = LabGuidedReloadPlan.Clone(source);
            missingChannelAnchors.phases[0].channels |= LabGuidedChannels.Weapon | LabGuidedChannels.Mechanism;
            var missingAnchorIssues = LabGuidedReloadValidation.Inspect(missingChannelAnchors);
            Require(missingAnchorIssues.Any(i => i.code == "anchor.weaponRequired")
                && missingAnchorIssues.Any(i => i.code == "anchor.mechanismRequired"),
                "weapon and mechanism channels each report their missing phase-local calibration");

            var authorizedOverlap = LabGuidedReloadPlan.Clone(source);
            authorizedOverlap.phases[0].end = 0.5f;
            authorizedOverlap.phases[0].allowOverlap = true;
            authorizedOverlap.phases[1].start = 0.3f;
            authorizedOverlap.phases[1].allowOverlap = true;
            Require(!LabGuidedReloadValidation.Inspect(authorizedOverlap, context).Any(i => i.code == "phase.overlap"),
                "explicitly authorized phase overlap is accepted");

            var revolverPattern = new LabGuidedReloadPlan
            {
                configured = true,
                patternId = "revolver-pattern",
                patternName = "Cargar tambor",
                generationMode = LabGuidedGenerationMode.SelectedChannels
            };
            revolverPattern.pieces.Add(new LabGuidedPieceDefinition
            {
                id = "cylinder",
                displayName = "Tambor",
                kind = LabGuidedPieceKind.RevolverCylinder
            });
            revolverPattern.phases.Add(new LabGuidedReloadPhase
            {
                id = "operate-cylinder",
                displayName = "Abrir tambor",
                role = LabGuidedPhaseRole.OperateMechanism,
                start = 0f,
                end = 1f,
                pieceId = "cylinder",
                pieceAction = LabGuidedPieceAction.Operate,
                hands = LabGuidedHands.Right,
                channels = LabGuidedChannels.Mechanism
            });
            Require(!LabGuidedReloadValidation.Inspect(revolverPattern).Any(i => i.severity == LabGuidedIssueSeverity.Error),
                "a custom revolver pattern validates without any magazine phase");

            var sharedReference = LabGuidedReloadPlan.Clone(source);
            sharedReference.calibration.bindings[1].objectId = sharedReference.calibration.bindings[0].objectId;
            Require(LabGuidedReloadValidation.Inspect(sharedReference).Any(i => i.code == "binding.reused"),
                "reusing one transform for two abstract pieces is surfaced as a limitation");

            var visualOnly = LabGuidedReloadPlan.Clone(source);
            visualOnly.generationMode = LabGuidedGenerationMode.VisualMotionOnly;
            Require(LabGuidedReloadValidation.Inspect(visualOnly).Any(i => i.code == "phase.visualOnly"),
                "visual-only scope refuses piece/mechanism channels without silently removing them");

            var invalid = LabGuidedReloadPlan.Clone(source);
            invalid.phases[1].id = invalid.phases[0].id;
            invalid.phases[1].start = 0.3f;
            invalid.phases[0].end = 0.5f;
            invalid.phases[0].pieceId = "missing-piece";
            invalid.calibration.bindings[0].objectId = "part:Missing";
            invalid.calibration.anchors[0].time = 2f;
            string invalidBefore = JsonUtility.ToJson(invalid);
            var issues = LabGuidedReloadValidation.Inspect(invalid, context);
            Require(issues.Any(i => i.code == "phase.id")
                && issues.Any(i => i.code == "phase.overlap")
                && issues.Any(i => i.code == "phase.pieceMissing")
                && issues.Any(i => i.code == "binding.unresolved")
                && issues.Any(i => i.code == "anchor.time"),
                "validator identifies duplicate IDs, overlap, missing target, unresolved binding and invalid anchor time");
            Require(invalidBefore == JsonUtility.ToJson(invalid), "failed validation preserves invalid source values exactly");

            var invalidTime = LabGuidedReloadPlan.Clone(source);
            invalidTime.phases[0].start = float.NaN;
            string invalidTimeBefore = JsonUtility.ToJson(invalidTime);
            var timeIssues = LabGuidedReloadValidation.Inspect(invalidTime);
            Require(timeIssues.Any(i => i.code == "phase.time")
                && JsonUtility.ToJson(invalidTime) == invalidTimeBefore,
                "validator detects non-finite phase times without rewriting them");

            var noCalibration = LabGuidedReloadPlan.Clone(source);
            noCalibration.calibration = null;
            var noCalibrationIssues = LabGuidedReloadValidation.Inspect(noCalibration,
                new LabGuidedReloadValidationContext { availableHands = LabGuidedHands.Left });
            Require(noCalibrationIssues.Any(i => i.code == "calibration.missing")
                && noCalibrationIssues.Any(i => i.code == "phase.handUnavailable"),
                "hand compatibility is checked independently of missing calibration");

            var missingAnchor = LabGuidedReloadPlan.Clone(source);
            missingAnchor.calibration.anchors.Clear();
            Require(LabGuidedReloadValidation.Inspect(missingAnchor).Any(i => i.code == "anchor.required"
                && i.itemId == "extract"),
                "wrist phases report which assigned hand is still missing a local guard anchor");
        }

        private static void CheckEditingOperations(LabGuidedReloadPlan source)
        {
            Require(AnimationLabWindow.GuidedTimeToFrame(0.5f, 120) == 60
                && AnimationLabWindow.GuidedFrameToTime(60, 120) == 0.5f
                && AnimationLabWindow.GuidedFrameToTime(120, 120) == 1f,
                "normalized phase ranges map consistently to timeline frame endpoints");
            var offGridPhase = new LabGuidedReloadPhase { start = 0.123456f, end = 0.67891f };
            float exactStart = offGridPhase.start, exactEnd = offGridPhase.end;
            AnimationLabWindow.ApplyGuidedPhaseTiming(offGridPhase,
                AnimationLabWindow.GuidedTimeToFrame(exactStart, 200),
                AnimationLabWindow.GuidedTimeToFrame(exactEnd, 200), false, false, 200);
            Require(offGridPhase.start == exactStart && offGridPhase.end == exactEnd,
                "editing unrelated phase fields preserves exact off-grid normalized boundaries");
            AnimationLabWindow.ApplyGuidedPhaseTiming(offGridPhase, 25, 140, true, false, 200);
            Require(offGridPhase.start == 0.125f && offGridPhase.end == exactEnd,
                "editing one phase boundary leaves the other exact boundary unchanged");

            var protectedTake = LabAssetIO.Clone(null);
            protectedTake.guidedReload = LabGuidedReloadPlan.Clone(source);
            var guardedWindow = ScriptableObject.CreateInstance<AnimationLabWindow>();
            bool trialGuardWorked = false, recoveryGuardWorked = false, protectedMutationRan = false;
            try
            {
                LabPreview.Set(guardedWindow, "handTrialSource", protectedTake);
                try
                {
                    LabPreview.Call(guardedWindow, "ApplyGuidedChange", "Prueba protegida",
                        (Action)(() => protectedMutationRan = true));
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    trialGuardWorked = ex.InnerException is InvalidOperationException;
                }
                LabPreview.Set(guardedWindow, "handTrialSource", null);
                LabPreview.Set(guardedWindow, "quarantinedRecovery", true);
                try
                {
                    LabPreview.Call(guardedWindow, "ApplyGuidedChange", "Recuperación protegida",
                        (Action)(() => protectedMutationRan = true));
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    recoveryGuardWorked = ex.InnerException is InvalidOperationException;
                }
                Require(trialGuardWorked && recoveryGuardWorked && !protectedMutationRan,
                    "guided calibration mutations are rejected during a take trial or quarantined recovery");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guardedWindow);
                LabAssetIO.DestroyDraft(protectedTake);
            }

            var blank = LabGuidedReloadEditing.NewPlan("AK12");
            var piece = LabGuidedReloadEditing.AddPiece(blank);
            var first = LabGuidedReloadEditing.AddPhase(blank);
            first.end = 0.4f;
            var second = LabGuidedReloadEditing.AddPhase(blank);
            Require(blank.configured && piece.id != second.id && first.start == 0f
                && Mathf.Abs(second.start - 0.4f) < 0.0001f && second.end == 1f,
                "new pattern authoring creates stable elements and fills an available phase interval");
            first.pieceId = piece.id;
            Require(!LabGuidedReloadEditing.CanRemovePiece(blank, piece.id),
                "a piece referenced by a phase cannot be removed");
            first.pieceId = null;
            Require(LabGuidedReloadEditing.RemovePiece(blank, piece.id),
                "an unused abstract piece can be removed");

            var copied = LabGuidedReloadEditing.CopyPattern(source, "OtherEquipment");
            Require(copied.patternId != source.patternId && copied.patternName == "Copia de " + source.patternName
                && copied.generationMode == source.generationMode && copied.phases.Count == source.phases.Count
                && copied.calibration.equipmentId == "OtherEquipment" && copied.calibration.bindings.Count == 0
                && copied.phases[0].id == source.phases[0].id,
                "copying a pattern keeps its sequence but clears rig-specific calibration");
            Require(LabGuidedReloadEditing.MovePhase(copied, 1, -1)
                && copied.phases[0].id == source.phases[1].id,
                "phase ordering is editable without regenerating content");
            Require(LabGuidedReloadEditing.RemovePhase(copied, copied.phases[0].id)
                && copied.phases.Count == source.phases.Count - 1,
                "a selected phase can be removed independently");

            // Continuity linking helper test
            var linkingPlan = LabGuidedReloadPlan.Clone(source);
            int beforeLinkAnchors = linkingPlan.calibration.anchors.Count;
            // Add end anchor on phase 0
            linkingPlan.calibration.anchors.Add(new LabGuidedAnchor
            {
                id = "extract-end-wrist",
                phaseId = linkingPlan.phases[0].id,
                role = "ExtractEnd",
                kind = LabGuidedAnchorKind.Wrist,
                hand = LabGuidedHands.Left,
                space = LabGuidedAnchorSpace.Guard,
                time = linkingPlan.phases[0].end,
                position = new Vector3(0.05f, -0.02f, 0.08f),
                rotation = Quaternion.Euler(10f, 15f, 0f)
            });
            // Ensure phase 1 wants left wrist
            linkingPlan.phases[1].channels |= LabGuidedChannels.Wrist;
            linkingPlan.phases[1].hands |= LabGuidedHands.Left;
            int copiedAnchors = LabGuidedReloadEditing.CopyPreviousPhaseAnchors(linkingPlan, 1);
            Require(copiedAnchors == 1, "linking copies matching end anchors from previous phase to current phase start");
            var linkedAnchor = linkingPlan.calibration.anchors.FirstOrDefault(a => a.phaseId == linkingPlan.phases[1].id
                && LabGuidedAnchorTools.EffectiveKind(a) == LabGuidedAnchorKind.Wrist && a.hand == LabGuidedHands.Left
                && Math.Abs(a.time - linkingPlan.phases[1].start) <= 0.0001f);
            Require(linkedAnchor != null
                && Vector3.Distance(linkedAnchor.position, new Vector3(0.05f, -0.02f, 0.08f)) < 0.0001f
                && Quaternion.Angle(linkedAnchor.rotation, Quaternion.Euler(10f, 15f, 0f)) < 0.01f,
                "linked anchor has matching position and rotation at current phase start");

            var capturePlan = LabGuidedReloadPlan.Clone(source);
            var capturePhase = capturePlan.phases[0];
            var guardObject = new GameObject("Guided anchor test guard");
            var wristObject = new GameObject("Guided anchor test wrist");
            var pieceParent = new GameObject("Guided anchor test piece parent");
            var pieceObject = new GameObject("Guided anchor test piece");
            try
            {
                guardObject.transform.SetPositionAndRotation(
                    new Vector3(1.2f, -0.4f, 2.1f), Quaternion.Euler(15f, 40f, -8f));
                Vector3 localPosition = new Vector3(0.12f, 0.34f, -0.23f);
                Quaternion localRotation = Quaternion.Euler(9f, -27f, 11f);
                wristObject.transform.SetPositionAndRotation(
                    guardObject.transform.TransformPoint(localPosition),
                    guardObject.transform.rotation * localRotation);
                var legacyGuard = capturePlan.calibration.anchors.First(a => a.id == "left-guard");
                var legacyUpsert = LabGuidedAnchorCapture.CaptureHand(capturePlan, capturePhase,
                    HandGripToolkit.HandSide.Left, guardObject.transform, wristObject.transform, 0.2f);
                Require(legacyUpsert.id == legacyGuard.id
                    && legacyUpsert.kind == LabGuidedAnchorKind.Wrist
                    && capturePlan.calibration.anchors.Count == source.calibration.anchors.Count,
                    "early v1 guard anchors are inferred and upgraded in place when recaptured");
                capturePlan.calibration.anchors.RemoveAll(a => a != null
                    && a.phaseId == capturePhase.id && a.hand == LabGuidedHands.Left
                    && Mathf.Abs(a.time - 0.2f) <= LabTimeline.TimeEpsilon);
                int initialAnchors = capturePlan.calibration.anchors.Count;
                var captured = LabGuidedAnchorCapture.CaptureHand(capturePlan, capturePhase,
                    HandGripToolkit.HandSide.Left, guardObject.transform, wristObject.transform, 0.2f);
                string stableId = captured.id;
                Require(captured.phaseId == capturePhase.id && captured.hand == LabGuidedHands.Left
                    && captured.space == LabGuidedAnchorSpace.Guard && captured.objectId == null
                    && captured.time == 0.2f && Vector3.Distance(captured.position, localPosition) < 0.0001f
                    && Quaternion.Angle(captured.rotation, localRotation) < 0.01f,
                    "captured wrist anchor stores a guard-local pose and portable IDs, never a Transform");
                wristObject.transform.position += guardObject.transform.right * 0.1f;
                var recaptured = LabGuidedAnchorCapture.CaptureHand(capturePlan, capturePhase,
                    HandGripToolkit.HandSide.Left, guardObject.transform, wristObject.transform, 0.2f);
                Require(recaptured.id == stableId && capturePlan.calibration.anchors.Count == initialAnchors + 1,
                    "recapturing the same phase/hand/time updates one stable anchor instead of accumulating duplicates");
                LabGuidedAnchorCapture.CorrectLocalPose(captured, new Vector3(0.2f, -0.1f, 0.04f),
                    new Vector3(12f, -18f, 7f));
                Require(Vector3.Distance(captured.position, new Vector3(0.2f, -0.1f, 0.04f)) < 0.0001f
                    && Quaternion.Angle(captured.rotation, Quaternion.Euler(12f, -18f, 7f)) < 0.01f,
                    "anchor local position and orientation can be corrected independently of scene objects");
                bool rejectedNonFiniteCorrection = false;
                Vector3 beforeRejectedCorrection = captured.position;
                try
                {
                    LabGuidedAnchorCapture.CorrectLocalPose(captured,
                        new Vector3(float.NaN, 0f, 0f), Vector3.zero);
                }
                catch (InvalidOperationException) { rejectedNonFiniteCorrection = true; }
                Require(rejectedNonFiniteCorrection && captured.position == beforeRejectedCorrection,
                    "manual anchor correction rejects non-finite values before changing portable calibration");
                var fingersOnlyPhase = JsonUtility.FromJson<LabGuidedReloadPhase>(
                    JsonUtility.ToJson(capturePhase));
                fingersOnlyPhase.channels = LabGuidedChannels.Fingers;
                string beforeWristChannelCheck = JsonUtility.ToJson(capturePlan);
                bool rejectedDisabledWristChannel = false;
                try
                {
                    LabGuidedAnchorCapture.CaptureHand(capturePlan, fingersOnlyPhase,
                        HandGripToolkit.HandSide.Left, guardObject.transform, wristObject.transform, 0.2f);
                }
                catch (InvalidOperationException) { rejectedDisabledWristChannel = true; }
                Require(rejectedDisabledWristChannel && beforeWristChannelCheck == JsonUtility.ToJson(capturePlan),
                    "wrist capture is refused when the phase has disabled that channel");
                bool rejectedUnassignedHand = false;
                try
                {
                    LabGuidedAnchorCapture.CaptureHand(capturePlan, capturePhase,
                        HandGripToolkit.HandSide.Right, guardObject.transform, wristObject.transform, 0.2f);
                }
                catch (InvalidOperationException) { rejectedUnassignedHand = true; }
                Require(rejectedUnassignedHand, "anchor capture refuses a hand not assigned to its phase");

                var pieceBinding = capturePlan.calibration.bindings.First(b => b.pieceId == capturePhase.pieceId);
                pieceObject.transform.SetParent(pieceParent.transform, false);
                Vector3 pieceLocalPosition = new Vector3(-0.2f, 0.08f, 0.17f);
                Quaternion pieceLocalRotation = Quaternion.Euler(0f, 30f, 4f);
                pieceObject.transform.SetLocalPositionAndRotation(pieceLocalPosition, pieceLocalRotation);
                int beforePieceAnchor = capturePlan.calibration.anchors.Count;
                var pieceAnchor = LabGuidedAnchorCapture.CapturePiece(capturePlan, capturePhase,
                    pieceObject.transform, pieceBinding.objectId, 0.25f);
                string pieceAnchorId = pieceAnchor.id;
                Require(pieceAnchor.phaseId == capturePhase.id && pieceAnchor.pieceId == capturePhase.pieceId
                    && pieceAnchor.hand == LabGuidedHands.None && pieceAnchor.space == LabGuidedAnchorSpace.Parent
                    && pieceAnchor.objectId == pieceBinding.objectId && pieceAnchor.time == 0.25f
                    && Vector3.Distance(pieceAnchor.position, pieceLocalPosition) < 0.0001f
                    && Quaternion.Angle(pieceAnchor.rotation, pieceLocalRotation) < 0.01f,
                    "captured piece anchor stores local pose and stable object ID without retaining its Transform");
                pieceObject.transform.localPosition += Vector3.up * 0.1f;
                var recapturedPiece = LabGuidedAnchorCapture.CapturePiece(capturePlan, capturePhase,
                    pieceObject.transform, pieceBinding.objectId, 0.25f);
                Require(recapturedPiece.id == pieceAnchorId
                    && capturePlan.calibration.anchors.Count == beforePieceAnchor + 1,
                    "recapturing a piece at the same phase/time updates its anchor instead of duplicating it");

                capturePhase.channels |= LabGuidedChannels.Mechanism | LabGuidedChannels.Weapon;
                int beforeMechanismAnchor = capturePlan.calibration.anchors.Count;
                var mechanismAnchor = LabGuidedAnchorCapture.CaptureMechanism(capturePlan, capturePhase,
                    pieceObject.transform, pieceBinding.objectId, 0.3f);
                Require(mechanismAnchor.kind == LabGuidedAnchorKind.Mechanism
                    && mechanismAnchor.pieceId == capturePhase.pieceId
                    && mechanismAnchor.space == LabGuidedAnchorSpace.Parent
                    && mechanismAnchor.objectId == pieceBinding.objectId,
                    "mechanism capture records a binding-local pose distinct from a detachable piece anchor");

                var weaponParent = new GameObject("Guided anchor test weapon parent");
                var weaponObject = new GameObject("Guided anchor test weapon pose root");
                try
                {
                    weaponObject.transform.SetParent(weaponParent.transform, false);
                    Vector3 weaponLocalPosition = new Vector3(0.03f, -0.07f, 0.11f);
                    Quaternion weaponLocalRotation = Quaternion.Euler(-8f, 3f, 5f);
                    weaponObject.transform.SetLocalPositionAndRotation(weaponLocalPosition, weaponLocalRotation);
                    var weaponAnchor = LabGuidedAnchorCapture.CaptureWeapon(capturePlan, capturePhase,
                        weaponObject.transform, 0.35f);
                    Require(weaponAnchor.kind == LabGuidedAnchorKind.Weapon
                        && weaponAnchor.hand == LabGuidedHands.None && weaponAnchor.pieceId == null
                        && weaponAnchor.objectId == null && weaponAnchor.space == LabGuidedAnchorSpace.Parent
                        && Vector3.Distance(weaponAnchor.position, weaponLocalPosition) < 0.0001f
                        && Quaternion.Angle(weaponAnchor.rotation, weaponLocalRotation) < 0.01f,
                        "weapon capture stores a parent-local holder reference without binding the visual model or a Transform");
                    string weaponAnchorId = weaponAnchor.id;
                    weaponObject.transform.localPosition += Vector3.right * 0.1f;
                    var recapturedWeapon = LabGuidedAnchorCapture.CaptureWeapon(capturePlan, capturePhase,
                        weaponObject.transform, 0.35f);
                    Require(recapturedWeapon.id == weaponAnchorId,
                        "recapturing the weapon base at the same phase/time updates its stable anchor");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(weaponObject);
                    UnityEngine.Object.DestroyImmediate(weaponParent);
                }
                Require(capturePlan.calibration.anchors.Count == beforeMechanismAnchor + 2,
                    "piece, mechanism and weapon anchor kinds coexist without overwriting one another");
                var capturedIssues = LabGuidedReloadValidation.Inspect(capturePlan);
                Require(!capturedIssues.Any(i => i.severity == LabGuidedIssueSeverity.Error),
                    "captured wrist, piece, mechanism and weapon references pass structural validation: "
                        + string.Join(" | ", capturedIssues.Where(i => i.severity == LabGuidedIssueSeverity.Error)
                            .Select(i => i.code + ": " + i.message)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guardObject);
                UnityEngine.Object.DestroyImmediate(wristObject);
                UnityEngine.Object.DestroyImmediate(pieceObject);
                UnityEngine.Object.DestroyImmediate(pieceParent);
            }

            var undoTake = LabAssetIO.Clone(null);
            undoTake.guidedReload = LabGuidedReloadPlan.Clone(source);
            try
            {
                var undoAnchor = undoTake.guidedReload.calibration.anchors[0];
                Vector3 beforeUndo = undoAnchor.position;
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.RecordObject(undoTake, "Corregir anclaje guiado");
                LabGuidedAnchorCapture.CorrectLocalPose(undoAnchor, new Vector3(0.31f, -0.12f, 0.08f),
                    new Vector3(5f, 14f, -3f));
                Undo.CollapseUndoOperations(undoGroup);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Require(Vector3.Distance(undoTake.guidedReload.calibration.anchors[0].position, beforeUndo) < 0.0001f,
                    "Undo restores a corrected nested calibration anchor");
                Undo.PerformRedo();
                Require(Vector3.Distance(undoTake.guidedReload.calibration.anchors[0].position,
                        new Vector3(0.31f, -0.12f, 0.08f)) < 0.0001f,
                    "Redo reapplies the nested calibration anchor correction");
            }
            finally { LabAssetIO.DestroyDraft(undoTake); }
        }

        private static void CheckAK12FixtureTest()
        {
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            try
            {
                LabPreview.Call(window, "StartPreview");
                var preview = (LabPreview)LabPreview.Get(window, "preview");
                var draft = (AnimationLabTake)LabPreview.Get(window, "draft");
                Require(draft != null && draft.equipmentId == "AK12", "draft is initialized for AK12 fixture");
                Require(preview != null, "preview scene is initialized");

                // Set 120 frames at 60 fps (2.0s)
                draft.action.duration = 2.0f;
                int totalFrames = 120;
                float F(int frame) => frame / (float)totalFrames;

                var plan = new LabGuidedReloadPlan
                {
                    configured = true,
                    patternId = "ak12-test-pattern",
                    patternName = "AK12 Recarga 9 Fases",
                    generationMode = LabGuidedGenerationMode.SelectedChannels,
                    calibration = new LabGuidedReloadCalibration
                    {
                        calibrationId = "ak12-test-calibration",
                        equipmentId = "AK12"
                    }
                };
                var magPiece = new LabGuidedPieceDefinition
                {
                    id = "mag",
                    displayName = "Cargador",
                    kind = LabGuidedPieceKind.DetachableMagazine
                };
                plan.pieces.Add(magPiece);
                plan.calibration.bindings.Add(new LabGuidedPieceBinding
                {
                    pieceId = "mag",
                    objectId = "part:AK12Rig/Root/Mag"
                });

                // 8 phases covering the 9-point sequence from Section 4:
                // F1: Presentar (0 - 12 F): Weapon
                var p1 = new LabGuidedReloadPhase
                {
                    id = "p1-present", displayName = "Presentar", role = LabGuidedPhaseRole.PreparePresent,
                    start = F(0), end = F(12), channels = LabGuidedChannels.Weapon
                };
                // F2: Alcanzar (12 - 25 F): Left Wrist
                var p2 = new LabGuidedReloadPhase
                {
                    id = "p2-reach", displayName = "Alcanzar", role = LabGuidedPhaseRole.Reach,
                    start = F(12), end = F(25), hands = LabGuidedHands.Left, channels = LabGuidedChannels.Wrist
                };
                // F3: Extraer (25 - 42 F): Left Wrist
                var p3 = new LabGuidedReloadPhase
                {
                    id = "p3-extract", displayName = "Agarrar y extraer", role = LabGuidedPhaseRole.Extract,
                    start = F(25), end = F(42), hands = LabGuidedHands.Left, channels = LabGuidedChannels.Wrist,
                    pieceId = "mag"
                };
                // F4: Espera (42 - 73 F): Left Wrist
                var p4 = new LabGuidedReloadPhase
                {
                    id = "p4-wait", displayName = "Espera suministro", role = LabGuidedPhaseRole.Wait,
                    start = F(42), end = F(73), hands = LabGuidedHands.Left, channels = LabGuidedChannels.Wrist
                };
                // F5: Insertar (73 - 92 F): Left Wrist
                var p5 = new LabGuidedReloadPhase
                {
                    id = "p5-insert", displayName = "Alinear e insertar", role = LabGuidedPhaseRole.Insert,
                    start = F(73), end = F(92), hands = LabGuidedHands.Left, channels = LabGuidedChannels.Wrist,
                    pieceId = "mag"
                };
                // F6: Asentar (92 - 99 F): Left Wrist
                var p6 = new LabGuidedReloadPhase
                {
                    id = "p6-seat", displayName = "Asentar", role = LabGuidedPhaseRole.Seat,
                    start = F(92), end = F(99), hands = LabGuidedHands.Left, channels = LabGuidedChannels.Wrist,
                    pieceId = "mag"
                };
                // F7: Soltar (99 - 108 F): Left Wrist
                var p7 = new LabGuidedReloadPhase
                {
                    id = "p7-release", displayName = "Soltar", role = LabGuidedPhaseRole.Release,
                    start = F(99), end = F(108), hands = LabGuidedHands.Left, channels = LabGuidedChannels.Wrist
                };
                // F8: Recuperar (108 - 120 F): Left Wrist + Weapon
                var p8 = new LabGuidedReloadPhase
                {
                    id = "p8-recover", displayName = "Recuperar", role = LabGuidedPhaseRole.Recover,
                    start = F(108), end = F(120), hands = LabGuidedHands.Left,
                    channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon
                };

                plan.phases.AddRange(new[] { p1, p2, p3, p4, p5, p6, p7, p8 });

                // Anchors for weapon (holder local to parent)
                var weaponRoot = preview.Equipment.weaponPose.PoseRoot;
                Vector3 weaponBasePos = weaponRoot.localPosition;
                Quaternion weaponBaseRot = weaponRoot.localRotation;
                Vector3 weaponPresentedPos = weaponBasePos + new Vector3(-0.01f, 0.02f, 0.01f);
                Quaternion weaponPresentedRot = weaponBaseRot * Quaternion.Euler(2f, -12f, 8f);

                AddAnchor(plan, p1, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, F(0), weaponBasePos, weaponBaseRot);
                AddAnchor(plan, p1, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, F(12), weaponPresentedPos, weaponPresentedRot);
                AddAnchor(plan, p8, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, F(108), weaponPresentedPos, weaponPresentedRot);
                AddAnchor(plan, p8, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, F(120), weaponBasePos, weaponBaseRot);

                // Anchors for left wrist (guard local)
                Vector3 wHome = Vector3.zero;
                Quaternion rHome = Quaternion.identity;
                Vector3 wReach = new Vector3(0.04f, -0.06f, 0.12f);
                Quaternion rReach = Quaternion.Euler(-10f, 25f, -5f);
                Vector3 wExtract = new Vector3(0.02f, -0.22f, 0.06f);
                Quaternion rExtract = Quaternion.Euler(-15f, 20f, -10f);
                Vector3 wWait = new Vector3(0.01f, -0.25f, 0.04f);
                Quaternion rWait = Quaternion.Euler(-15f, 18f, -8f);
                Vector3 wPreSeat = new Vector3(0.04f, -0.08f, 0.11f);
                Quaternion rPreSeat = Quaternion.Euler(-10f, 24f, -5f);
                Vector3 wSeated = new Vector3(0.04f, -0.05f, 0.12f);
                Quaternion rSeated = Quaternion.Euler(-10f, 25f, -5f);
                Vector3 wRelease = new Vector3(0.02f, -0.07f, 0.08f);
                Quaternion rRelease = Quaternion.Euler(-5f, 15f, -2f);

                AddAnchor(plan, p2, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(12), wHome, rHome);
                AddAnchor(plan, p2, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(25), wReach, rReach);

                AddAnchor(plan, p3, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(25), wReach, rReach);
                AddAnchor(plan, p3, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(42), wExtract, rExtract);

                AddAnchor(plan, p4, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(42), wExtract, rExtract);
                AddAnchor(plan, p4, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(73), wWait, rWait);

                AddAnchor(plan, p5, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(73), wWait, rWait);
                AddAnchor(plan, p5, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(92), wPreSeat, rPreSeat);

                AddAnchor(plan, p6, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(92), wPreSeat, rPreSeat);
                AddAnchor(plan, p6, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(99), wSeated, rSeated);

                AddAnchor(plan, p7, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(99), wSeated, rSeated);
                AddAnchor(plan, p7, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(108), wRelease, rRelease);

                AddAnchor(plan, p8, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(108), wRelease, rRelease);
                AddAnchor(plan, p8, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, F(120), wHome, rHome);

                draft.guidedReload = plan;

                // Validate structure
                var issues = LabGuidedReloadValidation.Inspect(plan);
                Require(!issues.Any(i => i.severity == LabGuidedIssueSeverity.Error),
                    "9-phase reload plan passes structural validation");

                // Direct BuildCandidate check
                var candidate = LabGuidedReloadGenerator.BuildCandidate(draft, preview, 0f, 0f, 0f, false);
                try
                {
                    Require(candidate != null, "candidate is generated successfully for AK12 fixture");
                    Require(candidate.left.Count == 8, $"candidate left wrist has 8 keyframes (actual {candidate.left.Count})");
                    Require(candidate.weapon.Count == 4, $"candidate weapon has 4 keyframes (actual {candidate.weapon.Count})");
                    Require(candidate.right.Count == draft.right.Count, "candidate right hand is completely preserved");

                    // Evaluate across frames on preview
                    for (int f = 0; f <= totalFrames; f += 10)
                    {
                        float t = f / (float)totalFrames;
                        preview.Evaluate(candidate, t, 0f, 0f, false);
                        Require(LabTakeValidation.Finite(preview.lastWeaponOffset), $"evaluated weapon offset is finite at frame {f}");
                    }
                }
                finally
                {
                    LabAssetIO.DestroyDraft(candidate);
                }

                // Test reversible window trial
                string beforeJson = EditorJsonUtility.ToJson(draft) + EditorJsonUtility.ToJson(draft.action);
                LabPreview.Call(window, "StartGuidedReloadTrial");
                Require((bool)LabPreview.Get(window, "HasGuidedReloadTrial"), "window enters guided reload trial state");
                var windowCandidate = (AnimationLabTake)LabPreview.Get(window, "guidedReloadCandidate");
                Require(windowCandidate != null, "window candidate is created");
                Require((bool)LabPreview.Get(window, "guidedReloadBefore") == false, "trial defaults to AFTER view");

                // Check toggle BEFORE / AFTER
                LabPreview.Set(window, "guidedReloadBefore", true);
                var displayTakeBefore = (AnimationLabTake)LabPreview.Get(window, "DisplayTake");
                Require(displayTakeBefore != windowCandidate, "DisplayTake returns source in BEFORE mode");

                LabPreview.Set(window, "guidedReloadBefore", false);
                var displayTakeAfter = (AnimationLabTake)LabPreview.Get(window, "DisplayTake");
                Require(displayTakeAfter == windowCandidate, "DisplayTake returns candidate in AFTER mode");

                // Cancel leaves draft untouched
                LabPreview.Call(window, "CancelGuidedReloadTrial", true);
                Require(!(bool)LabPreview.Get(window, "HasGuidedReloadTrial"), "cancel clears trial state");
                string afterCancelJson = EditorJsonUtility.ToJson(draft) + EditorJsonUtility.ToJson(draft.action);
                Require(beforeJson == afterCancelJson, "cancel leaves draft completely untouched");

                // Restart and Accept
                LabPreview.Call(window, "StartGuidedReloadTrial");
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                LabPreview.Call(window, "AcceptGuidedReloadTrial");
                Undo.CollapseUndoOperations(undoGroup);
                Undo.FlushUndoRecordObjects();

                Require(!(bool)LabPreview.Get(window, "HasGuidedReloadTrial"), "accept clears trial state");
                Require(draft.left.Count == 8, "draft left hand received 8 reload keyframes");
                Require(draft.weapon.Count == 4, "draft weapon received 4 reload keyframes");

                // Test Undo
                Undo.PerformUndo();
                Require(draft.left.Count != 8 || draft.weapon.Count != 4, "Undo reverts accepted reload on draft");

                // Test Redo
                Undo.PerformRedo();
                Require(draft.left.Count == 8 && draft.weapon.Count == 4, "Redo restores accepted reload on draft");

                // Clean draft
                Undo.PerformUndo();
            }
            finally
            {
                LabPreview.Call(window, "ClosePreview");
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void CheckGuidedReloadWizard()
        {
            // 1. Check Archetype generation
            var tactical = LabGuidedReloadEditing.CreateTacticalMagazinePlan("TEST_EQ", "part:AK12Rig/Root/Mag");
            Require(tactical.configured, "tactical reload plan is configured");
            Require(tactical.patternName == "Recarga táctica con cargador", "tactical reload name matches");
            Require(tactical.generationMode == LabGuidedGenerationMode.SelectedChannels, "tactical reload uses selected channels");
            Require(tactical.motionProfile == LabGuidedMotionProfile.Snappy, "tactical reload uses Snappy profile");
            Require(tactical.magazinePolicy == LabGuidedMagazinePolicy.ExtractAndReplace, "tactical reload uses ExtractAndReplace policy");
            Require(tactical.pieces.Count == 1, "tactical reload has 1 piece");
            Require(tactical.pieces[0].kind == LabGuidedPieceKind.DetachableMagazine, "tactical piece is DetachableMagazine");
            Require(tactical.phases.Count == 6, "tactical reload has 6 phases");
            Require(tactical.phases[0].role == LabGuidedPhaseRole.PreparePresent && tactical.phases[0].gripPreset == 9, "phase 1 is PreparePresent with grip 9");
            Require(tactical.phases[1].role == LabGuidedPhaseRole.Reach && tactical.phases[1].gripPreset == 12, "phase 2 is Reach with grip 12");
            Require(tactical.phases[2].role == LabGuidedPhaseRole.Extract && tactical.phases[2].pieceId == tactical.pieces[0].id && tactical.phases[2].gripPreset == 5, "phase 3 is Extract with mag piece and grip 5");
            Require(tactical.phases[3].role == LabGuidedPhaseRole.Insert && tactical.phases[3].pieceId == tactical.pieces[0].id && tactical.phases[3].gripPreset == 5, "phase 4 is Insert with mag piece and grip 5");
            Require(tactical.phases[4].role == LabGuidedPhaseRole.Seat && tactical.phases[4].gripPreset == 6, "phase 5 is Seat with grip 6");
            Require(tactical.phases[5].role == LabGuidedPhaseRole.Recover && tactical.phases[5].gripPreset == 9, "phase 6 is Recover with grip 9");
            Require(tactical.calibration.bindings.Count == 1 && tactical.calibration.bindings[0].objectId == "part:AK12Rig/Root/Mag", "tactical binding is set");

            var empty = LabGuidedReloadEditing.CreateEmptyReloadPlan("TEST_EQ");
            Require(empty.patternName == "Recarga vacía con cerrojo", "empty reload name matches");
            Require(empty.pieces.Count == 2, "empty reload has 2 pieces");
            Require(empty.pieces[0].kind == LabGuidedPieceKind.DetachableMagazine, "piece 0 is DetachableMagazine");
            Require(empty.pieces[1].kind == LabGuidedPieceKind.ChargingHandle, "piece 1 is ChargingHandle");
            Require(empty.phases.Count == 7, "empty reload has 7 phases");
            Require(empty.phases[5].role == LabGuidedPhaseRole.OperateMechanism && empty.phases[5].pieceId == empty.pieces[1].id && empty.phases[5].gripPreset == 7, "phase 6 operates mechanism with grip 7");

            var procedural = LabGuidedReloadEditing.CreateProceduralHandsPlan("TEST_EQ");
            Require(procedural.patternName == "Recarga procedimental de manos", "procedural hands name matches");
            Require(procedural.generationMode == LabGuidedGenerationMode.VisualMotionOnly, "procedural hands is VisualMotionOnly");
            Require(procedural.motionProfile == LabGuidedMotionProfile.Fluid, "procedural hands uses Fluid profile");
            Require(procedural.pieces.Count == 0, "procedural hands has 0 pieces");
            Require(procedural.phases.Count == 5, "procedural hands has 5 phases");

            // 2. Check AutoDetectBindings
            var mockObjects = new[] { "Mesh_Root", "part:AK12Rig/Root/Mag", "part:AK12Rig/Root/Charge", "props/test" };
            int boundCount = LabGuidedReloadEditing.AutoDetectBindings(empty, mockObjects);
            Require(boundCount == 2, "auto-detected 2 pieces for empty reload");
            Require(empty.calibration.bindings.Any(b => b.pieceId == empty.pieces[0].id && b.objectId == "part:AK12Rig/Root/Mag"), "mag auto-bound correctly");
            Require(empty.calibration.bindings.Any(b => b.pieceId == empty.pieces[1].id && b.objectId == "part:AK12Rig/Root/Charge"), "charging handle auto-bound correctly");
            int secondRun = LabGuidedReloadEditing.AutoDetectBindings(empty, mockObjects);
            Require(secondRun == 0, "second auto-detect run does not rebind already bound pieces");

            // 3. Check GetRequirements and Checklist (Hito 3 ampliado: Muñeca, Arma; Dedos usan anclas de muñeca)
            var reqs = LabGuidedReloadEditing.GetRequirements(tactical);
            Require(reqs.Count == 18, $"tactical reload has 18 required anchor endpoints (wrist+weapon only, fingers share wrist anchors), got {reqs.Count}");
            Require(reqs.All(r => !r.isSatisfied), "all requirements initially unsatisfied");

            // Satisfy phase 0 start Wrist Left
            tactical.calibration.anchors.Add(new LabGuidedAnchor
            {
                phaseId = tactical.phases[0].id,
                kind = LabGuidedAnchorKind.Wrist,
                hand = LabGuidedHands.Left,
                time = 0.00f,
                position = Vector3.zero
            });
            var reqsAfter1 = LabGuidedReloadEditing.GetRequirements(tactical);
            Require(reqsAfter1.Count(r => r.isSatisfied) == 1, "exactly 1 requirement satisfied after adding anchor");
            Require(reqsAfter1.First(r => r.phaseId == tactical.phases[0].id && r.kind == LabGuidedAnchorKind.Wrist && r.isStart).isSatisfied, "phase 0 start wrist left is satisfied");

            // Satisfy phase 0 end Wrist Left
            tactical.calibration.anchors.Add(new LabGuidedAnchor
            {
                phaseId = tactical.phases[0].id,
                kind = LabGuidedAnchorKind.Wrist,
                hand = LabGuidedHands.Left,
                time = 0.15f,
                position = new Vector3(0.01f, 0.02f, 0.03f)
            });

            // Test CopyPreviousPhaseAnchors satisfies phase 1 start Wrist Left
            int copied = LabGuidedReloadEditing.CopyPreviousPhaseAnchors(tactical, 1);
            Require(copied >= 1, "copied at least 1 anchor to phase 1 start");
            var reqsAfterCopy = LabGuidedReloadEditing.GetRequirements(tactical);
            Require(reqsAfterCopy.First(r => r.phaseId == tactical.phases[1].id && r.kind == LabGuidedAnchorKind.Wrist && r.isStart).isSatisfied, "phase 1 start wrist left is satisfied via continuity copy");

            // 4. Check Shotgun Tube Plan
            var shotgun = LabGuidedReloadEditing.CreateShotgunTubePlan("SHOTGUN_EQ", "part:Tube/Round", "part:Receiver/Pump", 4);
            Require(shotgun.patternName == "Recarga de escopeta de tubo", "shotgun pattern name matches");
            Require(shotgun.generationMode == LabGuidedGenerationMode.SelectedChannels, "shotgun uses SelectedChannels");
            Require(shotgun.motionProfile == LabGuidedMotionProfile.Controlled, "shotgun uses Controlled profile");
            Require(shotgun.pieces.Count == 2, "shotgun has 2 pieces (round + pump)");
            Require(shotgun.pieces[0].kind == LabGuidedPieceKind.Round && shotgun.pieces[0].suggestedCount == 4, "shotgun piece 0 is Round with 4 shells");
            Require(shotgun.pieces[1].kind == LabGuidedPieceKind.ChargingHandle, "shotgun piece 1 is pump handle");
            Require(shotgun.phases.Count == 6, "shotgun has 6 phases");
            Require(shotgun.phases[2].role == LabGuidedPhaseRole.Insert && shotgun.phases[2].repetitions == 4, "shotgun insert phase repeats 4 times");
            Require(shotgun.phases[4].role == LabGuidedPhaseRole.OperateMechanism && shotgun.phases[4].pieceId == shotgun.pieces[1].id, "shotgun pump mechanism phase");
            Require(shotgun.calibration.bindings.Count == 2, "shotgun bindings created");

            // 5. Check Revolver Plan
            var revolver = LabGuidedReloadEditing.CreateRevolverPlan("REVOLVER_EQ", "part:Frame/Cylinder", "prop:Speedloader");
            Require(revolver.patternName == "Recarga de revólver con tambor", "revolver pattern name matches");
            Require(revolver.pieces.Count == 2, "revolver has 2 pieces (cylinder + speedloader)");
            Require(revolver.pieces[0].kind == LabGuidedPieceKind.RevolverCylinder, "revolver piece 0 is RevolverCylinder");
            Require(revolver.phases.Count == 6, "revolver has 6 phases");
            Require(revolver.phases[1].role == LabGuidedPhaseRole.OperateMechanism && revolver.phases[1].pieceId == revolver.pieces[0].id, "revolver phase 2 swings cylinder");
            Require(revolver.phases[2].role == LabGuidedPhaseRole.Extract, "revolver phase 3 ejects spent casings");
            Require(revolver.phases[4].role == LabGuidedPhaseRole.Seat && revolver.phases[4].pieceId == revolver.pieces[0].id, "revolver phase 5 snaps cylinder shut");
            Require(revolver.calibration.bindings.Count == 2, "revolver bindings created");

            // 6. Check extended auto-detect for Shotgun and Revolver
            var mockWeapons = new[] { "part:Shotgun/Tube_Round", "part:Shotgun/Forend_Pump", "part:Colt/Cylinder_Drum" };
            var autoShotgun = LabGuidedReloadEditing.CreateShotgunTubePlan("SHOTGUN_EQ", null, "auto_pump", 6);
            int shotgunBound = LabGuidedReloadEditing.AutoDetectBindings(autoShotgun, mockWeapons);
            Require(shotgunBound >= 1, "auto-detected shotgun pieces");
            Require(autoShotgun.calibration.bindings.Any(b => b.objectId == "part:Shotgun/Tube_Round"), "round auto-detected");

            var autoRevolver = LabGuidedReloadEditing.CreateRevolverPlan("REVOLVER_EQ");
            int revBound = LabGuidedReloadEditing.AutoDetectBindings(autoRevolver, mockWeapons);
            Require(revBound >= 1, "auto-detected revolver cylinder");
            Require(autoRevolver.calibration.bindings.Any(b => b.objectId == "part:Colt/Cylinder_Drum"), "cylinder auto-detected");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[AnimationLabGuidedReloadChecks] " + message);
        }
    }
}
