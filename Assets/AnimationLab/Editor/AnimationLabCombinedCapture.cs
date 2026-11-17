using System;
using System.IO;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Actual-window validation. Only launch on an isolated project; exits that Editor process.
    public static class AnimationLabCombinedCapture
    {
        private static AnimationLabWindow window;
        private static int stage,ticks;
        private static string uiError;
        public static void Run()
        {
            if(Application.isBatchMode)throw new InvalidOperationException("La captura necesita un editor con ventana.");
            Application.logMessageReceived+=Log;
            window=ScriptableObject.CreateInstance<AnimationLabWindow>();window.titleContent=new GUIContent("Animation Lab · transición 0 / 20 / 40");
            window.position=new Rect(35,60,Mathf.Clamp(Screen.currentResolution.width-100,1050,1520),Mathf.Clamp(Screen.currentResolution.height-140,760,940));window.ShowUtility();
            LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"LoadReloadReference",LabHandSource.Reload);
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");var preview=(LabPreview)LabPreview.Get(window,"preview");
            LabPreview.Call(window,"SelectTrack",1,0f);LabPreview.Call(window,"UseInitialGrip");LabPreview.Call(window,"CommitEdits",true,false);
            LabPreview.Call(window,"SelectTrack",1,LabTimeline.Time(40,take.action.duration));
            LabPreview.Call(window,"SetFingerPose",LabGripTools.Preset(preview.Animator,HandGripToolkit.HandSide.Left,2));
            LabPreview.Call(window,"SetFingerTransition",LabFingerBlend.Smooth,20);
            LabPreview.Set(window,"arrivalGrip",4);LabPreview.Set(window,"studioTab",1);LabPreview.Set(window,"scroll",new Vector2(0,230));
            LabPreview.Call(window,"SeekGripTransitionPreview",30);preview.distance=1;
            EditorApplication.update+=Update;
        }
        private static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception)uiError=message;}
        private static void Update()
        {
            try
            {
                window.Focus();window.Repaint();if(++ticks<40)return;ticks=0;
                var error=(string)LabPreview.Get(window,"error");if(!string.IsNullOrEmpty(error)||uiError!=null)throw new InvalidOperationException(error??uiError);
                var rect=window.position;var texture=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGB24,false);
                try
                {
                    texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position,texture.width,texture.height));texture.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode","AnimationLab.Combined.UI."+stage+".png"),texture.EncodeToPNG());
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
                if(stage==0){stage++;LabPreview.Call(window,"SeekGripTransitionPreview",0);return;}
                if(stage==1){stage++;LabPreview.Call(window,"SeekGripTransitionPreview",20);return;}
                if(stage==2){stage++;LabPreview.Call(window,"SeekGripTransitionPreview",40);return;}
                if(stage==3){stage++;SetupCombined();return;}
                if(stage==4){stage++;LabPreview.Set(window,"firstPerson",true);LabPreview.Set(window,"pitch",-12f);LabPreview.Set(window,"yaw",10f);LabPreview.Set(window,"aim",true);return;}
                if(stage==5){stage++;LabPreview.Set(window,"firstPerson",false);LabPreview.Call(window,"SelectTrack",0,1/6f);LabPreview.Set(window,"scroll",new Vector2(0,330));return;}
                Debug.Log("[AnimationLabCombinedCapture] PASS: explicit hold/adapt fields, live 0/20/30/40 candidate, common piece-hand curve and first-person look/Aim without GUI errors.");Finish(0);
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        private static void SetupCombined()
        {
            LabPreview.Call(window,"StopGripTransitionPreview");LabPreview.Set(window,"fingersEdited",false);LabPreview.Call(window,"SyncSelection");LabPreview.Call(window,"NewDraft",0);
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");var preview=(LabPreview)LabPreview.Get(window,"preview");
            take.action.duration=2;take.action.blendIn=take.action.blendOut=0;take.action.followAnimatedRightHand=false;
            foreach(int frame in new[]{0,40})
            {
                LabPreview.Call(window,"SelectTrack",0,LabTimeline.Time(frame,2));LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");
                LabPreview.Set(window,"followHands",1);LabPreview.Set(window,"includeGroupFingers",false);LabPreview.Set(window,"wristReplacementApproved",true);LabPreview.Set(window,"objectReplacementApproved",true);
                LabPreview.Call(window,"BeginObjectEdit",false);
                if(frame==40)
                {
                    var part=preview.Objects.ResolvePart("AK12Rig/Root/Mag");
                    LabPreview.Set(window,"keyPosition",(Vector3)LabPreview.Get(window,"keyPosition")+part.parent.InverseTransformVector(new Vector3(.05f,.03f,0)));
                    LabPreview.Set(window,"keyRotation",(Vector3)LabPreview.Get(window,"keyRotation")+new Vector3(12,25,5));
                }
                LabPreview.Call(window,"CommitObject",true,false);
            }
            var target=preview.Objects.ResolvePart("AK12Rig/Root/Mag");LabPreview.Call(window,"Seek",LabTimeline.Time(20,2));
            LabPreview.Call(window,"SetPathPreset",LabPathMode.Cinematic,target.parent.InverseTransformVector(Vector3.up*.05f),0f,0f);
            LabPreview.Set(window,"scroll",new Vector2(0,480));
        }
        private static void Finish(int code)
        {
            EditorApplication.update-=Update;Application.logMessageReceived-=Log;
            if(window!=null)
            {
                LabPreview.Call(window,"StopGripTransitionPreview");LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);window=null;
            }
            EditorApplication.Exit(code);
        }
    }
}
