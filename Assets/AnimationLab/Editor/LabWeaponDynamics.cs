using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Small deterministic secondary motion, compiled into the existing weapon channel.
    // No random shake, camera animation, spring simulation or evaluation-time dependency.
    internal static class LabWeaponDynamics
    {
        internal static EquipmentVectorCurves Copy(EquipmentVectorCurves curves)=>JsonUtility.FromJson<EquipmentVectorCurves>(JsonUtility.ToJson(curves));
        internal static bool MatchesCompiled(AnimationLabTake take)
        {
            var r=take.weaponDynamics;if(r==null||!r.applied)return false;
            var times=new SortedSet<float>{0,1};
            foreach(var v in new[]{take.action.weaponPosition,take.action.weaponRotation,r.compiledPosition,r.compiledRotation})
                foreach(var curve in new[]{v.x,v.y,v.z})foreach(var key in curve.keys)times.Add(Mathf.Clamp01(key.time));
            var anchors=times.ToArray();
            for(int i=0;i+1<anchors.Length;i++){times.Add(Mathf.Lerp(anchors[i],anchors[i+1],1/3f));times.Add(Mathf.Lerp(anchors[i],anchors[i+1],2/3f));}
            foreach(float t in times)
            {
                float position=Vector3.Distance(take.action.weaponPosition.Evaluate(t),r.compiledPosition.Evaluate(t));
                float rotation=Vector3.Distance(take.action.weaponRotation.Evaluate(t),r.compiledRotation.Evaluate(t));
                if(float.IsNaN(position)||float.IsNaN(rotation)||position>.000001f||rotation>.0001f)return false;
            }
            return true;
        }
        internal static AnimationLabTake Generate(AnimationLabTake source,LabPreview preview,float intensity)
        {
            if(float.IsNaN(intensity)||float.IsInfinity(intensity)||intensity<0||intensity>1)throw new InvalidOperationException("La intensidad debe estar entre 0 y 1.");
            if(source.action.duration<=0||source.action.duration>30)throw new InvalidOperationException("El movimiento natural admite tomas de hasta 30 segundos.");
            var take=LabAssetIO.Clone(source);
            try
            {
                var settings=take.weaponDynamics??(take.weaponDynamics=new LabWeaponDynamicsRecipe());
                if(!settings.applied||!MatchesCompiled(source))
                {
                    settings.sourceKeys=LabObjectAuthoring.CloneKeys(source.weapon);
                    settings.sourcePosition=Copy(source.action.weaponPosition);settings.sourceRotation=Copy(source.action.weaponRotation);
                }
                var anchors=new SortedSet<float>(settings.sourceKeys.Select(k=>k.time)){0,1};
                var events=source.parts.SelectMany(p=>p.keys).Select(k=>k.time)
                    .Concat(source.props.SelectMany(p=>p.keys).Select(k=>k.time)).Concat(anchors).Distinct().ToArray();
                int samples=Mathf.CeilToInt(source.action.duration*60);
                var times=new SortedSet<float>(Enumerable.Range(0,samples+1).Select(i=>i/(float)samples));
                foreach(float t in anchors)times.Add(t);
                foreach(float t in events)times.Add(t);
                var parent=preview.Equipment.weaponPose.PoseRoot.parent;
                Vector3 scale=parent!=null?parent.lossyScale:Vector3.one;
                scale=new Vector3(Mathf.Max(.000001f,Mathf.Abs(scale.x)),Mathf.Max(.000001f,Mathf.Abs(scale.y)),Mathf.Max(.000001f,Mathf.Abs(scale.z)));
                take.weapon.Clear();
                foreach(float t in times)
                {
                    float a=anchors.LastOrDefault(k=>k<=t),b=anchors.FirstOrDefault(k=>k>t);
                    if(b<=a)b=1;
                    float u=Mathf.InverseLerp(a,b,t),envelope=Mathf.Pow(Mathf.Sin(u*Mathf.PI),2);
                    if(anchors.Contains(t))envelope=0;
                    float seconds=t*source.action.duration;
                    float breath=Mathf.Sin(seconds*Mathf.PI*2*.65f)*.65f+Mathf.Sin(seconds*Mathf.PI*2*1.1f+.6f)*.2f;
                    float reaction=0;
                    foreach(float cue in events)
                    {
                        float d=(t-cue)*source.action.duration;
                        if(d>0&&d<.7f)reaction+=Mathf.Exp(-d*8)*Mathf.Sin(d*18);
                    }
                    reaction=Mathf.Clamp(reaction,-1,1);
                    float amount=intensity*envelope;
                    Vector3 physical=Vector3.ClampMagnitude(new Vector3(.0025f*breath,-.0035f*breath-.003f*reaction,.002f*reaction),.006f)*amount;
                    Vector3 offset=new Vector3(physical.x/scale.x,physical.y/scale.y,physical.z/scale.z);
                    Vector3 turn=Vector3.ClampMagnitude(new Vector3(.9f*breath+1.2f*reaction,.35f*breath,-.65f*breath-.4f*reaction),1.8f)*amount;
                    take.weapon.Add(new LabWeaponKey{time=t,position=settings.sourcePosition.Evaluate(t)+offset,euler=settings.sourceRotation.Evaluate(t)+turn,pathMode=LabPathMode.Linear});
                }
                settings.applied=true;settings.intensity=intensity;
                LabAssetIO.BuildWeaponMotion(take,true);
                settings.compiledPosition=Copy(take.action.weaponPosition);settings.compiledRotation=Copy(take.action.weaponRotation);
                LabCoordinatedPaths.Compile(take,preview);
                return take;
            }
            catch{LabAssetIO.DestroyDraft(take);throw;}
        }
        internal static AnimationLabTake Restore(AnimationLabTake source,LabPreview preview)
        {
            if(source.weaponDynamics==null||!source.weaponDynamics.applied)throw new InvalidOperationException("Esta toma no tiene una capa natural que quitar.");
            if(!MatchesCompiled(source))throw new InvalidOperationException("Las curvas del arma se editaron después de aplicar la capa. No se restaurará una base antigua sobre esos cambios.");
            var take=LabAssetIO.Clone(source);
            try
            {
                take.weapon=LabObjectAuthoring.CloneKeys(take.weaponDynamics.sourceKeys);
                take.action.weaponPosition=Copy(take.weaponDynamics.sourcePosition);take.action.weaponRotation=Copy(take.weaponDynamics.sourceRotation);
                take.weaponDynamics.applied=false;LabCoordinatedPaths.Compile(take,preview);return take;
            }
            catch{LabAssetIO.DestroyDraft(take);throw;}
        }
    }
}
