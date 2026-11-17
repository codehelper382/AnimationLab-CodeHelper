using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Compile authoring contacts to ordinary action curves. No evaluation-time constraint or extra graph.
    internal static class LabCoordinatedPaths
    {
        internal sealed class Segment
        {
            internal string objectId;
            internal HandSide side;
            internal LabWeaponKey from,to;
        }
        internal static IEnumerable<Segment> Segments(AnimationLabTake take)
        {
            var tracks=take.parts.Select(p=>(id:"part:"+p.path,keys:p.keys)).Concat(take.props.Select(p=>(id:"prop:"+p.id,keys:p.keys)));
            foreach(var track in tracks)
            {
                var keys=track.keys.OrderBy(k=>k.time).ToArray();
                for(int i=0;i+1<keys.Length;i++)foreach(HandSide side in new[]{HandSide.Left,HandSide.Right})
                    if(!(side==HandSide.Left?keys[i].breakLeftContact:keys[i].breakRightContact)&&Contact(keys[i],side)!=null&&Contact(keys[i+1],side)!=null)
                        yield return new Segment{objectId=track.id,side=side,from=keys[i],to=keys[i+1]};
            }
        }
        internal static LabPathContact Contact(LabWeaponKey key,HandSide side)
        {var c=side==HandSide.Left?key.leftContact:key.rightContact;return c!=null&&c.active?c:null;}
        internal static void SetContact(LabWeaponKey key,HandSide side,LabPathContact contact)
        {if(side==HandSide.Left)key.leftContact=contact;else key.rightContact=contact;}
        internal static Transform Target(LabPreview preview,string id)=>id.StartsWith("part:")?preview.Objects.ResolvePart(id.Substring(5)):preview.Objects.ResolveProp(id.Substring(5));
        internal static Segment At(AnimationLabTake take,HandSide side,float time)=>Segments(take).FirstOrDefault(s=>s.side==side&&time>=s.from.time-LabTimeline.TimeEpsilon&&time<=s.to.time+LabTimeline.TimeEpsilon);
        // Contacts are compiled at full action weight; stop before the global blend-out and let it fade the last frames.
        internal static float ContactHoldTime(AnimationLabTake take)
        {
            float duration=Mathf.Max(.0001f,take.action.duration);
            return take.action.blendOut>0?Mathf.Clamp01(1f-take.action.blendOut/duration):1f;
        }
        internal static void Compile(AnimationLabTake source,LabPreview preview)
        {
            var segments=Segments(source).ToArray();if(segments.Length==0)return;
            if(source.action.duration<=0||source.action.duration>30)throw new InvalidOperationException("El recorrido combinado admite tomas de hasta 30 segundos.");
            var oldLeft=preview.Slot.leftHandPose;var oldRight=preview.Slot.rightHandPose;
            var take=LabAssetIO.Clone(source);var output=new Dictionary<HandSide,List<LabHandKey>>();var weights=new Dictionary<HandSide,AnimationCurve>();
            try
            {
                if(source.left.Any(k=>!k.fingersOnly))preview.EnableHand(HandSide.Left);else preview.RestoreHandBinding(HandSide.Left);
                if(source.right.Any(k=>!k.fingersOnly))preview.EnableHand(HandSide.Right);else preview.RestoreHandBinding(HandSide.Right);
                foreach(var side in segments.Select(s=>s.side).Distinct())
                {
                    if(side==HandSide.Right&&source.action.followAnimatedRightHand)throw new InvalidOperationException("Desactiva explícitamente Follow Animated Right Hand antes de combinar la derecha con una pieza.");
                    var ranges=segments.Where(s=>s.side==side).OrderBy(s=>s.from.time).ToArray();
                    for(int i=1;i<ranges.Length;i++)if(ranges[i].from.time<ranges[i-1].to.time-LabTimeline.TimeEpsilon)
                        throw new InvalidOperationException("Una mano no puede seguir dos piezas en el mismo intervalo.");
                    int count=Mathf.CeilToInt(source.action.duration*60);
                    var times=new SortedSet<float>(Enumerable.Range(0,count+1).Select(i=>i/(float)count));
                    var keys=side==HandSide.Left?source.left:source.right;
                    foreach(var key in keys)times.Add(key.time);
                    foreach(var range in ranges){times.Add(range.from.time);times.Add(range.to.time);}
                    var originals=new Dictionary<float,(Vector3 p,Quaternion r,float weight)>();
                    foreach(float t in times)
                    {
                        preview.Evaluate(source,t);
                        var chain=side==HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
                        var wrist=chain.target!=null?chain.target:chain.hand;
                        originals[t]=(wrist.position,wrist.rotation,chain.weight);
                    }
                    preview.EnableHand(side);
                    var binding=side==HandSide.Left?preview.Slot.leftHandPose:preview.Slot.rightHandPose;
                    var ik=side==HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
                    if(binding.weight<1||preview.Equipment.handIK.alpha<1||ik.positionWeight<1||ik.rotationWeight<1)
                        throw new InvalidOperationException("El recorrido combinado necesita binding y solver IK al 100%; no se cambia su calibración automáticamente.");
                    var motion=side==HandSide.Left?take.action.leftHandMotion:take.action.rightHandMotion;
                    motion.enabled=true;motion.motionClip=null;motion.reach=AnimationCurve.Constant(0,1,0);
                    motion.position=new EquipmentVectorCurves();motion.rotation=new EquipmentVectorCurves();
                    if(side==HandSide.Left)take.action.leftHandTargetMotion=null;
                    var generated=new List<LabHandKey>();var ikFrames=new List<Keyframe>();Vector3 previous=Vector3.zero;
                    foreach(float t in times)
                    {
                        preview.Evaluate(take,t);
                        var guard=preview.Guard(side);var original=originals[t];
                        Vector3 goal=original.p;Quaternion rotation=original.r;
                        var range=ranges.FirstOrDefault(s=>t>=s.from.time-LabTimeline.TimeEpsilon&&t<=s.to.time+LabTimeline.TimeEpsilon);
                        if(range!=null)
                        {
                            var target=Target(preview,range.objectId);if(target==null)throw new InvalidOperationException("La pieza combinada ya no existe.");
                            float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(range.from.time,range.to.time,t));
                            var a=Contact(range.from,side);var b=Contact(range.to,side);
                            goal=target.TransformPoint(Vector3.Lerp(a.position,b.position,u));
                            rotation=target.rotation*Quaternion.Slerp(Quaternion.Euler(a.euler),Quaternion.Euler(b.euler),u);
                        }
                        float weight=EquipmentSceneController.ActionWeight(take.action,t);
                        Vector3 local=guard.InverseTransformPoint(goal);Quaternion delta=Quaternion.Inverse(guard.rotation)*rotation;
                        if(weight<.00001f)
                        {
                            if((range!=null||original.weight>.001f)&&(local.magnitude>.001f||Quaternion.Angle(delta,Quaternion.identity)>.1f))
                                throw new InvalidOperationException("El contacto no coincide con la guardia en un fundido de peso cero. Coloca el conjunto fuera de ese fundido o ajusta explícitamente la guardia.");
                            local=Vector3.zero;delta=Quaternion.identity;
                        }
                        else
                        {
                            local/=weight;delta.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;
                            if(Mathf.Abs(angle)/weight>180.01f)throw new InvalidOperationException("El giro combinado no puede conservarse con el fundido actual.");
                            delta=Mathf.Abs(angle)<.00001f?Quaternion.identity:Quaternion.AngleAxis(angle/weight,axis);
                        }
                        var euler=LabTimeline.UnwrapEuler(delta.eulerAngles,previous);previous=euler;
                        generated.Add(new LabHandKey{time=t,position=local,euler=euler,pathMode=LabPathMode.Linear});
                        ikFrames.Add(new Keyframe(t,range!=null?1:original.weight));
                    }
                    output[side]=generated;
                    var curve=new AnimationCurve(ikFrames.ToArray());
                    for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                    weights[side]=curve;
                }
                // Commit only after every side was sampled successfully. Retain all finger objects and timing.
                foreach(var pair in output)
                {
                    var keys=pair.Key==HandSide.Left?source.left:source.right;
                    keys.RemoveAll(k=>k.fingers==null);foreach(var key in keys)key.fingersOnly=true;
                    foreach(var sample in pair.Value)
                    {
                        var key=LabTimeline.Find(keys,sample.time);
                        if(key==null){keys.Add(sample);continue;}
                        key.fingersOnly=false;key.position=sample.position;key.euler=sample.euler;key.pathMode=LabPathMode.Linear;
                        key.bend=key.outgoing=key.incoming=Vector3.zero;
                    }
                    keys.Sort((a,b)=>a.time.CompareTo(b.time));
                    var motion=pair.Key==HandSide.Left?source.action.leftHandMotion:source.action.rightHandMotion;
                    LabAssetIO.BuildMotion(keys,motion);
                    if(pair.Key==HandSide.Left){source.action.leftHandTargetMotion=null;source.action.leftIK=weights[pair.Key];source.leftContactMotion=true;}
                    else{source.action.rightIK=weights[pair.Key];source.rightContactMotion=true;}
                }
            }
            finally{preview.Slot.leftHandPose=oldLeft;preview.Slot.rightHandPose=oldRight;LabAssetIO.DestroyDraft(take);}
        }
    }
}
