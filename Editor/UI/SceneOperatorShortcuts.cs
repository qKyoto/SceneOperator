using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SceneOperator.Editor
{
    /// <summary>
    /// Keyboard shortcuts. They are registered with Unity's shortcut manager, so they can be rebound both
    /// from the Scene Operator preferences and from Edit &gt; Shortcuts.
    /// </summary>
    internal static class SceneOperatorShortcuts
    {
        public const string SwitcherId = "Scene Operator/Open Scene Switcher";
        public const string WindowId = "Scene Operator/Open Scene Operator Window";

        [Shortcut(SwitcherId, KeyCode.O, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        private static void OpenSwitcher() => SceneSwitcherPopup.Open();

        [Shortcut(WindowId, KeyCode.None, ShortcutModifiers.None)]
        private static void OpenWindow() => SceneOperatorWindow.ShowWindow();

        [MenuItem("Tools/Scene Switcher", false, 1)]
        private static void OpenSwitcherFromMenu() => SceneSwitcherPopup.Open();

        /// <summary>The current binding as text ("Ctrl+Shift+O"), or an empty string when unbound.</summary>
        public static string BindingText(string shortcutId)
        {
            try
            {
                ShortcutBinding binding = ShortcutManager.instance.GetShortcutBinding(shortcutId);
                string text = binding.ToString();
                return string.IsNullOrEmpty(text) ? string.Empty : text;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Unity's built-in "Default" profile cannot be changed; a personal profile has to be active.</summary>
        public static bool CanRebind
        {
            get
            {
                try
                {
                    return !ShortcutManager.instance.IsProfileReadOnly(ShortcutManager.instance.activeProfileId);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string ActiveProfile
        {
            get
            {
                try
                {
                    return ShortcutManager.instance.activeProfileId;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Creates (or activates) a personal shortcut profile so shortcuts become editable.
        /// The profile inherits every other binding, and Edit &gt; Shortcuts can switch back to Default.
        /// </summary>
        public static bool CreateUserProfile(string profileId, out string error)
        {
            error = null;
            try
            {
                IShortcutManager manager = ShortcutManager.instance;
                bool exists = false;
                foreach (string id in manager.GetAvailableProfileIds())
                {
                    if (id == profileId)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    manager.CreateProfile(profileId);
                manager.activeProfileId = profileId;
                return true;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static bool TryRebind(string shortcutId, KeyCode keyCode, ShortcutModifiers modifiers, out string error)
        {
            error = null;
            try
            {
                ShortcutManager.instance.RebindShortcut(shortcutId, new ShortcutBinding(new KeyCombination(keyCode, modifiers)));
                return true;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static void ResetBinding(string shortcutId)
        {
            try
            {
                ShortcutManager.instance.ClearShortcutOverride(shortcutId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Scene Operator] Could not reset the shortcut: " + e.Message);
            }
        }
    }
}
