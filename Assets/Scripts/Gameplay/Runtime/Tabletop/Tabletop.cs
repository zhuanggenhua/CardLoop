using System;
using System.Collections.Generic;
using GAS.Runtime;
using Gameplay.Actions;
using Gameplay.Tabletop.Actions;
using Gameplay.Content;
using Unity.Mathematics;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 普通牌桌行动采用回合推进或按回合规则换算的即时推进。
	/// </summary>
	public enum ActionProgressionMode
	{
		TurnBased = 0,
		RealTime = 10
	}

	internal readonly struct TabletopActionCompletion
	{
		internal TabletopActionCompletion(
			Tabletop tabletop,
			ActionInstance action,
			TabletopCardStack completedStack,
			ActionSettlementResult result)
		{
			Tabletop = tabletop ?? throw new ArgumentNullException(nameof(tabletop));
			Action = action ?? throw new ArgumentNullException(nameof(action));
			CompletedStack = completedStack;
			Result = result ?? throw new ArgumentNullException(nameof(result));
		}

		internal Tabletop Tabletop { get; }

		internal ActionInstance Action { get; }

		internal ContentId ActionId => Action.ActionId;

		internal TabletopCardStack CompletedStack { get; }

		internal ActionSettlementResult Result { get; }
	}

	/// <summary>
	/// 当前剧本单局的牌桌聚合根，统一拥有卡牌、牌堆、行动实例、权威随机和状态写入口。
	/// </summary>
	public sealed class Tabletop
	{
		private const float ProjectileAttackPreActivationSeconds = 0.5f;
		private const float AutomaticBehaviorInitialDelayMinSeconds = 0.5f;
		private const float AutomaticBehaviorInitialDelayMaxSeconds = 1.0f;

		private readonly struct PeriodicProductionRequest
		{
			internal PeriodicProductionRequest(ContentId productCardId, Vector2 sourcePosition)
			{
				ProductCardId = productCardId;
				SourcePosition = sourcePosition;
			}

			internal ContentId ProductCardId { get; }

			internal Vector2 SourcePosition { get; }
		}

		private readonly struct AutomaticMovementRequest
		{
			internal AutomaticMovementRequest(TabletopCardId cardId)
			{
				CardId = cardId;
			}

			internal TabletopCardId CardId { get; }
		}

		private sealed class LocalStackDragState
		{
			internal LocalStackDragState(
				TabletopCardId cardId,
				TabletopCardStack originalStack,
				TabletopCardStack draggedStack,
				ActionInstance stackAction,
				bool actionPausedByDrag)
			{
				CardId = cardId;
				OriginalStack = originalStack ?? throw new ArgumentNullException(nameof(originalStack));
				DraggedStack = draggedStack ?? throw new ArgumentNullException(nameof(draggedStack));
				StackAction = stackAction;
				ActionPausedByDrag = actionPausedByDrag;
				DetachedFromOriginalStack = !ReferenceEquals(originalStack, draggedStack);
			}

			internal TabletopCardId CardId { get; }

			internal TabletopCardStack OriginalStack { get; }

			internal TabletopCardStack DraggedStack { get; }

			internal ActionInstance StackAction { get; }

			internal bool ActionPausedByDrag { get; }

			internal bool DetachedFromOriginalStack { get; }
		}

		private readonly ContentIndex m_contentIndex;
		private readonly Func<ContentId, bool> m_isContentDiscovered;
		private readonly Action<TabletopActionCompletion> m_actionCompleted;
		private readonly Action<IReadOnlyList<ContentId>> m_cardsDefeated;
		private readonly TabletopCardPlacementRules m_basePlacementRules;

		private readonly List<ActionInstance> m_activeActions = new List<ActionInstance>();
		private readonly List<ActionPlan> m_actionPlans = new List<ActionPlan>();
		private readonly IReadOnlyList<ActionPlan> m_readOnlyActionPlans;
		private readonly List<Battle> m_activeBattles = new List<Battle>();
		private readonly IReadOnlyList<Battle> m_readOnlyBattles;
		private readonly List<PeriodicProductionRequest> m_periodicProductionRequests =
			new List<PeriodicProductionRequest>();
		private readonly List<TabletopCardCreationRequest> m_periodicProductionCreations =
			new List<TabletopCardCreationRequest>();
		private readonly List<AutomaticMovementRequest> m_automaticMovementRequests =
			new List<AutomaticMovementRequest>();
		private TabletopCardId m_localInputHeldAutomaticBehaviorCardId;
		private LocalStackDragState m_localStackDragState;
		private readonly BattleFormation m_battleFormation;
		private ulong m_nextBattleId = 1uL;
		private ulong m_battleRevision;
		private int m_currentPlacementCardLimitBonus;
		private TabletopCardPlacementRules m_currentPlacementRules;

		private float m_realTimeSecondsPerTurn;

		private Unity.Mathematics.Random m_authoritativeRandom;

		public TabletopCards Cards { get; }

		/// <summary>当前牌桌的唯一放置规则；基础作者源固定，边界可由桌面卡牌上限加成派生扩展。</summary>
		public TabletopCardPlacementRules PlacementRules => m_currentPlacementRules;

		/// <summary>当前牌桌所有卡牌提供的上限加成总和，也是可放置边界扩展的唯一派生来源。</summary>
		public int CardLimitBonus => CalculateCardLimitBonus();

		public IReadOnlyList<ActionInstance> ActiveActions => m_activeActions;

		/// <summary>当前牌桌中尚未提交的行动计划。</summary>
		public IReadOnlyList<ActionPlan> ActionPlans => m_readOnlyActionPlans;

		/// <summary>当前牌桌拥有的活动战斗；战斗状态不复制卡牌状态。</summary>
		public IReadOnlyList<Battle> ActiveBattles => m_readOnlyBattles;

		/// <summary>活动战斗关系变更版本，只供表现层判断是否需要重新投影，不承载第二份玩法状态。</summary>
		internal ulong BattleRevision => m_battleRevision;

		internal ContentIndex ContentIndex => m_contentIndex;

		/// <summary>读取当前单局的内容发现事实；牌桌表现用它投影卡包商贩收藏进度，不保存第二份发现状态。</summary>
		internal bool IsContentDiscovered(ContentId contentId)
		{
			return m_isContentDiscovered(contentId);
		}

		internal event Action<ContentId, ActionSettlementResult> ActionSettled;

		internal event Action<TabletopPresentationCue> PresentationCueRequested;

		public ActionProgressionMode ProgressionMode { get; private set; } = ActionProgressionMode.TurnBased;

		/// <summary>牌桌所属单局是否已经结束；结束后只允许读取最终状态与快照。</summary>
		public bool IsEnded { get; private set; }

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardPlacementRules placementRules,
			Func<ContentId, bool> isContentDiscovered,
			Action<ContentId, ActionSettlementResult> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
			: this(
				contentIndex,
				placementRules,
				isContentDiscovered,
				CreateLegacyActionCompletionCallback(actionCompleted),
				cardsDefeated,
				battleFormationRules,
				cardIdSequence)
		{
		}

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardPlacementRules placementRules,
			Func<ContentId, bool> isContentDiscovered,
			Action<TabletopActionCompletion> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
		{
			m_contentIndex = contentIndex ?? throw new ArgumentNullException("contentIndex");
			m_basePlacementRules = placementRules ?? throw new ArgumentNullException(nameof(placementRules));
			m_currentPlacementRules = m_basePlacementRules;
			m_isContentDiscovered = isContentDiscovered ?? throw new ArgumentNullException(nameof(isContentDiscovered));
			m_actionCompleted = actionCompleted ?? throw new ArgumentNullException(nameof(actionCompleted));
			m_cardsDefeated = cardsDefeated ?? throw new ArgumentNullException(nameof(cardsDefeated));
			Cards = new TabletopCards(cardIdSequence, ResolveCardSize);
			m_readOnlyActionPlans = m_actionPlans.AsReadOnly();
			m_readOnlyBattles = m_activeBattles.AsReadOnly();
			m_battleFormation = battleFormationRules?.CreateRuntime();
		}

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardStateSnapshot cardStateSnapshot,
			TabletopCardPlacementRules placementRules,
			Func<ContentId, bool> isContentDiscovered,
			Action<ContentId, ActionSettlementResult> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
			: this(
				contentIndex,
				cardStateSnapshot,
				placementRules,
				isContentDiscovered,
				CreateLegacyActionCompletionCallback(actionCompleted),
				cardsDefeated,
				battleFormationRules,
				cardIdSequence)
		{
		}

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardStateSnapshot cardStateSnapshot,
			TabletopCardPlacementRules placementRules,
			Func<ContentId, bool> isContentDiscovered,
			Action<TabletopActionCompletion> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
		{
			m_contentIndex = contentIndex ?? throw new ArgumentNullException("contentIndex");
			m_basePlacementRules = placementRules ?? throw new ArgumentNullException(nameof(placementRules));
			m_currentPlacementRules = m_basePlacementRules;
			m_isContentDiscovered = isContentDiscovered ?? throw new ArgumentNullException(nameof(isContentDiscovered));
			m_actionCompleted = actionCompleted ?? throw new ArgumentNullException(nameof(actionCompleted));
			m_cardsDefeated = cardsDefeated ?? throw new ArgumentNullException(nameof(cardsDefeated));
			Cards = TabletopCards.Restore(
				cardStateSnapshot,
				cardIdSequence ?? throw new ArgumentNullException(nameof(cardIdSequence)),
				RestoreCardFromSnapshot,
				ResolveCardSize);
			m_readOnlyActionPlans = m_actionPlans.AsReadOnly();
			m_readOnlyBattles = m_activeBattles.AsReadOnly();
			m_battleFormation = battleFormationRules?.CreateRuntime();
			try
			{
				RefreshPlacementRulesForCurrentCards(reflowExistingStacks: false);
				Cards.RequireValidPlacement(PlacementRules);
				for (int stackIndex = 0; stackIndex < Cards.Stacks.Count; stackIndex++)
				{
					TabletopCardStack stack = Cards.Stacks[stackIndex];
					for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
					{
						TabletopCard card = stack.Cards[cardIndex];
						RequireCardDefinition(card.ContentId, $"恢复牌桌卡牌 {card.Id}");
					}
				}
			}
			catch
			{
				DisposeCharacters();
				throw;
			}
		}

		private TabletopCard RestoreCardFromSnapshot(TabletopCardSnapshot snapshot)
		{
			CardDefinition definition = RequireCardDefinition(snapshot.ContentId, $"恢复牌桌卡牌 {snapshot.CardId}");
			TabletopCard card = definition.RestoreRuntimeCard(snapshot);
			card.SetAutomaticBehaviorInitialDelays(
				snapshot.PeriodicProductionInitialDelaySeconds,
				snapshot.AutomaticMovementInitialDelaySeconds);
			return card;
		}

		private TabletopCard CreateRuntimeCard(CardDefinition definition, TabletopCardId id)
		{
			TabletopCard card = definition.CreateRuntimeCard(id);
			card.SetAutomaticBehaviorInitialDelays(
				definition.HasPeriodicProduction ? NextAutomaticBehaviorInitialDelay() : 0f,
				definition.UsesAutomaticMovement ? NextAutomaticBehaviorInitialDelay() : 0f);
			return card;
		}

		private float NextAutomaticBehaviorInitialDelay()
		{
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException(
					"创建带自动行为的卡牌前必须先初始化牌桌权威随机流。");
			}
			return m_authoritativeRandom.NextFloat(
				AutomaticBehaviorInitialDelayMinSeconds,
				AutomaticBehaviorInitialDelayMaxSeconds);
		}

		/// <summary>
		/// 通过已恢复的卡牌状态和活动行动快照重建牌桌；恢复失败时不会发布任何活动行动。
		/// </summary>
		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardStateSnapshot cardStateSnapshot,
			TabletopCardPlacementRules placementRules,
			IReadOnlyList<ActionInstanceSnapshot> actionSnapshots,
			Func<ContentId, bool> isContentDiscovered,
			Action<ContentId, ActionSettlementResult> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
			: this(
				contentIndex,
				cardStateSnapshot,
				placementRules,
				actionSnapshots,
				isContentDiscovered,
				CreateLegacyActionCompletionCallback(actionCompleted),
				cardsDefeated,
				battleFormationRules,
				cardIdSequence)
		{
		}

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardStateSnapshot cardStateSnapshot,
			TabletopCardPlacementRules placementRules,
			IReadOnlyList<ActionInstanceSnapshot> actionSnapshots,
			Func<ContentId, bool> isContentDiscovered,
			Action<TabletopActionCompletion> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
			: this(
				contentIndex,
				cardStateSnapshot,
				placementRules,
				isContentDiscovered,
				actionCompleted,
				cardsDefeated,
				battleFormationRules,
				cardIdSequence)
		{
			try
			{
				RestoreActiveActions(actionSnapshots);
			}
			catch
			{
				DisposeCharacters();
				throw;
			}
		}

		public TabletopCard CreateCard(
			ContentId contentId,
			Vector2 position,
			bool isPlacementLocked = false,
			bool allowSpawnAttach = false,
			TabletopCardId spawnAttachIgnoredStackCardId = default)
		{
			RequireActive();
			CardDefinition definition = RequireCardDefinition(contentId, "创建卡牌");
			if (allowSpawnAttach)
			{
				TabletopCardStack spawnedStack = Cards.CreateCardStackAtRequestedPosition(
					contentId,
					1,
					position,
					isPlacementLocked,
					definition.InitialUses,
					id => CreateRuntimeCard(definition, id));
				TabletopCard spawnedCard = spawnedStack.BottomCard;
				FinishSpawnAttachedStack(
					spawnedStack,
					position,
					spawnAttachIgnoredStackCardId,
					placementLockedStackCardId: default,
					usesDragHeight: false);
				return spawnedCard;
			}

			TabletopCard card = Cards.CreateCard(
				contentId,
				position,
				PlacementRules,
				isPlacementLocked,
				definition.InitialUses,
				id => CreateRuntimeCard(definition, id));
			RefreshPlacementRulesForCurrentCards(reflowExistingStacks: false);
			return card;
		}

		public TabletopCardStack CreateCardStack(
			ContentId contentId,
			int count,
			Vector2 position,
			bool isPlacementLocked = false,
			bool allowSpawnAttach = false,
			TabletopCardId spawnAttachIgnoredStackCardId = default,
			TabletopCardId placementLockedStackCardId = default,
			float spawnPresentationHeightOffset = 0f,
			TabletopCardId spawnPresentationOriginCardId = default,
			bool useDragHeightForSpawn = false)
		{
			RequireActive();
			CardDefinition definition = RequireCardDefinition(contentId, "创建牌堆");
			if (allowSpawnAttach)
			{
				TabletopCardStack spawnedStack = Cards.CreateCardStackAtRequestedPosition(
					contentId,
					count,
					position,
					isPlacementLocked,
					definition.InitialUses,
					id => CreateRuntimeCard(definition, id));
				return FinishSpawnAttachedStack(
					spawnedStack,
					position,
					spawnAttachIgnoredStackCardId,
					placementLockedStackCardId,
					usesDragHeight: useDragHeightForSpawn,
					spawnPresentationHeightOffset,
					spawnPresentationOriginCardId);
			}

			TabletopCardStack stack = Cards.CreateCardStack(
				contentId,
				count,
				position,
				PlacementRules,
				isPlacementLocked,
				definition.InitialUses,
				id => CreateRuntimeCard(definition, id),
				placementLockedStackCardId);
			RefreshPlacementRulesForCurrentCards(reflowExistingStacks: false);
			return stack;
		}

		private TabletopCardStack FinishSpawnAttachedStack(
			TabletopCardStack spawnedStack,
			Vector2 requestedPosition,
			TabletopCardId spawnAttachIgnoredStackCardId,
			TabletopCardId placementLockedStackCardId,
			bool usesDragHeight,
			float spawnPresentationHeightOffset = 0f,
			TabletopCardId spawnPresentationOriginCardId = default)
		{
			RequestCardSpawnPresentation(
				spawnedStack,
				requestedPosition,
				usesDragHeight,
				spawnPresentationHeightOffset,
				spawnPresentationOriginCardId);
			TabletopCardStack result = AttachSpawnedStackToNearestStackableStack(
				spawnedStack,
				spawnAttachIgnoredStackCardId);
			RefreshPlacementRulesForCurrentCards(reflowExistingStacks: false);
			Cards.ReflowPlacementAfterSpawn(PlacementRules, placementLockedStackCardId);
			return result;
		}

		private void RequestCardSpawnPresentation(
			TabletopCardStack stack,
			Vector2 requestedPosition,
			bool usesDragHeight,
			float spawnPresentationHeightOffset = 0f,
			TabletopCardId spawnPresentationOriginCardId = default)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}
			if (!float.IsFinite(spawnPresentationHeightOffset) || spawnPresentationHeightOffset < 0f)
			{
				throw new ArgumentException(
					"卡牌出生表现高度偏移必须是大于等于 0 的有限值。",
					nameof(spawnPresentationHeightOffset));
			}
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				Vector2 cardSpawnPosition = requestedPosition + PlacementRules.Geometry.StackStep * cardIndex;
				RequestPresentationCue(TabletopPresentationCue.CardSpawn(
					stack.Cards[cardIndex].Id,
					cardSpawnPosition,
					usesDragHeight,
					spawnPresentationHeightOffset,
					spawnPresentationOriginCardId));
			}
		}

		private TabletopCardStack AttachSpawnedStackToNearestStackableStack(
			TabletopCardStack spawnedStack,
			TabletopCardId ignoredStackCardId)
		{
			if (spawnedStack == null)
			{
				throw new ArgumentNullException(nameof(spawnedStack));
			}
			float radius = PlacementRules.SpawnAttachRadius;
			if (radius == 0f)
			{
				return spawnedStack;
			}
			if (spawnedStack.IsPlacementLocked)
			{
				throw new InvalidOperationException("锁定牌堆不能启用出生吸附。");
			}
			if (!TryFindNearestSpawnAttachTarget(
				spawnedStack,
				ignoredStackCardId,
				radius,
				out TabletopCardStack targetStack))
			{
				return spawnedStack;
			}

			return MergeStackOnto(spawnedStack.BottomCard.Id, targetStack.BottomCard.Id);
		}

		private bool TryFindNearestSpawnAttachTarget(
			TabletopCardStack spawnedStack,
			TabletopCardId ignoredStackCardId,
			float radius,
			out TabletopCardStack targetStack)
		{
			targetStack = null;
			float radiusSquared = radius * radius;
			float bestSqrDistance = float.PositiveInfinity;
			CardDefinition spawnedDefinition = RequireCardDefinition(
				spawnedStack.TopCard.ContentId,
				"判断出生吸附来源");
			Vector2 spawnedPosition = spawnedStack.Position;
			IReadOnlyList<TabletopCardStack> stacks = Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				TabletopCardStack candidateStack = stacks[stackIndex];
				if (ReferenceEquals(candidateStack, spawnedStack) ||
					StackContainsCard(candidateStack, ignoredStackCardId) ||
					StackContainsBattleCard(candidateStack) ||
					!CanSpawnedStackJoinTarget(spawnedStack, candidateStack))
				{
					continue;
				}
				CardDefinition targetBottomDefinition = RequireCardDefinition(
					candidateStack.BottomCard.ContentId,
					"判断出生吸附目标");
				if (!PlacementRules.StackingRules.CanStack(spawnedDefinition, targetBottomDefinition))
				{
					continue;
				}

				if (!TryGetStackCraftSpawnAttachCandidateCenterSqrDistance(
						candidateStack,
						spawnedPosition,
						radiusSquared,
						out float candidateSqrDistance))
				{
					continue;
				}

				if (candidateSqrDistance < bestSqrDistance)
				{
					bestSqrDistance = candidateSqrDistance;
					targetStack = candidateStack;
				}
			}
			return targetStack != null;
		}

		private bool StackContainsCard(TabletopCardStack stack, TabletopCardId cardId)
		{
			return cardId.IsValid && stack.IndexOf(cardId) >= 0;
		}

		private bool StackContainsBattleCard(TabletopCardStack stack)
		{
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				TabletopCardId cardId = stack.Cards[cardIndex].Id;
				if (TryFindBattleContaining(cardId, out _))
				{
					return true;
				}
			}
			return false;
		}

		private bool CanSpawnedStackJoinTarget(
			TabletopCardStack spawnedStack,
			TabletopCardStack targetStack)
		{
			if (!TryFindActiveActionOwnedByStack(targetStack, out ActionInstance action))
			{
				return true;
			}
			return CanRunningActionAcceptAdditionalStack(action, spawnedStack, spawnedStack.TopCard.Id);
		}

		private bool TryGetStackCraftSpawnAttachCandidateCenterSqrDistance(
			TabletopCardStack stack,
			Vector2 spawnedPosition,
			float radiusSquared,
			out float centerSqrDistance)
		{
			bool overlapsAttachSphere = false;
			centerSqrDistance = float.PositiveInfinity;
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				TabletopCard candidateCard = stack.Cards[cardIndex];
				Vector2 candidateCardPosition = Cards.GetCardTablePosition(candidateCard.Id, PlacementRules.Geometry);
				Vector2 candidateCardSize = ResolveCardSize(candidateCard.ContentId, PlacementRules.Geometry.CardSize);
				// StackCraft 的出生吸附先用 OverlapSphere 碰到卡牌碰撞体，再按卡牌中心距离选择最近堆。
				if (CardFootprintSqrDistance(
						candidateCardPosition,
						candidateCardSize,
						spawnedPosition) <= radiusSquared)
				{
					overlapsAttachSphere = true;
				}

				float sqrDistance = (candidateCardPosition - spawnedPosition).sqrMagnitude;
				if (sqrDistance < centerSqrDistance)
				{
					centerSqrDistance = sqrDistance;
				}
			}
			return overlapsAttachSphere && float.IsFinite(centerSqrDistance);
		}

		private static float CardFootprintSqrDistance(
			Vector2 cardPosition,
			Vector2 cardSize,
			Vector2 position)
		{
			Vector2 halfSize = cardSize * 0.5f;
			float dx = Mathf.Max(Mathf.Abs(position.x - cardPosition.x) - halfSize.x, 0f);
			float dy = Mathf.Max(Mathf.Abs(position.y - cardPosition.y) - halfSize.y, 0f);
			return dx * dx + dy * dy;
		}

		public void RemoveCard(TabletopCardId cardId)
		{
			RequireActive();
			if (!Cards.TryGetCard(cardId, out TabletopCard card))
			{
				throw new KeyNotFoundException($"牌桌中不存在局内卡牌 {cardId}。");
			}
			if (TryFindBattleContaining(cardId, out _))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 仍属于活动战斗，必须先离开战斗或结束战斗后才能移除。");
			}
			UnbindCardFromActionPlans(cardId);
			Cards.RemoveCard(cardId);
			RefreshPlacementRulesForCurrentCards(reflowExistingStacks: true);
			if (card is CharacterCard characterCard)
			{
				characterCard.Dispose();
			}
		}

		/// <summary>使用一张卡牌一次；最后一次使用直接通过牌桌正式移除链提交。</summary>
		internal void UseCard(TabletopCardId cardId)
		{
			RequireActive();
			if (!Cards.TryGetCard(cardId, out TabletopCard card))
			{
				throw new KeyNotFoundException($"牌桌中不存在局内卡牌 {cardId}。");
			}
			if (card.RemainingUses == 1)
			{
				RemoveCard(cardId);
				return;
			}

			Cards.ConsumeUse(cardId);
		}

		/// <summary>
		/// 将当前地区中的卡牌实例迁移到另一个地区牌桌。迁移保留原卡牌对象与角色 GAS 状态。
		/// </summary>
		internal void TransferCardsTo(
			Tabletop target,
			IReadOnlyList<TabletopCardId> cardIds,
			IReadOnlyList<Vector2> targetPositions)
		{
			RequireCardsCanTransferTo(target, cardIds, targetPositions);
			Cards.TransferCardsTo(target.Cards, cardIds, targetPositions, target.PlacementRules);
			RefreshPlacementRulesForCurrentCards(reflowExistingStacks: true);
			target.RefreshPlacementRulesForCurrentCards(reflowExistingStacks: false);
		}

		internal void RequireCardsCanTransferTo(
			Tabletop target,
			IReadOnlyList<TabletopCardId> cardIds,
			IReadOnlyList<Vector2> targetPositions)
		{
			RequireActive();
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}
			target.RequireActive();
			if (ReferenceEquals(this, target))
			{
				throw new InvalidOperationException("旅行目标必须是另一个剧本地区。");
			}
			if (cardIds == null)
			{
				throw new ArgumentNullException(nameof(cardIds));
			}
			if (targetPositions == null || targetPositions.Count != cardIds.Count)
			{
				throw new ArgumentException("旅行卡牌与目标位置数量必须一致。", nameof(targetPositions));
			}

			List<TabletopCard> cards = new List<TabletopCard>(cardIds.Count);
			HashSet<TabletopCardId> uniqueIds = new HashSet<TabletopCardId>();
			for (int i = 0; i < cardIds.Count; i++)
			{
				TabletopCardId cardId = cardIds[i];
				if (!uniqueIds.Add(cardId))
				{
					throw new InvalidOperationException($"旅行卡牌列表重复引用局内卡牌 {cardId}。");
				}
				if (!Cards.TryGetCard(cardId, out TabletopCard card))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 不属于当前地区牌桌。");
				}
				if (TryFindBattleContaining(cardId, out _))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 仍属于活动战斗，不能离开当前地区。");
				}
				if (IsActiveActionParticipant(cardId))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 仍参与活动行动，不能离开当前地区。");
				}
				if (IsActionPlanParticipant(cardId))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 仍填在待确认行动计划中，不能离开当前地区。");
				}
				cards.Add(card);
			}

			target.Cards.RequireCanAcceptCards(cards, targetPositions, target.PlacementRules);
		}

		internal void RequireCardChangesCanBeCommitted(
			IReadOnlyList<TabletopCardId> removalCardIds,
			IReadOnlyList<TabletopCardCreationRequest> creations)
		{
			RequireCardChangesCanBeCommitted(
				removalCardIds,
				creations,
				Array.Empty<TabletopCardRestorationRequest>());
		}

		internal void RequireCardChangesCanBeCommitted(
			IReadOnlyList<TabletopCardId> removalCardIds,
			IReadOnlyList<TabletopCardCreationRequest> creations,
			IReadOnlyList<TabletopCardRestorationRequest> restorations)
		{
			RequireCardChangesCanBeCommitted(
				removalCardIds,
				creations,
				restorations,
				requirePlacementConverged: true);
		}

		internal void RequireCardChangesCanBeCommitted(
			IReadOnlyList<TabletopCardId> removalCardIds,
			IReadOnlyList<TabletopCardCreationRequest> creations,
			IReadOnlyList<TabletopCardRestorationRequest> restorations,
			bool requirePlacementConverged)
		{
			RequireActive();
			if (removalCardIds == null)
			{
				throw new ArgumentNullException(nameof(removalCardIds));
			}
			if (creations == null)
			{
				throw new ArgumentNullException(nameof(creations));
			}
			if (restorations == null)
			{
				throw new ArgumentNullException(nameof(restorations));
			}

			for (int i = 0; i < removalCardIds.Count; i++)
			{
				TabletopCardId cardId = removalCardIds[i];
				if (!Cards.TryGetCard(cardId, out _))
				{
					throw new InvalidOperationException($"牌桌变更引用了不存在的局内卡牌 {cardId}。");
				}
				if (TryFindBattleContaining(cardId, out _))
				{
					throw new InvalidOperationException(
						$"牌桌卡牌 {cardId} 仍属于活动战斗，不能提交移除它的行动结果。");
				}
			}
			for (int i = 0; i < creations.Count; i++)
			{
				TabletopCardCreationRequest creation = creations[i];
				RequireCardDefinition(creation.ContentId, "创建行动产物");
				if (creation.Count <= 0)
				{
					throw new InvalidOperationException("行动产物牌堆必须至少包含一张卡牌。");
				}
			}
			for (int i = 0; i < restorations.Count; i++)
			{
				TabletopCardRestorationRequest restoration = restorations[i];
				TabletopCardSnapshot snapshot = restoration.Snapshot;
				if (snapshot == null || !snapshot.ContentId.IsValid)
				{
					throw new InvalidOperationException("牌桌恢复请求缺少有效卡牌快照。");
				}
				RequireCardDefinition(snapshot.ContentId, "恢复离桌卡牌");
			}
			Cards.RequireCardChangesCanBePlaced(
				removalCardIds,
				creations,
				restorations,
				PlacementRules,
				requirePlacementConverged);
		}

		internal void RestoreCardSnapshot(TabletopCardSnapshot snapshot, Vector2 position)
		{
			RequireActive();
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}
			RequireCardDefinition(snapshot.ContentId, "恢复离桌卡牌");
			Cards.RestoreCardSnapshot(snapshot, position, PlacementRules, RestoreCardFromSnapshot);
			RefreshPlacementRulesForCurrentCards(reflowExistingStacks: false);
		}

		/// <summary>
		/// 创建活动战斗并冻结本次战斗方分组。角色阵营与敌我关系必须在调用前由正式规则解析，
		/// 牌桌只保存这场战斗的临时分组，不复制 GAS 阵营标签。
		/// </summary>
		public Battle StartBattle(params IReadOnlyList<TabletopCardId>[] sideRosters)
		{
			RequireActive();
			if (m_nextBattleId == ulong.MaxValue)
			{
				throw new InvalidOperationException("本局牌桌战斗 ID 已耗尽。");
			}

			if (m_battleFormation == null)
			{
				throw new InvalidOperationException("当前剧本没有配置战斗阵型规则，不能创建战斗。");
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("本局牌桌尚未初始化权威随机流，不能创建战斗。");
			}

			Unity.Mathematics.Random candidateRandom = m_authoritativeRandom;
			uint battleSeed = candidateRandom.NextUInt(1u, uint.MaxValue);
			Battle battle = new Battle(new BattleId(m_nextBattleId), sideRosters, battleSeed);
			m_battleFormation.ValidateBattle(battle);
			for (int sideIndex = 0; sideIndex < battle.Sides.Count; sideIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[sideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					TabletopCardId cardId = cardIds[cardIndex];
					if (!Cards.TryGetCard(cardId, out _))
					{
						throw new InvalidOperationException(
							$"战斗参战对象 {cardId} 不属于当前牌桌，不能创建战斗。");
					}
					if (Cards.TryGetCard(cardId, out TabletopCard participantCard) &&
						participantCard is not CharacterCard)
					{
						throw new InvalidOperationException(
							$"战斗参战对象 {cardId} 不是拥有唯一 EX-GAS 状态的角色卡，不能加入战斗。");
					}
					if (IsActiveActionParticipant(cardId))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardId} 仍参与活动行动，必须先完成或取消行动后才能加入战斗。");
					}
					if (IsActionPlanParticipant(cardId))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardId} 已填在待确认行动计划中，必须先提交或取消该计划后才能加入战斗。");
					}
					if (TryFindBattleContaining(cardId, out _))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardId} 已属于活动战斗，不能重复加入战斗。");
					}
				}
			}
			battle.InitializeAreaCenter(CalculateBattleAreaCenter(battle));
			Rect candidateArea = CalculateBattleArea(battle);
			List<Battle> overlappingBattles = FindOverlappingBattles(candidateArea);
			for (int index = 0; index < overlappingBattles.Count; index++)
			{
				RequireMatchingBattleSides(overlappingBattles[index], battle);
			}

			m_activeBattles.Add(battle);
			m_authoritativeRandom = candidateRandom;
			m_nextBattleId++;
			if (overlappingBattles.Count > 0)
			{
				Battle destination = overlappingBattles[0];
				int[] sideMapping = CreateIdentitySideMapping(battle.SideCount);
				MergeBattlesUnchecked(destination, battle, sideMapping);
				for (int index = 1; index < overlappingBattles.Count; index++)
				{
					MergeBattlesUnchecked(destination, overlappingBattles[index], sideMapping);
				}
				battle = destination;
			}
			m_battleRevision++;
			return battle;
		}

		/// <summary>
		/// 把一张角色卡加入既有战斗的指定战斗方。敌我关系由调用方的剧本规则决定，
		/// 牌桌只校验当前参战事实并提交成员变化。
		/// </summary>
		public void JoinBattle(Battle battle, int sideIndex, TabletopCardId cardId)
		{
			RequireActive();
			RequireActiveBattle(battle);
			if (sideIndex < 0 || sideIndex >= battle.SideCount)
			{
				throw new ArgumentOutOfRangeException(
					nameof(sideIndex),
					sideIndex,
					$"战斗方索引必须位于 0 到 {battle.SideCount - 1} 之间。");
			}
			if (!Cards.TryGetCard(cardId, out TabletopCard card))
			{
				throw new InvalidOperationException($"增援对象 {cardId} 不属于当前牌桌，不能加入战斗。");
			}
			if (card is not CharacterCard)
			{
				throw new InvalidOperationException(
					$"增援对象 {cardId} 不是拥有唯一 EX-GAS 状态的角色卡，不能加入战斗。");
			}
			if (IsActiveActionParticipant(cardId))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 仍参与活动行动，必须先完成或取消行动后才能加入战斗。");
			}
			if (IsActionPlanParticipant(cardId))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 已填在待确认行动计划中，必须先提交或取消该计划后才能加入战斗。");
			}
			if (TryFindBattleContaining(cardId, out Battle existingBattle))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 已属于活动战斗 {existingBattle.Id}，不能重复加入战斗。");
			}

			Rect expandedArea = CalculateBattleArea(battle, sideIndex);
			List<Battle> overlappingBattles = FindOverlappingBattles(expandedArea, battle);
			for (int index = 0; index < overlappingBattles.Count; index++)
			{
				RequireMatchingBattleSides(battle, overlappingBattles[index]);
			}

			battle.AddParticipant(sideIndex, cardId);
			if (overlappingBattles.Count > 0)
			{
				int[] sideMapping = CreateIdentitySideMapping(battle.SideCount);
				for (int index = 0; index < overlappingBattles.Count; index++)
				{
					MergeBattlesUnchecked(battle, overlappingBattles[index], sideMapping);
				}
			}
			m_battleRevision++;
		}

		/// <summary>
		/// 判断一次普通拖拽释放是否会按 StackCraft 语义由两个相邻敌对牌堆发起战斗。
		/// </summary>
		public bool CanStartBattleOnDrop(TabletopCardId sourceCardId, TabletopCardId targetCardId)
		{
			RequireActive();
			if (!TryResolveManualBattleStartStacks(
				sourceCardId,
				targetCardId,
				out TabletopCardStack sourceStack,
				out TabletopCardStack targetStack,
				out int sourceFactionTagCode,
				out int targetFactionTagCode))
			{
				return false;
			}

			return HasManualBattleCharactersInStack(sourceStack, sourceFactionTagCode) &&
				HasManualBattleCharactersInStack(targetStack, targetFactionTagCode);
		}

		/// <summary>
		/// 把拖拽牌堆释放到既有战斗区域内时，将牌堆中可参战角色加入对应战斗方。
		/// StackCraft 使用被拖拽卡牌自身的实际位置判断区域，不使用鼠标释放点。
		/// </summary>
		public bool TryJoinBattleAtPosition(
			TabletopCardId sourceCardId,
			Vector2 cardDropPosition,
			out Battle battle)
		{
			RequireActive();
			if (!sourceCardId.IsValid)
			{
				throw new ArgumentException("加入战斗的拖拽来源必须引用有效牌桌卡牌。", nameof(sourceCardId));
			}
			if (!IsFinitePosition(cardDropPosition))
			{
				throw new ArgumentException("加入战斗的卡牌实际落点必须是有限二维坐标。", nameof(cardDropPosition));
			}

			battle = null;
			if (!Cards.TryGetCard(sourceCardId, out TabletopCard sourceCard))
			{
				throw new KeyNotFoundException($"牌桌中不存在拖拽来源卡牌 {sourceCardId}。");
			}
			int sourceFactionTagCode = ResolveBattleFactionTag(sourceCard, "拖拽来源卡牌");
			if (!IsSupportedManualBattleFaction(sourceFactionTagCode) ||
				!TryFindBattleAtPosition(cardDropPosition, out Battle targetBattle))
			{
				return false;
			}

			TabletopCardStack sourceStack = Cards.GetStackContaining(sourceCardId);
			List<TabletopCardId> playerCards = new List<TabletopCardId>();
			List<TabletopCardId> enemyCards = new List<TabletopCardId>();
			CollectManualBattleCharactersInStack(sourceStack, XTag.Faction_Player, playerCards);
			CollectManualBattleCharactersInStack(sourceStack, XTag.Faction_Enemy, enemyCards);

			int playerSideIndex = -1;
			int enemySideIndex = -1;
			if (playerCards.Count > 0)
			{
				if (!TryResolveBattleFactionSide(targetBattle, XTag.Faction_Player, out playerSideIndex))
				{
					return false;
				}
			}
			if (enemyCards.Count > 0)
			{
				if (!TryResolveBattleFactionSide(targetBattle, XTag.Faction_Enemy, out enemySideIndex))
				{
					return false;
				}
			}

			bool joinedAny = false;
			if (playerCards.Count > 0)
			{
				JoinBattleCards(targetBattle, playerSideIndex, playerCards);
				joinedAny = true;
			}
			if (enemyCards.Count > 0)
			{
				JoinBattleCards(targetBattle, enemySideIndex, enemyCards);
				joinedAny = true;
			}

			battle = joinedAny ? targetBattle : null;
			return joinedAny;
		}

		/// <summary>
		/// 把拖拽牌堆释放到敌对牌堆上时，按 StackCraft 普通释放链在合堆前发起战斗。
		/// </summary>
		public bool TryStartBattleOnDrop(
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			out Battle battle)
		{
			RequireActive();
			battle = null;
			if (!TryResolveManualBattleStartStacks(
				sourceCardId,
				targetCardId,
				out TabletopCardStack sourceStack,
				out TabletopCardStack targetStack,
				out int sourceFactionTagCode,
				out int targetFactionTagCode))
			{
				return false;
			}

			List<TabletopCardId> sourceSide = new List<TabletopCardId>();
			List<TabletopCardId> targetSide = new List<TabletopCardId>();
			CollectManualBattleCharactersInStack(sourceStack, sourceFactionTagCode, sourceSide);
			CollectManualBattleCharactersInStack(targetStack, targetFactionTagCode, targetSide);
			if (sourceSide.Count == 0 || targetSide.Count == 0)
			{
				return false;
			}

			battle = StartBattle(sourceSide, targetSide);
			return true;
		}

		/// <summary>
		/// 将来源战斗的各方按调用方给出的映射并入目标战斗，并保留目标战斗的身份和权威随机流。
		/// 敌我关系仍由剧本规则在调用前确定，牌桌不会根据 GAS 标签猜测分组。
		/// </summary>
		public void MergeBattles(
			Battle destination,
			Battle source,
			IReadOnlyList<int> sourceSideToDestinationSide)
		{
			RequireActive();
			RequireActiveBattle(destination);
			RequireActiveBattle(source);
			if (ReferenceEquals(destination, source))
			{
				throw new InvalidOperationException("不能把一场战斗并入自身。");
			}
			if (sourceSideToDestinationSide == null)
			{
				throw new ArgumentNullException(nameof(sourceSideToDestinationSide));
			}
			if (sourceSideToDestinationSide.Count != source.SideCount)
			{
				throw new ArgumentException(
					$"来源战斗包含 {source.SideCount} 个战斗方，必须为每个来源方提供一个目标方映射。",
					nameof(sourceSideToDestinationSide));
			}

			for (int sourceSideIndex = 0; sourceSideIndex < source.SideCount; sourceSideIndex++)
			{
				int destinationSideIndex = sourceSideToDestinationSide[sourceSideIndex];
				if (destinationSideIndex < 0 || destinationSideIndex >= destination.SideCount)
				{
					throw new ArgumentOutOfRangeException(
						nameof(sourceSideToDestinationSide),
						destinationSideIndex,
						$"来源战斗方 {sourceSideIndex} 映射的目标方索引必须位于 0 到 {destination.SideCount - 1} 之间。");
				}

				IReadOnlyList<TabletopCardId> cardIds = source.Sides[sourceSideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					TabletopCardId cardId = cardIds[cardIndex];
					if (!Cards.TryGetCard(cardId, out TabletopCard card) || card is not CharacterCard)
					{
						throw new InvalidOperationException(
							$"来源战斗 {source.Id} 的参与对象 {cardId} 不再是当前牌桌中的角色卡，战斗状态已损坏。");
					}
					if (!TryFindBattleContaining(cardId, out Battle participantBattle) ||
						!ReferenceEquals(participantBattle, source))
					{
						throw new InvalidOperationException(
							$"来源战斗 {source.Id} 的参与对象 {cardId} 不再唯一属于该战斗，战斗状态已损坏。");
					}
					if (destination.HasParticipant(cardId))
					{
						throw new InvalidOperationException(
							$"目标战斗 {destination.Id} 已包含来源参与对象 {cardId}。");
					}
				}
			}

			MergeBattlesUnchecked(destination, source, sourceSideToDestinationSide);
			m_battleRevision++;
		}

		private void MergeBattlesUnchecked(
			Battle destination,
			Battle source,
			IReadOnlyList<int> sourceSideToDestinationSide)
		{
			destination.AbsorbAreaCenter(source);
			for (int sourceSideIndex = 0; sourceSideIndex < source.SideCount; sourceSideIndex++)
			{
				int destinationSideIndex = sourceSideToDestinationSide[sourceSideIndex];
				IReadOnlyList<TabletopCardId> cardIds = source.Sides[sourceSideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					destination.AddParticipant(destinationSideIndex, cardIds[cardIndex]);
				}
			}

			source.End();
			m_activeBattles.Remove(source);
		}

		/// <summary>从所属战斗移除一张牌；只剩一个有成员的战斗方时，战斗随之结束。</summary>
		public void LeaveBattle(Battle battle, TabletopCardId cardId)
		{
			RequireActive();
			RequireActiveBattle(battle);
			battle.RemoveParticipant(cardId);
			m_battleRevision++;
			if (battle.ParticipantCount < 2 || battle.ActiveSideCount < 2)
			{
				EndBattle(battle);
			}
		}

		/// <summary>
		/// 解释参战卡牌的一次释放：落在战斗区域内保持参战，落在区域外则离战并提交牌堆放置。
		/// StackCraft 使用被拖拽卡牌自身的实际位置判断区域和离场落点，不使用鼠标释放点。
		/// </summary>
		public bool TryDropBattleParticipant(
			TabletopCardId cardId,
			Vector2 cardDropPosition,
			out bool leftBattle,
			out TabletopCardStack placedStack)
		{
			RequireActive();
			if (!cardId.IsValid)
			{
				throw new ArgumentException("战斗释放必须引用有效的局内卡牌。", nameof(cardId));
			}
			if (!IsFinitePosition(cardDropPosition))
			{
				throw new ArgumentException("战斗释放的卡牌实际落点必须是有限二维坐标。", nameof(cardDropPosition));
			}

			leftBattle = false;
			placedStack = null;
			if (!TryFindBattleContaining(cardId, out Battle battle))
			{
				return false;
			}

			if (CalculateBattleArea(battle).Contains(cardDropPosition))
			{
				return true;
			}

			RequireNoOtherBattleParticipantInDetachedTail(cardId, "逃离战斗");
			if (!Cards.CanPlaceStack(cardId, cardDropPosition, PlacementRules))
			{
				return true;
			}

			battle.RemoveParticipant(cardId);
			if (battle.ParticipantCount < 2 || battle.ActiveSideCount < 2)
			{
				EndBattle(battle);
			}
			else
			{
				m_battleRevision++;
			}

			if (!Cards.TryPlaceStack(cardId, cardDropPosition, PlacementRules, out placedStack))
			{
				throw new InvalidOperationException("战斗离场放置的预演已经通过，但正式提交失败。");
			}
			leftBattle = true;
			return true;
		}

		/// <summary>结束并移除一场活动战斗。</summary>
		public void EndBattle(Battle battle)
		{
			RequireActive();
			RequireActiveBattle(battle);
			battle.End();
			m_activeBattles.Remove(battle);
			m_battleRevision++;
		}

		/// <summary>
		/// 请求一张参战角色卡向本场另一参战角色卡激活已有 EX-GAS Ability。
		/// 标签、消耗、冷却、Timeline 和效果结算仍完全由 EX-GAS 负责。
		/// </summary>
		public AbilityActivationResult RequestBattleAbilityActivation(
			Battle battle,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			int abilityCode)
		{
			return ActivateBattleAbility(
				battle,
				sourceCardId,
				targetCardId,
				abilityCode,
				tracksAutomaticTurn: false);
		}

		private AbilityActivationResult ActivateBattleAbility(
			Battle battle,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			int abilityCode,
			bool tracksAutomaticTurn)
		{
			RequireActive();
			RequireActiveBattle(battle);
			CharacterCard source = RequireBattleCharacter(battle, sourceCardId, "施法者");
			CharacterCard target = RequireBattleCharacter(battle, targetCardId, "目标");
			AbilitySpec ability = source.AbilitySystem.GetAbilitySpec(abilityCode)
				?? throw new InvalidOperationException(
					$"参战角色卡 {sourceCardId} 没有 Ability {abilityCode}，不能提交战斗能力请求。");

			AbilityActivationResult result = ability.CheckActivation();
			if (result != AbilityActivationResult.Success)
			{
				return result;
			}

			Vector2 sourcePosition = source.Position;
			if (tracksAutomaticTurn)
			{
				int combatTypeTagCode = ResolveAttackCombatTypeTagCode(source.AbilitySystem);
				float preActivationSeconds = GetPreActivationSeconds(combatTypeTagCode);
				battle.BeginTurn(
					sourceCardId,
					targetCardId,
					ability,
					combatTypeTagCode,
					preActivationSeconds);
				if (preActivationSeconds > 0f)
				{
					return result;
				}
			}
			try
			{
				ActivateBattleAbilityNow(
					battle,
					sourcePosition,
					target.AbilitySystem,
					ability);
			}
			catch
			{
				if (tracksAutomaticTurn)
				{
					battle.AbortTurn();
				}
				throw;
			}
			return result;
		}

		private static int ResolveAttackCombatTypeTagCode(AbilitySystemCell abilitySystem)
		{
			if (abilitySystem == null)
			{
				throw new ArgumentNullException(nameof(abilitySystem));
			}

			bool isMelee = abilitySystem.HasTag(XTag.Combat_Melee);
			bool isRanged = abilitySystem.HasTag(GAS.Runtime.XTag.Combat_Ranged);
			bool isMagic = abilitySystem.HasTag(GAS.Runtime.XTag.Combat_Magic);
			int matchedCount = (isMelee ? 1 : 0) + (isRanged ? 1 : 0) + (isMagic ? 1 : 0);
			if (matchedCount > 1)
			{
				throw new InvalidOperationException(
					"角色 ASC 同时拥有多个 Combat.* 战斗类型标签，无法确定本次自动攻击表现。");
			}

			if (isRanged)
			{
				return GAS.Runtime.XTag.Combat_Ranged;
			}
			if (isMagic)
			{
				return GAS.Runtime.XTag.Combat_Magic;
			}
			return isMelee ? XTag.Combat_Melee : 0;
		}

		private static float GetPreActivationSeconds(int combatTypeTagCode)
		{
			return combatTypeTagCode == GAS.Runtime.XTag.Combat_Ranged ||
				combatTypeTagCode == GAS.Runtime.XTag.Combat_Magic
					? ProjectileAttackPreActivationSeconds
					: 0f;
		}

		private void ActivateBattleAbilityNow(
			Battle battle,
			Vector2 sourcePosition,
			AbilitySystemCell targetAbilitySystem,
			AbilitySpec ability)
		{
			ability.TryActivate(new AbilityActivationContext(
				TabletopCoordinateSpace.ToLocalPosition(sourcePosition),
				targetAbilitySystem,
				battle.TakeAbilityActivationSeed()));
		}

		private void ActivatePendingBattleAbility(
			Battle battle,
			BattlePendingAbilityActivation activation)
		{
			if (!Cards.TryGetCard(activation.SourceCardId, out TabletopCard sourceCard) ||
				sourceCard is not CharacterCard source)
			{
				battle.AbortTurn();
				return;
			}
			if (!Cards.TryGetCard(activation.TargetCardId, out TabletopCard targetCard) ||
				targetCard is not CharacterCard target)
			{
				battle.AbortTurn();
				return;
			}

			try
			{
				ActivateBattleAbilityNow(
					battle,
					source.Position,
					target.AbilitySystem,
					activation.Ability);
			}
			catch
			{
				battle.AbortTurn();
				throw;
			}
		}

		public TabletopCardStack MergeStackOnto(TabletopCardId sourceCardId, TabletopCardId targetCardId)
		{
			RequireActive();
			RequireNoBattleParticipantInAffectedStack(sourceCardId, "合并牌堆");
			RequireNoBattleParticipantInAffectedStack(targetCardId, "合并牌堆");
			return Cards.MergeStackOnto(sourceCardId, targetCardId);
		}

		/// <summary>
		/// 判断一次玩家拖拽释放是否允许按地区牌桌合堆规则合并到目标堆。
		/// </summary>
		public bool CanStackOnto(TabletopCardId sourceCardId, TabletopCardId targetCardId)
		{
			RequireActive();
			TabletopCardStack sourceStack = Cards.GetStackContaining(sourceCardId);
			TabletopCardStack targetStack = Cards.GetStackContaining(targetCardId);
			if (!CanUseTargetStackForDraggedSegment(sourceStack, sourceCardId, targetStack, targetCardId))
			{
				return false;
			}
			if (IsStackCraftTradeZoneStack(targetStack))
			{
				return false;
			}
			if (TryFindActiveActionOwnedByStack(targetStack, out ActionInstance targetAction) &&
				!IsDroppingLocalDragBackToOriginalActionStack(sourceStack, targetStack) &&
				!CanRunningActionAcceptAdditionalStack(targetAction, sourceStack, sourceCardId))
			{
				return false;
			}
			if (!Cards.TryGetCard(sourceCardId, out TabletopCard sourceCard))
			{
				throw new KeyNotFoundException($"牌桌中不存在拖动卡牌 {sourceCardId}。");
			}
			CardDefinition sourceDefinition = RequireCardDefinition(sourceCard.ContentId, "判断牌桌合堆来源");
			CardDefinition targetBottomDefinition = RequireCardDefinition(
				targetStack.BottomCard.ContentId,
				"判断牌桌合堆目标");
			return PlacementRules.StackingRules.CanStack(sourceDefinition, targetBottomDefinition);
		}

		/// <summary>
		/// StackCraft 的商贩和收购点是交易区，不是 CardInstance 牌堆；当前牌桌用卡牌表现承载它们时，普通合堆必须显式排除。
		/// </summary>
		private bool IsStackCraftTradeZoneStack(TabletopCardStack stack)
		{
			if (stack == null || stack.Cards.Count == 0)
			{
				return false;
			}

			CardDefinition targetBottomDefinition = RequireCardDefinition(
				stack.BottomCard.ContentId,
				"判断 StackCraft 交易区牌堆");
			return targetBottomDefinition is PackVendorDefinition or CardBuyerDefinition;
		}

		/// <summary>
		/// 只读判断拖拽中是否应按 StackCraft 的可合堆底牌规则显示轮廓高亮；制作中的目标堆不参与高亮。
		/// </summary>
		internal bool CanShowStackCraftStackableDropHighlight(
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId)
		{
			RequireActive();
			if (!sourceCardId.IsValid || !targetCardId.IsValid || sourceCardId == targetCardId)
			{
				return false;
			}

			TabletopCardStack sourceStack = Cards.GetStackContaining(sourceCardId);
			TabletopCardStack targetStack = Cards.GetStackContaining(targetCardId);
			if (!CanUseTargetStackForDraggedSegment(sourceStack, sourceCardId, targetStack, targetCardId) ||
				TryFindActiveActionOwnedByStack(targetStack, out _) ||
				IsStackCraftTradeZoneStack(targetStack))
			{
				return false;
			}
			if (!Cards.TryGetCard(sourceCardId, out TabletopCard sourceCard))
			{
				throw new KeyNotFoundException($"牌桌中不存在拖动卡牌 {sourceCardId}。");
			}

			CardDefinition sourceDefinition = RequireCardDefinition(
				sourceCard.ContentId,
				"判断 StackCraft 高亮来源");
			CardDefinition targetBottomDefinition = RequireCardDefinition(
				targetStack.BottomCard.ContentId,
				"判断 StackCraft 高亮目标");
			return PlacementRules.StackingRules.CanStack(sourceDefinition, targetBottomDefinition);
		}

		/// <summary>
		/// 按 StackCraft 拖拽起手语义提交真实拆堆：按下时新牌段已经成为权威牌堆。
		/// </summary>
		public TabletopCardStack BeginLocalStackDrag(TabletopCardId cardId)
		{
			RequireActive();
			if (!cardId.IsValid)
			{
				throw new ArgumentException("本地拖拽必须引用有效牌桌卡牌。", nameof(cardId));
			}

			return BeginLocalStackDrag(
				cardId,
				Cards.GetCardTablePosition(cardId, PlacementRules.Geometry));
		}

		/// <summary>
		/// 按 StackCraft 拖拽起手语义提交真实拆堆，并使用被点卡当前画面位置作为新牌堆锚点。
		/// </summary>
		public TabletopCardStack BeginLocalStackDrag(
			TabletopCardId cardId,
			Vector2 currentCardTablePosition)
		{
			RequireActive();
			if (!cardId.IsValid)
			{
				throw new ArgumentException("本地拖拽必须引用有效牌桌卡牌。", nameof(cardId));
			}
			if (!IsFinitePosition(currentCardTablePosition))
			{
				throw new ArgumentException("本地拖拽起手位置必须是有限牌桌坐标。", nameof(currentCardTablePosition));
			}
			if (m_localStackDragState != null)
			{
				throw new InvalidOperationException(
					$"牌桌已经有未结束的本地拖拽 {m_localStackDragState.CardId}，不能再次开始。");
			}
			if (TryFindBattleContaining(cardId, out Battle battle))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 属于活动战斗 {battle.Id}，必须走战斗拖放入口。");
			}
			RequireNoBattleParticipantInDetachedTail(cardId, "开始拖拽牌堆");

			TabletopCardStack originalStack = Cards.GetStackContaining(cardId);
			int splitIndex = originalStack.GetDraggedSegmentStartIndex(cardId);
			TryFindActiveActionOwnedByStack(originalStack, out ActionInstance stackAction);

			TabletopCardStack draggedStack = Cards.DetachStackAt(cardId, currentCardTablePosition);
			bool actionPausedByDrag = false;
			if (stackAction != null &&
				splitIndex > 0 &&
				stackAction.State == ActionInstanceState.Running)
			{
				stackAction.Pause();
				actionPausedByDrag = true;
			}

			m_localStackDragState = new LocalStackDragState(
				cardId,
				originalStack,
				draggedStack,
				stackAction,
				actionPausedByDrag);
			return draggedStack;
		}

		/// <summary>
		/// 结束本地拖拽，并按释放结果恢复或复核拖拽前被暂停的行动。
		/// </summary>
		public void FinishLocalStackDragIfActive(TabletopCardId cardId, bool keepReleasedPlacement)
		{
			RequireActive();
			LocalStackDragState drag = m_localStackDragState;
			if (drag == null)
			{
				return;
			}
			if (drag.CardId != cardId)
			{
				throw new InvalidOperationException(
					$"牌桌正在结束拖拽 {cardId}，但活动拖拽属于 {drag.CardId}。");
			}

			try
			{
				if (!keepReleasedPlacement)
				{
					RestoreLocalStackDrag(drag);
				}
				ReconcileLocalStackDragAction(drag);
			}
			finally
			{
				m_localStackDragState = null;
			}
		}

		private static Action<TabletopActionCompletion> CreateLegacyActionCompletionCallback(
			Action<ContentId, ActionSettlementResult> actionCompleted)
		{
			if (actionCompleted == null)
			{
				throw new ArgumentNullException(nameof(actionCompleted));
			}

			return completion => actionCompleted(completion.ActionId, completion.Result);
		}

		/// <summary>
		/// 按 StackCraft 普通释放语义把拖拽牌段合并到目标牌堆；未满足合堆规则时不修改牌桌。
		/// </summary>
		public bool TryDropStackOnto(
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			Vector2 releasedCardTablePosition,
			out TabletopCardStack mergedStack)
		{
			RequireActive();
			if (!IsFinitePosition(releasedCardTablePosition))
			{
				throw new ArgumentException("牌桌合堆释放位置必须是有限二维坐标。", nameof(releasedCardTablePosition));
			}
			RequireNoBattleParticipantInDetachedTail(sourceCardId, "拖拽合并牌堆");
			RequireNoBattleParticipantInAffectedStack(targetCardId, "拖拽合并牌堆");
			if (!CanStackOnto(sourceCardId, targetCardId))
			{
				mergedStack = null;
				return false;
			}

			TabletopCardStack targetStack = Cards.GetStackContaining(targetCardId);
			TabletopCardId targetBottomCardId = targetStack.BottomCard.Id;
			TabletopCardStack sourceStack = Cards.DetachStackAt(sourceCardId);
			Cards.MoveStackToStackCraftReleasePosition(
				sourceStack,
				releasedCardTablePosition,
				PlacementRules);
			mergedStack = Cards.MergeStackOnto(sourceStack.BottomCard.Id, targetBottomCardId);
			Cards.ReflowPlacementAfterDrop(PlacementRules);
			return true;
		}

		public TabletopCardStack DetachStackAt(TabletopCardId cardId)
		{
			RequireActive();
			RequireNoBattleParticipantInDetachedTail(cardId, "拆分牌堆");
			return Cards.DetachStackAt(cardId);
		}

		public bool TryPlaceStack(TabletopCardId cardId, Vector2 position, out TabletopCardStack placedStack)
		{
			RequireActive();
			RequireNoBattleParticipantInDetachedTail(cardId, "放置牌堆");
			return Cards.TryPlaceStack(cardId, position, PlacementRules, out placedStack);
		}

		/// <summary>
		/// 对齐 StackCraft 拖拽过程中的 Board.ClampToBounds：只夹桌面边界，释放时再执行完整放置规则。
		/// </summary>
		internal Vector2 ClampDragStackPositionToBounds(TabletopCardId cardId, Vector2 position)
		{
			RequireActive();
			RequireNoBattleParticipantInDetachedTail(cardId, "拖拽牌堆边界夹取");
			return Cards.ClampStackPositionToBounds(cardId, position, PlacementRules);
		}

		/// <summary>
		/// 对齐 StackCraft 拖拽过程中的 SetDragTargetPosition：普通拖拽每帧夹取边界并提交真实牌堆位置。
		/// </summary>
		internal Vector2 MoveLocalDraggedStackToBounds(TabletopCardId cardId, Vector2 position)
		{
			RequireActive();
			RequireNoBattleParticipantInDetachedTail(cardId, "拖拽牌堆移动");
			return Cards.MoveStackDuringLocalDrag(cardId, position, PlacementRules);
		}

		/// <summary>只移动指定卡牌本身，供自动移动等非拖拽行为复用牌桌的唯一放置提交链。</summary>
		public bool TryPlaceSingleCard(TabletopCardId cardId, Vector2 position, out TabletopCardStack placedStack)
		{
			RequireActive();
			RequireNoBattleParticipantInAffectedStack(cardId, "抽出单张卡牌");
			return Cards.TryPlaceSingleCard(cardId, position, PlacementRules, out placedStack);
		}

		/// <summary>
		/// 为视图层查询参与战斗卡牌的派生姿态；牌桌卡牌的权威位置仍保存在各自牌堆。
		/// </summary>
		internal bool TryGetBattlePose(
			TabletopCardId cardId,
			int baseSortingOrder,
			out TabletopCardPose pose)
		{
			for (int battleIndex = 0; battleIndex < m_activeBattles.Count; battleIndex++)
			{
				Battle battle = m_activeBattles[battleIndex];
				if (battle.HasParticipant(cardId))
				{
					if (m_battleFormation == null)
					{
						throw new InvalidOperationException("活动战斗缺少剧本战斗阵型规则。");
					}
					return m_battleFormation.TryCalculatePose(
						battle,
						Cards,
						cardId,
						baseSortingOrder,
						out pose);
				}
			}

			pose = default;
			return false;
		}

		internal ActionCandidate[] FindCandidates(TabletopCardPointerReleaseIntent intent, IReadOnlyList<ActionDefinition> availableActions)
		{
			RequireActive();
			return ActionCandidateResolver.FindCandidates(intent, Cards, m_contentIndex, availableActions);
		}

		internal ActionCandidate[] FindStackCandidates(
			TabletopCardStack stack,
			IReadOnlyList<ActionDefinition> availableActions)
		{
			RequireActive();
			return ActionCandidateResolver.FindStackCandidates(stack, Cards, m_contentIndex, availableActions);
		}

		internal ActionInstance StartAction(ActionRequest request)
		{
			RequireActive();
			if (request == null)
			{
				throw new ArgumentNullException("request");
			}
			return StartActionInstance(CreateCandidateFromRequest(request));
		}

		/// <summary>
		/// 按模板的多配方相对权重语义，在完整牌堆匹配出的 ready 行动之间选择一个。
		/// 这里只决定要启动哪个行动；行动内部随机结果仍由 `SelectResultBranch` 在行动开始时处理。
		/// </summary>
		internal bool TrySelectReadyStackActionCandidate(
			IReadOnlyList<ActionCandidate> candidates,
			out ActionCandidate selectedCandidate)
		{
			RequireActive();
			if (candidates == null)
			{
				throw new ArgumentNullException(nameof(candidates));
			}

			selectedCandidate = null;
			if (candidates.Count == 0)
			{
				return false;
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("整堆制作候选需要权威随机流选择，但当前牌桌尚未初始化权威随机流。");
			}

			float totalWeight = 0f;
			for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
			{
				ActionCandidate candidate = RequireReadyStackActionCandidate(candidates[candidateIndex], candidateIndex);
				float weight = candidate.Action.RecipeSelectionWeight;
				if (!float.IsFinite(weight) || weight < 0f)
				{
					throw new InvalidOperationException(
						$"行动 {candidate.Action.ContentId} 的配方候选权重必须是大于等于 0 的有限数值，当前值为 {weight}。");
				}
				totalWeight += weight;
			}

			Unity.Mathematics.Random candidateRandom = m_authoritativeRandom;
			if (totalWeight <= 0f)
			{
				selectedCandidate = candidates[candidateRandom.NextInt(candidates.Count)];
				m_authoritativeRandom = candidateRandom;
				return true;
			}

			float roll = candidateRandom.NextFloat(0f, totalWeight);
			ActionCandidate lastWeightedCandidate = null;
			for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
			{
				ActionCandidate candidate = candidates[candidateIndex];
				float weight = candidate.Action.RecipeSelectionWeight;
				if (weight <= 0f)
				{
					continue;
				}
				lastWeightedCandidate = candidate;
				roll -= weight;
				if (roll <= 0f)
				{
					selectedCandidate = candidate;
					m_authoritativeRandom = candidateRandom;
					return true;
				}
			}

			selectedCandidate = lastWeightedCandidate ??
				throw new InvalidOperationException("整堆制作候选权重合计大于 0，但没有任何可选择的正权重候选。");
			m_authoritativeRandom = candidateRandom;
			return true;
		}

		public ActionPlan CreateActionPlan(ActionCandidate candidate)
		{
			RequireActive();
			if (candidate == null)
			{
				throw new ArgumentNullException(nameof(candidate));
			}
			if (!m_contentIndex.TryGet(candidate.Action.ContentId, out ActionDefinition action) ||
				!ReferenceEquals(action, candidate.Action))
			{
				throw new InvalidOperationException(
					$"行动候选 {candidate.Action.ContentId} 不属于当前牌桌的内容索引。");
			}

			ActionPlan plan = new ActionPlan(candidate);
			ValidateActionPlan(plan, requireComplete: false);
			m_actionPlans.Add(plan);
			return plan;
		}

		public void AddCardToActionPlan(
			ActionPlan plan,
			string slotKey,
			TabletopCardId cardId)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			ActionPlanBinding targetBinding = plan.GetBinding(slotKey);
			if (!CanAddCardToActionPlan(plan, targetBinding, cardId, out string failureMessage))
			{
				throw new InvalidOperationException(failureMessage);
			}

			targetBinding.Add(cardId);
		}

		public bool TryAddCardToActionPlan(
			ActionPlan plan,
			string slotKey,
			TabletopCardId cardId)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			ActionPlanBinding targetBinding = plan.GetBinding(slotKey);
			if (!CanAddCardToActionPlan(plan, targetBinding, cardId, out _))
			{
				return false;
			}

			targetBinding.Add(cardId);
			return true;
		}

		private bool CanAddCardToActionPlan(
			ActionPlan plan,
			ActionPlanBinding targetBinding,
			TabletopCardId cardId,
			out string failureMessage)
		{
			if (!Cards.TryGetCard(cardId, out TabletopCard card) ||
				!m_contentIndex.TryGet(card.ContentId, out ContentAsset contentAsset))
			{
				failureMessage = $"牌桌中不存在可填入行动计划的卡牌 {cardId}。";
				return false;
			}
			for (int bindingIndex = 0; bindingIndex < plan.Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> existingIds = plan.Bindings[bindingIndex].CardIds;
				for (int cardIndex = 0; cardIndex < existingIds.Count; cardIndex++)
				{
					if (existingIds[cardIndex] == cardId)
					{
						failureMessage = $"行动计划 {plan.ActionId} 已经绑定牌桌卡牌 {cardId}。";
						return false;
					}
				}
			}
			if (IsActionPlanParticipant(cardId, plan))
			{
				failureMessage = $"牌桌卡牌 {cardId} 已填在其它待确认行动计划中。";
				return false;
			}
			if (targetBinding.Slot.MaximumParticipants > 0 &&
				targetBinding.CardIds.Count >= targetBinding.Slot.MaximumParticipants)
			{
				failureMessage =
					$"行动计划 {plan.ActionId} 的槽位 {targetBinding.Slot.Key} 已达到参与上限。";
				return false;
			}
			AbilitySystemCell abilitySystem = card is CharacterCard character
				? character.AbilitySystem
				: null;
			if (!ActionParticipationEvaluator.MatchesParticipant(
					targetBinding.Slot,
					contentAsset,
					abilitySystem))
			{
				failureMessage =
					$"牌桌卡牌 {cardId} 不满足行动计划 {plan.ActionId} 的槽位 {targetBinding.Slot.Key} 条件。";
				return false;
			}

			failureMessage = string.Empty;
			return true;
		}

		public void RemoveCardFromActionPlan(
			ActionPlan plan,
			string slotKey,
			TabletopCardId cardId)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			plan.GetBinding(slotKey).Remove(cardId);
		}

		public ActionInstance SubmitActionPlan(ActionPlan plan)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			ValidateActionPlan(plan, requireComplete: true);
			ActionInstance instance = StartActionInstance(
				CreateCandidateFromRequest(plan.CreateRequest()),
				plan);
			m_actionPlans.Remove(plan);
			return instance;
		}

		public void CancelActionPlan(ActionPlan plan)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			m_actionPlans.Remove(plan);
		}

		public ActionInstanceSnapshot[] CreateActiveActionSnapshots()
		{
			ActionInstanceSnapshot[] snapshots = new ActionInstanceSnapshot[m_activeActions.Count];
			for (int i = 0; i < m_activeActions.Count; i++)
			{
				snapshots[i] = m_activeActions[i].CreateSnapshot();
			}
			return snapshots;
		}

		internal TabletopSnapshot CreateSnapshot()
		{
			if (m_activeBattles.Count > 0)
			{
				throw new InvalidOperationException("牌桌仍有活动战斗；本游戏不保存战斗中状态，必须先结束战斗再存档。");
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流尚未初始化，不能生成可继续运行的快照。");
			}
			return new TabletopSnapshot(
				Cards.CreateSnapshot(),
				CreateActiveActionSnapshots(),
				m_authoritativeRandom.state);
		}

		internal uint AuthoritativeRandomState => m_authoritativeRandom.state;

		internal float NextAuthoritativeFloat()
		{
			RequireActive();
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流尚未初始化。");
			}
			return m_authoritativeRandom.NextFloat();
		}

		internal TabletopCard CreateCardAtAuthoritativeRandomPosition(ContentId contentId)
		{
			RequireActive();
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流尚未初始化。");
			}
			Rect bounds = PlacementRules.Area.Bounds;
			Vector2 position = new Vector2(
				m_authoritativeRandom.NextFloat(bounds.xMin, bounds.xMax),
				m_authoritativeRandom.NextFloat(bounds.yMin, bounds.yMax));
			return CreateCard(contentId, position);
		}

		internal void RequestPresentationCue(TabletopPresentationCue cue)
		{
			RequireActive();
			PresentationCueRequested?.Invoke(cue);
		}

		internal void RestoreAuthoritativeRandom(uint state)
		{
			if (state == 0u)
			{
				throw new InvalidOperationException("牌桌快照的权威随机状态不能为 0。");
			}
			if (m_authoritativeRandom.state != 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流已经初始化，不能重复恢复。");
			}
			m_authoritativeRandom.state = state;
		}

		internal void InitializeAuthoritativeRandom(uint seed)
		{
			RequireActive();
			if (seed == 0)
			{
				throw new ArgumentOutOfRangeException("seed", "权威随机种子不能为 0。");
			}
			if (m_authoritativeRandom.state != 0)
			{
				throw new InvalidOperationException("本局牌桌行动的权威随机流已经初始化，不能重置。");
			}
			if (m_activeActions.Count > 0)
			{
				throw new InvalidOperationException("存在活动行动时不能初始化权威随机流。");
			}
			m_authoritativeRandom = new Unity.Mathematics.Random(seed);
		}

		internal void UseRealTimeProgression(float secondsPerTurn)
		{
			RequireActive();
			m_realTimeSecondsPerTurn = ValidateSecondsPerTurn(secondsPerTurn);
			ProgressionMode = ActionProgressionMode.RealTime;
		}

		internal void UseTurnBasedProgression()
		{
			RequireActive();
			ProgressionMode = ActionProgressionMode.TurnBased;
			m_realTimeSecondsPerTurn = 0f;
		}

		public void PauseAction(ActionInstance action)
		{
			RequireActive();
			RequireActiveAction(action);
			action.Pause();
		}

		public void ResumeAction(ActionInstance action)
		{
			RequireActive();
			RequireActiveAction(action);
			action.Resume();
		}

		public void CancelAction(ActionInstance action)
		{
			RequireActive();
			RequireActiveAction(action);
			action.Cancel(ActionCancellationReason.Requested);
			m_activeActions.Remove(action);
		}

		internal void AdvanceConfirmedTurn()
		{
			RequireActive();
			if (ProgressionMode == ActionProgressionMode.TurnBased)
			{
				AdvanceActiveActions(1f);
			}
		}

		internal void AdvanceRealTime(float deltaSeconds)
		{
			RequireActive();
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"牌桌实时推进秒数必须是大于或等于 0 的有限值。");
			}
			if (deltaSeconds == 0f)
			{
				return;
			}

			AdvanceActiveBattles(deltaSeconds);
			AdvancePeriodicCardProduction(deltaSeconds);
			AdvanceAutomaticMovement(deltaSeconds);
			if (ProgressionMode == ActionProgressionMode.RealTime)
			{
				AdvanceActiveActions(deltaSeconds / ValidateSecondsPerTurn(m_realTimeSecondsPerTurn));
			}
		}

		private void AdvancePeriodicCardProduction(float deltaSeconds)
		{
			m_periodicProductionRequests.Clear();
			IReadOnlyList<TabletopCardStack> stacks = Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					TabletopCard card = cards[cardIndex];
					CardDefinition definition = RequireCardDefinition(card.ContentId, "推进周期产出");
					if (!definition.HasPeriodicProduction)
					{
						continue;
					}
					if (IsAutomaticBehaviorHeldByLocalInput(card.Id))
					{
						continue;
					}
					if (!CanCardProduceNow(card.Id))
					{
						continue;
					}

					int productionCount = card.AdvancePeriodicProduction(
						deltaSeconds,
						definition.PeriodicProductionIntervalSeconds);
					if (productionCount == 0)
					{
						continue;
					}

					RequireCardDefinition(definition.PeriodicProductionCardId, "周期产出");
					for (int productionIndex = 0; productionIndex < productionCount; productionIndex++)
					{
						m_periodicProductionRequests.Add(new PeriodicProductionRequest(
							definition.PeriodicProductionCardId,
							card.Position));
					}
				}
			}

			m_periodicProductionCreations.Clear();
			for (int requestIndex = 0; requestIndex < m_periodicProductionRequests.Count; requestIndex++)
			{
				PeriodicProductionRequest request = m_periodicProductionRequests[requestIndex];
				m_periodicProductionCreations.Add(new TabletopCardCreationRequest(
					request.ProductCardId,
					1,
					request.SourcePosition));
			}
			RequireCardChangesCanBeCommitted(
				Array.Empty<TabletopCardId>(),
				m_periodicProductionCreations,
				Array.Empty<TabletopCardRestorationRequest>(),
				requirePlacementConverged: false);

			for (int requestIndex = 0; requestIndex < m_periodicProductionRequests.Count; requestIndex++)
			{
				PeriodicProductionRequest request = m_periodicProductionRequests[requestIndex];
				CreateCard(
					request.ProductCardId,
					request.SourcePosition,
					allowSpawnAttach: true);
				RequestPresentationCue(TabletopPresentationCue.AtTablePosition(
					TabletopPresentationCueKind.CardSmoke,
					request.SourcePosition));
			}
			m_periodicProductionCreations.Clear();
			m_periodicProductionRequests.Clear();
		}

		private bool CanCardProduceNow(TabletopCardId cardId)
		{
			return !IsAutomaticBehaviorHeldByLocalInput(cardId) &&
			!IsCardInActiveCraftingStack(cardId) &&
				!IsActiveActionParticipant(cardId) &&
				!TryFindBattleContaining(cardId, out _);
		}

		private void AdvanceAutomaticMovement(float deltaSeconds)
		{
			m_automaticMovementRequests.Clear();
			IReadOnlyList<TabletopCardStack> stacks = Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					TabletopCard card = cards[cardIndex];
					CardDefinition definition = RequireCardDefinition(card.ContentId, "推进自动移动");
					if (!definition.UsesAutomaticMovement)
					{
						continue;
					}
					if (m_authoritativeRandom.state == 0u)
					{
						throw new InvalidOperationException("牌桌权威随机流尚未初始化，不能推进自动移动。");
					}
					if (IsAutomaticBehaviorHeldByLocalInput(card.Id))
					{
						continue;
					}
					if (!CanCardMoveAutomaticallyNow(card.Id))
					{
						continue;
					}

					bool shouldMove = card.AdvanceAutomaticMovement(
						deltaSeconds,
						PlacementRules.AutomaticMovementIntervalSeconds);
					if (shouldMove &&
						!ShouldStayInAutomaticMovementRetentionStack(card))
					{
						m_automaticMovementRequests.Add(new AutomaticMovementRequest(card.Id));
					}
				}
			}

			if (m_automaticMovementRequests.Count == 0)
			{
				return;
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流尚未初始化，不能推进自动移动。");
			}

			for (int requestIndex = 0; requestIndex < m_automaticMovementRequests.Count; requestIndex++)
			{
				AutomaticMovementRequest request = m_automaticMovementRequests[requestIndex];
				if (!Cards.TryGetCard(request.CardId, out TabletopCard card) ||
					!CanCardMoveAutomaticallyNow(request.CardId))
				{
					continue;
				}
				CardDefinition definition = RequireCardDefinition(card.ContentId, "执行自动移动");
				EnsureSingleCardStackForAutomaticBehavior(card.Id, "自动行为起手");
				if (!TryExecuteAutomaticHostileBehavior(card, definition))
				{
					TryMoveCardRandomly(card, definition);
				}
			}
			m_automaticMovementRequests.Clear();
		}

		private bool ShouldStayInAutomaticMovementRetentionStack(TabletopCard card)
		{
			if (card == null)
			{
				throw new ArgumentNullException(nameof(card));
			}
			if (card is CharacterCard character &&
				character.AbilitySystem.HasTag(XTag.Faction_Enemy))
			{
				return false;
			}

			TabletopCardStack stack = card.Stack
				?? throw new InvalidOperationException($"自动移动卡牌 {card.Id} 不属于任何牌堆。");
			if (stack.Cards.Count <= 1)
			{
				return false;
			}

			int cardIndex = stack.IndexOf(card.Id);
			if (cardIndex < 0)
			{
				throw new InvalidOperationException($"自动移动卡牌 {card.Id} 不在其所属牌堆中。");
			}

			for (int enclosureIndex = 0; enclosureIndex < stack.Cards.Count; enclosureIndex++)
			{
				TabletopCard enclosure = stack.Cards[enclosureIndex];
				CardDefinition enclosureDefinition = RequireCardDefinition(
					enclosure.ContentId,
					"判断自动移动留存容量");
				int capacity = enclosureDefinition.AutomaticMovementRetentionCapacity;
				if (capacity <= 0)
				{
					continue;
				}

				int distanceAboveEnclosure = cardIndex - enclosureIndex;
				return distanceAboveEnclosure > 0 && distanceAboveEnclosure <= capacity;
			}
			return false;
		}

		private bool TryExecuteAutomaticHostileBehavior(TabletopCard card, CardDefinition definition)
		{
			if (card is not CharacterCard character ||
				definition is not CharacterCardDefinition characterDefinition ||
				!characterDefinition.HasAutomaticHostileBehavior ||
				!character.AbilitySystem.HasTag(XTag.Faction_Enemy))
			{
				return false;
			}

			if (TryJoinClosestRelevantBattle(character, characterDefinition))
			{
				return true;
			}
			if (TryHuntClosestPlayer(character, characterDefinition))
			{
				return true;
			}
			return false;
		}

		private bool TryJoinClosestRelevantBattle(
			CharacterCard character,
			CharacterCardDefinition definition)
		{
			if (!TryFindClosestRelevantBattle(
				character,
				definition.AutomaticAggroRadius,
				out Battle battle,
				out int joinSideIndex))
			{
				return false;
			}

			float distance = Vector2.Distance(character.Position, battle.AreaCenter);
			if (distance <= definition.AutomaticAttackRadius * 1.5f)
			{
				JoinBattle(battle, joinSideIndex, character.Id);
			}
			else
			{
				TryMoveCardTowards(
					character,
					battle.AreaCenter,
					PlacementRules.AutomaticMovementRadius);
			}
			return true;
		}

		private bool TryHuntClosestPlayer(
			CharacterCard character,
			CharacterCardDefinition definition)
		{
			if (!TryFindClosestPlayerCharacter(
				character.Position,
				definition.AutomaticAggroRadius,
				out CharacterCard target))
			{
				return false;
			}

			float distance = Vector2.Distance(character.Position, target.Position);
			if (distance <= definition.AutomaticAttackRadius)
			{
				List<TabletopCardId> attackerIds = new List<TabletopCardId>(1);
				List<TabletopCardId> defenderIds = new List<TabletopCardId>();
				CollectAutomaticCombatCharactersInStack(
					Cards.GetStackContaining(character.Id),
					XTag.Faction_Enemy,
					attackerIds);
				CollectAutomaticCombatCharactersInStack(
					Cards.GetStackContaining(target.Id),
					XTag.Faction_Player,
					defenderIds);
				if (attackerIds.Count > 0 && defenderIds.Count > 0)
				{
					StartBattle(attackerIds, defenderIds);
				}
			}
			else
			{
				TryMoveCardTowards(
					character,
					target.Position,
					PlacementRules.AutomaticMovementRadius);
			}
			return true;
		}

		private bool TryFindClosestRelevantBattle(
			CharacterCard character,
			float radius,
			out Battle battle,
			out int joinSideIndex)
		{
			battle = null;
			joinSideIndex = -1;
			float bestDistanceSquared = radius * radius;
			for (int battleIndex = 0; battleIndex < m_activeBattles.Count; battleIndex++)
			{
				Battle candidate = m_activeBattles[battleIndex];
				if (!TryResolveEnemyJoinSide(candidate, out int candidateJoinSideIndex))
				{
					continue;
				}

				float distanceSquared = (candidate.AreaCenter - character.Position).sqrMagnitude;
				if (distanceSquared >= bestDistanceSquared)
				{
					continue;
				}

				bestDistanceSquared = distanceSquared;
				battle = candidate;
				joinSideIndex = candidateJoinSideIndex;
			}
			return battle != null;
		}

		private bool TryResolveManualBattleStartStacks(
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			out TabletopCardStack sourceStack,
			out TabletopCardStack targetStack,
			out int sourceFactionTagCode,
			out int targetFactionTagCode)
		{
			if (!sourceCardId.IsValid)
			{
				throw new ArgumentException("拖拽发起战斗必须引用有效来源卡牌。", nameof(sourceCardId));
			}
			if (!targetCardId.IsValid)
			{
				throw new ArgumentException("拖拽发起战斗必须引用有效目标卡牌。", nameof(targetCardId));
			}

			sourceStack = Cards.GetStackContaining(sourceCardId);
			targetStack = Cards.GetStackContaining(targetCardId);
			sourceFactionTagCode = 0;
			targetFactionTagCode = 0;
			if (ReferenceEquals(sourceStack, targetStack))
			{
				return false;
			}

			sourceFactionTagCode = ResolveBattleFactionTag(sourceStack.TopCard, "拖拽牌堆领牌");
			targetFactionTagCode = ResolveBattleFactionTag(targetStack.TopCard, "目标牌堆领牌");
			return AreOpposingManualBattleFactions(sourceFactionTagCode, targetFactionTagCode);
		}

		private bool TryFindBattleAtPosition(Vector2 position, out Battle battle)
		{
			for (int battleIndex = 0; battleIndex < m_activeBattles.Count; battleIndex++)
			{
				Battle candidate = m_activeBattles[battleIndex];
				if (CalculateBattleArea(candidate).Contains(position))
				{
					battle = candidate;
					return true;
				}
			}

			battle = null;
			return false;
		}

		private bool TryResolveBattleFactionSide(
			Battle battle,
			int factionTagCode,
			out int sideIndex)
		{
			if (!IsSupportedManualBattleFaction(factionTagCode))
			{
				throw new ArgumentException("手动加入战斗只支持玩家与敌方阵营标签。", nameof(factionTagCode));
			}
			sideIndex = -1;
			int matchedSideCount = 0;
			for (int candidateSideIndex = 0; candidateSideIndex < battle.SideCount; candidateSideIndex++)
			{
				bool sideHasFaction = false;
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[candidateSideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (!Cards.TryGetCard(cardIds[cardIndex], out TabletopCard card))
					{
						throw new InvalidOperationException(
							$"战斗 {battle.Id} 的参与对象 {cardIds[cardIndex]} 不属于当前牌桌。");
					}
					int participantFactionTagCode = ResolveBattleFactionTag(card, "既有战斗参与卡牌");
					if (participantFactionTagCode == factionTagCode)
					{
						sideHasFaction = true;
					}
				}

				if (!sideHasFaction)
				{
					continue;
				}

				matchedSideCount++;
				sideIndex = candidateSideIndex;
			}

			if (matchedSideCount > 1)
			{
				throw new InvalidOperationException(
					$"战斗 {battle.Id} 存在多个 {factionTagCode} 阵营战斗方，手动增援无法判断加入哪一方。");
			}
			return matchedSideCount == 1;
		}

		private bool HasManualBattleCharactersInStack(
			TabletopCardStack stack,
			int factionTagCode)
		{
			IReadOnlyList<TabletopCard> cards = stack.Cards;
			for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
			{
				if (ResolveBattleFactionTag(cards[cardIndex], "手动战斗候选卡牌") == factionTagCode &&
					CanCardJoinManualBattleNow(cards[cardIndex].Id))
				{
					return true;
				}
			}
			return false;
		}

		private void CollectManualBattleCharactersInStack(
			TabletopCardStack stack,
			int factionTagCode,
			List<TabletopCardId> result)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}
			if (result == null)
			{
				throw new ArgumentNullException(nameof(result));
			}

			IReadOnlyList<TabletopCard> cards = stack.Cards;
			for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
			{
				TabletopCard card = cards[cardIndex];
				if (ResolveBattleFactionTag(card, "手动战斗候选卡牌") == factionTagCode &&
					CanCardJoinManualBattleNow(card.Id))
				{
					result.Add(card.Id);
				}
			}
		}

		private void JoinBattleCards(
			Battle battle,
			int sideIndex,
			IReadOnlyList<TabletopCardId> cardIds)
		{
			for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
			{
				JoinBattle(battle, sideIndex, cardIds[cardIndex]);
			}
		}

		private bool CanCardJoinManualBattleNow(TabletopCardId cardId)
		{
			return !IsActiveActionParticipant(cardId) &&
				!IsActionPlanParticipant(cardId) &&
				!TryFindBattleContaining(cardId, out _);
		}

		private static int ResolveBattleFactionTag(TabletopCard card, string role)
		{
			if (card is not CharacterCard character)
			{
				return 0;
			}

			bool isPlayer = character.AbilitySystem.HasTag(XTag.Faction_Player);
			bool isEnemy = character.AbilitySystem.HasTag(XTag.Faction_Enemy);
			if (isPlayer && isEnemy)
			{
				throw new InvalidOperationException(
					$"{role} {character.Id} 同时拥有 Faction.Player 与 Faction.Enemy，不能解析战斗阵营。");
			}
			if (isPlayer)
			{
				return XTag.Faction_Player;
			}
			return isEnemy ? XTag.Faction_Enemy : 0;
		}

		private static bool AreOpposingManualBattleFactions(int leftTagCode, int rightTagCode)
		{
			return leftTagCode == XTag.Faction_Player && rightTagCode == XTag.Faction_Enemy ||
				leftTagCode == XTag.Faction_Enemy && rightTagCode == XTag.Faction_Player;
		}

		private static bool IsSupportedManualBattleFaction(int factionTagCode)
		{
			return factionTagCode == XTag.Faction_Player ||
				factionTagCode == XTag.Faction_Enemy;
		}

		private bool TryResolveEnemyJoinSide(Battle battle, out int sideIndex)
		{
			sideIndex = -1;
			int enemySideCount = 0;
			bool hasPlayerSide = false;
			for (int candidateSideIndex = 0; candidateSideIndex < battle.SideCount; candidateSideIndex++)
			{
				bool sideHasPlayer = false;
				bool sideHasEnemy = false;
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[candidateSideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (!Cards.TryGetCard(cardIds[cardIndex], out TabletopCard card) ||
						card is not CharacterCard character)
					{
						throw new InvalidOperationException(
							$"战斗 {battle.Id} 的参与对象 {cardIds[cardIndex]} 不再是当前牌桌中的角色卡。");
					}
					sideHasPlayer |= character.AbilitySystem.HasTag(XTag.Faction_Player);
					sideHasEnemy |= character.AbilitySystem.HasTag(XTag.Faction_Enemy);
				}

				hasPlayerSide |= sideHasPlayer;
				if (!sideHasEnemy)
				{
					continue;
				}

				enemySideCount++;
				sideIndex = candidateSideIndex;
			}

			if (!hasPlayerSide)
			{
				sideIndex = -1;
				return false;
			}
			if (enemySideCount > 1)
			{
				throw new InvalidOperationException(
					$"战斗 {battle.Id} 存在多个 Faction.Enemy 战斗方，自动增援无法判断加入哪一方。");
			}
			return enemySideCount == 1;
		}

		private bool TryFindClosestPlayerCharacter(
			Vector2 sourcePosition,
			float radius,
			out CharacterCard target)
		{
			// StackCraft 传入了索敌半径，但 FindClosestPlayerCard 实际未使用它；这里保留该模板语义。
			target = null;
			float bestDistanceSquared = float.PositiveInfinity;
			IReadOnlyList<TabletopCardStack> stacks = Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					if (cards[cardIndex] is not CharacterCard character ||
						!character.AbilitySystem.HasTag(XTag.Faction_Player) ||
						TryFindBattleContaining(character.Id, out _))
					{
						continue;
					}

					float distanceSquared = (character.Position - sourcePosition).sqrMagnitude;
					if (distanceSquared >= bestDistanceSquared)
					{
						continue;
					}

					bestDistanceSquared = distanceSquared;
					target = character;
				}
			}
			return target != null;
		}

		private void CollectAutomaticCombatCharactersInStack(
			TabletopCardStack stack,
			int factionTagCode,
			List<TabletopCardId> result)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}
			if (result == null)
			{
				throw new ArgumentNullException(nameof(result));
			}

			IReadOnlyList<TabletopCard> cards = stack.Cards;
			for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
			{
				if (cards[cardIndex] is CharacterCard character &&
					character.AbilitySystem.HasTag(factionTagCode) &&
					!TryFindBattleContaining(character.Id, out _))
				{
					result.Add(character.Id);
				}
			}
		}

		private void EnsureSingleCardStackForAutomaticBehavior(TabletopCardId cardId, string operation)
		{
			TabletopCardStack stack = Cards.GetStackContaining(cardId);
			if (stack.Cards.Count == 1)
			{
				return;
			}
			if (!TryPlaceSingleCard(cardId, stack.Position, out _))
			{
				throw new InvalidOperationException($"{operation}需要先把自动行动卡牌从牌堆中抽出，但当前位置无法提交单卡放置。");
			}
		}

		private bool TryMoveCardTowards(
			TabletopCard card,
			Vector2 targetPosition,
			float movementRadius)
		{
			Vector2 direction = targetPosition - card.Position;
			if (direction.sqrMagnitude <= 0.0001f)
			{
				return false;
			}

			Vector2 candidatePosition = card.Position + direction.normalized * movementRadius;
			return IsAutomaticMovementCandidateValid(card, candidatePosition) &&
				TryPlaceSingleCard(card.Id, candidatePosition, out _);
		}

		private bool TryMoveCardRandomly(TabletopCard card, CardDefinition definition)
		{
			if (card == null)
			{
				throw new ArgumentNullException(nameof(card));
			}
			if (definition == null)
			{
				throw new ArgumentNullException(nameof(definition));
			}
			if (!definition.UsesAutomaticMovement)
			{
				return false;
			}
			if (PlacementRules.AutomaticMovementMaxAttempts <= 0)
			{
				throw new InvalidOperationException("牌桌自动移动尝试次数必须大于 0。");
			}
			if (!float.IsFinite(PlacementRules.AutomaticMovementRadius) || PlacementRules.AutomaticMovementRadius <= 0f)
			{
				throw new InvalidOperationException("牌桌自动移动半径必须大于 0。");
			}

			Vector2 basePosition = card.Position;
			for (int attempt = 0; attempt < PlacementRules.AutomaticMovementMaxAttempts; attempt++)
			{
				float angle = m_authoritativeRandom.NextFloat(0f, math.PI * 2f);
				Vector2 direction = new Vector2(math.cos(angle), math.sin(angle));
				Vector2 candidatePosition = basePosition + direction * PlacementRules.AutomaticMovementRadius;
				if (IsAutomaticMovementCandidateValid(card, candidatePosition) &&
					TryPlaceSingleCard(card.Id, candidatePosition, out _))
				{
					return true;
				}
			}
			Cards.ReflowPlacementAfterDrop(PlacementRules);
			return false;
		}

		private bool IsAutomaticMovementCandidateValid(TabletopCard card, Vector2 position)
		{
			if (card == null)
			{
				throw new ArgumentNullException(nameof(card));
			}
			if (!IsFinitePosition(position))
			{
				return false;
			}

			Rect footprint = PlacementRules.Geometry.CalculateFootprint(
				position,
				1,
				ResolveCardSize(card.ContentId, PlacementRules.Geometry.CardSize));
			TabletopCardPlacementArea area = PlacementRules.Area;
			if (!IsRectInside(area.Bounds, footprint))
			{
				return false;
			}
			for (int restrictedIndex = 0; restrictedIndex < area.RestrictedAreas.Count; restrictedIndex++)
			{
				if (RectanglesOverlap(footprint, area.RestrictedAreas[restrictedIndex]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool IsRectInside(Rect bounds, Rect footprint)
		{
			return footprint.xMin >= bounds.xMin &&
				footprint.xMax <= bounds.xMax &&
				footprint.yMin >= bounds.yMin &&
				footprint.yMax <= bounds.yMax;
		}

		private static bool RectanglesOverlap(Rect left, Rect right)
		{
			return left.xMin < right.xMax &&
				left.xMax > right.xMin &&
				left.yMin < right.yMax &&
				left.yMax > right.yMin;
		}

		private bool CanCardMoveAutomaticallyNow(TabletopCardId cardId)
		{
			return !IsAutomaticBehaviorHeldByLocalInput(cardId) &&
				!IsCardInActiveCraftingStack(cardId) &&
				!IsActiveActionParticipant(cardId) &&
				!TryFindBattleContaining(cardId, out _);
		}

		/// <summary>
		/// 对齐 StackCraft CardAI 的整叠制作暂停语义：牌堆处于活动行动期间，
		/// 其中所有卡牌都不能自动移动或周期产出，不只限制行动绑定卡牌。
		/// </summary>
		private bool IsCardInActiveCraftingStack(TabletopCardId cardId)
		{
			if (!Cards.TryGetCard(cardId, out TabletopCard card))
			{
				throw new InvalidOperationException($"自动行为卡牌 {cardId} 不存在。");
			}
			return card.Stack != null && HasActiveActionOnStack(card.Stack);
		}

		internal void HoldAutomaticBehaviorForLocalInput(TabletopCardId cardId)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("本地输入保持自动行为必须引用有效卡牌。", nameof(cardId));
			}
			if (!Cards.TryGetCard(cardId, out _))
			{
				throw new InvalidOperationException($"本地输入要保持自动行为的卡牌 {cardId} 不存在。");
			}
			if (m_localInputHeldAutomaticBehaviorCardId.IsValid &&
				m_localInputHeldAutomaticBehaviorCardId != cardId)
			{
				throw new InvalidOperationException(
					$"牌桌已经有本地输入保持卡牌 {m_localInputHeldAutomaticBehaviorCardId}，不能同时保持 {cardId}。");
			}
			m_localInputHeldAutomaticBehaviorCardId = cardId;
		}

		internal void ReleaseAutomaticBehaviorForLocalInput(TabletopCardId cardId)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("释放本地输入保持必须引用有效卡牌。", nameof(cardId));
			}
			if (!m_localInputHeldAutomaticBehaviorCardId.IsValid)
			{
				return;
			}
			if (m_localInputHeldAutomaticBehaviorCardId != cardId)
			{
				throw new InvalidOperationException(
					$"牌桌当前保持的是卡牌 {m_localInputHeldAutomaticBehaviorCardId}，不能用 {cardId} 释放。");
			}
			m_localInputHeldAutomaticBehaviorCardId = default;
		}

		private bool IsAutomaticBehaviorHeldByLocalInput(TabletopCardId cardId)
		{
			return m_localInputHeldAutomaticBehaviorCardId.IsValid &&
				m_localInputHeldAutomaticBehaviorCardId == cardId;
		}

		private void AdvanceActiveBattles(float deltaSeconds)
		{
			// 战斗始终走真实秒数；普通牌桌行动是否按秒推进仍由 ProgressionMode 单独决定。
			for (int battleIndex = m_activeBattles.Count - 1; battleIndex >= 0; battleIndex--)
			{
				Battle battle = m_activeBattles[battleIndex];
				if (battle.TryConsumePendingActivation(
					deltaSeconds,
					out BattlePendingAbilityActivation activation))
				{
					ActivatePendingBattleAbility(battle, activation);
				}
				if (battle.HasExecutingTurn)
				{
					continue;
				}
				if (!battle.HasExecutingTurn)
				{
					ResolveDefeatedParticipants(battle);
					if (battle.IsEnded)
					{
						continue;
					}
				}

				for (int sideIndex = 0; sideIndex < battle.SideCount; sideIndex++)
				{
					IReadOnlyList<TabletopCardId> cardIds = battle.Sides[sideIndex].CardIds;
					for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
					{
						TabletopCardId cardId = cardIds[cardIndex];
						CharacterCard character = RequireBattleCharacter(battle, cardId, "自动行动角色");
						float attackSpeed = character.AbilitySystem.GetAttrCurrentValue(
							XAttrSet.FightUnit,
							XAttribute.AttackSpeed);
						if (!float.IsFinite(attackSpeed) || attackSpeed < 0f)
						{
							throw new InvalidOperationException(
								$"参战角色卡 {cardId} 的 EX-GAS 攻击速度必须是大于或等于 0 的有限值。");
						}
						battle.AddActionProgress(cardId, attackSpeed * deltaSeconds);
					}
				}

				if (!battle.ConsumeTurnInterval(deltaSeconds) ||
					!TrySelectAutomaticBattleActor(battle, out CharacterCard actor))
				{
					continue;
				}

				TabletopCardId targetId = battle.TakeRandomOpponent(actor.Id);
				ActivateBattleAbility(
					battle,
					actor.Id,
					targetId,
					actor.AutomaticBattleAbilityCode,
					tracksAutomaticTurn: true);
			}
		}

		private bool TrySelectAutomaticBattleActor(Battle battle, out CharacterCard actor)
		{
			actor = null;
			float highestProgress = float.NegativeInfinity;
			for (int sideIndex = 0; sideIndex < battle.SideCount; sideIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[sideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					CharacterCard candidate = RequireBattleCharacter(
						battle,
						cardIds[cardIndex],
						"自动行动候选");
					if (candidate.AutomaticBattleAbilityCode <= 0)
					{
						continue;
					}
					float progress = battle.GetActionProgress(candidate.Id);
					if (actor == null || progress > highestProgress)
					{
						actor = candidate;
						highestProgress = progress;
					}
				}
			}
			return actor != null;
		}

		private bool TrySelectCardLoot(ContentId sourceContentId, out ContentId lootContentId)
		{
			lootContentId = default;
			if (!m_contentIndex.TryGet(sourceContentId, out CardDefinition sourceDefinition))
			{
				throw new InvalidOperationException(
					$"被击败卡牌 {sourceContentId} 不存在对应的卡牌定义，无法结算掉落。");
			}
			IReadOnlyList<CardLootEntry> lootEntries = sourceDefinition.Loot;
			if (lootEntries.Count == 0)
			{
				return false;
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("卡牌掉落需要牌桌权威随机流，但随机流尚未初始化。");
			}

			int totalWeight = 0;
			for (int i = 0; i < lootEntries.Count; i++)
			{
				CardLootEntry entry = lootEntries[i]
					?? throw new InvalidOperationException(
						$"卡牌 {sourceContentId} 的第 {i + 1} 个加权产出为空。");
				if (entry.Weight <= 0 ||
					!m_contentIndex.TryGet(entry.CardId, out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"卡牌 {sourceContentId} 的加权产出 {entry.CardId} 或权重无效。");
				}
				totalWeight = checked(totalWeight + entry.Weight);
			}
			if (totalWeight <= 0)
			{
				throw new InvalidOperationException(
					$"卡牌 {sourceContentId} 没有可抽取的加权产出。");
			}

			int roll = m_authoritativeRandom.NextInt(totalWeight);
			for (int i = 0; i < lootEntries.Count; i++)
			{
				CardLootEntry entry = lootEntries[i];
				if (roll < entry.Weight)
				{
					lootContentId = entry.CardId;
					return true;
				}
				roll -= entry.Weight;
			}
			throw new InvalidOperationException(
				$"卡牌 {sourceContentId} 的加权产出抽取没有命中任何条目。");
		}

		private void ResolveDefeatedParticipants(Battle battle)
		{
			List<ContentId> defeatedCardIds = null;
			List<TabletopPresentationCue> presentationCues = null;
			bool removedAny = false;
			for (int sideIndex = battle.SideCount - 1; sideIndex >= 0; sideIndex--)
			{
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[sideIndex].CardIds;
				for (int cardIndex = cardIds.Count - 1; cardIndex >= 0; cardIndex--)
				{
					TabletopCardId cardId = cardIds[cardIndex];
					CharacterCard character = RequireBattleCharacter(battle, cardId, "战败角色");
					if (character.CurrentHealth > 0f)
					{
						continue;
					}
					bool hasLoot = TrySelectCardLoot(character.ContentId, out ContentId lootContentId);
					Vector2 defeatPosition = character.Position;

					defeatedCardIds ??= new List<ContentId>();
					presentationCues ??= new List<TabletopPresentationCue>();
					defeatedCardIds.Add(character.ContentId);
					presentationCues.Add(TabletopPresentationCue.AtTablePosition(
						TabletopPresentationCueKind.CardSmoke,
						character.Position));
					battle.RemoveParticipant(cardId);
					UnbindCardFromActionPlans(cardId);
					Cards.RemoveCard(cardId);
					character.Dispose();
					RefreshPlacementRulesForCurrentCards(reflowExistingStacks: true);
					if (hasLoot)
					{
						CreateCard(
							lootContentId,
							defeatPosition,
							allowSpawnAttach: true);
					}
					removedAny = true;
				}
			}
			if (!removedAny)
			{
				return;
			}

			m_battleRevision++;
			if (battle.ParticipantCount < 2 || battle.ActiveSideCount < 2)
			{
				EndBattle(battle);
			}
			m_cardsDefeated(defeatedCardIds);
			for (int i = 0; i < presentationCues.Count; i++)
			{
				RequestPresentationCue(presentationCues[i]);
			}
		}

		/// <summary>结束牌桌并取消尚未结算的行动；终止后不允许再次写入。</summary>
		internal void End()
		{
			RequireActive();
			for (int i = m_activeBattles.Count - 1; i >= 0; i--)
			{
				m_activeBattles[i].End();
			}
			m_activeBattles.Clear();
			m_battleRevision++;
			for (int i = m_activeActions.Count - 1; i >= 0; i--)
			{
				m_activeActions[i].Cancel(ActionCancellationReason.ScenarioEnded);
			}
			m_activeActions.Clear();
			m_actionPlans.Clear();
			DisposeCharacters();
			m_realTimeSecondsPerTurn = 0f;
			m_authoritativeRandom = default(Unity.Mathematics.Random);
			ProgressionMode = ActionProgressionMode.TurnBased;
			IsEnded = true;
		}

		private void AdvanceActiveActions(float turnUnits)
		{
			for (int i = m_activeActions.Count - 1; i >= 0; i--)
			{
				ActionInstance action = m_activeActions[i];
				if (!AreActionParticipantsValid(action))
				{
					action.Cancel(ActionCancellationReason.ParticipantInvalidated);
					m_activeActions.RemoveAt(i);
				}
				else
				{
					action.Advance(turnUnits);
					if (action.State == ActionInstanceState.Completed)
					{
						m_activeActions.RemoveAt(i);
						CommitCompletedAction(action);
					}
				}
			}
		}

		private void CommitCompletedAction(ActionInstance action)
		{
			Unity.Mathematics.Random candidateRandom = m_authoritativeRandom;
			CommitCompletedAction(action, ref candidateRandom);
		}

		private void CommitCompletedAction(
			ActionInstance action,
			ref Unity.Mathematics.Random candidateRandom)
		{
			TabletopCardStack completedStack = null;
			if (action.Action.RecheckStackAfterCompletion)
			{
				TryFindStackContainingAction(action, out completedStack);
			}
			ActionSettlementResult result = ActionResultSettlement.Commit(
				action,
				this,
				m_isContentDiscovered,
				ref candidateRandom);
			m_authoritativeRandom = candidateRandom;
			m_actionCompleted(new TabletopActionCompletion(this, action, completedStack, result));
			ActionSettled?.Invoke(action.ActionId, result);
		}

		private string SelectResultBranch(
			ActionDefinition action,
			ref Unity.Mathematics.Random authoritativeRandom)
		{
			if (action.ResultBranches.Count == 0)
			{
				return string.Empty;
			}
			if (authoritativeRandom.state == 0)
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 声明了随机结果分支，但本局牌桌行动尚未初始化权威随机流。");
			}
			uint totalWeight = 0u;
			for (int i = 0; i < action.ResultBranches.Count; i++)
			{
				ActionResultBranchDefinition branch = action.ResultBranches[i];
				if (branch == null)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 包含空的随机结果分支。");
				}
				if (string.IsNullOrWhiteSpace(branch.Key))
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支缺少分支键。");
				}
				if (branch.Weight <= 0)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支 {branch.Key} 权重必须大于 0，当前值为 {branch.Weight}。");
				}
				for (int previousIndex = 0; previousIndex < i; previousIndex++)
				{
					ActionResultBranchDefinition previousBranch = action.ResultBranches[previousIndex];
					if (previousBranch != null && string.Equals(previousBranch.Key, branch.Key, StringComparison.Ordinal))
					{
						throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支键重复：{branch.Key}。");
					}
				}
				totalWeight = checked(totalWeight + (uint)branch.Weight);
			}
			uint roll = authoritativeRandom.NextUInt(totalWeight);
			for (int j = 0; j < action.ResultBranches.Count; j++)
			{
				ActionResultBranchDefinition branch2 = action.ResultBranches[j];
				uint branchWeight = (uint)branch2.Weight;
				if (roll < branchWeight)
				{
					return branch2.Key;
				}
				roll -= branchWeight;
			}
			throw new InvalidOperationException($"行动 {action.ContentId} 的权威随机结果没有命中任何分支。");
		}

		private void RequireActiveAction(ActionInstance action)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			if (!m_activeActions.Contains(action))
			{
				throw new InvalidOperationException($"行动实例 {action.ActionId} 不属于当前单局牌桌的活动集合。");
			}
		}

		private void RequireActiveBattle(Battle battle)
		{
			if (battle == null)
			{
				throw new ArgumentNullException(nameof(battle));
			}
			if (!m_activeBattles.Contains(battle))
			{
				throw new InvalidOperationException("指定战斗不属于当前牌桌的活动战斗集合。");
			}
		}

		/// <summary>为表现层读取活动战斗的权威区域；区域尺寸始终由当前参与人数派生。</summary>
		internal Rect GetBattleArea(Battle battle)
		{
			RequireActiveBattle(battle);
			return CalculateBattleArea(battle);
		}

		private Rect CalculateBattleArea(Battle battle, int additionalParticipantSideIndex = -1)
		{
			return m_battleFormation.CalculateArea(
				battle,
				PlacementRules.Geometry.CardSize,
				additionalParticipantSideIndex);
		}

		private Vector2 CalculateBattleAreaCenter(Battle battle)
		{
			Vector2 total = Vector2.zero;
			int participantCount = 0;
			for (int sideIndex = 0; sideIndex < battle.SideCount; sideIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[sideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					total += Cards.GetCardTablePosition(
						cardIds[cardIndex],
						PlacementRules.Geometry);
					participantCount++;
				}
			}
			return total / participantCount;
		}

		private List<Battle> FindOverlappingBattles(Rect area, Battle excludedBattle = null)
		{
			List<Battle> result = new List<Battle>();
			for (int index = 0; index < m_activeBattles.Count; index++)
			{
				Battle candidate = m_activeBattles[index];
				if (!ReferenceEquals(candidate, excludedBattle) &&
					AreasOverlap(area, CalculateBattleArea(candidate)))
				{
					result.Add(candidate);
				}
			}
			return result;
		}

		private static bool AreasOverlap(Rect left, Rect right)
		{
			return left.xMin <= right.xMax && left.xMax >= right.xMin &&
				left.yMin <= right.yMax && left.yMax >= right.yMin;
		}

		private static void RequireMatchingBattleSides(Battle destination, Battle source)
		{
			if (destination.SideCount != source.SideCount)
			{
				throw new InvalidOperationException(
					$"重叠战斗 {destination.Id} 与 {source.Id} 的战斗方数量不同，无法按战斗方顺序自动合并。");
			}
		}

		private static int[] CreateIdentitySideMapping(int sideCount)
		{
			int[] mapping = new int[sideCount];
			for (int index = 0; index < sideCount; index++)
			{
				mapping[index] = index;
			}
			return mapping;
		}

		private CharacterCard RequireBattleCharacter(
			Battle battle,
			TabletopCardId cardId,
			string role)
		{
			if (!battle.HasParticipant(cardId))
			{
				throw new InvalidOperationException(
					$"战斗能力的{role}卡牌 {cardId} 不属于战斗 {battle.Id}。");
			}
			if (!Cards.TryGetCard(cardId, out TabletopCard card) || card is not CharacterCard character)
			{
				throw new InvalidOperationException(
					$"战斗 {battle.Id} 的{role}卡牌 {cardId} 不再是当前牌桌中的角色卡，战斗状态已损坏。");
			}
			return character;
		}

		private void RestoreActiveActions(IReadOnlyList<ActionInstanceSnapshot> snapshots)
		{
			if (snapshots == null)
			{
				throw new ArgumentNullException(nameof(snapshots));
			}

			List<ActionInstance> restoredActions = new List<ActionInstance>(snapshots.Count);
			for (int snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
			{
				ActionInstanceSnapshot snapshot = snapshots[snapshotIndex];
				if (snapshot == null)
				{
					throw new InvalidOperationException($"活动行动快照的第 {snapshotIndex} 项为空。");
				}

				ActionRequest request = ActionRequest.FromSnapshot(snapshot);
				ActionCandidate candidate = CreateCandidateFromRequest(request);
				ActionInstance action = ActionInstance.Restore(candidate, snapshot);
				ActionResultSettlement.ValidateRestoredPlan(action, this);
				restoredActions.Add(action);
			}

			m_activeActions.AddRange(restoredActions);
		}

		private void DisposeCharacters()
		{
			for (int stackIndex = 0; stackIndex < Cards.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = Cards.Stacks[stackIndex];
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					if (stack.Cards[cardIndex] is CharacterCard characterCard)
					{
						characterCard.Dispose();
					}
				}
			}
		}

		private static bool CanUseTargetStackForDraggedSegment(
			TabletopCardStack sourceStack,
			TabletopCardId sourceCardId,
			TabletopCardStack targetStack,
			TabletopCardId targetCardId)
		{
			if (!ReferenceEquals(sourceStack, targetStack))
			{
				return true;
			}

			int segmentStartIndex = sourceStack.GetDraggedSegmentStartIndex(sourceCardId);
			int targetIndex = sourceStack.IndexOf(targetCardId);
			return segmentStartIndex > 0 && targetIndex >= 0 && targetIndex < segmentStartIndex;
		}

		private void RestoreLocalStackDrag(LocalStackDragState drag)
		{
			if (drag == null ||
				!drag.DetachedFromOriginalStack ||
				!Cards.TryGetStackContaining(drag.CardId, out TabletopCardStack currentDraggedStack) ||
				!ReferenceEquals(currentDraggedStack, drag.DraggedStack) ||
				!ContainsStack(drag.OriginalStack) ||
				drag.DraggedStack.Cards.Count == 0 ||
				drag.OriginalStack.Cards.Count == 0)
			{
				return;
			}

			Cards.MergeStackOnto(
				drag.DraggedStack.BottomCard.Id,
				drag.OriginalStack.BottomCard.Id);
		}

		private void ReconcileLocalStackDragAction(LocalStackDragState drag)
		{
			ActionInstance action = drag?.StackAction;
			if (action == null || !m_activeActions.Contains(action))
			{
				return;
			}
			if (ActionParticipantsRemainInStack(action, drag.OriginalStack))
			{
				if (drag.ActionPausedByDrag && action.State == ActionInstanceState.Paused)
				{
					action.Resume();
				}
				return;
			}

			action.Cancel(ActionCancellationReason.ParticipantInvalidated);
			m_activeActions.Remove(action);
		}

		private bool TryFindActiveActionOwnedByStack(TabletopCardStack stack, out ActionInstance action)
		{
			action = null;
			for (int actionIndex = 0; actionIndex < m_activeActions.Count; actionIndex++)
			{
				ActionInstance candidate = m_activeActions[actionIndex];
				if (!ActionParticipantsRemainInStack(candidate, stack))
				{
					continue;
				}
				if (action != null)
				{
					throw new InvalidOperationException(
						"一个牌堆同时承载了多个活动行动；当前 StackCraft 同态拖拽无法唯一复核行动生命周期。");
				}
				action = candidate;
			}
			return action != null;
		}

		private static bool ActionParticipantsRemainInStack(ActionInstance action, TabletopCardStack stack)
		{
			if (action == null || stack == null || stack.Cards.Count == 0)
			{
				return false;
			}
			for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = action.Bindings[bindingIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (stack.IndexOf(cardIds[cardIndex]) < 0)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static ActionCandidate RequireReadyStackActionCandidate(
			ActionCandidate candidate,
			int candidateIndex)
		{
			if (candidate == null)
			{
				throw new InvalidOperationException($"整堆制作候选的第 {candidateIndex + 1} 项为空。");
			}
			if (!candidate.IsReady)
			{
				throw new InvalidOperationException(
					$"整堆制作候选 {candidate.Action.ContentId} 尚未满足全部参与槽位，不能按 StackCraft 配方自动启动。");
			}
			return candidate;
		}

		internal bool HasActiveActionOnStack(TabletopCardStack stack)
		{
			return TryFindActiveActionOwnedByStack(stack, out _);
		}

		internal bool ContainsStack(TabletopCardStack stack)
		{
			for (int stackIndex = 0; stackIndex < Cards.Stacks.Count; stackIndex++)
			{
				if (ReferenceEquals(Cards.Stacks[stackIndex], stack))
				{
					return true;
				}
			}
			return false;
		}

		private bool TryFindStackContainingAction(
			ActionInstance action,
			out TabletopCardStack stack)
		{
			stack = null;
			for (int stackIndex = 0; stackIndex < Cards.Stacks.Count; stackIndex++)
			{
				TabletopCardStack candidate = Cards.Stacks[stackIndex];
				if (!ActionParticipantsRemainInStack(action, candidate))
				{
					continue;
				}
				if (stack != null)
				{
					throw new InvalidOperationException(
						$"行动 {action.ActionId} 的参与对象同时存在于多个牌堆，不能按 StackCraft 同堆制作语义复查。");
				}
				stack = candidate;
			}
			return stack != null;
		}

		private bool IsDroppingLocalDragBackToOriginalActionStack(
			TabletopCardStack sourceStack,
			TabletopCardStack targetStack)
		{
			LocalStackDragState drag = m_localStackDragState;
			return drag != null &&
				ReferenceEquals(drag.DraggedStack, sourceStack) &&
				ReferenceEquals(drag.OriginalStack, targetStack);
		}

		private bool CanRunningActionAcceptAdditionalStack(
			ActionInstance action,
			TabletopCardStack sourceStack,
			TabletopCardId incomingCardId)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}
			if (sourceStack == null || sourceStack.Cards.Count == 0)
			{
				return false;
			}
			if (!action.Action.AllowRunningStackAdditions)
			{
				return false;
			}
			int incomingCardIndex = sourceStack.IndexOf(incomingCardId);
			if (incomingCardIndex < 0)
			{
				throw new InvalidOperationException(
					$"运行中制作加料引用的起手牌 {incomingCardId} 不属于拖入牌堆。");
			}
			return CanRunningActionAcceptAdditionalCard(action.Action, sourceStack.Cards[incomingCardIndex]);
		}

		private bool CanRunningActionAcceptAdditionalCard(
			ActionDefinition action,
			TabletopCard card)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}
			if (card == null ||
				!m_contentIndex.TryGet(card.ContentId, out ContentAsset contentAsset))
			{
				return false;
			}
			AbilitySystemCell abilitySystemCell = card is CharacterCard characterCard
				? characterCard.AbilitySystem
				: null;
			for (int slotIndex = 0; slotIndex < action.ParticipationSlots.Count; slotIndex++)
			{
				if (ActionParticipationEvaluator.MatchesParticipant(
						action.ParticipationSlots[slotIndex],
						contentAsset,
						abilitySystemCell))
				{
					return true;
				}
			}
			return false;
		}

		private void RequireNoBattleParticipantInAffectedStack(TabletopCardId cardId, string operation)
		{
			TabletopCardStack stack = Cards.GetStackContaining(cardId);
			for (int index = 0; index < stack.Cards.Count; index++)
			{
				if (TryFindBattleContaining(stack.Cards[index].Id, out _))
				{
					throw new InvalidOperationException(
						$"{operation}会改变活动战斗参与牌 {stack.Cards[index].Id} 的位置或堆栈关系。必须先通过战斗命令离开或结束战斗。");
				}
			}
		}

		private void RequireNoBattleParticipantInDetachedTail(TabletopCardId cardId, string operation)
		{
			TabletopCardStack stack = Cards.GetStackContaining(cardId);
			int startIndex = stack.GetDraggedSegmentStartIndex(cardId);
			for (int index = startIndex; index < stack.Cards.Count; index++)
			{
				if (TryFindBattleContaining(stack.Cards[index].Id, out _))
				{
					throw new InvalidOperationException(
					$"{operation}会移动活动战斗参与牌 {stack.Cards[index].Id}。必须先通过战斗命令离开或结束战斗。");
				}
			}
		}

		private void RequireNoOtherBattleParticipantInDetachedTail(TabletopCardId cardId, string operation)
		{
			TabletopCardStack stack = Cards.GetStackContaining(cardId);
			int startIndex = stack.GetDraggedSegmentStartIndex(cardId);
			for (int index = startIndex; index < stack.Cards.Count; index++)
			{
				TabletopCardId candidateId = stack.Cards[index].Id;
				if (candidateId != cardId && TryFindBattleContaining(candidateId, out _))
				{
					throw new InvalidOperationException(
						$"{operation}会连带移动其它活动战斗参与牌 {candidateId}。必须先通过战斗命令离开或结束战斗。");
				}
			}
		}

		private bool TryFindBattleContaining(TabletopCardId cardId, out Battle battle)
		{
			for (int i = 0; i < m_activeBattles.Count; i++)
			{
				Battle candidate = m_activeBattles[i];
				if (candidate.HasParticipant(cardId))
				{
					battle = candidate;
					return true;
				}
			}

			battle = null;
			return false;
		}

		private bool IsActiveActionParticipant(TabletopCardId cardId)
		{
			for (int actionIndex = 0; actionIndex < m_activeActions.Count; actionIndex++)
			{
				ActionInstance action = m_activeActions[actionIndex];
				for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
				{
					IReadOnlyList<TabletopCardId> participantIds = action.Bindings[bindingIndex].CardIds;
					for (int cardIndex = 0; cardIndex < participantIds.Count; cardIndex++)
					{
						if (participantIds[cardIndex] == cardId)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private bool IsActionPlanParticipant(TabletopCardId cardId)
		{
			return IsActionPlanParticipant(cardId, ignoredPlan: null);
		}

		private bool IsActionPlanParticipant(TabletopCardId cardId, ActionPlan ignoredPlan)
		{
			for (int planIndex = 0; planIndex < m_actionPlans.Count; planIndex++)
			{
				if (ReferenceEquals(m_actionPlans[planIndex], ignoredPlan))
				{
					continue;
				}
				IReadOnlyList<ActionPlanBinding> bindings = m_actionPlans[planIndex].Bindings;
				for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
				{
					IReadOnlyList<TabletopCardId> cardIds = bindings[bindingIndex].CardIds;
					for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
					{
						if (cardIds[cardIndex] == cardId)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private void UnbindCardFromActionPlans(TabletopCardId cardId)
		{
			for (int planIndex = 0; planIndex < m_actionPlans.Count; planIndex++)
			{
				IReadOnlyList<ActionPlanBinding> bindings = m_actionPlans[planIndex].Bindings;
				for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
				{
					ActionPlanBinding binding = bindings[bindingIndex];
					for (int cardIndex = binding.CardIds.Count - 1; cardIndex >= 0; cardIndex--)
					{
						if (binding.CardIds[cardIndex] == cardId)
						{
							binding.Remove(cardId);
						}
					}
				}
			}
		}

		private bool AreActionParticipantsValid(ActionInstance action)
		{
			for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
			{
				ActionSlotBinding binding = action.Bindings[bindingIndex];
				if (!AreSlotParticipantsValid(binding.Slot, binding.CardIds))
				{
					return false;
				}
			}
			return true;
		}

		private void RequireOwnedActionPlan(ActionPlan plan)
		{
			if (plan == null)
			{
				throw new ArgumentNullException(nameof(plan));
			}
			if (!m_actionPlans.Contains(plan))
			{
				throw new InvalidOperationException(
					$"行动计划 {plan.ActionId} 不属于当前牌桌。");
			}
		}

		private void ValidateActionPlan(ActionPlan plan, bool requireComplete)
		{
			if (plan.Bindings.Count != plan.Action.ParticipationSlots.Count)
			{
				throw new InvalidOperationException(
					$"行动计划 {plan.ActionId} 的槽位数量与作者定义不一致。");
			}
			for (int i = 0; i < plan.Bindings.Count; i++)
			{
				ActionPlanBinding binding = plan.Bindings[i];
				if (!ReferenceEquals(binding.Slot, plan.Action.ParticipationSlots[i]))
				{
					throw new InvalidOperationException(
						$"行动计划 {plan.ActionId} 的第 {i + 1} 个槽位与作者定义不一致。");
				}
				if (requireComplete && !AreSlotParticipantsValid(binding.Slot, binding.CardIds))
				{
					throw new InvalidOperationException(
						$"行动计划 {plan.ActionId} 的槽位 {binding.Slot.Key} 尚未完整或参与对象已失效。");
				}
				for (int cardIndex = 0; cardIndex < binding.CardIds.Count; cardIndex++)
				{
					if (IsActionPlanParticipant(binding.CardIds[cardIndex], plan))
					{
						throw new InvalidOperationException(
							$"行动计划 {plan.ActionId} 的槽位 {binding.Slot.Key} 引用了其它待确认行动计划中的卡牌 {binding.CardIds[cardIndex]}。");
					}
				}
			}
		}

		internal ActionCandidate CreateCandidateFromRequest(ActionRequest request)
		{
			if (!m_contentIndex.TryGet(request.ActionId, out ActionDefinition action))
			{
				throw new InvalidOperationException($"行动请求引用的行动 {request.ActionId} 不在当前内容索引中。");
			}
			Dictionary<string, ActionRequestBinding> requestBindings = new Dictionary<string, ActionRequestBinding>(StringComparer.Ordinal);
			HashSet<TabletopCardId> usedCards = new HashSet<TabletopCardId>();
			for (int i = 0; i < request.Bindings.Count; i++)
			{
				ActionRequestBinding requestBinding = request.Bindings[i];
				if (requestBindings.ContainsKey(requestBinding.SlotKey))
				{
					throw new InvalidOperationException($"行动请求 {request.ActionId} 重复提交槽位 {requestBinding.SlotKey}。");
				}
				requestBindings.Add(requestBinding.SlotKey, requestBinding);
				for (int cardIndex = 0; cardIndex < requestBinding.CardIds.Count; cardIndex++)
				{
					TabletopCardId cardId = requestBinding.CardIds[cardIndex];
					if (!cardId.IsValid)
					{
						throw new InvalidOperationException($"行动请求 {request.ActionId} 的槽位 {requestBinding.SlotKey} 包含无效卡牌 ID。");
					}
					if (!usedCards.Add(cardId))
					{
						throw new InvalidOperationException($"行动请求 {request.ActionId} 重复绑定牌桌卡牌 {cardId}。");
					}
				}
			}
			List<ActionSlotBinding> bindings = new List<ActionSlotBinding>(action.ParticipationSlots.Count);
			for (int slotIndex = 0; slotIndex < action.ParticipationSlots.Count; slotIndex++)
			{
				ActionSlotDefinition slot = action.ParticipationSlots[slotIndex];
				if (!requestBindings.Remove(slot.Key, out var requestBinding2))
				{
					throw new InvalidOperationException($"行动请求 {request.ActionId} 缺少参与槽位 {slot.Key}。");
				}
				bindings.Add(new ActionSlotBinding(slot, requestBinding2.CardIds));
			}
			if (requestBindings.Count > 0)
			{
				using Dictionary<string, ActionRequestBinding>.KeyCollection.Enumerator enumerator = requestBindings.Keys.GetEnumerator();
				if (enumerator.MoveNext())
				{
					string unknownSlotKey = enumerator.Current;
					throw new InvalidOperationException($"行动请求 {request.ActionId} 包含当前作者源不存在的槽位 {unknownSlotKey}。");
				}
			}
			for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
			{
				ActionSlotBinding binding = bindings[bindingIndex];
				if (!AreSlotParticipantsValid(binding.Slot, binding.CardIds))
				{
					throw new InvalidOperationException(
						$"行动请求 {request.ActionId} 的槽位 {binding.Slot.Key} 参与对象已变化或不满足当前条件。");
				}
			}
			return new ActionCandidate(action, bindings, 0);
		}

		private ActionInstance StartActionInstance(ActionCandidate candidate, ActionPlan ignoredPlan = null)
		{
			for (int bindingIndex = 0; bindingIndex < candidate.Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = candidate.Bindings[bindingIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (TryFindBattleContaining(cardIds[cardIndex], out Battle battle))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardIds[cardIndex]} 仍属于活动战斗 {battle.Id}，必须先离开或结束战斗后才能启动普通行动。");
					}
					if (IsActiveActionParticipant(cardIds[cardIndex]))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardIds[cardIndex]} 已参与活动行动，必须先完成或取消该行动后才能启动新的普通行动。");
					}
					if (IsActionPlanParticipant(cardIds[cardIndex], ignoredPlan))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardIds[cardIndex]} 已填在待确认行动计划中，必须先提交或取消该计划后才能启动新的普通行动。");
					}
				}
			}

			int turnCost = candidate.Action.TurnCost;
			Unity.Mathematics.Random candidateRandom = m_authoritativeRandom;
			string resultBranchKey = SelectResultBranch(candidate.Action, ref candidateRandom);
			ActionResultPlan resultPlan = ActionResultSettlement.Compile(
				candidate.Action,
				candidate,
				resultBranchKey,
				m_contentIndex,
				Cards,
				PlacementRules,
				m_isContentDiscovered,
				ref candidateRandom);
			ActionInstance action = new ActionInstance(candidate, turnCost, resultBranchKey, resultPlan);
			if (action.State == ActionInstanceState.Running)
			{
				m_authoritativeRandom = candidateRandom;
				m_activeActions.Add(action);
			}
			else
			{
				CommitCompletedAction(action, ref candidateRandom);
			}
			return action;
		}

		private bool AreSlotParticipantsValid(
			ActionSlotDefinition slot,
			IReadOnlyList<TabletopCardId> cardIds)
		{
			if (!ActionParticipationEvaluator.IsParticipantCountSatisfied(slot, cardIds.Count))
			{
				return false;
			}
			for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
			{
				TabletopCardId cardId = cardIds[cardIndex];
				if (!Cards.TryGetCard(cardId, out var card) || !m_contentIndex.TryGet(card.ContentId, out var contentAsset))
				{
					return false;
				}
				AbilitySystemCell abilitySystemCell = card is CharacterCard characterCard
					? characterCard.AbilitySystem
					: null;
				if (!ActionParticipationEvaluator.MatchesParticipant(slot, contentAsset, abilitySystemCell))
				{
					return false;
				}
			}
			return true;
		}

		private int CalculateCardLimitBonus()
		{
			int cardLimitBonus = 0;
			IReadOnlyList<TabletopCardStack> stacks = Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					CardDefinition definition = RequireCardDefinition(cards[cardIndex].ContentId, "计算牌桌上限加成");
					cardLimitBonus = checked(cardLimitBonus + definition.CardLimitBonus);
				}
			}
			return cardLimitBonus;
		}

		private void RefreshPlacementRulesForCurrentCards(bool reflowExistingStacks)
		{
			int cardLimitBonus = CalculateCardLimitBonus();
			if (cardLimitBonus == m_currentPlacementCardLimitBonus)
			{
				return;
			}

			bool isShrinking = cardLimitBonus < m_currentPlacementCardLimitBonus;
			TabletopCardPlacementRules previousPlacementRules = m_currentPlacementRules;
			m_currentPlacementCardLimitBonus = cardLimitBonus;
			m_currentPlacementRules = m_basePlacementRules.CreateForCardLimitBonus(cardLimitBonus);
			Cards.MoveLockedStacksWithTopRestrictedBand(previousPlacementRules, PlacementRules);
			if (reflowExistingStacks && isShrinking)
			{
				Cards.ReflowPlacement(PlacementRules);
			}
		}

		private CardDefinition RequireCardDefinition(ContentId contentId, string operation)
		{
			if (!m_contentIndex.TryGet(contentId, out CardDefinition definition))
			{
				throw new InvalidOperationException($"{operation}引用的内容 {contentId} 缺失或不是卡牌定义。");
			}
			return definition;
		}

		private Vector2 ResolveCardSize(ContentId contentId, Vector2 defaultCardSize)
		{
			CardDefinition definition = RequireCardDefinition(contentId, "解析牌桌卡牌尺寸");
			return definition.GetViewSize(defaultCardSize);
		}

		private void RequireActive()
		{
			if (IsEnded)
			{
				throw new InvalidOperationException("牌桌所属剧本已经结束，不能再修改牌桌或执行行动。");
			}
		}

		private static float ValidateSecondsPerTurn(float secondsPerTurn)
		{
			if (!float.IsFinite(secondsPerTurn) || secondsPerTurn <= 0f)
			{
				throw new InvalidOperationException($"普通行动的每回合秒数必须是大于 0 的有限值，当前值为 {secondsPerTurn}。");
			}
			return secondsPerTurn;
		}

		private static bool IsFinitePosition(Vector2 position)
		{
			return float.IsFinite(position.x) && float.IsFinite(position.y);
		}
	}
}
