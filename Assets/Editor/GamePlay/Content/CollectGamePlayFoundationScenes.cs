using System;
using UnityEditor;
using YooAsset.Editor;

namespace GamePlay.Editor
{
    /// <summary>
    /// 只收集第二模块地基测试使用的入口场景和附加地图场景。
    /// </summary>
    [DisplayName("收集 GamePlay 地基测试场景")]
    public sealed class CollectGamePlayFoundationScenes : IAssetFilterRule
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
                       GamePlayFoundationTestSceneMenu.ScenePath,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       data.AssetPath,
                       GamePlayFoundationTestSceneMenu.MapScenePath,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       data.AssetPath,
                       GamePlayFoundationTestSceneMenu.SecondMapScenePath,
                       StringComparison.Ordinal);
        }
    }
}
