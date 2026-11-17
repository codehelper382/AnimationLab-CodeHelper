using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Weapons
{
    public static class AimTransitionMath
    {
        public static float Blend(float progress)
        {
            float t = WeaponAimSpring.Safe(progress, 0f, 1f, 0f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Finite-duration damped oscillation, continuous on reversal and exactly zero at t=1.
        /// Unlike the independent physical spring, its duration is the shared pose transition.</summary>
        public static Vector3 Motion(Vector3 start, Vector3 kick, float progress, float oscillations, float damping, float limit)
        {
            float t = WeaponAimSpring.Safe(progress, 0f, 1f, 0f);
            limit = WeaponAimSpring.Safe(limit, 0f, 15f, 0f);
            if (t >= 1f || limit <= 0f) return Vector3.zero;
            if (!(new WeaponPose { localPosition = start }).IsFinite) start = Vector3.zero;
            if (!(new WeaponPose { localPosition = kick }).IsFinite) kick = Vector3.zero;
            float cycles = WeaponAimSpring.Safe(oscillations, 1f, 3f, 1f);
            float drag = WeaponAimSpring.Safe(damping, 0.05f, 1f, 0.6f);
            float envelope = Mathf.Sin(Mathf.PI * t);
            // Squared envelope also reaches zero velocity at the deadline for fractional cycles.
            float wave = Mathf.Sin(2f * Mathf.PI * cycles * t) * envelope * envelope * Mathf.Exp(-3f * drag * t);
            return Vector3.ClampMagnitude(start * (1f - Blend(t)) + Vector3.ClampMagnitude(kick, limit) * wave, limit);
        }
    }
}
