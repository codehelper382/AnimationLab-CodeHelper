using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>Samples transform-only weapon clips on a separate model, never on the character Animator/holder.</summary>
    internal sealed class EquipmentWeaponClip
    {
        private Transform _root;
        private AnimationClip _clip;
        private Transform[] _bones;
        private Vector3[] _positions, _scales, _lastPositions, _lastScales;
        private Quaternion[] _rotations, _lastRotations;
        private Vector3[] _samplePositions, _sampleScales;
        private Quaternion[] _sampleRotations;
        private bool[] _ownsPosition, _ownsRotation, _ownsScale;

        public void Sample(Transform root, AnimationClip clip, float progress, float weight = 1f)
        {
            if (root == null || clip == null || clip.isHumanMotion) { Clear(); return; }
            if (_root != root || _clip != clip)
            {
                Clear();
                _root = root; _clip = clip;
                _bones = root.GetComponentsInChildren<Transform>(true);
                int count = _bones.Length;
                _positions = new Vector3[count]; _scales = new Vector3[count];
                _rotations = new Quaternion[count];
                _lastPositions = new Vector3[count]; _lastScales = new Vector3[count];
                _lastRotations = new Quaternion[count];
                _samplePositions = new Vector3[count]; _sampleScales = new Vector3[count];
                _sampleRotations = new Quaternion[count];
                _ownsPosition = new bool[count]; _ownsRotation = new bool[count]; _ownsScale = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    _positions[i] = _bones[i].localPosition;
                    _rotations[i] = _bones[i].localRotation;
                    _scales[i] = _bones[i].localScale;
                }
            }
            // Preserve the calibrated model root even if a supplied clip has a root curve.
            Vector3 position = root.localPosition, scale = root.localScale;
            Quaternion rotation = root.localRotation;
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                _samplePositions[i] = _bones[i].localPosition;
                _sampleRotations[i] = _bones[i].localRotation;
                _sampleScales[i] = _bones[i].localScale;
            }
            clip.SampleAnimation(root.gameObject, EquipmentNumbers.Progress(progress) * clip.length);
            root.SetLocalPositionAndRotation(position, rotation);
            root.localScale = scale;
            weight = EquipmentNumbers.Progress(weight);
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                _ownsPosition[i] |= Vector3.Distance(_samplePositions[i], _bones[i].localPosition) > 0.0000001f;
                _ownsRotation[i] |= Quaternion.Angle(_sampleRotations[i], _bones[i].localRotation) > 0.001f;
                _ownsScale[i] |= Vector3.Distance(_sampleScales[i], _bones[i].localScale) > 0.0000001f;
                // Blend only animated channels. User adjustment points and owned runtime children
                // below the model are not part of the clip and must never be reset or attenuated.
                if (_bones[i] != root && weight < 1f)
                {
                    if (_ownsPosition[i]) _bones[i].localPosition = Vector3.Lerp(_positions[i], _bones[i].localPosition, weight);
                    if (_ownsRotation[i]) _bones[i].localRotation = Quaternion.Slerp(_rotations[i], _bones[i].localRotation, weight);
                    if (_ownsScale[i]) _bones[i].localScale = Vector3.Lerp(_scales[i], _bones[i].localScale, weight);
                }
                _lastPositions[i] = _bones[i].localPosition;
                _lastRotations[i] = _bones[i].localRotation;
                _lastScales[i] = _bones[i].localScale;
            }
        }

        public void Clear()
        {
            if (_bones != null)
                for (int i = 0; i < _bones.Length; i++)
                {
                    Transform bone = _bones[i];
                    if (bone == null || bone == _root) continue;
                    // Restore only channels still showing our output, not another system's subsequent write.
                    if (_ownsPosition[i] && Vector3.Distance(bone.localPosition, _lastPositions[i]) < 0.00001f) bone.localPosition = _positions[i];
                    if (_ownsRotation[i] && Quaternion.Angle(bone.localRotation, _lastRotations[i]) < 0.01f) bone.localRotation = _rotations[i];
                    if (_ownsScale[i] && Vector3.Distance(bone.localScale, _lastScales[i]) < 0.00001f) bone.localScale = _scales[i];
                }
            _root = null; _clip = null; _bones = null;
        }
    }
}
