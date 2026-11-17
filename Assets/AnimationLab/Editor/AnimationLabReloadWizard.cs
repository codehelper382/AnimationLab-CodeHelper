using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool reloadWizard;
        private int reloadPlacement = -1;
        private static readonly string[] ReloadNames = { "Cargador · sacar / insertar", "Revólver · abrir tambor y cargar", "Cartucho a cartucho · sniper / escopeta", "Cerrojo · levantar / tirar / cerrar", "Disparo gestual + ciclo de cerrojo" };

        private void DrawReloadWizard()
        {
            EditorGUILayout.Space();
            reloadWizard = EditorGUILayout.Foldout(reloadWizard, "Avanzado · generar mano + pieza (3 referencias)", true);
            if (!reloadWizard) return;
            var r = draft.reload ?? (draft.reload = new LabReloadRecipe());
            string[] ids = ObjectIds();
            int current = Array.IndexOf(ids, r.objectId) + 1;
            int next = EditorGUILayout.Popup("Pieza de la plantilla", current, new[] { "Selecciona pieza / diseño…" }.Concat(ids.Select(ObjectLabel)).ToArray());
            if (next > 0 && next != current) Safe(() =>
            {
                if (!CanLeaveFrame()) return;
                Undo.RecordObject(draft, "Cambiar pieza del asistente");
                r.objectId = ids[next - 1]; r.hasRest = r.hasContact = r.hasOutside = false; dirty = true;
                SyncSelection(); // CanLeaveFrame's Discard must not leave an adjustment to be committed later.
            });
            EditorGUI.BeginChangeCheck();
            var template = (LabReloadTemplate)EditorGUILayout.Popup("Ejemplo prefabricado", (int)r.template, ReloadNames);
            var hand = (HandSide)EditorGUILayout.Popup("Mano que manipula", (int)r.side, new[] { "Izquierda", "Derecha" });
            float seconds = EditorGUILayout.Slider("Duración del ejemplo (s)", r.duration, 0.3f, 30);
            int rounds = r.rounds;
            if (template == LabReloadTemplate.Revolver || template == LabReloadTemplate.SingleRound)
                rounds = EditorGUILayout.IntSlider("Repeticiones / cartuchos", rounds, 1, 12);
            Vector3 arc = EditorGUILayout.Vector3Field("Arco del recorrido (cm)", r.arc * 100) * 0.01f;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(draft, "Configurar plantilla");
                if (r.side != hand) r.hasContact = false;
                r.template = template; r.side = hand; r.duration = seconds; r.rounds = rounds; r.arc = arc; dirty = true;
            }
            EditorGUILayout.HelpBox("Coloca las referencias con los controles/arrastre de abajo y pulsa Capturar. Capturar conserva esa referencia, no añade un fotograma y termina el ajuste temporal. Puedes repetir los pasos antes de Generar.", MessageType.Info);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(r.objectId) || playing))
            {
                DrawReloadStep(0, r.hasRest, "Pieza en reposo / cartucho en la recámara");
                using (new EditorGUI.DisabledScope(!r.hasRest))
                {
                    DrawReloadStep(1, r.hasContact, "Muñeca en contacto con la pieza");
                    DrawReloadStep(2, r.hasOutside, "Pieza fuera / tambor abierto / cerrojo atrás");
                }
            }
            if (r.hasRest && r.hasContact && r.hasOutside)
            {
                EditorGUILayout.HelpBox("Referencias listas. Generar reemplaza TODA la posición, dedos e influencia IK de la mano elegida y la pista de esta pieza. Limita Blend In/Out al 15% de la duración para mantener contacto. Conserva las pistas de la otra mano y clips fuente; duración y fundidos afectan a toda la toma.", MessageType.Warning);
                if (GUILayout.Button("Generar ejemplo editable con dedos", GUILayout.Height(34))) Safe(GenerateReload);
            }
            EditorGUILayout.HelpBox("Plantillas visuales, no mecánica automática. Revólver: necesitas tambor separado; cartuchos: selecciona un cartucho/diseño de referencia que se reutiliza, sin crear ni ocultar munición. Disparo es gesto de índice sin proyectil/recoil automático. Coloca la muñeca y corrige dedos/contactos. No incluye modelos de revólver o sniper.", MessageType.None);
        }

        private void DrawReloadStep(int step, bool ready, string label)
        {
            EditorGUILayout.LabelField($"{step + 1}. {(ready ? "✓" : "○")} {label}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(step == 1 ? "Colocar mano" : "Colocar pieza")) Safe(() => PlaceReloadReference(step));
                bool selected = step == 1 ? reloadPlacement == 1 && !editObject && !editWeapon && !editFingers && side == draft.reload.side
                    : editObject && objectId == draft.reload.objectId;
                using (new EditorGUI.DisabledScope(!selected))
                    if (GUILayout.Button("Capturar " + (step + 1))) Safe(() => CaptureReloadReference(step));
            }
        }

        private void PlaceReloadReference(int step)
        {
            if (!CanLeaveFrame()) return;
            var r = draft.reload;
            if (step == 1)
            {
                SelectTrack((int)r.side * 2, 0); reloadPlacement = 1;
                EvaluateNow(false);
                if (r.hasContact && BeginPositionEdit())
                {
                    var target = LabReloadGenerator.Resolve(preview, r);
                    SetEditedWorldPose(target.TransformPoint(r.contactPosition), target.rotation * Quaternion.Euler(r.contactEuler));
                }
            }
            else
            {
                followHands = step == 2 ? (int)r.side + 1 : 0; SelectObject(r.objectId);
                if ((r.hasRest || step == 2) && BeginObjectEdit())
                {
                    keyPosition = step == 2 && r.hasOutside ? r.outsidePosition : r.restPosition;
                    keyRotation = step == 2 && r.hasOutside ? r.outsideEuler : r.restEuler;
                    if (step == 2 && r.hasContact) followContacts[r.side] = (r.contactPosition, Quaternion.Euler(r.contactEuler));
                }
            }
            notice = "Coloca " + (step == 1 ? "la muñeca en contacto" : step == 0 ? "la pieza en reposo" : "la pieza en el extremo del recorrido") + " y pulsa Capturar " + (step + 1) + ". No hace falta Guardar posición.";
        }

        private void CaptureReloadReference(int step)
        {
            var r = draft.reload;
            if (step == 1 && (reloadPlacement != 1 || editObject || editWeapon || editFingers || side != r.side))
                throw new InvalidOperationException("Pulsa Colocar mano en el paso 2 antes de capturar su contacto con la pieza en reposo.");
            if (step != 0 && !r.hasRest) throw new InvalidOperationException("Captura primero la pieza en reposo.");
            EvaluateNow(Pending);
            var target = LabReloadGenerator.Resolve(preview, r);
            if (target == null) throw new InvalidOperationException("Selecciona una pieza compatible.");
            Undo.RecordObject(draft, "Capturar referencia de recarga");
            if (step == 0)
            {
                r.restPosition = target.localPosition; r.restEuler = target.localEulerAngles;
                r.hasRest = true; r.hasContact = r.hasOutside = false;
            }
            else if (step == 1)
            {
                var chain = r.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                var wrist = chain.target != null ? chain.target : chain.hand;
                r.contactPosition = target.InverseTransformPoint(wrist.position);
                r.contactEuler = (Quaternion.Inverse(target.rotation) * wrist.rotation).eulerAngles; r.hasContact = true;
            }
            else
            {
                r.outsidePosition = target.localPosition; r.outsideEuler = target.localEulerAngles; r.hasOutside = true;
            }
            dirty = true; notice = "Referencia " + (step + 1) + " capturada. Se conserva en la toma y su recuperación.";
            SyncSelection();
        }

        private void GenerateReload()
        {
            if (!CanLeaveFrame()) return;
            if (!EditorUtility.DisplayDialog("Generar recarga de ejemplo", "Se sustituirán toda la pista de muñeca, dedos y curva IK de la mano elegida (IK al 100%), posición/giro de la pieza y tipo/duración. Blend In/Out se limitan al 15% de la duración para mantener contacto. Guarda una copia antes. Los clips fuente y las demás pistas no se borran, pero duración/fundidos las retiman globalmente.", "Generar", "Cancelar")) return;
            AnimationLabTake generated = null;
            try
            {
                generated = LabReloadGenerator.Generate(draft, preview);
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                Undo.RecordObject(draft, "Generar recarga"); Undo.RecordObject(draft.action, "Generar recarga");
                foreach (var pose in draft.left.Concat(draft.right).Select(k => k.fingers).Where(p => p != null)) retired.Add(pose);
                // Retain old poses for Undo and transfer ownership of the generated deep copies.
                draft.left = generated.left; draft.right = generated.right; draft.parts = generated.parts; draft.props = generated.props;
                foreach (var pose in draft.left.Concat(draft.right).Select(k => k.fingers).Where(p => p != null)) retired.Add(pose);
                draft.kind = generated.kind; EditorUtility.CopySerialized(generated.action, draft.action);
                generated.left = new System.Collections.Generic.List<LabHandKey>(); generated.right = new System.Collections.Generic.List<LabHandKey>();
                Undo.CollapseUndoOperations(group);
                progress = 0; playing = false; dirty = true;
                notice = "Ejemplo generado: pieza editable y muñeca muestreada a 60 Hz para acompañar el contacto. Si cambias las referencias o pieza, vuelve a Generar. Revisa dedos y primera persona.";
                SyncSelection();
            }
            finally { LabAssetIO.DestroyDraft(generated); }
        }
    }
}
