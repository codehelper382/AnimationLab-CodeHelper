using System;
using System.IO;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Only run in the isolated validation project. Exits that Editor after actual-window screenshots.
    public static class AnimationLabGripFrameCapture
    {
        private static AnimationLabWindow window;
        private static int stage,ticks;
        private static string uiError;
        public static void Run()
        {
            if(Application.isBatchMode)throw new InvalidOperationException("La captura necesita un editor con ventana.");
            Application.logMessageReceived+=Log;
            window=ScriptableObject.CreateInstance<AnimationLabWindow>();window.titleContent=new GUIContent("Animation Lab · agarres y fotogramas");
            window.position=new Rect(35,60,Mathf.Clamp(Screen.currentResolution.width-100,1050,1520),Mathf.Clamp(Screen.currentResolution.height-140,760,940));window.ShowUtility();
            LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"LoadReloadReference",LabHandSource.Reload);
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            LabPreview.Call(window,"SelectTrack",1,LabTimeline.Time(40,take.action.duration));
            var preview=(LabPreview)LabPreview.Get(window,"preview");
            LabPreview.Call(window,"SetFingerPose",LabGripTools.Preset(preview.Animator,HandGripToolkit.HandSide.Left,2));
            LabPreview.Call(window,"SetFingerTransition",LabFingerBlend.Smooth,10);
            LabPreview.Call(window,"CommitEdits",true,false);
            LabPreview.Set(window,"studioTab",1);LabPreview.Set(window,"scroll",new Vector2(0,190));
            preview.distance=1;
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
                    File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode","AnimationLab.GripFrames.UI."+stage+".png"),texture.EncodeToPNG());
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
                var take=(AnimationLabTake)LabPreview.Get(window,"draft");
                if(stage==0){stage++;LabPreview.Call(window,"Seek",LabTimeline.Time(4,take.action.duration));return;}
                if(stage==1){stage++;LabPreview.Call(window,"Seek",LabTimeline.Time(40,take.action.duration));LabPreview.Set(window,"scroll",new Vector2(0,700));return;}
                if(stage==2){stage++;LabPreview.Call(window,"CopyFrame",false);LabPreview.Call(window,"Seek",LabTimeline.Time(80,take.action.duration));LabPreview.Call(window,"PasteFrame");LabPreview.Set(window,"scroll",new Vector2(0,190));return;}
                if(stage==3){stage++;LabPreview.Set(window,"firstPerson",true);LabPreview.Set(window,"pitch",-12f);LabPreview.Set(window,"yaw",10f);LabPreview.Set(window,"aim",true);return;}
                Debug.Log("[AnimationLabGripFrameCapture] PASS: actual grip transitions, initial-grip library and frame-copy controls rendered without GUI errors.");Finish(0);
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        private static void Finish(int code)
        {
            EditorApplication.update-=Update;Application.logMessageReceived-=Log;
            if(window!=null)
            {
                LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"previewingKey",false);
                UnityEngine.Object.DestroyImmediate(window);window=null;
            }
            EditorApplication.Exit(code);
        }
    }
}
