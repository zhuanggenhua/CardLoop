using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GAS.Runtime;
using Gameplay.Content;
using Gameplay.Tabletop;

namespace Gameplay.Actions
{
    /// <summary>
    /// 一条行动槽位与本次牌桌候选卡牌的不可变绑定。
    /// 绑定只记录查询结果，不表示卡牌已移动、合堆、消耗或开始行动。
    /// </summary>
    public sealed class TabletopCardActionSlotBinding
    {
        private readonly ReadOnlyCollection<TabletopCardId> m_cardIds;

        internal TabletopCardActionSlotBinding(
            ActionSlotDefinition slot,
            IReadOnlyList<TabletopCardId> cardIds)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            m_cardIds = new List<TabletopCardId>(cardIds ?? Array.Empty<TabletopCardId>()).AsReadOnly();
        }

        /// <summary>本次绑定对应的行动槽位作者声明。</summary>
        public ActionSlotDefinition Slot { get; }

        /// <summary>按输入事实顺序绑定到该槽位的局内卡牌身份。</summary>
        public IReadOnlyList<TabletopCardId> CardIds => m_cardIds;
    }

    /// <summary>
    /// 一次牌桌输入查询得到的行动候选。
    /// 候选可以已经满足全部槽位，也可以等待玩家在后续填充交互中补齐参与对象。
    /// </summary>
    public sealed class TabletopCardActionCandidate
    {
        private readonly ReadOnlyCollection<TabletopCardActionSlotBinding> m_bindings;

        internal TabletopCardActionCandidate(
            ActionDefinition action,
            IReadOnlyList<TabletopCardActionSlotBinding> bindings,
            int missingParticipantCount)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            m_bindings = new List<TabletopCardActionSlotBinding>(
                bindings ?? Array.Empty<TabletopCardActionSlotBinding>()).AsReadOnly();
            MissingParticipantCount = missingParticipantCount;
        }

        /// <summary>本候选引用的唯一行动作者源。</summary>
        public ActionDefinition Action { get; }

        /// <summary>按行动作者槽位顺序保存的卡牌绑定，包含尚未填入卡牌的空槽位。</summary>
        public IReadOnlyList<TabletopCardActionSlotBinding> Bindings => m_bindings;

        /// <summary>距离满足全部槽位最少数量仍缺少的参与对象总数。</summary>
        public int MissingParticipantCount { get; }

        /// <summary>当前绑定是否已经满足全部槽位数量下限，可以进入后续行动提交。</summary>
        public bool IsReady => MissingParticipantCount == 0;
    }

    /// <summary>
    /// 把牌桌来源卡、目标卡和调用方明确提供的可用行动集合解析为确定性候选。
    /// 本查询不扫描全局内容、不拥有可用行动列表，也不修改牌桌或行动状态。
    /// </summary>
    public static class TabletopCardActionCandidateResolver
    {
        /// <summary>
        /// 查找本次释放输入能够进入的行动候选。
        /// 每个输入卡牌必须能分配到某个槽位；仍缺少其它必填参与者时保留为未就绪候选，供填充式交互继续补充。
        /// 返回顺序与可用行动输入顺序一致；同一行动 ID 重复出现时只保留第一次。
        /// </summary>
        public static TabletopCardActionCandidate[] FindCandidates(
            TabletopCardPointerReleaseIntent intent,
            TabletopCardState state,
            ContentIndex contentIndex,
            IReadOnlyList<ActionDefinition> availableActions)
        {
            return FindCandidatesInternal(
                intent,
                state,
                contentIndex,
                availableActions,
                abilitySystemCellResolver: null);
        }

        /// <summary>
        /// 查找需要角色当前 GAS 标签参与判断的行动候选。
        /// 只有角色运行状态系统应调用本入口；纯物品、地点或静态符号查询使用 <see cref="FindCandidates"/>，不承担 GAS 依赖。
        /// </summary>
        public static TabletopCardActionCandidate[] FindCandidatesWithAbilitySystem(
            TabletopCardPointerReleaseIntent intent,
            TabletopCardState state,
            ContentIndex contentIndex,
            IReadOnlyList<ActionDefinition> availableActions,
            Func<TabletopCardId, AbilitySystemCell> abilitySystemCellResolver)
        {
            if (abilitySystemCellResolver == null)
            {
                throw new ArgumentNullException(nameof(abilitySystemCellResolver));
            }

            return FindCandidatesInternal(
                intent,
                state,
                contentIndex,
                availableActions,
                abilitySystemCellResolver);
        }

        private static TabletopCardActionCandidate[] FindCandidatesInternal(
            TabletopCardPointerReleaseIntent intent,
            TabletopCardState state,
            ContentIndex contentIndex,
            IReadOnlyList<ActionDefinition> availableActions,
            Func<TabletopCardId, AbilitySystemCell> abilitySystemCellResolver)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (contentIndex == null)
            {
                throw new ArgumentNullException(nameof(contentIndex));
            }

            if (availableActions == null)
            {
                throw new ArgumentNullException(nameof(availableActions));
            }

            if (!TryCreateParticipant(
                    intent.CardId,
                    state,
                    contentIndex,
                    abilitySystemCellResolver,
                    out CandidateParticipant source))
            {
                return Array.Empty<TabletopCardActionCandidate>();
            }

            var participants = new List<CandidateParticipant> { source };
            if (intent.TargetCardId.IsValid)
            {
                if (intent.TargetCardId == intent.CardId ||
                    !TryCreateParticipant(
                        intent.TargetCardId,
                        state,
                        contentIndex,
                        abilitySystemCellResolver,
                        out CandidateParticipant target))
                {
                    return Array.Empty<TabletopCardActionCandidate>();
                }

                participants.Add(target);
            }

            var candidates = new List<TabletopCardActionCandidate>();
            var seenActionIds = new HashSet<ContentId>();
            for (int actionIndex = 0; actionIndex < availableActions.Count; actionIndex++)
            {
                ActionDefinition action = availableActions[actionIndex];
                if (action == null || !action.ContentId.IsValid || !seenActionIds.Add(action.ContentId))
                {
                    continue;
                }

                if (TryCreateCandidate(action, participants, out TabletopCardActionCandidate candidate))
                {
                    candidates.Add(candidate);
                }
            }

            return candidates.ToArray();
        }

        private static bool TryCreateParticipant(
            TabletopCardId cardId,
            TabletopCardState state,
            ContentIndex contentIndex,
            Func<TabletopCardId, AbilitySystemCell> abilitySystemCellResolver,
            out CandidateParticipant participant)
        {
            if (!cardId.IsValid ||
                !state.TryGetCard(cardId, out TabletopCard card) ||
                !contentIndex.TryGet(card.ContentId, out ContentAsset contentAsset))
            {
                participant = default;
                return false;
            }

            participant = new CandidateParticipant(
                cardId,
                contentAsset,
                abilitySystemCellResolver?.Invoke(cardId));
            return true;
        }

        private static bool TryCreateCandidate(
            ActionDefinition action,
            IReadOnlyList<CandidateParticipant> participants,
            out TabletopCardActionCandidate candidate)
        {
            IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
            if (!AreSlotsUsable(slots))
            {
                candidate = null;
                return false;
            }

            var working = new List<TabletopCardId>[slots.Count];
            for (int slotIndex = 0; slotIndex < working.Length; slotIndex++)
            {
                working[slotIndex] = new List<TabletopCardId>();
            }

            SearchResult best = null;
            SearchAssignments(
                participantIndex: 0,
                participants,
                slots,
                working,
                ref best);
            if (best == null)
            {
                candidate = null;
                return false;
            }

            var bindings = new List<TabletopCardActionSlotBinding>(slots.Count);
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                bindings.Add(new TabletopCardActionSlotBinding(slots[slotIndex], best.CardIdsBySlot[slotIndex]));
            }

            candidate = new TabletopCardActionCandidate(action, bindings, best.MissingParticipantCount);
            return true;
        }

        private static void SearchAssignments(
            int participantIndex,
            IReadOnlyList<CandidateParticipant> participants,
            IReadOnlyList<ActionSlotDefinition> slots,
            List<TabletopCardId>[] working,
            ref SearchResult best)
        {
            if (participantIndex >= participants.Count)
            {
                int missing = CalculateMissingParticipantCount(slots, working);
                if (best == null || missing < best.MissingParticipantCount)
                {
                    best = new SearchResult(working, missing);
                }

                return;
            }

            CandidateParticipant participant = participants[participantIndex];
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                ActionSlotDefinition slot = slots[slotIndex];
                if ((slot.MaximumParticipants > 0 && working[slotIndex].Count >= slot.MaximumParticipants) ||
                    !ActionParticipationEvaluator.MatchesParticipant(
                        slot,
                        participant.ContentAsset,
                        participant.AbilitySystemCell))
                {
                    continue;
                }

                working[slotIndex].Add(participant.CardId);
                SearchAssignments(
                    participantIndex + 1,
                    participants,
                    slots,
                    working,
                    ref best);
                working[slotIndex].RemoveAt(working[slotIndex].Count - 1);
            }
        }

        private static bool AreSlotsUsable(IReadOnlyList<ActionSlotDefinition> slots)
        {
            if (slots == null || slots.Count == 0)
            {
                return false;
            }

            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                ActionSlotDefinition slot = slots[slotIndex];
                if (slot == null ||
                    slot.MinimumParticipants < 0 ||
                    slot.MaximumParticipants < 0 ||
                    (slot.MaximumParticipants > 0 && slot.MaximumParticipants < slot.MinimumParticipants))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CalculateMissingParticipantCount(
            IReadOnlyList<ActionSlotDefinition> slots,
            IReadOnlyList<List<TabletopCardId>> cardIdsBySlot)
        {
            int missing = 0;
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                missing += Math.Max(0, slots[slotIndex].MinimumParticipants - cardIdsBySlot[slotIndex].Count);
            }

            return missing;
        }

        private readonly struct CandidateParticipant
        {
            internal CandidateParticipant(
                TabletopCardId cardId,
                ContentAsset contentAsset,
                AbilitySystemCell abilitySystemCell)
            {
                CardId = cardId;
                ContentAsset = contentAsset;
                AbilitySystemCell = abilitySystemCell;
            }

            internal TabletopCardId CardId { get; }
            internal ContentAsset ContentAsset { get; }
            internal AbilitySystemCell AbilitySystemCell { get; }
        }

        private sealed class SearchResult
        {
            internal SearchResult(IReadOnlyList<List<TabletopCardId>> working, int missingParticipantCount)
            {
                CardIdsBySlot = new TabletopCardId[working.Count][];
                for (int slotIndex = 0; slotIndex < working.Count; slotIndex++)
                {
                    CardIdsBySlot[slotIndex] = working[slotIndex].ToArray();
                }

                MissingParticipantCount = missingParticipantCount;
            }

            internal TabletopCardId[][] CardIdsBySlot { get; }
            internal int MissingParticipantCount { get; }
        }
    }

    /// <summary>
    /// 把玩家提交的行动唯一内容 ID 解析回本次候选快照。
    /// 本入口不提供“第一个候选就是默认项”的隐式规则，也不执行选中的行动。
    /// </summary>
    public static class TabletopCardActionCandidateSelector
    {
        /// <summary>
        /// 只允许选择本次候选集合中实际存在的行动。候选为空、行动 ID 无效或未命中时返回 false。
        /// </summary>
        public static bool TrySelect(
            IReadOnlyList<TabletopCardActionCandidate> candidates,
            ContentId selectedActionId,
            out TabletopCardActionCandidate selectedCandidate)
        {
            if (candidates != null && selectedActionId.IsValid)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    TabletopCardActionCandidate candidate = candidates[i];
                    if (candidate?.Action != null && candidate.Action.ContentId.Equals(selectedActionId))
                    {
                        selectedCandidate = candidate;
                        return true;
                    }
                }
            }

            selectedCandidate = null;
            return false;
        }
    }
}
