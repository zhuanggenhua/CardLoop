using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 运行时地表状态到临时表现 Tile 的映射项。
    /// 多个状态同时存在时由优先级决定最终显示哪张临时效果 Tile。
    /// </summary>
    [Serializable]
    public sealed class TerrainStateTileMapping
    {
        [InspectorName("运行时状态")]
        [Tooltip("例如 Burning/Wet/Oiled。这里只处理临时表现，不改写基础地形。")]
        [SerializeField] private ETerrainElementStateKind m_stateKind =
            ETerrainElementStateKind.None;

        [InspectorName("临时效果 Tile")]
        [Tooltip("写入临时效果 Tilemap 的 Tile。为空表示该状态没有专属表现。")]
        [SerializeField] private TileBase m_tile = null;

        [InspectorName("显示优先级")]
        [Tooltip("同一格多个状态都命中时，优先级更高的 Tile 覆盖显示。")]
        [SerializeField] private int m_priority = 0;

        public ETerrainElementStateKind StateKind => m_stateKind;
        public TileBase Tile => m_tile;
        public int Priority => m_priority;
    }

    /// <summary>
    /// 基础地表到结果 Tile 的旧兼容映射。
    /// 当前燃尽露土不走结果覆盖 Tile，而是隐藏草覆盖后露出作者底层 Tile。
    /// </summary>
    [Serializable]
    public sealed class TerrainSurfaceTileMapping
    {
        [InspectorName("地表类型")]
        [SerializeField] private ETerrainSurfaceKind m_surfaceKind =
            ETerrainSurfaceKind.None;

        [InspectorName("结果 Tile")]
        [Tooltip("仅用于仍需要结果覆盖表现的旧路径；正式露土验收不依赖该映射。")]
        [SerializeField] private TileBase m_tile = null;

        public ETerrainSurfaceKind SurfaceKind => m_surfaceKind;
        public TileBase Tile => m_tile;
    }

    /// <summary>
    /// 瞬时表现信号到 Tile 的映射项，例如蒸汽。
    /// 它有固定显示时长，结束后会恢复该格当前运行时状态表现。
    /// </summary>
    [Serializable]
    public sealed class TerrainSignalTileMapping
    {
        [InspectorName("表现信号")]
        [SerializeField] private EElementPresentationSignal m_signal =
            EElementPresentationSignal.None;

        [InspectorName("信号 Tile")]
        [Tooltip("信号触发期间写入临时效果 Tilemap 的 Tile。")]
        [SerializeField] private TileBase m_tile = null;

        [InspectorName("持续时间")]
        [Tooltip("信号 Tile 的显示秒数；结束后会重新采样运行时地表状态。")]
        [Min(0.01f)]
        [SerializeField] private float m_duration = 0.35f;

        public EElementPresentationSignal Signal => m_signal;
        public TileBase Tile => m_tile;
        public float Duration => Mathf.Max(0.01f, m_duration);
    }

    /// <summary>
    /// 地表元素表现配置资产。
    /// 它是运行时状态到临时视觉 Tile 的作者入口，不是地形规则、寻路或持久地貌变化的真相源。
    /// </summary>
    [CreateAssetMenu(
        fileName = "地表元素表现-",
        menuName = "GameCore/元素/地表元素表现配置")]
    public sealed class TerrainSurfacePresentationConfig : DatabaseEntry
    {
        [Header("临时状态")]
        [InspectorName("状态 Tile 映射")]
        [Tooltip("运行时状态到临时效果 Tile 的映射。多个状态命中时按映射优先级选择。")]
        [SerializeField] private TerrainStateTileMapping[] m_stateTiles =
            Array.Empty<TerrainStateTileMapping>();

        [Header("兼容结果覆盖")]
        [InspectorName("地表结果 Tile 映射")]
        [Tooltip("旧结果覆盖路径使用。正式草层燃尽露土应由覆盖层隐藏露出底层 Tile。")]
        [SerializeField] private TerrainSurfaceTileMapping[] m_surfaceTiles =
            Array.Empty<TerrainSurfaceTileMapping>();

        [Header("瞬时信号")]
        [InspectorName("信号 Tile 映射")]
        [Tooltip("Steam 等一次性表现信号到临时 Tile 和显示时长的映射。")]
        [SerializeField] private TerrainSignalTileMapping[] m_signalTiles =
            Array.Empty<TerrainSignalTileMapping>();

        public bool TryGetTemporaryStateTile(
            in TerrainCellRuntimeStateSnapshot snapshot,
            out TileBase tile)
        {
            tile = null;
            int selectedPriority = int.MinValue;
            for (int stateIndex = 0; stateIndex < snapshot.ActiveStates.Count; stateIndex++)
            {
                ETerrainElementStateKind stateKind =
                    snapshot.ActiveStates[stateIndex].StateKind;
                for (int mappingIndex = 0;
                     mappingIndex < m_stateTiles.Length;
                     mappingIndex++)
                {
                    TerrainStateTileMapping mapping = m_stateTiles[mappingIndex];
                    if (mapping != null &&
                        mapping.StateKind == stateKind &&
                        mapping.Tile != null &&
                        mapping.Priority >= selectedPriority)
                    {
                        selectedPriority = mapping.Priority;
                        tile = mapping.Tile;
                    }
                }
            }

            return tile != null;
        }

        public bool TryGetSurfaceTile(
            ETerrainSurfaceKind surfaceKind,
            out TileBase tile)
        {
            for (int i = 0; i < m_surfaceTiles.Length; i++)
            {
                TerrainSurfaceTileMapping mapping = m_surfaceTiles[i];
                if (mapping != null &&
                    mapping.SurfaceKind == surfaceKind &&
                    mapping.Tile != null)
                {
                    tile = mapping.Tile;
                    return true;
                }
            }

            tile = null;
            return false;
        }

        public bool TryGetSignalTile(
            EElementPresentationSignal signal,
            out TileBase tile,
            out float duration)
        {
            for (int i = 0; i < m_signalTiles.Length; i++)
            {
                TerrainSignalTileMapping mapping = m_signalTiles[i];
                if (mapping != null &&
                    mapping.Signal == signal &&
                    mapping.Tile != null)
                {
                    tile = mapping.Tile;
                    duration = mapping.Duration;
                    return true;
                }
            }

            tile = null;
            duration = 0.0f;
            return false;
        }
    }
}
