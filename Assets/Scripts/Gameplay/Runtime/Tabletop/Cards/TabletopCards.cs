using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GAS.Runtime;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>牌桌原子提交时要新建的一整个牌堆。</summary>
	internal readonly struct TabletopCardCreationRequest
	{
		internal ContentId ContentId { get; }

		internal int Count { get; }

		internal Vector2 Position { get; }

		internal TabletopCardCreationRequest(ContentId contentId, int count, Vector2 position)
		{
			ContentId = contentId;
			Count = count;
			Position = position;
		}
	}

	/// <summary>牌桌原子提交时要把一张离桌卡牌按原局内 ID 恢复成单独牌堆。</summary>
	internal readonly struct TabletopCardRestorationRequest
	{
		internal TabletopCardSnapshot Snapshot { get; }

		internal Vector2 Position { get; }

		internal TabletopCardRestorationRequest(TabletopCardSnapshot snapshot, Vector2 position)
		{
			Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
			Position = position;
		}
	}

	/// <summary>
	/// 牌桌直接拥有的卡牌与牌堆集合，负责局内索引、成员关系和原子堆叠操作。
	/// </summary>
	public sealed class TabletopCards
	{
		private readonly TabletopCardIdSequence m_cardIdSequence;

		private readonly Dictionary<TabletopCardId, TabletopCard> m_cards = new Dictionary<TabletopCardId, TabletopCard>();

		private readonly List<TabletopCardStack> m_stacks = new List<TabletopCardStack>();

		private readonly ReadOnlyCollection<TabletopCardStack> m_readOnlyStacks;

		public int CardCount => m_cards.Count;

		public int StackCount => m_stacks.Count;

		public ulong Revision { get; private set; }

		public IReadOnlyList<TabletopCardStack> Stacks => m_readOnlyStacks;

		internal TabletopCardIdSequence CardIdSequence => m_cardIdSequence;

		internal TabletopCards(TabletopCardIdSequence cardIdSequence = null)
		{
			m_cardIdSequence = cardIdSequence ?? new TabletopCardIdSequence();
			m_readOnlyStacks = m_stacks.AsReadOnly();
		}

		public bool TryGetCard(TabletopCardId cardId, out TabletopCard tabletopCard)
		{
			return m_cards.TryGetValue(cardId, out tabletopCard);
		}

		internal TabletopCard CreateCard(
			ContentId contentId,
			Vector2 position,
			TabletopCardPlacementRules placementRules,
			bool isPlacementLocked = false,
			int initialUses = 1,
			Func<TabletopCardId, TabletopCard> createCard = null)
		{
			TabletopCardStack stack = CreateCardStack(
				contentId,
				1,
				position,
				placementRules,
				isPlacementLocked,
				initialUses,
				createCard);
			return stack.BottomCard;
		}

		internal TabletopCardStack CreateCardStack(
			ContentId contentId,
			int count,
			Vector2 position,
			TabletopCardPlacementRules placementRules,
			bool isPlacementLocked = false,
			int initialUses = 1,
			Func<TabletopCardId, TabletopCard> createCard = null)
		{
			TabletopCardId bottomCardId = CreateNextCardId(contentId, position, count);
			Dictionary<TabletopCardId, Vector2> solvedPositions =
				ResolveNewStackPlacement(bottomCardId, position, count, isPlacementLocked, placementRules);
			List<TabletopCard> cards = new List<TabletopCard>(count);
			try
			{
				for (int i = 0; i < count; i++)
				{
					TabletopCardId cardId = new TabletopCardId(m_cardIdSequence.NextValue + (ulong)i);
					TabletopCard card = createCard != null
						? createCard(cardId)
						: new TabletopCard(cardId, contentId, initialUses);
					if (card == null || card.Id != cardId || card.ContentId != contentId)
					{
						DisposeCharacterCard(card);
						throw new InvalidOperationException($"卡牌 {contentId} 的运行时工厂返回了空对象或错误身份。");
					}
					cards.Add(card);
				}

				return AddCardStack(
					cards,
					solvedPositions[bottomCardId],
					isPlacementLocked,
					solvedPositions);
			}
			catch
			{
				DisposeCharacterCards(cards);
				throw;
			}
		}

		public TabletopCardStateSnapshot CreateSnapshot()
		{
			TabletopCardStackSnapshot[] stackSnapshots = new TabletopCardStackSnapshot[m_stacks.Count];
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				TabletopCardSnapshot[] cardSnapshots = new TabletopCardSnapshot[stack.Cards.Count];
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					TabletopCard card = stack.Cards[cardIndex];
					cardSnapshots[cardIndex] = card.CreateSnapshot();
				}
				stackSnapshots[stackIndex] = new TabletopCardStackSnapshot(stack.Position, stack.IsPlacementLocked, cardSnapshots);
			}
			return new TabletopCardStateSnapshot(stackSnapshots);
		}

		internal static TabletopCards Restore(
			TabletopCardStateSnapshot snapshot,
			TabletopCardIdSequence cardIdSequence,
			Func<TabletopCardSnapshot, TabletopCard> restoreCard = null)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException("snapshot");
			}
			if (cardIdSequence == null)
			{
				throw new ArgumentNullException(nameof(cardIdSequence));
			}
			IReadOnlyList<TabletopCardStackSnapshot> stackSnapshots = snapshot.Stacks;
			if (stackSnapshots == null)
			{
				throw new InvalidOperationException("牌桌快照缺少堆栈集合。");
			}
			TabletopCards restored = new TabletopCards(cardIdSequence);
			try
			{
				ulong highestCardId = 0uL;
				for (int stackIndex = 0; stackIndex < stackSnapshots.Count; stackIndex++)
				{
					TabletopCardStackSnapshot stackSnapshot = stackSnapshots[stackIndex];
					if (stackSnapshot == null)
					{
						throw new InvalidOperationException($"牌桌快照的第 {stackIndex} 个堆栈为空。");
					}
					if (!IsFinitePosition(stackSnapshot.Position))
					{
						throw new InvalidOperationException($"牌桌快照的第 {stackIndex} 个堆栈位置不是有限二维坐标。");
					}
					IReadOnlyList<TabletopCardSnapshot> cardSnapshots = stackSnapshot.Cards;
					if (cardSnapshots == null || cardSnapshots.Count == 0)
					{
						throw new InvalidOperationException($"牌桌快照的第 {stackIndex} 个堆栈没有卡牌。");
					}
					List<TabletopCard> cards = new List<TabletopCard>(cardSnapshots.Count);
					for (int cardIndex = 0; cardIndex < cardSnapshots.Count; cardIndex++)
					{
						TabletopCardSnapshot cardSnapshot = cardSnapshots[cardIndex];
						if (cardSnapshot == null || !cardSnapshot.CardId.IsValid)
						{
							throw new InvalidOperationException($"牌桌快照堆栈 {stackIndex} 的第 {cardIndex} 张卡牌缺少有效局内 ID。");
						}
						if (!cardSnapshot.ContentId.IsValid)
						{
							throw new InvalidOperationException($"牌桌快照卡牌 {cardSnapshot.CardId} 缺少有效内容 ID。");
						}
						TabletopCard card = restoreCard != null
							? restoreCard(cardSnapshot)
							: new TabletopCard(
								cardSnapshot.CardId,
								cardSnapshot.ContentId,
								cardSnapshot.RemainingUses,
								cardSnapshot.PeriodicProductionElapsedSeconds,
								cardSnapshot.AutomaticMovementElapsedSeconds);
						if (card == null || card.Id != cardSnapshot.CardId || card.ContentId != cardSnapshot.ContentId)
						{
							DisposeCharacterCard(card);
							throw new InvalidOperationException($"牌桌快照卡牌 {cardSnapshot.CardId} 的恢复结果身份不一致。");
						}
						if (!restored.m_cards.TryAdd(card.Id, card))
						{
							DisposeCharacterCard(card);
							throw new InvalidOperationException($"牌桌快照包含重复局内卡牌 ID：{card.Id}。");
						}
						cards.Add(card);
						highestCardId = Math.Max(highestCardId, card.Id.Value);
					}
					TabletopCardStack stack = new TabletopCardStack(cards, stackSnapshot.Position, stackSnapshot.IsPlacementLocked);
					restored.m_stacks.Add(stack);
				}
				if (cardIdSequence.NextValue <= highestCardId)
				{
					throw new InvalidOperationException($"单局下一卡牌 ID {cardIdSequence.NextValue} 必须大于牌桌已存在最大 ID {highestCardId}。");
				}
				return restored;
			}
			catch
			{
				restored.DisposeCharacterCards();
				throw;
			}
		}

		private void DisposeCharacterCards()
		{
			foreach (TabletopCard card in m_cards.Values)
			{
				DisposeCharacterCard(card);
			}
		}

		private static void DisposeCharacterCards(IReadOnlyList<TabletopCard> cards)
		{
			if (cards == null)
			{
				return;
			}
			for (int i = 0; i < cards.Count; i++)
			{
				DisposeCharacterCard(cards[i]);
			}
		}

		private static void DisposeCharacterCard(TabletopCard card)
		{
			if (card is CharacterCard character)
			{
				character.Dispose();
			}
		}

		internal void RequireValidPlacement(TabletopCardPlacementRules placementRules)
		{
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			List<TabletopCardStackSpatialBody> bodies = CreateSpatialBodies(placementRules.Geometry);
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies);
			if (!result.Converged)
			{
				throw new InvalidOperationException("恢复的牌桌卡牌状态不满足当前剧本的放置规则。");
			}
			for (int i = 0; i < result.Bodies.Count; i++)
			{
				TabletopCardStackSpatialBody solved = result.Bodies[i];
				if (GetStackContaining(solved.BottomCardId).Position != solved.Position)
				{
					throw new InvalidOperationException(
						$"恢复的牌桌牌堆 {solved.BottomCardId} 需要被自动移动才能满足当前剧本规则，快照不能直接恢复。");
				}
			}
		}

		internal void ReflowPlacement(TabletopCardPlacementRules placementRules)
		{
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				CreateSpatialBodies(placementRules.Geometry));
			if (!result.Converged)
			{
				throw new InvalidOperationException("当前牌桌没有足够空间把现有牌堆收回新的放置边界内。");
			}
			if (ApplySolvedPositions(CreateSolvedPositionMap(result)))
			{
				Revision++;
			}
		}

		public TabletopCardStack GetStackContaining(TabletopCardId cardId)
		{
			if (!TryGetStackContaining(cardId, out var stack))
			{
				throw new KeyNotFoundException($"牌桌中不存在局内卡牌 {cardId}。");
			}
			return stack;
		}

		public bool TryGetStackContaining(TabletopCardId cardId, out TabletopCardStack stack)
		{
			if (m_cards.TryGetValue(cardId, out TabletopCard card) && card.Stack != null)
			{
				stack = card.Stack;
				return true;
			}

			stack = null;
			return false;
		}

		internal void RemoveCard(TabletopCardId cardId)
		{
			TabletopCardStack stack = GetStackContaining(cardId);
			stack.RemoveCard(cardId);
			m_cards.Remove(cardId);
			if (stack.Cards.Count == 0)
			{
				m_stacks.Remove(stack);
			}
			Revision++;
		}

		internal void EnsureCanCreateCards(int count)
		{
			m_cardIdSequence.EnsureAvailable(count);
		}

		internal TabletopCardStack MergeStackOnto(TabletopCardId sourceCardId, TabletopCardId targetCardId)
		{
			TabletopCardStack source = GetStackContaining(sourceCardId);
			TabletopCardStack target = GetStackContaining(targetCardId);
			if (source == target)
			{
				return target;
			}
			if (source.IsPlacementLocked)
			{
				throw new InvalidOperationException("锁定堆栈不能作为合堆来源移动。");
			}
			target.AppendOnTop(source);
			m_stacks.Remove(source);
			Revision++;
			return target;
		}

		internal TabletopCardStack DetachStackAt(TabletopCardId cardId)
		{
			TabletopCardStack source = GetStackContaining(cardId);
			int splitIndex = source.IndexOf(cardId);
			if (splitIndex == 0)
			{
				if (source.IsPlacementLocked)
				{
					throw new InvalidOperationException("锁定堆栈不能从底部整体移走。");
				}
				return source;
			}
			TabletopCardStack detached = source.DetachFrom(splitIndex);
			m_stacks.Add(detached);
			Revision++;
			return detached;
		}

		internal bool TryPlaceStack(TabletopCardId cardId, Vector2 position, TabletopCardPlacementRules placementRules, out TabletopCardStack placedStack)
		{
			if (!TryCreateStackPlacementPlan(cardId, position, placementRules, out StackPlacementPlan plan))
			{
				placedStack = null;
				return false;
			}

			TabletopCardStack source = plan.Source;
			bool changed = plan.SplitIndex > 0;
			if (plan.SplitIndex > 0)
			{
				placedStack = source.DetachFrom(plan.SplitIndex);
				m_stacks.Add(placedStack);
			}
			else
			{
				placedStack = source;
			}
			changed |= ApplySolvedPositions(plan.SolvedPositions);
			if (changed)
			{
				Revision++;
			}
			return true;
		}

		/// <summary>只抽出并放置指定卡牌；不同于玩家拖拽尾段牌堆，不会带走它上方的卡牌。</summary>
		internal bool TryPlaceSingleCard(
			TabletopCardId cardId,
			Vector2 position,
			TabletopCardPlacementRules placementRules,
			out TabletopCardStack placedStack)
		{
			if (!TryCreateSingleCardPlacementPlan(cardId, position, placementRules, out SingleCardPlacementPlan plan))
			{
				placedStack = null;
				return false;
			}

			bool changed = plan.Source.Cards.Count > 1;
			if (changed)
			{
				placedStack = plan.Source.DetachSingleAt(plan.CardIndex);
				m_stacks.Add(placedStack);
			}
			else
			{
				placedStack = plan.Source;
			}
			changed |= ApplySolvedPositions(plan.SolvedPositions);
			if (changed)
			{
				Revision++;
			}
			return true;
		}

		internal bool CanPlaceStack(TabletopCardId cardId, Vector2 position, TabletopCardPlacementRules placementRules)
		{
			return TryCreateStackPlacementPlan(cardId, position, placementRules, out _);
		}

		private bool TryCreateStackPlacementPlan(
			TabletopCardId cardId,
			Vector2 position,
			TabletopCardPlacementRules placementRules,
			out StackPlacementPlan plan)
		{
			if (!IsFinitePosition(position))
			{
				throw new ArgumentException("牌桌位置必须是有限二维坐标。", "position");
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException("placementRules");
			}
			TabletopCardStack source = GetStackContaining(cardId);
			int splitIndex = source.IndexOf(cardId);
			if (splitIndex == 0 && source.IsPlacementLocked)
			{
				throw new InvalidOperationException("锁定堆栈不能从底部整体移走。");
			}
			int candidateStackCount = m_stacks.Count + ((splitIndex > 0) ? 1 : 0);
			List<TabletopCardStackSpatialBody> spatialBodies = new List<TabletopCardStackSpatialBody>(candidateStackCount);
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				if (stack != source)
				{
					spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(stack.BottomCard.Id, stack.Position, stack.Cards.Count, stack.IsPlacementLocked));
					continue;
				}
				if (splitIndex == 0)
				{
					spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(source.BottomCard.Id, position, source.Cards.Count, source.IsPlacementLocked));
					continue;
				}
				spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(source.BottomCard.Id, source.Position, splitIndex, source.IsPlacementLocked));
				spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(cardId, position, source.Cards.Count - splitIndex, isLocked: false));
			}
			TabletopCardStackSpatialResult spatialResult = TabletopCardStackPlacementSolver.Solve(placementRules.Area, spatialBodies);
			if (!spatialResult.Converged)
			{
				plan = default;
				return false;
			}
			Dictionary<TabletopCardId, Vector2> solvedPositions = new Dictionary<TabletopCardId, Vector2>(spatialResult.Bodies.Count);
			for (int bodyIndex = 0; bodyIndex < spatialResult.Bodies.Count; bodyIndex++)
			{
				TabletopCardStackSpatialBody body = spatialResult.Bodies[bodyIndex];
				solvedPositions.Add(body.BottomCardId, body.Position);
			}
			for (int i = 0; i < spatialBodies.Count; i++)
			{
				if (!solvedPositions.ContainsKey(spatialBodies[i].BottomCardId))
				{
					throw new InvalidOperationException($"空间解算结果缺少底牌为 {spatialBodies[i].BottomCardId} 的候选堆栈。");
				}
			}

			plan = new StackPlacementPlan(source, splitIndex, solvedPositions);
			return true;
		}

		private bool TryCreateSingleCardPlacementPlan(
			TabletopCardId cardId,
			Vector2 position,
			TabletopCardPlacementRules placementRules,
			out SingleCardPlacementPlan plan)
		{
			if (!IsFinitePosition(position))
			{
				throw new ArgumentException("牌桌位置必须是有限二维坐标。", nameof(position));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			TabletopCardStack source = GetStackContaining(cardId);
			int cardIndex = source.IndexOf(cardId);
			if (cardIndex == 0 && source.IsPlacementLocked)
			{
				throw new InvalidOperationException("锁定堆栈不能把底牌抽出或整体移走。");
			}

			int candidateStackCount = m_stacks.Count + (source.Cards.Count > 1 ? 1 : 0);
			List<TabletopCardStackSpatialBody> spatialBodies = new List<TabletopCardStackSpatialBody>(candidateStackCount);
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				if (stack != source)
				{
					spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(
						stack.BottomCard.Id,
						stack.Position,
						stack.Cards.Count,
						stack.IsPlacementLocked));
					continue;
				}

				if (source.Cards.Count == 1)
				{
					spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(
						cardId,
						position,
						1,
						isLocked: false));
					continue;
				}

				TabletopCardId remainingBottomCardId = cardIndex == 0
					? source.Cards[1].Id
					: source.BottomCard.Id;
				spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(
					remainingBottomCardId,
					source.Position,
					source.Cards.Count - 1,
					source.IsPlacementLocked));
				spatialBodies.Add(placementRules.Geometry.CreateSpatialBody(
					cardId,
					position,
					1,
					isLocked: false));
			}

			TabletopCardStackSpatialResult spatialResult = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				spatialBodies);
			if (!spatialResult.Converged)
			{
				plan = default;
				return false;
			}

			Dictionary<TabletopCardId, Vector2> solvedPositions = new Dictionary<TabletopCardId, Vector2>(spatialResult.Bodies.Count);
			for (int bodyIndex = 0; bodyIndex < spatialResult.Bodies.Count; bodyIndex++)
			{
				TabletopCardStackSpatialBody body = spatialResult.Bodies[bodyIndex];
				solvedPositions.Add(body.BottomCardId, body.Position);
			}
			for (int i = 0; i < spatialBodies.Count; i++)
			{
				if (!solvedPositions.ContainsKey(spatialBodies[i].BottomCardId))
				{
					throw new InvalidOperationException($"空间解算结果缺少底牌为 {spatialBodies[i].BottomCardId} 的候选堆栈。");
				}
			}

			plan = new SingleCardPlacementPlan(source, cardIndex, solvedPositions);
			return true;
		}

		private bool ApplySolvedPositions(IReadOnlyDictionary<TabletopCardId, Vector2> solvedPositions)
		{
			bool changed = false;
			for (int k = 0; k < m_stacks.Count; k++)
			{
				TabletopCardStack stack = m_stacks[k];
				if (!solvedPositions.TryGetValue(stack.BottomCard.Id, out Vector2 solvedPosition))
				{
					throw new InvalidOperationException(
						$"牌桌放置结果缺少现有牌堆 {stack.BottomCard.Id}。");
				}
				if (stack.Position != solvedPosition)
				{
					stack.MoveTo(solvedPosition);
					changed = true;
				}
			}
			return changed;
		}

		private readonly struct StackPlacementPlan
		{
			internal TabletopCardStack Source { get; }

			internal int SplitIndex { get; }

			internal IReadOnlyDictionary<TabletopCardId, Vector2> SolvedPositions { get; }

			internal StackPlacementPlan(
				TabletopCardStack source,
				int splitIndex,
				IReadOnlyDictionary<TabletopCardId, Vector2> solvedPositions)
			{
				Source = source ?? throw new ArgumentNullException(nameof(source));
				SplitIndex = splitIndex;
				SolvedPositions = solvedPositions ?? throw new ArgumentNullException(nameof(solvedPositions));
			}
		}

		private readonly struct SingleCardPlacementPlan
		{
			internal TabletopCardStack Source { get; }

			internal int CardIndex { get; }

			internal IReadOnlyDictionary<TabletopCardId, Vector2> SolvedPositions { get; }

			internal SingleCardPlacementPlan(
				TabletopCardStack source,
				int cardIndex,
				IReadOnlyDictionary<TabletopCardId, Vector2> solvedPositions)
			{
				Source = source ?? throw new ArgumentNullException(nameof(source));
				CardIndex = cardIndex;
				SolvedPositions = solvedPositions ?? throw new ArgumentNullException(nameof(solvedPositions));
			}
		}

		private TabletopCardId CreateNextCardId(ContentId contentId, Vector2 position, int count = 1)
		{
			if (!contentId.IsValid)
			{
				throw new ArgumentException("牌桌卡牌必须引用有效的 Gameplay 内容 ID。", nameof(contentId));
			}
			if (count <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count), "新建牌堆必须至少包含一张卡牌。");
			}
			if (!IsFinitePosition(position))
			{
				throw new ArgumentException("牌桌位置必须是有限二维坐标。", nameof(position));
			}
			EnsureCanCreateCards(count);
			return m_cardIdSequence.PeekNext();
		}

		internal void ConsumeUse(TabletopCardId cardId)
		{
			if (!m_cards.TryGetValue(cardId, out TabletopCard card))
			{
				throw new KeyNotFoundException($"牌桌中不存在局内卡牌 {cardId}。");
			}
			card.ConsumeUse();
			Revision++;
		}

		internal void RestoreCardSnapshot(
			TabletopCardSnapshot snapshot,
			Vector2 position,
			TabletopCardPlacementRules placementRules,
			Func<TabletopCardSnapshot, TabletopCard> restoreCard)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}
			if (restoreCard == null)
			{
				throw new ArgumentNullException(nameof(restoreCard));
			}
			if (!snapshot.CardId.IsValid ||
				!snapshot.ContentId.IsValid ||
				snapshot.RemainingUses <= 0)
			{
				throw new InvalidOperationException("要恢复的离桌卡牌快照缺少有效身份、内容或剩余次数。");
			}
			if (snapshot.CardId.Value >= m_cardIdSequence.NextValue)
			{
				throw new InvalidOperationException(
					$"离桌卡牌 {snapshot.CardId} 的局内 ID 不小于下一分配号 {m_cardIdSequence.NextValue}，不能恢复。");
			}
			if (m_cards.ContainsKey(snapshot.CardId))
			{
				throw new InvalidOperationException($"牌桌已经包含局内卡牌 {snapshot.CardId}，不能重复恢复。");
			}

			Dictionary<TabletopCardId, Vector2> solvedPositions =
				ResolveRestoredCardPlacement(snapshot.CardId, position, placementRules);
			TabletopCard card = restoreCard(snapshot);
			if (card == null ||
				card.Id != snapshot.CardId ||
				card.ContentId != snapshot.ContentId)
			{
				DisposeCharacterCard(card);
				throw new InvalidOperationException($"离桌卡牌 {snapshot.CardId} 的恢复结果身份不一致。");
			}

			ApplySolvedPositions(solvedPositions);
			TabletopCardStack stack = new TabletopCardStack(
				card,
				solvedPositions[snapshot.CardId],
				isPlacementLocked: false);
			m_cards.Add(card.Id, card);
			m_stacks.Add(stack);
			Revision++;
		}

		internal void RequireCardChangesCanBePlaced(
			IReadOnlyList<TabletopCardId> removalCardIds,
			IReadOnlyList<TabletopCardCreationRequest> creations,
			IReadOnlyList<TabletopCardRestorationRequest> restorations,
			TabletopCardPlacementRules placementRules)
		{
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
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			HashSet<TabletopCardId> removals = new HashSet<TabletopCardId>();
			for (int i = 0; i < removalCardIds.Count; i++)
			{
				TabletopCardId cardId = removalCardIds[i];
				if (!m_cards.ContainsKey(cardId))
				{
					throw new InvalidOperationException($"牌桌变更引用了不存在的局内卡牌 {cardId}。");
				}
				if (!removals.Add(cardId))
				{
					throw new InvalidOperationException($"牌桌变更重复移除局内卡牌 {cardId}。");
				}
			}

			List<TabletopCardStackSpatialBody> bodies = new List<TabletopCardStackSpatialBody>(
				m_stacks.Count + creations.Count + restorations.Count);
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				TabletopCardId remainingBottomCardId = default;
				int remainingCount = 0;
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					TabletopCard card = stack.Cards[cardIndex];
					if (removals.Contains(card.Id))
					{
						continue;
					}
					if (remainingCount == 0)
					{
						remainingBottomCardId = card.Id;
					}
					remainingCount++;
				}
				if (remainingCount > 0)
				{
					bodies.Add(placementRules.Geometry.CreateSpatialBody(
						remainingBottomCardId,
						stack.Position,
						remainingCount,
						stack.IsPlacementLocked));
				}
			}

			int totalCreationCount = 0;
			for (int creationIndex = 0; creationIndex < creations.Count; creationIndex++)
			{
				TabletopCardCreationRequest creation = creations[creationIndex];
				if (creation.Count <= 0)
				{
					throw new InvalidOperationException("牌桌产物牌堆必须至少包含一张卡牌。");
				}
				Vector2 position = creation.Position;
				if (!IsFinitePosition(position))
				{
					throw new InvalidOperationException("牌桌产物位置必须是有限二维坐标。");
				}
				TabletopCardId bottomCardId = new TabletopCardId(m_cardIdSequence.NextValue + (ulong)totalCreationCount);
				bodies.Add(placementRules.Geometry.CreateSpatialBody(bottomCardId, position, creation.Count, isLocked: false));
				totalCreationCount = checked(totalCreationCount + creation.Count);
			}
			EnsureCanCreateCards(totalCreationCount);

			HashSet<TabletopCardId> restoredIds = new HashSet<TabletopCardId>();
			for (int restoreIndex = 0; restoreIndex < restorations.Count; restoreIndex++)
			{
				TabletopCardRestorationRequest restoration = restorations[restoreIndex];
				TabletopCardSnapshot snapshot = restoration.Snapshot;
				if (snapshot == null ||
					!snapshot.CardId.IsValid ||
					!snapshot.ContentId.IsValid ||
					snapshot.RemainingUses <= 0)
				{
					throw new InvalidOperationException("牌桌恢复请求包含无效卡牌快照。");
				}
				if (snapshot.CardId.Value >= m_cardIdSequence.NextValue)
				{
					throw new InvalidOperationException(
						$"离桌卡牌 {snapshot.CardId} 的局内 ID 不小于下一分配号 {m_cardIdSequence.NextValue}，不能恢复。");
				}
				if (m_cards.ContainsKey(snapshot.CardId) ||
					!restoredIds.Add(snapshot.CardId))
				{
					throw new InvalidOperationException($"牌桌恢复请求重复使用局内卡牌 ID {snapshot.CardId}。");
				}
				if (!IsFinitePosition(restoration.Position))
				{
					throw new InvalidOperationException("牌桌恢复位置必须是有限二维坐标。");
				}
				bodies.Add(placementRules.Geometry.CreateSpatialBody(
					snapshot.CardId,
					restoration.Position,
					1,
					isLocked: false));
			}

			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies);
			if (!result.Converged)
			{
				throw new InvalidOperationException("牌桌没有足够空间原子提交本次卡牌移除与产物创建。");
			}
		}

		private TabletopCardStack AddCardStack(
			IReadOnlyList<TabletopCard> cards,
			Vector2 position,
			bool isPlacementLocked,
			IReadOnlyDictionary<TabletopCardId, Vector2> solvedPositions)
		{
			ApplySolvedPositions(solvedPositions);
			TabletopCardStack stack = new TabletopCardStack(cards, position, isPlacementLocked);
			for (int i = 0; i < cards.Count; i++)
			{
				TabletopCard card = cards[i];
				m_cards.Add(card.Id, card);
				m_cardIdSequence.Commit(card.Id);
			}
			m_stacks.Add(stack);
			Revision++;
			return stack;
		}

		internal void RequireCanAcceptCards(
			IReadOnlyList<TabletopCard> cards,
			IReadOnlyList<Vector2> positions,
			TabletopCardPlacementRules placementRules)
		{
			ResolveTransferredCardPlacement(cards, positions, placementRules);
		}

		internal void TransferCardsTo(
			TabletopCards target,
			IReadOnlyList<TabletopCardId> cardIds,
			IReadOnlyList<Vector2> positions,
			TabletopCardPlacementRules targetPlacementRules)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}
			if (ReferenceEquals(this, target))
			{
				throw new InvalidOperationException("卡牌跨地区迁移的来源与目标牌桌不能相同。");
			}

			List<TabletopCard> cards = ResolveTransferCards(cardIds);
			Dictionary<TabletopCardId, Vector2> solvedPositions =
				target.ResolveTransferredCardPlacement(cards, positions, targetPlacementRules);
			for (int i = 0; i < cards.Count; i++)
			{
				RemoveCard(cards[i].Id);
			}

			target.ApplySolvedPositions(solvedPositions);
			for (int i = 0; i < cards.Count; i++)
			{
				TabletopCard card = cards[i];
				TabletopCardStack stack = new TabletopCardStack(card, solvedPositions[card.Id], isPlacementLocked: false);
				target.m_cards.Add(card.Id, card);
				target.m_stacks.Add(stack);
			}
			target.Revision++;
		}

		private List<TabletopCard> ResolveTransferCards(IReadOnlyList<TabletopCardId> cardIds)
		{
			if (cardIds == null)
			{
				throw new ArgumentNullException(nameof(cardIds));
			}
			if (cardIds.Count == 0)
			{
				throw new InvalidOperationException("跨地区旅行必须至少包含一张卡牌。");
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
				if (!m_cards.TryGetValue(cardId, out TabletopCard card))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 不属于来源地区牌桌。");
				}
				cards.Add(card);
			}
			return cards;
		}

		private Dictionary<TabletopCardId, Vector2> ResolveTransferredCardPlacement(
			IReadOnlyList<TabletopCard> cards,
			IReadOnlyList<Vector2> positions,
			TabletopCardPlacementRules placementRules)
		{
			if (cards == null)
			{
				throw new ArgumentNullException(nameof(cards));
			}
			if (positions == null || positions.Count != cards.Count)
			{
				throw new ArgumentException("旅行卡牌与目标位置数量必须一致。", nameof(positions));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			List<TabletopCardStackSpatialBody> bodies = CreateSpatialBodies(placementRules.Geometry);
			for (int i = 0; i < cards.Count; i++)
			{
				TabletopCard card = cards[i] ?? throw new InvalidOperationException("旅行卡牌列表包含空对象。");
				if (m_cards.ContainsKey(card.Id))
				{
					throw new InvalidOperationException($"目标地区牌桌已经包含局内卡牌 {card.Id}。");
				}
				if (!IsFinitePosition(positions[i]))
				{
					throw new InvalidOperationException($"旅行卡牌 {card.Id} 的目标位置不是有限二维坐标。");
				}
				bodies.Add(placementRules.Geometry.CreateSpatialBody(card.Id, positions[i], 1, isLocked: false));
			}

			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(placementRules.Area, bodies);
			if (!result.Converged)
			{
				throw new InvalidOperationException("目标地区牌桌没有足够空间接收旅行卡牌。");
			}
			return CreateSolvedPositionMap(result);
		}

		private Dictionary<TabletopCardId, Vector2> ResolveNewStackPlacement(
			TabletopCardId cardId,
			Vector2 requestedPosition,
			int cardCount,
			bool isPlacementLocked,
			TabletopCardPlacementRules placementRules)
		{
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			List<TabletopCardStackSpatialBody> bodies = CreateSpatialBodies(placementRules.Geometry);
			bodies.Add(placementRules.Geometry.CreateSpatialBody(
				cardId,
				requestedPosition,
				cardCount,
				isPlacementLocked));
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies);
			if (!result.Converged)
			{
				throw new InvalidOperationException(
					$"牌桌没有满足当前规则的空间用于创建卡牌 {cardId}。");
			}
			return CreateSolvedPositionMap(result);
		}

		private Dictionary<TabletopCardId, Vector2> ResolveRestoredCardPlacement(
			TabletopCardId cardId,
			Vector2 requestedPosition,
			TabletopCardPlacementRules placementRules)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("恢复牌桌卡牌必须提供有效局内 ID。", nameof(cardId));
			}
			if (!IsFinitePosition(requestedPosition))
			{
				throw new ArgumentException("牌桌位置必须是有限二维坐标。", nameof(requestedPosition));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			List<TabletopCardStackSpatialBody> bodies = CreateSpatialBodies(placementRules.Geometry);
			bodies.Add(placementRules.Geometry.CreateSpatialBody(
				cardId,
				requestedPosition,
				1,
				isLocked: false));
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies);
			if (!result.Converged)
			{
				throw new InvalidOperationException($"牌桌没有满足当前规则的空间用于恢复卡牌 {cardId}。");
			}
			return CreateSolvedPositionMap(result);
		}

		private List<TabletopCardStackSpatialBody> CreateSpatialBodies(TabletopCardStackGeometry geometry)
		{
			List<TabletopCardStackSpatialBody> bodies = new List<TabletopCardStackSpatialBody>(m_stacks.Count);
			for (int i = 0; i < m_stacks.Count; i++)
			{
				TabletopCardStack stack = m_stacks[i];
				bodies.Add(geometry.CreateSpatialBody(
					stack.BottomCard.Id,
					stack.Position,
					stack.Cards.Count,
					stack.IsPlacementLocked));
			}
			return bodies;
		}

		private static Dictionary<TabletopCardId, Vector2> CreateSolvedPositionMap(
			TabletopCardStackSpatialResult result)
		{
			Dictionary<TabletopCardId, Vector2> positions =
				new Dictionary<TabletopCardId, Vector2>(result.Bodies.Count);
			for (int i = 0; i < result.Bodies.Count; i++)
			{
				TabletopCardStackSpatialBody body = result.Bodies[i];
				positions.Add(body.BottomCardId, body.Position);
			}
			return positions;
		}

		private static bool IsFinitePosition(Vector2 position)
		{
			return float.IsFinite(position.x) && float.IsFinite(position.y);
		}
	}
}
