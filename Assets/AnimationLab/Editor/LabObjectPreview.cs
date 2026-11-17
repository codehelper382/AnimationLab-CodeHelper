using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Owns only the isolated preview's transforms and prototype meshes. No graph, physics or gameplay.
    internal sealed class LabObjectPreview : IDisposable
    {
        private readonly LabPreview preview;
        private readonly Dictionary<Transform, (Vector3 p, Quaternion r)> rest = new Dictionary<Transform, (Vector3, Quaternion)>();
        private readonly Dictionary<string, Transform> props = new Dictionary<string, Transform>();
        private readonly Dictionary<string, string> signatures = new Dictionary<string, string>();
        private readonly List<Transform> touched = new List<Transform>();
        private Material material;
        internal LabObjectPreview(LabPreview preview)
        {
            this.preview = preview;
            foreach (var slot in preview.Equipment.slots)
                if (slot.visualRoot != null)
                    foreach (var t in slot.visualRoot.GetComponentsInChildren<Transform>(true)) rest[t] = (t.localPosition, t.localRotation);
        }

        internal Transform ResolvePart(string path)
        {
            var root = preview.Slot.visualRoot;
            if (root == null || string.IsNullOrEmpty(path)) return null; // Never animate the calibrated model root.
            var matches = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root && UnityEditor.AnimationUtility.CalculateTransformPath(t, root) == path).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        internal Transform ResolveProp(string id) => id != null && props.TryGetValue(id, out var t) ? t : null;

        internal void Prepare(AnimationLabTake take)
        {
            // Restore previous outputs before the source weapon clip samples. This also handles scrub backwards/deletion.
            foreach (var t in touched) if (t != null && rest.TryGetValue(t, out var pose)) t.SetLocalPositionAndRotation(pose.p, pose.r);
            touched.Clear();
            var ids = new HashSet<string>(take.props.Select(p => p.id));
            foreach (string id in props.Keys.Where(id => !ids.Contains(id)).ToArray()) RemoveProp(id);
            foreach (var prop in take.props)
            {
                if (string.IsNullOrEmpty(prop.id)) throw new InvalidOperationException("Objeto sin identidad. Crea otra copia del objeto.");
                string signature = prop.name + string.Join(";", prop.shapes.Select(s => JsonUtility.ToJson(s)));
                if (!props.ContainsKey(prop.id) || signatures[prop.id] != signature)
                {
                    RemoveProp(prop.id);
                    var root = new GameObject("LabProp_" + prop.id).transform;
                    root.SetParent(preview.Equipment.weaponPose.PoseRoot, false);
                    for (int i = 0; i < prop.shapes.Count; i++) CreateShape(root, prop.shapes[i], i, GetMaterial());
                    props[prop.id] = root; signatures[prop.id] = signature;
                }
                props[prop.id].SetLocalPositionAndRotation(prop.position, Quaternion.Euler(prop.euler));
            }
            foreach (var track in take.parts)
            {
                var t = ResolvePart(track.path);
                if (t != null && !rest.ContainsKey(t)) rest[t] = (t.localPosition, t.localRotation);
            }
        }

        internal void Touch(Transform t)
        {
            if (!rest.ContainsKey(t)) rest[t] = (t.localPosition, t.localRotation);
            if (!touched.Contains(t)) touched.Add(t);
        }

        internal void Apply(AnimationLabTake take, float time)
        {
            foreach (var track in take.parts)
            {
                if (track.keys.Count == 0) continue;
                var t = ResolvePart(track.path);
                if (t == null) continue;
                var pose = LabObjectAuthoring.Sample(track.keys, time, t.localPosition, t.localEulerAngles);
                float weight = preview.Slot.blendWeaponClip
                    ? global::CodeHelper.ProceduralShooterFramework.Equipment.EquipmentSceneController.ActionWeight(take.action, time) : 1;
                var baseline = rest[t];
                t.SetLocalPositionAndRotation(Vector3.Lerp(baseline.p, pose.position, weight), Quaternion.Slerp(baseline.r, Quaternion.Euler(pose.euler), weight));
                touched.Add(t);
            }
            foreach (var prop in take.props)
            {
                if (!props.TryGetValue(prop.id, out var root)) continue;
                var pose = LabObjectAuthoring.Sample(prop.keys, time, prop.position, prop.euler);
                root.SetLocalPositionAndRotation(pose.position, Quaternion.Euler(pose.euler));
            }
        }

        internal static GameObject CreateShape(Transform root, LabPrimitive shape, int index, Material material = null)
        {
            var go = GameObject.CreatePrimitive(shape.type);
            go.name = "Forma_" + index;
            go.transform.SetParent(root, false);
            go.transform.SetLocalPositionAndRotation(shape.position, Quaternion.Euler(shape.euler));
            Vector3 size = shape.size;
            if (shape.type == PrimitiveType.Cylinder) size.y *= 0.5f;
            go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider); // Reference geometry is not a live gameplay collider.
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private Material GetMaterial()
        {
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.color = new Color(0.35f, 0.7f, 0.9f);
            }
            return material;
        }

        private void RemoveProp(string id)
        {
            if (props.TryGetValue(id, out var t) && t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            props.Remove(id); signatures.Remove(id);
        }

        public void Dispose()
        {
            foreach (string id in props.Keys.ToArray()) RemoveProp(id);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
