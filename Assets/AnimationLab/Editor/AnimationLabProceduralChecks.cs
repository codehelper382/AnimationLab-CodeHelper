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
    public static class AnimationLabProceduralChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar recorridos y plantillas")]
        public static void Run()
        {
            string scene = File.ReadAllText(LabPreview.ScenePath);
            int count = EditorSceneManager.previewSceneCount;
            TestCurves();
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode))) { TestTemplates(mode); TestWizard(mode); }
            Require(File.ReadAllText(LabPreview.ScenePath) == scene && EditorSceneManager.previewSceneCount == count, "isolation and cleanup");
            Debug.Log("[AnimationLabProceduralChecks] PASS: legacy interpolation, arcs, smooth curves, all templates/both hands, sampled contacts, source preservation, save/reimport/recovery and part bake equivalence.");
        }

        public static void RunAll() { Run(); AnimationLabWorkshopChecks.RunAll(); }

        private static void TestCurves()
        {
            var keys = new List<LabWeaponKey>
            {
                new LabWeaponKey { time = 0, position = Vector3.zero },
                new LabWeaponKey { time = 1, position = Vector3.right }
            };
            Require(Vector3.Distance(LabObjectAuthoring.Sample(keys, 0.25f, Vector3.zero, Vector3.zero).position, Vector3.right * 0.25f) < 0.00001f, "legacy objects remain linear");
            var take = LabAssetIO.Clone(null);
            try
            {
                take.weapon = LabObjectAuthoring.CloneKeys(keys); LabAssetIO.BuildWeaponMotion(take);
                Require(Mathf.Abs(take.action.weaponPosition.Evaluate(0.25f).x - 0.15625f) < 0.00001f, "legacy weapon retains zero tangents");
                take.left.Add(new LabHandKey { time = 0 }); take.left.Add(new LabHandKey { time = 1, position = Vector3.right });
                LabAssetIO.BuildMotion(take.left, take.action.leftHandMotion);
                Require(Mathf.Abs(take.action.leftHandMotion.position.Evaluate(0.25f).x - 0.15625f) < 0.00001f, "legacy wrist retains zero tangents");
                keys[0].pathMode = LabPathMode.Arc; keys[0].bend = Vector3.up * 0.1f;
                var arc = LabObjectAuthoring.Sample(keys, 0.5f, Vector3.zero, Vector3.zero);
                Require(Vector3.Distance(arc.position, new Vector3(0.5f, 0.1f, 0)) < 0.00001f, "two-point arc has a spatial midpoint offset");
                var positions = LabPathCurves.Objects(keys);
                Require(positions.Evaluate(0) == Vector3.zero && positions.Evaluate(1) == Vector3.right, "arc hits endpoints");
                keys.Insert(1, new LabWeaponKey { time = 0.4f, position = new Vector3(0.4f, 0.3f, 0), pathMode = LabPathMode.Smooth });
                keys[0].pathMode = LabPathMode.Smooth;
                var smooth = LabPathCurves.Objects(keys);
                Require(Vector3.Distance(smooth.Evaluate(0.4f), keys[1].position) < 0.00001f, "smooth path hits middle key");
                Require(smooth.Evaluate(0.2f).y > 0.15f, "smooth path bends instead of merely easing along a line");
                take.parts.Add(new LabObjectTrack { path = "test", keys = keys });
                var clone = LabAssetIO.Clone(take);
                try
                {
                    clone.parts[0].keys[0].bend = Vector3.one; clone.reload.rounds = 8;
                    Require(take.parts[0].keys[0].bend != Vector3.one && take.reload.rounds != 8, "path and wizard metadata deep copied");
                }
                finally { LabAssetIO.DestroyDraft(clone); }
            }
            finally { LabAssetIO.DestroyDraft(take); }
        }

        private static void TestTemplates(SpineDriveMode mode)
        {
            using (var preview = new LabPreview(mode))
            {
                var take = LabAssetIO.Clone(null);
                AnimationLabTake saved = null, recovery = null;
                try
                {
                    take.slot = 0; preview.Select(0); take.equipmentId = preview.Slot.definition.equipmentId;
                    take.action.duration = 1; preview.Evaluate(take, 0);
                    string definition = EditorJsonUtility.ToJson(preview.Slot.definition);
                    int ammunition = preview.Equipment.Runtime.Current.TotalLoadedRounds;
                    uint commits = preview.Equipment.Runtime.Current.Action.CommitVersion;
                    var part = preview.Objects.ResolvePart("AK12Rig/Root/Mag");
                    var rightPose = LabGripTools.Preset(preview.Animator, HandSide.Right, 0);
                    take.right.Add(new LabHandKey { time = 0, fingersOnly = true, fingers = rightPose });
                    var originalWeapon = preview.Slot.definition is global::CodeHelper.ProceduralShooterFramework.Equipment.FirearmDefinition gun ? gun.reload.weaponClip : null;
                    take.action.weaponClip = originalWeapon;
                    take.action.leftIK = AnimationCurve.Constant(0, 1, 0);
                    take.action.rightIK = AnimationCurve.Constant(0, 1, 0.35f);
                    take.action.blendIn = take.action.blendOut = 1; // Deliberately overlap the attached phase.
                    take.reload = new LabReloadRecipe
                    {
                        objectId = "part:AK12Rig/Root/Mag", hasRest = true, hasContact = true, hasOutside = true,
                        restPosition = part.localPosition, restEuler = part.localEulerAngles,
                        contactPosition = part.InverseTransformPoint(preview.Equipment.handIK.leftHand.target.position),
                        contactEuler = (Quaternion.Inverse(part.rotation) * preview.Equipment.handIK.leftHand.target.rotation).eulerAngles,
                        outsidePosition = part.localPosition + part.parent.InverseTransformVector(Vector3.down * 0.08f),
                        outsideEuler = part.localEulerAngles + new Vector3(0, 0, 20), duration = 1.2f, rounds = 3
                    };
                    foreach (LabReloadTemplate template in Enum.GetValues(typeof(LabReloadTemplate)))
                    foreach (HandSide side in Enum.GetValues(typeof(HandSide)))
                    {
                        take.reload.template = template; take.reload.side = side;
                        string original = EditorJsonUtility.ToJson(take.action);
                        var generated = LabReloadGenerator.Generate(take, preview);
                        try
                        {
                            var hand = side == HandSide.Left ? generated.left : generated.right;
                            Require(hand.Count(k => !k.fingersOnly) >= 73 && hand.Count(k => k.fingers != null) >= 6, "template generates wrist and finger phases");
                            Require(EditorJsonUtility.ToJson(take.action) == original && take.parts.Count == 0, "generator leaves source draft untouched");
                            Require(generated.action.weaponClip == originalWeapon, "generator retains weapon source clip");
                            Require((side == HandSide.Left ? generated.action.leftIK : generated.action.rightIK).Evaluate(0.5f) == 1, "chosen hand takes full IK ownership");
                            Require(generated.action.blendIn <= generated.action.duration * 0.15f && generated.action.blendOut <= generated.action.duration * 0.15f, "long inherited fades end outside the contact interval");
                            if (side == HandSide.Left) Require(generated.right[0].fingers != rightPose && generated.right[0].fingers != null, "other hand preserved independently");
                            if (template == LabReloadTemplate.Magazine || template == LabReloadTemplate.Bolt || template == LabReloadTemplate.FireBolt)
                                foreach (float t in new[] { 0.25f, 0.5f, 0.75f })
                                {
                                    preview.Evaluate(generated, t);
                                    var chain = side == HandSide.Left ? preview.Equipment.handIK.leftHand : preview.Equipment.handIK.rightHand;
                                    var target = LabReloadGenerator.Resolve(preview, generated.reload);
                                    Require(Vector3.Distance(chain.target.position, target.TransformPoint(generated.reload.contactPosition)) < 0.001f, "sampled wrist follows rotated piece " + template + "/" + side + "/" + t);
                                    if (side == HandSide.Left && template == LabReloadTemplate.Magazine)
                                    {
                                        Require(chain.weight == 1, "inherited zero IK is replaced at playback");
                                        Require(Vector3.Distance(chain.hand.position, chain.target.TransformPoint(chain.positionOffset)) < 0.001f, "solved wrist reaches target, not only the procedural marker");
                                        Require(Quaternion.Angle(chain.hand.rotation, chain.target.rotation * Quaternion.Euler(chain.rotationOffsetEuler)) < 0.1f, "solved wrist rotation follows contact");
                                    }
                                }
                            if (template == LabReloadTemplate.Magazine && side == HandSide.Left)
                            {
                                preview.Evaluate(generated, 0.5f, pitch: 10, yaw: 15, aim: true);
                                CapturePreview(preview, "Magazine." + mode, false);
                                CapturePreview(preview, "Magazine.FirstPerson." + mode, true);
                                saved = LabAssetIO.Save(generated, preview, "__ProceduralCheck");
                                recovery = LabAssetIO.Save(generated, preview, "__ProceduralRecovery", false);
                                string path = AssetDatabase.GetAssetPath(saved);
                                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                                var persisted = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path);
                                Require(persisted.reload.hasContact && persisted.reload.rounds == 3 && persisted.parts[0].keys.Any(k => k.pathMode == LabPathMode.Arc), "wizard and path settings survive reimport");
                                Require(recovery.action.weaponClip == originalWeapon && recovery.parts.Count == 1, "recovery retains unbaked recipe");
                                var runtime = LabAssetIO.Clone(saved);
                                try
                                {
                                    runtime.parts.Clear(); runtime.action.weaponClip = saved.action.weaponClip;
                                    foreach (float t in new[] { 0f, 0.05f, 0.31f, 0.49f, 0.68f, 0.95f, 1f })
                                    {
                                        preview.Evaluate(generated, t); Vector3 position = part.position; Quaternion rotation = part.rotation;
                                        preview.Evaluate(runtime, t);
                                        Require(Vector3.Distance(part.position, position) < 0.0001f && Quaternion.Angle(part.rotation, rotation) < 0.1f, "curved part bake matches preview " + t);
                                    }
                                }
                                finally { LabAssetIO.DestroyDraft(runtime); }
                            }
                        }
                        finally { LabAssetIO.DestroyDraft(generated); }
                    }
                    Require(EditorJsonUtility.ToJson(preview.Slot.definition) == definition, "shared definition unchanged");
                    Require(preview.Equipment.Runtime.Current.TotalLoadedRounds == ammunition && preview.Equipment.Runtime.Current.Action.CommitVersion == commits, "templates do not commit gameplay or consume ammunition");
                    TestObjectVariants(take, preview);
                    TestLegacyOtherHand(take, preview);
                    Debug.Log("[AnimationLabProceduralChecks] Passed " + mode);
                }
                finally
                {
                    LabAssetIO.DestroyDraft(take);
                    if (saved != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(saved));
                    if (recovery != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(recovery));
                }
            }
        }

        private static void Require(bool condition, string text)
        { if (!condition) throw new InvalidOperationException("[AnimationLabProceduralChecks] " + text); }

        private static void TestLegacyOtherHand(AnimationLabTake take, LabPreview preview)
        {
            var binding = preview.Slot.leftHandPose;
            try
            {
                preview.Slot.leftHandPose = null;
                take.reload.side = HandSide.Right;
                foreach (bool disabling in new[] { true, false })
                {
                    take.kind = disabling ? global::CodeHelper.ProceduralShooterFramework.Equipment.EquipmentActionKind.Reload : global::CodeHelper.ProceduralShooterFramework.Equipment.EquipmentActionKind.Fire;
                    take.reload.template = disabling ? LabReloadTemplate.Bolt : LabReloadTemplate.Magazine;
                    preview.Evaluate(take, 0);
                    Require((bool)LabPreview.Get(preview.Equipment, "_usesLeftReloadTarget") == disabling, "fixture exercises both legacy route transitions");
                    bool blocked = false;
                    try { var unexpected = LabReloadGenerator.Generate(take, preview); LabAssetIO.DestroyDraft(unexpected); }
                    catch (InvalidOperationException ex) { blocked = ex.Message.Contains("izquierda depende"); }
                    Require(blocked, "changing action kind cannot silently enable or disable untouched legacy hand");
                }
            }
            finally { preview.Slot.leftHandPose = binding; }
        }

        private static void TestObjectVariants(AnimationLabTake take, LabPreview preview)
        {
            var prop = new LabProp { id = "ProceduralCylinder", name = "Tambor de referencia", position = new Vector3(0, -0.1f, 0) };
            prop.shapes.Add(new LabPrimitive { type = PrimitiveType.Cylinder, size = new Vector3(0.04f, 0.05f, 0.04f) }); take.props.Add(prop);
            foreach (string id in new[] { "part:AK12Rig/Root/Charge", "prop:" + prop.id })
            {
                take.reload.objectId = id; take.reload.side = HandSide.Left;
                take.reload.template = id.StartsWith("part:") ? LabReloadTemplate.Bolt : LabReloadTemplate.Revolver;
                preview.Evaluate(take, 0);
                var target = LabReloadGenerator.Resolve(preview, take.reload);
                var r = take.reload;
                r.restPosition = target.localPosition; r.restEuler = target.localEulerAngles;
                r.outsidePosition = target.localPosition + target.parent.InverseTransformVector(Vector3.back * 0.03f);
                r.outsideEuler = target.localEulerAngles + new Vector3(0, 0, 12);
                r.contactPosition = target.InverseTransformPoint(preview.Equipment.handIK.leftHand.target.position);
                r.contactEuler = (Quaternion.Inverse(target.rotation) * preview.Equipment.handIK.leftHand.target.rotation).eulerAngles;
                var generated = LabReloadGenerator.Generate(take, preview);
                try
                {
                    Require(LabReloadGenerator.Keys(generated, generated.reload).Count > 5, "actual charging handle and reference cylinder have editable phase keys");
                    preview.Evaluate(generated, 0.5f);
                    Require(LabReloadGenerator.Resolve(preview, generated.reload) != null, "generated alternate target resolves");
                    if (id.StartsWith("prop:")) Require(generated.parts.Count == 0 && generated.props[0].keys.Count > 0, "prototype generation never writes a weapon hierarchy path");
                }
                finally { LabAssetIO.DestroyDraft(generated); }
            }
        }

        private static void CapturePreview(LabPreview preview, string name, bool firstPerson)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var active = RenderTexture.active;
            var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = preview.Render(1000, 800, firstPerson);
                image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply();
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "opencode");
                File.WriteAllBytes(Path.Combine(folder, "AnimationLab." + name + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void TestWizard(SpineDriveMode mode)
        {
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            try
            {
                LabPreview.Set(window, "mode", mode); LabPreview.Call(window, "StartPreview");
                LabPreview.Call(window, "NewDraft", 0);
                var take = (AnimationLabTake)LabPreview.Get(window, "draft");
                var preview = (LabPreview)LabPreview.Get(window, "preview");
                take.reload.objectId = "part:AK12Rig/Root/Mag";
                LabPreview.Call(window, "PlaceReloadReference", 0);
                LabPreview.Call(window, "CaptureReloadReference", 0);
                Require(take.reload.hasRest && take.parts.Count == 0, "capturing rest does not create animation keys");
                LabPreview.Call(window, "PlaceReloadReference", 1);
                LabPreview.Set(window, "wristReplacementApproved", true);
                LabPreview.Call(window, "BeginPositionEdit");
                LabPreview.Set(window, "keyPosition", new Vector3(0.01f, -0.02f, 0.03f));
                LabPreview.Set(window, "keyRotation", new Vector3(0, 12, -5));
                LabPreview.Call(window, "CaptureReloadReference", 1);
                Require(take.reload.hasContact && take.left.Count == 0 && !(bool)LabPreview.Get(window, "previewingKey"), "capturing pending wrist consumes only reference, not wrist track");
                bool staleContactBlocked = false;
                try { LabPreview.Call(window, "CaptureReloadReference", 1); }
                catch (System.Reflection.TargetInvocationException ex) { staleContactBlocked = ex.InnerException is InvalidOperationException; }
                Require(staleContactBlocked, "contact capture requires active rest-placement context");
                LabPreview.Set(window, "wristReplacementApproved", true);
                LabPreview.Call(window, "PlaceReloadReference", 2);
                var target = preview.Objects.ResolvePart("AK12Rig/Root/Mag");
                var outside = take.reload.restPosition + target.parent.InverseTransformVector(Vector3.down * 0.07f);
                LabPreview.Set(window, "keyPosition", outside);
                LabPreview.Call(window, "EvaluateNow", true);
                var contact = target.TransformPoint(take.reload.contactPosition);
                Require(Vector3.Distance(preview.Equipment.handIK.leftHand.target.position, contact) < 0.0001f, "placing exterior uses captured contact");
                LabPreview.Call(window, "CaptureReloadReference", 2);
                Require(take.reload.hasOutside && take.parts.Count == 0 && take.left.Count == 0, "three references ready without authoring intermediate keys");
                Require(Vector3.Distance(take.reload.outsidePosition, outside) < 0.00001f, "exterior pose survives capture");
                var generated = LabReloadGenerator.Generate(take, preview);
                try { Require(generated.left.Any(k => !k.fingersOnly) && generated.parts.Count == 1, "captured UI references generate a take"); }
                finally { LabAssetIO.DestroyDraft(generated); }
            }
            finally
            {
                LabPreview.Set(window, "dirty", false); LabPreview.Set(window, "previewingKey", false);
                LabPreview.Set(window, "fingersEdited", false); LabPreview.Set(window, "objectPending", false);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
