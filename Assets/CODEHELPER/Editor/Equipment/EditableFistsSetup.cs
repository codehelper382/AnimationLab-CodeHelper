#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>One-time authoring in an isolated project: calibrate from the existing guard, then create editable scene points.</summary>
    public static class EditableFistsSetup
    {
        public static void Configure()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run only in an isolated validation project.");
            var scene = EditorSceneManager.OpenScene(EquipmentAssetPaths.Scene);
            var equipment = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EquipmentSceneController>(true)).Single();
            var slot = equipment.slots[1];
            if (slot.leftHandPose?.guard != null || slot.rightHandPose?.guard != null)
                throw new InvalidOperationException("Fists already have editable targets; refusing overwrite.");
            Transform[] transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var positions = transforms.Select(item => item.localPosition).ToArray();
            var rotations = transforms.Select(item => item.localRotation).ToArray();
            var scales = transforms.Select(item => item.localScale).ToArray();
            var active = transforms.Select(item => item.gameObject.activeSelf).ToArray();
            Pose left = default, right = default;
            int initial = equipment.initialSlot;
            try
            {
                Invoke(equipment.spine, "Awake"); Invoke(equipment.handIK, "Awake");
                Invoke(equipment.weaponPose, "Awake"); Invoke(equipment.weaponPose, "OnEnable");
                Invoke(equipment.look, "Awake"); equipment.look.ConnectCamera();
                equipment.spine.animator.Rebind(); equipment.spine.animator.Update(0f);
                if (!(bool)Invoke(equipment.spine, "ResolveRuntimeBones")) throw new Exception("Missing Humanoid.");
                Invoke(equipment.spine, "CaptureBindPose");
                Set(equipment.spine, "_activeMode", SpineDriveMode.LateUpdate);
                if (!(bool)Invoke(equipment.spine, "TryBuildEquipmentGraph")) throw new Exception("Missing graph.");
                Set(equipment.spine, "_ready", true);
                var graph = (PlayableGraph)Get(equipment.spine, "_graph");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                equipment.initialSlot = 1;
                Invoke(equipment, "OnEnable"); equipment.Step(10f);
                equipment.look.SetLookAngles(0f, equipment.spine.animationRoot.eulerAngles.y);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Invoke(equipment.spine, "Update"); graph.Evaluate(0f); Invoke(equipment.spine, "LateUpdate");
                Invoke(equipment.look.cameraRig, "ApplyCamera", 0f); equipment.ApplyMotion();
                Transform view = equipment.look.cameraRig.cameraHolder != null ? equipment.look.cameraRig.cameraHolder : equipment.look.cameraRig.transform;
                left = LocalPose(view, equipment.handIK.leftHand.hand);
                right = LocalPose(view, equipment.handIK.rightHand.hand);
            }
            finally
            {
                Invoke(equipment, "OnDisable"); Invoke(equipment.spine, "TearDown");
                equipment.initialSlot = initial;
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
                    transforms[i].localScale = scales[i]; transforms[i].gameObject.SetActive(active[i]);
                }
            }
            EquipmentHandTargetsEditor.CreateMissing(equipment, 1);
            SetLocal(slot.leftHandPose, left); SetLocal(slot.rightHandPose, right);
            var definition = (MeleeDefinition)slot.definition;
            // One existing right punch. Other actions/clip/timing and the AK12 definition remain unchanged.
            var attack = definition.attacks.Single();
            attack.rightHandMotion = new EquipmentHandMotion
            {
                enabled = true,
                reach = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(attack.commitTime, 1f), new Keyframe(1f, 0f))
            };
            EditorUtility.SetDirty(attack);
            AssetDatabase.SaveAssetIfDirty(definition);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[EditableFistsSetup] Guard calibrated from original fists: left={left.position:F4}, right={right.position:F4}; " +
                "straight right punch endpoint +0.16m in view Z; existing scene transforms restored before save.");
        }

        private static Pose LocalPose(Transform root, Transform wrist) => new Pose(root.InverseTransformPoint(wrist.position),
            Quaternion.Inverse(root.rotation) * wrist.rotation);
        private static void SetLocal(EquipmentHandBinding binding, Pose pose)
        {
            binding.guard.SetLocalPositionAndRotation(pose.position, pose.rotation);
            binding.actionTarget.SetLocalPositionAndRotation(pose.position + Vector3.forward * 0.16f, pose.rotation);
        }
        private static object Invoke(object value, string name, params object[] args) => value.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
        private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
    }
}
#endif
