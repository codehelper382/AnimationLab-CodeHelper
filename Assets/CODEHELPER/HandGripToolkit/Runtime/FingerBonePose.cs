using System;
using UnityEngine;

namespace HandGripToolkit
{
    /// <summary>
    /// One serialized entry of a hand pose: the humanoid finger bone it drives
    /// and the local rotation the bone should have while the pose is active.
    /// </summary>
    [Serializable]
    public class FingerBonePose
    {
        [Tooltip("Humanoid finger bone driven by this entry.")]
        public HumanBodyBones bone;

        [Tooltip("Local rotation of the bone while this pose is active.")]
        public Quaternion localRotation = Quaternion.identity;

        public FingerBonePose()
        {
        }

        public FingerBonePose(HumanBodyBones bone, Quaternion localRotation)
        {
            this.bone = bone;
            this.localRotation = localRotation;
        }
    }
}
