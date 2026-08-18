using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Scenarios
{
	/// <summary>剧本作者声明的日终规则；运行时由单局冻结，不保存日程状态。</summary>
	[Serializable]
	public sealed class ScenarioDayCycleRules
	{
		[SerializeField]
		[LabelText("启用完整日终")]
		[Tooltip("启用后，每日最后一个行动回合结束时进入进食、超限处理、遭遇和新日确认流程。")]
		private bool m_enabled;

		[SerializeField]
		[Min(0f)]
		[LabelText("每名角色所需食物次数")]
		[Tooltip("日终时每名角色需要消耗的食物使用次数；0 表示本剧本不执行饥饿消耗。")]
		private int m_hungerPerCharacter;

		[SerializeField]
		[Min(0f)]
		[LabelText("基础卡牌上限")]
		[Tooltip("进食后用于判断是否必须处理超限卡牌的基础数量。")]
		private int m_baseCardLimit;

		[SerializeField]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.GameplayEffects()")]
		[LabelText("进食恢复效果")]
		[Tooltip("日终进食恢复生命时应用的 EX-GAS GameplayEffect；运行时会把第一个生命加法 Modifier 设置为本次恢复量。")]
		private int m_feedingHealingEffectId = 2005;

		[SerializeField]
		[LabelText("日终遭遇")]
		[Tooltip("进食和超限处理完成后，从当天符合条件的候选中最多执行一个。")]
		private ScenarioDayEncounterRule[] m_encounters = Array.Empty<ScenarioDayEncounterRule>();

		public bool Enabled => m_enabled;

		public int HungerPerCharacter => m_hungerPerCharacter;

		public int BaseCardLimit => m_baseCardLimit;

		public int FeedingHealingEffectId => m_feedingHealingEffectId;

		public IReadOnlyList<ScenarioDayEncounterRule> Encounters =>
			m_encounters ?? Array.Empty<ScenarioDayEncounterRule>();

		internal ScenarioDayCycleRulesRuntime CreateRuntime()
		{
			ScenarioDayEncounterRuleRuntime[] encounters = new ScenarioDayEncounterRuleRuntime[Encounters.Count];
			for (int i = 0; i < Encounters.Count; i++)
			{
				encounters[i] = Encounters[i].CreateRuntime();
			}
			return new ScenarioDayCycleRulesRuntime(
				Enabled,
				HungerPerCharacter,
				BaseCardLimit,
				FeedingHealingEffectId,
				encounters);
		}

		internal void Validate(ContentValidationContext context, ScenarioDefinition scenario)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}
			if (scenario == null)
			{
				throw new ArgumentNullException(nameof(scenario));
			}
			if (HungerPerCharacter < 0)
			{
				context.AddError(
					"SCENARIO_DAY_CYCLE_HUNGER_INVALID",
					$"剧本 {scenario.ContentId} 的每名角色食物次数不能为负数。",
					scenario);
			}
			if (Enabled && BaseCardLimit < 1)
			{
				context.AddError(
					"SCENARIO_DAY_CYCLE_CARD_LIMIT_INVALID",
					$"剧本 {scenario.ContentId} 启用完整日终时，基础卡牌上限必须大于 0。",
					scenario);
			}
			if (Enabled && HungerPerCharacter > 0 && FeedingHealingEffectId <= 0)
			{
				context.AddError(
					"SCENARIO_DAY_CYCLE_HEALING_EFFECT_INVALID",
					$"剧本 {scenario.ContentId} 启用进食时必须配置有效的 EX-GAS 生命恢复效果。",
					scenario);
			}
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Encounters.Count; i++)
			{
				ScenarioDayEncounterRule encounter = Encounters[i];
				if (encounter == null)
				{
					context.AddError(
						"SCENARIO_DAY_ENCOUNTER_NULL",
						$"剧本 {scenario.ContentId} 的第 {i + 1} 个日终遭遇为空。",
						scenario);
					continue;
				}
				encounter.Validate(context, scenario, keys);
			}
		}

		internal void EnsureLocalKeys()
		{
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Encounters.Count; i++)
			{
				Encounters[i]?.EnsureLocalKey(i, keys);
			}
		}
	}

	/// <summary>剧本日终候选中的一项卡牌遭遇。</summary>
	[Serializable]
	public sealed class ScenarioDayEncounterRule
	{
		[SerializeField]
		[HideInInspector]
		private string m_key;

		[SerializeField]
		[TextArea]
		[LabelText("提示文本")]
		[Tooltip("日终遭遇触发后展示给玩家的事件说明；为空时只显示生成卡牌摘要。")]
		private string m_notificationMessage;

		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("生成卡牌")]
		private ContentId m_cardId;

		[SerializeField]
		[Min(1f)]
		[LabelText("生成数量")]
		private int m_count = 1;

		[SerializeField]
		[LabelText("仅触发一次")]
		private bool m_oneTimeOnly;

		[SerializeField]
		[Min(1f)]
		[LabelText("最早天数")]
		private int m_minimumDay = 1;

		[SerializeField]
		[Min(1f)]
		[LabelText("最晚天数")]
		private int m_maximumDay = int.MaxValue;

		[SerializeField]
		[Min(0f)]
		[LabelText("重复间隔")]
		[Tooltip("0 表示不限制间隔；大于 0 时，只在天数能被该间隔整除时生效。")]
		private int m_interval;

		[SerializeField]
		[LabelText("优先级")]
		private int m_priority;

		[SerializeField]
		[Range(0f, 1f)]
		[LabelText("触发概率")]
		private float m_chance = 1f;

		[SerializeField]
		[Min(1f)]
		[LabelText("牌桌卡牌上限")]
		[Tooltip("当前活动牌桌达到该数量时，本遭遇不进入候选。")]
		private int m_maxCardsOnTabletop = 100;

		internal ScenarioDayEncounterRuleRuntime CreateRuntime()
		{
			return new ScenarioDayEncounterRuleRuntime(
				m_key,
				m_notificationMessage,
				m_cardId,
				m_count,
				m_oneTimeOnly,
				m_minimumDay,
				m_maximumDay,
				m_interval,
				m_priority,
				m_chance,
				m_maxCardsOnTabletop);
		}

		internal void EnsureLocalKey(int index, ISet<string> usedKeys)
		{
			m_key = ActionLocalKeyUtility.EnsureUniqueKey(m_key, "day-encounter", index, usedKeys);
		}

		internal void Validate(
			ContentValidationContext context,
			ScenarioDefinition scenario,
			ISet<string> keys)
		{
			if (!ActionLocalKeyUtility.IsValidKey(m_key) || !keys.Add(m_key))
			{
				context.AddError(
					"SCENARIO_DAY_ENCOUNTER_KEY_INVALID",
					$"剧本 {scenario.ContentId} 的日终遭遇 key 无效或重复：{m_key}。",
					scenario);
			}
			if (!m_cardId.IsValid || !context.TryGet(m_cardId, out CardDefinition _))
			{
				context.AddError(
					"SCENARIO_DAY_ENCOUNTER_CARD_INVALID",
					$"剧本 {scenario.ContentId} 的日终遭遇 {m_key} 引用了不存在或类型错误的卡牌 {m_cardId}。",
					scenario);
			}
			if (m_count <= 0 || m_minimumDay <= 0 || m_maximumDay < m_minimumDay ||
				m_interval < 0 || !float.IsFinite(m_chance) || m_chance < 0f || m_chance > 1f ||
				m_maxCardsOnTabletop <= 0)
			{
				context.AddError(
					"SCENARIO_DAY_ENCOUNTER_RULE_INVALID",
					$"剧本 {scenario.ContentId} 的日终遭遇 {m_key} 日期、概率、数量或牌桌限制无效。",
					scenario);
			}
		}
	}

	internal readonly struct ScenarioDayCycleRulesRuntime
	{
		internal bool Enabled { get; }

		internal int HungerPerCharacter { get; }

		internal int BaseCardLimit { get; }

		internal int FeedingHealingEffectId { get; }

		internal IReadOnlyList<ScenarioDayEncounterRuleRuntime> Encounters { get; }

		internal ScenarioDayCycleRulesRuntime(
			bool enabled,
			int hungerPerCharacter,
			int baseCardLimit,
			int feedingHealingEffectId,
			IReadOnlyList<ScenarioDayEncounterRuleRuntime> encounters)
		{
			Enabled = enabled;
			HungerPerCharacter = hungerPerCharacter;
			BaseCardLimit = baseCardLimit;
			FeedingHealingEffectId = feedingHealingEffectId;
			Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
		}
	}

	internal readonly struct ScenarioDayEncounterRuleRuntime
	{
		internal string Key { get; }
		internal string NotificationMessage { get; }
		internal ContentId CardId { get; }
		internal int Count { get; }
		internal bool OneTimeOnly { get; }
		internal int MinimumDay { get; }
		internal int MaximumDay { get; }
		internal int Interval { get; }
		internal int Priority { get; }
		internal float Chance { get; }
		internal int MaxCardsOnTabletop { get; }
		internal int Specificity => MinimumDay == MaximumDay
			? 0
			: Interval > 0
				? 1
				: MaximumDay < int.MaxValue
					? 2
					: 3;

		internal ScenarioDayEncounterRuleRuntime(
			string key,
			string notificationMessage,
			ContentId cardId,
			int count,
			bool oneTimeOnly,
			int minimumDay,
			int maximumDay,
			int interval,
			int priority,
			float chance,
			int maxCardsOnTabletop)
		{
			Key = key;
			NotificationMessage = notificationMessage ?? string.Empty;
			CardId = cardId;
			Count = count;
			OneTimeOnly = oneTimeOnly;
			MinimumDay = minimumDay;
			MaximumDay = maximumDay;
			Interval = interval;
			Priority = priority;
			Chance = chance;
			MaxCardsOnTabletop = maxCardsOnTabletop;
		}

		internal bool MatchesDay(int day)
		{
			return day >= MinimumDay && day <= MaximumDay &&
				(Interval == 0 || day % Interval == 0);
		}
	}
}
