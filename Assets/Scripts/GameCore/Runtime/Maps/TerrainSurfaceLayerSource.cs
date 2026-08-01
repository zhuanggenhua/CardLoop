using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 一个作者/表现 Tilemap 到玩法地表语义的显式映射来源。
    /// Tilemap 仍负责视觉分层；可燃、可销毁、可再生等玩法语义由这里的 Tile 映射声明。
    /// 寻路规则和 Unity 物理碰撞不从这里读取。
    /// </summary>
    [Serializable]
    public sealed class TerrainSurfaceLayerSource
    {
        [InspectorName("来源 ID")]
        [Tooltip("稳定来源 ID，用于运行时表现恢复和后续持久化定位。不同地表来源层必须唯一。")]
        [SerializeField] private int m_sourceId =
            TerrainSurfaceCoverSourceReference.DefaultSurfaceLayerSourceId;

        [InspectorName("作者层职责")]
        [Tooltip("描述该 Tilemap 的作者/表现职责，例如地表覆盖或装饰。玩法是否可燃由 Tile 映射决定。")]
        [SerializeField] private ETerrainSurfaceLayerRole m_role =
            ETerrainSurfaceLayerRole.SurfaceCover;

        [InspectorName("来源 Tilemap")]
        [Tooltip("承载该作者层视觉 Tile 的 Tilemap。不要用层名推断玩法语义。")]
        [SerializeField] private Tilemap m_tilemap = null;

        [InspectorName("解析优先级")]
        [Tooltip("同一格多个来源都映射为地表覆盖时，数值越小越先被当前首期单覆盖槽选中。")]
        [SerializeField] private int m_priority = 0;

        [InspectorName("地表覆盖 Tile 映射")]
        [Tooltip("显式声明本层哪些 Tile 对应草、花、苔藓、道路覆盖等可反应组件。未配置的 Tile 只保留视觉职责。")]
        [SerializeField] private TerrainSurfaceCoverTileMapping[] m_surfaceCoverTileMappings =
            Array.Empty<TerrainSurfaceCoverTileMapping>();

        public int SourceId => m_sourceId;
        public ETerrainSurfaceLayerRole Role => m_role;
        public Tilemap Tilemap => m_tilemap;
        public int Priority => m_priority;
        public bool IsValid =>
            m_tilemap != null && m_role != ETerrainSurfaceLayerRole.None;

        public bool TryResolveSurfaceCover(
            Vector3Int cell,
            out ETerrainSurfaceCoverKind coverKind,
            out ETerrainSurfaceCoverTraits traits)
        {
            coverKind = ETerrainSurfaceCoverKind.None;
            traits = ETerrainSurfaceCoverTraits.None;
            if (!IsValid || m_surfaceCoverTileMappings == null)
            {
                return false;
            }

            TileBase tile = m_tilemap.GetTile(cell);
            if (tile == null)
            {
                return false;
            }

            for (int i = 0; i < m_surfaceCoverTileMappings.Length; i++)
            {
                TerrainSurfaceCoverTileMapping mapping = m_surfaceCoverTileMappings[i];
                if (mapping == null ||
                    !mapping.IsValid ||
                    mapping.Tile != tile)
                {
                    continue;
                }

                coverKind = mapping.CoverKind;
                traits = mapping.Traits;
                return true;
            }

            return false;
        }
    }
}
