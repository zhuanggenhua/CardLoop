using System;
using System.Collections;
using System.Collections.Generic;
using GAS.Runtime;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using UnityEngine;
using CoreInputSystem = GameCore.InputSystem;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// `FoundationTest` 场景的最小牌桌装配器。
    /// 它创建固定测试状态、绑定正式输入与视图，并把释放意图交给行动候选解析器；
    /// 释放接线不自动提交移动、拆堆、合堆或行动；验收代码可以通过显式选择入口启动普通行动作业。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoundationTestSceneHarness : MonoBehaviour
    {
        /// <summary>测试场景使用的唯一内容 ID。</summary>
        public const string TestContentId = "test.foundation.card";

        /// <summary>测试场景普通行动即时换算规则使用的唯一内容 ID。</summary>
        public const string TestTurnTimingContentId = "test.foundation.turn-timing";

        /// <summary>测试场景行动完成后生成的产物内容 ID。</summary>
        public const string TestProductContentId = "test.foundation.product";

        /// <summary>测试场景用于结果结算的唯一行动内容 ID。</summary>
        public const string TestActionContentId = "test.foundation.action";

        /// <summary>测试场景用于验证任务生命周期的唯一任务内容 ID。</summary>
        public const string TestQuestContentId = "test.foundation.quest";

        /// <summary>测试场景进入正式剧本生命周期时使用的唯一剧本内容 ID。</summary>
        public const string TestScenarioContentId = "test.foundation.scenario";

        /// <summary>统一测试场景注入牌桌行动权威随机流的固定种子。</summary>
        public const uint TestActionRandomSeed = 12345;

        [Header("场景引用")]
        [SerializeField, InspectorName("牌桌视图投影")]
        [Tooltip("负责从测试卡牌状态创建 YooAsset 卡牌视图；缺失时测试场景不会进入就绪状态。")]
        private TabletopCardViewProjector m_viewProjector;

        [SerializeField, InspectorName("牌桌拖拽输入")]
        [Tooltip("消费 GameCore.InputSystem 的 Point/Click，并把释放结果回传给本测试记录器。")]
        private TabletopCardDragInput m_dragInput;

        /// <summary>测试牌桌是否已完成内容索引、视图和正式输入绑定。</summary>
        public bool IsReady { get; private set; }

        /// <summary>测试场景当前使用的权威可堆叠卡牌状态；场景释放消费者不会修改它。</summary>
        public TabletopCardState CardState { get; private set; }

        /// <summary>三张测试卡牌堆栈中的底部卡牌。</summary>
        public TabletopCardId BottomCardId { get; private set; }

        /// <summary>三张测试卡牌堆栈中的中间卡牌，用于验证从中间成员开始的尾段预览。</summary>
        public TabletopCardId MiddleCardId { get; private set; }

        /// <summary>三张测试卡牌堆栈中的顶部卡牌。</summary>
        public TabletopCardId TopCardId { get; private set; }

        /// <summary>与来源堆分离的空间候选卡牌。</summary>
        public TabletopCardId TargetCardId { get; private set; }

        /// <summary>测试场景已经收到的释放意图数量。</summary>
        public int ReleaseIntentCount { get; private set; }

        /// <summary>最近一次释放意图；在 ReleaseIntentCount 为零时内容无效。</summary>
        public TabletopCardPointerReleaseIntent LastReleaseIntent { get; private set; }

        /// <summary>测试场景已经执行的行动候选查询次数。</summary>
        public int ActionCandidateQueryCount { get; private set; }

        /// <summary>最近一次释放意图得到的候选快照；没有匹配行动时为空集合。</summary>
        public IReadOnlyList<TabletopCardActionCandidate> LastActionCandidates { get; private set; } =
            Array.Empty<TabletopCardActionCandidate>();

        /// <summary>统一测试场景使用的局内发现状态；只用于验证行动候选必须先通过发现门槛。</summary>
        public ContentDiscoveryState DiscoveryState => m_discoveryState;

        private readonly Dictionary<TabletopCardId, AbilitySystemCell> m_abilitySystemCells = new();
        private ContentIndex m_contentIndex;
        private ContentDiscoveryState m_discoveryState;
        private ActionDefinition[] m_availableActions = Array.Empty<ActionDefinition>();

        private IEnumerator Start()
        {
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
            {
                yield return null;
            }

            if (GameManager.StartupState != GameManagerStartupState.Ready)
            {
                Debug.LogError(
                    $"牌桌地基测试无法启动：GameManager 状态为 {GameManager.StartupState}。\n{GameManager.StartupException}",
                    this);
                yield break;
            }

            if (m_viewProjector == null || m_dragInput == null)
            {
                Debug.LogError("牌桌地基测试缺少视图投影或拖拽输入引用。", this);
                yield break;
            }

            ContentRegistrySystem contentSystem = GameManager.GetSystem<ContentRegistrySystem>();
            var contentId = new ContentId(TestContentId);
            if (!contentSystem.Index.TryGet(contentId, out CardDefinition _))
            {
                Debug.LogError($"牌桌地基测试内容没有进入正式索引：{TestContentId}", this);
                yield break;
            }

            if (!contentSystem.Index.TryGet(
                    new ContentId(TestActionContentId),
                    out ActionDefinition actionDefinition))
            {
                Debug.LogError($"牌桌地基测试行动没有进入正式内容索引：{TestActionContentId}", this);
                yield break;
            }

            if (!contentSystem.Index.TryGet(
                    new ContentId(TestProductContentId),
                    out CardDefinition _))
            {
                Debug.LogError($"牌桌地基测试产物没有进入正式内容索引：{TestProductContentId}", this);
                yield break;
            }

            if (!contentSystem.Index.TryGet(
                    new ContentId(TestScenarioContentId),
                    out ScenarioDefinition scenarioDefinition))
            {
                Debug.LogError($"牌桌地基测试剧本没有进入正式内容索引：{TestScenarioContentId}", this);
                yield break;
            }

            ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
            scenarioDirector.StartScenario(scenarioDefinition.ContentId, contentSystem.Index);

            CardState = new TabletopCardState();
            TabletopCard bottom = CardState.CreateCard(contentId, new Vector2(-2.2f, -0.2f));
            TabletopCard middle = CardState.CreateCard(contentId, new Vector2(-2.2f, -0.2f));
            TabletopCard top = CardState.CreateCard(contentId, new Vector2(-2.2f, -0.2f));
            TabletopCard target = CardState.CreateCard(contentId, new Vector2(2.2f, 0.35f));
            CardState.MergeStackOnto(middle.Id, bottom.Id);
            CardState.MergeStackOnto(top.Id, bottom.Id);

            BottomCardId = bottom.Id;
            MiddleCardId = middle.Id;
            TopCardId = top.Id;
            TargetCardId = target.Id;

            int[] abilitySystemTags = GetTestAbilitySystemTags(actionDefinition);
            RegisterAbilitySystemCell(BottomCardId, abilitySystemTags);
            RegisterAbilitySystemCell(MiddleCardId, abilitySystemTags);
            RegisterAbilitySystemCell(TopCardId, abilitySystemTags);
            RegisterAbilitySystemCell(TargetCardId, abilitySystemTags);
            m_contentIndex = contentSystem.Index;
            m_discoveryState = new ContentDiscoveryState();
            m_discoveryState.MarkDiscovered(actionDefinition.ContentId, m_contentIndex);
            m_availableActions = ActionDiscoveryFilter.FilterDiscoveredActions(
                new[] { actionDefinition },
                m_discoveryState);

            TabletopCardActionSystem actionSystem = GameManager.GetSystem<TabletopCardActionSystem>();
            actionSystem.BindTabletopActionStateWithAbilitySystem(
                CardState,
                m_contentIndex,
                ResolveAbilitySystemCell);
            actionSystem.InitializeAuthoritativeRandom(TestActionRandomSeed);

            m_viewProjector.Bind(CardState, contentSystem.Index);
            CoreInputSystem inputSystem = GameManager.GetSystem<CoreInputSystem>();
            m_dragInput.Bind(inputSystem, CardState, m_viewProjector, RecordReleaseIntent);
            inputSystem.SetActionMap(EActionMap.Gameplay);
            IsReady = true;
        }

        private void OnDisable()
        {
            IsReady = false;
            m_dragInput?.Unbind();
            foreach (AbilitySystemCell abilitySystemCell in m_abilitySystemCells.Values)
            {
                abilitySystemCell.Dispose();
            }

            m_abilitySystemCells.Clear();
            m_contentIndex = null;
            m_discoveryState = null;
            m_availableActions = Array.Empty<ActionDefinition>();
            LastActionCandidates = Array.Empty<TabletopCardActionCandidate>();
        }

        private void RecordReleaseIntent(TabletopCardPointerReleaseIntent intent)
        {
            LastReleaseIntent = intent;
            ReleaseIntentCount++;
            LastActionCandidates = TabletopCardActionCandidateResolver.FindCandidatesWithAbilitySystem(
                intent,
                CardState,
                m_contentIndex,
                m_availableActions,
                ResolveAbilitySystemCell);
            ActionCandidateQueryCount++;
            Debug.Log(
                $"牌桌释放意图：卡牌 {intent.CardId}，拖拽 {intent.IsDrag}，候选卡牌 {intent.TargetCardId}，可选行动 {LastActionCandidates.Count}。",
                this);
        }

        private static int[] GetTestAbilitySystemTags(ActionDefinition actionDefinition)
        {
            if (actionDefinition.ParticipationSlots.Count == 0)
            {
                return Array.Empty<int>();
            }

            ActionSlotDefinition slot = actionDefinition.ParticipationSlots[0];
            if (slot.RequiredAnyAbilitySystemTagCodes.Count > 0)
            {
                return new[] { slot.RequiredAnyAbilitySystemTagCodes[0] };
            }

            if (slot.RequiredAllAbilitySystemTagCodes.Count > 0)
            {
                return new[] { slot.RequiredAllAbilitySystemTagCodes[0] };
            }

            return Array.Empty<int>();
        }

        private void RegisterAbilitySystemCell(TabletopCardId cardId, IReadOnlyList<int> tags)
        {
            var abilitySystemCell = new AbilitySystemCell();
            abilitySystemCell.Init(
                tags,
                Array.Empty<AttrSetConfig>(),
                Array.Empty<AbilityConfig>());
            m_abilitySystemCells.Add(cardId, abilitySystemCell);
        }

        private AbilitySystemCell ResolveAbilitySystemCell(TabletopCardId cardId)
        {
            return m_abilitySystemCells.TryGetValue(cardId, out AbilitySystemCell abilitySystemCell)
                ? abilitySystemCell
                : null;
        }

        /// <summary>
        /// 在统一测试场景中显式选择最近一次查询实际返回的行动，并交给正式普通行动作业系统。
        /// 本入口只供地基验收调用，不把单候选解释成自动执行，也不承担正式 UI 职责。
        /// </summary>
        public TabletopCardActionJob StartSelectedAction(ContentId selectedActionId)
        {
            if (!TabletopCardActionCandidateSelector.TrySelect(
                    LastActionCandidates,
                    selectedActionId,
                    out TabletopCardActionCandidate selectedCandidate))
            {
                throw new InvalidOperationException(
                    $"最近一次牌桌查询没有返回行动 {selectedActionId}，测试场景不能绕过候选直接启动作业。");
            }

            TabletopCardActionRequest request = TabletopCardActionRequest.FromCandidate(selectedCandidate);
            TabletopCardActionJob job = GameManager.GetSystem<TabletopCardActionSystem>().StartAction(request);
            if (job.State == TabletopCardActionJobState.Completed)
            {
                m_viewProjector.Refresh();
            }
            else
            {
                StartCoroutine(RefreshAfterActionCompletes(job));
            }

            return job;
        }

        /// <summary>
        /// 统一测试场景按两张现存卡牌构造一次正式释放事实并查询行动候选。
        /// 本入口只供运行验收使用，不绕过候选解析，也不承担正式 UI 或自动执行职责。
        /// </summary>
        public IReadOnlyList<TabletopCardActionCandidate> QueryTestActionCandidates(
            TabletopCardId sourceCardId,
            TabletopCardId targetCardId)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("牌桌地基测试尚未就绪，不能查询行动候选。");
            }

            Vector2 sourcePosition = CardState.GetStackContaining(sourceCardId).Position;
            Vector2 targetPosition = CardState.GetStackContaining(targetCardId).Position;
            RecordReleaseIntent(new TabletopCardPointerReleaseIntent(
                sourceCardId,
                sourcePosition,
                targetPosition,
                isDrag: true,
                targetCardId));
            return LastActionCandidates;
        }

        /// <summary>
        /// 测试场景在正式作业离开运行态后刷新一次现有视图投影，不让测试装配器轮询或复制牌桌状态。
        /// </summary>
        private IEnumerator RefreshAfterActionCompletes(TabletopCardActionJob job)
        {
            while (job.State is TabletopCardActionJobState.Running or TabletopCardActionJobState.Paused)
            {
                yield return null;
            }

            m_viewProjector.Refresh();
        }
    }
}
