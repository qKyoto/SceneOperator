using System;
using System.Collections.Generic;
using System.Linq;
using SceneOperator.Editor.Constants;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using EditorStyles = SceneOperator.Editor.Constants.EditorStyles;

namespace SceneOperator.Editor
{
    public class SceneOperatorEditor : EditorWindow
    {
        [SerializeField] private StyleSheet _styleSheet;
        
        private List<SceneCollection> _sceneCollections;
        private SceneCollection _selectedCollection;
        private SerializedObject _serializedCollection;
        private ScrollView _activeCollectionView;
        private SceneLoader _sceneLoader;
        private List<Button> _toggleContentButtons;
        private List<Label> _sceneNameLabels;
        
        private event Action ActiveCollectionViewChanged;
        
        [MenuItem(EditorMainConstants.EDITOR_PATH)]
        public static void ShowEditor()
        {
            EditorWindow editorWindow = GetWindow<SceneOperatorEditor>();
            editorWindow.titleContent = new GUIContent(EditorMainConstants.MAIN_EDITOR_TITLE);
        }

        private void OnEnable()
        {
            _sceneCollections = new List<SceneCollection>();
            _sceneLoader = new SceneLoader();

            LoadSceneCollections();
            TrySelectSavedCollection();

            ActiveCollectionViewChanged += OnActiveCollectionViewChanged;
            SceneCollectionProcessor.CollectionCreated += OnCollectionCreated;
            SceneCollectionProcessor.CollectionDestroyed += OnCollectionDestroyed;
        }

        private void CreateGUI()
        {
            rootVisualElement.styleSheets.Add(_styleSheet);
            DrawEditor();
        }

        private void LoadSceneCollections()
        {
            string[] guids = AssetDatabase.FindAssets(EditorMainConstants.SCENE_COLLECTION_FILTER, new[] { EditorMainConstants.SCENE_COLLECTION_PATH });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SceneCollection sceneCollection = AssetDatabase.LoadAssetAtPath<SceneCollection>(path);
                _sceneCollections.Add(sceneCollection);
            }
        }

        private void TrySelectSavedCollection()
        {
            string collectionId = EditorPrefs.GetString(EditorMainConstants.SAVED_COLLECTION_KEY);

            if (!IsValidCollection(collectionId))
            {
                EditorPrefs.DeleteKey(EditorMainConstants.SAVED_COLLECTION_KEY);
                return;
            }
            
            foreach (SceneCollection sceneCollection in _sceneCollections.Where(sceneCollection => sceneCollection.Id == collectionId))
            {
                _selectedCollection = sceneCollection;
                return;
            }
        }

        private bool IsValidCollection(string collectionId)
        {
            return _sceneCollections.Any(sceneCollection => sceneCollection.Id == collectionId);
        }

        private void OnActiveCollectionViewChanged()
        {
            _serializedCollection?.Dispose();
            _serializedCollection = new SerializedObject(_selectedCollection);
            _activeCollectionView.TrackSerializedObjectValue(_serializedCollection, _ =>
            {
                DrawEditor();
            });
        }

        private void OnCollectionCreated(SceneCollection sceneCollection)
        {
            _sceneCollections.Add(sceneCollection);
            DrawEditor();
        }

        private void OnCollectionDestroyed(SceneCollection sceneCollection)
        {
            _sceneCollections.Remove(sceneCollection);
            EditorPrefs.DeleteKey(sceneCollection.Id);

            if (_selectedCollection == sceneCollection)
                _selectedCollection = null;

            DrawEditor();
        }

        private void DrawEditor()
        {
            _toggleContentButtons = new List<Button>();
            _sceneNameLabels = new List<Label>();
            
            rootVisualElement.Clear();
            
            DrawDropdownGroup();
            DropHorizontalLine();
            DrawSelectedCollection();
            DrawSignature();
            UpdatedToggleContentButtonsWidth();
            UpdatedSceneNameLabelsWidth();
        }

        private void DropHorizontalLine()
        {
            Box horizontalLine = new();
            horizontalLine.AddToClassList(EditorStyles.HORIZONTAL_LINE);
            rootVisualElement.Add(horizontalLine);
        }
        
        private void DrawDropdownGroup()
        {
            VisualElement dropdownGroup = new();
            DropdownField dropdownField = new();
            Button selectCollectionButton = new() {tooltip = Tooltips.SELECT_SCENE_COLLECTION };
            
            dropdownGroup.AddToClassList(EditorStyles.CONTAINER_DROPDOWN);
            dropdownField.AddToClassList(EditorStyles.CONTAINER_DROPDOWN);
            selectCollectionButton.AddToClassList(EditorStyles.PING_BUTTON);

            dropdownField.RegisterValueChangedCallback(OnSelectedCollectionChanged);
            selectCollectionButton.clicked += () =>
            {
                if (_selectedCollection == null)
                    return;
                
                Selection.activeObject = _selectedCollection;
                EditorGUIUtility.PingObject(_selectedCollection);
            };

            foreach (SceneCollection sceneCollection in _sceneCollections)
                dropdownField.choices.Add(sceneCollection.name);
            
            if (_selectedCollection != null)
                dropdownField.SetValueWithoutNotify(_selectedCollection.name);
            
            dropdownGroup.Add(dropdownField);
            dropdownGroup.Add(selectCollectionButton);
            rootVisualElement.Add(dropdownGroup);
        }
        
        private void OnSelectedCollectionChanged(ChangeEvent<string> changeEvent) 
        {
            _activeCollectionView?.Clear();
            
            if (rootVisualElement.Contains(_activeCollectionView))
                rootVisualElement.Remove(_activeCollectionView);
            
            ChangeSelectedCollection(changeEvent.newValue);
            DrawSelectedCollection();
        }

        private void ChangeSelectedCollection(string newValue)
        {
            foreach (SceneCollection sceneContainer in _sceneCollections.Where(sceneContainer => sceneContainer.name == newValue))
            {
                _selectedCollection = sceneContainer;
                EditorPrefs.SetString(EditorMainConstants.SAVED_COLLECTION_KEY, _selectedCollection.Id);
                break;
            }
        }

        private void DrawSelectedCollection()
        {
            if (_selectedCollection == null)
                return;
            
            _activeCollectionView = new ScrollView();
            _activeCollectionView.AddToClassList(EditorStyles.ACTIVE_COLLECTION_VIEW);
        
            foreach (SceneGroup sceneGroup in _selectedCollection.SceneGroups)
            {
                (VisualElement groupHeader, VisualElement groupContent) collectionGroup = CreateCollectionGroup(sceneGroup);

                foreach (SceneAsset sceneAsset in sceneGroup.SceneAssets)
                {
                    if (sceneAsset == null)
                        continue;
                    
                    VisualElement sceneContent = CreateSceneGroupContent(sceneAsset);
                    collectionGroup.groupContent.Add(sceneContent);
                }
                
                _activeCollectionView.Add(collectionGroup.groupHeader);
                _activeCollectionView.Add(collectionGroup.groupContent);
            }
            
            ActiveCollectionViewChanged?.Invoke();
            rootVisualElement.Add(_activeCollectionView);
        }

        private (VisualElement groupHeader, VisualElement groupContent) CreateCollectionGroup(SceneGroup sceneGroup)
        {
            VisualElement groupHeader = new();
            VisualElement groupContent = new();

            Button toggleContentButton = new() { text = sceneGroup.GroupName };
            Button loadGroupSingleButton = new() { text = "Group (single)" };
            Button loadGroupAdditiveButton = new() { text = "Group (additive)" };
            Button unloadGroupButton = new( ) { tooltip = Tooltips.UNLOAD_SCENE_GROUP };

            groupHeader.AddToClassList(EditorStyles.GROUP_HEADER);
            groupContent.AddToClassList(EditorStyles.ZERO_HEIGHT);
            toggleContentButton.AddToClassList(EditorStyles.TOGGLE_CONTENT_BUTTON);
            toggleContentButton.AddToClassList(EditorStyles.STRETCH);
            loadGroupSingleButton.AddToClassList(EditorStyles.BUTTON_SIZE_100);
            loadGroupAdditiveButton.AddToClassList(EditorStyles.BUTTON_SIZE_100);
            unloadGroupButton.AddToClassList(EditorStyles.UNLOAD_BUTTON);
            unloadGroupButton.AddToClassList(EditorStyles.BUTTON_SIZE_46);

            toggleContentButton.clicked += () => { ChangeSceneGroupVisibility(groupContent); };
            loadGroupSingleButton.clicked += () => { RequestToLoadMultipleScenes(sceneGroup, OpenSceneMode.Single); };
            loadGroupAdditiveButton.clicked += () => { RequestToLoadMultipleScenes(sceneGroup, OpenSceneMode.Additive); };
            unloadGroupButton.clicked += () => { RequestToUploadMultipleScenes(sceneGroup); };
            _sceneLoader.ScenesHasChanged += () => { UpdateStateOfTheGroupButtons(sceneGroup, loadGroupAdditiveButton, loadGroupSingleButton, unloadGroupButton); };
            
            UpdateStateOfTheGroupButtons(sceneGroup, loadGroupAdditiveButton, loadGroupSingleButton, unloadGroupButton);

            groupHeader.Add(toggleContentButton);
            groupHeader.Add(loadGroupSingleButton);
            groupHeader.Add(loadGroupAdditiveButton);
            groupHeader.Add(unloadGroupButton);
            
            _toggleContentButtons.Add(toggleContentButton);

            (VisualElement groupHeader, VisualElement groupContent) result = (groupHeader, groupContent);
            return result;
        }

        private VisualElement CreateSceneGroupContent(SceneAsset sceneAsset)
        {
            VisualElement sceneContent = new();
            Label sceneName = new() { text = sceneAsset.name };
            Button loadSingleButton = new() { text = "Scene (single)" };
            Button loadAdditiveButton = new() { text = "Scene (additive)" };
            Button selectSceneButton = new();
            Button unloadButton = new() { tooltip = Tooltips.UNLOAD_SCENE };
            
            sceneContent.AddToClassList(EditorStyles.SCENE_CONTENT);
            sceneName.AddToClassList(EditorStyles.SCENE_NAME_LABEL);
            sceneName.AddToClassList(EditorStyles.STRETCH);
            loadSingleButton.AddToClassList(EditorStyles.BUTTON_SIZE_100);
            loadAdditiveButton.AddToClassList(EditorStyles.BUTTON_SIZE_100);
            selectSceneButton.AddToClassList(EditorStyles.PING_BUTTON);
            unloadButton.AddToClassList(EditorStyles.UNLOAD_BUTTON);
            unloadButton.AddToClassList(EditorStyles.BUTTON_SIZE_20);
            
            _sceneLoader.ScenesHasChanged += () => { UpdateStateSceneButtons(sceneAsset, loadAdditiveButton, loadSingleButton, unloadButton); };
            loadSingleButton.clicked += () => { RequestToLoadSingleScene(sceneAsset, OpenSceneMode.Single); };
            loadAdditiveButton.clicked += () => { RequestToLoadSingleScene(sceneAsset, OpenSceneMode.Additive); };
            unloadButton.clicked += () => { RequestToUnloadSingleScene(sceneAsset); };
            selectSceneButton.clicked += () =>
            {
                Selection.activeObject = sceneAsset;
                EditorGUIUtility.PingObject(sceneAsset);
            };
            
            UpdateStateSceneButtons(sceneAsset, loadAdditiveButton, loadSingleButton, unloadButton);
            
            sceneContent.Add(sceneName);
            sceneContent.Add(loadSingleButton);
            sceneContent.Add(loadAdditiveButton);
            sceneContent.Add(selectSceneButton);
            sceneContent.Add(unloadButton);
            
            _sceneNameLabels.Add(sceneName);

            return sceneContent;
        }

        private void ChangeSceneGroupVisibility(VisualElement groupContent)
        {
            if (groupContent.visible)
            {
                groupContent.RemoveFromClassList(EditorStyles.SCENE_COLLECTION_CONTENT);
                groupContent.AddToClassList(EditorStyles.ZERO_HEIGHT);
            }
            else
            {
                groupContent.AddToClassList(EditorStyles.SCENE_COLLECTION_CONTENT);
                groupContent.RemoveFromClassList(EditorStyles.ZERO_HEIGHT);
            }
        }
        
        private void UpdateStateOfTheGroupButtons(SceneGroup sceneGroup, Button loadGroupAdditiveButton, Button loadGroupSingleButton, Button unloadButton)
        {
            bool isAllGroupSceneLoaded = IsAllGroupSceneLoaded(sceneGroup);
            bool isAllGroupSceneUnloaded = IsAllGroupSceneUnloaded(sceneGroup);
                
            loadGroupAdditiveButton.SetEnabled(!isAllGroupSceneLoaded);
            loadGroupSingleButton.SetEnabled(!isAllGroupSceneLoaded || CanLoadSceneGroup(sceneGroup));
            unloadButton.SetEnabled(!isAllGroupSceneUnloaded && !_sceneLoader.IsOnlyOneSceneLoaded);

            if (!isAllGroupSceneUnloaded && !isAllGroupSceneLoaded)
            {
                loadGroupAdditiveButton.AddToClassList(EditorStyles.HIGHLIGHT_BUTTON);
                loadGroupSingleButton.AddToClassList(EditorStyles.HIGHLIGHT_BUTTON);
            }
            else
            {
                loadGroupAdditiveButton.RemoveFromClassList(EditorStyles.HIGHLIGHT_BUTTON);
                loadGroupSingleButton.RemoveFromClassList(EditorStyles.HIGHLIGHT_BUTTON);
            }
        }
        
        private void UpdateStateSceneButtons(SceneAsset sceneAsset, Button loadAdditiveButton, Button loadSingleButton, Button unloadButton)
        {
            bool isLoadedScene = _sceneLoader.IsLoadedScene(sceneAsset.name);
            loadAdditiveButton.SetEnabled(!isLoadedScene);
            loadSingleButton.SetEnabled(!isLoadedScene || !_sceneLoader.IsOnlyOneSceneLoaded);
            unloadButton.SetEnabled(isLoadedScene && !_sceneLoader.IsOnlyOneSceneLoaded);
        }
        
        private void UpdatedToggleContentButtonsWidth()
        {
            float width = GetWidthByToggleGroupButton();
            foreach (Button toggleContentButton in _toggleContentButtons)
                toggleContentButton.style.width = width;
        }

        private void UpdatedSceneNameLabelsWidth()
        {
            float width = GetWidthByToggleGroupButton();
            foreach (Label sceneNameLabel in _sceneNameLabels)
                sceneNameLabel.style.width = width;
        }

        private float GetWidthByToggleGroupButton()
        {
            float width = _toggleContentButtons.Select(toggleContentButton => toggleContentButton.style.width.value.value).Prepend(0).Max();
            return _sceneNameLabels.Select(sceneNameLabel => sceneNameLabel.style.width.value.value).Prepend(width).Max();
        }

        private bool CanLoadSceneGroup(SceneGroup sceneGroup)
        {
            return !_sceneLoader.IsOnlyGroupScenesLoaded(sceneGroup);
        }
        
        private bool IsAllGroupSceneLoaded(SceneGroup sceneGroup)
        {
            return sceneGroup.SceneAssets.All(sceneAsset => _sceneLoader.IsLoadedScene(sceneAsset != null ? sceneAsset.name : null));
        }

        private bool IsAllGroupSceneUnloaded(SceneGroup sceneGroup)
        {
            return sceneGroup.SceneAssets.All(sceneAsset => !_sceneLoader.IsLoadedScene(sceneAsset != null ? sceneAsset.name : null));
        }

        private void RequestToLoadSingleScene(SceneAsset sceneAsset, OpenSceneMode openSceneMode)
        {
            _sceneLoader.LoadSingleScene(AssetDatabase.GetAssetPath(sceneAsset), openSceneMode);
        }

        private void RequestToLoadMultipleScenes(SceneGroup sceneGroup, OpenSceneMode openSceneMode)
        {
            List<string> scenePaths = (from sceneAsset in sceneGroup.SceneAssets where sceneAsset != null select AssetDatabase.GetAssetPath(sceneAsset)).ToList();
            _sceneLoader.LoadMultipleScenes(scenePaths, openSceneMode);
        }

        private void RequestToUnloadSingleScene(SceneAsset sceneAsset)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                _sceneLoader.UnloadSingleScene(sceneAsset.name);
        }
        
        private void RequestToUploadMultipleScenes(SceneGroup sceneGroup)
        {
            List<string> sceneNames = (from sceneAsset in sceneGroup.SceneAssets where sceneAsset != null select sceneAsset.name).ToList();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                _sceneLoader.UnloadMultipleScenes(sceneNames);
        }
        
        private void DrawSignature()
        {
            VisualElement signature = new();
            Label prefixLabel = new() { text = EditorMainConstants.SIGNATURE_PREFIX_LABEL };
            Image icon = new();
            Label postfixLabel = new() { text = EditorMainConstants.SIGNATURE_POSTFIX_LABEL };
            
            signature.AddToClassList(EditorStyles.SIGNATURE);
            prefixLabel.AddToClassList(EditorStyles.SIGNATURE_LABEL);
            postfixLabel.AddToClassList(EditorStyles.SIGNATURE_LABEL);
            icon.AddToClassList(EditorStyles.SIGNATURE_ICON);
            
            signature.Add(prefixLabel);
            signature.Add(icon);
            signature.Add(postfixLabel);
            rootVisualElement.Add(signature);
        }
        
        private void OnDestroy()
        {
            _sceneLoader.Dispose();
            
            ActiveCollectionViewChanged -= OnActiveCollectionViewChanged;
            SceneCollectionProcessor.CollectionCreated -= OnCollectionCreated;
            SceneCollectionProcessor.CollectionDestroyed -= OnCollectionDestroyed;
        }
    }
}