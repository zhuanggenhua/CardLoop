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

		internal TabletopCardId SpawnAttachIgnoredStackCardId { get; }

		/// <summary>创建时仅用于放置解算的临时锁定锚点；不改变牌堆的长期锁定状态。</summary>
		internal TabletopCardId PlacementLockedStackCardId { get; }

		/// <summary>只影响新卡首帧表现高度；StackCraft 开包产物会在源卡包当前高度上额外抬升 0.1。</summary>
		internal float SpawnPresentationHeightOffset { get; }

		/// <summary>新卡首帧表现的来源卡牌；仅用于读取当前拖起卡包的可见高度。</summary>
		internal TabletopCardId SpawnPresentationOriginCardId { get; }

		/// <summary>是否从当前拖拽高度开始出生；它与放置解算时的临时锁定是两个独立事实。</summary>
		internal bool UseDragHeightForSpawn { get; }

		internal TabletopCardCreationRequest(
			ContentId contentId,
			int count,
			Vector2 position,
			TabletopCardId spawnAttachIgnoredStackCardId = default,
			TabletopCardId placementLockedStackCardId = default,
			float spawnPresentationHeightOffset = 0f,
			TabletopCardId spawnPresentationOriginCardId = default,
			bool useDragHeightForSpawn = false)
		{
			if (!float.IsFinite(spawnPresentationHeightOffset) || spawnPresentationHeightOffset < 0f)
			{
				throw new ArgumentException(
					"卡牌创建请求的出生表现高度偏移必须是大于等于 0 的有限值。",
					nameof(spawnPresentationHeightOffset));
			}
			if (useDragHeightForSpawn && !spawnPresentationOriginCardId.IsValid)
			{
				throw new ArgumentException(
					"使用拖拽高度的卡牌创建请求必须引用当前被拖起的来源卡牌。",
					nameof(spawnPresentationOriginCardId));
			}
			ContentId = contentId;
			Count = count;
			Position = position;
			SpawnAttachIgnoredStackCardId = spawnAttachIgnoredStackCardId;
			PlacementLockedStackCardId = placementLockedStackCardId;
			SpawnPresentationHeightOffset = spawnPresentationHeightOffset;
			SpawnPresentationOriginCardId = spawnPresentationOriginCardId;
			UseDragHeightForSpawn = useDragHeightForSpawn;
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

		private readonly Func<ContentId, Vector2, Vector2> m_resolveCardSize;

		public int CardCount => m_cards.Count;

		public int StackCount => m_stacks.Count;

		public ulong Revision { get; private set; }

		public IReadOnlyList<TabletopCardStack> Stacks => m_readOnlyStacks;

		internal TabletopCardIdSequence CardIdSequence => m_cardIdSequence;

		internal TabletopCards(
			TabletopCardIdSequence cardIdSequence = null,
			Func<ContentId, Vector2, Vector2> resolveCardSize = null)
		{
			m_cardIdSequence = cardIdSequence ?? new TabletopCardIdSequence();
			m_resolveCardSize = resolveCardSize ?? ResolveDefaultCardSize;
			m_readOnlyStacks = m_stacks.AsReadOnly();
		}

		public bool TryGetCard(TabletopCardId cardId, out TabletopCard tabletopCard)
		{
			return m_cards.TryGetValue(cardId, out tabletopCard);
		}

		/// <summary>读取单张卡牌在当前牌堆外露序列里的桌面坐标。</summary>
		internal Vector2 GetCardTablePosition(
			TabletopCardId cardId,
			TabletopCardStackGeometry geometry)
		{
			TabletopCardStack stack = GetStackContaining(cardId);
			int cardIndex = stack.IndexOf(cardId);
			if (cardIndex < 0)
			{
				throw new InvalidOperationException($"牌桌卡牌 {cardId} 声明属于牌堆，但成员列表中不存在该卡牌。");
			}
			return stack.Position + geometry.StackStep * cardIndex;
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
			Func<TabletopCardId, TabletopCard> createCard = null,
			TabletopCardId placementLockedStackCardId = default)
		{
			TabletopCardId bottomCardId = CreateNextStackBottomCardId(contentId, position, count);
			Dictionary<TabletopCardId, Vector2> solvedPositions =
				ResolveNewStackPlacement(
					bottomCardId,
					contentId,
					position,
					count,
					isPlacementLocked,
					placementRules,
					placementLockedStackCardId);
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

		internal TabletopCardStack CreateCardStackAtRequestedPosition(
			ContentId contentId,
			int count,
			Vector2 position,
			bool isPlacementLocked = false,
			int initialUses = 1,
			Func<TabletopCardId, TabletopCard> createCard = null)
		{
			CreateNextStackBottomCardId(contentId, position, count);
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
					position,
					isPlacementLocked,
					solvedPositions: null);
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
			Func<TabletopCardSnapshot, TabletopCard> restoreCard = null,
			Func<ContentId, Vector2, Vector2> resolveCardSize = null)
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
			TabletopCards restored = new TabletopCards(cardIdSequence, resolveCardSize);
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
				bodies,
				placementRules.OverlapResolveMaxIterations);
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
			ReflowPlacement(placementRules, temporarilyLockedStackCardId: default);
		}

		internal void ReflowPlacementAfterDrop(TabletopCardPlacementRules placementRules)
		{
			ReflowPlacement(
				placementRules,
				temporarilyLockedStackCardId: default,
				requireConverged: false,
				unresolvedMessage: null,
				enforceInitialAreaConstraints: false);
		}

		internal void ReflowPlacementAfterSpawn(
			TabletopCardPlacementRules placementRules,
			TabletopCardId temporarilyLockedStackCardId)
		{
			ReflowPlacement(
				placementRules,
				temporarilyLockedStackCardId,
				requireConverged: false,
				unresolvedMessage: null,
				enforceInitialAreaConstraints: false);
		}

		internal void ReflowPlacement(
			TabletopCardPlacementRules placementRules,
			TabletopCardId temporarilyLockedStackCardId)
		{
			ReflowPlacement(
				placementRules,
				temporarilyLockedStackCardId,
				requireConverged: true,
				unresolvedMessage: "当前牌桌没有足够空间把现有牌堆收回新的放置边界内。");
		}

		private void ReflowPlacement(
			TabletopCardPlacementRules placementRules,
			TabletopCardId temporarilyLockedStackCardId,
			bool requireConverged,
			string unresolvedMessage,
			bool enforceInitialAreaConstraints = true)
		{
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			List<TabletopCardStackSpatialBody> bodies = CreateSpatialBodies(placementRules.Geometry);
			LockStackForCreationPlacementIfNeeded(
				bodies,
				placementRules.Geometry,
				temporarilyLockedStackCardId);
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies,
				placementRules.OverlapResolveMaxIterations,
				enforceInitialAreaConstraints);
			if (requireConverged && !result.Converged)
			{
				throw new InvalidOperationException(unresolvedMessage);
			}
			if (ApplySolvedPositions(CreateSolvedPositionMap(result)))
			{
				Revision++;
			}
		}

		internal bool MoveLockedStacksWithTopRestrictedBand(
			TabletopCardPlacementRules previousRules,
			TabletopCardPlacementRules currentRules)
		{
			if (previousRules == null)
			{
				throw new ArgumentNullException(nameof(previousRules));
			}
			if (currentRules == null)
			{
				throw new ArgumentNullException(nameof(currentRules));
			}
			if (!previousRules.Area.TryGetFullWidthTopRestrictedBand(out Rect previousBand) ||
				!currentRules.Area.TryGetFullWidthTopRestrictedBand(out Rect currentBand))
			{
				return false;
			}

			Vector2 delta = currentBand.center - previousBand.center;
			if (delta.sqrMagnitude <= 9.999999E-09f)
			{
				return false;
			}

			bool moved = false;
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				if (!stack.IsPlacementLocked)
				{
					continue;
				}

				Rect previousFootprint = CalculateFootprint(previousRules.Geometry, stack);
				if (!Overlaps(previousFootprint, previousBand))
				{
					continue;
				}

				stack.MoveTo(stack.Position + delta);
				moved = true;
			}

			if (moved)
			{
				Revision++;
			}
			return moved;
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
			target.MergeDroppedStack(source);
			m_stacks.Remove(source);
			Revision++;
			return target;
		}

		internal TabletopCardStack DetachStackAt(TabletopCardId cardId)
		{
			return DetachStackAt(cardId, null);
		}

		internal TabletopCardStack DetachStackAt(TabletopCardId cardId, Vector2? detachedStackPosition)
		{
			TabletopCardStack source = GetStackContaining(cardId);
			int splitIndex = source.GetDraggedSegmentStartIndex(cardId);
			if (splitIndex == 0)
			{
				if (source.IsPlacementLocked)
				{
					throw new InvalidOperationException("锁定堆栈不能整体移走。");
				}
				if (detachedStackPosition.HasValue && source.Position != detachedStackPosition.Value)
				{
					// StackCraft 用按下瞬间的可见卡牌位置作为拖拽起点，整叠拖拽也必须提交到同一锚点。
					source.MoveTo(detachedStackPosition.Value);
					Revision++;
				}
				return source;
			}
			TabletopCardStack detached = source.DetachFrom(splitIndex, detachedStackPosition ?? source.Position);
			m_stacks.Add(detached);
			Revision++;
			return detached;
		}

		internal bool TryPlaceStack(TabletopCardId cardId, Vector2 position, TabletopCardPlacementRules placementRules, out TabletopCardStack placedStack)
		{
			RequireStackPlacementInput(cardId, position, placementRules);
			TabletopCardStack source = GetStackContaining(cardId);
			int splitIndex = source.GetDraggedSegmentStartIndex(cardId);
			if (splitIndex == 0 && source.IsPlacementLocked)
			{
				throw new InvalidOperationException("锁定堆栈不能整体移走。");
			}

			bool changed = splitIndex > 0;
			if (splitIndex > 0)
			{
				placedStack = source.DetachFrom(splitIndex);
				m_stacks.Add(placedStack);
			}
			else
			{
				placedStack = source;
			}
			Vector2 releasedPosition = ResolveStackCraftReleasePosition(
				placedStack,
				position,
				placementRules);
			if (placedStack.Position != releasedPosition)
			{
				placedStack.MoveTo(releasedPosition);
				changed = true;
			}
			if (changed)
			{
				Revision++;
			}
			ReflowPlacementAfterDrop(placementRules);
			return true;
		}

		internal void MoveStackToStackCraftReleasePosition(
			TabletopCardStack stack,
			Vector2 requestedPosition,
			TabletopCardPlacementRules placementRules)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}
			RequireStackPlacementInput(stack.TopCard.Id, requestedPosition, placementRules);
			Vector2 releasedPosition = ResolveStackCraftReleasePosition(
				stack,
				requestedPosition,
				placementRules);
			if (stack.Position != releasedPosition)
			{
				// StackCraft 普通释放会先把被拖牌堆压回桌面落点，再尝试吸附到目标堆。
				stack.MoveTo(releasedPosition);
				Revision++;
			}
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
			RequireStackPlacementInput(cardId, position, placementRules);
			TabletopCardStack source = GetStackContaining(cardId);
			int splitIndex = source.GetDraggedSegmentStartIndex(cardId);
			if (splitIndex == 0 && source.IsPlacementLocked)
			{
				throw new InvalidOperationException("锁定堆栈不能整体移走。");
			}
			return true;
		}

		internal Vector2 ClampStackPositionToBounds(
			TabletopCardId cardId,
			Vector2 position,
			TabletopCardPlacementRules placementRules)
		{
			if (!IsFinitePosition(position))
			{
				throw new ArgumentException("牌桌拖拽位置必须是有限二维坐标。", nameof(position));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			TabletopCardStack stack = GetStackContaining(cardId);
			return placementRules.Geometry.ClampStackPositionToBounds(
				placementRules.Area.Bounds,
				position,
				stack.Cards.Count,
				ResolveCardSize(stack.TopCard.ContentId, placementRules.Geometry));
		}

		internal Vector2 MoveStackDuringLocalDrag(
			TabletopCardId cardId,
			Vector2 position,
			TabletopCardPlacementRules placementRules)
		{
			if (!IsFinitePosition(position))
			{
				throw new ArgumentException("牌桌拖拽位置必须是有限二维坐标。", nameof(position));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			TabletopCardStack stack = GetStackContaining(cardId);
			Vector2 clampedPosition = placementRules.Geometry.ClampStackPositionToBounds(
				placementRules.Area.Bounds,
				position,
				stack.Cards.Count,
				ResolveCardSize(stack.TopCard.ContentId, placementRules.Geometry));
			if ((stack.Position - clampedPosition).sqrMagnitude > 9.999999E-09f)
			{
				// StackCraft 拖拽中只更新当前牌堆的目标位置；牌堆成员和结构没有变化，不触发整桌刷新版本。
				stack.MoveTo(clampedPosition);
			}
			return clampedPosition;
		}

		private void RequireStackPlacementInput(
			TabletopCardId cardId,
			Vector2 position,
			TabletopCardPlacementRules placementRules)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("牌桌放置必须引用有效局内卡牌。", nameof(cardId));
			}
			if (!IsFinitePosition(position))
			{
				throw new ArgumentException("牌桌位置必须是有限二维坐标。", nameof(position));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}
		}

		private Vector2 ResolveStackCraftReleasePosition(
			TabletopCardStack stack,
			Vector2 requestedPosition,
			TabletopCardPlacementRules placementRules)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}
			List<TabletopCardStackSpatialBody> releasedBody = new List<TabletopCardStackSpatialBody>(1)
			{
				CreateSpatialBody(
					placementRules.Geometry,
					stack.BottomCard.Id,
					requestedPosition,
					stack.Cards.Count,
					isLocked: false,
					topCardContentId: stack.TopCard.ContentId)
			};
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				releasedBody,
				placementRules.OverlapResolveMaxIterations);
			if (result.Bodies.Count != 1)
			{
				throw new InvalidOperationException("牌桌释放位置解算必须只返回当前释放牌堆。");
			}
			return result.Bodies[0].Position;
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
				throw new InvalidOperationException("锁定堆栈不能把领牌抽出或整体移走。");
			}

			int candidateStackCount = m_stacks.Count + (source.Cards.Count > 1 ? 1 : 0);
			List<TabletopCardStackSpatialBody> spatialBodies = new List<TabletopCardStackSpatialBody>(candidateStackCount);
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				if (stack != source)
				{
					spatialBodies.Add(CreateSpatialBody(placementRules.Geometry, stack));
					continue;
				}

				if (source.Cards.Count == 1)
				{
					spatialBodies.Add(CreateSpatialBody(
						placementRules.Geometry,
						cardId,
						position,
						1,
						isLocked: false,
						topCardContentId: source.TopCard.ContentId));
					continue;
				}

				TabletopCardId remainingBottomCardId = cardIndex == source.Cards.Count - 1
					? source.Cards[source.Cards.Count - 2].Id
					: source.BottomCard.Id;
				int remainingTopIndex = cardIndex == 0
					? 1
					: 0;
				spatialBodies.Add(CreateSpatialBody(
					placementRules.Geometry,
					remainingBottomCardId,
					source.Position,
					source.Cards.Count - 1,
					source.IsPlacementLocked,
					source.Cards[remainingTopIndex].ContentId));
				spatialBodies.Add(CreateSpatialBody(
					placementRules.Geometry,
					cardId,
					position,
					1,
					isLocked: false,
					topCardContentId: source.Cards[cardIndex].ContentId));
			}

			TabletopCardStackSpatialResult spatialResult = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				spatialBodies,
				placementRules.OverlapResolveMaxIterations);

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

		private TabletopCardId CreateNextStackBottomCardId(ContentId contentId, Vector2 position, int count = 1)
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
			return new TabletopCardId(m_cardIdSequence.NextValue + (ulong)(count - 1));
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
				ResolveRestoredCardPlacement(snapshot.CardId, snapshot.ContentId, position, placementRules);
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
			TabletopCardPlacementRules placementRules,
			bool requirePlacementConverged = true)
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
				ContentId remainingTopCardContentId = default;
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
						remainingTopCardContentId = card.ContentId;
					}
					remainingBottomCardId = card.Id;
					remainingCount++;
				}
				if (remainingCount > 0)
				{
					bool isLocked = stack.IsPlacementLocked ||
						ShouldLockStackForCreationPlacement(stack, creations);
					bodies.Add(CreateSpatialBody(
						placementRules.Geometry,
						remainingBottomCardId,
						stack.Position,
						remainingCount,
						isLocked,
						remainingTopCardContentId));
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
				TabletopCardId bottomCardId = new TabletopCardId(
					m_cardIdSequence.NextValue + (ulong)(totalCreationCount + creation.Count - 1));
				bodies.Add(CreateSpatialBody(
					placementRules.Geometry,
					bottomCardId,
					position,
					creation.Count,
					isLocked: false,
					topCardContentId: creation.ContentId));
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
				bodies.Add(CreateSpatialBody(
					placementRules.Geometry,
					snapshot.CardId,
					restoration.Position,
					1,
					isLocked: false,
					topCardContentId: snapshot.ContentId));
			}

			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies,
				placementRules.OverlapResolveMaxIterations);
			if (requirePlacementConverged && !result.Converged)
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
			if (solvedPositions != null)
			{
				ApplySolvedPositions(solvedPositions);
			}
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
				bodies.Add(CreateSpatialBody(
					placementRules.Geometry,
					card.Id,
					positions[i],
					1,
					isLocked: false,
					topCardContentId: card.ContentId));
			}

			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies,
				placementRules.OverlapResolveMaxIterations);
			if (!result.Converged)
			{
				throw new InvalidOperationException("目标地区牌桌没有足够空间接收旅行卡牌。");
			}
			return CreateSolvedPositionMap(result);
		}

		private Dictionary<TabletopCardId, Vector2> ResolveNewStackPlacement(
			TabletopCardId cardId,
			ContentId contentId,
			Vector2 requestedPosition,
			int cardCount,
			bool isPlacementLocked,
			TabletopCardPlacementRules placementRules,
			TabletopCardId placementLockedStackCardId = default)
		{
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}

			List<TabletopCardStackSpatialBody> bodies = CreateSpatialBodies(placementRules.Geometry);
			LockStackForCreationPlacementIfNeeded(
				bodies,
				placementRules.Geometry,
				placementLockedStackCardId);
			bodies.Add(CreateSpatialBody(
				placementRules.Geometry,
				cardId,
				requestedPosition,
				cardCount,
				isPlacementLocked,
				contentId));
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies,
				placementRules.OverlapResolveMaxIterations);
			// StackCraft 生成卡牌时先把新牌放进桌面，再做有限轮重叠解算；同点出生的多张卡不会阻止生成。
			return CreateSolvedPositionMap(result);
		}

		private void LockStackForCreationPlacementIfNeeded(
			IList<TabletopCardStackSpatialBody> bodies,
			TabletopCardStackGeometry geometry,
			TabletopCardId placementLockedStackCardId)
		{
			if (!placementLockedStackCardId.IsValid)
			{
				return;
			}
			for (int stackIndex = 0; stackIndex < m_stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = m_stacks[stackIndex];
				if (stack.IndexOf(placementLockedStackCardId) < 0)
				{
					continue;
				}
				bodies[stackIndex] = CreateSpatialBody(
					geometry,
					stack.BottomCard.Id,
					stack.Position,
					stack.Cards.Count,
					isLocked: true,
					topCardContentId: stack.TopCard.ContentId);
				return;
			}
		}

		private static bool ShouldLockStackForCreationPlacement(
			TabletopCardStack stack,
			IReadOnlyList<TabletopCardCreationRequest> creations)
		{
			for (int creationIndex = 0; creationIndex < creations.Count; creationIndex++)
			{
				TabletopCardId cardId = creations[creationIndex].PlacementLockedStackCardId;
				if (cardId.IsValid && stack.IndexOf(cardId) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private Dictionary<TabletopCardId, Vector2> ResolveRestoredCardPlacement(
			TabletopCardId cardId,
			ContentId contentId,
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
			bodies.Add(CreateSpatialBody(
				placementRules.Geometry,
				cardId,
				requestedPosition,
				1,
				isLocked: false,
				topCardContentId: contentId));
			TabletopCardStackSpatialResult result = TabletopCardStackPlacementSolver.Solve(
				placementRules.Area,
				bodies,
				placementRules.OverlapResolveMaxIterations);
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
				bodies.Add(CreateSpatialBody(geometry, stack));
			}
			return bodies;
		}

		private TabletopCardStackSpatialBody CreateSpatialBody(
			TabletopCardStackGeometry geometry,
			TabletopCardStack stack)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}

			return geometry.CreateSpatialBody(
				stack.BottomCard.Id,
				stack.Position,
				stack.Cards.Count,
				stack.IsPlacementLocked,
				ResolveCardSize(stack.TopCard.ContentId, geometry));
		}

		private TabletopCardStackSpatialBody CreateSpatialBody(
			TabletopCardStackGeometry geometry,
			TabletopCardId bottomCardId,
			Vector2 position,
			int cardCount,
			bool isLocked,
			ContentId topCardContentId)
		{
			return geometry.CreateSpatialBody(
				bottomCardId,
				position,
				cardCount,
				isLocked,
				ResolveCardSize(topCardContentId, geometry));
		}

		private Rect CalculateFootprint(
			TabletopCardStackGeometry geometry,
			TabletopCardStack stack)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}

			return geometry.CalculateFootprint(
				stack.Position,
				stack.Cards.Count,
				ResolveCardSize(stack.TopCard.ContentId, geometry));
		}

		internal Vector2 ResolveCardSize(ContentId contentId, TabletopCardStackGeometry geometry)
		{
			Vector2 cardSize = m_resolveCardSize(contentId, geometry.CardSize);
			if (!float.IsFinite(cardSize.x) || !float.IsFinite(cardSize.y) || cardSize.x <= 0f || cardSize.y <= 0f)
			{
				throw new InvalidOperationException($"牌桌卡牌 {contentId} 的放置尺寸必须是有限正数，当前值为 {cardSize}。");
			}
			return cardSize;
		}

		private static Vector2 ResolveDefaultCardSize(ContentId contentId, Vector2 defaultCardSize)
		{
			return defaultCardSize;
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

		private static bool Overlaps(Rect first, Rect second)
		{
			return first.xMin < second.xMax &&
				first.xMax > second.xMin &&
				first.yMin < second.yMax &&
				first.yMax > second.yMin;
		}
	}
}
