using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow : EditorWindow
    {
        private LabPreview preview;
        private AnimationLabTake draft, saved;
        private HandPoseData fingers, fingerBase;
        private HandSide side = HandSide.Right;
        private readonly List<UnityEngine.Object> retired = new List<UnityEngine.Object>();
        private Vector2 scroll;
        private float progress, pitch, yaw, speed = 1;
        private bool playing, loop = true, aim, firstPerson, dirty, advanced;
        private string takeName = "Nueva animación", error;
        private Vector3 keyPosition, keyRotation;
        private Vector3[] curls = new Vector3[5];
        private int curlAxis = 2, anchorIndex;
        private double lastTime;
        private UnityEditor.Editor actionEditor;
        private SpineDriveMode mode;
        private bool joints, fingerFineEdit;
        private HandPoseData selectedPose;
        private float handleDepth;
        private bool editWeapon, editFingers, fingersEdited, weaponPending, help = true, calibrations;
        private bool wristReplacementApproved, weaponReplacementApproved;
        private string notice;
        private bool Pending => previewingKey || fingersEdited || weaponPending || objectPending;
        private List<LabHandKey> HandKeys => side == HandSide.Left ? draft.left : draft.right;
        private EquipmentHandMotion HandMotion => side == HandSide.Left ? draft.action.leftHandMotion : draft.action.rightHandMotion;
        private string TargetName => editObject ? (objectId != null ? ObjectLabel(objectId) : "pieza / diseño") : editWeapon ? "arma completa" : side == HandSide.Left ? "mano izquierda" : "mano derecha";
        private int CurrentFrame => LabTimeline.Frame(progress, DisplayDuration);

        [MenuItem("Tools/CodeHelper/Animation Lab/Abrir laboratorio")]
        public static void Open() => GetWindow<AnimationLabWindow>("Animation Lab");

        private void OnEnable()
        {
            minSize = new Vector2(1050, 760);
            simpleMode=EditorPrefs.GetBool("AnimationLab.SimpleMode",true);
            automaticRecovery=EditorPrefs.GetBool("AnimationLab.AutoRecovery",true);
            recoveryInterval=Mathf.Clamp(EditorPrefs.GetInt("AnimationLab.RecoveryInterval",120),30,900);
            lastAutoRecoveryAt=EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += UndoChanged;
        }

        private void OnDisable()
        {
            bool preserved=true;
            // Preserve unsaved work before assembly reload, editor shutdown or closing the window.
            if ((dirty || Pending || HasTakeTrial) && draft != null && preview != null)
            {
                try { PreserveOnClose(); }
                catch (Exception ex)
                {
                    preserved=false;playing=false;error="No se pudo conservar el borrador: "+ex.Message;
                    Debug.LogError("[AnimationLab] "+error+". No se destruye el borrador; vuelve a abrir la ventana y guarda una copia.");
                    EditorApplication.delayCall+=()=>{if(this!=null){Show();Repaint();}};
                }
            }
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= UndoChanged;
            if(preserved)ClosePreview();
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool recovered = false;
                try
                {
                    if ((dirty || Pending || HasTakeTrial) && draft != null && preview != null) PreserveOnClose();
                    recovered = true;
                }
                catch (Exception ex) { EditorApplication.isPlaying = false; Debug.LogError("[AnimationLab] Recuperación fallida: " + ex.Message); }
                finally { if (recovered) ClosePreview(); else playing = false; }
            }
        }

        private void UndoChanged() { CancelFrameDrag(); AbandonPathDragAfterUndo(); CancelTransitionBlockDrag(); CancelViewportDrag(); if (draft != null && preview != null) { if (HasHandTrial) trialStale = true; if (HasGuidedReloadTrial) guidedReloadStale = true; if (!HasHandTrial && !HasGuidedReloadTrial) dirty = true; wristReplacementApproved = weaponReplacementApproved = false; if(!quarantinedRecovery)Safe(SyncSelection); } Repaint(); }

        private void ClosePreview()
        {
            CancelTransitionBlockDrag(); CancelPathDrag(); CancelViewportDrag();
            CancelFrameDrag();
            CancelTransitionTrial(false);
            CancelWeaponMotionTrial(false);
            CancelGuidedReloadTrial(false);
            StopGripTransitionPreview();
            frameClipboard?.Dispose();frameClipboard=null;
            CancelHandTrial(false);
            playing = false;
            if (actionEditor != null) DestroyImmediate(actionEditor);
            quarantinedRecovery=false;quarantinedPending=null;
            preview?.Dispose(); preview = null;
            LabAssetIO.DestroyDraft(draft); draft = null;
            if (fingers != null) DestroyImmediate(fingers);
            if (fingerBase != null) DestroyImmediate(fingerBase);
            fingers = fingerBase = null;
            foreach (var obj in retired) if (obj != null && !AssetDatabase.Contains(obj)) DestroyImmediate(obj);
            retired.Clear(); dirty = false; previewingKey = fingersEdited = weaponPending = objectPending = false;
            followContacts.Clear(); navigating = false; navigationKeys.Clear();
            hasUnsavedChanges=false;
        }

        private bool CanDiscard() => (!dirty && !Pending && !HasTakeTrial) || EditorUtility.DisplayDialog("Borrador sin guardar", "Hay una toma, pose o prueba sin guardar. Guarda una copia o acepta/cancela la prueba antes de cambiar.", "Descartar borrador", "Volver");

        private void StartPreview()
        {
            ClosePreview();
            preview = new LabPreview(mode);
            NewDraft(preview.Equipment.SelectedSlot);
            lastTime = EditorApplication.timeSinceStartup;
        }

        private void NewDraft(int slot)
        {
            quarantinedRecovery=false;quarantinedPending=null;
            CancelFrameDrag(); CancelTransitionTrial(false); transitionPlan = null;
            CancelWeaponMotionTrial(false);
            CancelHandTrial(false);
            if (actionEditor != null) DestroyImmediate(actionEditor);
            LabAssetIO.DestroyDraft(draft);
            draft = LabAssetIO.Clone(null);
            draft.slot = slot;
            preview.Select(slot);
            draft.equipmentId = preview.Slot.definition.equipmentId;
            draft.kind = preview.Slot.definition is FirearmDefinition ? EquipmentActionKind.Reload : EquipmentActionKind.MeleeAttack;
            draft.action.duration = 1f;
            if(preview.Slot.definition is FirearmDefinition firearm&&firearm.reload!=null)
            {draft.handInsert.source=LabHandSource.Reload;draft.handInsert.template=LabHandTemplate.Rifle;draft.action.duration=Mathf.Clamp(firearm.reload.duration,.01f,300);}
            draft.action.blendIn = draft.action.blendOut = 0.08f;
            saved = null; progress = 0; playing = false; dirty = false;
            takeLineage=null;guidedPlayed=false;guidedRhythm=false;guidedStep=1;
            wristReplacementApproved = weaponReplacementApproved = false;
            editObject = false; objectPending = false; objectId = null; objectReplacementApproved = false;
            SyncSelection();
        }

        private void Load(AnimationLabTake source)
        {
            if (source == null || source.action == null) throw new InvalidOperationException("La toma no contiene una acción.");
            if (source.slot < 0 || source.slot >= preview.Equipment.slots.Length
                || preview.Equipment.slots[source.slot].definition == null
                || preview.Equipment.slots[source.slot].definition.equipmentId != source.equipmentId)
                throw new InvalidOperationException("La toma pertenece a otro equipo. Usa el mismo rig/equipmentId antes de cargarla.");
            LabTakeValidation.RequireValid(source);
            var loaded = LabAssetIO.Clone(source);
            CancelFrameDrag(); CancelTransitionTrial(false); transitionPlan = null;
            CancelWeaponMotionTrial(false);
            CancelHandTrial(false);
            if (actionEditor != null) DestroyImmediate(actionEditor);
            LabAssetIO.DestroyDraft(draft);
            draft = loaded; saved = source; takeName = source.name;
            quarantinedRecovery=false;quarantinedPending=null;
            takeLineage=LabTakeCatalog.Lineage(source);guidedPlayed=false;guidedRhythm=false;guidedStep=1;
            editObject = false; objectId = null; objectPending = false; objectReplacementApproved = false;
            preview.RestoreAnchors(); preview.Select(draft.slot);
            if (draft.left.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Left);
            if (draft.right.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Right);
            LabAssetIO.ApplyAnchors(draft, preview);
            progress = 0; playing = false; dirty = false;
            wristReplacementApproved = weaponReplacementApproved = false;
            SyncSelection();
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - lastTime), 0f, 0.1f); lastTime = now;
            if (preview == null || draft == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(quarantinedRecovery){TickAutomaticRecovery();Repaint();return;}
            AdvanceNavigation(dt);
            try
            {
                AdvanceGripTransitionPreview(dt);
                if (playing)
                {
                    progress += dt * speed / Mathf.Max(0.001f, DisplayDuration);
                    GesturePlaybackRange(out float start, out float end);
                    if (progress >= end) { if (loop) progress = start + (progress - start) % Mathf.Max(.000001f, end - start); else { progress = end; playing = false; SyncSelection(); } }
                }
                TickAutomaticRecovery();
                EvaluateNow(!playing && Pending);
                Repaint();
            }
            catch (Exception ex) { error = ex.GetBaseException().Message; playing = false; }
        }

        private void Safe(Action action)
        {
            try { error = null; action(); }
            catch (ExitGUIException) { throw; }
            catch (Exception ex) { error = ex.GetBaseException().Message; Debug.LogException(ex); }
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout) RebuildReviewCache();
            DrawStudioHeader();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("Trabaja fuera de Play. El laboratorio ya evalúa columna, cámara e IK sin iniciar el juego.", MessageType.Info); return; }
            if (preview == null)
            {
                DrawHome();
                return;
            }
            if(quarantinedRecovery){DrawRecoveryRepair();return;}
            FrameClipboardShortcuts();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(studioTab == 1 ? Mathf.Clamp(position.width * .4f, 410, 530) : Mathf.Clamp(position.width * .34f, 360, 490))))
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    DrawStudioInspector();
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope()) { DrawView(); }
            }
            DrawTimeline();
        }

        private void DrawSetup()
        {
            EditorGUILayout.LabelField("1 · Equipo y acción", EditorStyles.boldLabel);
            string[] names = preview.Equipment.slots.Select(s => s.definition != null ? s.definition.displayName : "Vacío").ToArray();
            int slot = EditorGUILayout.Popup("Equipo", draft.slot, names);
            if (slot != draft.slot && CanDiscard()) Safe(() => { preview.RestoreAnchors(); NewDraft(slot); });
            var sources = new List<EquipmentActionDefinition>();
            var definition = preview.Slot.definition;
            sources.Add(definition.equip); sources.Add(definition.unequip);
            if (definition is FirearmDefinition gun) sources.AddRange(new[] { gun.reload, gun.reloadEmpty, gun.fire, gun.cycle });
            if (definition is MeleeDefinition melee && melee.attacks != null) sources.AddRange(melee.attacks);
            var choices = sources.Where(a => a != null).Distinct().ToArray();
            if (highlightDuplicateAction)
                EditorGUILayout.HelpBox("Elige aquí la acción que quieres duplicar. Se abrirá una copia editable y el original no cambiará.", MessageType.Info);
            int chosen = EditorGUILayout.Popup("Copiar acción existente", 0, new[] { "Seleccionar…" }.Concat(choices.Select(a => a.name)).ToArray());
            if (chosen > 0 && CanDiscard()) Safe(() =>
            {
                highlightDuplicateAction = false;
                var source = choices[chosen - 1];
                preview.RestoreAnchors(); NewDraft(draft.slot);
                if (actionEditor != null) DestroyImmediate(actionEditor);
                DestroyImmediate(draft.action);
                draft.action = Instantiate(source); draft.action.hideFlags = HideFlags.HideAndDontSave;
                draft.hasSourceCharacterClip = true; draft.sourceCharacterClip = source.characterClip;
            draft.sourceCharacterMask = source.characterMask;
                draft.kind = source == definition.equip ? EquipmentActionKind.Equip : source == definition.unequip ? EquipmentActionKind.Unequip :
                    definition is FirearmDefinition f ? (source == f.fire ? EquipmentActionKind.Fire : source == f.cycle ? EquipmentActionKind.Cycle : EquipmentActionKind.Reload) : EquipmentActionKind.MeleeAttack;
                progress = 0; dirty = true;
                wristReplacementApproved = weaponReplacementApproved = false;
                SyncSelection();
            });
            EditorGUI.BeginChangeCheck();
            takeName = EditorGUILayout.TextField("Nombre de la toma", takeName);
            draft.kind = (EquipmentActionKind)EditorGUILayout.EnumPopup("Tipo", draft.kind);
            if (EditorGUI.EndChangeCheck()) dirty = true;
            DrawDurationEditor();
            EditorGUILayout.HelpBox("Lanzamientos/manipulaciones pueden editarse como movimiento. Proyectiles, daño y eventos de juego no se ejecutan aquí.", MessageType.None);
        }

        private void DrawHand()
        {
            LabStudioUI.Section("Duración de la animación");
            DrawDurationEditor();
            EditorGUILayout.Space(); EditorGUILayout.LabelField("2 · ¿Qué quieres animar?", EditorStyles.boldLabel);
            int selected = editObject ? 3 : editWeapon ? 2 : (int)side;
            int next = GUILayout.Toolbar(selected, new[] { "Izquierda", "Derecha", "Arma", "Piezas / diseños" });
            if (next != selected) Safe(() => SelectTrack(next == 3 ? 5 : next == 2 ? 4 : next * 2 + (editFingers ? 1 : 0), progress));
            if (!editWeapon && !editObject)
            {
                int tool = GUILayout.Toolbar(editFingers ? 1 : 0, new[] { "Posición y giro", "Solo dedos" });
                if (tool != (editFingers ? 1 : 0)) Safe(() => SelectTrack((int)side * 2 + tool, progress));
            }
            EditorGUILayout.LabelField($"Editar {TargetName} · F {CurrentFrame} · {progress * DisplayDuration:F3} s", EditorStyles.boldLabel);
            DrawChannelOrigin();
            DrawMovePoint();
            DrawTransitionAssistant();
            SaveTransitionPreferences();
            using (new EditorGUI.DisabledScope(playing))
            { if (editObject) DrawObjectEditor(); else if (editFingers && !editWeapon) DrawFingerEditor(); else {if(editWeapon&&!simpleMode)DrawWeaponDynamics();DrawPositionEditor();} }
            if (!HasTakeTrial && !editObject && !editWeapon && !editFingers && side == draft.handInsert.side)
                if (GUILayout.Button("Usar esta pose para la recarga · sin crear clave",new GUIStyle(GUI.skin.button){wordWrap=true},GUILayout.MinWidth(0),GUILayout.Height(30))) Safe(() => { CaptureHandTemplateContact(); studioTab = 0; });
            if(!simpleMode)DrawPathEditor();
            DrawKeyList();
            if (!simpleMode && !editWeapon && !editObject)
            {
                calibrations = EditorGUILayout.Foldout(calibrations, "Ajustar agarres de reposo (no es una animación)", true);
                if (calibrations) DrawAnchors();
            }
            if(!simpleMode)advanced = EditorGUILayout.Foldout(advanced, "Avanzado: clips, curvas, pesos y contacto", true);
            if (advanced&&!simpleMode)
            {
                UnityEditor.Editor.CreateCachedEditor(draft.action, null, ref actionEditor);
                EditorGUI.BeginChangeCheck(); actionEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(draft,"Recalcular receta del recorrido combinado");dirty = true;
                    if (!Pending) Safe(()=>{RefreshCoordinatedPaths();SyncSelection();});
                }
            }
        }

        private void DrawDurationEditor()
        {
            using(new EditorGUI.DisabledScope(HasTakeTrial))
            {
                float seconds=EditorGUILayout.DelayedFloatField(new GUIContent("Duración (segundos)","Reajusta toda la toma sin borrar puntos ni modificar el clip original."),DisplayDuration);
                int frames=EditorGUILayout.DelayedIntField(new GUIContent("Fotogramas (60 fps)","Duración total a 60 fotogramas por segundo."),LabTimeline.LastFrame(DisplayDuration));
                if(!HasTakeTrial&&seconds!=draft.action.duration)Safe(()=>SetDuration(seconds));
                else if(!HasTakeTrial&&frames!=LabTimeline.LastFrame(draft.action.duration))Safe(()=>SetDuration(Mathf.Max(1,frames)/60f));
            }
            if(HasTakeTrial)EditorGUILayout.LabelField("Acepta o cancela la prueba para cambiar la duración.",EditorStyles.wordWrappedMiniLabel);
            if(draft.action.duration>30.0001f)
                EditorGUILayout.HelpBox("Esta toma supera 30 s. El recorrido combinado, la inserción de manos, las recargas y el movimiento natural admiten hasta 30 s.",MessageType.Warning);
        }
        private void SetDuration(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds))throw new InvalidOperationException("La duración debe ser un número finito.");
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de cambiar la duración.");
            if(!CanLeaveFrame())return;
            float previous=draft.action.duration;bool previousPending=coordinatedPathPending;
            Undo.RecordObject(draft,"Cambiar duración");Undo.RecordObject(draft.action,"Cambiar duración");
            draft.action.duration=Mathf.Clamp(seconds,.01f,300);
            try{RefreshCoordinatedPaths();}
            catch{draft.action.duration=previous;coordinatedPathPending=previousPending;throw;}
            playing=false;dirty=true;SyncSelection();
        }

        private void DrawPositionEditor()
        {
            EditorGUILayout.HelpBox(editObject ? "Edita esta pieza o diseño, no el arma completa. Mover junto con permite guardar también la pose de las manos en el mismo fotograma."
                : editWeapon ? "Mueve el soporte del arma completa. Para el cargador o cerrojo, usa Piezas / diseños. Las manos acompañan solo si sus grips/controles lo permiten."
                : "Arrastra el punto amarillo para mover esta mano. Los campos siguen la pose del fotograma elegido. Guardar posición no cambia los dedos.", MessageType.None);
            worldSpace = GUILayout.Toolbar(worldSpace ? 1 : 0, new[] { "Local", "Global (escenario)" }) == 1;
            EditedWorldPose(out var worldPosition, out var worldRotation);
            Vector3 units = LocalUnitScale;
            EditorGUI.BeginChangeCheck();
            Vector3 p = EditorGUILayout.Vector3Field(new GUIContent(worldSpace ? "Posición global (cm)" : editObject ? "Posición local (cm)" : "Desplazamiento (cm)", "Global usa los ejes del escenario; se convierte al espacio local de la receta al guardar. No bloquea la mano en el mundo durante toda la animación."),
                (worldSpace ? worldPosition : Vector3.Scale(keyPosition, units)) * 100f) * 0.01f;
            Vector3 r = EditorGUILayout.Vector3Field("Giro (grados)", worldSpace ? LabTimeline.UnwrapEuler(worldRotation.eulerAngles, Vector3.zero) : keyRotation);
            if (EditorGUI.EndChangeCheck()) Safe(() =>
            {
                LabTakeValidation.RequireFinite(p, "La posición"); LabTakeValidation.RequireFinite(r, "El giro");
                if (!BeginPositionEdit()) return;
                if (worldSpace) SetEditedWorldPose(p, Quaternion.Euler(r));
                else { keyPosition = new Vector3(p.x / Mathf.Max(units.x, 0.000001f), p.y / Mathf.Max(units.y, 0.000001f), p.z / Mathf.Max(units.z, 0.000001f)); keyRotation = r; }
            });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!editObject && GUILayout.Button(new GUIContent("Volver al reposo", "Pone el desplazamiento y giro a cero en este fotograma; todavía debes guardar el punto.")))
                    Safe(() => { if (BeginPositionEdit()) keyPosition = keyRotation = Vector3.zero; });
                if (GUILayout.Button("Descartar ajuste")) Safe(() =>
                {
                    if (Pending && !EditorUtility.DisplayDialog("Descartar ajuste", "Se perderá la pose editada que todavía no has guardado como punto.", "Descartar", "Volver")) return;
                    SyncSelection();
                });
            }
            if (GUILayout.Button($"● {(editObject && followHands > 0 ? "Guardar conjunto mano + pieza" : "Guardar posición")} en F {CurrentFrame}", GUILayout.Height(32))) Safe(() => CommitEdits(true));
            if (previewingKey || weaponPending || objectPending) EditorGUILayout.HelpBox("POSE SIN CONFIRMAR · Guarda el punto. Al cambiar de fotograma te preguntaremos qué hacer.", MessageType.Warning);
        }

        private void DrawFingerEditor()
        {
            EditorGUILayout.LabelField("Agarre de dedos · no cambia el recorrido de la muñeca",EditorStyles.wordWrappedMiniLabel);
            DrawSavedFingerStatus();
            DrawHandPieceContactStatus();
            DrawGripArrivalPicker();
            bool savedFingerKey=HandKeys.Any(k=>k.fingers!=null&&Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
            if(!savedFingerKey&&!fingersEdited)
            {
                EditorGUILayout.HelpBox("No hay una clave de dedos en este fotograma. Navegar no crea una transición ni cambia el agarre guardado. Elige un agarre de llegada arriba para añadirlo aquí.",MessageType.Info);
                if(GUILayout.Button($"Editar pose actual como nuevo agarre en F {CurrentFrame}"))Safe(()=>
                {SetFingerPose(Instantiate(fingers));fingerFineEdit=true;});
                return;
            }
            DrawFingerTransition();
            if(GUILayout.Button($"3 · Guardar agarre de llegada en F {CurrentFrame}",GUILayout.Height(30)))Safe(()=>CommitEdits(true));
            fingerFineEdit=EditorGUILayout.Foldout(fingerFineEdit,"Ajustar dedos / crear variantes",true);
            if(fingerFineEdit)
            {
            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Usar pose de este fotograma como base", "Captura la pose fuente sin fundidos para no degradar los dedos en los extremos de la toma."))) Safe(CaptureFingerBase);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mano abierta")) Safe(() => FingerPreset(1));
                if (GUILayout.Button("Mano cerrada")) Safe(() => FingerPreset(-1));
            }
            var poseAsset = (HandPoseData)EditorGUILayout.ObjectField("Cargar pose de dedos", selectedPose, typeof(HandPoseData), false);
            if (poseAsset != selectedPose && poseAsset != null) Safe(() =>
            {
                if (poseAsset.Side != side) throw new InvalidOperationException("Esta pose pertenece a la otra mano.");
                SetFingerPose(Instantiate(poseAsset));selectedPose=poseAsset;
            });
            int axis = EditorGUILayout.Popup("Eje local de flexión", curlAxis, new[] { "X", "Y", "Z" });
            if (axis != curlAxis) { curlAxis = axis; Safe(UpdateFingerCurl); }
            if (fingerBase != null)
            {
                EditorGUI.BeginChangeCheck();
                string[] labels = { "Pulgar", "Índice", "Medio", "Anular", "Meñique" };
                for (int f = 0; f < 5; f++)
                {
                    float amount = EditorGUILayout.Slider(labels[f], curls[f].x, -100f, 100f);
                    curls[f] = new Vector3(amount, 0, 0);
                }
                if (EditorGUI.EndChangeCheck()) Safe(UpdateFingerCurl);
                joints = EditorGUILayout.Foldout(joints, "Ajuste fino de articulaciones", true);
                if (joints) for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++)
                {
                    var entry = fingers.GetEntry((FingerId)f, j); if (entry == null) continue;
                    EditorGUI.BeginChangeCheck();
                    var angles = EditorGUILayout.Vector3Field(labels[f] + " · " + (j + 1), entry.localRotation.eulerAngles);
                    if (EditorGUI.EndChangeCheck()) Safe(() => { LabTakeValidation.RequireFinite(angles,"El giro de dedos"); StopGripTransitionPreview();entry.localRotation = Quaternion.Euler(angles); fingersEdited = true;fingerPoseLabel="Pose editada"; });
                }
            }
            if (GUILayout.Button($"● Guardar dedos en F {CurrentFrame}", GUILayout.Height(32))) Safe(() => CommitEdits(true));
            if (fingersEdited) EditorGUILayout.HelpBox("DEDOS SIN CONFIRMAR · Se muestra la forma sin fundidos mientras editas. Reproducir usa la mezcla final.", MessageType.Warning);
            if (GUILayout.Button("Guardar pose de dedos independiente…")) Safe(SaveFingerPose);
            DrawGripLibrary();
            }
        }

        private void DrawAnchors()
        {
            var s = preview.Slot;
            var points = new[] { preview.Guard(side), side == HandSide.Left ? s.leftHint : s.rightHint,
                side == HandSide.Left ? s.leftHandPose?.actionTarget : s.rightHandPose?.actionTarget,
                side == HandSide.Left ? s.leftReloadTarget : null }.Where(t => t != null).Distinct().ToArray();
            if (points.Length == 0) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Calibrar agarre / punto", EditorStyles.boldLabel);
            anchorIndex = EditorGUILayout.Popup(Mathf.Clamp(anchorIndex, 0, points.Length - 1), points.Select(t => t.name).ToArray());
            var point = points[anchorIndex];
            EditorGUI.BeginChangeCheck();
            var scale=point.parent!=null?point.parent.lossyScale:Vector3.one;
            var units=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            var p = EditorGUILayout.Vector3Field("Posición local (cm)", Vector3.Scale(point.localPosition,units)*100f);
            var r = EditorGUILayout.Vector3Field("Giro local", point.localEulerAngles);
            if (EditorGUI.EndChangeCheck())
            {
                Safe(() => ApplyAnchorEdit(point,p,r));
            }
        }
        private void ApplyAnchorEdit(Transform point, Vector3 centimeters, Vector3 euler)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Termina la prueba antes de calibrar.");
            if(!CanLeaveFrame())return;
            LabTakeValidation.RequireFinite(centimeters,"La calibración");LabTakeValidation.RequireFinite(euler,"El giro");
            var scale=point.parent!=null?point.parent.lossyScale:Vector3.one;
            var units=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            if(units.x<.000001f||units.y<.000001f||units.z<.000001f)throw new InvalidOperationException("La referencia tiene una escala nula.");
            var oldPosition=point.localPosition;var oldRotation=point.localRotation;
            Undo.RecordObject(point,"Calibrar agarre (copia)");Undo.RecordObject(draft,"Recalcular recorrido");Undo.RecordObject(draft.action,"Recalcular recorrido");
            try
            {
                RunAuthoringTransaction(() =>
                {
                    point.SetLocalPositionAndRotation(new Vector3(centimeters.x/units.x,centimeters.y/units.y,centimeters.z/units.z)*.01f,Quaternion.Euler(euler));
                    RefreshCoordinatedPaths();SyncSelection();
                });
            }
            catch{point.SetLocalPositionAndRotation(oldPosition,oldRotation);throw;}
        }

        private void DrawView()
        {
            firstPerson = GUILayout.Toolbar(firstPerson ? 1 : 0, new[] { "Personaje · arrastrar para orbitar", "Primera persona" }) == 1;
            if(HasTransitionTrial)DrawTransitionTrialActions();
            else if(HasWeaponMotionTrial)WeaponMotionTrialActions();
            else if(HasGuidedReloadTrial)DrawGuidedReloadTrialActions();
            else if(studioTab==0||HasHandTrial)TrialActions();
            Rect rect = GUILayoutUtility.GetRect(200, 1600, 128, 800, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
            {
                try { GUI.DrawTexture(ImageRect(rect), preview.Render((int)rect.width, (int)rect.height, firstPerson), ScaleMode.StretchToFill, false); }
                catch (Exception ex) { error = ex.GetBaseException().Message; }
            }
            rect = ImageRect(rect);
            if(gripTransitionActive)GUI.Label(new Rect(rect.x+10,rect.y+10,rect.width-20,42),
                $"PRUEBA DE AGARRE · F {LabTimeline.Frame(gripTransitionTime,draft.action.duration)} · adaptación {GripTransitionWeight:P0}\nDestino en F {CurrentFrame} · no se ha guardado esta prueba",EditorStyles.whiteMiniLabel);
            DrawPathReferences(rect);
            DrawGestureHandles(rect);
            DrawSegmentHandle(rect);
            DrawGestureWarnings(rect);
            DrawCameraNavigation(rect);
            if ((!editFingers || editWeapon || editObject) && !playing && !HasTakeTrial && studioTab == 1) DrawWristHandle(rect);
            if (rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && !firstPerson && !Event.current.shift)
                { preview.orbit += new Vector2(Event.current.delta.x, -Event.current.delta.y) * 0.5f; Event.current.Use(); }
                if (Event.current.type == EventType.ScrollWheel && !firstPerson)
                { preview.distance = Mathf.Clamp(preview.distance + Event.current.delta.y * 0.08f, 0.25f, 8f); Event.current.Use(); }
            }
            pitch = EditorGUILayout.Slider("Mirar arriba / abajo", pitch, -90, 90);
            yaw = EditorGUILayout.Slider("Mirar a los lados", yaw, -80, 80);
            aim = EditorGUILayout.Toggle("Probar Aim", aim);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frontal")) SetCameraView(180f, 0f);
                if (GUILayout.Button("Lateral")) SetCameraView(90f, 0f);
                if (GUILayout.Button("Tres cuartos")) SetCameraView(155f, 8f);
                if (GUILayout.Button("Superior")) SetCameraView(155f, 75f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Encuadrar selección")) FocusSelection();
                if (GUILayout.Button("Restablecer vista")) { preview.pan = Vector3.zero; preview.orbit = new Vector2(155, 8); preview.distance = 2.4f; }
            }
            EditorGUILayout.LabelField("Rueda: zoom · Botón central/Shift+arrastrar: desplazar · RMB+WASD/QE: navegar", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox($"F {CurrentFrame} · {TargetName}" + (Pending ? " · Ajuste sin confirmar (sin fundidos)" : " · Vista de reproducción con fundidos"), MessageType.None);
        }

        private void SetCameraView(float orbitYaw, float orbitPitch)
        {
            if (preview == null) return;
            firstPerson = false;
            preview.pan = Vector3.zero;
            preview.orbit = new Vector2(orbitYaw, orbitPitch);
            preview.distance = 2.4f;
            EvaluateNow(Pending);
            Repaint();
        }

        private void DrawWristHandle(Rect rect)
        {
            DrawPoseGizmo(rect);
        }

        private void DrawTimeline() => DrawFrameTimeline();

        private void DrawSave()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("4 · Guardar / llevar al juego" + (dirty ? " · cambios sin guardar" : ""), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Copia actual", saved != null ? AssetDatabase.GetAssetPath(saved) : "Sin guardar todavía", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = (AnimationLabTake)EditorGUILayout.ObjectField("Abrir toma", saved, typeof(AnimationLabTake), false);
                if (GUILayout.Button(showSaveBrowser ? "Ocultar" : "Buscar…", GUILayout.Width(80))) showSaveBrowser = !showSaveBrowser;
                if (selected != saved && selected != null) Safe(() => { OpenTake(selected, false); GUIUtility.ExitGUI(); });
            }
            if (showSaveBrowser) DrawSaveBrowser();
            if (GUILayout.Button(new GUIContent("Guardar como nueva copia", "Crea un asset nuevo en " + LabAssetIO.Output + ". No sobrescribe la copia actual."), GUILayout.Height(30))) Safe(SaveTake);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Guardar recuperación ahora", GUILayout.Height(24))) Safe(SaveRecoveryNow);
                using (new EditorGUI.DisabledScope(saved == null))
                    if (GUILayout.Button("Revelar copia actual", GUILayout.Height(24))) EditorUtility.RevealInFinder(AssetDatabase.GetAssetPath(saved));
            }
            using (new EditorGUI.DisabledScope(saved == null || dirty || Pending))
                if (GUILayout.Button("Exportar copia guardada al juego (.unitypackage)…")) Safe(() =>
                {
                    string path = EditorUtility.SaveFilePanel("Exportar al juego", "", saved.name, "unitypackage");
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.ExportPackage(LabAssetIO.ExportPaths(saved), path, ExportPackageOptions.Default);
                        EditorUtility.RevealInFinder(path);
                    }
                });
            EditorGUILayout.HelpBox("Cada guardado crea un asset nuevo para no sobrescribir la versión anterior; las copias se conservan en " + LabAssetIO.Output + ". Exporta la toma y scripts portables. Los recursos externos deben existir en el juego con sus GUID. Si partes de un clip de terceros, su bake puede contener curvas derivadas: úsalo solo donde tu licencia lo permita. No sobrescribe tu escena del juego.", MessageType.Info);
        }

        private void CaptureFingerBase()
        {
            playing = false;
            var edited = fingersEdited && fingers != null ? Instantiate(fingers) : null;
            ClearFingerOverride();
            fingerBase = edited != null ? edited : CreateInstance<HandPoseData>();
            fingerBase.Side = side; fingerBase.hideFlags = HideFlags.HideAndDontSave;
            if (edited == null)
            {
                float blendIn = draft.action.blendIn, blendOut = draft.action.blendOut;
                try
                {
                    draft.action.blendIn = draft.action.blendOut = 0;
                    preview.Evaluate(draft, progress, pitch, yaw, aim, rawFingers: true);
                    if (!fingerBase.Capture(preview.Animator)) throw new InvalidOperationException("El avatar no tiene dedos de esta mano.");
                }
                finally { draft.action.blendIn = blendIn; draft.action.blendOut = blendOut; }
            }
            fingers = Instantiate(fingerBase); fingers.hideFlags = HideFlags.HideAndDontSave;
            curls = new Vector3[5]; fingersEdited = edited != null;
            EvaluateNow(Pending);
        }

        private void ClearFingerOverride()
        {
            StopGripTransitionPreview();
            if (fingers != null) DestroyImmediate(fingers);
            if (fingerBase != null) DestroyImmediate(fingerBase);
            fingers = fingerBase = null;
            selectedPose = null;
            fingersEdited = false;
        }

        private void FingerPreset(float stretch)
        {
            fingerPoseLabel = stretch > 0 ? "Mano abierta natural" : "Puño táctico cerrado";
            playing = false; ClearFingerOverride();
            var pose = stretch > 0
                ? LabGripTools.Open(preview.Animator, side)
                : LabGripTools.Preset(preview.Animator, side, 11); // Puño táctico cerrado
            try
            {
                fingerBase = Instantiate(pose);
                fingerBase.Side = side;
                fingers = Instantiate(fingerBase);
                fingerBase.hideFlags = fingers.hideFlags = HideFlags.HideAndDontSave;
                curls = new Vector3[5];
                fingersEdited = true;
            }
            finally
            {
                DestroyImmediate(pose);
            }
            EvaluateNow(Pending);
        }

        private void UpdateFingerCurl()
        {
            if (fingerBase == null) return;
            StopGripTransitionPreview();
            fingerPoseLabel="Pose editada";
            playing = false; fingersEdited = true;
            for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++)
            {
                var original = fingerBase.GetEntry((FingerId)f, j);
                var entry = fingers.GetEntry((FingerId)f, j);
                if (original == null || entry == null) continue;
                Vector3 axis = curlAxis == 0 ? Vector3.right : curlAxis == 1 ? Vector3.up : Vector3.forward;
                entry.localRotation = original.localRotation * Quaternion.AngleAxis(curls[f].x * (j == 0 ? 0.6f : 1f), axis);
            }
        }

        private bool previewingKey;

        private void ApplyTemporaryKey()
        {
            var hand = side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
            var guard = preview.Guard(side);
            hand.target.SetPositionAndRotation(guard.TransformPoint(keyPosition), guard.rotation * Quaternion.Euler(keyRotation));
            LabPreview.Call(preview.Equipment.handIK, "LateUpdate");
            if (fingersEdited) fingers?.Apply(preview.Animator);
        }

        private void SaveFingerPose()
        {
            var pose = fingers != null ? Instantiate(fingers) : CreateInstance<HandPoseData>(); pose.Side = side;
            if (fingers == null && !pose.Capture(preview.Animator)) { DestroyImmediate(pose); return; }
            string path = EditorUtility.SaveFilePanelInProject("Guardar pose de dedos", side + "_Grip", "asset", "Guardar una nueva pose sin sobrescribir otra.", LabAssetIO.Output);
            if (string.IsNullOrEmpty(path)) { DestroyImmediate(pose); return; }
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            pose.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(pose, path); AssetDatabase.SaveAssets(); EditorGUIUtility.PingObject(pose);
        }
    }
}
