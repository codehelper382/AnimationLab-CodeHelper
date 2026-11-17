using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>One weapon-relative motion target plus a grip blend; never parents anything below an arm.</summary>
    internal sealed class EquipmentLeftHandTarget
    {
        private Transform _motion, _blend;
        public Transform Target => _blend;

        public bool Prepare(Transform weaponRoot, Transform grip, Transform anchor, Transform leftArm, Transform rightArm)
        {
            if (weaponRoot == null || grip == null || anchor == null || anchor == weaponRoot || !anchor.IsChildOf(weaponRoot))
                return false;
            if (UnderArm(weaponRoot, leftArm) || UnderArm(weaponRoot, rightArm) ||
                UnderArm(anchor, leftArm) || UnderArm(anchor, rightArm) || UnderArm(grip, leftArm) || UnderArm(grip, rightArm))
                return false;
            if (_motion == null) _motion = Create("Left reload motion (runtime)", anchor);
            if (_blend == null) _blend = Create("Left reload IK (runtime)", weaponRoot);
            _motion.SetParent(anchor, false);
            _blend.SetParent(weaponRoot, false);
            _blend.SetPositionAndRotation(grip.position, grip.rotation);
            return true;
        }

        private static bool UnderArm(Transform candidate, Transform arm) => arm != null && candidate.IsChildOf(arm);

        public void Evaluate(Transform grip, AnimationClip clip, float progress, float gripWeight)
        {
            if (_motion == null || _blend == null || grip == null) return;
            _motion.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _motion.localScale = Vector3.one;
            if (clip != null && !clip.isHumanMotion)
                clip.SampleAnimation(_motion.gameObject, EquipmentNumbers.Progress(progress) * clip.length);
            _motion.localScale = Vector3.one;
            float blend = EquipmentNumbers.Progress(gripWeight);
            _blend.SetPositionAndRotation(Vector3.Lerp(_motion.position, grip.position, blend),
                Quaternion.Slerp(_motion.rotation, grip.rotation, blend));
        }

        private static Transform Create(string name, Transform parent)
        {
            var item = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        public void Dispose()
        {
            Destroy(_motion); Destroy(_blend);
            _motion = _blend = null;
        }

        private static void Destroy(Transform value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value.gameObject);
            else Object.DestroyImmediate(value.gameObject);
        }
    }
}
