#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using CodeHelper.ProceduralShooterFramework.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

namespace CodeHelper.ProceduralShooterFramework.EditorTools
{
    /// <summary>Native, isolated authoring only. No KINEMATION code or importer changes on vendor assets.</summary>
    public static class KnifeAnimationSetup
    {
        public const string Folder = EquipmentAssetPaths.Knife;
        public const string Source = "Assets/kindemo/Demo 1/Meshes/Weapons/Knife";
        public static readonly string[] RigPaths = { "Weapon Motion", "Weapon Motion/Left Motion", "Weapon Motion/Right Motion",
            "Weapon Motion/Left Elbow", "Weapon Motion/Right Elbow" };

        [Serializable]
        public sealed class BakeInfo
        {
            public string sourcePose, sourceAttack;
            public float duration, frameRate;
            public float retargetScale;
            public Vector3 sourceViewAnchor;
            public string[] paths;
            public Pose[] guard;
        }

        public static void Configure()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Author in an isolated batch validation project only.");
            if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Knife assets already exist; refusing overwrite.");
            string working = AssetDatabase.GenerateUniqueAssetPath(Folder + "_Build");
            SampleEquipmentSetup.EnsureFolder(EquipmentAssetPaths.Melee);
            AssetDatabase.CreateFolder(EquipmentAssetPaths.Melee, Path.GetFileName(working));
            try
            {
                AssetDatabase.CreateFolder(working, "Humanoid");
                AnimationClip attack = CopyHumanoid(Source + "/C_Knife_Attack.fbx", working + "/Humanoid/Knife_Attack.fbx");
                AnimationClip pose = FreezePose(attack);
                AssetDatabase.CreateAsset(pose, working + "/Knife_Pose.anim");
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(EquipmentAssetPaths.ArmsMask);
                Bake(working, pose, attack, mask);
                AssetDatabase.SaveAssets();
                string error = AssetDatabase.MoveAsset(working, Folder);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            }
            catch
            {
                if (AssetDatabase.IsValidFolder(working)) AssetDatabase.DeleteAsset(working); // Only this invocation's staging folder.
                throw;
            }
            var scene = EditorSceneManager.OpenScene(EquipmentAssetPaths.Scene);
            var equipment = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EquipmentSceneController>(true)).Single();
            if (equipment.slots.Length != 2) throw new Exception("Expected AK12/fists only; refusing to replace existing slots.");
            var definition = AssetDatabase.LoadAssetAtPath<MeleeDefinition>(Folder + "/Knife_Definition.asset");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(definition.visualPrefab, scene);
            Transform view = equipment.look.cameraRig.cameraHolder != null ? equipment.look.cameraRig.cameraHolder : equipment.look.cameraRig.transform;
            model.transform.SetParent(view, false);
            model.SetActive(false);
            var slot = new EquipmentSceneSlot
            {
                definition = definition, visualRoot = model.transform, blendWeaponClip = true,
                leftHandPose = new EquipmentHandBinding { guard = model.transform.Find(RigPaths[1] + "/Left Guard"),
                    elbowHint = model.transform.Find(RigPaths[3] + "/Left Elbow Hint") },
                rightHandPose = new EquipmentHandBinding { guard = model.transform.Find(RigPaths[2] + "/Right Guard"),
                    elbowHint = model.transform.Find(RigPaths[4] + "/Right Elbow Hint") }
            };
            equipment.slots = equipment.slots.Concat(new[] { slot }).ToArray();
            EditorUtility.SetDirty(equipment); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[KnifeAnimationSetup] Authored knife created as slot 3: coordinated weapon/hands/elbows clip, " +
                "native Humanoid fingers in the existing graph, plain mesh prefab and independent view rig. No vendor components.");
        }

        private static AnimationClip FreezePose(AnimationClip attack)
        {
            // Freeze the matching attack's first frame, not an unrelated locomotion avatar's static pose.
            var pose = UnityEngine.Object.Instantiate(attack); pose.name = "Knife_Pose";
            foreach (var binding in AnimationUtility.GetCurveBindings(pose))
            {
                float value = AnimationUtility.GetEditorCurve(pose, binding).Evaluate(0f);
                AnimationUtility.SetEditorCurve(pose, binding, AnimationCurve.Constant(0f, 1f / 60f, value));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(pose);
            settings.startTime = 0f; settings.stopTime = 1f / 60f; settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(pose, settings);
            if (!pose.isHumanMotion) throw new Exception("Frozen knife pose lost Humanoid muscle data.");
            return pose;
        }

        private static AnimationClip CopyHumanoid(string source, string destination)
        {
            if (!AssetDatabase.CopyAsset(source, destination)) throw new Exception("Could not copy " + source);
            var importer = (ModelImporter)AssetImporter.GetAtPath(destination);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            var clips = importer.clipAnimations;
            if (clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (var clip in clips) clip.loopTime = false;
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new Exception("Invalid owned Humanoid avatar: " + destination);
            var animation = AssetDatabase.LoadAllAssetsAtPath(destination).OfType<AnimationClip>().First(item => !item.name.StartsWith("__preview"));
            if (!animation.isHumanMotion) throw new Exception("Humanoid clip conversion failed.");
            return animation;
        }

        private static void Bake(string folder, AnimationClip pose, AnimationClip attack, AvatarMask mask)
        {
            var scene = EditorSceneManager.OpenPreviewScene(EquipmentAssetPaths.Scene);
            GameObject source = null, rig = null;
            EquipmentSceneController equipment = null;
            try
            {
                source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/C_Knife_Attack.fbx"));
                SceneManager.MoveGameObjectToScene(source, scene);
                source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform skeleton = source.transform.Find("Skeleton");
                Transform sourceRight = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Right_Hand");
                Transform sourceWeapon = new GameObject("WeaponBone").transform; sourceWeapon.SetParent(skeleton, false);
                var rawPose = AssetDatabase.LoadAssetAtPath<AnimationClip>(Source + "/C_Knife_Pose.anim");
                var rawAttack = AssetDatabase.LoadAssetAtPath<AnimationClip>(Source + "/C_Knife_Attack.anim");
                if (Mathf.Abs(rawAttack.length - attack.length) > 0.001f) throw new Exception("Original and converted attack clocks differ.");
                Transform[] originals = source.GetComponentsInChildren<Transform>(true);
                Vector3[] positions = originals.Select(item => item.localPosition).ToArray();
                Quaternion[] rotations = originals.Select(item => item.localRotation).ToArray();
                Vector3[] scales = originals.Select(item => item.localScale).ToArray();
                equipment = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EquipmentSceneController>(true)).Single();
                var animator = equipment.spine.animator;
                animator.Rebind(); animator.Update(0f);
                Transform sourceLeft = originals.Single(item => item.name == "Left_Hand");
                Transform sourceLeftElbow = originals.Single(item => item.name == "Left_LowerArm");
                Transform sourceRightElbow = originals.Single(item => item.name == "Right_LowerArm");
                Transform sourceUpper = originals.Single(item => item.name == "Right_UpperArm");
                rawPose.SampleAnimation(source, 0f);
                float sourceReach = Vector3.Distance(sourceUpper.position, sourceRightElbow.position) + Vector3.Distance(sourceRightElbow.position, sourceRight.position);
                float targetReach = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position, animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position) +
                    Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position, animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                float retargetScale = targetReach / sourceReach;
                Vector3 sourceViewAnchor = originals.Single(item => item.name == "Head").position + equipment.look.cameraRig.offset;
                // Export the authored source rig relative to its neutral eye position. IK performs
                // the character adaptation at runtime; unrelated Humanoid torso poses cannot turn
                // the weapon behind the camera or distort the authored weapon/hand relationship.
                Pose[] Sample(float progress, bool action)
                {
                    for (int i = 0; i < originals.Length; i++)
                    {
                        originals[i].SetLocalPositionAndRotation(positions[i], rotations[i]); originals[i].localScale = scales[i];
                    }
                    (action ? rawAttack : rawPose).SampleAnimation(source, action ? progress * rawAttack.length : 0f);
                    Pose weapon = new Pose(sourceWeapon.position, sourceWeapon.rotation);
                    Pose weaponInView = new Pose((weapon.position - sourceViewAnchor) * retargetScale, weapon.rotation);
                    Pose Scaled(Transform child)
                    {
                        Pose result = Relative(weapon, child); result.position *= retargetScale; return result;
                    }
                    return new[] { weaponInView, Scaled(sourceLeft), Scaled(sourceRight), Scaled(sourceLeftElbow), Scaled(sourceRightElbow) };
                }
                Pose[] guard = Sample(0f, false);
                var motion = EquipmentRigClipBaker.Bake("Knife_AttackRig", attack.length, 60f, RigPaths, progress => Sample(progress, true));
                AssetDatabase.CreateAsset(motion, folder + "/Knife_AttackRig.anim");
                Sample(0f, false); // Restore the source mesh's calibration frame.
                var renderer = source.GetComponentsInChildren<MeshRenderer>(true).Single(item => item.name == "bayonet_AK74");
                Pose meshInWeapon = Relative(new Pose(sourceWeapon.position, sourceWeapon.rotation), renderer.transform);
                meshInWeapon.position *= retargetScale;
                Vector3 meshScale = renderer.transform.lossyScale * retargetScale;
                rig = new GameObject("Knife Presentation"); SceneManager.MoveGameObjectToScene(rig, scene);
                for (int i = 0; i < RigPaths.Length; i++)
                {
                    string parentPath = i == 0 ? "" : "Weapon Motion";
                    var node = new GameObject(RigPaths[i].Split('/').Last()).transform;
                    node.SetParent(i == 0 ? rig.transform : rig.transform.Find(parentPath), false);
                    node.SetLocalPositionAndRotation(guard[i].position, guard[i].rotation);
                }
                AddPoint(rig.transform.Find(RigPaths[1]), "Left Guard");
                AddPoint(rig.transform.Find(RigPaths[2]), "Right Guard");
                AddPoint(rig.transform.Find(RigPaths[3]), "Left Elbow Hint");
                AddPoint(rig.transform.Find(RigPaths[4]), "Right Elbow Hint");
                var blade = new GameObject("Blade"); blade.transform.SetParent(rig.transform.Find(RigPaths[0]), false);
                blade.transform.SetLocalPositionAndRotation(meshInWeapon.position, meshInWeapon.rotation); blade.transform.localScale = meshScale;
                blade.AddComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                blade.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                var prefab = PrefabUtility.SaveAsPrefabAsset(rig, folder + "/Knife_Presentation.prefab");
                MeleeDefinition definition = EquipmentTemplates.CreateMelee(false);
                definition.name = definition.equipmentId = "Knife"; definition.displayName = "Cuchillo";
                definition.visualPrefab = prefab; definition.characterPose = pose; definition.characterMask = mask;
                definition.attackInterval = 1f;
                definition.equip.duration = definition.unequip.duration = 0.25f;
                var hit = definition.attacks.Single(); hit.name = "Knife Attack";
                hit.characterClip = attack; hit.characterMask = mask; hit.weaponClip = motion;
                hit.duration = attack.length; hit.blendIn = hit.blendOut = 0.15f; hit.commitTime = 0.5f;
                AssetDatabase.CreateAsset(definition, folder + "/Knife_Definition.asset");
                foreach (var action in EquipmentTemplates.Actions(definition)) AssetDatabase.AddObjectToAsset(action, definition);
                EditorUtility.SetDirty(definition);
                var info = new BakeInfo { sourcePose = Source + "/C_Knife_Pose.anim", sourceAttack = Source + "/C_Knife_Attack.anim",
                    duration = attack.length, frameRate = 60f, paths = RigPaths, guard = guard,
                    retargetScale = retargetScale, sourceViewAnchor = sourceViewAnchor };
                File.WriteAllText(folder + "/Knife_Bake.json", JsonUtility.ToJson(info, true)); AssetDatabase.ImportAsset(folder + "/Knife_Bake.json");
                Debug.Log($"[KnifeAnimationSetup] Baked original knife at {attack.length:F4}s/60fps: " +
                    $"weapon view guard={guard[0].position:F4}, right relative={guard[2].position:F4}; mesh scale={meshScale:F4}.");
            }
            finally
            {
                if (equipment != null) Invoke(equipment.spine, "TearDown");
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Pose Relative(Pose root, Transform child) => new Pose(Quaternion.Inverse(root.rotation) * (child.position - root.position), Quaternion.Inverse(root.rotation) * child.rotation);
        private static void AddPoint(Transform parent, string name) => new GameObject(name).transform.SetParent(parent, false);
        private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
        private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);

        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EquipmentAssetPaths.Scene);
            GameObject source = null;
            try
            {
                source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/C_Knife_Attack.fbx"));
                SceneManager.MoveGameObjectToScene(source, scene);
                source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationClip pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(Source + "/C_Knife_Pose.anim");
                AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(Source + "/C_Knife_Attack.anim");
                Debug.Log("[KnifeAnimationSetup] Source hierarchy: " + string.Join(", ", source.GetComponentsInChildren<Transform>(true).Select(item => item.name)));
                Transform right = source.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "Right_Hand");
                Transform weapon = source.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "WeaponBone");
                if (weapon == null) { weapon = new GameObject("WeaponBone").transform; weapon.SetParent(source.transform.Find("Skeleton"), false); }
                if (right == null) throw new Exception("Source wrist missing.");
                var embedded = AssetDatabase.LoadAllAssetsAtPath(Source + "/C_Knife_Attack.fbx").OfType<AnimationClip>().First(item => !item.name.StartsWith("__preview"));
                Debug.Log("[KnifeAnimationSetup] FBX animated paths: " + string.Join(", ", AnimationUtility.GetCurveBindings(embedded).Select(item => item.path).Distinct().Where(path => path.Contains("Knife") || path.Contains("gun") || path.Contains("Weapon"))));
                Debug.Log("[KnifeAnimationSetup] Renderers: " + string.Join("; ", source.GetComponentsInChildren<Renderer>().Select(item => item.name + " " + item.GetType().Name + " bounds=" + item.bounds)));
                pose.SampleAnimation(source, 0f);
                Debug.Log($"[KnifeAnimationSetup] Source scale={source.transform.lossyScale}, guard wrist={right.position:F4}, weapon={weapon.position:F4}; " +
                    $"pose={pose.length:F4}s, attack={attack.length:F4}s @ {attack.frameRate}fps, humanoid={attack.isHumanMotion}.");
                foreach (float progress in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    attack.SampleAnimation(source, progress * attack.length);
                    Debug.Log($"[KnifeAnimationSetup] Source p={progress:F2}: hand={right.position:F4}, weapon={weapon.position:F4}, " +
                        $"weapon-in-hand={right.InverseTransformPoint(weapon.position):F4}, localRotation={Quaternion.Inverse(right.rotation) * weapon.rotation}.");
                }
                var equipment = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CodeHelper.ProceduralShooterFramework.Equipment.EquipmentSceneController>(true)).Single();
                Transform[] bones = equipment.spine.animator.GetComponentsInChildren<Transform>(true);
                Debug.Log($"[KnifeAnimationSetup] Actual animator={equipment.spine.animator.name}, scale={equipment.spine.animator.transform.lossyScale}, " +
                    $"WeaponBone exists={bones.Any(item => item.name == "WeaponBone")}, source bones={source.GetComponentsInChildren<Transform>(true).Length}.");
                var mesh = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("e7c130c499b47e249abc51146659f4f5")).OfType<Mesh>().Single(item => item.GetInstanceID() != 0);
                Debug.Log($"[KnifeAnimationSetup] Mesh={mesh.name}, bounds={mesh.bounds}, size at prefab scale100={mesh.bounds.size * 100f:F4}.");
            }
            finally { if (source != null) UnityEngine.Object.DestroyImmediate(source); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
