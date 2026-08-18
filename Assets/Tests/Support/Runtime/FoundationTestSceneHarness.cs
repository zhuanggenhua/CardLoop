using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using UnityEngine;
using YokiFrame;
using CoreInputSystem = GameCore.InputSystem;
using RuntimeTabletop = Gameplay.Tabletop.Tabletop;

namespace Gameplay.Tests.Support
{
    /// <summary>
    /// `FoundationTest` 场景的统一牌桌运行验收装配器。
    /// 它创建固定测试状态、绑定正式输入与视图，并按目标形态消费释放意图：
    /// 卡牌目标查询行动并交给 UIKit 选择，空白桌面提交拆堆放置；纯规则验收仍可使用显式选择入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoundationTestSceneHarness : MonoBehaviour
    {
        /// <summary>测试场景使用的唯一内容 ID。</summary>
        public const string TestContentId = "test.foundation.card";

        /// <summary>测试场景行动完成后生成的产物内容 ID。</summary>
        public const string TestProductContentId = "test.foundation.product";

        /// <summary>测试场景用于结果结算的唯一行动内容 ID。</summary>
        public const string TestActionContentId = "test.foundation.action";

		/// <summary>测试多候选与填槽计划的行动内容 ID。</summary>
		public const string TestActionPlanContentId = "test.foundation.action.plan";

		public const string TestFoodContentId = "test.foundation.day-cycle.food";

		public const string TestSellableCardContentId = "test.foundation.day-cycle.sellable";

		public const string TestCurrencyCardContentId = "test.foundation.day-cycle.currency";

		public const string TestBuyerCardContentId = "test.foundation.day-cycle.buyer";

		public const string TestEncounterCardContentId = "test.foundation.day-cycle.encounter";

		public const string TestSellActionContentId = "test.foundation.day-cycle.sell";

		public const string TestDayCycleScenarioContentId = "test.foundation.scenario.day-cycle";

		public const string TestCardPackContentId = "test.foundation.pack";

		public const string TestBeginningPackContentId = "test.foundation.pack.beginning";

		public const string TestCardPackFirstRewardContentId = "test.foundation.pack.reward.first";

		public const string TestCardPackSecondRewardContentId = "test.foundation.pack.reward.second";

		public const string TestBeginningPackSoilContentId = "test.foundation.pack.beginning.soil";

		public const string TestBeginningPackTreeContentId = "test.foundation.pack.beginning.tree";

		public const string TestBeginningPackChickenContentId = "test.foundation.pack.beginning.chicken";

		public const string TestBeginningPackSlimeContentId = "test.foundation.pack.beginning.slime";

		public const string TestBeginningPackGoldenKeyContentId = "test.foundation.pack.beginning.golden-key";

		public const string TestBeginningPackEggContentId = "test.foundation.pack.beginning.egg";

		public const string TestRecipeGrowingBerryActionContentId = "test.foundation.recipe.growing-berry";

		public const string TestRecipeBuildingHouseActionContentId = "test.foundation.recipe.building-house";

		public const string TestRecipeMakingLoveActionContentId = "test.foundation.recipe.making-love";

		public const string TestRecipeMakingTimberActionContentId = "test.foundation.recipe.making-timber";

		public const string TestRecipeCraftingStickActionContentId = "test.foundation.recipe.crafting-stick";

		public const string TestRecipeGrowingBerryCardContentId = "test.foundation.recipe-card.growing-berry";

		public const string TestRecipeBuildingHouseCardContentId = "test.foundation.recipe-card.building-house";

		public const string TestRecipeMakingLoveCardContentId = "test.foundation.recipe-card.making-love";

		public const string TestRecipeMakingTimberCardContentId = "test.foundation.recipe-card.making-timber";

		public const string TestRecipeCraftingStickCardContentId = "test.foundation.recipe-card.crafting-stick";

		public const string TestOpenCardPackActionContentId = "test.foundation.pack.open";

		public const string TestPackVendorContentId = "test.foundation.pack.vendor";

		public const string TestBeginningPackVendorContentId = "test.foundation.pack.beginning.vendor";

		public const string TestPurchaseCardPackActionContentId = "test.foundation.pack.purchase";

		public const string TestChestContentId = "test.foundation.chest";

		public const string TestDepositCurrencyIntoChestActionContentId = "test.foundation.chest.deposit";

		public const string TestWithdrawCurrencyFromChestActionContentId = "test.foundation.chest.withdraw";

        /// <summary>测试场景用于验证任务生命周期的唯一任务内容 ID。</summary>
        public const string TestQuestContentId = "test.foundation.quest";

        /// <summary>测试场景进入正式剧本生命周期时使用的唯一剧本内容 ID。</summary>
        public const string TestScenarioContentId = "test.foundation.scenario";

        /// <summary>测试剧本通过正式场景系统进入附加地图时使用的唯一剧本内容 ID。</summary>
        public const string TestSceneScenarioContentId = "test.foundation.scenario.scene";

		/// <summary>统一牌桌测试剧本的初始地区内容 ID。</summary>
		public const string TestRegionContentId = "test.foundation.region";

		/// <summary>阶段 B 组合验收中承接旅行与战斗的第二牌桌地区内容 ID。</summary>
		public const string TestBattleRegionContentId = "test.foundation.region.battle";

		/// <summary>场景切换测试剧本的第一地区内容 ID。</summary>
		public const string TestSceneRegionContentId = "test.foundation.region.scene";

		/// <summary>场景切换测试剧本的第二地区内容 ID。</summary>
		public const string TestSecondSceneRegionContentId = "test.foundation.region.scene.second";

        [Header("场景引用")]
        [SerializeField, InspectorName("牌桌视图")]
        [Tooltip("负责从测试卡牌状态创建 YooAsset 卡牌视图；缺失时测试场景不会进入就绪状态。")]
        private TabletopView m_tabletopView;

		[SerializeField, InspectorName("牌桌拖拽输入")]
		[Tooltip("消费 GameCore.InputSystem 的 Point/Click，并把释放结果回传给本测试记录器。")]
		private TabletopCardDragInput m_dragInput;

		[SerializeField, InspectorName("牌桌交互")]
		[Tooltip("使用正式单局解释空白放置与行动选择；测试装配器只记录返回结果。")]
		private TabletopInteraction m_tabletopInteraction;

        /// <summary>测试牌桌是否已完成内容索引、视图和正式输入绑定。</summary>
        public bool IsReady { get; private set; }

        /// <summary>测试场景当前牌桌直接拥有的卡牌与牌堆集合；写操作统一经过当前单局牌桌。</summary>
        public TabletopCards Cards { get; private set; }

        /// <summary>三张测试卡牌堆栈中的底部卡牌。</summary>
        public TabletopCardId BottomCardId { get; private set; }

        /// <summary>三张测试卡牌堆栈中的中间卡牌，用于验证从中间成员开始的尾段预览。</summary>
        public TabletopCardId MiddleCardId { get; private set; }

        /// <summary>三张测试卡牌堆栈中的顶部卡牌。</summary>
        public TabletopCardId TopCardId { get; private set; }

        /// <summary>与来源堆分离的空间候选卡牌。</summary>
        public TabletopCardId TargetCardId { get; private set; }

		/// <summary>测试场景中可由玩家单击打开的卡包。</summary>
		public TabletopCardId CardPackId { get; private set; }

		public TabletopCardId BeginningPackId { get; private set; }

		public TabletopCardId PackVendorId { get; private set; }

		public TabletopCardId FirstPackPaymentId { get; private set; }

		public TabletopCardId SecondPackPaymentId { get; private set; }

		public TabletopCardId ChestId { get; private set; }

		public TabletopCardId FirstChestCurrencyId { get; private set; }

		public TabletopCardId SecondChestCurrencyId { get; private set; }

        /// <summary>测试场景已经收到的释放意图数量。</summary>
        public int ReleaseIntentCount { get; private set; }

        /// <summary>最近一次释放意图；在 ReleaseIntentCount 为零时内容无效。</summary>
        public TabletopCardPointerReleaseIntent LastReleaseIntent { get; private set; }

        /// <summary>测试场景已经执行的行动候选查询次数。</summary>
        public int ActionCandidateQueryCount { get; private set; }

        /// <summary>最近一次释放意图得到的候选快照；没有匹配行动时为空集合。</summary>
        public IReadOnlyList<ActionCandidate> LastActionCandidates { get; private set; } =
            Array.Empty<ActionCandidate>();

        /// <summary>统一测试场景当前剧本拥有的边界、卡牌几何和解算轮数。</summary>
        public TabletopCardPlacementRules PlacementRules => m_tabletop?.PlacementRules ??
            throw new InvalidOperationException("测试牌桌尚未由剧本创建。");

        /// <summary>统一测试场景当前运行的剧本单局。</summary>
		public ScenarioRun ScenarioRun => m_scenarioRun;

		/// <summary>只为卡包玩家链验收创建一张测试卡包，不改变统一场景的基础卡牌数量。</summary>
		public TabletopCardId CreateCardPackTestCard()
		{
			if (!IsReady)
			{
				throw new InvalidOperationException("牌桌地基测试尚未就绪，不能创建测试卡包。");
			}
			if (CardPackId.IsValid && Cards.TryGetCard(CardPackId, out _))
			{
				throw new InvalidOperationException("当前测试牌桌已经存在卡包测试卡。");
			}
			m_scenarioRun.DiscoverContent(new ContentId(TestOpenCardPackActionContentId));
			CardPackId = m_tabletop.CreateCard(
				new ContentId(TestCardPackContentId),
				new Vector2(0f, -2.5f)).Id;
			return CardPackId;
		}

		/// <summary>只为 Beginning 卡包业务验收创建一张参考模板对应的卡包。</summary>
		public TabletopCardId CreateBeginningCardPackTestCard()
		{
			if (!IsReady)
			{
				throw new InvalidOperationException("牌桌地基测试尚未就绪，不能创建 Beginning 卡包。");
			}
			if (BeginningPackId.IsValid && Cards.TryGetCard(BeginningPackId, out _))
			{
				throw new InvalidOperationException("当前测试牌桌已经存在 Beginning 卡包。");
			}
			m_scenarioRun.DiscoverContent(new ContentId(TestOpenCardPackActionContentId));
			BeginningPackId = m_tabletop.CreateCard(
				new ContentId(TestBeginningPackContentId),
				new Vector2(1.8f, -2.5f)).Id;
			return BeginningPackId;
		}

		/// <summary>为卡包商贩玩家链显式创建商贩与两枚付款货币。</summary>
		public void CreatePackVendorTestCards()
		{
			if (!IsReady)
			{
				throw new InvalidOperationException("牌桌地基测试尚未就绪，不能创建卡包商贩测试状态。");
			}
			if (PackVendorId.IsValid && Cards.TryGetCard(PackVendorId, out _))
			{
				throw new InvalidOperationException("当前测试牌桌已经存在卡包商贩测试状态。");
			}
			ContentId vendorId = new ContentId(TestPackVendorContentId);
			ContentId currencyId = new ContentId(TestCurrencyCardContentId);
			ContentId purchaseActionId = new ContentId(TestPurchaseCardPackActionContentId);
			if (!m_scenarioRun.ContentIndex.TryGet(vendorId, out PackVendorDefinition _) ||
				!m_scenarioRun.ContentIndex.TryGet(currencyId, out CardDefinition _) ||
				!m_scenarioRun.ContentIndex.TryGet(purchaseActionId, out ActionDefinition _))
			{
				throw new InvalidOperationException("卡包商贩、货币或购买行动没有进入正式内容索引。");
			}
			m_scenarioRun.DiscoverContent(purchaseActionId);
			PackVendorId = m_tabletop.CreateCard(vendorId, new Vector2(0f, -2.5f)).Id;
			FirstPackPaymentId = m_tabletop.CreateCard(currencyId, new Vector2(-2.2f, -2.5f)).Id;
			SecondPackPaymentId = m_tabletop.CreateCard(currencyId, new Vector2(-1.3f, -2.5f)).Id;
		}

		/// <summary>为箱子存币、取币和用箱子付款的玩家链创建测试牌桌状态。</summary>
		public void CreateChestTestCards()
		{
			if (!IsReady)
			{
				throw new InvalidOperationException("牌桌地基测试尚未就绪，不能创建箱子测试状态。");
			}
			if (ChestId.IsValid && Cards.TryGetCard(ChestId, out _))
			{
				throw new InvalidOperationException("当前测试牌桌已经存在箱子测试状态。");
			}
			ContentId chestId = new ContentId(TestChestContentId);
			ContentId currencyId = new ContentId(TestCurrencyCardContentId);
			ContentId depositActionId = new ContentId(TestDepositCurrencyIntoChestActionContentId);
			ContentId withdrawActionId = new ContentId(TestWithdrawCurrencyFromChestActionContentId);
			ContentId purchaseActionId = new ContentId(TestPurchaseCardPackActionContentId);
			if (!m_scenarioRun.ContentIndex.TryGet(chestId, out ChestCardDefinition _) ||
				!m_scenarioRun.ContentIndex.TryGet(currencyId, out CardDefinition _) ||
				!m_scenarioRun.ContentIndex.TryGet(depositActionId, out ActionDefinition _) ||
				!m_scenarioRun.ContentIndex.TryGet(withdrawActionId, out ActionDefinition _) ||
				!m_scenarioRun.ContentIndex.TryGet(purchaseActionId, out ActionDefinition _))
			{
				throw new InvalidOperationException("箱子、货币、存币、取币或购买行动没有进入正式内容索引。");
			}
			m_scenarioRun.DiscoverContent(depositActionId);
			m_scenarioRun.DiscoverContent(withdrawActionId);
			m_scenarioRun.DiscoverContent(purchaseActionId);
			ChestId = m_tabletop.CreateCard(chestId, new Vector2(2.2f, -2.5f)).Id;
			FirstChestCurrencyId = m_tabletop.CreateCard(currencyId, new Vector2(0.4f, -2.5f)).Id;
			SecondChestCurrencyId = m_tabletop.CreateCard(currencyId, new Vector2(1.1f, -2.5f)).Id;
		}

		/// <summary>只供地基验收显式发现填槽测试行动。</summary>
        public void DiscoverActionPlanTestContent()
		{
			if (!IsReady)
			{
				throw new InvalidOperationException("牌桌地基测试尚未就绪，不能发现填槽测试行动。");
			}
			m_scenarioRun.DiscoverContent(new ContentId(TestActionPlanContentId));
		}

        /// <summary>通过正式 UIKit 入口打开剧本存档窗口，供统一场景运行验收。</summary>
        public void OpenSavePanel(ScenarioSavePanelMode mode = ScenarioSavePanelMode.Save)
        {
            if (!IsReady || m_scenarioDirector == null)
            {
                throw new InvalidOperationException("牌桌地基测试尚未就绪，不能打开剧本存档窗口。");
            }

            UIKit.OpenPanelAsync<ScenarioSavePanel>(
                level: UILevel.Pop,
                data: new ScenarioSavePanelData(m_scenarioDirector, mode));
        }

        /// <summary>通过正式 UIKit 入口打开当前单局的只读剧本日志。</summary>
        public void OpenJournalPanel()
        {
            if (!IsReady || m_scenarioRun == null)
            {
                throw new InvalidOperationException("牌桌地基测试尚未就绪，不能打开剧本日志。");
            }

            UIKit.OpenPanelAsync<ScenarioJournalPanel>(
                level: UILevel.Pop,
                data: new ScenarioJournalPanelData(m_scenarioRun));
        }


        private ScenarioRun m_scenarioRun;
        private RuntimeTabletop m_tabletop;
		private ScenarioDirector m_scenarioDirector;
		private bool m_runChangeRegistered;

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

			if (m_tabletopView == null || m_dragInput == null || m_tabletopInteraction == null)
			{
				Debug.LogError("牌桌地基测试缺少视图投影或拖拽输入。", this);
                yield break;
            }

			m_scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			while (m_scenarioDirector.IsChangingScenario)
			{
				yield return null;
			}
			if (!m_scenarioDirector.HasActiveScenario)
			{
				yield return m_scenarioDirector
					.StartScenarioAsync(new ContentId(TestScenarioContentId))
					.ToCoroutine();
			}
			m_scenarioRun = m_scenarioDirector.ActiveRun;
			ContentIndex contentIndex = m_scenarioRun.ContentIndex;
			var contentId = new ContentId(TestContentId);
			if (!contentIndex.TryGet(contentId, out CardDefinition _))
            {
                Debug.LogError($"牌桌地基测试内容没有进入正式索引：{TestContentId}", this);
                yield break;
            }

			if (!contentIndex.TryGet(
                    new ContentId(TestActionContentId),
                    out ActionDefinition actionDefinition))
            {
                Debug.LogError($"牌桌地基测试行动没有进入正式内容索引：{TestActionContentId}", this);
                yield break;
            }

			if (!contentIndex.TryGet(
                    new ContentId(TestProductContentId),
                    out CardDefinition _))
            {
                Debug.LogError($"牌桌地基测试产物没有进入正式内容索引：{TestProductContentId}", this);
                yield break;
            }

			if (!contentIndex.TryGet(
                    new ContentId(TestScenarioContentId),
                    out ScenarioDefinition scenarioDefinition))
            {
                Debug.LogError($"牌桌地基测试剧本没有进入正式内容索引：{TestScenarioContentId}", this);
                yield break;
            }

			if (scenarioDefinition.ContentId != m_scenarioRun.ScenarioId)
			{
				Debug.LogError("牌桌地基测试活动剧本与请求内容不一致。", this);
				yield break;
			}

			if (!contentIndex.TryGet(new ContentId(TestCardPackContentId), out CardPackDefinition cardPack) ||
				!contentIndex.TryGet(new ContentId(TestOpenCardPackActionContentId), out ActionDefinition openPackAction))
			{
				Debug.LogError("牌桌地基测试卡包或打开行动没有进入正式内容索引。", this);
				yield break;
			}
            m_tabletop = m_scenarioRun.Tabletop;
            Cards = m_tabletop.Cards;
			TabletopCard bottom = m_tabletop.CreateCard(contentId, new Vector2(-2.2f, -0.2f));
			TabletopCard middle = m_tabletop.CreateCard(contentId, new Vector2(-2.2f, -0.2f));
			TabletopCard top = m_tabletop.CreateCard(contentId, new Vector2(-2.2f, -0.2f));
			TabletopCard target = m_tabletop.CreateCard(contentId, new Vector2(2.2f, 0.35f));
            m_tabletop.MergeStackOnto(middle.Id, bottom.Id);
            m_tabletop.MergeStackOnto(top.Id, bottom.Id);

            BottomCardId = bottom.Id;
            MiddleCardId = middle.Id;
            TopCardId = top.Id;
            TargetCardId = target.Id;

			m_scenarioRun.DiscoverContent(actionDefinition.ContentId);

			m_tabletopView.Bind(m_tabletop);
			m_tabletopInteraction.Bind(m_scenarioRun);
            CoreInputSystem inputSystem = GameManager.GetSystem<CoreInputSystem>();
			m_dragInput.Bind(inputSystem, m_tabletop, m_tabletopView, RecordReleaseIntent);
            EventKit.Type.Register<ScenarioRunChangedEvent>(OnScenarioRunChanged);
			m_runChangeRegistered = true;
            inputSystem.SetActionMap(EActionMap.Gameplay);
			Exception panelFailure = null;
			yield return OpenRequiredHudPanelsAsync()
				.ToCoroutine(exception => panelFailure = exception);
			if (panelFailure != null)
			{
				Debug.LogException(panelFailure, this);
			}
        }

		private async UniTask OpenRequiredHudPanelsAsync()
		{
			CancellationToken cancellationToken = destroyCancellationToken;
			try
			{
				ScenarioTurnPanel turnPanel = await UIKit.OpenPanelUniTaskAsync<ScenarioTurnPanel>(
					level: UILevel.Hud,
					data: new ScenarioTurnPanelData(m_scenarioDirector),
					ct: cancellationToken);
				if (cancellationToken.IsCancellationRequested || this == null)
				{
					return;
				}
				if (turnPanel == null)
				{
					throw new InvalidOperationException("活动剧本已经启动，但 UIKit 没有加载回合 HUD。");
				}

				TabletopCardInfoPanel infoPanel = await UIKit.OpenPanelUniTaskAsync<TabletopCardInfoPanel>(
					level: UILevel.Hud,
					data: new TabletopCardInfoPanelData(m_tabletopView, m_scenarioRun),
					ct: cancellationToken);
				if (cancellationToken.IsCancellationRequested || this == null)
				{
					return;
				}
				if (infoPanel == null)
				{
					throw new InvalidOperationException("牌桌已经就绪，但 UIKit 没有加载卡牌详情面板。");
				}

				IsReady = true;
			}
			catch (OperationCanceledException)
			{
				// 场景卸载会取消尚未完成的 UIKit 异步实例化；这是测试场景生命周期的正常退出。
			}
		}

		private void OnDisable()
		{
			IsReady = false;
			UIKit.ClosePanel<TabletopCardInfoPanel>();
			UIKit.ClosePanel<ScenarioTurnPanel>();
			UIKit.ClosePanel<ScenarioJournalPanel>();
			UIKit.ClosePanel<ScenarioSavePanel>();
			m_dragInput?.Unbind();
			m_tabletopInteraction?.Unbind();
			m_tabletopView?.Unbind();
			if (m_runChangeRegistered)
			{
				EventKit.Type.UnRegister<ScenarioRunChangedEvent>(OnScenarioRunChanged);
				m_runChangeRegistered = false;
			}
			m_scenarioDirector = null;
            m_scenarioRun = null;
            m_tabletop = null;
            LastActionCandidates = Array.Empty<ActionCandidate>();
        }

		private void OnScenarioRunChanged(ScenarioRunChangedEvent changedEvent)
		{
			if (!ReferenceEquals(changedEvent.PreviousRun, m_scenarioRun))
			{
				return;
			}

			m_dragInput.Unbind();
			m_tabletopInteraction.Unbind();
			m_tabletopView.Unbind();
			m_scenarioRun = changedEvent.CurrentRun;
			m_tabletop = m_scenarioRun?.Tabletop;
			Cards = m_tabletop?.Cards;
			LastActionCandidates = Array.Empty<ActionCandidate>();
			if (m_scenarioRun == null)
			{
				IsReady = false;
				return;
			}

			m_tabletopView.Bind(m_tabletop);
			m_tabletopInteraction.Bind(m_scenarioRun);
			m_dragInput.Bind(
				GameManager.GetSystem<CoreInputSystem>(),
				m_tabletop,
				m_tabletopView,
				RecordReleaseIntent);
			IsReady = true;
		}

        private void RecordReleaseIntent(TabletopCardPointerReleaseIntent intent)
        {
            LastReleaseIntent = intent;
            ReleaseIntentCount++;
            LastActionCandidates = m_tabletopInteraction.HandleRelease(intent);
            if (intent.IsDrag && intent.TargetCardId.IsValid)
            {
                ActionCandidateQueryCount++;
            }
            Debug.Log(
                $"牌桌释放意图：卡牌 {intent.CardId}，拖拽 {intent.IsDrag}，候选卡牌 {intent.TargetCardId}，可选行动 {LastActionCandidates.Count}。",
                this);
        }

        /// <summary>
        /// 在统一测试场景中显式选择最近一次查询实际返回的行动，并交给当前单局的正式行动系统。
        /// 本入口只供地基验收调用，不把单候选解释成自动执行，也不承担正式 UI 职责。
        /// </summary>
        public ActionInstance StartSelectedAction(ContentId selectedActionId)
        {
            ActionCandidate selectedCandidate = null;
            for (int i = 0; i < LastActionCandidates.Count; i++)
            {
                ActionCandidate candidate = LastActionCandidates[i];
                if (candidate.Action.ContentId.Equals(selectedActionId))
                {
                    selectedCandidate = candidate;
                    break;
                }
            }

            if (selectedCandidate == null)
            {
                throw new InvalidOperationException(
                    $"最近一次牌桌查询没有返回行动 {selectedActionId}，测试场景不能绕过候选直接开始行动。");
            }

            ActionRequest request = ActionRequest.FromCandidate(selectedCandidate);
            return m_scenarioRun.StartAction(request);
        }

        /// <summary>
        /// 统一测试场景按两张现存卡牌构造一次正式释放事实并查询行动候选。
        /// 本入口只供纯规则运行验收使用，不触发玩家 UI，也不绕过候选解析或自动执行行动。
        /// </summary>
        public IReadOnlyList<ActionCandidate> QueryTestActionCandidates(
            TabletopCardId sourceCardId,
            TabletopCardId targetCardId)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("牌桌地基测试尚未就绪，不能查询行动候选。");
            }

            Vector2 sourcePosition = Cards.GetStackContaining(sourceCardId).Position;
            Vector2 targetPosition = Cards.GetStackContaining(targetCardId).Position;
			TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
                sourceCardId,
				sourcePosition,
				targetPosition,
				sourcePosition,
				isDrag: true,
                targetCardId);
			LastActionCandidates = m_scenarioRun.FindActionCandidates(intent);
			ActionCandidateQueryCount++;
            return LastActionCandidates;
        }

    }
}
