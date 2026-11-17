#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using CodeHelper.ProceduralShooterFramework.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class KnifeEquipmentChecks
{
    public static void Check(EquipmentSceneController equipment, PlayableGraph graph)
    {
        int previousSlot = equipment.SelectedSlot;
        float pitch = equipment.look.CameraPitch, yaw = equipment.look.CameraYaw;
        var slot = equipment.slots[2];
        var definition = (MeleeDefinition)slot.definition;
        var attack = definition.attacks.Single();
        Transform motion = slot.visualRoot.Find("Weapon Motion");
        Transform blade = motion.Find("Blade");
        Vector3 rootPosition = slot.visualRoot.localPosition; Quaternion rootRotation = slot.visualRoot.localRotation;
        Vector3 guardOffset = slot.rightHandPose.guard.localPosition;
        Quaternion guardOffsetRotation = slot.rightHandPose.guard.localRotation;
        var info = JsonUtility.FromJson<KnifeAnimationSetup.BakeInfo>(AssetDatabase.LoadAssetAtPath<TextAsset>(KnifeAnimationSetup.Folder + "/Knife_Bake.json").text);
        GameObject reference = null, source = null;
        float maxRigError = 0f, maxGripError = 0f, maxWristResidual = 0f, fingerError = 0f;
        int limited = 0;
        try
        {
            Require(!definition.isUnarmed && !definition.canAim && slot.blendWeaponClip && attack.weaponClip != null && attack.characterClip.isHumanMotion,
                "knife uses its own compatible finger animation and coordinated rig recipe");
            Require(slot.visualRoot.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
                !slot.visualRoot.IsChildOf(equipment.handIK.leftHand.upperArm) && !slot.visualRoot.IsChildOf(equipment.handIK.rightHand.upperArm),
                "knife visual uses no vendor components and is outside both arm chains");
            Require(blade.GetComponent<MeshFilter>().sharedMesh != null && blade.GetComponent<Renderer>().sharedMaterials.All(material => material != null),
                "knife model has its original mesh and materials");
            Require(Mathf.Abs(info.duration - attack.duration) < 0.0001f && Mathf.Abs(attack.weaponClip.length - attack.characterClip.length) < 0.001f,
                "weapon/hand rig and character fingers preserve original duration");
            reference = UnityEngine.Object.Instantiate(definition.visualPrefab);
            SceneManager.MoveGameObjectToScene(reference, equipment.gameObject.scene);
            source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(KnifeAnimationSetup.Source + "/C_Knife_Attack.fbx"));
            SceneManager.MoveGameObjectToScene(source, equipment.gameObject.scene);
            var sourceWeapon = new GameObject("WeaponBone").transform;
            sourceWeapon.SetParent(source.transform.Find("Skeleton"), false);
            Transform sourceHand = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Right_Hand");
            var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(info.sourceAttack);
            Require(equipment.SelectSlot(2), "select knife as third equipment"); Frame(equipment, graph, 10f);
            Require(equipment.SelectedSlot == 2 && equipment.Runtime.IsReady && slot.visualRoot.gameObject.activeSelf &&
                !equipment.slots[0].visualRoot.gameObject.activeSelf && equipment.handIK.leftHand.weight == 1f && equipment.handIK.rightHand.weight == 1f,
                "knife equips alone with independent hand IK");
            RequireRest(slot.visualRoot, info);
            foreach (float lookPitch in new[] { 0f, 45f, 90f, -45f, -90f })
            {
                equipment.look.SetLookAngles(lookPitch, yaw);
                Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
                Frame(equipment, graph, 1f);
                int contacts = equipment.Runtime.Current.MeleeContactCount;
                Require(equipment.Runtime.TryMeleeAttack(), "start knife at camera pitch " + lookPitch);
                float last = 0f;
                foreach (float progress in new[] { 0.02f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 0.98f })
                {
                    Frame(equipment, graph, (progress - last) * attack.duration); last = progress;
                    float actualProgress = equipment.Runtime.Current.Action.Progress;
                    float weight = EquipmentSceneController.ActionWeight(attack, actualProgress);
                    attack.weaponClip.SampleAnimation(reference, actualProgress * attack.weaponClip.length);
                    for (int node = 0; node < info.paths.Length; node++)
                    {
                        Transform expected = reference.transform.Find(info.paths[node]);
                        Transform actual = slot.visualRoot.Find(info.paths[node]);
                        Vector3 position = Vector3.Lerp(info.guard[node].position, expected.localPosition, weight);
                        Quaternion rotation = Quaternion.Slerp(info.guard[node].rotation, expected.localRotation, weight);
                        float error = Vector3.Distance(actual.localPosition, position);
                        maxRigError = Mathf.Max(maxRigError, error);
                        Require(error < 0.00002f && Quaternion.Angle(actual.localRotation, rotation) < 0.1f,
                            "all authored rig channels use the same clock and blend at " + progress);
                    }
                    Require(Vector3.Distance(equipment.handIK.rightHand.target.position, slot.rightHandPose.guard.position) < 0.00001f &&
                        Vector3.Distance(equipment.handIK.leftHand.target.position, slot.leftHandPose.guard.position) < 0.00001f,
                        "both hand goals follow their sampled motion nodes");
                    if (progress < 0.03f)
                    {
                        slot.rightHandPose.guard.localPosition += new Vector3(0.01f, 0f, 0f);
                        Frame(equipment, graph, 0f);
                        Require(Vector3.Distance(slot.rightHandPose.guard.localPosition, guardOffset + new Vector3(0.01f, 0f, 0f)) < 0.00001f,
                            "partial weapon-clip blend never attenuates unanimated user adjustment points");
                        slot.rightHandPose.guard.localPosition = guardOffset; Frame(equipment, graph, 0f);
                    }
                    maxWristResidual = Mathf.Max(maxWristResidual, WristResidual(equipment.handIK.rightHand, ref limited), WristResidual(equipment.handIK.leftHand, ref limited));
                    if (weight >= 0.999f)
                    {
                        sourceClip.SampleAnimation(source, actualProgress * sourceClip.length);
                        Quaternion authoredWrist = Quaternion.Inverse(sourceWeapon.rotation) * sourceHand.rotation;
                        Vector3 grip = sourceWeapon.InverseTransformPoint(sourceHand.position) * info.retargetScale + authoredWrist * guardOffset;
                        Vector3 actualGrip = motion.InverseTransformPoint(equipment.handIK.rightHand.target.position);
                        float gripError = Vector3.Distance(grip, actualGrip); maxGripError = Mathf.Max(maxGripError, gripError);
                        // Local-rig resampling removes tiny constraint drift from the source's
                        // interpolated world hierarchy between its original 60 Hz keys.
                        Require(gripError < 0.001f, $"original right-hand/weapon relation at p={actualProgress:F4}: " +
                            $"expected={grip:F5}, actual={actualGrip:F5}, error={gripError * 1000f:F3}mm");
                        Require(Quaternion.Angle(Quaternion.Inverse(motion.rotation) * equipment.handIK.rightHand.target.rotation,
                            authoredWrist * guardOffsetRotation) < 0.15f, "artist grip rotation is retained on top of authored wrist motion");
                    }
                    var layer = Get(equipment.spine, "_equipmentLayer");
                    Require((AnimationClip)Get(layer, "_actionClip") == attack.characterClip &&
                        Math.Abs(((AnimationClipPlayable)Get(layer, "_action")).GetTime() - actualProgress * attack.characterClip.length) < 0.0001,
                        "finger clip shares the authoritative action progress");
                    Require(equipment.Runtime.Current.MeleeContactCount == contacts + (actualProgress >= attack.commitTime ? 1 : 0), "knife contact commits once at its milestone");
                }
                Frame(equipment, graph, attack.duration);
                RequireRest(slot.visualRoot, info);
                Require(equipment.Runtime.Current.MeleeContactCount == contacts + 1, "knife attack completes once");
            }

            equipment.look.SetLookAngles(0f, yaw); Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f); Frame(equipment, graph, 1f);
            Require(equipment.Runtime.TryMeleeAttack(), "start knife finger/control sample"); Frame(equipment, graph, attack.duration * 0.35f);
            var fingers = Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>().Where(bone => bone.ToString().Contains("Proximal") ||
                bone.ToString().Contains("Intermediate") || bone.ToString().Contains("Distal")).Select(bone => equipment.spine.animator.GetBoneTransform(bone)).Where(bone => bone != null).ToArray();
            Quaternion[] fingerPose = fingers.Select(bone => bone.localRotation).ToArray();
            Invoke(equipment.handIK, "Update"); Invoke(equipment.spine, "Update");
            double poseTime = (double)Get(equipment, "_idleTime") % definition.characterPose.length;
            equipment.spine.SetEquipmentAnimation(definition.characterPose, definition.characterMask, poseTime,
                attack.characterClip, attack.characterMask, equipment.Runtime.Current.Action.Progress * attack.characterClip.length,
                EquipmentSceneController.ActionWeight(attack, equipment.Runtime.Current.Action.Progress));
            graph.Evaluate(0f);
            for (int i = 0; i < fingers.Length; i++) fingerError = Mathf.Max(fingerError, Quaternion.Angle(fingerPose[i], fingers[i].localRotation));
            Require(fingerError < 0.15f, "finger poses match native unfiltered original-clip playback at identical clocks");
            Frame(equipment, graph, 0f);
            Vector3 bladePosition = blade.position; Quaternion bladeRotation = blade.rotation;
            slot.rightHandPose.guard.localPosition += new Vector3(0.01f, 0f, 0f);
            slot.rightHandPose.guard.localRotation *= Quaternion.Euler(0f, 5f, 0f);
            Frame(equipment, graph, 0f);
            Require(Vector3.Distance(bladePosition, blade.position) < 0.00001f && Quaternion.Angle(bladeRotation, blade.rotation) < 0.1f &&
                Vector3.Distance(equipment.handIK.rightHand.target.position, slot.rightHandPose.guard.position) < 0.00001f,
                "editable hand offset is preserved without moving the weapon mesh");
            slot.rightHandPose.guard.SetLocalPositionAndRotation(guardOffset, guardOffsetRotation);
            Frame(equipment, graph, 0f);
            Vector3 frozen = motion.position;
            for (int i = 0; i < 30; i++) { Frame(equipment, graph, 0f); Require(Vector3.Distance(frozen, motion.position) < 0.00001f, "knife playback has no frozen-clock drift"); }
            Require(equipment.Runtime.CancelAction(), "knife action can cancel"); Frame(equipment, graph, 1f); RequireRest(slot.visualRoot, info);
            Require(equipment.Runtime.TryMeleeAttack(), "start knife disable cleanup check"); Frame(equipment, graph, attack.duration * 0.25f);
            var knifeInstance = equipment.Runtime.Current;
            int knifeContacts = knifeInstance.MeleeContactCount;
            Invoke(equipment, "OnDisable"); RequireRest(slot.visualRoot, info);
            Require(!slot.visualRoot.gameObject.activeSelf, "disable restores original knife visibility");
            Invoke(equipment, "OnEnable"); Frame(equipment, graph, 10f);
            Require(equipment.Runtime.Current == knifeInstance && knifeInstance.MeleeContactCount == knifeContacts,
                "reenable preserves knife instance without inventing a contact");
            RequireRest(slot.visualRoot, info);
            foreach (int rate in new[] { 30, 60, 144 })
            {
                int contacts = equipment.Runtime.Current.MeleeContactCount;
                Require(equipment.Runtime.TryMeleeAttack(), "continuous knife starts");
                for (int i = 0; i < Mathf.CeilToInt(attack.duration * rate) + 2; i++) Frame(equipment, graph, 1f / rate, true);
                RequireRest(slot.visualRoot, info);
                Require(equipment.Runtime.Current.MeleeContactCount == contacts + 1, "continuous knife confirms once at " + rate);
                Frame(equipment, graph, 1f, true);
            }
            Require(Vector3.Distance(rootPosition, slot.visualRoot.localPosition) < 0.00001f && Quaternion.Angle(rootRotation, slot.visualRoot.localRotation) < 0.1f,
                "independent knife root was not animated or reparented");
        }
        finally
        {
            equipment.Runtime.CancelAction();
            slot.rightHandPose.guard.SetLocalPositionAndRotation(guardOffset, guardOffsetRotation);
            equipment.look.SetLookAngles(pitch, yaw); Invoke(equipment.look, "ProcessLookFrame", Vector2.zero, 0f, false, 0f);
            Frame(equipment, graph, 1f);
            if (equipment.SelectedSlot != previousSlot) { Require(equipment.SelectSlot(previousSlot), "restore equipment after knife check"); Frame(equipment, graph, 10f); }
            if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
        }
        Debug.Log($"[KnifeEquipmentChecks] Passed authored knife: weapon/both hands/elbows share clock and blends, original grip relation, " +
            $"native fingers, view 0/±45/±90, cancellation/return, independent root and 30/60/144 Hz; rig error={maxRigError * 1000f:F3}mm, " +
            $"original grip error={maxGripError * 1000f:F3}mm, physical reach residual={maxWristResidual * 1000f:F3}mm, limited samples={limited}, finger error={fingerError:F3}deg.");
    }

    private static float WristResidual(HandIKChain hand, ref int limited)
    {
        Vector3 target = hand.target.TransformPoint(hand.positionOffset);
        float upper = Vector3.Distance(hand.upperArm.position, hand.forearm.position), lower = Vector3.Distance(hand.forearm.position, hand.hand.position);
        float distance = Vector3.Distance(hand.upperArm.position, target);
        float unavoidable = Mathf.Max(0f, distance - upper - lower, Mathf.Abs(upper - lower) - distance);
        if (unavoidable > 0.003f) limited++;
        float residual = Mathf.Max(0f, Vector3.Distance(hand.hand.position, target) - unavoidable);
        Require(residual < 0.003f, "knife hand tracks within anatomical reach"); return residual;
    }
    private static void RequireRest(Transform root, KnifeAnimationSetup.BakeInfo info)
    {
        for (int i = 0; i < info.paths.Length; i++)
        {
            Transform node = root.Find(info.paths[i]);
            Require(Vector3.Distance(node.localPosition, info.guard[i].position) < 0.00002f && Quaternion.Angle(node.localRotation, info.guard[i].rotation) < 0.1f,
                "knife rig returns to authored guard");
        }
    }
    private static void Frame(EquipmentSceneController equipment, PlayableGraph graph, float dt, bool advance = false)
    {
        equipment.Step(dt); Invoke(equipment.spine, "Update"); Invoke(equipment.weaponPose, "PreparePose", dt); Invoke(equipment.handIK, "Update");
        graph.Evaluate(advance ? dt : 0f); Invoke(equipment.spine, "LateUpdate"); Invoke(equipment.look.cameraRig, "ApplyCamera", dt);
        Invoke(equipment.weaponPose, "ApplyPreparedPose", dt); equipment.ApplyMotion(); Invoke(equipment.handIK, "LateUpdate");
    }
    private static object Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
    private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("[KnifeEquipmentChecks] " + message); }
}
#endif
