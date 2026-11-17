using System;
using System.Linq;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private void DrawGesturePointInspector()
        {
            var r=GestureSettings;
            LabStudioUI.Section("Dibuja el gesto", "Puntos en la vista · arrastra para mover · Ctrl+clic para añadir en el fotograma actual.");
            if(r.points.Count<2)
            {
                if(LabStudioUI.Primary("＋ Crear puntos visuales")) Safe(CreateGesturePoints);
                if(r.source!=LabHandSource.Procedural)
                {EditorGUILayout.HelpBox("Puedes probar directamente la recarga original. Crear puntos permite corregirla sin reemplazar su movimiento de base.",MessageType.None);return;}
                using(new EditorGUILayout.HorizontalScope())
                {
                    using(new EditorGUI.DisabledScope(HasHandTrial))
                        if(GUILayout.Button("Colocar mano")) Safe(()=>{studioTab=1; SelectTrack((int)r.side*2,Mathf.Lerp(r.start,r.end,.25f));});
                    using(new EditorGUI.DisabledScope(HasHandTrial || editObject || editWeapon || editFingers || side!=r.side))
                        if(GUILayout.Button("Usar pose como contacto")) Safe(CaptureHandTemplateContact);
                }
                EditorGUI.BeginChangeCheck();
                Vector3 travel=EditorGUILayout.Vector3Field("Viaje en ejes del personaje (cm)",r.travel*100)*.01f;
                float arc=EditorGUILayout.Slider("Arco libre (cm)",r.arc*100,0,8)*.01f;
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(GestureOwner,"Cambiar viaje");r.travel=travel;r.arc=arc;GestureChanged();}
                return;
            }
            selectedGesturePoint=EditorGUILayout.Popup("Punto seleccionado",Mathf.Clamp(selectedGesturePoint,0,r.points.Count-1),r.points.Select((p,i)=>$"{i+1} · {p.label}").ToArray());
            var point=r.points[selectedGesturePoint];
            var guard=preview.Guard(r.side); Vector3 scale=guard!=null?guard.lossyScale:Vector3.one;
            scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            EditorGUI.BeginChangeCheck();
            string label=EditorGUILayout.TextField("Nombre",point.label);
            Vector3 pos=EditorGUILayout.Vector3Field("Muñeca local (cm)",GestureDisplayVector(Vector3.Scale(LabGestureAuthoring.Effective(r,GesturePointPosition(point)),scale)*100))*.01f;
            Vector3 euler=EditorGUILayout.Vector3Field("Orientación de muñeca",GestureDisplayVector(GesturePointEuler(point)));
            Vector3 bend=EditorGUILayout.Vector3Field("Arco base al siguiente (cm)",GestureDisplayVector(Vector3.Scale(point.bend,scale)*100))*.01f;
            float close=EditorGUILayout.Slider("Dedos: −1 plantilla / 0 abrir / 1 cerrar",point.grip,-1,1);
            var mode=(LabPathMode)EditorGUILayout.EnumPopup("Salida hacia el siguiente",point.mode);
            float departure=point.departureSpeed,arrival=point.arrivalSpeed;
            if(mode==LabPathMode.Cinematic||mode==LabPathMode.Bezier||mode==LabPathMode.Flow){departure=EditorGUILayout.Slider("Velocidad al salir",departure,0,1);arrival=EditorGUILayout.Slider("Velocidad al llegar",arrival,0,1);}
            Vector3 outgoing=point.outgoing,incoming=point.incoming;
            if(mode==LabPathMode.Bezier||mode==LabPathMode.Flow)
            {
                var next=r.points.Where(p=>p.time>point.time).OrderBy(p=>p.time).FirstOrDefault();
                if(mode!=point.mode&&next!=null&&outgoing==Vector3.zero&&incoming==Vector3.zero)
                {var delta=r.source==LabHandSource.Procedural?next.position-point.position:(next.position-next.referencePosition)-(point.position-point.referencePosition);outgoing=delta/3;incoming=-delta/3;}
                outgoing=DivideUnits(EditorGUILayout.Vector3Field("Control salida (cm)",Vector3.Scale(outgoing,scale)*100)*.01f,scale);
                incoming=DivideUnits(EditorGUILayout.Vector3Field("Control entrada (cm)",Vector3.Scale(incoming,scale)*100)*.01f,scale);
            }
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(GestureOwner,"Editar punto de gesto"); point.label=label;
                var desired=new Vector3(pos.x/Mathf.Max(.000001f,scale.x),pos.y/Mathf.Max(.000001f,scale.y),pos.z/Mathf.Max(.000001f,scale.z));
                point.position=GestureEditedPosition(point,desired);
                point.bend=new Vector3(bend.x/Mathf.Max(.000001f,scale.x),bend.y/Mathf.Max(.000001f,scale.y),bend.z/Mathf.Max(.000001f,scale.z));
                point.euler=GestureStoredEuler(point,euler);point.grip=close;point.mode=mode;point.departureSpeed=departure;point.arrivalSpeed=arrival;point.outgoing=outgoing;point.incoming=incoming;GestureChanged();
            }
            if(!point.contactAnchor&&GUILayout.Button("Usar este punto como ancla de contacto"))
            {Undo.RecordObject(GestureOwner,"Elegir ancla de contacto");foreach(var p in r.points)p.contactAnchor=false;point.contactAnchor=true;GestureChanged();}
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Ir al punto")) Safe(()=>Seek(Mathf.Lerp(r.start,r.end,LabGestureAuthoring.Unmap(r,point.time))));
                if(GUILayout.Button("＋ En este fotograma")) Safe(AddGesturePointAtTime);
                using(new EditorGUI.DisabledScope(r.points.Count<=2)) if(GUILayout.Button("Quitar"))
                {Undo.RecordObject(GestureOwner,"Quitar punto de gesto");r.points.RemoveAt(selectedGesturePoint);selectedGesturePoint=Mathf.Clamp(selectedGesturePoint,0,r.points.Count-1);GestureChanged();}
            }
            EditorGUILayout.HelpBox("Los ejes dibujados son los de la muñeca, no una normal universal de palma. Contacto y recorrido son locales a la guardia: no son un constraint sobre una pieza móvil.",MessageType.None);
            if(GUILayout.Button("Restablecer recorrido de la plantilla…")) Safe(()=>
            {if(EditorUtility.DisplayDialog("Restablecer recorrido","Se reemplazan los puntos visuales, no las claves de la toma hasta Aceptar.","Restablecer","Cancelar")) CreateGesturePoints();});
        }

        private void DrawGestureArtControls()
        {
            var r=GestureSettings; LabStudioUI.Section("Dirección artística","Sin temblores de contacto. Ajusta energía, tamaño y recuperación.");
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Suave")) ApplyGestureMood(.25f,.8f,.9f);
                if(GUILayout.Button("Táctica")) ApplyGestureMood(.65f,.65f,1);
                if(GUILayout.Button("Enérgica")) ApplyGestureMood(.85f,.3f,1.1f);
            }
            EditorGUI.BeginChangeCheck();
            float reach=EditorGUILayout.Slider("Compacta ↔ amplia",r.reachScale,.4f,1.5f);
            float energy=EditorGUILayout.Slider("Suave ↔ enérgica",r.energy,0,1);
            float recover=EditorGUILayout.Slider("Recuperación amortiguada",r.recovery,0,1);
            float curvature=EditorGUILayout.Slider("Curvatura del viaje",r.curvature,0,2);
            var phaseBounds=LabGestureAuthoring.Bounds(r);float intervalFrames=(r.end-r.start)*draft.action.duration*60;
            float pause=EditorGUILayout.Slider("Pausa de asentamiento (F)",r.settle*intervalFrames,0,Mathf.Max(0,Mathf.Min(12,(phaseBounds[5]-phaseBounds[4])*intervalFrames*.65f)));
            float thumb=EditorGUILayout.Slider("Cierre del pulgar",r.thumb,0,1);
            int preset=EditorGUILayout.Popup("Forma inicial de dedos",r.gripPreset+1,new[]{"Según plantilla"}.Concat(LabGripTools.Names).ToArray())-1;
            if(EditorGUI.EndChangeCheck())
            {Undo.RecordObject(GestureOwner,"Ajustar estilo");r.useStyle=true;r.reachScale=reach;r.energy=energy;r.recovery=recover;r.curvature=curvature;r.settle=pause/Mathf.Max(.000001f,intervalFrames);r.thumb=thumb;r.gripPreset=preset;GestureChanged();}
            LabStudioUI.Section("Fase seleccionada","También puedes arrastrar las divisiones de color en la línea de tiempo.");
            selectedPhase=EditorGUILayout.Popup("Fase",selectedPhase,LabGestureAuthoring.PhaseNames);
            var bounds=LabGestureAuthoring.Bounds(r);
            int end=LabTimeline.Frame(Mathf.Lerp(r.start,r.end,bounds[selectedPhase+1]),draft.action.duration);
            using(new EditorGUI.DisabledScope(selectedPhase==5))
            {
                int next=EditorGUILayout.IntField("Fin de fase (F)",end);
                if(next!=end && selectedPhase<5)
                {
                    Undo.RecordObject(GestureOwner,"Duración de fase"); r.phaseTimes=(float[])bounds.Clone();
                    r.phaseTimes[selectedPhase+1]=Mathf.Clamp(Mathf.InverseLerp(r.start,r.end,LabTimeline.Time(next,draft.action.duration)),bounds[selectedPhase]+.005f,bounds[selectedPhase+2]-.005f);GestureChanged();
                }
            }
            bool own=r.phaseGrip!=null && r.phaseGrip.Length==6 && r.phaseGrip[selectedPhase]>=0;
            bool use=EditorGUILayout.Toggle("Dedos propios de esta fase",own);
            if(use!=own){Undo.RecordObject(GestureOwner,"Dedos de fase");if(r.phaseGrip==null||r.phaseGrip.Length!=6)r.phaseGrip=Enumerable.Repeat(-1f,6).ToArray();r.phaseGrip[selectedPhase]=use ? .7f : -1;GestureChanged();}
            if(use)
            {
                float value=EditorGUILayout.Slider("Abrir ↔ agarrar",r.phaseGrip[selectedPhase],0,1);
                if(value!=r.phaseGrip[selectedPhase]){Undo.RecordObject(GestureOwner,"Cerrar dedos de fase");r.phaseGrip[selectedPhase]=value;GestureChanged();}
            }
            if(GUILayout.Button("Restaurar ajustes de esta fase"))
            {Undo.RecordObject(GestureOwner,"Restaurar fase");LabGestureAuthoring.RestorePhase(r,selectedPhase);GestureChanged();}
        }
        private void ApplyGestureMood(float energy,float recovery,float reach)
        {Undo.RecordObject(GestureOwner,"Aplicar estilo");var r=GestureSettings;r.useStyle=true;r.energy=energy;r.recovery=recovery;r.reachScale=reach;GestureChanged();}
        private static Vector3 GestureDisplayVector(Vector3 value) => new Vector3(Mathf.Round(value.x*1000)/1000,Mathf.Round(value.y*1000)/1000,Mathf.Round(value.z*1000)/1000);

        private void DrawGestureVariants()
        {
            LabStudioUI.Section("Biblioteca de variantes","Guarda la receta de puntos y estilo; no una escena ni una animación de gameplay.");
            variantName=EditorGUILayout.TextField("Nombre",variantName);
            if(GUILayout.Button("Guardar variante…")) Safe(()=>
            {
                string path=EditorUtility.SaveFilePanelInProject("Guardar receta de manos",variantName,"asset","Conserva puntos locales y estilo para este equipo.",LabAssetIO.Output);
                if(string.IsNullOrEmpty(path))return;
                var asset=CreateInstance<LabHandVariant>();asset.settings=LabGestureAuthoring.Copy(GestureSettings);asset.description=variantName;asset.equipmentId=draft.equipmentId;
                try{AssetDatabase.CreateAsset(asset,AssetDatabase.GenerateUniqueAssetPath(path));EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();}
                catch{if(!AssetDatabase.Contains(asset))DestroyImmediate(asset);throw;}
            });
            var load=(LabHandVariant)EditorGUILayout.ObjectField("Cargar variante",null,typeof(LabHandVariant),false);
            if(load!=null) Safe(()=>
            {
                if(load.equipmentId!=draft.equipmentId)throw new InvalidOperationException("Los puntos locales pertenecen a otro equipo. Usa una variante de este equipmentId.");
                var old=GestureSettings;var settings=LabGestureAuthoring.Copy(load.settings);settings.start=old.start;settings.end=old.end;
                Undo.RecordObject(GestureOwner,"Cargar variante");GestureOwner.handInsert=settings;GestureChanged();
            });
            EditorGUILayout.HelpBox("Las variantes conservan la mano y sus puntos, por lo que se exige el mismo equipo. El intervalo actual no cambia al cargarlas. Revisa avatar y contacto antes de aceptar.",MessageType.None);
        }
    }
}
