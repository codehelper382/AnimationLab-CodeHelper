using System;
using System.Linq;
using HandGripToolkit;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal struct FingerPoseMuscles
        {
            public float stretch1;
            public float spread;
            public float stretch2;
            public float stretch3;

            public FingerPoseMuscles(float s1, float spread, float s2, float s3)
            {
                this.stretch1 = s1;
                this.spread = spread;
                this.stretch2 = s2;
                this.stretch3 = s3;
            }
        }

        internal struct HandGripDefinition
        {
            public string name;
            public FingerPoseMuscles thumb;
            public FingerPoseMuscles index;
            public FingerPoseMuscles middle;
            public FingerPoseMuscles ring;
            public FingerPoseMuscles little;

            public HandGripDefinition(string name, FingerPoseMuscles thumb, FingerPoseMuscles index, FingerPoseMuscles middle, FingerPoseMuscles ring, FingerPoseMuscles little)
            {
                this.name = name;
                this.thumb = thumb;
                this.index = index;
                this.middle = middle;
                this.ring = ring;
                this.little = little;
            }

            public FingerPoseMuscles GetFinger(int f)
            {
                switch (f)
                {
                    case 0: return thumb;
                    case 1: return index;
                    case 2: return middle;
                    case 3: return ring;
                    default: return little;
                }
            }
        }

        internal static class LabGripTools
        {
            internal static readonly string[] Names =
            {
            // Índices 0..4 conservados para compatibilidad estricta
            "Cuchillo · mango",
            "Hacha · agarre fuerte",
            "Cargador · rectangular",
            "Cerrojo · pinza",
            "Esfera · envolver",
            // Poses tácticas y de recarga profesional (Hito 4 / MW 2019)
            "Cargador AK · agarre curvo (Rock-and-Lock)",
            "Asentar cargador · golpe de palma (Seat Slap)",
            "Palanca de cerrojo · tirón de gancho (Bolt Hook)",
            "Cargador · sujeción en copa (Base)",
            "Guardamanos · apoyo táctico (C-Clamp / Cradle)",
            "Retén / Pasador · pinza de precisión (Precision Pinch)",
            "Puño táctico cerrado (Tactical Fist)",
            "Tránsito relajado (Relaxed Transit)",
            "Mano abierta natural (Natural Rest / Open)"
        };

            private static readonly HandGripDefinition[] Definitions =
            {
            // 0: Cuchillo · mango
            new HandGripDefinition("Cuchillo · mango",
                new FingerPoseMuscles(-0.35f, 0.25f, -0.65f, -0.7f),
                new FingerPoseMuscles(-0.65f, -0.1f, -0.75f, -0.8f),
                new FingerPoseMuscles(-0.75f, 0.0f, -0.85f, -0.88f),
                new FingerPoseMuscles(-0.8f, 0.1f, -0.88f, -0.9f),
                new FingerPoseMuscles(-0.82f, 0.2f, -0.9f, -0.92f)),

            // 1: Hacha · agarre fuerte
            new HandGripDefinition("Hacha · agarre fuerte",
                new FingerPoseMuscles(-0.45f, 0.15f, -0.75f, -0.8f),
                new FingerPoseMuscles(-0.75f, -0.15f, -0.88f, -0.92f),
                new FingerPoseMuscles(-0.82f, 0.0f, -0.92f, -0.95f),
                new FingerPoseMuscles(-0.85f, 0.15f, -0.95f, -0.95f),
                new FingerPoseMuscles(-0.88f, 0.25f, -0.95f, -0.95f)),

            // 2: Cargador · rectangular (STANAG / SMG)
            new HandGripDefinition("Cargador · rectangular",
                new FingerPoseMuscles(-0.15f, 0.45f, -0.35f, -0.4f),
                new FingerPoseMuscles(-0.45f, -0.1f, -0.6f, -0.55f),
                new FingerPoseMuscles(-0.58f, 0.0f, -0.72f, -0.7f),
                new FingerPoseMuscles(-0.62f, 0.12f, -0.75f, -0.72f),
                new FingerPoseMuscles(-0.65f, 0.2f, -0.78f, -0.75f)),

            // 3: Cerrojo · pinza
            new HandGripDefinition("Cerrojo · pinza",
                new FingerPoseMuscles(-0.45f, 0.35f, -0.65f, -0.7f),
                new FingerPoseMuscles(-0.55f, -0.15f, -0.75f, -0.7f),
                new FingerPoseMuscles(-0.2f, 0.1f, 0.25f, 0.35f),
                new FingerPoseMuscles(0.1f, 0.15f, 0.4f, 0.5f),
                new FingerPoseMuscles(0.2f, 0.25f, 0.55f, 0.65f)),

            // 4: Esfera · envolver
            new HandGripDefinition("Esfera · envolver",
                new FingerPoseMuscles(-0.25f, 0.5f, -0.45f, -0.5f),
                new FingerPoseMuscles(-0.35f, -0.2f, -0.45f, -0.45f),
                new FingerPoseMuscles(-0.38f, 0.0f, -0.48f, -0.48f),
                new FingerPoseMuscles(-0.4f, 0.18f, -0.5f, -0.5f),
                new FingerPoseMuscles(-0.42f, 0.32f, -0.52f, -0.52f)),

            // 5: Cargador AK · agarre curvo (Rock-and-Lock)
            new HandGripDefinition("Cargador AK · agarre curvo (Rock-and-Lock)",
                new FingerPoseMuscles(-0.2f, 0.5f, -0.35f, -0.35f),
                new FingerPoseMuscles(-0.48f, -0.12f, -0.68f, -0.62f),
                new FingerPoseMuscles(-0.65f, 0.0f, -0.78f, -0.75f),
                new FingerPoseMuscles(-0.7f, 0.12f, -0.82f, -0.8f),
                new FingerPoseMuscles(-0.72f, 0.22f, -0.85f, -0.82f)),

            // 6: Asentar cargador · golpe de palma (Seat Slap)
            new HandGripDefinition("Asentar cargador · golpe de palma (Seat Slap)",
                new FingerPoseMuscles(0.15f, 0.4f, 0.05f, 0.0f),
                new FingerPoseMuscles(0.12f, -0.12f, 0.08f, 0.05f),
                new FingerPoseMuscles(0.1f, -0.02f, 0.06f, 0.04f),
                new FingerPoseMuscles(0.08f, 0.08f, 0.05f, 0.03f),
                new FingerPoseMuscles(0.05f, 0.15f, 0.03f, 0.02f)),

            // 7: Palanca de cerrojo · tirón de gancho (Bolt Hook)
            new HandGripDefinition("Palanca de cerrojo · tirón de gancho (Bolt Hook)",
                new FingerPoseMuscles(-0.35f, 0.25f, -0.6f, -0.65f),
                new FingerPoseMuscles(-0.68f, -0.15f, -0.95f, -0.88f),
                new FingerPoseMuscles(-0.8f, 0.0f, -0.92f, -0.9f),
                new FingerPoseMuscles(-0.82f, 0.15f, -0.95f, -0.92f),
                new FingerPoseMuscles(-0.85f, 0.25f, -0.95f, -0.92f)),

            // 8: Cargador · sujeción en copa (Base)
            new HandGripDefinition("Cargador · sujeción en copa (Base)",
                new FingerPoseMuscles(-0.1f, 0.55f, -0.3f, -0.35f),
                new FingerPoseMuscles(-0.35f, -0.15f, -0.5f, -0.45f),
                new FingerPoseMuscles(-0.45f, 0.0f, -0.58f, -0.55f),
                new FingerPoseMuscles(-0.52f, 0.15f, -0.65f, -0.6f),
                new FingerPoseMuscles(-0.58f, 0.25f, -0.7f, -0.65f)),

            // 9: Guardamanos · apoyo táctico (C-Clamp / Cradle)
            new HandGripDefinition("Guardamanos · apoyo táctico (C-Clamp / Cradle)",
                new FingerPoseMuscles(0.05f, 0.55f, -0.35f, -0.4f),
                new FingerPoseMuscles(-0.42f, -0.15f, -0.6f, -0.6f),
                new FingerPoseMuscles(-0.55f, 0.0f, -0.7f, -0.7f),
                new FingerPoseMuscles(-0.62f, 0.12f, -0.76f, -0.75f),
                new FingerPoseMuscles(-0.68f, 0.22f, -0.8f, -0.78f)),

            // 10: Retén / Pasador · pinza de precisión (Precision Pinch)
            new HandGripDefinition("Retén / Pasador · pinza de precisión (Precision Pinch)",
                new FingerPoseMuscles(-0.5f, 0.4f, -0.7f, -0.75f),
                new FingerPoseMuscles(-0.6f, -0.2f, -0.8f, -0.75f),
                new FingerPoseMuscles(-0.45f, 0.08f, -0.5f, -0.5f),
                new FingerPoseMuscles(-0.4f, 0.18f, -0.45f, -0.45f),
                new FingerPoseMuscles(-0.35f, 0.28f, -0.4f, -0.4f)),

            // 11: Puño táctico cerrado (Tactical Fist)
            new HandGripDefinition("Puño táctico cerrado (Tactical Fist)",
                new FingerPoseMuscles(-0.55f, -0.1f, -0.85f, -0.88f),
                new FingerPoseMuscles(-0.85f, -0.12f, -0.95f, -0.95f),
                new FingerPoseMuscles(-0.9f, 0.0f, -0.98f, -0.98f),
                new FingerPoseMuscles(-0.92f, 0.12f, -0.98f, -0.98f),
                new FingerPoseMuscles(-0.95f, 0.22f, -0.98f, -0.98f)),

            // 12: Tránsito relajado (Relaxed Transit)
            new HandGripDefinition("Tránsito relajado (Relaxed Transit)",
                new FingerPoseMuscles(0.05f, 0.3f, -0.12f, -0.15f),
                new FingerPoseMuscles(0.12f, -0.08f, -0.05f, -0.1f),
                new FingerPoseMuscles(-0.08f, 0.0f, -0.22f, -0.25f),
                new FingerPoseMuscles(-0.22f, 0.1f, -0.38f, -0.4f),
                new FingerPoseMuscles(-0.35f, 0.22f, -0.5f, -0.52f)),

            // 13: Mano abierta natural (Natural Rest / Open)
            new HandGripDefinition("Mano abierta natural (Natural Rest / Open)",
                new FingerPoseMuscles(0.25f, 0.45f, 0.1f, 0.05f),
                new FingerPoseMuscles(0.35f, -0.15f, 0.25f, 0.2f),
                new FingerPoseMuscles(0.32f, 0.0f, 0.22f, 0.18f),
                new FingerPoseMuscles(0.28f, 0.15f, 0.18f, 0.15f),
                new FingerPoseMuscles(0.22f, 0.28f, 0.15f, 0.12f))
        };

            private static readonly float[][] Stretch =
            {
            new[] { -0.6f, -0.75f, -0.85f, -0.9f, -0.85f },
            new[] { -0.7f, -0.85f, -0.95f, -0.95f, -0.9f },
            new[] { -0.25f, -0.45f, -0.6f, -0.65f, -0.6f },
            new[] { -0.55f, -0.65f, 0.2f, 0.45f, 0.65f },
            new[] { -0.2f, -0.3f, -0.4f, -0.45f, -0.4f }
        };

            private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

            internal static float PoseDifference(HandPoseData a, HandPoseData b)
            {
                float max = 0;
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        var x = a.GetEntry((FingerId)f, j);
                        var y = b.GetEntry((FingerId)f, j);
                        if (x != null && y != null) max = Mathf.Max(max, Quaternion.Angle(x.localRotation, y.localRotation));
                    }
                return max;
            }

            internal static HandPoseData Preset(Animator animator, HandSide side, int index)
            {
                int safeIndex = Mathf.Clamp(index, 0, Definitions.Length - 1);
                return WithPose(animator, side, (handler, human, restore) =>
                {
                    ApplyGrip(ref human, side, Definitions[safeIndex]);
                    handler.SetHumanPose(ref human);
                });
            }

            internal static HandPoseData Open(Animator animator, HandSide side) => WithPose(animator, side, (handler, human, restore) =>
            {
                ApplyGrip(ref human, side, Definitions[13]); // Mano abierta natural
                handler.SetHumanPose(ref human);
            });

            internal static HandPoseData Trigger(Animator animator, HandSide side) => WithPose(animator, side, (handler, human, restore) =>
            {
                // Dedos envolviendo empuñadura pistola y dedo índice curvado sobre gatillo
                ApplyFingerMuscles(ref human, side, 0, new FingerPoseMuscles(-0.4f, 0.25f, -0.6f, -0.65f));
                ApplyFingerMuscles(ref human, side, 1, new FingerPoseMuscles(-0.7f, -0.1f, -0.85f, -0.6f));
                ApplyFingerMuscles(ref human, side, 2, new FingerPoseMuscles(-0.75f, 0.0f, -0.88f, -0.85f));
                ApplyFingerMuscles(ref human, side, 3, new FingerPoseMuscles(-0.78f, 0.1f, -0.9f, -0.88f));
                ApplyFingerMuscles(ref human, side, 4, new FingerPoseMuscles(-0.8f, 0.2f, -0.92f, -0.9f));
                handler.SetHumanPose(ref human);
            });

            internal static int PresetForRole(LabGuidedPhaseRole role)
            {
                switch (role)
                {
                    case LabGuidedPhaseRole.PreparePresent:
                        return 9; // Guardamanos · apoyo táctico
                    case LabGuidedPhaseRole.Reach:
                        return 12; // Tránsito relajado
                    case LabGuidedPhaseRole.Acquire:
                    case LabGuidedPhaseRole.Extract:
                    case LabGuidedPhaseRole.Insert:
                        return 5; // Cargador AK · agarre curvo (Rock-and-Lock)
                    case LabGuidedPhaseRole.Seat:
                        return 6; // Asentar cargador · golpe de palma (Seat Slap)
                    case LabGuidedPhaseRole.OperateMechanism:
                        return 7; // Palanca de cerrojo · tirón de gancho (Bolt Hook)
                    case LabGuidedPhaseRole.Supply:
                        return 8; // Cargador · sujeción en copa (Base)
                    case LabGuidedPhaseRole.Release:
                    case LabGuidedPhaseRole.Wait:
                        return 13; // Mano abierta natural
                    case LabGuidedPhaseRole.Recover:
                        return 12; // Tránsito relajado
                    default:
                        return 2; // Cargador · rectangular
                }
            }

            private static void ApplyFingerMuscles(ref HumanPose human, HandSide side, int finger, FingerPoseMuscles m)
            {
                string prefix = (side == HandSide.Left ? "Left " : "Right ") + FingerNames[finger] + " ";
                for (int i = 0; i < HumanTrait.MuscleCount; i++)
                {
                    string name = HumanTrait.MuscleName[i];
                    if (!name.StartsWith(prefix)) continue;
                    if (name.EndsWith("1 Stretched")) human.muscles[i] = m.stretch1;
                    else if (name.EndsWith("Spread")) human.muscles[i] = m.spread;
                    else if (name.EndsWith("2 Stretched")) human.muscles[i] = m.stretch2;
                    else if (name.EndsWith("3 Stretched")) human.muscles[i] = m.stretch3;
                }
            }

            private static void ApplyGrip(ref HumanPose human, HandSide side, HandGripDefinition grip)
            {
                for (int f = 0; f < 5; f++)
                    ApplyFingerMuscles(ref human, side, f, grip.GetFinger(f));
            }

            private static void SetStretch(ref HumanPose human, HandSide side, int finger, float stretch)
            {
                // Flexión progresiva anatómica entre articulaciones y splay natural
                float s1 = stretch * 0.85f;
                float s2 = stretch;
                float s3 = stretch * 1.05f;
                float spread = finger == 0
                    ? (stretch < 0 ? -0.2f * stretch : 0.2f * stretch)
                    : (finger == 1 ? -0.1f * stretch : finger == 2 ? 0f : (finger - 2) * 0.1f * stretch);
                ApplyFingerMuscles(ref human, side, finger, new FingerPoseMuscles(s1, spread, s2, s3));
            }

            private static HandPoseData WithPose(Animator animator, HandSide side, Action<HumanPoseHandler, HumanPose, Action> edit)
            {
                var bones = animator.GetComponentsInChildren<Transform>(true);
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var scales = bones.Select(t => t.localScale).ToArray();
                Action restore = () =>
                {
                    for (int i = 0; i < bones.Length; i++)
                    {
                        bones[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
                        bones[i].localScale = scales[i];
                    }
                };
                var result = ScriptableObject.CreateInstance<HandPoseData>();
                result.Side = side;
                result.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                    {
                        var human = new HumanPose();
                        handler.GetHumanPose(ref human);
                        edit(handler, human, restore);
                        result.Capture(animator);
                    }
                    return result;
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(result);
                    throw;
                }
                finally
                {
                    restore();
                }
            }

            // Geometric approximation, not force closure, universal retargeting or a mesh collision solver.
            // The wrist must already be placed around the object. Fingers close until their sampled segments approach a surface.
            internal static HandPoseData Fit(Animator animator, HandSide side, Transform target, LabProp prop, float clearance, out int contacts)
            {
                if (target == null) throw new InvalidOperationException("Selecciona una pieza o un objeto de referencia.");
                int found = 0;
                var renderers = target.GetComponentsInChildren<Renderer>(true);
                if (prop == null && renderers.Length == 0) throw new InvalidOperationException("Esta pieza no tiene geometría visible para estimar el contacto. Usa un objeto hecho con primitivas como referencia.");
                // Skinned bones often have no renderer of their own. Do not pretend their parent model bounds represent this part.
                Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(target.position, Vector3.zero);
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var result = WithPose(animator, side, (handler, human, restore) =>
                {
                    var trial = ScriptableObject.CreateInstance<HandPoseData>(); trial.Side = side;
                    try
                    {
                        for (int finger = 0; finger < 5; finger++)
                        {
                            float chosen = 1f;
                            for (int step = 0; step <= 24; step++)
                            {
                                float stretch = 1f - 2f * step / 24;
                                SetStretch(ref human, side, finger, stretch);
                                handler.SetHumanPose(ref human); trial.Capture(animator);
                                restore(); trial.Apply(animator); // Keep the solved wrist/body fixed while evaluating candidate fingers.
                                float distance = FingerDistance(animator, side, finger, p => prop != null ? SignedDistance(target, prop, p) : BoundsDistance(bounds, p));
                                if (distance <= clearance)
                                {
                                    found++; break; // Retain the previous, less penetrating sample.
                                }
                                chosen = stretch;
                            }
                            SetStretch(ref human, side, finger, chosen);
                            handler.SetHumanPose(ref human); trial.Capture(animator); restore(); trial.Apply(animator);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(trial); }
                });
                contacts = found;
                return result;
            }

            private static float FingerDistance(Animator animator, HandSide side, int finger, Func<Vector3, float> distance)
            {
                float minimum = float.PositiveInfinity;
                Transform[] joints = Enumerable.Range(0, 3).Select(j => animator.GetBoneTransform(HandBoneMap.GetBone(side, (FingerId)finger, j))).ToArray();
                for (int j = 0; j < 3; j++)
                {
                    if (joints[j] == null) continue;
                    Vector3 start = joints[j].position;
                    Vector3 end = j < 2 && joints[j + 1] != null ? joints[j + 1].position :
                        j > 0 && joints[j - 1] != null ? start + (start - joints[j - 1].position) * 0.8f : start;
                    for (int n = 0; n <= 4; n++) minimum = Mathf.Min(minimum, distance(Vector3.Lerp(start, end, n / 4f)));
                }
                return minimum;
            }

            internal static float SignedDistance(Transform root, LabProp prop, Vector3 world)
            {
                float result = float.PositiveInfinity;
                Vector3 local = root.InverseTransformPoint(world);
                foreach (var shape in prop.shapes)
                {
                    Vector3 p = Quaternion.Inverse(Quaternion.Euler(shape.euler)) * (local - shape.position);
                    Vector3 half = shape.size * 0.5f;
                    float d;
                    if (shape.type == PrimitiveType.Sphere)
                    {
                        Vector3 q = new Vector3(p.x / half.x, p.y / half.y, p.z / half.z);
                        d = (q.magnitude - 1) * Mathf.Min(half.x, Mathf.Min(half.y, half.z));
                    }
                    else if (shape.type == PrimitiveType.Cylinder)
                    {
                        float radial = (new Vector2(p.x / half.x, p.z / half.z).magnitude - 1) * Mathf.Min(half.x, half.z);
                        var q = new Vector2(radial, Mathf.Abs(p.y) - half.y);
                        d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0);
                    }
                    else d = BoxDistance(p, half);
                    result = Mathf.Min(result, d);
                }
                var scale = root.lossyScale;
                return result * Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }

            private static float BoundsDistance(Bounds bounds, Vector3 world) => BoxDistance(world - bounds.center, bounds.extents);
            private static float BoxDistance(Vector3 p, Vector3 half)
            {
                var q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - half;
                return new Vector3(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0), Mathf.Max(q.z, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0);
            }
        }
}