using UnityEditor;
using YooAsset.Editor;

using Gameplay.Content;

namespace Gameplay.Editor.Content
{
    /// <summary>
    /// YooAsset 构建期过滤规则：凡是继承 ContentAsset 的作者资产都可由所属 Collector 收集。
    /// 该规则只判断资产类型，不生成内容 ID、不决定运行时加载集合。
    /// </summary>
    [DisplayName("收集 Gameplay 内容定义")]
    public sealed class ContentAssetFilterRule : IAssetFilterRule
    {
        /// <summary>通知 YooAsset 3.0.5 使用 Unity 的 ContentAsset 类型筛选缩小扫描范围。</summary>
        public string FindAssetType => nameof(ContentAsset);

        /// <summary>
        /// 只收集能由 AssetDatabase 读取为 Gameplay 内容资产的作者源，不读取运行时内容索引。
        /// </summary>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            return AssetDatabase.LoadAssetAtPath<ContentAsset>(data.AssetPath) != null;
        }
    }
}
