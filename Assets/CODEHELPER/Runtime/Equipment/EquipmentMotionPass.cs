using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    [AddComponentMenu("CodeHelper/Shooter Framework/Equipment/Equipment Motion Pass")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(575)]
    public sealed class EquipmentMotionPass : MonoBehaviour
    {
        public EquipmentSceneController source;
        private void LateUpdate()
        {
            if (source != null && source.isActiveAndEnabled) source.ApplyMotion();
        }
    }
}
