using System;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabReferenceChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar recargas existentes y curvas libres")]
        public static void Run()
        {
            for(int frame=1;frame<=18000;frame++)
                Require(LabTimeline.LastFrame(frame/60f)==frame&&LabTimeline.Time(frame,frame/60f)==1,"seconds/frame roundtrip has no phantom final frame");
            Require(LabTimeline.LastFrame(.605f)==37,"genuine fractional final frame remains selectable");
            string scene=File.ReadAllText(LabPreview.ScenePath);int count=EditorSceneManager.previewSceneCount;
            foreach(SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))Check(mode);
            Require(scene==File.ReadAllText(LabPreview.ScenePath)&&count==EditorSceneManager.previewSceneCount,"scene preserved and previews disposed");
            Debug.Log("[AnimationLabReferenceChecks] PASS: duration/Undo, original reload wrists and fingers, additive controls, full recipe copy, two-key editing and Bezier bake.");
        }
        public static void RunAll(){Run();AnimationLabStudioChecks.RunAll();}
        private static void Check(SpineDriveMode mode)
        {
            var window=ScriptableObject.CreateInstance<AnimationLabWindow>();AnimationLabTake saved=null;
            try
            {
                LabPreview.Set(window,"mode",mode);LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"NewDraft",0);
                var preview=(LabPreview)LabPreview.Get(window,"preview");var source=(AnimationLabTake)LabPreview.Get(window,"draft");
                var original=LabReloadReference.Resolve(preview,LabHandSource.Reload);
                Require(source.handInsert.source==LabHandSource.Reload&&source.action.duration==original.duration,"new firearm take starts with existing reload and its duration");
                float oldDuration=source.action.duration;
                Undo.IncrementCurrentGroup();LabPreview.Call(window,"SetDuration",3.25f);Undo.FlushUndoRecordObjects();
                Require(source.action.duration==3.25f&&LabTimeline.LastFrame(source.action.duration)==195,"Animar duration uses seconds and 60fps frames");
                Undo.PerformUndo();Require(source.action.duration==oldDuration,"duration can be undone");Undo.PerformRedo();
                string definition=EditorJsonUtility.ToJson(preview.Slot.definition);
                int ammo=preview.Equipment.Runtime.Current.TotalLoadedRounds;uint commits=preview.Equipment.Runtime.Current.Action.CommitVersion;
                foreach(var referenceSource in new[]{LabHandSource.Reload,LabHandSource.ReloadEmpty})
                {
                    source.handInsert.source=referenceSource;source.handInsert.start=.1f;source.handInsert.end=.9f;
                    source.handInsert.points.Clear();string actionBefore=EditorJsonUtility.ToJson(LabReloadReference.Resolve(preview,referenceSource));
                    CheckReference(source,preview);
                    CheckFingerIsolation(source,preview);
                    CheckView(window,source,preview);
                    source.handInsert.points=LabGestureAuthoring.CreatePoints(source,preview);
                    Require(source.handInsert.points.Count>7&&source.handInsert.points.All(p=>p.hasReference),"reference controls keep their sampled baselines");
                    CheckReference(source,preview);
                    var point=source.handInsert.points.First(p=>p.time>.35f&&p.time<.6f);
                    point.position+=new Vector3(.01f,0,0);point.euler+=new Vector3(6,2,0);
                    var generated=LabHandTemplateInsertion.Insert(source,preview);
                    try
                    {
                        preview.EnableHand(HandSide.Left);
                        preview.Evaluate(generated,Mathf.Lerp(source.handInsert.start,source.handInsert.end,point.time));
                        var guard=preview.Guard(HandSide.Left);var chain=preview.Equipment.handIK.leftHand;
                        Require(Vector3.Distance(guard.InverseTransformPoint(chain.target.position),point.position)<.001f,"visual correction reaches moved point without replacing reference motion");
                        Require(Quaternion.Angle(Quaternion.Inverse(guard.rotation)*chain.target.rotation,Quaternion.Euler(point.euler))<.2f,"visual orientation correction composes with original wrist");
                    }
                    finally{LabAssetIO.DestroyDraft(generated);}
                    LabPreview.Set(window,"progress",.411f);LabPreview.Call(window,"AddGesturePointAtTime");
                    Require(source.handInsert.points.All(p=>p.hasReference),"new points on real reload also have reference baselines");
                    Require(EditorJsonUtility.ToJson(LabReloadReference.Resolve(preview,referenceSource))==actionBefore,"shared reload action never modified");
                }
                CheckOtherHand(source,preview);
                LabPreview.Call(window,"LoadReloadReference",LabHandSource.ReloadEmpty);source=(AnimationLabTake)LabPreview.Get(window,"draft");
                original=LabReloadReference.Resolve(preview,LabHandSource.ReloadEmpty);
                Require(source.action!=original&&source.action.characterClip==original.characterClip&&source.action.leftHandTargetMotion==original.leftHandTargetMotion&&source.action.duration==original.duration,"complete reload opens as independent exact recipe copy");
                CheckTwoKeys(window,source,preview);
                source.handInsert.points=LabReloadReference.CreatePoints(source,preview);
                source.handInsert.points[2].position+=new Vector3(.01f,0,0);
                saved=LabAssetIO.Save(source,preview,"__ReferenceChecks");
                string path=AssetDatabase.GetAssetPath(saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                Require(AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path).left.First(k=>!k.fingersOnly).pathMode==LabPathMode.Bezier,"free curve survives save/reimport");
                var reopened=AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path);
                Require(reopened.handInsert.source==LabHandSource.ReloadEmpty&&reopened.handInsert.points.All(p=>p.hasReference)&&
                    reopened.handInsert.points[2].position==source.handInsert.points[2].position&&
                    reopened.handInsert.points[2].referencePosition==source.handInsert.points[2].referencePosition,"reference selection, baseline and independent correction survive save/reimport");
                Require(definition==EditorJsonUtility.ToJson(preview.Slot.definition)&&ammo==preview.Equipment.Runtime.Current.TotalLoadedRounds&&commits==preview.Equipment.Runtime.Current.Action.CommitVersion,"definition and gameplay untouched");
                Debug.Log("[AnimationLabReferenceChecks] Passed "+mode);
            }
            finally
            {
                LabPreview.Call(window,"CancelHandTrial",false);LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);if(saved!=null)AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
            }
        }
        private static void CheckReference(AnimationLabTake source,LabPreview preview)
        {
            var generated=LabHandTemplateInsertion.Insert(source,preview);
            try
            {
                foreach(float t in new[]{.2f,.4f,.6f,.75f})
                {
                    var reference=LabReloadReference.Create(source,preview);LabHandKey expected=null;
                    try
                    {
                        expected=LabReloadReference.Sample(reference,preview,t,HandSide.Left,true);
                        preview.EnableHand(HandSide.Left);preview.Evaluate(generated,Mathf.Lerp(source.handInsert.start,source.handInsert.end,t));
                        var guard=preview.Guard(HandSide.Left);var chain=preview.Equipment.handIK.leftHand;
                        Require(Vector3.Distance(guard.InverseTransformPoint(chain.target.position),expected.position)<.001f,"inserted wrist follows original reload "+t);
                        Require(Quaternion.Angle(Quaternion.Inverse(guard.rotation)*chain.target.rotation,Quaternion.Euler(expected.euler))<.2f,"original wrist rotation retained");
                        var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);
                        Require(Quaternion.Angle(bone.localRotation,expected.fingers.GetEntry(FingerId.Index,1).localRotation)<.5f,"real reload fingers retained instead of generic grip");
                    }
                    finally{if(expected?.fingers!=null)UnityEngine.Object.DestroyImmediate(expected.fingers);LabAssetIO.DestroyDraft(reference);}
                }
                Require(generated.action.weaponClip==source.action.weaponClip&&generated.kind==source.kind&&generated.action.duration==source.action.duration&&generated.parts.Count==source.parts.Count&&generated.right.Count==source.right.Count,"hand-only reference does not replace equipment or other hand");
            }
            finally{LabAssetIO.DestroyDraft(generated);}
        }
        private static void CheckTwoKeys(AnimationLabWindow window,AnimationLabTake source,LabPreview preview)
        {
            var pose=LabGripTools.Preset(preview.Animator,HandSide.Left,2);
            source.left.Add(new LabHandKey{time=.43f,fingersOnly=true,fingers=pose});
            var character=source.action.characterClip;var targetClip=source.action.leftHandTargetMotion;
            LabPreview.Call(window,"SelectTrack",0,.3f);LabPreview.Call(window,"CapturePathEndpoints",true);
            Require(source.left.Count(k=>!k.fingersOnly)==2&&source.left.Any(k=>k.fingers==pose)&&source.action.characterClip==character,"capture two clip endpoints preserves fingers and original character clip");
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();Require(source.left.Count(k=>!k.fingersOnly)==0&&source.action.leftHandTargetMotion==targetClip,"endpoint capture undo restores old target recipe");
            Undo.PerformRedo();Require(source.left.Count(k=>!k.fingersOnly)==2&&source.left.Any(k=>k.fingers==pose),"endpoint redo keeps fingers alive");
            var a=source.left.First(k=>!k.fingersOnly&&k.time==0);var b=source.left.First(k=>!k.fingersOnly&&k.time==1);
            a.position=Vector3.zero;b.position=new Vector3(.08f,0,0);a.outgoing=new Vector3(.025f,.04f,0);a.incoming=new Vector3(-.025f,-.04f,0);
            foreach(float cursor in new[]{0f,.4f,1f})
            {
                LabPreview.Call(window,"SelectTrack",0,cursor);LabPreview.Call(window,"SetPathPreset",LabPathMode.Bezier,Vector3.zero,0f,0f);
                Require(a.pathMode==LabPathMode.Bezier&&source.left.Count(k=>!k.fingersOnly)==2,"two-point path editable between keys and from last key");
            }
            Require(source.action.leftHandMotion.position.Evaluate(.25f).y>0&&source.action.leftHandMotion.position.Evaluate(.75f).y<0,"independent controls create S curve");
            source.action.leftHandTargetMotion=targetClip;
            string before=JsonUtility.ToJson(source.action.leftHandMotion);Vector3 oldControl=a.outgoing;
            LabPreview.Call(window,"BeginVisualDrag","Comprobar arrastre");
            Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup(); // MouseDown may finish a frame without a mutation.
            LabPreview.Call(window,"RecordPathDrag");a.outgoing+=new Vector3(.01f,.02f,0);LabPreview.Call(window,"RebuildSelectedPath");
            Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();
            LabPreview.Call(window,"RecordPathDrag");a.outgoing+=new Vector3(.01f,0,0);LabPreview.Call(window,"RebuildSelectedPath");
            LabPreview.Call(window,"EndVisualDrag");
            Require(source.action.leftHandTargetMotion==null,"confirmed manual curve rebuild disables legacy target motion");
            Undo.PerformUndo();
            Require(source.left.First(k=>!k.fingersOnly).outgoing==oldControl&&source.action.leftHandTargetMotion==targetClip&&JsonUtility.ToJson(source.action.leftHandMotion)==before,"delayed drag Undo restores controls and complete prior wrist recipe");
            Undo.PerformRedo();Require(source.action.leftHandTargetMotion==null&&source.left.First(k=>!k.fingersOnly).outgoing!=oldControl,"drag Redo restores both curve and ownership");
            CheckBezierBake(source,preview);
        }
        private static void CheckFingerIsolation(AnimationLabTake source,LabPreview preview)
        {
            var settings=LabGestureAuthoring.Copy(source.handInsert);
            AnimationLabTake generated=null,baseline=null;
            try
            {
                baseline=LabHandTemplateInsertion.Insert(source,preview);
                source.handInsert.phaseGrip=new[]{-1f,-1f,0f,-1f,-1f,-1f};
                Require(LabGestureAuthoring.FingerOverrideWeight(source.handInsert,.4f)>.9f&&
                    LabGestureAuthoring.FingerOverrideWeight(source.handInsert,.6f)==0,"phase override has local influence only");
                generated=LabHandTemplateInsertion.Insert(source,preview);
                float largest=0;
                foreach(float t in new[]{.2f,.4f,.6f,.75f})
                {
                    float time=Mathf.Lerp(source.handInsert.start,source.handInsert.end,t);
                    preview.EnableHand(HandSide.Left);preview.Evaluate(baseline,time);
                    var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);Quaternion expected=bone.localRotation;
                    preview.Evaluate(generated,time);float angle=Quaternion.Angle(expected,bone.localRotation);
                    if(t==.4f)largest=angle;else Require(angle<.05f,"unmodified phases retain reference fingers");
                }
                Require(largest>1,"selected phase really changes finger pose");
                source.handInsert.phaseGrip=Array.Empty<float>();
                source.handInsert.points=new System.Collections.Generic.List<LabGesturePoint>{
                    new LabGesturePoint{time=0},new LabGesturePoint{time=.4f,grip=0},new LabGesturePoint{time=.5f},new LabGesturePoint{time=1}};
                Require(LabGestureAuthoring.FingerOverrideWeight(source.handInsert,.4f)==1&&
                    LabGestureAuthoring.FingerOverrideWeight(source.handInsert,.6f)==0,"point override stops at neighboring automatic controls");
            }
            finally{source.handInsert=settings;LabAssetIO.DestroyDraft(generated);LabAssetIO.DestroyDraft(baseline);}
        }
        private static void CheckView(AnimationLabWindow window,AnimationLabTake source,LabPreview preview)
        {
            var settings=LabGestureAuthoring.Copy(source.handInsert);
            try
            {
                source.handInsert.points=LabReloadReference.CreatePoints(source,preview);
                var point=source.handInsert.points.First(p=>p.time>.35f&&p.time<.6f);point.position+=new Vector3(.013f,.002f,0);
                var view=(System.Collections.Generic.List<LabWeaponKey>)LabPreview.Call(window,"GestureReferenceView");
                Require(view.Count>60,"overlay retains dense source samples");
                var curve=LabPathCurves.Objects(view);var correction=LabPathCurves.Objects(LabReloadReference.Corrections(source.handInsert));
                foreach(float t in view.Where(k=>k.time>.15f&&k.time<.85f).Where((k,i)=>i%30==0).Select(k=>k.time))
                {
                    var actual=LabReloadReference.At(source,preview,t);
                    Require(Vector3.Distance(curve.Evaluate(t),actual.position)<.0001f,$"overlay retains exact dense reference sample at {t}");
                }
                Require(Vector3.Distance(curve.Evaluate(point.time)+correction.Evaluate(point.time),point.position)<.00001f,"overlay adds authored correction to reference");
                var target=preview.Slot.leftReloadTarget;Vector3 position=target.localPosition;
                try
                {
                    var cached=LabPreview.Get(window,"referenceView");
                    target.localPosition+=new Vector3(.001f,0,0);
                    var updated=(System.Collections.Generic.List<LabWeaponKey>)LabPreview.Call(window,"GestureReferenceView");
                    Require(!ReferenceEquals(cached,LabPreview.Get(window,"referenceView")),"millimeter reload target calibration invalidates dense overlay cache");
                    var actual=LabReloadReference.At(source,preview,point.time);Vector3 expected=actual.position+point.position-point.referencePosition;
                    Require(Vector3.Distance(LabPathCurves.Objects(updated).Evaluate(point.time)+correction.Evaluate(point.time),expected)<.00001f,"recalibrated overlay uses current baseline at authored controls");
                    Require(Vector3.Distance((Vector3)LabPreview.Call(window,"GesturePointPosition",point),expected)<.00001f,"recalibrated marker follows same baseline and correction");
                    Vector3 desired=expected+new Vector3(.005f,0,0);
                    Require(Vector3.Distance((Vector3)LabPreview.Call(window,"GestureStoredPosition",point,desired),point.position+new Vector3(.005f,0,0))<.00001f,"drag conversion preserves stored delta after recalibration");
                    var candidate=LabHandTemplateInsertion.Insert(source,preview);
                    try
                    {
                        preview.EnableHand(HandSide.Left);preview.Evaluate(candidate,Mathf.Lerp(source.handInsert.start,source.handInsert.end,point.time));
                        Require(Vector3.Distance(preview.Guard(HandSide.Left).InverseTransformPoint(preview.Equipment.handIK.leftHand.target.position),expected)<.001f,"recalibrated overlay agrees with inserted candidate");
                    }
                    finally{LabAssetIO.DestroyDraft(candidate);}
                    var anchor=source.handInsert.points.First(p=>p.contactAnchor);Vector3 stored=anchor.position;
                    foreach(float scale in new[]{.5f,1.5f})
                    {
                        anchor.position=stored;source.handInsert.useStyle=true;source.handInsert.reachScale=scale;
                        Vector3 visible=LabGestureAuthoring.Effective(source.handInsert,(Vector3)LabPreview.Call(window,"GesturePointPosition",anchor));
                        Vector3 moved=visible+new Vector3(.01f,0,0);
                        anchor.position=(Vector3)LabPreview.Call(window,"GestureEditedPosition",anchor,moved);
                        Require(Vector3.Distance(LabGestureAuthoring.Effective(source.handInsert,(Vector3)LabPreview.Call(window,"GesturePointPosition",anchor)),moved)<.00001f,"scaled reference contact anchor moves exactly requested distance after recalibration");
                    }
                }
                finally{target.localPosition=position;}
            }
            finally{source.handInsert=settings;}
        }
        private static void CheckOtherHand(AnimationLabTake source,LabPreview preview)
        {
            var authored=LabAssetIO.Clone(source);AnimationLabTake generated=null;
            try
            {
                authored.handInsert.points.Clear();authored.handInsert.source=LabHandSource.Reload;
                authored.action.followAnimatedRightHand=true;
                authored.right.Add(new LabHandKey{time=0});
                authored.right.Add(new LabHandKey{time=.5f,position=new Vector3(.02f,.01f,0),euler=new Vector3(0,5,0)});
                authored.right.Add(new LabHandKey{time=1});
                LabAssetIO.BuildMotion(authored.right,authored.action.rightHandMotion);
                string right=JsonUtility.ToJson(authored.action.rightHandMotion);
                generated=LabHandTemplateInsertion.Insert(authored,preview);
                Require(right==JsonUtility.ToJson(generated.action.rightHandMotion),"reference insertion retains authored unselected hand");
                foreach(float t in new[]{0f,.05f,.95f,1f})
                {
                    preview.EnableHand(HandSide.Left);preview.EnableHand(HandSide.Right);preview.Evaluate(authored,t);
                    var chain=preview.Equipment.handIK.leftHand;Vector3 expected=chain.target.position;
                    preview.Evaluate(generated,t);
                    Require(Vector3.Distance(expected,chain.target.position)<.001f,"outside inserted interval left wrist retains source with animated right hand");
                }
            }
            finally{LabAssetIO.DestroyDraft(generated);LabAssetIO.DestroyDraft(authored);}
        }
        private static void CheckBezierBake(AnimationLabTake source,LabPreview preview)
        {
            preview.Evaluate(source,0);var part=preview.Objects.ResolvePart("AK12Rig/Root/Mag");var rest=part.localPosition;
            Vector3 delta=part.parent.InverseTransformVector(Vector3.right*.02f),arc=part.parent.InverseTransformVector(Vector3.up*.02f);
            source.parts.Add(new LabObjectTrack{path="AK12Rig/Root/Mag",keys=new System.Collections.Generic.List<LabWeaponKey>{new LabWeaponKey{time=0,position=rest,pathMode=LabPathMode.Bezier,outgoing=delta/3+arc,incoming=-delta/3-arc},new LabWeaponKey{time=1,position=rest+delta}}});
            var clip=LabObjectAuthoring.BakeParts(source,preview);var exported=LabAssetIO.Clone(source);
            try
            {
                exported.parts.Clear();exported.action.weaponClip=clip;
                foreach(float t in new[]{.1f,.25f,.5f,.75f,.9f})
                {preview.Evaluate(source,t);Vector3 expected=part.position;preview.Evaluate(exported,t);Require(Vector3.Distance(expected,part.position)<.0001f,"Bezier bake matches preview");}
            }
            finally{LabAssetIO.DestroyDraft(exported);UnityEngine.Object.DestroyImmediate(clip);}
        }
        private static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException("[AnimationLabReferenceChecks] "+text);}
    }
}
