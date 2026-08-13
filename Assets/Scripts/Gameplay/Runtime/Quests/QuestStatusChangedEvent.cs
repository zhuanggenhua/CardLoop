using System;
using Gameplay.Content;

namespace Gameplay.Quests
{
	/// <summary>
	/// 任务日志提交状态变化后发布的领域事实。
	/// </summary>
	public readonly struct QuestStatusChangedEvent
	{
		public ContentId ScenarioId { get; }

		public ContentId QuestId { get; }

		public QuestStatus PreviousStatus { get; }

		public QuestStatus CurrentStatus { get; }

		public QuestStatusChangedEvent(
			ContentId scenarioId,
			ContentId questId,
			QuestStatus previousStatus,
			QuestStatus currentStatus)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("任务状态事实必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			ScenarioId = scenarioId;
			QuestId = questId;
			PreviousStatus = previousStatus;
			CurrentStatus = currentStatus;
		}
	}
}
