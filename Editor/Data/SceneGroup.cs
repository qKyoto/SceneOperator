using System;
using UnityEditor;
using UnityEngine;

namespace SceneOperator
{
    /// <summary>
    /// A block of scenes inside a <see cref="SceneCollection"/>. The name is optional — it is only shown
    /// when "Show group names" is enabled in the preferences; otherwise groups appear as a divider line.
    /// </summary>
    [Serializable]
    public struct SceneGroup
    {
        [SerializeField] private string _groupName;
        [SerializeField] private SceneAsset[] _sceneAssets;

        public string GroupName => _groupName;

        public SceneAsset[] SceneAssets => _sceneAssets ?? Array.Empty<SceneAsset>();
    }
}
