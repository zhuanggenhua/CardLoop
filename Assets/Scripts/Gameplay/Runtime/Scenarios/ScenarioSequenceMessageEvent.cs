using System;
using Gameplay.Content;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本单局已经提交一段短时流程提示；UI 只负责显示，不保存新的玩法状态。
	/// </summary>
	public readonly struct ScenarioSequenceMessageEvent
	{
		public ContentId ScenarioId { get; }

		public string Header { get; }

		public string Body { get; }

		public float DurationSeconds { get; }

		public ScenarioSequenceMessageEvent(
			ContentId scenarioId,
			string header,
			string body,
			float durationSeconds)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("剧本流程提示必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			if (string.IsNullOrWhiteSpace(header))
			{
				throw new ArgumentException("剧本流程提示标题不能为空。", nameof(header));
			}
			if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(durationSeconds),
					durationSeconds,
					"剧本流程提示持续时间必须是大于 0 的有限秒数。");
			}

			ScenarioId = scenarioId;
			Header = header;
			Body = body ?? string.Empty;
			DurationSeconds = durationSeconds;
		}
	}
}
