using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Data checks only. Never evaluate a graph or change an authored value to "repair" it.
    internal static class LabTakeValidation
    {
        internal sealed class Problem
        {
            internal string message, objectId;
            internal int? track;
            internal float? time;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        internal static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
            && q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w > .00000001f;
        internal static void RequireFinite(Vector3 value, string label)
        { if (!Finite(value)) throw new InvalidOperationException(label + " debe contener números finitos."); }
        internal static List<Problem> Inspect(AnimationLabTake take, bool duplicates = true)
        {
            var result = new List<Problem>();
            void Add(string message, int? track = null, string id = null, float? t = null) =>
                result.Add(new Problem { message=message,track=track,objectId=id,
                    time=t.HasValue&&Finite(t.Value)&&t.Value>=0&&t.Value<=1?t:null });
            if(take==null||take.action==null){Add("Falta la receta de acción.");return result;}
            var action=take.action;
            if(!Finite(action.duration)||action.duration<=0)Add("La duración debe ser finita y mayor que cero.");
            if(!Finite(action.blendIn)||!Finite(action.blendOut)||action.blendIn<0||action.blendOut<0)Add("Los fundidos deben ser finitos y no negativos.");
            if(!Finite(action.commitTime)||action.commitTime<0||action.commitTime>1)Add("El hito de juego debe estar dentro de [0,1].");
            bool Curve(AnimationCurve curve) => curve!=null&&curve.keys.Any(k=>!Finite(k.time)||k.time<0||k.time>1||!Finite(k.value)
                ||float.IsNaN(k.inTangent)||float.IsNaN(k.outTangent)||!Finite(k.inWeight)||!Finite(k.outWeight));
            bool VectorCurves(EquipmentVectorCurves c) => c!=null&&(Curve(c.x)||Curve(c.y)||Curve(c.z));
            void Motion(EquipmentHandMotion m,int track)
            {
                if(m!=null&&(Curve(m.reach)||VectorCurves(m.position)||VectorCurves(m.rotation)))Add("Curvas de muñeca con datos no válidos.",track);
            }
            Motion(action.leftHandMotion,0);Motion(action.rightHandMotion,2);
            if(VectorCurves(action.weaponPosition)||VectorCurves(action.weaponRotation))Add("Curvas de arma con datos no válidos.",4);
            if(new[]{action.leftIK,action.rightIK,action.leftGrip,action.rightGrip}.Any(Curve))Add("Pesos de manos con datos no válidos.");
            void Times(IEnumerable<float> values,int track,string id)
            {
                var times=values.OrderBy(t=>t).ToArray();
                foreach(float t in times)if(!Finite(t)||t<0||t>1)Add("Tiempo de punto fuera de [0,1] o no finito.",track,id);
                if(duplicates)for(int i=1;i<times.Length;i++)if(Mathf.Abs(times[i]-times[i-1])<LabTimeline.TimeEpsilon)
                    Add("Hay dos puntos del mismo canal en el mismo tiempo. Revisa los duplicados antes de aplicar.",track,id,times[i]);
            }
            void Hand(List<LabHandKey> keys,int track)
            {
                if(keys==null){Add("Falta la lista de puntos de una mano.",track);return;}
                foreach(var key in keys)
                {
                    if(key==null){Add("Hay una entrada de mano vacía.",track);continue;}
                    if(!key.fingersOnly&&(!Finite(key.position)||!Finite(key.euler)||!Finite(key.bend)||!Finite(key.outgoing)||!Finite(key.incoming)
                        ||!Finite(key.departureSpeed)||!Finite(key.arrivalSpeed)))Add("Posición, giro o controles de muñeca no válidos.",track,null,key.time);
                    if(key.fingers==null)continue;
                    if(!Finite(key.fingerBlendDuration)||key.fingerBlendDuration<0)
                        Add("Duración de dedos no válida.",track+1,null,key.time);
                    if(key.fingers.BoneCount>0&&key.fingers.Side!=(track==0?HandSide.Left:HandSide.Right))Add("El agarre pertenece a la otra mano.",track+1,null,key.time);
                    for(int f=0;f<5;f++)for(int j=0;j<3;j++)
                    {var bone=key.fingers.GetEntry((FingerId)f,j);if(bone!=null&&!Finite(bone.localRotation))Add("Una articulación de dedos no tiene un giro válido.",track+1,null,key.time);}
                }
                Times(keys.Where(k=>k!=null&&!k.fingersOnly).Select(k=>k.time),track,null);
                Times(keys.Where(k=>k!=null&&k.fingers!=null).Select(k=>k.time),track+1,null);
            }
            Hand(take.left,0);Hand(take.right,2);
            void Objects(List<LabWeaponKey> keys,int track,string id)
            {
                if(keys==null){Add("Falta la lista de puntos del objeto.",track,id);return;}
                foreach(var k in keys)
                {
                    if(k==null){Add("Hay una entrada de objeto vacía.",track,id);continue;}
                    if(!Finite(k.position)||!Finite(k.euler)||!Finite(k.bend)||!Finite(k.outgoing)||!Finite(k.incoming)
                        ||!Finite(k.departureSpeed)||!Finite(k.arrivalSpeed))Add("Posición, giro o controles de objeto no válidos.",track,id,k.time);
                    foreach(var c in new[]{k.leftContact,k.rightContact})
                        if(c!=null&&c.active&&(!Finite(c.position)||!Finite(c.euler)))Add("El contacto contiene datos no válidos.",track,id,k.time);
                }
                Times(keys.Where(k=>k!=null).Select(k=>k.time),track,id);
            }
            Objects(take.weapon,4,null);
            if(take.parts==null||take.props==null){Add("Falta una lista de piezas o diseños.");return result;}
            foreach(var p in take.parts)
                if(p==null||string.IsNullOrEmpty(p.path))Add("Pieza sin ruta.");else Objects(p.keys,5,"part:"+p.path);
            var ids=new HashSet<string>();
            foreach(var p in take.props)
            {
                if(p==null){Add("Diseño vacío.");continue;}
                if(string.IsNullOrEmpty(p.id)||!ids.Add(p.id))Add("Diseño sin identidad o con identidad duplicada.",5);
                if(!Finite(p.position)||!Finite(p.euler))Add("Pose base de diseño no válida.",5,"prop:"+p.id);
                Objects(p.keys,5,"prop:"+p.id);
            }
            if(take.anchors!=null)foreach(var anchor in take.anchors)
                if(anchor==null||!Finite(anchor.position)||!Finite(anchor.rotation))Add("La calibración contiene una pose no válida.");
            if(take.guidedReload!=null&&take.guidedReload.configured)
                foreach(var issue in LabGuidedReloadValidation.Inspect(take.guidedReload)
                    .Where(i=>i.severity==LabGuidedIssueSeverity.Error))
                    Add("Recarga guiada: "+issue.message,null,issue.itemId);
            return result;
        }
        internal static void RequireValid(AnimationLabTake take)
        {
            var errors=Inspect(take);
            if(errors.Count>0)throw new InvalidOperationException(errors[0].message+" Abre Revisar para localizarlo.");
        }
    }
}
