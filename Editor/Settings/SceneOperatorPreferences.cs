using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SceneOperator.Editor
{
    /// <summary>Edit &gt; Preferences &gt; Scene Operator.</summary>
    internal static class SceneOperatorPreferences
    {
        public const string Path = "Preferences/Scene Operator";

        private static string s_RecordingShortcutId;
        private static string s_Error;

        [SettingsProvider]
        private static SettingsProvider Create()
        {
            return new SettingsProvider(Path, SettingsScope.User)
            {
                label = "Scene Operator",
                keywords = new HashSet<string>(new[] { "scene", "collection", "switcher", "shortcut", "color", "editor" }),
                guiHandler = _ => Draw(),
            };
        }

        private static void Draw()
        {
            EditorGUIUtility.labelWidth = 240f;
            GUILayout.Space(8f);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawShortcuts();
                GUILayout.Space(10f);
                DrawWindowSection();
                GUILayout.Space(10f);
                DrawSwitcherSection();
                GUILayout.Space(10f);
                DrawAppearance();
            }
        }

        // ---------------------------------------------------------------- shortcuts

        private const string UserProfileId = "User";

        private static void DrawShortcuts()
        {
            EditorGUILayout.LabelField("Shortcuts", EditorStyles.boldLabel);

            bool canRebind = SceneOperatorShortcuts.CanRebind;
            using (new EditorGUI.DisabledScope(!canRebind))
            {
                DrawShortcutRow("Open scene switcher", SceneOperatorShortcuts.SwitcherId);
                DrawShortcutRow("Open Scene Operator window", SceneOperatorShortcuts.WindowId);
            }

            if (!canRebind)
            {
                EditorGUILayout.HelpBox(
                    "Unity's \"Default\" shortcut profile is read-only, so shortcuts cannot be changed yet. " +
                    "Creating a personal profile keeps every other shortcut as it is; you can switch back in Edit > Shortcuts.",
                    MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    if (GUILayout.Button("Create Personal Shortcut Profile", GUILayout.Width(240f)))
                    {
                        if (!SceneOperatorShortcuts.CreateUserProfile(UserProfileId, out string profileError))
                            s_Error = "Could not create the profile: " + profileError;
                        else
                            s_Error = null;
                    }
                }
            }

            if (!string.IsNullOrEmpty(s_Error))
                EditorGUILayout.HelpBox(s_Error, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button("Open Unity Shortcuts Window", GUILayout.Width(240f)))
                    EditorApplication.ExecuteMenuItem("Edit/Shortcuts...");
                GUILayout.Label("Profile: " + SceneOperatorShortcuts.ActiveProfile, EditorStyles.miniLabel);
            }
        }

        private static void DrawShortcutRow(string label, string shortcutId)
        {
            bool recording = s_RecordingShortcutId == shortcutId;
            string binding = SceneOperatorShortcuts.BindingText(shortcutId);
            string buttonText = recording ? "Press a key combination..." : (binding.Length > 0 ? binding : "Not set");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent(label, "Click the button and press the new combination. Esc cancels."));

                if (recording)
                    CaptureShortcut(shortcutId);

                if (GUILayout.Button(buttonText, recording ? EditorStyles.toolbarButton : EditorStyles.popup, GUILayout.Width(180f)))
                {
                    s_RecordingShortcutId = recording ? null : shortcutId;
                    s_Error = null;
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("Reset", GUILayout.Width(60f)))
                {
                    SceneOperatorShortcuts.ResetBinding(shortcutId);
                    s_RecordingShortcutId = null;
                    s_Error = null;
                }
            }
        }

        private static void CaptureShortcut(string shortcutId)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            if (e.keyCode == KeyCode.Escape)
            {
                s_RecordingShortcutId = null;
                e.Use();
                return;
            }

            if (IsModifierKey(e.keyCode) || e.keyCode == KeyCode.None)
                return;

            var modifiers = ShortcutModifiers.None;
            if (e.control || e.command)
                modifiers |= ShortcutModifiers.Action;
            if (e.shift)
                modifiers |= ShortcutModifiers.Shift;
            if (e.alt)
                modifiers |= ShortcutModifiers.Alt;

            s_RecordingShortcutId = null;
            if (!SceneOperatorShortcuts.TryRebind(shortcutId, e.keyCode, modifiers, out string error))
                s_Error = "Could not assign that combination: " + error;
            else
                s_Error = null;

            e.Use();
        }

        private static bool IsModifierKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                    return true;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- sections

        private static void DrawWindowSection()
        {
            EditorGUILayout.LabelField("Window", EditorStyles.boldLabel);
            SceneOperatorSettings.ShowGroupNames = EditorGUILayout.Toggle(
                new GUIContent("Show group names", "Draw the group name above each divider instead of only the divider line."),
                SceneOperatorSettings.ShowGroupNames);
            SceneOperatorSettings.RowHeight = EditorGUILayout.IntSlider(
                new GUIContent("Row height", "Height of a scene row, in points."),
                SceneOperatorSettings.RowHeight, 18, 32);
        }

        private static void DrawSwitcherSection()
        {
            EditorGUILayout.LabelField("Scene switcher", EditorStyles.boldLabel);
            SceneOperatorSettings.SwitcherIncludesProjectScenes = EditorGUILayout.Toggle(
                new GUIContent("Include all project scenes", "Besides the selected collection, list every scene of the project."),
                SceneOperatorSettings.SwitcherIncludesProjectScenes);
            SceneOperatorSettings.SwitcherShowPaths = EditorGUILayout.Toggle(
                new GUIContent("Show folders", "Show the folder of each scene next to its name."),
                SceneOperatorSettings.SwitcherShowPaths);
            SceneOperatorSettings.SwitcherMaxHeight = EditorGUILayout.IntSlider(
                new GUIContent("Max height", "Longer lists get a scrollbar."),
                SceneOperatorSettings.SwitcherMaxHeight, 200, 900);
        }

        private static void DrawAppearance()
        {
            EditorGUILayout.LabelField("Colors (" + (SceneOperatorSettings.IsDarkTheme ? "dark" : "light") + " editor theme)", EditorStyles.boldLabel);

            foreach (SceneOperatorSettings.ColorSetting color in SceneOperatorSettings.Colors)
                color.Value = EditorGUILayout.ColorField(color.Content, color.Value, true, true, false);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button("Reset Colors To Defaults", GUILayout.Width(220f)))
                    SceneOperatorSettings.ResetColors();
            }
        }
    }
}
