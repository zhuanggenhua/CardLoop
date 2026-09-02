using System;
using System.Collections.Generic;
using GAS.General;
using GAS.Runtime;
using Gameplay.Content;
using Gameplay.Tabletop;
using Gameplay.Actions;
using UnityEngine;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>行动结算提交后交给所属剧本的事实集合。</summary>
	internal sealed class ActionSettlementResult
	{
		private readonly ContentId[] m_discoveredContentIds;
		private readonly ContentId[] m_exploredContentIds;
		private readonly ContentId[] m_createdCardIds;
		private readonly ContentId[] m_purchasedPackIds;
		private readonly ContentId[] m_soldContentIds;
		private readonly ContentId[] m_equippedCardIds;
		private readonly TabletopPresentationCue[] m_presentationCues;

		internal IReadOnlyList<ContentId> DiscoveredContentIds => m_discoveredContentIds;
		internal IReadOnlyList<ContentId> ExploredContentIds => m_exploredContentIds;
		internal IReadOnlyList<ContentId> CreatedCardIds => m_createdCardIds;
		internal IReadOnlyList<ContentId> PurchasedPackIds => m_purchasedPackIds;
		internal IReadOnlyList<ContentId> SoldContentIds => m_soldContentIds;
		internal IReadOnlyList<ContentId> EquippedCardIds => m_equippedCardIds;
		internal IReadOnlyList<TabletopPresentationCue> PresentationCues => m_presentationCues;

		internal ActionSettlementResult(
			IReadOnlyList<ContentId> discoveredContentIds,
			IReadOnlyList<ContentId> exploredContentIds,
			IReadOnlyList<ContentId> createdCardIds,
			IReadOnlyList<ContentId> purchasedPackIds,
			IReadOnlyList<ContentId> soldContentIds,
			IReadOnlyList<ContentId> equippedCardIds,
			IReadOnlyList<TabletopPresentationCue> presentationCues)
		{
			m_discoveredContentIds = new List<ContentId>(discoveredContentIds).ToArray();
			m_exploredContentIds = new List<ContentId>(exploredContentIds).ToArray();
			m_createdCardIds = new List<ContentId>(createdCardIds).ToArray();
			m_purchasedPackIds = new List<ContentId>(purchasedPackIds).ToArray();
			m_soldContentIds = new List<ContentId>(soldContentIds).ToArray();
			m_equippedCardIds = new List<ContentId>(equippedCardIds).ToArray();
			m_presentationCues = new List<TabletopPresentationCue>(presentationCues).ToArray();
		}
	}

	/// <summary>
	/// 把行动作者结果编译为不可变计划，并在完成后由牌桌原子提交。
	/// </summary>
	internal static class ActionResultSettlement
	{
		private const float StackCraftPackSpawnHeightOffset = 0.1f;

		internal static ActionResultPlan Compile(
			ActionDefinition action,
			ActionCandidate candidate,
			string resultBranchKey,
			ContentIndex contentIndex,
			TabletopCards cards,
			TabletopCardPlacementRules placementRules,
			Func<ContentId, bool> isContentDiscovered,
			ref Unity.Mathematics.Random authoritativeRandom)
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
			if (cards == null)
			{
				throw new ArgumentNullException(nameof(cards));
			}
			if (placementRules == null)
			{
				throw new ArgumentNullException(nameof(placementRules));
			}
			if (isContentDiscovered == null)
			{
				throw new ArgumentNullException(nameof(isContentDiscovered));
			}
			if (!ReferenceEquals(action, candidate.Action))
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 的候选不属于当前作者源。");
			}
			List<TabletopCardId> removals = new List<TabletopCardId>();
			HashSet<TabletopCardId> removalSet = new HashSet<TabletopCardId>();
			List<TabletopCardId> uses = new List<TabletopCardId>();
			HashSet<TabletopCardId> useSet = new HashSet<TabletopCardId>();
			List<CardCreationSpec> creations = new List<CardCreationSpec>();
			List<ResearchDiscoverySpec> researchDiscoveries = new List<ResearchDiscoverySpec>();
			List<PackPurchaseSpec> packPurchases = new List<PackPurchaseSpec>();
			List<ChestCurrencyChangeSpec> chestCurrencyChanges = new List<ChestCurrencyChangeSpec>();
			List<EquipCardSpec> equipCards = new List<EquipCardSpec>();
			List<UnequipCardSpec> unequipCards = new List<UnequipCardSpec>();
			List<ContentId> soldContentIds = new List<ContentId>();
			List<ContentId> exploredContentIds = new List<ContentId>();
			HashSet<TabletopCardId> exploredCardSet = new HashSet<TabletopCardId>();
			for (int i = 0; i < action.ResultIntents.Count; i++)
			{
				AddIntent(action, candidate.Bindings, action.ResultIntents[i], contentIndex, cards, placementRules, isContentDiscovered, ref authoritativeRandom, removals, removalSet, uses, useSet, creations, researchDiscoveries, packPurchases, chestCurrencyChanges, equipCards, unequipCards, soldContentIds, exploredContentIds, exploredCardSet);
			}
			if (action.ResultBranches.Count > 0)
			{
				ActionResultBranchDefinition branch = FindBranch(action, resultBranchKey);
				for (int j = 0; j < branch.ResultIntents.Count; j++)
				{
					AddIntent(action, candidate.Bindings, branch.ResultIntents[j], contentIndex, cards, placementRules, isContentDiscovered, ref authoritativeRandom, removals, removalSet, uses, useSet, creations, researchDiscoveries, packPurchases, chestCurrencyChanges, equipCards, unequipCards, soldContentIds, exploredContentIds, exploredCardSet);
				}
			}
			return new ActionResultPlan(removals, uses, creations, researchDiscoveries, packPurchases, chestCurrencyChanges, equipCards, unequipCards, soldContentIds, exploredContentIds);
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
			for (int useIndex = 0; useIndex < plan.UseCardIds.Count; useIndex++)
			{
				TabletopCardId cardId = plan.UseCardIds[useIndex];
				if (!cards.TryGetCard(cardId, out TabletopCard card) || card.RemainingUses <= 0)
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的使用结果引用了不存在或已耗尽的牌桌卡牌 {cardId}。");
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
			for (int researchIndex = 0; researchIndex < plan.ResearchDiscoveries.Count; researchIndex++)
			{
				ResearchDiscoverySpec research = plan.ResearchDiscoveries[researchIndex];
				if (!cards.TryGetCard(research.AnchorCardId, out _))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的研究结果位置引用了不存在的牌桌卡牌 {research.AnchorCardId}。");
				}
				for (int entryIndex = 0; entryIndex < research.Entries.Count; entryIndex++)
				{
					ResearchDiscoveryEntrySpec entry = research.Entries[entryIndex];
					if (!tabletop.ContentIndex.TryGet(entry.ActionId, out ActionDefinition _) ||
						!tabletop.ContentIndex.TryGet(entry.RecipeCardId, out CardDefinition _))
					{
						throw new InvalidOperationException(
							$"行动实例快照 {action.ActionId} 的研究候选 {entry.ActionId} 或配方卡 {entry.RecipeCardId} 已不属于当前内容集合。");
					}
				}
			}
			for (int explorationIndex = 0; explorationIndex < plan.ExploredContentIds.Count; explorationIndex++)
			{
				ContentId exploredContentId = plan.ExploredContentIds[explorationIndex];
				if (!tabletop.ContentIndex.TryGet(exploredContentId, out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的探索内容 {exploredContentId} 缺失或不是卡牌定义。");
				}
			}
			for (int purchaseIndex = 0; purchaseIndex < plan.PackPurchases.Count; purchaseIndex++)
			{
				PackPurchaseSpec purchase = plan.PackPurchases[purchaseIndex];
				RequirePackPurchaseCanCommit(action.ActionId, tabletop, purchase);
			}
			for (int chestChangeIndex = 0; chestChangeIndex < plan.ChestCurrencyChanges.Count; chestChangeIndex++)
			{
				RequireChestCurrencyChangeCanCommit(action.ActionId, tabletop, plan.ChestCurrencyChanges[chestChangeIndex]);
			}
			for (int equipIndex = 0; equipIndex < plan.EquipCards.Count; equipIndex++)
			{
				RequireEquipCardCanCommit(action.ActionId, tabletop, plan.EquipCards[equipIndex]);
			}
			for (int unequipIndex = 0; unequipIndex < plan.UnequipCards.Count; unequipIndex++)
			{
				RequireUnequipCardCanCommit(action.ActionId, tabletop, plan.UnequipCards[unequipIndex]);
			}
			for (int saleIndex = 0; saleIndex < plan.SoldContentIds.Count; saleIndex++)
			{
				if (!tabletop.ContentIndex.TryGet(plan.SoldContentIds[saleIndex], out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"行动实例快照 {action.ActionId} 的出售事实内容 {plan.SoldContentIds[saleIndex]} 缺失或不是卡牌定义。");
				}
			}
			cards.EnsureCanCreateCards(plan.TotalCreationCount);
			tabletop.RequireCardChangesCanBeCommitted(
				plan.RemovalCardIds,
				Array.Empty<TabletopCardCreationRequest>(),
				CreateEquipmentRestorations(plan));
		}

		internal static ActionSettlementResult Commit(
			ActionInstance action,
			Gameplay.Tabletop.Tabletop tabletop,
			Func<ContentId, bool> isContentDiscovered,
			ref Unity.Mathematics.Random authoritativeRandom)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			if (tabletop == null)
			{
				throw new ArgumentNullException("tabletop");
			}
			if (isContentDiscovered == null)
			{
				throw new ArgumentNullException(nameof(isContentDiscovered));
			}
			if (action.State != ActionInstanceState.Completed)
			{
				throw new InvalidOperationException($"行动 {action.ActionId} 尚未完成，不能提交结果。");
			}
			TabletopCards cards = tabletop.Cards;
			ActionResultPlan plan = action.ResultPlan;
			List<TabletopCardCreationRequest> preUseCreations = new List<TabletopCardCreationRequest>(
				plan.TotalCreationCount + plan.ResearchDiscoveries.Count);
			List<TabletopCardCreationRequest> postUseCreations = new List<TabletopCardCreationRequest>(
				plan.TotalCreationCount + plan.ResearchDiscoveries.Count);
			List<ContentId> discoveries = new List<ContentId>(plan.ResearchDiscoveries.Count);
			List<ContentId> createdCardIds = new List<ContentId>();
			List<ContentId> purchasedPackIds = new List<ContentId>(plan.PackPurchases.Count);
			List<ContentId> soldContentIds = new List<ContentId>(plan.SoldContentIds);
			List<ContentId> exploredContentIds = new List<ContentId>(plan.ExploredContentIds);
			List<ContentId> equippedCardIds = new List<ContentId>(plan.EquipCards.Count);
			List<TabletopPresentationCue> presentationCues =
				CreatePresentationCues(plan, tabletop);
			HashSet<ContentId> plannedDiscoveries = new HashSet<ContentId>();
			List<TabletopCardId> effectiveRemovals = new List<TabletopCardId>(
				plan.RemovalCardIds.Count + plan.UseCardIds.Count);
			effectiveRemovals.AddRange(plan.RemovalCardIds);
			List<TabletopCardRestorationRequest> restorations = CreateEquipmentRestorations(plan);
			TabletopCard tabletopCard;
			for (int i = 0; i < plan.RemovalCardIds.Count; i++)
			{
				TabletopCardId cardId = plan.RemovalCardIds[i];
				if (!cards.TryGetCard(cardId, out tabletopCard))
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的结果引用了不存在的牌桌卡牌 {cardId}。");
				}
			}
			for (int useIndex = 0; useIndex < plan.UseCardIds.Count; useIndex++)
			{
				TabletopCardId cardId = plan.UseCardIds[useIndex];
				if (!cards.TryGetCard(cardId, out tabletopCard) || tabletopCard.RemainingUses <= 0)
				{
					throw new InvalidOperationException(
						$"行动 {action.ActionId} 的使用结果引用了不存在或已耗尽的牌桌卡牌 {cardId}。");
				}
				if (tabletopCard.RemainingUses == 1)
				{
					effectiveRemovals.Add(cardId);
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
				Vector2 creationPosition = anchorStack.Position + creation.PositionOffset;
				AddCreationRequests(
					creation,
					creationPosition,
					anchorStack.BottomCard.Id,
					ShouldCreateBeforeUsingAnchor(plan.UseCardIds, creation.AnchorCardId),
					creation.SpawnPresentationHeightOffset,
					creation.UseDragHeightForSpawn,
					preUseCreations,
					postUseCreations);
				if (!isContentDiscovered(creation.ContentId) && plannedDiscoveries.Add(creation.ContentId))
				{
					discoveries.Add(creation.ContentId);
				}
			}
			for (int researchIndex = 0; researchIndex < plan.ResearchDiscoveries.Count; researchIndex++)
			{
				ResearchDiscoverySpec research = plan.ResearchDiscoveries[researchIndex];
				if (!cards.TryGetCard(research.AnchorCardId, out tabletopCard))
				{
					throw new InvalidOperationException(
						$"行动 {action.ActionId} 的研究结果位置引用了不存在的牌桌卡牌 {research.AnchorCardId}。");
				}
				List<ResearchDiscoveryEntrySpec> available = new List<ResearchDiscoveryEntrySpec>();
				for (int entryIndex = 0; entryIndex < research.Entries.Count; entryIndex++)
				{
					ResearchDiscoveryEntrySpec entry = research.Entries[entryIndex];
					if (!isContentDiscovered(entry.ActionId) && !plannedDiscoveries.Contains(entry.ActionId))
					{
						available.Add(entry);
					}
				}
				if (available.Count == 0)
				{
					continue;
				}
				if (authoritativeRandom.state == 0u)
				{
					throw new InvalidOperationException(
						$"行动 {action.ActionId} 包含研究随机结果，但本局牌桌尚未初始化权威随机流。");
				}
				ResearchDiscoveryEntrySpec selected = available[authoritativeRandom.NextInt(available.Count)];
				TabletopCardStack anchorStack = cards.GetStackContaining(research.AnchorCardId);
				AddCreationRequest(
					selected.RecipeCardId,
					1,
					anchorStack.Position,
					anchorStack.BottomCard.Id,
					ShouldCreateBeforeUsingAnchor(plan.UseCardIds, research.AnchorCardId),
					research.AnchorCardId,
					research.AllowAnchorStackSpawnAttach,
					research.SpawnPresentationHeightOffset,
					research.UseDragHeightForSpawn,
					preUseCreations,
					postUseCreations);
				plannedDiscoveries.Add(selected.ActionId);
				discoveries.Add(selected.ActionId);
				if (!isContentDiscovered(selected.RecipeCardId) && plannedDiscoveries.Add(selected.RecipeCardId))
				{
					discoveries.Add(selected.RecipeCardId);
				}
			}
			for (int purchaseIndex = 0; purchaseIndex < plan.PackPurchases.Count; purchaseIndex++)
			{
				RequirePackPurchaseCanCommit(action.ActionId, tabletop, plan.PackPurchases[purchaseIndex]);
			}
			for (int chestChangeIndex = 0; chestChangeIndex < plan.ChestCurrencyChanges.Count; chestChangeIndex++)
			{
				RequireChestCurrencyChangeCanCommit(action.ActionId, tabletop, plan.ChestCurrencyChanges[chestChangeIndex]);
			}
			for (int equipIndex = 0; equipIndex < plan.EquipCards.Count; equipIndex++)
			{
				RequireEquipCardCanCommit(action.ActionId, tabletop, plan.EquipCards[equipIndex]);
			}
			for (int unequipIndex = 0; unequipIndex < plan.UnequipCards.Count; unequipIndex++)
			{
				RequireUnequipCardCanCommit(action.ActionId, tabletop, plan.UnequipCards[unequipIndex]);
			}
			if (preUseCreations.Count > 0)
			{
				tabletop.RequireCardChangesCanBeCommitted(
					Array.Empty<TabletopCardId>(),
					preUseCreations,
					Array.Empty<TabletopCardRestorationRequest>(),
					requirePlacementConverged: false);
			}
			tabletop.RequireCardChangesCanBeCommitted(
				effectiveRemovals,
				postUseCreations,
				restorations,
				requirePlacementConverged: postUseCreations.Count == 0);
			CreateCardStacks(tabletop, preUseCreations, createdCardIds);
			for (int equipIndex = 0; equipIndex < plan.EquipCards.Count; equipIndex++)
			{
				EquipCardSpec equip = plan.EquipCards[equipIndex];
				if (!cards.TryGetCard(equip.EquipmentCardId, out TabletopCard equipmentCard) ||
					!tabletop.ContentIndex.TryGet(equipmentCard.ContentId, out EquipmentCardDefinition equipmentDefinition) ||
					!cards.TryGetCard(equip.CharacterCardId, out TabletopCard characterCard) ||
					characterCard is not CharacterCard character)
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的装备计划在提交前消失。");
				}
				EquippedCardSnapshot replaced = character.Equip(equipmentDefinition, equipmentCard);
				RequireSameEquippedSnapshot(equip.ReplacedEquipmentSnapshot, replaced, $"行动 {action.ActionId} 的被替换装备");
				equippedCardIds.Add(equipmentDefinition.ContentId);
			}
			for (int unequipIndex = 0; unequipIndex < plan.UnequipCards.Count; unequipIndex++)
			{
				UnequipCardSpec unequip = plan.UnequipCards[unequipIndex];
				if (!cards.TryGetCard(unequip.CharacterCardId, out TabletopCard characterCard) ||
					characterCard is not CharacterCard character)
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的卸装计划在提交前消失。");
				}
				EquippedCardSnapshot removed = character.Unequip(unequip.SlotId);
				RequireSameEquippedSnapshot(unequip.EquipmentSnapshot, removed, $"行动 {action.ActionId} 的卸下装备");
			}
			for (int purchaseIndex = 0; purchaseIndex < plan.PackPurchases.Count; purchaseIndex++)
			{
				PackPurchaseSpec purchase = plan.PackPurchases[purchaseIndex];
				if (!cards.TryGetCard(purchase.VendorCardId, out TabletopCard vendorCard) ||
					vendorCard is not PackVendorCard vendor)
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的卡包商贩在提交前消失。");
				}
				bool completed = vendor.Pay(purchase.PaymentAmount);
				if (completed != purchase.CompletesPurchase)
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的卡包成交计划与商贩当前结果不一致。");
				}
				if (completed)
				{
					vendor.CompletePurchase();
					purchasedPackIds.Add(purchase.PackId);
				}
			}
			for (int chestChangeIndex = 0; chestChangeIndex < plan.ChestCurrencyChanges.Count; chestChangeIndex++)
			{
				ChestCurrencyChangeSpec change = plan.ChestCurrencyChanges[chestChangeIndex];
				if (!cards.TryGetCard(change.ChestCardId, out TabletopCard chestCard) || chestCard is not ChestCard chest)
				{
					throw new InvalidOperationException($"行动 {action.ActionId} 的箱子存币计划在提交前消失。");
				}
				chest.ApplyCurrencyChange(change.ExpectedStoredCurrencyCount, change.Delta);
			}
			for (int useIndex = 0; useIndex < plan.UseCardIds.Count; useIndex++)
			{
				tabletop.UseCard(plan.UseCardIds[useIndex]);
			}
			for (int k = 0; k < plan.RemovalCardIds.Count; k++)
			{
				tabletop.RemoveCard(plan.RemovalCardIds[k]);
			}
			for (int restoreIndex = 0; restoreIndex < restorations.Count; restoreIndex++)
			{
				TabletopCardRestorationRequest restoration = restorations[restoreIndex];
				tabletop.RestoreCardSnapshot(restoration.Snapshot, restoration.Position);
			}
			CreateCardStacks(tabletop, postUseCreations, createdCardIds);
			return new ActionSettlementResult(
				discoveries,
				exploredContentIds,
				createdCardIds,
				purchasedPackIds,
				soldContentIds,
				equippedCardIds,
				presentationCues);
		}

		private static void AddCreationRequests(
			CardCreationSpec creation,
			Vector2 creationPosition,
			TabletopCardId anchorStackBottomCardId,
			bool createBeforeCardUse,
			float spawnPresentationHeightOffset,
			bool useDragHeightForSpawn,
			List<TabletopCardCreationRequest> preUseCreations,
			List<TabletopCardCreationRequest> postUseCreations)
		{
			if (creation.CreateAsSingleStack)
			{
				AddCreationRequest(
					creation.ContentId,
					creation.Count,
					creationPosition,
					anchorStackBottomCardId,
					createBeforeCardUse,
					creation.AnchorCardId,
					creation.AllowAnchorStackSpawnAttach,
					spawnPresentationHeightOffset,
					creation.UseDragHeightForSpawn,
					preUseCreations,
					postUseCreations);
				return;
			}

			for (int creationIndex = 0; creationIndex < creation.Count; creationIndex++)
			{
				AddCreationRequest(
					creation.ContentId,
					1,
					creationPosition,
					anchorStackBottomCardId,
					createBeforeCardUse,
					creation.AnchorCardId,
					creation.AllowAnchorStackSpawnAttach,
					spawnPresentationHeightOffset,
					creation.UseDragHeightForSpawn,
					preUseCreations,
					postUseCreations);
			}
		}

		private static void AddCreationRequest(
			ContentId contentId,
			int count,
			Vector2 position,
			TabletopCardId anchorStackBottomCardId,
			bool createBeforeCardUse,
			TabletopCardId anchorCardId,
			bool allowAnchorStackSpawnAttach,
			float spawnPresentationHeightOffset,
			bool useDragHeightForSpawn,
			List<TabletopCardCreationRequest> preUseCreations,
			List<TabletopCardCreationRequest> postUseCreations)
		{
			if (createBeforeCardUse)
			{
				preUseCreations.Add(new TabletopCardCreationRequest(
					contentId,
					count,
					position,
					placementLockedStackCardId: anchorCardId,
					spawnPresentationHeightOffset: spawnPresentationHeightOffset,
					spawnPresentationOriginCardId: useDragHeightForSpawn ? anchorCardId : default,
					useDragHeightForSpawn: useDragHeightForSpawn));
				return;
			}

			TabletopCardId spawnAttachIgnoredStackCardId =
				allowAnchorStackSpawnAttach ? default : anchorStackBottomCardId;
			postUseCreations.Add(new TabletopCardCreationRequest(
				contentId,
				count,
				position,
				spawnAttachIgnoredStackCardId: spawnAttachIgnoredStackCardId,
				spawnPresentationHeightOffset: spawnPresentationHeightOffset,
				useDragHeightForSpawn: useDragHeightForSpawn));
		}

		private static bool ShouldCreateBeforeUsingAnchor(
			IReadOnlyList<TabletopCardId> useCardIds,
			TabletopCardId anchorCardId)
		{
			for (int useIndex = 0; useIndex < useCardIds.Count; useIndex++)
			{
				if (useCardIds[useIndex] == anchorCardId)
				{
					return true;
				}
			}
			return false;
		}

		private static void CreateCardStacks(
			Gameplay.Tabletop.Tabletop tabletop,
			IReadOnlyList<TabletopCardCreationRequest> creations,
			List<ContentId> createdCardIds)
		{
			for (int creationIndex = 0; creationIndex < creations.Count; creationIndex++)
			{
				TabletopCardCreationRequest creation = creations[creationIndex];
				tabletop.CreateCardStack(
					creation.ContentId,
					creation.Count,
				creation.Position,
				allowSpawnAttach: true,
				spawnAttachIgnoredStackCardId: creation.SpawnAttachIgnoredStackCardId,
				placementLockedStackCardId: creation.PlacementLockedStackCardId,
				spawnPresentationHeightOffset: creation.SpawnPresentationHeightOffset,
				spawnPresentationOriginCardId: creation.SpawnPresentationOriginCardId,
				useDragHeightForSpawn: creation.UseDragHeightForSpawn);
				for (int createdIndex = 0; createdIndex < creation.Count; createdIndex++)
				{
					createdCardIds.Add(creation.ContentId);
				}
			}
		}

		private static List<TabletopPresentationCue> CreatePresentationCues(
			ActionResultPlan plan,
			Gameplay.Tabletop.Tabletop tabletop)
		{
			List<TabletopPresentationCue> cues = new List<TabletopPresentationCue>();
			bool hasPackPurchase = plan.PackPurchases.Count > 0;
			bool hasSale = plan.SoldContentIds.Count > 0;
			bool hasChestDeposit = false;
			bool hasChestWithdrawal = false;
			for (int i = 0; i < plan.ChestCurrencyChanges.Count; i++)
			{
				ChestCurrencyChangeSpec change = plan.ChestCurrencyChanges[i];
				if (change.Delta > 0)
				{
					hasChestDeposit = true;
				}
				else if (change.Delta < 0)
				{
					hasChestWithdrawal = true;
				}
			}

			bool usesCardPack = false;
			for (int i = 0; i < plan.UseCardIds.Count; i++)
			{
				TabletopCardId cardId = plan.UseCardIds[i];
				if (!tabletop.Cards.TryGetCard(cardId, out TabletopCard card) ||
					!tabletop.ContentIndex.TryGet(card.ContentId, out CardDefinition definition))
				{
					throw new InvalidOperationException($"行动表现反馈引用了不存在或类型错误的已使用卡牌 {cardId}。");
				}
				if (definition is CardPackDefinition)
				{
					usesCardPack = true;
				}
				if (card.RemainingUses == 1)
				{
					AddCueIfMissing(
						cues,
						TabletopPresentationCue.AtTablePosition(
							TabletopPresentationCueKind.CardSmoke,
							card.Position));
				}
			}

			for (int i = 0; i < plan.PackPurchases.Count; i++)
			{
				AddCardSmokeCueAtCard(cues, tabletop, plan.PackPurchases[i].VendorCardId, "卡包购买");
			}
			for (int i = 0; i < plan.ChestCurrencyChanges.Count; i++)
			{
				AddCardSmokeCueAtCard(cues, tabletop, plan.ChestCurrencyChanges[i].ChestCardId, "箱子存取币");
			}
			if (hasSale)
			{
				for (int i = 0; i < plan.Creations.Count; i++)
				{
					AddCardSmokeCueAtCard(cues, tabletop, plan.Creations[i].AnchorCardId, "出售交易");
				}
			}
			if (plan.Creations.Count > 0 &&
				!hasPackPurchase &&
				!hasSale &&
				!hasChestWithdrawal &&
				!usesCardPack)
			{
				for (int i = 0; i < plan.Creations.Count; i++)
				{
					AddCardSmokeCueAtCard(cues, tabletop, plan.Creations[i].AnchorCardId, "生成卡牌");
				}
				AddCueIfMissing(cues, TabletopPresentationCue.Global(TabletopPresentationCueKind.Pop));
			}
			if (plan.RemovalCardIds.Count > 0 &&
				!hasPackPurchase &&
				!hasSale &&
				!hasChestDeposit &&
				plan.EquipCards.Count == 0)
			{
				for (int i = 0; i < plan.RemovalCardIds.Count; i++)
				{
					AddCardSmokeCueAtCard(cues, tabletop, plan.RemovalCardIds[i], "移除卡牌");
				}
			}
			if (hasSale || hasChestDeposit)
			{
				AddCueIfMissing(cues, TabletopPresentationCue.Global(TabletopPresentationCueKind.Coins));
			}
			if (hasChestWithdrawal && !hasPackPurchase)
			{
				AddCueIfMissing(cues, TabletopPresentationCue.Global(TabletopPresentationCueKind.Coin));
			}
			if (hasPackPurchase)
			{
				AddCueIfMissing(cues, TabletopPresentationCue.Global(TabletopPresentationCueKind.CashRegister));
			}
			return cues;
		}

		private static void AddCardSmokeCueAtCard(
			List<TabletopPresentationCue> cues,
			Gameplay.Tabletop.Tabletop tabletop,
			TabletopCardId cardId,
			string source)
		{
			if (!tabletop.Cards.TryGetCard(cardId, out TabletopCard card))
			{
				throw new InvalidOperationException(
					$"行动表现反馈的{source}引用了不存在的牌桌卡牌 {cardId}。");
			}
			AddCueIfMissing(
				cues,
				TabletopPresentationCue.AtTablePosition(
					TabletopPresentationCueKind.CardSmoke,
					card.Position));
		}

		private static void AddCueIfMissing(
			List<TabletopPresentationCue> cues,
			TabletopPresentationCue cue)
		{
			for (int i = 0; i < cues.Count; i++)
			{
				if (cues[i].Equals(cue))
				{
					return;
				}
			}
			cues.Add(cue);
		}

		private static void AddExploredCards(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			ExploreCardsResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			List<ContentId> exploredContentIds,
			HashSet<TabletopCardId> exploredCardSet)
		{
			string exploredSlotKey = ResolveResultSlotKey(action, intent.ExploredSlotKey, "探索结果");
			ActionSlotBinding exploredBinding = FindBinding(action.ContentId, bindings, exploredSlotKey);
			if (exploredBinding.CardIds.Count == 0)
			{
				throw new InvalidOperationException(
					$"行动 {action.ContentId} 的探索槽位 {exploredSlotKey} 没有绑定牌桌卡牌。");
			}

			for (int i = 0; i < exploredBinding.CardIds.Count; i++)
			{
				TabletopCardId cardId = exploredBinding.CardIds[i];
				if (!exploredCardSet.Add(cardId))
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 重复记录探索牌桌卡牌 {cardId}。");
				}
				if (!cards.TryGetCard(cardId, out TabletopCard card) ||
					!contentIndex.TryGet(card.ContentId, out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的探索结果引用了不存在或类型错误的牌桌卡牌 {cardId}。");
				}
				exploredContentIds.Add(card.ContentId);
			}
		}

		private static void AddExplorationLoot(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			ExploreLootResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			ref Unity.Mathematics.Random authoritativeRandom,
			List<CardCreationSpec> creations)
		{
			if (authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException(
					$"探索行动 {action.ContentId} 需要牌桌权威随机流，但随机流尚未初始化。");
			}

			string areaSlotKey = ResolveResultSlotKey(action, intent.AreaSlotKey, "探索区域");
			ActionSlotBinding areaBinding = FindBinding(action.ContentId, bindings, areaSlotKey);
			for (int cardIndex = 0; cardIndex < areaBinding.CardIds.Count; cardIndex++)
			{
				TabletopCardId areaCardId = areaBinding.CardIds[cardIndex];
				if (!cards.TryGetCard(areaCardId, out TabletopCard areaCard) ||
					!contentIndex.TryGet(areaCard.ContentId, out CardDefinition areaDefinition) ||
					!HasContentTag(areaDefinition, XTag.Card_Category_Area) ||
					areaDefinition.Loot.Count == 0)
				{
					continue;
				}

				int totalWeight = 0;
				for (int lootIndex = 0; lootIndex < areaDefinition.Loot.Count; lootIndex++)
				{
					CardLootEntry entry = areaDefinition.Loot[lootIndex]
						?? throw new InvalidOperationException(
							$"区域卡 {areaDefinition.ContentId} 的探索产出包含空条目。");
					if (entry.Weight <= 0 ||
						!contentIndex.TryGet(entry.CardId, out CardDefinition _))
					{
						throw new InvalidOperationException(
							$"区域卡 {areaDefinition.ContentId} 的探索产出 {entry.CardId} 或权重无效。");
					}
					totalWeight = checked(totalWeight + entry.Weight);
				}

				if (totalWeight <= 0)
				{
					throw new InvalidOperationException(
						$"区域卡 {areaDefinition.ContentId} 没有可抽取的探索产出。");
				}

				int roll = authoritativeRandom.NextInt(totalWeight);
				for (int lootIndex = 0; lootIndex < areaDefinition.Loot.Count; lootIndex++)
				{
					CardLootEntry entry = areaDefinition.Loot[lootIndex];
					if (roll < entry.Weight)
					{
						creations.Add(new CardCreationSpec(
							entry.CardId,
							1,
							areaCardId,
							allowAnchorStackSpawnAttach: true));
						return;
					}
					roll -= entry.Weight;
				}
				throw new InvalidOperationException(
					$"区域卡 {areaDefinition.ContentId} 的探索产出权重抽取没有命中任何条目。");
			}
		}

		private static bool HasContentTag(ContentAsset contentAsset, int tagCode)
		{
			IReadOnlyList<int> tagCodes = contentAsset.TagCodes;
			for (int i = 0; i < tagCodes.Count; i++)
			{
				if (TagHelper.HasTag(tagCodes[i], tagCode))
				{
					return true;
				}
			}
			return false;
		}

		private static void AddIntent(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			ActionResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			TabletopCardPlacementRules placementRules,
			Func<ContentId, bool> isContentDiscovered,
			ref Unity.Mathematics.Random authoritativeRandom,
			List<TabletopCardId> removals,
			HashSet<TabletopCardId> removalSet,
			List<TabletopCardId> uses,
			HashSet<TabletopCardId> useSet,
			List<CardCreationSpec> creations,
			List<ResearchDiscoverySpec> researchDiscoveries,
			List<PackPurchaseSpec> packPurchases,
			List<ChestCurrencyChangeSpec> chestCurrencyChanges,
			List<EquipCardSpec> equipCards,
			List<UnequipCardSpec> unequipCards,
			List<ContentId> soldContentIds,
			List<ContentId> exploredContentIds,
			HashSet<TabletopCardId> exploredCardSet)
		{
			if (intent is ExploreCardsResultIntent exploreIntent)
			{
				AddExploredCards(
					action,
					bindings,
					exploreIntent,
					contentIndex,
					cards,
					exploredContentIds,
					exploredCardSet);
				return;
			}
			if (intent is ExploreLootResultIntent exploreLootIntent)
			{
				AddExplorationLoot(
					action,
					bindings,
					exploreLootIntent,
					contentIndex,
					cards,
					ref authoritativeRandom,
					creations);
				return;
			}
			if (intent is EquipCardResultIntent equipIntent)
			{
				AddEquipCard(
					action,
					bindings,
					equipIntent,
					contentIndex,
					cards,
					removals,
					removalSet,
					useSet,
					equipCards,
					unequipCards);
				return;
			}
			if (intent is UnequipCardResultIntent unequipIntent)
			{
				AddUnequipCard(
					action,
					bindings,
					unequipIntent,
					contentIndex,
					cards,
					equipCards,
					unequipCards);
				return;
			}
			if (intent is PurchaseCardPackResultIntent purchaseIntent)
			{
				AddPackPurchase(
					action,
					bindings,
					purchaseIntent,
					contentIndex,
					cards,
					removals,
					removalSet,
					useSet,
					creations,
					packPurchases,
					chestCurrencyChanges);
				return;
			}
			if (intent is OpenCardPackResultIntent openPackIntent)
			{
				AddCardPackDraw(
					action,
					bindings,
					openPackIntent,
					contentIndex,
					cards,
					isContentDiscovered,
					ref authoritativeRandom,
					uses,
					removalSet,
					useSet,
					creations,
					researchDiscoveries);
				return;
			}
			if (intent is DepositCurrencyIntoChestResultIntent depositIntent)
			{
				AddChestDeposit(
					action,
					bindings,
					depositIntent,
					contentIndex,
					cards,
					removals,
					removalSet,
					useSet,
					chestCurrencyChanges);
				return;
			}
			if (intent is WithdrawCurrencyFromChestResultIntent withdrawIntent)
			{
				AddChestWithdrawal(
					action,
					bindings,
					withdrawIntent,
					contentIndex,
					cards,
					placementRules,
					creations,
					chestCurrencyChanges);
				return;
			}
			if (intent is SellCardsResultIntent sellIntent)
			{
				string soldSlotKey = ResolveResultSlotKey(action, sellIntent.SoldSlotKey, "出售结果");
				string anchorSlotKey = ResolveResultSlotKey(action, sellIntent.AnchorSlotKey, "货币生成位置");
				ActionSlotBinding soldBinding = FindBinding(action.ContentId, bindings, soldSlotKey);
				ActionSlotBinding anchorBinding = FindBinding(action.ContentId, bindings, anchorSlotKey);
				if (!sellIntent.CurrencyCardId.IsValid ||
					!contentIndex.TryGet(sellIntent.CurrencyCardId, out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的售卡结果缺少有效货币卡 {sellIntent.CurrencyCardId}。");
				}
				if (anchorBinding.CardIds.Count != 1)
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的货币生成位置来源槽位 {anchorSlotKey} 必须绑定一张收购点卡牌。");
				}
				if (soldBinding.CardIds.Count == 0)
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的出售槽位 {soldSlotKey} 没有绑定牌桌卡牌。");
				}
				TabletopCardId currencyAnchorCardId = anchorBinding.CardIds[0];
				if (!cards.TryGetCard(currencyAnchorCardId, out TabletopCard anchorCard) ||
					!contentIndex.TryGet(anchorCard.ContentId, out CardBuyerDefinition buyerDefinition))
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的货币生成位置来源槽位 {anchorSlotKey} 没有绑定有效收购点。");
				}

				int totalSellValue = 0;
				for (int i = 0; i < soldBinding.CardIds.Count; i++)
				{
					TabletopCardId cardId = soldBinding.CardIds[i];
					if (useSet.Contains(cardId) || !removalSet.Add(cardId))
					{
						throw new InvalidOperationException(
							$"行动 {action.ContentId} 的结果重复修改牌桌卡牌 {cardId}。");
					}
					if (!cards.TryGetCard(cardId, out TabletopCard card) ||
						!contentIndex.TryGet(card.ContentId, out CardDefinition soldDefinition))
					{
						throw new InvalidOperationException(
							$"行动 {action.ContentId} 的出售结果引用了不存在或类型错误的牌桌卡牌 {cardId}。");
					}
					if (card is ChestCard chest && chest.StoredCurrencyCount > 0)
					{
						throw new InvalidOperationException(
							$"箱子卡牌 {soldDefinition.ContentId} 仍存有货币，不能执行出售行动 {action.ContentId}。");
					}
					if (soldDefinition.SellValue <= 0)
					{
						throw new InvalidOperationException(
							$"卡牌 {soldDefinition.ContentId} 不可出售，不能执行行动 {action.ContentId}。");
					}
					totalSellValue = checked(totalSellValue + soldDefinition.SellValue);
					soldContentIds.Add(soldDefinition.ContentId);
					removals.Add(cardId);
				}
				creations.Add(new CardCreationSpec(
					sellIntent.CurrencyCardId,
					totalSellValue,
					currencyAnchorCardId,
					createAsSingleStack: true,
					positionOffset: buyerDefinition.CurrencySpawnOffset));
				return;
			}
			if (intent is UseCardsResultIntent useIntent)
			{
				string useSlotKey = ResolveResultSlotKey(action, useIntent.SlotKey, "使用结果");
				ActionSlotBinding useBinding = FindBinding(action.ContentId, bindings, useSlotKey);
				for (int i = 0; i < useBinding.CardIds.Count; i++)
				{
					TabletopCardId cardId = useBinding.CardIds[i];
					if (removalSet.Contains(cardId) || !useSet.Add(cardId))
					{
						throw new InvalidOperationException(
							$"行动 {action.ContentId} 的结果重复修改牌桌卡牌 {cardId}。");
					}
					uses.Add(cardId);
				}
				return;
			}
			if (intent is ResearchDiscoveryResultIntent researchIntent)
			{
				string anchorSlotKey = ResolveResultSlotKey(action, researchIntent.AnchorSlotKey, "研究结果位置");
				ActionSlotBinding anchorBinding = FindBinding(action.ContentId, bindings, anchorSlotKey);
				if (anchorBinding.CardIds.Count == 0)
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的研究结果位置来源槽位 {anchorSlotKey} 没有绑定牌桌卡牌。");
				}
				List<ResearchDiscoveryEntrySpec> entries = new List<ResearchDiscoveryEntrySpec>(researchIntent.Entries.Count);
				for (int entryIndex = 0; entryIndex < researchIntent.Entries.Count; entryIndex++)
				{
					ResearchDiscoveryEntry entry = researchIntent.Entries[entryIndex];
					if (entry == null ||
						!contentIndex.TryGet(entry.ActionId, out ActionDefinition _) ||
						!contentIndex.TryGet(entry.RecipeCardId, out CardDefinition _))
					{
						throw new InvalidOperationException(
							$"行动 {action.ContentId} 包含无效的研究候选或配方卡。");
					}
					entries.Add(new ResearchDiscoveryEntrySpec(entry.ActionId, entry.RecipeCardId));
				}
				if (entries.Count == 0)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的研究候选池不能为空。");
				}
				researchDiscoveries.Add(new ResearchDiscoverySpec(entries, anchorBinding.CardIds[0]));
				return;
			}
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
				if (useSet.Contains(cardId) || !removalSet.Add(cardId))
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的结果重复修改牌桌卡牌 {cardId}。");
				}
				removals.Add(cardId);
			}
		}

		private static void AddEquipCard(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			EquipCardResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			List<TabletopCardId> removals,
			HashSet<TabletopCardId> removalSet,
			HashSet<TabletopCardId> useSet,
			List<EquipCardSpec> equipCards,
			List<UnequipCardSpec> unequipCards)
		{
			string equipmentSlotKey = ResolveResultSlotKey(action, intent.EquipmentSlotKey, "装备卡槽位");
			string characterSlotKey = ResolveResultSlotKey(action, intent.CharacterSlotKey, "装备角色槽位");
			ActionSlotBinding equipmentBinding = FindBinding(action.ContentId, bindings, equipmentSlotKey);
			ActionSlotBinding characterBinding = FindBinding(action.ContentId, bindings, characterSlotKey);
			if (equipmentBinding.CardIds.Count != 1 || characterBinding.CardIds.Count != 1)
			{
				throw new InvalidOperationException($"装备行动 {action.ContentId} 必须绑定一张装备卡和一张角色卡。");
			}

			TabletopCardId equipmentCardId = equipmentBinding.CardIds[0];
			TabletopCardId characterCardId = characterBinding.CardIds[0];
			if (equipmentCardId == characterCardId || useSet.Contains(equipmentCardId) || !removalSet.Add(equipmentCardId))
			{
				throw new InvalidOperationException($"装备行动 {action.ContentId} 重复修改装备卡 {equipmentCardId}。");
			}
			if (!cards.TryGetCard(equipmentCardId, out TabletopCard equipmentCard) ||
				!contentIndex.TryGet(equipmentCard.ContentId, out EquipmentCardDefinition equipmentDefinition))
			{
				throw new InvalidOperationException($"装备行动 {action.ContentId} 没有绑定有效装备卡。");
			}
			if (!cards.TryGetCard(characterCardId, out TabletopCard characterCard) ||
				characterCard is not CharacterCard character)
			{
				throw new InvalidOperationException($"装备行动 {action.ContentId} 没有绑定有效角色卡。");
			}
			EnsureNoEquipmentPlanConflict(action.ContentId, characterCardId, equipmentDefinition.SlotId, equipCards, unequipCards);

			EquippedCardSnapshot replacedSnapshot = character.TryGetEquippedCard(equipmentDefinition.SlotId, out EquippedCardState replaced)
				? replaced.CreateSnapshot()
				: null;
			removals.Add(equipmentCardId);
			equipCards.Add(new EquipCardSpec(
				equipmentCardId,
				characterCardId,
				equipmentDefinition.SlotId,
				equipmentDefinition.OnEquippedGameplayEffectId,
				equipmentCard.CreateSnapshot(),
				equipmentCard.Position,
				replacedSnapshot));
		}

		private static void AddUnequipCard(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			UnequipCardResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			IReadOnlyList<EquipCardSpec> equipCards,
			List<UnequipCardSpec> unequipCards)
		{
			string characterSlotKey = ResolveResultSlotKey(action, intent.CharacterSlotKey, "卸装角色槽位");
			ActionSlotBinding characterBinding = FindBinding(action.ContentId, bindings, characterSlotKey);
			if (characterBinding.CardIds.Count != 1)
			{
				throw new InvalidOperationException($"卸装行动 {action.ContentId} 必须绑定一张角色卡。");
			}
			if (!intent.EquipmentSlotId.IsValid ||
				!contentIndex.TryGet(intent.EquipmentSlotId, out EquipmentSlotDefinition _))
			{
				throw new InvalidOperationException($"卸装行动 {action.ContentId} 缺少有效装备槽位 {intent.EquipmentSlotId}。");
			}

			TabletopCardId characterCardId = characterBinding.CardIds[0];
			if (!cards.TryGetCard(characterCardId, out TabletopCard card) ||
				card is not CharacterCard character)
			{
				throw new InvalidOperationException($"卸装行动 {action.ContentId} 没有绑定有效角色卡。");
			}
			EnsureNoEquipmentPlanConflict(action.ContentId, characterCardId, intent.EquipmentSlotId, equipCards, unequipCards);
			if (!character.TryGetEquippedCard(intent.EquipmentSlotId, out EquippedCardState equipped))
			{
				throw new InvalidOperationException($"角色卡 {characterCardId} 的槽位 {intent.EquipmentSlotId} 没有可卸下装备。");
			}
			if (!contentIndex.TryGet(equipped.CardSnapshot.ContentId, out EquipmentCardDefinition _))
			{
				throw new InvalidOperationException($"角色卡 {characterCardId} 已装备的卡牌 {equipped.CardSnapshot.ContentId} 缺少有效装备作者源。");
			}
			unequipCards.Add(new UnequipCardSpec(
				characterCardId,
				intent.EquipmentSlotId,
				equipped.CreateSnapshot(),
				character.Position));
		}

		private static void EnsureNoEquipmentPlanConflict(
			ContentId actionId,
			TabletopCardId characterCardId,
			ContentId slotId,
			IReadOnlyList<EquipCardSpec> equipCards,
			IReadOnlyList<UnequipCardSpec> unequipCards)
		{
			for (int i = 0; i < equipCards.Count; i++)
			{
				if (equipCards[i].CharacterCardId == characterCardId && equipCards[i].SlotId == slotId)
				{
					throw new InvalidOperationException($"行动 {actionId} 重复声明角色 {characterCardId} 的装备槽位 {slotId}。");
				}
			}
			for (int i = 0; i < unequipCards.Count; i++)
			{
				if (unequipCards[i].CharacterCardId == characterCardId && unequipCards[i].SlotId == slotId)
				{
					throw new InvalidOperationException($"行动 {actionId} 重复声明角色 {characterCardId} 的卸装槽位 {slotId}。");
				}
			}
		}

		private static List<TabletopCardRestorationRequest> CreateEquipmentRestorations(ActionResultPlan plan)
		{
			List<TabletopCardRestorationRequest> restorations = new List<TabletopCardRestorationRequest>(
				plan.EquipCards.Count + plan.UnequipCards.Count);
			for (int i = 0; i < plan.EquipCards.Count; i++)
			{
				EquipCardSpec equip = plan.EquipCards[i];
				if (equip.ReplacedEquipmentSnapshot != null)
				{
					restorations.Add(new TabletopCardRestorationRequest(
						equip.ReplacedEquipmentSnapshot.CardSnapshot,
						equip.ReturnPosition));
				}
			}
			for (int i = 0; i < plan.UnequipCards.Count; i++)
			{
				UnequipCardSpec unequip = plan.UnequipCards[i];
				restorations.Add(new TabletopCardRestorationRequest(
					unequip.EquipmentSnapshot.CardSnapshot,
					unequip.ReturnPosition));
			}
			return restorations;
		}

		private static void RequireEquipCardCanCommit(
			ContentId actionId,
			Gameplay.Tabletop.Tabletop tabletop,
			EquipCardSpec equip)
		{
			if (!tabletop.Cards.TryGetCard(equip.EquipmentCardId, out TabletopCard equipmentCard) ||
				!tabletop.ContentIndex.TryGet(equipmentCard.ContentId, out EquipmentCardDefinition equipmentDefinition))
			{
				throw new InvalidOperationException($"行动 {actionId} 的装备卡 {equip.EquipmentCardId} 不属于当前牌桌或不是装备。");
			}
			if (!tabletop.Cards.TryGetCard(equip.CharacterCardId, out TabletopCard characterCard) ||
				characterCard is not CharacterCard character)
			{
				throw new InvalidOperationException($"行动 {actionId} 的装备目标 {equip.CharacterCardId} 不属于当前牌桌或不是角色卡。");
			}
			if (equipmentDefinition.SlotId != equip.SlotId ||
				equipmentDefinition.OnEquippedGameplayEffectId != equip.GameplayEffectId ||
				GameplayEffectHelper.GetConfigByID(equip.GameplayEffectId) == null)
			{
				throw new InvalidOperationException($"行动 {actionId} 的装备作者源已不符合冻结计划。");
			}
			RequireSameCardSnapshot(equip.EquipmentSnapshot, equipmentCard, $"行动 {actionId} 的装备卡快照");
			EquippedCardSnapshot currentReplacement = character.TryGetEquippedCard(equip.SlotId, out EquippedCardState equipped)
				? equipped.CreateSnapshot()
				: null;
			RequireSameEquippedSnapshot(
				equip.ReplacedEquipmentSnapshot,
				currentReplacement,
				$"行动 {actionId} 的被替换装备");
			if (equip.ReplacedEquipmentSnapshot != null)
			{
				RequireEquippedCardSnapshotCanReturn(actionId, tabletop, equip.ReplacedEquipmentSnapshot);
			}
		}

		private static void RequireUnequipCardCanCommit(
			ContentId actionId,
			Gameplay.Tabletop.Tabletop tabletop,
			UnequipCardSpec unequip)
		{
			if (!tabletop.ContentIndex.TryGet(unequip.SlotId, out EquipmentSlotDefinition _))
			{
				throw new InvalidOperationException($"行动 {actionId} 的卸装槽位 {unequip.SlotId} 不属于当前内容集合。");
			}
			if (!tabletop.Cards.TryGetCard(unequip.CharacterCardId, out TabletopCard card) ||
				card is not CharacterCard character)
			{
				throw new InvalidOperationException($"行动 {actionId} 的卸装目标 {unequip.CharacterCardId} 不属于当前牌桌或不是角色卡。");
			}
			if (!character.TryGetEquippedCard(unequip.SlotId, out EquippedCardState equipped))
			{
				throw new InvalidOperationException($"行动 {actionId} 的卸装槽位 {unequip.SlotId} 当前没有装备。");
			}
			RequireSameEquippedSnapshot(
				unequip.EquipmentSnapshot,
				equipped.CreateSnapshot(),
				$"行动 {actionId} 的卸下装备");
			RequireEquippedCardSnapshotCanReturn(actionId, tabletop, unequip.EquipmentSnapshot);
		}

		private static void RequireEquippedCardSnapshotCanReturn(
			ContentId actionId,
			Gameplay.Tabletop.Tabletop tabletop,
			EquippedCardSnapshot snapshot)
		{
			if (snapshot == null ||
				snapshot.CardSnapshot == null ||
				!tabletop.ContentIndex.TryGet(snapshot.CardSnapshot.ContentId, out EquipmentCardDefinition _) ||
				GameplayEffectHelper.GetConfigByID(snapshot.OnEquippedGameplayEffectId) == null)
			{
				throw new InvalidOperationException($"行动 {actionId} 的离桌装备快照缺少有效装备内容或 GE。");
			}
		}

		private static void RequireSameCardSnapshot(
			TabletopCardSnapshot expected,
			TabletopCard actual,
			string label)
		{
			if (expected == null || actual == null ||
				expected.CardId != actual.Id ||
				expected.ContentId != actual.ContentId ||
				expected.RemainingUses != actual.RemainingUses)
			{
				throw new InvalidOperationException($"{label} 与当前牌桌卡牌不一致。");
			}
		}

		private static void RequireSameEquippedSnapshot(
			EquippedCardSnapshot expected,
			EquippedCardSnapshot actual,
			string label)
		{
			if (expected == null || actual == null)
			{
				if (expected != null || actual != null)
				{
					throw new InvalidOperationException($"{label} 与冻结装备状态不一致。");
				}
				return;
			}
			if (expected.SlotId != actual.SlotId ||
				expected.OnEquippedGameplayEffectId != actual.OnEquippedGameplayEffectId ||
				!SameCardSnapshot(expected.CardSnapshot, actual.CardSnapshot))
			{
				throw new InvalidOperationException($"{label} 与冻结装备状态不一致。");
			}
		}

		private static bool SameCardSnapshot(TabletopCardSnapshot left, TabletopCardSnapshot right)
		{
			return left != null &&
				right != null &&
				left.CardId == right.CardId &&
				left.ContentId == right.ContentId &&
				left.RemainingUses == right.RemainingUses;
		}

		private static void AddChestDeposit(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			DepositCurrencyIntoChestResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			List<TabletopCardId> removals,
			HashSet<TabletopCardId> removalSet,
			HashSet<TabletopCardId> useSet,
			List<ChestCurrencyChangeSpec> chestCurrencyChanges)
		{
			string chestSlotKey = ResolveResultSlotKey(action, intent.ChestSlotKey, "箱子存币槽位");
			string currencySlotKey = ResolveResultSlotKey(action, intent.CurrencySlotKey, "箱子存入货币槽位");
			ActionSlotBinding chestBinding = FindBinding(action.ContentId, bindings, chestSlotKey);
			ActionSlotBinding currencyBinding = FindBinding(action.ContentId, bindings, currencySlotKey);
			ChestCard chest = RequireBoundChest(action, cards, chestBinding, "存币");
			ChestCardDefinition chestDefinition = RequireChestDefinition(action, contentIndex, chest);
			int currentStored = GetPlannedChestStoredCurrencyCount(chest, chestCurrencyChanges);
			TabletopCardStack sourceStack = RequireStackCraftDepositSourceStack(
				action,
				cards,
				currencyBinding,
				chest);
			int remainingCapacity = chest.Capacity - currentStored;
			int depositAmount = 0;
			for (int i = 0; i < sourceStack.Cards.Count && depositAmount < remainingCapacity; i++)
			{
				TabletopCard currencyCard = sourceStack.Cards[i];
				if (currencyCard.ContentId != chestDefinition.CurrencyCardId)
				{
					continue;
				}
				TabletopCardId currencyCardId = currencyCard.Id;
				if (currencyCardId == chest.Id || useSet.Contains(currencyCardId) || !removalSet.Add(currencyCardId))
				{
					throw new InvalidOperationException($"箱子存币行动 {action.ContentId} 重复修改存入货币卡 {currencyCardId}。");
				}
				removals.Add(currencyCardId);
				depositAmount++;
			}
			if (depositAmount <= 0)
			{
				throw new InvalidOperationException($"箱子存币行动 {action.ContentId} 没有可存入的容量或货币。");
			}

			AddChestCurrencyChange(chestCurrencyChanges, chest, depositAmount);
		}

		private static TabletopCardStack RequireStackCraftDepositSourceStack(
			ActionDefinition action,
			TabletopCards cards,
			ActionSlotBinding currencyBinding,
			ChestCard chest)
		{
			if (currencyBinding.CardIds.Count <= 0)
			{
				throw new InvalidOperationException($"箱子存币行动 {action.ContentId} 必须绑定拖拽牌堆顶端货币。");
			}
			TabletopCardId currencyAnchorCardId = currencyBinding.CardIds[0];
			TabletopCardStack sourceStack = cards.GetStackContaining(currencyAnchorCardId);
			if (ReferenceEquals(sourceStack, chest.Stack))
			{
				throw new InvalidOperationException($"箱子存币行动 {action.ContentId} 的货币牌堆和钱箱目标不能是同一个牌堆。");
			}
			if (sourceStack.TopCard.Id != currencyAnchorCardId)
			{
				throw new InvalidOperationException(
					$"箱子存币行动 {action.ContentId} 必须从拖拽牌堆顶端货币开始，对齐 StackCraft ChestLogic.OnStack。");
			}
			return sourceStack;
		}

		private static void AddChestWithdrawal(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			WithdrawCurrencyFromChestResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			TabletopCardPlacementRules placementRules,
			List<CardCreationSpec> creations,
			List<ChestCurrencyChangeSpec> chestCurrencyChanges)
		{
			string chestSlotKey = ResolveResultSlotKey(action, intent.ChestSlotKey, "箱子取币槽位");
			ActionSlotBinding chestBinding = FindBinding(action.ContentId, bindings, chestSlotKey);
			ChestCard chest = RequireBoundChest(action, cards, chestBinding, "取币");
			ChestCardDefinition chestDefinition = RequireChestDefinition(action, contentIndex, chest);
			TabletopCardStack chestStack = cards.GetStackContaining(chest.Id);
			if (chestStack.Cards.Count != 1)
			{
				throw new InvalidOperationException(
					$"箱子取币行动 {action.ContentId} 必须在箱子单独成堆时执行，对齐 StackCraft ChestLogic.OnClick。");
			}
			int currentStored = GetPlannedChestStoredCurrencyCount(chest, chestCurrencyChanges);
			if (currentStored <= 0)
			{
				throw new InvalidOperationException($"箱子取币行动 {action.ContentId} 绑定的箱子没有可取出的货币。");
			}
			AddChestCurrencyChange(chestCurrencyChanges, chest, -1);
			Vector2 chestSize = cards.ResolveCardSize(chest.ContentId, placementRules.Geometry);
			creations.Add(new CardCreationSpec(
				chestDefinition.CurrencyCardId,
				1,
				chest.Id,
				positionOffset: new Vector2(chestSize.x, 0f)));
		}

		private static ChestCard RequireBoundChest(
			ActionDefinition action,
			TabletopCards cards,
			ActionSlotBinding binding,
			string operation)
		{
			if (binding.CardIds.Count != 1 ||
				!cards.TryGetCard(binding.CardIds[0], out TabletopCard card) ||
				card is not ChestCard chest)
			{
				throw new InvalidOperationException($"箱子{operation}行动 {action.ContentId} 必须绑定一张有效箱子卡。");
			}
			return chest;
		}

		private static ChestCardDefinition RequireChestDefinition(
			ActionDefinition action,
			ContentIndex contentIndex,
			ChestCard chest)
		{
			if (!contentIndex.TryGet(chest.ContentId, out ChestCardDefinition chestDefinition))
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 绑定的箱子 {chest.ContentId} 缺少有效箱子作者源。");
			}
			if (!contentIndex.TryGet(chestDefinition.CurrencyCardId, out CardDefinition _))
			{
				throw new InvalidOperationException($"箱子 {chest.ContentId} 声明的货币 {chestDefinition.CurrencyCardId} 不属于当前内容集合。");
			}
			return chestDefinition;
		}

		private static int GetPlannedChestStoredCurrencyCount(
			ChestCard chest,
			IReadOnlyList<ChestCurrencyChangeSpec> chestCurrencyChanges)
		{
			int current = chest.StoredCurrencyCount;
			for (int i = 0; i < chestCurrencyChanges.Count; i++)
			{
				ChestCurrencyChangeSpec change = chestCurrencyChanges[i];
				if (change.ChestCardId == chest.Id)
				{
					if (change.ExpectedStoredCurrencyCount != current)
					{
						throw new InvalidOperationException($"箱子 {chest.Id} 的冻结存币计划顺序不一致。");
					}
					current = checked(current + change.Delta);
				}
			}
			return current;
		}

		private static void AddChestCurrencyChange(
			List<ChestCurrencyChangeSpec> chestCurrencyChanges,
			ChestCard chest,
			int delta)
		{
			int expected = GetPlannedChestStoredCurrencyCount(chest, chestCurrencyChanges);
			int next = expected + delta;
			if (delta == 0 || next < 0 || next > chest.Capacity)
			{
				throw new InvalidOperationException($"箱子 {chest.Id} 的存币变化 {delta} 会让数量越界。");
			}
			chestCurrencyChanges.Add(new ChestCurrencyChangeSpec(chest.Id, expected, delta));
		}

		private static void AddPackPurchase(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			PurchaseCardPackResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			List<TabletopCardId> removals,
			HashSet<TabletopCardId> removalSet,
			HashSet<TabletopCardId> useSet,
			List<CardCreationSpec> creations,
			List<PackPurchaseSpec> packPurchases,
			List<ChestCurrencyChangeSpec> chestCurrencyChanges)
		{
			string vendorSlotKey = ResolveResultSlotKey(action, intent.VendorSlotKey, "卡包商贩槽位");
			string paymentSlotKey = ResolveResultSlotKey(action, intent.PaymentSlotKey, "卡包付款槽位");
			ActionSlotBinding vendorBinding = FindBinding(action.ContentId, bindings, vendorSlotKey);
			ActionSlotBinding paymentBinding = FindBinding(action.ContentId, bindings, paymentSlotKey);
			if (vendorBinding.CardIds.Count != 1 || paymentBinding.CardIds.Count == 0)
			{
				throw new InvalidOperationException($"卡包购买行动 {action.ContentId} 必须绑定一张商贩卡和至少一张付款卡。");
			}

			TabletopCardId vendorCardId = vendorBinding.CardIds[0];
			if (!cards.TryGetCard(vendorCardId, out TabletopCard card) ||
				card is not PackVendorCard vendorCard ||
				!contentIndex.TryGet(card.ContentId, out PackVendorDefinition vendorDefinition))
			{
				throw new InvalidOperationException($"卡包购买行动 {action.ContentId} 没有绑定有效卡包商贩。");
			}
			int paymentAmount = 0;
			int remainingPrice = vendorCard.RemainingPrice;
			IReadOnlyList<TabletopCardId> paymentCardIds =
				GetPaymentCardIdsInCurrentStackBottomFirst(action, cards, paymentBinding);
			for (int i = 0; i < paymentCardIds.Count && paymentAmount < remainingPrice; i++)
			{
				TabletopCardId paymentCardId = paymentCardIds[i];
				if (paymentCardId == vendorCardId || useSet.Contains(paymentCardId))
				{
					throw new InvalidOperationException($"卡包购买行动 {action.ContentId} 重复修改付款卡 {paymentCardId}。");
				}
				if (!cards.TryGetCard(paymentCardId, out TabletopCard paymentCard))
				{
					throw new InvalidOperationException($"卡包购买行动 {action.ContentId} 的付款卡 {paymentCardId} 已不存在。");
				}

				if (paymentCard is ChestCard chest)
				{
					int currentStored = GetPlannedChestStoredCurrencyCount(chest, chestCurrencyChanges);
					int amountFromChest = Math.Min(remainingPrice - paymentAmount, currentStored);
					if (amountFromChest <= 0)
					{
						break;
					}
					AddChestCurrencyChange(chestCurrencyChanges, chest, -amountFromChest);
					paymentAmount += amountFromChest;
					if (paymentAmount < remainingPrice)
					{
						break;
					}
					continue;
				}

				if (!CurrencyCardQuery.IsCurrencyCard(contentIndex, paymentCard.ContentId))
				{
					throw new InvalidOperationException(
						$"卡包购买行动 {action.ContentId} 的付款卡 {paymentCard.ContentId} 不是当前内容集合声明的货币卡。");
				}
				if (!removalSet.Add(paymentCardId))
				{
					throw new InvalidOperationException($"卡包购买行动 {action.ContentId} 重复移除付款卡 {paymentCardId}。");
				}
				removals.Add(paymentCardId);
				paymentAmount++;
			}
			if (paymentAmount <= 0)
			{
				throw new InvalidOperationException($"卡包购买行动 {action.ContentId} 没有可用付款来源。");
			}

			bool completesPurchase = paymentAmount == vendorCard.RemainingPrice;
			packPurchases.Add(new PackPurchaseSpec(
				vendorCardId,
				vendorCard.PaidAmount,
				paymentAmount,
				completesPurchase,
				vendorDefinition.OfferedPackId));
			if (completesPurchase)
			{
				creations.Add(new CardCreationSpec(
					vendorDefinition.OfferedPackId,
					1,
					vendorCardId,
					positionOffset: vendorDefinition.PackSpawnOffset,
					allowAnchorStackSpawnAttach: true));
			}
		}

		private static IReadOnlyList<TabletopCardId> GetPaymentCardIdsInCurrentStackBottomFirst(
			ActionDefinition action,
			TabletopCards cards,
			ActionSlotBinding paymentBinding)
		{
			if (paymentBinding.CardIds.Count <= 1)
			{
				return paymentBinding.CardIds;
			}

			TabletopCardStack sourceStack = cards.GetStackContaining(paymentBinding.CardIds[0]);
			HashSet<TabletopCardId> paymentSet = new HashSet<TabletopCardId>(paymentBinding.CardIds);
			List<TabletopCardId> ordered = new List<TabletopCardId>(paymentBinding.CardIds.Count);
			for (int i = sourceStack.Cards.Count - 1; i >= 0; i--)
			{
				TabletopCardId cardId = sourceStack.Cards[i].Id;
				if (!paymentSet.Contains(cardId))
				{
					continue;
				}
				ordered.Add(cardId);
				paymentSet.Remove(cardId);
			}
			if (paymentSet.Count != 0)
			{
				throw new InvalidOperationException(
					$"卡包购买行动 {action.ContentId} 的付款槽位必须来自同一个被释放牌堆。");
			}
			return ordered;
		}

		private static void RequirePackPurchaseCanCommit(
			ContentId actionId,
			Gameplay.Tabletop.Tabletop tabletop,
			PackPurchaseSpec purchase)
		{
			if (!tabletop.Cards.TryGetCard(purchase.VendorCardId, out TabletopCard card) ||
				card is not PackVendorCard vendor ||
				vendor.PaidAmount != purchase.ExpectedPaidAmount ||
				vendor.RemainingPrice < purchase.PaymentAmount)
			{
				throw new InvalidOperationException($"行动 {actionId} 的卡包商贩付款状态已不符合冻结计划。");
			}
			bool completes = vendor.RemainingPrice == purchase.PaymentAmount;
			if (completes != purchase.CompletesPurchase)
			{
				throw new InvalidOperationException($"行动 {actionId} 的卡包成交状态已不符合冻结计划。");
			}
			if (!tabletop.ContentIndex.TryGet(purchase.PackId, out CardPackDefinition _))
			{
				throw new InvalidOperationException($"行动 {actionId} 要购买的卡包 {purchase.PackId} 不属于当前内容集合。");
			}
		}

		private static void RequireChestCurrencyChangeCanCommit(
			ContentId actionId,
			Gameplay.Tabletop.Tabletop tabletop,
			ChestCurrencyChangeSpec change)
		{
			if (!tabletop.Cards.TryGetCard(change.ChestCardId, out TabletopCard card) ||
				card is not ChestCard chest ||
				chest.StoredCurrencyCount != change.ExpectedStoredCurrencyCount)
			{
				throw new InvalidOperationException($"行动 {actionId} 的箱子存币状态已不符合冻结计划。");
			}
			int next = chest.StoredCurrencyCount + change.Delta;
			if (change.Delta == 0 || next < 0 || next > chest.Capacity)
			{
				throw new InvalidOperationException($"行动 {actionId} 的箱子存币变化会让数量越界。");
			}
		}

		private static void AddCardPackDraw(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			OpenCardPackResultIntent intent,
			ContentIndex contentIndex,
			TabletopCards cards,
			Func<ContentId, bool> isContentDiscovered,
			ref Unity.Mathematics.Random authoritativeRandom,
			List<TabletopCardId> uses,
			HashSet<TabletopCardId> removalSet,
			HashSet<TabletopCardId> useSet,
			List<CardCreationSpec> creations,
			List<ResearchDiscoverySpec> researchDiscoveries)
		{
			if (authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException(
					$"打开卡包行动 {action.ContentId} 需要牌桌权威随机流，但随机流尚未初始化。");
			}
			string packSlotKey = ResolveResultSlotKey(action, intent.PackSlotKey, "卡包槽位");
			ActionSlotBinding binding = FindBinding(action.ContentId, bindings, packSlotKey);
			if (binding.CardIds.Count != 1)
			{
				throw new InvalidOperationException(
					$"打开卡包行动 {action.ContentId} 的槽位 {packSlotKey} 必须且只能绑定一张卡包卡。");
			}

			TabletopCardId packCardId = binding.CardIds[0];
			if (removalSet.Contains(packCardId) || !useSet.Add(packCardId))
			{
				throw new InvalidOperationException(
					$"打开卡包行动 {action.ContentId} 的结果重复修改牌桌卡牌 {packCardId}。");
			}
			if (!cards.TryGetCard(packCardId, out TabletopCard packCard) ||
				!contentIndex.TryGet(packCard.ContentId, out CardPackDefinition packDefinition))
			{
				throw new InvalidOperationException(
					$"打开卡包行动 {action.ContentId} 的参与卡牌 {packCardId} 不是有效卡包。");
			}
			int slotIndex = packDefinition.Slots.Count - packCard.RemainingUses;
			if (slotIndex < 0 || slotIndex >= packDefinition.Slots.Count)
			{
				throw new InvalidOperationException(
					$"卡包 {packDefinition.ContentId} 的剩余使用次数 {packCard.RemainingUses} 与 {packDefinition.Slots.Count} 个抽取槽位不一致。");
			}
			CardPackSlotDefinition slot = packDefinition.Slots[slotIndex] ??
				throw new InvalidOperationException(
					$"卡包 {packDefinition.ContentId} 的第 {slotIndex + 1} 个抽取槽位为空。");
			uses.Add(packCardId);

			if (slot.RecipeEntries.Count > 0 &&
				authoritativeRandom.NextFloat() < slot.RecipeChance)
			{
				List<CardPackRecipeEntry> availableRecipes = new List<CardPackRecipeEntry>();
				for (int i = 0; i < slot.RecipeEntries.Count; i++)
				{
					CardPackRecipeEntry recipe = slot.RecipeEntries[i];
					if (recipe != null && !isContentDiscovered(recipe.ActionId))
					{
						availableRecipes.Add(recipe);
					}
				}
				if (availableRecipes.Count > 0)
				{
					CardPackRecipeEntry selected =
						availableRecipes[authoritativeRandom.NextInt(availableRecipes.Count)];
					researchDiscoveries.Add(new ResearchDiscoverySpec(
						new[] { new ResearchDiscoveryEntrySpec(selected.ActionId, selected.RecipeCardId) },
						packCardId,
						allowAnchorStackSpawnAttach: true,
						spawnPresentationHeightOffset: StackCraftPackSpawnHeightOffset,
						useDragHeightForSpawn: true));
					return;
				}
			}

			int totalWeight = 0;
			for (int i = 0; i < slot.Entries.Count; i++)
			{
				CardPackEntry entry = slot.Entries[i] ??
					throw new InvalidOperationException(
						$"卡包 {packDefinition.ContentId} 的第 {slotIndex + 1} 个普通卡池包含空条目。");
				if (entry.Weight <= 0 || !contentIndex.TryGet(entry.CardId, out CardDefinition _))
				{
					throw new InvalidOperationException(
						$"卡包 {packDefinition.ContentId} 的第 {slotIndex + 1} 个普通卡池包含无效卡牌或权重。");
				}
				totalWeight = checked(totalWeight + entry.Weight);
			}
			if (totalWeight <= 0)
			{
				throw new InvalidOperationException(
					$"卡包 {packDefinition.ContentId} 的第 {slotIndex + 1} 个普通卡池没有可抽取内容。");
			}
			int roll = authoritativeRandom.NextInt(totalWeight);
			for (int i = 0; i < slot.Entries.Count; i++)
			{
				CardPackEntry entry = slot.Entries[i];
				if (roll < entry.Weight)
				{
					creations.Add(new CardCreationSpec(
						entry.CardId,
						1,
						packCardId,
						allowAnchorStackSpawnAttach: true,
						spawnPresentationHeightOffset: StackCraftPackSpawnHeightOffset,
						useDragHeightForSpawn: true));
					return;
				}
				roll -= entry.Weight;
			}
			throw new InvalidOperationException(
				$"卡包 {packDefinition.ContentId} 的第 {slotIndex + 1} 个权重抽取没有命中任何条目。");
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

		private readonly TabletopCardId[] m_useCardIds;

		private readonly CardCreationSpec[] m_creations;

		private readonly ResearchDiscoverySpec[] m_researchDiscoveries;

		private readonly PackPurchaseSpec[] m_packPurchases;

		private readonly ChestCurrencyChangeSpec[] m_chestCurrencyChanges;

		private readonly EquipCardSpec[] m_equipCards;

		private readonly UnequipCardSpec[] m_unequipCards;

		private readonly ContentId[] m_soldContentIds;

		private readonly ContentId[] m_exploredContentIds;

		internal IReadOnlyList<TabletopCardId> RemovalCardIds => m_removalCardIds;

		internal IReadOnlyList<TabletopCardId> UseCardIds => m_useCardIds;

		internal IReadOnlyList<CardCreationSpec> Creations => m_creations;

		internal IReadOnlyList<ResearchDiscoverySpec> ResearchDiscoveries => m_researchDiscoveries;

		internal IReadOnlyList<PackPurchaseSpec> PackPurchases => m_packPurchases;

		internal IReadOnlyList<ChestCurrencyChangeSpec> ChestCurrencyChanges => m_chestCurrencyChanges;

		internal IReadOnlyList<EquipCardSpec> EquipCards => m_equipCards;

		internal IReadOnlyList<UnequipCardSpec> UnequipCards => m_unequipCards;

		internal IReadOnlyList<ContentId> SoldContentIds => m_soldContentIds;

		internal IReadOnlyList<ContentId> ExploredContentIds => m_exploredContentIds;

		internal int TotalCreationCount { get; }

		internal ActionResultPlan(
			IReadOnlyList<TabletopCardId> removalCardIds,
			IReadOnlyList<TabletopCardId> useCardIds,
			IReadOnlyList<CardCreationSpec> creations,
			IReadOnlyList<ResearchDiscoverySpec> researchDiscoveries,
			IReadOnlyList<PackPurchaseSpec> packPurchases,
			IReadOnlyList<ChestCurrencyChangeSpec> chestCurrencyChanges,
			IReadOnlyList<EquipCardSpec> equipCards,
			IReadOnlyList<UnequipCardSpec> unequipCards,
			IReadOnlyList<ContentId> soldContentIds,
			IReadOnlyList<ContentId> exploredContentIds)
		{
			m_removalCardIds = new List<TabletopCardId>(removalCardIds ?? throw new ArgumentNullException("removalCardIds")).ToArray();
			m_useCardIds = new List<TabletopCardId>(useCardIds ?? throw new ArgumentNullException(nameof(useCardIds))).ToArray();
			m_creations = new List<CardCreationSpec>(creations ?? throw new ArgumentNullException("creations")).ToArray();
			m_researchDiscoveries = new List<ResearchDiscoverySpec>(
				researchDiscoveries ?? throw new ArgumentNullException(nameof(researchDiscoveries))).ToArray();
			m_packPurchases = new List<PackPurchaseSpec>(
				packPurchases ?? throw new ArgumentNullException(nameof(packPurchases))).ToArray();
			m_chestCurrencyChanges = new List<ChestCurrencyChangeSpec>(
				chestCurrencyChanges ?? throw new ArgumentNullException(nameof(chestCurrencyChanges))).ToArray();
			m_equipCards = new List<EquipCardSpec>(
				equipCards ?? throw new ArgumentNullException(nameof(equipCards))).ToArray();
			m_unequipCards = new List<UnequipCardSpec>(
				unequipCards ?? throw new ArgumentNullException(nameof(unequipCards))).ToArray();
			m_soldContentIds = new List<ContentId>(
				soldContentIds ?? throw new ArgumentNullException(nameof(soldContentIds))).ToArray();
			m_exploredContentIds = new List<ContentId>(
				exploredContentIds ?? throw new ArgumentNullException(nameof(exploredContentIds))).ToArray();
			int totalCreationCount = 0;
			for (int i = 0; i < m_creations.Length; i++)
			{
				totalCreationCount = checked(totalCreationCount + m_creations[i].Count);
			}
			TotalCreationCount = totalCreationCount;
		}
	}

	/// <summary>一次卡包商贩付款在行动开始时冻结的提交事实。</summary>
	internal readonly struct PackPurchaseSpec
	{
		internal TabletopCardId VendorCardId { get; }

		internal int ExpectedPaidAmount { get; }

		internal int PaymentAmount { get; }

		internal bool CompletesPurchase { get; }

		internal ContentId PackId { get; }

		internal PackPurchaseSpec(
			TabletopCardId vendorCardId,
			int expectedPaidAmount,
			int paymentAmount,
			bool completesPurchase,
			ContentId packId)
		{
			VendorCardId = vendorCardId;
			ExpectedPaidAmount = expectedPaidAmount;
			PaymentAmount = paymentAmount;
			CompletesPurchase = completesPurchase;
			PackId = packId;
		}
	}

	/// <summary>行动开始时冻结的箱子存币变化事实。</summary>
	internal readonly struct ChestCurrencyChangeSpec
	{
		internal TabletopCardId ChestCardId { get; }

		internal int ExpectedStoredCurrencyCount { get; }

		internal int Delta { get; }

		internal ChestCurrencyChangeSpec(
			TabletopCardId chestCardId,
			int expectedStoredCurrencyCount,
			int delta)
		{
			if (!chestCardId.IsValid)
			{
				throw new ArgumentException("箱子变化必须引用有效牌桌卡牌。", nameof(chestCardId));
			}
			if (expectedStoredCurrencyCount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(expectedStoredCurrencyCount));
			}
			if (delta == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(delta), delta, "箱子存币变化量不能为 0。");
			}
			ChestCardId = chestCardId;
			ExpectedStoredCurrencyCount = expectedStoredCurrencyCount;
			Delta = delta;
		}
	}

	/// <summary>行动开始时冻结的一次装备提交事实。</summary>
	internal readonly struct EquipCardSpec
	{
		internal TabletopCardId EquipmentCardId { get; }

		internal TabletopCardId CharacterCardId { get; }

		internal ContentId SlotId { get; }

		internal int GameplayEffectId { get; }

		internal TabletopCardSnapshot EquipmentSnapshot { get; }

		internal Vector2 ReturnPosition { get; }

		internal EquippedCardSnapshot ReplacedEquipmentSnapshot { get; }

		internal EquipCardSpec(
			TabletopCardId equipmentCardId,
			TabletopCardId characterCardId,
			ContentId slotId,
			int gameplayEffectId,
			TabletopCardSnapshot equipmentSnapshot,
			Vector2 returnPosition,
			EquippedCardSnapshot replacedEquipmentSnapshot)
		{
			if (!equipmentCardId.IsValid)
			{
				throw new ArgumentException("装备计划必须引用有效装备卡。", nameof(equipmentCardId));
			}
			if (!characterCardId.IsValid)
			{
				throw new ArgumentException("装备计划必须引用有效角色卡。", nameof(characterCardId));
			}
			if (!slotId.IsValid)
			{
				throw new ArgumentException("装备计划必须引用有效装备槽位。", nameof(slotId));
			}
			if (gameplayEffectId <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(gameplayEffectId));
			}
			EquipmentCardId = equipmentCardId;
			CharacterCardId = characterCardId;
			SlotId = slotId;
			GameplayEffectId = gameplayEffectId;
			EquipmentSnapshot = equipmentSnapshot ?? throw new ArgumentNullException(nameof(equipmentSnapshot));
			ReturnPosition = returnPosition;
			ReplacedEquipmentSnapshot = replacedEquipmentSnapshot;
		}
	}

	/// <summary>行动开始时冻结的一次卸装提交事实。</summary>
	internal readonly struct UnequipCardSpec
	{
		internal TabletopCardId CharacterCardId { get; }

		internal ContentId SlotId { get; }

		internal EquippedCardSnapshot EquipmentSnapshot { get; }

		internal Vector2 ReturnPosition { get; }

		internal UnequipCardSpec(
			TabletopCardId characterCardId,
			ContentId slotId,
			EquippedCardSnapshot equipmentSnapshot,
			Vector2 returnPosition)
		{
			if (!characterCardId.IsValid)
			{
				throw new ArgumentException("卸装计划必须引用有效角色卡。", nameof(characterCardId));
			}
			if (!slotId.IsValid)
			{
				throw new ArgumentException("卸装计划必须引用有效装备槽位。", nameof(slotId));
			}
			CharacterCardId = characterCardId;
			SlotId = slotId;
			EquipmentSnapshot = equipmentSnapshot ?? throw new ArgumentNullException(nameof(equipmentSnapshot));
			ReturnPosition = returnPosition;
		}
	}

	/// <summary>研究完成时可被选择的一项行动与对应配方卡。</summary>
	internal readonly struct ResearchDiscoveryEntrySpec
	{
		internal ContentId ActionId { get; }

		internal ContentId RecipeCardId { get; }

		internal ResearchDiscoveryEntrySpec(ContentId actionId, ContentId recipeCardId)
		{
			ActionId = actionId;
			RecipeCardId = recipeCardId;
		}
	}

	/// <summary>行动开始时冻结的研究候选池与配方卡生成位置。</summary>
	internal sealed class ResearchDiscoverySpec
	{
		private readonly ResearchDiscoveryEntrySpec[] m_entries;

		internal IReadOnlyList<ResearchDiscoveryEntrySpec> Entries => m_entries;

		internal TabletopCardId AnchorCardId { get; }

		internal bool AllowAnchorStackSpawnAttach { get; }

		internal float SpawnPresentationHeightOffset { get; }

		internal bool UseDragHeightForSpawn { get; }

		internal ResearchDiscoverySpec(
			IReadOnlyList<ResearchDiscoveryEntrySpec> entries,
			TabletopCardId anchorCardId,
			bool allowAnchorStackSpawnAttach = false,
			float spawnPresentationHeightOffset = 0f,
			bool useDragHeightForSpawn = false)
		{
			m_entries = new List<ResearchDiscoveryEntrySpec>(
				entries ?? throw new ArgumentNullException(nameof(entries))).ToArray();
			if (m_entries.Length == 0)
			{
				throw new ArgumentException("研究候选池不能为空。", nameof(entries));
			}
			if (!anchorCardId.IsValid)
			{
				throw new ArgumentException("研究结果位置必须引用有效牌桌卡牌。", nameof(anchorCardId));
			}
			if (!float.IsFinite(spawnPresentationHeightOffset) || spawnPresentationHeightOffset < 0f)
			{
				throw new ArgumentException(
					"研究结果卡牌生成表现高度偏移必须是大于等于 0 的有限值。",
					nameof(spawnPresentationHeightOffset));
			}
			AnchorCardId = anchorCardId;
			AllowAnchorStackSpawnAttach = allowAnchorStackSpawnAttach;
			SpawnPresentationHeightOffset = spawnPresentationHeightOffset;
			UseDragHeightForSpawn = useDragHeightForSpawn;
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

		internal bool CreateAsSingleStack { get; }

		internal Vector2 PositionOffset { get; }

		internal bool AllowAnchorStackSpawnAttach { get; }

		internal float SpawnPresentationHeightOffset { get; }

		internal bool UseDragHeightForSpawn { get; }

		internal CardCreationSpec(
			ContentId contentId,
			int count,
			TabletopCardId anchorCardId,
			bool createAsSingleStack = false,
			Vector2 positionOffset = default,
			bool allowAnchorStackSpawnAttach = false,
			float spawnPresentationHeightOffset = 0f,
			bool useDragHeightForSpawn = false)
		{
			if (float.IsNaN(positionOffset.x) || float.IsNaN(positionOffset.y) ||
				float.IsInfinity(positionOffset.x) || float.IsInfinity(positionOffset.y))
			{
				throw new ArgumentException("卡牌生成偏移必须是有限数值。", nameof(positionOffset));
			}
			if (!float.IsFinite(spawnPresentationHeightOffset) || spawnPresentationHeightOffset < 0f)
			{
				throw new ArgumentException(
					"卡牌生成表现高度偏移必须是大于等于 0 的有限值。",
					nameof(spawnPresentationHeightOffset));
			}
			ContentId = contentId;
			Count = count;
			AnchorCardId = anchorCardId;
			CreateAsSingleStack = createAsSingleStack;
			PositionOffset = positionOffset;
			AllowAnchorStackSpawnAttach = allowAnchorStackSpawnAttach;
			SpawnPresentationHeightOffset = spawnPresentationHeightOffset;
			UseDragHeightForSpawn = useDragHeightForSpawn;
		}
	}
}
