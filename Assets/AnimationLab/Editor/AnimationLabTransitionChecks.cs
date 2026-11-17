using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabTransitionChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar transiciones y mover fotogramas")]
        public static void Run()
        {
            int previews = EditorSceneManager.previewSceneCount;
            byte[] scene = File.ReadAllBytes(LabPreview.ScenePath);
            byte[] original = File.ReadAllBytes(AnimationLabDynamicsChecks.BasePath);
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode))) Check(mode);
            Require(previews == EditorSceneManager.previewSceneCount, "previews released");
            Require(scene.SequenceEqual(File.ReadAllBytes(LabPreview.ScenePath)), "fixture scene unchanged");
            Require(original.SequenceEqual(File.ReadAllBytes(AnimationLabDynamicsChecks.BasePath)), "BASE CORRECTA unchanged");
            Debug.Log("[AnimationLabTransitionChecks] PASS: independent/combined key moves, collisions, exact times, Undo/Redo, transition hold/travel/settle, fingers, candidate isolation, contact retiming, persistence and source preservation.");
        }
        public static void RunAll() { Run(); AnimationLabUsabilityChecks.RunAll(); }

        private static void Check(SpineDriveMode mode)
        {
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            AnimationLabTake saved = null;
            try
            {
                LabPreview.Set(window, "mode", mode);
                LabPreview.Call(window, "StartPreview"); LabPreview.Call(window, "NewDraft", 1);
                var preview = (LabPreview)LabPreview.Get(window, "preview");
                var take = Prepare(window, preview);
                float T(int f) => LabTimeline.Time(f, take.action.duration);
                var fingers = LabTimeline.Find(take.left, T(40)).fingers;
                string right = JsonUtility.ToJson(take.action.rightHandMotion), weapon = JsonUtility.ToJson(take.action.weaponPosition);
                LabPreview.Call(window, "SelectTrack", 0, T(40));
                string before = Snapshot(take);
                Move(window, 0, null, T(40), 60);
                Undo.FlushUndoRecordObjects();
                Require(LabTimeline.Find(take.left, T(40)).fingers == fingers && LabTimeline.Find(take.left, T(40)).fingersOnly, "moving wrist splits legacy combined key and retains fingers at old time");
                Require(LabTimeline.Find(take.left, T(60))?.fingers == null && !LabTimeline.Find(take.left, T(60)).fingersOnly, "wrist reaches new time without moving fingers");
                Require(right == JsonUtility.ToJson(take.action.rightHandMotion) && weapon == JsonUtility.ToJson(take.action.weaponPosition), "unrelated motion channels preserved");
                Undo.PerformUndo(); Require(Snapshot(take) == before, "move Undo restores all channels");
                Undo.PerformRedo(); Require(LabTimeline.Find(take.left, T(60)) != null, "move Redo");
                var wristMotion = take.action.leftHandMotion;
                Move(window, 1, null, T(40), 60);
                Require(LabTimeline.Find(take.left, T(60)).fingers != null && !LabTimeline.Find(take.left, T(60)).fingersOnly, "moving fingers merges with destination wrist");
                Require(ReferenceEquals(wristMotion, take.action.leftHandMotion), "finger move does not replace wrist recipe");
                Require(LabTimeline.Find(take.left, T(60)).fingerLabel == "Destino", "finger label survives move");
                string collision = Snapshot(take);
                bool refused = Refused(() => Move(window, 0, null, T(60), 96));
                Require(refused && collision == Snapshot(take), "collision rejects without changes");
                Move(window, 0, null, T(60), 96, replace: true);
                Require(LabTimeline.Find(take.left, T(60)).fingersOnly, "replacing wrist never deletes source fingers");
                take = Prepare(window, preview);
                var separated = LabAssetIO.Clone(take);
                try
                {
                    var separatePose = UnityEngine.Object.Instantiate(separated.left.First(k => k.fingers != null).fingers);
                    separated.left.Insert(0, new LabHandKey { time = T(96), fingersOnly = true, fingers = separatePose });
                    LabFrameEditing.Move(separated, 0, null, T(40), T(96), true);
                    Require(LabFrameEditing.Times(separated, 0, null).Count(t => LabFrameEditing.Same(t, T(96))) == 1
                        && separated.left.Any(k => LabFrameEditing.Same(k.time, T(96)) && k.fingers == separatePose),
                        "replacement preserves legacy separate finger entry at destination without duplicating wrist");
                }
                finally { LabAssetIO.DestroyDraft(separated); }
                Move(window, 0, null, T(40), 60, whole: true);
                Require(LabTimeline.Find(take.left, T(40)) == null && LabTimeline.Find(take.left, T(60)).fingers != null, "whole hand move coordinates both channels");
                take = Prepare(window, preview);
                var offGrid = new LabHandKey { time = .123456f, position = new Vector3(.01f, .02f, 0) };
                take.right.Add(offGrid); LabAssetIO.BuildMotion(take.right, take.action.rightHandMotion);
                Move(window, 2, null, offGrid.time, 27);
                Require(LabTimeline.Find(take.right, T(27)) != null && !take.right.Any(k => LabFrameEditing.Same(k.time, offGrid.time)), "off-grid origin moved by its exact time");
                Require(Refused(() => Move(window, 2, null, T(27), -1)), "negative frame rejected");

                take = Prepare(window, preview);
                var plan = Plan(take, 0);
                string snapshot = Snapshot(take);
                var candidate = LabTransitionAssistant.Generate(take, plan, Vector3.one);
                try
                {
                    Require(snapshot == Snapshot(take), "transition generation leaves source unchanged");
                    Require(candidate.left.Count(k => !k.fingersOnly) == 5, "hold and settle insert two control points");
                    Require(candidate.action.leftHandMotion.position.Evaluate(T(10)).sqrMagnitude < .000001f, "hold keeps start position");
                    Require(Vector3.Distance(candidate.action.leftHandMotion.position.Evaluate(T(60)), new Vector3(.12f, .04f, .02f)) < .000001f, "exact destination");
                    Require(Vector3.Distance(candidate.action.leftHandMotion.position.Evaluate(T(62)), new Vector3(.12f, .04f, .02f)) < .000001f, "settle holds destination");
                    Require(candidate.left.Count(k => k.fingers != null) == take.left.Count(k => k.fingers != null), "conserve option leaves finger keys intact");
                    Require(candidate.action.characterClip == take.action.characterClip && right == JsonUtility.ToJson(candidate.action.rightHandMotion), "source clips and other hand preserved");
                }
                finally { LabAssetIO.DestroyDraft(candidate); }
                var invalid = JsonUtility.FromJson<LabTransitionPlan>(JsonUtility.ToJson(plan)); invalid.arrivalFrame = 110;
                Require(Refused(() => { var bad = LabTransitionAssistant.Generate(take, invalid, Vector3.one); LabAssetIO.DestroyDraft(bad); }) && snapshot == Snapshot(take), "neighbor collision transactional");
                invalid.arcCentimeters.x = float.NaN;
                Require(Refused(() => { var bad = LabTransitionAssistant.Generate(take, invalid, Vector3.one); LabAssetIO.DestroyDraft(bad); }), "non-finite arc rejected");
                invalid = JsonUtility.FromJson<LabTransitionPlan>(JsonUtility.ToJson(plan)); invalid.settleFrames = int.MaxValue;
                Require(Refused(() => { var bad = LabTransitionAssistant.Generate(take, invalid, Vector3.one); LabAssetIO.DestroyDraft(bad); })
                    && snapshot == Snapshot(take), "settle integer overflow cannot overwrite F0");
                CheckPassAndFingerBlocks(take);

                LabPreview.Set(window, "transitionPlan", plan); LabPreview.Set(window, "transitionFingerChoice", 2);
                LabPreview.Call(window, "StartTransitionTrial");
                Require(snapshot == Snapshot(take), "UI trial leaves draft untouched");
                Require(Refused(() => Move(window, 0, null, T(40), 60)), "retiming blocked during trial");
                LabPreview.Call(window, "CancelTransitionTrial", true);
                Require(snapshot == Snapshot(take), "cancel has no commit");
                LabPreview.Call(window, "StartTransitionTrial");
                take.action.commitTime += .01f;
                Require(Refused(() => LabPreview.Call(window, "AcceptTransitionTrial")), "stale draft cannot be accepted");
                LabPreview.Call(window, "CancelTransitionTrial", true); take.action.commitTime -= .01f;
                LabPreview.Call(window, "StartTransitionTrial");
                var outside = take.left.Last(k => k.fingers != null).fingers;
                LabPreview.Call(window, "AcceptTransitionTrial"); Undo.FlushUndoRecordObjects();
                Require(LabTimeline.Find(take.left, T(60)).fingers != null && LabTimeline.Find(take.left, T(40)) == null, "accept synchronizes selected wrist and finger destination");
                Require(take.left.Last(k => k.fingers != null).fingers == outside, "unmodified later finger pose retains its object");
                string accepted = Snapshot(take);
                Undo.PerformUndo(); Require(take.left.Count(k => !k.fingersOnly) == 3, "transition Undo restores three source poses");
                Undo.PerformRedo(); Require(accepted == Snapshot(take), "transition Redo restores result");
                saved = LabAssetIO.Save(take, preview, "__TransitionChecks");
                string path = AssetDatabase.GetAssetPath(saved);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reopened = LabAssetIO.Clone(AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path));
                try
                {
                    Require(reopened.left.Any(k => !k.fingersOnly && LabFrameEditing.Same(k.time, T(64))), "settle survives save/reimport");
                    Require(reopened.left.Any(k => k.fingers != null && LabFrameEditing.Same(k.time, T(60))), "finger arrival survives save/reimport");
                }
                finally { LabAssetIO.DestroyDraft(reopened); }
                CheckVariantAndSettle(window, preview);
                CheckContacts(window, preview);
                Debug.Log("[AnimationLabTransitionChecks] Passed " + mode);
            }
            finally
            {
                LabPreview.Call(window, "ClosePreview"); UnityEngine.Object.DestroyImmediate(window);
                if (saved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
            }
        }

        private static AnimationLabTake Prepare(AnimationLabWindow window, LabPreview preview)
        {
            LabPreview.Call(window, "NewDraft", 1);
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            take.action.duration = 2;
            foreach (int frame in new[] { 0, 40, 96 })
                LabTimeline.RecordWrist(take.left, take.action.leftHandMotion, LabTimeline.Time(frame, 2),
                    frame == 40 ? new Vector3(.12f, .04f, .02f) : Vector3.zero, Vector3.zero);
            foreach (int frame in new[] { 0, 40, 100 })
            {
                var pose = frame == 40 ? LabGripTools.Preset(preview.Animator, HandSide.Left, 2) : LabGripTools.Open(preview.Animator, HandSide.Left);
                LabTimeline.RecordFingers(take.left, LabTimeline.Time(frame, 2), pose);
                var key = LabTimeline.Find(take.left, LabTimeline.Time(frame, 2));
                key.fingerBlend = LabFingerBlend.Smooth; key.fingerBlendDuration = 10f / 120; key.fingerLabel = frame == 40 ? "Destino" : "Base";
            }
            LabPreview.Set(window, "studioTab", 1); LabPreview.Call(window, "SelectTrack", 0, 0f);
            LabPreview.Set(window, "dirty", false);
            return take;
        }
        private static LabTransitionPlan Plan(AnimationLabTake take, int track) => new LabTransitionPlan
        { track = track, from = 0, to = LabTimeline.Time(40, take.action.duration), departureFrame = 20, arrivalFrame = 60,
            settleFrames = 4, fingerStartFrame = 30, shape = LabTransitionShape.Arc };

        private static void CheckContacts(AnimationLabWindow window, LabPreview preview)
        {
            var original = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath);
            LabPreview.Call(window, "Load", original);
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            const string id = "part:AK12Rig/Root/Mag";
            var part = LabFrameEditing.Objects(take, id);
            float from = part.OrderBy(k => k.time).ElementAt(1).time;
            var fingerObjects = take.left.Where(k => k.fingers != null).Select(k => k.fingers).ToArray();
            string right = JsonUtility.ToJson(take.action.rightHandMotion);
            Move(window, 5, id, from, 70);
            Require(fingerObjects.All(p => take.left.Any(k => k.fingers == p)), "piece move preserves finger objects");
            Require(right == JsonUtility.ToJson(take.action.rightHandMotion), "piece move preserves unrelated hand motion");
            foreach (var segment in LabCoordinatedPaths.Segments(take).Where(s => s.objectId == id))
            {
                float t = Mathf.Lerp(segment.from.time, segment.to.time, .5f);
                preview.Evaluate(take, t, -10, 10, true);
                var target = LabCoordinatedPaths.Target(preview, id);
                var a = LabCoordinatedPaths.Contact(segment.from, segment.side); var b = LabCoordinatedPaths.Contact(segment.to, segment.side);
                var chain = segment.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                Require(Vector3.Distance(chain.target.position, target.TransformPoint(Vector3.Lerp(a.position, b.position, .5f))) < .001f, "retimed contact follows piece with look/Aim");
            }
            string before = Snapshot(take);
            Require(Refused(() => Move(window, 0, null, take.left.First(k => !k.fingersOnly).time, 30)) && before == Snapshot(take), "direct retiming of compiled wrist is refused");
            float last = LabFrameEditing.Objects(take, id).Max(k => k.time);
            Require(Refused(() => Move(window, 5, id, last, LabTimeline.LastFrame(take.action.duration))) && before == Snapshot(take), "moving contact to zero-weight fade rejects transactionally");
            var isolated = new LabHandKey { time = .123456f, fingersOnly = true,
                fingers = UnityEngine.Object.Instantiate(take.left.First(k => k.fingers != null).fingers), fingerLabel = "Solo dedos" };
            take.left.Add(isolated); take.left.Sort((a, b) => a.time.CompareTo(b.time));
            var motion = take.action.leftHandMotion;
            Move(window, 1, null, isolated.time, 25, whole: true);
            Require(ReferenceEquals(motion, take.action.leftHandMotion), "whole-hand checkbox on a finger-only key does not touch/contact-check nonexistent wrist channel");
        }
        private static void CheckPassAndFingerBlocks(AnimationLabTake source)
        {
            var passSource = LabAssetIO.Clone(source);
            try
            {
                passSource.left.First(k => !k.fingersOnly && LabFrameEditing.Same(k.time, LabTimeline.Time(96, 2))).position = new Vector3(.2f, .06f, .04f);
                var passPlan = Plan(passSource, 0);
                passPlan.departureFrame = 0; passPlan.arrivalFrame = 40; passPlan.settleFrames = 0;
                passPlan.shape = LabTransitionShape.Direct; passPlan.continueAtArrival = true;
                var pass = LabTransitionAssistant.Generate(passSource, passPlan, Vector3.one);
                try
                {
                    float arrival = LabTimeline.Time(40, 2);
                    var frame = pass.action.leftHandMotion.position.x.keys.First(k => LabFrameEditing.Same(k.time, arrival));
                    Require(frame.inTangent > .01f && Mathf.Abs(frame.inTangent - frame.outTangent) < .000001f,
                        "pass-through has matching nonzero incoming/outgoing tangent, even when neighbors were Legacy");
                }
                finally { LabAssetIO.DestroyDraft(pass); }
                var fingerPlan = Plan(source, 1);
                var fingers = LabTransitionAssistant.Generate(source, fingerPlan, Vector3.one);
                try
                {
                    var hold = LabTimeline.Find(fingers.left, LabTimeline.Time(64, 2));
                    Require(hold?.fingers != null && hold.fingerBlend == LabFingerBlend.Step, "finger-only settle has an explicit held endpoint");
                    Require(JsonUtility.ToJson(source.action.leftHandMotion) == JsonUtility.ToJson(fingers.action.leftHandMotion), "finger blocks preserve wrist recipe");
                }
                finally { LabAssetIO.DestroyDraft(fingers); }
            }
            finally { LabAssetIO.DestroyDraft(passSource); }
        }
        private static void CheckVariantAndSettle(AnimationLabWindow window, LabPreview preview)
        {
            var source = Prepare(window, preview);
            var custom = LabGripTools.Preset(preview.Animator, HandSide.Left, 2);
            custom.GetEntry(FingerId.Index, 1).localRotation *= Quaternion.Euler(65, 80, -100);
            var expected = UnityEngine.Object.Instantiate(custom);
            var preset = ScriptableObject.CreateInstance<LabGripPreset>();
            preset.side = HandSide.Left; preset.fingers = custom;
            AnimationLabTake saved = null, exported = null;
            try
            {
                string originalPose = JsonUtility.ToJson(custom);
                LabAssetIO.NormalizeFingerPose(expected, preview);
                LabPreview.Set(window, "transitionPlan", Plan(source, 0));
                LabPreview.Set(window, "transitionFingerChoice", 4 + LabGripTools.Names.Length);
                LabPreview.Set(window, "transitionGrip", preset);
                LabPreview.Call(window, "StartTransitionTrial");
                var candidate = (AnimationLabTake)LabPreview.Get(window, "transitionCandidate");
                var end = LabTimeline.Find(candidate.left, LabTimeline.Time(60, 2));
                Require(LabGripTools.PoseDifference(expected, end.fingers) < .1f && originalPose == JsonUtility.ToJson(custom),
                    "custom variant normalized on copy before preview; original variant unchanged");
                Require(LabTimeline.Find(candidate.left, LabTimeline.Time(64, 2)).fingers != null,
                    "optional fingers explicitly held to settle endpoint");
                // A long next arrival transition must not start during the settle.
                var next = LabTimeline.Find(candidate.left, LabTimeline.Time(100, 2));
                next.fingerBlend = LabFingerBlend.Smooth; next.fingerBlendDuration = 60f / 120;
                preview.Evaluate(candidate, LabTimeline.Time(62, 2), rawFingers: true);
                var bone = preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate);
                Require(Quaternion.Angle(bone.localRotation, end.fingers.GetEntry(FingerId.Index, 1).localRotation) < .1f,
                    "next finger transition cannot interrupt settle");
                saved = LabAssetIO.Save(candidate, preview, "__TransitionVariantChecks");
                exported = LabAssetIO.Clone(saved);
                foreach (var key in exported.left.Concat(exported.right))
                    if (key.fingers != null) UnityEngine.Object.DestroyImmediate(key.fingers);
                exported.left.Clear(); exported.right.Clear();
                exported.action.characterClip = saved.action.characterClip; exported.action.characterMask = saved.action.characterMask;
                foreach (int frame in new[] { 60, 62, 64 })
                {
                    float time = LabTimeline.Time(frame, 2);
                    preview.Evaluate(candidate, time); Quaternion wanted = bone.localRotation;
                    preview.Evaluate(exported, time);
                    Require(Quaternion.Angle(wanted, bone.localRotation) < .7f, "normalized custom grip preview matches Humanoid bake at F" + frame);
                }
                LabPreview.Call(window, "CancelTransitionTrial", true);
                var intermediate = LabGripTools.Open(preview.Animator, HandSide.Left);
                LabTimeline.RecordFingers(source.left, LabTimeline.Time(62, 2), intermediate);
                string before = Snapshot(source);
                Require(Refused(() =>
                {
                    var bad = LabTransitionAssistant.Generate(source, Plan(source, 0), Vector3.one, expected);
                    LabAssetIO.DestroyDraft(bad);
                }) && before == Snapshot(source), "settle rejects an intermediate finger pose without overwriting it");
            }
            finally
            {
                LabPreview.Call(window, "CancelTransitionTrial", false);
                LabPreview.Set(window, "transitionGrip", null);
                LabAssetIO.DestroyDraft(exported);
                if (saved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
                UnityEngine.Object.DestroyImmediate(expected); UnityEngine.Object.DestroyImmediate(custom); UnityEngine.Object.DestroyImmediate(preset);
            }
        }
        private static void Move(AnimationLabWindow window, int track, string id, float from, int frame, bool whole = false, bool replace = false) =>
            LabPreview.Call(window, "MoveFramePoint", track, id, from, frame, whole, false, replace, false);
        private static bool Refused(Action action)
        {
            try { action(); return false; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return true; }
            catch (InvalidOperationException) { return true; }
        }
        private static string Snapshot(AnimationLabTake take) => EditorJsonUtility.ToJson(take) + EditorJsonUtility.ToJson(take.action);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("[AnimationLabTransitionChecks] " + message); }
    }
}
