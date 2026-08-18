using System;
using System.Collections.Generic;
using Gameplay.Content;

namespace Gameplay.Quests
{
	/// <summary>任务在当前单局中的生命周期状态。</summary>
	public enum QuestStatus
	{
		Locked = 0,
		Active = 10,
		Completed = 20
	}

	/// <summary>
	/// 一个任务在当前剧本单局中的运行对象，拥有自己的状态和子项进度。
	/// </summary>
	public sealed class QuestProgress
	{
		private readonly QuestTaskRuntimeState[] m_tasks;
		private readonly IReadOnlyList<QuestTaskRuntimeState> m_readonlyTasks;

		internal QuestProgress(QuestDefinition definition)
		{
			Definition = definition ?? throw new ArgumentNullException(nameof(definition));
			m_tasks = new QuestTaskRuntimeState[definition.Tasks.Count];
			for (int i = 0; i < definition.Tasks.Count; i++)
			{
				QuestTaskDefinition taskDefinition = definition.Tasks[i];
				if (taskDefinition == null)
				{
					throw new InvalidOperationException("任务定义包含空任务子项。");
				}

				m_tasks[i] = taskDefinition.CreateRuntimeStateForQuestLog();
			}
			m_readonlyTasks = Array.AsReadOnly(m_tasks);
		}

		internal QuestProgress(QuestDefinition definition, QuestProgressSnapshot snapshot)
			: this(definition)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}
			if (snapshot.QuestId != definition.ContentId)
			{
				throw new InvalidOperationException(
					$"任务快照 {snapshot.QuestId} 与当前任务定义 {definition.ContentId} 不一致。");
			}
			if (!Enum.IsDefined(typeof(QuestStatus), snapshot.Status))
			{
				throw new InvalidOperationException($"任务 {snapshot.QuestId} 的存档状态无效：{snapshot.Status}。");
			}
			if (snapshot.Tasks == null || snapshot.Tasks.Count != m_tasks.Length)
			{
				throw new InvalidOperationException($"任务 {snapshot.QuestId} 的存档子项数量与当前作者源不一致。");
			}
			for (int i = 0; i < m_tasks.Length; i++)
			{
				m_tasks[i].RestoreSnapshotForQuestLog(snapshot.Tasks[i]);
			}
			bool allCompleted = AreAllTasksCompleted();
			if (snapshot.Status == QuestStatus.Completed && !allCompleted)
			{
				throw new InvalidOperationException($"任务 {snapshot.QuestId} 标记为完成，但仍有未完成子项。");
			}
			if (snapshot.Status != QuestStatus.Completed && allCompleted)
			{
				throw new InvalidOperationException($"任务 {snapshot.QuestId} 的子项已经全部完成，但任务状态不是完成。");
			}
			Status = snapshot.Status;
		}

		public QuestDefinition Definition { get; }

		public QuestStatus Status { get; private set; } = QuestStatus.Locked;

		public IReadOnlyList<QuestTaskRuntimeState> Tasks => m_readonlyTasks;

		internal QuestProgressSnapshot CreateSnapshot()
		{
			QuestTaskStateSnapshot[] tasks = new QuestTaskStateSnapshot[m_tasks.Length];
			for (int i = 0; i < m_tasks.Length; i++)
			{
				tasks[i] = m_tasks[i].CreateSnapshotForQuestLog();
			}
			return new QuestProgressSnapshot(Definition.ContentId, Status, tasks);
		}

		internal bool RecordFact(QuestTaskFact fact, out bool completed)
		{
			bool changed = false;
			for (int i = 0; i < m_tasks.Length; i++)
			{
				changed |= m_tasks[i].RecordFactFromQuestLog(fact);
			}
			completed = changed && AreAllTasksCompleted();
			return changed;
		}

		internal void Activate()
		{
			if (Status != QuestStatus.Locked)
			{
				throw new InvalidOperationException(
					$"任务 {Definition.ContentId} 只能从锁定状态激活，当前状态为 {Status}。");
			}
			Status = QuestStatus.Active;
		}

		internal void Complete()
		{
			if (Status != QuestStatus.Active)
			{
				throw new InvalidOperationException(
					$"任务 {Definition.ContentId} 只能从活动状态完成，当前状态为 {Status}。");
			}
			Status = QuestStatus.Completed;
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
}
