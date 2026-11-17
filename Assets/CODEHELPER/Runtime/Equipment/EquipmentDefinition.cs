using UnityEngine;
using CodeHelper.ProceduralShooterFramework.Weapons;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    public enum EquipmentFamily { Firearm, Melee }
    public enum FirearmMechanism { SelfLoading, ManualBolt, Pump }
    public enum FireMode { SemiAutomatic, Automatic, Burst }
    public enum ReloadMethod { DetachableMagazine, IndividualRounds }
    public enum EquipmentActionKind { None, Equip, Unequip, Fire, Cycle, Reload, MeleeAttack }
    public enum EquipmentSwitchPhase { Ready, Unequipping, Equipping }

    /// <summary>Shared authoring data only. Never store ammunition or action progress here.</summary>
    public abstract class EquipmentDefinition : ScriptableObject
    {
        public string equipmentId;
        public string displayName;
        [Tooltip("Optional. Fists do not require a physical weapon prefab.")]
        public GameObject visualPrefab;
        public WeaponPoseProfile weaponPoses;
        [Tooltip("Base pose for arms/fingers, e.g. pistol grip or closed fists. Not an IK target.")]
        public AnimationClip characterPose;
        public AvatarMask characterMask;
        [Range(0f, 1f)] public float leftGripWeight = 1f;
        [Range(0f, 1f)] public float rightGripWeight = 1f;
        public bool canAim = true;
        public EquipmentActionDefinition equip;
        public EquipmentActionDefinition unequip;
        public abstract EquipmentFamily Family { get; }
    }
}
