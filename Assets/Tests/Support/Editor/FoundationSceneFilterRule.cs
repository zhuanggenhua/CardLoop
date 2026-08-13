using System;
using UnityEditor;
using YooAsset.Editor;

namespace Gameplay.Tests.Support.Editor
{
    /// <summary>
    /// 只收集第二模块地基测试使用的入口场景和附加地图场景。
    /// </summary>
    [DisplayName("收集 Gameplay 地基测试场景")]
    public sealed class FoundationSceneFilterRule : IAssetFilterRule
    {
        /// <summary>通知 YooAsset 该过滤规则只处理 Unity 场景资产。</summary>
        public string FindAssetType => nameof(SceneAsset);

        /// <summary>
        /// 只允许统一地基入口和两张附加地图测试场景进入测试资源包，避免误收正式或参考场景。
        /// </summary>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            return string.Equals(
                       data.AssetPath,
                       FoundationTestSceneMenu.ScenePath,
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
