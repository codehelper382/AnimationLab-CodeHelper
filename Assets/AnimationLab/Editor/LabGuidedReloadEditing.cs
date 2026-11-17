using System;
using System.Linq;

namespace CodeHelper.AnimationLab.Editor
{
    internal struct LabGuidedRequirement
    {
        public string phaseId;
        public string phaseDisplayName;
        public float time;
        public bool isStart;
        public LabGuidedAnchorKind kind;
        public LabGuidedHands hand;
        public string pieceId;
        public string pieceDisplayName;
        public bool isSatisfied;
        public string label;
    }

    // Data-only authoring operations shared by the UI and its checks.
    internal static class LabGuidedReloadEditing
    {
        internal static LabGuidedReloadPlan NewPlan(string equipmentId)
        {
            return new LabGuidedReloadPlan
            {
                configured = true,
                patternId = Guid.NewGuid().ToString("N"),
                patternName = "Recarga nueva",
                generationMode = LabGuidedGenerationMode.VisualMotionOnly,
                calibration = new LabGuidedReloadCalibration
                {
                    calibrationId = Guid.NewGuid().ToString("N"),
                    equipmentId = equipmentId
                }
            };
        }

        internal static LabGuidedReloadPlan CreateTacticalMagazinePlan(string equipmentId, string magObjectId = null)
        {
            var plan = NewPlan(equipmentId);
            plan.patternName = "Recarga táctica con cargador";
            plan.generationMode = LabGuidedGenerationMode.SelectedChannels;
            plan.motionProfile = LabGuidedMotionProfile.Snappy;
            plan.magazinePolicy = LabGuidedMagazinePolicy.ExtractAndReplace;

            var mag = AddPiece(plan);
            mag.displayName = "Cargador";
            mag.kind = LabGuidedPieceKind.DetachableMagazine;

            // 1. Presentar arma
            var p1 = AddPhase(plan);
            p1.displayName = "Presentar arma";
            p1.role = LabGuidedPhaseRole.PreparePresent;
            p1.start = 0.00f; p1.end = 0.15f;
            p1.hands = LabGuidedHands.Left;
            p1.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p1.gripPreset = 9; // Guardamanos · apoyo táctico

            // 2. Alcanzar cargador
            var p2 = AddPhase(plan);
            p2.displayName = "Alcanzar cargador";
            p2.role = LabGuidedPhaseRole.Reach;
            p2.start = 0.15f; p2.end = 0.30f;
            p2.hands = LabGuidedHands.Left;
            p2.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Fingers;
            p2.gripPreset = 12; // Tránsito relajado

            // 3. Extraer cargador
            var p3 = AddPhase(plan);
            p3.displayName = "Extraer cargador";
            p3.role = LabGuidedPhaseRole.Extract;
            p3.start = 0.30f; p3.end = 0.50f;
            p3.hands = LabGuidedHands.Left;
            p3.pieceId = mag.id;
            p3.pieceAction = LabGuidedPieceAction.Extract;
            p3.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece | LabGuidedChannels.Fingers;
            p3.gripPreset = 5; // Cargador AK

            // 4. Insertar cargador nuevo
            var p4 = AddPhase(plan);
            p4.displayName = "Insertar cargador nuevo";
            p4.role = LabGuidedPhaseRole.Insert;
            p4.start = 0.50f; p4.end = 0.75f;
            p4.hands = LabGuidedHands.Left;
            p4.pieceId = mag.id;
            p4.pieceAction = LabGuidedPieceAction.Insert;
            p4.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece | LabGuidedChannels.Fingers;
            p4.gripPreset = 5; // Cargador AK

            // 5. Asentar cargador (Golpe de palma)
            var p5 = AddPhase(plan);
            p5.displayName = "Asentar cargador (Golpe)";
            p5.role = LabGuidedPhaseRole.Seat;
            p5.start = 0.75f; p5.end = 0.88f;
            p5.hands = LabGuidedHands.Left;
            p5.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p5.gripPreset = 6; // Asentar cargador · golpe de palma

            // 6. Recuperar agarre
            var p6 = AddPhase(plan);
            p6.displayName = "Recuperar agarre";
            p6.role = LabGuidedPhaseRole.Recover;
            p6.start = 0.88f; p6.end = 1.00f;
            p6.hands = LabGuidedHands.Left;
            p6.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p6.gripPreset = 9; // Guardamanos · apoyo táctico

            if (!string.IsNullOrEmpty(magObjectId))
                SetBinding(plan, mag.id, magObjectId);

            return plan;
        }

        internal static LabGuidedReloadPlan CreateEmptyReloadPlan(string equipmentId, string magObjectId = null, string boltObjectId = null)
        {
            var plan = NewPlan(equipmentId);
            plan.patternName = "Recarga vacía con cerrojo";
            plan.generationMode = LabGuidedGenerationMode.SelectedChannels;
            plan.motionProfile = LabGuidedMotionProfile.Snappy;
            plan.magazinePolicy = LabGuidedMagazinePolicy.ExtractAndReplace;

            var mag = AddPiece(plan);
            mag.displayName = "Cargador";
            mag.kind = LabGuidedPieceKind.DetachableMagazine;

            var bolt = AddPiece(plan);
            bolt.displayName = "Cerrojo / Palanca";
            bolt.kind = LabGuidedPieceKind.ChargingHandle;

            // 1. Presentar arma
            var p1 = AddPhase(plan);
            p1.displayName = "Presentar arma";
            p1.role = LabGuidedPhaseRole.PreparePresent;
            p1.start = 0.00f; p1.end = 0.12f;
            p1.hands = LabGuidedHands.Left;
            p1.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p1.gripPreset = 9;

            // 2. Alcanzar cargador
            var p2 = AddPhase(plan);
            p2.displayName = "Alcanzar cargador";
            p2.role = LabGuidedPhaseRole.Reach;
            p2.start = 0.12f; p2.end = 0.25f;
            p2.hands = LabGuidedHands.Left;
            p2.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Fingers;
            p2.gripPreset = 12;

            // 3. Extraer cargador
            var p3 = AddPhase(plan);
            p3.displayName = "Extraer cargador";
            p3.role = LabGuidedPhaseRole.Extract;
            p3.start = 0.25f; p3.end = 0.42f;
            p3.hands = LabGuidedHands.Left;
            p3.pieceId = mag.id;
            p3.pieceAction = LabGuidedPieceAction.Extract;
            p3.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece | LabGuidedChannels.Fingers;
            p3.gripPreset = 5;

            // 4. Insertar cargador nuevo
            var p4 = AddPhase(plan);
            p4.displayName = "Insertar cargador nuevo";
            p4.role = LabGuidedPhaseRole.Insert;
            p4.start = 0.42f; p4.end = 0.62f;
            p4.hands = LabGuidedHands.Left;
            p4.pieceId = mag.id;
            p4.pieceAction = LabGuidedPieceAction.Insert;
            p4.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece | LabGuidedChannels.Fingers;
            p4.gripPreset = 5;

            // 5. Asentar cargador
            var p5 = AddPhase(plan);
            p5.displayName = "Asentar cargador (Golpe)";
            p5.role = LabGuidedPhaseRole.Seat;
            p5.start = 0.62f; p5.end = 0.72f;
            p5.hands = LabGuidedHands.Left;
            p5.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p5.gripPreset = 6;

            // 6. Cerrojo / Palanca
            var p6 = AddPhase(plan);
            p6.displayName = "Accionar cerrojo / palanca";
            p6.role = LabGuidedPhaseRole.OperateMechanism;
            p6.start = 0.72f; p6.end = 0.88f;
            p6.hands = LabGuidedHands.Left;
            p6.pieceId = bolt.id;
            p6.pieceAction = LabGuidedPieceAction.Operate;
            p6.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Mechanism | LabGuidedChannels.Fingers;
            p6.gripPreset = 7;

            // 7. Recuperar agarre
            var p7 = AddPhase(plan);
            p7.displayName = "Recuperar agarre";
            p7.role = LabGuidedPhaseRole.Recover;
            p7.start = 0.88f; p7.end = 1.00f;
            p7.hands = LabGuidedHands.Left;
            p7.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p7.gripPreset = 9;

            if (!string.IsNullOrEmpty(magObjectId))
                SetBinding(plan, mag.id, magObjectId);
            if (!string.IsNullOrEmpty(boltObjectId))
                SetBinding(plan, bolt.id, boltObjectId);

            return plan;
        }

        internal static LabGuidedReloadPlan CreateProceduralHandsPlan(string equipmentId)
        {
            var plan = NewPlan(equipmentId);
            plan.patternName = "Recarga procedimental de manos";
            plan.generationMode = LabGuidedGenerationMode.VisualMotionOnly;
            plan.motionProfile = LabGuidedMotionProfile.Fluid;
            plan.magazinePolicy = LabGuidedMagazinePolicy.KeepInPlace;

            // 1. Presentar arma
            var p1 = AddPhase(plan);
            p1.displayName = "Presentar arma";
            p1.role = LabGuidedPhaseRole.PreparePresent;
            p1.start = 0.00f; p1.end = 0.20f;
            p1.hands = LabGuidedHands.Left;
            p1.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p1.gripPreset = 9;

            // 2. Alcanzar zona de recarga
            var p2 = AddPhase(plan);
            p2.displayName = "Alcanzar zona de recarga";
            p2.role = LabGuidedPhaseRole.Reach;
            p2.start = 0.20f; p2.end = 0.40f;
            p2.hands = LabGuidedHands.Left;
            p2.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Fingers;
            p2.gripPreset = 12;

            // 3. Manipular
            var p3 = AddPhase(plan);
            p3.displayName = "Gesto de manipulación";
            p3.role = LabGuidedPhaseRole.Custom;
            p3.start = 0.40f; p3.end = 0.70f;
            p3.hands = LabGuidedHands.Left;
            p3.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Fingers;
            p3.gripPreset = 5;

            // 4. Asentar
            var p4 = AddPhase(plan);
            p4.displayName = "Asentar / Confirmar";
            p4.role = LabGuidedPhaseRole.Seat;
            p4.start = 0.70f; p4.end = 0.85f;
            p4.hands = LabGuidedHands.Left;
            p4.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p4.gripPreset = 6;

            // 5. Recuperar
            var p5 = AddPhase(plan);
            p5.displayName = "Recuperar guardia";
            p5.role = LabGuidedPhaseRole.Recover;
            p5.start = 0.85f; p5.end = 1.00f;
            p5.hands = LabGuidedHands.Left;
            p5.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p5.gripPreset = 9;

            return plan;
        }

        internal static LabGuidedReloadPlan CreateShotgunTubePlan(
            string equipmentId, string roundObjectId = null, string pumpObjectId = null, int shellCount = 4)
        {
            var plan = NewPlan(equipmentId);
            plan.patternName = "Recarga de escopeta de tubo";
            plan.generationMode = LabGuidedGenerationMode.SelectedChannels;
            plan.motionProfile = LabGuidedMotionProfile.Controlled;
            plan.magazinePolicy = LabGuidedMagazinePolicy.KeepInPlace;

            var round = AddPiece(plan);
            round.displayName = "Cartucho";
            round.kind = LabGuidedPieceKind.Round;
            round.suggestedCount = Math.Max(1, shellCount);

            LabGuidedPieceDefinition pump = null;
            if (!string.IsNullOrEmpty(pumpObjectId))
            {
                pump = AddPiece(plan);
                pump.displayName = "Corredera / Bombeo";
                pump.kind = LabGuidedPieceKind.ChargingHandle;
            }

            // 1. Presentar escopeta
            var p1 = AddPhase(plan);
            p1.displayName = "Presentar ventana de carga";
            p1.role = LabGuidedPhaseRole.PreparePresent;
            p1.start = 0.00f; p1.end = 0.12f;
            p1.hands = LabGuidedHands.Left;
            p1.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p1.gripPreset = 9; // Guardamanos · C-Clamp

            // 2. Alcanzar cartuchos
            var p2 = AddPhase(plan);
            p2.displayName = "Alcanzar cartuchos (Canana)";
            p2.role = LabGuidedPhaseRole.Reach;
            p2.start = 0.12f; p2.end = 0.25f;
            p2.hands = LabGuidedHands.Left;
            p2.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Fingers;
            p2.gripPreset = 10; // Precision Pinch

            // 3. Insertar cartucho en tubo
            var p3 = AddPhase(plan);
            p3.displayName = "Insertar en tubo de carga";
            p3.role = LabGuidedPhaseRole.Insert;
            p3.start = 0.25f; p3.end = 0.60f;
            p3.hands = LabGuidedHands.Left;
            p3.pieceId = round.id;
            p3.pieceAction = LabGuidedPieceAction.Insert;
            p3.repetitions = Math.Max(1, shellCount);
            p3.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece | LabGuidedChannels.Fingers;
            p3.gripPreset = 10; // Precision Pinch

            // 4. Asentar cartucho
            var p4 = AddPhase(plan);
            p4.displayName = "Asentar cartucho (Retenida)";
            p4.role = LabGuidedPhaseRole.Seat;
            p4.start = 0.60f; p4.end = 0.72f;
            p4.hands = LabGuidedHands.Left;
            p4.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p4.gripPreset = 6; // Seat Slap

            // 5. Bombeo / Corredera
            var p5 = AddPhase(plan);
            p5.displayName = "Accionar corredera / bombeo";
            p5.role = LabGuidedPhaseRole.OperateMechanism;
            p5.start = 0.72f; p5.end = 0.88f;
            p5.hands = LabGuidedHands.Left;
            if (pump != null)
            {
                p5.pieceId = pump.id;
                p5.pieceAction = LabGuidedPieceAction.Operate;
                p5.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Mechanism | LabGuidedChannels.Fingers;
            }
            else
            {
                p5.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            }
            p5.gripPreset = 11; // Tactical Fist

            // 6. Recuperar guardamanos
            var p6 = AddPhase(plan);
            p6.displayName = "Recuperar guardamanos";
            p6.role = LabGuidedPhaseRole.Recover;
            p6.start = 0.88f; p6.end = 1.00f;
            p6.hands = LabGuidedHands.Left;
            p6.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p6.gripPreset = 9;

            if (!string.IsNullOrEmpty(roundObjectId))
                SetBinding(plan, round.id, roundObjectId);
            if (pump != null && !string.IsNullOrEmpty(pumpObjectId))
                SetBinding(plan, pump.id, pumpObjectId);

            return plan;
        }

        internal static LabGuidedReloadPlan CreateRevolverPlan(
            string equipmentId, string cylinderObjectId = null, string loaderObjectId = null)
        {
            var plan = NewPlan(equipmentId);
            plan.patternName = "Recarga de revólver con tambor";
            plan.generationMode = LabGuidedGenerationMode.SelectedChannels;
            plan.motionProfile = LabGuidedMotionProfile.Controlled;
            plan.magazinePolicy = LabGuidedMagazinePolicy.KeepInPlace;

            var cylinder = AddPiece(plan);
            cylinder.displayName = "Tambor / Cilindro";
            cylinder.kind = LabGuidedPieceKind.RevolverCylinder;

            LabGuidedPieceDefinition loader = null;
            if (!string.IsNullOrEmpty(loaderObjectId))
            {
                loader = AddPiece(plan);
                loader.displayName = "Speedloader / Munición";
                loader.kind = LabGuidedPieceKind.Round;
                loader.suggestedCount = 6;
            }

            // 1. Presentar revólver
            var p1 = AddPhase(plan);
            p1.displayName = "Presentar revólver";
            p1.role = LabGuidedPhaseRole.PreparePresent;
            p1.start = 0.00f; p1.end = 0.12f;
            p1.hands = LabGuidedHands.Left;
            p1.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p1.gripPreset = 9;

            // 2. Bascular tambor
            var p2 = AddPhase(plan);
            p2.displayName = "Bascular / Abrir tambor";
            p2.role = LabGuidedPhaseRole.OperateMechanism;
            p2.start = 0.12f; p2.end = 0.28f;
            p2.hands = LabGuidedHands.Left;
            p2.pieceId = cylinder.id;
            p2.pieceAction = LabGuidedPieceAction.Operate;
            p2.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Mechanism | LabGuidedChannels.Fingers;
            p2.gripPreset = 10; // Precision Pinch

            // 3. Expulsar vainas
            var p3 = AddPhase(plan);
            p3.displayName = "Expulsar vainas (Varilla)";
            p3.role = LabGuidedPhaseRole.Extract;
            p3.start = 0.28f; p3.end = 0.44f;
            p3.hands = LabGuidedHands.Left;
            p3.pieceId = cylinder.id;
            p3.pieceAction = LabGuidedPieceAction.Extract;
            p3.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p3.gripPreset = 6; // Seat Slap a varilla extractora

            // 4. Insertar munición / speedloader
            var p4 = AddPhase(plan);
            p4.displayName = "Insertar munición / Speedloader";
            p4.role = LabGuidedPhaseRole.Insert;
            p4.start = 0.44f; p4.end = 0.70f;
            p4.hands = LabGuidedHands.Left;
            if (loader != null)
            {
                p4.pieceId = loader.id;
                p4.pieceAction = LabGuidedPieceAction.Insert;
                p4.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Piece | LabGuidedChannels.Fingers;
            }
            else
            {
                p4.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Fingers;
            }
            p4.gripPreset = 8; // Base Cup / copa

            // 5. Cerrar tambor
            var p5 = AddPhase(plan);
            p5.displayName = "Cerrar tambor (Cierre seco)";
            p5.role = LabGuidedPhaseRole.Seat;
            p5.start = 0.70f; p5.end = 0.84f;
            p5.hands = LabGuidedHands.Left;
            p5.pieceId = cylinder.id;
            p5.pieceAction = LabGuidedPieceAction.Seat;
            p5.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Mechanism | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p5.gripPreset = 6; // Golpe de palma para cerrar

            // 6. Recuperar empuñadura
            var p6 = AddPhase(plan);
            p6.displayName = "Recuperar empuñadura";
            p6.role = LabGuidedPhaseRole.Recover;
            p6.start = 0.84f; p6.end = 1.00f;
            p6.hands = LabGuidedHands.Left;
            p6.channels = LabGuidedChannels.Wrist | LabGuidedChannels.Weapon | LabGuidedChannels.Fingers;
            p6.gripPreset = 9;

            if (!string.IsNullOrEmpty(cylinderObjectId))
                SetBinding(plan, cylinder.id, cylinderObjectId);
            if (loader != null && !string.IsNullOrEmpty(loaderObjectId))
                SetBinding(plan, loader.id, loaderObjectId);

            return plan;
        }

        internal static int AutoDetectBindings(LabGuidedReloadPlan plan, System.Collections.Generic.IEnumerable<string> availableObjectIds)
        {
            if (plan?.pieces == null || availableObjectIds == null) return 0;
            var objectIdList = availableObjectIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (objectIdList.Count == 0) return 0;

            int boundCount = 0;
            foreach (var piece in plan.pieces)
            {
                if (piece == null || string.IsNullOrEmpty(piece.id)) continue;
                var existing = plan.calibration?.bindings?.FirstOrDefault(b => b != null && b.pieceId == piece.id);
                if (existing != null && !string.IsNullOrEmpty(existing.objectId)) continue;

                string matched = null;
                if (piece.kind == LabGuidedPieceKind.DetachableMagazine)
                {
                    matched = objectIdList.FirstOrDefault(id =>
                        id.IndexOf("mag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("cargador", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("clip", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else if (piece.kind == LabGuidedPieceKind.ChargingHandle || piece.kind == LabGuidedPieceKind.Bolt || piece.kind == LabGuidedPieceKind.Slide)
                {
                    matched = objectIdList.FirstOrDefault(id =>
                        id.IndexOf("charge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("bolt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("slide", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("cerrojo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("palanca", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("pump", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("bombeo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("corredera", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else if (piece.kind == LabGuidedPieceKind.RevolverCylinder)
                {
                    matched = objectIdList.FirstOrDefault(id =>
                        id.IndexOf("cylinder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("tambor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("drum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("revolver", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else if (piece.kind == LabGuidedPieceKind.Round)
                {
                    matched = objectIdList.FirstOrDefault(id =>
                        id.IndexOf("round", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("cartucho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("bullet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("shell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("bala", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id.IndexOf("speedloader", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else if (!string.IsNullOrEmpty(piece.displayName))
                {
                    matched = objectIdList.FirstOrDefault(id =>
                        id.IndexOf(piece.displayName, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (!string.IsNullOrEmpty(matched))
                {
                    SetBinding(plan, piece.id, matched);
                    boundCount++;
                }
            }
            return boundCount;
        }

        internal static System.Collections.Generic.List<LabGuidedRequirement> GetRequirements(LabGuidedReloadPlan plan)
        {
            var list = new System.Collections.Generic.List<LabGuidedRequirement>();
            if (plan?.phases == null) return list;

            var anchors = plan.calibration?.anchors ?? new System.Collections.Generic.List<LabGuidedAnchor>();

            foreach (var phase in plan.phases)
            {
                if (phase == null || string.IsNullOrEmpty(phase.id)) continue;
                string pName = string.IsNullOrEmpty(phase.displayName) ? "Fase" : phase.displayName;

                // Wrist Left
                if ((phase.channels & LabGuidedChannels.Wrist) != 0 && (phase.hands & LabGuidedHands.Left) != 0)
                {
                    AddReq(list, anchors, phase, pName, phase.start, true, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, null, null, "Muñeca izquierda");
                    AddReq(list, anchors, phase, pName, phase.end, false, LabGuidedAnchorKind.Wrist, LabGuidedHands.Left, null, null, "Muñeca izquierda");
                }
                // Wrist Right
                if ((phase.channels & LabGuidedChannels.Wrist) != 0 && (phase.hands & LabGuidedHands.Right) != 0)
                {
                    AddReq(list, anchors, phase, pName, phase.start, true, LabGuidedAnchorKind.Wrist, LabGuidedHands.Right, null, null, "Muñeca derecha");
                    AddReq(list, anchors, phase, pName, phase.end, false, LabGuidedAnchorKind.Wrist, LabGuidedHands.Right, null, null, "Muñeca derecha");
                }
                // Weapon
                if ((phase.channels & LabGuidedChannels.Weapon) != 0)
                {
                    AddReq(list, anchors, phase, pName, phase.start, true, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, null, null, "Pose de arma");
                    AddReq(list, anchors, phase, pName, phase.end, false, LabGuidedAnchorKind.Weapon, LabGuidedHands.None, null, null, "Pose de arma");
                }
                // Fingers - Hito 3 ampliado: se generan a partir de anclas de muñeca (comparten anclas).
                // No se requieren anclas separadas para dedos; usan las mismas poses de muñeca.
                // Piece, Mechanism: aún no soportados en Hito 3 ampliado.
                // Se reactivarán en futuras ampliaciones.
            }
            return list;
        }

        private static void AddReq(
            System.Collections.Generic.List<LabGuidedRequirement> list,
            System.Collections.Generic.List<LabGuidedAnchor> anchors,
            LabGuidedReloadPhase phase, string phaseName, float time, bool isStart,
            LabGuidedAnchorKind kind, LabGuidedHands hand, string pieceId, string pieceName, string channelLabel)
        {
            bool satisfied = anchors.Any(a => a != null
                && a.phaseId == phase.id
                && LabGuidedAnchorTools.EffectiveKind(a) == kind
                && (kind != LabGuidedAnchorKind.Wrist || a.hand == hand)
                && (kind != LabGuidedAnchorKind.Piece && kind != LabGuidedAnchorKind.Mechanism || string.Equals(a.pieceId, pieceId, StringComparison.Ordinal))
                && Math.Abs(a.time - time) <= 0.001f
                && !float.IsNaN(a.position.x));

            string timingLabel = isStart ? "Inicio" : "Fin";
            list.Add(new LabGuidedRequirement
            {
                phaseId = phase.id,
                phaseDisplayName = phaseName,
                time = time,
                isStart = isStart,
                kind = kind,
                hand = hand,
                pieceId = pieceId,
                pieceDisplayName = pieceName,
                isSatisfied = satisfied,
                label = $"{phaseName} · {channelLabel} ({timingLabel})"
            });
        }

        internal static LabGuidedReloadPlan CopyPattern(LabGuidedReloadPlan source, string equipmentId)
        {
            if (source == null || !source.configured)
                throw new InvalidOperationException("Selecciona una toma con una secuencia guiada configurada.");
            var copy = LabGuidedReloadPlan.Clone(source);
            copy.patternId = Guid.NewGuid().ToString("N");
            copy.patternName = "Copia de " + source.patternName;
            copy.calibration = new LabGuidedReloadCalibration
            {
                calibrationId = Guid.NewGuid().ToString("N"),
                equipmentId = equipmentId
            };
            return copy;
        }

        internal static LabGuidedPieceDefinition AddPiece(LabGuidedReloadPlan plan)
        {
            RequirePlan(plan);
            plan.pieces.Add(new LabGuidedPieceDefinition
            {
                displayName = "Pieza " + (plan.pieces.Count + 1),
                kind = LabGuidedPieceKind.Custom
            });
            return plan.pieces[plan.pieces.Count - 1];
        }

        internal static LabGuidedReloadPhase AddPhase(LabGuidedReloadPlan plan)
        {
            RequirePlan(plan);
            float cursor = 0f, start = 0f, end = 1f;
            bool foundGap = false;
            foreach (var interval in plan.phases.Where(p => p != null && Finite(p.start) && Finite(p.end)
                    && p.start >= 0f && p.end <= 1f && p.start < p.end)
                .Select(p => (start: p.start, end: p.end)).OrderBy(p => p.start))
            {
                if (interval.start > cursor + 0.0001f)
                {
                    start = cursor;
                    end = interval.start;
                    foundGap = true;
                    break;
                }
                cursor = Math.Max(cursor, interval.end);
            }
            if (!foundGap && cursor < 1f)
            {
                start = cursor;
                end = 1f;
                foundGap = true;
            }
            // A fully occupied schedule needs an explicit user choice to overlap or retime.
            if (!foundGap) { start = 0f; end = 1f; }

            plan.phases.Add(new LabGuidedReloadPhase
            {
                displayName = "Fase " + (plan.phases.Count + 1),
                role = LabGuidedPhaseRole.Custom,
                start = start,
                end = end
            });
            return plan.phases[plan.phases.Count - 1];
        }

        internal static bool CanRemovePiece(LabGuidedReloadPlan plan, string pieceId)
        {
            if (plan?.pieces == null || string.IsNullOrWhiteSpace(pieceId)) return false;
            return !(plan.phases?.Any(p => p != null && p.pieceId == pieceId) ?? false)
                && !(plan.calibration?.bindings?.Any(b => b != null && b.pieceId == pieceId) ?? false)
                && !(plan.calibration?.anchors?.Any(a => a != null && a.pieceId == pieceId) ?? false);
        }

        internal static bool RemovePiece(LabGuidedReloadPlan plan, string pieceId)
        {
            if (!CanRemovePiece(plan, pieceId)) return false;
            return plan.pieces.RemoveAll(p => p != null && p.id == pieceId) > 0;
        }

        internal static bool MovePhase(LabGuidedReloadPlan plan, int index, int delta)
        {
            if (plan?.phases == null || index < 0 || index >= plan.phases.Count) return false;
            int target = index + delta;
            if (target < 0 || target >= plan.phases.Count) return false;
            var phase = plan.phases[index];
            plan.phases[index] = plan.phases[target];
            plan.phases[target] = phase;
            return true;
        }

        internal static bool RemovePhase(LabGuidedReloadPlan plan, string phaseId)
        {
            return plan?.phases != null && plan.phases.RemoveAll(p => p != null && p.id == phaseId) > 0;
        }

        internal static int CopyPreviousPhaseAnchors(LabGuidedReloadPlan plan, int phaseIndex)
        {
            RequirePlan(plan);
            if (plan.calibration == null || plan.calibration.anchors == null) return 0;
            if (phaseIndex <= 0 || phaseIndex >= plan.phases.Count) return 0;
            var currentPhase = plan.phases[phaseIndex];
            var previousPhase = plan.phases[phaseIndex - 1];
            if (currentPhase == null || previousPhase == null) return 0;

            var prevEndAnchors = plan.calibration.anchors
                .Where(a => a != null && a.phaseId == previousPhase.id
                    && Math.Abs(a.time - previousPhase.end) <= 0.0001f)
                .ToList();

            int copied = 0;
            foreach (var prevAnchor in prevEndAnchors)
            {
                var kind = LabGuidedAnchorTools.EffectiveKind(prevAnchor);
                // Ensure the channel and hand are still relevant to the current phase
                if (kind == LabGuidedAnchorKind.Wrist)
                {
                    if ((currentPhase.channels & LabGuidedChannels.Wrist) == 0) continue;
                    if ((currentPhase.hands & prevAnchor.hand) == 0) continue;
                }
                else if (kind == LabGuidedAnchorKind.Weapon)
                {
                    if ((currentPhase.channels & LabGuidedChannels.Weapon) == 0) continue;
                }
                else if (kind == LabGuidedAnchorKind.Piece)
                {
                    if ((currentPhase.channels & LabGuidedChannels.Piece) == 0) continue;
                    if (!string.Equals(currentPhase.pieceId, prevAnchor.pieceId, StringComparison.Ordinal)) continue;
                }
                else if (kind == LabGuidedAnchorKind.Mechanism)
                {
                    if ((currentPhase.channels & LabGuidedChannels.Mechanism) == 0) continue;
                    if (!string.Equals(currentPhase.pieceId, prevAnchor.pieceId, StringComparison.Ordinal)) continue;
                }

                // Upsert into current phase start
                var existing = plan.calibration.anchors.FirstOrDefault(a => a != null
                    && a.phaseId == currentPhase.id && LabGuidedAnchorTools.EffectiveKind(a) == kind
                    && a.hand == prevAnchor.hand && a.space == prevAnchor.space
                    && Math.Abs(a.time - currentPhase.start) <= 0.0001f);

                if (existing == null)
                {
                    existing = new LabGuidedAnchor();
                    plan.calibration.anchors.Add(existing);
                }

                existing.phaseId = currentPhase.id;
                existing.role = currentPhase.role.ToString();
                existing.kind = kind;
                existing.pieceId = prevAnchor.pieceId;
                existing.hand = prevAnchor.hand;
                existing.objectId = prevAnchor.objectId;
                existing.space = prevAnchor.space;
                existing.time = currentPhase.start;
                existing.position = prevAnchor.position;
                existing.rotation = prevAnchor.rotation;
                copied++;
            }

            return copied;
        }

        internal static void SetBinding(LabGuidedReloadPlan plan, string pieceId, string objectId)
        {
            RequirePlan(plan);
            if (plan.calibration == null) plan.calibration = new LabGuidedReloadCalibration();
            if (plan.calibration.bindings == null) plan.calibration.bindings = new System.Collections.Generic.List<LabGuidedPieceBinding>();
            plan.calibration.bindings.RemoveAll(b => b != null && b.pieceId == pieceId);
            if (!string.IsNullOrEmpty(objectId))
                plan.calibration.bindings.Add(new LabGuidedPieceBinding { pieceId = pieceId, objectId = objectId });
        }

        private static void RequirePlan(LabGuidedReloadPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.pieces == null) plan.pieces = new System.Collections.Generic.List<LabGuidedPieceDefinition>();
            if (plan.phases == null) plan.phases = new System.Collections.Generic.List<LabGuidedReloadPhase>();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
