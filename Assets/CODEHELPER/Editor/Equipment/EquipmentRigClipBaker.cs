#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Bakes coordinated local transforms into one plain Generic clip. Editor-only; no runtime rig dependency.</summary>
    public static class EquipmentRigClipBaker
    {
        public static AnimationClip Bake(string name, float length, float frameRate, string[] paths, Func<float, Pose[]> sample)
        {
            if (length <= 0f || frameRate <= 0f || paths.Length == 0) throw new ArgumentException("Invalid rig bake.");
            var curves = Enumerable.Range(0, paths.Length * 7).Select(_ => new AnimationCurve()).ToArray();
            int frames = Mathf.CeilToInt(length * frameRate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = Mathf.Min(length, frame / frameRate);
                Pose[] poses = sample(time / length);
                if (poses.Length != paths.Length) throw new InvalidOperationException("Rig sample size mismatch.");
                for (int node = 0; node < poses.Length; node++)
                {
                    Vector3 p = poses[node].position; Quaternion q = poses[node].rotation;
                    float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                    for (int axis = 0; axis < 7; axis++) curves[node * 7 + axis].AddKey(new Keyframe(time, values[axis]));
                }
            }
            string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x",
                "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            var bindings = paths.SelectMany(path => properties.Select(property => EditorCurveBinding.FloatCurve(path, typeof(Transform), property))).ToArray();
            var clip = new AnimationClip { name = name, frameRate = frameRate };
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            clip.EnsureQuaternionContinuity();
            // Linear baked samples avoid automatic tangent overshoot between the authored frames.
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                for (int key = 0; key < curve.length; key++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            return clip;
        }
    }
}
#endif
