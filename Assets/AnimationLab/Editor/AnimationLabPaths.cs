using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private void DrawPathEditor()
        {
            if(DrawLinkedHandPath())return;
            if (!editObject && !editWeapon && editFingers) return;
            LabStudioUI.Section("Recorrido hasta el siguiente punto", "Control de trayectoria, salida y llegada · sin efectos de cámara ni recoil añadidos.");
            if(!SelectedSegment(out var objectKey,out var handKey,out Vector3 end,out float next))
            {
                EditorGUILayout.HelpBox("Necesitas dos puntos de posición, no solo de dedos. Puedes guardarlos o capturar los extremos del movimiento actual.", MessageType.Info);
                if(GUILayout.Button("Capturar inicio y final como dos puntos…")) Safe(()=>
                {
                    if(!CanLeaveFrame())return;
                    if(EditorUtility.DisplayDialog("Capturar extremos","Se reemplazarán solo las posiciones de esta pista por dos puntos (inicio/final) del movimiento actual. Los dedos, otras pistas y el clip original de personaje se conservan. Puedes deshacer.","Capturar dos puntos","Cancelar"))CapturePathEndpoints(true);
                });
                return;
            }
            var mode = objectKey != null ? objectKey.pathMode : handKey.pathMode;
            var bend = objectKey != null ? objectKey.bend : handKey.bend;
            float departure = objectKey != null ? objectKey.departureSpeed : handKey.departureSpeed;
            float arrival = objectKey != null ? objectKey.arrivalSpeed : handKey.arrivalSpeed;
            Vector3 outgoing=objectKey!=null?objectKey.outgoing:handKey.outgoing,incoming=objectKey!=null?objectKey.incoming:handKey.incoming;
            Vector3 units = LocalUnitScale;
            float from=objectKey!=null?objectKey.time:handKey.time;
            DrawCombinedPathControls(objectKey,next);
            EditorGUILayout.LabelField($"Tramo activo: F {LabTimeline.Frame(from,draft.action.duration)} → F {LabTimeline.Frame(next,draft.action.duration)}",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Ir al inicio"))Safe(()=>Seek(from));
                if(GUILayout.Button("Ir al final"))Safe(()=>Seek(next));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Directo")) Safe(()=>SetPathPreset(LabPathMode.Linear, Vector3.zero, 1, 1));
                if (GUILayout.Button("Arco suave")) Safe(()=>SetPathPreset(LabPathMode.Cinematic, bend.sqrMagnitude > 0 ? bend : new Vector3(0,.03f/Mathf.Max(.000001f,units.y),0), 0, 0));
                if (GUILayout.Button("Frenar")) Safe(()=>SetPathPreset(LabPathMode.Cinematic, Vector3.zero, 0, 0));
                if (GUILayout.Button("Continuo")) Safe(SetContinuousPath);
                if (GUILayout.Button("Libre")) Safe(()=>SetPathPreset(LabPathMode.Bezier, bend, 0, 0));
            }
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Forma del tramo", (int)mode,
                new[] { "Original (compatible)", "Recto · velocidad constante", "Curva suave · entre vecinos", "Arco · desvío editable", "Recto · acelerar / frenar", "Cinemático · arco con llegada suave", "Bézier · dos controles libres", "Fluido · Bézier con giro entre vecinos" });
            Vector3 offset = selected == (int)LabPathMode.Arc || selected == (int)LabPathMode.Cinematic
                ? EditorGUILayout.Vector3Field("Desvío central local (cm)", Vector3.Scale(bend, units) * 100) * 0.01f : Vector3.Scale(bend, units);
            if (selected == (int)LabPathMode.Cinematic||selected==(int)LabPathMode.Bezier||selected==(int)LabPathMode.Flow)
            {
                departure = EditorGUILayout.Slider("Salida · pausa ↔ continua", departure, 0, 1);
                arrival = EditorGUILayout.Slider("Llegada · frenar ↔ pasar", arrival, 0, 1);
            }
            if(selected==(int)LabPathMode.Bezier||selected==(int)LabPathMode.Flow)
            {
                if(mode!=(LabPathMode)selected&&outgoing==Vector3.zero&&incoming==Vector3.zero){var delta=end-(objectKey!=null?objectKey.position:handKey.position);outgoing=delta/3+bend;incoming=-delta/3+bend;}
                outgoing=DivideUnits(EditorGUILayout.Vector3Field("Control salida (cm)",Vector3.Scale(outgoing,units)*100)*.01f,units);
                incoming=DivideUnits(EditorGUILayout.Vector3Field("Control entrada (cm)",Vector3.Scale(incoming,units)*100)*.01f,units);
            }
            if (EditorGUI.EndChangeCheck()) Safe(() =>
            {
                var local = new Vector3(offset.x / units.x, offset.y / units.y, offset.z / units.z);
                ApplyPathSettings((LabPathMode)selected,local,departure,arrival,outgoing,incoming);
            });
            EditorGUILayout.HelpBox("Puedes editar desde cualquier fotograma del tramo, incluido el último punto. Libre permite curvas en S con controles independientes. Arrastra en el plano de la vista; Alt+arrastrar cambia profundidad. No cambia los extremos ni los dedos. Revisa el contacto al cambiar solo una pista.", MessageType.None);
        }

        private void SetPathPreset(LabPathMode mode, Vector3 bend, float departure, float arrival)
        {
            if(HasTakeTrial)throw new System.InvalidOperationException("Acepta o cancela la prueba antes de cambiar recorridos.");
            if (!CanLeaveFrame() || !SelectedSegment(out var obj,out var hand,out _,out _)) return;
            Vector3 outgoing=obj!=null?obj.outgoing:hand.outgoing,incoming=obj!=null?obj.incoming:hand.incoming;
            if(mode==LabPathMode.Bezier&&outgoing==Vector3.zero&&incoming==Vector3.zero&&SelectedSegment(out _,out _,out Vector3 end,out _))
            {var start=obj!=null?obj.position:hand.position;outgoing=(end-start)/3+bend;incoming=-(end-start)/3+bend;}
            ApplyPathSettings(mode,bend,departure,arrival,outgoing,incoming);
        }
        private void ApplyPathSettings(LabPathMode mode,Vector3 bend,float departure,float arrival,Vector3 outgoing,Vector3 incoming)
        {
            if(HasTakeTrial)throw new System.InvalidOperationException("Acepta o cancela la prueba antes de cambiar recorridos.");
            if(!CanLeaveFrame()||!SelectedSegment(out var obj,out var hand,out _,out _))return;
            if(!ApprovePathOwnership())return;
            Undo.RecordObject(draft,"Estilo de recorrido");Undo.RecordObject(draft.action,"Estilo de recorrido");
            RunAuthoringTransaction(() =>
            {
                LabTakeValidation.RequireFinite(bend,"El arco");LabTakeValidation.RequireFinite(outgoing,"La salida");LabTakeValidation.RequireFinite(incoming,"La entrada");
                if(obj!=null){obj.pathMode=mode;obj.bend=bend;obj.departureSpeed=departure;obj.arrivalSpeed=arrival;obj.outgoing=outgoing;obj.incoming=incoming;}
                else{hand.pathMode=mode;hand.bend=bend;hand.departureSpeed=departure;hand.arrivalSpeed=arrival;hand.outgoing=outgoing;hand.incoming=incoming;}
                RebuildSelectedPath();SyncSelection();
            });
        }
        private void SetContinuousPath()
        {
            if(HasTakeTrial)throw new System.InvalidOperationException("Acepta o cancela la prueba antes de cambiar recorridos.");
            if(!CanLeaveFrame()||!SelectedSegment(out var obj,out var hand,out _,out float end))return;
            var times=LabFrameEditing.Times(draft,Track,objectId).OrderBy(t=>t).ToArray();
            if(!times.Any(t=>t>end+LabTimeline.TimeEpsilon))throw new System.InvalidOperationException("Continuar necesita un punto posterior a la llegada.");
            if(!ApprovePathOwnership())return;
            Undo.RecordObject(draft,"Continuidad entre tramos");Undo.RecordObject(draft.action,"Continuidad entre tramos");
            RunAuthoringTransaction(() =>
            {
                if(obj!=null)
                {
                    var next=(editWeapon?draft.weapon:ObjectKeys()).First(k=>LabFrameEditing.Same(k.time,end));
                    obj.pathMode=next.pathMode=LabPathMode.Smooth;obj.bend=next.bend=Vector3.zero;
                }
                else
                {
                    var next=LabTimeline.FindWrist(HandKeys,end);
                    hand.pathMode=next.pathMode=LabPathMode.Smooth;hand.bend=next.bend=Vector3.zero;
                }
                RebuildSelectedPath();SyncSelection();
                notice="Continuidad aplicada a este tramo y al siguiente; sus poses se conservan.";
            });
        }
        private void RunAuthoringTransaction(System.Action change)
        {
            bool wasDirty=dirty,wasPending=coordinatedPathPending;
            using(var transaction=new LabAuthoringTransaction(draft))
            {
                try{change();LabTakeValidation.RequireValid(draft);transaction.Commit();dirty=true;}
                catch{dirty=wasDirty;coordinatedPathPending=wasPending;throw;}
            }
        }
        private bool ApprovePathOwnership()
        {
            if(HasTakeTrial)return false;
            bool mixed=!editObject&&!editWeapon&&(HandMotion.motionClip!=null||LabTimeline.HasMotion(HandMotion.reach)||(side==HandGripToolkit.HandSide.Left&&draft.action.leftHandTargetMotion!=null));
            if(!mixed||wristReplacementApproved)return true;
            if(!EditorUtility.DisplayDialog("El movimiento todavía depende de un clip","Editar esta trayectoria reconstruye la posición de la muñeca desde sus puntos y desactiva su Motion Clip/Reach/objetivo anterior. Los dedos y el clip de personaje se conservan. Guarda una copia si quieres mantener también la receta previa.","Usar mis puntos","Cancelar"))return false;
            wristReplacementApproved=true;return true;
        }
        private static Vector3 DivideUnits(Vector3 value,Vector3 scale)=>new Vector3(value.x/Mathf.Max(.000001f,scale.x),value.y/Mathf.Max(.000001f,scale.y),value.z/Mathf.Max(.000001f,scale.z));
    }
}
