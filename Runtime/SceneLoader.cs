using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SceneOperator
{
    public class SceneLoader : IDisposable
    {
        private const int DELAY_AFTER_SCENE_CLOSED = 10;

        public bool IsOnlyOneSceneLoaded => EditorSceneManager.sceneCount == 1;
        
        public event Action ScenesHasChanged;

        public SceneLoader()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        public bool IsLoadedScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            Scene scene = EditorSceneManager.GetSceneByName(sceneName);
            return scene.name == sceneName;
        }

        public bool IsOnlyGroupScenesLoaded(SceneGroup sceneGroup)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (!GroupContainsScene(EditorSceneManager.GetSceneAt(i).name, sceneGroup))
                    return false;

            return true;
        }

        public void LoadSingleScene(string scenePath, OpenSceneMode openSceneMode = OpenSceneMode.Single)
        {
            EditorSceneManager.OpenScene(scenePath, openSceneMode);
        }

        public void LoadMultipleScenes(List<string> scenePaths, OpenSceneMode openSceneMode = OpenSceneMode.Single)
        {
            if (scenePaths.Count == 0)
                return;

            if (openSceneMode == OpenSceneMode.Single)
            {
                LoadSingleScene(scenePaths[0]);
                scenePaths.RemoveAt(0);
            }

            foreach (string scenePath in scenePaths)
                LoadSingleScene(scenePath, OpenSceneMode.Additive);
        }

        public void UnloadSingleScene(string sceneName)
        {
            Scene unloadScene = SceneManager.GetSceneByName(sceneName);
            EditorSceneManager.CloseScene(unloadScene, true);
        }

        public void UnloadMultipleScenes(List<string> sceneNames)
        {
            foreach (string sceneName in sceneNames)
                UnloadSingleScene(sceneName);
        }

        private bool GroupContainsScene(string name, SceneGroup sceneGroup)
        {
            return sceneGroup.SceneAssets.Where(sceneAsset => sceneAsset != null).Any(sceneAsset => sceneAsset.name == name);
        }

        private async void OnSceneClosed(Scene scene)
        {
            await Task.Delay(DELAY_AFTER_SCENE_CLOSED);
            ScenesHasChanged?.Invoke();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ScenesHasChanged?.Invoke();
        }

        public void Dispose()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
        }
    }
}
