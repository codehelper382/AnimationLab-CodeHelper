using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    /// <summary>
    /// Un hueso del perfil. Se identifica por HumanBodyBones (rigs Humanoid)
    /// o por nombre (cualquier rig, colas, accesorios...).
    /// </summary>
    [Serializable]
    public class SpineLookElement
    {
        [Tooltip("Hueso Humanoid. Se ignora si 'customBone' tiene texto.")]
        public HumanBodyBones humanBone = HumanBodyBones.Spine;

        [Tooltip("Opcional: nombre exacto del hueso (rigs no humanoides, colas, etc.).")]
        public string customBone;

        [Tooltip("X = límite de input positivo | Y = límite de input negativo.\n" +
                 "Con PlayerLookController, pitch arriba usa Y y pitch abajo usa X; yaw derecha usa X.")]
        public Vector2 clampedAngle = new Vector2(30f, 30f);

        [Tooltip("El auto-reparto no toca este hueso y su ángulo no cuenta para el total.\n" +
                 "Útil para colas, mochilas, etc.")]
        public bool lockAngle;

        [Range(0f, 2f)] public float recoilFactor = 1f;

        [Tooltip("Rotación extra fija (en grados) aplicada a este hueso.")]
        public Vector3 offsetEuler;

        [Tooltip("Ignora el clip de bind pose: la referencia es siempre 'mirando al frente'.\n" +
                 "Ideal para cuello y cabeza.")]
        public bool ignoreClipSample;

        [HideInInspector] public Vector2 cachedAngle; // detecta qué valor editó el usuario

        public string DisplayName =>
            string.IsNullOrEmpty(customBone) ? humanBone.ToString() : customBone;
    }

    /// <summary>
    /// Perfil de mirada: QUÉ huesos giran y CUÁNTO (pitch / yaw / roll).
    /// Crea uno por arma o por personaje: Create → CodeHelper → Spine Look Profile.
    /// El comportamiento en runtime (alpha, suavizado, recoil) vive en ProceduralSpine.
    /// </summary>
    [CreateAssetMenu(fileName = "SpineLookProfile",
        menuName = "CodeHelper/Shooter Framework/Spine Look Profile")]
    public class SpineLookProfile : ScriptableObject
    {
        [Header("Auto-reparto de ángulos")]
        [Tooltip("Al editar el ángulo de un hueso, los huesos siguientes se reparten lo que falta\n" +
                 "para llegar al total de la lista.")]
        public bool autoDistribute = true;
        [Min(0f)] public float pitchTotal = 70f;
        [Min(0f)] public float yawTotal = 90f;
        [Min(0f)] public float rollTotal = 30f;

        [Header("Pitch (arriba / abajo)")]
        public List<SpineLookElement> pitchElements = new List<SpineLookElement>();

        [Header("Yaw (izquierda / derecha)")]
        public List<SpineLookElement> yawElements = new List<SpineLookElement>();

        [Header("Roll (inclinación lateral)")]
        public List<SpineLookElement> rollElements = new List<SpineLookElement>();

        // ─────────────────────────────────────────────────────────────────
        #region Valores por defecto (Humanoid)
        private static readonly HumanBodyBones[] LookBones =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head
        };
        private static readonly float[] PitchWeights = { 10f, 16f, 18f, 16f, 20f };
        private static readonly float[] YawWeights = { 12f, 16f, 18f, 16f, 28f };

        private static readonly HumanBodyBones[] RollBones =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest
        };
        private static readonly float[] RollWeights = { 12f, 16f, 18f };

        /// <summary>Perfil en memoria listo para usar con cualquier rig Humanoid.</summary>
        public static SpineLookProfile CreateHumanoidDefault()
        {
            var profile = CreateInstance<SpineLookProfile>();
            profile.name = "SpineLookProfile (Humanoid por defecto)";
            profile.FillHumanoidDefaults();
            return profile;
        }

        [ContextMenu("Fill Humanoid Defaults (reemplaza las listas)")]
        public void FillHumanoidDefaults()
        {
            pitchElements = BuildDefault(LookBones, PitchWeights, pitchTotal);
            yawElements = BuildDefault(LookBones, YawWeights, yawTotal);
            rollElements = BuildDefault(RollBones, RollWeights, rollTotal);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static List<SpineLookElement> BuildDefault(HumanBodyBones[] bones, float[] weights, float total)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += weights[i];

            var list = new List<SpineLookElement>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                float angle = weights[i] * total / sum;
                var e = new SpineLookElement
                {
                    humanBone = bones[i],
                    clampedAngle = new Vector2(angle, angle),
                    cachedAngle = new Vector2(angle, angle)
                };
                list.Add(e);
            }
            return list;
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Resolver huesos
        /// <summary>Busca el Transform real de un elemento. Devuelve null si no existe.</summary>
        public static Transform ResolveBone(Animator animator, Transform root, SpineLookElement e)
        {
            if (e == null) return null;

            if (!string.IsNullOrEmpty(e.customBone))
                return root != null ? FindByName(root, e.customBone) : null;

            if (animator != null && animator.isHuman && e.humanBone >= 0 && e.humanBone < HumanBodyBones.LastBone)
                return animator.GetBoneTransform(e.humanBone);

            return null;
        }

        private static Transform FindByName(Transform root, string boneName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == boneName) return all[i];
            return null;
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Auto-reparto (se ejecuta al editar en el Inspector)
        void OnValidate()
        {
            Process(pitchElements, pitchTotal);
            Process(yawElements, yawTotal);
            Process(rollElements, rollTotal);
        }

        private void Process(List<SpineLookElement> list, float total)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) list[i] = new SpineLookElement();
                Vector2 a = list[i].clampedAngle;
                list[i].clampedAngle = new Vector2(Mathf.Max(0f, a.x), Mathf.Max(0f, a.y));
            }

            if (autoDistribute) Distribute(list, total);

            for (int i = 0; i < list.Count; i++)
                list[i].cachedAngle = list[i].clampedAngle;
        }

        // Si el usuario cambió el hueso N, los huesos DESPUÉS de N se reparten
        // lo que queda (total - suma hasta N). Nunca produce ángulos negativos.
        private static void Distribute(List<SpineLookElement> list, float total)
        {
            for (int axis = 0; axis < 2; axis++) // 0 = X, 1 = Y
            {
                int changed = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!Mathf.Approximately(Get(list[i].clampedAngle, axis), Get(list[i].cachedAngle, axis)))
                    {
                        changed = i;
                        break;
                    }
                }
                if (changed < 0) continue;

                float used = 0f;
                for (int i = 0; i <= changed; i++)
                    if (!list[i].lockAngle) used += Get(list[i].clampedAngle, axis);

                int targets = 0;
                for (int i = changed + 1; i < list.Count; i++)
                    if (!list[i].lockAngle) targets++;
                if (targets == 0) continue;

                float each = Mathf.Max(0f, total - used) / targets;
                for (int i = changed + 1; i < list.Count; i++)
                {
                    if (list[i].lockAngle) continue;
                    Vector2 v = list[i].clampedAngle;
                    if (axis == 0) v.x = each; else v.y = each;
                    list[i].clampedAngle = v;
                }
            }
        }

        private static float Get(Vector2 v, int axis) => axis == 0 ? v.x : v.y;
        #endregion
    }
}
