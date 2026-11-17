using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private LabFingerBlend fingerBlendMode=LabFingerBlend.Smooth;
        private int fingerTransitionFrames=10;
        private int arrivalGrip;
        private string fingerPoseLabel="Pose editada";
        private void SyncFingerTransition()
        {
            var key=HandKeys.FirstOrDefault(k=>k.fingers!=null&&Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
            if(key==null)key=HandKeys.Where(k=>k.fingers!=null&&k.time<=progress).OrderBy(k=>k.time).LastOrDefault();
            fingerBlendMode=key!=null?key.fingerBlend:LabFingerBlend.Smooth;
            fingerTransitionFrames=key!=null?Mathf.RoundToInt(key.fingerBlendDuration*draft.action.duration*60):10;
            fingerPoseLabel=!string.IsNullOrEmpty(key?.fingerLabel)?key.fingerLabel:"Pose guardada";
            arrivalGrip=0; // Picker is a command for a new pose, not a remembered description of saved data.
        }
        private void DrawFingerTransition()
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Transición hacia este agarre",EditorStyles.boldLabel);
            var previous=HandKeys.Where(k=>k.fingers!=null&&k.time<progress-LabTimeline.TimeEpsilon).OrderBy(k=>k.time).LastOrDefault();
            int previousFrame=previous!=null?LabTimeline.Frame(previous.time,draft.action.duration):0;
            EditorGUI.BeginChangeCheck();
            var mode=(LabFingerBlend)EditorGUILayout.Popup("Cómo llegar",(int)fingerBlendMode,new[]{"Original (compatible)","Lineal","Suave","Instantánea"});
            int frames=fingerTransitionFrames;
            using(new EditorGUI.DisabledScope(mode==LabFingerBlend.Step||mode==LabFingerBlend.Legacy))
            {
                int start=EditorGUILayout.DelayedIntField(new GUIContent("Mantener hasta F","Hasta este fotograma se conserva el agarre anterior. Después empieza a adaptarse al nuevo."),Mathf.Max(previousFrame,CurrentFrame-frames));
                frames=CurrentFrame-Mathf.Clamp(start,previousFrame,CurrentFrame);
            }
            EditorGUILayout.LabelField("Llegar al agarre en F",CurrentFrame.ToString(),EditorStyles.boldLabel);
            if(EditorGUI.EndChangeCheck())Safe(()=>SetFingerTransition(mode,frames));
            int startFrame=Mathf.Max(previousFrame,CurrentFrame-Mathf.Max(0,fingerTransitionFrames));
            if(fingerBlendMode==LabFingerBlend.Legacy)startFrame=previousFrame;
            else if(fingerBlendMode==LabFingerBlend.Step)startFrame=CurrentFrame;
            string hint=fingerBlendMode==LabFingerBlend.Step?$"Mantener el agarre anterior y cambiar en F {CurrentFrame}.":
                fingerBlendMode==LabFingerBlend.Legacy?"Interpolación anterior entre claves. Elige Suave para decidir cuándo empieza el cambio.":
                $"F {previousFrame}–{startFrame}: MISMO AGARRE\nF {startFrame}–{CurrentFrame}: ADAPTAR POCO A POCO\nF {CurrentFrame}: AGARRE DE LLEGADA";
            bool exact=HandKeys.Any(k=>k.fingers!=null&&Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
            EditorGUILayout.HelpBox((!exact?"Al guardar un punto aquí:\n":"")+hint,MessageType.Info);
            using(new EditorGUI.DisabledScope(CurrentFrame<=previousFrame))
                if(GUILayout.Button("Mitad mantener / mitad adaptar"))Safe(()=>SetFingerTransition(LabFingerBlend.Smooth,Mathf.Max(1,(CurrentFrame-previousFrame)/2)));
            DrawGripTransitionPreview(previousFrame,startFrame);
        }
        private void DrawGripArrivalPicker()
        {
            LabStudioUI.Section("1 · Elige el agarre de llegada","La forma que quieres alcanzar en el fotograma seleccionado. La transición se configura debajo.");
            string[] names=new[]{"Editar pose actual","Base inicial · sostener arma"}.Concat(LabGripTools.Names).Concat(new[]{"Mi tipo / variante"}).ToArray();
            int next=EditorGUILayout.Popup("Agarre de llegada",arrivalGrip,names);
            if(next!=arrivalGrip){arrivalGrip=next;Safe(ApplyArrivalGrip);}
            if(arrivalGrip==names.Length-1)selectedGripType=(LabGripPreset)EditorGUILayout.ObjectField("Mi tipo / variante",selectedGripType,typeof(LabGripPreset),false);
            if(arrivalGrip>0&&GUILayout.Button("Usar este agarre como destino"))Safe(ApplyArrivalGrip);
        }
        private void ApplyArrivalGrip()
        {
            if(arrivalGrip==0)return;
            if(arrivalGrip==1){UseInitialGrip();return;}
            if(arrivalGrip<2+LabGripTools.Names.Length){SetFingerPose(LabGripTools.Preset(preview.Animator,side,arrivalGrip-2));fingerPoseLabel=LabGripTools.Names[arrivalGrip-2];return;}
            if(selectedGripType==null)return;
            if(selectedGripType.side!=side||selectedGripType.fingers==null)throw new InvalidOperationException("Elige un tipo de agarre de esta mano.");
            SetFingerPose(Instantiate(selectedGripType.fingers));
            fingerPoseLabel=selectedGripType.displayName;
        }
        private void SetFingerTransition(LabFingerBlend mode,int frames)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de editar dedos.");
            StopGripTransitionPreview();
            if(!Enum.IsDefined(typeof(LabFingerBlend),mode))throw new InvalidOperationException("Transición de dedos no válida.");
            fingerBlendMode=mode;fingerTransitionFrames=Mathf.Clamp(frames,0,LabTimeline.LastFrame(draft.action.duration));
            var key=HandKeys.FirstOrDefault(k=>k.fingers!=null&&Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
            if(key==null||fingersEdited)return;
            var initial=NeedsInitialGrip(mode)?CaptureInitialGrip():null;
            Undo.IncrementCurrentGroup();Undo.RecordObject(draft,"Editar transición de agarre");
            AddInitialGrip(initial);
            key.fingerBlend=mode;key.fingerBlendDuration=fingerTransitionFrames/(60f*draft.action.duration);
            dirty=true;playing=false;EvaluateNow(false);
        }
        private bool NeedsInitialGrip(LabFingerBlend mode)=>mode!=LabFingerBlend.Legacy&&progress>LabTimeline.TimeEpsilon&&
            !HandKeys.Any(k=>k.fingers!=null&&k.time<progress-LabTimeline.TimeEpsilon);
        private void AddInitialGrip(HandPoseData initial)
        {AddInitialGrip(side,initial);}
        private void AddInitialGrip(HandSide hand,HandPoseData initial)
        {
            if(initial==null)return;
            var keys=hand==HandSide.Left?draft.left:draft.right;
            LabTimeline.RecordFingers(keys,0,initial);
            var key=LabTimeline.FindFingers(keys,0);key.fingerBlend=LabFingerBlend.Step;key.fingerBlendDuration=0;
            retired.Add(initial);
        }
        // Sample the loaded animation, not a previously authored finger key or a pending preset.
        private HandPoseData CaptureInitialGrip()
        {
            var source=LabAssetIO.Clone(draft);HandPoseData result=null;
            try
            {
                var keys=side==HandSide.Left?source.left:source.right;
                foreach(var key in keys)
                {if(key.fingers!=null)DestroyImmediate(key.fingers);key.fingers=null;}
                source.action.blendIn=source.action.blendOut=0;
                preview.Evaluate(source,0,rawFingers:true);
                result=ScriptableObject.CreateInstance<HandPoseData>();result.Side=side;result.hideFlags=HideFlags.HideAndDontSave;
                if(!result.Capture(preview.Animator))throw new InvalidOperationException("La animación cargada no tiene dedos compatibles.");
                LabAssetIO.NormalizeFingerPose(result,preview);return result;
            }
            catch{if(result!=null)DestroyImmediate(result);throw;}
            finally{LabAssetIO.DestroyDraft(source);EvaluateNow(Pending);}
        }
        private void UseInitialGrip()
        {
            SetFingerPose(CaptureInitialGrip());
            fingerPoseLabel="Base inicial · sostener arma";
            gripName=preview.Slot.definition.displayName+" · sostener arma · "+(side==HandSide.Left?"izquierda":"derecha");
            gripCategory="Desde animación";
            notice="Agarre inicial de la animación cargada capturado como copia. Puedes editarlo y guardarlo como un tipo o una variante.";
        }
    }
}
