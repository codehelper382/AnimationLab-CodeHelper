using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private sealed class FrameCopy
        {
            internal int track,frame;
            internal bool wholeHand;
            internal string equipmentId,objectId;
            internal float fingerFrames;
            internal LabHandKey hand;
            internal LabWeaponKey item;
            internal void Dispose(){if(hand?.fingers!=null)DestroyImmediate(hand.fingers);}
        }
        private FrameCopy frameClipboard;
        private int frameCopyScope;
        private void DrawFrameClipboard()
        {
            using(new EditorGUI.DisabledScope(HasTakeTrial || studioTab == 3))
            using(new EditorGUILayout.HorizontalScope())
            {
                frameCopyScope=EditorGUILayout.Popup(frameCopyScope,new[]{"Pista seleccionada","Mano: posición + dedos"},GUILayout.Width(165));
                if(GUILayout.Button("Copiar F "+CurrentFrame,GUILayout.Width(110)))Safe(()=>CopyFrame(frameCopyScope==1));
                using(new EditorGUI.DisabledScope(frameClipboard==null))
                    if(GUILayout.Button("Pegar en F "+CurrentFrame,GUILayout.Width(110)))Safe(PasteFrame);
                GUILayout.Label(frameClipboard==null?"Copiar no modifica la toma.":$"Copiado: {TrackNames[frameClipboard.track]} · F {frameClipboard.frame}"+(frameClipboard.wholeHand?" · mano completa":""),EditorStyles.miniLabel);
            }
        }
        private void FrameClipboardShortcuts()
        {
            var e=Event.current;
            if(HasTakeTrial||studioTab==3||EditorGUIUtility.editingTextField||e.type!=EventType.KeyDown||!(e.control||e.command)||e.alt)return;
            if(e.keyCode==KeyCode.C){Safe(()=>CopyFrame(frameCopyScope==1));e.Use();}
            else if(e.keyCode==KeyCode.V&&frameClipboard!=null){Safe(PasteFrame);e.Use();}
        }
        private static LabHandKey CopyHandData(LabHandKey k)=>new LabHandKey{time=k.time,position=k.position,euler=k.euler,pathMode=k.pathMode,bend=k.bend,
            outgoing=k.outgoing,incoming=k.incoming,departureSpeed=k.departureSpeed,arrivalSpeed=k.arrivalSpeed,
            fingerBlend=k.fingerBlend,fingerBlendDuration=k.fingerBlendDuration,fingerLabel=k.fingerLabel};
        private void CopyFrame(bool wholeHand)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de copiar fotogramas.");
            if(!CanLeaveFrame())return;
            if(wholeHand&&(editObject||editWeapon))throw new InvalidOperationException("Selecciona una mano para copiar posición y dedos juntos, o elige Pista seleccionada.");
            SyncSelection();
            var copy=new FrameCopy{track=Track,wholeHand=wholeHand,equipmentId=draft.equipmentId,objectId=editObject?objectId:null,frame=CurrentFrame};
            try
            {
                if(editObject||editWeapon)
                {
                    if(editObject&&ObjectTarget==null)throw new InvalidOperationException("Selecciona una pieza antes de copiar.");
                    var key=(editObject?ObjectKeys():draft.weapon)?.FirstOrDefault(k=>Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
                    copy.item=key!=null?LabObjectAuthoring.CloneKeys(new System.Collections.Generic.List<LabWeaponKey>{key})[0]:
                        new LabWeaponKey{position=keyPosition,euler=keyRotation};
                }
                else
                {
                    var key=LabTimeline.Find(HandKeys,progress);
                    copy.hand=key!=null?CopyHandData(key):new LabHandKey{fingerBlend=LabFingerBlend.Smooth,fingerBlendDuration=10/(60f*draft.action.duration)};
                    copy.hand.position=keyPosition;copy.hand.euler=keyRotation;
                    if(wholeHand||editFingers)
                    {
                        if(key?.fingers!=null)copy.hand.fingers=Instantiate(key.fingers);
                        else
                        {
                            copy.hand.fingerBlend=LabFingerBlend.Smooth;copy.hand.fingerBlendDuration=10/(60f*draft.action.duration);
                            float incoming=draft.action.blendIn,outgoing=draft.action.blendOut;
                            try
                            {
                                draft.action.blendIn=draft.action.blendOut=0;preview.Evaluate(draft,progress,pitch,yaw,aim,rawFingers:true);
                                copy.hand.fingers=CreateInstance<HandPoseData>();copy.hand.fingers.Side=side;
                                if(!copy.hand.fingers.Capture(preview.Animator))throw new InvalidOperationException("No hay dedos compatibles para copiar.");
                                LabAssetIO.NormalizeFingerPose(copy.hand.fingers,preview);
                            }
                            finally{draft.action.blendIn=incoming;draft.action.blendOut=outgoing;EvaluateNow(false);}
                        }
                        copy.hand.fingers.hideFlags=HideFlags.HideAndDontSave;
                        copy.fingerFrames=copy.hand.fingerBlendDuration*draft.action.duration*60;
                    }
                }
                frameClipboard?.Dispose();frameClipboard=copy;
                notice=$"F {CurrentFrame} copiado como datos independientes. Ve al destino y pulsa Pegar.";
            }
            catch{copy.Dispose();throw;}
        }
        private void PasteFrame()
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de pegar fotogramas.");
            var copy=frameClipboard;
            if(copy==null)throw new InvalidOperationException("Copia primero un fotograma.");
            bool compatible=copy.equipmentId==draft.equipmentId&&(copy.wholeHand?
                !editObject&&!editWeapon&&copy.track/2==(int)side:copy.track==Track)&&
                (!editObject||copy.objectId==objectId&&ObjectTarget!=null);
            if(!compatible)throw new InvalidOperationException("Pega en la misma pista/mano y equipo; para piezas selecciona la misma pieza. No se hace espejo automático.");
            if(!CanLeaveFrame())return;
            bool wrist=!editObject&&!editWeapon&&(!editFingers||copy.wholeHand);
            bool finger=!editObject&&!editWeapon&&(editFingers||copy.wholeHand);
            if(wrist&&LabCoordinatedPaths.At(draft,side,progress)!=null)
                throw new InvalidOperationException("Esta mano comparte recorrido con una pieza. Pega la posición en esa pieza o separa el tramo antes de pegar solo la mano.");
            if((wrist||editWeapon)&&!ApprovePositionReplacement(true))return;
            if(editObject&&SelectedProp==null&&(ObjectKeys()?.Count??0)==0&&draft.action.weaponClip!=null&&
                AnimationUtility.GetCurveBindings(draft.action.weaponClip).Any(b=>b.path==objectId.Substring(5)&&b.type==typeof(Transform))&&
                !EditorUtility.DisplayDialog("Pegar posición de pieza","Los puntos reemplazarán el movimiento de esta pieza. Las otras pistas y el clip fuente se conservan.","Pegar en esta pieza","Cancelar"))return;
            HandPoseData pose=null,initial=null;
            try
            {
                if(finger)
                {
                    pose=Instantiate(copy.hand.fingers);pose.hideFlags=HideFlags.HideAndDontSave;
                    if(NeedsInitialGrip(copy.hand.fingerBlend))initial=CaptureInitialGrip();
                }
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
                Undo.RecordObject(draft,"Pegar fotograma");Undo.RecordObject(draft.action,"Pegar fotograma");
                if(editObject||editWeapon)
                {
                    var keys=editObject?ObjectKeys(true):draft.weapon;var key=keys.FirstOrDefault(k=>Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
                    if(key==null){key=new LabWeaponKey{time=progress};if(editObject)InheritObjectContacts(key);keys.Add(key);}
                    key.position=copy.item.position;key.euler=copy.item.euler;key.pathMode=copy.item.pathMode;key.bend=copy.item.bend;
                    key.outgoing=copy.item.outgoing;key.incoming=copy.item.incoming;key.departureSpeed=copy.item.departureSpeed;key.arrivalSpeed=copy.item.arrivalSpeed;
                    keys.Sort((a,b)=>a.time.CompareTo(b.time));if(editWeapon)LabAssetIO.BuildWeaponMotion(draft);
                }
                else
                {
                    if(wrist)
                    {
                        preview.EnableHand(side);LabTimeline.RecordWrist(HandKeys,HandMotion,progress,copy.hand.position,copy.hand.euler);
                        var key=LabTimeline.Find(HandKeys,progress);key.pathMode=copy.hand.pathMode;key.bend=copy.hand.bend;
                        key.outgoing=copy.hand.outgoing;key.incoming=copy.hand.incoming;key.departureSpeed=copy.hand.departureSpeed;key.arrivalSpeed=copy.hand.arrivalSpeed;
                        if(side==HandSide.Left)draft.action.leftHandTargetMotion=null;
                        LabAssetIO.BuildMotion(HandKeys,HandMotion);
                    }
                    if(finger)
                    {
                        var previous=LabTimeline.Find(HandKeys,progress)?.fingers;if(previous!=null)retired.Add(previous);
                        AddInitialGrip(initial);initial=null;
                        LabTimeline.RecordFingers(HandKeys,progress,pose);retired.Add(pose);pose=null;
                        var key=LabTimeline.Find(HandKeys,progress);key.fingerBlend=copy.hand.fingerBlend;
                        key.fingerLabel=copy.hand.fingerLabel;
                        key.fingerBlendDuration=copy.fingerFrames/(60f*draft.action.duration);
                    }
                }
                if(editObject||editWeapon)RefreshCoordinatedPaths();
                Undo.CollapseUndoOperations(group);dirty=true;playing=false;
                notice=$"F {copy.frame} pegado en F {CurrentFrame}. Solo se modificó "+(copy.wholeHand?"la mano seleccionada.":"la pista seleccionada.");
                SyncSelection();
            }
            finally{if(pose!=null)DestroyImmediate(pose);if(initial!=null)DestroyImmediate(initial);}
        }
    }
}
