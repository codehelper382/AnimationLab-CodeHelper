using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabDynamicsCapture
    {
        private static AnimationLabWindow window;
        private static int stage,ticks;
        private static string uiError;
        public static void Run()
        {
            if(Application.isBatchMode)throw new InvalidOperationException("La captura requiere un editor con ventana.");
            Application.logMessageReceived+=Log;
            window=ScriptableObject.CreateInstance<AnimationLabWindow>();window.titleContent=new GUIContent("BASE CORRECTA · movimiento y agarres");
            window.position=new Rect(35,60,Mathf.Clamp(Screen.currentResolution.width-100,1050,1520),Mathf.Clamp(Screen.currentResolution.height-140,760,940));window.ShowUtility();
            LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"Load",AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath));
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            LabPreview.Call(window,"SelectTrack",4,LabTimeline.Time(112,take.action.duration));LabPreview.Set(window,"studioTab",1);
            LabPreview.Call(window,"StartWeaponMotionTrial");
            var preview=(LabPreview)LabPreview.Get(window,"preview");preview.distance=1;
            preview.pan=preview.Objects.ResolvePart("AK12Rig/Root/Mag").position-(preview.Equipment.spine.animationRoot.position+Vector3.up*1.35f);
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
                var rect=window.position;var image=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGB24,false);
                try
                {
                    image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position,image.width,image.height));image.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode","AnimationLab.Dynamics.UI."+stage+".png"),image.EncodeToPNG());
                }
                finally{UnityEngine.Object.DestroyImmediate(image);}
                var take=(AnimationLabTake)LabPreview.Get(window,"draft");
                if(stage==0){stage++;LabPreview.Set(window,"weaponMotionBefore",true);return;}
                if(stage==1){stage++;LabPreview.Set(window,"weaponMotionBefore",false);LabPreview.Set(window,"firstPerson",true);LabPreview.Set(window,"pitch",-12f);LabPreview.Set(window,"yaw",10f);LabPreview.Set(window,"aim",true);return;}
                if(stage==2)
                {
                    stage++;LabPreview.Call(window,"CancelWeaponMotionTrial",true);LabPreview.Set(window,"firstPerson",false);
                    LabPreview.Call(window,"SelectTrack",1,LabTimeline.Time(150,take.action.duration));LabPreview.Set(window,"scroll",new Vector2(0,170));return;
                }
                if(stage==3){stage++;LabPreview.Call(window,"Seek",LabTimeline.Time(151,take.action.duration));return;}
                if(stage==4)
                {
                    stage++;LabPreview.Call(window,"Seek",LabTimeline.Time(150,take.action.duration));
                    LabPreview.Call(window,"SetFingerPose",UnityEngine.Object.Instantiate(take.left.First(k=>k.fingers!=null&&k.time==0).fingers));
                    LabPreview.Set(window,"fingerPoseLabel","Base guardada en F0");LabPreview.Call(window,"HoldFingerGripToEnd",true);
                    LabPreview.Call(window,"Seek",LabTimeline.Time(180,take.action.duration));return;
                }
                Debug.Log("[AnimationLabDynamicsCapture] PASS: real BASE CORRECTA before/after, natural weapon trial, first-person look/Aim, saved F150 vs no-key F151 and held grip after F150 without GUI errors.");Finish(0);
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        private static void Finish(int code)
        {
            EditorApplication.update-=Update;Application.logMessageReceived-=Log;
            if(window!=null)
            {
                LabPreview.Call(window,"CancelWeaponMotionTrial",false);LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);window=null;
            }
            EditorApplication.Exit(code);
        }
    }
}
