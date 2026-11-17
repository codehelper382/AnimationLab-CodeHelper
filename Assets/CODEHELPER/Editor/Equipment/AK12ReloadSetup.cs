#if UNITY_EDITOR
using System;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Finger clip only; wrist/arm placement belongs to the assigned weapon-relative IK target.</summary>
    public static class AK12ReloadSetup
    {
        public const string MaskPath = EquipmentAssetPaths.ReloadFingerMask;
        private const string CharacterSource = "Assets/kindemo/Demo 1/Animations/Locomotion/Humanoid/WeaponSet/Standing/C_Rifle_Reload_Humanoid.fbx";

        public static void Configure()
        {
            var definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(EquipmentAssetPaths.Ak12Definition);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(CharacterSource).OfType<AnimationClip>()
                .FirstOrDefault(value => value.name == "C_Rifle_Reload_Humanoid");
            if (definition == null || clip == null || !clip.isHumanMotion || clip.length <= 0f)
                throw new Exception("AK12 definition or existing Humanoid rifle reload missing.");
            if (definition.reload == null || definition.reloadEmpty == null) throw new Exception("Reload actions missing.");
            AvatarMask mask = GetFingerMask();
            ConfigureAction(definition.reload, clip, mask);
            ConfigureAction(definition.reloadEmpty, clip, mask);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(mask);
            AssetDatabase.SaveAssetIfDirty(definition);
            Debug.Log($"[AK12ReloadSetup] Reload configured: {clip.name}, {clip.length:F3}s. " +
                "Clip affects only left fingers; arm/wrist use reload target IK; right grip/IK stays 1.");
        }

        // Narrow migration: preserve user's clips, timing, curves and target trajectory; change only ownership mask.
        public static void ConfigureFingerMask()
        {
            var definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(EquipmentAssetPaths.Ak12Definition);
            if (definition?.reload == null || definition.reloadEmpty == null) throw new Exception("Reload actions missing.");
            AvatarMask mask = GetFingerMask();
            definition.reload.characterMask = definition.reloadEmpty.characterMask = mask;
            EditorUtility.SetDirty(definition.reload);
            EditorUtility.SetDirty(definition.reloadEmpty);
            AssetDatabase.SaveAssetIfDirty(mask);
            AssetDatabase.SaveAssetIfDirty(definition);
            Debug.Log("[AK12ReloadSetup] Finger-only mask assigned. All clips, action timing, target motion and camera settings preserved.");
        }

        private static AvatarMask GetFingerMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(MaskPath) != null) throw new Exception("Mask path contains a different asset.");
                mask = new AvatarMask { name = "LeftReloadFingers" };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                        i == (int)AvatarMaskBodyPart.LeftFingers);
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            // Never change a previously authored mask silently.
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                if (mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i) !=
                    (i == (int)AvatarMaskBodyPart.LeftFingers))
                    throw new Exception("Reload mask must affect only left fingers, not the IK-driven arm.");
            return mask;
        }

        private static void ConfigureAction(EquipmentActionDefinition action, AnimationClip clip, AvatarMask mask)
        {
            action.characterClip = clip;
            action.weaponClip = null;
            action.characterMask = mask;
            action.duration = clip.length;
            action.commitTime = 1f;
            action.blendIn = action.blendOut = clip.length * 0.04f;
            action.followAnimatedRightHand = false;
            action.canCancel = true;
            action.leftGrip = ReleaseCurve();
            action.rightGrip = AnimationCurve.Constant(0f, 1f, 1f);
            action.leftIK = AnimationCurve.Constant(0f, 1f, 1f);
            action.rightIK = AnimationCurve.Constant(0f, 1f, 1f);
            action.weaponPosition = new EquipmentVectorCurves();
            action.weaponRotation = new EquipmentVectorCurves();
            EditorUtility.SetDirty(action);
        }

        private static AnimationCurve ReleaseCurve() => new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.04f, 0f), new Keyframe(0.96f, 0f), new Keyframe(1f, 1f));

    }
}
#endif
