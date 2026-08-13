using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Unity.Mathematics;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 一次剧本运行的普通 C# 聚合，统一拥有地区、回合、内容发现和任务日志。
	/// </summary>
	public sealed class ScenarioRun
	{
		private readonly ContentIndex m_contentIndex;
		private readonly Dictionary<ContentId, ScenarioRegion> m_regions =
			new Dictionary<ContentId, ScenarioRegion>();
		private readonly List<ScenarioRegion> m_regionOrder = new List<ScenarioRegion>();
		private readonly IReadOnlyList<ScenarioRegion> m_readOnlyRegions;

		private readonly int m_turnsPerDay;
		private readonly float m_secondsPerTurn;
		private double m_realTimeElapsedSecondsInTurn;

		private readonly HashSet<ContentId> m_discoveredContentIds = new HashSet<ContentId>();
		private ScenarioRegion m_activeRegion;
		private ScenarioTravelPlan m_pendingTravel;
		private bool m_isEnded;

		public ContentId ScenarioId { get; }

		public QuestLog QuestLog { get; }

		/// <summary>当前正在呈现和接受玩家牌桌输入的地区。</summary>
		public ScenarioRegion ActiveRegion => m_activeRegion;

		/// <summary>本次剧本已经创建的全部地区运行对象；每个地区长期保留自己的牌桌。</summary>
		public IReadOnlyList<ScenarioRegion> Regions => m_readOnlyRegions;

		/// <summary>当前地区的牌桌便捷入口，不保存第二份牌桌状态。</summary>
		public Gameplay.Tabletop.Tabletop Tabletop => m_activeRegion.Tabletop;

		public bool IsEnded => m_isEnded;

		/// <summary>本次剧本开始时解析并冻结的只读内容集合。</summary>
		public ContentIndex ContentIndex => m_contentIndex;

		public int ConfirmedTurnIndex { get; private set; }

		/// <summary>由总确认回合和剧本日程推导的当前游戏日，首日为 1。</summary>
		public int CurrentDay
		{
			get
			{
				checked
				{
					return ConfirmedTurnIndex / m_turnsPerDay + 1;
				}
			}
		}

		/// <summary>当前游戏日内已经确认的回合数；跨入新日后回到 0。</summary>
		public int ConfirmedTurnsInCurrentDay => ConfirmedTurnIndex % m_turnsPerDay;

		public int TurnsPerDay => m_turnsPerDay;

		public ActionProgressionMode ProgressionMode => Tabletop.ProgressionMode;

		/// <summary>当前游戏日在回合制或即时制下共享的归一化进度。</summary>
		public float NormalizedDayProgress
		{
			get
			{
				double completedTurns = ConfirmedTurnsInCurrentDay;
				if (ProgressionMode == ActionProgressionMode.RealTime)
				{
					completedTurns += m_realTimeElapsedSecondsInTurn / m_secondsPerTurn;
				}
				return (float)(completedTurns / m_turnsPerDay);
			}
		}

		/// <summary>本剧本切换即时制时，一个普通行动回合单位对应的秒数。</summary>
		public float SecondsPerTurn => m_secondsPerTurn;

		/// <summary>当前单局已经发现的内容数量。</summary>
		public int DiscoveredContentCount => m_discoveredContentIds.Count;

		internal ScenarioRun(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			uint authoritativeRandomSeed)
		{
			m_readOnlyRegions = m_regionOrder.AsReadOnly();
			if (definition == null)
			{
				throw new ArgumentNullException("definition");
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException("contentIndex");
			}
			if (authoritativeRandomSeed == 0u)
			{
				throw new ArgumentOutOfRangeException(
					nameof(authoritativeRandomSeed),
					"剧本单局的权威随机根种子不能为 0。");
			}
			if (!definition.ContentId.IsValid)
			{
				throw new InvalidOperationException("剧本定义必须先拥有有效的唯一内容 ID。");
			}
			if (definition.TurnsPerDay <= 0)
			{
				throw new InvalidOperationException(
					$"剧本 {definition.ContentId} 的每日确认回合数必须大于 0，当前值为 {definition.TurnsPerDay}。");
			}
			if (!float.IsFinite(definition.SecondsPerTurn) || definition.SecondsPerTurn <= 0f)
			{
				throw new InvalidOperationException(
					$"剧本 {definition.ContentId} 的每回合秒数必须是大于 0 的有限值，当前值为 {definition.SecondsPerTurn}。");
			}
			m_contentIndex = contentIndex;
			m_turnsPerDay = definition.TurnsPerDay;
			m_secondsPerTurn = definition.SecondsPerTurn;
			ScenarioId = definition.ContentId;
			QuestLog = new QuestLog(ScenarioId, definition.QuestIds, contentIndex);
			if (!definition.InitialRegionId.IsValid)
			{
				throw new InvalidOperationException($"剧本 {definition.ContentId} 缺少有效的初始地区。");
			}
			if (definition.RegionIds.Count == 0)
			{
				throw new InvalidOperationException($"剧本 {definition.ContentId} 没有配置任何地区。");
			}

			TabletopCardIdSequence cardIdSequence = new TabletopCardIdSequence();
			for (int i = 0; i < definition.RegionIds.Count; i++)
			{
				ContentId regionId = definition.RegionIds[i];
				if (!regionId.IsValid)
				{
					throw new InvalidOperationException($"剧本 {definition.ContentId} 的第 {i + 1} 个地区引用无效。");
				}
				if (!contentIndex.TryGet(regionId, out ScenarioRegionDefinition regionDefinition))
				{
					throw new InvalidOperationException($"剧本 {definition.ContentId} 的地区 {regionId} 不存在或类型错误。");
				}
				uint regionSeed = math.hash(new uint2(authoritativeRandomSeed, checked((uint)i + 1u)));
				if (regionSeed == 0u)
				{
					regionSeed = 1u;
				}
				ScenarioRegion region = new ScenarioRegion(
					regionDefinition,
					contentIndex,
					cardIdSequence,
					OnActionCompleted,
					definition.BattleFormationRules,
					regionSeed);
				if (!m_regions.TryAdd(region.Id, region))
				{
					throw new InvalidOperationException($"剧本 {definition.ContentId} 重复引用地区 {region.Id}。");
				}
				m_regionOrder.Add(region);
			}

			if (!m_regions.TryGetValue(definition.InitialRegionId, out m_activeRegion))
			{
				throw new InvalidOperationException(
					$"剧本 {definition.ContentId} 的初始地区 {definition.InitialRegionId} 未包含在地区列表中。");
			}
		}

		private ScenarioRun(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			ScenarioRunSnapshot snapshot)
		{
			m_readOnlyRegions = m_regionOrder.AsReadOnly();
			if (definition == null)
			{
				throw new ArgumentNullException(nameof(definition));
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}
			if (snapshot.ScenarioId != definition.ContentId)
			{
				throw new InvalidOperationException(
					$"单局快照剧本 {snapshot.ScenarioId} 与当前剧本定义 {definition.ContentId} 不一致。");
			}
			contentIndex.RequireContentSet(snapshot.ContentSet);
			if (definition.TurnsPerDay <= 0 ||
				!float.IsFinite(definition.SecondsPerTurn) ||
				definition.SecondsPerTurn <= 0f)
			{
				throw new InvalidOperationException($"剧本 {definition.ContentId} 的时间配置无效。");
			}
			if (snapshot.NextCardId == 0uL)
			{
				throw new InvalidOperationException("单局快照的下一卡牌 ID 不能为 0。");
			}
			if (snapshot.ConfirmedTurnIndex < 0)
			{
				throw new InvalidOperationException("单局快照的已确认回合数不能为负数。");
			}
			if (!Enum.IsDefined(typeof(ActionProgressionMode), snapshot.ProgressionMode))
			{
				throw new InvalidOperationException($"单局快照的推进模式无效：{snapshot.ProgressionMode}。");
			}
			if (!double.IsFinite(snapshot.RealTimeElapsedSecondsInTurn) ||
				snapshot.RealTimeElapsedSecondsInTurn < 0d ||
				snapshot.RealTimeElapsedSecondsInTurn >= definition.SecondsPerTurn ||
				(snapshot.ProgressionMode == ActionProgressionMode.TurnBased &&
					snapshot.RealTimeElapsedSecondsInTurn != 0d))
			{
				throw new InvalidOperationException("单局快照的当前即时回合进度无效。");
			}
			if (snapshot.Regions == null || snapshot.Regions.Count != definition.RegionIds.Count)
			{
				throw new InvalidOperationException("单局快照的地区数量与当前剧本作者源不一致。");
			}

			m_contentIndex = contentIndex;
			m_turnsPerDay = definition.TurnsPerDay;
			m_secondsPerTurn = definition.SecondsPerTurn;
			ScenarioId = definition.ContentId;
			ConfirmedTurnIndex = snapshot.ConfirmedTurnIndex;
			m_realTimeElapsedSecondsInTurn = snapshot.RealTimeElapsedSecondsInTurn;
			QuestLog = new QuestLog(ScenarioId, definition.QuestIds, contentIndex, snapshot.QuestLog);

			Dictionary<ContentId, ScenarioRegionSnapshot> regionSnapshots =
				new Dictionary<ContentId, ScenarioRegionSnapshot>();
			for (int i = 0; i < snapshot.Regions.Count; i++)
			{
				ScenarioRegionSnapshot regionSnapshot = snapshot.Regions[i];
				if (regionSnapshot == null || !regionSnapshot.RegionId.IsValid ||
					!regionSnapshots.TryAdd(regionSnapshot.RegionId, regionSnapshot))
				{
					throw new InvalidOperationException($"单局快照的第 {i + 1} 个地区为空、ID 无效或重复。");
				}
			}

			TabletopCardIdSequence cardIdSequence = new TabletopCardIdSequence(snapshot.NextCardId);
			HashSet<TabletopCardId> restoredCardIds = new HashSet<TabletopCardId>();
			for (int i = 0; i < definition.RegionIds.Count; i++)
			{
				ContentId regionId = definition.RegionIds[i];
				if (!contentIndex.TryGet(regionId, out ScenarioRegionDefinition regionDefinition))
				{
					throw new InvalidOperationException($"剧本 {ScenarioId} 的地区 {regionId} 不存在或类型错误。");
				}
				if (!regionSnapshots.Remove(regionId, out ScenarioRegionSnapshot regionSnapshot))
				{
					throw new InvalidOperationException($"单局快照缺少剧本地区 {regionId}。");
				}
				ScenarioRegion region = new ScenarioRegion(
					regionDefinition,
					contentIndex,
					cardIdSequence,
					OnActionCompleted,
					definition.BattleFormationRules,
					regionSnapshot);
				for (int stackIndex = 0; stackIndex < region.Tabletop.Cards.Stacks.Count; stackIndex++)
				{
					IReadOnlyList<TabletopCard> cards = region.Tabletop.Cards.Stacks[stackIndex].Cards;
					for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
					{
						if (!restoredCardIds.Add(cards[cardIndex].Id))
						{
							throw new InvalidOperationException(
								$"单局快照在多个地区重复使用卡牌 ID {cards[cardIndex].Id}。");
						}
					}
				}
				m_regions.Add(region.Id, region);
				m_regionOrder.Add(region);
			}

			if (!m_regions.TryGetValue(snapshot.ActiveRegionId, out m_activeRegion))
			{
				throw new InvalidOperationException($"单局快照的当前地区 {snapshot.ActiveRegionId} 不属于当前剧本。");
			}
			if (snapshot.DiscoveredContentIds == null)
			{
				throw new InvalidOperationException("单局快照缺少已发现内容集合。");
			}
			for (int i = 0; i < snapshot.DiscoveredContentIds.Count; i++)
			{
				ContentId contentId = snapshot.DiscoveredContentIds[i];
				if (!contentId.IsValid || !contentIndex.TryGet(contentId, out ContentAsset _) ||
					!m_discoveredContentIds.Add(contentId))
				{
					throw new InvalidOperationException($"单局快照的第 {i + 1} 个已发现内容无效、不存在或重复：{contentId}。");
				}
			}

			if (snapshot.ProgressionMode == ActionProgressionMode.RealTime)
			{
				for (int i = 0; i < m_regionOrder.Count; i++)
				{
					m_regionOrder[i].Tabletop.UseRealTimeProgression(m_secondsPerTurn);
				}
			}
		}

		public static ScenarioRun Restore(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			ScenarioRunSnapshot snapshot)
		{
			return new ScenarioRun(definition, contentIndex, snapshot);
		}

		public ScenarioRunSnapshot CreateSnapshot()
		{
			RequireActive();
			if (m_pendingTravel != null)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 正在切换地区，不能保存未提交的旅行事务。");
			}

			ScenarioRegionSnapshot[] regions = new ScenarioRegionSnapshot[m_regionOrder.Count];
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				regions[i] = m_regionOrder[i].CreateSnapshot();
			}
			List<ContentId> discovered = new List<ContentId>(m_discoveredContentIds);
			discovered.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
			return new ScenarioRunSnapshot(
				ScenarioId,
				m_contentIndex.CreateSnapshot(),
				m_activeRegion.Id,
				regions,
				discovered.ToArray(),
				QuestLog.CreateSnapshot(),
				m_regionOrder[0].Tabletop.Cards.CardIdSequence.NextValue,
				ConfirmedTurnIndex,
				ProgressionMode,
				m_realTimeElapsedSecondsInTurn);
		}

		public ScenarioRegion GetRegion(ContentId regionId)
		{
			if (!m_regions.TryGetValue(regionId, out ScenarioRegion region))
			{
				throw new KeyNotFoundException($"剧本 {ScenarioId} 不包含地区 {regionId}。");
			}
			return region;
		}

		internal void ActivateInitialQuests()
		{
			RequireActive();
			QuestLog.ActivateInitialQuests();
			RefreshQuestState();
		}

		/// <summary>查询内容是否已经在当前剧本单局中被发现。</summary>
		public bool IsContentDiscovered(ContentId contentId)
		{
			return contentId.IsValid && m_discoveredContentIds.Contains(contentId);
		}

		/// <summary>
		/// 根据当前单局发现事实和牌桌状态生成可选行动；调用方不维护第二份行动清单。
		/// </summary>
		public ActionCandidate[] FindActionCandidates(TabletopCardPointerReleaseIntent intent)
		{
			RequireActive();
			List<ActionDefinition> discoveredActions = new List<ActionDefinition>();
			for (int i = 0; i < m_contentIndex.AllAssets.Count; i++)
			{
				if (m_contentIndex.AllAssets[i] is ActionDefinition action &&
					IsContentDiscovered(action.ContentId))
				{
					discoveredActions.Add(action);
				}
			}
			return Tabletop.FindCandidates(intent, discoveredActions);
		}

		/// <summary>提交已由玩家确认的行动请求，并在单局边界复核当前发现权限。</summary>
		public ActionInstance StartAction(ActionRequest request)
		{
			RequireActive();
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}
			if (!IsContentDiscovered(request.ActionId))
			{
				throw new InvalidOperationException($"行动 {request.ActionId} 尚未在当前剧本单局中发现，不能启动。");
			}
			return Tabletop.StartAction(request);
		}

		public ActionPlan CreateActionPlan(ActionCandidate candidate)
		{
			RequireActive();
			if (candidate == null)
			{
				throw new ArgumentNullException(nameof(candidate));
			}
			if (!IsContentDiscovered(candidate.Action.ContentId))
			{
				throw new InvalidOperationException(
					$"行动 {candidate.Action.ContentId} 尚未在当前剧本单局中发现，不能创建计划。");
			}
			return Tabletop.CreateActionPlan(candidate);
		}

		public ActionInstance SubmitActionPlan(ActionPlan plan)
		{
			RequireActive();
			if (plan == null)
			{
				throw new ArgumentNullException(nameof(plan));
			}
			if (!IsContentDiscovered(plan.ActionId))
			{
				throw new InvalidOperationException(
					$"行动 {plan.ActionId} 尚未在当前剧本单局中发现，不能提交计划。");
			}
			return Tabletop.SubmitActionPlan(plan);
		}

		public void UseRealTimeProgression()
		{
			RequireActive();
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				m_regionOrder[i].Tabletop.UseRealTimeProgression(m_secondsPerTurn);
			}
		}

		public void UseTurnBasedProgression()
		{
			RequireActive();
			if (m_realTimeElapsedSecondsInTurn > 0d)
			{
				throw new InvalidOperationException(
					$"剧本 {ScenarioId} 的即时世界回合已经推进 {m_realTimeElapsedSecondsInTurn:0.###} 秒，只能在回合边界切回回合制。");
			}
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				m_regionOrder[i].Tabletop.UseTurnBasedProgression();
			}
		}

		internal bool DiscoverContent(ContentId contentId)
		{
			RequireActive();
			if (!contentId.IsValid)
			{
				throw new InvalidOperationException($"不能发现无效 Gameplay 内容 ID：{contentId}。");
			}
			if (!m_contentIndex.TryGet(contentId, out var _))
			{
				throw new InvalidOperationException($"不能发现当前内容索引中不存在的 Gameplay 内容：{contentId}。");
			}
			if (!m_discoveredContentIds.Add(contentId))
			{
				return false;
			}

			RefreshQuestState();
			return true;
		}

		internal int ConfirmTurn()
		{
			RequireActive();
			if (Tabletop.ProgressionMode != ActionProgressionMode.TurnBased)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 处于即时制，世界回合由时间自动推进，不能手动确认。");
			}
			return AdvanceWorldTurn();
		}

		private int AdvanceWorldTurn()
		{
			int previousDay = CurrentDay;
			checked
			{
				ConfirmedTurnIndex++;
				for (int i = 0; i < m_regionOrder.Count; i++)
				{
					m_regionOrder[i].Tabletop.AdvanceConfirmedTurn();
				}
				if (CurrentDay != previousDay)
				{
					RefreshQuestState();
				}
				EventKit.Type.Send(new ScenarioTurnConfirmedEvent(
					ScenarioId,
					ConfirmedTurnIndex,
					CurrentDay,
					ConfirmedTurnsInCurrentDay));
				return ConfirmedTurnIndex;
			}
		}

		internal void AdvanceRealTime(float deltaSeconds)
		{
			RequireActive();
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"剧本即时推进秒数必须是大于或等于 0 的有限值。");
			}
			if (Tabletop.ProgressionMode != ActionProgressionMode.RealTime || deltaSeconds == 0f)
			{
				return;
			}

			double remainingSeconds = deltaSeconds;
			while (remainingSeconds > 0d)
			{
				double secondsUntilTurn = m_secondsPerTurn - m_realTimeElapsedSecondsInTurn;
				double stepSeconds = Math.Min(remainingSeconds, secondsUntilTurn);
				for (int i = 0; i < m_regionOrder.Count; i++)
				{
					m_regionOrder[i].Tabletop.AdvanceRealTime((float)stepSeconds);
				}
				m_realTimeElapsedSecondsInTurn += stepSeconds;
				remainingSeconds -= stepSeconds;
				if (m_realTimeElapsedSecondsInTurn >= m_secondsPerTurn)
				{
					m_realTimeElapsedSecondsInTurn = 0d;
					AdvanceWorldTurn();
				}
			}
		}

		internal void End()
		{
			RequireActive();
			if (m_pendingTravel != null)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 正在切换地区，不能同时结束单局。");
			}
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				m_regionOrder[i].Tabletop.End();
			}
			m_realTimeElapsedSecondsInTurn = 0d;
			m_isEnded = true;
		}

		internal ScenarioTravelPlan BeginTravel(
			ContentId targetRegionId,
			IReadOnlyList<TabletopCardId> travelerCardIds)
		{
			RequireActive();
			if (m_pendingTravel != null)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 已经在切换地区。");
			}
			ScenarioRegion targetRegion = GetRegion(targetRegionId);
			if (ReferenceEquals(targetRegion, m_activeRegion))
			{
				throw new InvalidOperationException($"剧本当前已经位于地区 {targetRegionId}。");
			}
			if (travelerCardIds == null)
			{
				throw new ArgumentNullException(nameof(travelerCardIds));
			}

			Vector2[] targetPositions = new Vector2[travelerCardIds.Count];
			for (int i = 0; i < targetPositions.Length; i++)
			{
				targetPositions[i] = targetRegion.ArrivalPosition;
			}
			m_activeRegion.Tabletop.RequireCardsCanTransferTo(
				targetRegion.Tabletop,
				travelerCardIds,
				targetPositions);

			m_pendingTravel = new ScenarioTravelPlan(
				this,
				m_activeRegion,
				targetRegion,
				travelerCardIds,
				targetPositions);
			return m_pendingTravel;
		}

		internal void CommitTravel(ScenarioTravelPlan travel)
		{
			RequirePendingTravel(travel);
			travel.SourceRegion.Tabletop.TransferCardsTo(
				travel.TargetRegion.Tabletop,
				travel.TravelerCardIds,
				travel.TargetPositions);
			m_activeRegion = travel.TargetRegion;
			m_pendingTravel = null;
			travel.MarkCommitted();
		}

		internal void CancelTravel(ScenarioTravelPlan travel)
		{
			RequirePendingTravel(travel);
			m_pendingTravel = null;
		}

		private void RequirePendingTravel(ScenarioTravelPlan travel)
		{
			if (travel == null)
			{
				throw new ArgumentNullException(nameof(travel));
			}
			if (!ReferenceEquals(m_pendingTravel, travel))
			{
				throw new InvalidOperationException("指定旅行不属于当前剧本正在执行的地区切换。");
			}
		}

		private void OnActionCompleted(ContentId actionId)
		{
			QuestLog.RecordFact(new ActionCompletedQuestTaskFact(actionId));
			RefreshQuestState();
			EventKit.Type.Send(new ActionCompletedEvent(ScenarioId, actionId));
		}

		private void RefreshQuestState()
		{
			List<ContentId> discoveredContentIds = new List<ContentId>(m_discoveredContentIds);
			discoveredContentIds.Sort((left, right) =>
				string.CompareOrdinal(left.Value, right.Value));
			bool changed;
			do
			{
				changed = QuestLog.RecordFact(new DayReachedQuestTaskFact(CurrentDay));
				for (int i = 0; i < discoveredContentIds.Count; i++)
				{
					changed |= QuestLog.RecordFact(
						new ContentDiscoveredQuestTaskFact(discoveredContentIds[i]));
				}
			}
			while (changed);
		}

		private void RequireActive()
		{
			if (m_isEnded)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 已经结束，不能再推进单局状态。");
			}
		}
	}
}
