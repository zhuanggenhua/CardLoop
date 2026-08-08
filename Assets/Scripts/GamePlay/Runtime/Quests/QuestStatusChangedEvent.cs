using Gameplay.Content;

namespace Gameplay.Quests
{
    /// <summary>
    /// 当前单局中的任务完成了一次生命周期状态变化。
    /// 这是直接交给 YokiFrame EventKit 的领域事实，不持有作者资产或运行系统引用。
    /// </summary>
    public readonly struct QuestStatusChangedEvent
    {
        public QuestStatusChangedEvent(
            ContentId questId,
            QuestStatus previousStatus,
            QuestStatus currentStatus)
        {
            QuestId = questId;
            PreviousStatus = previousStatus;
            CurrentStatus = currentStatus;
        }

        /// <summary>发生状态变化的任务唯一内容 ID。</summary>
        public ContentId QuestId { get; }

        /// <summary>本次变化前的任务状态。</summary>
        public QuestStatus PreviousStatus { get; }

        /// <summary>本次变化后已经提交的任务状态。</summary>
        public QuestStatus CurrentStatus { get; }
    }
}
