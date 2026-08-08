using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 一局可堆叠卡牌实例、堆栈成员关系和位置的唯一可写状态。
    /// </summary>
    public sealed class TabletopCardState
    {
        private readonly Dictionary<TabletopCardId, TabletopCard> m_cards = new();
        private readonly Dictionary<TabletopCardId, TabletopCardStack> m_stackByCardId = new();
        private readonly List<TabletopCardStack> m_stacks = new();
        private readonly ReadOnlyCollection<TabletopCardStack> m_readOnlyStacks;
        private ulong m_nextCardId = 1;

        /// <summary>
        /// 创建一个空卡牌状态。状态本身不依赖 Unity 场景、单例或表现组件。
        /// </summary>
        public TabletopCardState()
        {
            m_readOnlyStacks = m_stacks.AsReadOnly();
        }

        /// <summary>当前仍登记的局内卡牌数量。</summary>
        public int CardCount => m_cards.Count;

        /// <summary>当前卡牌状态中的独立堆栈数量。</summary>
        public int StackCount => m_stacks.Count;

        /// <summary>
        /// 当前卡牌堆栈的实时只读视图，顺序就是权威状态的堆栈登记顺序。
        /// 调用方只能通过本类型的操作修改成员关系和位置。
        /// </summary>
        public IReadOnlyList<TabletopCardStack> Stacks => m_readOnlyStacks;

        /// <summary>
        /// 尝试读取一张仍由当前牌桌状态拥有的卡牌。
        /// 该入口供行动候选和表现查询解析局内引用，不授予调用方修改卡牌或堆栈的权限。
        /// </summary>
        public bool TryGetCard(TabletopCardId cardId, out TabletopCard tabletopCard)
        {
            return m_cards.TryGetValue(cardId, out tabletopCard);
        }

        /// <summary>
        /// 创建一张引用指定作者内容的局内卡牌，并为它建立独立单卡堆栈。
        /// 局内卡牌 ID 由本状态自动分配；内容 ID 无效或 ID 空间耗尽时抛出异常。
        /// </summary>
        public TabletopCard CreateCard(
            ContentId contentId,
            Vector2 position,
            bool isPlacementLocked = false)
        {
            if (!contentId.IsValid)
            {
                throw new ArgumentException("牌桌卡牌必须引用有效的 Gameplay 内容 ID。", nameof(contentId));
            }

            if (m_nextCardId == ulong.MaxValue)
            {
                throw new InvalidOperationException("本局牌桌卡牌 ID 已耗尽。");
            }

            var cardId = new TabletopCardId(m_nextCardId++);
            var tabletopCard = new TabletopCard(cardId, contentId);
            var stack = new TabletopCardStack(tabletopCard, position, isPlacementLocked);

            m_cards.Add(cardId, tabletopCard);
            m_stacks.Add(stack);
            m_stackByCardId.Add(cardId, stack);

            return tabletopCard;
        }

        /// <summary>
        /// 返回卡牌当前所在的堆栈。卡牌不存在时抛出 <see cref="KeyNotFoundException"/>，
        /// 避免调用方把失效的联机命令或运行时引用当成正常空结果继续执行。
        /// </summary>
        public TabletopCardStack GetStackContaining(TabletopCardId cardId)
        {
            if (!TryGetStackContaining(cardId, out TabletopCardStack stack))
            {
                throw new KeyNotFoundException($"牌桌中不存在局内卡牌 {cardId}。");
            }

            return stack;
        }

        /// <summary>
        /// 尝试返回卡牌当前所在的堆栈。
        /// 该入口供视图预览和输入命中处理卡牌可能已被权威状态移除的正常竞态。
        /// </summary>
        public bool TryGetStackContaining(TabletopCardId cardId, out TabletopCardStack stack)
        {
            return m_stackByCardId.TryGetValue(cardId, out stack);
        }

        /// <summary>
        /// 从牌桌状态移除一张卡牌。调用方不能直接修改堆栈成员，空堆栈会由本状态同步注销。
        /// </summary>
        public void RemoveCard(TabletopCardId cardId)
        {
            TabletopCardStack stack = GetStackContaining(cardId);
            stack.RemoveCard(cardId);
            m_cards.Remove(cardId);
            m_stackByCardId.Remove(cardId);
            if (stack.Cards.Count == 0)
            {
                m_stacks.Remove(stack);
            }
        }

        internal void EnsureCanCreateCards(int count)
        {
            if (count < 0 || (count > 0 && m_nextCardId > ulong.MaxValue - (ulong)count))
            {
                throw new InvalidOperationException("牌桌卡牌 ID 已不足以提交本次结果产物。");
            }
        }

        /// <summary>
        /// 把来源卡牌所在的完整堆栈追加到目标堆顶部，并保持双方原有成员顺序。
        /// 目标位置和锁定状态保持不变；来源堆被锁定时拒绝操作，同堆卡牌之间调用则不产生变化。
        /// </summary>
        public TabletopCardStack MergeStackOnto(
            TabletopCardId sourceCardId,
            TabletopCardId targetCardId)
        {
            TabletopCardStack source = GetStackContaining(sourceCardId);
            TabletopCardStack target = GetStackContaining(targetCardId);

            if (ReferenceEquals(source, target))
            {
                return target;
            }

            if (source.IsPlacementLocked)
            {
                throw new InvalidOperationException("锁定堆栈不能作为合堆来源移动。");
            }

            for (int i = 0; i < source.Cards.Count; i++)
            {
                m_stackByCardId[source.Cards[i].Id] = target;
            }

            target.AppendOnTop(source);
            m_stacks.Remove(source);
            return target;
        }

        /// <summary>
        /// 从指定卡牌开始，把该卡牌及其上方成员拆成新堆。
        /// 选择未锁定堆的底部时返回原堆；选择锁定堆底部会拒绝整体移动。
        /// </summary>
        public TabletopCardStack DetachStackAt(TabletopCardId cardId)
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

            for (int i = 0; i < detached.Cards.Count; i++)
            {
                m_stackByCardId[detached.Cards[i].Id] = detached;
            }

            return detached;
        }

        /// <summary>
        /// 提交卡牌所在堆栈的新牌桌位置。锁定堆栈会拒绝移动；
        /// 输入层的拖拽预览不能绕过本方法直接修改正式状态。
        /// </summary>
        public void MoveStack(TabletopCardId cardId, Vector2 position)
        {
            TabletopCardStack stack = GetStackContaining(cardId);
            if (stack.IsPlacementLocked)
            {
                throw new InvalidOperationException("锁定堆栈不能整体移动。");
            }

            stack.MoveTo(position);
        }
    }
}
