#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using CodeHelper.ProceduralShooterFramework.Weapons;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Explicit asset authoring for the SampleScene fixture. Never overwrites existing assets or opens a scene.</summary>
    public static class SampleEquipmentSetup
    {
        public const string Folder = EquipmentAssetPaths.Root;

        public static void CreateAssets()
        {
            if (AssetDatabase.LoadMainAssetAtPath(EquipmentAssetPaths.Ak12Definition) != null ||
                AssetDatabase.LoadMainAssetAtPath(EquipmentAssetPaths.FistsDefinition) != null ||
                AssetDatabase.LoadMainAssetAtPath(EquipmentAssetPaths.ArmsMask) != null ||
                AssetDatabase.IsValidFolder(EquipmentAssetPaths.FistsAnimations))
                throw new InvalidOperationException("Sample equipment assets already exist; refusing overwrite.");
            // Preflight references before creating output. Vendor fists are Generic; own copies are converted below.
            var poses = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(EquipmentAssetPaths.Ak12Profile);
            if (poses == null) throw new Exception("Existing AK12 pose profile missing.");
            AnimationClip riflePose = Clip(AssetDatabase.GUIDToAssetPath("8230767c9bcb5484e8ba4420c3a9e7ae"), true);
            foreach (string clipName in new[] { "Idle", "Equip", "UnEquip", "Punch" })
                if (Clip("kindemo/Demo 1/Prefabs/Fists/Fists_" + clipName + ".fbx", false) == null)
                    throw new Exception("Source fists clip missing: " + clipName);
            if (!AssetDatabase.IsValidFolder("Assets/CODEHELPER/EquipmentAssets"))
                AssetDatabase.CreateFolder("Assets/CODEHELPER", "EquipmentAssets");
            string working = AssetDatabase.GenerateUniqueAssetPath(Folder + "_Build");
            string created = AssetDatabase.CreateFolder("Assets/CODEHELPER", System.IO.Path.GetFileName(working));
            if (string.IsNullOrEmpty(created)) throw new Exception("Cannot create owned staging folder: " + working);
            var promoted = new List<(string source, string destination)>();
            try
            {
            AssetDatabase.CreateFolder(working, "HumanoidFists");
            AnimationClip idle = ConvertFists(working, "Idle");
            AnimationClip equip = ConvertFists(working, "Equip");
            AnimationClip unequip = ConvertFists(working, "UnEquip");
            AnimationClip punch = ConvertFists(working, "Punch");
            var mask = new AvatarMask { name = "EquipmentArms" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                    i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.RightArm ||
                    i == (int)AvatarMaskBodyPart.LeftFingers || i == (int)AvatarMaskBodyPart.RightFingers);
            AssetDatabase.CreateAsset(mask, working + "/EquipmentArms.mask");

            FirearmDefinition ak = EquipmentTemplates.CreateFirearm("AK12", FirearmMechanism.SelfLoading, 30, 600f);
            ak.fireMode = FireMode.Automatic;
            ak.weaponPoses = poses;
            ak.characterPose = riflePose;
            ak.characterMask = mask;
            ak.equip.weaponPosition.y = AnimationCurve.EaseInOut(0f, -0.4f, 1f, 0f);
            ak.equip.weaponRotation.x = AnimationCurve.EaseInOut(0f, 25f, 1f, 0f);
            ak.unequip.weaponPosition.y = AnimationCurve.EaseInOut(0f, 0f, 1f, -0.4f);
            ak.unequip.weaponRotation.x = AnimationCurve.EaseInOut(0f, 0f, 1f, 25f);
            ak.fire.duration = 0.1f;
            ak.fire.weaponPosition.z = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, -0.012f), new Keyframe(1f, 0f));
            Save(ak, working, "AK12_Definition.asset");

            MeleeDefinition fists = EquipmentTemplates.CreateMelee(true);
            fists.characterMask = mask;
            fists.characterPose = idle;
            SetClip(fists.equip, equip, mask);
            SetClip(fists.unequip, unequip, mask);
            fists.equip.blendIn = 0f;
            fists.unequip.blendOut = 0f;
            // One real punch clip exists here. Do not pretend it is a distinct left/right pair.
            UnityEngine.Object.DestroyImmediate(fists.attacks[1]);
            fists.attacks = new[] { fists.attacks[0] };
            fists.attacks[0].name = "Punch";
            SetClip(fists.attacks[0], punch, mask);
            fists.attacks[0].commitTime = 0.4f;
            fists.attacks[0].blendIn = 0.08f;
            fists.attacks[0].blendOut = 0.15f;
            Save(fists, working, "Fists_Definition.asset");
            AssetDatabase.SaveAssets();
            Promote(working + "/HumanoidFists", EquipmentAssetPaths.FistsAnimations, promoted);
            Promote(working + "/EquipmentArms.mask", EquipmentAssetPaths.ArmsMask, promoted);
            Promote(working + "/AK12_Definition.asset", EquipmentAssetPaths.Ak12Definition, promoted);
            Promote(working + "/Fists_Definition.asset", EquipmentAssetPaths.FistsDefinition, promoted);
            AssetDatabase.DeleteAsset(working); // Empty staging folder, never an authored asset folder.
            Debug.Log($"[SampleEquipmentSetup] Created assets. Fists equip={fists.equip.duration:F3}s, " +
                $"unequip={fists.unequip.duration:F3}s, punch={fists.attacks[0].duration:F3}s. No scene changed.");
            }
            catch
            {
                for (int i = promoted.Count - 1; i >= 0; i--)
                {
                    string error = AssetDatabase.MoveAsset(promoted[i].destination, promoted[i].source);
                    if (!string.IsNullOrEmpty(error)) throw new Exception("Could not return staged asset safely: " + error);
                }
                // Exact folder generated/owned by this invocation; never delete a pre-existing user asset.
                AssetDatabase.DeleteAsset(working);
                throw;
            }
        }

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal)))
                throw new ArgumentException("Expected a Unity Assets folder.", nameof(folder));
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder))))
                throw new Exception("Cannot create asset folder: " + folder);
        }

        private static void Promote(string source, string destination, List<(string source, string destination)> promoted)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null || AssetDatabase.IsValidFolder(destination))
                throw new InvalidOperationException("Refusing overwrite: " + destination);
            EnsureFolder(System.IO.Path.GetDirectoryName(destination).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            promoted.Add((source, destination));
        }

        private static AnimationClip ConvertFists(string working, string name)
        {
            string path = working + "/HumanoidFists/Fists_" + name + ".fbx";
            if (!AssetDatabase.CopyAsset("Assets/kindemo/Demo 1/Prefabs/Fists/Fists_" + name + ".fbx", path))
                throw new Exception("Unable to copy fists clip: " + name);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            foreach (ModelImporterClipAnimation clip in clips) clip.loopTime = name == "Idle";
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid) throw new Exception("Invalid converted fists avatar: " + name);
            return Clip(path, true);
        }

        private static AnimationClip Clip(string path, bool required)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) path = "Assets/" + path;
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (required && (clip == null || !clip.isHumanMotion)) throw new Exception("Compatible Humanoid clip missing: " + path);
            return clip;
        }

        private static void SetClip(EquipmentActionDefinition action, AnimationClip clip, AvatarMask mask)
        {
            action.characterClip = clip;
            action.characterMask = mask;
            action.duration = clip.length;
        }

        private static void Save(EquipmentDefinition item, string folder, string fileName)
        {
            AssetDatabase.CreateAsset(item, folder + "/" + fileName);
            foreach (EquipmentActionDefinition action in EquipmentTemplates.Actions(item))
                if (action != null) AssetDatabase.AddObjectToAsset(action, item);
            EditorUtility.SetDirty(item);
        }
    }
}
#endif
