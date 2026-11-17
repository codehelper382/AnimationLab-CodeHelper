using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool handTemplateGuide;
        private int gesturePane;
        private string variantName = "Mi variante";

        private void DrawHandTemplates()
        {
            var r = GestureSettings;
            LabStudioUI.Section("Recargas rápidas", "1 · Elige base   /   2 · Ajusta recorrido   /   3 · Prueba y acepta");
            DrawReloadReferencePicker();
            if(r.source==LabHandSource.Procedural)
            {
            int template = EditorGUILayout.Popup("Ejemplo de manos", (int)r.template, LabHandTemplateLibrary.Names);
            if (template != (int)r.template && CanLeaveFrame())
            {
                Undo.RecordObject(GestureOwner, "Elegir ejemplo de manos"); r.template = (LabHandTemplate)template;
                r.side = r.template == LabHandTemplate.Bow || r.template == LabHandTemplate.SniperBolt ? HandSide.Right : HandSide.Left;
                r.travel = r.template == LabHandTemplate.Bow ? new Vector3(0, 0, -0.25f) : r.template == LabHandTemplate.SniperBolt ? new Vector3(0, 0, -0.08f) : new Vector3(-0.18f, -0.05f, 0);
                r.hasContact = false; r.phaseTimes=Array.Empty<float>(); r.phaseGrip=Array.Empty<float>(); r.points.Clear(); GestureChanged();
            }
            }
            int last = LabTimeline.LastFrame(draft.action.duration);
            int first = LabTimeline.Frame(r.start, draft.action.duration), end = LabTimeline.Frame(r.end, draft.action.duration);
            EditorGUI.BeginChangeCheck();
            var hand = (HandSide)EditorGUILayout.Popup("Mano que anima", (int)r.side, new[] { "Izquierda", "Derecha" });
            int minimum = Mathf.Min(6, last);
            first = EditorGUILayout.IntSlider("Insertar desde F", first, 0, Mathf.Max(0, last - minimum));
            end = EditorGUILayout.IntSlider("Hasta F", Mathf.Max(first + minimum, end), first + minimum, last);
            int repeats = r.repetitions;
            if (r.source==LabHandSource.Procedural&&(r.template == LabHandTemplate.SingleRound || r.template == LabHandTemplate.Revolver || r.template == LabHandTemplate.Shotgun))
                repeats = EditorGUILayout.IntSlider("Gestos / cartuchos", repeats, 1, 8);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(GestureOwner, "Configurar inserción de manos");
                if (hand != r.side) { r.hasContact = false; r.points.Clear(); }
                r.side = hand; r.start = LabTimeline.Time(first, draft.action.duration); r.end = LabTimeline.Time(end, draft.action.duration);
                // Edited visual paths are never silently regenerated when the number of gestures changes.
                if(repeats!=r.repetitions&&r.points.Count>=2)
                {
                    if(EditorUtility.DisplayDialog("Cambiar repeticiones","El recorrido tiene puntos editables. Para conservar las repeticiones nuevas hay que reconstruirlo y reemplazar esos puntos. Las claves de la toma no cambian hasta Aceptar.","Reconstruir recorrido","Conservar recorrido"))
                    {r.repetitions=repeats;Safe(CreateGesturePoints);}
                }
                else r.repetitions = repeats;
                GestureChanged();
            }
            gesturePane=GUILayout.Toolbar(gesturePane,new[]{"RECORRIDO","RITMO / DEDOS","VARIANTES"},GUILayout.Height(26));
            if(gesturePane==0) DrawGesturePointInspector();
            else if(gesturePane==1) DrawGestureArtControls();
            else DrawGestureVariants();
            if (last < 6) EditorGUILayout.HelpBox("Aumenta la duración: insertar necesita al menos 6 fotogramas.", MessageType.Warning);
            using(new EditorGUILayout.HorizontalScope())
            { loopHandRange=GUILayout.Toggle(loopHandRange,"Repetir tramo"); loopPhase=GUILayout.Toggle(loopPhase,"Solo fase seleccionada"); }
            handTemplateGuide = EditorGUILayout.Foldout(handTemplateGuide, "Guion de esta recarga · qué hacer en cada fotograma", true);
            if (handTemplateGuide)
            {
                if(r.source==LabHandSource.Procedural)foreach (string step in HandTemplateGuide(r.template, first, end)) EditorGUILayout.HelpBox(step, MessageType.None);
                EditorGUILayout.HelpBox(r.source==LabHandSource.Procedural?"Guion orientativo: al ajustar fases usa los bloques de la línea de tiempo.":"La referencia conserva el gesto de la recarga existente. Las fases solo retiman ese gesto; la prueba inserta la mano elegida. Usa Abrir recarga completa para conservar también el resto de la receta.", MessageType.None);
            }
        }

        internal static string[] HandTemplateGuide(LabHandTemplate template, int first, int end)
        {
            return LabHandTemplateLibrary.Guides[(int)template].Select(line => Regex.Replace(line, @"\d+", m =>
                (first + Mathf.RoundToInt(int.Parse(m.Value) * (end - first) / (float)LabHandTemplateLibrary.Lengths[(int)template])).ToString())).ToArray();
        }

        private void CaptureHandTemplateContact()
        {
            var r = GestureSettings;
            if (editObject || editWeapon || editFingers || side != r.side) throw new InvalidOperationException("Selecciona primero la posición de la mano que anima.");
            EvaluateNow(Pending);
            var chain = r.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
            var target = chain.target != null ? chain.target : chain.hand;
            var guard = preview.Guard(r.side);
            if (guard == null) throw new InvalidOperationException("Esta mano necesita una guardia compatible.");
            Undo.RecordObject(GestureOwner, "Marcar contacto de la mano");
            r.contactPosition = guard.InverseTransformPoint(target.position); r.contactEuler = (Quaternion.Inverse(guard.rotation) * target.rotation).eulerAngles;
            r.hasContact = true; GestureChanged(); notice = "Contacto marcado. Ahora prueba la plantilla; no necesitas Guardar posición.";
            SyncSelection();
        }

        private void InsertHandTemplate()
        {
            if (!CanLeaveFrame()) return;
            if (!EditorUtility.DisplayDialog("Insertar solo la mano elegida", "Se reemplazará el gesto dentro del tramo elegido. Para conservar su continuidad, el canal de esa mano se convierte a puntos a 60 Hz (incluido su movimiento original fuera del tramo), con IK completo durante el gesto. Los dedos también se muestrean; fuera del tramo la conservación es aproximada a esa resolución. No cambia duración, tipo, fundidos, arma, piezas ni otra mano. Puedes deshacer.", "Insertar manos", "Cancelar")) return;
            var generated = LabHandTemplateInsertion.Insert(draft, preview);
            try { ApplyHandTemplate(generated); }
            finally { LabAssetIO.DestroyDraft(generated); }
        }

        private void ApplyHandTemplate(AnimationLabTake generated)
        {
            var hand = generated.handInsert.side;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.RecordObject(draft, "Insertar plantilla de mano"); Undo.RecordObject(draft.action, "Insertar plantilla de mano");
            draft.handInsert = LabGestureAuthoring.Copy(generated.handInsert);
            var old = hand == HandSide.Left ? draft.left : draft.right;
            foreach (var pose in old.Select(k => k.fingers).Where(p => p != null)) retired.Add(pose);
            var keys = hand == HandSide.Left ? generated.left : generated.right;
            foreach (var pose in keys.Select(k => k.fingers).Where(p => p != null)) retired.Add(pose);
            if (hand == HandSide.Left)
            {
                draft.left = keys; draft.action.leftHandMotion = generated.action.leftHandMotion;
                draft.action.leftIK = generated.action.leftIK; draft.action.leftHandTargetMotion = generated.action.leftHandTargetMotion;
                generated.left = new List<LabHandKey>();
            }
            else
            {
                draft.right = keys; draft.action.rightHandMotion = generated.action.rightHandMotion; draft.action.rightIK = generated.action.rightIK;
                generated.right = new List<LabHandKey>();
            }
            Undo.CollapseUndoOperations(group);
            progress = draft.handInsert.start; playing = false; dirty = true;
            notice = "Mano insertada. Pulsa Reproducir. El arma y las piezas no se han movido: sigue el guion si quieres animarlas aparte.";
            SyncSelection();
        }
    }
}
