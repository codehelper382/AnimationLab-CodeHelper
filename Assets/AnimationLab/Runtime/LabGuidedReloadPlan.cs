using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeHelper.AnimationLab
{
    public enum LabGuidedPieceKind
    {
        Custom = 0,
        DetachableMagazine = 1,
        RevolverCylinder = 2,
        Round = 3,
        Slide = 4,
        Bolt = 5,
        ChargingHandle = 6
    }

    public enum LabGuidedPhaseRole
    {
        Custom = 0,
        PreparePresent = 1,
        Reach = 2,
        Acquire = 3,
        Release = 4,
        Extract = 5,
        Supply = 6,
        Align = 7,
        Insert = 8,
        Seat = 9,
        OperateMechanism = 10,
        Wait = 11,
        Recover = 12
    }

    public enum LabGuidedPieceAction
    {
        NoChange = 0,
        Acquire = 1,
        Hold = 2,
        Release = 3,
        Extract = 4,
        Transfer = 5,
        Insert = 6,
        Seat = 7,
        Operate = 8,
        Custom = 9
    }

    [Flags]
    public enum LabGuidedHands
    {
        None = 0,
        Left = 1,
        Right = 2,
        Both = Left | Right
    }

    [Flags]
    public enum LabGuidedChannels
    {
        None = 0,
        Wrist = 1,
        Fingers = 2,
        Weapon = 4,
        Piece = 8,
        Mechanism = 16,
        All = Wrist | Fingers | Weapon | Piece | Mechanism
    }

    public enum LabGuidedAnchorSpace
    {
        Guard = 0,
        Target = 1,
        Parent = 2
    }

    public enum LabGuidedAnchorKind
    {
        Unspecified = 0,
        Wrist = 1,
        Piece = 2,
        Weapon = 3,
        Mechanism = 4
    }

    public enum LabGuidedGenerationMode
    {
        VisualMotionOnly = 0,
        SelectedChannels = 1
    }

    public enum LabGuidedMotionProfile
    {
        Linear = 0,
        Controlled = 1,
        Fluid = 2,
        Snappy = 3
    }

    public enum LabGuidedMagazinePolicy
    {
        KeepInPlace = 0,    // El cargador no se desacopla (recarga táctica con retención o munición fija)
        ExtractAndDrop = 1, // Se extrae y cae (recarga táctica rápida / drop mag)
        ExtractAndStore = 2,// Se extrae y se guarda en equipamiento
        ExtractAndReplace = 3// Se extrae el cargador gastado y se introduce uno nuevo
    }

    [Serializable]
    public sealed class LabGuidedPieceDefinition
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "Pieza nueva";
        public LabGuidedPieceKind kind = LabGuidedPieceKind.Custom;
        [Min(1)] public int suggestedCount = 1;
    }

    [Serializable]
    public sealed class LabGuidedReloadPhase
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "Nueva fase";
        public LabGuidedPhaseRole role = LabGuidedPhaseRole.Custom;
        [Range(0f, 1f)] public float start;
        [Range(0f, 1f)] public float end = 1f;
        [Min(1)] public int repetitions = 1;
        public string pieceId;
        public LabGuidedPieceAction pieceAction = LabGuidedPieceAction.NoChange;
        public LabGuidedHands hands;
        public LabGuidedChannels channels;
        public bool allowOverlap;
        public LabPathMode pathMode = LabPathMode.Legacy; // Legacy (0) means auto / inherit from plan profile
        public float arc; // Optional extra arc height in cm (0 = auto from profile)
        public int gripPreset = -1; // -1 = automático según role; >= 0 índice explícito de preset
        public LabFingerBlend fingerBlend = LabFingerBlend.Smooth;
        [Range(0f, 1f)] public float fingerBlendDuration = 0.12f;
        public string fingerLabel;
    }

    [Serializable]
    public sealed class LabGuidedPieceBinding
    {
        public string pieceId;
        // Stable authoring IDs: "part:<hierarchy path>" or "prop:<take-local id>".
        public string objectId;
    }

    [Serializable]
    public sealed class LabGuidedAnchor
    {
        public string id = Guid.NewGuid().ToString("N");
        public string phaseId;
        public string role;
        public LabGuidedAnchorKind kind;
        public string pieceId;
        public LabGuidedHands hand;
        // Optional object binding for guard/target/parent-local reference data.
        public string objectId;
        public LabGuidedAnchorSpace space;
        [Range(0f, 1f)] public float time;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
    }

    public static class LabGuidedAnchorTools
    {
        // Early v1 snapshots predate this field. Infer only unambiguous wrist/piece shapes.
        public static LabGuidedAnchorKind EffectiveKind(LabGuidedAnchor anchor)
        {
            if (anchor == null) return LabGuidedAnchorKind.Unspecified;
            if (anchor.kind != LabGuidedAnchorKind.Unspecified) return anchor.kind;
            if (anchor.space == LabGuidedAnchorSpace.Guard
                && (anchor.hand == LabGuidedHands.Left || anchor.hand == LabGuidedHands.Right)
                && string.IsNullOrEmpty(anchor.objectId))
                return LabGuidedAnchorKind.Wrist;
            if (anchor.space == LabGuidedAnchorSpace.Parent
                && anchor.hand == LabGuidedHands.None
                && !string.IsNullOrEmpty(anchor.pieceId) && HasStableObjectId(anchor.objectId))
                return LabGuidedAnchorKind.Piece;
            return LabGuidedAnchorKind.Unspecified;
        }

        private static bool HasStableObjectId(string objectId) =>
            !string.IsNullOrWhiteSpace(objectId)
            && ((objectId.StartsWith("part:", StringComparison.Ordinal) && objectId.Length > 5)
                || (objectId.StartsWith("prop:", StringComparison.Ordinal) && objectId.Length > 5));
    }

    [Serializable]
    public sealed class LabGuidedReloadCalibration
    {
        public string calibrationId = Guid.NewGuid().ToString("N");
        public string equipmentId;
        public string rigId;
        public List<LabGuidedPieceBinding> bindings = new List<LabGuidedPieceBinding>();
        public List<LabGuidedAnchor> anchors = new List<LabGuidedAnchor>();
    }

    /// <summary>
    /// Portable authoring snapshot for a user-defined reload sequence.
    /// Contains data only; it is not a gameplay state machine and is not evaluated by runtime code.
    /// </summary>
    [Serializable]
    public sealed class LabGuidedReloadPlan
    {
        public const int CurrentVersion = 1;
        public const int MaxRepetitions = 32;

        public int version = CurrentVersion;
        public bool configured;
        public string patternId = Guid.NewGuid().ToString("N");
        public string patternName = "Recarga nueva";
        public LabGuidedGenerationMode generationMode = LabGuidedGenerationMode.VisualMotionOnly;
        public LabGuidedMotionProfile motionProfile = LabGuidedMotionProfile.Linear;
        public LabGuidedMagazinePolicy magazinePolicy = LabGuidedMagazinePolicy.ExtractAndReplace;
        public List<LabGuidedPieceDefinition> pieces = new List<LabGuidedPieceDefinition>();
        public List<LabGuidedReloadPhase> phases = new List<LabGuidedReloadPhase>();
        public LabGuidedReloadCalibration calibration = new LabGuidedReloadCalibration();

        public static LabGuidedReloadPlan Clone(LabGuidedReloadPlan source)
        {
            return source == null
                ? new LabGuidedReloadPlan()
                : JsonUtility.FromJson<LabGuidedReloadPlan>(JsonUtility.ToJson(source)) ?? new LabGuidedReloadPlan();
        }
    }
}
