using System;

using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 任务进度的存档基类，保存任务资产引用并负责恢复到具体进度实例。
    /// </summary>
    [Serializable]
    public abstract class QuestTaskProgressDataBlock : DataBlock
    {
        /// <summary>
        /// 该进度对应的任务资产引用，读档时通过数据库解析回 QuestTask。
        /// </summary>
        public DatabaseEntryReference<QuestTask> task = null;

        /// <summary>
        /// 根据存档块创建对应的运行时任务进度对象。
        /// </summary>
        public abstract IQuestTaskProgress CreateInstance();
    }

    /// <summary>
    /// 任务进度运行时合同，统一任务开始监听、停止监听、完成判断和存档导出。
    /// </summary>
    public interface IQuestTaskProgress
    {
        /// <summary>
        /// 当前进度对应的任务资产。
        /// </summary>
        public QuestTask task { get; }

        /// <summary>
        /// 初始化进度并开始监听任务所需事件；完成后通过回调通知任务系统。
        /// </summary>
        public void Initialize(Action<IQuestTaskProgress> completionCallback);

        /// <summary>
        /// 显式停止追踪任务进度并注销事件。
        /// </summary>
        public void StopTracking();

        /// <summary>
        /// 返回当前进度是否已经达到任务完成条件。
        /// </summary>
        public bool IsCompleted();

        /// <summary>
        /// 导出可持久化的任务进度存档块。
        /// </summary>
        public QuestTaskProgressDataBlock CreateDataBlock();
    }

    /// <summary>
    /// 强类型任务进度基类，封装任务引用恢复、完成回调和通用存档字段。
    /// </summary>
    public abstract class QuestTaskProgress<T> : IQuestTaskProgress, IDataBlockHandler<T> where T : QuestTaskProgressDataBlock, new()
    {
        protected QuestTask m_task = null;
        private Action<IQuestTaskProgress> m_completionCallback;
        private bool m_isTracking;
        private bool m_completionNotified;

        QuestTask IQuestTaskProgress.task => m_task;

        public QuestTaskProgress(QuestTask task)
        {
            m_task = task;
        }

        public QuestTaskProgress(T block)
        {
            LoadDataBlock(block);
        }

        public void Initialize(Action<IQuestTaskProgress> completionCallback)
        {
            if (completionCallback == null)
            {
                throw new ArgumentNullException(nameof(completionCallback));
            }

            StopTracking();
            m_completionCallback = completionCallback;
            m_completionNotified = false;
            m_isTracking = true;
            OnProgressTrackingStarted();
            Update();
        }

        public void StopTracking()
        {
            if (!m_isTracking)
            {
                return;
            }

            m_isTracking = false;
            OnProgressTrackingStopped();
        }

        protected abstract void OnProgressTrackingStarted();
        protected abstract void OnProgressTrackingStopped();
        public abstract bool IsCompleted();
        protected virtual void Update() { }

        public void UpdateProgression()
        {
            if (!IsCompleted() || m_completionNotified)
            {
                return;
            }

            m_completionNotified = true;
            StopTracking();
            m_completionCallback?.Invoke(this);
        }

        public virtual void LoadDataBlock(T block)
        {
            m_task = GameManager.Database.LoadFromReference(block.task);
            if (!m_task)
            {
                Debug.LogError($"[{GetType().Name}] 存档中的任务子项 GUID 无法解析，已跳过：{block.task?.guid}");
            }
        }

        public virtual T CreateDataBlock()
        {
            if (!GameManager.Database.TryCreateReference(m_task, out DatabaseEntryReference<QuestTask> taskReference))
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name}] 任务子项 {m_task?.name ?? "<null>"} 未登记，不能写入任务子项进度存档。");
            }

            return new T
            {
                task = taskReference
            };
        }

        QuestTaskProgressDataBlock IQuestTaskProgress.CreateDataBlock() => CreateDataBlock();
    }
}
