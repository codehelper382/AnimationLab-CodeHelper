using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private AnimationLabTake handTrialSource, handCandidate;
        private bool trialBefore, trialStale, loopHandRange = true, loopPhase;
        private string trialFingerprint, trialRecipe;
        private int selectedPhase, selectedGesturePoint = 2;
        private bool HasHandTrial => handTrialSource != null;
        private LabHandInsert GestureSettings => (handTrialSource != null ? handTrialSource : draft).handInsert;
        private AnimationLabTake GestureOwner => handTrialSource != null ? handTrialSource : draft;
        private AnimationLabTake DisplayTake => HasTransitionTrial
            ? transitionBefore || transitionCandidate == null ? transitionSource : transitionCandidate
            : HasWeaponMotionTrial
                ? weaponMotionBefore || weaponMotionCandidate == null ? weaponMotionSource : weaponMotionCandidate
                : HasGuidedReloadTrial
                    ? guidedReloadBefore || guidedReloadCandidate == null ? guidedReloadTrialSource : guidedReloadCandidate
                    : HasHandTrial ? trialBefore || handCandidate == null ? handTrialSource : handCandidate : draft;

        private float DisplayDuration => DisplayTake?.action != null ? DisplayTake.action.duration : 1f;

        private string DraftFingerprint() => EditorJsonUtility.ToJson(draft) + EditorJsonUtility.ToJson(draft.action);
        private void GestureChanged()
        {
            if (HasHandTrial) trialStale = true; else dirty = true;
            Repaint();
        }
        private void SetDisplayOwnership(AnimationLabTake take)
        {
            if (take.left.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Left); else preview.RestoreHandBinding(HandSide.Left);
            if (take.right.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Right); else preview.RestoreHandBinding(HandSide.Right);
        }
        private void StartHandTrial()
        {
            if(HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial)throw new InvalidOperationException("Acepta o cancela la prueba actual antes de probar manos.");
            if (!CanLeaveFrame()) return;
            side=GestureSettings.side;editObject=editWeapon=editFingers=false;
            if (!HasHandTrial)
            {
                trialFingerprint = DraftFingerprint(); handTrialSource = LabAssetIO.Clone(draft);
            }
            RebuildHandTrial();
        }
        private void RebuildHandTrial()
        {
            if (!HasHandTrial) { StartHandTrial(); return; }
            trialStale = true;
            var generated = LabHandTemplateInsertion.Insert(handTrialSource, preview);
            LabAssetIO.DestroyDraft(handCandidate); handCandidate = generated;
            trialRecipe = JsonUtility.ToJson(GestureSettings); trialStale = false; trialBefore = false;
            progress = Mathf.Clamp(progress, GestureSettings.start, GestureSettings.end); playing = false;
            EvaluateNow(false); Repaint();
        }
        private void CancelHandTrial(bool restore = true)
        {
            LabAssetIO.DestroyDraft(handCandidate); LabAssetIO.DestroyDraft(handTrialSource);
            handCandidate = handTrialSource = null; trialBefore = trialStale = false; playing = false;
            trialFingerprint = trialRecipe = null;
            if (restore && preview != null && draft != null) SyncSelection();
        }
        private void AcceptHandTrial()
        {
            if (!HasHandTrial) throw new InvalidOperationException("Prueba una plantilla antes de aceptarla.");
            if (DraftFingerprint() != trialFingerprint) throw new InvalidOperationException("La toma original cambió durante la prueba. Cancela y vuelve a probar para no reemplazar cambios nuevos.");
            if (trialStale || JsonUtility.ToJson(GestureSettings) != trialRecipe) RebuildHandTrial();
            var generated = handCandidate; var source = handTrialSource;
            handCandidate = handTrialSource = null; trialBefore = trialStale = false;
            try { ApplyHandTemplate(generated); }
            finally { LabAssetIO.DestroyDraft(generated); LabAssetIO.DestroyDraft(source); }
        }
        private void SaveRecoveryDraft()
        {
            if(HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial)
            {
                var weaponRecovery=LabAssetIO.Save(draft,preview,"Recovery_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"),false);
                takeCache=null;notice="Recuperación guardada: "+AssetDatabase.GetAssetPath(weaponRecovery);return;
            }
            // A trial recovers its editable recipe and original channels, never the unaccepted candidate.
            if (!HasHandTrial) CommitEdits(false, false);
            var recovery=HasHandTrial?CreateTrialRecoveryDraft():draft;
            try
            {
                var recoveryAsset=LabAssetIO.Save(recovery, preview, "Recovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), false);
                takeCache=null;notice="Recuperación guardada: "+AssetDatabase.GetAssetPath(recoveryAsset);
            }
            finally{if(recovery!=draft)LabAssetIO.DestroyDraft(recovery);}
        }
        private AnimationLabTake CreateTrialRecoveryDraft()
        {
            // Undo/external edits may have changed the real draft since the trial snapshot.
            // Recover those current channels plus working settings, not the stale baseline or candidate.
            var recovery=LabAssetIO.Clone(draft);
            if(HasHandTrial)recovery.handInsert=LabGestureAuthoring.Copy(handTrialSource.handInsert);
            return recovery;
        }
        private void TrialActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Color background=GUI.backgroundColor;GUI.backgroundColor=LabStudioUI.Accent;
                bool run;
                using(new EditorGUI.DisabledScope(LabTimeline.LastFrame(draft.action.duration)<6))
                    run=GUILayout.Button(HasHandTrial ? "↻ Actualizar prueba" : "▶ Probar sin insertar",LabStudioUI.Button,GUILayout.Width(155),GUILayout.Height(30));
                GUI.backgroundColor=background;if(run)Safe(StartHandTrial);
                if(HasHandTrial)
                {
                    trialBefore=GUILayout.Toolbar(trialBefore?0:1,new[]{"ANTES","DESPUÉS"},GUILayout.Width(155),GUILayout.Height(30))==0;
                    GUI.backgroundColor=LabStudioUI.Accent;bool accept=GUILayout.Button("✓ Aceptar manos",LabStudioUI.Button,GUILayout.Height(30));GUI.backgroundColor=background;if(accept)Safe(AcceptHandTrial);
                    if(GUILayout.Button("Cancelar",GUILayout.Width(78),GUILayout.Height(30)))Safe(()=>CancelHandTrial());
                }
                else GUILayout.Label("Sin modificar las claves hasta Aceptar",LabStudioUI.Subtitle);
            }
            if(HasHandTrial)EditorGUILayout.LabelField(trialStale?"● Cambios pendientes · pulsa Actualizar para ver el nuevo resultado":"● Prueba temporal · Aceptar inserta solo la mano elegida / Cancelar conserva el original",LabStudioUI.Subtitle);
        }

        private void GesturePlaybackRange(out float start, out float end)
        {
            start = 0; end = 1;
            if (HasTransitionTrial && transitionTrialPlan != null)
            {
                start = transitionTrialPlan.from;
                end = LabTimeline.Time(transitionTrialPlan.arrivalFrame + transitionTrialPlan.settleFrames, draft.action.duration);
                return;
            }
            if (HasGuidedReloadTrial) return;
            if (draft == null || !loopHandRange || (studioTab != 0 && !HasHandTrial)) return;
            var r = GestureSettings; start = r.start; end = r.end;
            if (loopPhase)
            {
                var bounds = LabGestureAuthoring.Bounds(r);
                start = Mathf.Lerp(r.start, r.end, bounds[selectedPhase]); end = Mathf.Lerp(r.start, r.end, bounds[selectedPhase + 1]);
            }
        }
        private void DrawGestureWarnings(Rect rect)
        {
            if (studioTab != 0 || preview == null) return;
            var r = GestureSettings; var hand = r.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
            if (hand.upperArm == null || hand.forearm == null || hand.hand == null || hand.target == null) return;
            float length = Vector3.Distance(hand.upperArm.position, hand.forearm.position) + Vector3.Distance(hand.forearm.position, hand.hand.position);
            bool stretched = length > 0 && Vector3.Distance(hand.upperArm.position, hand.target.TransformPoint(hand.positionOffset)) > length * .96f;
            Vector3 view = preview.Camera.WorldToViewportPoint(hand.target.position);
            bool outside = view.z <= 0 || view.x < .03f || view.x > .97f || view.y < .03f || view.y > .97f;
            string message = stretched ? "⚠ Brazo cerca de su alcance máximo: reduce recorrido o recoloca contacto." : outside ? "⚠ Mano fuera del encuadre: revisa primera persona / cámara." : r.travel.magnitude * (r.useStyle ? r.reachScale : 1) > .45f ? "⚠ Recorrido amplio: comprueba codo y penetraciones." : null;
            if (message != null) GUI.Label(new Rect(rect.x + 12, rect.yMax - 54, Mathf.Max(100,rect.width - 24), 44), message, new GUIStyle(EditorStyles.helpBox) { wordWrap = true });
        }
    }
}
