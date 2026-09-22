using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneOperator.Editor
{
    /// <summary>
    /// Keeps track of the scene collections in the project and of the one the user selected.
    /// The selection is stored per project by asset GUID, so it survives restarts, renames and moves.
    /// </summary>
    internal static class CollectionRegistry
    {
        private const string SelectedKey = "SceneOperator.SelectedCollection";

        private static List<SceneCollection> s_Collections;
        private static SceneCollection s_Selected;
        private static bool s_SelectionRestored;

        /// <summary>Raised when collections are added, removed or renamed, or when a scene asset changes.</summary>
        public static event Action Changed;

        public static IReadOnlyList<SceneCollection> Collections
        {
            get
            {
                EnsureLoaded();
                return s_Collections;
            }
        }

        public static SceneCollection Selected
        {
            get
            {
                EnsureLoaded();

                if (!s_SelectionRestored)
                {
                    s_SelectionRestored = true;
                    s_Selected = FindByGuid(EditorUserSettings.GetConfigValue(SelectedKey));
                }

                // The selection is only dropped when the collection is really gone from the project.
                if (s_Selected == null && s_Collections.Count > 0)
                    Select(s_Collections[0]);

                return s_Selected;
            }
        }

        public static void Select(SceneCollection collection)
        {
            s_Selected = collection;
            s_SelectionRestored = true;
            EditorUserSettings.SetConfigValue(SelectedKey, collection != null ? collection.AssetGuid : string.Empty);
            Changed?.Invoke();
        }

        /// <summary>Drops the cache; the next access re-scans the project.</summary>
        public static void Refresh()
        {
            s_Collections = null;
            EnsureLoaded();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (s_Collections != null)
                return;

            s_Collections = new List<SceneCollection>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SceneCollection)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var collection = AssetDatabase.LoadAssetAtPath<SceneCollection>(path);
                if (collection != null)
                    s_Collections.Add(collection);
            }

            s_Collections.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            // The previously selected asset may have been re-imported into a new instance.
            if (s_Selected == null && s_SelectionRestored)
                s_Selected = FindByGuid(EditorUserSettings.GetConfigValue(SelectedKey));
        }

        private static SceneCollection FindByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;

            return AssetDatabase.LoadAssetAtPath<SceneCollection>(path);
        }

        /// <summary>Creates a collection asset next to the current project selection and selects it.</summary>
        public static SceneCollection CreateCollection()
        {
            string folder = "Assets";
            if (Selection.activeObject != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selectedPath))
                    folder = AssetDatabase.IsValidFolder(selectedPath) ? selectedPath : System.IO.Path.GetDirectoryName(selectedPath);
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath((folder ?? "Assets") + "/Scene Collection.asset");
            var collection = ScriptableObject.CreateInstance<SceneCollection>();
            AssetDatabase.CreateAsset(collection, assetPath);
            AssetDatabase.SaveAssets();

            Refresh();
            Select(collection);
            SceneOps.Ping(assetPath);
            return collection;
        }

        /// <summary>Invalidates the cache whenever collections or scenes are added, removed, moved or renamed.</summary>
        private sealed class Watcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom))
                    return;

                s_Collections = null;
                // Defer: during the import callback the AssetDatabase is not ready for arbitrary loads.
                EditorApplication.delayCall += () =>
                {
                    EnsureLoaded();
                    Changed?.Invoke();
                };
            }

            private static bool Touches(string[] paths)
            {
                foreach (string path in paths)
                {
                    if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }
    }
}
