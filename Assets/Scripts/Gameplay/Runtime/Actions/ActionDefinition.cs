using System;
using System.Collections.Generic;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Actions
{
	/// <summary>
	/// 行动的 ScriptableObject 作者源，统一声明参与条件、回合消耗和结果意图；运行时副作用由当前牌桌提交。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/行动", fileName = "行动_")]
	public class ActionDefinition : DisplayableContentAsset
	{
		[Header("回合消耗")]
		[SerializeField]
		[Min(0f)]
		[LabelText("消耗回合数")]
		[Tooltip("普通行动完成所需的回合数；0 表示选择后立即完成。切换即时制时由当前回合规则统一换算秒数，不能在行动上另配持续秒数。战斗技能不使用本字段。")]
		private int m_turnCost = 1;

		[SerializeField]
		[LabelText("允许点击启动")]
		[Tooltip("开启后，玩家单击参与卡牌也会查询这项行动；关闭时只通过拖拽、填槽或其它正式入口启动。")]
		private bool m_canStartFromClick;

		[Header("参与槽位")]
		[SerializeField]
		[LabelText("参与槽位")]
		[Tooltip("声明行动需要哪些参与对象以及各自的匹配条件。槽位只负责判断能否参与，不扣除材料、不启动计时，也不执行结果。")]
		private ActionSlotDefinition[] m_participationSlots = Array.Empty<ActionSlotDefinition>();

		[SerializeReference]
		[LabelText("可用条件")]
		[Tooltip("候选显示和正式提交前都必须满足的单局条件；条件只读取事实，不保存第二份运行状态。")]
		private ActionCondition[] m_conditions = Array.Empty<ActionCondition>();

		[Header("结果意图")]
		[SerializeReference]
		[LabelText("结果意图")]
		[Tooltip("只声明行动完成后的结果意图；真正提交由对应状态 owner 在行动完成时统一校验和执行。")]
		private ActionResultIntent[] m_resultIntents = Array.Empty<ActionResultIntent>();

		[SerializeField]
		[LabelText("随机结果分支")]
		[Tooltip("行动开始时由权威随机流选择一个分支；分支只保存相对权重和结果意图，不直接执行副作用。")]
		private ActionResultBranchDefinition[] m_resultBranches = Array.Empty<ActionResultBranchDefinition>();

		public IReadOnlyList<ActionSlotDefinition> ParticipationSlots => m_participationSlots ?? Array.Empty<ActionSlotDefinition>();

		public IReadOnlyList<ActionResultIntent> ResultIntents => m_resultIntents ?? Array.Empty<ActionResultIntent>();

		public IReadOnlyList<ActionCondition> Conditions => m_conditions ?? Array.Empty<ActionCondition>();

		public IReadOnlyList<ActionResultBranchDefinition> ResultBranches => m_resultBranches ?? Array.Empty<ActionResultBranchDefinition>();

		public bool HasResultIntents
		{
			get
			{
				if (ResultIntents.Count > 0)
				{
					return true;
				}
				for (int i = 0; i < ResultBranches.Count; i++)
				{
					ActionResultBranchDefinition branch = ResultBranches[i];
					if (branch != null && branch.ResultIntents.Count > 0)
					{
						return true;
					}
				}
				return false;
			}
		}

		public int TurnCost => m_turnCost;

		public bool CanStartFromClick => m_canStartFromClick;

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (TurnCost < 0)
			{
				context.AddError(
					"ACTION_TURN_COST_INVALID",
					$"行动 {ContentId} 的消耗回合数不能为负数：{TurnCost}。",
					this);
			}
			HashSet<string> slotKeys = new(StringComparer.Ordinal);
			ValidateSlots(context, slotKeys);
			ValidateConditions(context, slotKeys);
			ValidateResultIntents(context, ResultIntents, slotKeys);
			ValidateResultBranches(context, slotKeys);
		}

		private void ValidateConditions(ContentValidationContext context, HashSet<string> slotKeys)
		{
			ActionResultValidationContext conditionContext = new(this, slotKeys, context);
			for (int i = 0; i < Conditions.Count; i++)
			{
				ActionCondition condition = Conditions[i];
				if (condition == null)
				{
					context.AddError("ACTION_CONDITION_NULL", $"行动 {ContentId} 的第 {i + 1} 个可用条件为空。", this);
					continue;
				}
				condition.Validate(conditionContext);
			}
		}

		private void ValidateSlots(
			ContentValidationContext context,
			HashSet<string> slotKeys)
		{
			for (int slotIndex = 0; slotIndex < ParticipationSlots.Count; slotIndex++)
			{
				ActionSlotDefinition slot = ParticipationSlots[slotIndex];
				if (slot == null)
				{
					context.AddError(
						"ACTION_SLOT_NULL",
						$"行动 {ContentId} 的第 {slotIndex + 1} 个参与槽位为空。",
						this);
					continue;
				}

				if (!ActionLocalKeyUtility.IsValidKey(slot.Key))
				{
					context.AddError(
						"ACTION_SLOT_KEY_INVALID",
						$"行动 {ContentId} 的参与槽位键无效：{slot.Key}。",
						this);
				}
				else if (!slotKeys.Add(slot.Key))
				{
					context.AddError(
						"ACTION_SLOT_KEY_DUPLICATE",
						$"行动 {ContentId} 重复声明参与槽位键：{slot.Key}。",
						this);
				}

				if (slot.MinimumParticipants < 0 || slot.MaximumParticipants < 0 ||
					(slot.MaximumParticipants > 0 && slot.MaximumParticipants < slot.MinimumParticipants))
				{
					context.AddError(
						"ACTION_SLOT_COUNT_INVALID",
						$"行动 {ContentId} 的槽位 {slot.Key} 参与数量范围无效：最少 {slot.MinimumParticipants}，最多 {slot.MaximumParticipants}。",
						this);
				}

				ValidateContentReferences(
					context,
					slot.AllowedContentIds,
					"ACTION_SLOT_ALLOWED_CONTENT_INVALID",
					"ACTION_SLOT_ALLOWED_CONTENT_UNKNOWN",
					$"行动 {ContentId} 的槽位 {slot.Key}");
				ValidateTagCodes(context, slot.RequiredAllContentTagCodes, "ACTION_SLOT_CONTENT_TAG_INVALID", $"行动 {ContentId} 的槽位 {slot.Key} 内容必须全部具有标签");
				ValidateTagCodes(context, slot.RequiredAnyContentTagCodes, "ACTION_SLOT_CONTENT_TAG_INVALID", $"行动 {ContentId} 的槽位 {slot.Key} 内容至少具有一个标签");
				ValidateTagCodes(context, slot.RequiredNoneContentTagCodes, "ACTION_SLOT_CONTENT_TAG_INVALID", $"行动 {ContentId} 的槽位 {slot.Key} 内容不能具有标签");
				ValidateTagCodes(context, slot.RequiredAllAbilitySystemTagCodes, "ACTION_SLOT_ABILITY_TAG_INVALID", $"行动 {ContentId} 的槽位 {slot.Key} 角色必须全部具有标签");
				ValidateTagCodes(context, slot.RequiredAnyAbilitySystemTagCodes, "ACTION_SLOT_ABILITY_TAG_INVALID", $"行动 {ContentId} 的槽位 {slot.Key} 角色至少具有一个标签");
				ValidateTagCodes(context, slot.RequiredNoneAbilitySystemTagCodes, "ACTION_SLOT_ABILITY_TAG_INVALID", $"行动 {ContentId} 的槽位 {slot.Key} 角色不能具有标签");
			}
		}

		private void ValidateResultBranches(
			ContentValidationContext context,
			HashSet<string> slotKeys)
		{
			HashSet<string> branchKeys = new(StringComparer.Ordinal);
			for (int branchIndex = 0; branchIndex < ResultBranches.Count; branchIndex++)
			{
				ActionResultBranchDefinition branch = ResultBranches[branchIndex];
				if (branch == null)
				{
					context.AddError(
						"ACTION_RESULT_BRANCH_NULL",
						$"行动 {ContentId} 的第 {branchIndex + 1} 个随机结果分支为空。",
						this);
					continue;
				}

				if (!ActionLocalKeyUtility.IsValidKey(branch.Key))
				{
					context.AddError(
						"ACTION_RESULT_BRANCH_KEY_INVALID",
						$"行动 {ContentId} 的随机结果分支键无效：{branch.Key}。",
						this);
				}
				else if (!branchKeys.Add(branch.Key))
				{
					context.AddError(
						"ACTION_RESULT_BRANCH_KEY_DUPLICATE",
						$"行动 {ContentId} 重复声明随机结果分支键：{branch.Key}。",
						this);
				}

				if (branch.Weight <= 0)
				{
					context.AddError(
						"ACTION_RESULT_BRANCH_WEIGHT_INVALID",
						$"行动 {ContentId} 的随机结果分支 {branch.Key} 权重必须大于 0，当前值为 {branch.Weight}。",
						this);
				}
				ValidateResultIntents(context, branch.ResultIntents, slotKeys);
			}
		}

		private void ValidateResultIntents(
			ContentValidationContext context,
			IReadOnlyList<ActionResultIntent> resultIntents,
			HashSet<string> slotKeys)
		{
			ActionResultValidationContext resultContext = new(this, slotKeys, context);
			for (int intentIndex = 0; intentIndex < resultIntents.Count; intentIndex++)
			{
				ActionResultIntent intent = resultIntents[intentIndex];
				if (intent == null)
				{
					context.AddError(
						"ACTION_RESULT_INTENT_NULL",
						$"行动 {ContentId} 的第 {intentIndex + 1} 个结果意图为空。",
						this);
					continue;
				}
				intent.ValidateIntent(resultContext);
			}
		}

		private void ValidateContentReferences(
			ContentValidationContext context,
			IReadOnlyList<ContentId> references,
			string invalidCode,
			string unknownCode,
			string messagePrefix)
		{
			for (int i = 0; i < references.Count; i++)
			{
				ContentId contentId = references[i];
				if (!contentId.IsValid)
				{
					context.AddError(invalidCode, $"{messagePrefix} 引用了无效内容 ID：{contentId}。", this);
				}
				else if (!context.TryGet(contentId, out ContentAsset _))
				{
					context.AddError(unknownCode, $"{messagePrefix} 引用了当前内容作者源集合中不存在的内容：{contentId}。", this);
				}
			}
		}

		private void ValidateTagCodes(
			ContentValidationContext context,
			IReadOnlyList<int> tagCodes,
			string code,
			string messagePrefix)
		{
			for (int i = 0; i < tagCodes.Count; i++)
			{
				if (tagCodes[i] <= 0)
				{
					context.AddError(code, $"{messagePrefix} 包含无效 EX-GAS 标签码：{tagCodes[i]}。", this);
				}
			}
		}

		public void EnsureLocalAuthoringKeys()
		{
			HashSet<string> usedSlotKeys = new HashSet<string>(StringComparer.Ordinal);
			IReadOnlyList<ActionSlotDefinition> slots = ParticipationSlots;
			for (int i = 0; i < slots.Count; i++)
			{
				slots[i]?.EnsureLocalKey("slot", i, usedSlotKeys);
			}
			HashSet<string> usedBranchKeys = new HashSet<string>(StringComparer.Ordinal);
			IReadOnlyList<ActionResultBranchDefinition> branches = ResultBranches;
			for (int j = 0; j < branches.Count; j++)
			{
				branches[j]?.EnsureLocalKey("branch", j, usedBranchKeys);
			}
		}

		#if UNITY_EDITOR
		/// <summary>编辑器校验时同时维护内容 ID 与行动内部隐藏 key。</summary>
		protected override void OnValidate()
		{
			base.OnValidate();
			EnsureLocalAuthoringKeys();
		}
		#endif
	}
}
