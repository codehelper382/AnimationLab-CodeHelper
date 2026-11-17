using UnityEngine;

namespace HandGripToolkit
{
    /// <summary>
    /// Static mapping between (HandSide, FingerId, joint index) and the
    /// HumanBodyBones values Unity exposes for humanoid finger bones.
    ///
    /// Unity version note (verified against this project's editor, 6000.3.14f1):
    /// finger bones are named e.g. HumanBodyBones.LeftThumbProximal /
    /// LeftThumbIntermediate / LeftThumbDistal. The legacy "Thumb1/2/3"
    /// spellings no longer exist in Unity 6000.3 and will not compile.
    ///
    /// Joint index convention: 0 = Proximal (base), 1 = Intermediate, 2 = Distal (tip).
    /// </summary>
    public static class HandBoneMap
    {
        public const int FingersPerHand = 5;
        public const int JointsPerFinger = 3;
        public const int BonesPerHand = FingersPerHand * JointsPerFinger; // 15

        // Joint indices, exposed for readability at call sites.
        public const int Proximal = 0;
        public const int Intermediate = 1;
        public const int Distal = 2;

        // [finger, joint] lookup tables for both hands.
        private static readonly HumanBodyBones[,] LeftBones =
        {
            { HumanBodyBones.LeftThumbProximal,  HumanBodyBones.LeftThumbIntermediate,  HumanBodyBones.LeftThumbDistal },
            { HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal },
            { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            { HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal },
            { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal }
        };

        private static readonly HumanBodyBones[,] RightBones =
        {
            { HumanBodyBones.RightThumbProximal,  HumanBodyBones.RightThumbIntermediate,  HumanBodyBones.RightThumbDistal },
            { HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal },
            { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            { HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal },
            { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal }
        };

        /// <summary>Returns the humanoid bone for one finger joint of a hand.</summary>
        public static HumanBodyBones GetBone(HandSide side, FingerId finger, int joint)
        {
            if (joint < Proximal || joint > Distal)
                throw new System.ArgumentOutOfRangeException(nameof(joint),
                    "Joint index must be 0 (Proximal), 1 (Intermediate) or 2 (Distal).");

            return side == HandSide.Left
                ? LeftBones[(int)finger, joint]
                : RightBones[(int)finger, joint];
        }

        /// <summary>
        /// Reverse lookup: which side/finger/joint a finger bone refers to.
        /// Returns false for bones that are not finger phalanges.
        /// </summary>
        public static bool TryGetFingerJoint(HumanBodyBones bone, out HandSide side, out FingerId finger, out int joint)
        {
            for (int s = 0; s < 2; s++)
            {
                HandSide hand = (HandSide)s;
                for (int f = 0; f < FingersPerHand; f++)
                {
                    for (int j = Proximal; j <= Distal; j++)
                    {
                        if (GetBone(hand, (FingerId)f, j) == bone)
                        {
                            side = hand;
                            finger = (FingerId)f;
                            joint = j;
                            return true;
                        }
                    }
                }
            }

            side = HandSide.Left;
            finger = FingerId.Thumb;
            joint = Proximal;
            return false;
        }

        /// <summary>Returns the opposite hand side.</summary>
        public static HandSide GetOppositeSide(HandSide side)
        {
            return side == HandSide.Left ? HandSide.Right : HandSide.Left;
        }

        /// <summary>
        /// Returns the equivalent bone on the other hand (finger phalanges and
        /// the hand bones). Non-hand bones are returned unchanged.
        /// </summary>
        public static HumanBodyBones GetMirroredBone(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.LeftHand:  return HumanBodyBones.RightHand;
                case HumanBodyBones.RightHand: return HumanBodyBones.LeftHand;
            }

            if (TryGetFingerJoint(bone, out HandSide side, out FingerId finger, out int joint))
                return GetBone(GetOppositeSide(side), finger, joint);

            return bone;
        }
    }
}
