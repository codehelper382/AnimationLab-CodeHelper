using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private AnimationLabTake gripTransitionTake;
        private float gripTransitionTime;
        private bool gripTransitionActive,gripTransitionPlaying;
        private float GripTransitionWeight
        {
            get
            {
                if(gripTransitionTake==null)return 0;
                var keys=side==HandSide.Left?gripTransitionTake.left:gripTransitionTake.right;
                var target=keys.FirstOrDefault(k=>k.fingers!=null&&Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
                var before=keys.Where(k=>k.fingers!=null&&k.time<progress-LabTimeline.TimeEpsilon).OrderBy(k=>k.time).LastOrDefault();
                return before==null||target==null?0:LabFingerKeyTools.Blend(before,target,gripTransitionTime);
            }
        }
        private void StopGripTransitionPreview()
        {
            gripTransitionActive=gripTransitionPlaying=false;
            LabAssetIO.DestroyDraft(gripTransitionTake);gripTransitionTake=null;
        }
        private void BuildGripTransitionPreview()
        {
            StopGripTransitionPreview();
            var candidate=LabAssetIO.Clone(draft);
            try
            {
                var keys=side==HandSide.Left?candidate.left:candidate.right;
                if(NeedsInitialGrip(fingerBlendMode))
                {
                    var initial=CaptureInitialGrip();LabTimeline.RecordFingers(keys,0,initial);
                    var baseKey=LabTimeline.FindFingers(keys,0);baseKey.fingerBlend=LabFingerBlend.Step;
                }
                var pose=fingers!=null?Instantiate(fingers):null;
                if(pose==null)throw new InvalidOperationException("Elige primero el agarre de llegada.");
                pose.hideFlags=HideFlags.HideAndDontSave;LabAssetIO.NormalizeFingerPose(pose,preview);
                var old=LabTimeline.FindFingers(keys,progress)?.fingers;if(old!=null)DestroyImmediate(old);
                LabTimeline.RecordFingers(keys,progress,pose);
                var key=LabTimeline.FindFingers(keys,progress);key.fingerBlend=fingerBlendMode;key.fingerBlendDuration=fingerTransitionFrames/(60f*draft.action.duration);
                gripTransitionTake=candidate;candidate=null;gripTransitionActive=true;
            }
            finally{LabAssetIO.DestroyDraft(candidate);}
        }
        private void SeekGripTransitionPreview(int frame)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba actual antes de probar agarres.");
            BuildGripTransitionPreview();playing=false;
            gripTransitionTime=LabTimeline.Time(Mathf.Clamp(frame,0,CurrentFrame),draft.action.duration);
            EvaluateNow(false);Repaint();
        }
        private void DrawGripTransitionPreview(int previousFrame,int start)
        {
            Rect bar=GUILayoutUtility.GetRect(100,42,GUILayout.ExpandWidth(true));
            float fraction=CurrentFrame>previousFrame?Mathf.InverseLerp(previousFrame,CurrentFrame,start):1;
            EditorGUI.DrawRect(bar,new Color(.13f,.34f,.28f));
            EditorGUI.DrawRect(new Rect(bar.x+bar.width*fraction,bar.y,bar.width*(1-fraction),bar.height),new Color(.15f,.32f,.5f));
            GUI.Label(new Rect(bar.x+5,bar.y+3,bar.width-10,18),$"MANTENER hasta F {start}   →   ADAPTAR hasta F {CurrentFrame}",EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(bar.x+5,bar.y+22,bar.width-10,18),$"Base F {previousFrame}                                       Llegada F {CurrentFrame}",EditorStyles.whiteMiniLabel);
            EditorGUILayout.LabelField("2 · Mira cómo cambia antes de guardar",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(CurrentFrame<=0))
            {
                int frame=gripTransitionActive?LabTimeline.Frame(gripTransitionTime,draft.action.duration):CurrentFrame;
                int selected=EditorGUILayout.IntSlider("F de prueba",frame,previousFrame,Mathf.Max(previousFrame,CurrentFrame));
                if(selected!=frame)Safe(()=>SeekGripTransitionPreview(selected));
                if(gripTransitionActive)EditorGUILayout.LabelField("Adaptación del agarre",$"{GripTransitionWeight:P0}",EditorStyles.boldLabel);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Base"))Safe(()=>SeekGripTransitionPreview(previousFrame));
                    if(GUILayout.Button("Inicio cambio"))Safe(()=>SeekGripTransitionPreview(start));
                    if(GUILayout.Button("50%"))Safe(()=>SeekGripTransitionPreview((start+CurrentFrame)/2));
                    if(GUILayout.Button("Llegada"))Safe(()=>SeekGripTransitionPreview(CurrentFrame));
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(gripTransitionPlaying?"Pausar prueba":"▶ Ver proceso completo"))Safe(()=>
                    {
                        if(gripTransitionPlaying){gripTransitionPlaying=false;return;}
                        SeekGripTransitionPreview(previousFrame);gripTransitionPlaying=true;
                    });
                    if(gripTransitionActive&&GUILayout.Button("Volver a editar destino"))
                    {StopGripTransitionPreview();EvaluateNow(Pending);}
                }
            }
            EditorGUILayout.HelpBox(gripTransitionActive?
                $"PRUEBA SIN GUARDAR · viendo F {LabTimeline.Frame(gripTransitionTime,draft.action.duration)}. El destino sigue siendo F {CurrentFrame}. Pulsa Guardar dedos para aplicar.":
                "La prueba anima los dedos en el visor, sin guardar ni cambiar la posición de las claves. Usa Base → Inicio cambio → 50% → Llegada.",MessageType.None);
        }
        private void AdvanceGripTransitionPreview(float dt)
        {
            if(!gripTransitionActive||!gripTransitionPlaying)return;
            gripTransitionTime+=dt/Mathf.Max(.01f,draft.action.duration);
            if(gripTransitionTime>=progress){gripTransitionTime=progress;gripTransitionPlaying=false;}
        }
    }
}
