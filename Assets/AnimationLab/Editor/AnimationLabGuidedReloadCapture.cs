using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Run only in the isolated validation project; this capture exits its own Unity Editor process.
    public static class AnimationLabGuidedReloadCapture
    {
        private static AnimationLabWindow window;
        private static int ticks, stage;
        private static string uiError;

        public static void Run()
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("La captura necesita el Editor gráfico sin -batchmode.");
            AssetDatabase.DesiredWorkerCount = 0;
            AssetDatabase.ForceToDesiredWorkerCount();
            Application.logMessageReceived += OnLog;
            window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            window.titleContent = new GUIContent("Animation Lab · recarga guiada");
            window.position = new Rect(35, 60,
                Mathf.Clamp(Screen.currentResolution.width - 100, 1100, 1500),
                Mathf.Clamp(Screen.currentResolution.height - 140, 760, 940));
            window.ShowUtility();
            LabPreview.Call(window, "StartPreview");
            LabPreview.Call(window, "NewDraft", 0);
            EditorApplication.update += Update;
        }

        private static void OnLog(string text, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) uiError = text;
        }

        private static void Update()
        {
            try
            {
                window.Focus();
                window.Repaint();
                if (++ticks < 35) return;
                ticks = 0;
                string windowError = (string)LabPreview.Get(window, "error");
                if (!string.IsNullOrEmpty(windowError) || uiError != null)
                    throw new InvalidOperationException(windowError ?? uiError);

                if (stage == 0)
                {
                    Capture("AnimationLab.GuidedReload.UI.Empty.png");
                    ConfigureFixturePattern();
                    LabPreview.Set(window, "guidedReloadWizardMode", true);
                    LabPreview.Set(window, "guidedReloadWizardStep", 0);
                    stage = 1;
                    return;
                }

                if (stage == 1)
                {
                    Capture("AnimationLab.GuidedReload.UI.Configured.png");
                    Capture("AnimationLab.GuidedReload.UI.WizardStep0.png");
                    LabPreview.Set(window, "guidedReloadWizardStep", 1);
                    stage = 2;
                    return;
                }

                if (stage == 2)
                {
                    Capture("AnimationLab.GuidedReload.UI.WizardStep1.png");
                    LabPreview.Set(window, "guidedReloadWizardStep", 2);
                    stage = 3;
                    return;
                }

                if (stage == 3)
                {
                    Capture("AnimationLab.GuidedReload.UI.Calibration.png");
                    Capture("AnimationLab.GuidedReload.UI.WizardStep2.png");
                    LabPreview.Set(window, "guidedReloadWizardStep", 3);
                    stage = 4;
                    return;
                }

                if (stage == 4)
                {
                    Capture("AnimationLab.GuidedReload.UI.WizardStep3.png");
                    LabPreview.Set(window, "guidedReloadWizardMode", false);
                    LabPreview.Set(window, "scroll", new Vector2(0f, 620f));
                    stage = 5;
                    return;
                }

                Capture("AnimationLab.GuidedReload.UI.Advanced.png");
                Debug.Log("[AnimationLabGuidedReloadCapture] PASS: empty start, wizard steps 0-3 and advanced editor rendered without GUI errors.");
                Finish(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(1);
            }
        }

        private static void ConfigureFixturePattern()
        {
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            var preview = (LabPreview)LabPreview.Get(window, "preview");
            var ids = (string[])LabPreview.Call(window, "ObjectIds");
            string magazine = ids.FirstOrDefault(id => id == "part:AK12Rig/Root/Mag");
            if (magazine == null)
                throw new InvalidOperationException("La captura necesita el Mag del fixture AK12 en el equipo 0.");

            var plan = LabGuidedReloadEditing.CreateTacticalMagazinePlan(take.equipmentId, magazine);
            take.guidedReload = plan;
            LabPreview.Set(window, "progress", 0.0f);
            preview.Evaluate(take, 0.0f);
            var left = preview.Equipment.handIK.leftHand;
            LabGuidedAnchorCapture.CaptureHand(plan, plan.phases[0], HandGripToolkit.HandSide.Left,
                preview.Guard(HandGripToolkit.HandSide.Left),
                left.target != null ? left.target : left.hand, 0.0f);
            LabGuidedAnchorCapture.CaptureWeapon(plan, plan.phases[0], preview.Equipment.weaponPose.PoseRoot, 0.0f);

            var context = new LabGuidedReloadValidationContext
            {
                equipmentId = take.equipmentId,
                availableHands = LabGuidedHands.Both,
                availableObjectIds = new HashSet<string>(ids, StringComparer.Ordinal),
                requireExportablePartMotion = true
            };
            var issues = LabGuidedReloadValidation.Inspect(plan, context);
            if (issues.Any(issue => issue.severity == LabGuidedIssueSeverity.Error))
                throw new InvalidOperationException("La ficha de captura no pasó su validación: "
                    + string.Join(" | ", issues.Where(i => i.severity == LabGuidedIssueSeverity.Error).Select(i => i.message)));

            EditorUtility.SetDirty(take);
        }

        private static void Capture(string fileName)
        {
            var rect = window.position;
            int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, width, height));
                image.Apply();
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "opencode");
                File.WriteAllBytes(Path.Combine(folder, fileName), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            if (window != null)
            {
                LabPreview.Set(window, "dirty", false);
                UnityEngine.Object.DestroyImmediate(window);
                window = null;
            }
            EditorApplication.Exit(code);
        }
    }
}
