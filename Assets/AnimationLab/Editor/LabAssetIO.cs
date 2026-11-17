using System;
using System.Collections.Generic;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Equipment;
using HandGripToolkit;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabAssetIO
    {
        internal const string Output = "Assets/AnimationLab/Authored";

        internal static AnimationLabTake Clone(AnimationLabTake source)
        {
            var take = ScriptableObject.CreateInstance<AnimationLabTake>();
            if (source != null)
            {
                take.slot = source.slot; take.equipmentId = source.equipmentId; take.kind = source.kind;
                take.hasSourceCharacterClip = source.hasSourceCharacterClip; take.sourceCharacterClip = source.sourceCharacterClip;
                take.sourceCharacterMask = source.sourceCharacterMask;
                take.hasSourceWeaponClip = source.hasSourceWeaponClip; take.sourceWeaponClip = source.sourceWeaponClip;
                take.left = CloneKeys(source.left); take.right = CloneKeys(source.right);
                take.leftContactMotion=source.leftContactMotion;take.rightContactMotion=source.rightContactMotion;
                take.weapon = LabObjectAuthoring.CloneKeys(source.weapon);
                take.weaponDynamics=source.weaponDynamics!=null?JsonUtility.FromJson<LabWeaponDynamicsRecipe>(JsonUtility.ToJson(source.weaponDynamics)):new LabWeaponDynamicsRecipe();
                take.reload = source.reload != null ? JsonUtility.FromJson<LabReloadRecipe>(JsonUtility.ToJson(source.reload)) : new LabReloadRecipe();
                take.handInsert = source.handInsert != null ? JsonUtility.FromJson<LabHandInsert>(JsonUtility.ToJson(source.handInsert)) : new LabHandInsert();
                take.guidedReload = LabGuidedReloadPlan.Clone(source.guidedReload);
                take.parts = source.parts.Select(t => new LabObjectTrack { path = t.path, keys = LabObjectAuthoring.CloneKeys(t.keys) }).ToList();
                take.props = source.props.Select(p => JsonUtility.FromJson<LabProp>(JsonUtility.ToJson(p))).ToList();
                take.anchors = source.anchors.Select(a => new LabAnchor { path = a.path, role = a.role, position = a.position, rotation = a.rotation }).ToList();
            }
            take.hideFlags = HideFlags.HideAndDontSave;
            take.action = source?.action != null ? UnityEngine.Object.Instantiate(source.action) : ScriptableObject.CreateInstance<EquipmentActionDefinition>();
            take.action.hideFlags = HideFlags.HideAndDontSave;
            take.action.name = "Action";
            if (source != null && source.hasSourceCharacterClip && AssetDatabase.Contains(source))
            { take.action.characterClip = source.sourceCharacterClip; take.action.characterMask = source.sourceCharacterMask; }
            if (source != null && source.hasSourceWeaponClip && AssetDatabase.Contains(source)) take.action.weaponClip = source.sourceWeaponClip;
            return take;
        }

        private static List<LabHandKey> CloneKeys(List<LabHandKey> keys) => keys.Select(k =>
        {
            var pose = k.fingers != null ? UnityEngine.Object.Instantiate(k.fingers) : null;
            if (pose != null) pose.hideFlags = HideFlags.HideAndDontSave;
            return new LabHandKey { time = k.time, position = k.position, euler = k.euler, fingers = pose, fingersOnly = k.fingersOnly, pathMode = k.pathMode, bend = k.bend, departureSpeed = k.departureSpeed, arrivalSpeed = k.arrivalSpeed, outgoing=k.outgoing, incoming=k.incoming, fingerBlend=k.fingerBlend, fingerBlendDuration=k.fingerBlendDuration,fingerLabel=k.fingerLabel };
        }).ToList();

        internal static void NormalizeFingerPose(HandPoseData pose, LabPreview preview)
        {
            // Arbitrary local finger twists are not representable by Humanoid muscles. Project recorded poses
            // through the avatar once, so the authoring pose and the exported muscle clip use the same limits.
            var bones = preview.Animator.GetComponentsInChildren<Transform>(true);
            var positions = bones.Select(t => t.localPosition).ToArray();
            var rotations = bones.Select(t => t.localRotation).ToArray();
            var scales = bones.Select(t => t.localScale).ToArray();
            try
            {
                pose.Apply(preview.Animator);
                using (var handler = new HumanPoseHandler(preview.Animator.avatar, preview.Animator.transform))
                {
                    var human = new HumanPose(); handler.GetHumanPose(ref human); handler.SetHumanPose(ref human);
                    pose.Capture(preview.Animator);
                }
            }
            finally
            {
                for (int i = 0; i < bones.Length; i++) { bones[i].SetLocalPositionAndRotation(positions[i], rotations[i]); bones[i].localScale = scales[i]; }
            }
        }

        internal static void DestroyDraft(AnimationLabTake take)
        {
            if (take == null || AssetDatabase.Contains(take)) return;
            foreach (var pose in (take.left??new List<LabHandKey>()).Concat(take.right??new List<LabHandKey>()).Where(k=>k!=null).Select(k => k.fingers).Where(p => p != null).Distinct())
                if (!AssetDatabase.Contains(pose)) UnityEngine.Object.DestroyImmediate(pose);
            if (take.action != null && !AssetDatabase.Contains(take.action)) UnityEngine.Object.DestroyImmediate(take.action);
            UnityEngine.Object.DestroyImmediate(take);
        }

        internal static void BuildMotion(List<LabHandKey> keys, EquipmentHandMotion motion)
        {
            keys = keys.Where(k => !k.fingersOnly).ToList();
            if (keys.Count == 0) return;
            keys.Sort((a, b) => a.time.CompareTo(b.time));
            motion.enabled = true;
            motion.motionClip = null;
            motion.reach = AnimationCurve.Constant(0, 1, 0);
            motion.position = LabPathCurves.Build(keys, k => k.time, k => k.position, k => k.pathMode, k => k.bend, false, departure: k => k.departureSpeed, arrival: k => k.arrivalSpeed, outgoing:k=>k.outgoing, incoming:k=>k.incoming);
            motion.rotation = LabPathCurves.Build(keys, k => k.time, k => k.euler, k => k.pathMode, k => k.bend, false, true, k => k.departureSpeed, k => k.arrivalSpeed);
        }

        internal static void BuildWeaponMotion(AnimationLabTake take,bool preserveDynamics=false)
        {
            if(!preserveDynamics&&take.weaponDynamics!=null)take.weaponDynamics.applied=false;
            var keys = take.weapon.OrderBy(k => k.time).ToArray();
            take.action.weaponPosition = LabPathCurves.Build(keys, k => k.time, k => k.position, k => k.pathMode, k => k.bend, false, departure: k => k.departureSpeed, arrival: k => k.arrivalSpeed, outgoing:k=>k.outgoing, incoming:k=>k.incoming);
            take.action.weaponRotation = LabPathCurves.Build(keys, k => k.time, k => k.euler, k => k.pathMode, k => k.bend, false, true, k => k.departureSpeed, k => k.arrivalSpeed);
        }

        internal static void CaptureAnchors(AnimationLabTake take, LabPreview preview)
        {
            take.anchors.Clear();
            var slot = preview.Slot;
            var points = new[] { slot.leftGrip, slot.rightGrip, slot.leftHint, slot.rightHint, slot.leftReloadTarget,
                slot.leftHandPose?.guard, slot.rightHandPose?.guard, slot.leftHandPose?.actionTarget,
                slot.rightHandPose?.actionTarget, slot.leftHandPose?.elbowHint, slot.rightHandPose?.elbowHint };
            string[] roles = { "leftGrip", "rightGrip", "leftHint", "rightHint", "leftReloadTarget", "leftGuard", "rightGuard", "leftAction", "rightAction", "leftElbow", "rightElbow" };
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null) take.anchors.Add(new LabAnchor { path = Path(points[i]), role = roles[i], position = points[i].localPosition, rotation = points[i].localRotation });
        }

        internal static string Path(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        internal static void ApplyAnchors(AnimationLabTake take, LabPreview preview)
        {
            var all = preview.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToDictionary(Path);
            foreach (var anchor in take.anchors)
                if (all.TryGetValue(anchor.path, out var t)) t.SetLocalPositionAndRotation(anchor.position, anchor.rotation);
                else Debug.LogWarning("[AnimationLab] Punto no encontrado: " + anchor.path);
        }

        internal static AnimationLabTake Save(AnimationLabTake draft, LabPreview preview, string name, bool bakeFingers = true)
        {
            if (bakeFingers) LabTakeValidation.RequireValid(draft);
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Escribe un nombre para la toma.");
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/AnimationLab", "Authored");
            CaptureAnchors(draft, preview);
            var saved = Clone(draft);
            saved.hasSourceCharacterClip = true;
            saved.sourceCharacterClip = draft.action.characterClip;
            saved.sourceCharacterMask = draft.action.characterMask;
            saved.hasSourceWeaponClip = true; saved.sourceWeaponClip = draft.action.weaponClip;
            saved.name = name;
            string path = AssetDatabase.GenerateUniqueAssetPath(Output + "/" + name + ".asset");
            AnimationClip baked = null;
            AnimationClip partsClip = null;
            AvatarMask mask = null;
            try
            {
                if (bakeFingers && saved.parts.Any(t => t.keys.Count > 0))
                { partsClip = LabObjectAuthoring.BakeParts(draft, preview); saved.action.weaponClip = partsClip; }
                if (bakeFingers && saved.left.Concat(saved.right).Any(k => k.fingers != null))
                {
                    baked = BakeFingers(draft, preview);
                    saved.action.characterClip = baked;
                    var originalMask = saved.action.characterMask != null ? saved.action.characterMask : preview.Slot.definition.characterMask;
                    mask = originalMask != null ? UnityEngine.Object.Instantiate(originalMask) : new AvatarMask();
                    mask.name = "FingerCompatibleMask";
                    if (saved.left.Any(k => k.fingers != null)) mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                    if (saved.right.Any(k => k.fingers != null)) mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                    saved.action.characterMask = mask;
                }
                // Keep imports suspended until every referenced transient object has a persistent local ID.
                AssetDatabase.StartAssetEditing();
                try
                {
                    saved.hideFlags = HideFlags.None;
                    AssetDatabase.CreateAsset(saved, path);
                    Add(saved.action, saved, "Action");
                    int i = 0;
                    foreach (var key in saved.left.Concat(saved.right))
                        if (key.fingers != null) Add(key.fingers, saved, "Fingers_" + i++);
                    if (baked != null) Add(baked, saved, "AuthoredFingers");
                    if (partsClip != null) Add(partsClip, saved, "AuthoredWeaponParts");
                    if (mask != null) Add(mask, saved, "CharacterMask");
                    foreach (var obj in saved.left.Concat(saved.right).Select(k => (UnityEngine.Object)k.fingers).Where(p => p != null).Concat(new UnityEngine.Object[] { saved, saved.action, baked, mask, partsClip }).Where(o => o != null))
                        EditorUtility.SetDirty(obj);
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.SaveAssets();
                return saved;
            }
            catch
            {
                if (AssetDatabase.Contains(saved)) AssetDatabase.DeleteAsset(path);
                else DestroyDraft(saved);
                if (baked != null && !AssetDatabase.Contains(baked)) UnityEngine.Object.DestroyImmediate(baked);
                if (mask != null && !AssetDatabase.Contains(mask)) UnityEngine.Object.DestroyImmediate(mask);
                if (partsClip != null && !AssetDatabase.Contains(partsClip)) UnityEngine.Object.DestroyImmediate(partsClip);
                throw;
            }
        }

        private static void Add(UnityEngine.Object obj, AnimationLabTake take, string name)
        {
            obj.hideFlags = HideFlags.None; obj.name = name;
            AssetDatabase.AddObjectToAsset(obj, take);
        }

        internal static AnimationClip BakeFingers(AnimationLabTake take, LabPreview preview)
        {
            // Preserve pre-existing body/arm animation; replace only authored finger muscle channels.
            var clip = take.action.characterClip != null ? UnityEngine.Object.Instantiate(take.action.characterClip) : new AnimationClip();
            clip.name = "AuthoredFingers";
            clip.frameRate = 60;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            bool left = take.left.Any(k => k.fingers != null), right = take.right.Any(k => k.fingers != null);
            var muscles = Enumerable.Range(0, HumanTrait.MuscleCount).Where(i => IsFinger(HumanTrait.MuscleName[i], left, right)).ToArray();
            var curves = muscles.Select(_ => new AnimationCurve()).ToArray();
            float duration = Mathf.Max(0.001f, take.action.duration);
            float clipDuration = take.action.characterClip != null && take.action.characterClip.length > 0 ? take.action.characterClip.length : duration;
            int samples = Mathf.Clamp(Mathf.CeilToInt(duration * 60), 1, 18000);
            var times=new SortedSet<float>(Enumerable.Range(0,samples+1).Select(n=>n/(float)samples));
            var leftSteps=new HashSet<float>();var rightSteps=new HashSet<float>();
            foreach(var keys in new[]{take.left,take.right})
            {
                var poses=keys.Where(k=>k.fingers!=null).OrderBy(k=>k.time).ToArray();
                for(int i=0;i<poses.Length;i++)
                {
                    var key=poses[i];times.Add(key.time);
                    if(i==0||key.fingerBlend==LabFingerBlend.Legacy)continue;
                    times.Add(Mathf.Max(poses[i-1].time,key.time-Mathf.Max(0,key.fingerBlendDuration)));
                    if(key.fingerBlend==LabFingerBlend.Step||key.fingerBlendDuration<=0)
                        (keys==take.left?leftSteps:rightSteps).Add(key.time);
                }
            }
            var sampleTimes=times.ToArray();
            try
            {
                using (var handler = new HumanPoseHandler(preview.Animator.avatar, preview.Animator.transform))
                {
                    var pose = new HumanPose();
                    foreach(float progress in sampleTimes)
                    {
                        preview.Evaluate(take, progress, rawFingers: true);
                        handler.GetHumanPose(ref pose);
                        for (int j = 0; j < muscles.Length; j++)
                        {
                            float time=progress*clipDuration;var frame=new Keyframe(time,pose.muscles[muscles[j]]);
                            if(curves[j].AddKey(frame)<0)
                            {
                                int index=Array.FindIndex(curves[j].keys,k=>k.time==time);
                                if(index>=0)curves[j].MoveKey(index,frame);
                            }
                        }
                    }
                }
                for (int j = 0; j < muscles.Length; j++)
                {
                    var steps=HumanTrait.MuscleName[muscles[j]].StartsWith("Left ")?leftSteps:rightSteps;
                    for(int i=0;i<curves[j].length;i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curves[j],i,AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curves[j],i,AnimationUtility.TangentMode.Linear);
                    }
                    var stepSeconds=new HashSet<float>(steps.Select(t=>t*clipDuration));var frameKeys=curves[j].keys;
                    for(int i=1;i<frameKeys.Length;i++)if(stepSeconds.Contains(frameKeys[i].time))
                    {
                        AnimationUtility.SetKeyRightTangentMode(curves[j],i-1,AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyLeftTangentMode(curves[j],i,AnimationUtility.TangentMode.Constant);
                    }
                    string[] parts = HumanTrait.MuscleName[muscles[j]].Split(new[] { ' ' }, 3);
                    string property = parts[0] + "Hand." + parts[1] + "." + parts[2];
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curves[j]);
                }
                if (!clip.isHumanMotion) throw new InvalidOperationException("El bake de dedos no produjo un clip Humanoid.");
                return clip;
            }
            catch { UnityEngine.Object.DestroyImmediate(clip); throw; }
        }

        private static bool IsFinger(string name, bool left, bool right) =>
            ((left && name.StartsWith("Left ")) || (right && name.StartsWith("Right "))) &&
            new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Any(f => name.Contains(" " + f + " "));

        internal static string[] ExportPaths(AnimationLabTake take)
        {
            var paths = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(take), true)
                .Where(p => p.StartsWith(Output + "/") || p.StartsWith("Assets/AnimationLab/Runtime/") ||
                    p.StartsWith("Assets/CODEHELPER/Runtime/") || p.StartsWith("Assets/CODEHELPER/HandGripToolkit/Runtime/")).ToList();
            // Scripts referenced indirectly by ScriptableObjects must keep their GUIDs on import.
            paths.AddRange(AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/AnimationLab/Runtime", "Assets/CODEHELPER/Runtime", "Assets/CODEHELPER/HandGripToolkit/Runtime" })
                .Select(AssetDatabase.GUIDToAssetPath));
            paths.AddRange(AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/AnimationLab/PortableEditor" }).Select(AssetDatabase.GUIDToAssetPath));
            return paths.Distinct().OrderBy(p => p).ToArray();
        }
    }
}
