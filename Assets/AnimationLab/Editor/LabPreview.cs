using System;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace CodeHelper.AnimationLab.Editor
{
    // All reflection is contained in the editor adapter. No second character graph and no runtime Tick while scrubbing.
    internal sealed class LabPreview : IDisposable
    {
        internal const string ScenePath = "Assets/Scenes/AnimationLab.unity";
        internal Scene Scene;
        internal EquipmentSceneController Equipment;
        internal PlayableGraph Graph;
        internal Camera Camera;
        internal LabObjectPreview Objects;
        internal Vector3 pan, lastWeaponOffset, lastWeaponEuler;
        internal EquipmentSceneSlot Slot => Equipment.slots[Equipment.SelectedSlot];
        internal Animator Animator => Equipment.spine.animator;
        private RenderTexture texture;
        private Transform[] transforms;
        private Vector3[] positions, scales;
        private Quaternion[] rotations;
        internal Vector2 orbit = new Vector2(155f, 8f);
        internal float distance = 2.4f;
        private bool disposed;
        private EquipmentHandBinding[] originalLeft, originalRight;
        private EquipmentHandBinding[] authoredLeft, authoredRight;
        private readonly System.Collections.Generic.List<GameObject> createdGuards = new System.Collections.Generic.List<GameObject>();
        private AvatarMask excludedFingerMask, maskSource;
        private bool maskLeft, maskRight;

        internal LabPreview(SpineDriveMode mode = SpineDriveMode.LateUpdate)
        {
            try
            {
                Scene = EditorSceneManager.OpenPreviewScene(ScenePath);
                var roots = Scene.GetRootGameObjects();
                var controllers = roots.SelectMany(r => r.GetComponentsInChildren<EquipmentSceneController>(true)).ToArray();
                if (controllers.Length != 1)
                    throw new InvalidOperationException($"La escena de preview debe tener exactamente un EquipmentSceneController; encontrados {controllers.Length}. Revisa {ScenePath}.");
                Equipment = controllers[0];
                foreach (var root in roots)
                    foreach (var component in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject) != 0)
                            throw new InvalidOperationException("Falta un componente en " + component.name);
                var spine = Equipment.spine;
                Call(spine, "Awake");
                Call(Equipment.weaponPose, "Awake");
                Call(Equipment.weaponPose, "OnEnable");
                Call(Equipment.handIK, "Awake");
                Call(Equipment.look, "Awake");
                Equipment.look.ConnectCamera();
                if (!HandPoseData.IsHumanoidAnimator(Animator, out string reason)) throw new InvalidOperationException(reason);
                Animator.fireEvents = false;
                // A manually evaluated preview can render several poses in one Editor frame.
                // Do not reuse the skinned matrices cached for the scene's ordinary update.
                foreach (var root in roots)
                    foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        renderer.updateWhenOffscreen = true;
                        renderer.forceMatrixRecalculationPerRender = true;
                    }
                Animator.Rebind(); Animator.Update(0f);
                if (!(bool)Call(spine, "ResolveRuntimeBones")) throw new InvalidOperationException("No se resolvió la columna.");
                Call(spine, "CaptureBindPose");
                spine.inputSmoothTime = 0f;
                spine.enableEquipmentAnimation = true;
                Set(spine, "_activeMode", mode);
                if (!(bool)Call(spine, mode == SpineDriveMode.AnimationJob ? "TryBuildJobGraph" : "TryBuildEquipmentGraph"))
                    throw new InvalidOperationException("No se pudo crear el grafo de previsualización.");
                Set(spine, "_ready", true);
                Graph = (PlayableGraph)Get(spine, "_graph");
                Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                Equipment.readInput = Equipment.showControls = false;
                Call(Equipment, "OnEnable");
                Equipment.Step(100f);
                originalLeft = Equipment.slots.Select(s => CloneBinding(s.leftHandPose)).ToArray();
                originalRight = Equipment.slots.Select(s => CloneBinding(s.rightHandPose)).ToArray();
                authoredLeft = new EquipmentHandBinding[Equipment.slots.Length];
                authoredRight = new EquipmentHandBinding[Equipment.slots.Length];
                var cameraObject = new GameObject("Lab preview camera");
                SceneManager.MoveGameObjectToScene(cameraObject, Scene);
                Camera = cameraObject.AddComponent<Camera>();
                Camera.enabled = false;
                Camera.scene = Scene;
                Camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(Scene);
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                Camera.nearClipPlane = 0.01f;
                transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                positions = transforms.Select(t => t.localPosition).ToArray();
                rotations = transforms.Select(t => t.localRotation).ToArray();
                scales = transforms.Select(t => t.localScale).ToArray();
                Objects = new LabObjectPreview(this);
            }
            catch { Dispose(); throw; }
        }

        internal void Select(int slot)
        {
            var action = Equipment.Runtime.Current?.Action;
            if (action != null) Set(action, "<IsActive>k__BackingField", false);
            if (slot == Equipment.SelectedSlot) return;
            if (!Equipment.SelectSlot(slot)) throw new InvalidOperationException("No se pudo seleccionar el equipo.");
            Equipment.Step(100f);
        }

        internal void Evaluate(AnimationLabTake take, float progress, float pitch = 0, float yaw = 0, bool aim = false,
            HandPoseData leftOverride = null, HandPoseData rightOverride = null, bool rawFingers = false, Action beforeFinalIK = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabPreview));
            Select(take.slot);
            var e = Equipment;
            var state = e.Runtime.Current.Action;
            Call(e, "RemoveMotion");
            Objects.Prepare(take);
            Call(e.handIK, "Update");
            Set(state, "<Definition>k__BackingField", take.action);
            Set(state, "<Kind>k__BackingField", take.kind);
            Set(state, "<IsActive>k__BackingField", take.action != null);
            Set(state, "<Progress>k__BackingField", Mathf.Clamp01(progress));
            Set(e, "_idleTime", (double)(progress * Mathf.Max(0.001f, take.action != null ? take.action.duration : 1f)));
            e.look.SetLookAngles(pitch, e.look.bodyRoot.eulerAngles.y + yaw);
            Call(e.look, "ProcessLookFrame", Vector2.zero, 0f, aim, 0f);
            var originalMask = take.action != null ? take.action.characterMask : null;
            try
            {
                if (take.action != null && (take.left.Any(k => k.fingers != null) || take.right.Any(k => k.fingers != null)))
                    take.action.characterMask = FingerMask(originalMask != null ? originalMask : Slot.definition.characterMask,
                        take.left.Any(k => k.fingers != null), take.right.Any(k => k.fingers != null));
                Call(e, "Synchronize", 0f);
            }
            finally { if (take.action != null) take.action.characterMask = originalMask; }
            Call(e.spine, "Update");
            Call(e.weaponPose, "PreparePose", 1000f);
            Graph.Evaluate(0f);
            Call(e.spine, "LateUpdate");
            if (e.look.cameraRig != null) Call(e.look.cameraRig, "ApplyCamera", 1000f);
            Call(e.weaponPose, "ApplyPreparedPose", 1000f);
            e.ApplyMotion();
            lastWeaponOffset = take.action != null ? take.action.weaponPosition.Evaluate(progress) : Vector3.zero;
            lastWeaponEuler = take.action != null ? take.action.weaponRotation.Evaluate(progress) : Vector3.zero;
            Objects.Apply(take, progress);
            if (take.parts.Any(t => t.keys.Count > 0))
            {
                float weight = EquipmentSceneController.ActionWeight(take.action, progress);
                if ((bool)Get(e, "_usesLeftPose")) Call(Get(e, "_leftPose"), "Evaluate", take.action.leftHandMotion, progress, weight);
                if ((bool)Get(e, "_usesRightPose")) Call(Get(e, "_rightPose"), "Evaluate", take.action.rightHandMotion, progress, weight);
            }
            beforeFinalIK?.Invoke();
            Call(e.handIK, "LateUpdate");
            float fingerWeight = rawFingers ? 1f : EquipmentSceneController.ActionWeight(take.action, progress);
            ApplyFingers(take.left, progress, Animator, fingerWeight);
            ApplyFingers(take.right, progress, Animator, fingerWeight);
            leftOverride?.Apply(Animator);
            rightOverride?.Apply(Animator);
        }

        internal static void ApplyFingers(System.Collections.Generic.List<LabHandKey> keys, float progress, Animator animator, float weight)
        {
            var poses = keys.Where(k => k.fingers != null).OrderBy(k => k.time).ToArray();
            if (poses.Length == 0) return;
            LabHandKey before = poses[0], after = poses[poses.Length - 1];
            foreach (var key in poses) { if (key.time <= progress) before = key; if (key.time >= progress) { after = key; break; } }
            float blend = LabFingerKeyTools.Blend(before,after,progress);
            for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++)
            {
                var a = before.fingers.GetEntry((FingerId)f, j); var b = after.fingers.GetEntry((FingerId)f, j);
                if (a == null && b == null) continue;
                var bone = animator.GetBoneTransform((a ?? b).bone);
                if (bone == null) continue;
                Quaternion target = Quaternion.Slerp(a != null ? a.localRotation : bone.localRotation, b != null ? b.localRotation : bone.localRotation, blend);
                bone.localRotation = Quaternion.Slerp(bone.localRotation, target, weight);
            }
        }

        private AvatarMask FingerMask(AvatarMask source, bool left, bool right)
        {
            if (excludedFingerMask != null && maskSource == source && maskLeft == left && maskRight == right) return excludedFingerMask;
            if (excludedFingerMask != null) UnityEngine.Object.DestroyImmediate(excludedFingerMask);
            excludedFingerMask = source != null ? UnityEngine.Object.Instantiate(source) : new AvatarMask();
            excludedFingerMask.hideFlags = HideFlags.HideAndDontSave;
            if (left) excludedFingerMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
            if (right) excludedFingerMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);
            maskSource = source; maskLeft = left; maskRight = right;
            return excludedFingerMask;
        }

        private static EquipmentHandBinding CloneBinding(EquipmentHandBinding b) => b == null ? null : new EquipmentHandBinding
        { enabled = b.enabled, guard = b.guard, actionTarget = b.actionTarget, elbowHint = b.elbowHint, weight = b.weight };

        internal Transform Guard(HandSide side)
        {
            var binding = side == HandSide.Left ? Slot.leftHandPose : Slot.rightHandPose;
            return binding?.guard != null ? binding.guard : side == HandSide.Left ? Slot.leftGrip : Slot.rightGrip;
        }

        internal void EnableHand(HandSide side)
        {
            var binding = side == HandSide.Left ? Slot.leftHandPose : Slot.rightHandPose;
            if (binding?.guard != null && binding.enabled) return;
            var authored = side == HandSide.Left ? authoredLeft : authoredRight;
            if (authored[Equipment.SelectedSlot] != null)
            {
                binding = CloneBinding(authored[Equipment.SelectedSlot]);
                if (side == HandSide.Left) Slot.leftHandPose = binding; else Slot.rightHandPose = binding;
                return;
            }
            Transform guard = Guard(side);
            if (guard == null)
            {
                var go = new GameObject(side + " Lab Guard");
                createdGuards.Add(go);
                go.transform.SetParent(Equipment.weaponPose.PoseRoot, false);
                var wrist = Animator.GetBoneTransform(side == HandSide.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                go.transform.SetPositionAndRotation(wrist.position, wrist.rotation);
                guard = go.transform;
            }
            binding = new EquipmentHandBinding { guard = guard, weight = 1f, enabled = true };
            authored[Equipment.SelectedSlot] = CloneBinding(binding);
            if (side == HandSide.Left) Slot.leftHandPose = binding; else Slot.rightHandPose = binding;
        }

        // Revert only ownership, never calibrated transforms. Used after discard, last-key deletion and Undo.
        internal void RestoreHandBinding(HandSide side)
        {
            var authored = side == HandSide.Left ? authoredLeft : authoredRight;
            if (authored == null || authored[Equipment.SelectedSlot] == null) return;
            var originals = side == HandSide.Left ? originalLeft : originalRight;
            var current = side == HandSide.Left ? Slot.leftHandPose : Slot.rightHandPose;
            var original = originals[Equipment.SelectedSlot];
            if (current?.guard == original?.guard && current?.enabled == original?.enabled) return;
            if (side == HandSide.Left) Slot.leftHandPose = CloneBinding(original); else Slot.rightHandPose = CloneBinding(original);
        }

        internal RenderTexture Render(int width, int height, bool firstPerson)
        {
            width = Mathf.Clamp(width, 128, 1600); height = Mathf.Clamp(height, 128, 1000);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) { Camera.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
                texture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (firstPerson && Equipment.look.cameraRig?.realCamera != null)
            {
                Camera.transform.SetPositionAndRotation(Equipment.look.cameraRig.realCamera.position, Equipment.look.cameraRig.realCamera.rotation);
                var original = Equipment.look.cameraRig.realCamera.GetComponent<Camera>();
                Camera.fieldOfView = original != null ? original.fieldOfView : 60;
                Camera.cullingMask = original != null ? original.cullingMask : -1;
            }
            else
            {
                Vector3 center = Equipment.spine.animationRoot.position + Vector3.up * 1.35f + pan;
                Camera.transform.rotation = Quaternion.Euler(orbit.y, orbit.x, 0);
                Camera.transform.position = center - Camera.transform.forward * distance;
                Camera.fieldOfView = 45;
                Camera.cullingMask = -1;
            }
            Camera.targetTexture = texture;
            Camera.Render();
            return texture;
        }

        internal void RestoreAnchors()
        {
            if (transforms == null) return;
            Call(Equipment, "RemoveMotion");
            for (int i = 0; i < Equipment.slots.Length; i++)
            { Equipment.slots[i].leftHandPose = CloneBinding(originalLeft[i]); Equipment.slots[i].rightHandPose = CloneBinding(originalRight[i]); }
            foreach (var go in createdGuards) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            createdGuards.Clear();
            if (authoredLeft != null) Array.Clear(authoredLeft, 0, authoredLeft.Length);
            if (authoredRight != null) Array.Clear(authoredRight, 0, authoredRight.Length);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null) { transforms[i].SetLocalPositionAndRotation(positions[i], rotations[i]); transforms[i].localScale = scales[i]; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (Equipment != null) Call(Equipment, "OnDisable"); }
            finally
            {
                try { if (Equipment?.spine != null) Call(Equipment.spine, "TearDown"); }
                finally
                {
                    if (Camera != null) Camera.targetTexture = null;
                    if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
                    if (excludedFingerMask != null) UnityEngine.Object.DestroyImmediate(excludedFingerMask);
                    Objects?.Dispose();
                    if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
                }
            }
        }

        internal static object Call(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null) throw new MissingMethodException(target.GetType().Name, name);
            return method.Invoke(target, args);
        }
        internal static object Get(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(target);
            var prop = target.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(target);
            throw new MissingFieldException(target.GetType().Name, name);
        }
        internal static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
    }
}
