using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneOperator.Editor
{
    /// <summary>
    /// Every scene operation of the tool, plus the state queries the UI draws from.
    /// Scenes are always identified by their asset path — two scenes may share a name.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneOps
    {
        /// <summary>Raised when the set of open scenes, the active scene or a save state changed.</summary>
        public static event Action Changed;

        static SceneOps()
        {
            EditorSceneManager.sceneOpened += (_, __) => Raise();
            EditorSceneManager.sceneClosed += _ => Raise();
            EditorSceneManager.sceneSaved += _ => Raise();
            EditorSceneManager.newSceneCreated += (_, __, ___) => Raise();
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Raise();
            EditorApplication.playModeStateChanged += _ => Raise();
        }

        private static void Raise() => Changed?.Invoke();

        // ---------------------------------------------------------------- state

        public static bool CanModifyScenes => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>The scene is present in the hierarchy (it may still be unloaded).</summary>
        public static bool IsOpen(string path) => GetScene(path).IsValid();

        public static bool IsLoaded(string path)
        {
            Scene scene = GetScene(path);
            return scene.IsValid() && scene.isLoaded;
        }

        public static bool IsActive(string path)
        {
            Scene active = SceneManager.GetActiveScene();
            return active.IsValid() && PathsEqual(active.path, path);
        }

        public static bool IsDirty(string path)
        {
            Scene scene = GetScene(path);
            return scene.IsValid() && scene.isDirty;
        }

        public static int OpenSceneCount => EditorSceneManager.sceneCount;

        /// <summary>Cheap fingerprint of everything the rows display; used to poll for changes.</summary>
        public static int StateHash()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    Scene scene = EditorSceneManager.GetSceneAt(i);
                    hash = hash * 31 + (scene.path?.GetHashCode() ?? 0);
                    hash = hash * 31 + (scene.isLoaded ? 1 : 0);
                    hash = hash * 31 + (scene.isDirty ? 1 : 0);
                }
                hash = hash * 31 + (SceneManager.GetActiveScene().path?.GetHashCode() ?? 0);
                hash = hash * 31 + (EditorApplication.isPlayingOrWillChangePlaymode ? 1 : 0);
                return hash;
            }
        }

        private static Scene GetScene(string path)
        {
            return string.IsNullOrEmpty(path) ? default : EditorSceneManager.GetSceneByPath(path);
        }

        private static bool PathsEqual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        public static bool SceneExists(string path) => !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

        // ---------------------------------------------------------------- operations

        /// <summary>Opens the scene, replacing everything that is currently open.</summary>
        public static void OpenSingle(string path) => Open(path, OpenSceneMode.Single);

        /// <summary>Opens the scene next to the ones already open.</summary>
        public static void OpenAdditive(string path) => Open(path, OpenSceneMode.Additive);

        public static void Open(string path, OpenSceneMode mode)
        {
            if (!Guard(path))
                return;

            // Replacing the open scenes discards them, so ask about unsaved work first (Cancel aborts).
            if (mode == OpenSceneMode.Single && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EditorSceneManager.OpenScene(path, mode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Scene Operator] Could not open \"{path}\": {e.Message}");
            }
        }

        /// <summary>Opens a whole group; the first scene replaces the current setup unless <paramref name="additive"/>.</summary>
        public static void OpenGroup(IReadOnlyList<string> paths, bool additive)
        {
            if (paths == null || paths.Count == 0 || !CanModifyScenes)
                return;
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            bool first = true;
            foreach (string path in paths)
            {
                if (!SceneExists(path))
                    continue;
                try
                {
                    EditorSceneManager.OpenScene(path, !additive && first ? OpenSceneMode.Single : OpenSceneMode.Additive);
                    first = false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Scene Operator] Could not open \"{path}\": {e.Message}");
                }
            }
        }

        /// <summary>Opens the scene alone and enters play mode.</summary>
        public static void OpenAndPlay(string path)
        {
            if (!Guard(path))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!IsLoaded(path) || OpenSceneCount > 1)
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            EditorApplication.EnterPlaymode();
        }

        /// <summary>Closes the scene and removes it from the hierarchy. Unsaved changes are offered for saving.</summary>
        public static void Close(string path)
        {
            if (!CanModifyScenes)
                return;

            Scene scene = GetScene(path);
            if (!scene.IsValid())
                return;

            // Closing the last open scene would leave the editor without one.
            if (OpenSceneCount <= 1)
                return;

            if (scene.isDirty && !EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene }))
                return;

            EditorSceneManager.CloseScene(scene, true);
        }

        public static void SetActive(string path)
        {
            Scene scene = GetScene(path);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.SetActiveScene(scene);
        }

        public static void Save(string path)
        {
            Scene scene = GetScene(path);
            if (scene.IsValid() && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }

        public static void Ping(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static bool Guard(string path)
        {
            if (!CanModifyScenes)
                return false;

            if (!SceneExists(path))
            {
                Debug.LogWarning($"[Scene Operator] Scene asset is missing: \"{path}\".");
                return false;
            }
            return true;
        }
    }
}
