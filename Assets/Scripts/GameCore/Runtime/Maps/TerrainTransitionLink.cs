using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 两个地形节点之间的显式跨层连接。
    /// 用于坡道、楼梯、梯子和落差；同格不同层不会因为重叠而自动连通。
    /// </summary>
    [Serializable]
    public sealed class TerrainTransitionLink
    {
        [Header("端点")]
        [InspectorName("起点层 ID")]
        [Tooltip("跨层连接的起点逻辑层。必须对应 TerrainNavigationLayerSource 的层 ID。")]
        [SerializeField] private int m_fromLayerId = TerrainNodeKey.DefaultLayerId;

        [InspectorName("起点格")]
        [Tooltip("连接从哪个规则格出发。")]
        [SerializeField] private Vector3Int m_fromCell;

        [InspectorName("终点层 ID")]
        [Tooltip("跨层连接的终点逻辑层。")]
        [SerializeField] private int m_toLayerId = TerrainNodeKey.DefaultLayerId;

        [InspectorName("终点格")]
        [Tooltip("连接到达的规则格。")]
        [SerializeField] private Vector3Int m_toCell;

        [Header("过渡")]
        [InspectorName("过渡类型")]
        [Tooltip("说明该连接的作者语义，例如坡道、楼梯或落差；None 会被视为无效连接。")]
        [SerializeField] private ETerrainTransitionLinkKind m_kind = ETerrainTransitionLinkKind.Ramp;

        [InspectorName("允许反向")]
        [Tooltip("开启后会同时生成反向边；关闭时只允许从起点到终点。")]
        [SerializeField] private bool m_bidirectional = true;

        [InspectorName("附加代价")]
        [Tooltip("进入该跨层连接的相对寻路代价。")]
        [Min(0.01f)]
        [SerializeField] private float m_traversalCost = 1.0f;

        [InspectorName("连续路径点")]
        [Tooltip("跨层过程中要经过的连续世界坐标点。为空时路径只使用节点中心。")]
        [SerializeField] private Vector2[] m_worldWaypoints = Array.Empty<Vector2>();

        [InspectorName("切层提交点")]
        [Tooltip("角色到达该世界坐标后可认为已经提交到目标层，用于后续连续跨层表现。")]
        [SerializeField] private Vector2 m_commitPoint;

        public TerrainNodeKey FromNode => new(m_fromLayerId, m_fromCell);
        public TerrainNodeKey ToNode => new(m_toLayerId, m_toCell);
        public ETerrainTransitionLinkKind Kind => m_kind;
        public bool Bidirectional => m_bidirectional;
        public float TraversalCost => Mathf.Max(0.01f, m_traversalCost);
        public IReadOnlyList<Vector2> WorldWaypoints => m_worldWaypoints ?? Array.Empty<Vector2>();
        public Vector2 CommitPoint => m_commitPoint;
        public bool IsValid => FromNode != ToNode && m_kind != ETerrainTransitionLinkKind.None;

        internal TerrainTransitionLink(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode,
            ETerrainTransitionLinkKind kind,
            bool bidirectional,
            float traversalCost,
            Vector2[] worldWaypoints,
            Vector2 commitPoint)
        {
            m_fromLayerId = fromNode.LayerId;
            m_fromCell = fromNode.Cell;
            m_toLayerId = toNode.LayerId;
            m_toCell = toNode.Cell;
            m_kind = kind;
            m_bidirectional = bidirectional;
            m_traversalCost = Mathf.Max(0.01f, traversalCost);
            m_worldWaypoints = worldWaypoints ?? Array.Empty<Vector2>();
            m_commitPoint = commitPoint;
        }
    }

    /// <summary>
    /// 跨层连接的作者语义。
    /// 当前只参与导航和调试，不自动生成视觉或碰撞体。
    /// </summary>
    public enum ETerrainTransitionLinkKind
    {
        None = 0,
        Ramp = 1,
        Stairs = 2,
        Ladder = 3,
        Drop = 4
    }
}
