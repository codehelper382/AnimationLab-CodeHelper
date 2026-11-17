using System;
using System.Collections.Generic;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private void DrawReloadReferencePicker()
        {
            var r=GestureSettings;
            var choices=new List<LabHandSource>();var names=new List<string>();
            if(LabReloadReference.Resolve(preview,LabHandSource.Reload)!=null){choices.Add(LabHandSource.Reload);names.Add("Recarga existente · normal");}
            if(LabReloadReference.Resolve(preview,LabHandSource.ReloadEmpty)!=null){choices.Add(LabHandSource.ReloadEmpty);names.Add("Recarga existente · vacía");}
            choices.Add(LabHandSource.Procedural);names.Add("Gesto procedural · orientativo");
            int current=choices.IndexOf(r.source),next=EditorGUILayout.Popup("Base del movimiento",Mathf.Max(0,current),names.ToArray());
            if(next!=current)Safe(()=>
            {
                if(!CanLeaveFrame())return;
                if(r.points.Count>0&&!EditorUtility.DisplayDialog("Cambiar base","Se quitarán los controles visuales de la base anterior. Las claves de la toma no cambian hasta Aceptar.","Cambiar base","Cancelar"))return;
                Undo.RecordObject(GestureOwner,"Elegir recarga existente");r.source=choices[next];r.points.Clear();r.phaseTimes=Array.Empty<float>();r.phaseGrip=Array.Empty<float>();r.hasContact=false;
                if(r.source!=LabHandSource.Procedural)r.template=LabHandTemplate.Rifle;
                GestureChanged();
            });
            if(r.source==LabHandSource.Procedural)return;
            var action=LabReloadReference.Resolve(preview,r.source);
            if(action==null){EditorGUILayout.HelpBox("La recarga seleccionada no existe en este equipo.",MessageType.Warning);return;}
            EditorGUILayout.HelpBox($"{preview.Slot.definition.displayName} · {action.name}\nLa prueba utiliza su muñeca y dedos originales, no un gesto inventado. Los puntos añaden correcciones sin sustituir la trayectoria de fondo.",MessageType.Info);
            using(new EditorGUI.DisabledScope(HasHandTrial))
            {
                if(GUILayout.Button($"Usar duración original · {action.duration:F2} s"))Safe(()=>SetDuration(action.duration));
                if(GUILayout.Button("Abrir recarga completa como copia",new GUIStyle(GUI.skin.button){wordWrap=true},GUILayout.MinWidth(0),GUILayout.Height(30)))
                    Safe(()=>{if(CanDiscard())LoadReloadReference(r.source);});
            }
        }
        private void LoadReloadReference(LabHandSource type)
        {
            if(HasTakeTrial)throw new InvalidOperationException("Acepta o cancela la prueba antes de abrir otra receta.");
            var original=LabReloadReference.Resolve(preview,type);
            if(original==null)throw new InvalidOperationException("No existe esa recarga en el equipo actual.");
            int slot=draft.slot;preview.RestoreAnchors();NewDraft(slot);
            DestroyImmediate(draft.action);draft.action=Instantiate(original);draft.action.hideFlags=HideFlags.HideAndDontSave;
            draft.hasSourceCharacterClip=true;draft.sourceCharacterClip=original.characterClip;draft.sourceCharacterMask=original.characterMask;
            draft.hasSourceWeaponClip=true;draft.sourceWeaponClip=original.weaponClip;draft.kind=EquipmentActionKind.Reload;
            draft.handInsert.source=type;draft.handInsert.template=LabHandTemplate.Rifle;
            takeName=preview.Slot.definition.displayName+"_"+(type==LabHandSource.ReloadEmpty?"RecargaVacia":"Recarga");
            dirty=true;progress=0;playing=false;SyncSelection();
            notice="Recarga existente abierta como copia. Se conservan los clips, dedos, objetivo y movimiento originales.";
        }
    }
}
