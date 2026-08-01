using UnityEditor;

namespace FantasyWord.GameCore
{
    public static class DatabaseRegistryExtensions
    {
        public static void SaveAsset(this DatabaseRegistry registry)
        {
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }
    }
}
