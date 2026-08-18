using System;
using System.Collections.Generic;
using Gameplay.Content;
using YokiFrame;

namespace Gameplay.Quests
{
	/// <summary>
	/// 当前剧本单局的任务集合；它是任务状态的唯一写入口和事实分发者。
	/// </summary>
	public sealed class QuestLog
	{
		private readonly ContentId m_scenarioId;
		private readonly Dictionary<ContentId, QuestProgress> m_quests =
			new Dictionary<ContentId, QuestProgress>();
		private readonly List<QuestProgress> m_questOrder = new List<QuestProgress>();
		private readonly IReadOnlyList<QuestProgress> m_readOnlyQuests;

		public int QuestCount => m_quests.Count;

		public int CompletedQuestCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < m_questOrder.Count; i++)
				{
					if (m_questOrder[i].Status == QuestStatus.Completed)
					{
						count++;
					}
				}
				return count;
			}
		}

		/// <summary>按剧本作者声明顺序读取当前单局的任务，不提供集合写入口。</summary>
		public IReadOnlyList<QuestProgress> Quests => m_readOnlyQuests;

		internal QuestLog(ContentId scenarioId, IEnumerable<ContentId> questIds, ContentIndex contentIndex)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("任务日志必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			if (questIds == null)
			{
				throw new ArgumentNullException(nameof(questIds));
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}

			m_scenarioId = scenarioId;
			m_readOnlyQuests = m_questOrder.AsReadOnly();
			foreach (ContentId questId in questIds)
			{
				if (!questId.IsValid)
				{
					throw new InvalidOperationException("任务集合包含无效内容 ID。");
				}
				if (!contentIndex.TryGet(questId, out QuestDefinition definition))
				{
					throw new InvalidOperationException($"任务集合引用的内容 {questId} 不存在或不是任务定义。");
				}
				QuestProgress quest = new QuestProgress(definition);
				if (!m_quests.TryAdd(questId, quest))
				{
					throw new InvalidOperationException($"任务集合重复包含任务 {questId}。");
				}
				m_questOrder.Add(quest);
			}

			foreach (QuestProgress quest in m_quests.Values)
			{
				RequirePrerequisitesPresent(quest);
			}
		}

		internal QuestLog(
			ContentId scenarioId,
			IEnumerable<ContentId> questIds,
			ContentIndex contentIndex,
			QuestLogSnapshot snapshot)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("任务日志必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			if (questIds == null)
			{
				throw new ArgumentNullException(nameof(questIds));
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}
			if (snapshot?.Quests == null)
			{
				throw new InvalidOperationException("任务日志快照缺少任务集合。");
			}

			m_scenarioId = scenarioId;
			m_readOnlyQuests = m_questOrder.AsReadOnly();
			Dictionary<ContentId, QuestProgressSnapshot> saved = new Dictionary<ContentId, QuestProgressSnapshot>();
			for (int i = 0; i < snapshot.Quests.Count; i++)
			{
				QuestProgressSnapshot questSnapshot = snapshot.Quests[i];
				if (questSnapshot == null || !questSnapshot.QuestId.IsValid || !saved.TryAdd(questSnapshot.QuestId, questSnapshot))
				{
					throw new InvalidOperationException($"任务日志快照的第 {i + 1} 项为空、ID 无效或重复。");
				}
			}

			foreach (ContentId questId in questIds)
			{
				if (!contentIndex.TryGet(questId, out QuestDefinition definition))
				{
					throw new InvalidOperationException($"任务集合引用的内容 {questId} 不存在或不是任务定义。");
				}
				if (!saved.Remove(questId, out QuestProgressSnapshot questSnapshot))
				{
					throw new InvalidOperationException($"任务日志快照缺少当前剧本任务 {questId}。");
				}
				QuestProgress quest = new QuestProgress(definition, questSnapshot);
				m_quests.Add(questId, quest);
				m_questOrder.Add(quest);
			}
			if (saved.Count > 0)
			{
				throw new InvalidOperationException("任务日志快照包含不属于当前剧本的任务。");
			}
			foreach (QuestProgress quest in m_quests.Values)
			{
				RequirePrerequisitesPresent(quest);
			}
		}

		internal QuestLogSnapshot CreateSnapshot()
		{
			List<ContentId> ids = new List<ContentId>(m_quests.Keys);
			ids.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
			QuestProgressSnapshot[] quests = new QuestProgressSnapshot[ids.Count];
			for (int i = 0; i < ids.Count; i++)
			{
				quests[i] = m_quests[ids[i]].CreateSnapshot();
			}
			return new QuestLogSnapshot(quests);
		}

		/// <summary>读取当前单局中的任务运行对象，不提供第二份状态副本。</summary>
		public QuestProgress GetQuest(ContentId questId)
		{
			if (!m_quests.TryGetValue(questId, out QuestProgress quest))
			{
				throw new InvalidOperationException($"任务 {questId} 不属于当前单局任务日志。");
			}
			return quest;
		}

		internal void ActivateInitialQuests()
		{
			List<QuestStatusChangedEvent> statusChanges = new List<QuestStatusChangedEvent>();
			foreach (QuestProgress quest in m_quests.Values)
			{
				if (quest.Status == QuestStatus.Locked && quest.Definition.PrerequisiteQuestIds.Count == 0)
				{
					ActivateQuest(quest, statusChanges);
				}
			}
			PublishStatusChanges(statusChanges);
		}

		/// <summary>
		/// 把一个已经提交的事实交给当时已激活的任务；同一次事实不会推进刚刚解锁的任务。
		/// </summary>
		internal bool RecordFact(QuestTaskFact fact)
		{
			if (fact == null)
			{
				throw new ArgumentNullException(nameof(fact));
			}

			List<QuestProgress> completedQuests = new List<QuestProgress>();
			bool anyProgressChanged = false;
			for (int i = 0; i < m_questOrder.Count; i++)
			{
				QuestProgress quest = m_questOrder[i];
				if (quest.Status != QuestStatus.Active ||
					!quest.RecordFact(fact, out bool completed))
				{
					continue;
				}

				anyProgressChanged = true;
				EventKit.Type.Send(new QuestProgressChangedEvent(
					m_scenarioId,
					quest.Definition.ContentId));
				if (completed)
				{
					completedQuests.Add(quest);
				}
			}

			if (completedQuests.Count == 0)
			{
				return anyProgressChanged;
			}

			List<QuestStatusChangedEvent> statusChanges = new List<QuestStatusChangedEvent>();
			for (int i = 0; i < completedQuests.Count; i++)
			{
				CompleteQuest(completedQuests[i], statusChanges);
			}
			ActivateEligibleQuests(statusChanges);
			PublishStatusChanges(statusChanges);
			return true;
		}

		private void CompleteQuest(
			QuestProgress quest,
			ICollection<QuestStatusChangedEvent> statusChanges)
		{
			quest.Complete();
			statusChanges.Add(new QuestStatusChangedEvent(
				m_scenarioId,
				quest.Definition.ContentId,
				QuestStatus.Active,
				QuestStatus.Completed));
		}

		private void ActivateEligibleQuests(ICollection<QuestStatusChangedEvent> statusChanges)
		{
			foreach (QuestProgress quest in m_quests.Values)
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
					ActivateQuest(quest, statusChanges);
				}
			}
		}

		private void ActivateQuest(
			QuestProgress quest,
			ICollection<QuestStatusChangedEvent> statusChanges)
		{
			quest.Activate();
			statusChanges.Add(new QuestStatusChangedEvent(
				m_scenarioId,
				quest.Definition.ContentId,
				QuestStatus.Locked,
				QuestStatus.Active));
		}

		private static void PublishStatusChanges(IReadOnlyList<QuestStatusChangedEvent> statusChanges)
		{
			for (int i = 0; i < statusChanges.Count; i++)
			{
				EventKit.Type.Send(statusChanges[i]);
			}
		}

		private void RequirePrerequisitesPresent(QuestProgress quest)
		{
			for (int i = 0; i < quest.Definition.PrerequisiteQuestIds.Count; i++)
			{
				ContentId prerequisiteId = quest.Definition.PrerequisiteQuestIds[i];
				if (!m_quests.ContainsKey(prerequisiteId))
				{
					throw new InvalidOperationException(
						$"任务集合包含 {quest.Definition.ContentId}，但缺少它的前置任务 {prerequisiteId}。");
				}
			}
		}
	}
}
