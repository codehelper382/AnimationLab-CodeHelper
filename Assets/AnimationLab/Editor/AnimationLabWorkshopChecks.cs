using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabWorkshopChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar piezas, diseños y agarres")]
        public static void Run()
        {
            string scene = File.ReadAllText(LabPreview.ScenePath);
            int scenes = EditorSceneManager.previewSceneCount;
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode))) TestWorkshop(mode);
            Require(File.ReadAllText(LabPreview.ScenePath) == scene && EditorSceneManager.previewSceneCount == scenes, "source scene preserved and previews disposed");
            Debug.Log("[AnimationLabWorkshopChecks] PASS: actual weapon parts, coordinated keys, local/global, camera navigation, source-preserving bake, prototype geometry/prefab, grip presets and approximation.");
        }

        public static void RunAll() { Run(); AnimationLabAuthoringChecks.RunAll(); }

        private static void TestWorkshop(SpineDriveMode mode)
        {
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            AnimationLabTake saved = null;
            AnimationLabTake propSaved = null;
            AnimationClip source = null;
            string sourcePath = null, prefabPath = null, gripPath = null;
            try
            {
                LabPreview.Set(window, "mode", mode); LabPreview.Call(window, "StartPreview");
                var preview = (LabPreview)LabPreview.Get(window, "preview");
                LabPreview.Call(window, "NewDraft", 0);
                var take = (AnimationLabTake)LabPreview.Get(window, "draft");
                string definition = EditorJsonUtility.ToJson(preview.Slot.definition);
                string[] ids = (string[])LabPreview.Call(window, "ObjectIds");
                Debug.Log("[AnimationLabWorkshopChecks] Available parts: " + string.Join(", ", ids));
                string magazine = ids.FirstOrDefault(p => p.ToLowerInvariant().Contains("mag"));
                string bolt = ids.FirstOrDefault(p => p.ToLowerInvariant().Contains("bolt") || p.ToLowerInvariant().Contains("charg"));
                Require(magazine != null, "fixture has a separately selectable magazine");
                Require(bolt != null, "fixture has a separately selectable bolt");
                string path = magazine.Substring(5);
                var part = preview.Objects.ResolvePart(path);
                Vector3 initial = part.localPosition;
                source = new AnimationClip { name = "Workshop source" };
                string other = bolt.Substring(5);
                float otherPosition = preview.Objects.ResolvePart(other).localPosition.x;
                AnimationUtility.SetEditorCurve(source, EditorCurveBinding.FloatCurve(other, typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, otherPosition, 1, otherPosition + 0.001f));
                sourcePath = AssetDatabase.GenerateUniqueAssetPath(LabAssetIO.Output + "/__WorkshopSource.anim");
                AssetDatabase.CreateAsset(source, sourcePath);
                take.action.weaponClip = source;
                LabPreview.Call(window, "SelectObject", magazine);
                LabPreview.Call(window, "BeginObjectEdit", true);
                Require((bool)LabPreview.Call(window, "CommitEdits", true, true), "record first magazine pose");
                LabPreview.Call(window, "Seek", 0.5f);
                LabPreview.Set(window, "followHands", 1);
                LabPreview.Set(window, "wristReplacementApproved", true); // Explicit affirmative confirmation, without modal UI in batch.
                LabPreview.Call(window, "BeginObjectEdit", true);
                Vector3 offset = part.parent.InverseTransformVector(Vector3.right * 0.02f);
                LabPreview.Set(window, "keyPosition", initial + offset);
                LabPreview.Set(window, "keyRotation", new Vector3(0, 0, -8));
                LabPreview.Call(window, "EvaluateNow", true);
                var desired = preview.Equipment.handIK.leftHand.target.position;
                Require((bool)LabPreview.Call(window, "CommitEdits", true, true), "record grouped magazine and hand");
                Require(take.parts[0].keys.Count == 2 && take.left.Any(k => !k.fingersOnly && Mathf.Abs(k.time - 0.5f) < 0.00001f && k.fingers != null), "piece, wrist and fingers share timestamp");
                Require(Vector3.Distance(preview.Equipment.handIK.leftHand.target.position, desired) < 0.002f, "grouped pose survives recording");
                saved = LabAssetIO.Save(take, preview, "__WorkshopTake");
                Require(saved.action.weaponClip != source && saved.sourceWeaponClip == source, "bake preserves original weapon source");
                Require(AnimationUtility.GetCurveBindings(saved.action.weaponClip).Any(b => b.path == other && b.propertyName == "m_LocalPosition.x"), "other source part channel preserved");
                var editable = LabAssetIO.Clone(saved);
                try
                {
                    Require(editable.action.weaponClip == source && editable.parts[0] != saved.parts[0], "reopen restores source and deep-copies tracks");
                    preview.Evaluate(take, 0.5f);
                    Vector3 expected = part.position; Quaternion rotation = part.rotation;
                    editable.parts.Clear(); editable.action.weaponClip = saved.action.weaponClip;
                    preview.Evaluate(editable, 0.5f);
                    Require(Vector3.Distance(part.position, expected) < 0.0001f && Quaternion.Angle(part.rotation, rotation) < 0.1f, "exported part clip matches preview");
                    bool blendParts = preview.Slot.blendWeaponClip;
                    float incoming = take.action.blendIn, outgoing = take.action.blendOut;
                    try
                    {
                        preview.Slot.blendWeaponClip = true;
                        take.action.blendIn = take.action.blendOut = editable.action.blendIn = editable.action.blendOut = 0.4f;
                        foreach (float t in new[] { 0.05f, 0.95f })
                        {
                            preview.Evaluate(take, t); expected = part.position; rotation = part.rotation;
                            preview.Evaluate(editable, t);
                            Require(Vector3.Distance(part.position, expected) < 0.0001f && Quaternion.Angle(part.rotation, rotation) < 0.1f, "part bake matches blended preview at endpoints");
                        }
                    }
                    finally { preview.Slot.blendWeaponClip = blendParts; take.action.blendIn = incoming; take.action.blendOut = outgoing; }
                }
                finally { LabAssetIO.DestroyDraft(editable); }
                TestGlobalAndNavigation(window, preview);
                LabPreview.Call(window, "SelectTrack", 5, 0.5f);
                LabPreview.Call(window, "CreateProp");
                var prop = take.props.Single();
                prop.shapes.Add(new LabPrimitive { type = PrimitiveType.Cylinder, position = Vector3.up * 0.1f, size = new Vector3(0.03f, 0.2f, 0.03f) });
                LabPreview.Call(window, "SyncSelection");
                var root = preview.Objects.ResolveProp(prop.id);
                Require(root != null && root.childCount == 2 && root.GetComponentsInChildren<Collider>().Length == 0, "composite reference shapes created without gameplay colliders");
                Require(Mathf.Abs(root.GetChild(1).localScale.y - 0.1f) < 0.00001f, "cylinder uses full requested height");
                Require(LabGripTools.SignedDistance(root, prop, root.TransformPoint(Vector3.zero)) < 0, "primitive distance detects interior");
                Require(LabGripTools.SignedDistance(root, prop, root.TransformPoint(Vector3.one)) > 0, "primitive distance detects exterior");
                prefabPath = LabAssetIO.Output + "/__WorkshopDesign_" + Guid.NewGuid().ToString("N") + ".prefab";
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                bool wasDirty = activeScene.isDirty;
                LabObjectAuthoring.ExportProp(prop, prefabPath);
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform.childCount == 2, "prototype exported as static prefab");
                Require(activeScene.isDirty == wasDirty, "prefab export leaves user scene dirty state unchanged");
                propSaved = LabAssetIO.Save(take, preview, "__WorkshopPropTake");
                string propPath = AssetDatabase.GetAssetPath(propSaved);
                AssetDatabase.ImportAsset(propPath, ImportAssetOptions.ForceUpdate);
                var persistedProp = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(propPath);
                Require(persistedProp.props.Count == 1 && persistedProp.props[0].shapes.Count == 2, "composite design survives native serialization/reimport");
                var clone = LabAssetIO.Clone(take);
                try { clone.props[0].shapes[0].size *= 2; Require(clone.props[0].shapes[0].size != prop.shapes[0].size, "prototype geometry cloned independently"); }
                finally { LabAssetIO.DestroyDraft(clone); }
                LabPreview.Call(window, "SelectTrack", 3, 0.5f);
                Transform wrist = preview.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Vector3 wristPosition = wrist.position; Quaternion wristRotation = wrist.rotation;
                for (int i = 0; i < LabGripTools.Names.Length; i++)
                {
                    var pose = LabGripTools.Preset(preview.Animator, HandSide.Right, i);
                    try { Require(pose.BoneCount > 0, "built-in grip has mapped fingers"); }
                    finally { UnityEngine.Object.DestroyImmediate(pose); }
                }
                Require(LabGripTools.Names.Length >= 14, "expanded professional tactical grip catalog available");
                var akGrip = LabGripTools.Preset(preview.Animator, HandSide.Right, 5);
                var seatSlap = LabGripTools.Preset(preview.Animator, HandSide.Right, 6);
                var boltHook = LabGripTools.Preset(preview.Animator, HandSide.Right, 7);
                var openGrip = LabGripTools.Open(preview.Animator, HandSide.Right);
                var triggerGrip = LabGripTools.Trigger(preview.Animator, HandSide.Right);
                try
                {
                    Require(akGrip.BoneCount > 0 && seatSlap.BoneCount > 0 && boltHook.BoneCount > 0, "tactical grips have mapped finger bones");
                    Require(openGrip.BoneCount > 0 && triggerGrip.BoneCount > 0, "natural open and trigger grips have mapped bones");
                    Require(LabGripTools.PoseDifference(akGrip, seatSlap) > 15f, "AK grip and seat slap have distinct anatomical shapes");
                    Require(LabGripTools.PoseDifference(boltHook, openGrip) > 20f, "bolt hook and open hand have distinct postures");
                    foreach (LabGuidedPhaseRole role in Enum.GetValues(typeof(LabGuidedPhaseRole)))
                    {
                        int suggested = LabGripTools.PresetForRole(role);
                        Require(suggested >= 0 && suggested < LabGripTools.Names.Length, "valid preset suggested for role " + role);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(akGrip);
                    UnityEngine.Object.DestroyImmediate(seatSlap);
                    UnityEngine.Object.DestroyImmediate(boltHook);
                    UnityEngine.Object.DestroyImmediate(openGrip);
                    UnityEngine.Object.DestroyImmediate(triggerGrip);
                }
                gripPath = LabAssetIO.Output + "/__WorkshopGrip_" + Guid.NewGuid().ToString("N") + ".asset";
                var custom = LabGripTools.Preset(preview.Animator, HandSide.Right, 2);
                try { LabObjectAuthoring.SaveGripType("Mi cargador", "Rectangular", HandSide.Right, custom, gripPath); }
                finally { UnityEngine.Object.DestroyImmediate(custom); }
                AssetDatabase.ImportAsset(gripPath, ImportAssetOptions.ForceUpdate);
                var grip = AssetDatabase.LoadAssetAtPath<LabGripPreset>(gripPath);
                Require(grip.category == "Rectangular" && grip.fingers != null && grip.fingers.BoneCount > 0, "custom grip type and embedded pose survive reimport");
                var fitted = LabGripTools.Fit(preview.Animator, HandSide.Right, root, prop, 0.002f, out int contacts);
                try
                {
                    Require(fitted.BoneCount > 0 && contacts >= 0 && contacts <= 5, "approximation returns an editable pose");
                    Require(Vector3.Distance(wrist.position, wristPosition) < 0.00001f && Quaternion.Angle(wrist.rotation, wristRotation) < 0.01f, "fitting leaves solved wrist untouched");
                }
                finally { UnityEngine.Object.DestroyImmediate(fitted); }
                Require(EditorJsonUtility.ToJson(preview.Slot.definition) == definition, "shared equipment unchanged");
                Debug.Log("[AnimationLabWorkshopChecks] Passed " + mode);
            }
            finally
            {
                LabPreview.Set(window, "dirty", false); LabPreview.Set(window, "previewingKey", false); LabPreview.Set(window, "fingersEdited", false);
                LabPreview.Set(window, "weaponPending", false); LabPreview.Set(window, "objectPending", false);
                UnityEngine.Object.DestroyImmediate(window);
                if (saved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
                if (propSaved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(propSaved));
                if (gripPath != null) AssetDatabase.DeleteAsset(gripPath);
                if (sourcePath != null) AssetDatabase.DeleteAsset(sourcePath);
                if (prefabPath != null) { AssetDatabase.DeleteAsset(prefabPath); AssetDatabase.DeleteAsset(Path.ChangeExtension(prefabPath, ".mat")); }
            }
        }

        private static void TestGlobalAndNavigation(AnimationLabWindow window, LabPreview preview)
        {
            LabPreview.Call(window, "SelectTrack", 0, 0.5f);
            var chain = preview.Equipment.handIK.leftHand;
            Vector3 desired = chain.target.position + Vector3.up * 0.01f;
            Quaternion rotation = chain.target.rotation;
            LabPreview.Call(window, "BeginPositionEdit");
            LabPreview.Call(window, "SetEditedWorldPose", desired, rotation);
            LabPreview.Call(window, "EvaluateNow", true);
            Require(Vector3.Distance(chain.target.position, desired) < 0.0001f, "world-space hand editing converts to local recipe correctly");
            LabPreview.Call(window, "SyncSelection");
            LabPreview.Call(window, "SelectTrack", 4, 0.5f);
            var holder = preview.Equipment.weaponPose.PoseRoot;
            desired = holder.position + Vector3.left * 0.01f; rotation = holder.rotation;
            LabPreview.Call(window, "BeginPositionEdit");
            LabPreview.Call(window, "SetEditedWorldPose", desired, rotation);
            LabPreview.Call(window, "EvaluateNow", true);
            Require(Vector3.Distance(holder.position, desired) < 0.0001f, "world-space weapon editing preserves the base holder pose");
            LabPreview.Call(window, "SyncSelection");
            Vector3 actor = preview.Equipment.look.bodyRoot.position;
            LabPreview.Set(window, "navigating", true);
            var keys = (HashSet<KeyCode>)LabPreview.Get(window, "navigationKeys"); keys.Add(KeyCode.W);
            LabPreview.Call(window, "AdvanceNavigation", 0.5f);
            Require(preview.pan.sqrMagnitude > 0 && preview.Equipment.look.bodyRoot.position == actor, "camera navigation moves view, not actor");
            LabPreview.Set(window, "navigating", false); keys.Clear();
        }

        private static void Require(bool condition, string text)
        { if (!condition) throw new InvalidOperationException("[AnimationLabWorkshopChecks] " + text); }
    }
}
