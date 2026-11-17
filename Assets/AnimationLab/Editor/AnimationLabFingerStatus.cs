using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private void DrawSavedFingerStatus()
        {
            var poses=HandKeys.Where(k=>k.fingers!=null).OrderBy(k=>k.time).ToArray();
            var exact=poses.FirstOrDefault(k=>Mathf.Abs(k.time-progress)<LabTimeline.TimeEpsilon);
            var before=poses.LastOrDefault(k=>k.time<=progress);var after=poses.FirstOrDefault(k=>k.time>progress+LabTimeline.TimeEpsilon);
            if(fingersEdited)EditorGUILayout.HelpBox("DESTINO SIN GUARDAR · "+fingerPoseLabel+". Elegir un tipo cambia la propuesta, no las claves guardadas.",MessageType.Info);
            else if(exact!=null)
                EditorGUILayout.HelpBox($"CLAVE GUARDADA · F {CurrentFrame} · {(!string.IsNullOrEmpty(exact.fingerLabel)?exact.fingerLabel:"Pose guardada (sin etiqueta de tipo)")}\nEl modo de llegada no es el tipo de agarre.",MessageType.None);
            else if(before!=null)
            {
                EditorGUILayout.HelpBox(after!=null?
                    $"SIN CLAVE NUEVA EN F {CurrentFrame} · se evalúa el tramo F {LabTimeline.Frame(before.time,draft.action.duration)} → F {LabTimeline.Frame(after.time,draft.action.duration)}.\nNo se cambia automáticamente ningún modo guardado.":
                    $"SIN CLAVE NUEVA EN F {CurrentFrame} · se mantiene la última pose, guardada en F {LabTimeline.Frame(before.time,draft.action.duration)}.",MessageType.None);
                if(GUILayout.Button($"Ir al agarre guardado de F {LabTimeline.Frame(before.time,draft.action.duration)}"))Safe(()=>Seek(before.time));
            }
            if(after!=null&&before!=null)EditorGUILayout.LabelField($"Siguiente pose de dedos: F {LabTimeline.Frame(after.time,draft.action.duration)} · revisar si no deseas otro cambio",EditorStyles.wordWrappedMiniLabel);
            var initial=poses.FirstOrDefault(k=>Mathf.Abs(k.time)<LabTimeline.TimeEpsilon);
            if(initial!=null&&exact!=null&&CurrentFrame>0&&!fingersEdited)
            {
                float difference=LabGripTools.PoseDifference(initial.fingers,exact.fingers);
                EditorGUILayout.LabelField(difference<.2f?"Esta pose coincide con el agarre guardado en F0.":$"Pose distinta de F0 · diferencia máxima de articulación {difference:F1}°.",EditorStyles.wordWrappedMiniLabel);
                if(difference>=.2f&&GUILayout.Button("Volver al agarre guardado en F0"))Safe(()=>
                {SetFingerPose(Instantiate(initial.fingers));fingerPoseLabel="Base guardada en F0";});
            }
            bool hasFuture=poses.Any(k=>k.time>progress+LabTimeline.TimeEpsilon);
            if(exact!=null||hasFuture)
                using(new EditorGUI.DisabledScope(HasTakeTrial||CurrentFrame<=0))
                    if(GUILayout.Button(exact!=null?"Mantener este agarre hasta el final…":$"Guardar este agarre en F {CurrentFrame} y mantenerlo hasta el final…"))Safe(()=>HoldFingerGripToEnd(false));
        }
        private void DrawHandPieceContactStatus()
        {
            var segments=LabCoordinatedPaths.Segments(draft).Where(s=>s.side==side).OrderBy(s=>s.from.time).ToArray();
            if(segments.Length==0)return;
            var last=segments.Last();var keys=TakeObjectKeys(draft,last.objectId);
            bool later=keys!=null&&keys.Any(k=>k.time>last.to.time+LabTimeline.TimeEpsilon);
            string fullLabel=ObjectLabel(last.objectId);int separator=fullLabel.IndexOf(" · ",StringComparison.Ordinal);
            string label=separator>0?fullLabel.Substring(0,separator):fullLabel;
            int from=LabTimeline.Frame(last.from.time,draft.action.duration),to=LabTimeline.Frame(last.to.time,draft.action.duration);
            float hold=LabCoordinatedPaths.ContactHoldTime(draft);int holdFrame=LabTimeline.Frame(hold,draft.action.duration);
            string holdLabel=draft.action.blendOut>0?$"F {holdFrame} (inicio del fundido)":"el final";
            bool reachesFade=last.to.time>=hold-LabTimeline.TimeEpsilon;
            bool active=progress>=last.from.time-LabTimeline.TimeEpsilon&&progress<=last.to.time+LabTimeline.TimeEpsilon;
            if(active)
            {
                bool atLast=progress>=last.to.time-LabTimeline.TimeEpsilon;
                string detail=reachesFade?"Llega hasta el inicio del fundido; los últimos fotogramas se atenúan con él.":
                    later?"La muñeca sigue la pieza.":
                    atLast?"Último punto: después la muñeca deja de seguir la pieza.":
                    $"La muñeca sigue la pieza; el último punto está en F {to}.";
                EditorGUILayout.HelpBox($"Contacto con {label} · F {from} → F {to}. {detail}",reachesFade||later||!atLast?MessageType.None:MessageType.Warning);
            }
            else if(!later&&!reachesFade&&last.to.time<1f-LabTimeline.TimeEpsilon&&progress>=last.to.time-LabTimeline.TimeEpsilon)
                EditorGUILayout.HelpBox($"El contacto con {label} terminó en F {to}. Después la muñeca vuelve a la guardia; los dedos son otra pista y el fundido global también atenúa el final.",MessageType.Warning);
            if(!later&&!reachesFade&&progress>=last.to.time-LabTimeline.TimeEpsilon)
                using(new EditorGUI.DisabledScope(HasTakeTrial))
                    if(GUILayout.Button($"Mantener mano y pieza hasta {holdLabel}…"))Safe(()=>KeepContactToHold(last.side,last.objectId,false));
            if(!later&&reachesFade)
                EditorGUILayout.LabelField(draft.action.blendOut>0?$"La mano sigue la pieza hasta el inicio del fundido (F {holdFrame}); después el fundido global atenúa el final.":"La mano y la pieza siguen juntas hasta el final de la toma.",EditorStyles.wordWrappedMiniLabel);
        }
        private void KeepContactToHold(HandSide hand,string id,bool confirmed)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de mantener el contacto.");
            if(!CanLeaveFrame())return;
            var last=LabCoordinatedPaths.Segments(draft).Where(s=>s.side==hand&&s.objectId==id).OrderBy(s=>s.to.time).LastOrDefault();
            if(last==null)throw new InvalidOperationException("No hay un tramo de contacto activo para esa mano y pieza.");
            var keys=TakeObjectKeys(draft,id);
            if(keys==null)throw new InvalidOperationException("La pista de la pieza ya no existe.");
            if(keys.Any(k=>k.time>last.to.time+LabTimeline.TimeEpsilon))throw new InvalidOperationException("Ya hay puntos posteriores de la pieza; edita ese tramo en vez de prolongar uno antiguo.");
            float hold=LabCoordinatedPaths.ContactHoldTime(draft);
            if(hold<=last.to.time+LabTimeline.TimeEpsilon)throw new InvalidOperationException("El contacto ya llega hasta el inicio del fundido de salida.");
            int holdFrame=LabTimeline.Frame(hold,draft.action.duration);
            if(!confirmed&&!EditorUtility.DisplayDialog("Mantener mano y pieza",
                $"Se añadirá un punto de {ObjectLabel(id)} en F {holdFrame} con el último contacto y se recalculará la muñeca {(hand==HandSide.Left?"izquierda":"derecha")} a 60 Hz hasta ahí. Los dedos, el arma y la otra mano no cambian.{(draft.action.blendOut>0?" El fundido global de salida sigue atenuando los últimos fotogramas.":"")} Puedes deshacer.","Mantener contacto","Cancelar"))return;
            var candidate=LabAssetIO.Clone(draft);
            try
            {
                var track=TakeObjectKeys(candidate,id,true);
                var source=track.FirstOrDefault(k=>Mathf.Abs(k.time-last.to.time)<LabTimeline.TimeEpsilon);
                if(source==null)throw new InvalidOperationException("No se encontró el punto de contacto original.");
                var end=JsonUtility.FromJson<LabWeaponKey>(JsonUtility.ToJson(source));
                end.time=hold;end.pathMode=LabPathMode.Linear;end.bend=end.outgoing=end.incoming=Vector3.zero;end.departureSpeed=end.arrivalSpeed=0;end.breakLeftContact=end.breakRightContact=false;
                track.Add(end);track.Sort((a,b)=>a.time.CompareTo(b.time));
                LabCoordinatedPaths.Compile(candidate,preview);
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
                Undo.RecordObject(draft,"Mantener contacto mano-pieza");Undo.RecordObject(draft.action,"Mantener contacto mano-pieza");
                var destination=TakeObjectKeys(draft,id,true);destination.Clear();destination.AddRange(LabObjectAuthoring.CloneKeys(track));
                ApplyCandidateHands(candidate,new[]{hand});
                Undo.CollapseUndoOperations(group);dirty=true;SyncSelection();
                notice=$"Contacto con {ObjectLabel(id)} mantenido hasta F {holdFrame}. La muñeca se recalculó; el fundido de salida sigue atenuando el final.";
            }
            finally{LabAssetIO.DestroyDraft(candidate);EvaluateNow(Pending);}
        }
        private void HoldFingerGripToEnd(bool confirmed)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de mantener un agarre.");
            int future=HandKeys.Count(k=>k.fingers!=null&&k.time>progress+LabTimeline.TimeEpsilon);
            if(!confirmed&&!EditorUtility.DisplayDialog("Mantener agarre",
                $"Se guardará la pose actual en F {CurrentFrame} y se quitarán {future} poses posteriores SOLO de dedos de esta mano. Las posiciones de muñeca, piezas y otra mano no cambian. La salida global de la acción sigue activa. Puedes deshacer.","Mantener hasta el final","Cancelar"))return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            if(!CommitEdits(true,false))return;
            Undo.RecordObject(draft,"Mantener agarre hasta el final");
            foreach(var key in HandKeys.Where(k=>k.time>progress+LabTimeline.TimeEpsilon&&k.fingers!=null).ToArray())
            {
                retired.Add(key.fingers);key.fingers=null;key.fingerLabel=null;
                if(key.fingersOnly)HandKeys.Remove(key);
            }
            Undo.CollapseUndoOperations(group);dirty=true;
            notice=$"Agarre mantenido desde F {CurrentFrame}. Se quitaron {future} poses futuras de dedos; el recorrido de muñeca no cambia.";
            SyncSelection();
        }
    }
}
