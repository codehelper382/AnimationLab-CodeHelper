#if UNITY_EDITOR
using System;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Explicit, scene-independent regression checks. No automatic execution or scene edits.</summary>
public static class ProceduralAnimationChecks
{
    [MenuItem("Tools/CodeHelper/Run Spine and Camera Checks")]
    public static void Run()
    {
        CheckAngles();
        CheckPelvisRestore();
        CheckHeadParentedCamera();
        CheckUnanimatedSpine();
        CheckAnimationJob();
        Debug.Log("[CodeHelper] Passed: angle limits, recoil clamp, pelvis restoration, external writes, " +
                  "head-parented camera, absolute/relative yaw, TPS zero distance, unanimated spine drift, " +
                  "and a manually evaluated AnimationStream job with an external root. " +
                  "Gameplay collisions and real Animator transitions still need Play Mode tests.");
    }

    private static void CheckAngles()
    {
        Vector2 limits = new Vector2(20f, 10f);
        Equal(SpineLookMath.ComputeAngle(limits, 0.5f, 0f, 1f, 1f), 10f, "positive input");
        Equal(SpineLookMath.ComputeAngle(limits, -0.5f, 0f, 1f, 1f), -5f, "negative input");
        Equal(SpineLookMath.ComputeAngle(limits, 1f, 50f, 1f, 1f), 20f, "recoil clamp");
        Quaternion current = Quaternion.Euler(12f, 30f, 4f);
        SameRotation(SpineLookMath.Rotate(current, Vector3.right, 20f, Quaternion.identity, 0f),
            current, "zero weight");
    }

    private static void CheckPelvisRestore()
    {
        GameObject root = CreateRoot();
        try
        {
            Transform pelvis = Child(root.transform, "Pelvis");
            Transform left = Child(pelvis, "Left");
            Transform right = Child(pelvis, "Right");
            pelvis.localRotation = Quaternion.Euler(5f, 7f, 3f);
            left.localRotation = Quaternion.Euler(2f, 4f, 6f);
            right.localRotation = Quaternion.Euler(3f, 5f, 7f);
            PelvisTilt tilt = root.AddComponent<PelvisTilt>();
            tilt.pelvisBone = pelvis; tilt.leftThigh = left; tilt.rightThigh = right;
            Quaternion p = pelvis.localRotation, l = left.localRotation, r = right.localRotation;
            for (int i = 0; i < 100; i++)
            {
                tilt.Apply(root.transform, 0.8f, 1f);
                tilt.Restore();
            }
            SameRotation(pelvis.localRotation, p, "pelvis restore");
            SameRotation(left.localRotation, l, "left thigh restore");
            SameRotation(right.localRotation, r, "right thigh restore");
            tilt.Apply(root.transform, 1f, 1f);
            Quaternion external = Quaternion.Euler(40f, 20f, 10f);
            pelvis.localRotation = external;
            tilt.Restore();
            SameRotation(pelvis.localRotation, external, "preserve external pelvis write");
            SameRotation(left.localRotation, l, "restore thigh after pelvis-only external write");
            SameRotation(right.localRotation, r, "restore other thigh after pelvis-only external write");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CheckHeadParentedCamera()
    {
        GameObject root = CreateRoot();
        try
        {
            Transform head = Child(root.transform, "Head");
            Transform rig = Child(head, "CameraRig");
            Transform holder = Child(rig, "Holder");
            Transform camera = Child(holder, "Camera");
            camera.gameObject.AddComponent<Camera>();
            CameraRigController controller = rig.gameObject.AddComponent<CameraRigController>();
            controller.characterRoot = root.transform;
            controller.positionSource = CameraPositionSource.Head; // Explicitly test the optional animated-head mode.
            controller.headBone = head;
            controller.realCamera = camera;
            controller.cameraHolder = holder;
            controller.useSpineLean = false;
            Require(controller.ValidateSetup(), "camera setup");
            controller.SetLookInputs(20f, 45f);
            Quaternion expected = Quaternion.Euler(-20f, 45f, 0f);
            for (int i = 0; i < 20; i++)
            {
                head.localRotation = Quaternion.Euler(i * 3f, i * 7f, i * 2f);
                Invoke(controller, "LateUpdate");
                SameRotation(camera.rotation, expected, "camera independent of head rotation");
                Require(Vector3.Distance(camera.position, head.position) < 0.001f, "head position follow");
            }
            controller.yawRelativeToRoot = true;
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Invoke(controller, "LateUpdate");
            SameRotation(camera.rotation, root.transform.rotation * expected, "root-relative yaw");
            controller.tpsOffset = Vector3.zero;
            controller.SetLookInputs(20f, 45f, 0f, true);
            Invoke(controller, "LateUpdate");
            Require(Vector3.Distance(camera.position, head.position) < 0.001f, "zero distance TPS");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CheckUnanimatedSpine()
    {
        GameObject root = CreateRoot();
        SpineLookProfile profile = ScriptableObject.CreateInstance<SpineLookProfile>();
        try
        {
            Transform bone = Child(root.transform, "Spine");
            profile.pitchElements.Add(new SpineLookElement { customBone = "Spine" });
            ProceduralSpine spine = root.AddComponent<ProceduralSpine>();
            spine.profile = profile;
            spine.animationRoot = root.transform;
            spine.stabilizationWeight = 0f;
            spine.alpha = 0.5f;
            spine.inputVertical = 30f;
            Require((bool)Invoke(spine, "ResolveRuntimeBones"), "spine resolution");
            Invoke(spine, "CaptureBindPose");
            typeof(ProceduralSpine).GetField("_ready", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(spine, true);
            Invoke(spine, "LateUpdate");
            Quaternion first = bone.localRotation;
            for (int i = 0; i < 100; i++)
            {
                Invoke(spine, "Update");
                Invoke(spine, "LateUpdate");
                SameRotation(bone.localRotation, first, "no accumulated look without Animator");
            }
            spine.alpha = 0f;
            Invoke(spine, "Update");
            Invoke(spine, "LateUpdate");
            SameRotation(bone.localRotation, Quaternion.identity, "zero alpha restores original pose");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
        }
    }

    private static GameObject CreateRoot()
    {
        var root = new GameObject("CodeHelper regression check (temporary)");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false); // Avoid running game lifecycle callbacks during isolated checks.
        return root;
    }

    private static void CheckAnimationJob()
    {
        GameObject root = CreateRoot();
        PlayableGraph graph = default;
        NativeArray<SpineLookAtom> pitch = default, yaw = default, roll = default;
        NativeArray<SpineLockedAtom> locked = default;
        AnimationClip clip = new AnimationClip();
        GameObject snapshotHost = CreateRoot();
        SpineLookProfile snapshotProfile = ScriptableObject.CreateInstance<SpineLookProfile>();
        ProceduralSpine snapshotReader = null;
        try
        {
            root.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            Transform model = Child(root.transform, "Model");
            Transform spine = Child(model, "Spine");
            Transform head = Child(spine, "Head");
            head.localPosition = Vector3.up * 0.4f;
            snapshotProfile.pitchElements.Add(new SpineLookElement { customBone = "Spine" });
            snapshotReader = snapshotHost.AddComponent<ProceduralSpine>();
            snapshotReader.animationRoot = root.transform;
            snapshotReader.profile = snapshotProfile;
            Require((bool)Invoke(snapshotReader, "ResolveRuntimeBones"), "job snapshot reader setup");
            Invoke(snapshotReader, "CaptureBindPose");
            Animator animator = model.gameObject.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root.SetActive(true);
            foreach (string axis in new[] { "x", "y", "z", "w" })
                clip.SetCurve("Spine", typeof(Transform), "localRotation." + axis,
                    AnimationCurve.Constant(0f, 1f, axis == "w" ? 1f : 0f));
            pitch = new NativeArray<SpineLookAtom>(1, Allocator.Persistent);
            yaw = new NativeArray<SpineLookAtom>(0, Allocator.Persistent);
            roll = new NativeArray<SpineLookAtom>(0, Allocator.Persistent);
            locked = new NativeArray<SpineLockedAtom>(1, Allocator.Persistent);
            TransformStreamHandle handle = animator.BindStreamTransform(spine);
            pitch[0] = new SpineLookAtom
            {
                handle = handle, limit = new Vector2(30f, 30f), offset = Quaternion.identity, recoilFactor = 1f
            };
            locked[0] = new SpineLockedAtom { handle = handle, reference = spine.rotation };
            SetPrivate(snapshotReader, "_activeMode", SpineDriveMode.AnimationJob);
            SetPrivate(snapshotReader, "_lockedAtoms", locked);
            SetPrivate(snapshotReader, "_ready", true);
            var job = new SpineLookJob
            {
                root = animator.BindSceneTransform(root.transform), // Parent ABOVE Animator.
                rootBindInv = Quaternion.Inverse(root.transform.rotation),
                ready = true, alpha = 1f, stabilization = 1f, pitchInput = 30f,
                pitch = pitch, yaw = yaw, roll = roll, locked = locked
            };
            graph = PlayableGraph.Create("CodeHelper job regression check");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var source = AnimationClipPlayable.Create(graph, clip);
            var playable = AnimationScriptPlayable.Create(graph, job, 1);
            playable.ConnectInput(0, source, 0, 1f);
            AnimationPlayableOutput.Create(graph, "Check", animator).SetSourcePlayable(playable);
            graph.Play();
            Quaternion expected = Quaternion.AngleAxis(10f, root.transform.right) * root.transform.rotation;
            for (int i = 0; i < 10; i++)
            {
                graph.Evaluate(1f / 60f);
                SameRotation(spine.rotation, expected, "stream job pose with external scene root");
                Require(locked[0].cameraPoseValid, "completed stream camera snapshots visible on main thread");
                SameRotation(locked[0].cameraBase, root.transform.rotation, "stream camera pre-look phase");
                SameRotation(locked[0].cameraPrePitch, root.transform.rotation, "stream camera pre-pitch phase");
            }
            Require(snapshotReader.TryGetCameraBonePose(spine, out Quaternion basePose, out _, out _, out _),
                "job camera snapshots exposed through spine API");
            SameRotation(basePose, root.transform.rotation, "job API base phase");
            var anchor = new HeadPositionStabilizer();
            Require(anchor.TryGetPosition(root.transform, head, snapshotReader, 70f, out Vector3 fixedAnchor),
                "job stabilized head setup");
            root.transform.rotation = Quaternion.Euler(0f, 60f, 0f); // No graph evaluation: paused Animator.
            Require(snapshotReader.TryGetCameraBonePose(spine, out basePose, out _, out _, out _),
                "paused job snapshots transported with body root");
            SameRotation(basePose, root.transform.rotation, "paused job base phase follows new body heading");
            anchor.TryGetPosition(root.transform, head, snapshotReader, 70f, out Vector3 pausedAnchor);
            Require(Vector3.Distance(pausedAnchor, fixedAnchor) < 0.0001f,
                "paused Animator body turn does not orbit stabilized head");
            spine.localRotation = Quaternion.Euler(55f, 0f, 0f);
            Require(!snapshotReader.TryGetCameraBonePose(spine, out _, out _, out _, out _),
                "external bone writes reject stale job snapshots");
            root.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            job.alpha = 0f;
            playable.SetJobData(job);
            graph.Evaluate(1f / 60f);
            SameRotation(spine.rotation, root.transform.rotation, "zero-weight stream job passes through clip pose");
            Require(!locked[0].cameraPoseValid, "zero-weight stream invalidates old camera snapshots");
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            // The test owns these arrays; the inactive API reader must not dispose them twice.
            if (snapshotReader != null) SetPrivate(snapshotReader, "_lockedAtoms", default(NativeArray<SpineLockedAtom>));
            if (pitch.IsCreated) pitch.Dispose();
            if (yaw.IsCreated) yaw.Dispose();
            if (roll.IsCreated) roll.Dispose();
            if (locked.IsCreated) locked.Dispose();
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(snapshotHost);
            Object.DestroyImmediate(snapshotProfile);
            Object.DestroyImmediate(root);
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static object Invoke(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static void SetPrivate(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Equal(float actual, float expected, string message) =>
        Require(Mathf.Abs(actual - expected) < 0.001f, message);

    private static void SameRotation(Quaternion actual, Quaternion expected, string message) =>
        Require(Quaternion.Angle(actual, expected) < 0.1f, message);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[CodeHelper] Failed: " + message);
    }
}
#endif
