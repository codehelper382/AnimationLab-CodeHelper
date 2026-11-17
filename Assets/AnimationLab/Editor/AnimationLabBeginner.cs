using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private bool simpleMode = true, guidedHelp = true, guidedPlayed, guidedRhythm;
        private int guidedStep = 1;
        private void DrawModeChoice()
        {
            bool simple = GUILayout.Toolbar(simpleMode ? 0 : 1, new[] { "Sencillo · empezar", "Completo · todas las herramientas" }) == 0;
            if (simple == simpleMode) return;
            CancelFrameDrag(); CancelTransitionBlockDrag(); CancelViewportDrag();
            simpleMode = simple;
            EditorPrefs.SetBool("AnimationLab.SimpleMode", simpleMode);
        }
        private void DrawBeginnerGuide()
        {
            guidedHelp = EditorGUILayout.Foldout(guidedHelp, "Guía interactiva · elegir → poses → ritmo → probar → guardar", true);
            if (!guidedHelp) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int step = GUILayout.Toolbar(guidedStep, new[] { "Elegir", "Poses", "Ritmo", "Probar", "Guardar" });
                if (step != guidedStep) guidedStep = step;
                int count = Times(Track).Count();
                string[] advice =
                {
                    "En Proyecto elige equipo y una acción existente o una toma en blanco. Trabajas sobre una copia.",
                    $"Selecciona un fotograma, ajusta la pose y pulsa Guardar posición o Guardar dedos. Esta pista tiene {count} puntos. Necesitas dos para crear un tramo.",
                    "Selecciona el primero de dos puntos y abre Crear transición fácil. Ajusta mantener, viajar y asentar; Probar no cambia el borrador.",
                    "Reproduce el tramo y comprueba primera persona, mirada y Aim. Acepta la propuesta si te sirve o cancélala para volver a los ajustes.",
                    "Guardar copia conserva el borrador como un asset nuevo. Un punto confirmado aún no está guardado en disco."
                };
                EditorGUILayout.HelpBox(advice[Mathf.Clamp(guidedStep, 0, 4)], MessageType.Info);
                if (guidedStep == 0 && GUILayout.Button("Ir a elegir equipo / acción")) { if (CanLeaveFrame()) studioTab = 2; }
                if (guidedStep == 1 && GUILayout.Button("Ir a editar poses")) { if (CanLeaveFrame()) studioTab = 1; }
                if (guidedStep == 2 && GUILayout.Button("Configurar el tramo actual"))
                    Safe(() => { SelectTransitionSegment(); transitionAssistantOpen = true; guidedRhythm = transitionPlan != null; });
                if (guidedStep == 3 && GUILayout.Button(playing ? "Pausar" : "Reproducir para revisar")) Safe(TogglePlayback);
                if (guidedStep == 4)
                {
                    using (new EditorGUI.DisabledScope(HasTakeTrial))
                        if (GUILayout.Button("Guardar copia nueva")) Safe(SaveTake);
                }
                EditorGUILayout.LabelField($"Ritmo: {(guidedRhythm ? "configurado" : "pendiente")} · Reproducción: {(guidedPlayed ? "probada" : "pendiente")}", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("← Anterior")) guidedStep = Mathf.Max(0, guidedStep - 1);
                    if (GUILayout.Button("Siguiente →")) guidedStep = Mathf.Min(4, guidedStep + 1);
                }
            }
        }
        private void DrawChannelOrigin()
        {
            if (Track < 4)
            {
                var hand = Track < 2 ? HandSide.Left : HandSide.Right;
                var contact = LabCoordinatedPaths.Segments(draft).FirstOrDefault(s => s.side == hand);
                if (Track % 2 == 0 && contact != null)
                    EditorGUILayout.HelpBox("ENLAZADA A PIEZA · muñeca compilada. Mueve la pieza para conservar el contacto; los dedos son independientes.", MessageType.Info);
                else if (Times(Track).Count() > 60)
                    EditorGUILayout.LabelField("PISTA DENSA · puede proceder de una inserción. Regenerar una receta puede sustituir muestras editadas.", EditorStyles.wordWrappedMiniLabel);
                else EditorGUILayout.LabelField("PISTA EDITABLE · posición y dedos se guardan por separado.", EditorStyles.wordWrappedMiniLabel);
            }
            else if (Track == 5 && LabCoordinatedPaths.Segments(draft).Any(s => s.objectId == objectId))
                EditorGUILayout.LabelField("CONJUNTO · mover la pieza recompila las muñecas enlazadas.", EditorStyles.wordWrappedMiniLabel);
        }
    }
}
