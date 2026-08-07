using UnityEditor;
using YooAsset.Editor;

namespace GamePlay.Editor
{
    /// <summary>
    /// YooAsset 构建期过滤规则：凡是继承 GamePlayContentAsset 的作者资产都会自动进入内容包。
    /// 作者不需要再维护额外内容清单或资源地址。
    /// </summary>
    [DisplayName("收集 GamePlay 内容定义")]
    public sealed class CollectGamePlayContentAssets : IAssetFilterRule
    {
        /// <summary>通知 YooAsset 以 GamePlay 内容技术基类作为收集类型。</summary>
        public string FindAssetType => nameof(GamePlayContentAsset);

        /// <summary>
        /// 只收集能由 AssetDatabase 读取为 GamePlay 内容资产的作者源，不依赖目录名或额外清单。
        /// </summary>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            return AssetDatabase.LoadAssetAtPath<GamePlayContentAsset>(data.AssetPath) != null;
        }
    }
}
