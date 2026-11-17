using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    public sealed partial class AnimationLabWindow
    {
        private int studioTab;
        private int phaseBoundaryDrag;
        private bool showTechnicalKeys;
        private void DrawStudioHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(100, 76, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.065f,.085f,.11f));
            EditorGUI.DrawRect(new Rect(rect.x,rect.y,4,rect.height), LabStudioUI.Accent);
            GUI.Label(new Rect(rect.x+18,rect.y+9,370,30), "ANIMATION LAB  /  STUDIO", LabStudioUI.Title);
            string state = HasTakeTrial ? "● PRUEBA TEMPORAL" : saved == null ? "● TOMA NUEVA · SIN GUARDAR" : dirty || Pending ? "● BORRADOR SIN GUARDAR" : "● LISTO";
            GUI.Label(new Rect(rect.x+19,rect.y+42,Mathf.Max(250,rect.width-330),24), preview == null ? "AUTORÍA EN PRIMERA PERSONA · VISTA AISLADA · SIN PLAY" : state + "   /   " + takeName, LabStudioUI.Subtitle);
            if (preview != null && GUI.Button(new Rect(rect.xMax-390,rect.y+19,80,36), "Inicio", LabStudioUI.Button))
                Safe(() => { ReturnHome(); GUIUtility.ExitGUI(); });
            int reviewCount = reviewCache == null ? 0 : reviewCache.Count(i => i.severity == MessageType.Error || i.severity == MessageType.Warning);
            if (preview != null && reviewCount > 0 && !quarantinedRecovery)
                if (GUI.Button(new Rect(rect.xMax-300,rect.y+19,120,36), "Revisar · " + reviewCount, LabStudioUI.Button))
                    if (CanLeaveFrame()) { studioTab = 3; scroll = Vector2.zero; }
            using (new EditorGUI.DisabledScope(preview == null || HasTakeTrial || quarantinedRecovery))
                if (GUI.Button(new Rect(rect.xMax-170,rect.y+19,150,36), "Guardar copia", LabStudioUI.Button)) Safe(SaveTake);
        }
        private void DrawStudioInspector()
        {
            int tab = GUILayout.Toolbar(studioTab, new[] { "RECARGAS", "ANIMAR", "PROYECTO", "REVISAR" }, GUILayout.Height(30));
            if (tab != studioTab && CanLeaveFrame()) { studioTab = tab; scroll = Vector2.zero; }
            DrawModeChoice();
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.LabelField(notice, EditorStyles.wordWrappedMiniLabel);
            if (studioTab != 3)
            {
                if (simpleMode) DrawBeginnerGuide(); else DrawNextStep();
            }
            switch (studioTab)
            {
                case 0:
                    if(HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial)EditorGUILayout.HelpBox("Acepta o cancela la prueba actual en la barra del visor.",MessageType.Info);
                    DrawGuidedReloadEditor();
                    using(new EditorGUI.DisabledScope(HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial))DrawHandTemplates();break;
                case 1:
                    if (HasHandTrial) EditorGUILayout.HelpBox("La prueba no modifica tu toma. Acepta o cancela en Recargas antes de editar claves manuales.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(HasTakeTrial)) DrawHand();
                    break;
                case 2:
                    using (new EditorGUI.DisabledScope(HasTakeTrial)) { DrawSetup(); DrawSave(); if(!simpleMode)DrawReloadWizard(); }
                    DrawRecoveryPreferences();
                    if (HasHandTrial) EditorGUILayout.HelpBox("Acepta o cancela la prueba para cambiar equipo, toma o guardar.", MessageType.Info);
                    DrawQuickHelp(); break;
                default:
                    DrawReviewPanel(); break;
            }
        }
        private void DrawGesturePhaseBar()
        {
            if(HasWeaponMotionTrial||HasTransitionTrial||HasGuidedReloadTrial)return;
            if (studioTab != 0 && !HasHandTrial) return;
            var r = GestureSettings; var bounds = LabGestureAuthoring.Bounds(r);
            Rect bar = GUILayoutUtility.GetRect(200, 40, GUILayout.ExpandWidth(true));
            var e = Event.current;
            int id=GUIUtility.GetControlID("LabPhaseBoundary".GetHashCode(),FocusType.Passive);
            // Boundaries own MouseDown before any phase label, even for a sub-pixel narrow phase.
            if(e.type==EventType.MouseDown&&e.button==0&&bar.Contains(e.mousePosition))
            {
                int nearest=-1;float distance=7;
                for(int i=1;i<6;i++){float x=bar.x+bar.width*Mathf.Lerp(r.start,r.end,bounds[i]);float d=Mathf.Abs(e.mousePosition.x-x);if(d<distance){distance=d;nearest=i;}}
                if(nearest>=0){Undo.RecordObject(GestureOwner,"Ajustar fase");r.phaseTimes=(float[])bounds.Clone();phaseBoundaryDrag=nearest;selectedPhase=nearest-1;GUIUtility.hotControl=id;e.Use();}
            }
            if(GUIUtility.hotControl==id&&e.type==EventType.MouseDrag)
            {
                float u=Mathf.InverseLerp(r.start,r.end,Mathf.InverseLerp(bar.x,bar.xMax,e.mousePosition.x));
                r.phaseTimes[phaseBoundaryDrag]=Mathf.Clamp(u,r.phaseTimes[phaseBoundaryDrag-1]+.005f,r.phaseTimes[phaseBoundaryDrag+1]-.005f);GestureChanged();e.Use();
            }
            if(GUIUtility.hotControl==id&&e.type==EventType.MouseUp){GUIUtility.hotControl=0;e.Use();if(HasHandTrial&&trialStale)Safe(RebuildHandTrial);}
            bounds=LabGestureAuthoring.Bounds(r);
            for (int i = 0; i < 6; i++)
            {
                int phase = i;
                float a = Mathf.Lerp(r.start, r.end, bounds[i]), b = Mathf.Lerp(r.start, r.end, bounds[i+1]);
                Rect cell = new Rect(bar.x+bar.width*a,bar.y+2,Mathf.Max(2,bar.width*(b-a)-2),34);
                EditorGUI.DrawRect(cell, LabStudioUI.Phases[i] * new Color(1,1,1,selectedPhase == i ? .85f : .4f));
                string label = LabGestureAuthoring.PhaseNames[i];
                string frames=$"F {LabTimeline.Frame(a,draft.action.duration)}–{LabTimeline.Frame(b,draft.action.duration)}";
                if (GUI.Button(new Rect(cell.x+6,cell.y,Mathf.Max(1,cell.width-12),cell.height), new GUIContent(label+"\n"+frames, $"{label} · {frames}\nClic: ir a la fase. Arrastra la división para cambiar su duración."), new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip }))
                    Safe(() => { selectedPhase = phase; Seek(a); });
            }
            for (int i = 1; i < 6; i++)
            {
                float t = Mathf.Lerp(r.start,r.end,bounds[i]); float x = bar.x+bar.width*t;
                Rect grab = new Rect(x-5,bar.y,10,bar.height); EditorGUIUtility.AddCursorRect(grab,MouseCursor.ResizeHorizontal);
                if(Event.current.type==EventType.Repaint)EditorGUI.DrawRect(new Rect(x-1,bar.y+4,2,30),new Color(.8f,.85f,.9f,.75f));
            }
        }
        private float[] VisibleTimes(int track)
        {
            var range = TimelineRange();
            var values = Times(track).Where(t => t >= range.x - LabTimeline.TimeEpsilon
                && t <= range.y + LabTimeline.TimeEpsilon).OrderBy(t => t).ToArray();
            if (showTechnicalKeys || values.Length <= 40) return values;
            return values.Where((t,i)=>i%Mathf.CeilToInt(values.Length/32f)==0 || i==values.Length-1).ToArray();
        }
    }
}
