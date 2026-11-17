using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private Vector2 dragMouse;
        private Vector3 dragViewport, dragPosition;
        private float timelineZoom = 1f;
        private float timelineStart;
        private Rect timelineRowsRect;
        private static readonly string[] TrackNames = { "Izquierda · posición", "Izquierda · dedos", "Derecha · posición", "Derecha · dedos", "Arma completa", "Pieza / diseño" };
        private int Track => editObject ? 5 : editWeapon ? 4 : (int)side * 2 + (editFingers ? 1 : 0);

        private void DrawQuickHelp()
        {
            help = EditorGUILayout.Foldout(help, "¿Primera vez? Tres pasos para crear movimiento", true);
            if (help) EditorGUILayout.HelpBox("1. Haz clic en un fotograma de la línea de tiempo.\n2. Elige mano/arma y ajusta su posición o dedos.\n3. Pulsa Guardar posición / Guardar dedos. Repite en otro fotograma y pulsa Reproducir. Los rombos son puntos guardados.", MessageType.Info);
        }

        private void DrawFrameTimeline()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("1 · Elige el fotograma", EditorStyles.boldLabel, GUILayout.Width(165));
                    int frame = EditorGUILayout.IntField(new GUIContent("Fotograma", "Referencia de 60 fps. El fotograma 0 es el inicio."), CurrentFrame, GUILayout.Width(215));
                    if (frame != CurrentFrame) Safe(() => Seek(LabTimeline.Time(Mathf.Clamp(frame, 0, LabTimeline.LastFrame(DisplayDuration)), DisplayDuration)));
                    GUILayout.Label($"/ {LabTimeline.LastFrame(DisplayDuration)}  ·  {progress * DisplayDuration:F3} / {DisplayDuration:F3} s");
                    using (new EditorGUI.DisabledScope(HasTakeTrial))
                        if (GUILayout.Button(new GUIContent("Guardar toma como copia", "Guarda los puntos pendientes y crea un asset nuevo, sin sobrescribir el original. Acepta o cancela antes una prueba temporal."), GUILayout.Width(180))) Safe(SaveTake);
                }
                int slider = EditorGUILayout.IntSlider(CurrentFrame, 0, LabTimeline.LastFrame(DisplayDuration));
                if (slider != CurrentFrame) Safe(() => Seek(LabTimeline.Time(slider, DisplayDuration)));
                DrawFrameClipboard();
                DrawGesturePhaseBar();
                DrawTrackRows();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|◀ Inicio", GUILayout.Width(75))) Safe(() => Seek(0));
                    if (GUILayout.Button("−1 fotograma", GUILayout.Width(105))) Safe(() => Seek(LabTimeline.Time(Mathf.Max(0, CurrentFrame - 1), DisplayDuration)));
                    if (GUILayout.Button(playing ? "Pausar" : "▶ Reproducir", GUILayout.Width(105))) Safe(TogglePlayback);
                    if (GUILayout.Button("+1 fotograma", GUILayout.Width(105))) Safe(() => Seek(LabTimeline.Time(CurrentFrame + 1, DisplayDuration)));
                    if (GUILayout.Button("◀ Punto", GUILayout.Width(70))) Safe(() => SeekAdjacentKey(-1));
                    if (GUILayout.Button("Punto ▶", GUILayout.Width(70))) Safe(() => SeekAdjacentKey(1));
                    if (GUILayout.Button("Fin ▶|", GUILayout.Width(65))) Safe(() => Seek(1));
                    loop = GUILayout.Toggle(loop, "Repetir", GUILayout.Width(70));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Velocidad", GUILayout.Width(60));
                    speed = GUILayout.HorizontalSlider(speed, 0.1f, 2f, GUILayout.Width(100));
                    GUILayout.Label($"{speed:F2}×", GUILayout.Width(42));
                    GUILayout.Label("Zoom", GUILayout.Width(38));
                    float zoom = GUILayout.HorizontalSlider(timelineZoom, 1f, 8f, GUILayout.Width(100));
                    if (!Mathf.Approximately(zoom, timelineZoom)) SetTimelineZoom(zoom);
                    GUILayout.Label($"{timelineZoom:F1}×", GUILayout.Width(36));
                    if (GUILayout.Button("Ajustar a ventana", GUILayout.Width(120))) SetTimelineZoom(1f);
                    GUILayout.Label("Ctrl + rueda sobre pistas · el zoom sigue al cursor temporal", EditorStyles.miniLabel);
                }
            }
        }

        private IEnumerable<float> Times(int track)
        {
            var take = DisplayTake;
            if (track == 4) return take.weapon.Select(k => k.time);
            if (track == 5) return objectId != null
                ? TakeObjectKeys(take, objectId)?.Select(k => k.time) ?? Enumerable.Empty<float>() : Enumerable.Empty<float>();
            var keys = track < 2 ? take.left : take.right;
            return keys.Where(k => track % 2 == 1 ? k.fingers != null : !k.fingersOnly).Select(k => k.time);
        }

        private void SetTimelineZoom(float zoom)
        {
            if(frameDragControl!=0)return;
            timelineZoom = Mathf.Clamp(zoom, 1f, 8f);
            float span = 1f / timelineZoom;
            timelineStart = Mathf.Clamp(progress - span * .5f, 0f, 1f - span);
        }

        private Vector2 TimelineRange()
        {
            float span = 1f / Mathf.Clamp(timelineZoom, 1f, 8f);
            if (progress < timelineStart || progress > timelineStart + span)
                timelineStart = Mathf.Clamp(progress - span * .5f, 0f, 1f - span);
            timelineStart = Mathf.Clamp(timelineStart, 0f, 1f - span);
            return new Vector2(timelineStart, timelineStart + span);
        }

        private void DrawTrackRows()
        {
            Rect area = GUILayoutUtility.GetRect(200, 152, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) timelineRowsRect = area;
            const float labelWidth = 155, rowHeight = 21;
            Rect ruler = new Rect(area.x + labelWidth, area.y, Mathf.Max(10, area.width - labelWidth - 10), 22);
            var range = TimelineRange();
            float start = range.x, span = range.y - range.x;
            var input = Event.current;
            if (input.type == EventType.ScrollWheel && (input.control || input.command) && area.Contains(input.mousePosition))
            {
                SetTimelineZoom(timelineZoom * Mathf.Pow(1.15f, -input.delta.y));
                input.Use();
            }
            if (Event.current.type == EventType.Repaint)
                for (int i = 0; i <= 8; i++)
                {
                    float x = ruler.x + ruler.width * i / 8f;
                    float normalized = start + span * i / 8f;
                    GUI.Label(new Rect(x - 18, ruler.y, 44, 18), "F " + LabTimeline.Frame(normalized, DisplayDuration), EditorStyles.centeredGreyMiniLabel);
                }
            for (int track = 0; track < TrackNames.Length; track++)
            {
                int selectedTrack = track;
                Rect row = new Rect(ruler.x, ruler.y + 22 + track * rowHeight, ruler.width, rowHeight - 1);
                if(frameDragControl!=0&&track==frameDragTrack)frameDragRow=row;
                EditorGUI.DrawRect(row, track == Track ? new Color(0.22f, 0.35f, 0.44f, 0.6f) : new Color(0.1f, 0.1f, 0.1f, 0.18f));
                if (GUI.Button(new Rect(area.x, row.y, labelWidth - 4, row.height), TrackNames[track], track == Track ? EditorStyles.miniButton : EditorStyles.label))
                    Safe(() => SelectTrack(selectedTrack, progress));
                foreach (float time in VisibleTimes(track))
                {
                    float u = (time - start) / span;
                    if (u < 0f || u > 1f) continue;
                    float keyTime = time;
                    Rect marker = new Rect(row.x + u * row.width - 8, row.y, 16, row.height);
                    Color previous = GUI.contentColor;
                    GUI.contentColor = track == 4 ? new Color(1f, 0.65f, 0.25f) : track % 2 == 1 ? new Color(0.45f, 0.9f, 0.7f) : Color.yellow;
                    DrawTimelineMarker(marker, row, selectedTrack, keyTime, start, span);
                    GUI.contentColor = previous;
                }
                float progressU = (progress - start) / span;
                if (progressU >= 0f && progressU <= 1f)
                    EditorGUI.DrawRect(new Rect(row.x + progressU * row.width, row.y, 2, row.height), Color.white);
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && row.Contains(evt.mousePosition))
                {
                    float normalized = Mathf.Clamp01(start + span * Mathf.InverseLerp(row.x, row.xMax, evt.mousePosition.x));
                    float time = LabTimeline.Time(LabTimeline.Frame(normalized, DisplayDuration), DisplayDuration);
                    Safe(() => SelectTrack(selectedTrack, time)); evt.Use();
                }
            }
            ProcessFrameDrag();
        }

        private bool CanLeaveFrame()
        {
            if (!Pending) return true;
            int choice = EditorUtility.DisplayDialogComplex("Pose sin confirmar", $"Has ajustado {TargetName} en F {CurrentFrame}. ¿Quieres guardar ese punto antes de continuar?", "Guardar punto", "Seguir editando", "Descartar ajuste");
            if (choice == 1) return false;
            if (choice == 2) { SyncSelection(); return true; }
            return CommitEdits(false);
        }

        private void Seek(float time)
        {
            if (!CanLeaveFrame()) return;
            progress = Mathf.Clamp01(time); playing = false; SyncSelection();
        }

        private void SelectTrack(int track, float time)
        {
            if (!CanLeaveFrame()) return;
            editWeapon = track == 4;
            editObject = track == 5;
            if (!editWeapon && !editObject) { side = track < 2 ? HandSide.Left : HandSide.Right; editFingers = track % 2 == 1; }
            wristReplacementApproved = weaponReplacementApproved = false;
            progress = Mathf.Clamp01(time); playing = false; SyncSelection();
        }

        private void TogglePlayback()
        {
            if (playing) { playing = false; SyncSelection(); return; }
            if (!CanLeaveFrame()) return;
            SyncSelection();
            GesturePlaybackRange(out float start, out float end);
            if (progress >= end || progress < start) progress = start;
            playing = true; guidedPlayed = true; lastTime = EditorApplication.timeSinceStartup;
        }

        private void SeekAdjacentKey(int direction)
        {
            var times = Times(Track).OrderBy(t => t).Distinct().ToArray();
            float target = -1f;
            if (direction < 0)
            {
                for (int i = times.Length - 1; i >= 0; i--)
                    if (times[i] < progress - LabTimeline.TimeEpsilon) { target = times[i]; break; }
            }
            else
            {
                for (int i = 0; i < times.Length; i++)
                    if (times[i] > progress + LabTimeline.TimeEpsilon) { target = times[i]; break; }
            }
            if (target < 0f) { notice = "No hay otro punto guardado en esta pista."; return; }
            Seek(target);
        }

        private void SyncSelection()
        {
            reloadPlacement = -1;
            previewingKey = weaponPending = objectPending = false;
            followContacts.Clear();
            ClearFingerOverride();
            if (draft.left.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Left); else preview.RestoreHandBinding(HandSide.Left);
            if (draft.right.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Right); else preview.RestoreHandBinding(HandSide.Right);
            float incoming = draft.action.blendIn, outgoing = draft.action.blendOut;
            try
            {
                draft.action.blendIn = draft.action.blendOut = 0;
                preview.Evaluate(draft, progress, pitch, yaw, aim, rawFingers: true);
                if (editObject)
                {
                    var target = ObjectTarget;
                    if (target != null)
                    {
                        keyPosition = target.localPosition;
                        Vector3 reference = LabObjectAuthoring.Sample(ObjectKeys() ?? new List<LabWeaponKey>(), progress, keyPosition, target.localEulerAngles).euler;
                        keyRotation = LabTimeline.UnwrapEuler(target.localEulerAngles, reference);
                    }
                    else keyPosition = keyRotation = Vector3.zero;
                }
                else if (editWeapon)
                {
                    keyPosition = draft.action.weaponPosition.Evaluate(progress);
                    keyRotation = draft.action.weaponRotation.Evaluate(progress);
                }
                else
                {
                    var chain = side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                    var guard = preview.Guard(side);
                    bool plainCurves = HandKeys.Any(k => !k.fingersOnly) && HandMotion.enabled && HandMotion.motionClip == null && !LabTimeline.HasMotion(HandMotion.reach);
                    if (plainCurves)
                    {
                        keyPosition = HandMotion.position.Evaluate(progress);
                        keyRotation = HandMotion.rotation.Evaluate(progress); // Preserve signed/multi-turn Euler continuity.
                    }
                    else if (guard != null && chain.target != null)
                    {
                        keyPosition = guard.InverseTransformPoint(chain.target.position);
                        Vector3 reference = HandMotion.enabled ? HandMotion.rotation.Evaluate(progress) : Vector3.zero;
                        keyRotation = LabTimeline.UnwrapEuler((Quaternion.Inverse(guard.rotation) * chain.target.rotation).eulerAngles, reference);
                    }
                    else
                    {
                        keyPosition = HandMotion.enabled ? HandMotion.position.Evaluate(progress) : Vector3.zero;
                        keyRotation = HandMotion.enabled ? HandMotion.rotation.Evaluate(progress) : Vector3.zero;
                    }
                }
            }
            finally { draft.action.blendIn = incoming; draft.action.blendOut = outgoing; }
            if (!editWeapon && !editObject && editFingers) {CaptureFingerBase();SyncFingerTransition();}
            EvaluateNow(false); Repaint();
        }

        private bool PositionReplacementRequired() => editWeapon
            ? draft.weapon.Count == 0 && (LabTimeline.HasValues(draft.action.weaponPosition) || LabTimeline.HasValues(draft.action.weaponRotation))
            : !HandKeys.Any(k => !k.fingersOnly) && (LabTimeline.ReplacesMotion(HandMotion) ||
                (side == HandSide.Left && draft.kind == EquipmentActionKind.Reload && (draft.action.leftHandTargetMotion != null || preview.Slot.leftReloadTarget != null)));

        private bool ApprovePositionReplacement(bool allowPrompt)
        {
            bool replacing = PositionReplacementRequired();
            bool approved = editWeapon ? weaponReplacementApproved : wristReplacementApproved;
            if (!replacing || approved) return true;
            if (!allowPrompt) throw new InvalidOperationException("No se reemplazó una trayectoria sin confirmar. Conserva una copia antes de reintentarlo.");
            if (!EditorUtility.DisplayDialog("Esta acción ya tiene movimiento", editWeapon
                ? "Crear puntos de arma reemplazará sus curvas de desplazamiento/giro. El clip de las piezas no cambia. Guarda una copia de la toma antes de continuar."
                : "Crear puntos de muñeca reemplazará su Motion Clip, Reach, curvas o ruta de objetivo de recarga. Si solo quieres cambiar dedos, elige Solo dedos: no altera la trayectoria.", "Crear mi trayectoria", "Cancelar")) return false;
            if (editWeapon) weaponReplacementApproved = true; else wristReplacementApproved = true;
            return true;
        }

        private bool BeginPositionEdit()
        {
            if (HasTakeTrial) throw new InvalidOperationException("Acepta o cancela la prueba antes de editar claves manuales.");
            if(!editObject&&!editWeapon&&LabCoordinatedPaths.At(draft,side,progress)!=null)
                throw new InvalidOperationException("Esta muñeca comparte el recorrido de una pieza. Edita la curva común desde la pieza o separa el tramo antes de editar solo la mano.");
            if (editObject) return BeginObjectEdit();
            if (!ApprovePositionReplacement(true)) return false;
            playing = false;
            if (editWeapon) weaponPending = true;
            else { preview.EnableHand(side); previewingKey = true; }
            return true;
        }

        private bool CommitEdits(bool recordUnchanged, bool allowPrompt = true)
        {
            if(gripTransitionActive){StopGripTransitionPreview();EvaluateNow(Pending);}
            if (HasTakeTrial) throw new InvalidOperationException("Acepta o cancela la prueba antes de guardar puntos.");
            if (editObject) return CommitObject(recordUnchanged, allowPrompt);
            if (!Pending && !recordUnchanged) return true;
            bool wrist = !editWeapon && (previewingKey || (recordUnchanged && !editFingers));
            bool finger = !editWeapon && (fingersEdited || (recordUnchanged && editFingers));
            if ((wrist || editWeapon) && !ApprovePositionReplacement(allowPrompt)) return false;
            Undo.RecordObject(draft, "Guardar punto de Animation Lab");
            Undo.RecordObject(draft.action, "Guardar punto de Animation Lab");
            if (editWeapon)
            {
                var key = draft.weapon.FirstOrDefault(k => Mathf.Abs(k.time - progress) < LabTimeline.TimeEpsilon);
                if (key == null) { key = new LabWeaponKey { time = progress }; draft.weapon.Add(key); }
                key.position = keyPosition; key.euler = keyRotation;
                draft.weapon.Sort((a, b) => a.time.CompareTo(b.time));
                LabAssetIO.BuildWeaponMotion(draft);
            }
            if (wrist)
            {
                preview.EnableHand(side);
                LabTimeline.RecordWrist(HandKeys, HandMotion, progress, keyPosition, keyRotation);
            }
            if (finger)
            {
                if (fingers == null) CaptureFingerBase();
                var pose = Instantiate(fingers); pose.hideFlags = HideFlags.HideAndDontSave;
                try { LabAssetIO.NormalizeFingerPose(pose, preview); }
                catch { DestroyImmediate(pose); throw; }
                var previous = LabTimeline.FindFingers(HandKeys, progress)?.fingers;
                if (previous != null) retired.Add(previous);
                if(NeedsInitialGrip(fingerBlendMode))AddInitialGrip(CaptureInitialGrip());
                LabTimeline.RecordFingers(HandKeys, progress, pose);
                var recorded=LabTimeline.FindFingers(HandKeys,progress);
                recorded.fingerBlend=fingerBlendMode;recorded.fingerBlendDuration=fingerTransitionFrames/(60f*draft.action.duration);
                recorded.fingerLabel=fingerPoseLabel;
                retired.Add(pose); // Undo may restore a removed key; retain its transient pose until closing.
            }
            dirty = true; notice = $"Punto de {TargetName} guardado en F {CurrentFrame}. Guarda la toma para conservarlo como asset.";
            SyncSelection(); return true;
        }

        private void SaveTake()
        {
            if(quarantinedRecovery)throw new InvalidOperationException("Corrige y revalida el respaldo antes de guardar una toma final.");
            if (HasTakeTrial) throw new InvalidOperationException("Acepta o cancela la prueba antes de guardar la toma. La recuperación conserva el borrador sin insertar el candidato.");
            if (Pending && !CommitEdits(false)) return;
            RefreshCoordinatedPaths();
            saved = LabAssetIO.Save(draft, preview, takeName); dirty = false;
            try { LabTakeCatalog.Register(saved, TakeLineage); }
            catch(Exception e) { Debug.LogWarning("[AnimationLab] Toma guardada; no se pudo actualizar la agrupación local: "+e.Message); }
            hasUnsavedChanges=false;
            takeCache = null;
            notice = "Toma guardada: " + AssetDatabase.GetAssetPath(saved);
            EditorGUIUtility.PingObject(saved);
            EvaluateNow(false);
        }

        private void EvaluateNow(bool editing)
        {
            if(HasTransitionTrial)
            {var take=DisplayTake;SetDisplayOwnership(take);preview.Evaluate(take,progress,pitch,yaw,aim);return;}
            if(HasWeaponMotionTrial)
            {var take=weaponMotionBefore||weaponMotionCandidate==null?weaponMotionSource:weaponMotionCandidate;SetDisplayOwnership(take);preview.Evaluate(take,progress,pitch,yaw,aim);return;}
            if(HasGuidedReloadTrial)
            {var take=guidedReloadBefore||guidedReloadCandidate==null?guidedReloadTrialSource:guidedReloadCandidate;SetDisplayOwnership(take);preview.Evaluate(take,progress,pitch,yaw,aim);return;}
            if(gripTransitionActive&&gripTransitionTake!=null)
            {SetDisplayOwnership(gripTransitionTake);preview.Evaluate(gripTransitionTake,gripTransitionTime,pitch,yaw,aim);return;}
            if (HasHandTrial && !Pending)
            {
                SetDisplayOwnership(DisplayTake); preview.Evaluate(DisplayTake, progress, pitch, yaw, aim); return;
            }
            float incoming = draft.action.blendIn, outgoing = draft.action.blendOut;
            var position = draft.action.weaponPosition; var rotation = draft.action.weaponRotation;
            try
            {
                if (editing) draft.action.blendIn = draft.action.blendOut = 0;
                if (weaponPending)
                {
                    draft.action.weaponPosition = Constant(keyPosition);
                    draft.action.weaponRotation = Constant(keyRotation);
                }
                preview.Evaluate(draft, progress, pitch, yaw, aim,
                    fingersEdited && side == HandSide.Left ? fingers : null, fingersEdited && side == HandSide.Right ? fingers : null,
                    beforeFinalIK: () =>
                    {
                        if (objectPending) ApplyObjectEdit();
                        if (reloadPlacement == 1 && draft.reload.hasRest)
                        {
                            var target = LabReloadGenerator.Resolve(preview, draft.reload);
                            if (target != null)
                            {
                                if (draft.reload.objectId.StartsWith("part:")) preview.Objects.Touch(target);
                                target.SetLocalPositionAndRotation(draft.reload.restPosition, Quaternion.Euler(draft.reload.restEuler));
                            }
                        }
                    });
                if (previewingKey) ApplyTemporaryKey();
            }
            finally
            {
                draft.action.blendIn = incoming; draft.action.blendOut = outgoing;
                draft.action.weaponPosition = position; draft.action.weaponRotation = rotation;
            }
        }

        private static EquipmentVectorCurves Constant(Vector3 value) => new EquipmentVectorCurves
        { x = AnimationCurve.Constant(0, 1, value.x), y = AnimationCurve.Constant(0, 1, value.y), z = AnimationCurve.Constant(0, 1, value.z) };

        private void DrawKeyList()
        {
            var allTimes = Times(Track).OrderBy(t => t).ToArray();
            if (allTimes.Length > 40) showTechnicalKeys = EditorGUILayout.Foldout(showTechnicalKeys, $"Muestras técnicas ({allTimes.Length}) · ocultas para editar por fases", true);
            float[] times = showTechnicalKeys ? allTimes.OrderBy(t=>Mathf.Abs(t-progress)).Take(80).OrderBy(t=>t).ToArray() : allTimes.Length > 40 ? Array.Empty<float>() : allTimes;
            EditorGUILayout.Space(); EditorGUILayout.LabelField($"Puntos de {(editObject ? "pieza / diseño" : editWeapon ? "arma" : editFingers ? "dedos" : "posición")} ({times.Length})", EditorStyles.boldLabel);
            if (allTimes.Length == 0) EditorGUILayout.HelpBox("Aún no hay puntos en esta pista. Guarda uno al inicio y otro más adelante para crear movimiento.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                var before = times.Where(t => t < progress - LabTimeline.TimeEpsilon).ToArray();
                var after = times.Where(t => t > progress + LabTimeline.TimeEpsilon).ToArray();
                using (new EditorGUI.DisabledScope(before.Length == 0)) if (GUILayout.Button("◀ Punto anterior")) Safe(() => Seek(before.Last()));
                using (new EditorGUI.DisabledScope(after.Length == 0)) if (GUILayout.Button("Punto siguiente ▶")) Safe(() => Seek(after.First()));
            }
            foreach (float time in times)
            {
                float keyTime = time;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"F {LabTimeline.Frame(time, DisplayDuration)} · {time * DisplayDuration:F3} s" + (Mathf.Abs(time - progress) < LabTimeline.TimeEpsilon ? "  ← aquí" : ""))) Safe(() => Seek(keyTime));
                    if (GUILayout.Button("Quitar", GUILayout.Width(55))) Safe(() => RemovePoint(keyTime));
                }
            }
        }

        private void RemovePoint(float time)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de quitar puntos.");
            if (!CanLeaveFrame()) return;
            if(!editObject&&!editWeapon&&!editFingers&&LabCoordinatedPaths.At(draft,side,time)!=null)
                throw new InvalidOperationException("Separa el recorrido combinado desde la pieza antes de quitar solo una clave de muñeca.");
            Undo.RecordObject(draft, "Quitar punto"); Undo.RecordObject(draft.action, "Quitar punto");
            if (editObject)
            {
                if (objectId == null || ObjectKeys() == null) return;
                var candidate = LabAssetIO.Clone(draft);
                try
                {
                    TakeObjectKeys(candidate, objectId).RemoveAll(k => Mathf.Abs(k.time - time) < LabTimeline.TimeEpsilon);
                    LabCoordinatedPaths.Compile(candidate, preview);
                    var hands = LabCoordinatedPaths.Segments(candidate).Select(s => s.side).Distinct().ToArray();
                    ApplyCoordinatedCandidate(candidate, objectId, hands);
                    coordinatedPathPending = false;
                }
                finally { LabAssetIO.DestroyDraft(candidate); }
            }
            else if (editWeapon)
            {
                draft.weapon.RemoveAll(k => Mathf.Abs(k.time - time) < LabTimeline.TimeEpsilon);
                LabAssetIO.BuildWeaponMotion(draft);
            }
            else
            {
                var key = editFingers ? LabTimeline.FindFingers(HandKeys, time) : LabTimeline.FindWrist(HandKeys, time);
                if (key == null) return;
                if (editFingers)
                {
                    if (key.fingers != null) retired.Add(key.fingers);
                    key.fingers = null; if (key.fingersOnly) HandKeys.Remove(key);
                }
                else LabTimeline.RemoveWrist(HandKeys, key, HandMotion);
            }
            dirty = true; SyncSelection();
        }

        private static Rect ImageRect(Rect bounds)
        {
            float width = Mathf.Clamp((int)bounds.width, 128, 1600), height = Mathf.Clamp((int)bounds.height, 128, 1000);
            float scale = Mathf.Min(bounds.width / width, bounds.height / height);
            var size = new Vector2(width * scale, height * scale);
            return new Rect(bounds.center - size * 0.5f, size);
        }

        private Vector3 HandleWorld()
        {
            if (editObject) return ObjectTarget != null ? ObjectTarget.position : preview.Equipment.weaponPose.PoseRoot.position;
            if (editWeapon) return preview.Slot.visualRoot != null ? preview.Slot.visualRoot.position : preview.Equipment.weaponPose.PoseRoot.position;
            var chain = side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
            return chain.target != null ? chain.target.position : chain.hand.position;
        }

        private void DrawPathReferences(Rect rect)
        {
            if (studioTab == 0 || studioTab == 3) return;
            if ((!editObject && !editWeapon && editFingers) || preview.Camera == null || Event.current.type != EventType.Repaint) return;
            var reference = LocalReference;
            if (reference == null) return;
            var objectKeys = editObject ? ObjectKeys() : editWeapon ? draft.weapon : null;
            var handKeys = HandKeys.Where(k => !k.fingersOnly).OrderBy(k => k.time).ToArray();
            if (objectKeys != null ? objectKeys.Count == 0 : editObject || handKeys.Length == 0) return;
            var curve = editObject ? LabPathCurves.Objects(objectKeys) : editWeapon ? draft.action.weaponPosition : HandMotion.position;
            Vector3 origin = editWeapon ? preview.Equipment.weaponPose.PoseRoot.localPosition - preview.lastWeaponOffset : Vector3.zero;
            Vector2? previous = null;
            Handles.BeginGUI(); Handles.color = editObject ? Color.cyan : editWeapon ? new Color(1, 0.6f, 0.2f) : Color.yellow;
            for (int n = 0; n <= 96; n++)
            {
                var view = preview.Camera.WorldToViewportPoint(reference.TransformPoint(origin + curve.Evaluate(n / 96f)));
                var point = new Vector2(rect.x + view.x * rect.width, rect.y + (1 - view.y) * rect.height);
                if (view.z <= 0 || !rect.Contains(point)) { previous = null; continue; }
                if (previous.HasValue) Handles.DrawLine(previous.Value, point);
                previous = point;
            }
            foreach (float time in Times(Track))
            {
                var view = preview.Camera.WorldToViewportPoint(reference.TransformPoint(origin + curve.Evaluate(time)));
                var point = new Vector2(rect.x + view.x * rect.width, rect.y + (1 - view.y) * rect.height);
                if (view.z <= 0 || !rect.Contains(point)) continue;
                Handles.DrawWireDisc(point, Vector3.forward, 4);
                GUI.Label(new Rect(point.x + 5, point.y + 4, 70, 18), "F " + LabTimeline.Frame(time, DisplayDuration), EditorStyles.whiteMiniLabel);
            }
            Handles.EndGUI();
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), "Recorrido local de referencia · padres fijos en este fotograma, sin fundidos", EditorStyles.whiteMiniLabel);
        }
    }
}
