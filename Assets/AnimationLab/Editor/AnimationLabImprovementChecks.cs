using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabImprovementChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar seguridad y flujo sencillo")]
        public static void Run()
        {
            int scenes=EditorSceneManager.previewSceneCount;
            byte[] original=File.ReadAllBytes(AnimationLabDynamicsChecks.BasePath),scene=File.ReadAllBytes(LabPreview.ScenePath);
            foreach(SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))Check(mode);
            Require(scenes==EditorSceneManager.previewSceneCount,"isolated previews released");
            Require(original.SequenceEqual(File.ReadAllBytes(AnimationLabDynamicsChecks.BasePath)),"original BASE CORRECTA unchanged");
            Require(scene.SequenceEqual(File.ReadAllBytes(LabPreview.ScenePath)),"original fixture unchanged");
            Debug.Log("[AnimationLabImprovementChecks] PASS: split legacy fingers match accepted preview, finite/range review, transactional paths/calibration, continuity, emergency and periodic recovery, candidate exclusion, relative timing and duplicate samples.");
        }
        public static void RunAll(){Run();AnimationLabTransitionChecks.RunAll();}
        private static void Check(SpineDriveMode mode)
        {
            var window=ScriptableObject.CreateInstance<AnimationLabWindow>();
            try
            {
                LabPreview.Set(window,"mode",mode);LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"NewDraft",1);
                var preview=(LabPreview)LabPreview.Get(window,"preview");
                CheckSplit(window,preview);
                CheckValidation(window);
                CheckRollback(window,preview);
                CheckUndoAndBindings(window,preview);
                CheckRecovery(window,preview);
                Debug.Log("[AnimationLabImprovementChecks] Passed "+mode);
            }
            finally{LabPreview.Call(window,"ClosePreview");UnityEngine.Object.DestroyImmediate(window);}
        }
        private static void CheckSplit(AnimationLabWindow window,LabPreview preview)
        {
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            take.action.duration=1;
            float arrival=LabTimeline.Time(40,1);
            take.left.Add(new LabHandKey{time=0,fingers=LabGripTools.Open(preview.Animator,HandSide.Left)});
            take.left.Add(new LabHandKey{time=arrival,position=Vector3.right*.02f});
            take.left.Add(new LabHandKey{time=arrival,fingersOnly=true,fingers=LabGripTools.Open(preview.Animator,HandSide.Left),fingerLabel="Old A"});
            LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);preview.EnableHand(HandSide.Left);
            var requested=LabGripTools.Preset(preview.Animator,HandSide.Left,2);
            var plan=new LabTransitionPlan{track=0,from=0,to=arrival,arrivalFrame=40,departureFrame=0,fingerStartFrame=20};
            var candidate=LabTransitionAssistant.Generate(take,plan,Vector3.one,requested,null,"Requested B");
            try
            {
                Require(candidate.left.Count(k=>k.fingers!=null&&LabFrameEditing.Same(k.time,arrival))==1,"one candidate finger destination despite split wrist/finger keys");
                preview.Evaluate(candidate,arrival);
                var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);
                var proposed=bone.localRotation;
                LabPreview.Call(window,"CommitEditCandidate",candidate,0,null,false,false,true,"Split regression");
                preview.Evaluate(take,arrival);
                Require(Quaternion.Angle(proposed,bone.localRotation)<.01f,"accepted pose equals candidate pose");
                Require(LabTimeline.FindFingers(take.left,arrival).fingerLabel=="Requested B","old grip cannot overwrite requested grip");
                Require(take.left.Count(k=>!k.fingersOnly&&LabFrameEditing.Same(k.time,arrival))==1,"wrist destination retained exactly once");
                Undo.FlushUndoRecordObjects();Undo.PerformUndo();
                Require(LabTimeline.FindFingers(take.left,arrival).fingerLabel=="Old A","Undo restores split original grip");
                Undo.PerformRedo();
                Require(LabTimeline.FindFingers(take.left,arrival).fingerLabel=="Requested B","Redo restores accepted grip");
            }
            finally{LabAssetIO.DestroyDraft(candidate);UnityEngine.Object.DestroyImmediate(requested);}
        }
        private static void CheckValidation(AnimationLabWindow window)
        {
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            string before=Snapshot(take);
            take.left[0].position=new Vector3(float.NaN,0,0);
            take.weapon.Add(new LabWeaponKey{time=1.5f});
            Require(LabTakeValidation.Inspect(take).Count>=2,"NaN position and out-of-range point detected");
            Require(((IEnumerable)LabPreview.Call(window,"BuildReviewIssues")).Cast<object>().Count()>=2,"Review exposes numeric errors");
            Require(Refused(()=>LabAssetIO.Save(take,(LabPreview)LabPreview.Get(window,"preview"),"__InvalidData")),"invalid numeric data cannot be baked/saved as final take");
            take.left[0].position=Vector3.zero;take.weapon.Clear();
            Require(before==Snapshot(take),"validation is read-only");
            var times=LabTimeline.SampleTimes(new[]{0,.5f,.50000006f,1},new[]{.50000006f});
            Require(times.Length==3&&times.Contains(.50000006f),"reference time wins over nearly duplicate regular sample");
            var p=new LabTransitionPlan{track=0,from=0,to=LabTimeline.Time(40,1),departureFrame=0,arrivalFrame=40,relativeFingers=true,fingerDurationFrames=10};
            Require(LabTransitionAssistant.FingerStart(p,1)==30,"relative fingers end with wrist arrival");
            p.arrivalFrame=50;
            Require(LabTransitionAssistant.FingerStart(p,1)==40,"relative fingers follow changed arrival without stale absolute field");
            p.settleFrames=int.MaxValue;
            Require(LabTransitionAssistant.TimingError(take,p)!=null,"inline timing rejects overflow");
        }
        private static void CheckRollback(AnimationLabWindow window,LabPreview preview)
        {
            var original=AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath);
            LabPreview.Call(window,"Load",original);LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            var first=take.parts.First(p=>p.path=="AK12Rig/Root/Mag").keys.OrderBy(k=>k.time).First();
            LabPreview.Call(window,"SelectTrack",5,first.time);
            take.action.duration=31;LabPreview.Set(window,"dirty",false);
            string before=Snapshot(take);var bend=first.bend;
            Require(Refused(()=>LabPreview.Call(window,"SetPathPreset",LabPathMode.Cinematic,new Vector3(.12f,.23f,.34f),0f,0f)),"incompatible contact compile rejected");
            Require(before==Snapshot(take)&&first.bend==bend&&!(bool)LabPreview.Get(window,"dirty"),"failed old path edit rolls back data and dirty state, retaining key identity");
            var anchor=preview.Guard(HandSide.Left);var position=anchor.localPosition;var rotation=anchor.localRotation;
            Require(Refused(()=>LabPreview.Call(window,"ApplyAnchorEdit",anchor,new Vector3(1,2,3),new Vector3(10,20,30))),"calibration compile failure rejected");
            Require(anchor.localPosition==position&&anchor.localRotation==rotation&&before==Snapshot(take),"failed calibration restores transform and draft");
            LabPreview.Call(window,"NewDraft",1);take=(AnimationLabTake)LabPreview.Get(window,"draft");take.action.duration=1;
            foreach(var k in new[]{new LabHandKey{time=0},new LabHandKey{time=.5f,position=Vector3.right},new LabHandKey{time=1,position=Vector3.right*2}})take.left.Add(k);
            LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);LabPreview.Call(window,"SelectTrack",0,0f);
            LabPreview.Call(window,"SetContinuousPath");
            var frame=take.action.leftHandMotion.position.x.keys.First(k=>LabFrameEditing.Same(k.time,.5f));
            Require(frame.inTangent>.01f&&Mathf.Abs(frame.inTangent-frame.outTangent)<.000001f,"old Continuous control smooths both adjacent segments");
        }
        private static void CheckRecovery(AnimationLabWindow window,LabPreview preview)
        {
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            LabPreview.Call(window,"SelectTrack",0,0f);
            var pending=new Vector3(.023f,.045f,.067f);
            LabPreview.Set(window,"keyPosition",pending);LabPreview.Set(window,"previewingKey",true);
            var snapshot=(LabEmergencyRecovery.Envelope)LabPreview.Call(window,"CaptureEmergency");
            Require(snapshot.pending.wrist&&snapshot.pending.position==pending,"emergency captures pending pose without committing it");
            var restored=LabEmergencyRecovery.Restore(JsonUtility.FromJson<LabEmergencyRecovery.Envelope>(JsonUtility.ToJson(snapshot)));
            try{Require(Snapshot(restored)==Snapshot(take),"checkpoint roundtrip preserves channels independently of live objects");}
            finally{LabAssetIO.DestroyDraft(restored);}
            LabPreview.Set(window,"automaticRecovery",true);LabPreview.Set(window,"lastAutoRecoveryAt",-1000d);
            LabPreview.Call(window,"TickAutomaticRecovery");
            string auto=(string)LabPreview.Get(window,"latestEmergencyPath");
            Require(File.Exists(auto),"periodic recovery writes a checkpoint");
            LabPreview.Set(window,"lastAutoRecoveryAt",-1000d);LabPreview.Call(window,"TickAutomaticRecovery");
            Require(auto==(string)LabPreview.Get(window,"latestEmergencyPath"),"unchanged pending draft not checkpointed twice");
            LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"dirty",false);LabPreview.Call(window,"SyncSelection");
            LabPreview.Call(window,"OpenCheckpoint",auto);
            take=(AnimationLabTake)LabPreview.Get(window,"draft");
            Require((bool)LabPreview.Get(window,"previewingKey")&&(Vector3)LabPreview.Get(window,"keyPosition")==pending,"reopen restores pending pose for explicit confirmation");
            LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"dirty",false);LabPreview.Call(window,"SyncSelection");
            LabPreview.Call(window,"Load",AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath));
            LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");take=(AnimationLabTake)LabPreview.Get(window,"draft");
            take.action.duration=31;LabPreview.Set(window,"objectPending",true);LabPreview.Set(window,"dirty",true);
            LabPreview.Call(window,"PreserveOnClose");
            string emergency=(string)LabPreview.Get(window,"latestEmergencyPath");
            Require(File.Exists(emergency),"failed normal recovery retains disk emergency checkpoint");
            var data=LabEmergencyRecovery.ReadHeader(emergency);
            Require(data.pending.item&&data.reason.StartsWith("Recuperación normal fallida"),"failure fallback retains uncommitted object edit");
            restored=LabEmergencyRecovery.Restore(data);
            try{Require(restored.action.duration==31&&restored.parts.Count==take.parts.Count,"failed normal save does not destroy recoverable channels");}
            finally{LabAssetIO.DestroyDraft(restored);}
            LabPreview.Set(window,"objectPending",false);LabPreview.Set(window,"dirty",false);
            LabPreview.Call(window,"NewDraft",1);take=(AnimationLabTake)LabPreview.Get(window,"draft");take.action.duration=1;
            take.left.Add(new LabHandKey{time=0});take.left.Add(new LabHandKey{time=.5f,position=Vector3.right*.02f});
            LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);LabPreview.Call(window,"SelectTrack",0,0f);
            var plan=new LabTransitionPlan{track=0,from=0,to=.5f,departureFrame=0,arrivalFrame=40};
            LabPreview.Set(window,"transitionPlan",plan);LabPreview.Call(window,"StartTransitionTrial");
            snapshot=(LabEmergencyRecovery.Envelope)LabPreview.Call(window,"CaptureEmergency");
            restored=LabEmergencyRecovery.Restore(snapshot);
            try{Require(restored.left.Last().time==.5f&&!snapshot.pending.wrist,"trial checkpoint retains draft, never candidate arrival F40");}
            finally{LabAssetIO.DestroyDraft(restored);}
            LabPreview.Call(window,"CancelTransitionTrial",true);
            take.left[0].time=1.5f;
            snapshot=(LabEmergencyRecovery.Envelope)LabPreview.Call(window,"CaptureEmergency");
            string invalid=LabEmergencyRecovery.Write(snapshot,"Invalid draft regression");
            take.left[0].time=0;LabPreview.Set(window,"dirty",false);
            LabPreview.Call(window,"OpenCheckpoint",invalid);
            Require((bool)LabPreview.Get(window,"quarantinedRecovery"),"invalid checkpoint opens in non-evaluating repair mode");
            take=(AnimationLabTake)LabPreview.Get(window,"draft");
            Require(take.left[0].time==1.5f,"repair mode preserves invalid values rather than silently clamping");
            LabPreview.Call(window,"Tick");
            Require(Refused(()=>LabPreview.Call(window,"SaveTake")),"invalid repair mode cannot save a final take");
            take.left[0].time=0;LabPreview.Call(window,"FinishRecoveryRepair");
            Require(!(bool)LabPreview.Get(window,"quarantinedRecovery"),"explicit revalidation returns repaired draft to studio");
            take.left[0].time=1.5f;LabPreview.Set(window,"dirty",true);
            var assetsBefore=AssetDatabase.FindAssets("t:AnimationLabTake",new[]{LabAssetIO.Output});
            LabPreview.Call(window,"PreserveOnClose");
            var created=AssetDatabase.FindAssets("t:AnimationLabTake",new[]{LabAssetIO.Output}).Except(assetsBefore).Single();
            var recovery=AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AssetDatabase.GUIDToAssetPath(created));
            string assetBefore=EditorJsonUtility.ToJson(recovery);
            take.left[0].time=0;LabPreview.Set(window,"dirty",false);
            LabPreview.Call(window,"OpenTake",recovery,true);
            Require((bool)LabPreview.Get(window,"quarantinedRecovery"),"invalid Recovery asset also opens in safe repair mode");
            take=(AnimationLabTake)LabPreview.Get(window,"draft");take.left[0].time=0;
            LabPreview.Call(window,"FinishRecoveryRepair");
            Require(assetBefore==EditorJsonUtility.ToJson(recovery),"repair never edits persistent Recovery asset");
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(recovery));
            if(File.Exists(invalid))File.Delete(invalid);
            if(File.Exists(auto))File.Delete(auto);if(File.Exists(emergency))File.Delete(emergency);
        }
        private static void CheckUndoAndBindings(AnimationLabWindow window,LabPreview preview)
        {
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            var key=take.left[0];
            Undo.IncrementCurrentGroup();Undo.RecordObject(take,"Earlier edit");
            Undo.RecordObject(take.action,"Earlier edit");
            key.position=Vector3.one*.01f;LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);
            Undo.FlushUndoRecordObjects();
            LabPreview.Call(window,"BeginVisualDrag","Drag regression");LabPreview.Call(window,"RecordPathDrag");
            key.position=Vector3.one*.02f;LabAssetIO.BuildMotion(take.left,take.action.leftHandMotion);
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();Undo.PerformUndo();
            var undone=take.left[0].position;
            LabPreview.Call(window,"CancelPathDrag");
            Require(undone==take.left[0].position&&undone==Vector3.zero,"losing focus after repeated Undo cannot replay a drag snapshot");
            LabPreview.Call(window,"NewDraft",0);take=(AnimationLabTake)LabPreview.Get(window,"draft");
            LabPreview.Call(window,"SelectTrack",0,0f);LabPreview.Set(window,"wristReplacementApproved",true);
            var left=preview.Slot.leftHandPose;var right=preview.Slot.rightHandPose;
            Require((bool)LabPreview.Call(window,"BeginViewportPositionEdit"),"begin viewport edit without replacing draft");
            Require(!ReferenceEquals(left,preview.Slot.leftHandPose),"AK12 edit actually changes ownership before cancellation");
            LabPreview.Set(window,"viewportDragControl",1);LabPreview.Call(window,"CancelViewportDrag");
            Require(ReferenceEquals(left,preview.Slot.leftHandPose)&&ReferenceEquals(right,preview.Slot.rightHandPose)
                &&!(bool)LabPreview.Get(window,"previewingKey"),"cancel restores both original IK bindings and pending flags");
        }
        private static string Snapshot(AnimationLabTake t)=>JsonUtility.ToJson(t.action)+"|"+string.Join("|",t.left.Select(k=>$"{k.time:R}:{k.position}:{k.euler}:{k.fingersOnly}:{k.fingerLabel}:{k.fingerBlend}:{k.fingerBlendDuration:R}:"+(k.fingers!=null?JsonUtility.ToJson(k.fingers):"")))+"|"+JsonUtility.ToJson(t.weaponDynamics);
        private static bool Refused(Action action)
        {try{action();return false;}catch(TargetInvocationException e)when(e.InnerException is InvalidOperationException){return true;}catch(InvalidOperationException){return true;}}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("[AnimationLabImprovementChecks] "+message);}
    }
}
