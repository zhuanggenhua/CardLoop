using System;
using Gameplay.Content;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本确认推进一个行动回合后发布的领域事实。
	/// </summary>
	public readonly struct ScenarioTurnConfirmedEvent
	{
		public ContentId ScenarioId { get; }

		public int ConfirmedTurnIndex { get; }

		public int CurrentDay { get; }

		public int ConfirmedTurnsInCurrentDay { get; }

		public ScenarioTurnConfirmedEvent(
			ContentId scenarioId,
			int confirmedTurnIndex,
			int currentDay,
			int confirmedTurnsInCurrentDay)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("回合确认事实必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			ScenarioId = scenarioId;
			ConfirmedTurnIndex = confirmedTurnIndex;
			CurrentDay = currentDay;
			ConfirmedTurnsInCurrentDay = confirmedTurnsInCurrentDay;
		}
	}
}
