using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabFingerKeyTools
    {
        internal static float Blend(LabHandKey before,LabHandKey after,float time)
        {
            if(after.time<=before.time)return 0;
            if(after.fingerBlend==LabFingerBlend.Legacy)return Mathf.InverseLerp(before.time,after.time,time);
            if(after.fingerBlend==LabFingerBlend.Step||after.fingerBlendDuration<=0)return time>=after.time?1:0;
            float start=Mathf.Max(before.time,after.time-after.fingerBlendDuration);
            float weight=Mathf.InverseLerp(start,after.time,time);
            return after.fingerBlend==LabFingerBlend.Smooth?Mathf.SmoothStep(0,1,weight):weight;
        }
    }
}
