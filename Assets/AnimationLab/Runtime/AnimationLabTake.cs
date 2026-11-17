using System;
using System.Collections.Generic;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab
{
    // Zero preserves old takes: wrists/weapon used zero tangents; pieces used straight segments.
    public enum LabPathMode { Legacy, Linear, Smooth, Arc, Ease, Cinematic, Bezier, Flow }
    public enum LabHandSource { Procedural, Reload, ReloadEmpty }
    public enum LabFingerBlend { Legacy, Linear, Smooth, Step }
    public enum LabReloadTemplate { Magazine, Revolver, SingleRound, Bolt, FireBolt }
    public enum LabHandTemplate { Pistol, SubmachineGun, Rifle, SniperBolt, SingleRound, Revolver, Shotgun, Bow }

    [Serializable]
    public sealed class LabGesturePoint
    {
        public string label = "Punto";
        public float time;
        public Vector3 position, euler, bend;
        public LabPathMode mode = LabPathMode.Cinematic;
        public float grip = -1; // Negative: use the template's finger phase.
        public float departureSpeed, arrivalSpeed;
        public bool contactAnchor;
        public Vector3 outgoing, incoming;
        public bool hasReference;
        public Vector3 referencePosition, referenceEuler;
    }

    [Serializable]
    public sealed class LabHandInsert
    {
        public LabHandTemplate template;
        public LabHandSource source;
        public HandSide side = HandSide.Left;
        public float start, end = 1;
        public int repetitions = 3;
        public bool hasContact;
        public Vector3 contactPosition, contactEuler;
        // Physical metres in character axes, independent of an imported bone's scale.
        public Vector3 travel = new Vector3(-0.18f, -0.05f, 0);
        public float arc = 0.03f;
        public bool useStyle;
        public float reachScale = 1, energy = 0.5f, recovery = 0.5f, thumb = 0.5f, curvature = 1;
        public float settle; // Fraction of inserted interval held at the start of the release phase.
        public int gripPreset = -1;
        // Six editable phases. Empty preserves the original template's timing.
        public float[] phaseTimes = Array.Empty<float>();
        public float[] phaseGrip = Array.Empty<float>();
        public List<LabGesturePoint> points = new List<LabGesturePoint>();
    }

    [Serializable]
    public sealed class LabReloadRecipe
    {
        public LabReloadTemplate template;
        public HandSide side = HandSide.Left;
        public string objectId;
        public bool hasRest, hasContact, hasOutside;
        // Rest/outside in the piece parent's coordinates; contact in piece coordinates.
        public Vector3 restPosition, restEuler, contactPosition, contactEuler, outsidePosition, outsideEuler;
        public Vector3 arc = new Vector3(0.04f, 0, 0); // Physical metres in the piece parent's axes.
        public int rounds = 3;
        public float duration = 2.4f;
    }

    [Serializable]
    public sealed class LabHandKey
    {
        [Range(0f, 1f)] public float time;
        public Vector3 position, euler;
        public HandPoseData fingers;
        // False for legacy combined keys; finger-only keys never rebuild wrist motion.
        public bool fingersOnly;
        public LabPathMode pathMode;
        public Vector3 bend; // Outgoing segment midpoint offset in the same local units as position.
        public float departureSpeed, arrivalSpeed;
        public Vector3 outgoing, incoming;
        // Incoming finger transition only. Zero preserves legacy whole-interval interpolation.
        public LabFingerBlend fingerBlend;
        public float fingerBlendDuration; // Normalized duration ending at this key.
        public string fingerLabel; // Optional authoring label; never infer it from a stale UI selection.
    }

    [Serializable]
    public sealed class LabPathContact
    {
        public bool active; // Unity inline serialization does not preserve null; false keeps old keys unlinked.
        public Vector3 position, euler;
    }

    [Serializable]
    public sealed class LabWeaponKey
    {
        [Range(0f, 1f)] public float time;
        public Vector3 position, euler;
        public LabPathMode pathMode;
        public Vector3 bend;
        public float departureSpeed, arrivalSpeed;
        public Vector3 outgoing, incoming;
        // Authoring recipe only; compiled to ordinary wrist curves, never a runtime constraint.
        public LabPathContact leftContact, rightContact;
        public bool breakLeftContact, breakRightContact; // Detach only this outgoing interval, not its neighbour.
    }

    [Serializable]
    public sealed class LabWeaponDynamicsRecipe
    {
        public bool applied;
        public float intensity=.55f;
        public EquipmentVectorCurves compiledPosition=new EquipmentVectorCurves(),compiledRotation=new EquipmentVectorCurves();
        public List<LabWeaponKey> sourceKeys=new List<LabWeaponKey>();
        public EquipmentVectorCurves sourcePosition=new EquipmentVectorCurves(),sourceRotation=new EquipmentVectorCurves();
    }

    [Serializable]
    public sealed class LabObjectTrack
    {
        // Relative to the separate equipment visual, never a scene object reference.
        public string path;
        public List<LabWeaponKey> keys = new List<LabWeaponKey>();
    }

    [Serializable]
    public sealed class LabPrimitive
    {
        public PrimitiveType type = PrimitiveType.Cube;
        public Vector3 position, euler;
        // Full dimensions in metres; cylinders use diameter/height/diameter, not Unity's 2-unit default height.
        public Vector3 size = new Vector3(0.04f, 0.12f, 0.03f);
    }

    [Serializable]
    public sealed class LabProp
    {
        public string id, name = "Mi objeto";
        public Vector3 position, euler;
        public List<LabPrimitive> shapes = new List<LabPrimitive>();
        public List<LabWeaponKey> keys = new List<LabWeaponKey>();
    }

    [Serializable]
    public sealed class LabAnchor
    {
        public string path, role;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
    }

    /// <summary>Portable authoring source. Runtime consumes the exported EquipmentActionDefinition.
    /// No scene object references, gameplay state or third-party runtime dependency.</summary>
    public sealed class AnimationLabTake : ScriptableObject
    {
        public int slot;
        public string equipmentId;
        public EquipmentActionKind kind = EquipmentActionKind.MeleeAttack;
        public EquipmentActionDefinition action;
        public bool hasSourceCharacterClip;
        public AnimationClip sourceCharacterClip;
        public AvatarMask sourceCharacterMask;
        public bool hasSourceWeaponClip;
        public AnimationClip sourceWeaponClip;
        public List<LabHandKey> left = new List<LabHandKey>();
        public List<LabHandKey> right = new List<LabHandKey>();
        public List<LabWeaponKey> weapon = new List<LabWeaponKey>();
        public LabWeaponDynamicsRecipe weaponDynamics=new LabWeaponDynamicsRecipe();
        public List<LabObjectTrack> parts = new List<LabObjectTrack>();
        public List<LabProp> props = new List<LabProp>();
        public List<LabAnchor> anchors = new List<LabAnchor>();
        // Keep compensation provenance after detaching the last contact interval.
        public bool leftContactMotion, rightContactMotion;
        public LabReloadRecipe reload = new LabReloadRecipe();
        public LabHandInsert handInsert = new LabHandInsert();
        // User-authored phase data; never interpreted by runtime gameplay.
        public LabGuidedReloadPlan guidedReload = new LabGuidedReloadPlan();
    }
}
