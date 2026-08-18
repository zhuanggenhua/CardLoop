using Gameplay.Content;

namespace Gameplay.Quests
{
	/// <summary>任务日志已提交某个任务的子项进度变化。</summary>
	public readonly struct QuestProgressChangedEvent
	{
		public ContentId ScenarioId { get; }

		public ContentId QuestId { get; }

		public QuestProgressChangedEvent(ContentId scenarioId, ContentId questId)
		{
			ScenarioId = scenarioId;
			QuestId = questId;
		}
	}
}
