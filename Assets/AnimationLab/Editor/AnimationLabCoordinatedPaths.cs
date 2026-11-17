using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool coordinatedPathPending;
        private static System.Collections.Generic.List<LabWeaponKey> TakeObjectKeys(AnimationLabTake take,string id,bool create=false)
        {
            if(id.StartsWith("prop:"))return take.props.FirstOrDefault(p=>"prop:"+p.id==id)?.keys;
            string path=id.Substring(5);var track=take.parts.FirstOrDefault(p=>p.path==path);
            if(track==null&&create){track=new LabObjectTrack{path=path};take.parts.Add(track);}
            return track?.keys;
        }
        private void ApplyCoordinatedCandidate(AnimationLabTake candidate,string id,HandSide[] changedHands,System.Collections.Generic.HashSet<(HandSide,float)> newFingers=null)
        {
            var destination=ObjectKeys(true);destination.Clear();destination.AddRange(TakeObjectKeys(candidate,id));
            ApplyCandidateHands(candidate,changedHands,newFingers);
        }
        private void ApplyCandidateHands(AnimationLabTake candidate,HandSide[] changedHands,System.Collections.Generic.HashSet<(HandSide,float)> newFingers=null)
        {
            foreach(var hand in changedHands.Distinct())
            {
                var old=hand==HandSide.Left?draft.left:draft.right;var input=hand==HandSide.Left?candidate.left:candidate.right;
                var result=new System.Collections.Generic.List<LabHandKey>();
                foreach(var key in input)
                {
                    var copy=CopyHandData(key);copy.fingersOnly=key.fingersOnly;
                    if(key.fingers!=null)
                    {
                        var original=old.FirstOrDefault(k=>k.fingers!=null&&Mathf.Abs(k.time-key.time)<LabTimeline.TimeEpsilon)?.fingers;
                        bool replace=newFingers!=null&&newFingers.Any(p=>p.Item1==hand&&Mathf.Abs(p.Item2-key.time)<LabTimeline.TimeEpsilon);
                        if(original!=null&&!replace)copy.fingers=original;
                        else{copy.fingers=key.fingers;key.fingers=null;retired.Add(copy.fingers);}
                    }
                    result.Add(copy);
                }
                foreach(var pose in old.Select(k=>k.fingers).Where(p=>p!=null).Distinct())
                    if(!result.Any(k=>k.fingers==pose))retired.Add(pose);
                if(hand==HandSide.Left)
                {draft.left=result;draft.action.leftHandMotion=candidate.action.leftHandMotion;draft.action.leftHandTargetMotion=candidate.action.leftHandTargetMotion;draft.action.leftIK=candidate.action.leftIK;draft.leftContactMotion=candidate.leftContactMotion;}
                else{draft.right=result;draft.action.rightHandMotion=candidate.action.rightHandMotion;draft.action.rightIK=candidate.action.rightIK;draft.rightContactMotion=candidate.rightContactMotion;}
            }
        }
        private static LabPathContact SegmentContact(LabCoordinatedPaths.Segment segment,float time)
        {
            float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(segment.from.time,segment.to.time,time));
            var a=LabCoordinatedPaths.Contact(segment.from,segment.side);var b=LabCoordinatedPaths.Contact(segment.to,segment.side);
            return new LabPathContact{active=true,position=Vector3.Lerp(a.position,b.position,u),euler=Quaternion.Slerp(Quaternion.Euler(a.euler),Quaternion.Euler(b.euler),u).eulerAngles};
        }
        private void InheritObjectContacts(LabWeaponKey key)
        {
            foreach(var segment in LabCoordinatedPaths.Segments(draft).Where(s=>s.objectId==objectId&&key.time>s.from.time&&key.time<s.to.time))
            {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(segment.from.time,segment.to.time,key.time));
                var a=LabCoordinatedPaths.Contact(segment.from,segment.side);var b=LabCoordinatedPaths.Contact(segment.to,segment.side);
                var contact=new LabPathContact{active=true,position=Vector3.Lerp(a.position,b.position,u),euler=Quaternion.Slerp(Quaternion.Euler(a.euler),Quaternion.Euler(b.euler),u).eulerAngles};
                LabCoordinatedPaths.SetContact(key,segment.side,contact);
            }
        }
        private void RefreshCoordinatedPaths()
        {
            if(!LabCoordinatedPaths.Segments(draft).Any()){coordinatedPathPending=false;return;}
            LabCoordinatedPaths.Compile(draft,preview);coordinatedPathPending=false;
        }
        private bool DrawLinkedHandPath()
        {
            if(editObject||editWeapon||editFingers)return false;
            var segment=LabCoordinatedPaths.At(draft,side,progress);if(segment==null)return false;
            LabStudioUI.Section("Recorrido combinado · mano + pieza","La pieza controla esta curva. La muñeca se recalcula a 60 Hz conservando su contacto.");
            if(GUILayout.Button("Editar la curva común de "+ObjectLabel(segment.objectId)))Safe(()=>SelectObject(segment.objectId));
            EditorGUILayout.HelpBox("No tienes que curvar la mano por separado. Para editarla de forma independiente, separa el tramo desde la pieza.",MessageType.Info);
            return true;
        }
        private void DrawCombinedPathControls(LabWeaponKey from,float end)
        {
            if(!editObject)return;
            var next=ObjectKeys().First(k=>Mathf.Abs(k.time-end)<LabTimeline.TimeEpsilon);
            bool left=!from.breakLeftContact&&LabCoordinatedPaths.Contact(from,HandSide.Left)!=null&&LabCoordinatedPaths.Contact(next,HandSide.Left)!=null,
                right=!from.breakRightContact&&LabCoordinatedPaths.Contact(from,HandSide.Right)!=null&&LabCoordinatedPaths.Contact(next,HandSide.Right)!=null;
            EditorGUILayout.LabelField(left||right?"CURVA COMÚN · pieza + "+(left&&right?"ambas manos":left?"mano izquierda":"mano derecha"):"Esta curva todavía mueve solo la pieza",EditorStyles.boldLabel);
            if(left||right)
            {
                EditorGUILayout.HelpBox("Cambiar Directo, Arco o Libre y arrastrar sus controles actualiza también las manos enlazadas al soltar. Los dedos siguen teniendo su propia transición.",MessageType.Info);
                if(GUILayout.Button("Separar las manos de este tramo"))Safe(()=>
                {
                    if(!CanLeaveFrame())return;
                    Undo.RecordObject(draft,"Separar recorrido combinado");
                    from.breakLeftContact=from.breakRightContact=true;dirty=true;SyncSelection();
                });
            }
            else
            {
                if(followHands==0)EditorGUILayout.HelpBox("Elige Mover junto con → Mano izquierda/derecha para unir también el recorrido, no solo los extremos.",MessageType.None);
                using(new EditorGUI.DisabledScope(followHands==0))
                    if(GUILayout.Button("Unir mano y pieza en este tramo"))Safe(()=>LinkObjectSegment(from,next));
            }
        }
        private void LinkObjectSegment(LabWeaponKey from,LabWeaponKey to,bool confirmed=false)
        {
            if(!CanLeaveFrame())return;
            var hands=FollowingSides().ToArray();if(hands.Length==0)throw new InvalidOperationException("Elige la mano que acompaña a la pieza.");
            if(!confirmed&&!EditorUtility.DisplayDialog("Unir recorrido","Se conservarán los contactos en ambos extremos y se recompilará la muñeca para acompañar la curva de esta pieza. Los dedos y otras pistas no cambian. Puedes deshacer.","Unir recorrido","Cancelar"))return;
            var data=new System.Collections.Generic.Dictionary<(LabWeaponKey,HandSide),LabPathContact>();
            var candidate=LabAssetIO.Clone(draft);
            try
            {
                foreach(var key in new[]{from,to})
                {
                    preview.Evaluate(draft,key.time,rawFingers:true);var target=ObjectTarget;
                    foreach(var hand in hands)
                    {
                        var chain=hand==HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
                        var wrist=chain.target!=null?chain.target:chain.hand;
                        var linked=LabCoordinatedPaths.At(draft,hand,key.time);
                        data[(key,hand)]=linked!=null&&linked.objectId==objectId?SegmentContact(linked,key.time):
                            new LabPathContact{active=true,position=target.InverseTransformPoint(wrist.position),euler=(Quaternion.Inverse(target.rotation)*wrist.rotation).eulerAngles};
                    }
                }
                var keys=TakeObjectKeys(candidate,objectId);
                foreach(var item in data)LabCoordinatedPaths.SetContact(keys.First(k=>k.time==item.Key.Item1.time),item.Key.Item2,item.Value);
                var first=keys.First(k=>k.time==from.time);
                foreach(var hand in hands){if(hand==HandSide.Left)first.breakLeftContact=false;else first.breakRightContact=false;}
                LabCoordinatedPaths.Compile(candidate,preview);
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
                Undo.RecordObject(draft,"Unir recorrido");Undo.RecordObject(draft.action,"Unir recorrido");
                ApplyCoordinatedCandidate(candidate,objectId,LabCoordinatedPaths.Segments(candidate).Select(s=>s.side).Distinct().ToArray());
                Undo.CollapseUndoOperations(group);dirty=true;SyncSelection();
            }
            finally{LabAssetIO.DestroyDraft(candidate);EvaluateNow(Pending);}
        }
    }
}
