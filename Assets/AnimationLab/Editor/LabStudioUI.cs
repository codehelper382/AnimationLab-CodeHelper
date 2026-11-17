using UnityEditor;
using UnityEngine;

namespace CodeHelper.AnimationLab.Editor
{
    internal static class LabStudioUI
    {
        internal static readonly Color Accent = new Color(.22f, .82f, .77f);
        internal static readonly Color Violet = new Color(.65f, .56f, 1);
        internal static readonly Color[] Phases = { new Color(.4f,.55f,.75f), new Color(.23f,.75f,.63f), new Color(.3f,.62f,.95f), new Color(.7f,.55f,.95f), new Color(.94f,.68f,.34f), new Color(.5f,.65f,.65f) };
        internal static GUIStyle Title => new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, normal = { textColor = Color.white } };
        internal static GUIStyle Subtitle => new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(.68f,.75f,.8f) } };
        internal static GUIStyle Button => new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(12,12,8,8) };
        internal static void Section(string title, string hint = null)
        {
            EditorGUILayout.Space(8); EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect line = GUILayoutUtility.GetRect(1, 2, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(line, Accent * new Color(1,1,1,.55f));
            if (hint != null) EditorGUILayout.LabelField(hint, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3);
        }
        internal static bool Primary(string text)
        {
            Color old = GUI.backgroundColor; GUI.backgroundColor = Accent;
            bool clicked = GUILayout.Button(text, Button, GUILayout.Height(36)); GUI.backgroundColor = old; return clicked;
        }
    }
}
