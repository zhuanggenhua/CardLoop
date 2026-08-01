using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 点击目标解析失败的可诊断原因。
    /// 调用方用它生成玩家/调试可读的失败状态，而不是默默回退成直线移动。
    /// </summary>
    internal enum ETerrainDestinationResolutionFailure
    {
        None = 0,
        StartNodeUnavailable = 1,
        NoCandidate = 2,
        Unreachable = 3,
        Ambiguous = 4
    }

    /// <summary>
    /// 一次点击目标对应的候选导航节点。
    /// WorldPosition 保留最终落点，可与节点中心不同，用于“格子可达但目标点需吸附”的情况。
    /// </summary>
    internal readonly struct TerrainDestinationCandidate
    {
        public TerrainDestinationCandidate(
            in TerrainNodeKey nodeKey,
            Vector2 worldPosition,
            bool wasSnapped)
        {
            NodeKey = nodeKey;
            WorldPosition = worldPosition;
            WasSnapped = wasSnapped;
        }

        public TerrainNodeKey NodeKey { get; }
        public Vector2 WorldPosition { get; }
        public bool WasSnapped { get; }
    }

    /// <summary>
    /// 从世界点击点解析唯一可达导航目标。
    /// 它只做候选筛选和歧义判断，实际图数据与路径搜索仍由 TerrainNavigationMap 提供。
    /// </summary>
    internal sealed class TerrainDestinationResolver
    {
        private readonly List<TerrainDestinationCandidate> m_candidates = new();
        private readonly List<TerrainNodeKey> m_pathScratch = new();

        public bool TryResolveStart(
            TerrainNavigationMap map,
            Vector2 startWorld,
            int currentLayerId,
            out TerrainDestinationCandidate start)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return map.TryResolveNavigationCandidateOnLayer(
                startWorld,
                currentLayerId,
                out start);
        }

        public bool TryResolveDestination(
            TerrainNavigationMap map,
            in TerrainNodeKey startNode,
            Vector2 destinationWorld,
            int currentLayerId,
            out TerrainDestinationCandidate destination,
            out ETerrainDestinationResolutionFailure failure)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            map.CollectNavigationCandidates(destinationWorld, m_candidates);
            if (m_candidates.Count == 0)
            {
                destination = default;
                failure = ETerrainDestinationResolutionFailure.NoCandidate;
                return false;
            }

            // 多层同格点击可能命中多个候选；优先当前层，其次只接受唯一可达目标，避免误跨层。
            int reachableCount = 0;
            TerrainDestinationCandidate uniqueReachable = default;
            bool hasReachableCurrentLayer = false;
            TerrainDestinationCandidate reachableCurrentLayer = default;
            for (int i = 0; i < m_candidates.Count; i++)
            {
                TerrainDestinationCandidate candidate = m_candidates[i];
                if (!map.TryBuildNodePath(startNode, candidate.NodeKey, m_pathScratch))
                {
                    continue;
                }

                reachableCount++;
                uniqueReachable = candidate;
                if (candidate.NodeKey.LayerId == currentLayerId)
                {
                    hasReachableCurrentLayer = true;
                    reachableCurrentLayer = candidate;
                }
            }

            if (hasReachableCurrentLayer)
            {
                destination = reachableCurrentLayer;
                failure = ETerrainDestinationResolutionFailure.None;
                return true;
            }

            if (reachableCount == 1)
            {
                destination = uniqueReachable;
                failure = ETerrainDestinationResolutionFailure.None;
                return true;
            }

            destination = default;
            failure = reachableCount == 0
                ? ETerrainDestinationResolutionFailure.Unreachable
                : ETerrainDestinationResolutionFailure.Ambiguous;
            return false;
        }
    }
}
