using System;
using Gameplay.Content;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本日志条目的悬浮信息请求；只服务 UI 展示，不改变任务、配方或卡牌状态。
	/// </summary>
	public readonly struct ScenarioJournalEntryInfoEvent
	{
		public ContentId ScenarioId { get; }

		public ContentId EntryId { get; }

		public bool IsVisible { get; }

		public string Header { get; }

		public string Body { get; }

		public ScenarioJournalEntryInfoEvent(
			ContentId scenarioId,
			ContentId entryId,
			bool isVisible,
			string header,
			string body)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("剧本日志条目信息必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			if (!entryId.IsValid)
			{
				throw new ArgumentException("剧本日志条目信息必须引用有效的条目内容 ID。", nameof(entryId));
			}
			if (isVisible && string.IsNullOrWhiteSpace(header))
			{
				throw new ArgumentException("显示剧本日志条目信息时标题不能为空。", nameof(header));
			}

			ScenarioId = scenarioId;
			EntryId = entryId;
			IsVisible = isVisible;
			Header = header ?? string.Empty;
			Body = body ?? string.Empty;
		}

		public static ScenarioJournalEntryInfoEvent Show(
			ContentId scenarioId,
			ContentId entryId,
			string header,
			string body)
		{
			return new ScenarioJournalEntryInfoEvent(
				scenarioId,
				entryId,
				isVisible: true,
				header,
				body);
		}

		public static ScenarioJournalEntryInfoEvent Clear(ContentId scenarioId, ContentId entryId)
		{
			return new ScenarioJournalEntryInfoEvent(
				scenarioId,
				entryId,
				isVisible: false,
				string.Empty,
				string.Empty);
		}
	}
}
