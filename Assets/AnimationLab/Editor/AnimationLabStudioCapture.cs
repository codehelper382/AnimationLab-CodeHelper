using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Run on the isolated validation project without -batchmode. Captures actual EditorWindow IMGUI,
    // not a mockup or a second rendering graph. Exits only that isolated Editor process.
    public static class AnimationLabStudioCapture
    {
        private static AnimationLabWindow window;
        private static int ticks, stage;
        private static string uiError;
        public static void Run()
        {
            if(Application.isBatchMode)throw new InvalidOperationException("La captura de interfaz necesita un editor con ventana, sin -batchmode.");
            Application.logMessageReceived+=Log;
            window=ScriptableObject.CreateInstance<AnimationLabWindow>();window.titleContent=new GUIContent("Animation Lab Studio · revisión aislada");
            int width=Mathf.Clamp(Screen.currentResolution.width-100,1050,1520),height=Mathf.Clamp(Screen.currentResolution.height-140,760,940);
            window.position=new Rect(35,60,width,height);window.ShowUtility();
            LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"NewDraft",0);
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");take.handInsert.start=.1f;take.handInsert.end=.95f;
            LabPreview.Call(window,"CreateGesturePoints");LabPreview.Call(window,"StartHandTrial");LabPreview.Set(window,"progress",.5f);
            var preview=(LabPreview)LabPreview.Get(window,"preview");
            preview.distance=1;preview.pan=preview.Objects.ResolvePart("AK12Rig/Root/Mag").position-(preview.Equipment.spine.animationRoot.position+Vector3.up*1.35f);
            EditorApplication.update+=Update;
        }
        private static void Log(string text,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception)uiError=text;}
        private static void Update()
        {
            try
            {
                window.Focus();window.Repaint();if(++ticks<45)return;ticks=0;
                string error=(string)LabPreview.Get(window,"error");if(!string.IsNullOrEmpty(error)||uiError!=null)throw new InvalidOperationException(error??uiError);
                var preview=(LabPreview)LabPreview.Get(window,"preview");
                if(stage==1)
                {
                    var old=RenderTexture.active;var frame=new Texture2D(1000,800,TextureFormat.RGB24,false);
                    try{RenderTexture.active=preview.Render(1000,800,true);frame.ReadPixels(new Rect(0,0,1000,800),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode","AnimationLab.Studio.FirstPerson.png"),frame.EncodeToPNG());}
                    finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(frame);}
                }
                var rect=window.position;int w=(int)rect.width,h=(int)rect.height;
                var image=new Texture2D(w,h,TextureFormat.RGB24,false);
                try
                {
                    image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position,w,h));image.Apply();
                    string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode");
                    File.WriteAllBytes(Path.Combine(folder,"AnimationLab.Studio.UI."+stage+".png"),image.EncodeToPNG());
                }
                finally{UnityEngine.Object.DestroyImmediate(image);}
                if(stage==0){stage++;LabPreview.Set(window,"firstPerson",true);return;}
                if(stage==1){stage++;LabPreview.Set(window,"firstPerson",false);LabPreview.Set(window,"gesturePane",1);return;}
                if(stage==2)
                {
                    stage++;LabPreview.Call(window,"AcceptHandTrial");
                    var take=(AnimationLabTake)LabPreview.Get(window,"draft");
                    // Only this isolated screenshot draft: old generated poses remain owned by the window's Undo resource list.
                    take.left=new System.Collections.Generic.List<LabHandKey>{new LabHandKey{time=0,outgoing=new Vector3(-.06f,.09f,.02f),incoming=new Vector3(.06f,-.09f,-.02f)},new LabHandKey{time=1,position=new Vector3(-.12f,-.04f,0)}};
                    LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);LabPreview.Set(window,"studioTab",1);
                    LabPreview.Call(window,"SelectTrack",0,.5f);LabPreview.Call(window,"SetPathPreset",LabPathMode.Bezier,new Vector3(0,.02f,0),0f,0f);return;
                }
                if(stage==3){stage++;LabPreview.Set(window,"studioTab",2);return;}
                if(stage==4){stage++;LabPreview.Set(window,"studioTab",1);LabPreview.Set(window,"scroll",new Vector2(0,370));return;}
                Debug.Log("[AnimationLabStudioCapture] PASS: actual Studio window rendered in character, first-person, art, manual curves and project panels without GUI errors.");Finish(0);
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        private static void Finish(int code)
        {
            EditorApplication.update-=Update;Application.logMessageReceived-=Log;
            if(window!=null){LabPreview.Call(window,"CancelHandTrial",false);LabPreview.Set(window,"dirty",false);UnityEngine.Object.DestroyImmediate(window);window=null;}
            EditorApplication.Exit(code);
        }
    }
}
