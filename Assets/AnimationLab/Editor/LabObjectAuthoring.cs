using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabObjectAuthoring
    {
        internal static LabWeaponKey Sample(List<LabWeaponKey> keys, float time, Vector3 position, Vector3 euler)
        {
            if (keys.Count == 0) return new LabWeaponKey { time = time, position = position, euler = euler };
            return new LabWeaponKey { time = time, position = LabPathCurves.Objects(keys).Evaluate(time), euler = LabPathCurves.Objects(keys, true).Evaluate(time) };
        }

        internal static void Record(List<LabWeaponKey> keys, float time, Vector3 position, Vector3 euler)
        {
            var key = keys.FirstOrDefault(k => Mathf.Abs(k.time - time) < LabTimeline.TimeEpsilon);
            if (key == null) { key = new LabWeaponKey { time = time }; keys.Add(key); }
            key.position = position; key.euler = euler;
            keys.Sort((a, b) => a.time.CompareTo(b.time));
        }

        internal static List<LabWeaponKey> CloneKeys(List<LabWeaponKey> keys) => keys.Select(k => new LabWeaponKey { time = k.time, position = k.position, euler = k.euler, pathMode = k.pathMode, bend = k.bend, departureSpeed = k.departureSpeed, arrivalSpeed = k.arrivalSpeed, outgoing=k.outgoing, incoming=k.incoming,
            leftContact=CloneContact(k.leftContact),rightContact=CloneContact(k.rightContact),breakLeftContact=k.breakLeftContact,breakRightContact=k.breakRightContact }).ToList();
        private static LabPathContact CloneContact(LabPathContact c)=>c==null?null:new LabPathContact{active=c.active,position=c.position,euler=c.euler};

        internal static AnimationClip BakeParts(AnimationLabTake take, LabPreview preview)
        {
            var source = take.action.weaponClip;
            if (source != null && source.isHumanMotion) throw new InvalidOperationException("Las piezas necesitan un clip Generic, no Humanoid.");
            var clip = source != null ? UnityEngine.Object.Instantiate(source) : new AnimationClip();
            clip.name = "AuthoredWeaponParts"; clip.frameRate = 60;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            float duration = source != null && source.length > 0 ? source.length : Mathf.Max(0.01f, take.action.duration);
            try
            {
                foreach (var track in take.parts.Where(t => t.keys.Count > 0))
                {
                    if (preview.Objects.ResolvePart(track.path) == null) throw new InvalidOperationException("Pieza no encontrada o ambigua: " + track.path);
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.path == track.path && b.type == typeof(Transform) &&
                        (b.propertyName.StartsWith("m_LocalPosition.") || b.propertyName.StartsWith("m_LocalRotation.") || b.propertyName.StartsWith("localEulerAngles"))))
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    var positions = LabPathCurves.Objects(track.keys);
                    var rotations = LabPathCurves.Objects(track.keys, true);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(track.path, typeof(Transform), "m_LocalPosition." + "xyz"[axis]), LabPathCurves.Seconds(new[] { positions.x, positions.y, positions.z }[axis], duration));
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(track.path, typeof(Transform), "localEulerAnglesRaw." + "xyz"[axis]), LabPathCurves.Seconds(new[] { rotations.x, rotations.y, rotations.z }[axis], duration));
                    }
                    // Ensure the clip lasts the whole action even when authored keys stop before the end.
                    var endCurve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(track.path, typeof(Transform), "m_LocalPosition.x"));
                    if (endCurve.length > 0 && endCurve.keys[endCurve.length - 1].time < duration)
                    {
                        endCurve.AddKey(new Keyframe(duration, endCurve.Evaluate(duration), 0, 0));
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(track.path, typeof(Transform), "m_LocalPosition.x"), endCurve);
                    }
                }
                return clip;
            }
            catch { UnityEngine.Object.DestroyImmediate(clip); throw; }
        }

        internal static void ExportProp(LabProp prop, string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject(prop.name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            string materialPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(path, ".mat"));
            bool materialCreated = false;
            Material material = null;
            try
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath); materialCreated = true;
                for (int i = 0; i < prop.shapes.Count; i++) LabObjectPreview.CreateShape(root.transform, prop.shapes[i], i, material);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new InvalidOperationException("No se pudo guardar el prefab.");
                EditorGUIUtility.PingObject(prefab);
            }
            catch
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                if (materialCreated) AssetDatabase.DeleteAsset(materialPath);
                throw;
            }
            finally
            {
                if (material != null && !AssetDatabase.Contains(material)) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        internal static LabGripPreset SaveGripType(string name, string category, HandGripToolkit.HandSide side, HandGripToolkit.HandPoseData pose, string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var asset = ScriptableObject.CreateInstance<LabGripPreset>(); asset.displayName = name; asset.category = category; asset.side = side;
            asset.fingers = UnityEngine.Object.Instantiate(pose); asset.fingers.hideFlags = HideFlags.None; asset.fingers.name = "FingerPose";
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    AssetDatabase.CreateAsset(asset, path); AssetDatabase.AddObjectToAsset(asset.fingers, asset);
                    EditorUtility.SetDirty(asset); EditorUtility.SetDirty(asset.fingers); AssetDatabase.SaveAssets();
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.SaveAssets(); return asset;
            }
            catch
            {
                if (AssetDatabase.Contains(asset)) AssetDatabase.DeleteAsset(path);
                else { UnityEngine.Object.DestroyImmediate(asset.fingers); UnityEngine.Object.DestroyImmediate(asset); }
                throw;
            }
        }
    }
}
