using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public static class AnimationLabUsabilityChecks
    {
        [MenuItem("Tools/CodeHelper/Animation Lab/Verificar inicio, revisión y navegación")]
        public static void Run()
        {
            var original = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(AnimationLabDynamicsChecks.BasePath);
            Require(original != null, "BASE CORRECTA fixture exists");
            byte[] originalBytes = File.ReadAllBytes(AnimationLabDynamicsChecks.BasePath);
            byte[] sceneBytes = File.ReadAllBytes(LabPreview.ScenePath);
            int scenes = EditorSceneManager.previewSceneCount;
            foreach (SpineDriveMode mode in Enum.GetValues(typeof(SpineDriveMode))) Check(original, mode);
            CheckInvalidHome(original);
            Require(originalBytes.SequenceEqual(File.ReadAllBytes(AnimationLabDynamicsChecks.BasePath)), "BASE CORRECTA unchanged byte-for-byte");
            Require(sceneBytes.SequenceEqual(File.ReadAllBytes(LabPreview.ScenePath)), "scene unchanged byte-for-byte");
            Require(scenes == EditorSceneManager.previewSceneCount, "no leaked preview scenes");
            Debug.Log("[AnimationLabUsabilityChecks] PASS: home/load/recovery, read-only review, source recipes, navigation, duration rollback, trial guards and original assets preserved.");
        }

        public static void RunAll()
        {
            Run();
            AnimationLabDynamicsChecks.RunAll();
        }

        private static void Check(AnimationLabTake original, SpineDriveMode mode)
        {
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            AnimationLabTake invalid = null;
            try
            {
                LabPreview.Set(window, "mode", mode);
                LabPreview.Call(window, "OpenTake", original, false);
                var draft = (AnimationLabTake)LabPreview.Get(window, "draft");
                var preview = (LabPreview)LabPreview.Get(window, "preview");
                Require(draft != original && draft.action != original.action, "home opens a deep editable copy");
                string before = Snapshot(draft);
                string[] issues = ReviewTitles(window);
                Require(before == Snapshot(draft), "review never changes draft or action");
                Require(issues.Contains("El contacto termina antes del fundido"), "review detects real contact ending early");
                CheckRecipeReview(window, draft);

                LabPreview.Set(window, "dirty", false);
                bool durationRefused = false;
                try { LabPreview.Call(window, "SetDuration", 45f); }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException
                    && ex.InnerException.Message.Contains("30 segundos")) { durationRefused = true; }
                Require(durationRefused, "duration with contacts rejects >30 s");
                Require(draft.action.duration == original.action.duration, "duration rollback preserves previous value");
                Require(!(bool)LabPreview.Get(window, "dirty"), "rejected duration does not mark clean draft dirty");
                Require(before == Snapshot(draft), "failed duration recompile preserves every channel");
                CheckPieceReview(window, draft);
                CheckRemovalRollback(window, draft);

                // A recipe can animate without any manually recorded authoring points.
                LabPreview.Call(window, "LoadReloadReference", LabHandSource.Reload);
                issues = ReviewTitles(window);
                Require(!issues.Contains("Sin puntos ni fuentes de animación"), "existing reload is not reported as motionless");

                // Exact times, including off-grid points, must survive previous/next navigation.
                draft = (AnimationLabTake)LabPreview.Get(window, "draft");
                draft.weapon.Clear();
                draft.weapon.Add(new LabWeaponKey { time = .123456f });
                draft.weapon.Add(new LabWeaponKey { time = .733333f });
                LabAssetIO.BuildWeaponMotion(draft);
                LabPreview.Call(window, "SelectTrack", 4, .5f);
                LabPreview.Call(window, "SeekAdjacentKey", -1);
                Require(Mathf.Abs((float)LabPreview.Get(window, "progress") - .123456f) < LabTimeline.TimeEpsilon, "previous key preserves exact off-grid time");
                LabPreview.Call(window, "SeekAdjacentKey", 1);
                Require(Mathf.Abs((float)LabPreview.Get(window, "progress") - .733333f) < LabTimeline.TimeEpsilon, "next key preserves exact off-grid time");
                CheckZoom(window);

                LabPreview.Set(window, "dirty", false);
                LabPreview.Call(window, "Load", original);
                draft = (AnimationLabTake)LabPreview.Get(window, "draft");
                LabPreview.Call(window, "StartWeaponMotionTrial");
                var candidate = (AnimationLabTake)LabPreview.Get(window, "weaponMotionCandidate");
                LabPreview.Call(window, "SelectTrack", 4, .501f);
                float next = candidate.weapon.Where(k => k.time > .501f + LabTimeline.TimeEpsilon).Min(k => k.time);
                LabPreview.Call(window, "SeekAdjacentKey", 1);
                Require(Mathf.Abs((float)LabPreview.Get(window, "progress") - next) < LabTimeline.TimeEpsilon, "trial navigation uses displayed candidate, not original points");
                string fingerprint = Snapshot(draft);
                invalid = LabAssetIO.Clone(original);
                invalid.equipmentId = "__incompatible__";
                bool refused = false;
                try { LabPreview.Call(window, "Load", invalid); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { refused = true; }
                Require(refused && fingerprint == Snapshot(draft), "incompatible take rejected without changing draft");
                Require(LabPreview.Get(window, "weaponMotionSource") != null, "rejected load preserves active weapon trial");
                ReviewTitles(window);
                Require(fingerprint == Snapshot(draft), "review during trial leaves draft untouched");
                CheckTrialRecovery(window, draft);
                Debug.Log("[AnimationLabUsabilityChecks] Passed " + mode);
            }
            finally
            {
                LabAssetIO.DestroyDraft(invalid);
                LabPreview.Call(window, "ClosePreview");
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void CheckZoom(AnimationLabWindow window)
        {
            LabPreview.Call(window, "Seek", .5f);
            LabPreview.Call(window, "SetTimelineZoom", 4f);
            var range = (Vector2)LabPreview.Call(window, "TimelineRange");
            Require(Mathf.Abs(range.y - range.x - .25f) < .000001f, "4x zoom shows one quarter of timeline");
            LabPreview.Call(window, "Seek", .55f);
            Require(range == (Vector2)LabPreview.Call(window, "TimelineRange"), "zoom does not recenter on every nearby click");
            foreach (float time in new[] { .9f, 0f, 1f })
            {
                LabPreview.Call(window, "Seek", time);
                range = (Vector2)LabPreview.Call(window, "TimelineRange");
                Require(range.x >= 0f && range.y <= 1f && range.x <= time && range.y >= time, "zoom keeps cursor visible within normalized bounds");
            }
            LabPreview.Call(window, "SetTimelineZoom", 1f);
        }

        private static void CheckPieceReview(AnimationLabWindow window, AnimationLabTake draft)
        {
            var part = draft.parts.First(p => p.keys.Count > 0);
            var duplicate = LabObjectAuthoring.CloneKeys(new System.Collections.Generic.List<LabWeaponKey> { part.keys[0] })[0];
            part.keys.Add(duplicate);
            try
            {
                string before = Snapshot(draft);
                var issues = ((IEnumerable)LabPreview.Call(window, "BuildReviewIssues")).Cast<object>();
                var issue = issues.First(i => ((string)Field(i, "title")).StartsWith("Puntos duplicados en Pieza"));
                Require((string)Field(issue, "objectId") == "part:" + part.path, "piece issue stores exact target identity");
                Require(before == Snapshot(draft), "duplicate/contact diagnostics are read-only");
                LabPreview.Call(window, "JumpToReviewIssue", issue);
                Require((int)LabPreview.Get(window, "studioTab") == 1
                    && (string)LabPreview.Get(window, "objectId") == "part:" + part.path, "piece issue opens correct editor and object");
            }
            finally { part.keys.Remove(duplicate); }
        }

        private static void CheckRemovalRollback(AnimationLabWindow window, AnimationLabTake draft)
        {
            var part = draft.parts.First(p => p.keys.Count >= 3);
            float duration = draft.action.duration;
            LabPreview.Call(window, "SelectObject", "part:" + part.path);
            draft.action.duration = 45f; // Deliberately invalid for contact compilation.
            LabPreview.Set(window, "dirty", false);
            string before = Snapshot(draft);
            try
            {
                bool refused = false;
                try { LabPreview.Call(window, "RemovePoint", part.keys.OrderBy(k => k.time).ElementAt(1).time); }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { refused = true; }
                Require(refused && before == Snapshot(draft), "failed piece deletion/recompile preserves points and all channels");
                Require(!(bool)LabPreview.Get(window, "dirty"), "failed deletion does not mark clean draft dirty");
            }
            finally { draft.action.duration = duration; }
        }

        private static void CheckTrialRecovery(AnimationLabWindow window, AnimationLabTake draft)
        {
            var pathsBefore = AssetDatabase.FindAssets("t:AnimationLabTake", new[] { LabAssetIO.Output })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string position = JsonUtility.ToJson(draft.action.weaponPosition);
            string rotation = JsonUtility.ToJson(draft.action.weaponRotation);
            try
            {
                // Closing a clean draft with only an unaccepted weapon trial must recover the source.
                LabPreview.Set(window, "dirty", false);
                LabPreview.Call(window, "OnDisable");
                var newPaths = AssetDatabase.FindAssets("t:AnimationLabTake", new[] { LabAssetIO.Output })
                    .Select(AssetDatabase.GUIDToAssetPath).Except(pathsBefore).ToArray();
                Require(newPaths.Length == 1 && Path.GetFileName(newPaths[0]).StartsWith("Recovery_"), "closing an otherwise-clean weapon trial creates recovery");
                var recovery = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(newPaths[0]);
                Require(position == JsonUtility.ToJson(recovery.action.weaponPosition)
                    && rotation == JsonUtility.ToJson(recovery.action.weaponRotation), "recovery contains source channels, not unaccepted candidate");
                LabPreview.Call(window, "OpenTake", recovery, false);
                Require((AnimationLabTake)LabPreview.Get(window, "saved") == null
                    && (bool)LabPreview.Get(window, "dirty")
                    && !((string)LabPreview.Get(window, "takeName")).StartsWith("Recovery_"), "recovery auto-detected and opened as unsaved finalizable draft");
            }
            finally
            {
                LabPreview.Call(window, "ClosePreview");
                foreach (string path in AssetDatabase.FindAssets("t:AnimationLabTake", new[] { LabAssetIO.Output })
                    .Select(AssetDatabase.GUIDToAssetPath).Except(pathsBefore).ToArray())
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static void CheckRecipeReview(AnimationLabWindow window, AnimationLabTake originalDraft)
        {
            var temporary = LabAssetIO.Clone(null);
            temporary.slot = originalDraft.slot;
            temporary.equipmentId = originalDraft.equipmentId;
            temporary.action.duration = 1f;
            LabPreview.Set(window, "draft", temporary);
            try
            {
                Require(ReviewTitles(window).Contains("Sin puntos ni fuentes de animación"), "empty recipe has actionable notice");
                var pose = ScriptableObject.CreateInstance<HandGripToolkit.HandPoseData>();
                temporary.left.Add(new LabHandKey { time = 0, fingersOnly = true, fingers = pose });
                Require(!ReviewTitles(window).Contains("Sin puntos ni fuentes de animación"), "finger-only authoring is valid");
                temporary.left.Clear();
                UnityEngine.Object.DestroyImmediate(pose);
                temporary.action.weaponPosition.x = new AnimationCurve(new Keyframe(0, 0, 0, 1), new Keyframe(1, 0, -1, 0));
                Require(temporary.action.weaponPosition.x.Evaluate(.5f) > .1f, "tangent-only fixture actually moves");
                Require(!ReviewTitles(window).Contains("Sin puntos ni fuentes de animación"), "review recognizes tangent-only weapon curves");
                temporary.action.leftHandMotion.enabled = true;
                temporary.action.leftHandMotion.reach = temporary.action.weaponPosition.x;
                temporary.action.weaponPosition = new global::CodeHelper.ProceduralShooterFramework.Equipment.EquipmentVectorCurves();
                Require(!ReviewTitles(window).Contains("Sin puntos ni fuentes de animación"), "review recognizes tangent-only Reach");
            }
            finally
            {
                LabPreview.Set(window, "draft", originalDraft);
                LabAssetIO.DestroyDraft(temporary);
            }
        }

        private static void CheckInvalidHome(AnimationLabTake original)
        {
            int scenes = EditorSceneManager.previewSceneCount;
            var window = ScriptableObject.CreateInstance<AnimationLabWindow>();
            var invalid = LabAssetIO.Clone(original);
            invalid.equipmentId = "__incompatible__";
            try
            {
                bool refused = false;
                try { LabPreview.Call(window, "OpenTake", invalid, false); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { refused = true; }
                Require(refused && LabPreview.Get(window, "preview") == null && LabPreview.Get(window, "draft") == null,
                    "failed home load returns to usable home without half-initialized preview");
                Require(scenes == EditorSceneManager.previewSceneCount, "failed home load cleans preview");
            }
            finally
            {
                LabAssetIO.DestroyDraft(invalid);
                LabPreview.Call(window, "ClosePreview");
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static string[] ReviewTitles(AnimationLabWindow window)
        {
            return ((IEnumerable)LabPreview.Call(window, "BuildReviewIssues")).Cast<object>()
                .Select(i => (string)Field(i, "title")).ToArray();
        }

        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static string Snapshot(AnimationLabTake take) => EditorJsonUtility.ToJson(take) + EditorJsonUtility.ToJson(take.action);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[AnimationLabUsabilityChecks] " + message);
        }
    }
}
