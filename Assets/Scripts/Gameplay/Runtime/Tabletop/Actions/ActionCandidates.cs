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
			SearchAssignments(0, participants, slots, working, ref best);
			if (best == null)
			{
				candidate = null;
				return false;
			}
			List<ActionSlotBinding> bindings = new List<ActionSlotBinding>(slots.Count);
			for (int i = 0; i < slots.Count; i++)
			{
				bindings.Add(new ActionSlotBinding(slots[i], best.CardIdsBySlot[i]));
			}
			candidate = new ActionCandidate(action, bindings, best.MissingParticipantCount);
			return true;
		}

		private static void SearchAssignments(int participantIndex, IReadOnlyList<CandidateParticipant> participants, IReadOnlyList<ActionSlotDefinition> slots, List<TabletopCardId>[] working, ref SearchResult best)
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
				if ((slot.MaximumParticipants <= 0 || working[slotIndex].Count < slot.MaximumParticipants) && ActionParticipationEvaluator.MatchesParticipant(slot, participant.ContentAsset, participant.AbilitySystemCell))
				{
					working[slotIndex].Add(participant.CardId);
					SearchAssignments(participantIndex + 1, participants, slots, working, ref best);
					working[slotIndex].RemoveAt(working[slotIndex].Count - 1);
				}
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
