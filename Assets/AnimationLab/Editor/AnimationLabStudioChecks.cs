using System;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabStudioChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar estudio visual y pruebas temporales")]
        public static void Run()
        {
            int count=EditorSceneManager.previewSceneCount;string scene=File.ReadAllText(LabPreview.ScenePath);
            TestCinematic();
            foreach(SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))Test(mode);
            Require(count==EditorSceneManager.previewSceneCount&&scene==File.ReadAllText(LabPreview.ScenePath),"isolated cleanup and source scene preservation");
            Debug.Log("[AnimationLabStudioChecks] PASS: temporary before/after, deterministic rebuild, cancel/accept/Undo, visual points, phases, variants, cinematic curves/bake and unchanged gameplay.");
        }
        public static void RunAll(){Run();AnimationLabHandTemplateChecks.RunAll();}

        private static void TestCinematic()
        {
            var keys=new System.Collections.Generic.List<LabWeaponKey>{new LabWeaponKey{time=0,pathMode=LabPathMode.Cinematic,bend=Vector3.up*.1f},new LabWeaponKey{time=1,position=Vector3.right}};
            var curves=LabPathCurves.Objects(keys);
            Require(Vector3.Distance(curves.Evaluate(.5f),new Vector3(.5f,.1f,0))<.000001f,"cinematic arc reaches editable midpoint");
            Require(curves.x.keys[0].outTangent==0&&curves.x.keys.Last().inTangent==0,"cinematic settles at both ends");
            keys[0].departureSpeed=1;keys[0].arrivalSpeed=.7f;curves=LabPathCurves.Objects(keys);
            Require(Mathf.Abs(curves.x.keys[0].outTangent-1)<.00001f&&Mathf.Abs(curves.x.keys.Last().inTangent-.7f)<.00001f,"departure and arrival velocities compiled");
            var seconds=LabPathCurves.Seconds(curves.y,2.3f);
            Require(Mathf.Abs(seconds.Evaluate(.37f*2.3f)-curves.y.Evaluate(.37f))<.000001f,"cinematic seconds conversion is exact");
        }
        private static void Test(SpineDriveMode mode)
        {
            var window=ScriptableObject.CreateInstance<AnimationLabWindow>();AnimationLabTake saved=null;string variantPath=null;
            try
            {
                LabPreview.Set(window,"mode",mode);LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"NewDraft",0);
                var source=(AnimationLabTake)LabPreview.Get(window,"draft");var preview=(LabPreview)LabPreview.Get(window,"preview");
                source.action.duration=2;source.handInsert.start=.2f;source.handInsert.end=.8f;
                source.handInsert.source=LabHandSource.Procedural;
                int ammo=preview.Equipment.Runtime.Current.TotalLoadedRounds;uint commits=preview.Equipment.Runtime.Current.Action.CommitVersion;
                LabPreview.Call(window,"CreateGesturePoints");Require(source.handInsert.points.Count==7&&source.left.Count==0,"visual authoring does not create wrist keys");
                TestPointAuthoring(source,preview);
                var original=EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.action);
                LabPreview.Call(window,"StartHandTrial");
                var trial=(AnimationLabTake)LabPreview.Get(window,"handTrialSource");var candidate=(AnimationLabTake)LabPreview.Get(window,"handCandidate");
                Require(candidate.left.Count>0&&source.left.Count==0,"candidate separated from original");
                LabPreview.Set(window,"trialBefore",true);LabPreview.Call(window,"EvaluateNow",false);
                Require(preview.Equipment.Runtime.Current.Action.Definition==trial.action,"before displays original recipe");
                LabPreview.Set(window,"trialBefore",false);LabPreview.Call(window,"EvaluateNow",false);
                Require(preview.Equipment.Runtime.Current.Action.Definition==candidate.action,"after displays candidate recipe");
                trial.handInsert.phaseTimes=LabGestureAuthoring.Defaults(trial.handInsert.template);trial.handInsert.phaseTimes[3]+=.035f;
                trial.handInsert.useStyle=true;trial.handInsert.energy=.75f;trial.handInsert.recovery=.8f;
                trial.handInsert.points[3].position+=Vector3.left*.01f;
                for(int i=0;i<=20;i++)Require(Mathf.Abs(LabGestureAuthoring.Unmap(trial.handInsert,LabGestureAuthoring.Map(trial.handInsert,i/20f))-i/20f)<.00001f,"phase/style map monotone inverse");
                trial.handInsert.settle=.02f;var phaseBounds=LabGestureAuthoring.Bounds(trial.handInsert);
                Require(Mathf.Abs(LabGestureAuthoring.Map(trial.handInsert,phaseBounds[4]+.01f)-LabGestureAuthoring.Defaults(trial.handInsert.template)[4])<.00001f,"settlement pause holds release phase without changing duration");
                trial.handInsert.settle=0;
                LabPreview.Call(window,"GestureChanged");LabPreview.Call(window,"RebuildHandTrial");
                candidate=(AnimationLabTake)LabPreview.Get(window,"handCandidate");string motion=JsonUtility.ToJson(candidate.action.leftHandMotion);
                LabPreview.Call(window,"RebuildHandTrial");candidate=(AnimationLabTake)LabPreview.Get(window,"handCandidate");
                Require(JsonUtility.ToJson(candidate.action.leftHandMotion)==motion,"repeated trials always sample original, not previous candidate");
                Require(EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.action)==original,"trial edits never mutate original channels or metadata");
                LabPreview.Call(window,"CancelHandTrial",true);
                Require((AnimationLabTake)LabPreview.Get(window,"handCandidate")==null&&source.left.Count==0&&EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.action)==original,"cancel restores original without inserting");
                LabPreview.Call(window,"StartHandTrial");source.action.duration=2.1f;bool staleBlocked=false;
                trial=(AnimationLabTake)LabPreview.Get(window,"handTrialSource");trial.handInsert.points[0].label="Recovery recipe";
                var recovery=(AnimationLabTake)LabPreview.Call(window,"CreateTrialRecoveryDraft");
                try{Require(recovery.action.duration==2.1f&&recovery.left.Count==0&&recovery.handInsert.points[0].label=="Recovery recipe","recovery preserves externally changed draft and working recipe, never candidate keys");}
                finally{LabAssetIO.DestroyDraft(recovery);}
                try{LabPreview.Call(window,"AcceptHandTrial");}catch(System.Reflection.TargetInvocationException ex){staleBlocked=ex.InnerException is InvalidOperationException;}
                Require(staleBlocked&&source.left.Count==0,"accept refuses a changed original draft");source.action.duration=2;LabPreview.Call(window,"CancelHandTrial",true);
                LabPreview.Call(window,"StartHandTrial");trial=(AnimationLabTake)LabPreview.Get(window,"handTrialSource");
                LabPreview.Set(window,"progress",.45f);LabPreview.Call(window,"AddGesturePointAtTime");
                Require(trial.handInsert.points.Count==8&&source.handInsert.points.Count==7,"visual point added only to working recipe");
                LabPreview.Call(window,"AcceptHandTrial");
                Require(source.left.Count>0&&source.handInsert.points.Count==8,"accept transfers candidate and working recipe");
                Undo.FlushUndoRecordObjects();Undo.PerformUndo();Require(source.left.Count==0&&source.handInsert.points.Count==7,"accept can be undone");
                Undo.PerformRedo();Require(source.left.Count>0&&source.handInsert.points.Count==8,"redo keeps finger resources alive");
                var visible=(float[])LabPreview.Call(window,"VisibleTimes",0);Require(visible.Length<=40&&source.left.Count>100,"technical samples hidden without deleting keys");
                var b=LabGestureAuthoring.Bounds(source.handInsert);float phaseTime=(b[2]+b[3])*.5f;
                source.handInsert.phaseGrip=Enumerable.Repeat(-1f,6).ToArray();source.handInsert.phaseGrip[2]=.15f;
                Require(Mathf.Abs(LabGestureAuthoring.Grip(source.handInsert,phaseTime,.8f)-.15f)<.00001f,"phase finger override");
                LabGestureAuthoring.RestorePhase(source.handInsert,2);Require(source.handInsert.phaseGrip[2]<0,"phase restore clears only selected finger override");
                TestBake(source,preview);
                saved=LabAssetIO.Save(source,preview,"__StudioCheck");string takePath=AssetDatabase.GetAssetPath(saved);AssetDatabase.ImportAsset(takePath,ImportAssetOptions.ForceUpdate);
                Require(AssetDatabase.LoadAssetAtPath<AnimationLabTake>(takePath).handInsert.points.Count>=7,"visual points persist in take");
                var variant=ScriptableObject.CreateInstance<LabHandVariant>();variant.equipmentId=source.equipmentId;variant.settings=LabGestureAuthoring.Copy(source.handInsert);
                variantPath=AssetDatabase.GenerateUniqueAssetPath(LabAssetIO.Output+"/__StudioVariant.asset");AssetDatabase.CreateAsset(variant,variantPath);EditorUtility.SetDirty(variant);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(variantPath,ImportAssetOptions.ForceUpdate);
                var loaded=AssetDatabase.LoadAssetAtPath<LabHandVariant>(variantPath);Require(loaded.equipmentId==source.equipmentId&&loaded.settings.points.Count==source.handInsert.points.Count,"variant serialization");
                loaded.settings.points[0].position+=Vector3.one;Require(loaded.settings.points[0].position!=source.handInsert.points[0].position,"variant points deeply independent");
                Require(preview.Equipment.Runtime.Current.TotalLoadedRounds==ammo&&preview.Equipment.Runtime.Current.Action.CommitVersion==commits,"trial does not commit gameplay");
                Debug.Log("[AnimationLabStudioChecks] Passed "+mode);
            }
            finally
            {
                LabPreview.Call(window,"CancelHandTrial",false);LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);
                if(saved!=null)AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));if(variantPath!=null)AssetDatabase.DeleteAsset(variantPath);
            }
        }
        private static void TestBake(AnimationLabTake source,LabPreview preview)
        {
            var part=preview.Objects.ResolvePart("AK12Rig/Root/Mag");preview.Evaluate(source,0);
            Vector3 rest=part.localPosition;Vector3 delta=part.parent.InverseTransformVector(Vector3.down*.025f);
            source.parts.Add(new LabObjectTrack{path="AK12Rig/Root/Mag",keys=new System.Collections.Generic.List<LabWeaponKey>{new LabWeaponKey{time=0,position=rest,pathMode=LabPathMode.Cinematic,bend=delta*.25f,departureSpeed=.2f,arrivalSpeed=.4f},new LabWeaponKey{time=1,position=rest+delta}}});
            var clip=LabObjectAuthoring.BakeParts(source,preview);var runtime=LabAssetIO.Clone(source);
            bool blend=preview.Slot.blendWeaponClip;
            try
            {
                runtime.parts.Clear();runtime.action.weaponClip=clip;preview.Slot.blendWeaponClip=true;
                foreach(float t in new[]{.015f,.25f,.5f,.73f,.985f})
                {preview.Evaluate(source,t);Vector3 p=part.position;Quaternion q=part.rotation;preview.Evaluate(runtime,t);Require(Vector3.Distance(p,part.position)<.0001f&&Quaternion.Angle(q,part.rotation)<.1f,"cinematic part bake matches preview with fades");}
            }
            finally{preview.Slot.blendWeaponClip=blend;LabAssetIO.DestroyDraft(runtime);UnityEngine.Object.DestroyImmediate(clip);}
        }
        private static void TestPointAuthoring(AnimationLabTake source,LabPreview preview)
        {
            var r=LabGestureAuthoring.Copy(source.handInsert);
            try
            {
                source.handInsert.hasContact=true;source.handInsert.contactPosition=new Vector3(.02f,.03f,.01f);source.handInsert.contactEuler=new Vector3(48,16,-12);
                var pistol=LabGestureAuthoring.CreatePoints(source,preview);var extreme=pistol.First(p=>Mathf.Abs(p.time-.5f)<.0001f);
                LabHandTemplateLibrary.Gesture(LabHandTemplate.Pistol,.5f,3,out _,out Vector3 turn,out _);
                Require(Quaternion.Angle(Quaternion.Euler(extreme.euler),Quaternion.Euler(source.handInsert.contactEuler)*Quaternion.Euler(turn))<.01f,"visual point rotations compose quaternions like procedural gesture");
                Require(pistol.Any(p=>p.bend.sqrMagnitude>0),"creating visual points retains free-travel arc");
                source.handInsert.points=pistol;source.handInsert.useStyle=true;source.handInsert.reachScale=1.4f;
                var raw=pistol[3].position;var effective=LabGestureAuthoring.Effective(source.handInsert,raw);
                Require(Vector3.Distance(LabGestureAuthoring.Uneffective(source.handInsert,effective),raw)<.000001f,"style-scaled handle transform has exact inverse");
                foreach(var template in new[]{LabHandTemplate.SingleRound,LabHandTemplate.Revolver,LabHandTemplate.Shotgun})
                {
                    source.handInsert.template=template;source.handInsert.repetitions=3;
                    var points=LabGestureAuthoring.CreatePoints(source,preview);
                    Require(points.Count(p=>p.label.Contains("Suministro"))>=3,"visual path keeps each repeated wrist trip");
                    source.handInsert.points=points;
                    var positions=LabPathCurves.Objects(LabGestureAuthoring.PointKeys(source.handInsert));
                    float start=template==LabHandTemplate.Revolver?30/120f:35/120f;
                    for(int n=0;n<3;n++)
                    {
                        float peak=Mathf.Lerp(start,.75f,(n+.45f)/3);
                        var p=points.First(p=>Mathf.Abs(p.time-peak)<.00001f);
                        Require(Vector3.Distance(positions.Evaluate(peak),p.position)<.000001f,"each repeated visual extreme lies on its compiled path");
                    }
                }
            }
            finally{source.handInsert=r;}
        }
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException("[AnimationLabStudioChecks] "+text);}
    }
}
