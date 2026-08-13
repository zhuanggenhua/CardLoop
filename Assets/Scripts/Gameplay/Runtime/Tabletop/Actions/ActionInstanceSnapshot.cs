using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 行动实例的可序列化事实快照，不保存 Unity 对象、资源句柄或可变作者资产。
	/// </summary>
	[Serializable]
	public sealed class ActionInstanceSnapshot
	{
		[SerializeField]
		private ContentId m_actionId;

		[SerializeField]
		private int m_turnCost;

		[SerializeField]
		private float m_progressedTurns;

		[SerializeField]
		private ActionInstanceState m_state;

		[SerializeField]
		private string m_resultBranchKey;

		[SerializeField]
		private ActionInstanceBindingSnapshot[] m_bindings;

		[SerializeField]
		private ActionResultPlanSnapshot m_resultPlan;

		public ContentId ActionId => m_actionId;

		public int TurnCost => m_turnCost;

		public float ProgressedTurns => m_progressedTurns;

		public ActionInstanceState State => m_state;

		public string ResultBranchKey => m_resultBranchKey ?? string.Empty;

		public IReadOnlyList<ActionInstanceBindingSnapshot> Bindings => m_bindings;

		/// <summary>行动开始时冻结的结果事实，恢复时不会重新读取已经变更的行动资产。</summary>
		public ActionResultPlanSnapshot ResultPlan => m_resultPlan;

		internal ActionInstanceSnapshot(
			ContentId actionId,
			int turnCost,
			float progressedTurns,
			ActionInstanceState state,
			string resultBranchKey,
			IReadOnlyList<ActionInstanceBindingSnapshot> bindings,
			ActionResultPlan resultPlan)
		{
			if (!actionId.IsValid)
			{
				throw new ArgumentException("行动实例快照缺少有效行动内容 ID。", nameof(actionId));
			}
			if (bindings == null)
			{
				throw new ArgumentNullException(nameof(bindings));
			}

			m_actionId = actionId;
			m_turnCost = turnCost;
			m_progressedTurns = progressedTurns;
			m_state = state;
			m_resultBranchKey = resultBranchKey ?? string.Empty;
			m_bindings = new ActionInstanceBindingSnapshot[bindings.Count];
			for (int i = 0; i < bindings.Count; i++)
			{
				m_bindings[i] = bindings[i] ?? throw new ArgumentException(
					$"行动实例快照 {actionId} 的第 {i} 个槽位绑定为空。",
					nameof(bindings));
			}
			m_resultPlan = new ActionResultPlanSnapshot(resultPlan);
		}

		internal ActionResultPlan RestoreResultPlan()
		{
			if (m_resultPlan == null)
			{
				throw new InvalidOperationException($"行动实例快照 {ActionId} 缺少冻结结果计划。");
			}
			return m_resultPlan.CreateRuntimePlan();
		}
	}

	/// <summary>
	/// 行动快照中的槽位与局内卡牌 ID 绑定。
	/// </summary>
	[Serializable]
	public sealed class ActionInstanceBindingSnapshot
	{
		[SerializeField]
		private string m_slotKey;

		[SerializeField]
		private ulong[] m_cardIds;

		public string SlotKey => m_slotKey ?? string.Empty;

		public IReadOnlyList<TabletopCardId> CardIds
		{
			get
			{
				if (m_cardIds == null)
				{
					return Array.Empty<TabletopCardId>();
				}
				TabletopCardId[] cardIds = new TabletopCardId[m_cardIds.Length];
				for (int i = 0; i < m_cardIds.Length; i++)
				{
					cardIds[i] = new TabletopCardId(m_cardIds[i]);
				}
				return cardIds;
			}
		}

		internal ActionInstanceBindingSnapshot(string slotKey, IReadOnlyList<TabletopCardId> cardIds)
		{
			if (!ActionLocalKeyUtility.IsValidKey(slotKey))
			{
				throw new ArgumentException("行动实例快照槽位键无效：" + slotKey + "。", nameof(slotKey));
			}
			if (cardIds == null)
			{
				throw new ArgumentNullException(nameof(cardIds));
			}

			m_slotKey = slotKey;
			m_cardIds = new ulong[cardIds.Count];
			for (int i = 0; i < cardIds.Count; i++)
			{
				if (!cardIds[i].IsValid)
				{
					throw new ArgumentException(
						$"行动实例快照槽位 {slotKey} 包含无效局内卡牌 ID。",
						nameof(cardIds));
				}
				m_cardIds[i] = cardIds[i].Value;
			}
		}

		internal TabletopCardId[] CreateCardIds()
		{
			if (!ActionLocalKeyUtility.IsValidKey(SlotKey))
			{
				throw new InvalidOperationException("行动实例快照槽位键无效：" + SlotKey + "。");
			}
			if (m_cardIds == null)
			{
				throw new InvalidOperationException($"行动实例快照槽位 {SlotKey} 缺少局内卡牌集合。");
			}

			TabletopCardId[] cardIds = new TabletopCardId[m_cardIds.Length];
			for (int i = 0; i < m_cardIds.Length; i++)
			{
				cardIds[i] = new TabletopCardId(m_cardIds[i]);
				if (!cardIds[i].IsValid)
				{
					throw new InvalidOperationException($"行动实例快照槽位 {SlotKey} 包含无效局内卡牌 ID。");
				}
			}
			return cardIds;
		}
	}

	/// <summary>
	/// 行动开始时冻结的结果计划快照；它只保存牌桌状态提交所需的事实。
	/// </summary>
	[Serializable]
	public sealed class ActionResultPlanSnapshot
	{
		[SerializeField]
		private ulong[] m_removalCardIds;

		[SerializeField]
		private ActionCardCreationSnapshot[] m_creations;

		public IReadOnlyList<ActionCardCreationSnapshot> Creations => m_creations;

		internal ActionResultPlanSnapshot(ActionResultPlan resultPlan)
		{
			if (resultPlan == null)
			{
				throw new ArgumentNullException(nameof(resultPlan));
			}

			m_removalCardIds = new ulong[resultPlan.RemovalCardIds.Count];
			for (int i = 0; i < resultPlan.RemovalCardIds.Count; i++)
			{
				m_removalCardIds[i] = resultPlan.RemovalCardIds[i].Value;
			}
			m_creations = new ActionCardCreationSnapshot[resultPlan.Creations.Count];
			for (int i = 0; i < resultPlan.Creations.Count; i++)
			{
				m_creations[i] = new ActionCardCreationSnapshot(resultPlan.Creations[i]);
			}
		}

		internal ActionResultPlan CreateRuntimePlan()
		{
			if (m_removalCardIds == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少移除卡牌集合。");
			}
			if (m_creations == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少生成卡牌集合。");
			}

			List<TabletopCardId> removalCardIds = new List<TabletopCardId>(m_removalCardIds.Length);
			HashSet<TabletopCardId> seenRemovalCardIds = new HashSet<TabletopCardId>();
			for (int i = 0; i < m_removalCardIds.Length; i++)
			{
				TabletopCardId cardId = new TabletopCardId(m_removalCardIds[i]);
				if (!cardId.IsValid)
				{
					throw new InvalidOperationException("行动结果计划快照包含无效移除卡牌 ID。");
				}
				if (!seenRemovalCardIds.Add(cardId))
				{
					throw new InvalidOperationException($"行动结果计划快照重复移除牌桌卡牌 {cardId}。");
				}
				removalCardIds.Add(cardId);
			}

			List<CardCreationSpec> creations = new List<CardCreationSpec>(m_creations.Length);
			for (int i = 0; i < m_creations.Length; i++)
			{
				ActionCardCreationSnapshot creation = m_creations[i];
				if (creation == null)
				{
					throw new InvalidOperationException($"行动结果计划快照的第 {i} 个生成卡牌事实为空。");
				}
				creations.Add(creation.CreateRuntimeSpec());
			}
			return new ActionResultPlan(removalCardIds, creations);
		}
	}

	/// <summary>
	/// 结果计划中一项卡牌生成事实的可序列化表示。
	/// </summary>
	[Serializable]
	public sealed class ActionCardCreationSnapshot
	{
		[SerializeField]
		private ContentId m_contentId;

		[SerializeField]
		private int m_count;

		[SerializeField]
		private ulong m_anchorCardId;

		public ContentId ContentId => m_contentId;

		public int Count => m_count;

		public TabletopCardId AnchorCardId => new TabletopCardId(m_anchorCardId);

		internal ActionCardCreationSnapshot(CardCreationSpec creation)
		{
			m_contentId = creation.ContentId;
			m_count = creation.Count;
			m_anchorCardId = creation.AnchorCardId.Value;
		}

		internal CardCreationSpec CreateRuntimeSpec()
		{
			if (!ContentId.IsValid)
			{
				throw new InvalidOperationException("行动结果计划快照包含无效产物内容 ID。");
			}
			if (Count <= 0)
			{
				throw new InvalidOperationException($"行动结果计划快照的产物数量必须大于 0，当前值为 {Count}。");
			}
			if (!AnchorCardId.IsValid)
			{
				throw new InvalidOperationException("行动结果计划快照包含无效产物位置卡牌 ID。");
			}
			return new CardCreationSpec(ContentId, Count, AnchorCardId);
		}
	}
}
