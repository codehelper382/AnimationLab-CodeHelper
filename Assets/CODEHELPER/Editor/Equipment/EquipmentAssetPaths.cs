#if UNITY_EDITOR
namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Authoring paths only. Runtime equipment classification lives in EquipmentFamily.</summary>
    public static class EquipmentAssetPaths
    {
        public const string Root = "Assets/CODEHELPER/EquipmentAssets";
        public const string Scene = "Assets/Scenes/AnimationLab.unity";
        public const string Ak12 = Root + "/Firearms/AK12";
        public const string Ak12Definition = Ak12 + "/AK12_Definition.asset";
        public const string Ak12Profile = "Assets/AK 12.asset";
        public const string Ak12ReloadMotion = Ak12 + "/Animations/AK12_LeftReloadTarget.anim";
        public const string Melee = Root + "/Melee";
        public const string Fists = Melee + "/Fists";
        public const string FistsDefinition = Fists + "/Fists_Definition.asset";
        public const string FistsAnimations = Fists + "/Animations";
        public const string Knife = Melee + "/Knife";
        public const string Masks = Root + "/Shared/Masks";
        public const string ArmsMask = Masks + "/EquipmentArms.mask";
        public const string ReloadFingerMask = Masks + "/LeftReloadFingers.mask";
        public const string SpineProfile = "Assets/Base.asset";
    }
}
#endif
