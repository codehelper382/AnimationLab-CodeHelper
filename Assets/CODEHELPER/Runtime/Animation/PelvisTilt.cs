using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    /// <summary>
    /// Inclina la pelvis según el pitch y compensa los muslos (experimental, no fija los pies).
    /// No tiene Update propio: lo dirige ProceduralSpine para respetar el orden
    /// (restaurar → estabilizar spine → inclinar pelvis → aplicar look).
    /// Añádelo al mismo GameObject que ProceduralSpine y asigna los 3 huesos.
    /// </summary>
    [AddComponentMenu("CodeHelper/Shooter Framework/Animation/Pelvis Tilt (experimental)")]
    [DisallowMultipleComponent]
    public class PelvisTilt : MonoBehaviour
    {
        public Transform pelvisBone;
        public Transform leftThigh;
        public Transform rightThigh;
        [Range(0f, 30f)] public float maxAngle = 7f;

        // Si la pelvis ya no coincide con lo que escribimos, otro sistema (Animator) la sobrescribió.
        private const float ExternalWriteThreshold = 0.05f;

        private Quaternion _before;
        private Quaternion _after;
        private Quaternion _leftBefore, _rightBefore, _leftAfter, _rightAfter;
        private Transform _appliedPelvis, _appliedLeft, _appliedRight;
        private Transform _validatedPelvis, _validatedLeft, _validatedRight;
        private bool _applied;
        private bool _valid;

        void OnEnable() { ValidateSetup(); }
        void OnDisable() { Restore(); }
        void OnValidate() { ValidateSetup(); }

        /// <summary>Revalidates only when bone references change (also safe for runtime setup).</summary>
        public bool IsConfigured
        {
            get
            {
                if (_validatedPelvis != pelvisBone || _validatedLeft != leftThigh || _validatedRight != rightThigh)
                    ValidateSetup();
                return _valid && pelvisBone != null && leftThigh != null && rightThigh != null;
            }
        }

        // La jerarquía se valida una vez, no cada frame.
        private void ValidateSetup()
        {
            _validatedPelvis = pelvisBone;
            _validatedLeft = leftThigh;
            _validatedRight = rightThigh;
            _valid = pelvisBone != null && leftThigh != null && rightThigh != null &&
                     leftThigh != rightThigh && leftThigh != pelvisBone && rightThigh != pelvisBone &&
                     leftThigh.IsChildOf(pelvisBone) && rightThigh.IsChildOf(pelvisBone) &&
                     !leftThigh.IsChildOf(rightThigh) && !rightThigh.IsChildOf(leftThigh);
        }

        /// <param name="pitch01">Pitch normalizado (-1 a 1).</param>
        public void Apply(Transform root, float pitch01, float alpha)
        {
            Restore(); // Public API is safe even if called twice without a separate Restore.
            if (!IsConfigured || root == null || pelvisBone == root || alpha <= 0.001f) return;

            float angle = Mathf.Clamp(pitch01, -1f, 1f) * Mathf.Clamp(maxAngle, 0f, 30f) * Mathf.Clamp01(alpha);
            if (Mathf.Abs(angle) < 0.001f) return;

            Quaternion left = leftThigh.rotation;
            Quaternion right = rightThigh.rotation;

            _before = pelvisBone.localRotation;
            _leftBefore = leftThigh.localRotation;
            _rightBefore = rightThigh.localRotation;
            pelvisBone.rotation = Quaternion.AngleAxis(angle, root.right) * pelvisBone.rotation;
            leftThigh.rotation = left;
            rightThigh.rotation = right;
            _after = pelvisBone.localRotation;
            _leftAfter = leftThigh.localRotation;
            _rightAfter = rightThigh.localRotation;
            _appliedPelvis = pelvisBone;
            _appliedLeft = leftThigh;
            _appliedRight = rightThigh;
            _applied = true;
        }

        /// <summary>Deshace la inclinación anterior si nadie más tocó la pelvis.</summary>
        public void Restore()
        {
            if (!_applied) return;
            _applied = false;

            // Undo our LOCAL compensation too. Preserving the compensated world rotation here
            // leaves a residual thigh offset when the Animator is paused/disabled.
            // Each bone is checked independently: a pelvis-only external write does not
            // imply that the Animator also replaced our thigh compensation.
            if (_appliedPelvis != null && Quaternion.Angle(_appliedPelvis.localRotation, _after) <= ExternalWriteThreshold)
                _appliedPelvis.localRotation = _before;
            if (_appliedLeft != null && Quaternion.Angle(_appliedLeft.localRotation, _leftAfter) <= ExternalWriteThreshold)
                _appliedLeft.localRotation = _leftBefore;
            if (_appliedRight != null && Quaternion.Angle(_appliedRight.localRotation, _rightAfter) <= ExternalWriteThreshold)
                _appliedRight.localRotation = _rightBefore;
        }
    }
}
