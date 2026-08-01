using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 当前场景的 Tilemap 地形规则和路径查询入口。
    /// 它把规则 Tilemap 投影成 A* 输入，但不接管角色移动，也不从视觉 Tilemap 猜玩法数据。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainNavigationMap : MonoBehaviour
    {
        [Header("寻路规则层")]
        [InspectorName("兼容默认寻路规则 Tilemap")]
        [Tooltip("旧单层地图的兼容入口。它是寻路真相源，不是视觉层，也不是 Unity 物理碰撞层。多层地图应改用下方规则层来源列表。")]
        [SerializeField] private Tilemap m_ruleTilemap = null;

        [InspectorName("寻路规则层来源")]
        [Tooltip("同一个地形导航入口管理的多个逻辑寻路层。为空时自动使用兼容默认寻路规则 Tilemap。")]
        [SerializeField] private TerrainNavigationLayerSource[] m_layerSources = Array.Empty<TerrainNavigationLayerSource>();

        [Header("地表语义来源")]
        [InspectorName("地表语义来源层")]
        [Tooltip("多个作者/表现 Tilemap 到玩法地表语义的显式映射。用于让地表覆盖、装饰等来源都能参与元素反应；不负责寻路和物理碰撞。")]
        [SerializeField] private TerrainSurfaceLayerSource[] m_surfaceLayerSources =
            Array.Empty<TerrainSurfaceLayerSource>();

        [InspectorName("兼容上层地表 Tilemap")]
        [Tooltip("旧版单地表覆盖兼容入口。新地图应优先使用“地表语义来源层”。")]
        [SerializeField] private Tilemap m_surfaceCoverTilemap = null;

        [InspectorName("兼容上层地表 Tile 映射")]
        [Tooltip("旧版单覆盖层 Tile 映射。新地图应把映射配置到对应“地表语义来源层”上。")]
        [SerializeField] private TerrainSurfaceCoverTileMapping[] m_surfaceCoverTileMappings =
            Array.Empty<TerrainSurfaceCoverTileMapping>();

        [InspectorName("跨层连接")]
        [Tooltip("显式连接两个地形节点的坡道、楼梯、梯子或落差入口。同格不同层不会自动连通。")]
        [SerializeField] private TerrainTransitionLink[] m_transitionLinks = Array.Empty<TerrainTransitionLink>();

        [Header("路径设置")]
        [InspectorName("最近可行走格搜索半径")]
        [Min(0)]
        [Tooltip("点击点落在阻挡边缘时，允许在该格半径内寻找最近合法目标。找不到时命令明确失败。")]
        [SerializeField] private int m_nearestWalkableSearchRadius = 2;

        [Header("编辑器调试")]
        [InspectorName("绘制最近导航路径")]
        [Tooltip("在 Scene 视图中绘制最近一次点击寻路的起点、目标和实际路径。需要开启 Scene 视图的 Gizmos。")]
        [SerializeField] private bool m_drawLastNavigationPath = true;

        [InspectorName("路径标记半径")]
        [Min(0.01f)]
        [SerializeField] private float m_debugMarkerRadius = 0.26f;

        [InspectorName("成功路径颜色")]
        [SerializeField] private Color m_debugPathColor = new(1.0f, 0.88f, 0.05f, 1.0f);

        [InspectorName("失败目标颜色")]
        [SerializeField] private Color m_debugFailureColor = new(1.0f, 0.25f, 0.2f, 1.0f);

        [InspectorName("吸附目标颜色")]
        [SerializeField] private Color m_debugResolvedGoalColor = new(0.15f, 0.75f, 1.0f, 1.0f);

        [Header("编辑器路径预览")]
        [InspectorName("启用编辑器路径预览")]
        [Tooltip("选中本组件时，可在 Scene 视图拖动起点和点击点并直接查看真实导航路径。")]
        [SerializeField] private bool m_showEditorNavigationPreview = true;

        [InspectorName("预览起点")]
        [SerializeField] private Vector2 m_editorPreviewStart = new(0.75f, -3.1f);

        [InspectorName("预览点击点")]
        [SerializeField] private Vector2 m_editorPreviewDestination = new(5.2f, -3.1f);

        [Header("运行时路径提示")]
        [InspectorName("显示运行时路径提示")]
        [Tooltip("在 Game 视图中显示最近一次点击移动的一条可走路线、目标点或失败红叉。")]
        [SerializeField] private bool m_showRuntimeNavigationPath = true;

        [InspectorName("显示运行时调试细节")]
        [Tooltip("开启后叠加起点、点击点、吸附格、辅助线和路径点圈；默认关闭，避免玩家视图混入内部寻路细节。")]
        [SerializeField] private bool m_showRuntimeNavigationDebugDetails;

        [InspectorName("路径线宽")]
        [Min(0.01f)]
        [SerializeField] private float m_runtimePathLineWidth = 0.18f;

        [InspectorName("路径点半径")]
        [Min(0.01f)]
        [SerializeField] private float m_runtimeWaypointRadius = 0.11f;

        [InspectorName("目标圆环分段")]
        [Range(12, 96)]
        [SerializeField] private int m_runtimeMarkerSegments = 40;

        private static readonly Vector3Int[] CardinalNeighborOffsets =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down
        };

        private readonly Dictionary<TerrainNodeKey, TerrainCellRuntimeState> m_runtimeSurfaceStates = new();
        private readonly Dictionary<TerrainNodeKey, float> m_runtimeTraversalCostMultipliers = new();
        private readonly Queue<Vector3Int> m_areaTraversalQueue = new();
        private readonly HashSet<Vector3Int> m_areaVisitedCells = new();
        private readonly List<TerrainNodeKey> m_areaNodeScratch = new();
        private readonly List<TerrainNavigationLayerSource> m_activeLayerSources = new();
        private readonly List<TerrainSurfaceLayerSource> m_activeSurfaceLayerSources = new();
        private readonly HashSet<int> m_layerIdScratch = new();
        private readonly HashSet<int> m_surfaceSourceIdScratch = new();
        private readonly HashSet<TerrainNodeKey> m_layerNodeScratch = new();
        private readonly HashSet<int> m_destinationLayerScratch = new();
        private readonly List<TerrainNodeKey> m_nodePathScratch = new();
        private readonly TerrainNavigationGraph m_navigationGraph = new();
        private readonly TerrainDestinationResolver m_destinationResolver = new();

        private BoundsInt m_cachedBounds;
        private TerrainNavigationTile[,] m_cachedTiles;
        private float[,] m_cachedCostMap;
        private Vector2 m_lastDebugStart;
        private Vector2 m_lastDebugDestination;
        private Vector2 m_lastDebugFinalDestination;
        private Vector2 m_lastDebugResolvedCellCenter;
        private Vector2[] m_lastDebugWorldPath = Array.Empty<Vector2>();
        private bool m_hasDebugPathRequest;
        private bool m_lastDebugPathSucceeded;
        private string m_lastDebugPathStatus = string.Empty;
        private bool m_suppressNavigationDebug;
        private readonly TerrainNavigationRuntimePathDebugView m_runtimePathDebugView = new();

        public Tilemap RuleTilemap => ActiveRuleTilemap;
        public IReadOnlyList<TerrainNavigationLayerSource> LayerSources => m_layerSources;
        public IReadOnlyList<TerrainSurfaceLayerSource> SurfaceLayerSources =>
            m_surfaceLayerSources;
        public IReadOnlyList<TerrainTransitionLink> TransitionLinks => m_transitionLinks;
        public int RuntimeStateCount => m_runtimeSurfaceStates.Count;
        public bool ShowEditorNavigationPreview => m_showEditorNavigationPreview;
        public Vector2 EditorPreviewStart => m_editorPreviewStart;
        public Vector2 EditorPreviewDestination => m_editorPreviewDestination;

        private Tilemap ActiveRuleTilemap
        {
            get
            {
                TerrainNavigationLayerSource defaultLayer = DefaultLayerSource;
                return defaultLayer != null && defaultLayer.IsValid
                    ? defaultLayer.RuleTilemap
                    : m_ruleTilemap;
            }
        }

        private TerrainNavigationLayerSource DefaultLayerSource
        {
            get
            {
                if (m_layerSources == null)
                {
                    return null;
                }

                for (int i = 0; i < m_layerSources.Length; i++)
                {
                    TerrainNavigationLayerSource source = m_layerSources[i];
                    if (source != null &&
                        source.LayerId == TerrainNodeKey.DefaultLayerId &&
                        source.IsValid)
                    {
                        return source;
                    }
                }

                return null;
            }
        }

        public event Action<TerrainCellStateChange> CellStateChanged;
        public event Action RuntimeSurfaceStatesCleared;

        private void OnEnable()
        {
            RefreshNavigationData();
        }

        private void OnDisable()
        {
            ClearRuntimeNavigationDebugPath();
        }

        /// <summary>
        /// 重新读取规则 Tilemap。地图作者修改规则格或运行时替换规则后必须调用一次。
        /// </summary>
        public void RefreshNavigationData()
        {
            if (!TryRefreshLayerSources(out Tilemap activeRuleTilemap))
            {
                ClearNavigationCache();
                return;
            }

            if (!TryRefreshSurfaceLayerSources())
            {
                ClearNavigationCache();
                return;
            }

            if (activeRuleTilemap == null)
            {
                ClearNavigationCache();
                return;
            }

            m_cachedBounds = activeRuleTilemap.cellBounds;
            int width = m_cachedBounds.size.x;
            int height = m_cachedBounds.size.y;
            if (width <= 0 || height <= 0)
            {
                ClearNavigationCache();
                return;
            }

            m_cachedTiles = new TerrainNavigationTile[height, width];
            m_cachedCostMap = new float[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3Int cell = IndexToCell(x, y);
                    TerrainNavigationTile tile = activeRuleTilemap.GetTile<TerrainNavigationTile>(cell);
                    m_cachedTiles[y, x] = tile;
                    m_cachedCostMap[y, x] = tile != null && tile.Walkable ? tile.TraversalCost : -1.0f;
                }
            }

            foreach (KeyValuePair<TerrainNodeKey, float> runtimeCost in m_runtimeTraversalCostMultipliers)
            {
                UpdateCachedTraversalCost(runtimeCost.Key, runtimeCost.Value);
            }

            BuildNavigationGraph();
        }

        /// <summary>
        /// 为即时点击移动生成连续世界路径点。
        /// 返回 false 表示当前规则地图不存在合法路线，调用方不得回退成穿越悬崖的直线命令。
        /// </summary>
        public bool TryBuildWorldPath(Vector2 startWorld, Vector2 destinationWorld, out Vector2[] worldPath)
        {
            return TryBuildWorldPath(
                startWorld,
                TerrainNodeKey.DefaultLayerId,
                destinationWorld,
                out worldPath,
                out _);
        }

        public bool TryResolveRampMovementDirection(
            Vector2 currentWorld,
            Vector2 desiredDirection,
            out Vector2 resolvedDirection)
        {
            resolvedDirection = desiredDirection;
            if (desiredDirection.sqrMagnitude <= 0.000001f ||
                !EnsureNavigationData())
            {
                return false;
            }

            Tilemap tilemap = ActiveRuleTilemap;
            if (tilemap == null)
            {
                return false;
            }

            Vector3Int currentCell = tilemap.WorldToCell(currentWorld);
            if (!TryGetTile(currentCell, out TerrainNavigationTile currentTile) ||
                currentTile.TransitionKind != ETerrainTransitionKind.Ramp ||
                currentTile.RampDirection == ETerrainRampDirection.None)
            {
                return false;
            }

            Vector2 inputDirection = desiredDirection.normalized;
            Vector2 bestDirection = Vector2.zero;
            float bestScore = 0.25f;
            for (int i = 0; i < CardinalNeighborOffsets.Length; i++)
            {
                Vector3Int candidateCell = currentCell + CardinalNeighborOffsets[i];
                if (!TryGetTile(candidateCell, out TerrainNavigationTile candidateTile) ||
                    !candidateTile.Walkable ||
                    !CanTraverseElevation(currentCell, currentTile, candidateCell, candidateTile))
                {
                    continue;
                }

                bool isSameRamp =
                    candidateTile.TransitionKind == ETerrainTransitionKind.Ramp &&
                    candidateTile.RampDirection == currentTile.RampDirection;
                bool isGroundExit = candidateTile.TransitionKind != ETerrainTransitionKind.Ramp;
                if (!isSameRamp && !isGroundExit)
                {
                    continue;
                }

                Vector2 candidateCenter = tilemap.GetCellCenterWorld(candidateCell);
                Vector2 candidateDirection = candidateCenter - currentWorld;
                if (candidateDirection.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                candidateDirection.Normalize();
                float score = Vector2.Dot(inputDirection, candidateDirection);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = candidateDirection;
                }
            }

            if (bestDirection.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            resolvedDirection = bestDirection;
            return true;
        }

        internal bool TryBuildWorldPathWithoutDebug(
            Vector2 startWorld,
            Vector2 destinationWorld,
            out Vector2[] worldPath)
        {
            Vector2 previousStart = m_lastDebugStart;
            Vector2 previousDestination = m_lastDebugDestination;
            Vector2 previousFinalDestination = m_lastDebugFinalDestination;
            Vector2 previousResolvedCellCenter = m_lastDebugResolvedCellCenter;
            Vector2[] previousWorldPath = m_lastDebugWorldPath;
            bool previousHasRequest = m_hasDebugPathRequest;
            bool previousSucceeded = m_lastDebugPathSucceeded;
            string previousStatus = m_lastDebugPathStatus;
            bool previousSuppress = m_suppressNavigationDebug;

            m_suppressNavigationDebug = true;
            try
            {
                return TryBuildWorldPath(startWorld, destinationWorld, out worldPath);
            }
            finally
            {
                m_lastDebugStart = previousStart;
                m_lastDebugDestination = previousDestination;
                m_lastDebugFinalDestination = previousFinalDestination;
                m_lastDebugResolvedCellCenter = previousResolvedCellCenter;
                m_lastDebugWorldPath = previousWorldPath;
                m_hasDebugPathRequest = previousHasRequest;
                m_lastDebugPathSucceeded = previousSucceeded;
                m_lastDebugPathStatus = previousStatus;
                m_suppressNavigationDebug = previousSuppress;
            }
        }

        public bool TryBuildWorldPath(
            Vector2 startWorld,
            int currentLayerId,
            Vector2 destinationWorld,
            out Vector2[] worldPath,
            out TerrainNodeKey destinationNode)
        {
            worldPath = Array.Empty<Vector2>();
            destinationNode = default;
            m_hasDebugPathRequest = true;
            m_lastDebugStart = startWorld;
            m_lastDebugDestination = destinationWorld;
            m_lastDebugFinalDestination = destinationWorld;
            m_lastDebugResolvedCellCenter = destinationWorld;
            m_lastDebugWorldPath = Array.Empty<Vector2>();
            m_lastDebugPathSucceeded = false;
            m_lastDebugPathStatus = "正在计算路径";

            if (!EnsureNavigationData())
            {
                m_lastDebugPathStatus = "失败：缺少有效规则地图";
                Debug.LogError($"地形导航组件 '{name}' 缺少有效的规则 Tilemap，无法计算路径。", this);
                SyncRuntimeNavigationDebugPath();
                return false;
            }

            if (!m_destinationResolver.TryResolveStart(
                    this,
                    startWorld,
                    currentLayerId,
                    out TerrainDestinationCandidate start))
            {
                m_lastDebugPathStatus = "失败：当前地形层附近没有可行走起点";
                SyncRuntimeNavigationDebugPath();
                return false;
            }

            if (!m_destinationResolver.TryResolveDestination(
                    this,
                    start.NodeKey,
                    destinationWorld,
                    currentLayerId,
                    out TerrainDestinationCandidate destination,
                    out ETerrainDestinationResolutionFailure resolutionFailure))
            {
                m_lastDebugPathStatus = GetDestinationFailureStatus(resolutionFailure);
                SyncRuntimeNavigationDebugPath();
                return false;
            }

            destinationNode = destination.NodeKey;
            if (!TryGetNodeWorldCenter(destination.NodeKey, out Vector2 resolvedCellCenter))
            {
                m_lastDebugPathStatus = "失败：目标地形层缺少有效 Tilemap";
                SyncRuntimeNavigationDebugPath();
                return false;
            }

            m_lastDebugResolvedCellCenter = resolvedCellCenter;
            m_lastDebugFinalDestination = destination.WorldPosition;
            m_nodePathScratch.Clear();
            if (!m_navigationGraph.TryFindPath(
                    start.NodeKey,
                    destination.NodeKey,
                    m_nodePathScratch))
            {
                m_lastDebugPathStatus = "失败：目标节点不可达";
                SyncRuntimeNavigationDebugPath();
                return false;
            }

            worldPath = ConvertNodePathToWorldPath(m_nodePathScratch, destination);
            m_lastDebugWorldPath = worldPath;
            m_lastDebugPathSucceeded = worldPath.Length > 0;
            if (m_lastDebugPathSucceeded)
            {
                m_lastDebugFinalDestination = worldPath[^1];
            }

            m_lastDebugPathStatus = m_lastDebugPathSucceeded
                ? $"成功：{worldPath.Length} 个路径点"
                : "失败：路径点为空";
            SyncRuntimeNavigationDebugPath();
            return m_lastDebugPathSucceeded;
        }

        private static string GetDestinationFailureStatus(
            ETerrainDestinationResolutionFailure failure)
        {
            return failure switch
            {
                ETerrainDestinationResolutionFailure.NoCandidate =>
                    "失败：点击点附近没有可行走地形层",
                ETerrainDestinationResolutionFailure.Unreachable =>
                    "失败：点击位置存在地形层，但当前节点无法到达",
                ETerrainDestinationResolutionFailure.Ambiguous =>
                    "失败：点击位置对应多个可达地形层，无法确定目标",
                _ => "失败：无法解析点击目标"
            };
        }

        /// <summary>
        /// 查询世界坐标下的正式地形规则。
        /// </summary>
        public bool TryGetSurfaceSample(Vector2 worldPosition, out TerrainSurfaceSample sample)
        {
            if (!EnsureNavigationData())
            {
                sample = default;
                return false;
            }

            Vector3Int cell = ActiveRuleTilemap.WorldToCell(worldPosition);
            return TryGetSurfaceSample(cell, out sample);
        }

        public bool TryGetSurfaceSample(Vector3Int cell, out TerrainSurfaceSample sample)
        {
            return TryGetSurfaceSample(TerrainNodeKey.Default(cell), out sample);
        }

        public bool TryGetSurfaceSample(
            in TerrainNodeKey nodeKey,
            out TerrainSurfaceSample sample)
        {
            sample = default;
            if (!EnsureNavigationData())
            {
                return false;
            }

            if (nodeKey.IsDefaultLayer)
            {
                return TryGetDefaultLayerSurfaceSample(nodeKey, out sample);
            }

            return TryGetLayerSurfaceSample(nodeKey, out sample);
        }

        private bool TryGetDefaultLayerSurfaceSample(
            in TerrainNodeKey nodeKey,
            out TerrainSurfaceSample sample)
        {
            sample = default;
            if (!TryGetTile(nodeKey.Cell, out TerrainNavigationTile tile))
            {
                return false;
            }

            (int x, int y) = CellToIndex(nodeKey.Cell);
            sample = CreateSurfaceSample(
                nodeKey,
                tile,
                m_cachedCostMap[y, x]);
            return true;
        }

        private bool TryGetLayerSurfaceSample(
            in TerrainNodeKey nodeKey,
            out TerrainSurfaceSample sample)
        {
            sample = default;
            if (!TryGetLayerTile(nodeKey, out TerrainNavigationTile tile))
            {
                return false;
            }

            sample = CreateSurfaceSample(
                nodeKey,
                tile,
                GetEffectiveTraversalCost(nodeKey, tile));
            return true;
        }

        private TerrainSurfaceSample CreateSurfaceSample(
            in TerrainNodeKey nodeKey,
            TerrainNavigationTile tile,
            float effectiveTraversalCost)
        {
            TerrainCellRuntimeStateSnapshot runtimeStateSnapshot;
            ETerrainSurfaceKind effectiveSurface;
            ETerrainSurfaceCoverKind baseSurfaceCover =
                ResolveBaseSurfaceCover(
                    nodeKey,
                    out ETerrainSurfaceCoverTraits coverTraits,
                    out TerrainSurfaceCoverSourceReference coverSource);
            ETerrainSurfaceCoverKind effectiveSurfaceCover;
            ETerrainSurfaceCoverLifecycle surfaceCoverLifecycle;
            if (m_runtimeSurfaceStates.TryGetValue(
                    nodeKey,
                    out TerrainCellRuntimeState runtimeState))
            {
                runtimeStateSnapshot = runtimeState.CreateSnapshot(
                    tile.SurfaceKind,
                    baseSurfaceCover);
                effectiveSurface = runtimeStateSnapshot.EffectiveSurface;
                effectiveSurfaceCover = runtimeStateSnapshot.EffectiveSurfaceCover;
                surfaceCoverLifecycle = runtimeStateSnapshot.SurfaceCoverLifecycle;
            }
            else
            {
                runtimeStateSnapshot = TerrainCellRuntimeStateSnapshot.Empty(
                    tile.SurfaceKind,
                    baseSurfaceCover);
                effectiveSurface = tile.SurfaceKind;
                effectiveSurfaceCover = baseSurfaceCover;
                surfaceCoverLifecycle = baseSurfaceCover == ETerrainSurfaceCoverKind.None
                    ? ETerrainSurfaceCoverLifecycle.None
                    : ETerrainSurfaceCoverLifecycle.Alive;
            }

            ETerrainSurfaceCoverTraits effectiveCoverTraits =
                effectiveSurfaceCover == baseSurfaceCover
                    ? coverTraits
                    : ETerrainSurfaceCoverTraits.None;

            return new TerrainSurfaceSample(
                nodeKey,
                tile.Elevation,
                tile.SurfaceKind,
                effectiveSurface,
                baseSurfaceCover,
                effectiveSurfaceCover,
                effectiveCoverTraits,
                coverSource,
                surfaceCoverLifecycle,
                tile.TraversalCost,
                effectiveTraversalCost,
                runtimeStateSnapshot);
        }

        public bool TryGetRuntimeState(
            Vector3Int cell,
            out TerrainCellRuntimeState runtimeState)
        {
            return TryGetRuntimeNodeState(
                TerrainNodeKey.Default(cell),
                out runtimeState);
        }

        public bool TryGetRuntimeNodeState(
            in TerrainNodeKey nodeKey,
            out TerrainCellRuntimeState runtimeState)
        {
            return m_runtimeSurfaceStates.TryGetValue(nodeKey, out runtimeState);
        }

        /// <summary>
        /// 设置地图实例上的临时地表状态，不修改共享的规则 Tile 资产。
        /// </summary>
        public bool SetRuntimeSurfaceState(Vector2 worldPosition, ETerrainRuntimeSurfaceState state)
        {
            if (!EnsureNavigationData())
            {
                return false;
            }

            Vector3Int cell = ActiveRuleTilemap.WorldToCell(worldPosition);
            if (!TryGetSurfaceSample(cell, out TerrainSurfaceSample previousSample))
            {
                return false;
            }

            TerrainNodeKey nodeKey = TerrainNodeKey.Default(cell);
            m_runtimeSurfaceStates.TryGetValue(
                nodeKey,
                out TerrainCellRuntimeState existingState);
            if (state == ETerrainRuntimeSurfaceState.None && existingState == null)
            {
                return true;
            }

            TerrainCellRuntimeState runtimeState = existingState;
            if (runtimeState == null &&
                !TryGetOrCreateRuntimeNodeState(nodeKey, out runtimeState))
            {
                return false;
            }

            if (!runtimeState.ReplaceCompatibilityFlags(state))
            {
                return true;
            }

            return CommitRuntimeNodeState(
                nodeKey,
                previousSample,
                1.0f,
                EElementPresentationSignal.None);
        }

        public void ClearRuntimeSurfaceStates()
        {
            if (m_runtimeSurfaceStates.Count == 0 &&
                m_runtimeTraversalCostMultipliers.Count == 0)
            {
                return;
            }

            m_runtimeSurfaceStates.Clear();
            m_runtimeTraversalCostMultipliers.Clear();
            RefreshNavigationData();
            RuntimeSurfaceStatesCleared?.Invoke();
        }

        /// <summary>
        /// 把世界元素范围转换为规则格，并沿合法同层/坡道连接展开。
        /// 视觉重叠不能绕过悬崖、阻挡或缺失规则格。
        /// </summary>
        public bool TryCollectAffectedCells(
            in ElementApplication application,
            List<Vector3Int> affectedCells)
        {
            if (affectedCells == null)
            {
                throw new ArgumentNullException(nameof(affectedCells));
            }

            affectedCells.Clear();
            m_areaNodeScratch.Clear();
            if (!TryCollectAffectedNodes(application, m_areaNodeScratch))
            {
                return false;
            }

            for (int i = 0; i < m_areaNodeScratch.Count; i++)
            {
                affectedCells.Add(m_areaNodeScratch[i].Cell);
            }

            return affectedCells.Count > 0;
        }

        public bool TryCollectAffectedNodes(
            in ElementApplication application,
            List<TerrainNodeKey> affectedNodes)
        {
            if (affectedNodes == null)
            {
                throw new ArgumentNullException(nameof(affectedNodes));
            }

            affectedNodes.Clear();
            if (!application.IsValid || !EnsureNavigationData())
            {
                return false;
            }

            Vector3Int originCell = ActiveRuleTilemap.WorldToCell(application.Origin);
            if (!TryGetTile(originCell, out TerrainNavigationTile originTile) ||
                !originTile.Walkable)
            {
                return false;
            }

            m_areaTraversalQueue.Clear();
            m_areaVisitedCells.Clear();
            m_areaTraversalQueue.Enqueue(originCell);
            m_areaVisitedCells.Add(originCell);

            while (m_areaTraversalQueue.Count > 0)
            {
                Vector3Int currentCell = m_areaTraversalQueue.Dequeue();
                if (IsInsideElementArea(currentCell, application))
                {
                    affectedNodes.Add(TerrainNodeKey.Default(currentCell));
                }

                if (application.Area.Kind == EElementAreaKind.Point)
                {
                    continue;
                }

                for (int i = 0; i < CardinalNeighborOffsets.Length; i++)
                {
                    Vector3Int neighborCell = currentCell + CardinalNeighborOffsets[i];
                    if (m_areaVisitedCells.Contains(neighborCell) ||
                        !IsInsideElementArea(neighborCell, application) ||
                        !CanTraverseCardinalCells(currentCell, neighborCell))
                    {
                        continue;
                    }

                    m_areaVisitedCells.Add(neighborCell);
                    m_areaTraversalQueue.Enqueue(neighborCell);
                }
            }

            return affectedNodes.Count > 0;
        }

        internal bool TryGetOrCreateRuntimeState(
            Vector3Int cell,
            out TerrainCellRuntimeState runtimeState)
        {
            return TryGetOrCreateRuntimeNodeState(
                TerrainNodeKey.Default(cell),
                out runtimeState);
        }

        internal bool TryGetOrCreateRuntimeNodeState(
            in TerrainNodeKey nodeKey,
            out TerrainCellRuntimeState runtimeState)
        {
            if (!EnsureNavigationData() ||
                !TryGetNodeTile(nodeKey, out _))
            {
                runtimeState = null;
                return false;
            }

            if (!m_runtimeSurfaceStates.TryGetValue(nodeKey, out runtimeState))
            {
                runtimeState = new TerrainCellRuntimeState();
                m_runtimeSurfaceStates.Add(nodeKey, runtimeState);
            }

            return true;
        }

        internal void CollectTimedRuntimeStateCells(List<Vector3Int> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            cells.Clear();
            foreach (KeyValuePair<TerrainNodeKey, TerrainCellRuntimeState> pair in m_runtimeSurfaceStates)
            {
                if (pair.Value != null && pair.Value.HasTimedStates)
                {
                    cells.Add(pair.Key.Cell);
                }
            }
        }

        internal void CollectTimedRuntimeStateNodes(List<TerrainNodeKey> nodes)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            nodes.Clear();
            foreach (KeyValuePair<TerrainNodeKey, TerrainCellRuntimeState> pair in m_runtimeSurfaceStates)
            {
                if (pair.Value != null && pair.Value.HasTimedStates)
                {
                    nodes.Add(pair.Key);
                }
            }
        }

        internal void CollectRuntimeStateCells(List<Vector3Int> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            cells.Clear();
            foreach (TerrainNodeKey nodeKey in m_runtimeSurfaceStates.Keys)
            {
                cells.Add(nodeKey.Cell);
            }
        }

        internal void CollectRuntimeStateNodes(List<TerrainNodeKey> nodes)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            nodes.Clear();
            foreach (TerrainNodeKey nodeKey in m_runtimeSurfaceStates.Keys)
            {
                nodes.Add(nodeKey);
            }
        }

        internal int NavigationGraphNodeCount => m_navigationGraph.NodeCount;
        internal int NavigationGraphEdgeCount => m_navigationGraph.EdgeCount;

        internal bool HasNavigationGraphNode(TerrainNodeKey nodeKey)
        {
            EnsureNavigationData();
            return m_navigationGraph.ContainsNode(nodeKey);
        }

        internal bool HasNavigationGraphEdge(TerrainNodeKey fromNode, TerrainNodeKey toNode)
        {
            EnsureNavigationData();
            return m_navigationGraph.HasEdge(fromNode, toNode);
        }

        internal bool TryBuildNodePath(
            TerrainNodeKey startNode,
            TerrainNodeKey goalNode,
            List<TerrainNodeKey> nodePath)
        {
            EnsureNavigationData();
            return m_navigationGraph.TryFindPath(startNode, goalNode, nodePath);
        }

        internal void CollectNavigationCandidates(
            Vector2 worldPosition,
            List<TerrainDestinationCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            candidates.Clear();
            m_destinationLayerScratch.Clear();
            if (TryResolveNavigationCandidateOnLayer(
                    worldPosition,
                    TerrainNodeKey.DefaultLayerId,
                    out TerrainDestinationCandidate defaultCandidate))
            {
                candidates.Add(defaultCandidate);
                m_destinationLayerScratch.Add(TerrainNodeKey.DefaultLayerId);
            }

            for (int i = 0; i < m_activeLayerSources.Count; i++)
            {
                TerrainNavigationLayerSource source = m_activeLayerSources[i];
                if (!m_destinationLayerScratch.Add(source.LayerId) ||
                    !TryResolveNavigationCandidateOnLayer(
                        worldPosition,
                        source.LayerId,
                        out TerrainDestinationCandidate candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            bool hasExactCandidate = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].WasSnapped)
                {
                    hasExactCandidate = true;
                    break;
                }
            }

            if (!hasExactCandidate)
            {
                return;
            }

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (candidates[i].WasSnapped)
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        internal bool TryResolveNavigationCandidateOnLayer(
            Vector2 worldPosition,
            int layerId,
            out TerrainDestinationCandidate candidate)
        {
            if (!TryGetLayerTilemap(layerId, out Tilemap tilemap))
            {
                candidate = default;
                return false;
            }

            Vector3Int requestedCell = tilemap.WorldToCell(worldPosition);
            TerrainNodeKey requestedNode = new(layerId, requestedCell);
            if (m_navigationGraph.ContainsNode(requestedNode))
            {
                candidate = new TerrainDestinationCandidate(
                    requestedNode,
                    worldPosition,
                    wasSnapped: false);
                return true;
            }

            bool found = false;
            float bestDistanceSquared = float.PositiveInfinity;
            Vector3Int bestCell = default;
            for (int radius = 1; radius <= m_nearestWalkableSearchRadius; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        {
                            continue;
                        }

                        Vector3Int cell = requestedCell + new Vector3Int(x, y);
                        TerrainNodeKey nodeKey = new(layerId, cell);
                        if (!m_navigationGraph.ContainsNode(nodeKey))
                        {
                            continue;
                        }

                        Vector2 center = tilemap.GetCellCenterWorld(cell);
                        float distanceSquared = (center - worldPosition).sqrMagnitude;
                        if (!found ||
                            distanceSquared < bestDistanceSquared - 0.0001f ||
                            Mathf.Approximately(distanceSquared, bestDistanceSquared) &&
                            IsCellBefore(cell, bestCell))
                        {
                            found = true;
                            bestDistanceSquared = distanceSquared;
                            bestCell = cell;
                        }
                    }
                }

                if (found)
                {
                    break;
                }
            }

            if (!found)
            {
                candidate = default;
                return false;
            }

            TerrainNodeKey resolvedNode = new(layerId, bestCell);
            candidate = new TerrainDestinationCandidate(
                resolvedNode,
                tilemap.GetCellCenterWorld(bestCell),
                wasSnapped: true);
            return true;
        }

        private static bool IsCellBefore(Vector3Int candidate, Vector3Int current)
        {
            return candidate.y < current.y ||
                candidate.y == current.y && candidate.x < current.x;
        }

        internal bool CommitRuntimeState(
            Vector3Int cell,
            in TerrainSurfaceSample previousSample,
            float traversalCostMultiplier,
            EElementPresentationSignal presentationSignal)
        {
            return CommitRuntimeNodeState(
                TerrainNodeKey.Default(cell),
                previousSample,
                traversalCostMultiplier,
                presentationSignal);
        }

        internal bool CommitRuntimeNodeState(
            in TerrainNodeKey nodeKey,
            in TerrainSurfaceSample previousSample,
            float traversalCostMultiplier,
            EElementPresentationSignal presentationSignal)
        {
            if (!EnsureNavigationData() ||
                !m_runtimeSurfaceStates.TryGetValue(
                    nodeKey,
                    out TerrainCellRuntimeState runtimeState))
            {
                return false;
            }

            if (runtimeState.IsEmpty)
            {
                m_runtimeSurfaceStates.Remove(nodeKey);
                m_runtimeTraversalCostMultipliers.Remove(nodeKey);
                UpdateCachedTraversalCost(nodeKey, 1.0f);
            }
            else
            {
                float normalizedMultiplier = Mathf.Max(0.01f, traversalCostMultiplier);
                m_runtimeTraversalCostMultipliers[nodeKey] = normalizedMultiplier;
                UpdateCachedTraversalCost(nodeKey, normalizedMultiplier);
            }

            if (!TryGetSurfaceSample(nodeKey, out TerrainSurfaceSample currentSample))
            {
                return false;
            }

            CellStateChanged?.Invoke(new TerrainCellStateChange(
                this,
                nodeKey,
                previousSample,
                currentSample,
                presentationSignal));
            return true;
        }

        private bool EnsureNavigationData()
        {
            if (m_cachedTiles == null || m_cachedCostMap == null)
            {
                RefreshNavigationData();
            }

            return ActiveRuleTilemap != null && m_cachedTiles != null && m_cachedCostMap != null;
        }

        private bool TryRefreshLayerSources(out Tilemap activeRuleTilemap)
        {
            activeRuleTilemap = null;
            m_activeLayerSources.Clear();
            m_layerIdScratch.Clear();
            m_layerNodeScratch.Clear();

            if (m_layerSources != null && m_layerSources.Length > 0)
            {
                for (int i = 0; i < m_layerSources.Length; i++)
                {
                    TerrainNavigationLayerSource source = m_layerSources[i];
                    if (source == null || !source.IsValid)
                    {
                        continue;
                    }

                    if (!m_layerIdScratch.Add(source.LayerId))
                    {
                        Debug.LogError(
                            $"地形导航组件 '{name}' 存在重复地形层 ID：{source.LayerId}。请确保每个规则层来源使用唯一 LayerId。",
                            this);
                        return false;
                    }

                    if (!TryRegisterLayerNodes(source))
                    {
                        return false;
                    }

                    m_activeLayerSources.Add(source);
                    if (source.LayerId == TerrainNodeKey.DefaultLayerId)
                    {
                        activeRuleTilemap = source.RuleTilemap;
                    }
                }
            }

            if (activeRuleTilemap != null)
            {
                return true;
            }

            activeRuleTilemap = m_ruleTilemap;
            return activeRuleTilemap != null;
        }

        private bool TryRefreshSurfaceLayerSources()
        {
            m_activeSurfaceLayerSources.Clear();
            m_surfaceSourceIdScratch.Clear();

            if (m_surfaceLayerSources == null || m_surfaceLayerSources.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < m_surfaceLayerSources.Length; i++)
            {
                TerrainSurfaceLayerSource source = m_surfaceLayerSources[i];
                if (source == null || !source.IsValid)
                {
                    continue;
                }

                if (source.SourceId < 0)
                {
                    Debug.LogError(
                        $"地形导航组件 '{name}' 的地表语义来源 ID 不能为负数：{source.SourceId}。",
                        this);
                    return false;
                }

                if (!m_surfaceSourceIdScratch.Add(source.SourceId))
                {
                    Debug.LogError(
                        $"地形导航组件 '{name}' 存在重复地表语义来源 ID：{source.SourceId}。请确保每个来源层使用唯一 SourceId。",
                        this);
                    return false;
                }

                m_activeSurfaceLayerSources.Add(source);
            }

            m_activeSurfaceLayerSources.Sort(
                (left, right) => left.Priority.CompareTo(right.Priority));
            return true;
        }

        private bool TryRegisterLayerNodes(TerrainNavigationLayerSource source)
        {
            BoundsInt bounds = source.RuleTilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (source.RuleTilemap.GetTile<TerrainNavigationTile>(cell) == null)
                {
                    continue;
                }

                TerrainNodeKey nodeKey = source.CreateNodeKey(cell);
                if (m_layerNodeScratch.Add(nodeKey))
                {
                    continue;
                }

                Debug.LogError(
                    $"地形导航组件 '{name}' 的规则层来源产生重复节点：{nodeKey}。同一 LayerId + Cell 只能由一个规则来源提供。",
                    this);
                return false;
            }

            return true;
        }

        private void ClearNavigationCache()
        {
            m_cachedBounds = default;
            m_cachedTiles = null;
            m_cachedCostMap = null;
            m_navigationGraph.Clear();
        }

        private void BuildNavigationGraph()
        {
            m_navigationGraph.Clear();
            if (DefaultLayerSource == null)
            {
                BuildLegacyDefaultLayerGraph();
            }

            for (int i = 0; i < m_activeLayerSources.Count; i++)
            {
                BuildLayerSourceGraph(m_activeLayerSources[i]);
            }

            RegisterTransitionLinks();
        }

        private void BuildLegacyDefaultLayerGraph()
        {
            for (int y = 0; y < m_cachedBounds.size.y; y++)
            {
                for (int x = 0; x < m_cachedBounds.size.x; x++)
                {
                    TerrainNavigationTile tile = m_cachedTiles[y, x];
                    if (tile == null || !tile.Walkable)
                    {
                        continue;
                    }

                    Vector3Int cell = IndexToCell(x, y);
                    TerrainNodeKey nodeKey = TerrainNodeKey.Default(cell);
                    m_navigationGraph.AddNode(nodeKey, tile, m_cachedCostMap[y, x]);
                }
            }

            for (int y = 0; y < m_cachedBounds.size.y; y++)
            {
                for (int x = 0; x < m_cachedBounds.size.x; x++)
                {
                    Vector3Int cell = IndexToCell(x, y);
                    TerrainNodeKey fromNode = TerrainNodeKey.Default(cell);
                    if (!m_navigationGraph.ContainsNode(fromNode))
                    {
                        continue;
                    }

                    AddDefaultLayerGraphEdges(fromNode, cell);
                }
            }
        }

        private void BuildLayerSourceGraph(TerrainNavigationLayerSource source)
        {
            BoundsInt bounds = source.RuleTilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                TerrainNavigationTile tile = source.RuleTilemap.GetTile<TerrainNavigationTile>(cell);
                if (tile == null || !tile.Walkable)
                {
                    continue;
                }

                TerrainNodeKey nodeKey = source.CreateNodeKey(cell);
                m_navigationGraph.AddNode(
                    nodeKey,
                    tile,
                    GetEffectiveTraversalCost(nodeKey, tile));
            }

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                TerrainNodeKey fromNode = source.CreateNodeKey(cell);
                if (!m_navigationGraph.ContainsNode(fromNode))
                {
                    continue;
                }

                AddLayerSourceGraphEdges(source, fromNode, cell);
            }
        }

        private void AddDefaultLayerGraphEdges(TerrainNodeKey fromNode, Vector3Int fromCell)
        {
            for (int i = 0; i < CardinalNeighborOffsets.Length; i++)
            {
                Vector3Int toCell = fromCell + CardinalNeighborOffsets[i];
                TerrainNodeKey toNode = TerrainNodeKey.Default(toCell);
                if (!m_navigationGraph.ContainsNode(toNode) ||
                    !CanTraverseCardinalCells(fromCell, toCell) ||
                    !TryGetTile(toCell, out TerrainNavigationTile toTile))
                {
                    continue;
                }

                m_navigationGraph.TryAddSameLayerEdge(
                    fromNode,
                    toNode,
                    GetEffectiveTraversalCost(toNode, toTile));
            }
        }

        private void AddLayerSourceGraphEdges(
            TerrainNavigationLayerSource source,
            TerrainNodeKey fromNode,
            Vector3Int fromCell)
        {
            TerrainNavigationTile fromTile = source.RuleTilemap.GetTile<TerrainNavigationTile>(fromCell);
            if (fromTile == null || !fromTile.Walkable)
            {
                return;
            }

            for (int i = 0; i < CardinalNeighborOffsets.Length; i++)
            {
                Vector3Int toCell = fromCell + CardinalNeighborOffsets[i];
                TerrainNavigationTile toTile = source.RuleTilemap.GetTile<TerrainNavigationTile>(toCell);
                TerrainNodeKey toNode = source.CreateNodeKey(toCell);
                if (toTile == null ||
                    !toTile.Walkable ||
                    !m_navigationGraph.ContainsNode(toNode) ||
                    !CanTraverseElevation(fromCell, fromTile, toCell, toTile))
                {
                    continue;
                }

                m_navigationGraph.TryAddSameLayerEdge(
                    fromNode,
                    toNode,
                    GetEffectiveTraversalCost(toNode, toTile));
            }
        }

        private void RegisterTransitionLinks()
        {
            if (m_transitionLinks == null)
            {
                return;
            }

            for (int i = 0; i < m_transitionLinks.Length; i++)
            {
                TerrainTransitionLink link = m_transitionLinks[i];
                if (link == null)
                {
                    continue;
                }

                if (!link.IsValid)
                {
                    Debug.LogError(
                        $"地形导航组件 '{name}' 存在无效跨层连接：第 {i} 项端点相同或类型为空。",
                        this);
                    continue;
                }

                if (!m_navigationGraph.ContainsNode(link.FromNode) ||
                    !m_navigationGraph.ContainsNode(link.ToNode))
                {
                    Debug.LogError(
                        $"地形导航组件 '{name}' 的跨层连接端点不存在：{link.FromNode} -> {link.ToNode}。",
                        this);
                    continue;
                }

                if (!m_navigationGraph.TryAddTransitionEdge(link))
                {
                    Debug.LogError(
                        $"地形导航组件 '{name}' 的跨层连接重复或被已有边占用：{link.FromNode} -> {link.ToNode}。",
                        this);
                }
            }
        }

        private bool TryResolveWalkableCell(Vector3Int requestedCell, out Vector3Int resolvedCell)
        {
            if (IsWalkable(requestedCell))
            {
                resolvedCell = requestedCell;
                return true;
            }

            for (int radius = 1; radius <= m_nearestWalkableSearchRadius; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        {
                            continue;
                        }

                        Vector3Int candidate = requestedCell + new Vector3Int(x, y);
                        if (IsWalkable(candidate))
                        {
                            resolvedCell = candidate;
                            return true;
                        }
                    }
                }
            }

            resolvedCell = default;
            return false;
        }

        /// <summary>
        /// 第三方 A* 在 walkableDiagonals=false 时仍会返回“至少一侧正交格可走”的斜邻步。
        /// 连续移动角色有碰撞体积，直接连斜线会擦进另一侧悬崖角，因此这里把斜步展开为两个合法正交步。
        /// </summary>
        private (int x, int y)[] ExpandDiagonalSteps((int x, int y)[] gridPath)
        {
            if (gridPath == null || gridPath.Length == 0)
            {
                return Array.Empty<(int x, int y)>();
            }

            List<(int x, int y)> expandedPath = new() { gridPath[0] };
            for (int i = 1; i < gridPath.Length; i++)
            {
                (int x, int y) previous = expandedPath[^1];
                (int x, int y) current = gridPath[i];
                int deltaX = current.x - previous.x;
                int deltaY = current.y - previous.y;
                if (Mathf.Abs(deltaX) > 1 || Mathf.Abs(deltaY) > 1)
                {
                    return Array.Empty<(int x, int y)>();
                }

                if (deltaX != 0 && deltaY != 0)
                {
                    (int x, int y) horizontalBridge = (current.x, previous.y);
                    (int x, int y) verticalBridge = (previous.x, current.y);
                    bool canUseHorizontalBridge =
                        IsLegalCardinalStep(previous, horizontalBridge) &&
                        IsLegalCardinalStep(horizontalBridge, current);
                    bool canUseVerticalBridge =
                        IsLegalCardinalStep(previous, verticalBridge) &&
                        IsLegalCardinalStep(verticalBridge, current);

                    if (!canUseHorizontalBridge && !canUseVerticalBridge)
                    {
                        return Array.Empty<(int x, int y)>();
                    }

                    (int x, int y) bridge = canUseHorizontalBridge && canUseVerticalBridge
                        ? GetTraversalCost(horizontalBridge) <= GetTraversalCost(verticalBridge)
                            ? horizontalBridge
                            : verticalBridge
                        : canUseHorizontalBridge
                            ? horizontalBridge
                            : verticalBridge;
                    expandedPath.Add(bridge);
                }

                expandedPath.Add(current);
            }

            return expandedPath.ToArray();
        }

        private bool ValidateElevationTransitions((int x, int y)[] gridPath)
        {
            for (int i = 1; i < gridPath.Length; i++)
            {
                Vector3Int previousCell = IndexToCell(gridPath[i - 1].x, gridPath[i - 1].y);
                Vector3Int currentCell = IndexToCell(gridPath[i].x, gridPath[i].y);
                if (!TryGetTile(previousCell, out TerrainNavigationTile previousTile) ||
                    !TryGetTile(currentCell, out TerrainNavigationTile currentTile))
                {
                    return false;
                }

                if (!CanTraverseElevation(previousCell, previousTile, currentCell, currentTile))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLegalCardinalStep((int x, int y) from, (int x, int y) to)
        {
            if (Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y) != 1)
            {
                return false;
            }

            Vector3Int fromCell = IndexToCell(from.x, from.y);
            Vector3Int toCell = IndexToCell(to.x, to.y);
            return CanTraverseCardinalCells(fromCell, toCell);
        }

        private float GetTraversalCost((int x, int y) index)
        {
            return m_cachedCostMap[index.y, index.x];
        }

        private bool CanTraverseCardinalCells(Vector3Int fromCell, Vector3Int toCell)
        {
            if (Mathf.Abs(toCell.x - fromCell.x) + Mathf.Abs(toCell.y - fromCell.y) != 1)
            {
                return false;
            }

            return TryGetTile(fromCell, out TerrainNavigationTile fromTile) &&
                TryGetTile(toCell, out TerrainNavigationTile toTile) &&
                fromTile.Walkable &&
                toTile.Walkable &&
                CanTraverseElevation(fromCell, fromTile, toCell, toTile);
        }

        private bool IsInsideElementArea(
            Vector3Int cell,
            in ElementApplication application)
        {
            if (application.Area.Kind == EElementAreaKind.Point)
            {
                return cell == ActiveRuleTilemap.WorldToCell(application.Origin);
            }

            Vector2 cellCenter = ActiveRuleTilemap.GetCellCenterWorld(cell);
            Vector2 offset = cellCenter - application.Origin;
            float radius = application.Area.Radius;
            if (offset.sqrMagnitude > radius * radius + 0.0001f)
            {
                return false;
            }

            if (application.Area.Kind == EElementAreaKind.Circle ||
                offset.sqrMagnitude <= 0.000001f)
            {
                return true;
            }

            float minimumDot = Mathf.Cos(
                application.Area.ConeHalfAngleDegrees * Mathf.Deg2Rad);
            return Vector2.Dot(offset.normalized, application.Direction) >= minimumDot;
        }

        private void UpdateCachedTraversalCost(in TerrainNodeKey nodeKey, float stateMultiplier)
        {
            if (!nodeKey.IsDefaultLayer)
            {
                return;
            }

            UpdateCachedDefaultTraversalCost(nodeKey.Cell, stateMultiplier);
        }

        private void UpdateCachedDefaultTraversalCost(Vector3Int cell, float stateMultiplier)
        {
            if (!TryGetTile(cell, out TerrainNavigationTile tile) ||
                !TryCellToIndex(cell, out int x, out int y))
            {
                return;
            }

            m_cachedCostMap[y, x] = tile.Walkable
                ? tile.TraversalCost * Mathf.Max(0.01f, stateMultiplier)
                : -1.0f;
        }

        private bool TryGetNodeTile(
            in TerrainNodeKey nodeKey,
            out TerrainNavigationTile tile)
        {
            return nodeKey.IsDefaultLayer
                ? TryGetTile(nodeKey.Cell, out tile)
                : TryGetLayerTile(nodeKey, out tile);
        }

        private bool TryGetLayerTile(
            in TerrainNodeKey nodeKey,
            out TerrainNavigationTile tile)
        {
            tile = null;
            TerrainNavigationLayerSource source = GetLayerSource(nodeKey.LayerId);
            if (source == null)
            {
                return false;
            }

            tile = source.RuleTilemap.GetTile<TerrainNavigationTile>(nodeKey.Cell);
            return tile != null;
        }

        private TerrainNavigationLayerSource GetLayerSource(int layerId)
        {
            for (int i = 0; i < m_activeLayerSources.Count; i++)
            {
                TerrainNavigationLayerSource source = m_activeLayerSources[i];
                if (source.LayerId == layerId)
                {
                    return source;
                }
            }

            return null;
        }

        private bool TryGetLayerTilemap(int layerId, out Tilemap tilemap)
        {
            TerrainNavigationLayerSource source = GetLayerSource(layerId);
            if (source != null && source.IsValid)
            {
                tilemap = source.RuleTilemap;
                return true;
            }

            if (layerId == TerrainNodeKey.DefaultLayerId && ActiveRuleTilemap != null)
            {
                tilemap = ActiveRuleTilemap;
                return true;
            }

            tilemap = null;
            return false;
        }

        public bool TryGetSurfaceCoverTilemap(
            in TerrainSurfaceCoverSourceReference sourceReference,
            out Tilemap tilemap)
        {
            if (!sourceReference.IsValid)
            {
                tilemap = null;
                return false;
            }

            for (int i = 0; i < m_activeSurfaceLayerSources.Count; i++)
            {
                TerrainSurfaceLayerSource source = m_activeSurfaceLayerSources[i];
                if (source.SourceId == sourceReference.SourceId &&
                    source.IsValid)
                {
                    tilemap = source.Tilemap;
                    return tilemap != null;
                }
            }

            if (sourceReference.SourceId ==
                TerrainSurfaceCoverSourceReference.LegacySurfaceCoverSourceId)
            {
                tilemap = m_surfaceCoverTilemap;
                return tilemap != null;
            }

            tilemap = null;
            return false;
        }

        private bool TryGetNodeWorldCenter(
            in TerrainNodeKey nodeKey,
            out Vector2 worldCenter)
        {
            if (!TryGetLayerTilemap(nodeKey.LayerId, out Tilemap tilemap))
            {
                worldCenter = default;
                return false;
            }

            worldCenter = tilemap.GetCellCenterWorld(nodeKey.Cell);
            return true;
        }

        private float GetEffectiveTraversalCost(
            in TerrainNodeKey nodeKey,
            TerrainNavigationTile tile)
        {
            float multiplier = m_runtimeTraversalCostMultipliers.TryGetValue(
                nodeKey,
                out float runtimeMultiplier)
                ? runtimeMultiplier
                : 1.0f;
            return tile.Walkable
                ? tile.TraversalCost * Mathf.Max(0.01f, multiplier)
                : -1.0f;
        }

        private ETerrainSurfaceCoverKind ResolveBaseSurfaceCover(
            in TerrainNodeKey nodeKey,
            out ETerrainSurfaceCoverTraits traits,
            out TerrainSurfaceCoverSourceReference sourceReference)
        {
            traits = ETerrainSurfaceCoverTraits.None;
            sourceReference = TerrainSurfaceCoverSourceReference.None;
            for (int i = 0; i < m_activeSurfaceLayerSources.Count; i++)
            {
                TerrainSurfaceLayerSource source = m_activeSurfaceLayerSources[i];
                if (!source.TryResolveSurfaceCover(
                        nodeKey.Cell,
                        out ETerrainSurfaceCoverKind coverKind,
                        out ETerrainSurfaceCoverTraits sourceTraits))
                {
                    continue;
                }

                traits = sourceTraits;
                sourceReference = new TerrainSurfaceCoverSourceReference(
                    source.SourceId,
                    source.Role);
                return coverKind;
            }

            if (m_activeSurfaceLayerSources.Count == 0)
            {
                return ResolveLegacyBaseSurfaceCover(
                    nodeKey,
                    out traits,
                    out sourceReference);
            }

            return ETerrainSurfaceCoverKind.None;
        }

        private ETerrainSurfaceCoverKind ResolveLegacyBaseSurfaceCover(
            in TerrainNodeKey nodeKey,
            out ETerrainSurfaceCoverTraits traits,
            out TerrainSurfaceCoverSourceReference sourceReference)
        {
            traits = ETerrainSurfaceCoverTraits.None;
            sourceReference = TerrainSurfaceCoverSourceReference.None;
            if (m_surfaceCoverTilemap == null)
            {
                return ETerrainSurfaceCoverKind.None;
            }

            TileBase coverTile = m_surfaceCoverTilemap.GetTile(nodeKey.Cell);
            if (coverTile == null)
            {
                return ETerrainSurfaceCoverKind.None;
            }

            if (m_surfaceCoverTileMappings == null)
            {
                return ETerrainSurfaceCoverKind.None;
            }

            for (int i = 0; i < m_surfaceCoverTileMappings.Length; i++)
            {
                TerrainSurfaceCoverTileMapping mapping = m_surfaceCoverTileMappings[i];
                if (mapping == null ||
                    !mapping.IsValid ||
                    mapping.Tile != coverTile)
                {
                    continue;
                }

                traits = mapping.Traits;
                sourceReference = TerrainSurfaceCoverSourceReference.LegacySurfaceCover;
                return mapping.CoverKind;
            }

            return ETerrainSurfaceCoverKind.None;
        }

        private static bool CanTraverseElevation(
            Vector3Int previousCell,
            TerrainNavigationTile previousTile,
            Vector3Int currentCell,
            TerrainNavigationTile currentTile)
        {
            int elevationDifference = Mathf.Abs(previousTile.Elevation - currentTile.Elevation);
            if (elevationDifference == 0)
            {
                return true;
            }

            if (elevationDifference > 1 ||
                !TryResolveRampDirection(previousTile, currentTile, out ETerrainRampDirection rampDirection))
            {
                return false;
            }

            Vector3Int lowCell = previousTile.Elevation < currentTile.Elevation
                ? previousCell
                : currentCell;
            Vector3Int highCell = previousTile.Elevation < currentTile.Elevation
                ? currentCell
                : previousCell;
            Vector2Int lowToHighStep = new(
                highCell.x - lowCell.x,
                highCell.y - lowCell.y);
            return IsStepTowardRampDirection(lowToHighStep, rampDirection);
        }

        private static bool TryResolveRampDirection(
            TerrainNavigationTile firstTile,
            TerrainNavigationTile secondTile,
            out ETerrainRampDirection rampDirection)
        {
            rampDirection = ETerrainRampDirection.None;
            if (!TryMergeRampDirection(firstTile, ref rampDirection) ||
                !TryMergeRampDirection(secondTile, ref rampDirection))
            {
                return false;
            }

            return rampDirection != ETerrainRampDirection.None;
        }

        private static bool TryMergeRampDirection(
            TerrainNavigationTile tile,
            ref ETerrainRampDirection rampDirection)
        {
            if (tile.TransitionKind != ETerrainTransitionKind.Ramp)
            {
                return true;
            }

            if (tile.RampDirection == ETerrainRampDirection.None ||
                rampDirection != ETerrainRampDirection.None &&
                rampDirection != tile.RampDirection)
            {
                return false;
            }

            rampDirection = tile.RampDirection;
            return true;
        }

        private static bool IsStepTowardRampDirection(
            Vector2Int lowToHighStep,
            ETerrainRampDirection rampDirection)
        {
            if (Mathf.Abs(lowToHighStep.x) + Mathf.Abs(lowToHighStep.y) != 1)
            {
                return false;
            }

            return rampDirection switch
            {
                ETerrainRampDirection.NorthEast =>
                    lowToHighStep == Vector2Int.up || lowToHighStep == Vector2Int.right,
                ETerrainRampDirection.NorthWest =>
                    lowToHighStep == Vector2Int.up || lowToHighStep == Vector2Int.left,
                ETerrainRampDirection.SouthEast =>
                    lowToHighStep == Vector2Int.down || lowToHighStep == Vector2Int.right,
                ETerrainRampDirection.SouthWest =>
                    lowToHighStep == Vector2Int.down || lowToHighStep == Vector2Int.left,
                _ => false
            };
        }

        private Vector2[] ConvertToWorldPath(
            (int x, int y)[] gridPath,
            bool useRequestedDestination,
            Vector2 requestedDestination)
        {
            if (gridPath.Length == 1)
            {
                Vector3Int onlyCell = IndexToCell(gridPath[0].x, gridPath[0].y);
                return new[]
                {
                    useRequestedDestination
                        ? requestedDestination
                        : (Vector2)ActiveRuleTilemap.GetCellCenterWorld(onlyCell)
                };
            }

            List<Vector2> sampledPoints = new();
            List<bool> preservedPoints = new();
            AppendWorldPoint(
                sampledPoints,
                preservedPoints,
                ActiveRuleTilemap.GetCellCenterWorld(IndexToCell(gridPath[0].x, gridPath[0].y)),
                preserve: false);

            int pathIndex = 1;
            while (pathIndex < gridPath.Length)
            {
                if (TryAppendRampCenterLine(
                    gridPath,
                    pathIndex,
                    sampledPoints,
                    preservedPoints,
                    out int nextPathIndex))
                {
                    pathIndex = nextPathIndex;
                    continue;
                }

                Vector3Int cell = IndexToCell(gridPath[pathIndex].x, gridPath[pathIndex].y);
                AppendWorldPoint(
                    sampledPoints,
                    preservedPoints,
                    ActiveRuleTilemap.GetCellCenterWorld(cell),
                    preserve: false);
                pathIndex++;
            }

            if (useRequestedDestination)
            {
                sampledPoints[^1] = requestedDestination;
                preservedPoints[^1] = true;
            }

            List<Vector2> compressedPoints = CompressWorldPoints(sampledPoints, preservedPoints);
            compressedPoints.RemoveAt(0);
            return compressedPoints.ToArray();
        }

        private Vector2[] ConvertNodePathToWorldPath(
            IReadOnlyList<TerrainNodeKey> nodePath,
            in TerrainDestinationCandidate destination)
        {
            if (nodePath == null || nodePath.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            bool isDefaultLayerPath = true;
            for (int i = 0; i < nodePath.Count; i++)
            {
                if (!nodePath[i].IsDefaultLayer)
                {
                    isDefaultLayerPath = false;
                    break;
                }
            }

            if (isDefaultLayerPath)
            {
                (int x, int y)[] gridPath = new (int x, int y)[nodePath.Count];
                for (int i = 0; i < nodePath.Count; i++)
                {
                    gridPath[i] = CellToIndex(nodePath[i].Cell);
                }

                return ConvertToWorldPath(
                    gridPath,
                    useRequestedDestination: !destination.WasSnapped,
                    destination.WorldPosition);
            }

            if (nodePath.Count == 1)
            {
                return new[] { destination.WorldPosition };
            }

            if (!TryGetNodeWorldCenter(nodePath[0], out Vector2 startCenter))
            {
                return Array.Empty<Vector2>();
            }

            List<Vector2> sampledPoints = new() { startCenter };
            List<bool> preservedPoints = new() { false };
            for (int i = 1; i < nodePath.Count; i++)
            {
                TerrainNodeKey fromNode = nodePath[i - 1];
                TerrainNodeKey toNode = nodePath[i];
                if (!m_navigationGraph.TryGetEdge(fromNode, toNode, out TerrainNavigationGraphEdge edge) ||
                    !TryGetNodeWorldCenter(toNode, out Vector2 toCenter))
                {
                    return Array.Empty<Vector2>();
                }

                TerrainTransitionLink transition = edge.TransitionLink;
                if (transition != null && transition.WorldWaypoints.Count > 0)
                {
                    bool forward = fromNode == transition.FromNode;
                    for (int waypointIndex = 0;
                         waypointIndex < transition.WorldWaypoints.Count;
                         waypointIndex++)
                    {
                        int sourceIndex = forward
                            ? waypointIndex
                            : transition.WorldWaypoints.Count - 1 - waypointIndex;
                        AppendWorldPoint(
                            sampledPoints,
                            preservedPoints,
                            transition.WorldWaypoints[sourceIndex],
                            preserve: true);
                    }
                }

                AppendWorldPoint(
                    sampledPoints,
                    preservedPoints,
                    toCenter,
                    preserve: edge.IsTransition);
            }

            if (!destination.WasSnapped)
            {
                sampledPoints[^1] = destination.WorldPosition;
                preservedPoints[^1] = true;
            }

            List<Vector2> compressedPoints = CompressWorldPoints(
                sampledPoints,
                preservedPoints);
            compressedPoints.RemoveAt(0);
            return compressedPoints.ToArray();
        }

        /// <summary>
        /// Godot 楼梯示例通过逐帧修正速度让角色沿斜线移动。
        /// 本项目把同一思想上移到路径层：坡道格仍供 A* 选路，但所有单位最终共享作者指定的中心线。
        /// </summary>
        private bool TryAppendRampCenterLine(
            (int x, int y)[] gridPath,
            int startIndex,
            List<Vector2> sampledPoints,
            List<bool> preservedPoints,
            out int nextPathIndex)
        {
            nextPathIndex = startIndex + 1;
            Vector3Int startCell = IndexToCell(gridPath[startIndex].x, gridPath[startIndex].y);
            if (!TryGetTile(startCell, out TerrainNavigationTile startTile) ||
                startTile.TransitionKind != ETerrainTransitionKind.Ramp ||
                startTile.RampDirection == ETerrainRampDirection.None)
            {
                return false;
            }

            int endIndex = startIndex;
            int minimumElevation = startTile.Elevation;
            int maximumElevation = startTile.Elevation;
            while (endIndex + 1 < gridPath.Length)
            {
                Vector3Int candidateCell = IndexToCell(
                    gridPath[endIndex + 1].x,
                    gridPath[endIndex + 1].y);
                if (!TryGetTile(candidateCell, out TerrainNavigationTile candidateTile) ||
                    candidateTile.TransitionKind != ETerrainTransitionKind.Ramp ||
                    candidateTile.RampDirection != startTile.RampDirection)
                {
                    break;
                }

                endIndex++;
                minimumElevation = Mathf.Min(minimumElevation, candidateTile.Elevation);
                maximumElevation = Mathf.Max(maximumElevation, candidateTile.Elevation);
            }

            if (endIndex == startIndex || minimumElevation == maximumElevation)
            {
                return false;
            }

            Vector3Int endCell = IndexToCell(gridPath[endIndex].x, gridPath[endIndex].y);
            Vector2Int runDelta = new(endCell.x - startCell.x, endCell.y - startCell.y);
            Vector2Int authoredDirection = GetRampDiagonalDirection(startTile.RampDirection);
            if (!IsRampRunAligned(runDelta, authoredDirection))
            {
                return false;
            }

            for (int i = startIndex; i <= endIndex; i++)
            {
                Vector3Int rampCell = IndexToCell(gridPath[i].x, gridPath[i].y);
                AppendWorldPoint(
                    sampledPoints,
                    preservedPoints,
                    ActiveRuleTilemap.GetCellCenterWorld(rampCell),
                    preserve: true);
            }

            nextPathIndex = endIndex + 1;
            return true;
        }

        private static bool IsRampRunAligned(Vector2Int runDelta, Vector2Int authoredDirection)
        {
            if (runDelta.x == 0 || runDelta.y == 0 ||
                Mathf.Abs(runDelta.x) != Mathf.Abs(runDelta.y))
            {
                return false;
            }

            Vector2Int runDirection = new(Math.Sign(runDelta.x), Math.Sign(runDelta.y));
            return runDirection == authoredDirection || runDirection == -authoredDirection;
        }

        private static Vector2Int GetRampDiagonalDirection(ETerrainRampDirection rampDirection)
        {
            return rampDirection switch
            {
                ETerrainRampDirection.NorthEast => new Vector2Int(1, 1),
                ETerrainRampDirection.NorthWest => new Vector2Int(-1, 1),
                ETerrainRampDirection.SouthEast => new Vector2Int(1, -1),
                ETerrainRampDirection.SouthWest => new Vector2Int(-1, -1),
                _ => Vector2Int.zero
            };
        }

        private static void AppendWorldPoint(
            List<Vector2> points,
            List<bool> preservedPoints,
            Vector2 point,
            bool preserve)
        {
            if (points.Count > 0 && (points[^1] - point).sqrMagnitude <= 0.000001f)
            {
                preservedPoints[^1] |= preserve;
                return;
            }

            points.Add(point);
            preservedPoints.Add(preserve);
        }

        private static List<Vector2> CompressWorldPoints(
            IReadOnlyList<Vector2> sampledPoints,
            IReadOnlyList<bool> preservedPoints)
        {
            List<Vector2> compressedPoints = new(sampledPoints.Count) { sampledPoints[0] };
            for (int i = 1; i < sampledPoints.Count - 1; i++)
            {
                if (preservedPoints[i] ||
                    !AreCollinearInSameDirection(
                        compressedPoints[^1],
                        sampledPoints[i],
                        sampledPoints[i + 1]))
                {
                    compressedPoints.Add(sampledPoints[i]);
                }
            }

            compressedPoints.Add(sampledPoints[^1]);
            return compressedPoints;
        }

        private static bool AreCollinearInSameDirection(
            Vector2 previous,
            Vector2 current,
            Vector2 next)
        {
            Vector2 firstSegment = current - previous;
            Vector2 secondSegment = next - current;
            float cross = firstSegment.x * secondSegment.y - firstSegment.y * secondSegment.x;
            return Mathf.Abs(cross) <= 0.0001f &&
                Vector2.Dot(firstSegment, secondSegment) > 0.0f;
        }

        private bool IsWalkable(Vector3Int cell)
        {
            return TryGetTile(cell, out TerrainNavigationTile tile) && tile.Walkable;
        }

        private bool TryGetTile(Vector3Int cell, out TerrainNavigationTile tile)
        {
            tile = null;
            if (!TryCellToIndex(cell, out int x, out int y))
            {
                return false;
            }

            tile = m_cachedTiles[y, x];
            return tile != null;
        }

        private bool TryCellToIndex(Vector3Int cell, out int x, out int y)
        {
            x = cell.x - m_cachedBounds.xMin;
            y = cell.y - m_cachedBounds.yMin;
            return x >= 0 && y >= 0 &&
                x < m_cachedBounds.size.x &&
                y < m_cachedBounds.size.y;
        }

        private (int x, int y) CellToIndex(Vector3Int cell)
        {
            return (cell.x - m_cachedBounds.xMin, cell.y - m_cachedBounds.yMin);
        }

        private Vector3Int IndexToCell(int x, int y)
        {
            return new Vector3Int(m_cachedBounds.xMin + x, m_cachedBounds.yMin + y);
        }

        private void OnDrawGizmos()
        {
            if (!m_drawLastNavigationPath || !m_hasDebugPathRequest)
            {
                return;
            }

            Tilemap activeRuleTilemap = ActiveRuleTilemap;
            float z = (activeRuleTilemap != null ? activeRuleTilemap.transform.position.z : transform.position.z) - 0.05f;
            Vector3 start = ToDebugPosition(m_lastDebugStart, z);
            Vector3 click = ToDebugPosition(m_lastDebugDestination, z);
            Vector3 finalDestination = ToDebugPosition(m_lastDebugFinalDestination, z);
            Vector3 resolvedCell = ToDebugPosition(m_lastDebugResolvedCellCenter, z);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(start, m_debugMarkerRadius);
            Gizmos.color = m_debugFailureColor;
            Gizmos.DrawWireSphere(click, m_debugMarkerRadius * 0.78f);

            if (!m_lastDebugPathSucceeded)
            {
                Gizmos.color = m_debugFailureColor;
                Gizmos.DrawWireSphere(click, m_debugMarkerRadius);
                DrawFailureCross(click);
                return;
            }

            Gizmos.color = m_debugResolvedGoalColor;
            Gizmos.DrawWireSphere(resolvedCell, m_debugMarkerRadius * 0.55f);
            if ((m_lastDebugResolvedCellCenter - m_lastDebugDestination).sqrMagnitude > 0.0001f)
            {
                Gizmos.DrawLine(click, resolvedCell);
            }

            Gizmos.color = m_debugPathColor;
            Gizmos.DrawWireSphere(finalDestination, m_debugMarkerRadius);
            if ((m_lastDebugFinalDestination - m_lastDebugResolvedCellCenter).sqrMagnitude > 0.0001f)
            {
                Gizmos.DrawLine(resolvedCell, finalDestination);
            }

            Vector3 previous = start;
            for (int i = 0; i < m_lastDebugWorldPath.Length; i++)
            {
                Vector3 waypoint = ToDebugPosition(m_lastDebugWorldPath[i], z);
                Gizmos.DrawLine(previous, waypoint);
                Gizmos.DrawWireSphere(waypoint, m_debugMarkerRadius * 0.65f);
                previous = waypoint;
            }

            Gizmos.DrawSphere(previous, m_debugMarkerRadius * 0.45f);
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                start + Vector3.up * m_debugMarkerRadius * 1.5f,
                $"Start\n{m_lastDebugPathStatus}");
            UnityEditor.Handles.color = m_debugFailureColor;
            UnityEditor.Handles.Label(
                click + Vector3.down * m_debugMarkerRadius * 1.5f,
                "Click");
            UnityEditor.Handles.color = m_debugResolvedGoalColor;
            UnityEditor.Handles.Label(
                resolvedCell + Vector3.down * m_debugMarkerRadius * 2.7f,
                "Cell");
            UnityEditor.Handles.color = m_debugPathColor;
            UnityEditor.Handles.Label(
                finalDestination + Vector3.up * m_debugMarkerRadius * 1.5f,
                "End");
#endif
        }

        private void SyncRuntimeNavigationDebugPath()
        {
            if (m_suppressNavigationDebug)
            {
                return;
            }

            if (!Application.isPlaying ||
                !m_showRuntimeNavigationPath ||
                !m_hasDebugPathRequest)
            {
                ClearRuntimeNavigationDebugPath();
                return;
            }

            TerrainNavigationRuntimePathDebugSnapshot snapshot = new(
                transform,
                name,
                GetRuntimeDebugZ(),
                m_showRuntimeNavigationDebugDetails,
                m_debugMarkerRadius,
                m_runtimePathLineWidth,
                m_runtimeWaypointRadius,
                m_runtimeMarkerSegments,
                m_debugPathColor,
                m_debugFailureColor,
                m_debugResolvedGoalColor,
                m_lastDebugStart,
                m_lastDebugDestination,
                m_lastDebugFinalDestination,
                m_lastDebugResolvedCellCenter,
                m_lastDebugWorldPath,
                m_lastDebugPathSucceeded,
                m_lastDebugPathStatus);
            m_runtimePathDebugView.Sync(snapshot);
        }

        private void ClearRuntimeNavigationDebugPath()
        {
            m_runtimePathDebugView.Clear(
                m_debugPathColor,
                m_debugFailureColor,
                m_debugResolvedGoalColor,
                m_runtimePathLineWidth);
        }

        private float GetRuntimeDebugZ()
        {
            Tilemap activeRuleTilemap = ActiveRuleTilemap;
            return (activeRuleTilemap != null ? activeRuleTilemap.transform.position.z : transform.position.z) - 0.08f;
        }

        private void DrawFailureCross(Vector3 destination)
        {
            float radius = m_debugMarkerRadius * 0.75f;
            Gizmos.DrawLine(
                destination + new Vector3(-radius, -radius, 0.0f),
                destination + new Vector3(radius, radius, 0.0f));
            Gizmos.DrawLine(
                destination + new Vector3(-radius, radius, 0.0f),
                destination + new Vector3(radius, -radius, 0.0f));
        }

        private static Vector3 ToDebugPosition(Vector2 position, float z)
        {
            return new Vector3(position.x, position.y, z);
        }
    }
}
