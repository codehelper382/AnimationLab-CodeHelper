#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using CodeHelper.ProceduralShooterFramework.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class SampleEquipmentSceneChecks
{
    public static void RunRegressionAndScene()
    {
        PlayerLookChecks.RunAll();
        Run();
        EquipmentHandAuthoringChecks.Run();
        EquipmentJitterChecks.Run();
    }

    [MenuItem("Tools/CodeHelper/Check SampleScene Equipment (Isolated)")]
    public static void Run()
    {
        CheckMode(SpineDriveMode.LateUpdate);
        CheckMode(SpineDriveMode.AnimationJob);
        Debug.Log("[SampleEquipmentSceneChecks] Passed actual saved scene in preview isolation: Humanoid references, " +
            "one spine-owned graph in both modes, AK12/fists/AK12 selection, procedural lowering, fist pose/finger changes, " +
            "punch playback/contact, grip release/restoration, repeated switches and disable cleanup. " +
            "Visual contact quality and physical input still require Play Mode.");
    }

    private static void CheckMode(SpineDriveMode mode)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(EquipmentAssetPaths.Scene);
        EquipmentSceneController equipment = null;
        var ownedFists = new List<UnityEngine.Object>();
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            equipment = roots.SelectMany(root => root.GetComponentsInChildren<EquipmentSceneController>(true)).Single();
            // Allow isolated authoring controls to change recipes without ever touching shared assets.
            var fists = UnityEngine.Object.Instantiate((MeleeDefinition)equipment.slots[1].definition);
            ownedFists.Add(fists);
            fists.equip = CloneAction(fists.equip, ownedFists);
            fists.unequip = CloneAction(fists.unequip, ownedFists);
            fists.attacks = fists.attacks.Select(action => CloneAction(action, ownedFists)).ToArray();
            equipment.slots[1].definition = fists;
            Require(equipment.slots.Length == 3 && equipment.slots[0].visualRoot != null && equipment.slots[1].visualRoot == null && equipment.slots[2].visualRoot != null,
                "AK12 existing view, fists without model and independent authored knife");
            var spine = equipment.spine;
            var hands = equipment.handIK;
            var pose = equipment.weaponPose;
            Require(spine.animator != null && spine.animator.isHuman && spine.enableEquipmentAnimation, "Humanoid with clip channel enabled");
            Require(spine.animator.cullingMode == AnimatorCullingMode.AlwaysAnimate,
                "FPS arms animate even when the body renderer is outside the camera");
            Require(equipment.slots[1].definition.characterPose.isHumanMotion, "compatible fists pose");
            foreach (GameObject root in roots)
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "no missing components: " + item.name);
            Invoke(spine, "Awake");
            Invoke(pose, "Awake");
            Invoke(pose, "OnEnable");
            Invoke(hands, "Awake");
            Vector3 originalHolder = pose.PoseRoot.localPosition;
            Vector3 leftGripPosition = equipment.slots[0].leftGrip.localPosition;
            Vector3 rightGripPosition = equipment.slots[0].rightGrip.localPosition;
            Transform originalLeft = hands.leftHand.target, originalRight = hands.rightHand.target;
            spine.animator.Rebind();
            spine.animator.Update(0f);
            Require((bool)Invoke(spine, "ResolveRuntimeBones"), "resolve actual character bones");
            Invoke(spine, "CaptureBindPose");
            Set(spine, "_activeMode", mode);
            bool built = (bool)Invoke(spine, mode == SpineDriveMode.AnimationJob ? "TryBuildJobGraph" : "TryBuildEquipmentGraph");
            Require(built, "single graph builds for " + mode);
            Set(spine, "_ready", true);
            var graph = (PlayableGraph)Get(spine, "_graph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            Require(graph.GetOutputCount() == 1, "exactly one Animator output");
            Invoke(equipment, "OnEnable");

            Tick(equipment, graph, 10f);
            Require(equipment.SelectedSlot == 0 && equipment.Runtime.IsReady && equipment.slots[0].visualRoot.gameObject.activeSelf,
                "starts with existing AK12");
            Require(hands.leftHand.target == originalLeft && hands.rightHand.target == originalRight &&
                hands.leftHand.weight == 1f && hands.rightHand.weight == 1f, "AK12 grips preserved");
            Invoke(equipment.look, "Awake");
            Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, true, 0f);
            Tick(equipment, graph, 1f);
            Require(pose.IsAiming, "held RMB reaches weapon pose through scene binding");
            Require(equipment.Runtime.TryFire() && equipment.Runtime.CanAim, "fire does not suppress ability to hold ADS");
            Tick(equipment, graph, 0.01f);
            Require(pose.IsAiming, "held ADS survives active fire animation");
            Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
            Tick(equipment, graph, 1f);
            CheckReload(equipment, graph, originalHolder);
            CheckPerWeaponReload(equipment, graph, originalHolder);
            CheckReloadLook(equipment, graph);
            Vector3 rifleHand = hands.rightHand.hand.position;
            Transform finger = spine.animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Quaternion rifleFinger = finger.localRotation;
            Transform[] fingers = Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>()
                .Where(bone => bone.ToString().Contains("Proximal") || bone.ToString().Contains("Intermediate") || bone.ToString().Contains("Distal"))
                .Select(bone => spine.animator.GetBoneTransform(bone)).Where(bone => bone != null).ToArray();
            Quaternion[] rifleFingers = fingers.Select(bone => bone.localRotation).ToArray();
            Require(equipment.SelectSlot(1), "select fists");
            Tick(equipment, graph, 0.1f);
            Require(equipment.SelectedSlot == 0 && pose.PoseRoot.localPosition.y < originalHolder.y - 0.01f,
                "unequip lowers current weapon before hiding it");
            Tick(equipment, graph, 10f);
            Tick(equipment, graph, 0.1f);
            Require(equipment.SelectedSlot == 1 && equipment.Runtime.IsReady && !equipment.slots[0].visualRoot.gameObject.activeSelf,
                "fists equipped without physical weapon");
            bool editableHands = equipment.slots[1].leftHandPose?.guard != null;
            Require(editableHands
                ? hands.leftHand.weight == 1f && hands.rightHand.weight == 1f && hands.leftHand.target != originalLeft && hands.rightHand.target != originalRight
                : hands.leftHand.weight == 0f && hands.rightHand.weight == 0f && hands.leftHand.target == null && hands.rightHand.target == null,
                "fists use editable independent targets when assigned, never rifle grips");
            float fingerDelta = fingers.Select((bone, index) => Quaternion.Angle(rifleFingers[index], bone.localRotation)).Max();
            Debug.Log($"[SampleEquipmentSceneChecks] {mode} fist hand delta={Vector3.Distance(rifleHand, hands.rightHand.hand.position):F5}, " +
                $"max finger delta={fingerDelta:F3}, index={Quaternion.Angle(rifleFinger, finger.localRotation):F3}");
            Require(Vector3.Distance(rifleHand, hands.rightHand.hand.position) > 0.01f &&
                fingerDelta > 1f, "fists clip actually changes arms and fingers: " +
                $"mode={mode}, hand delta={Vector3.Distance(rifleHand, hands.rightHand.hand.position):F5}, " +
                $"finger delta={fingerDelta:F3}, " +
                $"pose={equipment.Runtime.Current.Definition.characterPose.name}");
            Require(pose.profile == null && !equipment.Runtime.CanAim, "fists do not inherit rifle Aim profile");
            Vector3 guard = hands.rightHand.hand.position;
            Require(equipment.Runtime.TryMeleeAttack(), "punch starts");
            float duration = equipment.Runtime.Current.Action.Definition.duration;
            Tick(equipment, graph, duration * 0.3f);
            Require(equipment.Runtime.Current.MeleeContactCount == 0 &&
                Vector3.Distance(guard, hands.rightHand.hand.position) > 0.005f, "punch clip plays before contact milestone");
            Tick(equipment, graph, 10f);
            Require(equipment.Runtime.Current.MeleeContactCount == 1, "punch confirms exactly one potential contact");
            var leftPose = equipment.slots[1].leftHandPose;
            var rightPose = equipment.slots[1].rightHandPose;
            try
            {
                equipment.slots[1].leftHandPose = equipment.slots[1].rightHandPose = null;
                Tick(equipment, graph, 0f, true);
                CheckFistsLook(equipment, graph);
                CheckFistsMotionLifecycle(equipment, graph);
            }
            finally
            {
                equipment.slots[1].leftHandPose = leftPose;
                equipment.slots[1].rightHandPose = rightPose;
                Tick(equipment, graph, 1f, true);
            }
            if (editableHands) EquipmentHandTargetChecks.Check(equipment, graph);
            KnifeEquipmentChecks.Check(equipment, graph);
            for (int i = 0; i < 4; i++)
            {
                Require(equipment.SelectSlot(0), "return to AK12");
                Tick(equipment, graph, 10f);
                Require(equipment.SelectedSlot == 0 && hands.leftHand.target == originalLeft && hands.rightHand.target == originalRight,
                    "AK12 targets restored after fist animation");
                Require(pose.profile == equipment.slots[0].definition.weaponPoses &&
                    Vector3.Distance(pose.PoseRoot.localPosition, originalHolder) < 0.0001f, "AK12 saved Idle preserved without action offset drift");
                if (i < 3) { Require(equipment.SelectSlot(1), "repeat fists selection"); Tick(equipment, graph, 10f); }
            }
            Require(equipment.slots[0].leftGrip.localPosition == leftGripPosition &&
                equipment.slots[0].rightGrip.localPosition == rightGripPosition, "never move saved grips");
            Require(equipment.Runtime.TrySelect(null), "explicit holster");
            Tick(equipment, graph, 10f);
            Require(equipment.SelectedSlot == -1 && pose.profile == null && !pose.IsAiming &&
                hands.leftHand.target == null && hands.rightHand.target == null &&
                !equipment.slots[0].visualRoot.gameObject.activeSelf, "holster clears every presentation channel");
            Require(equipment.SelectSlot(0), "recover after holster");
            Tick(equipment, graph, 10f);
            Require(equipment.SelectSlot(1), "cleanup during transition");
            Tick(equipment, graph, 0.1f);
            Invoke(equipment, "OnDisable");
            Require(hands.leftHand.target == originalLeft && hands.rightHand.target == originalRight &&
                equipment.slots[0].visualRoot.gameObject.activeSelf && pose.useLookController, "disable restores original bindings/view");
        }
        finally
        {
            if (equipment != null)
            {
                Invoke(equipment, "OnDisable");
                if (equipment.spine != null) Invoke(equipment.spine, "TearDown");
            }
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (UnityEngine.Object item in ownedFists) UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static EquipmentActionDefinition CloneAction(EquipmentActionDefinition source, List<UnityEngine.Object> owned)
    {
        if (source == null) return null;
        var clone = UnityEngine.Object.Instantiate(source);
        owned.Add(clone);
        return clone;
    }

    private static void Tick(EquipmentSceneController equipment, PlayableGraph graph, float dt, bool applyCamera = false)
    {
        equipment.Step(dt);
        Invoke(equipment.spine, "Update");
        Invoke(equipment.weaponPose, "PreparePose", dt);
        Invoke(equipment.handIK, "Update");
        graph.Evaluate(0f);
        Invoke(equipment.spine, "LateUpdate");
        if (applyCamera) Invoke(equipment.look.cameraRig, "ApplyCamera", dt);
        Invoke(equipment.weaponPose, "ApplyPreparedPose", dt);
        equipment.ApplyMotion();
        Invoke(equipment.handIK, "LateUpdate");
    }

    private static void CheckReload(EquipmentSceneController equipment, PlayableGraph graph, Vector3 idle)
    {
        var firearm = equipment.slots[0].definition as FirearmDefinition;
        Require(equipment.enableReloadInput && firearm != null && firearm.reload.characterClip != null &&
            firearm.reload.characterClip.isHumanMotion && firearm.reload.characterClip.name == "C_Rifle_Reload_Humanoid" &&
            firearm.reload.weaponClip == null && !firearm.reload.followAnimatedRightHand &&
            firearm.reload.leftHandTargetMotion != null && equipment.slots[0].leftReloadTarget != null,
            "reload uses existing finger clip and an explicitly assigned weapon-local hand target");
        AvatarMask mask = firearm.reload.characterMask;
        Require(mask != null, "left-only reload mask assigned");
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            Require(mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i) ==
                (i == (int)AvatarMaskBodyPart.LeftFingers),
                "reload clip affects only fingers; arm/wrist are exclusively driven by IK");
        Transform model = equipment.slots[0].visualRoot;
        Transform magazine = model.Find("AK12Rig/Root/Mag");
        Require(magazine != null, "scene magazine exists");
        Vector3 magPosition = magazine.localPosition, modelPosition = model.localPosition;
        Quaternion magRotation = magazine.localRotation, modelRotation = model.localRotation;
        var item = equipment.Runtime.Current;
        int loaded = item.TotalLoadedRounds, reserve = item.ReserveRounds;
        Vector3 animatedLeft = equipment.handIK.leftHand.hand.position;
        Quaternion holderRotation = equipment.weaponPose.PoseRoot.localRotation;
        Require(equipment.Runtime.TryReload(), "start simple left-arm reload");
        float duration = item.Action.Definition.duration;
        Tick(equipment, graph, duration * 0.25f);
        Require(item.IsBusy && !equipment.Runtime.CanAim && !equipment.Runtime.TryFire() && !equipment.SelectSlot(1),
            "reload blocks aim/fire/equip while clip is playing");
        Require(equipment.handIK.leftHand.weight == 1f && equipment.handIK.rightHand.weight == 1f &&
            equipment.handIK.leftHand.target != equipment.slots[0].leftGrip &&
            equipment.handIK.leftHand.target.IsChildOf(equipment.weaponPose.PoseRoot),
            "left releases weapon grip but follows independent weapon-local reload target with IK");
        Require(item.TotalLoadedRounds == loaded && item.ReserveRounds == reserve, "ammo waits until animation end");
        Require(Vector3.Distance(animatedLeft, equipment.handIK.leftHand.hand.position) > 0.01f,
            "reload character clip actually moves the manipulating hand");
        Require(Vector3.Distance(equipment.handIK.rightHand.hand.position, equipment.slots[0].rightGrip.position) < 0.001f,
            "right hand remains on its fixed weapon grip");
        // Compare with the base pose at the SAME time, not an earlier frame of the looping rifle idle.
        Transform rightFinger = equipment.spine.animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Quaternion reloadFinger = rightFinger.localRotation;
        double baseTime = (double)Get(equipment, "_idleTime") % firearm.characterPose.length;
        equipment.spine.SetEquipmentAnimation(firearm.characterPose, firearm.characterMask, baseTime, null, null, 0, 0f);
        graph.Evaluate(0f);
        float rightFingerDifference = Quaternion.Angle(reloadFinger, rightFinger.localRotation);
        Require(rightFingerDifference < 0.1f,
            "reload mask does not replace right-hand finger pose at matching time: " + rightFingerDifference);
        Tick(equipment, graph, 0f); // Restore the active left-arm action after the control sample.
        foreach (float step in new[] { 0.25f, 0.25f, 0.21f, 0.03f })
        {
            Tick(equipment, graph, duration * step);
            Require(equipment.handIK.rightHand.weight == 1f &&
                Vector3.Distance(equipment.weaponPose.PoseRoot.localPosition, idle) < 0.0001f &&
                Quaternion.Angle(equipment.weaponPose.PoseRoot.localRotation, holderRotation) < 0.01f,
                "right weight stays 1 and weapon anchor stays fixed throughout reload");
        }
        Require(Vector3.Distance(magazine.localPosition, magPosition) < 0.00001f && Quaternion.Angle(magazine.localRotation, magRotation) < 0.001f,
            "unrelated old weapon animation is not sampled");
        Require(model.localPosition == modelPosition && Quaternion.Angle(model.localRotation, modelRotation) < 0.001f,
            "weapon clip never overwrites calibrated model root");
        Tick(equipment, graph, duration);
        Require(!item.IsBusy && item.MagazineRounds == item.MagazineCapacity &&
            item.TotalLoadedRounds + item.ReserveRounds == loaded + reserve, "complete reload transfers ammo once and conserves total");
        Require(equipment.handIK.leftHand.weight == 1f && equipment.handIK.rightHand.weight == 1f &&
            Vector3.Distance(equipment.weaponPose.PoseRoot.localPosition, idle) < 0.0001f, "reload returns to saved Idle and grips");
        Require(Vector3.Distance(magazine.localPosition, magPosition) < 0.00001f && Quaternion.Angle(magazine.localRotation, magRotation) < 0.001f,
            "weapon pieces reset after reload");
        reserve = item.ReserveRounds;
        Tick(equipment, graph, 10f);
        Require(item.ReserveRounds == reserve, "finished reload cannot transfer ammo again");

        Require(equipment.Runtime.TryFire(), "spend round before cancellation test");
        Tick(equipment, graph, 1f);
        loaded = item.TotalLoadedRounds; reserve = item.ReserveRounds;
        Require(equipment.Runtime.TryReload(), "cancel test reload starts");
        Tick(equipment, graph, duration * 0.5f);
        Require(equipment.Runtime.CancelAction(), "cancel reload before commit");
        Tick(equipment, graph, 0f);
        Require(item.TotalLoadedRounds == loaded && item.ReserveRounds == reserve && equipment.handIK.leftHand.weight == 1f &&
            Vector3.Distance(magazine.localPosition, magPosition) < 0.00001f, "cancel restores presentation without granting ammo");

        while (item.TotalLoadedRounds > 0)
        {
            Require(equipment.Runtime.TryFire(), "empty AK12 for empty reload");
            Tick(equipment, graph, 1f);
        }
        Require(equipment.Runtime.TryReload() && item.Action.Definition == firearm.reloadEmpty, "empty reload uses simple left-arm action");
        Tick(equipment, graph, duration * 0.25f);
        Require(equipment.handIK.leftHand.weight == 1f && equipment.handIK.rightHand.weight == 1f &&
            item.Action.Definition.characterMask == firearm.reload.characterMask &&
            item.Action.Definition.weaponClip == null && !item.Action.Definition.followAnimatedRightHand &&
            Vector3.Distance(equipment.weaponPose.PoseRoot.localPosition, idle) < 0.0001f,
            "empty reload keeps the same left-only animation and fixed right grip");
        Tick(equipment, graph, duration + 0.1f);
        Require(item.ChamberLoaded && item.TotalLoadedRounds == item.MagazineCapacity &&
            equipment.handIK.rightHand.weight == 1f && Vector3.Distance(equipment.weaponPose.PoseRoot.localPosition, idle) < 0.0001f,
            "empty reload chambers round and recovers pose");
        Require(equipment.Runtime.TryFire(), "spend round before disable test");
        Tick(equipment, graph, 1f);
        loaded = item.TotalLoadedRounds; reserve = item.ReserveRounds;
        Require(equipment.Runtime.TryReload(), "disable test reload starts");
        Tick(equipment, graph, duration * 0.25f);
        Invoke(equipment, "OnDisable");
        Require(!item.IsBusy && item.TotalLoadedRounds == loaded && item.ReserveRounds == reserve &&
            Vector3.Distance(magazine.localPosition, magPosition) < 0.00001f && Quaternion.Angle(magazine.localRotation, magRotation) < 0.001f,
            "disable cleans weapon pieces and does not grant unfinished reload ammo");
        Invoke(equipment, "OnEnable");
        Tick(equipment, graph, 10f);
        Require(equipment.Runtime.Current == item && item.TotalLoadedRounds == loaded &&
            Vector3.Distance(equipment.weaponPose.PoseRoot.localPosition, idle) < 0.0001f,
            "reenabling preserves item state and restores normal weapon pose");
        Debug.Log("[SampleEquipmentSceneChecks] Weapon-relative left reload passed: local target motion, existing finger clip, left-only mask, " +
            "right grip/IK always 1, fixed weapon, unchanged right fingers/pieces, ammo at end, " +
            "cancellation, empty reload and disable/reenable.");
    }

    private static void CheckPerWeaponReload(EquipmentSceneController equipment, PlayableGraph graph, Vector3 idle)
    {
        // Extend only the preview copy. No prefab, asset or slot is added to the saved scene.
        EquipmentSceneSlot[] originalSlots = equipment.slots;
        int originalSelection = equipment.SelectedSlot;
        var originalInstances = (EquipmentInstance[])Get(equipment, "_instances");
        EquipmentInstance originalItem = equipment.Runtime.Current;
        int originalLoaded = originalItem.TotalLoadedRounds, originalReserve = originalItem.ReserveRounds;
        int originalShots = originalItem.ShotCount;
        var source = (FirearmDefinition)originalSlots[0].definition;
        string sourceBefore = JsonUtility.ToJson(source);
        string actionBefore = JsonUtility.ToJson(source.reload);
        FirearmDefinition alternate = null;
        EquipmentActionDefinition action = null;
        AnimationClip clip = null;
        AnimationClip targetClip = null;
        GameObject temporaryView = null;
        try
        {
            alternate = UnityEngine.Object.Instantiate(source);
            action = UnityEngine.Object.Instantiate(source.reload);
            clip = UnityEngine.Object.Instantiate(source.reload.characterClip);
            targetClip = UnityEngine.Object.Instantiate(source.reload.leftHandTargetMotion);
            temporaryView = new GameObject("Weapon B - temporary test grips only");
            SceneManager.MoveGameObjectToScene(temporaryView, equipment.gameObject.scene);
            temporaryView.transform.SetParent(equipment.weaponPose.PoseRoot, false);
            alternate.name = alternate.equipmentId = "Temporary Weapon B";
            alternate.magazineCapacity = 7;
            alternate.initialMagazineRounds = 2;
            alternate.initialReserveRounds = 7;
            alternate.equip = alternate.unequip = null;
            alternate.reload = alternate.reloadEmpty = action;
            clip.name = "Temporary B left-arm movement (not a production reload)";
            // A different user-authored target trajectory, plus independent finger animation.
            AnimationUtility.SetEditorCurve(targetClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Constant(0f, targetClip.length, 0.15f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "LeftHand.Index.1 Stretched"),
                AnimationCurve.Constant(0f, clip.length, 1f));
            action.characterClip = clip;
            action.leftHandTargetMotion = targetClip;
            action.duration = source.reload.duration * 0.75f;
            var view = new EquipmentSceneSlot
            {
                definition = alternate,
                visualRoot = temporaryView.transform,
                leftGrip = CopyTarget(originalSlots[0].leftGrip, temporaryView.transform),
                rightGrip = CopyTarget(originalSlots[0].rightGrip, temporaryView.transform),
                leftHint = CopyTarget(originalSlots[0].leftHint, temporaryView.transform),
                rightHint = CopyTarget(originalSlots[0].rightHint, temporaryView.transform),
                leftReloadTarget = CopyTarget(originalSlots[0].leftReloadTarget, temporaryView.transform)
            };
            temporaryView.SetActive(false);
            Invoke(equipment, "OnDisable");
            equipment.slots = new[] { originalSlots[0], originalSlots[1], view };
            Set(equipment, "_instances", null); // Same slot count can now replace knife with temporary B; rebuild only this fixture.
            Invoke(equipment, "OnEnable");
            Tick(equipment, graph, 10f);
            EquipmentInstance itemA = equipment.Runtime.Current;
            int loadedA = itemA.TotalLoadedRounds, reserveA = itemA.ReserveRounds;
            Require(equipment.Runtime.TryReload(), "weapon A starts its own reload");
            Tick(equipment, graph, source.reload.duration * 0.5f);
            Require((AnimationClip)Get(Get(equipment.spine, "_equipmentLayer"), "_actionClip") == source.reload.characterClip,
                "native character graph receives weapon A's assigned clip");
            Require(equipment.Runtime.CancelAction(), "finish A test without transferring ammo");
            Tick(equipment, graph, 0f);
            Require(equipment.SelectSlot(2), "same controller selects temporary weapon B");
            Tick(equipment, graph, 10f);
            Require(equipment.handIK.leftHand.target == view.leftGrip && equipment.handIK.rightHand.target == view.rightGrip,
                "each weapon selects its own grip references");
            Require(equipment.Runtime.TryReload(), "weapon B starts its own reload");
            Tick(equipment, graph, action.duration * 0.5f);
            Require((AnimationClip)Get(Get(equipment.spine, "_equipmentLayer"), "_actionClip") == clip &&
                equipment.Runtime.Current.Action.Definition == action, "native graph uses B's different clip and action");
            Require(equipment.handIK.rightHand.weight == 1f && equipment.handIK.leftHand.weight == 1f &&
                Vector3.Distance(equipment.weaponPose.PoseRoot.localPosition, idle) < 0.0001f,
                "different weapon uses its own hand target while right grip remains fixed");
            Transform characterRoot = equipment.spine.animationRoot;
            Vector3 leftB = characterRoot.InverseTransformPoint(equipment.handIK.leftHand.hand.position);
            Transform leftFinger = equipment.spine.animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            Transform rightFinger = equipment.spine.animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Quaternion leftBRotation = leftFinger.localRotation, rightBRotation = rightFinger.localRotation;
            float progress = equipment.Runtime.Current.Action.Progress;
            // Same base clock/action progress and same character-relative frame: only the clip changes.
            action.leftHandTargetMotion = source.reload.leftHandTargetMotion;
            EvaluateControl(equipment, graph, clip, action.characterMask,
                progress * clip.length, EquipmentSceneController.ActionWeight(action, progress));
            float leftDifference = Vector3.Distance(leftB, characterRoot.InverseTransformPoint(equipment.handIK.leftHand.hand.position));
            Require(leftDifference > 0.01f, "edited clip changes left-hand trajectory against a matching-time unedited control");
            action.leftHandTargetMotion = targetClip;
            EvaluateControl(equipment, graph, null, null, 0, 0f);
            float leftFingerDifference = Quaternion.Angle(leftBRotation, leftFinger.localRotation);
            Require(leftFingerDifference > 1f && Quaternion.Angle(rightBRotation, rightFinger.localRotation) < 0.1f,
                "left finger animation applies, while right fingers match an action-disabled control at the same base time");
            action.leftHandTargetMotion = null;
            Vector3 anchorBefore = view.leftReloadTarget.localPosition;
            view.leftReloadTarget.localPosition += new Vector3(0.01f, 0f, 0f);
            EvaluateControl(equipment, graph, clip, action.characterMask, progress * clip.length, 1f);
            Require(Vector3.Distance(equipment.handIK.leftHand.target.position, view.leftReloadTarget.position) < 0.0001f,
                "without a motion clip, assigning/moving the per-weapon target directly positions the hand goal");
            view.leftReloadTarget.localPosition = anchorBefore;
            Transform assignedAnchor = view.leftReloadTarget;
            view.leftReloadTarget = null;
            Tick(equipment, graph, 0f);
            Require(equipment.handIK.leftHand.target == view.leftGrip && equipment.handIK.leftHand.weight == 0f,
                "slots with no reload target retain the original FK fallback");
            view.leftReloadTarget = assignedAnchor;
            action.leftHandTargetMotion = targetClip;
            Tick(equipment, graph, 0f); // Restore B after the two controlled samples, without advancing its action clock.
            Tick(equipment, graph, action.duration);
            Require(equipment.Runtime.Current.MagazineRounds == 7 && equipment.Runtime.Current.ReserveRounds == 2 &&
                itemA.TotalLoadedRounds == loadedA && itemA.ReserveRounds == reserveA,
                "B reload does not change A ammunition");
            Require(equipment.SelectSlot(0), "return to weapon A");
            Tick(equipment, graph, 10f);
            Require(equipment.Runtime.Current == itemA && equipment.handIK.leftHand.target == originalSlots[0].leftGrip,
                "return restores A instance and grips");
            Require(equipment.Runtime.TryReload(), "A can reload again after switching");
            Tick(equipment, graph, source.reload.duration * 0.5f);
            Require((AnimationClip)Get(Get(equipment.spine, "_equipmentLayer"), "_actionClip") == source.reload.characterClip,
                "returning to A restores its clip, not the last selected weapon's clip");
            Require(equipment.Runtime.CancelAction(), "end temporary selection test");
            Tick(equipment, graph, 0f);
            Require(JsonUtility.ToJson(source) == sourceBefore && JsonUtility.ToJson(source.reload) == actionBefore,
                "per-weapon test never changes the real definition or action");
            Debug.Log($"[SampleEquipmentSceneChecks] Per-weapon test passed: A/B/A select their own clips/grips and independent ammo, " +
                $"controlled left-hand difference={leftDifference * 100f:F2} cm, left-finger difference={leftFingerDifference:F2} deg; " +
                "same controller, right IK=1, weapon fixed. " +
                "B is a temporary diagnostic definition, not a new saved weapon.");
        }
        finally
        {
            try
            {
                Invoke(equipment, "OnDisable");
                equipment.slots = originalSlots;
                Set(equipment, "_instances", originalInstances);
                Set(equipment, "<SelectedSlot>k__BackingField", originalSelection);
                Invoke(equipment, "OnEnable");
                Tick(equipment, graph, 10f);
                Require(equipment.Runtime.Current == originalItem && originalItem.TotalLoadedRounds == originalLoaded &&
                    originalItem.ReserveRounds == originalReserve && originalItem.ShotCount == originalShots,
                    "temporary test restores original item instances, ammunition and shot counters");
            }
            finally
            {
                if (temporaryView != null) UnityEngine.Object.DestroyImmediate(temporaryView);
                if (alternate != null) UnityEngine.Object.DestroyImmediate(alternate);
                if (action != null) UnityEngine.Object.DestroyImmediate(action);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (targetClip != null) UnityEngine.Object.DestroyImmediate(targetClip);
            }
        }
    }

    private static void EvaluateControl(EquipmentSceneController equipment, PlayableGraph graph,
        AnimationClip clip, AvatarMask mask, double time, float weight)
    {
        Invoke(equipment.handIK, "Update");
        Invoke(equipment.spine, "Update");
        EquipmentDefinition definition = equipment.Runtime.Current.Definition;
        double baseTime = (double)Get(equipment, "_idleTime") % definition.characterPose.length;
        equipment.spine.SetEquipmentAnimation(definition.characterPose, definition.characterMask, baseTime, clip, mask, time, weight);
        graph.Evaluate(0f);
        Invoke(equipment.spine, "LateUpdate");
        Invoke(equipment.weaponPose, "ApplyPreparedPose", 0f);
        equipment.ApplyMotion();
        Invoke(equipment.handIK, "LateUpdate");
    }

    private static Transform CopyTarget(Transform source, Transform parent)
    {
        if (source == null) return null;
        var copy = new GameObject(source.name + " (temporary)").transform;
        copy.SetParent(parent, false);
        copy.SetPositionAndRotation(source.position, source.rotation);
        return copy;
    }

    private static void CheckReloadLook(EquipmentSceneController equipment, PlayableGraph graph)
    {
        equipment.look.ConnectCamera();
        var hand = equipment.handIK.leftHand;
        Transform root = equipment.weaponPose.PoseRoot;
        float[] samples = { 0.02f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 0.98f };
        var referencePositions = new Vector3[samples.Length];
        var referenceRotations = new Quaternion[samples.Length];
        float maxResidual = 0f;
        int limitedByReach = 0;
        float yaw = equipment.look.CameraYaw;
        try
        {
            foreach (float pitch in new[] { 0f, 45f, 90f, -45f, -90f })
            {
                equipment.look.SetLookAngles(pitch, yaw);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Tick(equipment, graph, 1f, true);
                Require(equipment.Runtime.TryReload(), "reload can start at look pitch " + pitch);
                float duration = equipment.Runtime.Current.Action.Definition.duration;
                float previous = 0f;
                for (int sample = 0; sample < samples.Length; sample++)
                {
                    Tick(equipment, graph, duration * (samples[sample] - previous), true);
                    previous = samples[sample];
                    Require(hand.weight == 1f && equipment.handIK.rightHand.weight == 1f && equipment.handIK.IsLeftHandReady,
                        "both IK chains remain active while left follows reload target");
                    Vector3 local = root.InverseTransformPoint(hand.target.position);
                    Quaternion rotation = Quaternion.Inverse(root.rotation) * hand.target.rotation;
                    if (pitch == 0f) { referencePositions[sample] = local; referenceRotations[sample] = rotation; }
                    Require(Vector3.Distance(local, referencePositions[sample]) < 0.0001f && Quaternion.Angle(rotation, referenceRotations[sample]) < 0.1f,
                        "reload path remains identical in weapon space when looking up/down: " + pitch);
                    Vector3 goal = hand.target.TransformPoint(hand.positionOffset);
                    float upper = Vector3.Distance(hand.upperArm.position, hand.forearm.position);
                    float lower = Vector3.Distance(hand.forearm.position, hand.hand.position);
                    float distance = Vector3.Distance(hand.upperArm.position, goal);
                    float unavoidable = Mathf.Max(0f, distance - upper - lower, Mathf.Abs(upper - lower) - distance);
                    float residual = Vector3.Distance(hand.hand.position, goal) - unavoidable;
                    maxResidual = Mathf.Max(maxResidual, residual);
                    if (unavoidable > 0.003f) limitedByReach++;
                    Require(residual < 0.003f, "left wrist tracks target within physical arm reach at pitch " + pitch);
                }
                Require(equipment.Runtime.CancelAction(), "cancel angle fixture before ammo commit");
                Tick(equipment, graph, 0f, true);
                Require(hand.target == equipment.slots[0].leftGrip && hand.weight == 1f, "angle test restores grip");
            }
        }
        finally
        {
            equipment.Runtime.CancelAction();
            equipment.look.SetLookAngles(0f, yaw);
            Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
            Tick(equipment, graph, 1f, true);
        }
        Debug.Log($"[SampleEquipmentSceneChecks] Reload look passed at 0/+45/+90/-45/-90 degrees, 7 points per reload: weapon-local path/rotation invariant; " +
            $"max IK residual beyond physical reach={maxResidual * 1000f:F2} mm, reach-limited poses={limitedByReach}.");
    }

    private static void CheckFistsLook(EquipmentSceneController equipment, PlayableGraph graph)
    {
        var rig = equipment.look.cameraRig;
        float oldPitch = equipment.look.CameraPitch, oldYaw = equipment.look.CameraYaw;
        Quaternion oldRoot = equipment.spine.animationRoot.rotation;
        var arms = new[] { equipment.handIK.leftHand, equipment.handIK.rightHand };
        float maxAngle = 0f, maxPathError = 0f;
        try
        {
            // Same animation clocks at every view: the authored guard/punch may move, but must not
            // change its direction relative to the view merely because torso look has smaller limits.
            for (int sample = 0; sample < 5; sample++)
            {
                if (sample == 1) Require(equipment.Runtime.TryMeleeAttack(), "start camera-follow punch");
                if (sample > 0) Tick(equipment, graph, equipment.Runtime.Current.Action.Definition.duration * 0.2f, true);
                equipment.look.SetLookAngles(0f, oldYaw);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Tick(equipment, graph, 0f, true);
                var rotations = arms.Select(arm => Quaternion.Inverse(rig.ViewRotation) * arm.hand.rotation).ToArray();
                var paths = arms.Select(arm => Quaternion.Inverse(rig.ViewRotation) * (arm.hand.position - arm.upperArm.position)).ToArray();
                foreach (Vector2 angles in new[] { new Vector2(45f, 0f), new Vector2(90f, 0f), new Vector2(-45f, 0f),
                    new Vector2(-90f, 0f), new Vector2(0f, 60f), new Vector2(60f, -60f), new Vector2(-60f, 60f) })
                {
                    equipment.look.SetLookAngles(angles.x, oldYaw + angles.y);
                    Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                    Tick(equipment, graph, 0f, true);
                    for (int side = 0; side < arms.Length; side++)
                    {
                        float angle = Quaternion.Angle(rotations[side], Quaternion.Inverse(rig.ViewRotation) * arms[side].hand.rotation);
                        float pathError = Vector3.Distance(paths[side], Quaternion.Inverse(rig.ViewRotation) *
                            (arms[side].hand.position - arms[side].upperArm.position));
                        maxAngle = Mathf.Max(maxAngle, angle); maxPathError = Mathf.Max(maxPathError, pathError);
                        Require(angle < 0.15f && pathError < 0.0005f,
                            $"fists follow camera at sample={sample}, pitch/yaw={angles}, side={side}: rotation error={angle:F3}deg, path={pathError * 1000f:F3}mm");
                    }
                }
            }
            equipment.Runtime.CancelAction();
            foreach (float heading in new[] { 90f, 179f, -90f })
            {
                equipment.spine.animationRoot.rotation = Quaternion.AngleAxis(heading, Vector3.up) * oldRoot;
                equipment.look.SetLookAngles(0f, oldYaw + heading);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Tick(equipment, graph, 0f, true);
                var rotations = arms.Select(arm => Quaternion.Inverse(rig.ViewRotation) * arm.hand.rotation).ToArray();
                equipment.look.SetLookAngles(75f, oldYaw + heading + 50f);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Tick(equipment, graph, 0f, true);
                for (int side = 0; side < arms.Length; side++)
                    Require(Quaternion.Angle(rotations[side], Quaternion.Inverse(rig.ViewRotation) * arms[side].hand.rotation) < 0.15f,
                        "fists follow after body turns: " + heading);
            }
        }
        finally
        {
            equipment.Runtime.CancelAction();
            equipment.spine.animationRoot.rotation = oldRoot;
            equipment.look.SetLookAngles(oldPitch, oldYaw);
            Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
            Tick(equipment, graph, 1f, true);
        }
        Debug.Log($"[SampleEquipmentSceneChecks] Fists guard/punch follow camera at 0/±45/±90 pitch and ±60 yaw: " +
            $"max wrist orientation error={maxAngle:F3}deg, shoulder-relative path error={maxPathError * 1000f:F3}mm.");
    }

    private static void CheckFistsMotionLifecycle(EquipmentSceneController equipment, PlayableGraph graph)
    {
        var rig = equipment.look.cameraRig;
        var arms = new[] { equipment.handIK.leftHand, equipment.handIK.rightHand };
        float oldPitch = equipment.look.CameraPitch, oldYaw = equipment.look.CameraYaw;
        try
        {
            equipment.look.SetLookAngles(70f, oldYaw + 50f);
            Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
            equipment.Step(0f);
            Invoke(equipment.spine, "Update");
            Invoke(equipment.weaponPose, "PreparePose", 0f);
            Invoke(equipment.handIK, "Update");
            graph.Evaluate(0f);
            Invoke(equipment.spine, "LateUpdate");
            Invoke(rig, "ApplyCamera", 0f);
            Invoke(equipment.weaponPose, "ApplyPreparedPose", 0f);
            Quaternion[] before = arms.Select(arm => arm.upperArm.localRotation).ToArray();
            Transform head = equipment.spine.animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 headPosition = head.position, cameraPosition = rig.realCamera.position;
            Quaternion headRotation = head.rotation, cameraRotation = rig.realCamera.rotation;
            Transform finger = equipment.spine.animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Quaternion fingerRotation = finger.localRotation;
            equipment.ApplyMotion();
            Require(Quaternion.Angle(before[0], arms[0].upperArm.localRotation) > 5f,
                "camera follow is an active correction, not a vacuous idle test");
            var hands = arms.Select(arm => arm.hand.position).ToArray();
            var solved = arms.Select(arm => arm.upperArm.localRotation).ToArray();
            for (int repeat = 0; repeat < 60; repeat++)
            {
                equipment.ApplyMotion();
                for (int side = 0; side < arms.Length; side++)
                    Require(Vector3.Distance(hands[side], arms[side].hand.position) < 0.00001f &&
                        Quaternion.Angle(solved[side], arms[side].upperArm.localRotation) < 0.1f,
                        "repeated fist follow does not accumulate");
            }
            Require(Vector3.Distance(headPosition, head.position) < 0.00001f && Quaternion.Angle(headRotation, head.rotation) < 0.1f &&
                Vector3.Distance(cameraPosition, rig.realCamera.position) < 0.00001f && Quaternion.Angle(cameraRotation, rig.realCamera.rotation) < 0.1f &&
                Quaternion.Angle(fingerRotation, finger.localRotation) < 0.1f, "follow does not write head, camera or finger pose");
            equipment.Step(0f);
            for (int side = 0; side < arms.Length; side++)
                Require(Quaternion.Angle(before[side], arms[side].upperArm.localRotation) < 0.1f,
                    "Update restores animated arm pose before next Animator evaluation");
            equipment.ApplyMotion();
            Quaternion external = arms[0].upperArm.localRotation * Quaternion.Euler(7f, 3f, 0f);
            arms[0].upperArm.localRotation = external;
            equipment.Step(0f);
            Require(Quaternion.Angle(external, arms[0].upperArm.localRotation) < 0.1f, "restore preserves subsequent external writes");
            Tick(equipment, graph, 0f, true);
            hands = arms.Select(arm => arm.hand.position).ToArray();
            headPosition = head.position; cameraPosition = rig.realCamera.position;
            for (int repeat = 0; repeat < 60; repeat++)
            {
                Tick(equipment, graph, 0f, true);
                Require(Vector3.Distance(headPosition, head.position) < 0.00001f &&
                    Vector3.Distance(cameraPosition, rig.realCamera.position) < 0.00001f,
                    "frozen graph clock does not drift body or camera with fists follow");
                for (int side = 0; side < arms.Length; side++)
                    Require(Vector3.Distance(hands[side], arms[side].hand.position) < 0.00001f,
                        "frozen graph clock does not drift fists");
            }
            equipment.Step(0f);
            before = arms.Select(arm => arm.upperArm.localRotation).ToArray();
            equipment.ApplyMotion();
            Invoke(equipment, "OnDisable");
            for (int side = 0; side < arms.Length; side++)
                Require(Quaternion.Angle(before[side], arms[side].upperArm.localRotation) < 0.1f,
                    "disable removes fist follow before restoring original grips");
            Invoke(equipment, "OnEnable");
            Tick(equipment, graph, 10f, true);
            Require(equipment.SelectedSlot == 1 && equipment.handIK.leftHand.weight == 0f && equipment.handIK.rightHand.weight == 0f,
                "reenable returns to fists without rifle IK");
        }
        finally
        {
            equipment.look.SetLookAngles(oldPitch, oldYaw);
            Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
            Tick(equipment, graph, 1f, true);
        }
        Debug.Log("[SampleEquipmentSceneChecks] Fists camera follow lifecycle passed: no accumulated rotation, camera/head/finger writes, " +
            "external-write preservation, restore before Animator and disable/reenable cleanup.");
    }

    private static object Invoke(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private static void Require(bool value, string message)
    { if (!value) throw new Exception("[SampleEquipmentSceneChecks] " + message); }
}
#endif
