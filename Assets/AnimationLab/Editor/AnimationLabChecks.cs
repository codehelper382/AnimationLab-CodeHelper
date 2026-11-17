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
    public static class AnimationLabChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar laboratorio (aislado)")]
        public static void Run()
        {
            string sceneBefore = File.ReadAllText(LabPreview.ScenePath);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int previewsBefore = EditorSceneManager.previewSceneCount;
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode)))
            {
                using (var preview = new LabPreview(mode))
                {
                    Require(preview.Graph.GetOutputCount() == 1, "one character graph");
                    Require(!preview.Animator.fireEvents, "animation events explicitly disabled");
                    foreach (int slot in Enumerable.Range(0, preview.Equipment.slots.Length))
                    {
                        var take = LabAssetIO.Clone(null);
                        try
                        {
                            take.slot = slot; preview.Select(slot);
                            var definition = preview.Slot.definition;
                            take.equipmentId = definition.equipmentId;
                            var original = definition is FirearmDefinition gun ? gun.reload : ((MeleeDefinition)definition).attacks.FirstOrDefault();
                            string definitionBefore = EditorJsonUtility.ToJson(definition);
                            UnityEngine.Object.DestroyImmediate(take.action);
                            take.action = original != null ? UnityEngine.Object.Instantiate(original) : ScriptableObject.CreateInstance<EquipmentActionDefinition>();
                            take.action.hideFlags = HideFlags.HideAndDontSave;
                            take.kind = definition is FirearmDefinition ? EquipmentActionKind.Reload : EquipmentActionKind.MeleeAttack;
                            int ammo = preview.Equipment.Runtime.Current.TotalLoadedRounds;
                            uint commits = preview.Equipment.Runtime.Current.Action.CommitVersion;
                            preview.Evaluate(take, 0.3f, 0);
                            Vector3 wrist = preview.Animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                            Quaternion finger = preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).localRotation;
                            preview.Evaluate(take, 0.8f, 75);
                            preview.Evaluate(take, 0.3f, 0);
                            float drift = Vector3.Distance(wrist, preview.Animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                            Require(drift < 0.002f, "deterministic scrub " + mode + "/" + slot + " drift=" + drift);
                            Require(Quaternion.Angle(finger, preview.Animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).localRotation) < 0.5f, "repeatable fingers");
                            Require(preview.Equipment.Runtime.Current.TotalLoadedRounds == ammo && preview.Equipment.Runtime.Current.Action.CommitVersion == commits,
                                "no ammo or gameplay commits while scrubbing");
                            Require(EditorJsonUtility.ToJson(definition) == definitionBefore, "shared definition unchanged");
                            Debug.Log("[AnimationLabChecks] Scrub passed " + mode + "/" + slot);
                        }
                        finally { LabAssetIO.DestroyDraft(take); }
                    }
                }
            }
            TestSaveBakeAndExport();
            Require(File.ReadAllText(LabPreview.ScenePath) == sceneBefore, "saved scene unchanged");
            Require(EditorSceneManager.previewSceneCount == previewsBefore, "all preview scenes cleaned up");
            var after = EditorSceneManager.GetSceneManagerSetup();
            Require(after.Length == setup.Length && after.Select(s => s.path).SequenceEqual(setup.Select(s => s.path)), "user scenes preserved");
            Debug.Log("[AnimationLabChecks] PASS: both spine modes, all equipment, repeatable scrub, no gameplay commits, finger bake, save/load/export and cleanup.");
        }

        private static void TestSaveBakeAndExport()
        {
            AnimationLabTake take = null, saved = null, clone = null;
            using (var preview = new LabPreview())
            {
                try
                {
                    take = LabAssetIO.Clone(null); take.slot = 1; preview.Select(1); take.equipmentId = preview.Slot.definition.equipmentId;
                    take.action.duration = 0.5f; take.action.blendIn = take.action.blendOut = 0;
                    preview.EnableHand(HandSide.Right);
                    preview.Evaluate(take, 0);
                    var pose = ScriptableObject.CreateInstance<HandPoseData>(); pose.Side = HandSide.Right; pose.Capture(preview.Animator);
                    var bent = UnityEngine.Object.Instantiate(pose);
                    var entry = bent.GetEntry(FingerId.Index, 0); entry.localRotation *= Quaternion.Euler(0, 0, 25);
                    LabAssetIO.NormalizeFingerPose(pose, preview); LabAssetIO.NormalizeFingerPose(bent, preview);
                    take.right.Add(new LabHandKey { time = 0, fingers = pose });
                    take.right.Add(new LabHandKey { time = 0.5f, position = new Vector3(0, 0, 0.08f), fingers = bent });
                    take.right.Add(new LabHandKey { time = 1, fingers = UnityEngine.Object.Instantiate(pose) });
                    LabAssetIO.BuildMotion(take.right, take.action.rightHandMotion);
                    preview.Evaluate(take, 0.5f);
                    Quaternion expected = preview.Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).localRotation;
                    saved = LabAssetIO.Save(take, preview, "__ValidationTake");
                    Require(saved.action != take.action && saved.right[0].fingers != take.right[0].fingers, "save deep copies data");
                    Require(saved.action.characterClip != null && saved.action.characterClip.isHumanMotion, "humanoid finger clip baked: clip=" + saved.action.characterClip + ", bindings=" + (saved.action.characterClip != null ? AnimationUtility.GetCurveBindings(saved.action.characterClip).Length : -1));
                    Require(AnimationUtility.GetCurveBindings(saved.action.characterClip).Any(b => b.propertyName.StartsWith("RightHand.Index.")), "native finger muscle bindings");
                    clone = LabAssetIO.Clone(saved);
                    Require(clone.action != saved.action && clone.right[0].fingers != saved.right[0].fingers, "load isolates saved assets");
                    // Check the actual exported runtime clip, not the laboratory's direct finger override.
                    foreach (var key in clone.right) UnityEngine.Object.DestroyImmediate(key.fingers);
                    clone.right.Clear();
                    clone.action.characterClip = saved.action.characterClip;
                    clone.action.characterMask = saved.action.characterMask;
                    preview.Evaluate(clone, 0.5f);
                    float angle = Quaternion.Angle(expected, preview.Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).localRotation);
                    Require(angle < 3f, "runtime finger bake matches preview angle=" + angle);
                    // Recheck with fades enabled; laboratory and exported clip must apply them only once.
                    take.action.blendIn = saved.action.blendIn = clone.action.blendIn = 0.1f;
                    take.action.blendOut = saved.action.blendOut = clone.action.blendOut = 0.1f;
                    preview.Evaluate(take, 0.1f);
                    expected = preview.Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).localRotation;
                    preview.Evaluate(clone, 0.1f);
                    Require(Quaternion.Angle(expected, preview.Animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).localRotation) < 3f, "finger fade matches runtime");
                    Require(saved.hasSourceCharacterClip && saved.sourceCharacterClip == null, "original empty source retained for editable reload");
                    var edited = LabAssetIO.Clone(saved);
                    Require(edited.action.characterClip == null, "reopening restores unbaked source");
                    LabAssetIO.DestroyDraft(edited);
                    LabAssetIO.CaptureAnchors(take, preview);
                    var actor = UnityEngine.Object.Instantiate(preview.Equipment.look.bodyRoot.gameObject);
                    actor.name = "Another character";
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, preview.Scene);
                    try
                    {
                        var destination = actor.GetComponentInChildren<EquipmentSceneController>(true);
                        Vector3 originalGuard = preview.Slot.rightHandPose.guard.localPosition;
                        var calibration = take.anchors.First(a => a.role == "rightGuard"); calibration.position += Vector3.right * 0.02f;
                        AnimationLabTakeEditor.Apply(take, destination);
                        Require(preview.Slot.rightHandPose.guard.localPosition == originalGuard, "calibration never changes other actor");
                        Require(destination.slots[1].rightHandPose.guard.localPosition == calibration.position, "calibration applies by selected actor role");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                    string[] paths = LabAssetIO.ExportPaths(saved);
                    Require(paths.Contains(AssetDatabase.GetAssetPath(saved)), "take included in export");
                    Require(!paths.Any(p => p.StartsWith("Assets/KINEMATION/") || p.EndsWith(".fbx") || p.EndsWith(".unity")), "external scenes/models excluded");
                    string package = Path.GetFullPath("AnimationLab.Validation.unitypackage");
                    AssetDatabase.ExportPackage(paths, package, ExportPackageOptions.Default);
                    Require(new FileInfo(package).Length > 0, "portable package written");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        var texture = preview.Render(960, 640, false);
                        var previous = RenderTexture.active; RenderTexture.active = texture;
                        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                        File.WriteAllBytes("AnimationLab.Preview.png", image.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
                    }
                    Debug.Log("[AnimationLabChecks] Bake/save/export passed; runtime finger error=" + angle);
                }
                finally
                {
                    LabAssetIO.DestroyDraft(take); LabAssetIO.DestroyDraft(clone);
                    if (saved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
                }
            }
        }

        private static void Require(bool condition, string text)
        { if (!condition) throw new InvalidOperationException("[AnimationLabChecks] " + text); }
    }
}
