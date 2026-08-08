using UnityEditor;
using YooAsset.Editor;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Editor.Content
{
    /// <summary>
    /// YooAsset 构建期过滤规则：凡是继承 ContentAsset 的作者资产都会自动进入内容包。
    /// 作者不需要再维护额外内容清单或资源地址。
    /// </summary>
    [DisplayName("收集 Gameplay 内容定义")]
    public sealed class ContentAssetFilterRule : IAssetFilterRule
    {
        /// <summary>通知 YooAsset 以 Gameplay 内容技术基类作为收集类型。</summary>
        public string FindAssetType => nameof(ContentAsset);

        /// <summary>
        /// 只收集能由 AssetDatabase 读取为 Gameplay 内容资产的作者源，不依赖目录名或额外清单。
        /// </summary>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            return AssetDatabase.LoadAssetAtPath<ContentAsset>(data.AssetPath) != null;
        }
    }
}
