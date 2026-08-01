using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 单个任务进行中的存档块，保存任务资产、已完成子任务、当前子任务和后续队列。
    /// </summary>
    [Serializable]
    public class QuestProgressDataBlock : DataBlock
    {
        /// <summary>
        /// 当前进度对应的任务资产。
        /// </summary>
        public DatabaseEntryReference<Quest> quest;

        /// <summary>
        /// 已完成子任务的存档块。
        /// </summary>
        [SerializeReference] public QuestTaskProgressDataBlock[] completedTasks;

        /// <summary>
        /// 正在追踪的子任务存档块。
        /// </summary>
        [SerializeReference] public QuestTaskProgressDataBlock[] currentTasks;

        /// <summary>
        /// 仍未开始的后续子任务队列。
        /// </summary>
        public DatabaseEntryReference<QuestTask>[] nextTasks;
    }

    /// <summary>
    /// 单个任务的运行时进度，负责推进当前子任务、排队后续子任务并在全完成时通知日志系统。
    /// </summary>
    public class QuestProgress : IDataBlockHandler<QuestProgressDataBlock>
    {
        /// <summary>
        /// 当前进度对应的任务资产。
        /// </summary>
        public Quest quest => m_quest;

        private Quest m_quest = null;
        private List<IQuestTaskProgress> m_completedTasks = new();
        private List<IQuestTaskProgress> m_currentTasks = new();
        private Queue<QuestTask> m_nextTasks = new();
        private Action<QuestProgress> m_fullfilledCallback;
        private bool m_fullfilledNotified;

        public QuestProgress(Quest quest, Action<QuestProgress> fullfilledCallback)
        {
            m_quest = quest;
            m_fullfilledCallback = fullfilledCallback;
        }

        public QuestProgress(QuestProgressDataBlock block, Action<QuestProgress> fullfilledCallback)
        {
            m_fullfilledCallback = fullfilledCallback;
            LoadDataBlock(block);
        }

        /// <summary>
        /// 初始化任务进度，把任务资产中的子任务推入待执行队列。
        /// </summary>
        public void Initialize()
        {
            if (!quest)
            {
                Debug.LogError($"[{nameof(QuestProgress)}] 不能初始化缺失任务资产的进度。");
                return;
            }

            foreach (QuestTask task in quest.GetTasks())
            {
                if (task)
                {
                    m_nextTasks.Enqueue(task);
                }
                else
                {
                    Debug.LogError($"[{nameof(QuestProgress)}] 任务 {quest.name} 包含空子任务，已跳过。", quest);
                }
            }

            // 没有子任务的任务应立即进入可交付状态。
            CheckFullfillment();

            UpdateCurrentTasks();
        }

        public void LoadDataBlock(QuestProgressDataBlock block)
        {
            StopTracking();
            m_fullfilledNotified = false;
            if (block == null)
            {
                Debug.LogError($"[{nameof(QuestProgress)}] 不能从空任务进度存档块恢复任务。");
                m_quest = null;
                m_completedTasks = new List<IQuestTaskProgress>();
                m_currentTasks = new List<IQuestTaskProgress>();
                m_nextTasks = new Queue<QuestTask>();
                return;
            }

            m_quest = GameManager.Database.LoadFromReference(block.quest);
            if (!m_quest)
            {
                Debug.LogError($"[{nameof(QuestProgress)}] 存档中的任务 GUID 无法解析，已跳过：{block.quest?.guid}");
                m_completedTasks = new List<IQuestTaskProgress>();
                m_currentTasks = new List<IQuestTaskProgress>();
                m_nextTasks = new Queue<QuestTask>();
                return;
            }

            m_completedTasks = CreateTaskProgressInstances(block.completedTasks).ToList();
            m_currentTasks = CreateTaskProgressInstances(block.currentTasks).ToList();
            m_nextTasks = new Queue<QuestTask>(ResolveQuestTasks(block.nextTasks));

            foreach (IQuestTaskProgress task in m_currentTasks)
            {
                task.Initialize(OnTaskCompleted);
            }
        }

        public QuestProgressDataBlock CreateDataBlock()
        {
            if (!GameManager.Database.TryCreateReference(m_quest, out DatabaseEntryReference<Quest> questReference))
            {
                throw new InvalidOperationException(
                    $"[{nameof(QuestProgress)}] 任务 {m_quest?.name ?? "<null>"} 未登记，不能写入任务进度存档。");
            }

            return new QuestProgressDataBlock
            {
                quest = questReference,
                completedTasks = CreateTaskProgressDataBlocks(m_completedTasks, m_quest ? m_quest.name : "<null>"),
                currentTasks = CreateTaskProgressDataBlocks(m_currentTasks, m_quest ? m_quest.name : "<null>"),
                nextTasks = CreateQuestTaskReferences(m_nextTasks)
            };
        }

        private static IEnumerable<IQuestTaskProgress> CreateTaskProgressInstances(QuestTaskProgressDataBlock[] blocks)
        {
            if (blocks == null)
            {
                yield break;
            }

            foreach (QuestTaskProgressDataBlock block in blocks)
            {
                if (block == null || block.task == null || string.IsNullOrWhiteSpace(block.task.guid))
                {
                    continue;
                }

                IQuestTaskProgress progress = block.CreateInstance();
                if (progress?.task == null)
                {
                    continue;
                }

                yield return progress;
            }
        }

        private static IEnumerable<QuestTask> ResolveQuestTasks(DatabaseEntryReference<QuestTask>[] taskReferences)
        {
            if (taskReferences == null)
            {
                yield break;
            }

            foreach (DatabaseEntryReference<QuestTask> taskReference in taskReferences)
            {
                QuestTask task = GameManager.Database.LoadFromReference(taskReference);
                if (task)
                {
                    yield return task;
                    continue;
                }

                Debug.LogError($"[{nameof(QuestProgress)}] 存档中的后续任务子项 GUID 无法解析，已跳过：{taskReference?.guid}");
            }
        }

        private static QuestTaskProgressDataBlock[] CreateTaskProgressDataBlocks(
            IEnumerable<IQuestTaskProgress> progresses,
            string questName)
        {
            if (progresses == null)
            {
                return Array.Empty<QuestTaskProgressDataBlock>();
            }

            List<QuestTaskProgressDataBlock> blocks = new();
            foreach (IQuestTaskProgress progress in progresses)
            {
                if (progress == null)
                {
                    throw new InvalidOperationException(
                        $"[{nameof(QuestProgress)}] 任务 {questName} 包含空任务子项进度，不能写入部分存档。");
                }

                QuestTaskProgressDataBlock block = progress.CreateDataBlock();
                if (block?.task == null || string.IsNullOrWhiteSpace(block.task.guid))
                {
                    throw new InvalidOperationException(
                        $"[{nameof(QuestProgress)}] 任务 {questName} 的子项进度缺少稳定任务引用，不能写入部分存档。");
                }

                blocks.Add(block);
            }

            return blocks.ToArray();
        }

        private static DatabaseEntryReference<QuestTask>[] CreateQuestTaskReferences(IEnumerable<QuestTask> tasks)
        {
            if (tasks == null)
            {
                return Array.Empty<DatabaseEntryReference<QuestTask>>();
            }

            List<DatabaseEntryReference<QuestTask>> references = new();
            foreach (QuestTask task in tasks)
            {
                if (!GameManager.Database.TryCreateReference(task, out DatabaseEntryReference<QuestTask> reference))
                {
                    Debug.LogError($"[{nameof(QuestProgress)}] 后续任务子项 {task?.name ?? "<null>"} 未登记，已跳过。", task);
                    continue;
                }

                references.Add(reference);
            }

            return references.ToArray();
        }

        // 任务推进仍由 QuestProgress 持有，外部读取时只拿当前快照。
        public IQuestTaskProgress[] GetCompletedTasks() => m_completedTasks.ToArray();
        public IQuestTaskProgress[] GetCurrentTasks() => m_currentTasks.ToArray();
        public bool HasCompletedTask(QuestTask task) => m_completedTasks.Any(taskProgress => taskProgress.task == task);
        public bool HasCurrentTask(QuestTask task) => m_currentTasks.Any(taskProgress => taskProgress.task == task);
        public bool IsValid() => quest != null;

        /// <summary>
        /// 显式停止当前任务进度持有的事件监听。JournalSystem 在读档、系统停止和任务迁移时调用。
        /// </summary>
        public void StopTracking()
        {
            foreach (IQuestTaskProgress taskProgress in m_completedTasks)
            {
                taskProgress?.StopTracking();
            }

            foreach (IQuestTaskProgress taskProgress in m_currentTasks)
            {
                taskProgress?.StopTracking();
            }
        }

        /// <summary>
        /// 检查任务是否已经没有当前或后续子任务，并在满足时触发达成回调。
        /// </summary>
        public void CheckFullfillment()
        {
            if (!m_fullfilledNotified && m_currentTasks.Count == 0 && m_nextTasks.Count == 0)
            {
                m_fullfilledNotified = true;
                StopTracking();
                m_fullfilledCallback?.Invoke(this);
            }
        }

        /// <summary>
        /// 从后续队列推进当前子任务，遇到需要前置完成的子任务时暂停继续出队。
        /// </summary>
        public void UpdateCurrentTasks()
        {
            if (!quest)
            {
                return;
            }

            while (m_nextTasks.Count > 0)
            {
                QuestTask task = m_nextTasks.Dequeue();
                if (!task)
                {
                    Debug.LogError($"[{nameof(QuestProgress)}] 任务 {quest.name} 的后续子任务缺失，已跳过。", quest);
                    continue;
                }

                IQuestTaskProgress taskProgress = task.CreateTaskProgress();
                m_currentTasks.Add(taskProgress);
                taskProgress.Initialize(OnTaskCompleted);

                // 下一个子任务要求前置完成时，停止继续出队，等待当前批次完成。
                if (m_nextTasks.Count > 0 && m_nextTasks.Peek().requirePreviousTaskCompletion)
                {
                    return;
                }
            }

            CheckFullfillment();
        }

        private void OnTaskCompleted(IQuestTaskProgress taskProgress)
        {
            if (taskProgress == null || !m_currentTasks.Remove(taskProgress))
            {
                return;
            }

            taskProgress.StopTracking();
            m_completedTasks.Add(taskProgress);

            if (m_currentTasks.Count == 0)
            {
                if (m_nextTasks.Count > 0)
                {
                    UpdateCurrentTasks();
                }
                else
                {
                    CheckFullfillment();
                }
            }

            GameRuntimeEvents.NotifyQuestProgressionUpdated(quest);
        }

        /// <summary>
        /// 外部强制完成指定子任务，常用于对话或调试入口直接推进任务。
        /// </summary>
        public void CompleteTask(QuestTask task)
        {
            foreach (IQuestTaskProgress taskProgress in m_currentTasks.ToArray())
            {
                if (taskProgress.task == task)
                {
                    OnTaskCompleted(taskProgress);
                }
            }
        }
    }
}

