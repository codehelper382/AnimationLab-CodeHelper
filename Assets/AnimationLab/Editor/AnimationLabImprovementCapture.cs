using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Isolated graphical verification only: closes this Editor process when finished.
    public static class AnimationLabImprovementCapture
    {
        private static AnimationLabWindow window;
        private static int stage,ticks;
        private static string failure;
        public static void Run()
        {
            if(Application.isBatchMode)throw new InvalidOperationException("Esta prueba requiere gráficos, sin batchmode.");
            AssetDatabase.DesiredWorkerCount=0;AssetDatabase.ForceToDesiredWorkerCount();
            Application.logMessageReceived+=Log;
            window=ScriptableObject.CreateInstance<AnimationLabWindow>();window.ShowUtility();window.position=new Rect(30,40,1100,850);
            LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"NewDraft",1);
            var take=Take;take.action.duration=2;
            foreach(int frame in new[]{0,40,100})
                take.left.Add(new LabHandKey{time=LabTimeline.Time(frame,2),position=Vector3.right*(frame/1000f)});
            LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);
            LabPreview.Set(window,"studioTab",1);LabPreview.Set(window,"guidedHelp",false);
            LabPreview.Call(window,"SelectTrack",0,0f);LabPreview.Call(window,"SelectTransitionSegment");
            LabPreview.Set(window,"transitionAssistantOpen",true);
            LabPreview.Set(window,"scroll",new Vector2(0,170));
            LabPreview.Call(window,"FocusSelection");
            EditorApplication.update+=Update;
        }
        private static AnimationLabTake Take=>(AnimationLabTake)LabPreview.Get(window,"draft");
        private static void Log(string text,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failure=text;}
        private static void Update()
        {
            try
            {
                window.Focus();window.Repaint();if(++ticks<30)return;ticks=0;
                string error=(string)LabPreview.Get(window,"error");
                if(failure!=null||!string.IsNullOrEmpty(error))throw new InvalidOperationException(failure??error);
                Capture(stage);
                if(stage==0)ExtendSettle(false);
                else if(stage==1)
                {
                    ExtendSettle(true);
                    MoveArrival();
                    LabPreview.Set(window,"transitionAssistantOpen",false);
                    LabPreview.Call(window,"SelectTrack",0,LabTimeline.Time(40,2));
                }
                else if(stage==2)HiddenOriginDrag(false);
                else if(stage==3)HiddenOriginDrag(true);
                else if(stage==4)AxisDrag();
                else if(stage==6)
                {
                    LabPreview.Call(window,"Load",AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath));
                    LabPreview.Set(window,"firstPerson",true);LabPreview.Set(window,"pitch",-10f);LabPreview.Set(window,"yaw",10f);LabPreview.Set(window,"aim",true);
                    LabPreview.Call(window,"SelectTrack",1,LabTimeline.Time(100,Take.action.duration));
                    LabPreview.Set(window,"scroll",Vector2.zero);
                }
                else if(stage==7)
                {
                    CheckContact();
                    LabPreview.Set(window,"firstPerson",false);
                    LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");
                    LabPreview.Call(window,"SelectTrack",5,LabTimeline.Time(100,Take.action.duration));
                    LabPreview.Call(window,"FocusSelection");
                }
                else
                {
                    LabPreview.Set(window,"simpleMode",false);
                    if(stage>7)
                    {
                        CheckContact();
                        Debug.Log("[AnimationLabImprovementCapture] PASS: simple/complete UI, zero-length settle extension, stable settle bounds/Escape, hidden-origin diamond release/Escape, local-axis movement/Escape, BASE CORRECTA hands/contact in first person and look/Aim; no GUI errors.");
                        Finish(0);return;
                    }
                }
                stage++;
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void ExtendSettle(bool cancel)
        {
            var plan=(LabTransitionPlan)LabPreview.Get(window,"transitionPlan");
            Rect rect=(Rect)LabPreview.Get(window,"transitionBlockWindowRect");
            int max=LabTransitionAssistant.MaximumFrame(Take,plan);
            float X(int f)=>rect.x+rect.width*(f-LabTimeline.Frame(plan.from,2))/(max-LabTimeline.Frame(plan.from,2));
            int before=plan.settleFrames,target=cancel?15:10;
            float start=X(plan.arrivalFrame+before),end=X(plan.arrivalFrame+target);
            Send(EventType.MouseDown,new Vector2(start,rect.center.y));
            Send(EventType.MouseDrag,new Vector2(end,rect.center.y));
            if(cancel)window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
            Send(EventType.MouseUp,new Vector2(end,rect.center.y));
            if(plan.settleFrames!=(cancel?before:target))throw new InvalidOperationException("No se pudo extender/cancelar el bloque Asentar desde duración cero.");
        }
        private static void HiddenOriginDrag(bool cancel)
        {
            int from=cancel?60:40,to=cancel?70:60;
            LabPreview.Set(window,"timelineZoom",1f);LabPreview.Set(window,"timelineStart",0f);
            Rect area=(Rect)LabPreview.Get(window,"timelineRowsRect");
            float X(int f)=>area.x+155+LabTimeline.Time(f,2)*(area.width-165);
            float y=area.y+22+10;
            string before=EditorJsonUtility.ToJson(Take)+EditorJsonUtility.ToJson(Take.action);
            Send(EventType.MouseDown,new Vector2(X(from),y));
            if((int)LabPreview.Get(window,"frameDragControl")==0)throw new InvalidOperationException("No se inició el arrastre del rombo.");
            // Simulate the visible range/subsampling changing after the origin was captured.
            LabPreview.Set(window,"timelineZoom",4f);LabPreview.Set(window,"timelineStart",.7f);
            LabPreview.Set(window,"progress",.8f);
            if(((float[])LabPreview.Call(window,"VisibleTimes",0)).Any(t=>LabFrameEditing.Same(t,LabTimeline.Time(from,2))))
                throw new InvalidOperationException("La prueba no ocultó realmente el origen del arrastre.");
            Send(EventType.MouseDrag,new Vector2(X(to),y));
            if(cancel)window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
            Send(EventType.MouseUp,new Vector2(X(to),y));
            if((int)LabPreview.Get(window,"frameDragControl")!=0)throw new InvalidOperationException("El origen oculto dejó el arrastre activo.");
            string after=EditorJsonUtility.ToJson(Take)+EditorJsonUtility.ToJson(Take.action);
            if(cancel&&before!=after)throw new InvalidOperationException("Escape alteró datos con el origen oculto.");
            if(!cancel&&!Take.left.Any(k=>LabFrameEditing.Same(k.time,LabTimeline.Time(to,2))))throw new InvalidOperationException("MouseUp no confirmó el rombo oculto.");
            LabPreview.Set(window,"timelineZoom",1f);LabPreview.Set(window,"timelineStart",0f);
        }
        private static void MoveArrival()
        {
            var plan=(LabTransitionPlan)LabPreview.Get(window,"transitionPlan");
            var rect=(Rect)LabPreview.Get(window,"transitionBlockWindowRect");
            int max=LabTransitionAssistant.MaximumFrame(Take,plan),before=plan.arrivalFrame,settle=plan.settleFrames;
            float X(int f)=>rect.x+rect.width*f/max;
            Send(EventType.MouseDown,new Vector2(X(before),rect.y+5));
            Send(EventType.MouseDrag,new Vector2(X(before+8),rect.y+5));
            if(plan.arrivalFrame!=before+8||plan.settleFrames!=settle)throw new InvalidOperationException("La llegada no se pudo alargar conservando el asentamiento.");
            window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
            Send(EventType.MouseUp,new Vector2(X(before+8),rect.y+5));
            if(plan.arrivalFrame!=before||plan.settleFrames!=settle)throw new InvalidOperationException("Escape no restauró el bloque de llegada.");
        }
        private static void AxisDrag()
        {
            var ends=(Vector2[])LabPreview.Get(window,"viewportAxisWindowEnds");
            var origin=(Vector2)LabPreview.Get(window,"viewportGizmoWindowPoint");
            int axis=ends[1]!=Vector2.zero?1:Array.FindIndex(ends,p=>p!=Vector2.zero);
            if(axis<0)throw new InvalidOperationException("No se ven los ejes de edición.");
            Vector3 before=(Vector3)LabPreview.Get(window,"keyPosition");
            Vector2 start=ends[axis],end=start+(start-origin).normalized*22;
            Send(EventType.MouseDown,start);Send(EventType.MouseDrag,end);
            if((int)LabPreview.Get(window,"viewportDragControl")==0)throw new InvalidOperationException("El evento no capturó el eje del visor.");
            if((int)LabPreview.Get(window,"viewportAxis")!=axis)throw new InvalidOperationException("La selección de ejes proyectados es ambigua.");
            var moved=(Vector3)LabPreview.Get(window,"keyPosition");
            if(Mathf.Abs(moved[axis]-before[axis])<.00001f)throw new InvalidOperationException("El eje elegido no movió la pose.");
            for(int i=0;i<3;i++)if(i!=axis&&Mathf.Abs(moved[i]-before[i])>.00001f)throw new InvalidOperationException("El arrastre por eje alteró otro eje local.");
            window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
            Send(EventType.MouseUp,end);
            if((Vector3)LabPreview.Get(window,"keyPosition")!=before||(bool)LabPreview.Get(window,"previewingKey"))throw new InvalidOperationException("Escape no restauró la pose y su estado pendiente.");
        }
        private static void Send(EventType type,Vector2 point)=>window.SendEvent(new Event{type=type,button=0,mousePosition=point});
        private static void CheckContact()
        {
            float t=LabTimeline.Time(100,Take.action.duration);
            var segment=LabCoordinatedPaths.At(Take,HandGripToolkit.HandSide.Left,t);
            if(segment==null)throw new InvalidOperationException("Falta el tramo de contacto de la referencia.");
            var preview=(LabPreview)LabPreview.Get(window,"preview");
            var target=LabCoordinatedPaths.Target(preview,segment.objectId);
            var a=LabCoordinatedPaths.Contact(segment.from,segment.side);var b=LabCoordinatedPaths.Contact(segment.to,segment.side);
            var point=Vector3.Lerp(a.position,b.position,Mathf.InverseLerp(segment.from.time,segment.to.time,t));
            if(Vector3.Distance(preview.Equipment.handIK.leftHand.target.position,target.TransformPoint(point))>.001f)
                throw new InvalidOperationException("El contacto visible no coincide con la pieza al probar mirada/Aim.");
        }
        private static void Capture(int n)
        {
            var rect=window.position;int w=(int)rect.width,h=(int)rect.height;
            var colors=UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position,w,h);
            var texture=new Texture2D(w,h,TextureFormat.RGB24,false);
            try
            {
                texture.SetPixels(colors);texture.Apply();
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode");
                File.WriteAllBytes(Path.Combine(folder,"AnimationLab.Improvements.UI."+n+".png"),texture.EncodeToPNG());
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        private static void Finish(int code)
        {
            EditorApplication.update-=Update;Application.logMessageReceived-=Log;
            if(window!=null){LabPreview.Call(window,"ClosePreview");window.Close();}
            EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
        }
    }
}
