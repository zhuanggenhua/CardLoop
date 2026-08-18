using System;
using Gameplay.Tabletop;

namespace Gameplay.Scenarios
{
	/// <summary>一次日终流程当前等待玩家处理的阶段。</summary>
	public enum ScenarioDayCyclePhase
	{
		Inactive = 0,
		AwaitingFeedingConfirmation = 10,
		AwaitingExcessCardResolution = 20,
		AwaitingNewDayConfirmation = 30,
		GameOver = 40
	}

	/// <summary>当前剧本单局正在执行的一次日终流程。</summary>
	internal sealed class ScenarioDayCycle
	{
		internal int EndingDay { get; }

		internal ScenarioDayCyclePhase Phase { get; private set; }

		internal ScenarioDayEncounterResult? EncounterResult { get; private set; }

		internal ScenarioDayCycle(int endingDay)
		{
			if (endingDay <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(endingDay));
			}
			EndingDay = endingDay;
			Phase = ScenarioDayCyclePhase.AwaitingFeedingConfirmation;
		}

		internal void FinishFeeding(bool hasSurvivingCharacters, int excessCardCount)
		{
			RequirePhase(ScenarioDayCyclePhase.AwaitingFeedingConfirmation);
			if (excessCardCount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(excessCardCount));
			}
			Phase = !hasSurvivingCharacters
				? ScenarioDayCyclePhase.GameOver
				: excessCardCount > 0
					? ScenarioDayCyclePhase.AwaitingExcessCardResolution
					: ScenarioDayCyclePhase.AwaitingNewDayConfirmation;
		}

		internal void FinishExcessCardResolution()
		{
			RequirePhase(ScenarioDayCyclePhase.AwaitingExcessCardResolution);
			Phase = ScenarioDayCyclePhase.AwaitingNewDayConfirmation;
		}

		internal void RecordEncounter(Content.ContentId cardId, int count, string notificationMessage)
		{
			RequirePhase(ScenarioDayCyclePhase.AwaitingNewDayConfirmation);
			if (!cardId.IsValid)
			{
				throw new ArgumentException("日终遭遇必须引用有效卡牌内容。", nameof(cardId));
			}
			if (count <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}
			if (EncounterResult.HasValue)
			{
				throw new InvalidOperationException($"第 {EndingDay} 天已经提交过日终遭遇。");
			}
			EncounterResult = new ScenarioDayEncounterResult(cardId, count, notificationMessage);
		}

		private void RequirePhase(ScenarioDayCyclePhase expected)
		{
			if (Phase != expected)
			{
				throw new InvalidOperationException(
					$"第 {EndingDay} 天的日终流程处于 {Phase}，不能执行要求 {expected} 的操作。");
			}
		}
	}

	/// <summary>当前日终已生成并需要反馈给玩家的遭遇摘要。</summary>
	public readonly struct ScenarioDayEncounterResult
	{
		public Content.ContentId CardId { get; }

		public int Count { get; }

		public string NotificationMessage { get; }

		public ScenarioDayEncounterResult(Content.ContentId cardId, int count, string notificationMessage)
		{
			CardId = cardId;
			Count = count;
			NotificationMessage = notificationMessage ?? string.Empty;
		}
	}

	/// <summary>日终阶段已经由所属单局提交后的领域事实。</summary>
	public readonly struct ScenarioDayCycleChangedEvent
	{
		public Content.ContentId ScenarioId { get; }

		public int EndingDay { get; }

		public ScenarioDayCyclePhase Phase { get; }

		public int ExcessCardCount { get; }

		public ScenarioDayCycleChangedEvent(
			Content.ContentId scenarioId,
			int endingDay,
			ScenarioDayCyclePhase phase,
			int excessCardCount)
		{
			ScenarioId = scenarioId;
			EndingDay = endingDay;
			Phase = phase;
			ExcessCardCount = excessCardCount;
		}
	}

	/// <summary>日终进食消耗食物卡时给当前牌桌表现层使用的只读反馈事实。</summary>
	public readonly struct ScenarioFeedingPresentationEvent
	{
		public Content.ContentId ScenarioId { get; }

		public global::Gameplay.Tabletop.Tabletop Tabletop { get; }

		public TabletopCardId FoodCardId { get; }

		public TabletopCardId CharacterCardId { get; }

		public UnityEngine.Vector2 FoodPosition { get; }

		public bool FoodWillBeConsumed { get; }

		public ScenarioFeedingPresentationEvent(
			Content.ContentId scenarioId,
			global::Gameplay.Tabletop.Tabletop tabletop,
			TabletopCardId foodCardId,
			TabletopCardId characterCardId,
			UnityEngine.Vector2 foodPosition,
			bool foodWillBeConsumed)
		{
			if (!scenarioId.IsValid)
			{
				throw new ArgumentException("进食表现事实必须引用有效剧本内容 ID。", nameof(scenarioId));
			}
			ScenarioId = scenarioId;
			Tabletop = tabletop ?? throw new ArgumentNullException(nameof(tabletop));
			FoodCardId = foodCardId.IsValid
				? foodCardId
				: throw new ArgumentException("进食表现事实必须引用有效食物卡。", nameof(foodCardId));
			CharacterCardId = characterCardId.IsValid
				? characterCardId
				: throw new ArgumentException("进食表现事实必须引用有效角色卡。", nameof(characterCardId));
			if (!float.IsFinite(foodPosition.x) || !float.IsFinite(foodPosition.y))
			{
				throw new ArgumentException("进食表现事实的食物位置必须是有限值。", nameof(foodPosition));
			}
			FoodPosition = foodPosition;
			FoodWillBeConsumed = foodWillBeConsumed;
		}
	}
}
