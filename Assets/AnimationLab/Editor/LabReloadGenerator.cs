using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabReloadGenerator
    {
        internal static Transform Resolve(LabPreview preview, LabReloadRecipe recipe) => recipe.objectId != null && recipe.objectId.StartsWith("part:")
            ? preview.Objects.ResolvePart(recipe.objectId.Substring(5)) : recipe.objectId != null && recipe.objectId.StartsWith("prop:")
            ? preview.Objects.ResolveProp(recipe.objectId.Substring(5)) : null;

        internal static List<LabWeaponKey> Keys(AnimationLabTake take, LabReloadRecipe recipe)
        {
            if (recipe.objectId.StartsWith("prop:"))
                return take.props.Single(p => "prop:" + p.id == recipe.objectId).keys;
            string path = recipe.objectId.Substring(5);
            var track = take.parts.FirstOrDefault(p => p.path == path);
            if (track == null) { track = new LabObjectTrack { path = path }; take.parts.Add(track); }
            return track.keys;
        }

        // Transactional: caller keeps the old draft until every curve and finger pose is ready.
        internal static AnimationLabTake Generate(AnimationLabTake source, LabPreview preview)
        {
            var r = source.reload;
            if (r == null || !r.hasRest || !r.hasContact || !r.hasOutside || string.IsNullOrEmpty(r.objectId))
                throw new InvalidOperationException("Completa las tres referencias antes de generar.");
            if (r.duration < 0.3f || r.duration > 30 || float.IsNaN(r.duration) || float.IsInfinity(r.duration))
                throw new InvalidOperationException("Usa una duración entre 0.3 y 30 segundos.");
            preview.Evaluate(source, 0, rawFingers: true);
            var target = Resolve(preview, r);
            if (target == null) throw new InvalidOperationException("La pieza de las referencias ya no existe en este equipo.");
            var kind = r.template == LabReloadTemplate.Bolt ? EquipmentActionKind.Cycle : r.template == LabReloadTemplate.FireBolt ? EquipmentActionKind.Fire : EquipmentActionKind.Reload;
            bool oldReloadRoute = (bool)LabPreview.Get(preview.Equipment, "_usesLeftReloadTarget");
            var take = LabAssetIO.Clone(source);
            HandPoseData homeFingers = null, open = null, closed = null, trigger = null;
            try
            {
                take.kind = kind;
                if (r.side == HandSide.Right && kind != source.kind)
                {
                    preview.Evaluate(take, 0, rawFingers: true);
                    if ((bool)LabPreview.Get(preview.Equipment, "_usesLeftReloadTarget") != oldReloadRoute)
                        throw new InvalidOperationException("La izquierda depende del selector heredado de Reload. Cambiar el tipo activaría/desactivaría su ruta y alteraría esa otra mano. Parte de una toma del mismo tipo o convierte primero su recorrido completo a puntos de muñeca y guarda una copia.");
                }
                preview.EnableHand(r.side);
                var guard = preview.Guard(r.side);
                var chain = r.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                var binding = r.side == HandSide.Left ? preview.Slot.leftHandPose : preview.Slot.rightHandPose;
                if (binding.weight < 1 || preview.Equipment.handIK.alpha < 1 || chain.positionWeight < 1 || chain.rotationWeight < 1)
                    throw new InvalidOperationException("La plantilla necesita peso completo en el binding y solver IK de la mano elegida. Ajusta esa calibración explícitamente; el asistente no modifica esos componentes compartidos.");
                var wrist = chain.target != null ? chain.target : chain.hand;
                Vector3 home = guard.InverseTransformPoint(wrist.position);
                Quaternion homeRotation = Quaternion.Inverse(guard.rotation) * wrist.rotation;
                homeFingers = ScriptableObject.CreateInstance<HandPoseData>(); homeFingers.Side = r.side;
                homeFingers.hideFlags = HideFlags.HideAndDontSave; homeFingers.Capture(preview.Animator);
                open = LabGripTools.Open(preview.Animator, r.side);
                closed = LabGripTools.Preset(preview.Animator, r.side, r.template == LabReloadTemplate.Magazine ? 2 : 3);
                trigger = LabGripTools.Trigger(preview.Animator, r.side);
                var keys = r.side == HandSide.Left ? take.left : take.right;
                foreach (var pose in keys.Select(k => k.fingers).Where(p => p != null).Distinct()) UnityEngine.Object.DestroyImmediate(pose);
                keys.Clear();
                var motion = r.side == HandSide.Left ? take.action.leftHandMotion : take.action.rightHandMotion;
                motion.enabled = true; motion.motionClip = null; motion.reach = AnimationCurve.Constant(0, 1, 0);
                motion.position = new EquipmentVectorCurves(); motion.rotation = new EquipmentVectorCurves();
                if (r.side == HandSide.Left) take.action.leftIK = AnimationCurve.Constant(0, 1, 1);
                else take.action.rightIK = AnimationCurve.Constant(0, 1, 1);
                take.action.duration = r.duration;
                take.kind = kind;
                // Contact phase [0.23, 0.83] must have full action/part influence. Scaling a
                // rotated contact twice is not equivalent to following the blended piece.
                take.action.blendIn = Mathf.Min(take.action.blendIn, r.duration * 0.15f);
                take.action.blendOut = Mathf.Min(take.action.blendOut, r.duration * 0.15f);
                if (r.side == HandSide.Left) take.action.leftHandTargetMotion = null;
                var parts = Keys(take, r); parts.Clear();
                BuildPiece(parts, r, target.parent);
                // Sample after source animation + moving part, before compiling wrist curves. This keeps
                // contact through rotations at 60 Hz without introducing runtime/editor constraints.
                int samples = Mathf.CeilToInt(r.duration * 60);
                float incoming = take.action.blendIn, outgoing = take.action.blendOut;
                take.action.blendIn = take.action.blendOut = 0;
                try
                {
                    Vector3 lastEuler = Vector3.zero;
                    for (int i = 0; i <= samples; i++)
                    {
                        float t = (float)i / samples;
                        preview.Evaluate(take, t, rawFingers: true);
                        target = Resolve(preview, r); guard = preview.Guard(r.side);
                        Vector3 contact = target.TransformPoint(r.contactPosition);
                        Quaternion rotation = target.rotation * Quaternion.Euler(r.contactEuler);
                        float attach = Attachment(t, r.template);
                        if (r.template == LabReloadTemplate.Revolver && t >= 0.25f && t <= 0.78f)
                        {
                            float cycle = (t - 0.25f) / 0.53f * Mathf.Clamp(r.rounds, 1, 12);
                            Vector3 direction = target.parent.TransformVector(r.outsidePosition - r.restPosition).normalized;
                            if (direction.sqrMagnitude < 0.5f) direction = target.parent.up;
                            contact += direction * (0.10f * Mathf.Pow(Mathf.Sin(cycle * Mathf.PI), 2));
                        }
                        Vector3 position = Vector3.Lerp(guard.TransformPoint(home), contact, attach);
                        rotation = Quaternion.Slerp(guard.rotation * homeRotation, rotation, attach);
                        Vector3 euler = LabTimeline.UnwrapEuler((Quaternion.Inverse(guard.rotation) * rotation).eulerAngles, lastEuler);
                        keys.Add(new LabHandKey { time = t, position = guard.InverseTransformPoint(position), euler = euler, pathMode = LabPathMode.Linear });
                        lastEuler = euler;
                    }
                }
                finally { take.action.blendIn = incoming; take.action.blendOut = outgoing; }
                LabAssetIO.BuildMotion(keys, motion);
                AddFingers(keys, 0, homeFingers);
                if (r.template == LabReloadTemplate.FireBolt)
                { AddFingers(keys, 0.055f, trigger); AddFingers(keys, 0.12f, homeFingers); }
                AddFingers(keys, 0.16f, open); AddFingers(keys, 0.23f, closed);
                if (r.template == LabReloadTemplate.Revolver || r.template == LabReloadTemplate.SingleRound)
                    for (int round = 0; round < Mathf.Clamp(r.rounds, 1, 12); round++)
                    {
                        float t = 0.25f + round * 0.53f / Mathf.Clamp(r.rounds, 1, 12);
                        AddFingers(keys, t, closed); AddFingers(keys, t + 0.22f / Mathf.Clamp(r.rounds, 1, 12), open);
                        AddFingers(keys, t + 0.49f / Mathf.Clamp(r.rounds, 1, 12), closed);
                    }
                AddFingers(keys, 0.83f, closed); AddFingers(keys, 0.89f, open); AddFingers(keys, 1, homeFingers);
                keys.Sort((a, b) => a.time.CompareTo(b.time));
                return take;
            }
            catch { LabAssetIO.DestroyDraft(take); throw; }
            finally
            {
                foreach (var pose in new[] { homeFingers, open, closed, trigger }) if (pose != null) UnityEngine.Object.DestroyImmediate(pose);
            }
        }

        private static float Attachment(float t, LabReloadTemplate template)
        {
            float start = template == LabReloadTemplate.FireBolt ? 0.12f : 0;
            return t < 0.23f ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, 0.23f, t))
                : t > 0.83f ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.83f, 1, t)) : 1;
        }

        private static void AddFingers(List<LabHandKey> keys, float t, HandPoseData pose)
        {
            var copy = UnityEngine.Object.Instantiate(pose); copy.hideFlags = HideFlags.HideAndDontSave;
            var existing = LabTimeline.FindFingers(keys, t)?.fingers;
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            LabTimeline.RecordFingers(keys, t, copy);
        }

        private static void BuildPiece(List<LabWeaponKey> keys, LabReloadRecipe r, Transform parent)
        {
            Vector3 endEuler = LabTimeline.UnwrapEuler(r.outsideEuler, r.restEuler);
            Vector3 bend = parent.InverseTransformVector(parent.rotation * r.arc);
            Action<float, Vector3, Vector3, bool> add = (t, p, e, arc) => keys.Add(new LabWeaponKey
            { time = t, position = p, euler = e, pathMode = arc ? LabPathMode.Arc : LabPathMode.Ease, bend = bend });
            if (r.template == LabReloadTemplate.SingleRound)
            {
                add(0, r.outsidePosition, endEuler, false); add(0.23f, r.outsidePosition, endEuler, true);
                int rounds = Mathf.Clamp(r.rounds, 1, 12);
                for (int n = 0; n < rounds; n++)
                {
                    float start = 0.23f + n * 0.57f / rounds;
                    add(start + 0.25f / rounds, r.restPosition, r.restEuler, false);
                    add(start + 0.32f / rounds, r.restPosition, r.restEuler, true);
                    add(start + 0.57f / rounds, r.outsidePosition, endEuler, true);
                }
                add(1, r.outsidePosition, endEuler, false);
                return;
            }
            add(0, r.restPosition, r.restEuler, false);
            add(0.23f, r.restPosition, r.restEuler, r.template == LabReloadTemplate.Magazine);
            if (r.template == LabReloadTemplate.Bolt || r.template == LabReloadTemplate.FireBolt)
            {
                add(0.34f, r.restPosition, endEuler, false); // Lift/unlock before pulling.
                add(0.51f, r.outsidePosition, endEuler, false);
                add(0.58f, r.outsidePosition, endEuler, false);
                add(0.73f, r.restPosition, endEuler, false);
            }
            else
            {
                add(0.43f, r.outsidePosition, endEuler, false);
                add(r.template == LabReloadTemplate.Revolver ? 0.78f : 0.57f, r.outsidePosition, endEuler, true);
            }
            add(0.83f, r.restPosition, r.restEuler, false); add(1, r.restPosition, r.restEuler, false);
        }
    }
}
