using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal sealed class LabFrameCollisionException : InvalidOperationException
    {
        internal LabFrameCollisionException() : base("Ya hay un punto en el destino. Elige otro fotograma o confirma Reemplazar.") { }
    }
    // Operates only on an owned candidate. UI commits after compilation succeeds.
    internal static class LabFrameEditing
    {
        internal static List<LabHandKey> Hand(AnimationLabTake take, int track) => track < 2 ? take.left : take.right;
        internal static List<LabWeaponKey> Objects(AnimationLabTake take, string id)
        {
            if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Selecciona una pieza o diseño.");
            var keys = id.StartsWith("part:", StringComparison.Ordinal)
                ? take.parts.FirstOrDefault(p => p.path == id.Substring(5))?.keys
                : id.StartsWith("prop:", StringComparison.Ordinal) ? take.props.FirstOrDefault(p => p.id == id.Substring(5))?.keys : null;
            return keys ?? throw new InvalidOperationException("La pieza no tiene puntos guardados.");
        }
        internal static IEnumerable<float> Times(AnimationLabTake take, int track, string id)
        {
            ValidateTrack(track);
            if (track >= 4) return (track == 4 ? take.weapon : Objects(take, id)).Select(k => k.time);
            return Hand(take, track).Where(k => track % 2 == 0 ? !k.fingersOnly : k.fingers != null).Select(k => k.time);
        }
        internal static void ValidateTime(float time)
        {
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0 || time > 1)
                throw new InvalidOperationException("El punto debe quedar dentro de la duración de la toma.");
        }
        private static void ValidateTrack(int track)
        {
            if (track < 0 || track > 5) throw new InvalidOperationException("Pista no válida.");
        }
        internal static bool Same(float a, float b) => Mathf.Abs(a - b) < LabTimeline.TimeEpsilon;
        internal static bool Collision(AnimationLabTake take, int track, string id, float from, float to) =>
            !Same(from, to) && Times(take, track, id).Any(t => Same(t, to) && !Same(t, from));

        internal static void EnsureWristEditable(AnimationLabTake take, int track, bool allowRecipe)
        {
            if (track >= 4 || track % 2 == 1) return;
            var side = track == 0 ? HandSide.Left : HandSide.Right;
            if (LabCoordinatedPaths.Segments(take).Any(s => s.side == side))
                throw new InvalidOperationException("Esta muñeca sigue una pieza. Mueve el punto o la transición de la pieza, no una muestra de muñeca.");
            var motion = track == 0 ? take.action.leftHandMotion : take.action.rightHandMotion;
            if (!allowRecipe && (motion.motionClip != null || LabTimeline.HasMotion(motion.reach)
                || (track == 0 && take.action.leftHandTargetMotion != null)))
                throw new InvalidOperationException("La muñeca todavía depende de un clip/Reach. Confirma su sustitución o usa Solo dedos.");
        }

        internal static void Move(AnimationLabTake take, int track, string id, float from, float to,
            bool replace, bool wholeHand = false, bool linkedFingers = false, bool allowRecipe = false)
        {
            ValidateTrack(track); ValidateTime(from); ValidateTime(to);
            if (!Times(take, track, id).Any(t => Same(t, from))) throw new InvalidOperationException("No hay un punto de esta pista en el origen.");
            if (Same(from, to)) return;
            var channels = new List<int> { track };
            if (wholeHand && track < 4)
            {
                int other = track % 2 == 0 ? track + 1 : track - 1;
                if (Times(take, other, id).Any(t => Same(t, from))) channels.Add(other);
            }
            foreach (int wrist in channels.Where(c => c == 0 || c == 2)) EnsureWristEditable(take, wrist, allowRecipe);
            if (linkedFingers && track == 5)
            {
                var key = Objects(take, id).First(k => Same(k.time, from));
                foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
                {
                    int fingerTrack = side == HandSide.Left ? 1 : 3;
                    if (LabCoordinatedPaths.Contact(key, side) != null && Times(take, fingerTrack, null).Any(t => Same(t, from)))
                        channels.Add(fingerTrack);
                }
            }
            foreach (int channel in channels)
                if (!replace && Collision(take, channel, id, from, to))
                    throw new LabFrameCollisionException();
            foreach (int channel in channels)
            {
                if (channel < 4) MoveHand(take, channel, from, to);
                else
                {
                    var keys = channel == 4 ? take.weapon : Objects(take, id);
                    var key = keys.First(k => Same(k.time, from));
                    keys.RemoveAll(k => k != key && Same(k.time, to));
                    key.time = to;
                    keys.Sort((a, b) => a.time.CompareTo(b.time));
                }
            }
            if (channels.Any(c => c == 0)) RebuildWrist(take, 0);
            if (channels.Any(c => c == 2)) RebuildWrist(take, 2);
            if (track == 4) LabAssetIO.BuildWeaponMotion(take);
        }

        private static void MoveHand(AnimationLabTake take, int track, float from, float to)
        {
            var keys = Hand(take, track);
            var source = keys.First(k => Same(k.time, from) && (track % 2 == 0 ? !k.fingersOnly : k.fingers != null));
            // Old takes may keep position and fingers in separate entries at the same time.
            var destination = keys.FirstOrDefault(k => k != source && Same(k.time, to)
                && (track % 2 == 0 ? !k.fingersOnly : k.fingers != null))
                ?? keys.FirstOrDefault(k => k != source && Same(k.time, to));
            if (track % 2 == 0)
            {
                if (destination == null) { destination = new LabHandKey { time = to, fingersOnly = true }; keys.Add(destination); }
                destination.position = source.position; destination.euler = source.euler; destination.pathMode = source.pathMode;
                destination.bend = source.bend; destination.departureSpeed = source.departureSpeed; destination.arrivalSpeed = source.arrivalSpeed;
                destination.outgoing = source.outgoing; destination.incoming = source.incoming; destination.fingersOnly = false;
                if (source.fingers != null) source.fingersOnly = true; else keys.Remove(source);
            }
            else
            {
                if (destination == null) { destination = new LabHandKey { time = to, fingersOnly = true }; keys.Add(destination); }
                if (destination.fingers != null && destination.fingers != source.fingers)
                    UnityEngine.Object.DestroyImmediate(destination.fingers); // Candidate owns this cloned pose.
                destination.fingers = source.fingers; source.fingers = null;
                destination.fingerBlend = source.fingerBlend; destination.fingerBlendDuration = source.fingerBlendDuration;
                destination.fingerLabel = source.fingerLabel;
                if (source.fingersOnly) keys.Remove(source);
            }
            keys.Sort((a, b) => a.time.CompareTo(b.time));
        }

        internal static void RebuildWrist(AnimationLabTake take, int track)
        {
            if (track == 0) take.action.leftHandTargetMotion = null;
            LabAssetIO.BuildMotion(Hand(take, track), track == 0 ? take.action.leftHandMotion : take.action.rightHandMotion);
        }

        internal static HandSide[] AffectedHands(AnimationLabTake source, AnimationLabTake candidate, int track, string id)
        {
            var contacts = LabCoordinatedPaths.Segments(source).Concat(LabCoordinatedPaths.Segments(candidate));
            var sides = track == 4 ? contacts.Select(s => s.side)
                : track == 5 ? contacts.Where(s => s.objectId == id).Select(s => s.side) : Enumerable.Empty<HandSide>();
            if (track < 4 && track % 2 == 0) sides = sides.Append(track == 0 ? HandSide.Left : HandSide.Right);
            return sides.Distinct().ToArray();
        }
    }
}
