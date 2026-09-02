using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gameplay.Actions;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 行动实例在当前单局中的权威生命周期状态。
	/// </summary>
	public enum ActionInstanceState
	{
		Running = 0,
		Paused = 10,
		Completed = 20,
		Cancelled = 30
	}

	/// <summary>
	/// 行动实例取消时记录的明确原因，用于存档、联机和调试事实。
	/// </summary>
	public enum ActionCancellationReason
	{
		None = 0,
		Requested = 10,
		ParticipantInvalidated = 20,
		ScenarioEnded = 30
	}

	/// <summary>
	/// 已经通过权威复核的运行中行动，拥有参与者、进度、终态和开始时冻结的结果计划。
	/// </summary>
	public sealed class ActionInstance
	{
		private readonly ReadOnlyCollection<ActionSlotBinding> m_bindings;

		internal ActionDefinition Action { get; }

		public ContentId ActionId { get; }

		public string ResultBranchKey { get; }

		public IReadOnlyList<ActionSlotBinding> Bindings => m_bindings;

		public int TurnCost { get; }

		public float ProgressedTurns { get; private set; }

		public ActionInstanceState State { get; private set; }

		public ActionCancellationReason CancellationReason { get; private set; }

		public float Progress => (TurnCost == 0) ? 1f : Mathf.Clamp01(ProgressedTurns / (float)TurnCost);

		public float RemainingTurns => Mathf.Max(0f, TurnCost - ProgressedTurns);

		internal ActionResultPlan ResultPlan { get; }

		internal ActionInstance(ActionCandidate candidate, int turnCost, string resultBranchKey, ActionResultPlan resultPlan)
			: this(
				candidate,
				turnCost,
				resultBranchKey,
				resultPlan,
				0f,
				turnCost == 0 ? ActionInstanceState.Completed : ActionInstanceState.Running)
		{
		}

		private ActionInstance(
			ActionCandidate candidate,
			int turnCost,
			string resultBranchKey,
			ActionResultPlan resultPlan,
			float progressedTurns,
			ActionInstanceState state)
		{
			if (candidate == null)
			{
				throw new ArgumentNullException("candidate");
			}
			if (turnCost < 0)
			{
				throw new ArgumentOutOfRangeException("turnCost", turnCost, "行动实例回合消耗必须大于或等于 0。");
			}
			Action = candidate.Action;
			ActionId = Action.ContentId;
			m_bindings = new List<ActionSlotBinding>(candidate.Bindings).AsReadOnly();
			ResultBranchKey = resultBranchKey ?? string.Empty;
			ResultPlan = resultPlan ?? throw new ArgumentNullException("resultPlan");
			TurnCost = turnCost;
			ProgressedTurns = progressedTurns;
			State = state;
			CancellationReason = ActionCancellationReason.None;
		}

		/// <summary>
		/// 从同一牌桌、同一作者源重新复核过的快照恢复一个未完成行动。
		/// </summary>
		internal static ActionInstance Restore(ActionCandidate candidate, ActionInstanceSnapshot snapshot)
		{
			if (candidate == null)
			{
				throw new ArgumentNullException("candidate");
			}
			if (snapshot == null)
			{
				throw new ArgumentNullException("snapshot");
			}
			if (!candidate.IsReady)
			{
				throw new InvalidOperationException($"行动实例快照 {snapshot.ActionId} 缺少参与对象，不能恢复。");
			}
			if (!snapshot.ActionId.IsValid || snapshot.ActionId != candidate.Action.ContentId)
			{
				throw new InvalidOperationException(
					$"行动实例快照引用的行动 {snapshot.ActionId} 与当前作者源 {candidate.Action.ContentId} 不一致。");
			}
			if (snapshot.TurnCost <= 0 || snapshot.TurnCost != candidate.Action.TurnCost)
			{
				throw new InvalidOperationException(
					$"行动实例快照 {snapshot.ActionId} 的回合消耗 {snapshot.TurnCost} 与当前作者源 {candidate.Action.TurnCost} 不一致，不能恢复。");
			}
			if (snapshot.State != ActionInstanceState.Running && snapshot.State != ActionInstanceState.Paused)
			{
				throw new InvalidOperationException(
					$"行动实例快照 {snapshot.ActionId} 的状态 {snapshot.State} 不是可恢复的活动状态。");
			}
			if (!float.IsFinite(snapshot.ProgressedTurns) ||
				snapshot.ProgressedTurns < 0f ||
				snapshot.ProgressedTurns >= snapshot.TurnCost)
			{
				throw new InvalidOperationException(
					$"行动实例快照 {snapshot.ActionId} 的已推进回合数 {snapshot.ProgressedTurns} 不在活动行动的合法范围内。");
			}

			ValidateSnapshotBindings(candidate, snapshot);
			ValidateResultBranch(candidate, snapshot);
			return new ActionInstance(
				candidate,
				snapshot.TurnCost,
				snapshot.ResultBranchKey,
				snapshot.RestoreResultPlan(),
				snapshot.ProgressedTurns,
				snapshot.State);
		}

		public ActionInstanceSnapshot CreateSnapshot()
		{
			List<ActionInstanceBindingSnapshot> bindingSnapshots = new List<ActionInstanceBindingSnapshot>(Bindings.Count);
			for (int i = 0; i < Bindings.Count; i++)
			{
				ActionSlotBinding binding = Bindings[i];
				bindingSnapshots.Add(new ActionInstanceBindingSnapshot(binding.Slot.Key, binding.CardIds));
			}
			return new ActionInstanceSnapshot(
				ActionId,
				TurnCost,
				ProgressedTurns,
				State,
				ResultBranchKey,
				bindingSnapshots,
				ResultPlan);
		}

		/// <summary>判断指定牌桌卡牌是否参与了这次行动；只读查询，不改变行动生命周期。</summary>
		public bool ContainsParticipant(TabletopCardId cardId)
		{
			if (!cardId.IsValid)
			{
				return false;
			}
			for (int bindingIndex = 0; bindingIndex < Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = Bindings[bindingIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (cardIds[cardIndex] == cardId)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>返回第一个有效参与卡牌，用于牌桌表现把行动进度锚定到真实参与对象。</summary>
		public bool TryGetFirstParticipantCardId(out TabletopCardId cardId)
		{
			for (int bindingIndex = 0; bindingIndex < Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = Bindings[bindingIndex].CardIds;
				for (int participantIndex = 0; participantIndex < cardIds.Count; participantIndex++)
				{
					if (cardIds[participantIndex].IsValid)
					{
						cardId = cardIds[participantIndex];
						return true;
					}
				}
			}

			cardId = default;
			return false;
		}

		internal void Advance(float turnUnits)
		{
			if (State != ActionInstanceState.Paused)
			{
				if (State != ActionInstanceState.Running)
				{
					throw new InvalidOperationException($"行动实例 {ActionId} 处于 {State}，只有运行中的行动可以推进。");
				}
				if (!float.IsFinite(turnUnits) || turnUnits <= 0f)
				{
					throw new ArgumentOutOfRangeException("turnUnits", turnUnits, "行动实例每次推进的回合单位必须是大于 0 的有限数值。");
				}
				ProgressedTurns = Math.Min(TurnCost, ProgressedTurns + turnUnits);
				if (ProgressedTurns >= (float)TurnCost)
				{
					State = ActionInstanceState.Completed;
				}
			}
		}

		internal void Pause()
		{
			RequireState(ActionInstanceState.Running, "暂停");
			State = ActionInstanceState.Paused;
		}

		internal void Resume()
		{
			RequireState(ActionInstanceState.Paused, "恢复");
			State = ActionInstanceState.Running;
		}

		internal void Cancel(ActionCancellationReason reason)
		{
			ActionInstanceState state = State;
			if (state != ActionInstanceState.Running &&
				state != ActionInstanceState.Paused)
			{
				throw new InvalidOperationException($"行动实例 {ActionId} 处于 {State}，只有运行或暂停中的行动可以取消。");
			}
			if (reason == ActionCancellationReason.None)
			{
				throw new ArgumentOutOfRangeException("reason", "取消行动实例必须提供明确原因。");
			}
			CancellationReason = reason;
			State = ActionInstanceState.Cancelled;
		}

		private void RequireState(ActionInstanceState expected, string operation)
		{
			if (State != expected)
			{
				throw new InvalidOperationException($"行动实例 {ActionId} 处于 {State}，不能执行{operation}；要求状态为 {expected}。");
			}
		}

		private static void ValidateSnapshotBindings(ActionCandidate candidate, ActionInstanceSnapshot snapshot)
		{
			if (snapshot.Bindings == null || snapshot.Bindings.Count != candidate.Bindings.Count)
			{
				throw new InvalidOperationException(
					$"行动实例快照 {snapshot.ActionId} 的槽位绑定数量与当前行动作者源不一致。");
			}

			for (int bindingIndex = 0; bindingIndex < candidate.Bindings.Count; bindingIndex++)
			{
				ActionSlotBinding candidateBinding = candidate.Bindings[bindingIndex];
				ActionInstanceBindingSnapshot snapshotBinding = snapshot.Bindings[bindingIndex];
				if (snapshotBinding == null ||
					!string.Equals(candidateBinding.Slot.Key, snapshotBinding.SlotKey, StringComparison.Ordinal))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {snapshot.ActionId} 的第 {bindingIndex + 1} 个槽位与当前行动作者源不一致。");
				}

				TabletopCardId[] snapshotCardIds = snapshotBinding.CreateCardIds();
				if (snapshotCardIds.Length != candidateBinding.CardIds.Count)
				{
					throw new InvalidOperationException(
						$"行动实例快照 {snapshot.ActionId} 的槽位 {snapshotBinding.SlotKey} 参与对象数量与当前牌桌不一致。");
				}
				for (int cardIndex = 0; cardIndex < snapshotCardIds.Length; cardIndex++)
				{
					if (snapshotCardIds[cardIndex] != candidateBinding.CardIds[cardIndex])
					{
						throw new InvalidOperationException(
							$"行动实例快照 {snapshot.ActionId} 的槽位 {snapshotBinding.SlotKey} 参与对象与当前牌桌不一致。");
					}
				}
			}
		}

		private static void ValidateResultBranch(ActionCandidate candidate, ActionInstanceSnapshot snapshot)
		{
			if (candidate.Action.ResultBranches.Count == 0)
			{
				if (!string.IsNullOrEmpty(snapshot.ResultBranchKey))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {snapshot.ActionId} 记录了随机结果分支，但当前行动没有随机结果分支。");
				}
				return;
			}

			if (string.IsNullOrWhiteSpace(snapshot.ResultBranchKey))
			{
				throw new InvalidOperationException($"行动实例快照 {snapshot.ActionId} 缺少已选随机结果分支键。");
			}
			for (int branchIndex = 0; branchIndex < candidate.Action.ResultBranches.Count; branchIndex++)
			{
				ActionResultBranchDefinition branch = candidate.Action.ResultBranches[branchIndex];
				if (branch != null &&
					string.Equals(branch.Key, snapshot.ResultBranchKey, StringComparison.Ordinal))
				{
					return;
				}
			}

			throw new InvalidOperationException(
				$"行动实例快照 {snapshot.ActionId} 记录的随机结果分支 {snapshot.ResultBranchKey} 不存在于当前行动作者源。");
		}
	}
}
