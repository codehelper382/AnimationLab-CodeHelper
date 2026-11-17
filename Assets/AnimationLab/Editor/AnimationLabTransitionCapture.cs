using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    // Only for an isolated verification project: exits that graphical Editor.
    public static class AnimationLabTransitionCapture
    {
        private static AnimationLabWindow window;
        private static int stage, ticks;
        private static string uiError;
        private const string ObjectId = "part:AK12Rig/Root/Mag";
        public static void Run()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("La captura necesita un editor gráfico, sin batchmode.");
            AssetDatabase.DesiredWorkerCount = 0; AssetDatabase.ForceToDesiredWorkerCount();
            Application.logMessageReceived += Log;
            window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            window.ShowUtility(); window.position = new Rect(25, 40, 1050, 760);
            var original = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath);
            LabPreview.Call(window, "OpenTake", original, false);
            LabPreview.Set(window, "studioTab", 1);
            LabPreview.Call(window, "SelectObject", ObjectId);
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            var keys = LabFrameEditing.Objects(take, ObjectId).OrderBy(k => k.time).ToArray();
            var plan = new LabTransitionPlan { track = 5, objectId = ObjectId, from = keys[0].time, to = keys[1].time,
                departureFrame = 70, arrivalFrame = 110, settleFrames = 4, shape = LabTransitionShape.Arc, arcCentimeters = new Vector3(0, 1, 0) };
            LabPreview.Set(window, "transitionPlan", plan); LabPreview.Set(window, "transitionAssistantOpen", true);
            LabPreview.Call(window, "SelectTrack", 5, plan.from);
            LabPreview.Set(window, "scroll", new Vector2(0, 320));
            LabPreview.Call(window, "FocusSelection");
            EditorApplication.update += Update;
        }
        private static void Log(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) uiError = text; }
        private static void Update()
        {
            try
            {
                window.Focus(); window.Repaint();
                if (++ticks < 35) return;
                ticks = 0;
                string error = (string)LabPreview.Get(window, "error");
                if (!string.IsNullOrEmpty(error) || uiError != null) throw new InvalidOperationException(string.IsNullOrEmpty(error) ? uiError : error);
                Capture(stage);
                if (stage == 0) LabPreview.Call(window, "StartTransitionTrial");
                else if (stage == 1)
                {
                    LabPreview.Call(window, "AcceptTransitionTrial");
                    LabPreview.Set(window, "scroll", Vector2.zero);
                }
                else if (stage == 2) Drag(110, 125, false);
                else if (stage == 3) Drag(125, 130, true);
                else
                {
                    Debug.Log("[AnimationLabTransitionCapture] PASS: actual assistant/trial/accept UI, mouse drag F110→125 and Escape cancellation, without GUI errors.");
                    Finish(0); return;
                }
                stage++;
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }
        private static void Drag(int fromFrame, int toFrame, bool cancel)
        {
            var take = (AnimationLabTake)LabPreview.Get(window, "draft");
            string before = EditorJsonUtility.ToJson(take) + EditorJsonUtility.ToJson(take.action);
            var area = (Rect)LabPreview.Get(window, "timelineRowsRect");
            float span = 1f / (float)LabPreview.Get(window, "timelineZoom");
            float start = (float)LabPreview.Get(window, "timelineStart");
            float y = area.y + 22 + 5 * 21 + 10;
            float X(int frame) => area.x + 155 + (LabTimeline.Time(frame, take.action.duration) - start) / span * (area.width - 165);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(X(fromFrame), y) });
            window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(X(toFrame), y) });
            if (cancel) window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(X(toFrame), y) });
            string after = EditorJsonUtility.ToJson(take) + EditorJsonUtility.ToJson(take.action);
            if (cancel)
            {
                if (before != after) throw new InvalidOperationException("Escape modificó la toma.");
            }
            else if (!LabFrameEditing.Objects(take, ObjectId).Any(k => LabFrameEditing.Same(k.time, LabTimeline.Time(toFrame, take.action.duration))))
                throw new InvalidOperationException("El arrastre real no movió el punto al fotograma pedido.");
        }
        private static void Capture(int index)
        {
            var rect = window.position;
            int width = (int)rect.width, height = (int)rect.height;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, width, height)); image.Apply();
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "opencode");
                File.WriteAllBytes(Path.Combine(folder, $"AnimationLab.Transitions.UI.{index}.png"), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Update; Application.logMessageReceived -= Log;
            if (window != null) { LabPreview.Call(window, "ClosePreview"); UnityEngine.Object.DestroyImmediate(window); window = null; }
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
