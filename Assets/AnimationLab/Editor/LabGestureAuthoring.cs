using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabGestureAuthoring
    {
        internal static readonly string[] PhaseNames = { "Preparar", "Agarrar", "Extraer / viajar", "Insertar / volver", "Soltar", "Recuperar" };
        internal static LabHandInsert Copy(LabHandInsert r) => JsonUtility.FromJson<LabHandInsert>(JsonUtility.ToJson(r));
        internal static float[] Defaults(LabHandTemplate template)
        {
            switch (template)
            {
                case LabHandTemplate.Pistol: return new[] { 0f, 20/80f, 25/80f, 40/80f, 60/80f, 70/80f, 1 };
                case LabHandTemplate.SubmachineGun: return new[] { 0f, 20/90f, 25/90f, 42/90f, 65/90f, 78/90f, 1 };
                case LabHandTemplate.Rifle: return new[] { 0f, .2f, .28f, .5f, .75f, .9f, 1 };
                case LabHandTemplate.SniperBolt: return new[] { 0f, .18f, .26f, .4f, .55f, .64f, 1 };
                case LabHandTemplate.Bow: return new[] { 0f, 20/120f, 35/120f, 50/120f, 85/120f, 100/120f, 1 };
                case LabHandTemplate.Revolver: return new[] { 0f, 20/120f, .25f, .5f, .75f, 105/120f, 1 };
                default: return new[] { 0f, 25/120f, 35/120f, .5f, .75f, 105/120f, 1 };
            }
        }
        internal static float[] Bounds(LabHandInsert r) => r.phaseTimes != null && r.phaseTimes.Length == 7 ? r.phaseTimes : Defaults(r.template);
        internal static bool Valid(LabHandInsert r)
        {
            var times = Bounds(r);
            return Enum.IsDefined(typeof(LabHandTemplate),r.template) && Enum.IsDefined(typeof(LabHandSource),r.source) && (r.phaseTimes==null || r.phaseTimes.Length==0 || r.phaseTimes.Length==7) &&
                (r.phaseGrip==null || r.phaseGrip.Length==0 || r.phaseGrip.Length==6 && r.phaseGrip.All(Finite)) &&
                times[0] == 0 && times[6] == 1 && times.All(Finite) && Enumerable.Range(0, 6).All(i => times[i + 1] - times[i] >= 0.0049f) &&
                r.points != null && r.points.All(p => Finite(p.time) && p.time >= 0 && p.time <= 1 && Vector(p.position) && Vector(p.euler) && Vector(p.bend) && Vector(p.outgoing) && Vector(p.incoming) && Vector(p.referencePosition) && Vector(p.referenceEuler) && Finite(p.grip) && Finite(p.departureSpeed) && Finite(p.arrivalSpeed) && Enum.IsDefined(typeof(LabPathMode),p.mode)) &&
                r.points.Select(p => p.time).Distinct().Count() == r.points.Count && Finite(r.reachScale) && r.reachScale > 0 && r.reachScale <= 2 && Finite(r.energy) && Finite(r.recovery) && Finite(r.thumb) && Finite(r.curvature) && Finite(r.settle) && r.settle>=0 && r.settle<=1;
        }
        internal static int Phase(float[] bounds, float u)
        { for (int i = 0; i < 5; i++) if (u < bounds[i + 1]) return i; return 5; }

        internal static float Map(LabHandInsert r, float u)
        {
            var b = Bounds(r); var original = Defaults(r.template); int i = Phase(b, u);
            float t = Mathf.InverseLerp(b[i], b[i + 1], u);
            if (r.useStyle)
            {
                if(i==4&&r.settle>0)t=Mathf.InverseLerp(Mathf.Clamp(r.settle/Mathf.Max(.000001f,b[i+1]-b[i]),0,.8f),1,t);
                float energy = i == 5 ? 1 - Mathf.Clamp01(r.recovery) : Mathf.Clamp01(r.energy);
                float exponent = Mathf.Pow(2, (.5f - energy) * 1.5f);
                float a = Mathf.Pow(t, exponent), z = Mathf.Pow(1 - t, exponent);
                t = a / Mathf.Max(0.000001f, a + z);
            }
            return Mathf.Lerp(original[i], original[i + 1], t);
        }
        internal static float Unmap(LabHandInsert r, float canonical)
        {
            // Monotone inverse also includes the artistic timing warp.
            float a = 0, b = 1;
            for (int i = 0; i < 24; i++) { float m = (a + b) * .5f; if (Map(r, m) < canonical) a = m; else b = m; }
            return (a + b) * .5f;
        }
        internal static List<LabWeaponKey> PointKeys(LabHandInsert r) => r.points.OrderBy(p => p.time).Select(p => new LabWeaponKey
        { time = p.time, position = p.position, euler = p.euler, bend = p.bend * (r.useStyle ? Mathf.Clamp(r.curvature,0,2) : 1), pathMode = p.mode, departureSpeed = p.departureSpeed, arrivalSpeed = p.arrivalSpeed,outgoing=p.outgoing,incoming=p.incoming }).ToList();

        internal static Vector3 Contact(LabHandInsert r)
        {var anchor=r.points.FirstOrDefault(p=>p.contactAnchor);return anchor!=null?anchor.position:r.hasContact?r.contactPosition:r.points.Count>0?r.points[0].position:Vector3.zero;}
        internal static Vector3 Effective(LabHandInsert r,Vector3 position) => Contact(r)+(position-Contact(r))*(r.useStyle?r.reachScale:1);
        internal static Vector3 Uneffective(LabHandInsert r,Vector3 position) => Contact(r)+(position-Contact(r))/Mathf.Max(.000001f,r.useStyle?r.reachScale:1);

        internal static float Grip(LabHandInsert r, float canonical, float original)
        {
            int phase = Phase(Defaults(r.template), canonical);
            if (r.phaseGrip != null && r.phaseGrip.Length == 6 && r.phaseGrip[phase] >= 0)
            {
                var bounds=Defaults(r.template);float t=Mathf.InverseLerp(bounds[phase],bounds[phase+1],canonical);
                float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.15f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-t)/.15f));
                original = Mathf.Lerp(original,Mathf.Clamp01(r.phaseGrip[phase]),fade);
            }
            var points = r.points.OrderBy(p => p.time).ToArray();
            if (points.Length == 0) return original;
            var before = points.LastOrDefault(p => p.time <= canonical); var after = points.FirstOrDefault(p => p.time >= canonical);
            if (before == null || after == null) return original;
            return Mathf.Lerp(before.grip>=0?before.grip:original, after.grip>=0?after.grip:original, Mathf.SmoothStep(0,1,Mathf.InverseLerp(before.time, after.time, canonical)));
        }
        internal static float FingerOverrideWeight(LabHandInsert r,float canonical)
        {
            if(r.gripPreset>=0)return 1;
            int phase=Phase(Defaults(r.template),canonical);float weight=0;
            if(r.phaseGrip!=null&&r.phaseGrip.Length==6&&r.phaseGrip[phase]>=0)
            {
                var b=Defaults(r.template);float t=Mathf.InverseLerp(b[phase],b[phase+1],canonical);
                weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.15f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-t)/.15f));
            }
            var points=r.points.OrderBy(p=>p.time).ToArray();var before=points.LastOrDefault(p=>p.time<=canonical);var after=points.FirstOrDefault(p=>p.time>=canonical);
            if(before!=null&&after!=null)weight=Mathf.Max(weight,Mathf.Lerp(before.grip>=0?1:0,after.grip>=0?1:0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(before.time,after.time,canonical))));
            return weight;
        }

        internal static List<LabGesturePoint> CreatePoints(AnimationLabTake take, LabPreview preview)
        {
            if(take.handInsert.source!=LabHandSource.Procedural)return LabReloadReference.CreatePoints(take,preview);
            var r = take.handInsert; var result = new List<LabGesturePoint>();
            var left = preview.Slot.leftHandPose; var right = preview.Slot.rightHandPose;
            try
            {
                if(take.left.Any(k=>!k.fingersOnly))preview.EnableHand(HandGripToolkit.HandSide.Left);else preview.RestoreHandBinding(HandGripToolkit.HandSide.Left);
                if(take.right.Any(k=>!k.fingersOnly))preview.EnableHand(HandGripToolkit.HandSide.Right);else preview.RestoreHandBinding(HandGripToolkit.HandSide.Right);
                preview.Evaluate(take, Mathf.Lerp(r.start, r.end, .25f)); preview.EnableHand(r.side);
                var chain = r.side == HandGripToolkit.HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                var guard = preview.Guard(r.side); var wrist = chain.target != null ? chain.target : chain.hand;
                Vector3 contact = r.hasContact ? r.contactPosition : guard.InverseTransformPoint(wrist.position);
                Vector3 angles = r.hasContact ? r.contactEuler : (Quaternion.Inverse(guard.rotation) * wrist.rotation).eulerAngles;
                var b = Defaults(r.template);var times=new System.Collections.Generic.SortedSet<float>(b);
                bool repeated=r.template==LabHandTemplate.SingleRound||r.template==LabHandTemplate.Revolver||r.template==LabHandTemplate.Shotgun;
                if(repeated)
                {
                    float start=r.template==LabHandTemplate.Revolver?30/120f:35/120f,end=90/120f;
                    times.RemoveWhere(t=>t>start+.000001f&&t<end-.000001f);
                    int repeats=Mathf.Clamp(r.repetitions,1,8);
                    for(int n=0;n<repeats;n++){times.Add(Mathf.Lerp(start,end,n/(float)repeats));times.Add(Mathf.Lerp(start,end,(n+.45f)/repeats));times.Add(Mathf.Lerp(start,end,(n+1f)/repeats));}
                }
                string[] labels = { "Inicio", "Aproximación", "Contacto", "Suministro / extremo", "Inserción / anclaje", "Liberar", "Regreso" };
                Vector3 previousEuler=angles;
                foreach(float time in times)
                {
                    preview.Evaluate(take, Mathf.Lerp(r.start, r.end, Unmap(r, time))); guard = preview.Guard(r.side);
                    LabHandTemplateLibrary.Gesture(r.template, time, r.repetitions, out float travel, out Vector3 turn, out _);
                    var body = preview.Equipment.look.bodyRoot;
                    var delta = body.TransformDirection(r.travel * travel);
                    if (r.template != LabHandTemplate.SniperBolt && r.template != LabHandTemplate.Bow) delta += body.up * r.arc * Mathf.Sin(travel * Mathf.PI);
                    int i=Array.FindIndex(b,t=>Mathf.Abs(t-time)<.000001f);
                    string label=i>=0?labels[i]:travel>.9f?"Suministro / cartucho":"Contacto / cartucho";
                    Vector3 euler=LabTimeline.UnwrapEuler((Quaternion.Euler(angles)*Quaternion.Euler(turn)).eulerAngles,previousEuler);previousEuler=euler;
                    result.Add(new LabGesturePoint { label = label, time = time, position = contact + guard.InverseTransformVector(delta), euler = euler, contactAnchor=Mathf.Abs(time-b[2])<.000001f });
                }
                for(int i=0;i+1<result.Count;i++)
                {
                    LabHandTemplateLibrary.Gesture(r.template,result[i].time,r.repetitions,out float a,out _,out _);
                    LabHandTemplateLibrary.Gesture(r.template,result[i+1].time,r.repetitions,out float z,out _,out _);
                    if(Mathf.Abs(a-z)>.5f&&r.template!=LabHandTemplate.SniperBolt&&r.template!=LabHandTemplate.Bow)
                        result[i].bend=guard.InverseTransformVector(preview.Equipment.look.bodyRoot.up*r.arc);
                }
                return result;
            }
            finally { preview.Slot.leftHandPose = left; preview.Slot.rightHandPose = right; }
        }

        internal static void RestorePhase(LabHandInsert r, int phase)
        {
            var original = Defaults(r.template); var bounds = (float[])Bounds(r).Clone();
            int index = Mathf.Clamp(phase + 1, 1, 5);
            bounds[index] = Mathf.Clamp(original[index], bounds[index - 1] + .005f, bounds[index + 1] - .005f);
            r.phaseTimes = bounds;
            if (r.phaseGrip != null && r.phaseGrip.Length == 6) r.phaseGrip[Mathf.Clamp(phase, 0, 5)] = -1;
            r.points.RemoveAll(p => p.time > original[phase] + .00001f && p.time < original[phase + 1] - .00001f);
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Vector(Vector3 x) => Finite(x.x) && Finite(x.y) && Finite(x.z);
    }
}
