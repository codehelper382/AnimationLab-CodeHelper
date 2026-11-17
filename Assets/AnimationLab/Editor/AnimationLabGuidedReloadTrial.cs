using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private AnimationLabTake guidedReloadTrialSource, guidedReloadCandidate;
        private bool guidedReloadBefore, guidedReloadStale;
        private string guidedReloadFingerprint;
        private bool HasGuidedReloadTrial => guidedReloadTrialSource != null;

        private void StartGuidedReloadTrial()
        {
            if (HasHandTrial || HasWeaponMotionTrial || HasTransitionTrial)
                throw new InvalidOperationException("Acepta o cancela la prueba actual antes de generar la recarga guiada.");
            if (quarantinedRecovery)
                throw new InvalidOperationException("Repara y revalida el borrador antes de generar movimiento.");
            if (!CanLeaveFrame()) return;
            StopGripTransitionPreview();
            if (HasGuidedReloadTrial)
            {
                RebuildGuidedReloadTrial();
                return;
            }

            editObject = editWeapon = editFingers = false;
            guidedReloadFingerprint = WeaponFingerprint();
            guidedReloadTrialSource = LabAssetIO.Clone(draft);
            try
            {
                RebuildGuidedReloadTrial();
            }
            catch
            {
                CancelGuidedReloadTrial(true);
                throw;
            }
        }

        private void RebuildGuidedReloadTrial()
        {
            if (!HasGuidedReloadTrial)
                throw new InvalidOperationException("Inicia una prueba de recarga guiada antes de actualizarla.");
            if (WeaponFingerprint() != guidedReloadFingerprint)
                throw new InvalidOperationException("El borrador cambió durante la prueba. Cancela y vuelve a probar desde el estado actual.");

            guidedReloadStale = true;
            AnimationLabTake generated = null;
            try
            {
                generated = LabGuidedReloadGenerator.BuildCandidate(
                    guidedReloadTrialSource, preview, progress, pitch, yaw, aim);
                LabAssetIO.DestroyDraft(guidedReloadCandidate);
                guidedReloadCandidate = generated;
                generated = null;
                guidedReloadStale = false;
                guidedReloadBefore = false;
                playing = false;
                EvaluateNow(false);
            }
            finally
            {
                LabAssetIO.DestroyDraft(generated);
            }
        }

        private void CancelGuidedReloadTrial(bool restore = true)
        {
            LabAssetIO.DestroyDraft(guidedReloadCandidate);
            LabAssetIO.DestroyDraft(guidedReloadTrialSource);
            guidedReloadCandidate = guidedReloadTrialSource = null;
            guidedReloadBefore = guidedReloadStale = false;
            guidedReloadFingerprint = null;
            playing = false;
            if (restore && preview != null && draft != null) SyncSelection();
        }

        private void AcceptGuidedReloadTrial()
        {
            if (!HasGuidedReloadTrial || guidedReloadCandidate == null)
                throw new InvalidOperationException("Genera un candidato antes de aceptarlo.");
            if (guidedReloadStale || WeaponFingerprint() != guidedReloadFingerprint)
                throw new InvalidOperationException("El borrador cambió durante la prueba. Cancela y vuelve a generar para no perder cambios.");

            LabTakeValidation.RequireValid(guidedReloadCandidate);
            var candidate = guidedReloadCandidate;
            var changedHands = LabGuidedReloadGenerator.GeneratedHands(candidate.guidedReload);
            bool replaceWeapon = LabGuidedReloadGenerator.GeneratesWeapon(candidate.guidedReload);
            var fingerTimes = LabGuidedReloadGenerator.GeneratedFingerTimes(candidate.guidedReload);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Aceptar candidato de recarga guiada");
            using (var transaction = new LabAuthoringTransaction(draft))
            {
                try
                {
                    Undo.RecordObject(draft, "Aceptar candidato de recarga guiada");
                    Undo.RecordObject(draft.action, "Aceptar candidato de recarga guiada");
                    if (changedHands.Length > 0)
                    {
                        ApplyCandidateHands(candidate, changedHands, fingerTimes);
                    }
                    if (replaceWeapon)
                    {
                        draft.weapon = LabObjectAuthoring.CloneKeys(candidate.weapon);
                        draft.action.weaponPosition = LabWeaponDynamics.Copy(candidate.action.weaponPosition);
                        draft.action.weaponRotation = LabWeaponDynamics.Copy(candidate.action.weaponRotation);
                    }
                    LabTakeValidation.RequireValid(draft);
                    transaction.Commit();
                    EditorUtility.SetDirty(draft);
                    EditorUtility.SetDirty(draft.action);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            Undo.CollapseUndoOperations(group);

            var scopes = new System.Collections.Generic.List<string>();
            var wristHands = LabGuidedReloadGenerator.GeneratedWristHands(candidate.guidedReload);
            if (wristHands.Length > 0)
                scopes.Add(string.Join(", ", wristHands.Select(h =>
                    h == HandGripToolkit.HandSide.Left ? "muñeca izquierda" : "muñeca derecha")));
            if (replaceWeapon) scopes.Add("arma");
            var fingerHands = LabGuidedReloadGenerator.GeneratedFingerHands(candidate.guidedReload);
            if (fingerHands.Length > 0)
                scopes.Add(string.Join(", ", fingerHands.Select(h =>
                    h == HandGripToolkit.HandSide.Left ? "dedos izquierdos" : "dedos derechos")));
            string scope = string.Join(" y ", scopes);
            dirty = true;
            notice = "Candidato guiado aceptado con Undo. Se sustituyeron " + scope
                + "; canales no seleccionados (piezas, mecanismos) se conservaron.";
            CancelGuidedReloadTrial(false);
            SyncSelection();
        }

        private void DrawGuidedReloadTrialActions()
        {
            EditorGUILayout.HelpBox(
                "PRUEBA TEMPORAL (Hito 3 ampliado) · Aceptar reemplazará por completo las trayectorias de muñeca, arma y dedos. "
                + "Piezas, mecanismos, la otra mano, clips fuente y receta guiada se conservan. "
                + (guidedReloadStale ? "El borrador cambió: cancela y vuelve a probar." : "Nada se inserta hasta Aceptar."),
                guidedReloadStale ? MessageType.Warning : MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool before = GUILayout.Toolbar(guidedReloadBefore ? 0 : 1,
                    new[] { "ANTES", "DESPUÉS" }, GUILayout.Width(160)) == 0;
                bool viewChanged = before != guidedReloadBefore;
                guidedReloadBefore = before;
                using (new EditorGUI.DisabledScope(guidedReloadStale))
                    if (GUILayout.Button("↻ Actualizar", GUILayout.Width(100))) Safe(RebuildGuidedReloadTrial);
                using (new EditorGUI.DisabledScope(guidedReloadCandidate == null || guidedReloadStale))
                    if (GUILayout.Button("✓ Aceptar candidato", GUILayout.Width(145))) Safe(AcceptGuidedReloadTrial);
                if (GUILayout.Button("Cancelar", GUILayout.Width(85))) CancelGuidedReloadTrial();
                if (viewChanged) EvaluateNow(false);
            }
        }
    }
}
