using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // One exact Hermite representation for authoring, preview and exported runtime curves.
    internal static class LabPathCurves
    {
        internal static EquipmentVectorCurves Build<T>(IEnumerable<T> source, Func<T, float> time,
            Func<T, Vector3> value, Func<T, LabPathMode> mode, Func<T, Vector3> bend, bool legacyLinear, bool rotation = false,
            Func<T, float> departure = null, Func<T, float> arrival = null,Func<T,Vector3> outgoing=null,Func<T,Vector3> incoming=null)
        {
            var keys = source.OrderBy(time).ToArray();
            var curves = new AnimationCurve[3];
            for (int axis = 0; axis < 3; axis++)
            {
                var frames = keys.Select(k => new Keyframe(time(k), value(k)[axis])).ToArray();
                var middle = new List<Keyframe>();
                for (int i = 0; i + 1 < keys.Length; i++)
                {
                    float dt = time(keys[i + 1]) - time(keys[i]);
                    if (dt <= LabTimeline.TimeEpsilon) continue;
                    float delta = (value(keys[i + 1])[axis] - value(keys[i])[axis]) / dt;
                    float start = delta, end = delta;
                    var kind = mode(keys[i]);
                    if(kind==LabPathMode.Bezier||kind==LabPathMode.Flow)
                    {
                        start=rotation?delta*Mathf.Clamp01(departure?.Invoke(keys[i])??0):3*(outgoing?.Invoke(keys[i])[axis]??0)/dt;
                        end=rotation?delta*Mathf.Clamp01(arrival?.Invoke(keys[i])??0):-3*(incoming?.Invoke(keys[i])[axis]??0)/dt;
                        if(rotation&&kind==LabPathMode.Flow)
                        {
                            int before=Mathf.Max(0,i-1),after=Mathf.Min(keys.Length-1,i+2);
                            start=(value(keys[i+1])[axis]-value(keys[before])[axis])/Mathf.Max(LabTimeline.TimeEpsilon,time(keys[i+1])-time(keys[before]))*Mathf.Clamp01(departure?.Invoke(keys[i])??0);
                            end=(value(keys[after])[axis]-value(keys[i])[axis])/Mathf.Max(LabTimeline.TimeEpsilon,time(keys[after])-time(keys[i]))*Mathf.Clamp01(arrival?.Invoke(keys[i])??0);
                        }
                    }
                    else if (kind == LabPathMode.Cinematic)
                    {
                        start = delta * Mathf.Clamp01(departure?.Invoke(keys[i]) ?? 0);
                        end = delta * Mathf.Clamp01(arrival?.Invoke(keys[i]) ?? 0);
                        middle.Add(new Keyframe((time(keys[i]) + time(keys[i + 1])) * 0.5f,
                            (value(keys[i])[axis] + value(keys[i + 1])[axis]) * 0.5f + (rotation ? 0 : bend(keys[i])[axis]),
                            delta * 1.5f, delta * 1.5f));
                    }
                    else if (kind == LabPathMode.Ease || (kind == LabPathMode.Legacy && !legacyLinear)) start = end = 0;
                    else if (kind == LabPathMode.Smooth)
                    {
                        int before = Mathf.Max(0, i - 1), after = Mathf.Min(keys.Length - 1, i + 2);
                        start = (value(keys[i + 1])[axis] - value(keys[before])[axis]) / Mathf.Max(LabTimeline.TimeEpsilon, time(keys[i + 1]) - time(keys[before]));
                        end = (value(keys[after])[axis] - value(keys[i])[axis]) / Mathf.Max(LabTimeline.TimeEpsilon, time(keys[after]) - time(keys[i]));
                    }
                    else if (kind == LabPathMode.Arc && !rotation)
                    {
                        start += 4 * bend(keys[i])[axis] / dt;
                        end -= 4 * bend(keys[i])[axis] / dt;
                    }
                    frames[i].outTangent = start; frames[i + 1].inTangent = end;
                }
                curves[axis] = new AnimationCurve(frames.Concat(middle).OrderBy(k => k.time).ToArray()) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
            }
            return new EquipmentVectorCurves { x = curves[0], y = curves[1], z = curves[2] };
        }

        internal static EquipmentVectorCurves Objects(List<LabWeaponKey> keys, bool rotation = false) =>
            Build(keys, k => k.time, k => rotation ? k.euler : k.position, k => k.pathMode, k => k.bend, true, rotation, k => k.departureSpeed, k => k.arrivalSpeed, k=>k.outgoing,k=>k.incoming);

        internal static AnimationCurve Seconds(AnimationCurve source, float duration)
        {
            return new AnimationCurve(source.keys.Select(k => new Keyframe(k.time * duration, k.value,
                k.inTangent / duration, k.outTangent / duration)).ToArray());
        }
    }
}
