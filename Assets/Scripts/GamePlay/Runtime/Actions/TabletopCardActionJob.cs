using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 牌桌行动作业的封闭生命周期。
    /// 这些状态由运行规则完整解释，不允许 Mod 增加系统无法推进的新状态。
    /// </summary>
    public enum TabletopCardActionJobState
    {
        Running = 0,
        Paused = 10,
        Completed = 20,
        Cancelled = 30
    }

    /// <summary>
    /// 普通牌桌行动当前使用的内置推进方式。
    /// 两种方式都推进同一份回合进度，因此切换时不转换作业数据。
    /// </summary>
    public enum TabletopCardActionProgressionMode
    {
        TurnBased = 0,
        RealTime = 10
    }

    /// <summary>
    /// 已开始作业进入取消终态的正式原因。
    /// 这里只记录真实生命周期结果，不把拒绝开始、作者配置错误或联机过期命令混成取消。
    /// </summary>
    public enum TabletopCardActionCancellationReason
    {
        None = 0,
        Requested = 10,
        ParticipantInvalidated = 20,
        SystemStopped = 30
    }

    /// <summary>
    /// 玩家显式选择普通牌桌行动后形成的一次作业。
    /// 作业只保存行动身份、参与绑定与进度，不重新判断条件，也不执行材料或结果结算。
    /// </summary>
    public sealed class TabletopCardActionJob
    {
        private readonly IReadOnlyList<TabletopCardActionSlotBinding> m_bindings;

        internal TabletopCardActionJob(
            TabletopCardActionCandidate candidate,
            int turnCost,
            string resultBranchKey)
        {
            ActionId = candidate.Action.ContentId;
            m_bindings = candidate.Bindings;
            ResultBranchKey = resultBranchKey ?? string.Empty;
            RequiresResultSettlement = candidate.Action.HasResultIntents;
            TurnCost = turnCost;
            State = turnCost == 0
                ? TabletopCardActionJobState.Completed
                : TabletopCardActionJobState.Running;
        }

        /// <summary>本次作业引用的唯一行动内容身份。</summary>
        public GamePlayContentId ActionId { get; }

        /// <summary>
        /// 行动开始时由权威随机流选定的结果分支键；没有随机分支时为空。
        /// </summary>
        public string ResultBranchKey { get; }

        /// <summary>开始作业时已经确定的参与槽位与牌桌卡牌绑定。</summary>
        public IReadOnlyList<TabletopCardActionSlotBinding> Bindings => m_bindings;

        /// <summary>本次作业从开始到完成所需的唯一回合消耗。</summary>
        public int TurnCost { get; }

        /// <summary>
        /// 当前行动作者源是否声明了需要正式结果 owner 结算的意图。
        /// </summary>
        internal bool RequiresResultSettlement { get; }

        /// <summary>本次作业已经推进的回合单位；即时制下可以包含小数。</summary>
        public float ProgressedTurns { get; private set; }

        /// <summary>当前唯一生命周期状态。</summary>
        public TabletopCardActionJobState State { get; private set; }

        /// <summary>作业进入取消终态的原因；未取消时为 <see cref="TabletopCardActionCancellationReason.None"/>。</summary>
        public TabletopCardActionCancellationReason CancellationReason { get; private set; }

        /// <summary>供表现层读取的归一化进度；零回合行动从创建起就是 1。</summary>
        public float Progress => TurnCost == 0
            ? 1f
            : Mathf.Clamp01(ProgressedTurns / TurnCost);

        /// <summary>
        /// 导出当前作业事实快照。快照只表达已经存在的作业状态，不提供恢复或写回入口。
        /// </summary>
        public TabletopCardActionJobSnapshot CreateSnapshot()
        {
            var bindingSnapshots = new List<TabletopCardActionJobBindingSnapshot>(Bindings.Count);
            for (int i = 0; i < Bindings.Count; i++)
            {
                TabletopCardActionSlotBinding binding = Bindings[i];
                bindingSnapshots.Add(new TabletopCardActionJobBindingSnapshot(
                    binding.Slot.Key,
                    binding.CardIds));
            }

            return new TabletopCardActionJobSnapshot(
                ActionId,
                TurnCost,
                ProgressedTurns,
                State,
                CancellationReason,
                ResultBranchKey,
                bindingSnapshots);
        }

        internal void Advance(float turnUnits)
        {
            if (State == TabletopCardActionJobState.Paused)
            {
                return;
            }

            if (State != TabletopCardActionJobState.Running)
            {
                throw new InvalidOperationException(
                    $"行动作业 {ActionId} 处于 {State}，只有运行中的作业可以推进。");
            }

            if (!float.IsFinite(turnUnits) || turnUnits <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnUnits),
                    turnUnits,
                    "行动作业每次推进的回合单位必须是大于 0 的有限数值。");
            }

            ProgressedTurns = Math.Min(TurnCost, ProgressedTurns + turnUnits);
            if (ProgressedTurns >= TurnCost)
            {
                State = TabletopCardActionJobState.Completed;
            }
        }

        internal void Pause()
        {
            RequireState(TabletopCardActionJobState.Running, "暂停");
            State = TabletopCardActionJobState.Paused;
        }

        internal void Resume()
        {
            RequireState(TabletopCardActionJobState.Paused, "恢复");
            State = TabletopCardActionJobState.Running;
        }

        internal void Cancel(TabletopCardActionCancellationReason reason)
        {
            if (State is not (TabletopCardActionJobState.Running or TabletopCardActionJobState.Paused))
            {
                throw new InvalidOperationException(
                    $"行动作业 {ActionId} 处于 {State}，只有运行或暂停中的作业可以取消。");
            }

            if (reason == TabletopCardActionCancellationReason.None)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), "取消行动作业必须提供明确原因。");
            }

            CancellationReason = reason;
            State = TabletopCardActionJobState.Cancelled;
        }

        private void RequireState(TabletopCardActionJobState expected, string operation)
        {
            if (State != expected)
            {
                throw new InvalidOperationException(
                    $"行动作业 {ActionId} 处于 {State}，不能执行{operation}；要求状态为 {expected}。");
            }
        }
    }

}
