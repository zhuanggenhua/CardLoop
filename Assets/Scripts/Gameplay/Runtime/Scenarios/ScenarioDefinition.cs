using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本的 ScriptableObject 作者源，组合地区、时间规则、任务集合和当前已接入的剧本级规则。
	/// </summary>
	[CreateAssetMenu(fileName = "剧本_", menuName = "Gameplay/内容/剧本")]
	public class ScenarioDefinition : DisplayableContentAsset
	{
		[Header("地区")]
		[SerializeField]
		[ContentIdReference(typeof(ScenarioRegionDefinition))]
		[LabelText("初始地区")]
		[Tooltip("开始剧本时进入的地区。地区负责场景载体与牌桌规则，剧本不重复保存场景地址。")]
		private ContentId m_initialRegionId;

		[SerializeField]
		[ContentIdReference(typeof(ScenarioRegionDefinition))]
		[LabelText("剧本地区")]
		[Tooltip("本剧本允许进入的全部地区。初始地区必须包含在该列表中。")]
		private ContentId[] m_regionIds = Array.Empty<ContentId>();

		[SerializeField]
		[ContentIdReference(typeof(QuestDefinition))]
		[LabelText("剧本任务")]
		[Tooltip("进入本剧本时启用的任务定义。编辑器自动保存所选任务的唯一内容 ID；前置任务也必须包含在同一列表中。")]
		private ContentId[] m_questIds = Array.Empty<ContentId>();

		[Header("时间")]
		[SerializeField]
		[Min(1f)]
		[LabelText("每日确认回合数")]
		[Tooltip("本剧本一个游戏日包含的确认回合数。默认值为 2；单局根据这个配置从总确认回合推导日期，不额外保存第二份日历状态。")]
		private int m_turnsPerDay = 2;

		[SerializeField]
		[Min(0.001f)]
		[LabelText("每回合秒数")]
		[Tooltip("本剧本切换为即时制时，一个普通行动回合单位对应的游戏秒数。战斗、技能时间轴和冷却不使用这个值。")]
		private float m_secondsPerTurn = 1f;

		[Header("战斗")]
		[SerializeField]
		[InlineProperty]
		[LabelText("战斗阵型规则")]
		[Tooltip("本剧本战斗参与者的队列与前后排空间规则。它只定义表现位置，不保存卡牌位置、战斗进度或独立内容 ID。未配置时本剧本不能创建战斗。")]
		private BattleFormationRules m_battleFormationRules = new BattleFormationRules();

		public IReadOnlyList<ContentId> QuestIds => m_questIds ?? Array.Empty<ContentId>();

		public ContentId InitialRegionId => m_initialRegionId;

		public IReadOnlyList<ContentId> RegionIds => m_regionIds ?? Array.Empty<ContentId>();

		public int TurnsPerDay => m_turnsPerDay;

		public float SecondsPerTurn => m_secondsPerTurn;

		public BattleFormationRules BattleFormationRules => m_battleFormationRules;

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (TurnsPerDay <= 0)
			{
				context.AddError(
					"SCENARIO_TURNS_PER_DAY_INVALID",
					$"剧本 {ContentId} 的每日确认回合数必须大于 0，当前值为 {TurnsPerDay}。",
					this);
			}
			if (!float.IsFinite(SecondsPerTurn) || SecondsPerTurn <= 0f)
			{
				context.AddError(
					"SCENARIO_SECONDS_PER_TURN_INVALID",
					$"剧本 {ContentId} 的每回合秒数必须是大于 0 的有限值，当前值为 {SecondsPerTurn}。",
					this);
			}
			ValidateRegions(context);
			m_battleFormationRules?.ValidateContent(context, this);
			HashSet<ContentId> questIds = new();
			for (int i = 0; i < QuestIds.Count; i++)
			{
				ContentId questId = QuestIds[i];
				if (!questId.IsValid)
				{
					context.AddError(
						"SCENARIO_QUEST_INVALID",
						$"剧本 {ContentId} 引用了无效任务 ID：{questId}。",
						this);
				}
				else if (!questIds.Add(questId))
				{
					context.AddError(
						"SCENARIO_QUEST_DUPLICATE",
						$"剧本 {ContentId} 重复引用任务 {questId}。",
						this);
				}
				else if (!context.TryGet(questId, out ContentAsset questAsset))
				{
					context.AddError(
						"SCENARIO_QUEST_UNKNOWN",
						$"剧本 {ContentId} 引用了当前内容作者源中不存在的任务 {questId}。",
						this);
				}
				else if (questAsset is not QuestDefinition)
				{
					context.AddError(
						"SCENARIO_QUEST_TYPE_INVALID",
						$"剧本 {ContentId} 引用的内容 {questId} 不是任务定义。",
						this);
				}
			}

			for (int i = 0; i < QuestIds.Count; i++)
			{
				ContentId questId = QuestIds[i];
				if (!context.TryGet(questId, out QuestDefinition quest))
				{
					continue;
				}

				for (int prerequisiteIndex = 0;
					 prerequisiteIndex < quest.PrerequisiteQuestIds.Count;
					 prerequisiteIndex++)
				{
					ContentId prerequisiteId = quest.PrerequisiteQuestIds[prerequisiteIndex];
					if (prerequisiteId.IsValid && !questIds.Contains(prerequisiteId))
					{
						context.AddError(
							"SCENARIO_QUEST_PREREQUISITE_MISSING",
							$"剧本 {ContentId} 包含任务 {questId}，但没有同时包含它的前置任务 {prerequisiteId}。",
							this);
					}
				}
			}
		}

		private void ValidateRegions(ContentValidationContext context)
		{
			HashSet<ContentId> regionIds = new HashSet<ContentId>();
			for (int i = 0; i < RegionIds.Count; i++)
			{
				ContentId regionId = RegionIds[i];
				if (!regionId.IsValid)
				{
					context.AddError(
						"SCENARIO_REGION_INVALID",
						$"剧本 {ContentId} 的第 {i + 1} 个地区引用无效。",
						this);
				}
				else if (!regionIds.Add(regionId))
				{
					context.AddError(
						"SCENARIO_REGION_DUPLICATE",
						$"剧本 {ContentId} 重复引用地区 {regionId}。",
						this);
				}
				else if (!context.TryGet(regionId, out ContentAsset regionAsset))
				{
					context.AddError(
						"SCENARIO_REGION_UNKNOWN",
						$"剧本 {ContentId} 引用了当前内容作者源中不存在的地区 {regionId}。",
						this);
				}
				else if (regionAsset is not ScenarioRegionDefinition)
				{
					context.AddError(
						"SCENARIO_REGION_TYPE_INVALID",
						$"剧本 {ContentId} 的地区引用 {regionId} 不是剧本地区定义。",
						this);
				}
			}

			if (!m_initialRegionId.IsValid)
			{
				context.AddError(
					"SCENARIO_INITIAL_REGION_INVALID",
					$"剧本 {ContentId} 缺少有效的初始地区。",
					this);
			}
			else if (!regionIds.Contains(m_initialRegionId))
			{
				context.AddError(
					"SCENARIO_INITIAL_REGION_NOT_INCLUDED",
					$"剧本 {ContentId} 的初始地区 {m_initialRegionId} 未包含在剧本地区列表中。",
					this);
			}
		}
	}
}
