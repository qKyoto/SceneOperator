using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneOperator.Editor
{
    /// <summary>Locates the package's own assets (style sheet, sprites) wherever the package is installed.</summary>
    internal static class PackageResources
    {
        private const string FallbackRoot = "Packages/com.kyoto.scene-operator";

        private static string s_Root;
        private static StyleSheet s_StyleSheet;

        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(s_Root))
                    return s_Root;

                UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PackageResources).Assembly);
                s_Root = package != null ? package.assetPath : FallbackRoot;
                return s_Root;
            }
        }

        public static StyleSheet StyleSheet
        {
            get
            {
                if (s_StyleSheet != null)
                    return s_StyleSheet;

                s_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/Styles/SceneOperatorStyles.uss");
                if (s_StyleSheet == null)
                {
                    foreach (string guid in AssetDatabase.FindAssets("SceneOperatorStyles t:StyleSheet"))
                    {
                        s_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                        if (s_StyleSheet != null)
                            break;
                    }
                }
                return s_StyleSheet;
            }
        }

        /// <summary>A sprite from the package's Art folder, as a texture.</summary>
        public static Texture2D Sprite(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Sprites/" + fileName);
        }

        /// <summary>
        /// A built-in editor icon. Unity picks the light or dark variant by itself for most names;
        /// for the ones that only ship a dark version the "d_" prefix is tried as a fallback.
        /// Returns null instead of throwing when the name is unknown.
        /// </summary>
        public static Texture2D BuiltIn(string iconName)
        {
            Texture2D icon = Load(iconName);
            if (icon == null && !iconName.StartsWith("d_"))
                icon = Load("d_" + iconName);
            return icon;
        }

        private static Texture2D Load(string iconName)
        {
            try
            {
                GUIContent content = EditorGUIUtility.IconContent(iconName);
                return content?.image as Texture2D;
            }
            catch
            {
                return null;
            }
        }
    }
}
