using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Frame numbers are a UI reference, never stored in portable actions. Preserve off-grid legacy keys.
    internal static class LabTimeline
    {
        internal const int FrameRate = 60;
        internal const float TimeEpsilon = 0.000001f;
        internal static float[] SampleTimes(IEnumerable<float> samples, IEnumerable<float> exact)
        {
            var times=new SortedSet<float>();
            // Reference/recipe times win over numerically adjacent regular 60 Hz samples.
            foreach(float t in exact.Concat(samples))
                if(!times.GetViewBetween(t-TimeEpsilon,t+TimeEpsilon).Any())times.Add(t);
            return times.ToArray();
        }
        internal static int LastFrame(float duration)
        {
            float frames=duration*FrameRate,nearest=Mathf.Round(frames);
            // Float seconds such as 3.3 can multiply to 198.00002. Do not invent a final frame.
            if(Mathf.Abs(frames-nearest)<=Mathf.Max(.0001f,Mathf.Abs(frames)*.0000002f))frames=nearest;
            return Mathf.Max(1,Mathf.CeilToInt(frames));
        }
        internal static int Frame(float time, float duration) => time >= 1 ? LastFrame(duration) : Mathf.Clamp(Mathf.RoundToInt(time * duration * FrameRate), 0, LastFrame(duration));
        internal static float Time(int frame, float duration) => frame>=LastFrame(duration)?1f:Mathf.Clamp01(frame / (FrameRate * Mathf.Max(0.01f, duration)));
        internal static LabHandKey Find(List<LabHandKey> keys, float time) => keys.FirstOrDefault(k => Mathf.Abs(k.time - time) < TimeEpsilon);
        internal static LabHandKey FindWrist(List<LabHandKey> keys, float time) => keys.FirstOrDefault(k => !k.fingersOnly && Mathf.Abs(k.time-time)<TimeEpsilon);
        internal static LabHandKey FindFingers(List<LabHandKey> keys, float time) => keys.FirstOrDefault(k => k.fingers!=null && Mathf.Abs(k.time-time)<TimeEpsilon);
        internal static bool HasValues(EquipmentVectorCurves curves) => curves != null &&
            new[] { curves.x, curves.y, curves.z }.Any(HasMotion);
        internal static bool HasMotion(AnimationCurve curve) => curve != null && curve.keys.Any(k =>
            Mathf.Abs(k.value) > 0.00001f || Mathf.Abs(k.inTangent) > 0.00001f || Mathf.Abs(k.outTangent) > 0.00001f);
        internal static Vector3 UnwrapEuler(Vector3 value, Vector3 reference) => new Vector3(
            reference.x + Mathf.DeltaAngle(reference.x, value.x), reference.y + Mathf.DeltaAngle(reference.y, value.y), reference.z + Mathf.DeltaAngle(reference.z, value.z));
        internal static bool ReplacesMotion(EquipmentHandMotion motion) => motion != null &&
            (motion.motionClip != null || HasValues(motion.position) || HasValues(motion.rotation) ||
                HasMotion(motion.reach));

        internal static void RecordWrist(List<LabHandKey> keys, EquipmentHandMotion motion, float time, Vector3 position, Vector3 rotation)
        {
            var key = FindWrist(keys, time) ?? Find(keys, time);
            if (key == null) { key = new LabHandKey { time = time }; keys.Add(key); }
            key.fingersOnly = false; key.position = position; key.euler = rotation;
            keys.Sort((a, b) => a.time.CompareTo(b.time));
            LabAssetIO.BuildMotion(keys, motion);
        }

        // The caller owns/clones the pose; changing fingers must not touch the recipe's wrist channel.
        internal static void RecordFingers(List<LabHandKey> keys, float time, HandGripToolkit.HandPoseData pose)
        {
            var key = FindFingers(keys, time) ?? Find(keys, time);
            if (key == null) { key = new LabHandKey { time = time, fingersOnly = true }; keys.Add(key); }
            key.fingers = pose;
            keys.Sort((a, b) => a.time.CompareTo(b.time));
        }

        internal static void RemoveWrist(List<LabHandKey> keys, LabHandKey key, EquipmentHandMotion motion)
        {
            if (key.fingers != null) key.fingersOnly = true; else keys.Remove(key);
            if (keys.Any(k => !k.fingersOnly)) LabAssetIO.BuildMotion(keys, motion);
            else
            {
                motion.enabled = false; motion.motionClip = null;
                motion.reach = AnimationCurve.Constant(0, 1, 0);
                motion.position = new EquipmentVectorCurves(); motion.rotation = new EquipmentVectorCurves();
            }
        }
    }
}
