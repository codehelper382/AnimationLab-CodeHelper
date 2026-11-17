#if UNITY_EDITOR
using CodeHelper.ProceduralShooterFramework.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.ProceduralShooterFramework.Editor
{
    /// <summary>Explicit authoring only: no ExecuteAlways, automatic captures or scene modifications.</summary>
    public static class WeaponPoseAuthoring
    {
        /// <summary>Explicit presets affect only transient motion, never Idle/Aim or transition time.</summary>
        public static void ApplyMotionPreset(WeaponPoseProfile profile, bool exaggerated)
        {
            if (profile == null) return;
            Undo.RecordObject(profile, "Ajustar rebote del arma");
            var motion = new WeaponAimMotionSettings { enabled = true };
            if (exaggerated)
            {
                motion.frequency = 2f;
                motion.damping = 0.2f;
                motion.aimPositionKick = new Vector3(0f, 0.08f, -0.05f);
                motion.idlePositionKick = new Vector3(0f, -0.06f, 0.04f);
                motion.aimRotationKick = new Vector3(-12f, 0f, 6f);
                motion.idleRotationKick = new Vector3(10f, 0f, -5f);
                motion.maxPositionOffset = 0.1f;
                motion.maxRotationOffset = 15f;
            }
            profile.aimMotion = motion;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        public static bool Capture(WeaponPoseController controller, WeaponPoseState state)
        {
            if (controller == null || controller.profile == null || !controller.TryValidateRoot(out _)) return false;
            WeaponPose pose = controller.GetPoseForCapture();
            if (!pose.IsFinite) return false;
            Undo.RecordObject(controller.profile, "Capturar pose " + state);
            if (!controller.profile.StorePose(state, pose)) return false;
            EditorUtility.SetDirty(controller.profile);
            AssetDatabase.SaveAssetIfDirty(controller.profile);
            return true;
        }

        public static bool Preview(WeaponPoseController controller, WeaponPoseState state)
        {
            if (Application.isPlaying || controller == null || controller.profile == null ||
                !controller.TryValidateRoot(out _) || !controller.profile.TryGetPose(state, out WeaponPose pose)) return false;
            Transform root = controller.PoseRoot;
            Undo.RecordObject(root, "Previsualizar pose " + state);
            root.SetLocalPositionAndRotation(pose.localPosition, pose.Rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            if (root.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return true;
        }
    }

    [CustomEditor(typeof(WeaponPoseController))]
    public class WeaponPoseControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (WeaponPoseController)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Captura de poses locales", EditorStyles.boldLabel);
            if (!controller.TryValidateRoot(out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }
            EditorGUILayout.HelpBox("Referencia local: " + controller.PoseRoot.parent.name +
                ". Conserva el mismo padre para Idle y Aim. No se captura la cámara ni se mueven los grips por separado.", MessageType.Info);

            if (controller.profile == null)
            {
                EditorGUILayout.HelpBox("Tu colocación actual es Aim: crea primero el perfil desde ella. Idle quedará pendiente.", MessageType.Info);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                    if (GUILayout.Button("Crear perfil y guardar Aim actual...")) CreateProfile(controller);
                return;
            }

            EditorGUILayout.LabelField("Idle", controller.profile.HasIdle ? "Guardado" : "Sin capturar");
            EditorGUILayout.LabelField("Aim", controller.profile.HasAim ? "Guardado" : "Sin capturar");
            if (!controller.profile.HasIdle || !controller.profile.HasAim)
                EditorGUILayout.HelpBox("Una pose sin capturar no mueve el arma. Guarda ambas para probar la transición.", MessageType.Warning);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Capturar guarda el ASSET incluso desde Play. Excluye nuestro rebote, pero captura la pose base interpolada actual. No conserva referencias de escena al salir. Previsualización solo fuera de Play.", MessageType.Warning);

            CaptureButton(controller, WeaponPoseState.Idle);
            CaptureButton(controller, WeaponPoseState.Aim);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUI.DisabledScope(!controller.profile.HasIdle))
                    if (GUILayout.Button("Previsualizar Idle")) WeaponPoseAuthoring.Preview(controller, WeaponPoseState.Idle);
                using (new EditorGUI.DisabledScope(!controller.profile.HasAim))
                    if (GUILayout.Button("Previsualizar Aim")) WeaponPoseAuthoring.Preview(controller, WeaponPoseState.Aim);
            }
            if (GUILayout.Button("Validar configuración")) controller.ValidateSetup();
            EditorGUILayout.Space();
            WeaponAimMotionPresetGUI.Draw(controller.profile);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Aim de entrada", controller.useLookController
                    ? controller.lookController != null && controller.lookController.enabled && controller.lookController.IsAiming ? "Activo" : "Inactivo"
                    : "Entrada externa");
                EditorGUILayout.LabelField("Rebote posición (m)", controller.ProceduralPositionOffset.ToString("F4"));
                EditorGUILayout.LabelField("Rebote rotación (grados)", controller.ProceduralRotationOffset.ToString("F2"));
                if (controller.profile.synchronizeAimEffects)
                    EditorGUILayout.LabelField("Transición compartida", (controller.TransitionProgress * 100f).ToString("F0") + "%");
                CameraRigController rig = controller.lookController != null ? controller.lookController.cameraRig : null;
                if (rig != null && rig.aimEffectsSource == controller)
                {
                    EditorGUILayout.LabelField("Aim FOV adicional", rig.AimFovOffset.ToString("F2"));
                    EditorGUILayout.LabelField("Rebote cámara (grados)", rig.AimCameraRotationOffset.ToString("F3"));
                }
                Repaint();
            }
        }

        private static void CaptureButton(WeaponPoseController controller, WeaponPoseState state)
        {
            if (!GUILayout.Button("Guardar posición actual como " + state)) return;
            bool overwrite = state == WeaponPoseState.Idle ? controller.profile.HasIdle : controller.profile.HasAim;
            if ((Application.isPlaying || overwrite) && !EditorUtility.DisplayDialog("Guardar " + state,
                    "Se guardará la transformación local actual en el perfil " + controller.profile.name +
                    ". Afectará a todos los objetos que compartan este asset." +
                    (Application.isPlaying ? " El resultado actual puede incluir movimiento de otros scripts." : ""),
                    "Guardar", "Cancelar")) return;
            WeaponPoseAuthoring.Capture(controller, state);
        }

        private static void CreateProfile(WeaponPoseController controller)
        {
            string path = EditorUtility.SaveFilePanelInProject("Guardar perfil de poses", "AK12_Poses", "asset",
                "Se guardará Aim desde la colocación actual; Idle no se inventará.");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                EditorUtility.DisplayDialog("El asset ya existe", "Elige otro nombre para no sobrescribir un recurso existente.", "Aceptar");
                return;
            }
            var profile = ScriptableObject.CreateInstance<WeaponPoseProfile>();
            if (!profile.StorePose(WeaponPoseState.Aim, WeaponPose.FromLocalTransform(controller.PoseRoot)))
            {
                Object.DestroyImmediate(profile);
                return;
            }
            AssetDatabase.CreateAsset(profile, path);
            Undo.RecordObject(controller, "Asignar perfil de poses");
            controller.profile = profile;
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            if (controller.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            AssetDatabase.SaveAssetIfDirty(profile);
            EditorGUIUtility.PingObject(profile);
        }
    }

    public static class WeaponAimMotionPresetGUI
    {
        public static void Draw(WeaponPoseProfile profile)
        {
            EditorGUILayout.LabelField("Prueba de Aim Motion", EditorStyles.boldLabel);
            bool enabled = profile.aimMotion != null && profile.aimMotion.enabled;
            EditorGUILayout.LabelField("Estado del rebote", enabled ? "Activado" : "Desactivado");
            EditorGUILayout.LabelField("Efectos de cámara Aim", profile.aimCamera != null && profile.aimCamera.enabled ? "Activados" : "Desactivados");
            if (!enabled)
                EditorGUILayout.HelpBox("Sin Aim Motion activado no habrá rebote, aunque Idle/Aim cambien correctamente.", MessageType.Warning);
            if (GUILayout.Button("Rebote MUY visible (prueba)")) Apply(profile, true);
            if (GUILayout.Button("Rebote suave (recomendado)")) Apply(profile, false);
        }

        private static void Apply(WeaponPoseProfile profile, bool exaggerated)
        {
            if (!EditorUtility.DisplayDialog("Cambiar rebote",
                    "Se guardará el preset " + (exaggerated ? "MUY visible de diagnóstico" : "suave") +
                    " en " + profile.name + ". No cambia Idle, Aim ni su duración de transición. " +
                    "Afecta a todos los objetos que compartan este perfil. El preset exagerado puede provocar penetraciones: no es un ajuste final.",
                    "Aplicar", "Cancelar")) return;
            WeaponPoseAuthoring.ApplyMotionPreset(profile, exaggerated);
        }
    }

    [CustomEditor(typeof(WeaponPoseProfile))]
    public class WeaponPoseProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var profile = (WeaponPoseProfile)target;
            if (profile.synchronizeAimEffects)
                EditorGUILayout.HelpBox("Arma, FOV y rebotes usan Transition Duration. Transition Oscillations controla las oscilaciones; Frequency y Fov Blend Speed solo se usan en el modo independiente.", MessageType.Info);
            EditorGUILayout.HelpBox("Idle: " + (profile.HasIdle ? "guardado" : "sin capturar") +
                " | Aim: " + (profile.HasAim ? "guardado" : "sin capturar") +
                ". Captura las poses desde Weapon Pose Controller. Modificar valores de una pose sin capturar no la activa.", MessageType.Info);
            EditorGUILayout.Space();
            WeaponAimMotionPresetGUI.Draw(profile);
        }
    }
}
#endif
