using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Existing recipe is sampled read-only. Visual controls add corrections, never replace its dense motion.
    internal static class LabReloadReference
    {
        internal static EquipmentActionDefinition Resolve(LabPreview preview,LabHandSource source)
        {
            var gun=preview.Slot.definition as FirearmDefinition;
            return source==LabHandSource.Reload?gun?.reload:source==LabHandSource.ReloadEmpty?gun?.reloadEmpty:null;
        }
        internal static AnimationLabTake Create(AnimationLabTake owner,LabPreview preview)
        {
            var action=Resolve(preview,owner.handInsert.source);
            if(action==null)throw new InvalidOperationException("Este equipo no tiene la recarga de referencia seleccionada.");
            var take=LabAssetIO.Clone(null);
            UnityEngine.Object.DestroyImmediate(take.action);take.action=UnityEngine.Object.Instantiate(action);take.action.hideFlags=HideFlags.HideAndDontSave;
            take.slot=owner.slot;take.equipmentId=owner.equipmentId;take.kind=EquipmentActionKind.Reload;
            take.action.blendIn=take.action.blendOut=0;
            return take;
        }
        internal static LabHandKey Sample(AnimationLabTake reference,LabPreview preview,float time,HandSide side,bool fingers)
        {
            preview.RestoreHandBinding(HandSide.Left);preview.RestoreHandBinding(HandSide.Right);
            preview.Evaluate(reference,time,rawFingers:true);
            var chain=side==HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
            var wrist=chain.target!=null?chain.target:chain.hand;var guard=preview.Guard(side);
            if(guard==null)throw new InvalidOperationException("La referencia necesita una guardia compatible.");
            var result=new LabHandKey{time=time,position=guard.InverseTransformPoint(wrist.position),euler=(Quaternion.Inverse(guard.rotation)*wrist.rotation).eulerAngles};
            if(fingers)
            {
                result.fingers=ScriptableObject.CreateInstance<HandPoseData>();result.fingers.Side=side;result.fingers.hideFlags=HideFlags.HideAndDontSave;
                if(!result.fingers.Capture(preview.Animator)){UnityEngine.Object.DestroyImmediate(result.fingers);throw new InvalidOperationException("La referencia no tiene dedos compatibles.");}
            }
            return result;
        }
        internal static List<LabGesturePoint> CreatePoints(AnimationLabTake owner,LabPreview preview)
        {
            var left=preview.Slot.leftHandPose;var right=preview.Slot.rightHandPose;var reference=Create(owner,preview);
            try
            {
                var times=new SortedSet<float>(Enumerable.Range(0,13).Select(i=>i/12f));
                foreach(float t in LabGestureAuthoring.Defaults(owner.handInsert.template))times.Add(t);
                var result=new List<LabGesturePoint>();Vector3 previous=Vector3.zero;
                foreach(float time in times)
                {
                    var sample=Sample(reference,preview,time,owner.handInsert.side,false);
                    var euler=LabTimeline.UnwrapEuler(sample.euler,previous);previous=euler;
                    result.Add(new LabGesturePoint{time=time,label=$"Referencia · {Mathf.RoundToInt(time*100)}%",position=sample.position,euler=euler,
                        referencePosition=sample.position,referenceEuler=euler,hasReference=true,mode=LabPathMode.Cinematic,
                        contactAnchor=Mathf.Abs(time-LabGestureAuthoring.Defaults(owner.handInsert.template)[2])<.000001f});
                }
                return result;
            }
            finally{preview.Slot.leftHandPose=left;preview.Slot.rightHandPose=right;LabAssetIO.DestroyDraft(reference);}
        }
        internal static List<LabWeaponKey> Corrections(LabHandInsert r)
        {
            Vector3 previous=Vector3.zero;var result=new List<LabWeaponKey>();
            foreach(var point in r.points.OrderBy(p=>p.time))
            {
                if(!point.hasReference)throw new InvalidOperationException("Reconstruye los puntos desde la recarga original antes de editar esta referencia.");
                Vector3 delta=LabTimeline.UnwrapEuler((Quaternion.Euler(point.euler)*Quaternion.Inverse(Quaternion.Euler(point.referenceEuler))).eulerAngles,previous);previous=delta;
                result.Add(new LabWeaponKey{time=point.time,position=point.position-point.referencePosition,euler=delta,pathMode=point.mode,
                    bend=point.bend*(r.useStyle?r.curvature:1),outgoing=point.outgoing,incoming=point.incoming,departureSpeed=point.departureSpeed,arrivalSpeed=point.arrivalSpeed});
            }
            return result;
        }
        internal static LabHandKey At(AnimationLabTake owner,LabPreview preview,float time)
        {
            var left=preview.Slot.leftHandPose;var right=preview.Slot.rightHandPose;var reference=Create(owner,preview);
            try{return Sample(reference,preview,time,owner.handInsert.side,false);}
            finally{preview.Slot.leftHandPose=left;preview.Slot.rightHandPose=right;LabAssetIO.DestroyDraft(reference);}
        }
        internal static List<LabWeaponKey> ViewPath(AnimationLabTake owner,LabPreview preview)
        {
            var left=preview.Slot.leftHandPose;var right=preview.Slot.rightHandPose;var reference=Create(owner,preview);
            try
            {
                int count=Mathf.Clamp(Mathf.CeilToInt(reference.action.duration*60),1,1800);var result=new List<LabWeaponKey>();Vector3 previous=Vector3.zero;
                var times=new SortedSet<float>(Enumerable.Range(0,count+1).Select(i=>i/(float)count));
                foreach(var point in owner.handInsert.points)times.Add(point.time);
                foreach(float time in times)
                {
                    var p=Sample(reference,preview,time,owner.handInsert.side,false);var angles=LabTimeline.UnwrapEuler(p.euler,previous);previous=angles;
                    result.Add(new LabWeaponKey{time=p.time,position=p.position,euler=angles,pathMode=LabPathMode.Linear});
                }
                return result;
            }
            finally{preview.Slot.leftHandPose=left;preview.Slot.rightHandPose=right;LabAssetIO.DestroyDraft(reference);}
        }
    }
}
