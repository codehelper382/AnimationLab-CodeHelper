using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    /// <summary>
    /// Analytic two-segment IK using the law of cosines and a bend plane.
    /// Only depends on Unity. Computes rotations without modifying scene objects or allocating memory.
    /// </summary>
    public static class HandIKMath
    {
        private const float MinLength = 0.0001f;
        private const float DirectionEpsilon = 1e-10f;

        /// <summary>
        /// Finds upper/forearm world rotations for a weighted goal position. Targets outside the
        /// reach interval are clamped; bones never stretch. The wrist rotation is handled separately.
        /// For a fully folded goal at the shoulder, a small nonzero distance avoids division by zero.
        /// </summary>
        public static bool TrySolve(Vector3 shoulder, Vector3 elbow, Vector3 wrist,
            Quaternion upperRotation, Quaternion forearmRotation, Vector3 goal,
            bool hasHint, Vector3 hintPosition, float hintWeight,
            out Quaternion solvedUpper, out Quaternion solvedForearm)
        {
            solvedUpper = upperRotation;
            solvedForearm = forearmRotation;
            if (!IsFinite(shoulder) || !IsFinite(elbow) || !IsFinite(wrist) || !IsFinite(goal) ||
                !IsFinite(upperRotation) || !IsFinite(forearmRotation)) return false;

            Vector3 upperSegment = elbow - shoulder;
            Vector3 lowerSegment = wrist - elbow;
            float upperLength = upperSegment.magnitude;
            float lowerLength = lowerSegment.magnitude;
            if (!IsFinite(upperLength) || !IsFinite(lowerLength) ||
                upperLength < MinLength || lowerLength < MinLength) return false;
            Vector3 upperDirection = upperSegment / upperLength;
            Vector3 lowerDirection = lowerSegment / lowerLength;

            Vector3 goalDirection = goal - shoulder;
            float goalDistance = goalDirection.magnitude;
            if (!IsFinite(goalDistance)) return false;
            Vector3 animatedReach = wrist - shoulder;
            // A fully folded arm already at a coincident goal needs no positional correction.
            // Choosing an arbitrary direction here would rotate an already-satisfied pose.
            if (goalDistance < MinLength && animatedReach.sqrMagnitude < MinLength * MinLength &&
                (!hasHint || hintWeight <= 0f)) return true;
            if (goalDistance > MinLength) goalDirection /= goalDistance;
            else
            {
                // At the shoulder the goal supplies no direction. Keep the animated reach direction.
                goalDirection = animatedReach;
                if (goalDirection.sqrMagnitude < DirectionEpsilon)
                {
                    // At full fold the reach axis is undefined. A perpendicular axis preserves the
                    // existing elbow direction; choosing the upper segment itself would destroy it.
                    goalDirection = Vector3.ProjectOnPlane(upperRotation * Vector3.up, upperDirection);
                    if (goalDirection.sqrMagnitude < DirectionEpsilon)
                        goalDirection = Vector3.ProjectOnPlane(upperRotation * Vector3.right, upperDirection);
                }
                goalDirection.Normalize();
            }

            float minimumReach = Mathf.Max(Mathf.Abs(upperLength - lowerLength), MinLength);
            float maximumReach = upperLength + lowerLength;
            float reach = Mathf.Clamp(goalDistance, minimumReach, maximumReach);

            // Elbow is on the intersection circle of two spheres centered at shoulder and goal.
            float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            // Use the animated bend PLANE first. Projecting only the upper segment changes sign
            // abruptly when a moving target crosses the shoulder-elbow direction.
            // Directional tolerances must be dimensionless: raw segment cross products scale
            // with length squared and would discard a perfectly valid plane on small rigs.
            Vector3 animatedNormal = Vector3.Cross(upperDirection, lowerDirection);
            Vector3 bend = Vector3.Cross(goalDirection, animatedNormal);
            if (bend.sqrMagnitude < DirectionEpsilon)
            {
                bend = Vector3.ProjectOnPlane(upperDirection, goalDirection);
                if (bend.sqrMagnitude < DirectionEpsilon)
                    bend = Vector3.ProjectOnPlane(upperRotation * Vector3.up, goalDirection);
                if (bend.sqrMagnitude < DirectionEpsilon)
                    bend = Vector3.ProjectOnPlane(upperRotation * Vector3.right, goalDirection);
            }
            bend.Normalize();

            if (hasHint && hintWeight > 0f && IsFinite(hintPosition))
            {
                Vector3 pole = Vector3.ProjectOnPlane(hintPosition - shoulder, goalDirection);
                if (pole.sqrMagnitude > DirectionEpsilon)
                {
                    // Blend around the reach axis rather than lerping opposite vectors to zero.
                    float angle = Vector3.SignedAngle(bend, pole, goalDirection);
                    bend = Quaternion.AngleAxis(angle * Mathf.Clamp01(hintWeight), goalDirection) * bend;
                }
            }

            Vector3 desiredUpper = goalDirection * along + bend * height;
            Vector3 desiredLower = goalDirection * reach - desiredUpper;
            Quaternion upperDelta = Quaternion.FromToRotation(upperSegment, desiredUpper);
            Quaternion lowerDelta = Quaternion.FromToRotation(upperDelta * lowerSegment, desiredLower);
            solvedUpper = upperDelta * upperRotation;
            solvedForearm = lowerDelta * upperDelta * forearmRotation;
            return IsFinite(solvedUpper) && IsFinite(solvedForearm);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }
}
