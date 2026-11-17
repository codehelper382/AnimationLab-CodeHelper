using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Weapons
{
    /// <summary>Analytical damped spring around zero. Stable without fixed timestep substeps.</summary>
    public struct WeaponAimSpring
    {
        public Vector3 Offset { get; private set; }
        public Vector3 Velocity { get; private set; }

        public void Reset() { Offset = Velocity = Vector3.zero; }

        public void Kick(Vector3 kickScale, float frequency, float limit)
        {
            if (!Finite(kickScale)) return;
            float omega = 2f * Mathf.PI * Safe(frequency, 0.5f, 20f, 4.5f);
            limit = Safe(limit, 0f, 15f, 0f);
            if (limit <= 0f) { Reset(); return; }
            // Bound both the incoming impulse and stored speed even for rapid input reversals.
            Velocity = Vector3.ClampMagnitude(Velocity + Vector3.ClampMagnitude(kickScale, limit) * omega, limit * omega);
        }

        public void Step(float dt, float frequency, float damping, float limit)
        {
            limit = Safe(limit, 0f, 15f, 0f);
            if (limit <= 0f) { Reset(); return; }
            dt = Safe(dt, 0f, 60f, 0f);
            float omega = 2f * Mathf.PI * Safe(frequency, 0.5f, 20f, 4.5f);
            float ratio = Safe(damping, 0.05f, 1f, 0.6f);
            Vector3 position = Offset, velocity = Velocity;
            float decay = Mathf.Exp(-ratio * omega * dt);
            if (ratio >= 0.9999f)
            {
                Vector3 b = velocity + omega * position;
                Offset = decay * (position + b * dt);
                Velocity = decay * (velocity - omega * b * dt);
            }
            else
            {
                float dampedOmega = omega * Mathf.Sqrt(1f - ratio * ratio);
                float sine = Mathf.Sin(dampedOmega * dt), cosine = Mathf.Cos(dampedOmega * dt);
                Offset = decay * (position * cosine + (velocity + ratio * omega * position) * (sine / dampedOmega));
                Velocity = decay * (velocity * cosine - (ratio * omega * velocity + omega * omega * position) * (sine / dampedOmega));
            }
            Offset = Vector3.ClampMagnitude(Offset, limit);
            Velocity = Vector3.ClampMagnitude(Velocity, limit * omega);
            if (Offset.sqrMagnitude < 1e-12f && Velocity.sqrMagnitude < 1e-10f) Reset();
        }

        public static float Safe(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
