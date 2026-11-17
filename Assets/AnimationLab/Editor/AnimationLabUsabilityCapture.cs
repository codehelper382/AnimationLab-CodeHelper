using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Run only in a temporary verification project: this method exits its Editor process.
    public static class AnimationLabUsabilityCapture
    {
        private static AnimationLabWindow window;
        private static int ticks, stage;
        private static string uiError;
        private static readonly string[] Names = { "Inicio", "Proyecto", "Revisar", "Timeline", "Prueba" };

        public static void Run()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("La captura necesita un editor con ventana, sin -batchmode.");
            // Avoid background thumbnail-import workers racing the isolated Editor's shutdown.
            AssetDatabase.DesiredWorkerCount = 0;
            AssetDatabase.ForceToDesiredWorkerCount();
            Application.logMessageReceived += Log;
            window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            window.titleContent = new GUIContent("Animation Lab · verificación de usabilidad");
            window.position = new Rect(25, 40, 1100, 850);
            window.ShowUtility();
            EditorApplication.update += Update;
        }

        private static void Log(string text, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) uiError = text;
        }

        private static void Update()
        {
            try
            {
                window.Focus();
                window.Repaint();
                if (++ticks < 35) return;
                ticks = 0;
                string error = (string)LabPreview.Get(window, "error");
                if (!string.IsNullOrEmpty(error) || uiError != null) throw new InvalidOperationException(error ?? uiError);
                Capture(Names[stage]);
                if (stage == 0)
                {
                    LabPreview.Call(window, "StartPreview");
                    LabPreview.Set(window, "studioTab", 2);
                }
                else if (stage == 1)
                {
                    var source = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath);
                    LabPreview.Call(window, "OpenTake", source, false);
                    LabPreview.Set(window, "studioTab", 3);
                }
                else if (stage == 2)
                {
                    LabPreview.Set(window, "studioTab", 1);
                    LabPreview.Call(window, "SelectTrack", 4, .5f);
                    LabPreview.Call(window, "SetTimelineZoom", 4f);
                    LabPreview.Call(window, "SetCameraView", 155f, 8f);
                }
                else if (stage == 3)
                {
                    LabPreview.Call(window, "StartWeaponMotionTrial");
                    LabPreview.Set(window, "studioTab", 3);
                }
                else
                {
                    Debug.Log("[AnimationLabUsabilityCapture] PASS: actual home, project, review, zoomed timeline and trial UI rendered without GUI errors.");
                    Finish(0);
                    return;
                }
                stage++;
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        private static void Capture(string name)
        {
            var rect = window.position;
            int width = (int)rect.width, height = (int)rect.height;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, width, height));
                image.Apply();
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "opencode");
                File.WriteAllBytes(Path.Combine(folder, "AnimationLab.Usability." + name + ".png"), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Update;
            Application.logMessageReceived -= Log;
            if (window != null)
            {
                LabPreview.Call(window, "ClosePreview");
                UnityEngine.Object.DestroyImmediate(window);
                window = null;
            }
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
