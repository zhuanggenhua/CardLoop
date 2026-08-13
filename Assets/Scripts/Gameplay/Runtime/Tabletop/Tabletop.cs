using System;
using System.Collections.Generic;
using GAS.Runtime;
using Gameplay.Actions;
using Gameplay.Tabletop.Actions;
using Gameplay.Content;
using Unity.Mathematics;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 普通牌桌行动采用回合推进或按回合规则换算的即时推进。
	/// </summary>
	public enum ActionProgressionMode
	{
		TurnBased = 0,
		RealTime = 10
	}

	/// <summary>
	/// 当前剧本单局的牌桌聚合根，统一拥有卡牌、牌堆、行动实例、权威随机和状态写入口。
	/// </summary>
	public sealed class Tabletop
	{
		private readonly ContentIndex m_contentIndex;
		private readonly Action<ContentId> m_actionCompleted;

		private readonly List<ActionInstance> m_activeActions = new List<ActionInstance>();
		private readonly List<ActionPlan> m_actionPlans = new List<ActionPlan>();
		private readonly IReadOnlyList<ActionPlan> m_readOnlyActionPlans;
		private readonly List<Battle> m_activeBattles = new List<Battle>();
		private readonly IReadOnlyList<Battle> m_readOnlyBattles;
		private readonly BattleFormation m_battleFormation;
		private ulong m_nextBattleId = 1uL;
		private ulong m_battleRevision;

		private float m_realTimeSecondsPerTurn;

		private Unity.Mathematics.Random m_authoritativeRandom;

		public TabletopCards Cards { get; }

		/// <summary>本局牌桌创建时冻结的唯一放置规则。</summary>
		public TabletopCardPlacementRules PlacementRules { get; }

		public IReadOnlyList<ActionInstance> ActiveActions => m_activeActions;

		/// <summary>当前牌桌中尚未提交的行动计划。</summary>
		public IReadOnlyList<ActionPlan> ActionPlans => m_readOnlyActionPlans;

		/// <summary>当前牌桌拥有的活动战斗；战斗状态不复制卡牌状态。</summary>
		public IReadOnlyList<Battle> ActiveBattles => m_readOnlyBattles;

		/// <summary>活动战斗关系变更版本，只供表现层判断是否需要重新投影，不承载第二份玩法状态。</summary>
		internal ulong BattleRevision => m_battleRevision;

		internal ContentIndex ContentIndex => m_contentIndex;

		public ActionProgressionMode ProgressionMode { get; private set; } = ActionProgressionMode.TurnBased;

		/// <summary>牌桌所属单局是否已经结束；结束后只允许读取最终状态与快照。</summary>
		public bool IsEnded { get; private set; }

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardPlacementRules placementRules,
			Action<ContentId> actionCompleted,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
		{
			m_contentIndex = contentIndex ?? throw new ArgumentNullException("contentIndex");
			PlacementRules = placementRules ?? throw new ArgumentNullException(nameof(placementRules));
			m_actionCompleted = actionCompleted ?? throw new ArgumentNullException(nameof(actionCompleted));
			Cards = new TabletopCards(cardIdSequence);
			m_readOnlyActionPlans = m_actionPlans.AsReadOnly();
			m_readOnlyBattles = m_activeBattles.AsReadOnly();
			m_battleFormation = battleFormationRules?.CreateRuntime();
		}

		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardStateSnapshot cardStateSnapshot,
			TabletopCardPlacementRules placementRules,
			Action<ContentId> actionCompleted,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
		{
			m_contentIndex = contentIndex ?? throw new ArgumentNullException("contentIndex");
			PlacementRules = placementRules ?? throw new ArgumentNullException(nameof(placementRules));
			m_actionCompleted = actionCompleted ?? throw new ArgumentNullException(nameof(actionCompleted));
			Cards = TabletopCards.Restore(
				cardStateSnapshot,
				cardIdSequence ?? throw new ArgumentNullException(nameof(cardIdSequence)));
			m_readOnlyActionPlans = m_actionPlans.AsReadOnly();
			Cards.RequireValidPlacement(PlacementRules);
			m_readOnlyBattles = m_activeBattles.AsReadOnly();
			m_battleFormation = battleFormationRules?.CreateRuntime();
			for (int stackIndex = 0; stackIndex < Cards.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = Cards.Stacks[stackIndex];
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					TabletopCard card = stack.Cards[cardIndex];
					RequireCardDefinition(card.ContentId, $"恢复牌桌卡牌 {card.Id}");
				}
			}
		}

		/// <summary>
		/// 通过已恢复的卡牌状态和活动行动快照重建牌桌；恢复失败时不会发布任何活动行动。
		/// </summary>
		internal Tabletop(
			ContentIndex contentIndex,
			TabletopCardStateSnapshot cardStateSnapshot,
			TabletopCardPlacementRules placementRules,
			IReadOnlyList<ActionInstanceSnapshot> actionSnapshots,
			Action<ContentId> actionCompleted,
			BattleFormationRules battleFormationRules = null,
			TabletopCardIdSequence cardIdSequence = null)
			: this(contentIndex, cardStateSnapshot, placementRules, actionCompleted, battleFormationRules, cardIdSequence)
		{
			RestoreActiveActions(actionSnapshots);
		}

		public TabletopCard CreateCard(ContentId contentId, Vector2 position, bool isPlacementLocked = false)
		{
			RequireActive();
			CardDefinition definition = RequireCardDefinition(contentId, "创建卡牌");
			return definition is CharacterCardDefinition characterDefinition
				? Cards.CreateCharacterCard(
					contentId,
					position,
					characterDefinition.CreateAbilitySystemConfig(),
					PlacementRules,
					isPlacementLocked)
				: Cards.CreateCard(contentId, position, PlacementRules, isPlacementLocked);
		}

		public void RemoveCard(TabletopCardId cardId)
		{
			RequireActive();
			if (!Cards.TryGetCard(cardId, out TabletopCard card))
			{
				throw new KeyNotFoundException($"牌桌中不存在局内卡牌 {cardId}。");
			}
			if (TryFindBattleContaining(cardId, out _))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 仍属于活动战斗，必须先离开战斗或结束战斗后才能移除。");
			}
			UnbindCardFromActionPlans(cardId);
			Cards.RemoveCard(cardId);
			if (card is CharacterCard characterCard)
			{
				characterCard.Dispose();
			}
		}

		/// <summary>
		/// 将当前地区中的卡牌实例迁移到另一个地区牌桌。迁移保留原卡牌对象与角色 GAS 状态。
		/// </summary>
		internal void TransferCardsTo(
			Tabletop target,
			IReadOnlyList<TabletopCardId> cardIds,
			IReadOnlyList<Vector2> targetPositions)
		{
			RequireCardsCanTransferTo(target, cardIds, targetPositions);
			Cards.TransferCardsTo(target.Cards, cardIds, targetPositions, target.PlacementRules);
		}

		internal void RequireCardsCanTransferTo(
			Tabletop target,
			IReadOnlyList<TabletopCardId> cardIds,
			IReadOnlyList<Vector2> targetPositions)
		{
			RequireActive();
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}
			target.RequireActive();
			if (ReferenceEquals(this, target))
			{
				throw new InvalidOperationException("旅行目标必须是另一个剧本地区。");
			}
			if (cardIds == null)
			{
				throw new ArgumentNullException(nameof(cardIds));
			}
			if (targetPositions == null || targetPositions.Count != cardIds.Count)
			{
				throw new ArgumentException("旅行卡牌与目标位置数量必须一致。", nameof(targetPositions));
			}

			List<TabletopCard> cards = new List<TabletopCard>(cardIds.Count);
			HashSet<TabletopCardId> uniqueIds = new HashSet<TabletopCardId>();
			for (int i = 0; i < cardIds.Count; i++)
			{
				TabletopCardId cardId = cardIds[i];
				if (!uniqueIds.Add(cardId))
				{
					throw new InvalidOperationException($"旅行卡牌列表重复引用局内卡牌 {cardId}。");
				}
				if (!Cards.TryGetCard(cardId, out TabletopCard card))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 不属于当前地区牌桌。");
				}
				if (TryFindBattleContaining(cardId, out _))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 仍属于活动战斗，不能离开当前地区。");
				}
				if (IsActiveActionParticipant(cardId))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 仍参与活动行动，不能离开当前地区。");
				}
				if (IsActionPlanParticipant(cardId))
				{
					throw new InvalidOperationException($"旅行卡牌 {cardId} 仍填在待确认行动计划中，不能离开当前地区。");
				}
				cards.Add(card);
			}

			target.Cards.RequireCanAcceptCards(cards, targetPositions, target.PlacementRules);
		}

		internal void RequireCardChangesCanBeCommitted(
			IReadOnlyList<TabletopCardId> removalCardIds,
			IReadOnlyList<ContentId> creationContentIds,
			IReadOnlyList<Vector2> creationPositions)
		{
			RequireActive();
			if (removalCardIds == null)
			{
				throw new ArgumentNullException(nameof(removalCardIds));
			}
			if (creationContentIds == null)
			{
				throw new ArgumentNullException(nameof(creationContentIds));
			}
			if (creationPositions == null || creationPositions.Count != creationContentIds.Count)
			{
				throw new ArgumentException("卡牌产物内容与位置数量必须一致。", nameof(creationPositions));
			}

			for (int i = 0; i < removalCardIds.Count; i++)
			{
				TabletopCardId cardId = removalCardIds[i];
				if (!Cards.TryGetCard(cardId, out _))
				{
					throw new InvalidOperationException($"牌桌变更引用了不存在的局内卡牌 {cardId}。");
				}
				if (TryFindBattleContaining(cardId, out _))
				{
					throw new InvalidOperationException(
						$"牌桌卡牌 {cardId} 仍属于活动战斗，不能提交移除它的行动结果。");
				}
			}
			for (int i = 0; i < creationContentIds.Count; i++)
			{
				RequireCardDefinition(creationContentIds[i], "创建行动产物");
			}
			Cards.RequireCardChangesCanBePlaced(removalCardIds, creationPositions, PlacementRules);
		}

		/// <summary>
		/// 创建活动战斗并冻结本次战斗方分组。角色阵营与敌我关系必须在调用前由正式规则解析，
		/// 牌桌只保存这场战斗的临时分组，不复制 GAS 阵营标签。
		/// </summary>
		public Battle StartBattle(params IReadOnlyList<TabletopCardId>[] sideRosters)
		{
			RequireActive();
			if (m_nextBattleId == ulong.MaxValue)
			{
				throw new InvalidOperationException("本局牌桌战斗 ID 已耗尽。");
			}

			if (m_battleFormation == null)
			{
				throw new InvalidOperationException("当前剧本没有配置战斗阵型规则，不能创建战斗。");
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("本局牌桌尚未初始化权威随机流，不能创建战斗。");
			}

			Unity.Mathematics.Random candidateRandom = m_authoritativeRandom;
			uint battleSeed = candidateRandom.NextUInt(1u, uint.MaxValue);
			Battle battle = new Battle(new BattleId(m_nextBattleId), sideRosters, battleSeed);
			m_battleFormation.ValidateBattle(battle);
			for (int sideIndex = 0; sideIndex < battle.Sides.Count; sideIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = battle.Sides[sideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					TabletopCardId cardId = cardIds[cardIndex];
					if (!Cards.TryGetCard(cardId, out _))
					{
						throw new InvalidOperationException(
							$"战斗参战对象 {cardId} 不属于当前牌桌，不能创建战斗。");
					}
					if (Cards.TryGetCard(cardId, out TabletopCard participantCard) &&
						participantCard is not CharacterCard)
					{
						throw new InvalidOperationException(
							$"战斗参战对象 {cardId} 不是拥有唯一 EX-GAS 状态的角色卡，不能加入战斗。");
					}
					if (TryFindBattleContaining(cardId, out _))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardId} 已属于活动战斗，不能重复加入战斗。");
					}
				}
			}

			m_activeBattles.Add(battle);
			m_authoritativeRandom = candidateRandom;
			m_nextBattleId++;
			m_battleRevision++;
			return battle;
		}

		/// <summary>从所属战斗移除一张牌；只剩一个有成员的战斗方时，战斗随之结束。</summary>
		public void LeaveBattle(Battle battle, TabletopCardId cardId)
		{
			RequireActive();
			RequireActiveBattle(battle);
			battle.RemoveParticipant(cardId);
			m_battleRevision++;
			if (battle.ParticipantCount < 2 || battle.ActiveSideCount < 2)
			{
				EndBattle(battle);
			}
		}

		/// <summary>结束并移除一场活动战斗。</summary>
		public void EndBattle(Battle battle)
		{
			RequireActive();
			RequireActiveBattle(battle);
			battle.End();
			m_activeBattles.Remove(battle);
			m_battleRevision++;
		}

		/// <summary>
		/// 请求一张参战角色卡向本场另一参战角色卡激活已有 EX-GAS Ability。
		/// 标签、消耗、冷却、Timeline 和效果结算仍完全由 EX-GAS 负责。
		/// </summary>
		public AbilityActivationResult RequestBattleAbilityActivation(
			Battle battle,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			int abilityCode)
		{
			RequireActive();
			RequireActiveBattle(battle);
			CharacterCard source = RequireBattleCharacter(battle, sourceCardId, "施法者");
			CharacterCard target = RequireBattleCharacter(battle, targetCardId, "目标");
			AbilitySpec ability = source.AbilitySystem.GetAbilitySpec(abilityCode)
				?? throw new InvalidOperationException(
					$"参战角色卡 {sourceCardId} 没有 Ability {abilityCode}，不能提交战斗能力请求。");

			AbilityActivationResult result = ability.CheckActivation();
			if (result != AbilityActivationResult.Success)
			{
				return result;
			}

			Vector2 sourcePosition = source.Position;
			ability.TryActivate(new AbilityActivationContext(
				new Vector3(sourcePosition.x, sourcePosition.y, 0f),
				target.AbilitySystem,
				battle.TakeAbilityActivationSeed()));
			return result;
		}

		public TabletopCardStack MergeStackOnto(TabletopCardId sourceCardId, TabletopCardId targetCardId)
		{
			RequireActive();
			RequireNoBattleParticipantInAffectedStack(sourceCardId, "合并牌堆");
			RequireNoBattleParticipantInAffectedStack(targetCardId, "合并牌堆");
			return Cards.MergeStackOnto(sourceCardId, targetCardId);
		}

		public TabletopCardStack DetachStackAt(TabletopCardId cardId)
		{
			RequireActive();
			RequireNoBattleParticipantInDetachedTail(cardId, "拆分牌堆");
			return Cards.DetachStackAt(cardId);
		}

		public bool TryPlaceStack(TabletopCardId cardId, Vector2 position, out TabletopCardStack placedStack)
		{
			RequireActive();
			RequireNoBattleParticipantInDetachedTail(cardId, "放置牌堆");
			return Cards.TryPlaceStack(cardId, position, PlacementRules, out placedStack);
		}

		/// <summary>
		/// 为视图层查询参与战斗卡牌的派生姿态；牌桌卡牌的权威位置仍保存在各自牌堆。
		/// </summary>
		internal bool TryGetBattlePose(
			TabletopCardId cardId,
			int baseSortingOrder,
			out TabletopCardPose pose)
		{
			for (int battleIndex = 0; battleIndex < m_activeBattles.Count; battleIndex++)
			{
				Battle battle = m_activeBattles[battleIndex];
				if (battle.HasParticipant(cardId))
				{
					if (m_battleFormation == null)
					{
						throw new InvalidOperationException("活动战斗缺少剧本战斗阵型规则。");
					}
					return m_battleFormation.TryCalculatePose(
						battle,
						Cards,
						cardId,
						baseSortingOrder,
						out pose);
				}
			}

			pose = default;
			return false;
		}

		internal ActionCandidate[] FindCandidates(TabletopCardPointerReleaseIntent intent, IReadOnlyList<ActionDefinition> availableActions)
		{
			RequireActive();
			return ActionCandidateResolver.FindCandidates(intent, Cards, m_contentIndex, availableActions);
		}

		internal ActionInstance StartAction(ActionRequest request)
		{
			RequireActive();
			if (request == null)
			{
				throw new ArgumentNullException("request");
			}
			return StartActionInstance(CreateCandidateFromRequest(request));
		}

		public ActionPlan CreateActionPlan(ActionCandidate candidate)
		{
			RequireActive();
			if (candidate == null)
			{
				throw new ArgumentNullException(nameof(candidate));
			}
			if (!m_contentIndex.TryGet(candidate.Action.ContentId, out ActionDefinition action) ||
				!ReferenceEquals(action, candidate.Action))
			{
				throw new InvalidOperationException(
					$"行动候选 {candidate.Action.ContentId} 不属于当前牌桌的内容索引。");
			}

			ActionPlan plan = new ActionPlan(candidate);
			ValidateActionPlan(plan, requireComplete: false);
			m_actionPlans.Add(plan);
			return plan;
		}

		public void AddCardToActionPlan(
			ActionPlan plan,
			string slotKey,
			TabletopCardId cardId)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			ActionPlanBinding targetBinding = plan.GetBinding(slotKey);
			if (!Cards.TryGetCard(cardId, out TabletopCard card) ||
				!m_contentIndex.TryGet(card.ContentId, out ContentAsset contentAsset))
			{
				throw new InvalidOperationException($"牌桌中不存在可填入行动计划的卡牌 {cardId}。");
			}
			for (int bindingIndex = 0; bindingIndex < plan.Bindings.Count; bindingIndex++)
			{
				IReadOnlyList<TabletopCardId> existingIds = plan.Bindings[bindingIndex].CardIds;
				for (int cardIndex = 0; cardIndex < existingIds.Count; cardIndex++)
				{
					if (existingIds[cardIndex] == cardId)
					{
						throw new InvalidOperationException(
							$"行动计划 {plan.ActionId} 已经绑定牌桌卡牌 {cardId}。");
					}
				}
			}
			if (targetBinding.Slot.MaximumParticipants > 0 &&
				targetBinding.CardIds.Count >= targetBinding.Slot.MaximumParticipants)
			{
				throw new InvalidOperationException(
					$"行动计划 {plan.ActionId} 的槽位 {slotKey} 已达到参与上限。");
			}
			AbilitySystemCell abilitySystem = card is CharacterCard character
				? character.AbilitySystem
				: null;
			if (!ActionParticipationEvaluator.MatchesParticipant(
					targetBinding.Slot,
					contentAsset,
					abilitySystem))
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {cardId} 不满足行动计划 {plan.ActionId} 的槽位 {slotKey} 条件。");
			}

			targetBinding.Add(cardId);
		}

		public void RemoveCardFromActionPlan(
			ActionPlan plan,
			string slotKey,
			TabletopCardId cardId)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			plan.GetBinding(slotKey).Remove(cardId);
		}

		public ActionInstance SubmitActionPlan(ActionPlan plan)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			ValidateActionPlan(plan, requireComplete: true);
			ActionInstance instance = StartAction(plan.CreateRequest());
			m_actionPlans.Remove(plan);
			return instance;
		}

		public void CancelActionPlan(ActionPlan plan)
		{
			RequireActive();
			RequireOwnedActionPlan(plan);
			m_actionPlans.Remove(plan);
		}

		public ActionInstanceSnapshot[] CreateActiveActionSnapshots()
		{
			ActionInstanceSnapshot[] snapshots = new ActionInstanceSnapshot[m_activeActions.Count];
			for (int i = 0; i < m_activeActions.Count; i++)
			{
				snapshots[i] = m_activeActions[i].CreateSnapshot();
			}
			return snapshots;
		}

		internal TabletopSnapshot CreateSnapshot()
		{
			if (m_activeBattles.Count > 0)
			{
				throw new InvalidOperationException("牌桌仍有活动战斗；本游戏不保存战斗中状态，必须先结束战斗再存档。");
			}
			if (m_authoritativeRandom.state == 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流尚未初始化，不能生成可继续运行的快照。");
			}
			return new TabletopSnapshot(
				Cards.CreateSnapshot(),
				CreateActiveActionSnapshots(),
				m_authoritativeRandom.state);
		}

		internal uint AuthoritativeRandomState => m_authoritativeRandom.state;

		internal void RestoreAuthoritativeRandom(uint state)
		{
			if (state == 0u)
			{
				throw new InvalidOperationException("牌桌快照的权威随机状态不能为 0。");
			}
			if (m_authoritativeRandom.state != 0u)
			{
				throw new InvalidOperationException("牌桌权威随机流已经初始化，不能重复恢复。");
			}
			m_authoritativeRandom.state = state;
		}

		internal void InitializeAuthoritativeRandom(uint seed)
		{
			RequireActive();
			if (seed == 0)
			{
				throw new ArgumentOutOfRangeException("seed", "权威随机种子不能为 0。");
			}
			if (m_authoritativeRandom.state != 0)
			{
				throw new InvalidOperationException("本局牌桌行动的权威随机流已经初始化，不能重置。");
			}
			if (m_activeActions.Count > 0)
			{
				throw new InvalidOperationException("存在活动行动时不能初始化权威随机流。");
			}
			m_authoritativeRandom = new Unity.Mathematics.Random(seed);
		}

		internal void UseRealTimeProgression(float secondsPerTurn)
		{
			RequireActive();
			m_realTimeSecondsPerTurn = ValidateSecondsPerTurn(secondsPerTurn);
			ProgressionMode = ActionProgressionMode.RealTime;
		}

		internal void UseTurnBasedProgression()
		{
			RequireActive();
			ProgressionMode = ActionProgressionMode.TurnBased;
			m_realTimeSecondsPerTurn = 0f;
		}

		public void PauseAction(ActionInstance action)
		{
			RequireActive();
			RequireActiveAction(action);
			action.Pause();
		}

		public void ResumeAction(ActionInstance action)
		{
			RequireActive();
			RequireActiveAction(action);
			action.Resume();
		}

		public void CancelAction(ActionInstance action)
		{
			RequireActive();
			RequireActiveAction(action);
			action.Cancel(ActionCancellationReason.Requested);
			m_activeActions.Remove(action);
		}

		internal void AdvanceConfirmedTurn()
		{
			RequireActive();
			if (ProgressionMode == ActionProgressionMode.TurnBased)
			{
				AdvanceActiveActions(1f);
			}
		}

		internal void AdvanceRealTime(float deltaSeconds)
		{
			RequireActive();
			if (ProgressionMode == ActionProgressionMode.RealTime)
			{
				if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
				{
					throw new ArgumentOutOfRangeException("deltaSeconds", deltaSeconds, "即时行动推进秒数必须是大于或等于 0 的有限值。");
				}
				if (deltaSeconds != 0f)
				{
					AdvanceActiveActions(deltaSeconds / ValidateSecondsPerTurn(m_realTimeSecondsPerTurn));
				}
			}
		}

		/// <summary>结束牌桌并取消尚未结算的行动；终止后不允许再次写入。</summary>
		internal void End()
		{
			RequireActive();
			for (int i = m_activeBattles.Count - 1; i >= 0; i--)
			{
				m_activeBattles[i].End();
			}
			m_activeBattles.Clear();
			m_battleRevision++;
			for (int i = m_activeActions.Count - 1; i >= 0; i--)
			{
				m_activeActions[i].Cancel(ActionCancellationReason.ScenarioEnded);
			}
			m_activeActions.Clear();
			m_actionPlans.Clear();
			DisposeCharacters();
			m_realTimeSecondsPerTurn = 0f;
			m_authoritativeRandom = default(Unity.Mathematics.Random);
			ProgressionMode = ActionProgressionMode.TurnBased;
			IsEnded = true;
		}

		private void AdvanceActiveActions(float turnUnits)
		{
			for (int i = m_activeActions.Count - 1; i >= 0; i--)
			{
				ActionInstance action = m_activeActions[i];
				if (!AreActionParticipantsValid(action))
				{
					action.Cancel(ActionCancellationReason.ParticipantInvalidated);
					m_activeActions.RemoveAt(i);
				}
				else
				{
					action.Advance(turnUnits);
					if (action.State == ActionInstanceState.Completed)
					{
						m_activeActions.RemoveAt(i);
						CommitCompletedAction(action);
					}
				}
			}
		}

		private void CommitCompletedAction(ActionInstance action)
		{
			ActionResultSettlement.Commit(action, this);
			m_actionCompleted(action.ActionId);
		}

		private string SelectResultBranch(ActionDefinition action)
		{
			if (action.ResultBranches.Count == 0)
			{
				return string.Empty;
			}
			if (m_authoritativeRandom.state == 0)
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 声明了随机结果分支，但本局牌桌行动尚未初始化权威随机流。");
			}
			uint totalWeight = 0u;
			for (int i = 0; i < action.ResultBranches.Count; i++)
			{
				ActionResultBranchDefinition branch = action.ResultBranches[i];
				if (branch == null)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 包含空的随机结果分支。");
				}
				if (string.IsNullOrWhiteSpace(branch.Key))
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支缺少分支键。");
				}
				if (branch.Weight <= 0)
				{
					throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支 {branch.Key} 权重必须大于 0，当前值为 {branch.Weight}。");
				}
				for (int previousIndex = 0; previousIndex < i; previousIndex++)
				{
					ActionResultBranchDefinition previousBranch = action.ResultBranches[previousIndex];
					if (previousBranch != null && string.Equals(previousBranch.Key, branch.Key, StringComparison.Ordinal))
					{
						throw new InvalidOperationException($"行动 {action.ContentId} 的随机结果分支键重复：{branch.Key}。");
					}
				}
				totalWeight = checked(totalWeight + (uint)branch.Weight);
			}
			uint roll = m_authoritativeRandom.NextUInt(totalWeight);
			for (int j = 0; j < action.ResultBranches.Count; j++)
			{
				ActionResultBranchDefinition branch2 = action.ResultBranches[j];
				uint branchWeight = (uint)branch2.Weight;
				if (roll < branchWeight)
				{
					return branch2.Key;
				}
				roll -= branchWeight;
			}
			throw new InvalidOperationException($"行动 {action.ContentId} 的权威随机结果没有命中任何分支。");
		}

		private void RequireActiveAction(ActionInstance action)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			if (!m_activeActions.Contains(action))
			{
				throw new InvalidOperationException($"行动实例 {action.ActionId} 不属于当前单局牌桌的活动集合。");
			}
		}

		private void RequireActiveBattle(Battle battle)
		{
			if (battle == null)
			{
				throw new ArgumentNullException(nameof(battle));
			}
			if (!m_activeBattles.Contains(battle))
			{
				throw new InvalidOperationException("指定战斗不属于当前牌桌的活动战斗集合。");
			}
		}

		private CharacterCard RequireBattleCharacter(
			Battle battle,
			TabletopCardId cardId,
			string role)
		{
			if (!battle.HasParticipant(cardId))
			{
				throw new InvalidOperationException(
					$"战斗能力的{role}卡牌 {cardId} 不属于战斗 {battle.Id}。");
			}
			if (!Cards.TryGetCard(cardId, out TabletopCard card) || card is not CharacterCard character)
			{
				throw new InvalidOperationException(
					$"战斗 {battle.Id} 的{role}卡牌 {cardId} 不再是当前牌桌中的角色卡，战斗状态已损坏。");
			}
			return character;
		}

		private void RestoreActiveActions(IReadOnlyList<ActionInstanceSnapshot> snapshots)
		{
			if (snapshots == null)
			{
				throw new ArgumentNullException(nameof(snapshots));
			}

			List<ActionInstance> restoredActions = new List<ActionInstance>(snapshots.Count);
			for (int snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
			{
				ActionInstanceSnapshot snapshot = snapshots[snapshotIndex];
				if (snapshot == null)
				{
					throw new InvalidOperationException($"活动行动快照的第 {snapshotIndex} 项为空。");
				}

				ActionRequest request = ActionRequest.FromSnapshot(snapshot);
				ActionCandidate candidate = CreateCandidateFromRequest(request);
				ActionInstance action = ActionInstance.Restore(candidate, snapshot);
				ActionResultSettlement.ValidateRestoredPlan(action, this);
				restoredActions.Add(action);
			}

			m_activeActions.AddRange(restoredActions);
		}

		private void DisposeCharacters()
		{
			for (int stackIndex = 0; stackIndex < Cards.Stacks.Count; stackIndex++)
			{
				TabletopCardStack stack = Cards.Stacks[stackIndex];
				for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
				{
					if (stack.Cards[cardIndex] is CharacterCard characterCard)
					{
						characterCard.Dispose();
					}
				}
			}
		}

		private void RequireNoBattleParticipantInAffectedStack(TabletopCardId cardId, string operation)
		{
			TabletopCardStack stack = Cards.GetStackContaining(cardId);
			for (int index = 0; index < stack.Cards.Count; index++)
			{
				if (TryFindBattleContaining(stack.Cards[index].Id, out _))
				{
					throw new InvalidOperationException(
						$"{operation}会改变活动战斗参与牌 {stack.Cards[index].Id} 的位置或堆栈关系。必须先通过战斗命令离开或结束战斗。");
				}
			}
		}

		private void RequireNoBattleParticipantInDetachedTail(TabletopCardId cardId, string operation)
		{
			TabletopCardStack stack = Cards.GetStackContaining(cardId);
			int startIndex = stack.IndexOf(cardId);
			for (int index = startIndex; index < stack.Cards.Count; index++)
			{
				if (TryFindBattleContaining(stack.Cards[index].Id, out _))
				{
					throw new InvalidOperationException(
						$"{operation}会移动活动战斗参与牌 {stack.Cards[index].Id}。必须先通过战斗命令离开或结束战斗。");
				}
			}
		}

		private bool TryFindBattleContaining(TabletopCardId cardId, out Battle battle)
		{
			for (int i = 0; i < m_activeBattles.Count; i++)
			{
				Battle candidate = m_activeBattles[i];
				if (candidate.HasParticipant(cardId))
				{
					battle = candidate;
					return true;
				}
			}

			battle = null;
			return false;
		}

		private bool IsActiveActionParticipant(TabletopCardId cardId)
		{
			for (int actionIndex = 0; actionIndex < m_activeActions.Count; actionIndex++)
			{
				ActionInstance action = m_activeActions[actionIndex];
				for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
				{
					IReadOnlyList<TabletopCardId> participantIds = action.Bindings[bindingIndex].CardIds;
					for (int cardIndex = 0; cardIndex < participantIds.Count; cardIndex++)
					{
						if (participantIds[cardIndex] == cardId)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private bool IsActionPlanParticipant(TabletopCardId cardId)
		{
			for (int planIndex = 0; planIndex < m_actionPlans.Count; planIndex++)
			{
				IReadOnlyList<ActionPlanBinding> bindings = m_actionPlans[planIndex].Bindings;
				for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
				{
					IReadOnlyList<TabletopCardId> cardIds = bindings[bindingIndex].CardIds;
					for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
					{
						if (cardIds[cardIndex] == cardId)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private void UnbindCardFromActionPlans(TabletopCardId cardId)
		{
			for (int planIndex = 0; planIndex < m_actionPlans.Count; planIndex++)
			{
				IReadOnlyList<ActionPlanBinding> bindings = m_actionPlans[planIndex].Bindings;
				for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
				{
					ActionPlanBinding binding = bindings[bindingIndex];
					for (int cardIndex = binding.CardIds.Count - 1; cardIndex >= 0; cardIndex--)
					{
						if (binding.CardIds[cardIndex] == cardId)
						{
							binding.Remove(cardId);
						}
					}
				}
			}
		}

		private bool AreActionParticipantsValid(ActionInstance action)
		{
			for (int bindingIndex = 0; bindingIndex < action.Bindings.Count; bindingIndex++)
			{
				ActionSlotBinding binding = action.Bindings[bindingIndex];
				if (!AreSlotParticipantsValid(binding.Slot, binding.CardIds))
				{
					return false;
				}
			}
			return true;
		}

		private void RequireOwnedActionPlan(ActionPlan plan)
		{
			if (plan == null)
			{
				throw new ArgumentNullException(nameof(plan));
			}
			if (!m_actionPlans.Contains(plan))
			{
				throw new InvalidOperationException(
					$"行动计划 {plan.ActionId} 不属于当前牌桌。");
			}
		}

		private void ValidateActionPlan(ActionPlan plan, bool requireComplete)
		{
			if (plan.Bindings.Count != plan.Action.ParticipationSlots.Count)
			{
				throw new InvalidOperationException(
					$"行动计划 {plan.ActionId} 的槽位数量与作者定义不一致。");
			}
			for (int i = 0; i < plan.Bindings.Count; i++)
			{
				ActionPlanBinding binding = plan.Bindings[i];
				if (!ReferenceEquals(binding.Slot, plan.Action.ParticipationSlots[i]))
				{
					throw new InvalidOperationException(
						$"行动计划 {plan.ActionId} 的第 {i + 1} 个槽位与作者定义不一致。");
				}
				if (requireComplete && !AreSlotParticipantsValid(binding.Slot, binding.CardIds))
				{
					throw new InvalidOperationException(
						$"行动计划 {plan.ActionId} 的槽位 {binding.Slot.Key} 尚未完整或参与对象已失效。");
				}
			}
		}

		private ActionCandidate CreateCandidateFromRequest(ActionRequest request)
		{
			if (!m_contentIndex.TryGet(request.ActionId, out ActionDefinition action))
			{
				throw new InvalidOperationException($"行动请求引用的行动 {request.ActionId} 不在当前内容索引中。");
			}
			Dictionary<string, ActionRequestBinding> requestBindings = new Dictionary<string, ActionRequestBinding>(StringComparer.Ordinal);
			HashSet<TabletopCardId> usedCards = new HashSet<TabletopCardId>();
			for (int i = 0; i < request.Bindings.Count; i++)
			{
				ActionRequestBinding requestBinding = request.Bindings[i];
				if (requestBindings.ContainsKey(requestBinding.SlotKey))
				{
					throw new InvalidOperationException($"行动请求 {request.ActionId} 重复提交槽位 {requestBinding.SlotKey}。");
				}
				requestBindings.Add(requestBinding.SlotKey, requestBinding);
				for (int cardIndex = 0; cardIndex < requestBinding.CardIds.Count; cardIndex++)
				{
					TabletopCardId cardId = requestBinding.CardIds[cardIndex];
					if (!cardId.IsValid)
					{
						throw new InvalidOperationException($"行动请求 {request.ActionId} 的槽位 {requestBinding.SlotKey} 包含无效卡牌 ID。");
					}
					if (!usedCards.Add(cardId))
					{
						throw new InvalidOperationException($"行动请求 {request.ActionId} 重复绑定牌桌卡牌 {cardId}。");
					}
				}
			}
			List<ActionSlotBinding> bindings = new List<ActionSlotBinding>(action.ParticipationSlots.Count);
			for (int slotIndex = 0; slotIndex < action.ParticipationSlots.Count; slotIndex++)
			{
				ActionSlotDefinition slot = action.ParticipationSlots[slotIndex];
				if (!requestBindings.Remove(slot.Key, out var requestBinding2))
				{
					throw new InvalidOperationException($"行动请求 {request.ActionId} 缺少参与槽位 {slot.Key}。");
				}
				bindings.Add(new ActionSlotBinding(slot, requestBinding2.CardIds));
			}
			if (requestBindings.Count > 0)
			{
				using Dictionary<string, ActionRequestBinding>.KeyCollection.Enumerator enumerator = requestBindings.Keys.GetEnumerator();
				if (enumerator.MoveNext())
				{
					string unknownSlotKey = enumerator.Current;
					throw new InvalidOperationException($"行动请求 {request.ActionId} 包含当前作者源不存在的槽位 {unknownSlotKey}。");
				}
			}
			for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
			{
				ActionSlotBinding binding = bindings[bindingIndex];
				if (!AreSlotParticipantsValid(binding.Slot, binding.CardIds))
				{
					throw new InvalidOperationException(
						$"行动请求 {request.ActionId} 的槽位 {binding.Slot.Key} 参与对象已变化或不满足当前条件。");
				}
			}
			return new ActionCandidate(action, bindings, 0);
		}

		private ActionInstance StartActionInstance(ActionCandidate candidate)
		{
			int turnCost = candidate.Action.TurnCost;
			string resultBranchKey = SelectResultBranch(candidate.Action);
			ActionResultPlan resultPlan = ActionResultSettlement.Compile(candidate.Action, candidate, resultBranchKey, m_contentIndex);
			ActionInstance action = new ActionInstance(candidate, turnCost, resultBranchKey, resultPlan);
			if (action.State == ActionInstanceState.Running)
			{
				m_activeActions.Add(action);
			}
			else
			{
				CommitCompletedAction(action);
			}
			return action;
		}

		private bool AreSlotParticipantsValid(
			ActionSlotDefinition slot,
			IReadOnlyList<TabletopCardId> cardIds)
		{
			if (!ActionParticipationEvaluator.IsParticipantCountSatisfied(slot, cardIds.Count))
			{
				return false;
			}
			for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
			{
				TabletopCardId cardId = cardIds[cardIndex];
				if (!Cards.TryGetCard(cardId, out var card) || !m_contentIndex.TryGet(card.ContentId, out var contentAsset))
				{
					return false;
				}
				AbilitySystemCell abilitySystemCell = card is CharacterCard characterCard
					? characterCard.AbilitySystem
					: null;
				if (!ActionParticipationEvaluator.MatchesParticipant(slot, contentAsset, abilitySystemCell))
				{
					return false;
				}
			}
			return true;
		}

		private CardDefinition RequireCardDefinition(ContentId contentId, string operation)
		{
			if (!m_contentIndex.TryGet(contentId, out CardDefinition definition))
			{
				throw new InvalidOperationException($"{operation}引用的内容 {contentId} 缺失或不是卡牌定义。");
			}
			return definition;
		}

		private void RequireActive()
		{
			if (IsEnded)
			{
				throw new InvalidOperationException("牌桌所属剧本已经结束，不能再修改牌桌或执行行动。");
			}
		}

		private static float ValidateSecondsPerTurn(float secondsPerTurn)
		{
			if (!float.IsFinite(secondsPerTurn) || secondsPerTurn <= 0f)
			{
				throw new InvalidOperationException($"普通行动的每回合秒数必须是大于 0 的有限值，当前值为 {secondsPerTurn}。");
			}
			return secondsPerTurn;
		}
	}
}
