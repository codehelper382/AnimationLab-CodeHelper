using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    [CreateAssetMenu(menuName = "CodeHelper/Equipment/Melee", fileName = "Melee")]
    public sealed class MeleeDefinition : EquipmentDefinition
    {
        [Tooltip("Fists and held melee weapons share the Melee category. Enable only for unarmed equipment.")]
        public bool isUnarmed;
        [Tooltip("For unarmed equipment, keep the animated guard and punches oriented to the camera. Does not change finger animation or weapon grip IK.")]
        public bool followCamera;
        [Tooltip("Ordered actions, e.g. left/right punches. Runtime keeps the sequence index per item.")]
        public EquipmentActionDefinition[] attacks = new EquipmentActionDefinition[0];
        [Min(0f)] public float attackInterval = 0.35f;
        [Min(0f)] public float damage = 25f;
        [Min(0f)] public float reach = 1.5f;
        public override EquipmentFamily Family => EquipmentFamily.Melee;

        private void Reset()
        {
            canAim = false;
            leftGripWeight = rightGripWeight = 0f;
        }
    }
}
