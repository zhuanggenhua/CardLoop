using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gameplay.Actions;
using Gameplay.Content;

namespace Gameplay.Tabletop.Actions
{
    /// <summary>
    /// 牌桌中尚未提交的行动计划。它保存一项行动当前填入的卡牌，写操作只能由所属牌桌执行。
    /// </summary>
    public sealed class ActionPlan
    {
        private readonly List<ActionPlanBinding> m_bindings;
        private readonly ReadOnlyCollection<ActionPlanBinding> m_readOnlyBindings;

        public ContentId ActionId => Action.ContentId;

        public ActionDefinition Action { get; }

        public IReadOnlyList<ActionPlanBinding> Bindings => m_readOnlyBindings;

        public int MissingParticipantCount
        {
            get
            {
                int missing = 0;
                for (int i = 0; i < m_bindings.Count; i++)
                {
                    missing += Math.Max(
                        0,
                        m_bindings[i].Slot.MinimumParticipants - m_bindings[i].CardIds.Count);
                }
                return missing;
            }
        }

        public bool IsReady => MissingParticipantCount == 0;

        internal ActionPlan(ActionCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            Action = candidate.Action;
            m_bindings = new List<ActionPlanBinding>(candidate.Bindings.Count);
            for (int i = 0; i < candidate.Bindings.Count; i++)
            {
                ActionSlotBinding binding = candidate.Bindings[i];
                m_bindings.Add(new ActionPlanBinding(binding.Slot, binding.CardIds));
            }
            m_readOnlyBindings = m_bindings.AsReadOnly();
        }

        internal ActionPlanBinding GetBinding(string slotKey)
        {
            for (int i = 0; i < m_bindings.Count; i++)
            {
                if (string.Equals(m_bindings[i].Slot.Key, slotKey, StringComparison.Ordinal))
                {
                    return m_bindings[i];
                }
            }
            throw new InvalidOperationException($"行动计划 {ActionId} 不包含槽位 {slotKey}。");
        }

        internal ActionRequest CreateRequest()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    $"行动计划 {ActionId} 仍缺少 {MissingParticipantCount} 个参与对象，不能提交。");
            }

            List<ActionRequestBinding> requestBindings =
                new List<ActionRequestBinding>(m_bindings.Count);
            for (int i = 0; i < m_bindings.Count; i++)
            {
                ActionPlanBinding binding = m_bindings[i];
                requestBindings.Add(new ActionRequestBinding(binding.Slot.Key, binding.CardIds));
            }
            return new ActionRequest(ActionId, requestBindings);
        }
    }

    /// <summary>行动计划中一个作者槽位当前填入的局内卡牌。</summary>
    public sealed class ActionPlanBinding
    {
        private readonly List<TabletopCardId> m_cardIds;
        private readonly ReadOnlyCollection<TabletopCardId> m_readOnlyCardIds;

        public ActionSlotDefinition Slot { get; }

        public IReadOnlyList<TabletopCardId> CardIds => m_readOnlyCardIds;

        internal ActionPlanBinding(ActionSlotDefinition slot, IReadOnlyList<TabletopCardId> cardIds)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            m_cardIds = new List<TabletopCardId>(cardIds ?? Array.Empty<TabletopCardId>());
            m_readOnlyCardIds = m_cardIds.AsReadOnly();
        }

        internal void Add(TabletopCardId cardId)
        {
            m_cardIds.Add(cardId);
        }

        internal void Remove(TabletopCardId cardId)
        {
            if (!m_cardIds.Remove(cardId))
            {
                throw new InvalidOperationException(
                    $"行动计划槽位 {Slot.Key} 没有绑定牌桌卡牌 {cardId}。");
            }
        }
    }
}
