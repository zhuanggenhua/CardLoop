using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// A* 图中的单个可行走地形节点。
    /// 节点只缓存规则 Tile 的导航语义，不引用 Tilemap 或场景对象。
    /// </summary>
    internal readonly struct TerrainNavigationGraphNode
    {
        public TerrainNavigationGraphNode(
            in TerrainNodeKey nodeKey,
            int elevation,
            ETerrainSurfaceKind surfaceKind,
            float traversalCost)
        {
            NodeKey = nodeKey;
            Elevation = elevation;
            SurfaceKind = surfaceKind;
            TraversalCost = traversalCost;
        }

        public TerrainNodeKey NodeKey { get; }
        public int Elevation { get; }
        public ETerrainSurfaceKind SurfaceKind { get; }
        public float TraversalCost { get; }
    }

    /// <summary>
    /// 两个地形节点之间的有向边。
    /// 同层移动和楼梯/坡道等显式跨层连接都统一进入这张图。
    /// </summary>
    internal readonly struct TerrainNavigationGraphEdge
    {
        public TerrainNavigationGraphEdge(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode,
            float traversalCost,
            TerrainTransitionLink transitionLink = null)
        {
            FromNode = fromNode;
            ToNode = toNode;
            TraversalCost = Mathf.Max(0.01f, traversalCost);
            TransitionLink = transitionLink;
        }

        public TerrainNodeKey FromNode { get; }
        public TerrainNodeKey ToNode { get; }
        public float TraversalCost { get; }
        public TerrainTransitionLink TransitionLink { get; }
        public bool IsTransition => TransitionLink != null;
    }

    /// <summary>
    /// 地形导航的轻量 A* 图。
    /// TerrainNavigationMap 负责把 Tilemap 投影成节点和边，本类型只负责确定性寻路。
    /// </summary>
    internal sealed class TerrainNavigationGraph
    {
        private readonly Dictionary<TerrainNodeKey, TerrainNavigationGraphNode> m_nodes = new();
        private readonly Dictionary<TerrainNodeKey, List<TerrainNavigationGraphEdge>> m_edges = new();

        // A* 的临时容器复用，避免每次点击移动都分配一批短生命周期集合。
        private readonly List<TerrainNodeKey> m_openSet = new();
        private readonly HashSet<TerrainNodeKey> m_closedSet = new();
        private readonly Dictionary<TerrainNodeKey, TerrainNodeKey> m_cameFrom = new();
        private readonly Dictionary<TerrainNodeKey, float> m_gScore = new();
        private readonly Dictionary<TerrainNodeKey, float> m_fScore = new();

        public int NodeCount => m_nodes.Count;
        public int EdgeCount { get; private set; }

        public void Clear()
        {
            m_nodes.Clear();
            m_edges.Clear();
            EdgeCount = 0;
            ClearPathScratch();
        }

        public bool AddNode(
            in TerrainNodeKey nodeKey,
            TerrainNavigationTile tile,
            float traversalCost)
        {
            if (tile == null || !tile.Walkable || m_nodes.ContainsKey(nodeKey))
            {
                return false;
            }

            TerrainNavigationGraphNode node = new(
                nodeKey,
                tile.Elevation,
                tile.SurfaceKind,
                Mathf.Max(0.01f, traversalCost));
            m_nodes.Add(nodeKey, node);
            m_edges.Add(nodeKey, new List<TerrainNavigationGraphEdge>());
            return true;
        }

        public bool ContainsNode(in TerrainNodeKey nodeKey)
        {
            return m_nodes.ContainsKey(nodeKey);
        }

        public bool TryGetNode(
            in TerrainNodeKey nodeKey,
            out TerrainNavigationGraphNode node)
        {
            return m_nodes.TryGetValue(nodeKey, out node);
        }

        public bool TryAddSameLayerEdge(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode,
            float traversalCost)
        {
            if (fromNode.LayerId != toNode.LayerId ||
                !m_nodes.ContainsKey(fromNode) ||
                !m_nodes.ContainsKey(toNode))
            {
                return false;
            }

            return TryAddDirectedEdge(fromNode, toNode, traversalCost, null);
        }

        public bool TryAddTransitionEdge(TerrainTransitionLink link)
        {
            if (link == null || !link.IsValid)
            {
                return false;
            }

            bool added = TryAddDirectedEdge(
                link.FromNode,
                link.ToNode,
                link.TraversalCost,
                link);
            if (link.Bidirectional)
            {
                added |= TryAddDirectedEdge(
                    link.ToNode,
                    link.FromNode,
                    link.TraversalCost,
                    link);
            }

            return added;
        }

        private bool TryAddDirectedEdge(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode,
            float traversalCost,
            TerrainTransitionLink transitionLink)
        {
            if (!m_nodes.ContainsKey(fromNode) ||
                !m_nodes.ContainsKey(toNode) ||
                !m_edges.TryGetValue(fromNode, out List<TerrainNavigationGraphEdge> edges))
            {
                return false;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].ToNode == toNode)
                {
                    return false;
                }
            }

            edges.Add(new TerrainNavigationGraphEdge(
                fromNode,
                toNode,
                traversalCost,
                transitionLink));
            EdgeCount++;
            return true;
        }

        public bool HasEdge(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode)
        {
            if (!m_edges.TryGetValue(fromNode, out List<TerrainNavigationGraphEdge> edges))
            {
                return false;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].ToNode == toNode)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetEdge(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode,
            out TerrainNavigationGraphEdge edge)
        {
            if (m_edges.TryGetValue(fromNode, out List<TerrainNavigationGraphEdge> edges))
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    if (edges[i].ToNode == toNode)
                    {
                        edge = edges[i];
                        return true;
                    }
                }
            }

            edge = default;
            return false;
        }

        public bool TryGetEdges(
            in TerrainNodeKey nodeKey,
            out IReadOnlyList<TerrainNavigationGraphEdge> edges)
        {
            if (m_edges.TryGetValue(nodeKey, out List<TerrainNavigationGraphEdge> nodeEdges))
            {
                edges = nodeEdges;
                return true;
            }

            edges = Array.Empty<TerrainNavigationGraphEdge>();
            return false;
        }

        public bool TryFindPath(
            in TerrainNodeKey startNode,
            in TerrainNodeKey goalNode,
            List<TerrainNodeKey> path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            path.Clear();
            ClearPathScratch();
            if (!m_nodes.ContainsKey(startNode) || !m_nodes.ContainsKey(goalNode))
            {
                return false;
            }

            m_openSet.Add(startNode);
            m_gScore[startNode] = 0.0f;
            m_fScore[startNode] = Heuristic(startNode, goalNode);

            while (m_openSet.Count > 0)
            {
                TerrainNodeKey current = TakeBestOpenNode();
                if (current == goalNode)
                {
                    ReconstructPath(current, path);
                    return true;
                }

                m_closedSet.Add(current);
                if (!m_edges.TryGetValue(current, out List<TerrainNavigationGraphEdge> edges))
                {
                    continue;
                }

                for (int i = 0; i < edges.Count; i++)
                {
                    TerrainNavigationGraphEdge edge = edges[i];
                    TerrainNodeKey neighbor = edge.ToNode;
                    if (m_closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentativeScore = m_gScore[current] + edge.TraversalCost;
                    if (m_gScore.TryGetValue(neighbor, out float knownScore) &&
                        tentativeScore >= knownScore)
                    {
                        continue;
                    }

                    m_cameFrom[neighbor] = current;
                    m_gScore[neighbor] = tentativeScore;
                    m_fScore[neighbor] = tentativeScore + Heuristic(neighbor, goalNode);
                    if (!m_openSet.Contains(neighbor))
                    {
                        m_openSet.Add(neighbor);
                    }
                }
            }

            return false;
        }

        // 地图规模较小，线性扫描 open set 比引入额外堆结构更直接，也便于保持路径选择稳定。
        private TerrainNodeKey TakeBestOpenNode()
        {
            int bestIndex = 0;
            TerrainNodeKey bestNode = m_openSet[0];
            float bestScore = m_fScore.TryGetValue(bestNode, out float score)
                ? score
                : float.PositiveInfinity;

            for (int i = 1; i < m_openSet.Count; i++)
            {
                TerrainNodeKey candidate = m_openSet[i];
                float candidateScore = m_fScore.TryGetValue(candidate, out float value)
                    ? value
                    : float.PositiveInfinity;
                if (candidateScore >= bestScore)
                {
                    continue;
                }

                bestIndex = i;
                bestNode = candidate;
                bestScore = candidateScore;
            }

            m_openSet.RemoveAt(bestIndex);
            return bestNode;
        }

        private void ReconstructPath(
            TerrainNodeKey current,
            List<TerrainNodeKey> path)
        {
            path.Add(current);
            while (m_cameFrom.TryGetValue(current, out TerrainNodeKey previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
        }

        private void ClearPathScratch()
        {
            m_openSet.Clear();
            m_closedSet.Clear();
            m_cameFrom.Clear();
            m_gScore.Clear();
            m_fScore.Clear();
        }

        private static float Heuristic(
            in TerrainNodeKey fromNode,
            in TerrainNodeKey toNode)
        {
            Vector3Int from = fromNode.Cell;
            Vector3Int to = toNode.Cell;
            int planarDistance = Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
            int layerPenalty = fromNode.LayerId == toNode.LayerId ? 0 : 1;
            return planarDistance + layerPenalty;
        }
    }
}
