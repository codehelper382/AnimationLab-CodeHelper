using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private double lastAutoRecoveryAt;
        private string lastRecoveryFingerprint,latestEmergencyPath,takeLineage;
        private bool automaticRecovery=true,showCheckpointHistory;
        private int recoveryInterval=120;
        private LabEmergencyRecovery.Envelope CaptureEmergency()
        {
            var pending=new LabEmergencyRecovery.PendingState
            {
                track=Track,objectId=objectId,progress=progress,position=keyPosition,euler=keyRotation,
                wrist=previewingKey,weapon=weaponPending,item=objectPending,fingers=fingersEdited,
                fingerJson=fingersEdited&&fingers!=null?JsonUtility.ToJson(fingers):null,
                fingerBaseJson=fingersEdited&&fingerBase!=null?JsonUtility.ToJson(fingerBase):null,
                fingerBlend=fingerBlendMode,fingerFrames=fingerTransitionFrames,fingerLabel=fingerPoseLabel,
                followHands=followHands,includeGroupFingers=includeGroupFingers,groupBlend=groupFingerBlend,groupFrames=groupFingerFrames,
                wristApproved=wristReplacementApproved,weaponApproved=weaponReplacementApproved,objectApproved=objectReplacementApproved
            };
            pending.contacts=followContacts.Select(c=>new LabEmergencyRecovery.Contact{side=c.Key,position=c.Value.p,rotation=c.Value.r}).ToList();
            var source=HasHandTrial?CreateTrialRecoveryDraft():draft;
            try
            {
                if(quarantinedRecovery)pending=quarantinedPending;
                if(HasTakeTrial){pending.wrist=pending.weapon=pending.item=pending.fingers=false;pending.fingerJson=pending.fingerBaseJson=null;}
                string guid=saved!=null?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(saved)):null;
                var envelope=LabEmergencyRecovery.Capture(source,takeName,guid,TakeLineage,pending);
                if(!quarantinedRecovery)
                {
                    var anchors=ScriptableObject.CreateInstance<AnimationLabTake>();
                    try{LabAssetIO.CaptureAnchors(anchors,preview);envelope.anchors=anchors.anchors;}
                    finally{DestroyImmediate(anchors);}
                }
                return envelope;
            }
            finally{if(source!=draft)LabAssetIO.DestroyDraft(source);}
        }
        private string TakeLineage => string.IsNullOrEmpty(takeLineage)?(takeLineage=Guid.NewGuid().ToString("N")):takeLineage;
        private void PreserveOnClose()
        {
            LabEmergencyRecovery.Envelope snapshot=null;
            try{snapshot=CaptureEmergency();}
            catch(Exception e){Debug.LogWarning("[AnimationLab] No se pudo preparar respaldo alternativo: "+e.Message);}
            if(quarantinedRecovery&&snapshot!=null)
            {latestEmergencyPath=LabEmergencyRecovery.Write(snapshot,"Reparación pendiente al cerrar");return;}
            try{SaveRecoveryDraft();}
            catch(Exception original)
            {
                if(snapshot==null)throw;
                latestEmergencyPath=LabEmergencyRecovery.Write(snapshot,"Recuperación normal fallida: "+original.Message);
                Debug.LogWarning("[AnimationLab] El guardado normal falló; se conservó un respaldo de emergencia en "+latestEmergencyPath);
            }
        }
        private void TickAutomaticRecovery()
        {
            hasUnsavedChanges=draft!=null&&(dirty||Pending||HasTakeTrial);
            saveChangesMessage="Hay una toma o propuesta sin guardar. Guardar conserva la toma; una prueba sin aceptar se conserva solo como borrador.";
            if(!automaticRecovery||!hasUnsavedChanges||EditorApplication.timeSinceStartup-lastAutoRecoveryAt<recoveryInterval)return;
            lastAutoRecoveryAt=EditorApplication.timeSinceStartup;
            try
            {
                var snapshot=CaptureEmergency();string fingerprint=LabEmergencyRecovery.Fingerprint(snapshot);
                if(fingerprint==lastRecoveryFingerprint)return;
                latestEmergencyPath=LabEmergencyRecovery.Write(snapshot,"Respaldo periódico");
                lastRecoveryFingerprint=fingerprint;notice="Respaldo automático del borrador guardado · "+DateTime.Now.ToString("HH:mm");
            }
            catch(Exception e){notice="No se pudo guardar respaldo automático: "+e.Message;}
        }
        public override void SaveChanges()
        {
            if(draft!=null&&preview!=null)
            {
                if(HasTakeTrial)latestEmergencyPath=LabEmergencyRecovery.Write(CaptureEmergency(),"Guardar al cerrar: candidato sin aceptar");
                else SaveTake();
            }
            base.SaveChanges();
        }
        private void DrawCheckpointList()
        {
            var all=LabEmergencyRecovery.List().OrderByDescending(c=>c.data.createdUtc).ToArray();
            if(all.Length==0)return;
            LabStudioUI.Section("Respaldos automáticos y de emergencia","Conservan el borrador y ajustes pendientes, sin horneado ni aceptación automática de pruebas.");
            showCheckpointHistory=EditorGUILayout.ToggleLeft("Ver también respaldos anteriores de cada toma",showCheckpointHistory);
            var visible=showCheckpointHistory?all:all.GroupBy(c=>c.data.lineage??c.data.name).Select(g=>g.First()).ToArray();
            foreach(var entry in visible.Take(showCheckpointHistory?30:8))
            {
                string date=DateTime.TryParse(entry.data.createdUtc,out var time)?time.ToLocalTime().ToString("dd/MM HH:mm"):entry.data.createdUtc;
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(new GUIContent(entry.data.name+" · "+date,entry.data.reason+"\n"+entry.path),EditorStyles.miniButton))
                        Safe(()=>{OpenCheckpoint(entry.path);GUIUtility.ExitGUI();});
                    using(new EditorGUI.DisabledScope(entry.path.StartsWith("session:")))
                        if(GUILayout.Button("Ver archivo",EditorStyles.miniButton,GUILayout.Width(76)))EditorUtility.RevealInFinder(entry.path);
                }
            }
        }
        private void OpenCheckpoint(string path)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Termina la prueba antes de recuperar otra toma.");
            var envelope=LabEmergencyRecovery.ReadHeader(path);var source=LabEmergencyRecovery.Restore(envelope);
            try
            {
                if(preview!=null&&!CanDiscard())return;
                if(preview==null){preview=new LabPreview(mode);lastTime=EditorApplication.timeSinceStartup;}
                if(LabTakeValidation.Inspect(source).Count>0||PendingRecoveryProblem(envelope.pending)!=null)
                {
                    LoadInvalidRecovery(source);source=null;quarantinedPending=envelope.pending;
                    takeName=envelope.name;takeLineage=envelope.lineage;
                    notice="Respaldo abierto sin evaluar. Corrige los datos indicados y revalida para volver al estudio.";return;
                }
                Load(source);saved=null;takeName=envelope.name;takeLineage=envelope.lineage;dirty=true;
                RestoreCheckpointPending(envelope.pending);
                studioTab=1;notice="Respaldo recuperado con ajustes pendientes. Revisa y guarda una copia normal.";
                EvaluateNow(Pending);
            }
            catch
            {
                if(draft==null)ClosePreview();
                throw;
            }
            finally{LabAssetIO.DestroyDraft(source);}
        }
        private void RestoreCheckpointPending(LabEmergencyRecovery.PendingState p)
        {
            if(p==null)return;
            objectId=p.objectId;followHands=p.followHands;includeGroupFingers=p.includeGroupFingers;groupFingerBlend=p.groupBlend;groupFingerFrames=p.groupFrames;
            SelectTrack(p.track,Mathf.Clamp01(p.progress));
            keyPosition=p.position;keyRotation=p.euler;previewingKey=p.wrist;weaponPending=p.weapon;objectPending=p.item;
            wristReplacementApproved=p.wristApproved;weaponReplacementApproved=p.weaponApproved;objectReplacementApproved=p.objectApproved;
            followContacts.Clear();if(p.contacts!=null)foreach(var c in p.contacts)followContacts[c.side]=(c.position,c.rotation);
            if(p.wrist)preview.EnableHand(side);
            if(p.item)foreach(var hand in FollowingSides())preview.EnableHand(hand);
            if(p.fingers&&p.fingerJson!=null)
            {
                ClearFingerOverride();fingers=CreateInstance<HandPoseData>();JsonUtility.FromJsonOverwrite(p.fingerJson,fingers);
                fingerBase=CreateInstance<HandPoseData>();JsonUtility.FromJsonOverwrite(p.fingerBaseJson??p.fingerJson,fingerBase);
                fingers.hideFlags=fingerBase.hideFlags=HideFlags.HideAndDontSave;fingersEdited=true;
                fingerBlendMode=p.fingerBlend;fingerTransitionFrames=p.fingerFrames;fingerPoseLabel=p.fingerLabel;
            }
        }
        private void DrawRecoveryPreferences()
        {
            automaticRecovery=EditorGUILayout.ToggleLeft("Respaldo automático sin horneado",automaticRecovery);
            int seconds=EditorGUILayout.IntSlider("Cada (segundos)",recoveryInterval,30,900);
            if(seconds!=recoveryInterval){recoveryInterval=seconds;EditorPrefs.SetInt("AnimationLab.RecoveryInterval",seconds);}
            EditorPrefs.SetBool("AnimationLab.AutoRecovery",automaticRecovery);
            EditorGUILayout.LabelField("Los respaldos no sustituyen Guardar copia; nunca insertan una prueba sin aceptar.",EditorStyles.wordWrappedMiniLabel);
        }
    }
}
