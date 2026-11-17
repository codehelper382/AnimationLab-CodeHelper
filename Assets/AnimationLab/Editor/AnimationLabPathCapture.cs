using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private void CapturePathEndpoints(bool confirmed)
        {
            if(!confirmed)throw new InvalidOperationException("Confirma la sustitución de la posición antes de capturar extremos.");
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de capturar puntos.");
            if(!CanLeaveFrame())return;
            float incoming=draft.action.blendIn,outgoing=draft.action.blendOut;var samples=new List<LabWeaponKey>();
            try
            {
                draft.action.blendIn=draft.action.blendOut=0;Vector3 last=Vector3.zero;
                foreach(float t in new[]{0f,1f})
                {
                    preview.Evaluate(draft,t,rawFingers:true);Vector3 p,r;
                    if(editObject)
                    {if(ObjectTarget==null)throw new InvalidOperationException("Selecciona una pieza.");p=ObjectTarget.localPosition;r=LabTimeline.UnwrapEuler(ObjectTarget.localEulerAngles,last);}
                    else if(editWeapon){p=draft.action.weaponPosition.Evaluate(t);r=draft.action.weaponRotation.Evaluate(t);}
                    else
                    {
                        var chain=side==HandGripToolkit.HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
                        var target=chain.target!=null?chain.target:chain.hand;var guard=preview.Guard(side);
                        if(guard==null)throw new InvalidOperationException("La mano necesita una guardia.");
                        p=guard.InverseTransformPoint(target.position);r=LabTimeline.UnwrapEuler((Quaternion.Inverse(guard.rotation)*target.rotation).eulerAngles,last);
                    }
                    last=r;samples.Add(new LabWeaponKey{time=t,position=p,euler=r,pathMode=LabPathMode.Cinematic});
                }
            }
            finally{draft.action.blendIn=incoming;draft.action.blendOut=outgoing;}
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.RecordObject(draft,"Capturar dos puntos");Undo.RecordObject(draft.action,"Capturar dos puntos");
            if(editObject)
            {var keys=ObjectKeys(true);keys.Clear();keys.AddRange(samples);}
            else if(editWeapon){draft.weapon=samples;LabAssetIO.BuildWeaponMotion(draft);}
            else
            {
                HandKeys.RemoveAll(k=>!k.fingersOnly&&k.fingers==null);foreach(var k in HandKeys)k.fingersOnly=true;
                preview.EnableHand(side);
                foreach(var sample in samples){LabTimeline.RecordWrist(HandKeys,HandMotion,sample.time,sample.position,sample.euler);LabTimeline.Find(HandKeys,sample.time).pathMode=LabPathMode.Cinematic;}
                if(side==HandGripToolkit.HandSide.Left)draft.action.leftHandTargetMotion=null;
                LabAssetIO.BuildMotion(HandKeys,HandMotion);
            }
            Undo.CollapseUndoOperations(group);dirty=true;notice="Dos puntos de posición capturados. Los dedos y las demás pistas se conservan.";SyncSelection();
        }
    }
}
