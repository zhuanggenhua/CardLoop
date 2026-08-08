using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Actions;
using Gameplay.Content;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Quests
{
    /// <summary>任务在当前单局中的最小生命周期状态。</summary>
    public enum QuestStatus
    {
        Locked = 0,
        Active = 10,
        Completed = 20
    }

    /// <summary>
    /// 持有当前单局任务集合、任务状态和任务子项进度。
    /// 它吸收 FantasyWord JournalSystem / QuestProgress 的父级职责，但暂不接入交付、奖励、失败、存档和 UI。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestSystem : AGameSystem
    {
        private readonly Dictionary<ContentId, QuestRuntimeState> m_quests = new();
        private bool m_isRunning;
        private bool m_hasQuestSet;

        /// <summary>当前单局是否已经装入一组任务。</summary>
        public bool HasQuestSet => m_hasQuestSet;

        /// <summary>当前任务集合包含的任务数量。</summary>
        public int QuestCount => m_quests.Count;

        // GameManager 完成系统收集前不开放任务入口。
        private void Awake()
        {
            enabled = false;
        }

        /// <summary>进入正式运行阶段后允许开始任务集合，并监听已提交的行动事实。</summary>
        public override void OnSystemStart()
        {
            if (m_isRunning)
            {
                return;
            }

            m_isRunning = true;
            EventKit.Type.Register<TabletopCardActionCompletedEvent>(OnTabletopCardActionCompleted);
            enabled = true;
        }

        /// <summary>停止当前运行阶段并清空单局任务状态。</summary>
        public override void OnSystemStop()
        {
            enabled = false;
            EventKit.Type.UnRegister<TabletopCardActionCompletedEvent>(OnTabletopCardActionCompleted);
            m_isRunning = false;
            ClearQuestSet();
        }

        /// <summary>关闭系统时确保没有上一局任务状态残留。</summary>
        public override void OnSystemShutdown()
        {
            ClearQuestSet();
        }

        /// <summary>
        /// 从剧本已经选定的任务 ID 开始当前单局任务集合。
        /// 所有引用先完成校验，再一次性提交；根任务立即激活，其余任务等待全部前置完成。
        /// </summary>
        internal void StartQuestSet(
            IEnumerable<ContentId> questIds,
            ContentIndex contentIndex)
        {
            RequireRunningSystem();
            if (m_hasQuestSet)
            {
                throw new InvalidOperationException("当前单局任务集合已经开始，不能重复初始化。");
            }

            if (questIds == null)
            {
                throw new ArgumentNullException(nameof(questIds));
            }

            if (contentIndex == null)
            {
                throw new ArgumentNullException(nameof(contentIndex));
            }

            var pendingQuests = new Dictionary<ContentId, QuestRuntimeState>();
            foreach (ContentId questId in questIds)
            {
                if (!questId.IsValid)
                {
                    throw new InvalidOperationException("任务集合包含无效内容 ID。");
                }

                if (!contentIndex.TryGet(questId, out QuestDefinition definition))
                {
                    throw new InvalidOperationException(
                        $"任务集合引用的内容 {questId} 不存在或不是任务定义。");
                }

                if (!pendingQuests.TryAdd(
                        questId,
                        new QuestRuntimeState(definition, QuestStatus.Locked)))
                {
                    throw new InvalidOperationException($"任务集合重复包含任务 {questId}。");
                }
            }

            if (pendingQuests.Count == 0)
            {
                throw new InvalidOperationException("任务集合至少需要包含一个任务。");
            }

            foreach (QuestRuntimeState quest in pendingQuests.Values)
            {
                for (int i = 0; i < quest.Definition.PrerequisiteQuestIds.Count; i++)
                {
                    ContentId prerequisiteId = quest.Definition.PrerequisiteQuestIds[i];
                    if (!pendingQuests.ContainsKey(prerequisiteId))
                    {
                        throw new InvalidOperationException(
                            $"任务集合包含 {quest.Definition.ContentId}，但缺少它的前置任务 {prerequisiteId}。");
                    }
                }
            }

            foreach (KeyValuePair<ContentId, QuestRuntimeState> pair in pendingQuests)
            {
                m_quests.Add(pair.Key, pair.Value);
            }

            var statusChanges = new List<QuestStatusChangedEvent>();
            foreach (QuestRuntimeState quest in m_quests.Values)
            {
                if (quest.Definition.PrerequisiteQuestIds.Count == 0)
                {
                    ChangeStatus(quest, QuestStatus.Active, statusChanges);
                }
            }

            m_hasQuestSet = true;
            PublishStatusChanges(statusChanges);
        }

        /// <summary>由当前活动剧本结束它所开始的任务集合。</summary>
        internal void EndQuestSet()
        {
            RequireQuestSet();
            ClearQuestSet();
        }

        /// <summary>
        /// 提交一个已经由任务子项或剧本流程确认完成的活动任务，并激活所有前置已满足的任务。
        /// 未激活、重复完成或不属于当前集合的任务都会直接报错。
        /// </summary>
        internal void CompleteQuest(ContentId questId)
        {
            RequireQuestSet();
            if (!m_quests.TryGetValue(questId, out QuestRuntimeState quest))
            {
                throw new InvalidOperationException($"任务 {questId} 不属于当前单局任务集合。");
            }

            var statusChanges = new List<QuestStatusChangedEvent>();
            switch (quest.Status)
            {
                case QuestStatus.Locked:
                    throw new InvalidOperationException($"任务 {questId} 尚未激活，不能完成。");
                case QuestStatus.Completed:
                    throw new InvalidOperationException($"任务 {questId} 已经完成，不能重复完成。");
                case QuestStatus.Active:
                    ChangeStatus(quest, QuestStatus.Completed, statusChanges);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"任务 {questId} 处于未知状态：{quest.Status}。");
            }

            ActivateEligibleQuests(statusChanges);
            PublishStatusChanges(statusChanges);
        }

        /// <summary>读取当前单局中指定任务的生命周期状态。</summary>
        public QuestStatus GetStatus(ContentId questId)
        {
            RequireQuestSet();
            if (!m_quests.TryGetValue(questId, out QuestRuntimeState quest))
            {
                throw new InvalidOperationException($"任务 {questId} 不属于当前单局任务集合。");
            }

            return quest.Status;
        }

        private void ActivateEligibleQuests(List<QuestStatusChangedEvent> statusChanges)
        {
            foreach (QuestRuntimeState quest in m_quests.Values)
            {
                if (quest.Status != QuestStatus.Locked)
                {
                    continue;
                }

                bool prerequisitesCompleted = true;
                for (int i = 0; i < quest.Definition.PrerequisiteQuestIds.Count; i++)
                {
                    ContentId prerequisiteId = quest.Definition.PrerequisiteQuestIds[i];
                    if (m_quests[prerequisiteId].Status != QuestStatus.Completed)
                    {
                        prerequisitesCompleted = false;
                        break;
                    }
                }

                if (prerequisitesCompleted)
                {
                    ChangeStatus(quest, QuestStatus.Active, statusChanges);
                }
            }
        }

        /// <summary>
        /// 只让事实到达前已经激活的任务累计本次行动，避免完成前置任务的同一事实继续推进刚解锁的后继任务。
        /// </summary>
        private void OnTabletopCardActionCompleted(TabletopCardActionCompletedEvent completedEvent)
        {
            if (!m_hasQuestSet)
            {
                return;
            }

            var completedQuestIds = new List<ContentId>();
            foreach (KeyValuePair<ContentId, QuestRuntimeState> pair in m_quests)
            {
                QuestRuntimeState quest = pair.Value;
                if (quest.Status != QuestStatus.Active)
                {
                    continue;
                }

                if (quest.RecordActionCompletion(completedEvent.ActionId))
                {
                    completedQuestIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < completedQuestIds.Count; i++)
            {
                CompleteQuest(completedQuestIds[i]);
            }
        }

        private static void ChangeStatus(
            QuestRuntimeState quest,
            QuestStatus currentStatus,
            ICollection<QuestStatusChangedEvent> statusChanges)
        {
            QuestStatus previousStatus = quest.Status;
            quest.Status = currentStatus;
            statusChanges.Add(
                new QuestStatusChangedEvent(
                    quest.Definition.ContentId,
                    previousStatus,
                    currentStatus));
        }

        private static void PublishStatusChanges(
            IReadOnlyList<QuestStatusChangedEvent> statusChanges)
        {
            for (int i = 0; i < statusChanges.Count; i++)
            {
                EventKit.Type.Send(statusChanges[i]);
            }
        }

        private void RequireRunningSystem()
        {
            if (!m_isRunning)
            {
                throw new InvalidOperationException("任务系统尚未启动，不能开始或修改任务集合。");
            }
        }

        private void RequireQuestSet()
        {
            RequireRunningSystem();
            if (!m_hasQuestSet)
            {
                throw new InvalidOperationException("当前单局任务集合尚未开始。");
            }
        }

        private void ClearQuestSet()
        {
            m_quests.Clear();
            m_hasQuestSet = false;
        }

        private sealed class QuestRuntimeState
        {
            private readonly QuestTaskRuntimeState[] m_tasks;

            internal QuestRuntimeState(
                QuestDefinition definition,
                QuestStatus status)
            {
                Definition = definition;
                Status = status;
                m_tasks = new QuestTaskRuntimeState[definition.Tasks.Count];
                for (int i = 0; i < definition.Tasks.Count; i++)
                {
                    m_tasks[i] = QuestTaskRuntimeState.Create(definition.Tasks[i]);
                }
            }

            internal QuestDefinition Definition { get; }
            internal QuestStatus Status { get; set; }

            internal bool RecordActionCompletion(ContentId actionId)
            {
                bool changed = false;
                for (int i = 0; i < m_tasks.Length; i++)
                {
                    changed |= m_tasks[i].RecordActionCompletion(actionId);
                }

                return changed && AreAllTasksCompleted();
            }

            private bool AreAllTasksCompleted()
            {
                if (m_tasks.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < m_tasks.Length; i++)
                {
                    if (!m_tasks[i].IsCompleted)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private abstract class QuestTaskRuntimeState
        {
            internal abstract bool IsCompleted { get; }
            internal virtual bool RecordActionCompletion(ContentId actionId) => false;

            internal static QuestTaskRuntimeState Create(QuestTaskDefinition definition)
            {
                return definition switch
                {
                    ActionCompletionQuestTaskDefinition actionCompletion =>
                        new ActionCompletionQuestTaskRuntimeState(actionCompletion),
                    null => throw new InvalidOperationException("任务定义包含空任务子项。"),
                    _ => throw new InvalidOperationException(
                        $"任务子项类型 {definition.GetType().FullName} 没有登记运行时进度。")
                };
            }
        }

        private sealed class ActionCompletionQuestTaskRuntimeState : QuestTaskRuntimeState
        {
            private readonly ActionCompletionQuestTaskDefinition m_definition;
            private int m_completedCount;

            internal ActionCompletionQuestTaskRuntimeState(
                ActionCompletionQuestTaskDefinition definition)
            {
                m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
            }

            internal override bool IsCompleted =>
                m_completedCount >= m_definition.RequiredCompletionCount;

            internal override bool RecordActionCompletion(ContentId actionId)
            {
                if (IsCompleted || actionId != m_definition.ActionId)
                {
                    return false;
                }

                m_completedCount = checked(m_completedCount + 1);
                return true;
            }
        }
    }
}
