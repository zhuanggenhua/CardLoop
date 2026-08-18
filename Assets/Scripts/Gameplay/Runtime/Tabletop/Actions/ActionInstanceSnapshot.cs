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
		private ulong[] m_useCardIds;

		[SerializeField]
		private ActionCardCreationSnapshot[] m_creations;

		[SerializeField]
		private ActionResearchDiscoverySnapshot[] m_researchDiscoveries;

		[SerializeField]
		private ActionPackPurchaseSnapshot[] m_packPurchases;

		[SerializeField]
		private ActionChestCurrencyChangeSnapshot[] m_chestCurrencyChanges;

		[SerializeField]
		private ActionEquipCardSnapshot[] m_equipCards;

		[SerializeField]
		private ActionUnequipCardSnapshot[] m_unequipCards;

		[SerializeField]
		private ContentId[] m_soldContentIds;

		[SerializeField]
		private ContentId[] m_exploredContentIds;

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
			m_useCardIds = new ulong[resultPlan.UseCardIds.Count];
			for (int i = 0; i < resultPlan.UseCardIds.Count; i++)
			{
				m_useCardIds[i] = resultPlan.UseCardIds[i].Value;
			}
			m_creations = new ActionCardCreationSnapshot[resultPlan.Creations.Count];
			for (int i = 0; i < resultPlan.Creations.Count; i++)
			{
				m_creations[i] = new ActionCardCreationSnapshot(resultPlan.Creations[i]);
			}
			m_researchDiscoveries = new ActionResearchDiscoverySnapshot[resultPlan.ResearchDiscoveries.Count];
			for (int i = 0; i < resultPlan.ResearchDiscoveries.Count; i++)
			{
				m_researchDiscoveries[i] = new ActionResearchDiscoverySnapshot(resultPlan.ResearchDiscoveries[i]);
			}
			m_packPurchases = new ActionPackPurchaseSnapshot[resultPlan.PackPurchases.Count];
			for (int i = 0; i < resultPlan.PackPurchases.Count; i++)
			{
				m_packPurchases[i] = new ActionPackPurchaseSnapshot(resultPlan.PackPurchases[i]);
			}
			m_chestCurrencyChanges = new ActionChestCurrencyChangeSnapshot[resultPlan.ChestCurrencyChanges.Count];
			for (int i = 0; i < resultPlan.ChestCurrencyChanges.Count; i++)
			{
				m_chestCurrencyChanges[i] = new ActionChestCurrencyChangeSnapshot(resultPlan.ChestCurrencyChanges[i]);
			}
			m_equipCards = new ActionEquipCardSnapshot[resultPlan.EquipCards.Count];
			for (int i = 0; i < resultPlan.EquipCards.Count; i++)
			{
				m_equipCards[i] = new ActionEquipCardSnapshot(resultPlan.EquipCards[i]);
			}
			m_unequipCards = new ActionUnequipCardSnapshot[resultPlan.UnequipCards.Count];
			for (int i = 0; i < resultPlan.UnequipCards.Count; i++)
			{
				m_unequipCards[i] = new ActionUnequipCardSnapshot(resultPlan.UnequipCards[i]);
			}
			m_soldContentIds = new ContentId[resultPlan.SoldContentIds.Count];
			for (int i = 0; i < resultPlan.SoldContentIds.Count; i++)
			{
				m_soldContentIds[i] = resultPlan.SoldContentIds[i];
			}
			m_exploredContentIds = new ContentId[resultPlan.ExploredContentIds.Count];
			for (int i = 0; i < resultPlan.ExploredContentIds.Count; i++)
			{
				m_exploredContentIds[i] = resultPlan.ExploredContentIds[i];
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
			if (m_useCardIds == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少使用卡牌集合。");
			}
			if (m_researchDiscoveries == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少研究候选集合。");
			}
			if (m_packPurchases == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少卡包购买集合。");
			}
			if (m_chestCurrencyChanges == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少箱子存币变化集合。");
			}
			if (m_soldContentIds == null)
			{
				throw new InvalidOperationException("行动结果计划快照缺少出售内容集合。");
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
			List<TabletopCardId> useCardIds = new List<TabletopCardId>(m_useCardIds.Length);
			HashSet<TabletopCardId> seenUseCardIds = new HashSet<TabletopCardId>();
			for (int i = 0; i < m_useCardIds.Length; i++)
			{
				TabletopCardId cardId = new TabletopCardId(m_useCardIds[i]);
				if (!cardId.IsValid || seenRemovalCardIds.Contains(cardId) || !seenUseCardIds.Add(cardId))
				{
					throw new InvalidOperationException(
						$"行动结果计划快照包含无效、重复或同时移除的使用卡牌 ID：{cardId}。");
				}
				useCardIds.Add(cardId);
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
			List<ResearchDiscoverySpec> researchDiscoveries = new List<ResearchDiscoverySpec>(m_researchDiscoveries.Length);
			for (int i = 0; i < m_researchDiscoveries.Length; i++)
			{
				ActionResearchDiscoverySnapshot research = m_researchDiscoveries[i];
				if (research == null)
				{
					throw new InvalidOperationException($"行动结果计划快照的第 {i} 个研究候选池为空。");
				}
				researchDiscoveries.Add(research.CreateRuntimeSpec());
			}
			List<PackPurchaseSpec> packPurchases = new List<PackPurchaseSpec>(m_packPurchases.Length);
			for (int i = 0; i < m_packPurchases.Length; i++)
			{
				ActionPackPurchaseSnapshot purchase = m_packPurchases[i] ??
					throw new InvalidOperationException($"行动结果计划快照的第 {i} 个卡包购买事实为空。");
				packPurchases.Add(purchase.CreateRuntimeSpec());
			}
			List<ChestCurrencyChangeSpec> chestCurrencyChanges = new List<ChestCurrencyChangeSpec>(m_chestCurrencyChanges.Length);
			for (int i = 0; i < m_chestCurrencyChanges.Length; i++)
			{
				ActionChestCurrencyChangeSnapshot change = m_chestCurrencyChanges[i] ??
					throw new InvalidOperationException($"行动结果计划快照的第 {i} 个箱子存币变化为空。");
				chestCurrencyChanges.Add(change.CreateRuntimeSpec());
			}
			ActionEquipCardSnapshot[] equipSnapshots = m_equipCards ?? Array.Empty<ActionEquipCardSnapshot>();
			List<EquipCardSpec> equipCards = new List<EquipCardSpec>(equipSnapshots.Length);
			for (int i = 0; i < equipSnapshots.Length; i++)
			{
				ActionEquipCardSnapshot equip = equipSnapshots[i] ??
					throw new InvalidOperationException($"行动结果计划快照的第 {i} 个装备事实为空。");
				equipCards.Add(equip.CreateRuntimeSpec());
			}
			ActionUnequipCardSnapshot[] unequipSnapshots = m_unequipCards ?? Array.Empty<ActionUnequipCardSnapshot>();
			List<UnequipCardSpec> unequipCards = new List<UnequipCardSpec>(unequipSnapshots.Length);
			for (int i = 0; i < unequipSnapshots.Length; i++)
			{
				ActionUnequipCardSnapshot unequip = unequipSnapshots[i] ??
					throw new InvalidOperationException($"行动结果计划快照的第 {i} 个卸装事实为空。");
				unequipCards.Add(unequip.CreateRuntimeSpec());
			}
			List<ContentId> soldContentIds = new List<ContentId>(m_soldContentIds.Length);
			for (int i = 0; i < m_soldContentIds.Length; i++)
			{
				if (!m_soldContentIds[i].IsValid)
				{
					throw new InvalidOperationException($"行动结果计划快照的第 {i + 1} 个出售内容 ID 无效。");
				}
				soldContentIds.Add(m_soldContentIds[i]);
			}
			ContentId[] exploredContentIdSnapshots = m_exploredContentIds ?? Array.Empty<ContentId>();
			List<ContentId> exploredContentIds = new List<ContentId>(exploredContentIdSnapshots.Length);
			for (int i = 0; i < exploredContentIdSnapshots.Length; i++)
			{
				if (!exploredContentIdSnapshots[i].IsValid)
				{
					throw new InvalidOperationException($"行动结果计划快照的第 {i + 1} 个探索内容 ID 无效。");
				}
				exploredContentIds.Add(exploredContentIdSnapshots[i]);
			}
			return new ActionResultPlan(
				removalCardIds,
				useCardIds,
				creations,
				researchDiscoveries,
				packPurchases,
				chestCurrencyChanges,
				equipCards,
				unequipCards,
				soldContentIds,
				exploredContentIds);
		}
	}

	/// <summary>行动结果计划中的装备事实快照。</summary>
	[Serializable]
	public sealed class ActionEquipCardSnapshot
	{
		[SerializeField]
		private ulong m_equipmentCardId;

		[SerializeField]
		private ulong m_characterCardId;

		[SerializeField]
		private ContentId m_slotId;

		[SerializeField]
		private int m_gameplayEffectId;

		[SerializeField]
		private TabletopCardSnapshot m_equipmentSnapshot;

		[SerializeField]
		private Vector2 m_returnPosition;

		[SerializeField]
		private EquippedCardSnapshot m_replacedEquipmentSnapshot;

		internal ActionEquipCardSnapshot(EquipCardSpec equip)
		{
			m_equipmentCardId = equip.EquipmentCardId.Value;
			m_characterCardId = equip.CharacterCardId.Value;
			m_slotId = equip.SlotId;
			m_gameplayEffectId = equip.GameplayEffectId;
			m_equipmentSnapshot = equip.EquipmentSnapshot;
			m_returnPosition = equip.ReturnPosition;
			m_replacedEquipmentSnapshot = equip.ReplacedEquipmentSnapshot;
		}

		internal EquipCardSpec CreateRuntimeSpec()
		{
			TabletopCardId equipmentCardId = new TabletopCardId(m_equipmentCardId);
			TabletopCardId characterCardId = new TabletopCardId(m_characterCardId);
			if (!equipmentCardId.IsValid ||
				!characterCardId.IsValid ||
				!m_slotId.IsValid ||
				m_gameplayEffectId <= 0 ||
				m_equipmentSnapshot == null)
			{
				throw new InvalidOperationException("行动结果计划快照包含无效装备事实。");
			}
			return new EquipCardSpec(
				equipmentCardId,
				characterCardId,
				m_slotId,
				m_gameplayEffectId,
				m_equipmentSnapshot,
				m_returnPosition,
				m_replacedEquipmentSnapshot);
		}
	}

	/// <summary>行动结果计划中的卸装事实快照。</summary>
	[Serializable]
	public sealed class ActionUnequipCardSnapshot
	{
		[SerializeField]
		private ulong m_characterCardId;

		[SerializeField]
		private ContentId m_slotId;

		[SerializeField]
		private EquippedCardSnapshot m_equipmentSnapshot;

		[SerializeField]
		private Vector2 m_returnPosition;

		internal ActionUnequipCardSnapshot(UnequipCardSpec unequip)
		{
			m_characterCardId = unequip.CharacterCardId.Value;
			m_slotId = unequip.SlotId;
			m_equipmentSnapshot = unequip.EquipmentSnapshot;
			m_returnPosition = unequip.ReturnPosition;
		}

		internal UnequipCardSpec CreateRuntimeSpec()
		{
			TabletopCardId characterCardId = new TabletopCardId(m_characterCardId);
			if (!characterCardId.IsValid ||
				!m_slotId.IsValid ||
				m_equipmentSnapshot == null)
			{
				throw new InvalidOperationException("行动结果计划快照包含无效卸装事实。");
			}
			return new UnequipCardSpec(
				characterCardId,
				m_slotId,
				m_equipmentSnapshot,
				m_returnPosition);
		}
	}

	/// <summary>行动结果计划中的卡包付款事实快照。</summary>
	[Serializable]
	public sealed class ActionPackPurchaseSnapshot
	{
		[SerializeField]
		private ulong m_vendorCardId;

		[SerializeField]
		private int m_expectedPaidAmount;

		[SerializeField]
		private int m_paymentAmount;

		[SerializeField]
		private bool m_completesPurchase;

		[SerializeField]
		private ContentId m_packId;

		internal ActionPackPurchaseSnapshot(PackPurchaseSpec purchase)
		{
			m_vendorCardId = purchase.VendorCardId.Value;
			m_expectedPaidAmount = purchase.ExpectedPaidAmount;
			m_paymentAmount = purchase.PaymentAmount;
			m_completesPurchase = purchase.CompletesPurchase;
			m_packId = purchase.PackId;
		}

		internal PackPurchaseSpec CreateRuntimeSpec()
		{
			TabletopCardId vendorCardId = new TabletopCardId(m_vendorCardId);
			if (!vendorCardId.IsValid || m_expectedPaidAmount < 0 || m_paymentAmount <= 0 || !m_packId.IsValid)
			{
				throw new InvalidOperationException("行动结果计划快照包含无效卡包购买事实。");
			}
			return new PackPurchaseSpec(
				vendorCardId,
				m_expectedPaidAmount,
				m_paymentAmount,
				m_completesPurchase,
				m_packId);
		}
	}

	/// <summary>行动结果计划中的箱子存币变化快照。</summary>
	[Serializable]
	public sealed class ActionChestCurrencyChangeSnapshot
	{
		[SerializeField]
		private ulong m_chestCardId;

		[SerializeField]
		private int m_expectedStoredCurrencyCount;

		[SerializeField]
		private int m_delta;

		internal ActionChestCurrencyChangeSnapshot(ChestCurrencyChangeSpec change)
		{
			m_chestCardId = change.ChestCardId.Value;
			m_expectedStoredCurrencyCount = change.ExpectedStoredCurrencyCount;
			m_delta = change.Delta;
		}

		internal ChestCurrencyChangeSpec CreateRuntimeSpec()
		{
			TabletopCardId chestCardId = new TabletopCardId(m_chestCardId);
			if (!chestCardId.IsValid || m_expectedStoredCurrencyCount < 0 || m_delta == 0)
			{
				throw new InvalidOperationException("行动结果计划快照包含无效箱子存币变化事实。");
			}
			return new ChestCurrencyChangeSpec(chestCardId, m_expectedStoredCurrencyCount, m_delta);
		}
	}

	/// <summary>行动开始时冻结的研究候选池快照。</summary>
	[Serializable]
	public sealed class ActionResearchDiscoverySnapshot
	{
		[SerializeField]
		private ContentId[] m_actionIds;

		[SerializeField]
		private ContentId[] m_recipeCardIds;

		[SerializeField]
		private ulong m_anchorCardId;

		internal ActionResearchDiscoverySnapshot(ResearchDiscoverySpec research)
		{
			if (research == null)
			{
				throw new ArgumentNullException(nameof(research));
			}
			m_actionIds = new ContentId[research.Entries.Count];
			m_recipeCardIds = new ContentId[research.Entries.Count];
			for (int i = 0; i < research.Entries.Count; i++)
			{
				m_actionIds[i] = research.Entries[i].ActionId;
				m_recipeCardIds[i] = research.Entries[i].RecipeCardId;
			}
			m_anchorCardId = research.AnchorCardId.Value;
		}

		internal ResearchDiscoverySpec CreateRuntimeSpec()
		{
			if (m_actionIds == null || m_recipeCardIds == null ||
				m_actionIds.Length == 0 || m_actionIds.Length != m_recipeCardIds.Length)
			{
				throw new InvalidOperationException("行动研究候选快照缺少一一对应的行动和配方卡。");
			}
			TabletopCardId anchorCardId = new TabletopCardId(m_anchorCardId);
			if (!anchorCardId.IsValid)
			{
				throw new InvalidOperationException("行动研究候选快照缺少有效的结果位置卡牌。");
			}
			List<ResearchDiscoveryEntrySpec> entries = new List<ResearchDiscoveryEntrySpec>(m_actionIds.Length);
			HashSet<ContentId> seenActionIds = new HashSet<ContentId>();
			for (int i = 0; i < m_actionIds.Length; i++)
			{
				if (!m_actionIds[i].IsValid || !m_recipeCardIds[i].IsValid || !seenActionIds.Add(m_actionIds[i]))
				{
					throw new InvalidOperationException(
						$"行动研究候选快照的第 {i + 1} 项内容 ID 无效或重复。");
				}
				entries.Add(new ResearchDiscoveryEntrySpec(m_actionIds[i], m_recipeCardIds[i]));
			}
			return new ResearchDiscoverySpec(entries, anchorCardId);
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

		[SerializeField]
		private bool m_createAsSingleStack;

		public ContentId ContentId => m_contentId;

		public int Count => m_count;

		public TabletopCardId AnchorCardId => new TabletopCardId(m_anchorCardId);

		public bool CreateAsSingleStack => m_createAsSingleStack;

		internal ActionCardCreationSnapshot(CardCreationSpec creation)
		{
			m_contentId = creation.ContentId;
			m_count = creation.Count;
			m_anchorCardId = creation.AnchorCardId.Value;
			m_createAsSingleStack = creation.CreateAsSingleStack;
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
			return new CardCreationSpec(ContentId, Count, AnchorCardId, CreateAsSingleStack);
		}
	}
}
