using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using GameCore;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using UnityEngine;
using UnityEngine.EventSystems;
using YokiFrame;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 当前牌桌的玩家交互组件，把指针释放解释为空白放置或行动选择。
    /// 权威牌桌状态与行动实例仍由 <see cref="ScenarioRun"/> 提交。
    /// </summary>
    [DisallowMultipleComponent]
	public sealed class TabletopInteraction : MonoBehaviour
	{
		private enum StackCraftTerminalDropContinuation
		{
			None = 0,
			PlaceRemainingOnly = 1,
			ContinueStandardRelease = 2
		}

        private ScenarioRun m_scenarioRun;
		private TabletopView m_tabletopView;
        private TabletopActionChoicePanel m_openChoicePanel;
		private TabletopActionPlanPanel m_openPlanPanel;

        public bool IsBound => m_scenarioRun != null;

        public void Bind(ScenarioRun scenarioRun)
        {
            if (scenarioRun == null)
            {
                throw new ArgumentNullException(nameof(scenarioRun));
            }
            if (m_scenarioRun != null)
            {
                throw new InvalidOperationException("牌桌交互尚未解绑，不能覆盖上一局剧本。");
            }
			m_tabletopView = GetComponent<TabletopView>() ??
				throw new InvalidOperationException("牌桌交互需要与 TabletopView 挂在同一对象上。");
            if (!ReferenceEquals(scenarioRun.Tabletop, m_tabletopView.BoundTabletop))
            {
                throw new InvalidOperationException("牌桌交互、牌桌视图和当前剧本必须指向同一张活动牌桌。");
            }

            m_scenarioRun = scenarioRun;
        }

        public void Unbind()
        {
            if (m_scenarioRun == null)
            {
                return;
            }

            if (m_openChoicePanel != null)
            {
                UIKit.ClosePanel(m_openChoicePanel);
            }
			if (m_openPlanPanel != null)
			{
				UIKit.ClosePanel(m_openPlanPanel);
            }
            m_scenarioRun = null;
			m_tabletopView = null;
        }

		/// <summary>
		/// 处理一次正式指针释放，并返回本次按下拆出的牌段是否应保留当前落点。
		/// StackCraft 点击卡包成功后会保留被拆出的卡包段；UI 填槽只绑定行动计划，必须恢复拖拽前位置。
		/// </summary>
        public bool HandleRelease(TabletopCardPointerReleaseIntent intent, out ActionCandidate[] candidates)
        {
            ScenarioRun scenarioRun = m_scenarioRun ??
                throw new InvalidOperationException("牌桌交互尚未绑定活动剧本。");
			bool wasDragRelease = intent.IsDrag;
            if (!intent.IsDrag)
            {
				ActionCandidate[] clickCandidates = scenarioRun.FindActionCandidates(intent);
				if (TryStartStackCraftClickableAction(scenarioRun, intent, clickCandidates, out ActionCandidate clickableCandidate))
				{
					candidates = new ActionCandidate[] { clickableCandidate };
					return true;
				}
				intent = CreateStackCraftStandardDropIntentAfterUnhandledClick(
					scenarioRun,
					intent);
            }
			if (wasDragRelease && TryAcceptOpenActionPlanDropTarget(intent.CardId))
			{
				candidates = Array.Empty<ActionCandidate>();
				return false;
			}
			if (scenarioRun.Tabletop.TryDropBattleParticipant(
					intent.CardId,
					intent.ReleasedCardTablePosition,
					out _,
					out _))
			{
				candidates = Array.Empty<ActionCandidate>();
				return true;
			}
			if (TryHandleStackCraftNearbyTradeOrEquipDrop(scenarioRun, intent, out candidates))
			{
				return true;
			}
			ActionCandidate[] dropCandidates = scenarioRun.FindActionCandidates(intent);
			if (intent.TargetCardId.IsValid)
			{
				ActionCandidate[] terminalDropCandidates = FilterStackCraftTerminalDropCandidates(dropCandidates);
				if (TryStartStackCraftTerminalDropAction(
						scenarioRun,
						intent,
						terminalDropCandidates,
						out candidates))
				{
					return true;
				}
			}
			if (scenarioRun.Tabletop.TryJoinBattleAtPosition(
					intent.CardId,
					intent.ReleasedCardTablePosition,
					out _))
			{
				candidates = Array.Empty<ActionCandidate>();
				return true;
			}
			if (TryStartStackCraftNearbyBattleOnDrop(scenarioRun, intent))
			{
				candidates = Array.Empty<ActionCandidate>();
				return true;
			}
            if (!intent.TargetCardId.IsValid)
            {
                candidates = scenarioRun.Tabletop.TryPlaceStack(
                    intent.CardId,
                    intent.ReleasedCardTablePosition,
                    out TabletopCardStack placedStack)
						? HandleReleasedStack(scenarioRun, placedStack)
						: Array.Empty<ActionCandidate>();
				return true;
            }

			if (scenarioRun.Tabletop.TryDropStackOnto(
					intent.CardId,
					intent.TargetCardId,
					intent.ReleasedCardTablePosition,
					out TabletopCardStack mergedStack))
			{
				candidates = HandleReleasedStack(scenarioRun, mergedStack);
				return true;
			}
			if (dropCandidates.Length > 0)
			{
				candidates = PresentActionCandidates(dropCandidates);
				return true;
			}
			candidates = scenarioRun.Tabletop.TryPlaceStack(
				intent.CardId,
				intent.ReleasedCardTablePosition,
				out TabletopCardStack fallbackStack)
					? HandleReleasedStack(scenarioRun, fallbackStack)
					: Array.Empty<ActionCandidate>();
			return true;
		}

		private TabletopCardPointerReleaseIntent CreateStackCraftStandardDropIntentAfterUnhandledClick(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent clickIntent)
		{
			TabletopCardId targetCardId =
				TryFindNearestStackCraftAttachTarget(
					scenarioRun,
					clickIntent,
					out TabletopCardId resolvedTargetCardId)
					? resolvedTargetCardId
					: default;
			return new TabletopCardPointerReleaseIntent(
				clickIntent.CardId,
				clickIntent.PressPointerPosition,
				clickIntent.ReleasePointerPosition,
				clickIntent.ReleasedCardTablePosition,
				isDrag: true,
				targetCardId);
		}

		private bool TryFindNearestStackCraftAttachTarget(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			out TabletopCardId targetCardId)
		{
			TabletopCardStack sourceStack = scenarioRun.Tabletop.Cards.GetStackContaining(intent.CardId);
			return TryFindNearestStackCraftAttachTarget(
				scenarioRun,
				intent.CardId,
				sourceStack,
				out targetCardId);
		}

		private bool TryFindNearestStackCraftAttachTarget(
			ScenarioRun scenarioRun,
			TabletopCardId draggedCardId,
			TabletopCardStack sourceStack,
			out TabletopCardId targetCardId)
		{
			List<TabletopCardId> targetBottomCardIds = new List<TabletopCardId>();
			IReadOnlyList<TabletopCardStack> stacks = scenarioRun.Tabletop.Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				TabletopCardStack candidateStack = stacks[stackIndex];
				if (ReferenceEquals(candidateStack, sourceStack))
				{
					continue;
				}
				TabletopCardId bottomCardId = candidateStack.BottomCard.Id;
				if (scenarioRun.Tabletop.CanStackOnto(draggedCardId, bottomCardId))
				{
					targetBottomCardIds.Add(bottomCardId);
				}
			}
			if (targetBottomCardIds.Count > 0 &&
				m_tabletopView.TryFindNearestCardViewWithinAttachRadius(
					draggedCardId,
					sourceStack,
					targetBottomCardIds,
					out TabletopCardView targetView))
			{
				targetCardId = targetView.CardId;
				return true;
			}

			targetCardId = default;
			return false;
		}

		private bool TryHandleStackCraftNearbyTradeOrEquipDrop(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			out ActionCandidate[] candidates)
		{
			TabletopCardStack sourceStack = scenarioRun.Tabletop.Cards.GetStackContaining(intent.CardId);
			if (TryFindNearestTradeZoneTarget(intent, sourceStack, out TabletopCardId tradeZoneCardId) &&
				TryHandleTargetedTerminalDrop(
					scenarioRun,
					intent,
					tradeZoneCardId,
					IsStackCraftTradeZoneDropAction,
					out candidates))
			{
				return true;
			}
			if (TryFindNearestEquipmentTarget(scenarioRun, intent, sourceStack, out TabletopCardId characterCardId) &&
				TryHandleTargetedTerminalDrop(
					scenarioRun,
					intent,
					characterCardId,
					IsStackCraftEquipmentDropAction,
					out candidates))
			{
				return true;
			}

			candidates = Array.Empty<ActionCandidate>();
			return false;
		}

		private bool TryFindNearestTradeZoneTarget(
			TabletopCardPointerReleaseIntent intent,
			TabletopCardStack sourceStack,
			out TabletopCardId targetCardId)
		{
			if (m_tabletopView.TryFindNearestVisibleCardViewWithinAttachRadius(
					intent.CardId,
					sourceStack,
					(_, definition) => definition is PackVendorDefinition or CardBuyerDefinition,
					out TabletopCardView targetView))
			{
				targetCardId = targetView.CardId;
				return true;
			}

			targetCardId = default;
			return false;
		}

		private bool TryFindNearestEquipmentTarget(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			TabletopCardStack sourceStack,
			out TabletopCardId targetCardId)
		{
			targetCardId = default;
			if (!scenarioRun.ContentIndex.TryGet(
					sourceStack.TopCard.ContentId,
					out EquipmentCardDefinition _))
			{
				return false;
			}
			if (!m_tabletopView.TryFindNearestVisibleCardViewWithinAttachRadius(
					intent.CardId,
					sourceStack,
					(view, _) => view.TabletopCard is CharacterCard,
					out TabletopCardView targetView))
			{
				return false;
			}

			targetCardId = targetView.CardId;
			return true;
		}

		private bool TryHandleTargetedTerminalDrop(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent originalIntent,
			TabletopCardId targetCardId,
			Func<ActionDefinition, bool> actionPredicate,
			out ActionCandidate[] candidates)
		{
			TabletopCardPointerReleaseIntent targetedIntent =
				CreateTargetedDragIntent(originalIntent, targetCardId);
			ActionCandidate[] targetedCandidates =
				FilterActionCandidates(scenarioRun.FindActionCandidates(targetedIntent), actionPredicate);
			if (TryStartStackCraftTerminalDropAction(
					scenarioRun,
					originalIntent,
					targetedCandidates,
					out candidates))
			{
				return true;
			}

			candidates = Array.Empty<ActionCandidate>();
			return false;
		}

		private bool TryStartStackCraftNearbyBattleOnDrop(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent)
		{
			TabletopCardStack sourceStack = scenarioRun.Tabletop.Cards.GetStackContaining(intent.CardId);
			if (!m_tabletopView.TryFindNearestVisibleCardViewWithinAttachRadius(
					intent.CardId,
					sourceStack,
					(view, _) => scenarioRun.Tabletop.CanStartBattleOnDrop(intent.CardId, view.CardId),
					out TabletopCardView targetView))
			{
				return false;
			}

			return scenarioRun.Tabletop.TryStartBattleOnDrop(
				intent.CardId,
				targetView.CardId,
				out _);
		}

		private static TabletopCardPointerReleaseIntent CreateTargetedDragIntent(
			TabletopCardPointerReleaseIntent intent,
			TabletopCardId targetCardId)
		{
			return new TabletopCardPointerReleaseIntent(
				intent.CardId,
				intent.PressPointerPosition,
				intent.ReleasePointerPosition,
				intent.ReleasedCardTablePosition,
				isDrag: true,
				targetCardId);
		}

		private bool TryAcceptOpenActionPlanDropTarget(TabletopCardId cardId)
		{
			if (m_openPlanPanel == null)
			{
				return false;
			}
			EventSystem eventSystem = GameManager.EventSystem;
			if (eventSystem == null)
			{
				throw new InvalidOperationException("行动计划填槽需要正式 EventSystem 进行 UI 命中。");
			}

			Vector2 screenPosition =
				GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
			PointerEventData pointerEventData = new PointerEventData(eventSystem)
			{
				position = screenPosition
			};
			List<RaycastResult> results = new List<RaycastResult>();
			eventSystem.RaycastAll(pointerEventData, results);
			for (int i = 0; i < results.Count; i++)
			{
				ITabletopCardDropTarget dropTarget =
					results[i].gameObject.GetComponentInParent<ITabletopCardDropTarget>();
				if (dropTarget is not Component targetComponent ||
					!ReferenceEquals(
						targetComponent.GetComponentInParent<TabletopActionPlanPanel>(),
						m_openPlanPanel))
				{
					continue;
				}
				return dropTarget.TryAcceptCard(cardId);
			}
			return false;
		}

		private ActionCandidate[] HandleReleasedStack(
			ScenarioRun scenarioRun,
			TabletopCardStack stack)
		{
			if (scenarioRun.Tabletop.HasActiveActionOnStack(stack))
			{
				return Array.Empty<ActionCandidate>();
			}
			ActionCandidate[] stackCandidates = scenarioRun.FindStackActionCandidates(stack);
			if (scenarioRun.TryStartReadyStackAction(stackCandidates, out _))
			{
				return stackCandidates;
			}
			return PresentActionCandidates(stackCandidates);
		}

		private bool TryStartStackCraftTerminalDropAction(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			ActionCandidate[] terminalCandidates,
			out ActionCandidate[] candidates)
		{
			if (!TryGetSingleReadyImmediateCandidate(terminalCandidates, out ActionCandidate candidate))
			{
				candidates = Array.Empty<ActionCandidate>();
				return false;
			}

			TabletopCardId[] sourceCardIds = SnapshotCurrentStackCardIds(
				scenarioRun.Tabletop,
				intent.CardId);
			StackCraftTerminalDropContinuation continuation =
				GetStackCraftTerminalDropContinuation(candidate.Action);
			scenarioRun.StartAction(ActionRequest.FromCandidate(candidate));
			if (TryHandleRemainingStackAfterTerminalDrop(
					scenarioRun,
					intent,
					sourceCardIds,
					continuation,
					out ActionCandidate[] remainingCandidates))
			{
				candidates = remainingCandidates;
				return true;
			}

			candidates = terminalCandidates;
			return true;
		}

		private ActionCandidate[] ContinueStandardReleaseForRemainingStack(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			TabletopCardStack remainingStack)
		{
			TabletopCardId remainingAnchorCardId = remainingStack.TopCard.Id;
			if (TryFindNearestStackCraftAttachTarget(
					scenarioRun,
					remainingAnchorCardId,
					remainingStack,
					out TabletopCardId targetCardId) &&
				scenarioRun.Tabletop.TryDropStackOnto(
					remainingAnchorCardId,
					targetCardId,
					intent.ReleasedCardTablePosition,
					out TabletopCardStack mergedStack))
			{
				return HandleReleasedStack(scenarioRun, mergedStack);
			}

			return scenarioRun.Tabletop.TryPlaceStack(
				remainingAnchorCardId,
				intent.ReleasedCardTablePosition,
				out TabletopCardStack placedStack)
					? HandleReleasedStack(scenarioRun, placedStack)
					: Array.Empty<ActionCandidate>();
		}

		private ActionCandidate[] PlaceRemainingStackOnly(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			TabletopCardStack remainingStack)
		{
			scenarioRun.Tabletop.TryPlaceStack(
				remainingStack.TopCard.Id,
				intent.ReleasedCardTablePosition,
				out _);
			return Array.Empty<ActionCandidate>();
		}

		private bool TryHandleRemainingStackAfterTerminalDrop(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			IReadOnlyList<TabletopCardId> sourceCardIds,
			StackCraftTerminalDropContinuation continuation,
			out ActionCandidate[] candidates)
		{
			if (continuation == StackCraftTerminalDropContinuation.None ||
				!TryFindRemainingSourceStack(
					scenarioRun.Tabletop,
					sourceCardIds,
					out TabletopCardStack remainingStack))
			{
				candidates = Array.Empty<ActionCandidate>();
				return false;
			}

			candidates = continuation == StackCraftTerminalDropContinuation.ContinueStandardRelease
				? ContinueStandardReleaseForRemainingStack(scenarioRun, intent, remainingStack)
				: PlaceRemainingStackOnly(scenarioRun, intent, remainingStack);
			return true;
		}

		private static StackCraftTerminalDropContinuation GetStackCraftTerminalDropContinuation(
			ActionDefinition action)
		{
			if (IsStackCraftTradeZoneDropAction(action))
			{
				return StackCraftTerminalDropContinuation.ContinueStandardRelease;
			}
			return HasResultIntent(action, intent => intent is DepositCurrencyIntoChestResultIntent)
				? StackCraftTerminalDropContinuation.PlaceRemainingOnly
				: StackCraftTerminalDropContinuation.None;
		}

		private static TabletopCardId[] SnapshotCurrentStackCardIds(
			Tabletop tabletop,
			TabletopCardId cardId)
		{
			TabletopCardStack stack = tabletop.Cards.GetStackContaining(cardId);
			TabletopCardId[] ids = new TabletopCardId[stack.Cards.Count];
			for (int i = 0; i < stack.Cards.Count; i++)
			{
				ids[i] = stack.Cards[i].Id;
			}
			return ids;
		}

		private static bool TryFindRemainingSourceStack(
			Tabletop tabletop,
			IReadOnlyList<TabletopCardId> sourceCardIds,
			out TabletopCardStack remainingStack)
		{
			remainingStack = null;
			for (int i = 0; i < sourceCardIds.Count; i++)
			{
				if (!tabletop.Cards.TryGetStackContaining(
						sourceCardIds[i],
						out TabletopCardStack candidateStack))
				{
					continue;
				}
				if (remainingStack != null && !ReferenceEquals(remainingStack, candidateStack))
				{
					throw new InvalidOperationException(
						"一次 StackCraft 终端释放后，原拖拽牌段被拆成多个牌堆，无法继续按模板释放链收尾。");
				}
				remainingStack = candidateStack;
			}
			return remainingStack != null && remainingStack.Cards.Count > 0;
		}

		/// <summary>
		/// 只读判断一次拖拽目标是否可交互；用于拖拽中的目标高亮，不打开 UI。
		/// </summary>
		public bool CanShowDropTargetHighlight(TabletopCardPointerReleaseIntent intent)
		{
			ScenarioRun scenarioRun = m_scenarioRun ??
				throw new InvalidOperationException("牌桌交互尚未绑定活动剧本。");
			if (!intent.TargetCardId.IsValid)
			{
				return false;
			}
			if (scenarioRun.Tabletop.CanShowStackCraftStackableDropHighlight(
					intent.CardId,
					intent.TargetCardId))
			{
				return true;
			}

			ActionCandidate[] candidates = scenarioRun.FindActionCandidates(intent);
			return HasStackCraftTradeZoneDropCandidate(candidates);
		}

		private static bool TryStartStackCraftClickableAction(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			IReadOnlyList<ActionCandidate> candidates,
			out ActionCandidate clickableCandidate)
		{
			if (scenarioRun == null)
			{
				throw new ArgumentNullException(nameof(scenarioRun));
			}
			if (candidates == null)
			{
				throw new ArgumentNullException(nameof(candidates));
			}
			clickableCandidate = null;
			if (!IsStackCraftClickableCard(scenarioRun, intent.CardId) ||
				!TryGetSingleReadyStackCraftClickableCandidate(candidates, out clickableCandidate))
			{
				return false;
			}

			scenarioRun.StartAction(ActionRequest.FromCandidate(clickableCandidate));
			return true;
		}

		private static bool TryGetSingleReadyStackCraftClickableCandidate(
			IReadOnlyList<ActionCandidate> candidates,
			out ActionCandidate clickableCandidate)
		{
			if (candidates == null)
			{
				throw new ArgumentNullException(nameof(candidates));
			}

			clickableCandidate = null;
			for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
			{
				ActionCandidate candidate = candidates[candidateIndex];
				if (candidate == null ||
					!candidate.IsReady ||
					candidate.Action.TurnCost != 0 ||
					!HasStackCraftClickableResult(candidate.Action))
				{
					continue;
				}
				if (clickableCandidate != null)
				{
					throw new InvalidOperationException(
						"同一张 StackCraft 可点击卡牌解析出多个即时点击动作，作者源必须只保留一个开包或取币入口。");
				}
				clickableCandidate = candidate;
			}
			return clickableCandidate != null;
		}

		private static bool TryStartSingleReadyImmediateAction(
			ScenarioRun scenarioRun,
			IReadOnlyList<ActionCandidate> candidates)
		{
			if (!TryGetSingleReadyImmediateCandidate(candidates, out ActionCandidate candidate))
			{
				return false;
			}

			scenarioRun.StartAction(ActionRequest.FromCandidate(candidate));
			return true;
		}

		private static bool TryGetSingleReadyImmediateCandidate(
			IReadOnlyList<ActionCandidate> candidates,
			out ActionCandidate candidate)
		{
			if (candidates == null)
			{
				throw new ArgumentNullException(nameof(candidates));
			}
			candidate = null;
			for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
			{
				ActionCandidate current = candidates[candidateIndex];
				if (current == null ||
					!current.IsReady ||
					current.Action.TurnCost != 0)
				{
					continue;
				}
				if (candidate != null)
				{
					throw new InvalidOperationException(
						"同一次 StackCraft 终端释放解析出多个可立即执行的动作，作者源必须保证交易、装备、存币和出售入口唯一。");
				}
				candidate = current;
			}
			return candidate != null;
		}

		private static ActionCandidate[] FilterStackCraftTerminalDropCandidates(
			IReadOnlyList<ActionCandidate> candidates)
		{
			return FilterActionCandidates(candidates, IsStackCraftTerminalDropAction);
		}

		private static ActionCandidate[] FilterActionCandidates(
			IReadOnlyList<ActionCandidate> candidates,
			Func<ActionDefinition, bool> predicate)
		{
			if (candidates == null || candidates.Count == 0)
			{
				return Array.Empty<ActionCandidate>();
			}
			if (predicate == null)
			{
				throw new ArgumentNullException(nameof(predicate));
			}

			List<ActionCandidate> filtered = new List<ActionCandidate>(candidates.Count);
			for (int i = 0; i < candidates.Count; i++)
			{
				ActionCandidate candidate = candidates[i];
				if (candidate != null && predicate(candidate.Action))
				{
					filtered.Add(candidate);
				}
			}
			return filtered.ToArray();
		}

		private static bool IsStackCraftTerminalDropAction(ActionDefinition action)
		{
			return HasResultIntent(action, intent =>
				intent is EquipCardResultIntent or PurchaseCardPackResultIntent or
					DepositCurrencyIntoChestResultIntent or SellCardsResultIntent);
		}

		private static bool HasStackCraftTradeZoneDropCandidate(IReadOnlyList<ActionCandidate> candidates)
		{
			if (candidates == null || candidates.Count == 0)
			{
				return false;
			}

			for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
			{
				ActionCandidate candidate = candidates[candidateIndex];
				if (candidate != null && IsStackCraftTradeZoneDropAction(candidate.Action))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsStackCraftTradeZoneDropAction(ActionDefinition action)
		{
			return HasResultIntent(action, intent =>
				intent is PurchaseCardPackResultIntent or SellCardsResultIntent);
		}

		private static bool IsStackCraftEquipmentDropAction(ActionDefinition action)
		{
			return HasResultIntent(action, intent => intent is EquipCardResultIntent);
		}

		private static bool HasResultIntent(
			ActionDefinition action,
			Func<ActionResultIntent, bool> predicate)
		{
			if (action == null)
			{
				return false;
			}
			for (int i = 0; i < action.ResultIntents.Count; i++)
			{
				if (predicate(action.ResultIntents[i]))
				{
					return true;
				}
			}
			for (int branchIndex = 0; branchIndex < action.ResultBranches.Count; branchIndex++)
			{
				ActionResultBranchDefinition branch = action.ResultBranches[branchIndex];
				if (branch == null)
				{
					continue;
				}
				for (int intentIndex = 0; intentIndex < branch.ResultIntents.Count; intentIndex++)
				{
					if (predicate(branch.ResultIntents[intentIndex]))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool IsStackCraftClickableCard(ScenarioRun scenarioRun, TabletopCardId cardId)
		{
			if (!scenarioRun.Tabletop.Cards.TryGetCard(cardId, out TabletopCard card) ||
				!scenarioRun.ContentIndex.TryGet(card.ContentId, out ContentAsset content))
			{
				return false;
			}

			return content is CardPackDefinition or ChestCardDefinition;
		}

		private static bool HasStackCraftClickableResult(ActionDefinition action)
		{
			for (int intentIndex = 0; intentIndex < action.ResultIntents.Count; intentIndex++)
			{
				if (action.ResultIntents[intentIndex] is OpenCardPackResultIntent or
					WithdrawCurrencyFromChestResultIntent)
				{
					return true;
				}
			}
			return false;
		}

		private ActionCandidate[] PresentActionCandidates(ActionCandidate[] candidates)
		{
            if (candidates.Length == 0)
            {
                return candidates;
            }

            Vector2 screenAnchor =
                GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
            UIKit.OpenPanelAsync<TabletopActionChoicePanel>(
                callback: panel =>
                {
                    if (panel is not TabletopActionChoicePanel choicePanel)
                    {
                        throw new InvalidOperationException(
                            "牌桌行动候选已经产生，但 UIKit 没有加载行动选择面板。");
                    }

                    m_openChoicePanel = choicePanel;
                    choicePanel.OnClosed(() =>
                    {
                        if (ReferenceEquals(m_openChoicePanel, choicePanel))
                        {
                            m_openChoicePanel = null;
                        }
                    });
                },
                level: UILevel.Pop,
                data: new TabletopActionChoicePanelData(
                    candidates,
                    screenAnchor,
					SelectCandidate));
            return candidates;
        }

		private void SelectCandidate(ActionCandidate candidate)
		{
			ScenarioRun scenarioRun = m_scenarioRun ??
				throw new InvalidOperationException("牌桌交互尚未绑定活动剧本。");
			if (candidate.IsReady)
			{
				scenarioRun.StartAction(ActionRequest.FromCandidate(candidate));
				return;
			}

			ActionPlan plan = scenarioRun.CreateActionPlan(candidate);
			UIKit.OpenPanelAsync<TabletopActionPlanPanel>(
				callback: panel =>
				{
					if (panel is not TabletopActionPlanPanel planPanel)
					{
						throw new InvalidOperationException(
							"牌桌已创建行动计划，但 UIKit 没有加载填槽面板。");
					}
					m_openPlanPanel = planPanel;
					planPanel.OnClosed(() =>
					{
						if (ReferenceEquals(m_openPlanPanel, planPanel))
						{
							m_openPlanPanel = null;
						}
					});
				},
				level: UILevel.Pop,
				data: new TabletopActionPlanPanelData(scenarioRun, plan));
		}

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
