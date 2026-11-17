using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab
{
    [CreateAssetMenu(menuName = "CodeHelper/Animation Lab/Tipo de agarre", fileName = "GripPreset")]
    public sealed class LabGripPreset : ScriptableObject
    {
        public string displayName = "Mi agarre", category = "Personalizado";
        public HandSide side = HandSide.Right;
        public HandPoseData fingers;
    }
}
