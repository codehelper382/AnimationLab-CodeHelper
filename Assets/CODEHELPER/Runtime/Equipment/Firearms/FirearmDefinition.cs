using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    [CreateAssetMenu(menuName = "CodeHelper/Equipment/Firearm", fileName = "Firearm")]
    public sealed class FirearmDefinition : EquipmentDefinition
    {
        public FirearmMechanism mechanism;
        public FireMode fireMode;
        [Min(1)] public int burstSize = 3;
        public ReloadMethod reloadMethod;
        [Tooltip("Excludes the separate chamber. A full magazine plus one chambered round is supported.")]
        [Min(1)] public int magazineCapacity = 30;
        [Min(0)] public int initialMagazineRounds = 29;
        public bool initiallyChambered = true;
        [Min(0)] public int initialReserveRounds = 90;
        [Min(1f)] public float roundsPerMinute = 600f;
        [Tooltip("Enable only when the reload action also operates the mechanism. Otherwise use Cycle afterwards.")]
        public bool reloadChambersRound;
        public EquipmentActionDefinition fire;
        public EquipmentActionDefinition cycle;
        public EquipmentActionDefinition reload;
        public EquipmentActionDefinition reloadEmpty;
        [Tooltip("Optional single-shell insertion action; one invocation inserts one round.")]
        public EquipmentActionDefinition insertRound;
        public override EquipmentFamily Family => EquipmentFamily.Firearm;
    }
}
