using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>One reusable guard/action target, separate from weapon grips and the character hierarchy.</summary>
    internal sealed class EquipmentHandTarget
    {
        private Transform _motion, _target;
        private EquipmentHandBinding _binding;
        public Transform Target => _target;

        public bool Prepare(EquipmentHandBinding binding, Transform leftArm, Transform rightArm)
        {
            _binding = null;
            if (binding == null || !binding.enabled || binding.guard == null ||
                Unsafe(binding.guard, leftArm, rightArm) || Unsafe(binding.actionTarget, leftArm, rightArm) ||
                Unsafe(binding.elbowHint, leftArm, rightArm)) return false;
            if (_motion == null) _motion = Create("Hand motion (runtime)", binding.guard);
            if (_target == null) _target = Create("Hand pose IK (runtime)", binding.guard);
            _motion.SetParent(binding.guard, false);
            _target.SetParent(binding.guard, false);
            _target.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _binding = binding;
            return true;
        }

        private static bool Unsafe(Transform target, Transform left, Transform right) => target != null &&
            ((left != null && target.IsChildOf(left)) || (right != null && target.IsChildOf(right)));

        public void Evaluate(EquipmentHandMotion motion, float progress, float actionWeight)
        {
            if (_binding?.guard == null || _target == null || _motion == null) return;
            Transform guard = _binding.guard;
            float blend = motion != null && motion.enabled ? EquipmentNumbers.Progress(actionWeight) : 0f;
            float reach = blend > 0f && _binding.actionTarget != null
                ? EquipmentNumbers.Progress(EquipmentNumbers.Curve(motion.reach, progress, 0f)) * blend : 0f;
            _motion.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _motion.localScale = Vector3.one;
            if (blend > 0f && motion.motionClip != null && !motion.motionClip.isHumanMotion)
                motion.motionClip.SampleAnimation(_motion.gameObject, EquipmentNumbers.Progress(progress) * motion.motionClip.length);
            _motion.localScale = Vector3.one;
            Vector3 offset = blend > 0f ? (motion.position?.Evaluate(progress) ?? Vector3.zero) + _motion.localPosition : Vector3.zero;
            Quaternion rotation = blend > 0f
                ? Quaternion.Euler(motion.rotation?.Evaluate(progress) ?? Vector3.zero) * _motion.localRotation : Quaternion.identity;
            Transform end = _binding.actionTarget != null ? _binding.actionTarget : guard;
            Quaternion guardDelta = guard.rotation * Quaternion.Slerp(Quaternion.identity, rotation, blend) * Quaternion.Inverse(guard.rotation);
            _target.SetPositionAndRotation(Vector3.Lerp(guard.position, end.position, reach) + guard.TransformVector(offset * blend),
                guardDelta * Quaternion.Slerp(guard.rotation, end.rotation, reach));
        }

        private static Transform Create(string name, Transform parent)
        {
            var item = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        public void Dispose()
        {
            Destroy(_motion); Destroy(_target);
            _motion = _target = null; _binding = null;
        }

        private static void Destroy(Transform value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value.gameObject);
            else Object.DestroyImmediate(value.gameObject);
        }
    }
}
