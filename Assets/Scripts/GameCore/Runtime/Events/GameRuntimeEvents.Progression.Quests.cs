namespace FantasyWord.GameCore
{
    /// <summary>
    /// 任务进度推进时发送的事件。它只描述任务真相变化，不承载条件判断或日志文案。
    /// </summary>
    public readonly struct QuestProgressionUpdatedEvent
    {
        public QuestProgressionUpdatedEvent(Quest quest)
        {
            Quest = quest;
        }

        public Quest Quest { get; }
    }

    /// <summary>
    /// 任务正式开始时发送的事件。它只描述任务状态迁移，不决定对话或菜单行为。
    /// </summary>
    public readonly struct QuestStartedEvent
    {
        public QuestStartedEvent(Quest quest)
            : this(quest, GameCommandContext.Script())
        {
        }

        public QuestStartedEvent(Quest quest, GameCommandContext commandContext)
        {
            Quest = quest;
            CommandContext = commandContext;
        }

        public Quest Quest { get; }
        public GameCommandContext CommandContext { get; }
    }

    /// <summary>
    /// 任务解锁时发送的事件。它只描述任务已进入日志系统，不决定可用性或 UI 表现。
    /// </summary>
    public readonly struct QuestUnlockedEvent
    {
        public QuestUnlockedEvent(Quest quest)
        {
            Quest = quest;
        }

        public Quest Quest { get; }
    }

    /// <summary>
    /// 任务可用性变化时发送的事件。它只描述可接状态变化，不决定具体交付或提示逻辑。
    /// </summary>
    public readonly struct QuestAvailabilityChangedEvent
    {
        public QuestAvailabilityChangedEvent(Quest quest, bool available)
        {
            Quest = quest;
            Available = available;
        }

        public Quest Quest { get; }

        public bool Available { get; }
    }

    /// <summary>
    /// 任务满足交付条件时发送的事件。它只描述日志系统状态变化，不决定奖励和对话流程。
    /// </summary>
    public readonly struct QuestFullfilledEvent
    {
        public QuestFullfilledEvent(Quest quest)
        {
            Quest = quest;
        }

        public Quest Quest { get; }
    }

    /// <summary>
    /// 任务完成时发送的事件。它只描述日志系统状态变化，不决定奖励和后续任务链逻辑。
    /// </summary>
    public readonly struct QuestCompletedEvent
    {
        public QuestCompletedEvent(Quest quest)
        {
            Quest = quest;
        }

        public Quest Quest { get; }
    }

    public static partial class GameRuntimeEvents
    {
        public static void NotifyQuestProgressionUpdated(Quest quest)
        {
            if (!quest)
            {
                return;
            }

            Publish(new QuestProgressionUpdatedEvent(quest));
        }

        public static void NotifyQuestStarted(Quest quest)
        {
            NotifyQuestStarted(quest, GameCommandContext.Script());
        }

        public static void NotifyQuestStarted(Quest quest, GameCommandContext commandContext)
        {
            if (!quest)
            {
                return;
            }

            Publish(new QuestStartedEvent(quest, commandContext));
        }

        public static void NotifyQuestUnlocked(Quest quest)
        {
            if (!quest)
            {
                return;
            }

            Publish(new QuestUnlockedEvent(quest));
        }

        public static void NotifyQuestAvailabilityChanged(Quest quest, bool available)
        {
            if (!quest)
            {
                return;
            }

            Publish(new QuestAvailabilityChangedEvent(quest, available));
        }

        public static void NotifyQuestFullfilled(Quest quest)
        {
            if (!quest)
            {
                return;
            }

            Publish(new QuestFullfilledEvent(quest));
        }

        public static void NotifyQuestCompleted(Quest quest)
        {
            if (!quest)
            {
                return;
            }

            Publish(new QuestCompletedEvent(quest));
        }
    }
}
