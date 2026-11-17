using System;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool quarantinedRecovery,showRecoveryRawData;
        private LabEmergencyRecovery.PendingState quarantinedPending;
        private static string PendingRecoveryProblem(LabEmergencyRecovery.PendingState p)
        {
            if(p==null)return null;
            if(p.track<0||p.track>5||!LabTakeValidation.Finite(p.progress)||p.progress<0||p.progress>1)
                return "La selección temporal pendiente no es válida.";
            if((p.wrist||p.weapon||p.item)&&(!LabTakeValidation.Finite(p.position)||!LabTakeValidation.Finite(p.euler)))
                return "La pose pendiente contiene números no válidos.";
            if(p.fingers)
            {
                if(string.IsNullOrEmpty(p.fingerJson))return "La pose de dedos pendiente está incompleta.";
                var pose=CreateInstance<HandGripToolkit.HandPoseData>();
                try
                {
                    JsonUtility.FromJsonOverwrite(p.fingerJson,pose);
                    for(int f=0;f<5;f++)for(int j=0;j<3;j++)
                    {
                        var bone=pose.GetEntry((HandGripToolkit.FingerId)f,j);
                        if(bone!=null&&!LabTakeValidation.Finite(bone.localRotation))return "La pose pendiente de dedos contiene un giro no válido.";
                    }
                }
                catch(Exception){return "No se puede leer la pose pendiente de dedos.";}
                finally{DestroyImmediate(pose);}
            }
            return null;
        }
        private void LoadInvalidRecovery(AnimationLabTake restored)
        {
            if(AssetDatabase.Contains(restored))throw new InvalidOperationException("La reparación requiere una copia independiente.");
            if(restored.slot<0||restored.slot>=preview.Equipment.slots.Length||preview.Equipment.slots[restored.slot].definition==null
                ||preview.Equipment.slots[restored.slot].definition.equipmentId!=restored.equipmentId)
                throw new InvalidOperationException("El respaldo pertenece a otro equipo.");
            CancelFrameDrag();CancelPathDrag();CancelViewportDrag();CancelTransitionBlockDrag();
            CancelTransitionTrial(false);CancelWeaponMotionTrial(false);CancelHandTrial(false);
            ClearFingerOverride();LabAssetIO.DestroyDraft(draft);
            draft=restored;saved=null;progress=0;playing=false;dirty=true;
            previewingKey=weaponPending=objectPending=false;followContacts.Clear();
            editObject=editWeapon=editFingers=false;side=HandGripToolkit.HandSide.Left;objectId=null;
            quarantinedRecovery=true;quarantinedPending=null;showRecoveryRawData=false;
            if(actionEditor!=null)DestroyImmediate(actionEditor);
            // Do not apply anchors, evaluate a graph, compile curves or bake this draft.
            preview.RestoreAnchors();preview.Select(draft.slot);
        }
        private void DrawRecoveryRepair()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            LabStudioUI.Section("Respaldo recuperado · revisión segura","Los datos siguen intactos. No se evalúa esta toma hasta corregir sus errores.");
            var problems=LabTakeValidation.Inspect(draft);
            foreach(var problem in problems)EditorGUILayout.HelpBox(problem.message,MessageType.Error);
            string pendingProblem=PendingRecoveryProblem(quarantinedPending);
            if(pendingProblem!=null)EditorGUILayout.HelpBox(pendingProblem,MessageType.Error);
            EditorGUILayout.HelpBox("El editor de reparación usa el formato interno: tiempos normalizados [0,1], posiciones locales en metros y giros en grados. Solo modifica este borrador. No corrige, elimina ni acepta puntos automáticamente.",MessageType.Info);
            showRecoveryRawData=EditorGUILayout.Foldout(showRecoveryRawData,"Editar datos recuperados (avanzado)",true);
            if(showRecoveryRawData)
            {
                using(var data=new SerializedObject(draft))
                {
                    data.Update();
                    var property=data.GetIterator();bool enter=true;
                    while(property.NextVisible(enter))
                    {
                        enter=false;
                        if(property.name!="m_Script"&&property.name!="action")EditorGUILayout.PropertyField(property,true);
                    }
                    if(data.ApplyModifiedProperties())dirty=true;
                }
                if(draft.action!=null&&!AssetDatabase.Contains(draft.action))
                {
                    UnityEditor.Editor.CreateCachedEditor(draft.action,null,ref actionEditor);
                    EditorGUI.BeginChangeCheck();actionEditor.OnInspectorGUI();
                    if(EditorGUI.EndChangeCheck())dirty=true;
                }
                if(quarantinedPending!=null)
                {
                    var p=quarantinedPending;
                    EditorGUILayout.LabelField("Ajustes sin confirmar",EditorStyles.boldLabel);
                    p.progress=EditorGUILayout.FloatField("Tiempo pendiente [0,1]",p.progress);
                    p.track=EditorGUILayout.IntField("Pista pendiente [0,5]",p.track);
                    p.position=EditorGUILayout.Vector3Field("Posición pendiente (m)",p.position);
                    p.euler=EditorGUILayout.Vector3Field("Giro pendiente",p.euler);
                    if(GUILayout.Button("Descartar SOLO los ajustes sin confirmar…")
                        &&EditorUtility.DisplayDialog("Descartar ajustes pendientes","Se conservan todos los puntos del borrador. Se descarta solo la pose temporal; el archivo de respaldo anterior no se borra.","Descartar ajustes","Cancelar"))
                        quarantinedPending=null;
                }
            }
            if(GUILayout.Button("Conservar otro respaldo antes de reparar"))
                Safe(()=>{latestEmergencyPath=LabEmergencyRecovery.Write(CaptureEmergency(),"Copia anterior a reparación manual");notice="Respaldo conservado: "+latestEmergencyPath;});
            using(new EditorGUI.DisabledScope(problems.Count>0||pendingProblem!=null))
                if(GUILayout.Button("Revalidar y volver al estudio",GUILayout.Height(32)))Safe(FinishRecoveryRepair);
            if(!string.IsNullOrEmpty(notice))EditorGUILayout.LabelField(notice,EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
        private void FinishRecoveryRepair()
        {
            LabTakeValidation.RequireValid(draft);
            string pendingProblem=PendingRecoveryProblem(quarantinedPending);
            if(pendingProblem!=null)throw new InvalidOperationException(pendingProblem);
            if(draft.slot<0||draft.slot>=preview.Equipment.slots.Length||preview.Equipment.slots[draft.slot].definition==null
                ||preview.Equipment.slots[draft.slot].definition.equipmentId!=draft.equipmentId)
                throw new InvalidOperationException("La toma debe conservar un equipo compatible.");
            try
            {
                preview.Select(draft.slot);LabAssetIO.ApplyAnchors(draft,preview);
                SyncSelection();RestoreCheckpointPending(quarantinedPending);
                EvaluateNow(Pending);quarantinedRecovery=false;quarantinedPending=null;studioTab=1;
            }
            catch{quarantinedRecovery=true;throw;}
        }
    }
}
