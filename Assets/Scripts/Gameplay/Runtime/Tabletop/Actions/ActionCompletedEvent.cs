using System;
using Gameplay.Content;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 行动结果和所属单局任务日志成功提交后发布的领域事实。
	/// </summary>
	public readonly struct ActionCompletedEvent
	{
		public ContentId ScenarioId { get; }

		public ContentId ActionId { get; }

		public ActionCompletedEvent(ContentId scenarioId, ContentId actionId)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("行动完成事实必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			ScenarioId = scenarioId;
			ActionId = actionId;
		}
	}
}
