using System;
using Gameplay.Content;
using Gameplay.Tabletop;
using UnityEngine;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本单局请求导演播放一段短时表现序列；真正的输入锁、暂停和 UI 消息由导演串行提交。
	/// </summary>
	public readonly struct ScenarioSequencePresentationRequestEvent
	{
		private readonly Vector2 m_tablePosition;
		private readonly TabletopCardId m_cardId;

		public ContentId ScenarioId { get; }

		public string Header { get; }

		public string Body { get; }

		public float DurationSeconds { get; }

		public bool HasTablePosition { get; }

		public bool HasCardId { get; }

		public Vector2 TablePosition
		{
			get
			{
				if (!HasTablePosition)
				{
					throw new InvalidOperationException("剧本表现序列请求没有关联牌桌坐标。");
				}
				return m_tablePosition;
			}
		}

		public TabletopCardId CardId
		{
			get
			{
				if (!HasCardId)
				{
					throw new InvalidOperationException("剧本表现序列请求没有关联局内卡牌。");
				}
				return m_cardId;
			}
		}

		public ScenarioSequencePresentationRequestEvent(
			ContentId scenarioId,
			string header,
			string body,
			float durationSeconds,
			Vector2 tablePosition,
			TabletopCardId cardId)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("剧本表现序列请求必须引用有效的所属剧本内容 ID。", nameof(scenarioId));
			}
			if (string.IsNullOrWhiteSpace(header))
			{
				throw new ArgumentException("剧本表现序列请求标题不能为空。", nameof(header));
			}
			if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(durationSeconds),
					durationSeconds,
					"剧本表现序列持续时间必须是大于 0 的有限秒数。");
			}
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new ArgumentException("剧本表现序列牌桌坐标必须是有限值。", nameof(tablePosition));
			}
			if (!cardId.IsValid)
			{
				throw new ArgumentException("剧本表现序列卡牌高亮必须引用有效的局内卡牌。", nameof(cardId));
			}

			ScenarioId = scenarioId;
			Header = header;
			Body = body ?? string.Empty;
			DurationSeconds = durationSeconds;
			HasTablePosition = true;
			m_tablePosition = tablePosition;
			HasCardId = true;
			m_cardId = cardId;
		}
	}
}
