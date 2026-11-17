using System;
using System.IO;
using System.Linq;
using System.Text;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabDynamicsChecks
    {
        internal const string BasePath="Assets/AnimationLab/Authored/BASE CORRECTA.asset";
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar movimiento natural y diagnóstico de agarres")]
        public static void Run()
        {
            var original=AssetDatabase.LoadAssetAtPath<AnimationLabTake>(BasePath);
            if(original==null)throw new InvalidOperationException("Falta la toma BASE CORRECTA para esta comprobación específica.");
            string bytes=Convert.ToBase64String(File.ReadAllBytes(BasePath));Analyze(original);
            foreach(SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))Check(original,mode);
            Require(bytes==Convert.ToBase64String(File.ReadAllBytes(BasePath)),"original BASE CORRECTA asset unchanged byte-for-byte");
            Debug.Log("[AnimationLabDynamicsChecks] PASS: real BASE CORRECTA analysis, deterministic bounded weapon layer, original anchors/contacts, trial/cancel/accept/Undo, persistence/restore and F150 grip status/hold.");
        }
        public static void RunAll(){Run();AnimationLabCombinedChecks.RunAll();}
        internal static float Difference(HandPoseData a,HandPoseData b)
        {return LabGripTools.PoseDifference(a,b);}
        private static void Analyze(AnimationLabTake take)
        {
            var poses=take.left.Where(k=>k.fingers!=null).OrderBy(k=>k.time).ToArray();
            var report=new StringBuilder();
            report.AppendLine("BASE CORRECTA · análisis de datos (sin modificar el original)");
            report.AppendLine($"Duración {take.action.duration:F3} s; final F{LabTimeline.LastFrame(take.action.duration)}; arma {take.weapon.Count} controles; muñeca izquierda {take.left.Count(k=>!k.fingersOnly)} muestras; dedos {poses.Length} poses.");
            foreach(var key in take.weapon)report.AppendLine($"Arma F{LabTimeline.Frame(key.time,take.action.duration)}: offset={key.position:F4} m, giro={key.euler:F2}, modo={key.pathMode}.");
            foreach(var key in poses)report.AppendLine($"Dedos F{LabTimeline.Frame(key.time,take.action.duration)}: modo de llegada={key.fingerBlend}, duración={key.fingerBlendDuration*take.action.duration*60:F1} F; diferencia máx. frente F0={Difference(poses[0].fingers,key.fingers):F2}°.");
            report.AppendLine($"Contactos compilados: {LabCoordinatedPaths.Segments(take).Count()} tramos. Salida global {take.action.blendOut*60:F1} fotogramas.");
            report.AppendLine("Suave es un modo de llegada, no un tipo de agarre. En fotogramas sin clave el panel anterior mostraba un valor por defecto; no era un cambio guardado.");
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp","opencode","AnimationLab.BASE_CORRECTA.Analisis.txt");
            File.WriteAllText(path,report.ToString());Debug.Log("[AnimationLabDynamicsChecks] ANALYSIS\n"+report);
        }
        private static void Check(AnimationLabTake original,SpineDriveMode mode)
        {
            var window=ScriptableObject.CreateInstance<AnimationLabWindow>();AnimationLabTake saved=null;
            try
            {
                LabPreview.Set(window,"mode",mode);LabPreview.Call(window,"StartPreview");LabPreview.Call(window,"Load",original);
                var take=(AnimationLabTake)LabPreview.Get(window,"draft");var preview=(LabPreview)LabPreview.Get(window,"preview");
                string definition=EditorJsonUtility.ToJson(preview.Slot.definition);int ammo=preview.Equipment.Runtime.Current.TotalLoadedRounds;
                uint commits=preview.Equipment.Runtime.Current.Action.CommitVersion;
                float T(int f)=>LabTimeline.Time(f,take.action.duration);
                LabPreview.Call(window,"SelectTrack",1,T(150));var mode150=(LabFingerBlend)LabPreview.Get(window,"fingerBlendMode");
                LabPreview.Set(window,"arrivalGrip",1);LabPreview.Call(window,"Seek",T(151));
                Require((LabFingerBlend)LabPreview.Get(window,"fingerBlendMode")==mode150&&(int)LabPreview.Get(window,"arrivalGrip")==0,"no phantom Smooth or stale base type after F150");
                LabPreview.Call(window,"Seek",T(150));string wrist=JsonUtility.ToJson(take.action.leftHandMotion),parts=JsonUtility.ToJson(take.parts.First());
                var future=take.left.Where(k=>k.fingers!=null&&k.time>T(150)+LabTimeline.TimeEpsilon).ToArray();Require(future.Length>0,"real fixture contains later finger poses");
                LabPreview.Call(window,"UseInitialGrip");LabPreview.Call(window,"HoldFingerGripToEnd",true);Undo.FlushUndoRecordObjects();
                var held=take.left.Last(k=>k.fingers!=null).fingers;
                foreach(int frame in new[]{150,151,170,190,198,200})
                {
                    preview.Evaluate(take,T(frame),rawFingers:true);
                    var pose=ScriptableObject.CreateInstance<HandPoseData>();pose.Side=HandSide.Left;
                    try{pose.Capture(preview.Animator);Require(Difference(held,pose)<.1f,"held raw finger pose stays unchanged after F150");}
                    finally{UnityEngine.Object.DestroyImmediate(pose);}
                }
                Require(wrist==JsonUtility.ToJson(take.action.leftHandMotion)&&parts==JsonUtility.ToJson(take.parts.First()),"hold changes only finger channel");
                Undo.PerformUndo();Require(take.left.Count(k=>k.fingers!=null)==original.left.Count(k=>k.fingers!=null),"hold Undo restores original future poses");
                Undo.PerformRedo();Require(take.left.Last(k=>k.fingers!=null).fingerLabel=="Base inicial · sostener arma","hold label persists through Redo");
                LabPreview.Call(window,"Load",original);take=(AnimationLabTake)LabPreview.Get(window,"draft");
                int fingerCount=take.left.Count(k=>k.fingers!=null);
                var contactFingers=take.left.Where(k=>k.fingers!=null).Select(k=>k.fingers).ToArray();
                string weaponBefore=JsonUtility.ToJson(take.action.weaponPosition)+JsonUtility.ToJson(take.action.weaponRotation);
                string wristBefore=JsonUtility.ToJson(take.action.leftHandMotion);int wristKeysBefore=take.left.Count;
                float contactHold=LabCoordinatedPaths.ContactHoldTime(take);
                Require(contactHold>0&&contactHold<1,"fixture has a blend-out before the final frame");
                LabPreview.Call(window,"KeepContactToHold",HandSide.Left,"part:AK12Rig/Root/Mag",true);Undo.FlushUndoRecordObjects();
                var heldPart=take.parts.First(p=>p.path=="AK12Rig/Root/Mag");
                Require(heldPart.keys.Count==4&&heldPart.keys.Last().leftContact!=null&&heldPart.keys.Last().leftContact.active&&Mathf.Abs(heldPart.keys.Last().time-contactHold)<.001f,"contact hold adds an end key with the saved left contact");
                Require(LabCoordinatedPaths.Segments(take).Any(s=>s.side==HandSide.Left&&s.objectId=="part:AK12Rig/Root/Mag"&&Mathf.Abs(s.to.time-contactHold)<.001f),"contact hold compiles the last segment to the fade start");
                Require(fingerCount==take.left.Count(k=>k.fingers!=null)&&contactFingers.All(p=>take.left.Any(k=>k.fingers==p)),"contact hold preserves finger objects and timing");
                Require(weaponBefore==JsonUtility.ToJson(take.action.weaponPosition)+JsonUtility.ToJson(take.action.weaponRotation),"contact hold does not change weapon channels");
                foreach(int frame in new[]{160,180,195})
                {
                    float time=Mathf.Min(T(frame),contactHold-.0001f);preview.Evaluate(take,time,-10,10,true);
                    var segment=LabCoordinatedPaths.Segments(take).Last(s=>s.side==HandSide.Left&&s.to.time>time);
                    var a=LabCoordinatedPaths.Contact(segment.from,HandSide.Left);var b=LabCoordinatedPaths.Contact(segment.to,HandSide.Left);
                    float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(segment.from.time,segment.to.time,time));
                    var target=LabCoordinatedPaths.Target(preview,segment.objectId);var chain=preview.Equipment.handIK.leftHand;
                    Require(Vector3.Distance(chain.target.position,target.TransformPoint(Vector3.Lerp(a.position,b.position,u)))<.001f,"extended contact keeps the wrist on the magazine with look/Aim");
                }
                Undo.PerformUndo();
                Require(take.parts.First(p=>p.path=="AK12Rig/Root/Mag").keys.Count==3,"contact hold Undo restores the original piece keys");
                Require(wristBefore==JsonUtility.ToJson(take.action.leftHandMotion)&&take.left.Count==wristKeysBefore,"contact hold Undo restores the original wrist channel");
                Undo.PerformRedo();Require(take.parts.First(p=>p.path=="AK12Rig/Root/Mag").keys.Count==4,"contact hold Redo restores the extended contact");
                LabPreview.Call(window,"Load",original);take=(AnimationLabTake)LabPreview.Get(window,"draft");
                LabPreview.Call(window,"SelectTrack",4,T(112));string before=EditorJsonUtility.ToJson(take)+EditorJsonUtility.ToJson(take.action);
                var fingerObjects=take.left.Where(k=>k.fingers!=null).Select(k=>k.fingers).ToArray();
                LabPreview.Call(window,"StartWeaponMotionTrial");
                var candidate=(AnimationLabTake)LabPreview.Get(window,"weaponMotionCandidate");
                Require(before==EditorJsonUtility.ToJson(take)+EditorJsonUtility.ToJson(take.action),"trial leaves original draft untouched");
                CheckMotion(take,candidate,preview);
                LabPreview.Call(window,"RebuildWeaponMotionTrial");candidate=(AnimationLabTake)LabPreview.Get(window,"weaponMotionCandidate");
                var repeated=LabWeaponDynamics.Generate(candidate,preview,candidate.weaponDynamics.intensity);
                try
                {
                    Require(repeated.weaponDynamics.sourceKeys.Count==candidate.weaponDynamics.sourceKeys.Count,$"reapplying retains original source controls: repeated={repeated.weaponDynamics.sourceKeys.Count}, source={candidate.weaponDynamics.sourceKeys.Count}, matches={LabWeaponDynamics.MatchesCompiled(candidate)}");
                    for(int i=0;i<=200;i++)
                    {
                        Require(Vector3.Distance(repeated.action.weaponPosition.Evaluate(i/200f),candidate.action.weaponPosition.Evaluate(i/200f))<.000001f,"reapplying layer cannot accumulate position");
                        Require(Vector3.Distance(repeated.action.weaponRotation.Evaluate(i/200f),candidate.action.weaponRotation.Evaluate(i/200f))<.0001f,"reapplying layer cannot accumulate rotation");
                    }
                }
                finally{LabAssetIO.DestroyDraft(repeated);}
                LabPreview.Call(window,"CancelWeaponMotionTrial",true);
                Require(before==EditorJsonUtility.ToJson(take)+EditorJsonUtility.ToJson(take.action),"cancel does not insert procedural motion");
                LabPreview.Call(window,"StartWeaponMotionTrial");Undo.IncrementCurrentGroup();LabPreview.Call(window,"AcceptWeaponMotionTrial");Undo.FlushUndoRecordObjects();
                Require(take.weaponDynamics.applied&&fingerObjects.All(p=>take.left.Any(k=>k.fingers==p)),"accept keeps original finger objects");
                Undo.PerformUndo();Require(!take.weaponDynamics.applied&&take.weapon.Count==original.weapon.Count,"natural motion Undo restores original authoring keys");
                Undo.PerformRedo();Require(take.weaponDynamics.applied,"natural motion Redo restores layer recipe");
                Require(LabWeaponDynamics.MatchesCompiled(take),"accepted/Redo layer remains numerically identifiable");
                saved=LabAssetIO.Save(take,preview,"__DynamicsChecks");
                string path=AssetDatabase.GetAssetPath(saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var reopened=LabAssetIO.Clone(AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path));
                try
                {
                    Require(reopened.weaponDynamics.applied&&reopened.weaponDynamics.sourceKeys.Count==original.weapon.Count,"original motion recipe survives save/reimport");
                    Require(LabWeaponDynamics.MatchesCompiled(reopened),"saved/reimported compiled curves match numerical snapshot");
                    var restored=LabWeaponDynamics.Restore(reopened,preview);
                    try
                    {
                        for(int i=0;i<=60;i++)Require(Vector3.Distance(restored.action.weaponPosition.Evaluate(i/60f),original.action.weaponPosition.Evaluate(i/60f))<.000001f,"remove layer restores exact original weapon curves");
                    }
                    finally{LabAssetIO.DestroyDraft(restored);}
                    var frames=reopened.action.weaponRotation.x.keys;frames[frames.Length/2].value+=1;reopened.action.weaponRotation.x.keys=frames;
                    string edited=JsonUtility.ToJson(reopened.action.weaponRotation);bool refused=false;
                    try{var rejected=LabWeaponDynamics.Restore(reopened,preview);LabAssetIO.DestroyDraft(rejected);}
                    catch(InvalidOperationException){refused=true;}
                    Require(refused&&edited==JsonUtility.ToJson(reopened.action.weaponRotation),"restore refuses to overwrite later manual rotation edits");
                }
                finally{LabAssetIO.DestroyDraft(reopened);}
                LabPreview.Call(window,"RestoreWeaponMotion");
                Require(!take.weaponDynamics.applied&&Vector3.Distance((Vector3)LabPreview.Get(window,"keyPosition"),take.action.weaponPosition.Evaluate((float)LabPreview.Get(window,"progress")))<.000001f,"window restore refreshes editing fields before another position is saved");
                Require(definition==EditorJsonUtility.ToJson(preview.Slot.definition)&&ammo==preview.Equipment.Runtime.Current.TotalLoadedRounds&&commits==preview.Equipment.Runtime.Current.Action.CommitVersion,"shared definition and gameplay untouched");
                Debug.Log("[AnimationLabDynamicsChecks] Passed "+mode);
            }
            finally
            {
                LabPreview.Call(window,"CancelWeaponMotionTrial",false);LabPreview.Call(window,"CancelHandTrial",false);LabPreview.Set(window,"dirty",false);LabPreview.Set(window,"fingersEdited",false);LabPreview.Set(window,"previewingKey",false);LabPreview.Set(window,"objectPending",false);
                UnityEngine.Object.DestroyImmediate(window);if(saved!=null)AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
            }
        }
        private static void CheckMotion(AnimationLabTake source,AnimationLabTake candidate,LabPreview preview)
        {
            float largest=0;var scale=preview.Equipment.weaponPose.PoseRoot.parent;
            foreach(var key in source.weapon)
            {
                Require(Vector3.Distance(candidate.action.weaponPosition.Evaluate(key.time),source.action.weaponPosition.Evaluate(key.time))<.000001f,"authored weapon position anchors are exact");
                Require(Vector3.Distance(candidate.action.weaponRotation.Evaluate(key.time),source.action.weaponRotation.Evaluate(key.time))<.00001f,"authored weapon rotation anchors are exact");
            }
            foreach(var key in candidate.weapon)
            {
                var delta=key.position-source.action.weaponPosition.Evaluate(key.time);
                float physical=(scale!=null?scale.TransformVector(delta):delta).magnitude;
                largest=Mathf.Max(largest,physical);
                Require(physical<=.00601f&&Vector3.Distance(key.euler,source.action.weaponRotation.Evaluate(key.time))<=1.801f,"secondary motion remains bounded");
            }
            Require(largest>.0001f,"natural layer adds real secondary motion rather than only a UI label");
            Require(candidate.action.characterClip==source.action.characterClip&&candidate.action.weaponClip==source.action.weaponClip&&candidate.action.duration==source.action.duration,"dynamics preserves clips and duration");
            foreach(var segment in LabCoordinatedPaths.Segments(candidate))
            foreach(float u in new[]{0f,.25f,.5f,.75f,1f})
            {
                float t=Mathf.Lerp(segment.from.time,segment.to.time,u);preview.EnableHand(segment.side);preview.Evaluate(candidate,t,-10,10,true);
                var a=LabCoordinatedPaths.Contact(segment.from,segment.side);var b=LabCoordinatedPaths.Contact(segment.to,segment.side);
                var part=LabCoordinatedPaths.Target(preview,segment.objectId);var chain=segment.side==HandSide.Left?preview.Equipment.handIK.leftHand:preview.Equipment.handIK.rightHand;
                Require(Vector3.Distance(chain.target.position,part.TransformPoint(Vector3.Lerp(a.position,b.position,Mathf.SmoothStep(0,1,u))))<.001f,"natural weapon motion retains hand-piece contact with look/Aim");
            }
        }
        private static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException("[AnimationLabDynamicsChecks] "+text);}
    }
}
