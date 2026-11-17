using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Equipment
{
    /// <summary>Completes the view rotation missing from torso look, preserving animated arm/finger poses.</summary>
    internal sealed class EquipmentCameraArms
    {
        private readonly Arm _left = new Arm(), _right = new Arm();

        private sealed class Arm
        {
            public Transform bone;
            public Quaternion before, after;
            public bool applied;

            public void Restore()
            {
                if (applied && bone != null && Quaternion.Angle(bone.localRotation, after) <= 0.05f)
                    bone.localRotation = before;
                applied = false;
            }
        }

        public void Restore() { _left.Restore(); _right.Restore(); }

        public void Apply(ProceduralSpine spine, CameraRigController camera, Transform left, Transform right)
        {
            if (spine == null || !spine.isActiveAndEnabled || !spine.IsReady || spine.animationRoot == null ||
                camera == null || !camera.isActiveAndEnabled) return;
            Quaternion viewInRoot = camera.ViewRotation * Quaternion.Inverse(spine.animationRoot.rotation);
            Apply(_left, left, spine, camera, viewInRoot);
            Apply(_right, right, spine, camera, viewInRoot);
        }

        private static void Apply(Arm state, Transform arm, ProceduralSpine spine, CameraRigController camera,
            Quaternion viewInRoot)
        {
            if (arm == null || arm == spine.animationRoot || !arm.IsChildOf(spine.animationRoot) ||
                camera.transform.IsChildOf(arm) || (camera.realCamera != null && camera.realCamera.IsChildOf(arm))) return;
            // Find the nearest torso bone owned by the spine. Remove its CURRENT inherited look,
            // then apply full view from its neutral reference: never add pitch twice or capture at an arbitrary look angle.
            for (Transform parent = arm.parent; parent != null && parent != spine.animationRoot; parent = parent.parent)
            {
                if (!spine.TryGetReferenceWorldRotation(parent, out Quaternion neutral)) continue;
                Quaternion correction = viewInRoot * neutral * Quaternion.Inverse(parent.rotation);
                state.bone = arm;
                state.before = arm.localRotation;
                arm.rotation = correction * arm.rotation;
                state.after = arm.localRotation;
                state.applied = true;
                return;
            }
        }
    }
}
