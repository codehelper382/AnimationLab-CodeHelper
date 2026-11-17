using System;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabAuthoringChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar edición por fotogramas")]
        public static void Run()
        {
            Require(LabTimeline.Frame(1, 0.605f) == 37, "final short frame remains selectable");
            Require(Mathf.Abs(LabTimeline.Time(18, 0.6f) - 0.5f) < 0.0001f, "frame 18 maps to half of 0.6 seconds");
            Require(LabTimeline.Time(37, 0.605f) == 1, "last frame clamps to exact endpoint");
            string scene = File.ReadAllText(LabPreview.ScenePath);
            int previews = EditorSceneManager.previewSceneCount;
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))
            {
                var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
                AnimationLabTake saved = null;
                try
                {
                    LabPreview.Set(window, "mode", mode); LabPreview.Call(window, "StartPreview");
                    var preview = (LabPreview)LabPreview.Get(window, "preview");
                    for (int slot = 0; slot < preview.Equipment.slots.Length; slot++)
                    {
                        preview.RestoreAnchors(); LabPreview.Call(window, "NewDraft", slot);
                        var take = (AnimationLabTake)LabPreview.Get(window, "draft");
                        string original = EditorJsonUtility.ToJson(preview.Slot.definition);
                        int ammo = preview.Equipment.Runtime.Current.TotalLoadedRounds;
                        uint commits = preview.Equipment.Runtime.Current.Action.CommitVersion;
                        // Exercise the same selection, synchronization and recording commands as the window.
                        LabPreview.Call(window, "SelectTrack", 2, 0.5f);
                        Require((bool)LabPreview.Call(window, "BeginPositionEdit"), "begin wrist editing");
                        LabPreview.Set(window, "keyPosition", new Vector3(0.01f, 0.02f, 0.05f));
                        LabPreview.Set(window, "keyRotation", new Vector3(0, 5, 0));
                        Require((bool)LabPreview.Call(window, "CommitEdits", true, true), "record wrist");
                        LabPreview.Call(window, "Seek", 0.1f);
                        Require(Vector3.Distance((Vector3)LabPreview.Get(window, "keyPosition"), take.action.rightHandMotion.position.Evaluate(0.1f)) < 0.0001f,
                            "seeking synchronizes position fields");
                        // Fingers only: must not destroy the wrist's curves, Reach or Motion Clip.
                        take.action.rightHandMotion.reach = AnimationCurve.Linear(0, 0, 1, 0.5f);
                        string motion = JsonUtility.ToJson(take.action.rightHandMotion);
                        LabPreview.Call(window, "SelectTrack", 3, 0.25f);
                        var fingers = (HandPoseData)LabPreview.Get(window, "fingers");
                        var joint = fingers.GetEntry(FingerId.Index, 0);
                        joint.localRotation *= Quaternion.Euler(8, 0, 0);
                        LabPreview.Set(window, "fingersEdited", true);
                        Require((bool)LabPreview.Call(window, "CommitEdits", true, true), "record fingers only");
                        Require(JsonUtility.ToJson(take.action.rightHandMotion) == motion, "finger recording preserves complete wrist recipe");
                        Require(take.right.Any(k => k.fingersOnly && Mathf.Abs(k.time - 0.25f) < 0.0001f), "separate finger key stored");
                        LabPreview.Call(window, "SelectTrack", 3, 0f);
                        var source = (HandPoseData)LabPreview.Get(window, "fingers");
                        var recorded = take.right.First(k => k.fingers != null).fingers;
                        Require(Quaternion.Angle(source.GetEntry(FingerId.Index, 0).localRotation, recorded.GetEntry(FingerId.Index, 0).localRotation) < 0.1f,
                            "endpoint editing captures unblended source fingers");
                        // A finger key on a hand with no position keys must not create a guard/binding.
                        var leftBinding = preview.Slot.leftHandPose;
                        Transform guard = leftBinding?.guard;
                        LabPreview.Call(window, "SelectTrack", 1, 0.3f);
                        Require((bool)LabPreview.Call(window, "CommitEdits", true, true), "record independent left fingers");
                        Require(preview.Slot.leftHandPose == leftBinding && preview.Slot.leftHandPose?.guard == guard, "finger-only editing does not enable wrist ownership");
                        if (slot == 0)
                        {
                            LabPreview.Call(window, "SelectTrack", 4, 0.4f);
                            LabPreview.Call(window, "BeginPositionEdit");
                            LabPreview.Set(window, "keyPosition", new Vector3(0.02f, 0.01f, 0));
                            LabPreview.Set(window, "keyRotation", new Vector3(0, 0, 8));
                            Require((bool)LabPreview.Call(window, "CommitEdits", true, true), "record weapon point");
                            Require(Vector3.Distance(take.action.weaponPosition.Evaluate(0.4f), take.weapon[0].position) < 0.0001f, "weapon points build runtime curves");
                            var clone = LabAssetIO.Clone(take);
                            try { Require(clone.weapon[0] != take.weapon[0] && clone.right.Any(k => k.fingersOnly), "clone preserves independent keys deeply"); }
                            finally { LabAssetIO.DestroyDraft(clone); }
                            saved = LabAssetIO.Save(take, preview, "__FrameAuthoringValidation");
                            LabPreview.Set(window, "dirty", false);
                            LabPreview.Call(window, "Load", saved);
                            var loaded = (AnimationLabTake)LabPreview.Get(window, "draft");
                            Require(loaded.weapon.Count == 1 && loaded.right.Any(k => k.fingersOnly), "save/load retains new portable authoring data");
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved)); saved = null;
                        }
                        if (slot == 1) TestSignedRotation(window);
                        Require(EditorJsonUtility.ToJson(preview.Slot.definition) == original, "shared definition unchanged");
                        Require(preview.Equipment.Runtime.Current.TotalLoadedRounds == ammo && preview.Equipment.Runtime.Current.Action.CommitVersion == commits, "authoring never advances gameplay");
                        Debug.Log("[AnimationLabAuthoringChecks] Passed " + mode + "/" + slot);
                    }
                    TestInheritedReload(window, preview);
                }
                finally
                {
                    // Test drafts are intentionally discarded; never create recovery assets from tests.
                    LabPreview.Set(window, "dirty", false);
                    LabPreview.Set(window, "previewingKey", false); LabPreview.Set(window, "fingersEdited", false); LabPreview.Set(window, "weaponPending", false);
                    UnityEngine.Object.DestroyImmediate(window);
                    if (saved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
                }
            }
            TestLegacyKeys();
            var tangents = new EquipmentVectorCurves { x = new AnimationCurve(new Keyframe(0, 0, 0, 2), new Keyframe(1, 0, -2, 0)) };
            Require(LabTimeline.HasValues(tangents), "nonzero tangents count as existing motion");
            Require(File.ReadAllText(LabPreview.ScenePath) == scene && EditorSceneManager.previewSceneCount == previews, "scene unchanged and previews disposed");
            Debug.Log("[AnimationLabAuthoringChecks] PASS: frame selection, independent wrist/finger/weapon keys, raw endpoint editing, legacy data, save/load, both spine modes and all equipment.");
        }

        public static void RunAll() { Run(); AnimationLabChecks.Run(); }

        private static void TestSignedRotation(AnimationLabWindow window)
        {
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            take.right.Clear();
            take.action.rightHandMotion = new EquipmentHandMotion();
            LabTimeline.RecordWrist(take.right, take.action.rightHandMotion, 0, Vector3.zero, new Vector3(0, 0, -10));
            LabTimeline.RecordWrist(take.right, take.action.rightHandMotion, 1, Vector3.zero, new Vector3(0, 0, 10));
            float expected = take.action.rightHandMotion.rotation.Evaluate(0.2f).z;
            LabPreview.Call(window, "SelectTrack", 2, 0.2f);
            Require(Mathf.Abs(((Vector3)LabPreview.Get(window, "keyRotation")).z - expected) < 0.001f, "signed Euler curves survive frame selection");
            LabPreview.Call(window, "CommitEdits", true, true);
            Require(take.action.rightHandMotion.rotation.z.keys.All(k => Mathf.Abs(k.value) <= 10.001f), "inserting a point does not introduce a 360-degree jump");
        }

        private static void TestInheritedReload(AnimationLabWindow window, LabPreview preview)
        {
            preview.RestoreAnchors(); LabPreview.Call(window, "NewDraft", 0);
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            UnityEngine.Object.DestroyImmediate(take.action);
            take.action = UnityEngine.Object.Instantiate(((FirearmDefinition)preview.Slot.definition).reload);
            take.action.hideFlags = HideFlags.HideAndDontSave; take.kind = EquipmentActionKind.Reload;
            string binding = JsonUtility.ToJson(preview.Slot.leftHandPose);
            LabPreview.Call(window, "SelectTrack", 0, 0.4f);
            var target = preview.Equipment.handIK.leftHand.target;
            Vector3 expected = preview.Guard(HandSide.Left).InverseTransformPoint(target.position);
            Require(Vector3.Distance((Vector3)LabPreview.Get(window, "keyPosition"), expected) < 0.0001f, "legacy reload target populates position fields");
            Require((bool)LabPreview.Call(window, "PositionReplacementRequired"), "legacy reload requests replacement confirmation");
            LabPreview.Set(window, "wristReplacementApproved", true); // Simulate the user's explicit confirmation without opening a modal dialog.
            LabPreview.Call(window, "BeginPositionEdit");
            LabPreview.Call(window, "SyncSelection"); // Same operation as Discard adjustment.
            Require(JsonUtility.ToJson(preview.Slot.leftHandPose) == binding, "discard restores inherited reload binding");
            LabPreview.Call(window, "BeginPositionEdit");
            LabPreview.Call(window, "CommitEdits", true, true);
            take.left.Clear(); // Simulate Undo removing the authored position key.
            LabPreview.Call(window, "SyncSelection");
            Require(JsonUtility.ToJson(preview.Slot.leftHandPose) == binding, "ownership rebuild after Undo/last-key removal restores inherited binding");
            TestUndoRedo(window, take, preview, binding);
            LabPreview.Set(window, "dirty", false);
        }

        private static void TestUndoRedo(AnimationLabWindow window, AnimationLabTake take, LabPreview preview, string binding)
        {
            Undo.IncrementCurrentGroup();
            try
            {
                Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { take, take.action }, "Validate Animation Lab Undo");
                LabTimeline.RecordWrist(take.left, take.action.leftHandMotion, 0.4f, Vector3.up * 0.01f, Vector3.zero);
                LabPreview.Call(window, "SyncSelection");
                Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Require(take.left.All(k => k.fingersOnly) && JsonUtility.ToJson(preview.Slot.leftHandPose) == binding, "real Undo restores keys and inherited binding");
                Undo.PerformRedo();
                Require(take.left.Any(k => !k.fingersOnly) && preview.Slot.leftHandPose?.guard != null, "real Redo restores keys and wrist ownership");
            }
            finally
            {
                // Remove only this test's transient objects from history, never the user's Undo stack.
                Undo.ClearUndo(take); Undo.ClearUndo(take.action);
            }
        }

        private static void TestLegacyKeys()
        {
            var take = LabAssetIO.Clone(null);
            var pose = ScriptableObject.CreateInstance<HandPoseData>();
            try
            {
                var legacy = new LabHandKey { time = 0.5f, position = Vector3.right * 0.1f, fingers = pose };
                take.right.Add(legacy);
                LabAssetIO.BuildMotion(take.right, take.action.rightHandMotion);
                LabTimeline.RecordFingers(take.right, 0.25f, null);
                LabAssetIO.BuildMotion(take.right, take.action.rightHandMotion);
                Require(Mathf.Abs(take.action.rightHandMotion.position.Evaluate(0.25f).x - 0.1f) < 0.0001f, "finger-only keys excluded from legacy wrist curves");
                LabTimeline.RemoveWrist(take.right, legacy, take.action.rightHandMotion);
                Require(legacy.fingersOnly && legacy.fingers == pose && !take.action.rightHandMotion.enabled, "removing legacy wrist keeps its fingers");
                LabTimeline.RecordWrist(take.right, take.action.rightHandMotion, 0.5f, Vector3.up, Vector3.zero);
                Require(!legacy.fingersOnly && legacy.fingers == pose, "recording wrist on finger key preserves fingers");
                var longTake = new System.Collections.Generic.List<LabHandKey>();
                var longMotion = new EquipmentHandMotion();
                LabTimeline.RecordWrist(longTake, longMotion, LabTimeline.Time(8999, 300), Vector3.zero, Vector3.zero);
                LabTimeline.RecordWrist(longTake, longMotion, LabTimeline.Time(9000, 300), Vector3.one, Vector3.zero);
                Require(longTake.Count == 2, "adjacent frames stay distinct even in a 300-second take");
            }
            finally { LabAssetIO.DestroyDraft(take); }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("[AnimationLabAuthoringChecks] " + message); }
    }
}
