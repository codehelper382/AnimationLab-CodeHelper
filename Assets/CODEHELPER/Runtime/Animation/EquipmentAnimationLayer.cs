using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    /// <summary>Clip channels inside the spine-owned graph, BEFORE procedural look. No second output/graph.</summary>
    internal sealed class EquipmentAnimationLayer
    {
        private readonly PlayableGraph _graph;
        private readonly AnimationLayerMixerPlayable _mixer;
        private AnimationClipPlayable _pose, _action;
        private AnimationClip _poseClip, _actionClip;
        private AvatarMask _poseMask, _actionMask;
        private readonly AvatarMask _fallback;
        private struct OwnedMask
        {
            public AvatarMask source, mask;
            public bool left, right;
        }
        private readonly List<OwnedMask> _ownedMasks = new List<OwnedMask>();
        public Playable Output => _mixer;

        public EquipmentAnimationLayer(PlayableGraph graph, Playable source)
        {
            _graph = graph;
            _mixer = AnimationLayerMixerPlayable.Create(graph, 3);
            graph.Connect(source, 0, _mixer, 0);
            _mixer.SetInputWeight(0, 1f);
            _fallback = new AvatarMask { name = "Equipment arms only (runtime)", hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                _fallback.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                    i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.RightArm ||
                    i == (int)AvatarMaskBodyPart.LeftFingers || i == (int)AvatarMaskBodyPart.RightFingers);
        }

        public void Set(AnimationClip pose, AvatarMask poseMask, double poseTime,
            AnimationClip action, AvatarMask actionMask, double actionTime, float actionWeight,
            bool leftArmOwnedByIK = false, bool rightArmOwnedByIK = false)
        {
            poseMask = ResolveMask(poseMask, leftArmOwnedByIK, rightArmOwnedByIK);
            actionMask = ResolveMask(actionMask, leftArmOwnedByIK, rightArmOwnedByIK);
            SetClip(ref _pose, ref _poseClip, ref _poseMask, 1, pose, poseMask, poseTime);
            SetClip(ref _action, ref _actionClip, ref _actionMask, 2, action, actionMask, actionTime);
            _mixer.SetInputWeight(1, pose != null ? 1f : 0f);
            _mixer.SetInputWeight(2, action != null ? Mathf.Clamp01(actionWeight) : 0f);
        }

        private AvatarMask ResolveMask(AvatarMask source, bool left, bool right)
        {
            if (!left && !right) return source;
            source = source != null ? source : _fallback;
            foreach (OwnedMask entry in _ownedMasks)
                if (entry.source == source && entry.left == left && entry.right == right) return entry.mask;
            AvatarMask mask = Object.Instantiate(source);
            mask.name = "Equipment IK ownership (runtime)";
            mask.hideFlags = HideFlags.HideAndDontSave;
            if (left) mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
            if (right) mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, false);
            _ownedMasks.Add(new OwnedMask { source = source, mask = mask, left = left, right = right });
            return mask;
        }

        private void SetClip(ref AnimationClipPlayable playable, ref AnimationClip previous, ref AvatarMask previousMask,
            int input, AnimationClip clip, AvatarMask mask, double time)
        {
            if (previous != clip)
            {
                if (playable.IsValid())
                {
                    _graph.Disconnect(_mixer, input);
                    _graph.DestroyPlayable(playable);
                }
                playable = default;
                previous = clip;
                previousMask = null;
                if (clip != null)
                {
                    playable = AnimationClipPlayable.Create(_graph, clip);
                    playable.SetApplyFootIK(false);
                    playable.SetApplyPlayableIK(false);
                    playable.SetSpeed(0);
                    _graph.Connect(playable, 0, _mixer, input);
                    // These inputs are created AFTER graph.Play(); explicitly start their evaluation.
                    // Speed zero keeps their time owned by the equipment clock, not the graph.
                    playable.Play();
                }
            }
            if (clip == null) return;
            AvatarMask effective = mask != null ? mask : _fallback;
            if (previousMask != effective)
            {
                _mixer.SetLayerMaskFromAvatarMask((uint)input, effective);
                previousMask = effective;
            }
            playable.SetTime(time);
        }

        public void Dispose()
        {
            foreach (OwnedMask entry in _ownedMasks)
                if (Application.isPlaying) Object.Destroy(entry.mask);
                else Object.DestroyImmediate(entry.mask);
            _ownedMasks.Clear();
            if (Application.isPlaying) Object.Destroy(_fallback);
            else Object.DestroyImmediate(_fallback);
        }
    }
}
