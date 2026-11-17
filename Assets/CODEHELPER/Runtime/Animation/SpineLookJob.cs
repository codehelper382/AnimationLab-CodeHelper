using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CodeHelper.ProceduralShooterFramework.Animation
{
    /// <summary>
    /// La fórmula de mirada, en UN solo lugar. La usan tanto el modo LateUpdate
    /// como el Animation Job. La pose de entrada depende del modo y del Animator.
    /// </summary>
    public static class SpineLookMath
    {
        /// <summary>Ángulo final de un hueso: input + recoil, limitado a sus ángulos máximos.</summary>
        public static float ComputeAngle(Vector2 limit, float fraction,
                                         float recoil, float recoilFactor, float recoilSign)
        {
            float angle = (fraction > 0f ? limit.x : limit.y) * fraction + recoil * recoilSign * recoilFactor;
            return Mathf.Clamp(angle, -limit.y, limit.x);
        }

        /// <summary>Gira 'current' alrededor de un eje WORLD, aplica el offset y mezcla con weight.</summary>
        public static Quaternion Rotate(Quaternion current, Vector3 worldAxis, float angle,
                                        Quaternion offset, float weight)
        {
            Quaternion target = Quaternion.AngleAxis(angle, worldAxis) * current * offset;
            return weight >= 1f ? target : Quaternion.Slerp(current, target, weight);
        }
    }

    /// <summary>Hueso que gira (pitch / yaw / roll).</summary>
    public struct SpineLookAtom
    {
        public TransformStreamHandle handle;
        public Vector2 limit;
        public float recoilFactor;
        public Quaternion offset;
        public bool hasOffset;
    }

    /// <summary>Hueso que se estabiliza contra una pose de referencia.</summary>
    public struct SpineLockedAtom
    {
        public TransformStreamHandle handle;
        public Quaternion reference;
        public Quaternion cameraBase, cameraRoll, cameraPrePitch;
        public Quaternion cameraRoot, cameraFinal;
        public float cameraPitchYaw;
        public bool cameraPoseValid;
    }

    /// <summary>
    /// Mismo algoritmo que el modo LateUpdate, pero corre dentro del grafo de animación:
    ///   1. Estabilizar spine   2. Inclinar pelvis (opcional)   3. Roll → Yaw → Pitch
    /// Escribe en el AnimationStream (sin tocar Transforms ni sincronizar la jerarquía)
    /// y Unity puede ejecutarlo en un hilo de trabajo.
    /// ProceduralSpine rellena los campos cada frame; aquí no hay lógica de juego.
    /// </summary>
    public struct SpineLookJob : IAnimationJob
    {
        // ── Datos estáticos (se asignan una vez) ──
        public TransformSceneHandle root;
        public NativeArray<SpineLookAtom> roll;
        public NativeArray<SpineLookAtom> yaw;
        public NativeArray<SpineLookAtom> pitch;
        public NativeArray<SpineLockedAtom> locked;
        public Quaternion rootBindInv;

        public bool hasPelvis;
        public TransformStreamHandle pelvis;
        public TransformStreamHandle leftThigh;
        public TransformStreamHandle rightThigh;
        public float pelvisMaxAngle;

        // ── Datos por frame (se actualizan con SetJobData) ──
        public bool ready;
        public float alpha;
        public float stabilization;
        public float pitchInput;   // grados
        public float yawInput;     // grados
        public float leanInput;    // grados
        public Vector3 recoil;     // X = pitch, Y = yaw, Z = roll

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            // Pass-through frames must not expose snapshots from a previous active evaluation.
            for (int i = 0; i < locked.Length; i++)
            {
                SpineLockedAtom atom = locked[i];
                atom.cameraPoseValid = false;
                locked[i] = atom;
            }
            if (!ready || alpha <= 0.001f) return;
            if (!root.IsValid(stream)) return;

            Quaternion rootRot = root.GetRotation(stream);

            Stabilize(stream, rootRot);
            TiltPelvis(stream, rootRot);
            CaptureCameraPose(stream, 0, 0f);

            // Roll → Yaw → Pitch (mismo orden que el modo LateUpdate)
            ApplyAxis(stream, roll, rootRot * Vector3.forward, leanInput / 90f, recoil.z, 1f);
            CaptureCameraPose(stream, 1, 0f);
            float yawApplied = ApplyAxis(stream, yaw, rootRot * Vector3.up, yawInput / 90f, recoil.y, 1f);
            CaptureCameraPose(stream, 2, yawApplied);

            // El eje del pitch gira con el yaw REALMENTE aplicado.
            Vector3 pitchAxis = rootRot * (Quaternion.AngleAxis(yawApplied, Vector3.up) * Vector3.right);
            ApplyAxis(stream, pitch, pitchAxis, pitchInput / 90f, recoil.x, -1f);
            for (int i = 0; i < locked.Length; i++)
            {
                SpineLockedAtom atom = locked[i];
                if (!atom.handle.IsValid(stream)) continue;
                atom.cameraRoot = rootRot;
                atom.cameraFinal = atom.handle.GetRotation(stream);
                locked[i] = atom;
            }
        }

        private void CaptureCameraPose(AnimationStream stream, int phase, float yawApplied)
        {
            for (int i = 0; i < locked.Length; i++)
            {
                SpineLockedAtom atom = locked[i];
                if (!atom.handle.IsValid(stream)) continue;
                Quaternion rotation = atom.handle.GetRotation(stream);
                if (phase == 0) { atom.cameraBase = rotation; atom.cameraPoseValid = false; }
                else if (phase == 1) atom.cameraRoll = rotation;
                else
                {
                    atom.cameraPrePitch = rotation;
                    atom.cameraPitchYaw = yawApplied;
                    atom.cameraPoseValid = true;
                }
                locked[i] = atom;
            }
        }

        private void Stabilize(AnimationStream stream, Quaternion rootRot)
        {
            float weight = stabilization * alpha;
            if (weight <= 0.001f) return;

            Quaternion rootDelta = rootRot * rootBindInv;
            for (int i = 0; i < locked.Length; i++)
            {
                SpineLockedAtom a = locked[i];
                if (!a.handle.IsValid(stream)) continue;
                Quaternion target = rootDelta * a.reference;
                a.handle.SetRotation(stream, weight >= 1f
                    ? target
                    : Quaternion.Slerp(a.handle.GetRotation(stream), target, weight));
            }
        }

        private void TiltPelvis(AnimationStream stream, Quaternion rootRot)
        {
            if (!hasPelvis) return;
            if (!pelvis.IsValid(stream) || !leftThigh.IsValid(stream) || !rightThigh.IsValid(stream)) return;
            float angle = pitchInput / 90f * pelvisMaxAngle * alpha;
            if (Mathf.Abs(angle) < 0.001f) return;

            // Se inclina la pelvis y se devuelven los muslos a su rotación world.
            Quaternion left = leftThigh.GetRotation(stream);
            Quaternion right = rightThigh.GetRotation(stream);
            pelvis.SetRotation(stream,
                Quaternion.AngleAxis(angle, rootRot * Vector3.right) * pelvis.GetRotation(stream));
            leftThigh.SetRotation(stream, left);
            rightThigh.SetRotation(stream, right);
        }

        private float ApplyAxis(AnimationStream stream, NativeArray<SpineLookAtom> atoms,
                                Vector3 axis, float fraction, float recoilValue, float recoilSign)
        {
            float applied = 0f;
            for (int i = 0; i < atoms.Length; i++)
            {
                SpineLookAtom a = atoms[i];
                if (!a.handle.IsValid(stream)) continue;
                float angle = SpineLookMath.ComputeAngle(a.limit, fraction, recoilValue, a.recoilFactor, recoilSign);
                applied += angle;

                // Igual que LateUpdate: no leer/escribir rotaciones que no cambian.
                if (!a.hasOffset && Mathf.Abs(angle) < 1e-4f) continue;

                Quaternion current = a.handle.GetRotation(stream);
                a.handle.SetRotation(stream, SpineLookMath.Rotate(current, axis, angle, a.offset, alpha));
            }
            return applied * alpha;
        }
    }
}
