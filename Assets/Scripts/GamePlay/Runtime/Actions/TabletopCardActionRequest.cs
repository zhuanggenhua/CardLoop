using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gameplay.Content;
using Gameplay.Tabletop;

namespace Gameplay.Actions
{
    /// <summary>
    /// 一次由 UI、AI、联机客户端或回放系统提交的牌桌行动请求。
    /// 请求只保存稳定行动 ID 与局内卡牌 ID，不持有 ScriptableObject 或候选对象引用；
    /// 真正启动前必须由 <see cref="TabletopCardActionSystem"/> 使用当前权威状态重新复核。
    /// </summary>
    public sealed class TabletopCardActionRequest
    {
        private readonly ReadOnlyCollection<TabletopCardActionRequestBinding> m_bindings;

        public TabletopCardActionRequest(
            ContentId actionId,
            IReadOnlyList<TabletopCardActionRequestBinding> bindings)
        {
            if (!actionId.IsValid)
            {
                throw new ArgumentException($"行动请求包含无效行动内容 ID：{actionId}。", nameof(actionId));
            }

            ActionId = actionId;
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            var copiedBindings = new List<TabletopCardActionRequestBinding>(bindings.Count);
            for (int i = 0; i < bindings.Count; i++)
            {
                copiedBindings.Add(bindings[i] ?? throw new ArgumentException(
                    $"行动请求 {actionId} 的第 {i} 个槽位绑定为空。",
                    nameof(bindings)));
            }

            m_bindings = copiedBindings.AsReadOnly();
        }

        /// <summary>玩家显式选择的行动内容 ID。</summary>
        public ContentId ActionId { get; }

        /// <summary>按行动作者槽位键提交的局内卡牌绑定。</summary>
        public IReadOnlyList<TabletopCardActionRequestBinding> Bindings => m_bindings;

        /// <summary>
        /// 从本地刚查询出的候选生成可提交请求。调用方随后仍必须交给行动系统重新复核，不能把候选快照直接当权威结果。
        /// </summary>
        public static TabletopCardActionRequest FromCandidate(TabletopCardActionCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var requestBindings = new List<TabletopCardActionRequestBinding>(candidate.Bindings.Count);
            for (int i = 0; i < candidate.Bindings.Count; i++)
            {
                TabletopCardActionSlotBinding binding = candidate.Bindings[i];
                requestBindings.Add(new TabletopCardActionRequestBinding(binding.Slot.Key, binding.CardIds));
            }

            return new TabletopCardActionRequest(candidate.Action.ContentId, requestBindings);
        }
    }

    /// <summary>
    /// 行动请求中的一个槽位绑定。槽位键只在所属行动内解释，卡牌 ID 只在当前局内有效。
    /// </summary>
    public sealed class TabletopCardActionRequestBinding
    {
        private readonly ReadOnlyCollection<TabletopCardId> m_cardIds;

        public TabletopCardActionRequestBinding(string slotKey, IReadOnlyList<TabletopCardId> cardIds)
        {
            if (!ContentIdRules.IsValidKey(slotKey))
            {
                throw new ArgumentException($"行动请求槽位键无效：{slotKey}。", nameof(slotKey));
            }

            SlotKey = slotKey;
            m_cardIds = new List<TabletopCardId>(
                cardIds ?? throw new ArgumentNullException(nameof(cardIds))).AsReadOnly();
        }

        /// <summary>行动内稳定槽位键。</summary>
        public string SlotKey { get; }

        /// <summary>绑定到该槽位的局内卡牌 ID。</summary>
        public IReadOnlyList<TabletopCardId> CardIds => m_cardIds;
    }
}
