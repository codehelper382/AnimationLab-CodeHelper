using System;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabHandTemplateChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar inserción solo manos")]
        public static void Run()
        {
            int scenes = EditorSceneManager.previewSceneCount; string scene = File.ReadAllText(LabPreview.ScenePath);
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode))) Check(mode);
            Require(EditorSceneManager.previewSceneCount == scenes && File.ReadAllText(LabPreview.ScenePath) == scene, "isolated scenes cleaned and source preserved");
            Debug.Log("[AnimationLabHandTemplateChecks] PASS: hand-only templates, insertion range, unchanged equipment channels, source preservation, outside targets, contact capture, Undo/Redo and save/reimport.");
        }
        public static void RunAll() { Run(); AnimationLabProceduralChecks.RunAll(); }

        private static void Check(SpineDriveMode mode)
        {
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            AnimationLabTake persisted = null;
            try
            {
                LabPreview.Set(window, "mode", mode); LabPreview.Call(window, "StartPreview"); LabPreview.Call(window, "NewDraft", 0);
                var source = (AnimationLabTake)LabPreview.Get(window, "draft"); var preview = (LabPreview)LabPreview.Get(window, "preview");
                source.action.duration = 2;
                source.handInsert.source = LabHandSource.Procedural;
                source.handInsert.start = 0.2f; source.handInsert.end = 0.8f;
                source.handInsert.start = LabTimeline.Time(30, 2); source.handInsert.end = LabTimeline.Time(31, 2);
                bool shortBlocked = false;
                try { var unexpected = LabHandTemplateInsertion.Insert(source, preview); LabAssetIO.DestroyDraft(unexpected); }
                catch (InvalidOperationException ex) { shortBlocked = ex.Message.Contains("6 fotogramas"); }
                Require(shortBlocked && source.left.Count == 0, "one-frame insertion rejected before converting channels");
                source.handInsert.start = 0.2f; source.handInsert.end = 0.8f;
                var oldLeft = preview.Slot.leftHandPose; var oldRight = preview.Slot.rightHandPose;
                foreach (LabHandTemplate template in Enum.GetValues(typeof(LabHandTemplate)))
                {
                    source.handInsert.template = template;
                    source.handInsert.side = template == LabHandTemplate.Bow || template == LabHandTemplate.SniperBolt ? HandSide.Right : HandSide.Left;
                    source.handInsert.travel = template == LabHandTemplate.Bow ? new Vector3(0, 0, -0.12f) : new Vector3(-0.08f, 0, 0);
                    string original = EditorJsonUtility.ToJson(source.action);
                    preview.Slot.leftHandPose = oldLeft; preview.Slot.rightHandPose = oldRight;
                    preview.Evaluate(source, 0.1f);
                    var selected = source.handInsert.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                    Vector3 before = selected.target.position; Quaternion rotation = selected.target.rotation;
                    Vector3 other = source.handInsert.side == HandSide.Left ? preview.Equipment.handIK.rightHand.target.position : preview.Equipment.handIK.leftHand.target.position;
                    var take = LabHandTemplateInsertion.Insert(source, preview);
                    try
                    {
                        Require(source.parts.Count == 0 && EditorJsonUtility.ToJson(source.action) == original, "source action is untouched");
                        Require(take.action.duration == source.action.duration && take.kind == source.kind && take.action.blendIn == source.action.blendIn && take.action.blendOut == source.action.blendOut, "duration, kind and fades unchanged");
                        Require(take.parts.Count == 0 && take.weapon.Count == 0 && take.action.weaponClip == source.action.weaponClip && JsonUtility.ToJson(take.action.weaponPosition) == JsonUtility.ToJson(source.action.weaponPosition), "no weapon or piece animation added");
                        var untouched = source.handInsert.side == HandSide.Left ? take.right : take.left;
                        Require(untouched.Count == 0, "other hand channel is not generated");
                        var keys = source.handInsert.side == HandSide.Left ? take.left : take.right;
                        Require(keys.Count >= 121 && keys.All(k => k.fingers != null), "editable wrist/finger samples generated");
                        if (template == LabHandTemplate.Pistol || template == LabHandTemplate.SubmachineGun || template == LabHandTemplate.Rifle)
                        {
                            var carry = keys.OrderBy(k => Mathf.Abs(k.time - 0.5f)).First().fingers.GetEntry(FingerId.Index, 1);
                            var release = keys.OrderBy(k => Mathf.Abs(k.time - 0.74f)).First().fingers.GetEntry(FingerId.Index, 1);
                            Require(carry != null && release != null && Quaternion.Angle(carry.localRotation, release.localRotation) > 2, "generated magazine fingers visibly release, not only a grip scalar");
                        }
                        preview.EnableHand(source.handInsert.side); preview.Evaluate(take, 0.1f);
                        selected = source.handInsert.side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                        Require(Vector3.Distance(selected.target.position, before) < 0.001f && Quaternion.Angle(selected.target.rotation, rotation) < 0.1f, "outside target preserved " + template);
                        Vector3 otherAfter = source.handInsert.side == HandSide.Left ? preview.Equipment.handIK.rightHand.target.position : preview.Equipment.handIK.leftHand.target.position;
                        Require(Vector3.Distance(other, otherAfter) < 0.001f, "other hand target not moved");
                        preview.Evaluate(take, 0.5f); Require(selected.weight > 0.99f, "gesture takes IK ownership inside interval");
                    }
                    finally { LabAssetIO.DestroyDraft(take); preview.Slot.leftHandPose = oldLeft; preview.Slot.rightHandPose = oldRight; }
                }
                Require(AnimationLabWindow.HandTemplateGuide(LabHandTemplate.Pistol, 0, 80)[1].Contains("20–25"), "guide retains requested pistol contact timing");
                Require(AnimationLabWindow.HandTemplateGuide(LabHandTemplate.Pistol, 20, 100)[1].Contains("40–45"), "guide follows inserted frame range");
                foreach (var template in new[] { LabHandTemplate.Pistol, LabHandTemplate.SubmachineGun, LabHandTemplate.Rifle })
                {
                    LabHandTemplateLibrary.Gesture(template, 0, 3, out _, out _, out float approach);
                    LabHandTemplateLibrary.Gesture(template, 0.5f, 3, out _, out _, out float carry);
                    LabHandTemplateLibrary.Gesture(template, 0.9f, 3, out _, out _, out float release);
                    Require(approach == 0 && carry > 0.99f && release < 0.01f, "magazine fingers open, grip during carry and release explicitly");
                }
                TestInheritedAction(source, preview);
                source.handInsert.side = HandSide.Left; source.handInsert.template = LabHandTemplate.Pistol;
                var otherPose = LabGripTools.Preset(preview.Animator, HandSide.Right, 0);
                source.right.Add(new LabHandKey { time = 0.35f, fingersOnly = true, fingers = otherPose });
                LabPreview.Call(window, "SelectTrack", 0, 0.3f);
                LabPreview.Set(window, "wristReplacementApproved", true); LabPreview.Call(window, "BeginPositionEdit");
                LabPreview.Set(window, "keyPosition", new Vector3(0.01f, 0.02f, 0.03f));
                LabPreview.Call(window, "CaptureHandTemplateContact");
                Require(source.handInsert.hasContact && source.left.Count == 0 && !(bool)LabPreview.Get(window, "previewingKey"), "one optional contact captured without wrist keys");
                var generated = LabHandTemplateInsertion.Insert(source, preview);
                try { LabPreview.Call(window, "ApplyHandTemplate", generated); }
                finally { LabAssetIO.DestroyDraft(generated); }
                Require(source.left.Count > 0 && source.left[0].fingers != null, "generated ownership transferred to draft");
                Require(source.right.Count == 1 && source.right[0].fingers == otherPose, "applying insertion preserves the exact other-hand pose asset");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Require(source.left.Count == 0, "undo insertion restores previous hand");
                Undo.PerformRedo(); Require(source.left.Count > 0 && source.left[0].fingers != null, "redo restores live finger poses");
                persisted = LabAssetIO.Save(source, preview, "__HandInsertCheck");
                string path = AssetDatabase.GetAssetPath(persisted); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Require(AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path).handInsert.hasContact, "insertion settings persist");
                Debug.Log("[AnimationLabHandTemplateChecks] Passed " + mode);
            }
            finally
            {
                LabPreview.Set(window, "dirty", false); LabPreview.Set(window, "previewingKey", false); LabPreview.Set(window, "fingersEdited", false); LabPreview.Set(window, "objectPending", false);
                UnityEngine.Object.DestroyImmediate(window);
                if (persisted != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(persisted));
            }
        }
        private static void Require(bool condition, string text)
        { if (!condition) throw new InvalidOperationException("[AnimationLabHandTemplateChecks] " + text); }

        private static void TestInheritedAction(AnimationLabTake source, LabPreview preview)
        {
            var gun = preview.Slot.definition as global::CodeHelper.ProceduralShooterFramework.Equipment.FirearmDefinition;
            Require(gun != null && gun.reload != null, "real reload recipe available");
            var inherited = LabAssetIO.Clone(source);
            var left = preview.Slot.leftHandPose; var right = preview.Slot.rightHandPose;
            try
            {
                UnityEngine.Object.DestroyImmediate(inherited.action);
                inherited.action = UnityEngine.Object.Instantiate(gun.reload); inherited.action.hideFlags = HideFlags.HideAndDontSave;
                inherited.kind = global::CodeHelper.ProceduralShooterFramework.Equipment.EquipmentActionKind.Reload;
                inherited.handInsert.side = HandSide.Left; inherited.handInsert.template = LabHandTemplate.Pistol;
                inherited.handInsert.start = 0.25f; inherited.handInsert.end = 0.75f;
                string original = EditorJsonUtility.ToJson(inherited.action);
                preview.Evaluate(inherited, 0.9f);
                Vector3 after = preview.Equipment.handIK.leftHand.target.position;
                Quaternion rotation = preview.Equipment.handIK.leftHand.target.rotation;
                Vector3 weapon = preview.Equipment.weaponPose.PoseRoot.position;
                var generated = LabHandTemplateInsertion.Insert(inherited, preview);
                try
                {
                    Require(EditorJsonUtility.ToJson(inherited.action) == original, "inherited action not mutated");
                    Require(generated.action.characterClip == gun.reload.characterClip && generated.action.weaponClip == gun.reload.weaponClip, "original character and weapon clips retained");
                    Require(JsonUtility.ToJson(generated.action.rightHandMotion) == JsonUtility.ToJson(inherited.action.rightHandMotion), "inherited other hand recipe retained");
                    preview.EnableHand(HandSide.Left); preview.Evaluate(generated, 0.9f);
                    Require(Vector3.Distance(preview.Equipment.handIK.leftHand.target.position, after) < 0.002f && Quaternion.Angle(preview.Equipment.handIK.leftHand.target.rotation, rotation) < 0.2f, "inherited reload target preserved after insertion interval");
                    Require(Vector3.Distance(preview.Equipment.weaponPose.PoseRoot.position, weapon) < 0.001f, "inherited weapon presentation unchanged");
                }
                finally { LabAssetIO.DestroyDraft(generated); }
            }
            finally { preview.Slot.leftHandPose = left; preview.Slot.rightHandPose = right; LabAssetIO.DestroyDraft(inherited); }
        }
    }
}
