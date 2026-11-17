#if UNITY_EDITOR
using System;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Creates one editable example target clip from the existing left-arm reload at neutral look.</summary>
    public static class WeaponRelativeReloadSetup
    {
        public const string ClipPath = EquipmentAssetPaths.Ak12ReloadMotion;

        public static void Configure()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ClipPath) != null) throw new Exception("Target clip already exists; refusing overwrite.");
            var definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(EquipmentAssetPaths.Ak12Definition);
            if (definition == null || definition.reload?.characterClip == null) throw new Exception("Configured reload missing.");
            var scene = EditorSceneManager.OpenPreviewScene(EquipmentAssetPaths.Scene);
            PlayableGraph graph = default;
            AnimationClip motion = null;
            AvatarMask extractionMask = null;
            try
            {
                EquipmentSceneController binding = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EquipmentSceneController>(true)).Single();
                Transform anchor = binding.slots[0].leftReloadTarget;
                Animator animator = binding.spine.animator;
                if (anchor == null || animator == null || !animator.isHuman) throw new Exception("Reload anchor/Humanoid missing.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                graph = PlayableGraph.Create("Reload target bake - preview only");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var controller = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
                controller.SetSpeed(0);
                var pose = AnimationClipPlayable.Create(graph, definition.characterPose);
                pose.SetSpeed(0); pose.SetApplyFootIK(false);
                var action = AnimationClipPlayable.Create(graph, definition.reload.characterClip);
                action.SetSpeed(0); action.SetApplyFootIK(false);
                var mixer = AnimationLayerMixerPlayable.Create(graph, 3);
                graph.Connect(controller, 0, mixer, 0);
                graph.Connect(pose, 0, mixer, 1);
                graph.Connect(action, 0, mixer, 2);
                for (int i = 0; i < 3; i++) mixer.SetInputWeight(i, 1f);
                mixer.SetLayerMaskFromAvatarMask(1, definition.characterMask);
                // Extract the original arm trajectory in preview, even though runtime playback is fingers-only.
                extractionMask = new AvatarMask { hideFlags = HideFlags.HideAndDontSave };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    extractionMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                        i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.LeftFingers);
                mixer.SetLayerMaskFromAvatarMask(2, extractionMask);
                var output = AnimationPlayableOutput.Create(graph, "Preview only", animator);
                output.SetSourcePlayable(mixer);
                graph.Play();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var curves = Enumerable.Range(0, 7).Select(_ => new AnimationCurve()).ToArray();
                float length = definition.reload.characterClip.length;
                int frames = Mathf.CeilToInt(length * 30f);
                Vector3 start = Vector3.zero;
                Quaternion startRotation = Quaternion.identity;
                for (int frame = 0; frame <= frames; frame++)
                {
                    float time = frame == frames ? length : frame / 30f;
                    action.SetTime(time);
                    graph.Evaluate(0f);
                    Vector3 local = anchor.InverseTransformPoint(hand.position);
                    Quaternion rotation = Quaternion.Inverse(anchor.rotation) * hand.rotation;
                    if (frame == 0) { start = local; startRotation = rotation; }
                    local -= start;
                    rotation *= Quaternion.Inverse(startRotation);
                    float[] values = { local.x, local.y, local.z, rotation.x, rotation.y, rotation.z, rotation.w };
                    for (int i = 0; i < 7; i++) curves[i].AddKey(time, values[i]);
                }
                string[] names = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                motion = new AnimationClip { name = "AK12_LeftReloadTarget", frameRate = 30f };
                AnimationUtility.SetEditorCurves(motion, names.Select(name => EditorCurveBinding.FloatCurve("", typeof(Transform), name)).ToArray(), curves);
                motion.EnsureQuaternionContinuity();
                SampleEquipmentSetup.EnsureFolder(EquipmentAssetPaths.Ak12 + "/Animations");
                AssetDatabase.CreateAsset(motion, ClipPath);
                definition.reload.leftHandTargetMotion = motion;
                definition.reloadEmpty.leftHandTargetMotion = motion;
                EditorUtility.SetDirty(definition.reload);
                EditorUtility.SetDirty(definition.reloadEmpty);
                AssetDatabase.SaveAssetIfDirty(definition);
                Debug.Log("[WeaponRelativeReloadSetup] Assigned editable weapon-local left target motion. Character clip retained for fingers; scene was not saved by this utility.");
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (extractionMask != null) UnityEngine.Object.DestroyImmediate(extractionMask);
                if (motion != null && !EditorUtility.IsPersistent(motion)) UnityEngine.Object.DestroyImmediate(motion);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
