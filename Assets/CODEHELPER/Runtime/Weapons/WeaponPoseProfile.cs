using System;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Weapons
{
    public enum WeaponPoseState { Idle, Aim }

    [Serializable]
    public class WeaponAimMotionSettings
    {
        [Tooltip("Optional weapon-only bounce. Does not move the camera or alter captured poses.")]
        public bool enabled;
        [Range(0.5f, 20f)] public float frequency = 4.5f;
        [Range(0.05f, 1f)] public float damping = 0.6f;
        [Tooltip("Kick scale in metres, in the pose root parent's local axes. Not a permanent pose offset.")]
        public Vector3 aimPositionKick = new Vector3(0f, -0.006f, -0.012f);
        public Vector3 idlePositionKick = new Vector3(0f, 0.003f, 0.006f);
        [Tooltip("Kick scale in degrees, around the weapon's local axes.")]
        public Vector3 aimRotationKick = new Vector3(-1.5f, 0f, 0.4f);
        public Vector3 idleRotationKick = new Vector3(0.8f, 0f, -0.25f);
        [Range(0f, 0.1f)] public float maxPositionOffset = 0.025f;
        [Range(0f, 15f)] public float maxRotationOffset = 4f;
    }

    [Serializable]
    public class WeaponAimCameraSettings
    {
        [Tooltip("Optional FOV and angular-only camera effects. Does not move the camera anchor or look input.")]
        public bool enabled;
        public bool applyInThirdPerson;
        [Tooltip("Added to the camera's base FOV. Positive widens the view; negative zooms in.")]
        [Range(-30f, 30f)] public float aimFovOffset = 3f;
        [Min(0f)] public float fovBlendSpeed = 12f;
        public bool shakeEnabled = true;
        [Range(0.5f, 20f)] public float frequency = 8f;
        [Range(0.05f, 1f)] public float damping = 0.65f;
        [Tooltip("Transient angular impulse scale in degrees, around the camera's local axes.")]
        public Vector3 aimRotationKick = new Vector3(-0.25f, 0.08f, 0.04f);
        public Vector3 idleRotationKick = new Vector3(0.12f, -0.04f, -0.02f);
        [Range(0f, 2f)] public float maxRotationOffset = 0.35f;
    }

    [Serializable]
    public struct WeaponPose
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Quaternion Rotation => Quaternion.Euler(localEulerAngles);

        public bool IsFinite => Finite(localPosition) && Finite(localEulerAngles);

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        public static WeaponPose FromLocalTransform(Transform root) => new WeaponPose
        {
            localPosition = root.localPosition,
            localEulerAngles = root.localRotation.eulerAngles
        };
    }

    /// <summary>Only local pose data. No scene references, Animator or third-party dependencies.</summary>
    [CreateAssetMenu(menuName = "CodeHelper/Weapons/Pose Profile", fileName = "WeaponPoses")]
    public class WeaponPoseProfile : ScriptableObject
    {
        [Min(0f)] public float transitionDuration = 0.18f;
        [Tooltip("Use one pose clock for weapon bounce, camera bounce and FOV; all finish with the pose. Off keeps independent springs/FOV speed.")]
        public bool synchronizeAimEffects;
        [Tooltip("Number of timed oscillations inside a transition. Independent Frequency settings are ignored while synchronized.")]
        [Range(1f, 3f)] public float transitionOscillations = 1f;
        [SerializeField] private WeaponPose idle;
        [SerializeField] private WeaponPose aim;
        [SerializeField, HideInInspector] private bool idleCaptured;
        [SerializeField, HideInInspector] private bool aimCaptured;
        public WeaponAimMotionSettings aimMotion = new WeaponAimMotionSettings();
        public WeaponAimCameraSettings aimCamera = new WeaponAimCameraSettings();

        public bool HasIdle => idleCaptured && idle.IsFinite;
        public bool HasAim => aimCaptured && aim.IsFinite;

        public bool TryGetPose(WeaponPoseState state, out WeaponPose pose)
        {
            pose = state == WeaponPoseState.Aim ? aim : idle;
            return state == WeaponPoseState.Aim ? HasAim : state == WeaponPoseState.Idle && HasIdle;
        }

        public bool StorePose(WeaponPoseState state, WeaponPose pose)
        {
            if (!pose.IsFinite) return false;
            if (state == WeaponPoseState.Idle) { idle = pose; idleCaptured = true; }
            else if (state == WeaponPoseState.Aim) { aim = pose; aimCaptured = true; }
            else return false;
            return true;
        }
    }
}
