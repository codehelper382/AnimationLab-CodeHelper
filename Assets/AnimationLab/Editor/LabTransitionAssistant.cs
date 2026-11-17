using System;
using System.Linq;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal enum LabTransitionIntent { Approach, Pass, Grip, Release, Return }
    internal enum LabTransitionShape { Direct, Arc, Contact }

    [Serializable]
    internal sealed class LabTransitionPlan
    {
        public int track;
        public string objectId;
        public float from, to;
        public int departureFrame, arrivalFrame, settleFrames, fingerStartFrame;
        public LabTransitionIntent intent;
        public LabTransitionShape shape = LabTransitionShape.Arc;
        public Vector3 arcCentimeters = new Vector3(0, 3, 0);
        public bool continueAtArrival, useFirstPose;
        public bool relativeFingers;
        public int fingerDurationFrames = 10;
    }

    internal static class LabTransitionAssistant
    {
        internal static int FingerStart(LabTransitionPlan p,float duration) => p.relativeFingers
            ? (int)Math.Max(Math.Ceiling(p.from*duration*60-.0001),Math.Max(0L,(long)p.arrivalFrame-p.fingerDurationFrames))
            : p.fingerStartFrame;
        internal static string TimingError(AnimationLabTake source,LabTransitionPlan p)
        { try{ValidateTiming(source,p);return null;}catch(Exception e){return e.Message;} }
        internal static int MaximumFrame(AnimationLabTake source,LabTransitionPlan p)
        {
            var times=LabFrameEditing.Times(source,p.track,p.objectId).OrderBy(t=>t).ToArray();
            float next=times.FirstOrDefault(t=>t>p.to+LabTimeline.TimeEpsilon);
            return next>0?Mathf.Min(LabTimeline.LastFrame(source.action.duration),Mathf.FloorToInt((next-LabTimeline.TimeEpsilon)*source.action.duration*60))
                :LabTimeline.LastFrame(source.action.duration);
        }
        private static void ValidateTiming(AnimationLabTake source,LabTransitionPlan plan)
        {
            if(source==null||source.action==null||plan==null||!LabTakeValidation.Finite(source.action.duration)||source.action.duration<=0)
                throw new InvalidOperationException("Selecciona una toma y un tramo válidos.");
            var times=LabFrameEditing.Times(source,plan.track,plan.objectId).OrderBy(t=>t).ToArray();
            int index=Array.FindIndex(times,t=>LabFrameEditing.Same(t,plan.from));
            if(index<0||index+1>=times.Length||!LabFrameEditing.Same(times[index+1],plan.to))
                throw new InvalidOperationException("El tramo cambió. Pulsa Actualizar tramo para usar sus puntos actuales.");
            int last=LabTimeline.LastFrame(source.action.duration);
            if(plan.departureFrame<0||plan.arrivalFrame>last||plan.settleFrames<0||(long)plan.arrivalFrame+plan.settleFrames>last)
                throw new InvalidOperationException($"Inicio, llegada y asentamiento deben quedar entre F0 y F{last}.");
            float start=LabTimeline.Time(plan.departureFrame,source.action.duration),end=LabTimeline.Time(plan.arrivalFrame,source.action.duration);
            if(start<plan.from-LabTimeline.TimeEpsilon||end<=start+LabTimeline.TimeEpsilon)
                throw new InvalidOperationException("La llegada debe estar después del inicio; el inicio no puede preceder a la pose inicial.");
            if(index+2<times.Length&&LabTimeline.Time(plan.arrivalFrame+plan.settleFrames,source.action.duration)>=times[index+2]-LabTimeline.TimeEpsilon)
                throw new InvalidOperationException($"El siguiente punto ocupa F{LabTimeline.Frame(times[index+2],source.action.duration)}. Termina antes de F{MaximumFrame(source,plan)+1} o mueve ese punto.");
            if(plan.continueAtArrival&&(plan.settleFrames>0||index+2>=times.Length||plan.shape!=LabTransitionShape.Direct))
                throw new InvalidOperationException("Continuar requiere un punto posterior, sin asentamiento y con curva entre vecinos.");
            if(plan.relativeFingers&&(plan.fingerDurationFrames<0||plan.fingerDurationFrames>last))
                throw new InvalidOperationException($"La adaptación de dedos debe durar entre 0 y {last} fotogramas.");
        }
        internal static AnimationLabTake Generate(AnimationLabTake source, LabTransitionPlan plan, Vector3 units,
            HandPoseData destinationFingers = null, HandPoseData initialFingers = null, string fingerLabel = null, bool allowRecipe = false)
        {
            if (plan == null) throw new InvalidOperationException("Elige dos poses para crear la transición.");
            if (source == null || source.action == null || float.IsNaN(source.action.duration)
                || float.IsInfinity(source.action.duration) || source.action.duration <= 0)
                throw new InvalidOperationException("La duración debe ser finita y mayor que cero.");
            if (!Finite(plan.arcCentimeters) || !Finite(units))
                throw new InvalidOperationException("El arco y la escala deben contener números finitos.");
            if (units.x <= 0 || units.y <= 0 || units.z <= 0)
                throw new InvalidOperationException("La escala de edición debe ser positiva en los tres ejes.");
            ValidateTiming(source,plan);
            var times = LabFrameEditing.Times(source, plan.track, plan.objectId).OrderBy(t => t).ToArray();
            int index = Array.FindIndex(times, t => LabFrameEditing.Same(t, plan.from));
            if (index < 0 || index + 1 >= times.Length || !LabFrameEditing.Same(times[index + 1], plan.to))
                throw new InvalidOperationException("El asistente necesita dos puntos consecutivos de la pista. Selecciona un tramo.");
            int last = LabTimeline.LastFrame(source.action.duration);
            if (plan.departureFrame < 0 || plan.arrivalFrame > last || plan.settleFrames < 0
                || (long)plan.arrivalFrame + plan.settleFrames > last)
                throw new InvalidOperationException("La transición y su asentamiento deben quedar dentro de la toma.");
            float departure = LabTimeline.Time(plan.departureFrame, source.action.duration);
            float arrival = LabTimeline.Time(plan.arrivalFrame, source.action.duration);
            float settled = LabTimeline.Time(plan.arrivalFrame + plan.settleFrames, source.action.duration);
            if (departure < plan.from - LabTimeline.TimeEpsilon || arrival <= departure + LabTimeline.TimeEpsilon)
                throw new InvalidOperationException("Mantener debe terminar antes de Llegar, y no puede empezar antes de la pose inicial.");
            if (index + 2 < times.Length && settled >= times[index + 2] - LabTimeline.TimeEpsilon)
                throw new InvalidOperationException("La transición alcanzaría el siguiente punto. Muévelo primero o reduce Llegar/Asentar.");
            if (plan.continueAtArrival && plan.settleFrames > 0)
                throw new InvalidOperationException("Continuar y detenerse para asentar son opciones distintas. Quita el asentamiento o elige Detenerse.");
            if (plan.continueAtArrival && (index + 2 >= times.Length || plan.shape != LabTransitionShape.Direct))
                throw new InvalidOperationException("Pasar sin parada requiere un punto posterior y movimiento Directo. Se suavizan este tramo y el siguiente conservando sus poses.");
            LabFrameEditing.EnsureWristEditable(source, plan.track, allowRecipe);
            var take = LabAssetIO.Clone(source);
            try
            {
                if (!LabFrameEditing.Same(plan.to, arrival))
                    LabFrameEditing.Move(take, plan.track, plan.objectId, plan.to, arrival, false, allowRecipe: allowRecipe);
                if (plan.track < 4 && plan.track % 2 == 1)
                {
                    SetFingers(take, plan.track, plan.from, arrival, plan.departureFrame,
                        destinationFingers, initialFingers, fingerLabel);
                    if (plan.settleFrames > 0)
                        HoldFingers(take, plan.track, arrival, settled);
                    return take;
                }
                var bend = new Vector3(plan.arcCentimeters.x / Mathf.Max(.000001f, units.x),
                    plan.arcCentimeters.y / Mathf.Max(.000001f, units.y), plan.arcCentimeters.z / Mathf.Max(.000001f, units.z)) * .01f;
                if (plan.track < 4)
                {
                    var keys = LabFrameEditing.Hand(take, plan.track);
                    var first = keys.First(k => !k.fingersOnly && LabFrameEditing.Same(k.time, plan.from));
                    var end = keys.First(k => !k.fingersOnly && LabFrameEditing.Same(k.time, arrival));
                    if (plan.useFirstPose)
                    {
                        var initial = keys.Where(k => !k.fingersOnly).OrderBy(k => k.time).First();
                        end.position = initial.position; end.euler = initial.euler;
                    }
                    var movement = first;
                    if (!LabFrameEditing.Same(departure, plan.from))
                    {
                        movement = new LabHandKey { time = departure, position = first.position, euler = first.euler };
                        var existing = LabTimeline.Find(keys, departure);
                        if (existing != null) { existing.fingersOnly = false; existing.position = first.position; existing.euler = first.euler; movement = existing; }
                        else keys.Add(movement);
                        Style(first, LabTransitionShape.Contact, Vector3.zero, false);
                    }
                    Style(movement, plan.shape, bend, plan.continueAtArrival);
                    if (plan.continueAtArrival) Style(end, LabTransitionShape.Direct, Vector3.zero, true);
                    if (plan.settleFrames > 0)
                    {
                        var hold = new LabHandKey { time = settled, position = end.position, euler = end.euler,
                            pathMode = end.pathMode, bend = end.bend, departureSpeed = end.departureSpeed, arrivalSpeed = end.arrivalSpeed,
                            outgoing = end.outgoing, incoming = end.incoming };
                        var existing = LabTimeline.Find(keys, settled);
                        if (existing != null) { existing.fingersOnly = false; existing.position = end.position; existing.euler = end.euler;
                            existing.pathMode = hold.pathMode; existing.bend = hold.bend; existing.departureSpeed = hold.departureSpeed;
                            existing.arrivalSpeed = hold.arrivalSpeed; existing.outgoing = hold.outgoing; existing.incoming = hold.incoming; }
                        else keys.Add(hold);
                        Style(end, LabTransitionShape.Contact, Vector3.zero, false);
                    }
                    keys.Sort((a, b) => a.time.CompareTo(b.time));
                    LabFrameEditing.RebuildWrist(take, plan.track);
                }
                else
                {
                    var keys = plan.track == 4 ? take.weapon : LabFrameEditing.Objects(take, plan.objectId);
                    var first = keys.First(k => LabFrameEditing.Same(k.time, plan.from));
                    var end = keys.First(k => LabFrameEditing.Same(k.time, arrival));
                    if (plan.useFirstPose)
                    {
                        if (plan.track == 5 && (LabCoordinatedPaths.Contact(end, HandSide.Left) != null || LabCoordinatedPaths.Contact(end, HandSide.Right) != null))
                            throw new InvalidOperationException("No se cambia automáticamente la pose de una pieza con contacto. Coloca el destino manualmente.");
                        var initial = keys.OrderBy(k => k.time).First();
                        end.position = initial.position; end.euler = initial.euler;
                    }
                    var movement = first;
                    if (!LabFrameEditing.Same(departure, plan.from))
                    {
                        movement = CloneKey(first); movement.time = departure; keys.Add(movement);
                        Style(first, LabTransitionShape.Contact, Vector3.zero, false);
                    }
                    Style(movement, plan.shape, bend, plan.continueAtArrival);
                    if (plan.continueAtArrival) Style(end, LabTransitionShape.Direct, Vector3.zero, true);
                    if (plan.settleFrames > 0)
                    {
                        var hold = CloneKey(end); hold.time = settled; keys.Add(hold);
                        Style(end, LabTransitionShape.Contact, Vector3.zero, false);
                    }
                    keys.Sort((a, b) => a.time.CompareTo(b.time));
                    if (plan.track == 4) LabAssetIO.BuildWeaponMotion(take);
                }
                if (destinationFingers != null && plan.track < 4)
                {
                    int fingerTrack = plan.track / 2 * 2 + 1;
                    if (!LabFrameEditing.Same(plan.to, arrival) && LabFrameEditing.Times(take, fingerTrack, null).Any(t => LabFrameEditing.Same(t, plan.to)))
                        LabFrameEditing.Move(take, fingerTrack, null, plan.to, arrival, false);
                    SetFingers(take, fingerTrack, plan.from, arrival, FingerStart(plan,source.action.duration), destinationFingers, initialFingers, fingerLabel);
                    if (plan.settleFrames > 0) HoldFingers(take, fingerTrack, arrival, settled);
                }
                return take;
            }
            catch { LabAssetIO.DestroyDraft(take); throw; }
        }

        private static void SetFingers(AnimationLabTake take, int track, float from, float arrival, int startFrame,
            HandPoseData pose, HandPoseData initial, string label)
        {
            var hand = track < 2 ? HandSide.Left : HandSide.Right;
            if ((pose != null && pose.Side != hand) || (initial != null && initial.Side != hand))
                throw new InvalidOperationException("El agarre debe pertenecer a esta mano.");
            var keys = LabFrameEditing.Hand(take, track);
            if (startFrame < 0 || startFrame > LabTimeline.LastFrame(take.action.duration))
                throw new InvalidOperationException("El inicio de dedos debe quedar dentro de la toma.");
            float start = LabTimeline.Time(startFrame, take.action.duration);
            if (start < from - LabTimeline.TimeEpsilon || start > arrival + LabTimeline.TimeEpsilon)
                throw new InvalidOperationException("El cambio de dedos debe empezar dentro de la transición y antes de llegar.");
            var prior = keys.Where(k => k.fingers != null && k.time < arrival - LabTimeline.TimeEpsilon).OrderBy(k => k.time).LastOrDefault();
            if (prior != null && prior.time > start + LabTimeline.TimeEpsilon)
                throw new InvalidOperationException("Hay otro agarre después de Inicio dedos. Adelántalo o retrasa el inicio para conservarlo.");
            if (prior == null)
            {
                if (initial == null) throw new InvalidOperationException("Falta una pose inicial de dedos. Guarda una o captura la base.");
                var baseline = UnityEngine.Object.Instantiate(initial); baseline.hideFlags = HideFlags.HideAndDontSave;
                LabTimeline.RecordFingers(keys, 0, baseline);
                var key = LabTimeline.FindFingers(keys, 0); key.fingerBlend = LabFingerBlend.Step; key.fingerLabel = "Base inicial · asistente";
            }
            var end = LabTimeline.FindFingers(keys, arrival);
            if (pose != null)
            {
                string previousLabel = end?.fingerLabel;
                if (end?.fingers != null) UnityEngine.Object.DestroyImmediate(end.fingers);
                var copy = UnityEngine.Object.Instantiate(pose); copy.hideFlags = HideFlags.HideAndDontSave;
                LabTimeline.RecordFingers(keys, arrival, copy); end = LabTimeline.FindFingers(keys, arrival);
                end.fingerLabel = label ?? previousLabel ?? "Agarre · asistente";
            }
            if (end?.fingers == null) throw new InvalidOperationException("Guarda un agarre en la llegada o elige una forma de dedos.");
            end.fingerBlend = LabFingerBlend.Smooth; end.fingerBlendDuration = Mathf.Max(0, arrival - start);
        }
        private static void HoldFingers(AnimationLabTake take, int track, float arrival, float settled)
        {
            var keys = LabFrameEditing.Hand(take, track);
            if (keys.Any(k => k.fingers != null && k.time > arrival + LabTimeline.TimeEpsilon && k.time <= settled + LabTimeline.TimeEpsilon))
                throw new InvalidOperationException("Hay un agarre dentro del asentamiento. Muévelo o reduce Asentar; no se reemplaza automáticamente.");
            var end = LabTimeline.FindFingers(keys, arrival);
            var pose = UnityEngine.Object.Instantiate(end.fingers); pose.hideFlags = HideFlags.HideAndDontSave;
            LabTimeline.RecordFingers(keys, settled, pose);
            var hold = LabTimeline.FindFingers(keys, settled); hold.fingerBlend = LabFingerBlend.Step; hold.fingerBlendDuration = 0; hold.fingerLabel = end.fingerLabel;
        }
        private static LabWeaponKey CloneKey(LabWeaponKey key) => LabObjectAuthoring.CloneKeys(new System.Collections.Generic.List<LabWeaponKey> { key })[0];
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        private static void Style(LabHandKey key, LabTransitionShape shape, Vector3 bend, bool continuous)
        {
            key.pathMode = continuous ? LabPathMode.Smooth : LabPathMode.Cinematic;
            key.bend = shape == LabTransitionShape.Arc ? bend : Vector3.zero;
            key.departureSpeed = continuous ? 1 : 0; key.arrivalSpeed = continuous ? 1 : 0;
            key.outgoing = key.incoming = Vector3.zero;
        }
        private static void Style(LabWeaponKey key, LabTransitionShape shape, Vector3 bend, bool continuous)
        {
            key.pathMode = continuous ? LabPathMode.Smooth : LabPathMode.Cinematic;
            key.bend = shape == LabTransitionShape.Arc ? bend : Vector3.zero;
            key.departureSpeed = continuous ? 1 : 0; key.arrivalSpeed = continuous ? 1 : 0;
            key.outgoing = key.incoming = Vector3.zero;
        }
    }
}
