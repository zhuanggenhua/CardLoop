using System;
using UnityEditor;
using YooAsset.Editor;

namespace Gameplay.Tests.Support.Editor
{
    /// <summary>
    /// 只收集地基测试使用的标题、牌桌入口、StackCraft 同态入口和附加地图场景。
    /// </summary>
    [DisplayName("收集 Gameplay 地基测试场景")]
    public sealed class FoundationSceneFilterRule : IAssetFilterRule
    {
        /// <summary>通知 YooAsset 该过滤规则只处理 Unity 场景资产。</summary>
        public string FindAssetType => nameof(SceneAsset);

        /// <summary>
        /// 只允许统一地基标题入口、牌桌入口、StackCraft 同态入口和两张附加地图测试场景进入测试资源包，避免误收正式或参考场景。
        /// </summary>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            return string.Equals(
                       data.AssetPath,
                       FoundationTestSceneMenu.TitleScenePath,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       data.AssetPath,
                       FoundationTestSceneMenu.ScenePath,
                       StringComparison.Ordinal) ||
				   string.Equals(
					   data.AssetPath,
					   FoundationTestSceneMenu.StackCraftParityScenePath,
					   StringComparison.Ordinal) ||
                   string.Equals(
                       data.AssetPath,
                       FoundationTestSceneMenu.MapScenePath,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       data.AssetPath,
                       FoundationTestSceneMenu.SecondMapScenePath,
                       StringComparison.Ordinal);
        }
    }
}
