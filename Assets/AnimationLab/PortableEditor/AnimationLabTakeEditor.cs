#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    [CustomEditor(typeof(AnimationLabTake))]
    public sealed class AnimationLabTakeEditor : UnityEditor.Editor
    {
        private EquipmentSceneController destination;

        public override void OnInspectorGUI()
        {
            var take = (AnimationLabTake)target;
            EditorGUILayout.HelpBox("La acción es compatible con EquipmentActionDefinition. Los agarres son calibración separada: aplícalos explícitamente al equipo de la escena, fuera de Play.", MessageType.Info);
            destination = (EquipmentSceneController)EditorGUILayout.ObjectField("Equipo de destino", destination, typeof(EquipmentSceneController), true);
            using (new EditorGUI.DisabledScope(destination == null || Application.isPlaying))
                if (GUILayout.Button("Aplicar agarres a la escena (Undo)"))
                    if (EditorUtility.DisplayDialog("Aplicar calibración", "Solo cambiará los puntos del equipo con equipmentId '" + take.equipmentId + "'. No asigna ni reemplaza acciones. Puedes deshacer.", "Aplicar", "Cancelar"))
                    {
                        try { Apply(take, destination); }
                        catch (Exception ex) { EditorUtility.DisplayDialog("No se pudo aplicar", ex.Message, "Aceptar"); }
                    }
            EditorGUILayout.Space(); DrawDefaultInspector();
        }

        public static void Apply(AnimationLabTake take, EquipmentSceneController destination)
        {
            if (take == null || destination == null || Application.isPlaying || !destination.gameObject.scene.IsValid() || EditorUtility.IsPersistent(destination))
                throw new InvalidOperationException("Selecciona un equipo de una escena, fuera de Play.");
            var slots = destination.slots.Where(s => s?.definition != null && s.definition.equipmentId == take.equipmentId).ToArray();
            if (slots.Length != 1) throw new InvalidOperationException("El equipmentId no identifica exactamente un slot de destino.");
            var slot = slots[0];
            Transform actorRoot = destination.look != null && destination.look.bodyRoot != null ? destination.look.bodyRoot : destination.transform;
            var all = actorRoot.GetComponentsInChildren<Transform>(true);
            var byPath = all.GroupBy(Path).ToDictionary(g => g.Key, g => g.ToArray());
            var resolved = new Dictionary<LabAnchor, Transform>();
            var bySourcePath = new Dictionary<string, Transform>();
            var missing = new List<LabAnchor>();
            foreach (var anchor in take.anchors)
            {
                var bound = Role(slot, anchor.role);
                if (bound != null)
                {
                    if (!bound.IsChildOf(actorRoot)) throw new InvalidOperationException("El punto de destino pertenece a otro personaje: " + anchor.role);
                    ValidateSafe(bound, destination); resolved[anchor] = bound;
                    if (!bySourcePath.ContainsKey(anchor.path)) bySourcePath[anchor.path] = bound;
                    continue;
                }
                if (bySourcePath.TryGetValue(anchor.path, out var shared)) { ValidateSafe(shared, destination); resolved[anchor] = shared; continue; }
                if (byPath.TryGetValue(anchor.path, out var matches) && matches.Length == 1)
                {
                    ValidateSafe(matches[0], destination);
                    resolved[anchor] = matches[0]; bySourcePath[anchor.path] = matches[0];
                }
                else
                {
                    if (anchor.role != "leftGuard" && anchor.role != "rightGuard") throw new InvalidOperationException("Punto no encontrado/ambiguo: " + anchor.path);
                    int slash = anchor.path.LastIndexOf('/');
                    if (slash < 0 || !byPath.TryGetValue(anchor.path.Substring(0, slash), out var parents) || parents.Length != 1)
                        throw new InvalidOperationException("Falta la raíz para crear " + anchor.path);
                    ValidateSafe(parents[0], destination);
                    missing.Add(anchor);
                }
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Aplicar agarres de Animation Lab");
            try
            {
                Undo.RecordObject(destination, "Enlazar guardias");
                foreach (var anchor in missing.GroupBy(a => a.path).Select(g => g.First()))
                {
                    int slash = anchor.path.LastIndexOf('/');
                    var go = new GameObject(anchor.path.Substring(slash + 1));
                    Undo.RegisterCreatedObjectUndo(go, "Crear guardia");
                    go.transform.SetParent(byPath[anchor.path.Substring(0, slash)][0], false);
                    foreach (var item in missing.Where(a => a.path == anchor.path)) resolved[item] = go.transform;
                }
                foreach (var anchor in take.anchors)
                {
                    Transform point = resolved[anchor];
                    Undo.RecordObject(point, "Calibrar punto"); point.SetLocalPositionAndRotation(anchor.position, anchor.rotation);
                    if (anchor.role == "leftGuard") { if (slot.leftHandPose == null) slot.leftHandPose = new EquipmentHandBinding(); slot.leftHandPose.guard = point; slot.leftHandPose.enabled = true; }
                    if (anchor.role == "rightGuard") { if (slot.rightHandPose == null) slot.rightHandPose = new EquipmentHandBinding(); slot.rightHandPose.guard = point; slot.rightHandPose.enabled = true; }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(point);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
                EditorSceneManager.MarkSceneDirty(destination.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static string Path(Transform t)
        { string path = t.name; while (t.parent != null) { t = t.parent; path = t.name + "/" + path; } return path; }

        private static Transform Role(EquipmentSceneSlot s, string role)
        {
            switch (role)
            {
                case "leftGrip": return s.leftGrip; case "rightGrip": return s.rightGrip;
                case "leftHint": return s.leftHint; case "rightHint": return s.rightHint;
                case "leftReloadTarget": return s.leftReloadTarget;
                case "leftGuard": return s.leftHandPose?.guard; case "rightGuard": return s.rightHandPose?.guard;
                case "leftAction": return s.leftHandPose?.actionTarget; case "rightAction": return s.rightHandPose?.actionTarget;
                case "leftElbow": return s.leftHandPose?.elbowHint; case "rightElbow": return s.rightHandPose?.elbowHint;
                default: throw new InvalidOperationException("Rol de punto desconocido: " + role);
            }
        }

        private static void ValidateSafe(Transform point, EquipmentSceneController equipment)
        {
            if (equipment.handIK == null) throw new InvalidOperationException("El equipo no tiene HandIKSolver.");
            foreach (var arm in new[] { equipment.handIK.leftHand.upperArm, equipment.handIK.rightHand.upperArm })
                if (arm != null && point.IsChildOf(arm)) throw new InvalidOperationException("Un agarre no puede pertenecer a la cadena del brazo.");
        }
    }
}
#endif
