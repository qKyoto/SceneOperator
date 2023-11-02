using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneOperator
{
    [CreateAssetMenu(fileName = "Scene Collection", menuName = "Scene Operator/Scene Collection")]
    public class SceneCollection : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private List<SceneGroup> _sceneGroups = new();
        [SerializeField, HideInInspector] private string _id;

        private string _idDump;

        public string Id => _id;
        public IEnumerable<SceneGroup> SceneGroups => _sceneGroups;

        private void Awake()
        {
            TryCreateId();
        }

        private void TryCreateId()
        {
            if (!string.IsNullOrEmpty(_id))
                return;

            _id = Guid.NewGuid().ToString();
            EditorUtility.SetDirty(this);
        }
        
        public void OnBeforeSerialize()
        {
            _idDump = _id;
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(_idDump))
                return;
            
            _id = _idDump;
        }
    }
}