using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Inserts a gesture into one hand. No equipment type, duration, fades, pieces or weapon channels are changed.
    internal static class LabHandTemplateInsertion
    {
        private sealed class Sample
        {
            internal float time, weight, influence;
            internal Vector3 position;
            internal Quaternion rotation;
            internal HandPoseData fingers;
            internal LabHandKey reference;
        }

        internal static AnimationLabTake Insert(AnimationLabTake source, LabPreview preview)
        {
            var r = source.handInsert;
            if (r == null || !Finite(r.start) || !Finite(r.end) || r.start < 0 || r.end > 1 || r.end <= r.start ||
                source.action.duration <= 0 || source.action.duration > 30 || !Finite(source.action.duration))
                throw new InvalidOperationException("Elige un tramo válido dentro de una toma de hasta 30 segundos.");
            if ((r.end - r.start) * source.action.duration * 60 < 6 - 0.0001f)
                throw new InvalidOperationException("Elige al menos 6 fotogramas para insertar un gesto; para un ritmo legible usa bastantes más.");
            if (!Finite(r.arc) || !Valid(r.travel) || !Valid(r.contactPosition) || !Valid(r.contactEuler))
                throw new InvalidOperationException("Los parámetros de recorrido y contacto deben ser números finitos.");
            if (!LabGestureAuthoring.Valid(r)) throw new InvalidOperationException("Revisa el orden de fases y puntos, sus tiempos y los parámetros artísticos.");
            if (source.action.followAnimatedRightHand && r.side == HandSide.Right)
                throw new InvalidOperationException("Esta acción mueve el arma desde la muñeca derecha animada. Desactiva explícitamente Follow Animated Right Hand antes de insertar solo manos, o elige la izquierda.");
            var oldLeft = preview.Slot.leftHandPose; var oldRight = preview.Slot.rightHandPose;
            var take = LabAssetIO.Clone(source); var raw = LabAssetIO.Clone(source);
            AnimationLabTake reference=null;
            var samples = new List<Sample>();
            HandPoseData open = null, closed = null;
            try
            {
                if (source.left.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Left); else preview.RestoreHandBinding(HandSide.Left);
                if (source.right.Any(k => !k.fingersOnly)) preview.EnableHand(HandSide.Right); else preview.RestoreHandBinding(HandSide.Right);
                int count = Mathf.CeilToInt(source.action.duration * 60);
                var times = new SortedSet<float>(Enumerable.Range(0, count + 1).Select(i => (float)i / count));
                times.Add(r.start); times.Add(r.end);
                foreach (float boundary in LabGestureAuthoring.Bounds(r)) times.Add(Mathf.Lerp(r.start, r.end, boundary));
                foreach (var point in r.points) times.Add(Mathf.Lerp(r.start, r.end, LabGestureAuthoring.Unmap(r, point.time)));
                var originals = r.side == HandSide.Left ? source.left : source.right;
                foreach (var key in originals) times.Add(key.time);
                // First capture the original, actually blended target and effective IK, before changing ownership.
                foreach (float t in LabTimeline.SampleTimes(times, originals.Select(k=>k.time).Concat(new[]{r.start,r.end})))
                {
                    preview.Evaluate(source, t);
                    var chain = Chain(preview, r.side); var wrist = chain.target != null ? chain.target : chain.hand;
                    samples.Add(new Sample { time = t, position = wrist.position, rotation = wrist.rotation, weight = chain.weight,
                        influence = t <= r.start || t >= r.end ? 0 : r.source==LabHandSource.Procedural?LabHandTemplateLibrary.Influence(r.template, LabGestureAuthoring.Map(r, Mathf.InverseLerp(r.start, r.end, t))):
                            Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.06f,Mathf.InverseLerp(r.start,r.end,t)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,1,Mathf.InverseLerp(r.start,r.end,t)))) });
                }
                raw.action.blendIn = raw.action.blendOut = 0;
                // Raw finger poses avoid baking action fades into a clip that will be faded again at runtime.
                foreach (var sample in samples)
                {
                    preview.Evaluate(raw, sample.time, rawFingers: true);
                    sample.fingers = ScriptableObject.CreateInstance<HandPoseData>(); sample.fingers.Side = r.side;
                    sample.fingers.hideFlags = HideFlags.HideAndDontSave;
                    if (!sample.fingers.Capture(preview.Animator)) throw new InvalidOperationException("No hay dedos Humanoid en esta mano.");
                }
                if(r.source!=LabHandSource.Procedural)
                {
                    reference=LabReloadReference.Create(source,preview);
                    foreach(var sample in samples.Where(s=>s.influence>0))
                        sample.reference=LabReloadReference.Sample(reference,preview,LabGestureAuthoring.Map(r,Mathf.InverseLerp(r.start,r.end,sample.time)),r.side,true);
                }
                if(source.left.Any(k=>!k.fingersOnly))preview.EnableHand(HandSide.Left);else preview.RestoreHandBinding(HandSide.Left);
                if(source.right.Any(k=>!k.fingersOnly))preview.EnableHand(HandSide.Right);else preview.RestoreHandBinding(HandSide.Right);
                preview.EnableHand(r.side);
                var binding = r.side == HandSide.Left ? preview.Slot.leftHandPose : preview.Slot.rightHandPose;
                var selected = Chain(preview, r.side);
                if (binding.weight < 1 || preview.Equipment.handIK.alpha < 1 || selected.positionWeight < 1 || selected.rotationWeight < 1)
                    throw new InvalidOperationException("Esta plantilla necesita binding/solver IK al 100%. No se modificarán esos componentes automáticamente.");
                open = LabGripTools.Open(preview.Animator, r.side);
                closed = LabGripTools.Preset(preview.Animator, r.side,
                    r.gripPreset >= 0 ? r.gripPreset : r.template == LabHandTemplate.Pistol || r.template == LabHandTemplate.SubmachineGun || r.template == LabHandTemplate.Rifle ? 2 : 3);
                var keys = r.side == HandSide.Left ? take.left : take.right;
                foreach (var pose in keys.Select(k => k.fingers).Where(p => p != null).Distinct()) UnityEngine.Object.DestroyImmediate(pose);
                keys.Clear();
                var motion = r.side == HandSide.Left ? take.action.leftHandMotion : take.action.rightHandMotion;
                motion.enabled = true; motion.motionClip = null; motion.reach = AnimationCurve.Constant(0, 1, 0);
                motion.position = new EquipmentVectorCurves(); motion.rotation = new EquipmentVectorCurves();
                if (r.side == HandSide.Left) take.action.leftHandTargetMotion = null;
                var ik = Linear(samples.Select(s => new Keyframe(s.time, Mathf.Lerp(s.weight, 1, s.influence))).ToArray());
                if (r.side == HandSide.Left) take.action.leftIK = ik; else take.action.rightIK = ik;
                var contactSample = samples.OrderBy(s => Mathf.Abs(s.time - Mathf.Lerp(r.start, r.end, 0.25f))).First();
                preview.Evaluate(take, contactSample.time);
                var guard = preview.Guard(r.side);
                Vector3 contact = r.hasContact ? r.contactPosition : guard.InverseTransformPoint(contactSample.position);
                Quaternion contactRotation = r.hasContact ? Quaternion.Euler(r.contactEuler) : Quaternion.Inverse(guard.rotation) * contactSample.rotation;
                var pointKeys = LabGestureAuthoring.PointKeys(r);
                var pointPosition = LabPathCurves.Objects(pointKeys); var pointRotation = LabPathCurves.Objects(pointKeys, true);
                var corrections=r.source==LabHandSource.Procedural?new List<LabWeaponKey>():LabReloadReference.Corrections(r);
                var correctionPosition=LabPathCurves.Objects(corrections);var correctionRotation=LabPathCurves.Objects(corrections,true);
                Vector3 lastEuler = Vector3.zero;
                foreach (var sample in samples)
                {
                    preview.Evaluate(take, sample.time);
                    guard = preview.Guard(r.side);
                    float t = LabGestureAuthoring.Map(r, Mathf.InverseLerp(r.start, r.end, sample.time));
                    LabHandTemplateLibrary.Gesture(r.template, t, r.repetitions, out float travel, out Vector3 turn, out float grip);
                    grip = LabGestureAuthoring.Grip(r, t, grip);
                    var body = preview.Equipment.look.bodyRoot;
                    Vector3 displacement = body.TransformDirection(r.travel * travel * (r.useStyle ? r.reachScale : 1));
                    // Small secondary arc only during free travel; never add it at either contact.
                    if (r.template != LabHandTemplate.SniperBolt && r.template != LabHandTemplate.Bow)
                        displacement += body.up * (r.arc * Mathf.Sin(travel * Mathf.PI));
                    Vector3 targetPosition = pointKeys.Count >= 2 ? guard.TransformPoint(LabGestureAuthoring.Effective(r,pointPosition.Evaluate(t))) : guard.TransformPoint(contact) + displacement;
                    Quaternion targetRotation = pointKeys.Count >= 2 ? guard.rotation * Quaternion.Euler(pointRotation.Evaluate(t)) : guard.rotation * contactRotation * Quaternion.Euler(turn);
                    if(sample.reference!=null)
                    {
                        Vector3 localReference=sample.reference.position+correctionPosition.Evaluate(t);
                        targetPosition=guard.TransformPoint(pointKeys.Count>=2?LabGestureAuthoring.Effective(r,localReference):localReference);
                        targetRotation=guard.rotation*Quaternion.Euler(correctionRotation.Evaluate(t))*Quaternion.Euler(sample.reference.euler);
                    }
                    Vector3 goal = Vector3.Lerp(sample.position, targetPosition, sample.influence);
                    Quaternion rotation = Quaternion.Slerp(sample.rotation, targetRotation, sample.influence);
                    float weight = EquipmentSceneController.ActionWeight(take.action, sample.time);
                    Vector3 local = guard.InverseTransformPoint(goal);
                    Quaternion delta = Quaternion.Inverse(guard.rotation) * rotation;
                    if (weight < 0.00001f)
                    {
                        if (sample.weight > 0.001f && (local.magnitude > 0.001f || Quaternion.Angle(delta, Quaternion.identity) > 0.1f))
                            throw new InvalidOperationException("La guardia original no coincide con el target durante un fundido de peso cero. Revisa su calibración antes de convertir esta mano a puntos.");
                        local = Vector3.zero; delta = Quaternion.identity;
                    }
                    else
                    {
                        local /= weight;
                        delta.ToAngleAxis(out float angle, out Vector3 axis); if (angle > 180) angle -= 360;
                        if (Mathf.Abs(angle) / weight > 180.01f)
                            throw new InvalidOperationException("El giro de esta mano no puede conservarse con el fundido actual. Elige un tramo sin ese fundido o corrige la guardia.");
                        delta = Mathf.Abs(angle) < 0.00001f ? Quaternion.identity : Quaternion.AngleAxis(angle / weight, axis);
                    }
                    var euler = LabTimeline.UnwrapEuler(delta.eulerAngles, lastEuler); lastEuler = euler;
                    if (sample.influence > 0)
                    {
                        if(sample.reference!=null)BlendReferenceFingers(sample.fingers,sample.reference.fingers,sample.influence);
                        float ownFingers=r.source==LabHandSource.Procedural?1:LabGestureAuthoring.FingerOverrideWeight(r,t);
                        if(ownFingers>0)BlendFingers(sample.fingers, open, closed, grip, sample.influence*ownFingers, r.useStyle ? Mathf.Lerp(.5f, 1.5f, Mathf.Clamp01(r.thumb)) : 1);
                        if(ownFingers<1&&r.useStyle&&Mathf.Abs(r.thumb-.5f)>.00001f)
                        {
                            var target=r.thumb>.5f?closed:open;
                            for(int joint=0;joint<3;joint++){var a=sample.fingers.GetEntry(FingerId.Thumb,joint);var b=target.GetEntry(FingerId.Thumb,joint);if(a!=null&&b!=null)a.localRotation=Quaternion.Slerp(a.localRotation,b.localRotation,Mathf.Abs(r.thumb-.5f)*2*sample.influence*(1-ownFingers));}
                        }
                        LabAssetIO.NormalizeFingerPose(sample.fingers, preview);
                    }
                    keys.Add(new LabHandKey { time = sample.time, position = local, euler = euler, pathMode = LabPathMode.Linear, fingers = sample.fingers });
                    sample.fingers = null; // Ownership transferred to the take.
                }
                LabAssetIO.BuildMotion(keys, motion);
                return take;
            }
            catch { LabAssetIO.DestroyDraft(take); throw; }
            finally
            {
                preview.Slot.leftHandPose = oldLeft; preview.Slot.rightHandPose = oldRight;
                foreach (var sample in samples) if (sample.fingers != null) UnityEngine.Object.DestroyImmediate(sample.fingers);
                foreach(var sample in samples)if(sample.reference?.fingers!=null)UnityEngine.Object.DestroyImmediate(sample.reference.fingers);
                if (open != null) UnityEngine.Object.DestroyImmediate(open); if (closed != null) UnityEngine.Object.DestroyImmediate(closed);
                LabAssetIO.DestroyDraft(raw);
                LabAssetIO.DestroyDraft(reference);
            }
        }

        private static global::CodeHelper.ProceduralShooterFramework.Animation.HandIKChain Chain(LabPreview p, HandSide s) => s == HandSide.Left ? p.Equipment.handIK.leftHand : p.Equipment.handIK.rightHand;
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static bool Valid(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static AnimationCurve Linear(Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            { UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Linear); UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Linear); }
            return curve;
        }
        private static void BlendFingers(HandPoseData pose, HandPoseData open, HandPoseData closed, float grip, float weight, float thumb)
        {
            for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++)
            {
                var entry = pose.GetEntry((FingerId)f, j); var a = open.GetEntry((FingerId)f, j); var b = closed.GetEntry((FingerId)f, j);
                if (entry != null && a != null && b != null)
                    entry.localRotation = Quaternion.Slerp(entry.localRotation, Quaternion.Slerp(a.localRotation, b.localRotation, Mathf.Clamp01(grip * (f == 0 ? thumb : 1))), weight);
            }
        }
        private static void BlendReferenceFingers(HandPoseData target,HandPoseData reference,float weight)
        {
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)
            {var a=target.GetEntry((FingerId)f,j);var b=reference.GetEntry((FingerId)f,j);if(a!=null&&b!=null)a.localRotation=Quaternion.Slerp(a.localRotation,b.localRotation,weight);}
        }
    }
}
