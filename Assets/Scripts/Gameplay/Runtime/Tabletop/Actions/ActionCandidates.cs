using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gameplay.Tabletop;
using Gameplay.Actions;
using GAS.Runtime;
using Gameplay.Content;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 一次候选或行动实例中，作者槽位与局内卡牌的只读绑定。
	/// </summary>
	public sealed class ActionSlotBinding
	{
		private readonly ReadOnlyCollection<TabletopCardId> m_cardIds;

		public ActionSlotDefinition Slot { get; }

		public IReadOnlyList<TabletopCardId> CardIds => m_cardIds;

		internal ActionSlotBinding(ActionSlotDefinition slot, IReadOnlyList<TabletopCardId> cardIds)
		{
			Slot = slot ?? throw new ArgumentNullException("slot");
			m_cardIds = new List<TabletopCardId>(cardIds ?? Array.Empty<TabletopCardId>()).AsReadOnly();
		}
	}

	/// <summary>
	/// 交互查询得到的行动候选快照；可供 UI 展示，但尚未成为权威运行状态。
	/// </summary>
	public sealed class ActionCandidate
	{
		private readonly ReadOnlyCollection<ActionSlotBinding> m_bindings;

		public ActionDefinition Action { get; }

		public IReadOnlyList<ActionSlotBinding> Bindings => m_bindings;

		public int MissingParticipantCount { get; }

		public bool IsReady => MissingParticipantCount == 0;

		internal ActionCandidate(ActionDefinition action, IReadOnlyList<ActionSlotBinding> bindings, int missingParticipantCount)
		{
			Action = action ?? throw new ArgumentNullException("action");
			m_bindings = new List<ActionSlotBinding>(bindings ?? Array.Empty<ActionSlotBinding>()).AsReadOnly();
			MissingParticipantCount = missingParticipantCount;
		}
	}

	/// <summary>
	/// 根据一次牌桌释放事实为行动槽位分配卡牌的内部解析器。
	/// </summary>
	internal static class ActionCandidateResolver
	{
		private readonly struct CandidateParticipant
		{
			internal TabletopCardId CardId { get; }

			internal ContentAsset ContentAsset { get; }

			internal AbilitySystemCell AbilitySystemCell { get; }

			internal CandidateParticipant(TabletopCardId cardId, ContentAsset contentAsset, AbilitySystemCell abilitySystemCell)
			{
				CardId = cardId;
				ContentAsset = contentAsset;
				AbilitySystemCell = abilitySystemCell;
			}
		}

		private sealed class SearchResult
		{
			internal TabletopCardId[][] CardIdsBySlot { get; }

			internal int MissingParticipantCount { get; }

			internal SearchResult(IReadOnlyList<List<TabletopCardId>> working, int missingParticipantCount)
			{
				CardIdsBySlot = new TabletopCardId[working.Count][];
				for (int slotIndex = 0; slotIndex < working.Count; slotIndex++)
				{
					CardIdsBySlot[slotIndex] = working[slotIndex].ToArray();
				}
				MissingParticipantCount = missingParticipantCount;
			}
		}

		internal static ActionCandidate[] FindCandidates(TabletopCardPointerReleaseIntent intent, TabletopCards cards, ContentIndex contentIndex, IReadOnlyList<ActionDefinition> availableActions)
		{
			if (cards == null)
			{
				throw new ArgumentNullException(nameof(cards));
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException("contentIndex");
			}
			if (availableActions == null)
			{
				throw new ArgumentNullException("availableActions");
			}
			if (!TryCreateParticipant(intent.CardId, cards, contentIndex, out var source))
			{
				return Array.Empty<ActionCandidate>();
			}
			List<CandidateParticipant> participants = new List<CandidateParticipant> { source };
			List<CandidateParticipant> draggedStackTail = CreateDraggedStackTail(intent, cards, contentIndex);
			if (intent.TargetCardId.IsValid)
			{
				if (intent.TargetCardId == intent.CardId || !TryCreateParticipant(intent.TargetCardId, cards, contentIndex, out var target))
				{
					return Array.Empty<ActionCandidate>();
				}
				participants.Add(target);
			}
			List<ActionCandidate> candidates = new List<ActionCandidate>();
			HashSet<ContentId> seenActionIds = new HashSet<ContentId>();
			for (int actionIndex = 0; actionIndex < availableActions.Count; actionIndex++)
			{
				ActionDefinition action = availableActions[actionIndex];
				if (action == null)
				{
					throw new InvalidOperationException($"可用行动集合的第 {actionIndex + 1} 项为空。");
				}
				if (!action.ContentId.IsValid)
				{
					throw new InvalidOperationException($"可用行动集合的第 {actionIndex + 1} 项缺少有效内容 ID。");
				}
				if (seenActionIds.Add(action.ContentId) && TryCreateCandidate(action, participants, out var candidate))
				{
					if (TryCreateDraggedTailCandidate(action, participants, draggedStackTail, out var stackTailCandidate))
					{
						candidates.Add(stackTailCandidate);
						continue;
					}
					if (ShouldRejectBecauseDraggedTailCannotFillSourceSlot(action, participants, draggedStackTail))
					{
						continue;
					}
					candidates.Add(candidate);
				}
			}
			return candidates.ToArray();
		}

		private static bool TryCreateParticipant(TabletopCardId cardId, TabletopCards cards, ContentIndex contentIndex, out CandidateParticipant participant)
		{
			if (!cardId.IsValid || !cards.TryGetCard(cardId, out var card) || !contentIndex.TryGet(card.ContentId, out var contentAsset))
			{
				participant = default(CandidateParticipant);
				return false;
			}
			AbilitySystemCell abilitySystemCell = card is CharacterCard characterCard
				? characterCard.AbilitySystem
				: null;
			participant = new CandidateParticipant(cardId, contentAsset, abilitySystemCell);
			return true;
		}

		private static List<CandidateParticipant> CreateDraggedStackTail(
			TabletopCardPointerReleaseIntent intent,
			TabletopCards cards,
			ContentIndex contentIndex)
		{
			if (!intent.IsDrag || !intent.TargetCardId.IsValid ||
				!cards.TryGetStackContaining(intent.CardId, out TabletopCardStack stack))
			{
				return null;
			}

			int startIndex = stack.IndexOf(intent.CardId);
			if (startIndex < 0 || startIndex >= stack.Cards.Count - 1)
			{
				return null;
			}

			List<CandidateParticipant> participants = new List<CandidateParticipant>(
				stack.Cards.Count - startIndex);
			for (int cardIndex = startIndex; cardIndex < stack.Cards.Count; cardIndex++)
			{
				if (!TryCreateParticipant(stack.Cards[cardIndex].Id, cards, contentIndex, out var participant))
				{
					throw new InvalidOperationException(
						$"牌桌堆栈中的卡牌 {stack.Cards[cardIndex].Id} 无法解析为行动参与对象。");
				}
				participants.Add(participant);
			}
			return participants;
		}

		private static bool TryCreateCandidate(ActionDefinition action, IReadOnlyList<CandidateParticipant> participants, out ActionCandidate candidate)
		{
			IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
			if (!AreSlotsUsable(slots))
			{
				candidate = null;
				return false;
			}
			List<TabletopCardId>[] working = new List<TabletopCardId>[slots.Count];
			for (int slotIndex = 0; slotIndex < working.Length; slotIndex++)
			{
				working[slotIndex] = new List<TabletopCardId>();
			}
			SearchResult best = null;
			SearchAssignments(0, participants, slots, working, ref best, forbiddenSlotIndex: -1);
			if (best == null)
			{
				candidate = null;
				return false;
			}
			candidate = CreateCandidate(action, slots, best);
			return true;
		}

		private static bool TryCreateDraggedTailCandidate(
			ActionDefinition action,
			IReadOnlyList<CandidateParticipant> participants,
			IReadOnlyList<CandidateParticipant> draggedStackTail,
			out ActionCandidate candidate)
		{
			candidate = null;
			if (draggedStackTail == null || draggedStackTail.Count <= 1 || participants.Count <= 1)
			{
				return false;
			}

			IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
			if (!AreSlotsUsable(slots))
			{
				return false;
			}

			List<CandidateParticipant> otherParticipants = new List<CandidateParticipant>(participants.Count - 1);
			for (int participantIndex = 1; participantIndex < participants.Count; participantIndex++)
			{
				otherParticipants.Add(participants[participantIndex]);
			}
			for (int sourceSlotIndex = 0; sourceSlotIndex < slots.Count; sourceSlotIndex++)
			{
				ActionSlotDefinition sourceSlot = slots[sourceSlotIndex];
				if (!CanSlotAcceptCount(sourceSlot, draggedStackTail.Count) ||
					!AllParticipantsMatch(sourceSlot, draggedStackTail))
				{
					continue;
				}

				List<TabletopCardId>[] working = new List<TabletopCardId>[slots.Count];
				for (int slotIndex = 0; slotIndex < working.Length; slotIndex++)
				{
					working[slotIndex] = new List<TabletopCardId>();
				}
				for (int tailIndex = 0; tailIndex < draggedStackTail.Count; tailIndex++)
				{
					working[sourceSlotIndex].Add(draggedStackTail[tailIndex].CardId);
				}

				SearchResult best = null;
				SearchAssignments(0, otherParticipants, slots, working, ref best, sourceSlotIndex);
				if (best != null)
				{
					candidate = CreateCandidate(action, slots, best);
					return true;
				}
			}
			return false;
		}

		private static bool ShouldRejectBecauseDraggedTailCannotFillSourceSlot(
			ActionDefinition action,
			IReadOnlyList<CandidateParticipant> participants,
			IReadOnlyList<CandidateParticipant> draggedStackTail)
		{
			if (draggedStackTail == null || draggedStackTail.Count <= 1 || participants == null || participants.Count <= 1)
			{
				return false;
			}

			CandidateParticipant source = participants[0];
			List<CandidateParticipant> otherParticipants = new List<CandidateParticipant>(participants.Count - 1);
			for (int participantIndex = 1; participantIndex < participants.Count; participantIndex++)
			{
				otherParticipants.Add(participants[participantIndex]);
			}

			IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
			for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
			{
				ActionSlotDefinition slot = slots[slotIndex];
				if (!CanSlotAcceptCount(slot, draggedStackTail.Count) ||
					!ActionParticipationEvaluator.MatchesParticipant(slot, source.ContentAsset, source.AbilitySystemCell) ||
					!CanAssignParticipantsExcludingSlot(slots, otherParticipants, slotIndex))
				{
					continue;
				}

				if (!AllParticipantsMatch(slot, draggedStackTail))
				{
					return true;
				}
			}
			return false;
		}

		private static bool CanAssignParticipantsExcludingSlot(
			IReadOnlyList<ActionSlotDefinition> slots,
			IReadOnlyList<CandidateParticipant> participants,
			int forbiddenSlotIndex)
		{
			List<TabletopCardId>[] working = new List<TabletopCardId>[slots.Count];
			for (int slotIndex = 0; slotIndex < working.Length; slotIndex++)
			{
				working[slotIndex] = new List<TabletopCardId>();
			}

			SearchResult best = null;
			SearchAssignments(0, participants, slots, working, ref best, forbiddenSlotIndex);
			return best != null;
		}

		private static ActionCandidate CreateCandidate(
			ActionDefinition action,
			IReadOnlyList<ActionSlotDefinition> slots,
			SearchResult best)
		{
			List<ActionSlotBinding> bindings = new List<ActionSlotBinding>(slots.Count);
			for (int i = 0; i < slots.Count; i++)
			{
				bindings.Add(new ActionSlotBinding(slots[i], best.CardIdsBySlot[i]));
			}
			return new ActionCandidate(action, bindings, best.MissingParticipantCount);
		}

		private static void SearchAssignments(int participantIndex, IReadOnlyList<CandidateParticipant> participants, IReadOnlyList<ActionSlotDefinition> slots, List<TabletopCardId>[] working, ref SearchResult best, int forbiddenSlotIndex)
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
				if (slotIndex == forbiddenSlotIndex)
				{
					continue;
				}
				ActionSlotDefinition slot = slots[slotIndex];
				if ((slot.MaximumParticipants <= 0 || working[slotIndex].Count < slot.MaximumParticipants) && ActionParticipationEvaluator.MatchesParticipant(slot, participant.ContentAsset, participant.AbilitySystemCell))
				{
					working[slotIndex].Add(participant.CardId);
					SearchAssignments(participantIndex + 1, participants, slots, working, ref best, forbiddenSlotIndex);
					working[slotIndex].RemoveAt(working[slotIndex].Count - 1);
				}
			}
		}

		private static bool CanSlotAcceptCount(ActionSlotDefinition slot, int participantCount)
		{
			return slot != null &&
				participantCount >= 0 &&
				(slot.MaximumParticipants == 0 || slot.MaximumParticipants >= participantCount);
		}

		private static bool AllParticipantsMatch(ActionSlotDefinition slot, IReadOnlyList<CandidateParticipant> participants)
		{
			for (int i = 0; i < participants.Count; i++)
			{
				if (!ActionParticipationEvaluator.MatchesParticipant(
						slot,
						participants[i].ContentAsset,
						participants[i].AbilitySystemCell))
				{
					return false;
				}
			}
			return true;
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
				if (slot == null || slot.MinimumParticipants < 0 || slot.MaximumParticipants < 0 || (slot.MaximumParticipants > 0 && slot.MaximumParticipants < slot.MinimumParticipants))
				{
					return false;
				}
			}
			return true;
		}

		private static int CalculateMissingParticipantCount(IReadOnlyList<ActionSlotDefinition> slots, IReadOnlyList<List<TabletopCardId>> cardIdsBySlot)
		{
			int missing = 0;
			for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
			{
				missing += Math.Max(0, slots[slotIndex].MinimumParticipants - cardIdsBySlot[slotIndex].Count);
			}
			return missing;
		}
	}
}
