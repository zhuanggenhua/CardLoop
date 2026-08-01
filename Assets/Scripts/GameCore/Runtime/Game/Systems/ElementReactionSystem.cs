using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    public sealed class ElementReactionSystem : AGameSystem
    {
        private readonly struct StateDefinitionBinding
        {
            public StateDefinitionBinding(
                string stableId,
                TerrainElementStateDefinition definition)
            {
                StableId = stableId;
                Definition = definition;
            }

            public string StableId { get; }
            public TerrainElementStateDefinition Definition { get; }
        }

        [Header("模拟")]
        [Min(0.01f)]
        [SerializeField] private float m_fixedStepSeconds = 0.1f;

        private readonly Dictionary<ETerrainElementStateKind, StateDefinitionBinding>
            m_stateDefinitions = new();
        private readonly HashSet<string> m_reactionStableIds =
            new(StringComparer.Ordinal);
        private readonly List<ElementReactionCandidate> m_reactionCandidates = new();
        private readonly HashSet<TerrainNodeKey> m_activeTimedNodes = new();
        private readonly List<TerrainNodeKey> m_affectedNodes = new();
        private readonly List<TerrainNodeKey> m_activeNodeSnapshot = new();
        private readonly List<ETerrainElementStateKind> m_expiredStates = new();
        private readonly List<ElementReactionCandidate> m_matchingReactions = new();

        private TerrainNavigationMap m_navigationMap;
        private float m_accumulatedTime;
        private bool m_initialized;
        private bool m_missingMapReported;

        public bool IsInitialized => m_initialized;
        public TerrainNavigationMap BoundNavigationMap => m_navigationMap;
        public int ActiveTimedCellCount => m_activeTimedNodes.Count;

        public override void OnSystemInit()
        {
            m_initialized = TryBuildRuleIndexes();
        }

        public override void OnSystemStart()
        {
            if (m_initialized)
            {
                TryBindActiveMap(reportFailure: false);
            }
        }

        public override void OnSystemStop()
        {
            UnbindMap(clearTransientState: false);
            m_accumulatedTime = 0.0f;
        }

        public override void OnMapLoaded()
        {
            if (m_initialized)
            {
                TryBindActiveMap(reportFailure: true);
            }
        }

        public override void OnMapUnloading()
        {
            UnbindMap(clearTransientState: true);
            m_accumulatedTime = 0.0f;
        }

        public bool Apply(in ElementApplication application)
        {
            if (!m_initialized)
            {
                Debug.LogError(
                    "元素反应系统尚未通过规则与状态定义校验，无法施加世界元素。",
                    this);
                return false;
            }

            if (!application.IsValid)
            {
                Debug.LogError("收到无效的世界元素施加数据。", this);
                return false;
            }

            if (m_navigationMap == null &&
                !TryBindActiveMap(reportFailure: true))
            {
                return false;
            }

            if (!m_navigationMap.TryCollectAffectedNodes(application, m_affectedNodes))
            {
                return false;
            }

            bool anyNodeChanged = false;
            for (int i = 0; i < m_affectedNodes.Count; i++)
            {
                anyNodeChanged |= ApplyToNode(m_affectedNodes[i], application);
            }

            return anyNodeChanged;
        }

        private void Update()
        {
            if (!m_initialized ||
                m_navigationMap == null ||
                Time.timeScale <= 0.0f)
            {
                return;
            }

            float fixedStep = Mathf.Max(0.01f, m_fixedStepSeconds);
            m_accumulatedTime += Time.deltaTime;
            while (m_accumulatedTime >= fixedStep)
            {
                m_accumulatedTime -= fixedStep;
                AdvanceTimedStates(fixedStep);
            }
        }

        private bool TryBuildRuleIndexes()
        {
            m_stateDefinitions.Clear();
            m_reactionStableIds.Clear();
            m_reactionCandidates.Clear();

            if (!GameManager.Exists() || GameManager.Database == null)
            {
                Debug.LogError(
                    "元素反应系统缺少 GameManager.Database，无法建立元素规则索引。",
                    this);
                return false;
            }

            KeyValuePair<string, DatabaseEntry>[] entries =
                GameManager.Database.GetEntries();
            bool valid = true;
            for (int i = 0; i < entries.Length; i++)
            {
                string stableId = entries[i].Key;
                DatabaseEntry entry = entries[i].Value;
                if (entry is TerrainElementStateDefinition stateDefinition)
                {
                    valid &= TryRegisterStateDefinition(stableId, stateDefinition);
                }
                else if (entry is ElementReactionDefinition reactionDefinition)
                {
                    valid &= TryRegisterReactionDefinition(stableId, reactionDefinition);
                }
            }

            if (m_stateDefinitions.Count == 0)
            {
                Debug.LogError("数据库中没有地表元素状态定义。", this);
                valid = false;
            }

            if (m_reactionCandidates.Count == 0)
            {
                Debug.LogError("数据库中没有元素反应定义。", this);
                valid = false;
            }

            for (int i = 0; i < m_reactionCandidates.Count; i++)
            {
                valid &= ValidateReactionOperations(m_reactionCandidates[i]);
            }

            return valid;
        }

        private bool TryRegisterStateDefinition(
            string stableId,
            TerrainElementStateDefinition definition)
        {
            if (string.IsNullOrEmpty(stableId) || definition == null)
            {
                Debug.LogError("发现缺少稳定 ID 的地表元素状态定义。", this);
                return false;
            }

            if (!definition.TryValidate(out string error))
            {
                Debug.LogError(
                    $"地表元素状态定义 '{stableId}' 无效：{error}",
                    definition);
                return false;
            }

            if (m_stateDefinitions.ContainsKey(definition.StateKind))
            {
                Debug.LogError(
                    $"地表元素状态 {definition.StateKind} 存在重复定义，冲突资产：'{stableId}'。",
                    definition);
                return false;
            }

            m_stateDefinitions.Add(
                definition.StateKind,
                new StateDefinitionBinding(stableId, definition));
            return true;
        }

        private bool TryRegisterReactionDefinition(
            string stableId,
            ElementReactionDefinition definition)
        {
            if (string.IsNullOrEmpty(stableId) || definition == null)
            {
                Debug.LogError("发现缺少稳定 ID 的元素反应定义。", this);
                return false;
            }

            if (!definition.TryValidate(out string error))
            {
                Debug.LogError(
                    $"元素反应定义 '{stableId}' 无效：{error}",
                    definition);
                return false;
            }

            if (!m_reactionStableIds.Add(stableId))
            {
                Debug.LogError(
                    $"元素反应稳定 ID '{stableId}' 重复。",
                    definition);
                return false;
            }

            m_reactionCandidates.Add(new ElementReactionCandidate(stableId, definition));
            return true;
        }

        private bool ValidateReactionOperations(ElementReactionCandidate candidate)
        {
            IReadOnlyList<ElementReactionOperation> operations =
                candidate.Definition.Operations;
            bool valid = true;
            for (int i = 0; i < operations.Count; i++)
            {
                ElementReactionOperation operation = operations[i];
                if (operation == null)
                {
                    Debug.LogError(
                        $"元素反应 '{candidate.StableId}' 包含空结果操作。",
                        candidate.Definition);
                    valid = false;
                    continue;
                }

                switch (operation.Kind)
                {
                    case EElementReactionOperationKind.AddOrRefreshState:
                        if (operation.StateKind == ETerrainElementStateKind.None ||
                            !m_stateDefinitions.ContainsKey(operation.StateKind))
                        {
                            Debug.LogError(
                                $"元素反应 '{candidate.StableId}' 引用了未定义的状态 {operation.StateKind}。",
                                candidate.Definition);
                            valid = false;
                        }

                        break;
                    case EElementReactionOperationKind.RemoveState:
                        if (operation.StateKind == ETerrainElementStateKind.None)
                        {
                            Debug.LogError(
                                $"元素反应 '{candidate.StableId}' 的移除状态操作未指定状态。",
                                candidate.Definition);
                            valid = false;
                        }

                        break;
                    case EElementReactionOperationKind.SetEffectiveSurface:
                        if (operation.SurfaceKind == ETerrainSurfaceKind.None)
                        {
                            Debug.LogError(
                                $"元素反应 '{candidate.StableId}' 的有效底层地表操作未指定地表。",
                                candidate.Definition);
                            valid = false;
                        }

                        break;
                    case EElementReactionOperationKind.SetSurfaceCover:
                        if (operation.SurfaceCoverKind == ETerrainSurfaceCoverKind.None)
                        {
                            Debug.LogError(
                                $"元素反应 '{candidate.StableId}' 的上层地表设置操作未指定覆盖类型。",
                                candidate.Definition);
                            valid = false;
                        }

                        break;
                    case EElementReactionOperationKind.EmitPresentationSignal:
                        if (operation.PresentationSignal == EElementPresentationSignal.None)
                        {
                            Debug.LogError(
                                $"元素反应 '{candidate.StableId}' 的表现信号操作未指定信号。",
                                candidate.Definition);
                            valid = false;
                        }

                        break;
                }
            }

            return valid;
        }

        private bool TryBindActiveMap(bool reportFailure)
        {
            if (!GameManager.TryGetSystem<MapSystem>(out MapSystem mapSystem))
            {
                if (reportFailure)
                {
                    Debug.LogError(
                        "元素反应系统无法取得 MapSystem，不能绑定当前地形地图。",
                        this);
                }

                return false;
            }

            MapInfo mapInfo = mapSystem.ResolveActiveMapInfo();
            if (mapInfo == null ||
                !mapInfo.TryGetTerrainNavigationMap(out TerrainNavigationMap navigationMap))
            {
                if (reportFailure && !m_missingMapReported)
                {
                    Debug.LogError(
                        "当前活动 MapInfo 未配置 TerrainNavigationMap，世界元素无法生效。",
                        this);
                    m_missingMapReported = true;
                }

                return false;
            }

            m_navigationMap = navigationMap;
            m_missingMapReported = false;
            RebuildActiveTimedCellIndex();
            return true;
        }

        private void UnbindMap(bool clearTransientState)
        {
            if (clearTransientState && m_navigationMap != null)
            {
                m_navigationMap.ClearRuntimeSurfaceStates();
            }

            m_navigationMap = null;
            m_activeTimedNodes.Clear();
            m_activeNodeSnapshot.Clear();
            m_missingMapReported = false;
        }

        private void RebuildActiveTimedCellIndex()
        {
            m_activeTimedNodes.Clear();
            if (m_navigationMap == null)
            {
                return;
            }

            m_navigationMap.CollectTimedRuntimeStateNodes(m_activeNodeSnapshot);
            for (int i = 0; i < m_activeNodeSnapshot.Count; i++)
            {
                m_activeTimedNodes.Add(m_activeNodeSnapshot[i]);
            }
        }

        private bool ApplyToNode(
            in TerrainNodeKey nodeKey,
            in ElementApplication application)
        {
            if (!m_navigationMap.TryGetSurfaceSample(
                    nodeKey,
                    out TerrainSurfaceSample previousSample))
            {
                return false;
            }

            ElementReactionContext context = new(
                EElementReactionTrigger.OnElementApplied,
                application,
                ETerrainElementStateKind.None,
                previousSample.BaseSurface,
                previousSample.EffectiveSurface,
                previousSample.EffectiveSurfaceCover,
                previousSample.SurfaceCoverTraits,
                previousSample.RuntimeState);
            ElementReactionResolver.CollectMatches(
                m_reactionCandidates,
                context,
                m_matchingReactions);
            if (m_matchingReactions.Count == 0 ||
                !m_navigationMap.TryGetOrCreateRuntimeNodeState(
                    nodeKey,
                    out TerrainCellRuntimeState runtimeState))
            {
                return false;
            }

            TerrainElementStateSource source = new(
                application.SourceEntity,
                application.SourceAbilityCode);
            bool changed = ExecuteMatchingOperations(
                runtimeState,
                application.Intensity,
                source,
                out EElementPresentationSignal presentationSignal);
            if (!changed && presentationSignal == EElementPresentationSignal.None)
            {
                return false;
            }

            float traversalCostMultiplier = CalculateTraversalCostMultiplier(runtimeState);
            bool committed = m_navigationMap.CommitRuntimeNodeState(
                nodeKey,
                previousSample,
                traversalCostMultiplier,
                presentationSignal);
            UpdateActiveTimedNode(nodeKey, runtimeState);
            return committed;
        }

        private bool ExecuteMatchingOperations(
            TerrainCellRuntimeState runtimeState,
            float triggerIntensity,
            in TerrainElementStateSource source,
            out EElementPresentationSignal presentationSignal)
        {
            bool changed = false;
            presentationSignal = EElementPresentationSignal.None;
            for (int reactionIndex = 0;
                 reactionIndex < m_matchingReactions.Count;
                 reactionIndex++)
            {
                ElementReactionCandidate reaction = m_matchingReactions[reactionIndex];
                IReadOnlyList<ElementReactionOperation> operations =
                    reaction.Definition.Operations;
                for (int operationIndex = 0;
                     operationIndex < operations.Count;
                     operationIndex++)
                {
                    ElementReactionOperation operation = operations[operationIndex];
                    switch (operation.Kind)
                    {
                        case EElementReactionOperationKind.AddOrRefreshState:
                            StateDefinitionBinding stateBinding =
                                m_stateDefinitions[operation.StateKind];
                            float duration = operation.DurationOverride > 0.0f
                                ? operation.DurationOverride
                                : stateBinding.Definition.DefaultDuration;
                            changed |= runtimeState.ApplyOrMergeState(
                                operation.StateKind,
                                triggerIntensity * operation.IntensityMultiplier,
                                duration,
                                source,
                                reaction.StableId,
                                stateBinding.Definition.MergePolicy,
                                stateBinding.StableId);
                            break;
                        case EElementReactionOperationKind.RemoveState:
                            changed |= runtimeState.RemoveState(operation.StateKind);
                            break;
                        case EElementReactionOperationKind.SetEffectiveSurface:
                            changed |= runtimeState.SetEffectiveSurface(operation.SurfaceKind);
                            break;
                        case EElementReactionOperationKind.ClearEffectiveSurface:
                            changed |= runtimeState.ClearEffectiveSurface();
                            break;
                        case EElementReactionOperationKind.EmitPresentationSignal:
                            presentationSignal = operation.PresentationSignal;
                            break;
                        case EElementReactionOperationKind.RemoveSurfaceCover:
                            changed |= runtimeState.RemoveSurfaceCover();
                            break;
                        case EElementReactionOperationKind.SetSurfaceCover:
                            changed |= runtimeState.SetSurfaceCover(
                                operation.SurfaceCoverKind);
                            break;
                        case EElementReactionOperationKind.ClearSurfaceCoverOverride:
                            changed |= runtimeState.ClearSurfaceCoverOverride();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return changed;
        }

        private void AdvanceTimedStates(float fixedStep)
        {
            if (m_activeTimedNodes.Count == 0)
            {
                return;
            }

            m_activeNodeSnapshot.Clear();
            foreach (TerrainNodeKey nodeKey in m_activeTimedNodes)
            {
                m_activeNodeSnapshot.Add(nodeKey);
            }

            for (int i = 0; i < m_activeNodeSnapshot.Count; i++)
            {
                AdvanceNode(m_activeNodeSnapshot[i], fixedStep);
            }
        }

        private void AdvanceNode(in TerrainNodeKey nodeKey, float fixedStep)
        {
            if (!m_navigationMap.TryGetRuntimeNodeState(
                    nodeKey,
                    out TerrainCellRuntimeState runtimeState) ||
                !runtimeState.HasTimedStates ||
                !m_navigationMap.TryGetSurfaceSample(
                    nodeKey,
                    out TerrainSurfaceSample previousSample))
            {
                m_activeTimedNodes.Remove(nodeKey);
                return;
            }

            runtimeState.AdvanceDurations(fixedStep, m_expiredStates);
            if (m_expiredStates.Count == 0)
            {
                return;
            }

            bool changed = false;
            EElementPresentationSignal presentationSignal =
                EElementPresentationSignal.None;
            ElementApplication noApplication = default;
            for (int i = 0; i < m_expiredStates.Count; i++)
            {
                ETerrainElementStateKind expiredStateKind = m_expiredStates[i];
                if (!runtimeState.TryGetState(
                        expiredStateKind,
                        out TerrainElementStateInstance expiredState))
                {
                    continue;
                }

                TerrainElementStateSource source = new(
                    expiredState.SourceEntity,
                    expiredState.SourceAbilityCode);
                if (m_navigationMap.TryGetSurfaceSample(
                        nodeKey,
                        out TerrainSurfaceSample currentSample))
                {
                    ElementReactionContext context = new(
                        EElementReactionTrigger.OnStateExpired,
                        noApplication,
                        expiredStateKind,
                        currentSample.BaseSurface,
                        currentSample.EffectiveSurface,
                        currentSample.EffectiveSurfaceCover,
                        currentSample.SurfaceCoverTraits,
                        currentSample.RuntimeState);
                    ElementReactionResolver.CollectMatches(
                        m_reactionCandidates,
                        context,
                        m_matchingReactions);
                    changed |= ExecuteMatchingOperations(
                        runtimeState,
                        expiredState.Intensity,
                        source,
                        out EElementPresentationSignal expirationSignal);
                    if (expirationSignal != EElementPresentationSignal.None)
                    {
                        presentationSignal = expirationSignal;
                    }
                }

                if (runtimeState.TryGetState(expiredStateKind, out TerrainElementStateInstance stateAfterRules) &&
                    stateAfterRules.RemainingDuration <= 0.0f)
                {
                    changed |= runtimeState.RemoveState(expiredStateKind);
                }
            }

            if (changed || presentationSignal != EElementPresentationSignal.None)
            {
                m_navigationMap.CommitRuntimeNodeState(
                    nodeKey,
                    previousSample,
                    CalculateTraversalCostMultiplier(runtimeState),
                    presentationSignal);
            }

            UpdateActiveTimedNode(nodeKey, runtimeState);
        }

        private float CalculateTraversalCostMultiplier(
            TerrainCellRuntimeState runtimeState)
        {
            float multiplier = 1.0f;
            IReadOnlyList<TerrainElementStateInstance> states =
                runtimeState.ActiveStates;
            for (int i = 0; i < states.Count; i++)
            {
                if (m_stateDefinitions.TryGetValue(
                        states[i].StateKind,
                        out StateDefinitionBinding binding))
                {
                    multiplier *= binding.Definition.TraversalCostMultiplier;
                }
            }

            return Mathf.Max(0.01f, multiplier);
        }

        private void UpdateActiveTimedNode(
            in TerrainNodeKey nodeKey,
            TerrainCellRuntimeState runtimeState)
        {
            if (runtimeState != null && runtimeState.HasTimedStates)
            {
                m_activeTimedNodes.Add(nodeKey);
            }
            else
            {
                m_activeTimedNodes.Remove(nodeKey);
            }
        }
    }
}
