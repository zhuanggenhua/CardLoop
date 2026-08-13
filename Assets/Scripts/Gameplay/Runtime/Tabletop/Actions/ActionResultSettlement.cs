using System;
using System.Collections.Generic;
using Gameplay.Content;
using Gameplay.Tabletop;
using Gameplay.Actions;
using UnityEngine;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 把行动作者结果编译为不可变计划，并在完成后由牌桌原子提交。
	/// </summary>
	internal static class ActionResultSettlement
	{
		private readonly struct CardCreationCommit
		{
			internal ContentId ContentId { get; }

			internal int Count { get; }

			internal Vector2 Position { get; }

			internal CardCreationCommit(ContentId contentId, int count, Vector2 position)
			{
				ContentId = contentId;
				Count = count;
				Position = position;
			}
		}

		internal static ActionResultPlan Compile(ActionDefinition action, ActionCandidate candidate, string resultBranchKey, ContentIndex contentIndex)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			if (candidate == null)
			{
				throw new ArgumentNullException("candidate");
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException("contentIndex");
			}
			if (!ReferenceEquals(action, candidate.Action))
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 的候选不属于当前作者源。");
			}
			List<TabletopCardId> removals = new List<TabletopCardId>();
			HashSet<TabletopCardId> removalSet = new HashSet<TabletopCardId>();
			List<CardCreationSpec> creations = new List<CardCreationSpec>();
			for (int i = 0; i < action.ResultIntents.Count; i++)
			{
				AddIntent(action, candidate.Bindings, action.ResultIntents[i], contentIndex, removals, removalSet, creations);
			}
			if (action.ResultBranches.Count > 0)
			{
				ActionResultBranchDefinition branch = FindBranch(action, resultBranchKey);
				for (int j = 0; j < branch.ResultIntents.Count; j++)
				{
					AddIntent(action, candidate.Bindings, branch.ResultIntents[j], contentIndex, removals, removalSet, creations);
				}
			}
			return new ActionResultPlan(removals, creations);
		}

		/// <summary>
		/// 在发布恢复后的活动行动前，确认冻结结果仍完整引用当前牌桌和内容索引。
		/// </summary>
		internal static void ValidateRestoredPlan(ActionInstance action, Gameplay.Tabletop.Tabletop tabletop)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			if (tabletop == null)
			{
				throw new ArgumentNullException("tabletop");
			}

			TabletopCards cards = tabletop.Cards;
			ActionResultPlan plan = action.ResultPlan;
			for (int removalIndex = 0; removalIndex < plan.RemovalCardIds.Count; removalIndex++)
			{
				TabletopCardId cardId = plan.RemovalCardIds[removalIndex];
				if (!cards.TryGetCard(cardId, out _))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的结果引用了不存在的牌桌卡牌 {cardId}。");
				}
			}
			for (int creationIndex = 0; creationIndex < plan.Creations.Count; creationIndex++)
			{
				CardCreationSpec creation = plan.Creations[creationIndex];
				if (!tabletop.ContentIndex.TryGet(creation.ContentId, out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的产物内容 {creation.ContentId} 缺失或不是卡牌定义。");
				}
				if (!cards.TryGetCard(creation.AnchorCardId, out _))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的产物位置引用了不存在的牌桌卡牌 {creation.AnchorCardId}。");
				}
			}
			cards.EnsureCanCreateCards(plan.TotalCreationCount);
		}

		internal static void Commit(ActionInstance action, Gameplay.Tabletop.Tabletop tabletop)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			if (tabletop == null)
			{
				throw new ArgumentNullException("tabletop");
			}
			if (action.State != ActionInstanceState.Completed)
			{
				throw new InvalidOperationException($"行动 {action.ActionId} 尚未完成，不能提交结果。");
			}
			TabletopCards cards = tabletop.Cards;
			ActionResultPlan plan = action.ResultPlan;
			List<CardCreationCommit> creations = new List<CardCreationCommit>(plan.Creations.Count);
			List<ContentId> creationContentIds = new List<ContentId>(plan.TotalCreationCount);
			List<Vector2> creationPositions = new List<Vector2>(plan.TotalCreationCount);
			TabletopCard tabletopCard;
			for (int i = 0; i < plan.RemovalCardIds.Count; i++)
			{
				TabletopCardId cardId = plan.RemovalCardIds[i];
				if (!cards.TryGetCard(cardId, out tabletopCard))
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的结果引用了不存在的牌桌卡牌 {cardId}。");
				}
			}
			for (int j = 0; j < plan.Creations.Count; j++)
			{
				CardCreationSpec creation = plan.Creations[j];
				if (!cards.TryGetCard(creation.AnchorCardId, out tabletopCard))
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的产物位置引用了不存在的牌桌卡牌 {creation.AnchorCardId}。");
				}
				TabletopCardStack anchorStack = cards.GetStackContaining(creation.AnchorCardId);
				creations.Add(new CardCreationCommit(creation.ContentId, creation.Count, anchorStack.Position));
				for (int cardIndex = 0; cardIndex < creation.Count; cardIndex++)
				{
					creationContentIds.Add(creation.ContentId);
					creationPositions.Add(anchorStack.Position);
				}
			}
			tabletop.RequireCardChangesCanBeCommitted(
				plan.RemovalCardIds,
				creationContentIds,
				creationPositions);
			for (int k = 0; k < plan.RemovalCardIds.Count; k++)
			{
				tabletop.RemoveCard(plan.RemovalCardIds[k]);
			}
			for (int l = 0; l < creations.Count; l++)
			{
				CardCreationCommit creation2 = creations[l];
				for (int cardIndex = 0; cardIndex < creation2.Count; cardIndex++)
				{
					tabletop.CreateCard(creation2.ContentId, creation2.Position);
				}
			}
		}

		private static void AddIntent(ActionDefinition action, IReadOnlyList<ActionSlotBinding> bindings, ActionResultIntent intent, ContentIndex contentIndex, List<TabletopCardId> removals, HashSet<TabletopCardId> removalSet, List<CardCreationSpec> creations)
		{
			if (!(intent is RemoveCardsResultIntent removeIntent))
			{
				if (!(intent is CreateCardsResultIntent { ContentId: var contentId } createIntent))
				{
					if (intent == null)
					{
						throw new InvalidOperationException($"行动 {action.ContentId} 包含空结果意图。");
					}
					throw new InvalidOperationException($"行动 {action.ContentId} 的结果意图类型 {intent.GetType().FullName} 没有注册牌桌结算入口。");
				}
				if (!contentId.IsValid || !contentIndex.TryGet(createIntent.ContentId, out CardDefinition _))
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的产物内容 {createIntent.ContentId} 缺失或不是卡牌定义。");
				}
				if (createIntent.Count <= 0)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的产物生成数量必须大于 0。");
				}
				string anchorSlotKey = ResolveResultSlotKey(action, createIntent.AnchorSlotKey, "生成位置");
				ActionSlotBinding anchorBinding = FindBinding(action.ContentId, bindings, anchorSlotKey);
				if (anchorBinding.CardIds.Count == 0)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的产物位置来源槽位 {anchorSlotKey} 没有绑定牌桌卡牌。");
				}
				creations.Add(new CardCreationSpec(createIntent.ContentId, createIntent.Count, anchorBinding.CardIds[0]));
				return;
			}
			string removalSlotKey = ResolveResultSlotKey(action, removeIntent.SlotKey, "移除结果");
			ActionSlotBinding removalBinding = FindBinding(action.ContentId, bindings, removalSlotKey);
			for (int i = 0; i < removalBinding.CardIds.Count; i++)
			{
				TabletopCardId cardId = removalBinding.CardIds[i];
				if (!removalSet.Add(cardId))
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的结果重复移除牌桌卡牌 {cardId}。");
				}
				removals.Add(cardId);
			}
		}

		private static ActionResultBranchDefinition FindBranch(ActionDefinition action, string branchKey)
		{
			if (string.IsNullOrWhiteSpace(branchKey))
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 缺少已选随机结果分支键。");
			}
			for (int i = 0; i < action.ResultBranches.Count; i++)
			{
				ActionResultBranchDefinition branch = action.ResultBranches[i];
				if (branch != null && string.Equals(branch.Key, branchKey, StringComparison.Ordinal))
				{
					return branch;
				}
			}
			throw new InvalidOperationException($"行动 {action.ContentId} 记录了不存在的随机结果分支 {branchKey}。");
		}

		private static string ResolveResultSlotKey(ActionDefinition action, string explicitSlotKey, string purpose)
		{
			if (ActionLocalKeyUtility.IsValidKey(explicitSlotKey))
			{
				return explicitSlotKey;
			}
			if (action.ParticipationSlots.Count == 1)
			{
				string onlySlotKey = action.ParticipationSlots[0]?.Key ?? string.Empty;
				if (ActionLocalKeyUtility.IsValidKey(onlySlotKey))
				{
					return onlySlotKey;
				}
			}
			throw new InvalidOperationException($"行动 {action.ContentId} 的{purpose}没有明确参与槽位；只有单槽位行动才能自动推导。");
		}

		private static ActionSlotBinding FindBinding(ContentId actionId, IReadOnlyList<ActionSlotBinding> bindings, string slotKey)
		{
			for (int i = 0; i < bindings.Count; i++)
			{
				ActionSlotBinding binding = bindings[i];
				if (binding.Slot.Key == slotKey)
				{
					return binding;
				}
			}
			throw new InvalidOperationException($"行动 {actionId} 的结果引用了不存在的参与槽位 {slotKey}。");
		}
	}

	/// <summary>
	/// 行动开始时冻结的牌桌结果计划，避免完成时重新读取可变作者资产。
	/// </summary>
	internal sealed class ActionResultPlan
	{
		private readonly TabletopCardId[] m_removalCardIds;

		private readonly CardCreationSpec[] m_creations;

		internal IReadOnlyList<TabletopCardId> RemovalCardIds => m_removalCardIds;

		internal IReadOnlyList<CardCreationSpec> Creations => m_creations;

		internal int TotalCreationCount { get; }

		internal ActionResultPlan(IReadOnlyList<TabletopCardId> removalCardIds, IReadOnlyList<CardCreationSpec> creations)
		{
			m_removalCardIds = new List<TabletopCardId>(removalCardIds ?? throw new ArgumentNullException("removalCardIds")).ToArray();
			m_creations = new List<CardCreationSpec>(creations ?? throw new ArgumentNullException("creations")).ToArray();
			int totalCreationCount = 0;
			for (int i = 0; i < m_creations.Length; i++)
			{
				totalCreationCount = checked(totalCreationCount + m_creations[i].Count);
			}
			TotalCreationCount = totalCreationCount;
		}
	}

	/// <summary>
	/// 结果计划中的卡牌生成事实，使用内容 ID 和局内锚点定位。
	/// </summary>
	internal readonly struct CardCreationSpec
	{
		internal ContentId ContentId { get; }

		internal int Count { get; }

		internal TabletopCardId AnchorCardId { get; }

		internal CardCreationSpec(ContentId contentId, int count, TabletopCardId anchorCardId)
		{
			ContentId = contentId;
			Count = count;
			AnchorCardId = anchorCardId;
		}
	}
}
