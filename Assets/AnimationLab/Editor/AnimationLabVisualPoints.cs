using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private Vector3 gestureDragView;
        private Vector2 gestureDragMouse;
        private int visualDragUndoGroup=-1;
        private LabAuthoringTransaction pathDragTransaction;
        private bool pathDragWasDirty;
        private string referenceViewKey;
        private List<LabWeaponKey> referenceView;
        private void BeginVisualDrag(string name)
        {Undo.IncrementCurrentGroup();visualDragUndoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(name);}
        private void EndVisualDrag()
        {
            if(visualDragUndoGroup<0)return;
            try
            {
                if(coordinatedPathPending){RecordPathDrag();RefreshCoordinatedPaths();}
                LabTakeValidation.RequireValid(draft);pathDragTransaction?.Commit();
            }
            catch{dirty=pathDragWasDirty;coordinatedPathPending=false;throw;}
            finally
            {
                pathDragTransaction?.Dispose();pathDragTransaction=null;
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(visualDragUndoGroup);visualDragUndoGroup=-1;Undo.IncrementCurrentGroup();
            }
        }
        private void RecordPathDrag()
        {
            if(pathDragTransaction==null){pathDragTransaction=new LabAuthoringTransaction(draft);pathDragWasDirty=dirty;}
            Undo.RecordObject(draft,"Editar recorrido visual");Undo.RecordObject(draft.action,"Editar recorrido visual");
        }
        private void CancelPathDrag()
        {
            if(pathDragTransaction!=null){GUIUtility.hotControl=0;pathDragTransaction.Dispose();pathDragTransaction=null;dirty=pathDragWasDirty;coordinatedPathPending=false;}
            visualDragUndoGroup=-1;
        }
        private void AbandonPathDragAfterUndo()
        {
            // Undo has already restored state. Release the snapshot without replaying it.
            if(pathDragTransaction!=null)
            {
                pathDragTransaction.Commit();pathDragTransaction.Dispose();pathDragTransaction=null;
                GUIUtility.hotControl=0;
            }
            coordinatedPathPending=false;visualDragUndoGroup=-1;
        }
        private static string ReferenceTransformKey(Transform t)=>t==null?"":t.GetInstanceID()+":"+t.localPosition.ToString("R")+":"+t.localRotation.ToString("R")+":"+t.localScale.ToString("R");
        private List<LabWeaponKey> GestureReferenceView()
        {
            var r=GestureSettings;var action=LabReloadReference.Resolve(preview,r.source);var guard=preview.Guard(r.side);
            var slot=preview.Slot;
            string key=r.source+":"+r.side+":"+draft.equipmentId+":"+ReferenceTransformKey(guard)+":"+
                ReferenceTransformKey(slot.leftReloadTarget)+":"+ReferenceTransformKey(slot.leftHandPose?.actionTarget)+":"+
                ReferenceTransformKey(slot.rightHandPose?.actionTarget)+":"+ReferenceTransformKey(slot.leftGrip)+":"+
                ReferenceTransformKey(slot.rightGrip)+":"+string.Join(",",r.points.Select(p=>p.time.ToString("R")))+":"+(action!=null?EditorJsonUtility.ToJson(action):"");
            if(referenceView==null||referenceViewKey!=key)
            {
                try{referenceView=LabReloadReference.ViewPath(GestureOwner,preview);referenceViewKey=key;}
                finally{EvaluateNow(false);}
            }
            return referenceView;
        }
        private LabWeaponKey GesturePointReference(LabGesturePoint point)=>GestureReferenceView().First(k=>k.time==point.time);
        private Vector3 GesturePointPosition(LabGesturePoint point)=>GestureSettings.source==LabHandSource.Procedural?point.position:
            GesturePointReference(point).position+point.position-point.referencePosition;
        private Vector3 GestureStoredPosition(LabGesturePoint point,Vector3 local)=>GestureSettings.source==LabHandSource.Procedural?local:
            local-GesturePointReference(point).position+point.referencePosition;
        private Vector3 GestureEditedPosition(LabGesturePoint point,Vector3 displayed)
        {
            var r=GestureSettings;
            if(!point.contactAnchor)return GestureStoredPosition(point,LabGestureAuthoring.Uneffective(r,displayed));
            var baselineDelta=r.source==LabHandSource.Procedural?Vector3.zero:GesturePointReference(point).position-point.referencePosition;
            return displayed-baselineDelta*(r.useStyle?r.reachScale:1);
        }
        private Vector3 GesturePointEuler(LabGesturePoint point)=>GestureSettings.source==LabHandSource.Procedural?point.euler:
            LabTimeline.UnwrapEuler((Quaternion.Euler(point.euler)*Quaternion.Inverse(Quaternion.Euler(point.referenceEuler))*Quaternion.Euler(GesturePointReference(point).euler)).eulerAngles,point.euler);
        private Vector3 GestureStoredEuler(LabGesturePoint point,Vector3 euler)=>GestureSettings.source==LabHandSource.Procedural?euler:
            LabTimeline.UnwrapEuler((Quaternion.Euler(euler)*Quaternion.Inverse(Quaternion.Euler(GesturePointReference(point).euler))*Quaternion.Euler(point.referenceEuler)).eulerAngles,point.euler);

        private void CreateGesturePoints()
        {
            if(!CanLeaveFrame())return;
            var points=LabGestureAuthoring.CreatePoints(GestureOwner,preview);
            Undo.RecordObject(GestureOwner,"Crear recorrido visual");GestureSettings.points=points;
            selectedGesturePoint=Mathf.Min(2,points.Count-1);GestureChanged();EvaluateNow(false);
        }
        private void AddGesturePointAtTime()
        {
            var r=GestureSettings;
            if(progress<r.start || progress>r.end)throw new InvalidOperationException("Sitúa el fotograma dentro del tramo antes de añadir un punto.");
            if(r.points.Count<2)CreateGesturePoints();
            float t=LabGestureAuthoring.Map(r,Mathf.InverseLerp(r.start,r.end,progress));
            var keys=LabGestureAuthoring.PointKeys(r);
            if(r.source==LabHandSource.Procedural)AddGesturePoint(t,LabPathCurves.Objects(keys).Evaluate(t),LabPathCurves.Objects(keys,true).Evaluate(t));
            else
            {
                var reference=LabReloadReference.At(GestureOwner,preview,t);var corrections=LabReloadReference.Corrections(r);
                var rotation=Quaternion.Euler(LabPathCurves.Objects(corrections,true).Evaluate(t))*Quaternion.Euler(reference.euler);
                AddGesturePoint(t,reference.position+LabPathCurves.Objects(corrections).Evaluate(t),LabTimeline.UnwrapEuler(rotation.eulerAngles,reference.euler));
            }
        }
        private void AddGesturePoint(float canonical,Vector3 position,Vector3 euler)
        {
            var r=GestureSettings;
            if(progress<r.start || progress>r.end)throw new InvalidOperationException("Sitúa el fotograma dentro del tramo antes de añadir un punto.");
            var reference=r.source!=LabHandSource.Procedural?LabReloadReference.At(GestureOwner,preview,canonical):null;
            Undo.RecordObject(GestureOwner,"Añadir punto visual");
            var point=r.points.FirstOrDefault(p=>Mathf.Abs(p.time-canonical)<LabTimeline.TimeEpsilon);
            if(point==null)
            {
                var before=r.points.Where(p=>p.time<canonical).OrderBy(p=>p.time).LastOrDefault();
                if(before!=null)before.arrivalSpeed=1;
                point=new LabGesturePoint{time=canonical,label="Punto libre",departureSpeed=1,arrivalSpeed=1};r.points.Add(point);
            }
            if(reference!=null){point.hasReference=true;point.referencePosition=reference.position;point.referenceEuler=LabTimeline.UnwrapEuler(reference.euler,euler);}
            point.position=position;point.euler=euler;r.points.Sort((a,b)=>a.time.CompareTo(b.time));selectedGesturePoint=r.points.IndexOf(point);GestureChanged();
        }
        private bool Project(Rect rect,Vector3 world,out Vector2 point)
        {
            Vector3 p=preview.Camera.WorldToViewportPoint(world);
            point=new Vector2(rect.x+p.x*rect.width,rect.y+(1-p.y)*rect.height);
            return p.z>0 && rect.Contains(point);
        }
        private void DrawGestureHandles(Rect rect)
        {
            if(HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial)return;
            if(studioTab!=0 || preview.Camera==null)return;
            var r=GestureSettings; if(r.points.Count<2)return;
            var referenceKeys=r.source!=LabHandSource.Procedural?GestureReferenceView():null;
            var guard=preview.Guard(r.side);if(guard==null)return;
            var projected=new Vector2[r.points.Count];var markers=new Vector2[r.points.Count];var visible=new bool[r.points.Count];
            var referenceCurve=referenceKeys!=null?LabPathCurves.Objects(referenceKeys):null;
            Func<LabGesturePoint,Vector3> pointPosition=p=>referenceCurve==null?p.position:referenceCurve.Evaluate(p.time)+p.position-p.referencePosition;
            for(int i=0;i<r.points.Count;i++)visible[i]=Project(rect,guard.TransformPoint(LabGestureAuthoring.Effective(r,pointPosition(r.points[i]))),out projected[i]);
            for(int i=0;i<r.points.Count;i++)
            {
                int count=0,rank=0;for(int j=0;j<r.points.Count;j++)if(visible[j]&&Vector2.Distance(projected[j],projected[i])<10){count++;if(j<i)rank++;}
                markers[i]=projected[i]+(count>1?new Vector2((rank-(count-1)*.5f)*19,22+(rank%2)*8):Vector2.zero);
            }
            selectedGesturePoint=Mathf.Clamp(selectedGesturePoint,0,r.points.Count-1);
            var e=Event.current;int id=GUIUtility.GetControlID("LabGesturePointHandle".GetHashCode(),FocusType.Passive);
            if(GUIUtility.hotControl==id && e.type==EventType.MouseUp)
            {GUIUtility.hotControl=0;EndVisualDrag();e.Use();if(HasHandTrial&&trialStale)Safe(RebuildHandTrial);return;}
            if(GUIUtility.hotControl==id && e.type==EventType.MouseDrag)
            {
                Vector2 delta=e.mousePosition-gestureDragMouse;
                Vector3 view=gestureDragView+new Vector3(delta.x/rect.width,-delta.y/rect.height,0);
                var selected=r.points[selectedGesturePoint];Vector3 desired=guard.InverseTransformPoint(preview.Camera.ViewportToWorldPoint(view));
                Undo.RecordObject(GestureOwner,"Mover punto visual");
                selected.position=GestureEditedPosition(selected,desired);GestureChanged();e.Use();
            }
            if(!playing && e.type==EventType.MouseDown && e.button==0 && (e.control||e.command) && rect.Contains(e.mousePosition))
            {
                float depth=preview.Camera.WorldToViewportPoint(guard.TransformPoint(LabGestureAuthoring.Effective(r,pointPosition(r.points[selectedGesturePoint])))).z;
                Vector3 world=preview.Camera.ViewportToWorldPoint(new Vector3((e.mousePosition.x-rect.x)/rect.width,1-(e.mousePosition.y-rect.y)/rect.height,Mathf.Max(.02f,depth)));
                Safe(()=>AddGesturePoint(LabGestureAuthoring.Map(r,Mathf.InverseLerp(r.start,r.end,progress)),LabGestureAuthoring.Uneffective(r,guard.InverseTransformPoint(world)),r.points[selectedGesturePoint].euler));
                e.Use();return;
            }
            if(e.type==EventType.Repaint)
            {
                var curve=LabPathCurves.Objects(referenceKeys??LabGestureAuthoring.PointKeys(r));
                var correction=referenceKeys!=null?LabPathCurves.Objects(LabReloadReference.Corrections(r)):null;Vector2? previous=null;
                Handles.BeginGUI();Handles.color=LabStudioUI.Accent;
                for(int n=0;n<=100;n++)
                {
                    Vector3 position=curve.Evaluate(n/100f)+(correction!=null?correction.Evaluate(n/100f):Vector3.zero);
                    if(Project(rect,guard.TransformPoint(LabGestureAuthoring.Effective(r,position)),out Vector2 p))
                    {if(previous.HasValue)Handles.DrawAAPolyLine(2.5f,previous.Value,p);previous=p;}else previous=null;
                }
                for(int i=0;i<r.points.Count;i++)
                {
                    var point=r.points[i];Vector3 world=guard.TransformPoint(LabGestureAuthoring.Effective(r,pointPosition(point)));
                    if(!visible[i])continue;Vector2 p=markers[i];
                    Handles.color=new Color(.7f,.8f,.85f,.45f);if(Vector2.Distance(p,projected[i])>1)Handles.DrawLine(projected[i],p);
                    Handles.color=i==selectedGesturePoint?Color.white:LabStudioUI.Accent;
                    Handles.DrawSolidDisc(p,Vector3.forward,i==selectedGesturePoint?7:5);
                    if(point.contactAnchor){Handles.color=LabStudioUI.Accent;Handles.DrawWireDisc(projected[i],Vector3.forward,12);}
                    if(i==selectedGesturePoint)GUI.Label(new Rect(p.x+10,p.y-18,190,24),$"{i+1} · {point.label}",EditorStyles.whiteMiniLabel);
                    else GUI.Label(new Rect(p.x-5,p.y+6,24,18),(i+1).ToString(),EditorStyles.whiteMiniLabel);
                    if(i==selectedGesturePoint)
                    {
                        var rotation=guard.rotation*Quaternion.Euler(GesturePointEuler(point));
                        Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};Color[] colors={Color.red,Color.green,Color.cyan};
                        for(int a=0;a<3;a++)if(Project(rect,world+rotation*axes[a]*.045f,out Vector2 tip))
                        {Handles.color=colors[a];Handles.DrawAAPolyLine(2,projected[i],tip);}
                    }
                }
                Handles.EndGUI();
                GUI.Label(new Rect(rect.x+12,rect.y+10,rect.width-24,40),"RECORRIDO EDITABLE · arrastrar puntos / Ctrl+clic para añadir\nReferencia local con padres fijos; el personaje se actualiza al soltar o Probar.",EditorStyles.whiteMiniLabel);
            }
            if(!playing && e.type==EventType.MouseDown && e.button==0 && !e.shift)
            {
                int nearest=-1;float distance=13;
                for(int i=0;i<r.points.Count;i++)if(visible[i]&&Vector2.Distance(markers[i],e.mousePosition)<distance)
                {distance=Vector2.Distance(markers[i],e.mousePosition);nearest=i;}
                if(nearest>=0)
                {
                    selectedGesturePoint=nearest;BeginVisualDrag("Mover punto visual");
                     gestureDragView=preview.Camera.WorldToViewportPoint(guard.TransformPoint(LabGestureAuthoring.Effective(r,pointPosition(r.points[nearest]))));gestureDragMouse=e.mousePosition;
                    GUIUtility.hotControl=id;e.Use();
                }
            }
        }

        private bool SelectedSegment(out LabWeaponKey objectKey,out LabHandKey handKey,out Vector3 end,out float nextTime)
        {
            objectKey=null;handKey=null;end=Vector3.zero;nextTime=0;
            if(editObject||editWeapon)
            {
                var keys=editObject?ObjectKeys():draft.weapon;if(keys==null)return false;
                var ordered=keys.OrderBy(k=>k.time).ToArray();if(ordered.Length<2)return false;
                var next=ordered.FirstOrDefault(k=>k.time>progress+LabTimeline.TimeEpsilon)??ordered.Last();
                if(next==ordered[0])next=ordered[1];
                objectKey=ordered.LastOrDefault(k=>k.time<next.time-LabTimeline.TimeEpsilon);
                if(objectKey==null||next==null)return false;end=next.position;nextTime=next.time;return true;
            }
            if(editFingers)return false;
            var wrists=HandKeys.Where(k=>!k.fingersOnly).OrderBy(k=>k.time).ToArray();if(wrists.Length<2)return false;
            var after=wrists.FirstOrDefault(k=>k.time>progress+LabTimeline.TimeEpsilon)??wrists.Last();
            if(after==wrists[0])after=wrists[1];
            handKey=wrists.LastOrDefault(k=>k.time<after.time-LabTimeline.TimeEpsilon);
            if(handKey==null||after==null)return false;end=after.position;nextTime=after.time;return true;
        }
        private void RebuildSelectedPath()
        {
            try
            {
            if(editWeapon)LabAssetIO.BuildWeaponMotion(draft);
            else if(!editObject)
            {
                if(side==HandGripToolkit.HandSide.Left)draft.action.leftHandTargetMotion=null;
                LabAssetIO.BuildMotion(HandKeys,HandMotion);
            }
            if(editObject&&Event.current!=null&&Event.current.type==EventType.MouseDrag)coordinatedPathPending=true;
            else if(editObject)RefreshCoordinatedPaths();
            dirty=true;
            }
            catch{CancelPathDrag();throw;}
        }
        private void DrawSegmentHandle(Rect rect)
        {
            if(studioTab!=1||HasTakeTrial||playing||preview.Camera==null||Pending)return;
            if(!SelectedSegment(out var obj,out var hand,out Vector3 end,out _))return;
            var mode=obj!=null?obj.pathMode:hand.pathMode;
            if(mode!=LabPathMode.Arc&&mode!=LabPathMode.Cinematic&&mode!=LabPathMode.Bezier&&mode!=LabPathMode.Flow)return;
            var reference=LocalReference;if(reference==null)return;
            Vector3 start=obj!=null?obj.position:hand.position,bend=obj!=null?obj.bend:hand.bend;
            Vector3 origin=editWeapon?preview.Equipment.weaponPose.PoseRoot.localPosition-preview.lastWeaponOffset:Vector3.zero;
            if(mode==LabPathMode.Bezier||mode==LabPathMode.Flow)
            {
                DrawFreePathHandle(rect,reference,origin,start,obj!=null?obj.outgoing:hand.outgoing,true,obj,hand);
                DrawFreePathHandle(rect,reference,origin,end,obj!=null?obj.incoming:hand.incoming,false,obj,hand);return;
            }
            Vector3 midpoint=(start+end)*.5f;
            var e=Event.current;int id=GUIUtility.GetControlID("LabSegmentArc".GetHashCode(),FocusType.Passive);
            if(GUIUtility.hotControl==id&&e.type==EventType.MouseUp){GUIUtility.hotControl=0;e.Use();Safe(()=>{EndVisualDrag();SyncSelection();});return;}
            if(GUIUtility.hotControl==id&&e.type==EventType.MouseDrag)
            {
                var view=PathDragView(rect,e);
                var value=reference.InverseTransformPoint(preview.Camera.ViewportToWorldPoint(view))-origin-midpoint;
                RecordPathDrag();
                Safe(()=>{if(obj!=null)obj.bend=value;else hand.bend=value;RebuildSelectedPath();});e.Use();
            }
            if(!Project(rect,reference.TransformPoint(origin+midpoint+bend),out Vector2 point))return;
            if(e.type==EventType.Repaint){Handles.BeginGUI();Handles.color=LabStudioUI.Violet;Handles.DrawSolidDisc(point,Vector3.forward,6);Handles.EndGUI();GUI.Label(new Rect(point.x+9,point.y,170,20),"Arco · Alt: profundidad",EditorStyles.whiteMiniLabel);}
            if(e.type==EventType.MouseDown&&e.button==0&&Vector2.Distance(e.mousePosition,point)<13)
            {if(!ApprovePathOwnership()){e.Use();return;}BeginVisualDrag("Curvar tramo visual");gestureDragMouse=e.mousePosition;gestureDragView=preview.Camera.WorldToViewportPoint(reference.TransformPoint(origin+midpoint+bend));GUIUtility.hotControl=id;e.Use();}
        }
        private Vector3 PathDragView(Rect rect,Event e)
        {
            var delta=e.mousePosition-gestureDragMouse;
            return e.alt?new Vector3(gestureDragView.x,gestureDragView.y,Mathf.Max(.01f,gestureDragView.z-delta.y*.003f)):
                gestureDragView+new Vector3(delta.x/rect.width,-delta.y/rect.height,0);
        }
        private void DrawFreePathHandle(Rect rect,Transform reference,Vector3 origin,Vector3 endpoint,Vector3 control,bool outgoing,LabWeaponKey obj,LabHandKey hand)
        {
            var e=Event.current;int id=GUIUtility.GetControlID((outgoing?"LabPathOut":"LabPathIn").GetHashCode(),FocusType.Passive);
            if(GUIUtility.hotControl==id&&e.type==EventType.MouseUp){GUIUtility.hotControl=0;e.Use();Safe(()=>{EndVisualDrag();SyncSelection();});return;}
            if(GUIUtility.hotControl==id&&e.type==EventType.MouseDrag)
            {
                var value=reference.InverseTransformPoint(preview.Camera.ViewportToWorldPoint(PathDragView(rect,e)))-origin-endpoint;
                RecordPathDrag();
                Safe(()=>{if(obj!=null){if(outgoing)obj.outgoing=value;else obj.incoming=value;}else{if(outgoing)hand.outgoing=value;else hand.incoming=value;}RebuildSelectedPath();});e.Use();
            }
            if(!Project(rect,reference.TransformPoint(origin+endpoint+control),out Vector2 point))return;
            if(e.type==EventType.Repaint)
            {
                Handles.BeginGUI();Handles.color=outgoing?LabStudioUI.Violet:Color.cyan;
                if(Project(rect,reference.TransformPoint(origin+endpoint),out Vector2 anchor))Handles.DrawLine(anchor,point);
                Handles.DrawSolidDisc(point,Vector3.forward,6);Handles.EndGUI();
                GUI.Label(new Rect(point.x+8,point.y-18,190,20),outgoing?"Salida libre · Alt: profundidad":"Entrada libre · Alt: profundidad",EditorStyles.whiteMiniLabel);
            }
            if(e.type==EventType.MouseDown&&e.button==0&&Vector2.Distance(e.mousePosition,point)<13)
            {if(!ApprovePathOwnership()){e.Use();return;}BeginVisualDrag("Editar control libre");gestureDragMouse=e.mousePosition;gestureDragView=preview.Camera.WorldToViewportPoint(reference.TransformPoint(origin+endpoint+control));GUIUtility.hotControl=id;e.Use();}
        }
    }
}
