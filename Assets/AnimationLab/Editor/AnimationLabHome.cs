using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeHelper.ProceduralShooterFramework.Animation;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private Vector2 homeScroll;
        private string takeSearch = "";
        private string recoverySearch = "";
        private string saveSearch = "";
        private bool showAllTakes;
        private bool showVersions;
        private bool showAllRecoveries;
        private bool browseRecoveries;
        private bool showSaveBrowser;
        private bool highlightDuplicateAction;
        private List<AnimationLabTake> takeCache;
        private double takeCacheTime;

        private void DrawHome()
        {
            homeScroll = EditorGUILayout.BeginScrollView(homeScroll);
            LabStudioUI.Section("Bienvenido al laboratorio", "Autoría de animaciones fuera de Play. La escena abierta y los assets originales no se modifican.");
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.LabelField(notice, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.HelpBox("Prepara una copia aislada del personaje y sus equipos. Después elige una mano o el arma, coloca un punto y guárdalo. No necesitas entrar en Play.", MessageType.Info);
            DrawModeChoice();
            if(!simpleMode)mode = (SpineDriveMode)EditorGUILayout.EnumPopup(new GUIContent("Modo de columna", "LateUpdate es el modo normal. AnimationJob permite comparar el otro modo de columna."), mode);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1 · Crear una toma nueva", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Empieza en blanco y elige equipo y acción en Proyecto.", EditorStyles.wordWrappedMiniLabel);
                if (LabStudioUI.Primary("Preparar personaje y crear toma"))
                    Safe(() =>
                    {
                        StartPreview(); studioTab = 2; highlightDuplicateAction = false;
                        notice = "Elige equipo y acción; después pasa a Animar o Recargas.";
                        GUIUtility.ExitGUI();
                    });
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2 · Duplicar una acción existente", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Copia una acción del equipo para corregirla sin tocar el original.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Preparar y elegir acción para duplicar", GUILayout.Height(30)))
                    Safe(() =>
                    {
                        StartPreview(); studioTab = 2; highlightDuplicateAction = true;
                        notice = "En Proyecto, elige «Copiar acción existente».";
                        GUIUtility.ExitGUI();
                    });
            }

            DrawCheckpointList();
            var recoveries = FindTakes(true);
            if (recoveries.Count > 0)
            {
                LabStudioUI.Section("Recuperar trabajo", "Tomas guardadas automáticamente al cerrar, entrar en Play o recompilar.");
                EditorGUILayout.HelpBox("Abrir una recuperación no borra nada. Guarda una copia para conservarla como toma definitiva.", MessageType.None);
                recoverySearch = EditorGUILayout.TextField("Buscar recuperación", recoverySearch);
                if (!string.IsNullOrWhiteSpace(recoverySearch)) recoveries = recoveries.Where(t => Matches(t, recoverySearch)).ToList();
                int limit = showAllRecoveries || !string.IsNullOrWhiteSpace(recoverySearch) ? recoveries.Count : 6;
                DrawTakeList(recoveries, true, limit);
                if (recoveries.Count == 0) EditorGUILayout.LabelField("No hay recuperaciones que coincidan.", EditorStyles.wordWrappedMiniLabel);
                if (recoveries.Count > limit && GUILayout.Button($"Ver todas las recuperaciones ({recoveries.Count})")) showAllRecoveries = true;
            }

            LabStudioUI.Section("Abrir una toma guardada", "Las tomas viven en " + LabAssetIO.Output + ". Abrir una toma no la modifica.");
            takeSearch = EditorGUILayout.TextField(new GUIContent("Buscar", "Filtra por nombre, equipo o tipo."), takeSearch);
            var takes = FindTakes(false);
            if (!string.IsNullOrWhiteSpace(takeSearch)) takes = takes.Where(t => Matches(t, takeSearch)).ToList();
            showVersions=EditorGUILayout.ToggleLeft("Mostrar todas las versiones guardadas",showVersions);
            if(!showVersions&&string.IsNullOrWhiteSpace(takeSearch))
                takes=takes.GroupBy(LabTakeCatalog.Lineage).Select(g=>g.First()).ToList();
            if (takes.Count == 0)
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(takeSearch)
                    ? "No hay tomas guardadas todavía. Crea una y pulsa Guardar copia."
                    : "No hay tomas que coincidan. Prueba otro nombre.", MessageType.None);
            else
            {
                int limit = showAllTakes || !string.IsNullOrWhiteSpace(takeSearch) ? takes.Count : 6;
                DrawTakeList(takes, false, limit);
                if (takes.Count > limit && GUILayout.Button($"Ver todas ({takes.Count})")) showAllTakes = true;
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Guía de inicio rápido")) OpenGuide("GUIA_INICIO_RAPIDO_ANIMATIONLAB.md");
                if (GUILayout.Button("Guía de Studio visual")) OpenGuide("GUIA_STUDIO_VISUAL.md");
                if (GUILayout.Button("Guía de recargas")) OpenGuide("GUIA_RECARGAS_POR_FOTOGRAMAS.md");
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTakeList(List<AnimationLabTake> takes, bool recovery, int limit)
        {
            int count = Mathf.Min(Mathf.Max(0, limit), takes.Count);
            for (int i = 0; i < count; i++)
            {
                var take = takes[i];
                string path = AssetDatabase.GetAssetPath(take);
                string label = recovery
                    ? RecoveryLabel(take.name) + $"   ·   {take.action.duration:F2} s   ·   {take.kind}"
                    : $"{take.name}   ·   {take.action.duration:F2} s   ·   {take.kind}";
                string tooltip = path + "\nEquipo: " + take.equipmentId + "\nClic para abrir una copia editable.";
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Height(24)))
                        Safe(() => { OpenTake(take, recovery); GUIUtility.ExitGUI(); });
                    if (GUILayout.Button(new GUIContent("Revelar", "Muestra el asset en el explorador."), EditorStyles.miniButton, GUILayout.Width(58)))
                        EditorUtility.RevealInFinder(path);
                }
            }
        }

        private void DrawSaveBrowser()
        {
            showSaveBrowser = EditorGUILayout.Foldout(showSaveBrowser, "Buscar tomas guardadas", true);
            if (!showSaveBrowser) return;
            browseRecoveries = GUILayout.Toolbar(browseRecoveries ? 1 : 0, new[] { "Tomas", "Recuperaciones" }) == 1;
            saveSearch = EditorGUILayout.TextField("Buscar", saveSearch);
            var takes = FindTakes(browseRecoveries);
            if (!string.IsNullOrWhiteSpace(saveSearch)) takes = takes.Where(t => Matches(t, saveSearch)).ToList();
            if (takes.Count == 0)
            {
                EditorGUILayout.HelpBox("No hay tomas que coincidan. Prueba otro nombre.", MessageType.None);
                return;
            }
            int shown = Mathf.Min(8, takes.Count);
            for (int i = 0; i < shown; i++)
            {
                var take = takes[i];
                string path = AssetDatabase.GetAssetPath(take);
                if (GUILayout.Button(new GUIContent($"{take.name}   ·   {take.action.duration:F2} s   ·   {take.kind}", path), EditorStyles.miniButton))
                    Safe(() => { OpenTake(take, browseRecoveries); GUIUtility.ExitGUI(); });
            }
            if (takes.Count > shown) EditorGUILayout.LabelField($"… y {takes.Count - shown} más. Usa Buscar para filtrar.", EditorStyles.wordWrappedMiniLabel);
        }

        private void OpenTake(AnimationLabTake take, bool recovery)
        {
            if (take == null) return;
            recovery |= take.name.StartsWith("Recovery_", StringComparison.Ordinal);
            if (HasTakeTrial) throw new InvalidOperationException("Acepta o cancela la prueba antes de abrir otra toma.");
            bool createdPreview = preview == null;
            if (!createdPreview && !CanDiscard()) return;
            try
            {
                if (createdPreview)
                {
                    preview = new LabPreview(mode);
                    lastTime = EditorApplication.timeSinceStartup;
                }
                if(recovery&&LabTakeValidation.Inspect(take).Count>0)
                {
                    var repaired=LabEmergencyRecovery.Restore(LabEmergencyRecovery.Capture(take,take.name,
                        AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(take)),LabTakeCatalog.Lineage(take),null));
                    try{LoadInvalidRecovery(repaired);repaired=null;}
                    finally{LabAssetIO.DestroyDraft(repaired);}
                }
                else Load(take);
            }
            catch
            {
                if (createdPreview) ClosePreview();
                throw;
            }

            string originalName = take.name;
            if (recovery)
            {
                // A recovery is a working draft, never a replacement for the original take.
                saved = null; takeName = "Toma recuperada"; dirty = true;
                notice = quarantinedRecovery
                    ? "Recuperación abierta sin evaluar. Corrige sus datos y revalida; el asset original no cambia."
                    : "Recuperación abierta. Guarda una copia para conservarla.";
            }
            else
            {
                notice = originalName == "BASE CORRECTA"
                    ? "Toma de referencia abierta como copia. Al guardar se crea una toma nueva; el original no cambia."
                    : "Toma abierta: " + originalName;
            }
            studioTab = 1; scroll = Vector2.zero; showSaveBrowser = false;
            Repaint();
        }

        private List<AnimationLabTake> FindTakes(bool recoveries)
        {
            if (takeCache == null || EditorApplication.timeSinceStartup - takeCacheTime > 1.5)
            {
                takeCache = new List<AnimationLabTake>();
                string[] guids = AssetDatabase.IsValidFolder(LabAssetIO.Output)
                    ? AssetDatabase.FindAssets("t:AnimationLabTake", new[] { LabAssetIO.Output }) : Array.Empty<string>();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var take = AssetDatabase.LoadAssetAtPath<AnimationLabTake>(path);
                    if (take != null && take.action != null) takeCache.Add(take);
                }
                takeCache.Sort((a, b) => File.GetLastWriteTimeUtc(AssetDatabase.GetAssetPath(b)).CompareTo(File.GetLastWriteTimeUtc(AssetDatabase.GetAssetPath(a))));
                takeCacheTime = EditorApplication.timeSinceStartup;
            }
            return takeCache.Where(t => t != null && t.action != null
                && t.name.StartsWith("Recovery_", StringComparison.Ordinal) == recoveries).ToList();
        }

        private static bool Matches(AnimationLabTake take, string search)
        {
            search = (search ?? "").Trim();
            if (search.Length == 0) return true;
            return take.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || (take.equipmentId ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || take.kind.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string RecoveryLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Recuperación";
            string stamp = name.StartsWith("Recovery_", StringComparison.Ordinal) ? name.Substring("Recovery_".Length) : name;
            return "Recuperación · " + stamp.Replace('_', ' ');
        }

        private void OpenGuide(string fileName)
        {
            string path = "Assets/" + fileName;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null && AssetDatabase.OpenAsset(asset)) return;
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath)) EditorUtility.OpenWithDefaultApp(fullPath);
            else notice = "No se encontró la guía: " + path;
        }

        private void SaveRecoveryNow()
        {
            if (draft == null || preview == null) return;
            if(quarantinedRecovery){latestEmergencyPath=LabEmergencyRecovery.Write(CaptureEmergency(),"Reparación pendiente");notice="Respaldo conservado: "+latestEmergencyPath;return;}
            SaveRecoveryDraft();
            takeCache = null;
            if (string.IsNullOrEmpty(notice)) notice = "Recuperación guardada en " + LabAssetIO.Output;
        }

        private void ReturnHome()
        {
            if (draft != null && (dirty || Pending || HasTakeTrial))
            {
                int choice = EditorUtility.DisplayDialogComplex("Volver al inicio",
                    "Hay cambios o una prueba temporal. Puedes conservar el borrador como recuperación antes de volver. La prueba sin aceptar no se inserta.",
                    "Guardar recuperación y volver", "Cancelar", "Descartar y volver");
                if (choice == 1) return;
                if (choice == 0) SaveRecoveryNow();
            }
            ClosePreview();
            takeCache = null;
        }
    }
}
