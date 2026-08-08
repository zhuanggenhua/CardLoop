using System;
using System.Collections.Generic;
using GAS.Runtime;
using Gameplay.Content;
using Gameplay.Tabletop;
using UnityEngine;

namespace Gameplay.Actions
{
    /// <summary>
    /// 一个行动内的参与位置，例如行动者、目标、工具或仪式材料。
    /// 槽位使用行动内稳定键区分绑定位置；参与对象的开放分类和“符号”继续使用 EX-GAS GameplayTag。
    /// </summary>
    [Serializable]
    public sealed class ActionSlotDefinition
    {
        [Header("槽位身份")]
        [SerializeField, InspectorName("槽位键"), Tooltip("只在当前行动内稳定区分该参与位置，供后续选择、存档和联机绑定使用。它不是第二个内容 ID，也不参与全局内容索引。")]
        private string m_key;

        [SerializeField, InspectorName("显示名"), Tooltip("给内容作者和玩家看的槽位名称，例如行动者、目标或工具；为空时使用槽位键。")]
        private string m_displayName;

        [Header("数量")]
        [SerializeField, Min(0), InspectorName("最少参与数"), Tooltip("该槽位成立所需的最少对象数量；0 表示槽位可选。")]
        private int m_minimumParticipants = 1;

        [SerializeField, Min(0), InspectorName("最多参与数"), Tooltip("该槽位在同一次行动内允许的最多对象数量；0 表示不限制，例如任意数量角色共同参与一次行动。工位数量和并行行动数量不由这里表达。")]
        private int m_maximumParticipants = 1;

        [Header("固定内容")]
        [SerializeField, InspectorName("允许的内容 ID"), Tooltip("非空时，参与对象必须命中其中一个唯一内容 ID；为空时不限制具体内容，只继续检查标签条件。")]
        private ContentId[] m_allowedContentIds = Array.Empty<ContentId>();

        [Header("内容静态标签")]
        [SerializeField, InspectorName("必须全部具有"), Tooltip("参与内容必须满足这里的全部 EX-GAS 标签查询；子标签可以匹配父标签。")]
        private int[] m_requiredAllContentTagCodes = Array.Empty<int>();

        [SerializeField, InspectorName("至少具有一个"), Tooltip("非空时，参与内容至少满足这里一个 EX-GAS 标签查询；子标签可以匹配父标签。")]
        private int[] m_requiredAnyContentTagCodes = Array.Empty<int>();

        [SerializeField, InspectorName("不能具有"), Tooltip("参与内容只要满足这里任意一个 EX-GAS 标签查询，就不能进入该槽位。")]
        private int[] m_requiredNoneContentTagCodes = Array.Empty<int>();

        [Header("角色动态 GAS 标签")]
        [SerializeField, InspectorName("必须全部具有"), Tooltip("非空时，参与对象必须提供 AbilitySystemCell，并在当前运行状态满足全部 EX-GAS 标签。")]
        private int[] m_requiredAllAbilitySystemTagCodes = Array.Empty<int>();

        [SerializeField, InspectorName("至少具有一个"), Tooltip("非空时，参与对象的 AbilitySystemCell 至少满足这里一个 EX-GAS 标签。")]
        private int[] m_requiredAnyAbilitySystemTagCodes = Array.Empty<int>();

        [SerializeField, InspectorName("不能具有"), Tooltip("参与对象的 AbilitySystemCell 只要满足这里任意一个 EX-GAS 标签，就不能进入该槽位。")]
        private int[] m_requiredNoneAbilitySystemTagCodes = Array.Empty<int>();

        /// <summary>当前行动内用于稳定绑定该槽位的键，不承担全局内容身份。</summary>
        public string Key => m_key ?? string.Empty;

        /// <summary>面向玩家和作者的槽位名称；未填写时回退到槽位键。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(m_displayName) ? Key : m_displayName;

        /// <summary>成立所需的最少参与对象数量。</summary>
        public int MinimumParticipants => m_minimumParticipants;

        /// <summary>允许的最多参与对象数量；0 表示没有上限。</summary>
        public int MaximumParticipants => m_maximumParticipants;

        /// <summary>允许进入该槽位的精确内容身份白名单；空集合表示不限制具体内容。</summary>
        public IReadOnlyList<ContentId> AllowedContentIds =>
            m_allowedContentIds ?? Array.Empty<ContentId>();

        /// <summary>参与内容必须全部满足的 EX-GAS 静态标签查询。</summary>
        public IReadOnlyList<int> RequiredAllContentTagCodes =>
            m_requiredAllContentTagCodes ?? Array.Empty<int>();

        /// <summary>参与内容至少满足一个的 EX-GAS 静态标签查询。</summary>
        public IReadOnlyList<int> RequiredAnyContentTagCodes =>
            m_requiredAnyContentTagCodes ?? Array.Empty<int>();

        /// <summary>参与内容不能满足任意一个的 EX-GAS 静态标签查询。</summary>
        public IReadOnlyList<int> RequiredNoneContentTagCodes =>
            m_requiredNoneContentTagCodes ?? Array.Empty<int>();

        /// <summary>角色运行状态必须全部满足的 EX-GAS 动态标签查询。</summary>
        public IReadOnlyList<int> RequiredAllAbilitySystemTagCodes =>
            m_requiredAllAbilitySystemTagCodes ?? Array.Empty<int>();

        /// <summary>角色运行状态至少满足一个的 EX-GAS 动态标签查询。</summary>
        public IReadOnlyList<int> RequiredAnyAbilitySystemTagCodes =>
            m_requiredAnyAbilitySystemTagCodes ?? Array.Empty<int>();

        /// <summary>角色运行状态不能满足任意一个的 EX-GAS 动态标签查询。</summary>
        public IReadOnlyList<int> RequiredNoneAbilitySystemTagCodes =>
            m_requiredNoneAbilitySystemTagCodes ?? Array.Empty<int>();
    }

    /// <summary>
    /// 对行动参与声明执行无副作用查询。
    /// 静态内容标签直接使用 EX-GAS 的层级比较，角色动态标签直接查询 AbilitySystemCell；本类型不保存标签表或运行状态。
    /// </summary>
    public static class ActionParticipationEvaluator
    {
        /// <summary>
        /// 检查已明确分配给槽位的参与对象数量。无效槽位、负数和作者配置矛盾都返回 false。
        /// </summary>
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

            return participantCount >= slot.MinimumParticipants &&
                (slot.MaximumParticipants == 0 || participantCount <= slot.MaximumParticipants);
        }

        /// <summary>
        /// 检查一个已解析的内容资产和可选角色运行状态是否满足槽位声明。
        /// 本方法只回答“能否参与”，不会扣除、移动、开始行动或修改任何对象。
        /// </summary>
        public static bool MatchesParticipant(
            ActionSlotDefinition slot,
            ContentAsset contentAsset,
            AbilitySystemCell abilitySystemCell)
        {
            return MatchesContent(slot, contentAsset) && MatchesAbilitySystemTags(slot, abilitySystemCell);
        }

        /// <summary>
        /// 检查固定内容身份与内容静态标签。标签父子关系只通过 EX-GAS <see cref="TagHelper.HasTag(int,int)"/> 判断。
        /// </summary>
        public static bool MatchesContent(
            ActionSlotDefinition slot,
            ContentAsset contentAsset)
        {
            if (slot == null || contentAsset == null || !MatchesAllowedContent(slot, contentAsset.ContentId))
            {
                return false;
            }

            IReadOnlyList<int> actualTags = contentAsset.TagCodes;
            return MatchesAllContentTags(actualTags, slot.RequiredAllContentTagCodes) &&
                MatchesAnyContentTag(actualTags, slot.RequiredAnyContentTagCodes) &&
                MatchesNoContentTags(actualTags, slot.RequiredNoneContentTagCodes);
        }

        /// <summary>
        /// 检查角色当前固有和临时标签。槽位没有动态标签条件时允许非角色内容传入空 Cell；存在条件时空 Cell 必然失败。
        /// </summary>
        public static bool MatchesAbilitySystemTags(
            ActionSlotDefinition slot,
            AbilitySystemCell abilitySystemCell)
        {
            if (slot == null)
            {
                return false;
            }

            bool hasRequirement = slot.RequiredAllAbilitySystemTagCodes.Count > 0 ||
                slot.RequiredAnyAbilitySystemTagCodes.Count > 0 ||
                slot.RequiredNoneAbilitySystemTagCodes.Count > 0;
            if (!hasRequirement)
            {
                return true;
            }

            if (abilitySystemCell == null)
            {
                return false;
            }

            return (slot.RequiredAllAbilitySystemTagCodes.Count == 0 ||
                    abilitySystemCell.HasAllTags(slot.RequiredAllAbilitySystemTagCodes)) &&
                (slot.RequiredAnyAbilitySystemTagCodes.Count == 0 ||
                    abilitySystemCell.HasAnyTags(slot.RequiredAnyAbilitySystemTagCodes)) &&
                (slot.RequiredNoneAbilitySystemTagCodes.Count == 0 ||
                    !abilitySystemCell.HasAnyTags(slot.RequiredNoneAbilitySystemTagCodes));
        }

        private static bool MatchesAllowedContent(
            ActionSlotDefinition slot,
            ContentId contentId)
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

        private static bool MatchesAllContentTags(
            IReadOnlyList<int> actualTags,
            IReadOnlyList<int> requiredTags)
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

        private static bool MatchesAnyContentTag(
            IReadOnlyList<int> actualTags,
            IReadOnlyList<int> requiredTags)
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

        private static bool MatchesNoContentTags(
            IReadOnlyList<int> actualTags,
            IReadOnlyList<int> forbiddenTags)
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
