using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private AnimationLabTake weaponMotionSource,weaponMotionCandidate;
        private bool weaponMotionBefore;
        private float weaponMotionIntensity=.55f;
        private string weaponMotionFingerprint;
        private bool HasWeaponMotionTrial=>weaponMotionSource!=null;
        private bool HasTakeTrial=>HasHandTrial||HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial;
        private string WeaponFingerprint()=>DraftFingerprint()+string.Join("|",draft.left.Concat(draft.right).Where(k=>k.fingers!=null).Select(k=>EditorJsonUtility.ToJson(k.fingers)));
        private void DrawWeaponDynamics()
        {
            LabStudioUI.Section("Movimiento natural del arma","Respiración leve, inercia y asentamiento según los puntos existentes. Conserva sus poses principales y recalcula contactos.");
            using(new EditorGUI.DisabledScope(HasTakeTrial))
            {
                if(GUILayout.Button("Probar movimiento natural del arma",GUILayout.Height(30)))Safe(StartWeaponMotionTrial);
                if(draft.weaponDynamics!=null&&draft.weaponDynamics.applied&&GUILayout.Button("Quitar capa natural · restaurar mi recorrido"))Safe(RestoreWeaponMotion);
            }
            EditorGUILayout.HelpBox("La prueba no guarda ni cambia BASE CORRECTA. Compara Antes/Después y acepta solo si mejora el gesto. No añade sacudidas de cámara ni física automática.",MessageType.None);
        }
        private void RestoreWeaponMotion()
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de restaurar el arma.");
            if(!CanLeaveFrame())return;
            var candidate=LabWeaponDynamics.Restore(draft,preview);
            try{ApplyWeaponMotion(candidate);SyncSelection();}
            finally{LabAssetIO.DestroyDraft(candidate);}
        }
        private void StartWeaponMotionTrial()
        {
            if(HasHandTrial||HasTransitionTrial||HasGuidedReloadTrial)throw new InvalidOperationException("Acepta o cancela la prueba actual antes de probar movimiento de arma.");
            if(!CanLeaveFrame())return;
            StopGripTransitionPreview();
            if(!HasWeaponMotionTrial)
            {weaponMotionFingerprint=WeaponFingerprint();weaponMotionSource=LabAssetIO.Clone(draft);weaponMotionIntensity=draft.weaponDynamics!=null&&draft.weaponDynamics.applied?draft.weaponDynamics.intensity:.55f;}
            try{RebuildWeaponMotionTrial();}
            catch{CancelWeaponMotionTrial(false);throw;}
        }
        private void RebuildWeaponMotionTrial()
        {
            var generated=LabWeaponDynamics.Generate(weaponMotionSource,preview,weaponMotionIntensity);
            LabAssetIO.DestroyDraft(weaponMotionCandidate);weaponMotionCandidate=generated;weaponMotionBefore=false;playing=false;EvaluateNow(false);
        }
        private void CancelWeaponMotionTrial(bool restore=true)
        {
            LabAssetIO.DestroyDraft(weaponMotionCandidate);LabAssetIO.DestroyDraft(weaponMotionSource);
            weaponMotionCandidate=weaponMotionSource=null;weaponMotionFingerprint=null;playing=false;
            if(restore&&preview!=null&&draft!=null)SyncSelection();
        }
        private void AcceptWeaponMotionTrial()
        {
            if(!HasWeaponMotionTrial||weaponMotionCandidate==null)throw new InvalidOperationException("Prueba primero el movimiento del arma.");
            if(WeaponFingerprint()!=weaponMotionFingerprint)throw new InvalidOperationException("El borrador original cambió durante la prueba. Cancela y vuelve a probar para no perder esos cambios.");
            ApplyWeaponMotion(weaponMotionCandidate);CancelWeaponMotionTrial(false);SyncSelection();
        }
        private void ApplyWeaponMotion(AnimationLabTake candidate)
        {
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            Undo.RecordObject(draft,"Aplicar movimiento natural");Undo.RecordObject(draft.action,"Aplicar movimiento natural");
            draft.weapon=LabObjectAuthoring.CloneKeys(candidate.weapon);
            draft.weaponDynamics=JsonUtility.FromJson<LabWeaponDynamicsRecipe>(JsonUtility.ToJson(candidate.weaponDynamics));
            draft.action.weaponPosition=LabWeaponDynamics.Copy(candidate.action.weaponPosition);draft.action.weaponRotation=LabWeaponDynamics.Copy(candidate.action.weaponRotation);
            ApplyCandidateHands(candidate,LabCoordinatedPaths.Segments(candidate).Select(s=>s.side).Distinct().ToArray());
            Undo.CollapseUndoOperations(group);dirty=true;
            notice="Movimiento de arma aplicado con Undo. Tus dedos, piezas, clips fuente y poses principales se conservan; guarda como nueva copia.";
        }
        private void WeaponMotionTrialActions()
        {
            EditorGUILayout.HelpBox("PRUEBA TEMPORAL DEL ARMA · no se ha insertado. Aceptar conserva el resultado en el borrador; Cancelar deja el original.",MessageType.Info);
            using(new EditorGUILayout.HorizontalScope())
            {
                float amount=EditorGUILayout.Slider("Intensidad",weaponMotionIntensity,0,1);
                if(!Mathf.Approximately(amount,weaponMotionIntensity)){weaponMotionIntensity=amount;Safe(RebuildWeaponMotionTrial);}
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                int view=GUILayout.Toolbar(weaponMotionBefore?0:1,new[]{"ANTES","DESPUÉS"});
                if(view!=(weaponMotionBefore?0:1)){weaponMotionBefore=view==0;EvaluateNow(false);}
                if(GUILayout.Button("Aceptar movimiento"))Safe(AcceptWeaponMotionTrial);
                if(GUILayout.Button("Cancelar"))CancelWeaponMotionTrial();
            }
        }
    }
}
