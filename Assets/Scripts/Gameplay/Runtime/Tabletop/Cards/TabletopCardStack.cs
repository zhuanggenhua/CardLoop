using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌中的牌堆对象，拥有有序卡牌成员、桌面位置和放置锁定状态。
	/// </summary>
	public sealed class TabletopCardStack
	{
		private readonly List<TabletopCard> m_cards;

		private readonly ReadOnlyCollection<TabletopCard> m_readOnlyCards;

		public IReadOnlyList<TabletopCard> Cards => m_readOnlyCards;

		/// <summary>StackCraft 语义里的领牌：牌堆列表第 0 张，拖拽整叠时它立即跟随指针。</summary>
		public TabletopCard TopCard => m_cards[0];

		/// <summary>StackCraft 语义里的目标底牌：牌堆列表最后一张，合堆规则和目标高亮都看它。</summary>
		public TabletopCard BottomCard => m_cards[m_cards.Count - 1];

		public Vector2 Position { get; private set; }

		public bool IsPlacementLocked { get; private set; }

		internal TabletopCardStack(TabletopCard initialCard, Vector2 position, bool isPlacementLocked)
			: this(new TabletopCard[1] { initialCard }, position, isPlacementLocked)
		{
		}

		internal TabletopCardStack(IReadOnlyList<TabletopCard> cards, Vector2 position, bool isPlacementLocked)
		{
			if (cards == null)
			{
				throw new ArgumentNullException("cards");
			}
			if (cards.Count == 0)
			{
				throw new ArgumentException("牌桌堆栈至少需要一张卡牌。", "cards");
			}
			if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
			{
				throw new ArgumentException("牌堆位置必须是有限二维坐标。", nameof(position));
			}

			var uniqueCards = new HashSet<TabletopCard>();
			for (int i = 0; i < cards.Count; i++)
			{
				TabletopCard card = cards[i] ?? throw new ArgumentException(
					$"牌堆的第 {i} 张卡牌为空。",
					nameof(cards));
				if (card.Stack != null)
				{
					throw new InvalidOperationException($"牌桌卡牌 {card.Id} 已属于另一个牌堆。");
				}
				if (!uniqueCards.Add(card))
				{
					throw new InvalidOperationException($"牌堆重复包含牌桌卡牌 {card.Id}。");
				}
			}

			m_cards = new List<TabletopCard>(cards.Count);
			m_readOnlyCards = m_cards.AsReadOnly();
			Position = position;
			IsPlacementLocked = isPlacementLocked;

			for (int i = 0; i < cards.Count; i++)
			{
				TabletopCard card = cards[i];
				card.AttachToStack(this);
				m_cards.Add(card);
			}
		}

		internal void MergeDroppedStack(TabletopCardStack source)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}
			if (ReferenceEquals(source, this))
			{
				throw new InvalidOperationException("牌堆不能合并到自身。");
			}

			for (int i = 0; i < source.m_cards.Count; i++)
			{
				source.m_cards[i].TransferToStack(source, this);
			}
			m_cards.AddRange(source.m_cards);
			source.m_cards.Clear();
		}

		internal int IndexOf(TabletopCardId cardId)
		{
			for (int i = 0; i < m_cards.Count; i++)
			{
				if (m_cards[i].Id == cardId)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// 计算玩家拖拽时应带走的牌段起点。对齐 StackCraft：点领牌拖整叠，点拖尾牌只拆出该牌和其后的拖尾牌。
		/// </summary>
		internal int GetDraggedSegmentStartIndex(TabletopCardId cardId)
		{
			int cardIndex = IndexOf(cardId);
			if (cardIndex < 0)
			{
				throw new KeyNotFoundException($"堆栈中不存在牌桌卡牌 {cardId}。");
			}
			return cardIndex == 0 ? 0 : cardIndex;
		}

		internal void RemoveCard(TabletopCardId cardId)
		{
			int index = IndexOf(cardId);
			if (index < 0)
			{
				throw new KeyNotFoundException($"堆栈中不存在牌桌卡牌 {cardId}。");
			}
			TabletopCard card = m_cards[index];
			m_cards.RemoveAt(index);
			card.DetachFromStack(this);
		}

		internal TabletopCardStack DetachFrom(int startIndex)
		{
			return DetachFrom(startIndex, Position);
		}

		internal TabletopCardStack DetachFrom(int startIndex, Vector2 detachedPosition)
		{
			if (startIndex <= 0 || startIndex >= m_cards.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex), "拆堆位置必须位于牌堆中间。");
			}

			int detachedCount = m_cards.Count - startIndex;
			List<TabletopCard> detachedCards = m_cards.GetRange(startIndex, detachedCount);
			m_cards.RemoveRange(startIndex, detachedCount);
			for (int i = 0; i < detachedCards.Count; i++)
			{
				detachedCards[i].DetachFromStack(this);
			}
			return new TabletopCardStack(detachedCards, detachedPosition, isPlacementLocked: false);
		}

		/// <summary>从当前牌堆抽出指定位置的一张卡，剩余卡牌继续留在原牌堆并保持相对顺序。</summary>
		internal TabletopCardStack DetachSingleAt(int cardIndex)
		{
			if (cardIndex < 0 || cardIndex >= m_cards.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(cardIndex), "抽出单张卡牌的位置必须位于牌堆内。");
			}
			if (m_cards.Count <= 1)
			{
				throw new InvalidOperationException("只有一张卡牌的牌堆不需要抽出单卡。");
			}

			TabletopCard detachedCard = m_cards[cardIndex];
			m_cards.RemoveAt(cardIndex);
			detachedCard.DetachFromStack(this);
			return new TabletopCardStack(detachedCard, Position, isPlacementLocked: false);
		}

		internal void MoveTo(Vector2 position)
		{
			if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
			{
				throw new ArgumentException("牌堆位置必须是有限二维坐标。", nameof(position));
			}
			Position = position;
		}
	}
}
