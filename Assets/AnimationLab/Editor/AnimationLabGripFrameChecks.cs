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
    public static class AnimationLabGripFrameChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar agarres, transiciones y copiar fotogramas")]
        public static void Run()
        {
            string scene=File.ReadAllText(LabPreview.ScenePath);int previews=EditorSceneManager.previewSceneCount;
            foreach(SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))Check(mode);
            Require(scene==File.ReadAllText(LabPreview.ScenePath)&&previews==EditorSceneManager.previewSceneCount,"scene and preview lifetime unchanged");
            Debug.Log("[AnimationLabGripFrameChecks] PASS: loaded initial grip, editable variants, local timed finger transitions, legacy interpolation, independent frame clipboard, Undo/Redo, persistence and finger bake.");
        }
        public static void RunAll(){Run();AnimationLabReferenceChecks.RunAll();}
        private static void Check(SpineDriveMode mode)
        {
            var window=ScriptableObject.CreateInstance<AnimationLabWindow>();var assets=new List<string>();HandPoseData initial=null;
            try
            {
                LabPreview.Set(window,"mode",mode);LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"LoadReloadReference",LabHandSource.Reload);
                var preview=(LabPreview)LabPreview.Get(window,"preview");var take=(AnimationLabTake)LabPreview.Get(window,"draft");
                string definition=EditorJsonUtility.ToJson(preview.Slot.definition);var character=take.action.characterClip;var target=take.action.leftHandTargetMotion;
                int ammo=preview.Equipment.Runtime.Current.TotalLoadedRounds;uint commits=preview.Equipment.Runtime.Current.Action.CommitVersion;
                float T(int frame)=>LabTimeline.Time(frame,take.action.duration);
                LabPreview.Call(window,"SelectTrack",1,T(40));
                initial=(HandPoseData)LabPreview.Call(window,"CaptureInitialGrip");
                string wrist=JsonUtility.ToJson(take.action.leftHandMotion);
                LabPreview.Call(window,"SetFingerPose",LabGripTools.Preset(preview.Animator,HandSide.Left,2));
                LabPreview.Call(window,"SetFingerTransition",LabFingerBlend.Smooth,10);
                Undo.IncrementCurrentGroup();LabPreview.Call(window,"CommitEdits",true,false);Undo.FlushUndoRecordObjects();
                Require(take.left.Count(k=>k.fingers!=null)==2&&take.left.All(k=>k.fingersOnly),"first late grip seeds initial grip without wrist keys");
                Require(take.action.characterClip==character&&take.action.leftHandTargetMotion==target&&JsonUtility.ToJson(take.action.leftHandMotion)==wrist,"grip editing preserves source clip and wrist recipe");
                foreach(int frame in new[]{0,1,4,20,29,30})
                    CheckPose(preview,take,T(frame),initial,"initial weapon-holding grip retained through F "+frame);
                var key=take.left.First(k=>k.time>0);var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);
                preview.Evaluate(take,T(32),rawFingers:true);
                Quaternion expected=Quaternion.Slerp(initial.GetEntry(FingerId.Index,1).localRotation,key.fingers.GetEntry(FingerId.Index,1).localRotation,Mathf.SmoothStep(0,1,.2f));
                Require(Quaternion.Angle(bone.localRotation,expected)<.05f,"smooth transition starts at F30, not at F0");
                CheckPose(preview,take,T(40),key.fingers,"target grip reached at F40");
                Undo.PerformUndo();Require(take.left.All(k=>k.fingers==null),"Undo removes first grip and automatic initial key together");
                Undo.PerformRedo();Require(take.left.Count(k=>k.fingers!=null)==2,"Redo restores both grips with live independent poses");
                LabPreview.Call(window,"SelectTrack",1,T(40));Undo.IncrementCurrentGroup();
                LabPreview.Call(window,"SetFingerTransition",LabFingerBlend.Step,0);Undo.FlushUndoRecordObjects();
                CheckPose(preview,take,T(39),initial,"instant transition holds initial grip until destination");
                Undo.PerformUndo();Require(take.left.Last().fingerBlend==LabFingerBlend.Smooth,"transition mode can be undone");
                LabPreview.Call(window,"SetFingerTransition",LabFingerBlend.Smooth,10);
                var legacyA=new LabHandKey{time=0};var legacyB=new LabHandKey{time=1};
                Require(LabFingerKeyTools.Blend(legacyA,legacyB,.25f)==.25f,"legacy takes retain linear interpolation");
                legacyB.fingerBlend=LabFingerBlend.Linear;legacyB.fingerBlendDuration=.2f;
                Require(LabFingerKeyTools.Blend(legacyA,legacyB,.5f)==0&&Mathf.Abs(LabFingerKeyTools.Blend(legacyA,legacyB,.9f)-.5f)<.0001f,"linear transition has explicit local start");
                CheckVariants(window,preview,initial,assets);
                LabPreview.Call(window,"SyncSelection");
                var saved=LabAssetIO.Save(take,preview,"__GripFrameChecks");assets.Add(AssetDatabase.GetAssetPath(saved));
                AssetDatabase.ImportAsset(assets.Last(),ImportAssetOptions.ForceUpdate);
                var reopened=AssetDatabase.LoadAssetAtPath<AnimationLabTake>(assets.Last());
                Require(reopened.left.Last().fingerBlend==LabFingerBlend.Smooth&&reopened.left.Last().fingerBlendDuration==take.left.Last().fingerBlendDuration,"transition timing survives save/reimport");
                CheckBake(take,saved,preview);
                CheckStepBake(take,preview,saved.action.characterMask);
                CheckClipboard(window,take,preview);
                CheckGroupInitial(window,preview);
                Require(definition==EditorJsonUtility.ToJson(preview.Slot.definition)&&ammo==preview.Equipment.Runtime.Current.TotalLoadedRounds&&commits==preview.Equipment.Runtime.Current.Action.CommitVersion,"shared definition and gameplay untouched");
                Debug.Log("[AnimationLabGripFrameChecks] Passed "+mode);
            }
            finally
            {
                if(initial!=null)UnityEngine.Object.DestroyImmediate(initial);
                LabPreview.Call(window,"CancelHandTrial",false);LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);foreach(string path in assets)AssetDatabase.DeleteAsset(path);
            }
        }
        private static void CheckVariants(AnimationLabWindow window,LabPreview preview,HandPoseData initial,List<string> assets)
        {
            LabPreview.Set(window,"fingerBlendMode",LabFingerBlend.Legacy);
            LabPreview.Call(window,"UseInitialGrip");var pose=(HandPoseData)LabPreview.Get(window,"fingers");
            Require((LabFingerBlend)LabPreview.Get(window,"fingerBlendMode")==LabFingerBlend.Smooth,"applying a new grip on an old key proposes protected smooth transition");
            Require(Quaternion.Angle(pose.GetEntry(FingerId.Index,1).localRotation,initial.GetEntry(FingerId.Index,1).localRotation)<.05f,"prepared initial grip is sampled from loaded animation, not magazine preset");
            string path=AssetDatabase.GenerateUniqueAssetPath(LabAssetIO.Output+"/__GripBase.asset");
            var original=LabObjectAuthoring.SaveGripType("Sostener arma","Desde animación",HandSide.Left,pose,path);assets.Add(path);
            string before=EditorJsonUtility.ToJson(original.fingers);
            pose.GetEntry(FingerId.Index,1).localRotation*=Quaternion.Euler(5,0,0);
            string variantPath=AssetDatabase.GenerateUniqueAssetPath(LabAssetIO.Output+"/__GripVariant.asset");
            var variant=LabObjectAuthoring.SaveGripType("Variante","Desde animación",HandSide.Left,pose,variantPath);assets.Add(variantPath);
            Require(original.fingers!=variant.fingers&&before==EditorJsonUtility.ToJson(original.fingers)&&Quaternion.Angle(original.fingers.GetEntry(FingerId.Index,1).localRotation,variant.fingers.GetEntry(FingerId.Index,1).localRotation)>1,"variants are independent and do not mutate the saved original");
            LabPreview.Set(window,"fingersEdited",false);
        }
        private static void CheckBake(AnimationLabTake take,AnimationLabTake saved,LabPreview preview)
        {
            var exported=LabAssetIO.Clone(take);
            try
            {
                foreach(var key in exported.left.Concat(exported.right))if(key.fingers!=null)UnityEngine.Object.DestroyImmediate(key.fingers);
                exported.left.Clear();exported.right.Clear();exported.action.characterClip=saved.action.characterClip;exported.action.characterMask=saved.action.characterMask;
                foreach(int frame in new[]{0,4,20,30,32,35,40,60})
                {
                    float time=LabTimeline.Time(frame,take.action.duration);preview.Evaluate(take,time);
                    var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);Quaternion expected=bone.localRotation;
                    preview.Evaluate(exported,time);
                    Require(Quaternion.Angle(expected,bone.localRotation)<.7f,"exported finger clip agrees with hold/transition preview at F"+frame);
                }
            }
            finally{LabAssetIO.DestroyDraft(exported);}
        }
        private static void CheckClipboard(AnimationLabWindow window,AnimationLabTake take,LabPreview preview)
        {
            float T(int frame)=>LabTimeline.Time(frame,take.action.duration);
            string left=JsonUtility.ToJson(take.handInsert);var clip=take.action.characterClip;var weaponClip=take.action.weaponClip;
            LabTimeline.RecordWrist(take.right,take.action.rightHandMotion,T(20),new Vector3(.01f,.02f,-.03f),new Vector3(15,20,3));
            var original=take.right.First();original.pathMode=LabPathMode.Bezier;original.outgoing=new Vector3(.01f,.02f,0);
            LabAssetIO.BuildMotion(take.right,take.action.rightHandMotion);
            LabPreview.Call(window,"SelectTrack",2,T(20));LabPreview.Call(window,"CopyFrame",false);
            Vector3 position=original.position;original.position+=Vector3.one; // Clipboard must be a snapshot, not a live key.
            LabPreview.Call(window,"SelectTrack",2,T(90));LabPreview.Call(window,"PasteFrame");Undo.FlushUndoRecordObjects();
            var pasted=LabTimeline.Find(take.right,T(90));
            Require(pasted.position==position&&pasted.pathMode==LabPathMode.Bezier&&pasted.outgoing==original.outgoing&&pasted.fingers==null,"wrist copy keeps independent position/rotation/path data, without copying fingers");
            Undo.PerformUndo();Require(LabTimeline.Find(take.right,T(90))==null,"paste position Undo");
            Undo.PerformRedo();Require(LabTimeline.Find(take.right,T(90))?.position==position,"paste position Redo");
            LabPreview.Call(window,"SelectTrack",1,T(40));var finger=LabTimeline.Find(take.left,T(40)).fingers;
            LabPreview.Call(window,"CopyFrame",false);LabPreview.Call(window,"SelectTrack",1,T(100));LabPreview.Call(window,"PasteFrame");
            var fingerCopy=LabTimeline.Find(take.left,T(100));
            Require(fingerCopy.fingersOnly&&fingerCopy.fingers!=finger&&Quaternion.Angle(fingerCopy.fingers.GetEntry(FingerId.Index,1).localRotation,finger.GetEntry(FingerId.Index,1).localRotation)<.001f,"finger clipboard deep copies only finger channel");
            Require(Mathf.Abs(fingerCopy.fingerBlendDuration*take.action.duration*60-10)<.001f,"copied finger transition retains frame length");
            LabPreview.Call(window,"SelectTrack",3,T(100));
            ExpectFailure(()=>LabPreview.Call(window,"PasteFrame"),"opposite hand clipboard rejected without mirroring");
            LabPreview.Call(window,"SelectTrack",2,T(20));LabPreview.Call(window,"CopyFrame",true);
            LabPreview.Call(window,"SelectTrack",2,T(130));LabPreview.Call(window,"PasteFrame");
            Require(LabTimeline.Find(take.right,T(130)).fingers!=null&&!LabTimeline.Find(take.right,T(130)).fingersOnly,"whole-hand copy includes wrist and independent fingers");
            take.weapon.Add(new LabWeaponKey{time=T(20),position=new Vector3(.02f,0,0),pathMode=LabPathMode.Arc,bend=new Vector3(0,.03f,0)});
            LabAssetIO.BuildWeaponMotion(take);LabPreview.Call(window,"SelectTrack",4,T(20));LabPreview.Call(window,"CopyFrame",false);
            LabPreview.Call(window,"SelectTrack",4,T(100));LabPreview.Call(window,"PasteFrame");
            Require(take.weapon.Last().position==take.weapon.First().position&&take.weapon.Last().bend==take.weapon.First().bend,"weapon position/path copied");
            LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");LabPreview.Call(window,"Seek",T(20));
            var part=preview.Objects.ResolvePart("AK12Rig/Root/Mag");
            take.parts.Add(new LabObjectTrack{path="AK12Rig/Root/Mag",keys=new List<LabWeaponKey>{new LabWeaponKey{time=T(20),position=part.localPosition,euler=part.localEulerAngles}}});
            LabPreview.Call(window,"SyncSelection");LabPreview.Call(window,"CopyFrame",false);LabPreview.Call(window,"Seek",T(100));LabPreview.Call(window,"PasteFrame");
            Require(take.parts.Single().keys.Count==2&&take.parts.Single().keys[0].position==take.parts.Single().keys[1].position,"piece frame copy stays on selected piece");
            LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Charge");ExpectFailure(()=>LabPreview.Call(window,"PasteFrame"),"different piece rejected");
            Require(take.action.characterClip==clip&&take.action.weaponClip==weaponClip&&left==JsonUtility.ToJson(take.handInsert),"clipboard preserves clips and reload recipe");
        }
        private static void CheckStepBake(AnimationLabTake source,LabPreview preview,AvatarMask mask)
        {
            var take=LabAssetIO.Clone(source);AnimationClip clip=null;AnimationLabTake exported=null;
            try
            {
                take.left.Last().fingerBlend=LabFingerBlend.Step;
                var a=LabGripTools.Open(preview.Animator,HandSide.Right);var b=LabGripTools.Preset(preview.Animator,HandSide.Right,2);
                take.right.Add(new LabHandKey{time=0,fingers=a,fingersOnly=true});
                take.right.Add(new LabHandKey{time=LabTimeline.Time(50,take.action.duration),fingers=b,fingersOnly=true,fingerBlend=LabFingerBlend.Smooth,fingerBlendDuration=30/(60f*take.action.duration)});
                clip=LabAssetIO.BakeFingers(take,preview);exported=LabAssetIO.Clone(take);
                foreach(var k in exported.left.Concat(exported.right))if(k.fingers!=null)UnityEngine.Object.DestroyImmediate(k.fingers);
                exported.left.Clear();exported.right.Clear();exported.action.characterClip=clip;exported.action.characterMask=mask;
                foreach(float frame in new[]{39f,39.95f,40f,40.05f,41f})
                {
                    float time=frame/(60f*take.action.duration);preview.Evaluate(take,time);
                    var bone=preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);Quaternion expected=bone.localRotation;
                    preview.Evaluate(exported,time);
                    Require(Quaternion.Angle(expected,bone.localRotation)<.7f,"instant grip export keeps exact change without preblend at F"+frame);
                }
                var rightCurves=AnimationUtility.GetCurveBindings(clip).Where(bnd=>bnd.propertyName.StartsWith("RightHand."));
                Require(rightCurves.All(bnd=>AnimationUtility.GetEditorCurve(clip,bnd).keys.All(k=>!float.IsInfinity(k.inTangent)&&!float.IsInfinity(k.outTangent))),"instant change on left does not add steps to right-hand curves");
            }
            finally{LabAssetIO.DestroyDraft(exported);if(clip!=null)UnityEngine.Object.DestroyImmediate(clip);LabAssetIO.DestroyDraft(take);}
        }
        private static void CheckGroupInitial(AnimationLabWindow window,LabPreview preview)
        {
            LabPreview.Call(window,"LoadReloadReference",LabHandSource.Reload);
            var take=(AnimationLabTake)LabPreview.Get(window,"draft");
            LabPreview.Call(window,"SelectTrack",0,LabTimeline.Time(40,take.action.duration));
            var initial=(HandPoseData)LabPreview.Call(window,"CaptureInitialGrip");
            try
            {
                LabPreview.Set(window,"followHands",1);LabPreview.Set(window,"includeGroupFingers",true);
                LabPreview.Call(window,"SelectObject","part:AK12Rig/Root/Mag");LabPreview.Set(window,"wristReplacementApproved",true);LabPreview.Set(window,"objectReplacementApproved",true);
                Undo.IncrementCurrentGroup();LabPreview.Call(window,"CommitObject",true,false);Undo.FlushUndoRecordObjects();
                Require(take.left.Any(k=>k.time==0&&k.fingers!=null)&&take.left.Last().fingerBlend==LabFingerBlend.Smooth,"first coordinated hand-piece key also preserves initial grip and local transition");
                CheckPose(preview,take,LabTimeline.Time(4,take.action.duration),initial,"coordinated group keeps original grip at F4");
                Undo.PerformUndo();Require(take.left.All(k=>k.fingers==null)&&take.parts.Count==0,"group Undo removes generated initial grip and piece key together");
            }
            finally{UnityEngine.Object.DestroyImmediate(initial);}
        }
        private static void CheckPose(LabPreview preview,AnimationLabTake take,float time,HandPoseData expected,string message)
        {
            preview.Evaluate(take,time,rawFingers:true);
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)
            {
                var entry=expected.GetEntry((FingerId)f,j);if(entry==null)continue;
                Require(Quaternion.Angle(preview.Animator.GetBoneTransform(entry.bone).localRotation,entry.localRotation)<.05f,message);
            }
        }
        private static void ExpectFailure(Action call,string text)
        {try{call();}catch(Exception e){Require(e.GetBaseException() is InvalidOperationException,text);return;}Require(false,text);}
        private static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException("[AnimationLabGripFrameChecks] "+text);}
    }
}
