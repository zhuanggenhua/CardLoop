using System;
using System.Collections.Generic;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;
using GAS.Runtime;

namespace Gameplay.Actions
{
	/// <summary>
	/// 行动中的参与槽位定义，描述人数、固定内容与 EX-GAS 标签条件。
	/// </summary>
	[Serializable]
	public sealed class ActionSlotDefinition
	{
		[SerializeField]
		[HideInInspector]
		private string m_key;

		[Header("槽位身份")]
		[SerializeField]
		[LabelText("显示名")]
		[Tooltip("给内容作者和玩家看的槽位名称，例如行动者、目标或工具。内部稳定键由行动资产自动维护，不需要手填。")]
		private string m_displayName;

		[Header("数量")]
		[SerializeField]
		[Min(0f)]
		[LabelText("最少参与数")]
		[Tooltip("该槽位成立所需的最少对象数量；0 表示槽位可选。")]
		private int m_minimumParticipants = 1;

		[SerializeField]
		[Min(0f)]
		[LabelText("最多参与数")]
		[Tooltip("该槽位在同一次行动内允许的最多对象数量；0 表示不限制，例如任意数量角色共同参与一次行动。工位数量和并行行动数量不由这里表达。")]
		private int m_maximumParticipants = 1;

		[SerializeField]
		[LabelText("整叠匹配允许同类超量")]
		[Tooltip("只用于完整牌堆制作匹配：开启后，超过本槽位最多数量、但仍匹配本槽位条件的卡牌可以留在牌堆里，不会被本次行动绑定或消耗。用于承接 StackCraft 标准配方里 Resource 材料的“至少数量”语义。")]
		private bool m_allowAdditionalMatchingParticipantsInStack;

		[Header("固定内容")]
		[SerializeField]
		[ContentIdReference]
		[LabelText("允许的内容")]
		[Tooltip("非空时，参与对象必须命中其中一个所选内容；为空时不限制具体内容，只继续检查标签条件。运行时只保存所选内容的唯一 ID。")]
		private ContentId[] m_allowedContentIds = Array.Empty<ContentId>();

		[Header("内容静态标签")]
		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("必须全部具有")]
		[Tooltip("参与内容必须满足这里的全部 EX-GAS 标签查询；子标签可以匹配父标签。")]
		private int[] m_requiredAllContentTagCodes = Array.Empty<int>();

		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("至少具有一个")]
		[Tooltip("非空时，参与内容至少满足这里一个 EX-GAS 标签查询；子标签可以匹配父标签。")]
		private int[] m_requiredAnyContentTagCodes = Array.Empty<int>();

		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("不能具有")]
		[Tooltip("参与内容只要满足这里任意一个 EX-GAS 标签查询，就不能进入该槽位。")]
		private int[] m_requiredNoneContentTagCodes = Array.Empty<int>();

		[Header("角色动态 GAS 标签")]
		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("必须全部具有")]
		[Tooltip("非空时，参与对象必须提供 AbilitySystemCell，并在当前运行状态满足全部 EX-GAS 标签。")]
		private int[] m_requiredAllAbilitySystemTagCodes = Array.Empty<int>();

		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("至少具有一个")]
		[Tooltip("非空时，参与对象的 AbilitySystemCell 至少满足这里一个 EX-GAS 标签。")]
		private int[] m_requiredAnyAbilitySystemTagCodes = Array.Empty<int>();

		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("不能具有")]
		[Tooltip("参与对象的 AbilitySystemCell 只要满足这里任意一个 EX-GAS 标签，就不能进入该槽位。")]
		private int[] m_requiredNoneAbilitySystemTagCodes = Array.Empty<int>();

		public string Key => m_key ?? string.Empty;

		public string DisplayName => string.IsNullOrWhiteSpace(m_displayName) ? Key : m_displayName;

		public int MinimumParticipants => m_minimumParticipants;

		public int MaximumParticipants => m_maximumParticipants;

		public bool AllowAdditionalMatchingParticipantsInStack => m_allowAdditionalMatchingParticipantsInStack;

		public IReadOnlyList<ContentId> AllowedContentIds => m_allowedContentIds ?? Array.Empty<ContentId>();

		public IReadOnlyList<int> RequiredAllContentTagCodes => m_requiredAllContentTagCodes ?? Array.Empty<int>();

		public IReadOnlyList<int> RequiredAnyContentTagCodes => m_requiredAnyContentTagCodes ?? Array.Empty<int>();

		public IReadOnlyList<int> RequiredNoneContentTagCodes => m_requiredNoneContentTagCodes ?? Array.Empty<int>();

		public IReadOnlyList<int> RequiredAllAbilitySystemTagCodes => m_requiredAllAbilitySystemTagCodes ?? Array.Empty<int>();

		public IReadOnlyList<int> RequiredAnyAbilitySystemTagCodes => m_requiredAnyAbilitySystemTagCodes ?? Array.Empty<int>();

		public IReadOnlyList<int> RequiredNoneAbilitySystemTagCodes => m_requiredNoneAbilitySystemTagCodes ?? Array.Empty<int>();

		internal void EnsureLocalKey(string prefix, int zeroBasedIndex, ISet<string> usedKeys)
		{
			m_key = ActionLocalKeyUtility.EnsureUniqueKey(m_key, prefix, zeroBasedIndex, usedKeys);
		}
	}

	/// <summary>
	/// 解释行动槽位条件的纯规则入口，不保存参与者状态。
	/// </summary>
	public static class ActionParticipationEvaluator
	{
		public static bool IsParticipantCountSatisfied(ActionSlotDefinition slot, int participantCount)
		{
			if (slot == null || participantCount < 0 || slot.MinimumParticipants < 0 || slot.MaximumParticipants < 0)
			{
				return false;
			}
			if (slot.MaximumParticipants > 0 && slot.MaximumParticipants < slot.MinimumParticipants)
			{
				return false;
			}
			return participantCount >= slot.MinimumParticipants && (slot.MaximumParticipants == 0 || participantCount <= slot.MaximumParticipants);
		}

		public static bool MatchesParticipant(ActionSlotDefinition slot, ContentAsset contentAsset, AbilitySystemCell abilitySystemCell)
		{
			return MatchesContent(slot, contentAsset) && MatchesAbilitySystemTags(slot, abilitySystemCell);
		}

		public static bool MatchesContent(ActionSlotDefinition slot, ContentAsset contentAsset)
		{
			if (slot == null || contentAsset == null || !MatchesAllowedContent(slot, contentAsset.ContentId))
			{
				return false;
			}
			IReadOnlyList<int> actualTags = contentAsset.TagCodes;
			return MatchesAllContentTags(actualTags, slot.RequiredAllContentTagCodes) && MatchesAnyContentTag(actualTags, slot.RequiredAnyContentTagCodes) && MatchesNoContentTags(actualTags, slot.RequiredNoneContentTagCodes);
		}

		public static bool MatchesAbilitySystemTags(ActionSlotDefinition slot, AbilitySystemCell abilitySystemCell)
		{
			if (slot == null)
			{
				return false;
			}
			if (slot.RequiredAllAbilitySystemTagCodes.Count <= 0 && slot.RequiredAnyAbilitySystemTagCodes.Count <= 0 && slot.RequiredNoneAbilitySystemTagCodes.Count <= 0)
			{
				return true;
			}
			if (abilitySystemCell == null)
			{
				return false;
			}
			return (slot.RequiredAllAbilitySystemTagCodes.Count == 0 || abilitySystemCell.HasAllTags(slot.RequiredAllAbilitySystemTagCodes)) && (slot.RequiredAnyAbilitySystemTagCodes.Count == 0 || abilitySystemCell.HasAnyTags(slot.RequiredAnyAbilitySystemTagCodes)) && (slot.RequiredNoneAbilitySystemTagCodes.Count == 0 || !abilitySystemCell.HasAnyTags(slot.RequiredNoneAbilitySystemTagCodes));
		}

		private static bool MatchesAllowedContent(ActionSlotDefinition slot, ContentId contentId)
		{
			if (slot.AllowedContentIds.Count == 0)
			{
				return true;
			}
			for (int i = 0; i < slot.AllowedContentIds.Count; i++)
			{
				if (slot.AllowedContentIds[i].Equals(contentId))
				{
					return true;
				}
			}
			return false;
		}

		private static bool MatchesAllContentTags(IReadOnlyList<int> actualTags, IReadOnlyList<int> requiredTags)
		{
			for (int i = 0; i < requiredTags.Count; i++)
			{
				if (!MatchesAtLeastOneActualTag(actualTags, requiredTags[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool MatchesAnyContentTag(IReadOnlyList<int> actualTags, IReadOnlyList<int> requiredTags)
		{
			if (requiredTags.Count == 0)
			{
				return true;
			}
			for (int i = 0; i < requiredTags.Count; i++)
			{
				if (MatchesAtLeastOneActualTag(actualTags, requiredTags[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static bool MatchesNoContentTags(IReadOnlyList<int> actualTags, IReadOnlyList<int> forbiddenTags)
		{
			for (int i = 0; i < forbiddenTags.Count; i++)
			{
				if (MatchesAtLeastOneActualTag(actualTags, forbiddenTags[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool MatchesAtLeastOneActualTag(IReadOnlyList<int> actualTags, int queryTag)
		{
			for (int i = 0; i < actualTags.Count; i++)
			{
				if (TagHelper.HasTag(actualTags[i], queryTag))
				{
					return true;
				}
			}
			return false;
		}
	}
}
