using System;
using System.Collections.Generic;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Quests
{
	/// <summary>单局任务日志的可序列化事实。</summary>
	[Serializable]
	public sealed class QuestLogSnapshot
	{
		[SerializeField]
		private QuestProgressSnapshot[] m_quests;

		public IReadOnlyList<QuestProgressSnapshot> Quests => m_quests;

		internal QuestLogSnapshot(QuestProgressSnapshot[] quests)
		{
			m_quests = quests ?? throw new ArgumentNullException(nameof(quests));
		}
	}

	/// <summary>单个任务及其子项的运行事实。</summary>
	[Serializable]
	public sealed class QuestProgressSnapshot
	{
		[SerializeField]
		private ContentId m_questId;

		[SerializeField]
		private QuestStatus m_status;

		[SerializeReference]
		private QuestTaskStateSnapshot[] m_tasks;

		public ContentId QuestId => m_questId;

		public QuestStatus Status => m_status;

		public IReadOnlyList<QuestTaskStateSnapshot> Tasks => m_tasks;

		internal QuestProgressSnapshot(
			ContentId questId,
			QuestStatus status,
			QuestTaskStateSnapshot[] tasks)
		{
			m_questId = questId;
			m_status = status;
			m_tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
		}
	}

	/// <summary>
	/// 一个任务子项自己的可序列化状态。Mod 子项通过派生类型扩展，不需要任务日志识别类型。
	/// </summary>
	[Serializable]
	public abstract class QuestTaskStateSnapshot
	{
	}

	[Serializable]
	internal sealed class QuestTaskAmountStateSnapshot : QuestTaskStateSnapshot
	{
		[SerializeField]
		private int m_currentAmount;

		internal int CurrentAmount => m_currentAmount;

		internal QuestTaskAmountStateSnapshot(int currentAmount)
		{
			m_currentAmount = currentAmount;
		}
	}
}
