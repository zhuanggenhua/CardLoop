using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 按“底部到顶部”顺序保存牌桌卡牌的运行时堆栈。
    /// </summary>
    public sealed class TabletopCardStack
    {
        private readonly List<TabletopCard> m_cards;
        private readonly ReadOnlyCollection<TabletopCard> m_readOnlyCards;

        /// <summary>
        /// 仅供卡牌状态以一张局内卡牌创建新堆栈，外部模块不能绕过权威状态维护成员关系。
        /// </summary>
        internal TabletopCardStack(TabletopCard initialCard, Vector2 position, bool isPlacementLocked)
            : this(new[] { initialCard }, position, isPlacementLocked)
        {
        }

        private TabletopCardStack(
            IReadOnlyList<TabletopCard> cards,
            Vector2 position,
            bool isPlacementLocked)
        {
            m_cards = new List<TabletopCard>(cards);
            m_readOnlyCards = m_cards.AsReadOnly();
            Position = position;
            IsPlacementLocked = isPlacementLocked;
        }

        /// <summary>
        /// 从底部到顶部排列的只读成员列表。调用方不得维护第二份成员关系。
        /// </summary>
        public IReadOnlyList<TabletopCard> Cards => m_readOnlyCards;

        /// <summary>当前堆栈最底部的卡牌。</summary>
        public TabletopCard BottomCard => m_cards[0];

        /// <summary>当前堆栈最顶部的卡牌。</summary>
        public TabletopCard TopCard => m_cards[m_cards.Count - 1];

        /// <summary>
        /// 堆栈在牌桌二维坐标中的中心位置。只有 <see cref="TabletopCardState"/> 能提交变化。
        /// </summary>
        public Vector2 Position { get; private set; }

        /// <summary>
        /// 锁定后禁止整体移动或作为合堆来源，但仍允许拆走底牌之上的卡牌。
        /// </summary>
        public bool IsPlacementLocked { get; private set; }

        /// <summary>
        /// 保持来源成员顺序追加到当前堆顶部，并清空来源堆；调用方必须同步更新牌桌索引和堆栈列表。
        /// </summary>
        internal void AppendOnTop(TabletopCardStack source)
        {
            m_cards.AddRange(source.m_cards);
            source.m_cards.Clear();
        }

        /// <summary>返回卡牌自底向上的成员索引；卡牌不在本堆时返回 -1。</summary>
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

        internal void RemoveCard(TabletopCardId cardId)
        {
            int index = IndexOf(cardId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"堆栈中不存在牌桌卡牌 {cardId}。");
            }

            m_cards.RemoveAt(index);
        }

        /// <summary>
        /// 从指定卡牌开始拆出顶部尾段并生成未锁定的新堆；调用方负责登记新堆和卡牌归属。
        /// </summary>
        internal TabletopCardStack DetachFrom(int startIndex)
        {
            int detachedCount = m_cards.Count - startIndex;
            List<TabletopCard> detachedCards = m_cards.GetRange(startIndex, detachedCount);
            m_cards.RemoveRange(startIndex, detachedCount);
            return new TabletopCardStack(detachedCards, Position, false);
        }

        /// <summary>由权威卡牌状态提交新的二维位置；本类型不自行绕过锁定规则。</summary>
        internal void MoveTo(Vector2 position)
        {
            Position = position;
        }
    }
}
