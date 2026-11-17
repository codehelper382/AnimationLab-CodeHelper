using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabCombinedChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar recorrido combinado y transición visual")]
        public static void Run()
        {
            string scene=File.ReadAllText(LabPreview.ScenePath);int count=EditorSceneManager.previewSceneCount;
            foreach(SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))Check(mode);
            Require(scene==File.ReadAllText(LabPreview.ScenePath)&&count==EditorSceneManager.previewSceneCount,"isolated scene and previews preserved");
            Debug.Log("[AnimationLabCombinedChecks] PASS: explicit 0/20/40 grip preview without commits, common hand-piece arcs/Bezier, sampled contact/rotation, Undo, persistence and runtime part bake.");
        }
        public static void RunAll(){Run();AnimationLabGripFrameChecks.RunAll();}
        private static void Check(SpineDriveMode mode)
        {
            var window=ScriptableObject.CreateInstance<AnimationLabWindow>();AnimationLabTake saved=null;
            try
            {
                LabPreview.Set(window,"mode",mode);LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"NewDraft",0);
                var take=(AnimationLabTake)LabPreview.Get(window,"draft");var preview=(LabPreview)LabPreview.Get(window,"preview");
                take.action.duration=2;take.action.blendIn=take.action.blendOut=0;take.action.followAnimatedRightHand=false;
                string definition=EditorJsonUtility.ToJson(preview.Slot.definition);int ammo=preview.Equipment.Runtime.Current.TotalLoadedRounds;
                uint commits=preview.Equipment.Runtime.Current.Action.CommitVersion;
                CheckGripPreview(window,take,preview);
                CheckCombined(window,take,preview);
                saved=LabAssetIO.Save(take,preview,"__CombinedChecks");
                string path=AssetDatabase.GetAssetPath(saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var reopened=AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path);
                Require(reopened.parts.First().keys.All(k=>k.leftContact!=null),"contact recipe survives save/reimport");
                var copy=LabAssetIO.Clone(reopened);
                try
                {
                    copy.parts.First().keys.First().leftContact.position+=Vector3.one;
                    Require(copy.parts.First().keys.First().leftContact.position!=reopened.parts.First().keys.First().leftContact.position,"contacts are deep copies");
                }
                finally{LabAssetIO.DestroyDraft(copy);}
                Require(definition==EditorJsonUtility.ToJson(preview.Slot.definition)&&ammo==preview.Equipment.Runtime.Current.TotalLoadedRounds&&commits==preview.Equipment.Runtime.Current.Action.CommitVersion,"definition and gameplay unchanged");
                Debug.Log("[AnimationLabCombinedChecks] Passed "+mode);
            }
            finally
            {
                LabPreview.Call(window,"StopGripTransitionPreview");LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);if(saved!=null)AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
            }
        }
        private static void CheckGripPreview(AnimationLabWindow window,AnimationLabTake take,LabPreview preview)
        {
            float T(int frame)=>LabTimeline.Time(frame,take.action.duration);
            LabPreview.Call(window,"SelectTrack",1,0f);LabPreview.Call(window,"UseInitialGrip");LabPreview.Call(window,"CommitEdits",true,false);
            var baseline=LabTimeline.Find(take.left,0).fingers;
            LabPreview.Call(window,"SelectTrack",1,T(40));LabPreview.Call(window,"SetFingerPose",LabGripTools.Preset(preview.Animator,HandSide.Left,2));
            LabPreview.Call(window,"SetFingerTransition",LabFingerBlend.Smooth,20);
            var target=UnityEngine.Object.Instantiate((HandPoseData)LabPreview.Get(window,"fingers"));
            try
            {
                LabAssetIO.NormalizeFingerPose(target,preview);string before=EditorJsonUtility.ToJson(take);string action=EditorJsonUtility.ToJson(take.action);
                foreach(int frame in new[]{0,4,20,30,40})
                {
                    LabPreview.Call(window,"SeekGripTransitionPreview",frame);
                    float weight=frame<=20?0:Mathf.SmoothStep(0,1,(frame-20)/20f);
                    var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);
                    Quaternion expected=Quaternion.Slerp(baseline.GetEntry(FingerId.Index,1).localRotation,target.GetEntry(FingerId.Index,1).localRotation,weight);
                    Require(Quaternion.Angle(expected,bone.localRotation)<.05f,"visual transition respects base through F20 and adapts through F40: F"+frame);
                    Require((float)LabPreview.Get(window,"progress")==T(40),"preview does not move the destination frame");
                    Require(before==EditorJsonUtility.ToJson(take)&&action==EditorJsonUtility.ToJson(take.action),"preview never inserts target or alters source channels");
                }
                LabPreview.Call(window,"StopGripTransitionPreview");
                Require(take.left.Count(k=>k.fingers!=null)==1,"cancel visual preview keeps only saved base");
                LabPreview.Call(window,"CommitEdits",true,false);
                Require(take.left.Count(k=>k.fingers!=null)==2&&Mathf.Abs(take.left.Last().fingerBlendDuration*120-20)<.001f,"save applies the explicit 20-frame transition");
            }
            finally{UnityEngine.Object.DestroyImmediate(target);}
        }
        private static void CheckCombined(AnimationLabWindow window,AnimationLabTake take,LabPreview preview)
        {
            float T(int frame)=>LabTimeline.Time(frame,take.action.duration);
            var fingers=take.left.Where(k=>k.fingers!=null).Select(k=>k.fingers).ToArray();
            foreach(int frame in new[]{0,40})
            {
                LabPreview.Call(window,"SelectTrack",0,T(frame));LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");
                LabPreview.Set(window,"followHands",1);LabPreview.Set(window,"includeGroupFingers",false);
                LabPreview.Set(window,"wristReplacementApproved",true);LabPreview.Set(window,"objectReplacementApproved",true);
                LabPreview.Call(window,"BeginObjectEdit",false);
                if(frame==40)
                {
                    var part=preview.Objects.ResolvePart("AK12Rig/Root/Mag");
                    var p=(Vector3)LabPreview.Get(window,"keyPosition");var r=(Vector3)LabPreview.Get(window,"keyRotation");
                    LabPreview.Set(window,"keyPosition",p+part.parent.InverseTransformVector(new Vector3(.05f,.03f,0)));
                    LabPreview.Set(window,"keyRotation",r+new Vector3(12,25,5));
                }
                LabPreview.Call(window,"CommitObject",true,false);
            }
            Require(LabCoordinatedPaths.Segments(take).Count()==1,"saving two group points creates one common hand-piece segment automatically: "+string.Join(";",take.parts.SelectMany(p=>p.keys).Select(k=>$"{k.time:F4} left={k.leftContact?.active} right={k.rightContact?.active}")));
            Require(fingers.All(p=>take.left.Any(k=>k.fingers==p)),"common path compilation preserves finger objects and transition");
            var partTarget=preview.Objects.ResolvePart("AK12Rig/Root/Mag");
            LabPreview.Call(window,"Seek",T(20));
            Undo.IncrementCurrentGroup();
            LabPreview.Call(window,"SetPathPreset",LabPathMode.Cinematic,partTarget.parent.InverseTransformVector(Vector3.up*.05f),0f,0f);
            Undo.FlushUndoRecordObjects();var curve=take.action.leftHandMotion.position.Evaluate(T(20));
            CheckContacts(take,preview);
            Undo.PerformUndo();Require(take.parts.First().keys.First().pathMode==LabPathMode.Legacy,"Undo common curve restores object controls");
            Undo.PerformRedo();Require(Vector3.Distance(take.action.leftHandMotion.position.Evaluate(T(20)),curve)<.000001f,"Redo restores matching compiled wrist curve");
            LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");LabPreview.Call(window,"Seek",T(20));
            LabPreview.Call(window,"SetPathPreset",LabPathMode.Bezier,partTarget.parent.InverseTransformVector(Vector3.up*.04f),0f,0f);
            CheckContacts(take,preview);
            Vector3 before=take.action.leftHandMotion.position.Evaluate(T(20));LabCoordinatedPaths.Compile(take,preview);
            Require(Vector3.Distance(before,take.action.leftHandMotion.position.Evaluate(T(20)))<.00001f,"repeated common compilation is stable");
            CheckPartBake(take,preview);
            // The same compiler also compensates fades when the linked segment is inside them.
            var keys=take.parts.First().keys;keys.First().time=.2f;keys.Last().time=.8f;
            take.action.blendIn=take.action.blendOut=.8f;LabCoordinatedPaths.Compile(take,preview);
            CheckContacts(take,preview);
            LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");LabPreview.Call(window,"Seek",.2f);
            Vector3 contact=take.parts.First().keys.First().leftContact.position;
            preview.Evaluate(take,.2f);Vector3 wrist=preview.Equipment.handIK.leftHand.target.position;
            LabPreview.Call(window,"CommitObject",true,false);
            Require(Vector3.Distance(contact,take.parts.First().keys.First().leftContact.position)<.000001f,"recapture inside a 50% fade reuses original contact");
            preview.Evaluate(take,.2f);Require(Vector3.Distance(wrist,preview.Equipment.handIK.leftHand.target.position)<.0001f,"recapture does not compensate the same fade twice");
            take.parts.First().keys.First().breakLeftContact=true;
            Require(!LabCoordinatedPaths.Segments(take).Any()&&take.leftContactMotion,"detaching last segment retains compensation provenance");
            LabPreview.Call(window,"SyncSelection");LabPreview.Call(window,"CommitObject",true,false);
            Require(Vector3.Distance(contact,take.parts.First().keys.First().leftContact.position)<.00001f,"recapture after detaching last segment still retains effective contact");
            preview.Evaluate(take,.2f);Require(Vector3.Distance(wrist,preview.Equipment.handIK.leftHand.target.position)<.0001f,"detach and recapture cannot amplify compensated wrist");
            CheckRejectedLink(window,take);
            CheckNearTimeFingerTransfer(window,take,preview);
            CheckNeighbours();
        }
        private static void CheckNearTimeFingerTransfer(AnimationLabWindow window,AnimationLabTake original,LabPreview preview)
        {
            var source=LabAssetIO.Clone(original);AnimationLabTake candidate=null;
            try
            {
                foreach(var k in source.left)if(k.fingers!=null)UnityEngine.Object.DestroyImmediate(k.fingers);
                source.left.Clear();
                var old=LabGripTools.Open(preview.Animator,HandSide.Left);
                source.left.Add(new LabHandKey{time=.2000005f,fingers=old,fingersOnly=true});
                candidate=LabAssetIO.Clone(source);var pose=LabGripTools.Preset(preview.Animator,HandSide.Left,2);
                var previous=candidate.left.First().fingers;UnityEngine.Object.DestroyImmediate(previous);
                LabTimeline.RecordFingers(candidate.left,.2f,pose);
                Require(candidate.left.First().time!=.2f,"near-time test retains original off-grid key time");
                LabPreview.Set(window,"draft",source);
                LabPreview.Call(window,"ApplyCoordinatedCandidate",candidate,"part:AK12Rig/Root/Mag",new[]{HandSide.Left},new HashSet<(HandSide,float)>{(HandSide.Left,.2f)});
                Require(source.left.First().fingers==pose&&source.left.First().fingers!=old,"new finger pose survives epsilon-matched transfer");
            }
            finally{LabPreview.Set(window,"draft",original);LabAssetIO.DestroyDraft(candidate);LabAssetIO.DestroyDraft(source);}
        }
        private static void CheckRejectedLink(AnimationLabWindow window,AnimationLabTake take)
        {
            var keys=take.parts.First().keys;
            take.action.followAnimatedRightHand=true;LabPreview.Set(window,"followHands",2);
            string data=EditorJsonUtility.ToJson(take),action=EditorJsonUtility.ToJson(take.action);
            ExpectFailure(()=>LabPreview.Call(window,"LinkObjectSegment",keys.First(),keys.Last(),true),"right-hand ownership rejection");
            Require(data==EditorJsonUtility.ToJson(take)&&action==EditorJsonUtility.ToJson(take.action),"rejected link leaves original keys, contacts and recipe intact");
            ExpectFailure(()=>LabPreview.Call(window,"CommitObject",true,false),"right-hand group rejection");
            Require(data==EditorJsonUtility.ToJson(take)&&action==EditorJsonUtility.ToJson(take.action),"rejected group never partially replaces wrist channels");
            take.action.followAnimatedRightHand=false;LabPreview.Set(window,"followHands",1);
        }
        private static void CheckNeighbours()
        {
            var take=LabAssetIO.Clone(null);
            try
            {
                var keys=new List<LabWeaponKey>();
                foreach(float t in new[]{0f,.4f,.8f})keys.Add(new LabWeaponKey{time=t,leftContact=new LabPathContact{active=true}});
                take.parts.Add(new LabObjectTrack{path="test",keys=keys});
                Require(LabCoordinatedPaths.Segments(take).Count()==2,"three group points have two intervals");
                keys[1].breakLeftContact=true;
                Require(LabCoordinatedPaths.Segments(take).Single().from==keys[0],"detaching the second interval does not detach its neighbour");
                keys[0].leftContact.active=false;
                Require(!LabCoordinatedPaths.Segments(take).Any(),"inactive inline contacts cannot accidentally link legacy keys");
            }
            finally{LabAssetIO.DestroyDraft(take);}
        }
        private static void ExpectFailure(Action call,string text)
        {try{call();}catch(Exception e){Require(e.GetBaseException() is InvalidOperationException,text);return;}Require(false,text);}
        private static void CheckContacts(AnimationLabTake take,LabPreview preview)
        {
            var segment=LabCoordinatedPaths.Segments(take).Single();var a=segment.from.leftContact;var b=segment.to.leftContact;
            foreach(float u in new[]{0f,.125f,.25f,.5f,.75f,.875f,1f})
            {
                float t=Mathf.Lerp(segment.from.time,segment.to.time,u);
                preview.EnableHand(HandSide.Left);preview.Evaluate(take,t);
                var part=LabCoordinatedPaths.Target(preview,segment.objectId);var chain=preview.Equipment.handIK.leftHand;
                Vector3 expected=part.TransformPoint(Vector3.Lerp(a.position,b.position,Mathf.SmoothStep(0,1,u)));
                Quaternion rotation=part.rotation*Quaternion.Slerp(Quaternion.Euler(a.euler),Quaternion.Euler(b.euler),Mathf.SmoothStep(0,1,u));
                Require(Vector3.Distance(chain.target.position,expected)<.001f,"hand follows exact common piece path between endpoints at "+u);
                Require(Quaternion.Angle(chain.target.rotation,rotation)<.3f,"common rotation retains contact through piece turns");
            }
            float middle=(segment.from.time+segment.to.time)*.5f;
            preview.Evaluate(take,middle,-12,15,true);var target=LabCoordinatedPaths.Target(preview,segment.objectId);
            Require(Vector3.Distance(preview.Equipment.handIK.leftHand.target.position,target.TransformPoint(Vector3.Lerp(a.position,b.position,.5f)))<.001f,"common contact survives look and Aim");
        }
        private static void CheckPartBake(AnimationLabTake take,LabPreview preview)
        {
            var clip=LabObjectAuthoring.BakeParts(take,preview);var exported=LabAssetIO.Clone(take);
            try
            {
                exported.parts.Clear();exported.action.weaponClip=clip;
                foreach(float t in new[]{.05f,.12f,.2f,.3f})
                {
                    preview.Evaluate(take,t);var part=preview.Objects.ResolvePart("AK12Rig/Root/Mag");Vector3 p=part.position,w=preview.Equipment.handIK.leftHand.target.position;
                    preview.Evaluate(exported,t);
                    Require(Vector3.Distance(p,part.position)<.0001f&&Vector3.Distance(w,preview.Equipment.handIK.leftHand.target.position)<.0001f,"ordinary compiled curves and Generic part clip match preview without a runtime constraint");
                }
            }
            finally{LabAssetIO.DestroyDraft(exported);UnityEngine.Object.DestroyImmediate(clip);}
        }
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException("[AnimationLabCombinedChecks] "+text);}
    }
}
