using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 地表运行时状态的配置资产。
    /// 它定义状态持续、叠加策略和通行代价影响，不保存某个地图格子的当前状态。
    /// </summary>
    [CreateAssetMenu(
        fileName = "地表元素状态-",
        menuName = "GameCore/元素/地表元素状态定义")]
    public sealed class TerrainElementStateDefinition : DatabaseEntry
    {
        [Header("状态")]
        [InspectorName("状态类型")]
        [Tooltip("运行时状态的稳定枚举值，不能为 None。")]
        [SerializeField] private ETerrainElementStateKind m_stateKind = ETerrainElementStateKind.None;

        [InspectorName("默认持续时间")]
        [Tooltip("反应操作未覆盖持续时间时使用该秒数。")]
        [Min(0.01f)]
        [SerializeField] private float m_defaultDuration = 1.0f;

        [InspectorName("叠加策略")]
        [Tooltip("同一状态重复施加时如何刷新、保留或叠加强度。")]
        [SerializeField] private ETerrainStateMergePolicy m_mergePolicy =
            ETerrainStateMergePolicy.RefreshDuration;

        [Header("导航")]
        [InspectorName("通行代价倍率")]
        [Tooltip("状态存在期间乘到基础通行代价上。大于 1 会降低寻路偏好，小于 1 会更容易经过。")]
        [Min(0.01f)]
        [SerializeField] private float m_traversalCostMultiplier = 1.0f;

        public ETerrainElementStateKind StateKind => m_stateKind;
        public float DefaultDuration => m_defaultDuration;
        public ETerrainStateMergePolicy MergePolicy => m_mergePolicy;
        public float TraversalCostMultiplier => m_traversalCostMultiplier;

        public bool TryValidate(out string error)
        {
            if (m_stateKind == ETerrainElementStateKind.None)
            {
                error = "状态类型不能为 None。";
                return false;
            }

            if (m_defaultDuration <= 0.0f)
            {
                error = $"状态 {m_stateKind} 的默认持续时间必须大于 0。";
                return false;
            }

            if (m_traversalCostMultiplier <= 0.0f)
            {
                error = $"状态 {m_stateKind} 的通行代价倍率必须大于 0。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
