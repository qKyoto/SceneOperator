using System;
using System.Threading.Tasks;
using UnityEditor;

namespace SceneOperator
{
    public class SceneCollectionProcessor : AssetModificationProcessor
    {
        private const string FILE_ENDING = ".asset";
        private const int AWAITING_DATA_BASE_TIME = 50;
        
        private static readonly Type TYPE = typeof(SceneCollection);

        public static event Action<SceneCollection> CollectionDestroyed; 
        public static event Action<SceneCollection> CollectionCreated; 

        public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions _)
        {
            if (CanCheckAsset(GetAssetType(path)))
            {
                SceneCollection sceneCollection = AssetDatabase.LoadAssetAtPath<SceneCollection>(path);
                CollectionDestroyed?.Invoke(sceneCollection);
            }
            
            return AssetDeleteResult.DidNotDelete;
        }
        
        private static async void OnWillCreateAsset(string path)
        {
            await WaitAssetDatabase();
            
            if (!path.EndsWith(FILE_ENDING) || !CanCheckAsset(GetAssetType(path)))
                return;
            
            SceneCollection sceneCollection = AssetDatabase.LoadAssetAtPath<SceneCollection>(path);
            CollectionCreated?.Invoke(sceneCollection);
        }

        private static Type GetAssetType(string path)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(path);
        }
        
        private static bool CanCheckAsset(Type assetType)
        {
            return assetType != null && (assetType == TYPE || assetType.IsSubclassOf(TYPE));
        }

        private static async Task WaitAssetDatabase()
        {
            await Task.Delay(AWAITING_DATA_BASE_TIME);
        }
    }
}