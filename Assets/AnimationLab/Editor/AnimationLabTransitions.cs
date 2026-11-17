using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool transitionAssistantOpen;
        private LabTransitionPlan transitionPlan;
        private int transitionFingerChoice, transitionBoundary;
        private LabGripPreset transitionGrip;
        private AnimationLabTake transitionSource, transitionCandidate;
        private LabTransitionPlan transitionTrialPlan;
        private string transitionFingerprint;
        private bool transitionBefore;
        private int transitionDragMax, transitionDragSettle, transitionBlockControl;
        private string transitionBlockBefore;
        private Rect transitionBlockWindowRect;
        private bool HasTransitionTrial => transitionSource != null;
        [Serializable] private sealed class TransitionPreferences
        {
            public LabTransitionPlan plan;
            public int fingerChoice;
            public string gripGuid;
        }
        private string transitionPreferencesJson;
        private string TransitionPreferenceKey => "AnimationLab.Transition."+Application.dataPath+"."+TakeLineage;
        private void SaveTransitionPreferences()
        {
            if(transitionPlan==null||HasTakeTrial)return;
            var preferences=new TransitionPreferences{plan=transitionPlan,fingerChoice=transitionFingerChoice,
                gripGuid=transitionGrip!=null?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(transitionGrip)):null};
            string json=JsonUtility.ToJson(preferences);
            if(json==transitionPreferencesJson)return;
            EditorPrefs.SetString(TransitionPreferenceKey,json);transitionPreferencesJson=json;
        }
        private void RestoreTransitionPreferences()
        {
            string json=EditorPrefs.GetString(TransitionPreferenceKey,"");
            if(string.IsNullOrEmpty(json))return;
            try
            {
                var settings=JsonUtility.FromJson<TransitionPreferences>(json);
                var old=settings?.plan;var current=transitionPlan;
                if(old==null||old.track!=current.track||old.objectId!=current.objectId
                    ||!LabFrameEditing.Same(old.from,current.from)||!LabFrameEditing.Same(old.to,current.to))return;
                transitionPlan=old;transitionFingerChoice=Mathf.Clamp(settings.fingerChoice,0,4+LabGripTools.Names.Length);
                transitionGrip=AssetDatabase.LoadAssetAtPath<LabGripPreset>(AssetDatabase.GUIDToAssetPath(settings.gripGuid??""));
                transitionPreferencesJson=json;
            }
            catch(Exception){ /* An obsolete local UI preference must never prevent authoring. */ }
        }

        private void DrawTransitionAssistant()
        {
            transitionAssistantOpen = EditorGUILayout.Foldout(transitionAssistantOpen, "Crear transición fácil · poses, ritmo y dedos", true);
            if (!transitionAssistantOpen) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Actualizar tramo · usar los dos puntos actuales")) Safe(SelectTransitionSegment);
                if (transitionPlan == null)
                {
                    EditorGUILayout.HelpBox("Guarda dos poses en una pista. Después selecciona su tramo y pulsa el botón de arriba. No se simplifican clips existentes sin confirmación.", MessageType.Info);
                    return;
                }
                var p = transitionPlan;
                bool fingersOnly = p.track < 4 && p.track % 2 == 1;
                EditorGUILayout.LabelField(TrackNames[p.track] + (p.track == 5 ? " · " + ObjectLabel(p.objectId) : ""), EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Desde F {LabTimeline.Frame(p.from, draft.action.duration)} · pose destino guardada en F {LabTimeline.Frame(p.to, draft.action.duration)}", EditorStyles.wordWrappedMiniLabel);
                if (!fingersOnly)
                {
                    int intent = EditorGUILayout.Popup("Qué quieres hacer", (int)p.intent,
                        new[] { "Acercarse suavemente", "Pasar por aquí", "Llegar / agarrar", "Soltar y retirarse", "Volver a la pose inicial" });
                    if (intent != (int)p.intent) SetTransitionIntent((LabTransitionIntent)intent);
                }
                DrawTransitionBlocks();
                p.departureFrame = EditorGUILayout.IntField(fingersOnly ? "Iniciar adaptación (F)" : "Iniciar movimiento (F)", p.departureFrame);
                p.arrivalFrame = EditorGUILayout.IntField("Llegar en F", p.arrivalFrame);
                p.settleFrames = EditorGUILayout.IntField("Asentar / mantener (F)", p.settleFrames);
                if (p.track >= 4 || p.track % 2 == 0)
                {
                    int shape=EditorGUILayout.Popup("Movimiento",p.shape==LabTransitionShape.Arc?1:0,
                        new[] { p.continueAtArrival ? "Continuo entre vecinos" : "Recto con frenada", "Arco suave" });
                    p.shape=shape==1?LabTransitionShape.Arc:LabTransitionShape.Direct;
                    p.continueAtArrival = EditorGUILayout.Popup("Al llegar", p.continueAtArrival ? 1 : 0, new[] { "Detenerse", "Continuar sin parada" }) == 1;
                    if (p.continueAtArrival)
                    {
                        p.shape = LabTransitionShape.Direct;
                        EditorGUILayout.LabelField("Continuar necesita un punto posterior y suaviza también el tramo siguiente; no cambia sus poses.", EditorStyles.wordWrappedMiniLabel);
                    }
                    if (p.shape == LabTransitionShape.Arc) p.arcCentimeters = EditorGUILayout.Vector3Field("Desvío del arco local (cm)", p.arcCentimeters);
                    p.useFirstPose = EditorGUILayout.ToggleLeft("Destino = primer punto de pista", p.useFirstPose);
                }
                if (p.track < 4)
                {
                    string[] fingers = new[] { fingersOnly ? "Conservar forma · editar solo el ritmo" : "No modificar los dedos", "Agarre guardado en la llegada", "Mano abierta", "Base inicial de la animación" }.Concat(LabGripTools.Names).Concat(new[] { "Mi tipo / variante" }).ToArray();
                    transitionFingerChoice = EditorGUILayout.Popup("Dedos", transitionFingerChoice, fingers);
                    if (transitionFingerChoice == fingers.Length - 1)
                        transitionGrip = (LabGripPreset)EditorGUILayout.ObjectField("Mi agarre", transitionGrip, typeof(LabGripPreset), false);
                    if (!fingersOnly && transitionFingerChoice > 0)
                    {
                        p.relativeFingers=EditorGUILayout.ToggleLeft("Los dedos terminan junto con la muñeca",p.relativeFingers);
                        if(p.relativeFingers)
                        {
                            p.fingerDurationFrames=EditorGUILayout.IntField("Últimos fotogramas",p.fingerDurationFrames);
                            EditorGUILayout.LabelField($"Adaptar desde F{LabTransitionAssistant.FingerStart(p,draft.action.duration)} hasta F{p.arrivalFrame}",EditorStyles.wordWrappedMiniLabel);
                        }
                        else p.fingerStartFrame=EditorGUILayout.IntField("Inicio dedos independiente (F)",p.fingerStartFrame);
                    }
                }
                string problem=TransitionProblem();
                if(problem!=null)EditorGUILayout.HelpBox(problem,MessageType.Warning);
                using(new EditorGUI.DisabledScope(problem!=null))
                    if (GUILayout.Button("▶ Probar transición · sin modificar la toma", GUILayout.Height(30))) Safe(StartTransitionTrial);
                EditorGUILayout.HelpBox("Mantener → viajar/adaptar → asentar. Contacto frena la llegada; no pega una mano a una pieza: usa un conjunto. Cambiar dedos crea poses manuales (base F0 si falta), no conserva toda la animación dinámica de dedos del clip. Conservar no la sustituye. Los presets no garantizan ausencia de penetraciones.", MessageType.None);
            }
        }

        private void SelectTransitionSegment()
        {
            if (HasTakeTrial) throw new InvalidOperationException("Acepta o cancela la prueba antes de elegir otro tramo.");
            if (!CanLeaveFrame()) return;
            LabFrameEditing.EnsureWristEditable(draft,Track,true);
            var times = LabFrameEditing.Times(draft, Track, objectId).OrderBy(t => t).ToArray();
            if (times.Length < 2) throw new InvalidOperationException("Guarda dos puntos en esta pista. Si partes de una recarga existente, usa Recargas para conservar su trayectoria.");
            int end = Array.FindIndex(times, t => t > progress + LabTimeline.TimeEpsilon);
            if (end < 0) end = times.Length - 1;
            if (end == 0) end = 1;
            transitionPlan = new LabTransitionPlan
            {
                track = Track, objectId = objectId, from = times[end - 1], to = times[end],
                departureFrame = Mathf.CeilToInt(times[end - 1] * draft.action.duration * 60 - .0001f),
                arrivalFrame = LabTimeline.Frame(times[end], draft.action.duration), relativeFingers=true
            };
            transitionPlan.fingerStartFrame = Mathf.Max(transitionPlan.departureFrame, transitionPlan.arrivalFrame - 10);
            transitionFingerChoice = Track < 4 && Track % 2 == 1 ? 1 : 0;
            RestoreTransitionPreferences();
        }

        private void SetTransitionIntent(LabTransitionIntent intent)
        {
            var p = transitionPlan; if (p == null) return;
            p.intent = intent; p.continueAtArrival = intent == LabTransitionIntent.Pass;
            p.useFirstPose = intent == LabTransitionIntent.Return;
            p.shape = intent == LabTransitionIntent.Grip ? LabTransitionShape.Contact
                : intent == LabTransitionIntent.Pass ? LabTransitionShape.Direct : LabTransitionShape.Arc;
            p.settleFrames = intent == LabTransitionIntent.Grip ? 2 : 0;
            if (p.track < 4 && intent == LabTransitionIntent.Return) transitionFingerChoice = 3;
            else if (p.track < 4 && intent == LabTransitionIntent.Release) transitionFingerChoice = 2;
            else transitionFingerChoice = p.track < 4 && p.track % 2 == 1 ? 1 : 0;
        }

        private void DrawTransitionBlocks()
        {
            var p = transitionPlan;
            Rect rect = GUILayoutUtility.GetRect(100, 42, GUILayout.ExpandWidth(true));
            if(Event.current.type==EventType.Repaint)
                transitionBlockWindowRect=new Rect(GUIUtility.GUIToScreenPoint(rect.position)-position.position,rect.size);
            int from = Mathf.CeilToInt(p.from*draft.action.duration*60-.0001f);
            int maximum;
            try{maximum=LabTransitionAssistant.MaximumFrame(draft,p);}catch{maximum=LabTimeline.LastFrame(draft.action.duration);}
            maximum=Mathf.Max(from+1,maximum);
            long rawEnd=(long)p.arrivalFrame+Math.Max(0,p.settleFrames);
            int end=(int)Math.Max(from,Math.Min(maximum,rawEnd));
            int[] boundaries = { from, p.departureFrame, p.arrivalFrame, end };
            string[] labels = { "Mantener", p.track < 4 && p.track % 2 == 1 ? "Adaptar" : "Viajar", "Asentar" };
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.InverseLerp(from, maximum, boundaries[i]), b = Mathf.InverseLerp(from, maximum, boundaries[i + 1]);
                var cell = new Rect(rect.x + a * rect.width, rect.y, Mathf.Max(1, (b - a) * rect.width - 1), rect.height);
                EditorGUI.DrawRect(cell, LabStudioUI.Phases[i + 1]);
                if (cell.width > 45) GUI.Label(cell, labels[i] + $"\nF {boundaries[i]}–{boundaries[i + 1]}", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip });
            }
            for(int i=1;i<=3;i++)
            {
                float x=rect.x+rect.width*Mathf.InverseLerp(from,maximum,boundaries[i]);
                var handle=new Rect(x-3,i==3?rect.center.y+4:rect.y+3,6,13);
                EditorGUI.DrawRect(handle,i==3?Color.cyan:Color.white);
                EditorGUIUtility.AddCursorRect(handle,MouseCursor.ResizeHorizontal);
            }
            if (HasTakeTrial) return;
            var e = Event.current; int control = GUIUtility.GetControlID("LabTransitionBlocks".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                int nearest = 1; float distance = float.MaxValue;
                for (int i = 1; i <= 3; i++)
                {
                    float x = rect.x + rect.width * Mathf.InverseLerp(from, maximum, boundaries[i]);
                    if (Mathf.Abs(e.mousePosition.x - x) <= distance) { distance = Mathf.Abs(e.mousePosition.x - x); nearest = i; }
                }
                if (distance < 10)
                {
                    if(!p.continueAtArrival&&boundaries[2]==boundaries[3]&&nearest>=2)
                        nearest=e.mousePosition.y<rect.center.y?2:3;
                    transitionBoundary=p.continueAtArrival&&nearest==3?2:nearest;
                    transitionDragMax=maximum;transitionDragSettle=Mathf.Clamp(p.settleFrames,0,Mathf.Max(0,maximum-from-1));transitionBlockBefore=JsonUtility.ToJson(p);
                    transitionBlockControl=control;GUIUtility.hotControl=control;e.Use();
                }
            }
            if (GUIUtility.hotControl != control) return;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){CancelTransitionBlockDrag();e.Use();return;}
            if (e.type == EventType.MouseDrag)
            {
                int frame = Mathf.Clamp(Mathf.RoundToInt(Mathf.LerpUnclamped(from,transitionDragMax,(e.mousePosition.x-rect.x)/rect.width)),from,transitionDragMax);
                if (transitionBoundary == 1) p.departureFrame = Mathf.Clamp(frame, from, Mathf.Max(from, p.arrivalFrame - 1));
                else if (transitionBoundary == 2)
                {
                    int settle=p.continueAtArrival?0:transitionDragSettle;
                    p.arrivalFrame = Mathf.Clamp(frame,p.departureFrame+1,Mathf.Max(p.departureFrame+1,transitionDragMax-settle));
                    p.settleFrames=settle;
                }
                else p.settleFrames = Mathf.Max(0, frame - p.arrivalFrame);
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp) { transitionBlockBefore=null;transitionBlockControl=0;GUIUtility.hotControl = 0; e.Use(); }
        }
        private void CancelTransitionBlockDrag()
        {
            if(transitionBlockControl!=0&&GUIUtility.hotControl==transitionBlockControl)GUIUtility.hotControl=0;
            if(transitionBlockBefore!=null&&transitionPlan!=null)JsonUtility.FromJsonOverwrite(transitionBlockBefore,transitionPlan);
            transitionBlockBefore=null;transitionBlockControl=0;
        }
        private string TransitionProblem()
        {
            var p=transitionPlan;
            if(p==null)return "Selecciona un tramo.";
            if(p.track!=Track||(p.track==5&&p.objectId!=objectId))return "El asistente conserva otra pista. Pulsa Actualizar tramo para editar la selección actual.";
            string timing=LabTransitionAssistant.TimingError(draft,p);if(timing!=null)return timing;
            if(!LabTakeValidation.Finite(p.arcCentimeters))return "El arco debe contener números finitos.";
            try{LabFrameEditing.EnsureWristEditable(draft,p.track,true);}catch(Exception e){return e.Message;}
            if(p.track<4&&transitionFingerChoice>0)
            {
                var keys=LabFrameEditing.Hand(draft,p.track);
                if(transitionFingerChoice==1&&!keys.Any(k=>k.fingers!=null&&LabFrameEditing.Same(k.time,p.to)))return "No hay un agarre en la llegada; elige una forma de dedos.";
                if(transitionFingerChoice==4+LabGripTools.Names.Length&&(transitionGrip==null||transitionGrip.fingers==null||transitionGrip.side!=(p.track<2?HandSide.Left:HandSide.Right)))
                    return "Selecciona un tipo de agarre compatible con esta mano.";
                if(p.track%2==0)
                {
                    int firstFingerFrame=LabTransitionAssistant.FingerStart(p,draft.action.duration);
                    if(firstFingerFrame<Mathf.CeilToInt(p.from*draft.action.duration*60-.0001f)||firstFingerFrame>p.arrivalFrame)return "El inicio de dedos debe quedar entre la pose inicial y la llegada.";
                }
                float arrival=LabTimeline.Time(p.arrivalFrame,draft.action.duration);
                float start=p.track%2==1?LabTimeline.Time(p.departureFrame,draft.action.duration)
                    :LabTimeline.Time(LabTransitionAssistant.FingerStart(p,draft.action.duration),draft.action.duration);
                if(keys.Any(k=>k.fingers!=null&&k.time>start+LabTimeline.TimeEpsilon&&k.time<arrival-LabTimeline.TimeEpsilon&&!LabFrameEditing.Same(k.time,p.to)))
                    return "Hay otro agarre dentro de la adaptación. Retrasa el inicio o mueve ese agarre; no se sustituye automáticamente.";
                float settled=LabTimeline.Time(p.arrivalFrame+p.settleFrames,draft.action.duration);
                if(keys.Any(k=>k.fingers!=null&&k.time>arrival+LabTimeline.TimeEpsilon&&k.time<=settled+LabTimeline.TimeEpsilon&&!LabFrameEditing.Same(k.time,p.to)))
                    return "Hay otro agarre durante Asentar. Reduce el asentamiento o mueve ese agarre.";
            }
            return null;
        }

        private void StartTransitionTrial()
        {
            if (HasTakeTrial) throw new InvalidOperationException("Acepta o cancela la prueba actual antes de probar otra transición.");
            if (transitionPlan == null) throw new InvalidOperationException("Selecciona primero un tramo.");
            string problem=TransitionProblem();if(problem!=null)throw new InvalidOperationException(problem);
            if (!CanLeaveFrame() || !ApproveFrameRecipe(transitionPlan.track, false, true)) return;
            SaveTransitionPreferences();
            StopGripTransitionPreview(); CancelFrameDrag();
            HandPoseData pose = null, baseline = null;
            var previousSide = side;
            try
            {
                var p = transitionPlan;
                if (p.track < 4)
                {
                    side = p.track < 2 ? HandSide.Left : HandSide.Right;
                    int choice = transitionFingerChoice;
                    var keys = LabFrameEditing.Hand(draft, p.track);
                    if (choice == 1)
                    {
                        var stored = keys.FirstOrDefault(k => LabFrameEditing.Same(k.time, p.to) && k.fingers != null)?.fingers;
                        if (stored == null) throw new InvalidOperationException("No hay agarre guardado en la llegada. Elige una forma de dedos o Conservar.");
                        pose = Instantiate(stored);
                    }
                    else if (choice == 2) pose = LabGripTools.Open(preview.Animator, side);
                    else if (choice == 3) pose = CaptureInitialGrip();
                    else if (choice >= 4 && choice < 4 + LabGripTools.Names.Length) pose = LabGripTools.Preset(preview.Animator, side, choice - 4);
                    else if (choice >= 4 + LabGripTools.Names.Length)
                    {
                        if (transitionGrip == null || transitionGrip.side != side || transitionGrip.fingers == null)
                            throw new InvalidOperationException("Selecciona un agarre compatible con esta mano.");
                        pose = Instantiate(transitionGrip.fingers);
                    }
                    if (pose != null)
                    {
                        pose.hideFlags = HideFlags.HideAndDontSave;
                        LabAssetIO.NormalizeFingerPose(pose, preview);
                    }
                    if (pose != null || p.track % 2 == 1) baseline = CaptureInitialGrip();
                }
                transitionFingerprint = WeaponFingerprint();
                transitionSource = LabAssetIO.Clone(draft);
                Vector3 units = TransitionUnits(transitionPlan);
                transitionCandidate = LabTransitionAssistant.Generate(transitionSource, transitionPlan, units, pose, baseline,
                    transitionFingerChoice == 1 ? null : "Agarre · asistente", true);
                if (transitionPlan.track >= 4) LabCoordinatedPaths.Compile(transitionCandidate, preview);
                transitionTrialPlan = JsonUtility.FromJson<LabTransitionPlan>(JsonUtility.ToJson(transitionPlan));
                transitionBefore = false; playing = false; progress = transitionPlan.from; EvaluateNow(false);
                guidedRhythm=true;
                notice = "Prueba de transición lista. Antes/Después compara; Aceptar aplica con Undo.";
            }
            catch { CancelTransitionTrial(false); throw; }
            finally
            {
                side = previousSide;
                if (pose != null) DestroyImmediate(pose);
                if (baseline != null) DestroyImmediate(baseline);
            }
        }

        private Vector3 TransitionUnits(LabTransitionPlan p)
        {
            Transform reference = p.track < 4 ? preview.Guard(p.track < 2 ? HandSide.Left : HandSide.Right)
                : p.track == 4 ? preview.Equipment.weaponPose.PoseRoot.parent : LabCoordinatedPaths.Target(preview, p.objectId)?.parent;
            var scale = reference != null ? reference.lossyScale : Vector3.one;
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        private void CancelTransitionTrial(bool restore = true)
        {
            LabAssetIO.DestroyDraft(transitionCandidate); LabAssetIO.DestroyDraft(transitionSource);
            transitionCandidate = transitionSource = null; transitionTrialPlan = null; transitionFingerprint = null; playing = false;
            if (restore && draft != null && preview != null) SyncSelection();
        }
        private void AcceptTransitionTrial()
        {
            if (!HasTransitionTrial || transitionCandidate == null) throw new InvalidOperationException("Prueba una transición antes de aceptarla.");
            if (WeaponFingerprint() != transitionFingerprint) throw new InvalidOperationException("El borrador cambió durante la prueba. Cancela y vuelve a probar para no perder cambios.");
            var p = transitionTrialPlan;
            CommitEditCandidate(transitionCandidate, p.track, p.objectId, false, false,
                p.track < 4 && transitionFingerChoice > 0, "Aceptar transición");
            float destination = LabTimeline.Time(p.arrivalFrame, draft.action.duration);
            if (p.track == 5) objectId = p.objectId;
            int track = p.track;
            CancelTransitionTrial(false); SelectTrack(track, destination);
            transitionPlan = null; notice = "Transición aceptada con Undo. Guarda una copia para conservarla.";
        }
        private void DrawTransitionTrialActions()
        {
            EditorGUILayout.HelpBox("PRUEBA DE TRANSICIÓN · no modifica el borrador hasta Aceptar. Cancelar vuelve a los ajustes.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                int view = GUILayout.Toolbar(transitionBefore ? 0 : 1, new[] { "ANTES", "DESPUÉS" });
                if (view != (transitionBefore ? 0 : 1)) { transitionBefore = view == 0; EvaluateNow(false); }
                if (GUILayout.Button(playing ? "Pausar" : "▶ Probar tramo")) Safe(TogglePlayback);
                if (GUILayout.Button("Aceptar transición")) Safe(AcceptTransitionTrial);
                if (GUILayout.Button("Cancelar / ajustes")) Safe(() => CancelTransitionTrial());
            }
        }
    }
}
