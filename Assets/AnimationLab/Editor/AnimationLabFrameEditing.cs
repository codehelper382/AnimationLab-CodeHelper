using System;
using System.Collections.Generic;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private int moveTargetFrame, moveTrack = -1;
        private float moveSelection = -1;
        private bool moveWholeHand, moveLinkedFingers;
        private int frameDragControl, frameDragTrack, frameDragTarget;
        private float frameDragTime, frameDragStart, frameDragSpan;
        private string frameDragObject;
        private Vector2 frameDragMouse;
        private bool frameDragging;
        private Rect frameDragRow;
        private bool CanRetimeTrack(int track)
        {
            try
            {
                if(track<4&&track%2==0)LabFrameEditing.EnsureWristEditable(draft,track,true);
                return true;
            }
            catch(InvalidOperationException){return false;}
        }

        private void DrawMovePoint()
        {
            float selected = progress;
            bool hasPoint = Times(Track).Any(t => LabFrameEditing.Same(t, selected));
            if (!hasPoint) return;
            if (moveTrack != Track || !LabFrameEditing.Same(moveSelection, selected))
            { moveSelection = selected; moveTrack = Track; moveTargetFrame = CurrentFrame; }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mover este punto en el tiempo", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(HasTakeTrial || playing))
            {
                moveTargetFrame = EditorGUILayout.IntField("Mover a F", moveTargetFrame);
                if (Track < 4) moveWholeHand = EditorGUILayout.ToggleLeft("También posición + dedos", moveWholeHand);
                if (Track == 5) moveLinkedFingers = EditorGUILayout.ToggleLeft("Dedos enlazados en este punto", moveLinkedFingers);
                if (GUILayout.Button($"Mover F {CurrentFrame} → F {moveTargetFrame}"))
                    Safe(() => MoveFramePoint(Track, objectId, selected, moveTargetFrame, moveWholeHand, moveLinkedFingers, false, true));
            }
            EditorGUILayout.LabelField("También puedes arrastrar un rombo. Solo se mueve esta pista; una pieza recompila sus contactos. Esc cancela el arrastre.", EditorStyles.wordWrappedMiniLabel);
        }

        private void MoveFramePoint(int track, string id, float from, int frame, bool wholeHand, bool linkedFingers,
            bool replaceConfirmed, bool allowPrompt)
        {
            if (HasTakeTrial || studioTab == 3) throw new InvalidOperationException("Termina la prueba y vuelve a Animar para mover puntos.");
            if (frame < 0 || frame > LabTimeline.LastFrame(draft.action.duration))
                throw new InvalidOperationException("El fotograma de destino debe quedar dentro de la toma.");
            if (!CanLeaveFrame()) return;
            float to = LabTimeline.Time(frame, draft.action.duration);
            if (LabFrameEditing.Same(from, to)) return;
            bool combined = wholeHand && track < 4 && LabFrameEditing.Times(draft, track % 2 == 0 ? track + 1 : track - 1, null)
                .Any(t => LabFrameEditing.Same(t, from));
            bool allowRecipe = ApproveFrameRecipe(track, combined, allowPrompt);
            if (!allowRecipe) return;
            var candidate = LabAssetIO.Clone(draft);
            try
            {
                try { LabFrameEditing.Move(candidate, track, id, from, to, replaceConfirmed, combined, linkedFingers, true); }
                catch (LabFrameCollisionException)
                {
                    if (!allowPrompt) throw;
                    if (!EditorUtility.DisplayDialog("Destino ocupado",
                        "Ya hay un punto de un canal que vas a mover en ese destino. Reemplazar afecta solo a esos canales; las demás pistas se conservan. Puedes deshacer.",
                        "Reemplazar", "Cancelar")) { notice = "Movimiento cancelado; el destino se conserva."; return; }
                    LabFrameEditing.Move(candidate, track, id, from, to, true, combined, linkedFingers, true);
                }
                if (track >= 4) LabCoordinatedPaths.Compile(candidate, preview);
                CommitEditCandidate(candidate, track, id, combined, linkedFingers, false, "Mover fotograma");
                if (track == 5) objectId = id;
                SelectTrack(track, to);
                notice = $"Punto movido a F {frame}. Otras pistas conservadas; puedes deshacer.";
            }
            finally { LabAssetIO.DestroyDraft(candidate); EvaluateNow(Pending); }
        }

        private bool ApproveFrameRecipe(int track, bool wholeHand, bool allowPrompt)
        {
            if (track >= 4 || (track % 2 == 1 && !wholeHand)) return true;
            int wrist = track / 2 * 2;
            try { LabFrameEditing.EnsureWristEditable(draft, wrist, false); return true; }
            catch (InvalidOperationException)
            {
                // A contact is never bypassed by confirming a clip replacement.
                LabFrameEditing.EnsureWristEditable(draft, wrist, true);
                if (!allowPrompt) throw;
                return EditorUtility.DisplayDialog("Movimiento basado en receta",
                    "Mover estos puntos reconstruirá el canal de muñeca y sustituirá su Motion Clip/Reach/objetivo. Los dedos y otras pistas no se reemplazan. Guarda una copia si quieres conservar esa receta.",
                    "Usar mis puntos", "Cancelar");
            }
        }

        private void CommitEditCandidate(AnimationLabTake candidate, int track, string id, bool wholeHand,
            bool linkedFingers, bool fingerOverride, string undoName)
        {
            LabTakeValidation.RequireValid(candidate);
            var hands = LabFrameEditing.AffectedHands(draft, candidate, track, id).ToList();
            if (track < 4 && wholeHand) hands.Add(track < 2 ? HandSide.Left : HandSide.Right);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.RecordObject(draft, undoName); Undo.RecordObject(draft.action, undoName);
            if (track == 4)
            {
                draft.weapon = LabObjectAuthoring.CloneKeys(candidate.weapon);
                draft.action.weaponPosition = candidate.action.weaponPosition; draft.action.weaponRotation = candidate.action.weaponRotation;
                draft.weaponDynamics = JsonUtility.FromJson<LabWeaponDynamicsRecipe>(JsonUtility.ToJson(candidate.weaponDynamics));
            }
            else if (track == 5)
            {
                var output = LabFrameEditing.Objects(draft, id);
                output.Clear(); output.AddRange(LabObjectAuthoring.CloneKeys(LabFrameEditing.Objects(candidate, id)));
            }
            foreach (var hand in hands.Distinct()) CommitMotionOnly(candidate, hand);
            if (track < 4 && (track % 2 == 1 || wholeHand || fingerOverride))
                CommitFingersOnly(candidate, track < 2 ? HandSide.Left : HandSide.Right);
            if (track == 5 && linkedFingers)
                foreach (HandSide hand in new[] { HandSide.Left, HandSide.Right }) CommitFingersOnly(candidate, hand);
            Undo.CollapseUndoOperations(group); coordinatedPathPending = false; dirty = true;
        }

        private void CommitMotionOnly(AnimationLabTake candidate, HandSide hand)
        {
            var old = hand == HandSide.Left ? draft.left : draft.right;
            var input = hand == HandSide.Left ? candidate.left : candidate.right;
            var result = old.Where(k => k.fingers != null).ToList();
            foreach (var key in result) key.fingersOnly = true;
            foreach (var key in input.Where(k => !k.fingersOnly))
            {
                var dest = LabTimeline.Find(result, key.time);
                if (dest == null) { dest = new LabHandKey { time = key.time }; result.Add(dest); }
                dest.fingersOnly = false; dest.position = key.position; dest.euler = key.euler;
                dest.pathMode = key.pathMode; dest.bend = key.bend; dest.departureSpeed = key.departureSpeed;
                dest.arrivalSpeed = key.arrivalSpeed; dest.outgoing = key.outgoing; dest.incoming = key.incoming;
            }
            result.Sort((a, b) => a.time.CompareTo(b.time));
            if (hand == HandSide.Left)
            { draft.left = result; draft.action.leftHandMotion = candidate.action.leftHandMotion;
                draft.action.leftHandTargetMotion = candidate.action.leftHandTargetMotion; draft.action.leftIK = candidate.action.leftIK;
                draft.leftContactMotion = candidate.leftContactMotion; }
            else
            { draft.right = result; draft.action.rightHandMotion = candidate.action.rightHandMotion; draft.action.rightIK = candidate.action.rightIK;
                draft.rightContactMotion = candidate.rightContactMotion; }
        }

        private void CommitFingersOnly(AnimationLabTake candidate, HandSide hand)
        {
            var keys = hand == HandSide.Left ? draft.left : draft.right;
            var input = hand == HandSide.Left ? candidate.left : candidate.right;
            var previous = keys.Where(k => k.fingers != null).Select(k => (time: k.time, pose: k.fingers)).ToArray();
            keys.RemoveAll(k => k.fingersOnly);
            foreach (var key in keys) key.fingers = null;
            foreach (var key in input.Where(k => k.fingers != null))
            {
                var pose = previous.FirstOrDefault(p => LabFrameEditing.Same(p.time, key.time)
                    && JsonUtility.ToJson(p.pose) == JsonUtility.ToJson(key.fingers)).pose;
                if (pose == null) { pose = key.fingers; key.fingers = null; retired.Add(pose); }
                var dest = LabTimeline.Find(keys, key.time);
                if (dest == null) { dest = new LabHandKey { time = key.time, fingersOnly = true }; keys.Add(dest); }
                dest.fingers = pose; dest.fingerLabel = key.fingerLabel;
                dest.fingerBlend = key.fingerBlend; dest.fingerBlendDuration = key.fingerBlendDuration;
            }
            foreach (var pose in previous.Select(p => p.pose).Distinct())
                if (!keys.Any(k => k.fingers == pose)) retired.Add(pose);
            keys.Sort((a, b) => a.time.CompareTo(b.time));
        }

        private void DrawTimelineMarker(Rect marker, Rect row, int track, float time, float start, float span)
        {
            int control = GUIUtility.GetControlID(("LabFrame:" + track + ":" + time.GetHashCode()).GetHashCode(), FocusType.Passive);
            var e = Event.current;
            bool movable = !HasTakeTrial && studioTab != 3 && !playing && CanRetimeTrack(track);
            if (e.type == EventType.Repaint)
                GUI.Label(marker, new GUIContent("◆", $"F {LabTimeline.Frame(time, draft.action.duration)} · {TrackNames[track]}\nClic: seleccionar. Arrastrar: mover el punto; Esc: cancelar."), EditorStyles.boldLabel);
            if (e.type == EventType.MouseDown && e.button == 0 && marker.Contains(e.mousePosition))
            {
                Safe(() =>
                {
                    SelectTrack(track, time);
                    if (Pending || Track != track || !LabFrameEditing.Same(progress, time)) return;
                    if (movable)
                    {
                        frameDragControl = control; GUIUtility.hotControl = control;
                        frameDragTrack = track; frameDragTime = time; frameDragObject = objectId;
                        frameDragMouse = e.mousePosition; frameDragTarget = LabTimeline.Frame(time, draft.action.duration);
                        frameDragStart = start; frameDragSpan = span; frameDragging = false;
                        frameDragRow = row;
                    }
                });
                e.Use();
            }
        }
        private void ProcessFrameDrag()
        {
            if(frameDragControl==0)return;
            if(HasTakeTrial||studioTab==3||playing||GUIUtility.hotControl!=frameDragControl){CancelFrameDrag();return;}
            var e=Event.current;var row=frameDragRow;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            { CancelFrameDrag(); notice = "Arrastre cancelado; no se ha movido ningún punto."; e.Use(); return; }
            if (e.type == EventType.MouseDrag)
            {
                if (Vector2.Distance(e.mousePosition, frameDragMouse) > 3) frameDragging = true;
                float normalized = Mathf.Clamp01(frameDragStart + frameDragSpan * (e.mousePosition.x - row.x) / row.width);
                frameDragTarget = LabTimeline.Frame(normalized, draft.action.duration); e.Use(); Repaint();
            }
            if (e.type == EventType.Repaint && frameDragging)
            {
                float target = LabTimeline.Time(frameDragTarget, draft.action.duration);
                float x = row.x + (target - frameDragStart) / frameDragSpan * row.width;
                EditorGUI.DrawRect(new Rect(Mathf.Clamp(x, row.x, row.xMax) - 1, row.y, 2, row.height), Color.cyan);
                GUI.Label(new Rect(Mathf.Clamp(x - 30, row.x, row.xMax - 100), row.y - 18, 100, 18), "Mover → F " + frameDragTarget, EditorStyles.whiteMiniLabel);
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                bool moved = frameDragging; int target = frameDragTarget;
                int selectedTrack = frameDragTrack; float from = frameDragTime; string id = frameDragObject;
                CancelFrameDrag(); e.Use();
                if (moved) Safe(() => MoveFramePoint(selectedTrack, id, from, target, moveWholeHand, moveLinkedFingers, false, true));
            }
        }
        private void CancelFrameDrag()
        {
            if (frameDragControl != 0 && GUIUtility.hotControl == frameDragControl) GUIUtility.hotControl = 0;
            frameDragControl = 0; frameDragging = false;
        }
    }
}
