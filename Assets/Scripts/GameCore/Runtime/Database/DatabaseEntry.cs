using UnityEngine;

namespace FantasyWord.GameCore
{
#if UNITY_EDITOR
    /// <summary>
    /// 数据库条目的编辑器扩展，负责从 AssetDatabase 读取资产 GUID。
    /// </summary>
    public partial class DatabaseEntry
    {
        /// <summary>
        /// 返回当前数据库资产在 Unity AssetDatabase 中的 GUID。
        /// </summary>
        public string GetAssetGUID()
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            return UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
        }
    }
#endif

    /// <summary>
    /// 所有项目数据库资产的基类，用于统一被 DatabaseRegistry 注册和引用。
    /// </summary>
    public partial class DatabaseEntry : ScriptableObject { }
}

