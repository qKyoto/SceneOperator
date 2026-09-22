using System;
using UnityEditor;
using UnityEngine;

namespace SceneOperator.Editor
{
    /// <summary>
    /// User preferences of the tool. Everything is stored in EditorPrefs (per machine);
    /// colors are stored separately for the dark and the light editor theme.
    /// </summary>
    internal static class SceneOperatorSettings
    {
        private const string Prefix = "SceneOperator.";

        /// <summary>Bumped on every appearance change so open windows can rebuild their styles.</summary>
        public static int AppearanceVersion { get; private set; }

        public static event Action Changed;

        public static bool IsDarkTheme => EditorGUIUtility.isProSkin;

        // ---------------------------------------------------------------- colors

        public static readonly ColorSetting Background = new ColorSetting(
            "Background", "Background", "Background of the window and of the scene switcher.",
            new Color32(0x38, 0x38, 0x38, 0xFF), new Color32(0xC8, 0xC8, 0xC8, 0xFF));

        public static readonly ColorSetting Text = new ColorSetting(
            "Text", "Scene name", "Color of scene names.",
            new Color32(0xE4, 0xE4, 0xE4, 0xFF), new Color32(0x1A, 0x1A, 0x1A, 0xFF));

        public static readonly ColorSetting MutedText = new ColorSetting(
            "MutedText", "Secondary text", "Group names, asset paths and other secondary text.",
            new Color32(0x99, 0x99, 0x99, 0xFF), new Color32(0x5E, 0x5E, 0x5E, 0xFF));

        public static readonly ColorSetting RowHover = new ColorSetting(
            "RowHover", "Row hover", "Background of the row under the mouse.",
            new Color32(0x46, 0x46, 0x46, 0xFF), new Color32(0xBD, 0xBD, 0xBD, 0xFF));

        public static readonly ColorSetting RowLoaded = new ColorSetting(
            "RowLoaded", "Loaded scene", "Background of scenes that are currently open.",
            new Color32(0x2F, 0x44, 0x4A, 0xFF), new Color32(0xBF, 0xD9, 0xDE, 0xFF));

        public static readonly ColorSetting ActiveAccent = new ColorSetting(
            "ActiveAccent", "Active scene marker", "The stripe marking the active scene (the one new objects go into).",
            new Color32(0x4E, 0xC9, 0xB0, 0xFF), new Color32(0x1F, 0x8A, 0x78, 0xFF));

        public static readonly ColorSetting DirtyMarker = new ColorSetting(
            "DirtyMarker", "Unsaved changes marker", "The asterisk shown next to scenes with unsaved changes.",
            new Color32(0xE8, 0xB3, 0x39, 0xFF), new Color32(0xB5, 0x7B, 0x00, 0xFF));

        public static readonly ColorSetting Selection = new ColorSetting(
            "Selection", "Switcher selection", "Background of the highlighted row in the scene switcher.",
            new Color32(0x2C, 0x5D, 0x87, 0xFF), new Color32(0x3A, 0x72, 0xB0, 0xFF));

        public static readonly ColorSetting SelectionText = new ColorSetting(
            "SelectionText", "Switcher selection text", "Text color of the highlighted row in the scene switcher.",
            Color.white, Color.white);

        public static readonly ColorSetting Separator = new ColorSetting(
            "Separator", "Separators and border", "Group separator lines and the switcher's border.",
            new Color32(0x4C, 0x58, 0x5A, 0xFF), new Color32(0xA0, 0xA8, 0xA8, 0xFF));

        public static readonly ColorSetting[] Colors =
        {
            Background, Text, MutedText, RowHover, RowLoaded, ActiveAccent, DirtyMarker, Selection, SelectionText, Separator,
        };

        public static void ResetColors()
        {
            foreach (var color in Colors)
                color.Reset();
            RaiseAppearanceChanged();
        }

        // ---------------------------------------------------------------- window

        public static bool ShowGroupNames
        {
            get => GetBool("ShowGroupNames", false);
            set => SetBool("ShowGroupNames", value, appearance: true);
        }

        public static int RowHeight
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(Prefix + "RowHeight", 22), 18, 32);
            set => SetInt("RowHeight", Mathf.Clamp(value, 18, 32), appearance: true);
        }

        // ---------------------------------------------------------------- switcher

        /// <summary>Also offer scenes that are not part of the selected collection.</summary>
        public static bool SwitcherIncludesProjectScenes
        {
            get => GetBool("SwitcherAllScenes", true);
            set => SetBool("SwitcherAllScenes", value, appearance: false);
        }

        public static bool SwitcherShowPaths
        {
            get => GetBool("SwitcherShowPaths", true);
            set => SetBool("SwitcherShowPaths", value, appearance: true);
        }

        public static int SwitcherMaxHeight
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(Prefix + "SwitcherMaxHeight", 420), 200, 900);
            set => SetInt("SwitcherMaxHeight", Mathf.Clamp(value, 200, 900), appearance: true);
        }

        // ---------------------------------------------------------------- storage

        private static bool GetBool(string key, bool defaultValue) => EditorPrefs.GetBool(Prefix + key, defaultValue);

        private static void SetBool(string key, bool value, bool appearance)
        {
            if (EditorPrefs.GetBool(Prefix + key, value) == value && EditorPrefs.HasKey(Prefix + key))
                return;
            EditorPrefs.SetBool(Prefix + key, value);
            if (appearance)
                RaiseAppearanceChanged();
            else
                Changed?.Invoke();
        }

        private static void SetInt(string key, int value, bool appearance)
        {
            if (EditorPrefs.HasKey(Prefix + key) && EditorPrefs.GetInt(Prefix + key) == value)
                return;
            EditorPrefs.SetInt(Prefix + key, value);
            if (appearance)
                RaiseAppearanceChanged();
            else
                Changed?.Invoke();
        }

        internal static void RaiseAppearanceChanged()
        {
            AppearanceVersion++;
            Changed?.Invoke();
        }

        /// <summary>A single user-editable color with per-theme storage and a per-theme default.</summary>
        internal sealed class ColorSetting
        {
            private readonly Color _defaultDark;
            private readonly Color _defaultLight;

            public ColorSetting(string key, string label, string tooltip, Color defaultDark, Color defaultLight)
            {
                Key = key;
                Content = new GUIContent(label, tooltip);
                _defaultDark = defaultDark;
                _defaultLight = defaultLight;
            }

            public string Key { get; }
            public GUIContent Content { get; }
            public Color DefaultValue => IsDarkTheme ? _defaultDark : _defaultLight;

            public Color Value
            {
                get
                {
                    string stored = EditorPrefs.GetString(PrefKey, string.Empty);
                    if (stored.Length > 0 && ColorUtility.TryParseHtmlString("#" + stored, out var color))
                        return color;
                    return DefaultValue;
                }
                set
                {
                    string html = ColorUtility.ToHtmlStringRGBA(value);
                    if (EditorPrefs.GetString(PrefKey, string.Empty) == html)
                        return;
                    EditorPrefs.SetString(PrefKey, html);
                    RaiseAppearanceChanged();
                }
            }

            public void Reset() => EditorPrefs.DeleteKey(PrefKey);

            private string PrefKey => Prefix + (IsDarkTheme ? "Dark." : "Light.") + Key;

            public static implicit operator Color(ColorSetting setting) => setting.Value;
        }
    }
}
