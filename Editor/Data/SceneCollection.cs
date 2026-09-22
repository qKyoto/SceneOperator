using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneOperator
{
    /// <summary>
    /// An ordered set of scenes you work with, split into groups. Groups only separate scenes visually;
    /// the window shows one flat list with a divider between them.
    /// </summary>
    [CreateAssetMenu(fileName = "Scene Collection", menuName = "Scene Operator/Scene Collection")]
    public class SceneCollection : ScriptableObject
    {
        [SerializeField] private List<SceneGroup> _sceneGroups = new List<SceneGroup>();

        public IReadOnlyList<SceneGroup> SceneGroups => _sceneGroups;

        /// <summary>Asset GUID; used to remember the selected collection between sessions.</summary>
        public string AssetGuid
        {
            get
            {
                string path = AssetDatabase.GetAssetPath(this);
                return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            }
        }
    }
}
