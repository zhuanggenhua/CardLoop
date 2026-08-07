using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GamePlay
{
    /// <summary>
    /// 牌桌行动作业的只读事实快照。它用于存档、断线恢复或调试记录的边界说明，
    /// 不负责恢复作业，也不允许外部修改行动系统状态。
    /// </summary>
    public sealed class TabletopCardActionJobSnapshot
    {
        private readonly ReadOnlyCollection<TabletopCardActionJobBindingSnapshot> m_bindings;

        internal TabletopCardActionJobSnapshot(
            GamePlayContentId actionId,
            int turnCost,
            float progressedTurns,
            TabletopCardActionJobState state,
            TabletopCardActionCancellationReason cancellationReason,
            string resultBranchKey,
            IReadOnlyList<TabletopCardActionJobBindingSnapshot> bindings)
        {
            ActionId = actionId;
            TurnCost = turnCost;
            ProgressedTurns = progressedTurns;
            State = state;
            CancellationReason = cancellationReason;
            ResultBranchKey = resultBranchKey ?? string.Empty;
            m_bindings = new List<TabletopCardActionJobBindingSnapshot>(
                bindings ?? throw new ArgumentNullException(nameof(bindings))).AsReadOnly();
        }

        public GamePlayContentId ActionId { get; }
        public int TurnCost { get; }
        public float ProgressedTurns { get; }
        public TabletopCardActionJobState State { get; }
        public TabletopCardActionCancellationReason CancellationReason { get; }
        public string ResultBranchKey { get; }
        public IReadOnlyList<TabletopCardActionJobBindingSnapshot> Bindings => m_bindings;
    }

    /// <summary>
    /// 已开始作业中的权威参与绑定事实。它与外部提交的请求绑定分开，避免把未校验输入当成作业状态。
    /// </summary>
    public sealed class TabletopCardActionJobBindingSnapshot
    {
        private readonly ReadOnlyCollection<TabletopCardId> m_cardIds;

        internal TabletopCardActionJobBindingSnapshot(
            string slotKey,
            IReadOnlyList<TabletopCardId> cardIds)
        {
            if (!GamePlayContentKeyUtility.IsValidKey(slotKey))
            {
                throw new ArgumentException($"行动作业快照槽位键无效：{slotKey}。", nameof(slotKey));
            }

            SlotKey = slotKey;
            m_cardIds = new List<TabletopCardId>(
                cardIds ?? throw new ArgumentNullException(nameof(cardIds))).AsReadOnly();
        }

        public string SlotKey { get; }
        public IReadOnlyList<TabletopCardId> CardIds => m_cardIds;
    }
}
