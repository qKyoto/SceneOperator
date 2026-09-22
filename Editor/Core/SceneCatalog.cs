using System;
using System.Collections.Generic;
using UnityEditor;

namespace SceneOperator.Editor
{
    /// <summary>One row: a scene of a collection, or a scene found in the project.</summary>
    internal readonly struct SceneItem
    {
        public SceneItem(string path, string name, int groupIndex, string groupName, bool missing)
        {
            Path = path;
            Name = name;
            GroupIndex = groupIndex;
            GroupName = groupName;
            Missing = missing;
        }

        public readonly string Path;
        public readonly string Name;

        /// <summary>Index of the group inside the collection; -1 for scenes that only exist in the project.</summary>
        public readonly int GroupIndex;

        public readonly string GroupName;

        /// <summary>The collection references a scene asset that no longer exists.</summary>
        public readonly bool Missing;

        public bool InCollection => GroupIndex >= 0;
    }

    /// <summary>Turns collections and the project into flat lists of rows.</summary>
    internal static class SceneCatalog
    {
        /// <summary>The collection as one flat list; <see cref="SceneItem.GroupIndex"/> marks where dividers go.</summary>
        public static List<SceneItem> FromCollection(SceneCollection collection)
        {
            var items = new List<SceneItem>();
            if (collection == null)
                return items;

            var groups = collection.SceneGroups;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                SceneGroup group = groups[groupIndex];
                foreach (SceneAsset sceneAsset in group.SceneAssets)
                {
                    if (sceneAsset == null)
                    {
                        items.Add(new SceneItem(string.Empty, "Missing scene", groupIndex, group.GroupName, true));
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(sceneAsset);
                    items.Add(new SceneItem(path, sceneAsset.name, groupIndex, group.GroupName, string.IsNullOrEmpty(path)));
                }
            }

            return items;
        }

        /// <summary>Every scene asset of the project, optionally without the ones already listed.</summary>
        public static List<SceneItem> ProjectScenes(ICollection<string> excludePaths = null)
        {
            var items = new List<SceneItem>();
            foreach (string guid in AssetDatabase.FindAssets("t:SceneAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || (excludePaths != null && excludePaths.Contains(path)))
                    continue;

                items.Add(new SceneItem(path, System.IO.Path.GetFileNameWithoutExtension(path), -1, null, false));
            }

            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return items;
        }

        /// <summary>The folder part of an asset path, without "Assets/" and without the file name.</summary>
        public static string FolderOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            if (folder.StartsWith("Assets/", StringComparison.Ordinal))
                folder = folder.Substring("Assets/".Length);
            else if (folder == "Assets")
                folder = string.Empty;
            return folder;
        }
    }
}
