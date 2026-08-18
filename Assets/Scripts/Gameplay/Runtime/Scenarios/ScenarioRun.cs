using System;
using System.Collections.Generic;
using GAS.Runtime;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using GameCore;
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
		private readonly ModPackageSetSnapshot m_modPackages;
		private readonly HashSet<ContentId> m_currencyCardIds;
		private readonly Dictionary<ContentId, ScenarioRegion> m_regions =
			new Dictionary<ContentId, ScenarioRegion>();
		private readonly List<ScenarioRegion> m_regionOrder = new List<ScenarioRegion>();
		private readonly IReadOnlyList<ScenarioRegion> m_readOnlyRegions;

		private readonly int m_turnsPerDay;
		private readonly float m_secondsPerTurn;
		private readonly ScenarioDayCycleRulesRuntime m_dayCycleRules;
		private readonly ScenarioStartOptions m_startOptions;
		private double m_realTimeElapsedSecondsInTurn;

		private readonly HashSet<ContentId> m_discoveredContentIds = new HashSet<ContentId>();
		private readonly HashSet<ContentId> m_seenJournalEntryIds = new HashSet<ContentId>();
		private readonly HashSet<string> m_completedDayEncounterKeys = new HashSet<string>(StringComparer.Ordinal);
		private ScenarioRegion m_activeRegion;
		private ScenarioTravelPlan m_pendingTravel;
		private ScenarioDayCycle m_dayCycle;
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
					return m_dayCycle != null
						? m_dayCycle.EndingDay
						: ConfirmedTurnIndex / m_turnsPerDay + 1;
				}
			}
		}

		/// <summary>当前游戏日内已经确认的回合数；跨入新日后回到 0。</summary>
		public int ConfirmedTurnsInCurrentDay => m_dayCycle != null
			? m_turnsPerDay
			: ConfirmedTurnIndex % m_turnsPerDay;

		public int TurnsPerDay => m_turnsPerDay;

		public ActionProgressionMode ProgressionMode => Tabletop.ProgressionMode;

		/// <summary>即时制普通行动已经推进到回合中途时，不能无损切回回合制。</summary>
		public bool CanReturnToTurnBasedProgression => m_realTimeElapsedSecondsInTurn <= 0d;

		/// <summary>本局是否启用友好模式；友好模式会阻止标记为敌对阵营的日终遭遇生成。</summary>
		public bool FriendlyMode => m_startOptions.FriendlyMode;

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

		public ScenarioDayCyclePhase DayCyclePhase =>
			m_dayCycle?.Phase ?? ScenarioDayCyclePhase.Inactive;

		public int ExcessCardCount => CalculateExcessCardCount();

		public ScenarioDayEncounterResult? DayEncounterResult => m_dayCycle?.EncounterResult;

		/// <summary>当前单局已经发现的内容数量。</summary>
		public int DiscoveredContentCount => m_discoveredContentIds.Count;

		internal ScenarioRun(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			uint authoritativeRandomSeed)
			: this(definition, contentIndex, authoritativeRandomSeed, new ModPackageSetSnapshot(Array.Empty<ModPackageSnapshot>()))
		{
		}

		internal ScenarioRun(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			uint authoritativeRandomSeed,
			ModPackageSetSnapshot modPackages)
			: this(definition, contentIndex, authoritativeRandomSeed, modPackages, ScenarioStartOptions.Default)
		{
		}

		internal ScenarioRun(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			uint authoritativeRandomSeed,
			ScenarioStartOptions startOptions)
			: this(
				definition,
				contentIndex,
				authoritativeRandomSeed,
				new ModPackageSetSnapshot(Array.Empty<ModPackageSnapshot>()),
				startOptions)
		{
		}

		internal ScenarioRun(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			uint authoritativeRandomSeed,
			ModPackageSetSnapshot modPackages,
			ScenarioStartOptions startOptions)
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
			m_modPackages = modPackages ?? throw new ArgumentNullException(nameof(modPackages));
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
			m_currencyCardIds = BuildCurrencyCardIds(contentIndex);
			m_turnsPerDay = definition.TurnsPerDay;
			m_secondsPerTurn = ResolveSecondsPerTurn(definition, startOptions);
			m_dayCycleRules = definition.DayCycleRules?.CreateRuntime() ?? default;
			m_startOptions = startOptions;
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
					IsContentDiscovered,
					OnActionCompleted,
					OnCardsDefeated,
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
			ModPackageSetSnapshot currentModPackages,
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
			if (currentModPackages == null)
			{
				throw new ArgumentNullException(nameof(currentModPackages));
			}
			if (snapshot.ScenarioId != definition.ContentId)
			{
				throw new InvalidOperationException(
					$"单局快照剧本 {snapshot.ScenarioId} 与当前剧本定义 {definition.ContentId} 不一致。");
			}
			currentModPackages.RequireExactMatch(snapshot.ModPackages);
			contentIndex.RequireContentSet(snapshot.ContentSet);
			if (definition.TurnsPerDay <= 0 ||
				!float.IsFinite(definition.SecondsPerTurn) ||
				definition.SecondsPerTurn <= 0f)
			{
				throw new InvalidOperationException($"剧本 {definition.ContentId} 的时间配置无效。");
			}
			ScenarioStartOptions restoredStartOptions = snapshot.StartOptions;
			float restoredSecondsPerTurn = ResolveSecondsPerTurn(definition, restoredStartOptions);
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
				snapshot.RealTimeElapsedSecondsInTurn >= restoredSecondsPerTurn ||
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
			m_modPackages = currentModPackages;
			m_currencyCardIds = BuildCurrencyCardIds(contentIndex);
			m_turnsPerDay = definition.TurnsPerDay;
			m_secondsPerTurn = restoredSecondsPerTurn;
			m_dayCycleRules = definition.DayCycleRules?.CreateRuntime() ?? default;
			m_startOptions = restoredStartOptions;
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
					IsContentDiscovered,
					OnActionCompleted,
					OnCardsDefeated,
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
			for (int i = 0; i < snapshot.SeenJournalEntryIds.Count; i++)
			{
				ContentId contentId = snapshot.SeenJournalEntryIds[i];
				ContentAsset asset = RequireJournalEntryContent(contentId);
				if (!m_seenJournalEntryIds.Add(asset.ContentId))
				{
					throw new InvalidOperationException($"单局快照的第 {i + 1} 个已读日志条目重复：{contentId}。");
				}
			}
			if (snapshot.CompletedDayEncounterKeys == null)
			{
				throw new InvalidOperationException("单局快照缺少已完成日终遭遇集合。");
			}
			for (int i = 0; i < snapshot.CompletedDayEncounterKeys.Count; i++)
			{
				string key = snapshot.CompletedDayEncounterKeys[i];
				if (!Gameplay.Actions.ActionLocalKeyUtility.IsValidKey(key) ||
					!m_completedDayEncounterKeys.Add(key))
				{
					throw new InvalidOperationException($"单局快照的第 {i + 1} 个日终遭遇 key 无效或重复：{key}。");
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
			return Restore(
				definition,
				contentIndex,
				new ModPackageSetSnapshot(Array.Empty<ModPackageSnapshot>()),
				snapshot);
		}

		public static ScenarioRun Restore(
			ScenarioDefinition definition,
			ContentIndex contentIndex,
			ModPackageSetSnapshot currentModPackages,
			ScenarioRunSnapshot snapshot)
		{
			return new ScenarioRun(definition, contentIndex, currentModPackages, snapshot);
		}

		public ScenarioRunSnapshot CreateSnapshot()
		{
			RequireActive();
			if (m_pendingTravel != null)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 正在切换地区，不能保存未提交的旅行事务。");
			}
			if (m_dayCycle != null)
			{
				throw new InvalidOperationException(
					$"剧本 {ScenarioId} 正在处理第 {m_dayCycle.EndingDay} 天的日终阶段，必须完成日终后再保存。");
			}

			ScenarioRegionSnapshot[] regions = new ScenarioRegionSnapshot[m_regionOrder.Count];
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				regions[i] = m_regionOrder[i].CreateSnapshot();
			}
			List<ContentId> discovered = new List<ContentId>(m_discoveredContentIds);
			discovered.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
			List<ContentId> seenJournalEntries = new List<ContentId>(m_seenJournalEntryIds);
			seenJournalEntries.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
			List<string> completedDayEncounterKeys = new List<string>(m_completedDayEncounterKeys);
			completedDayEncounterKeys.Sort(StringComparer.Ordinal);
			return new ScenarioRunSnapshot(
				ScenarioId,
				m_contentIndex.CreateSnapshot(),
				m_modPackages,
				m_activeRegion.Id,
				regions,
				discovered.ToArray(),
				seenJournalEntries.ToArray(),
				completedDayEncounterKeys.ToArray(),
				QuestLog.CreateSnapshot(),
				m_regionOrder[0].Tabletop.Cards.CardIdSequence.NextValue,
				ConfirmedTurnIndex,
				ProgressionMode,
				m_realTimeElapsedSecondsInTurn,
				m_startOptions);
		}

		/// <summary>
		/// 从当前全部地区牌桌即时派生玩家 HUD 使用的资源统计，不保存第二份玩法状态。
		/// </summary>
		public ScenarioTabletopStats GetTabletopStats()
		{
			return CreateTabletopStats(null, out _);
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

		/// <summary>查询当前单局日志中的任务或配方 / 行动条目是否已被玩家查看。</summary>
		public bool IsJournalEntrySeen(ContentId contentId)
		{
			ContentAsset asset = RequireJournalEntryContent(contentId);
			return m_seenJournalEntryIds.Contains(asset.ContentId);
		}

		/// <summary>把当前单局日志中的任务或配方 / 行动条目标记为已查看。</summary>
		public bool MarkJournalEntrySeen(ContentId contentId)
		{
			RequireActive();
			ContentAsset asset = RequireJournalEntryContent(contentId);
			return m_seenJournalEntryIds.Add(asset.ContentId);
		}

		/// <summary>按唯一内容 ID 稳定排序读取本局已经发现的行动作者定义。</summary>
		public ActionDefinition[] GetDiscoveredActions()
		{
			List<ActionDefinition> actions = new List<ActionDefinition>();
			for (int i = 0; i < m_contentIndex.AllAssets.Count; i++)
			{
				if (m_contentIndex.AllAssets[i] is ActionDefinition action &&
					m_discoveredContentIds.Contains(action.ContentId))
				{
					actions.Add(action);
				}
			}
			actions.Sort((left, right) =>
				string.CompareOrdinal(left.ContentId.Value, right.ContentId.Value));
			return actions.ToArray();
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
					IsContentDiscovered(action.ContentId) &&
					(intent.IsDrag || action.CanStartFromClick))
				{
					discoveredActions.Add(action);
				}
			}
			ActionCandidate[] candidates = Tabletop.FindCandidates(intent, discoveredActions);
			List<ActionCandidate> available = new List<ActionCandidate>(candidates.Length);
			for (int i = 0; i < candidates.Length; i++)
			{
				if (AreActionConditionsMet(candidates[i].Action, candidates[i].Bindings))
				{
					available.Add(candidates[i]);
				}
			}
			return available.ToArray();
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
			ActionCandidate candidate = Tabletop.CreateCandidateFromRequest(request);
			if (!AreActionConditionsMet(candidate.Action, candidate.Bindings))
			{
				throw new InvalidOperationException($"行动 {request.ActionId} 的当前单局可用条件不成立。");
			}
			return Tabletop.StartAction(request);
		}

		private bool AreActionConditionsMet(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings)
		{
			ActionConditionContext context = new ActionConditionContext(
				action,
				bindings,
				m_contentIndex,
				Tabletop.Cards,
				QuestLog.CompletedQuestCount);
			for (int i = 0; i < action.Conditions.Count; i++)
			{
				if (!action.Conditions[i].IsMet(context))
				{
					return false;
				}
			}
			return true;
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
			ActionProgressionMode previousMode = ProgressionMode;
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				m_regionOrder[i].Tabletop.UseRealTimeProgression(m_secondsPerTurn);
			}
			if (previousMode != ActionProgressionMode.RealTime)
			{
				QuestLog.RecordFact(new ProgressionModeChangedQuestTaskFact(ActionProgressionMode.RealTime));
				RefreshQuestState();
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
			ActionProgressionMode previousMode = ProgressionMode;
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				m_regionOrder[i].Tabletop.UseTurnBasedProgression();
			}
			if (previousMode != ActionProgressionMode.TurnBased)
			{
				QuestLog.RecordFact(new ProgressionModeChangedQuestTaskFact(ActionProgressionMode.TurnBased));
				RefreshQuestState();
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
			EventKit.Type.Send(new ContentDiscoveredEvent(ScenarioId, contentId));
			return true;
		}

		internal int ConfirmTurn()
		{
			RequireActive();
			if (m_dayCycle != null)
			{
				throw new InvalidOperationException(
					$"剧本 {ScenarioId} 正在处理第 {m_dayCycle.EndingDay} 天的日终阶段，不能继续确认普通回合。");
			}
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
				bool startsDayCycle = m_dayCycleRules.Enabled &&
					ConfirmedTurnIndex % m_turnsPerDay == 0;
				if (startsDayCycle)
				{
					m_dayCycle = new ScenarioDayCycle(previousDay);
					PublishDayCycleChanged();
				}
				else if (CurrentDay != previousDay)
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
			if (m_dayCycle != null)
			{
				return;
			}
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"剧本即时推进秒数必须是大于或等于 0 的有限值。");
			}
			if (deltaSeconds == 0f)
			{
				return;
			}
			if (Tabletop.ProgressionMode != ActionProgressionMode.RealTime)
			{
				for (int i = 0; i < m_regionOrder.Count; i++)
				{
					m_regionOrder[i].Tabletop.AdvanceRealTime(deltaSeconds);
				}
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
					if (m_dayCycle != null)
					{
						break;
					}
				}
			}
		}

		internal void ContinueDayCycle()
		{
			RequireActive();
			if (m_dayCycle == null)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 当前没有正在处理的日终流程。");
			}

			switch (m_dayCycle.Phase)
			{
				case ScenarioDayCyclePhase.AwaitingFeedingConfirmation:
					FeedCharacters();
					m_dayCycle.FinishFeeding(
						CountCharacters() > 0,
						CalculateExcessCardCount());
					if (m_dayCycle.Phase == ScenarioDayCyclePhase.AwaitingNewDayConfirmation)
					{
						ResolveDayEncounter();
					}
					PublishDayCycleChanged();
					return;
				case ScenarioDayCyclePhase.AwaitingExcessCardResolution:
					FinishExcessCardResolution();
					return;
				case ScenarioDayCyclePhase.AwaitingNewDayConfirmation:
					m_dayCycle = null;
					RefreshQuestState();
					PublishDayCycleChanged();
					return;
				case ScenarioDayCyclePhase.GameOver:
					throw new InvalidOperationException("当前日终已经判定全员死亡，不能开始新的一天。");
				default:
					throw new InvalidOperationException($"剧本 {ScenarioId} 的日终阶段无效：{m_dayCycle.Phase}。");
			}
		}

		private void FeedCharacters()
		{
			GameplayEffectConfig healingEffect = null;
			if (m_dayCycleRules.HungerPerCharacter > 0)
			{
				healingEffect = RequireFeedingHealingEffect();
			}
			for (int regionIndex = 0; regionIndex < m_regionOrder.Count; regionIndex++)
			{
				var tabletop = m_regionOrder[regionIndex].Tabletop;
				List<CharacterCard> characters = new List<CharacterCard>();
				List<TabletopCard> foods = new List<TabletopCard>();
				IReadOnlyList<TabletopCardStack> stacks = tabletop.Cards.Stacks;
				for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
				{
					IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
					for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
					{
						TabletopCard card = cards[cardIndex];
						if (card is CharacterCard character)
						{
							characters.Add(character);
						}
						else if (m_contentIndex.TryGet(card.ContentId, out FoodCardDefinition _))
						{
							foods.Add(card);
						}
					}
				}

				for (int characterIndex = 0; characterIndex < characters.Count; characterIndex++)
				{
					CharacterCard character = characters[characterIndex];
					int hunger = m_dayCycleRules.HungerPerCharacter;
					while (hunger > 0 && foods.Count > 0)
					{
						int nearestIndex = FindNearestFoodIndex(character, foods);
						TabletopCard food = foods[nearestIndex];
						if (!m_contentIndex.TryGet(food.ContentId, out FoodCardDefinition definition))
						{
							throw new InvalidOperationException($"进食候选 {food.ContentId} 不再是食物卡定义。");
						}
						int consumedNutrition = Math.Min(hunger, definition.NutritionPerUse);
						hunger -= consumedNutrition;
						Vector2 foodPosition = food.Position;
						bool foodWillBeConsumed = food.RemainingUses == 1;
						EventKit.Type.Send(new ScenarioFeedingPresentationEvent(
							ScenarioId,
							tabletop,
							food.Id,
							character.Id,
							foodPosition,
							foodWillBeConsumed));
						tabletop.UseCard(food.Id);
						ApplyFeedingHealing(character, consumedNutrition, healingEffect);
						if (!tabletop.Cards.TryGetCard(food.Id, out _))
						{
							foods.RemoveAt(nearestIndex);
						}
					}
					if (hunger > 0)
					{
						tabletop.RemoveCard(character.Id);
					}
				}
			}
		}

		private GameplayEffectConfig RequireFeedingHealingEffect()
		{
			GameplayEffectConfig effect = GameplayEffectHelper.GetConfigByID(
				m_dayCycleRules.FeedingHealingEffectId)
				?? throw new InvalidOperationException(
					$"剧本 {ScenarioId} 的进食恢复效果 {m_dayCycleRules.FeedingHealingEffectId} 不存在。");
			MCConfModifiers modifiers = null;
			for (int i = 0; i < effect.ComponentConfigs.Length; i++)
			{
				if (effect.ComponentConfigs[i] is not MCConfModifiers candidate)
				{
					continue;
				}
				if (modifiers != null)
				{
					throw new InvalidOperationException(
						$"进食恢复效果 {m_dayCycleRules.FeedingHealingEffectId} 重复声明 Modifier 组件。");
				}
				modifiers = candidate;
			}
			ModifierSetting[] settings = modifiers?.modifierSettings;
			if (settings == null || settings.Length != 1 ||
				settings[0].AttrSetCode != XAttrSet.FightUnit ||
				settings[0].AttrCode != XAttribute.Health ||
				settings[0].Operation != GEOperation.Add)
			{
				throw new InvalidOperationException(
					$"进食恢复效果 {m_dayCycleRules.FeedingHealingEffectId} 必须且只能包含一个 FightUnit.Health 加法 Modifier。");
			}
			return effect;
		}

		private void ApplyFeedingHealing(
			CharacterCard character,
			int consumedNutrition,
			GameplayEffectConfig healingEffect)
		{
			if (consumedNutrition <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(consumedNutrition));
			}
			float missingHealth = Math.Max(0f, character.MaxHealth - character.CurrentHealth);
			int requestedHealing = Mathf.RoundToInt(
				character.MaxHealth * 0.5f * consumedNutrition / m_dayCycleRules.HungerPerCharacter);
			float healing = Math.Min(missingHealth, requestedHealing);
			if (healing <= 0f)
			{
				return;
			}

			GameplayEffectSpec effect = new GameplayEffectSpec(healingEffect.ComponentConfigs);
			effect.SetModifierMagnitude(0, healing);
			effect.ApplyToSelf(character.AbilitySystem);
		}

		private static int FindNearestFoodIndex(CharacterCard character, IReadOnlyList<TabletopCard> foods)
		{
			int nearestIndex = 0;
			float nearestDistance = (foods[0].Position - character.Position).sqrMagnitude;
			for (int i = 1; i < foods.Count; i++)
			{
				float distance = (foods[i].Position - character.Position).sqrMagnitude;
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearestIndex = i;
				}
			}
			return nearestIndex;
		}

		private void ResolveDayEncounter()
		{
			ScenarioDayEncounterRuleRuntime? selected = null;
			for (int i = 0; i < m_dayCycleRules.Encounters.Count; i++)
			{
				ScenarioDayEncounterRuleRuntime candidate = m_dayCycleRules.Encounters[i];
				if (!candidate.MatchesDay(m_dayCycle.EndingDay) ||
					(candidate.OneTimeOnly && m_completedDayEncounterKeys.Contains(candidate.Key)) ||
					IsBlockedByFriendlyMode(candidate) ||
					Tabletop.Cards.CardCount >= candidate.MaxCardsOnTabletop ||
					Tabletop.NextAuthoritativeFloat() > candidate.Chance)
				{
					continue;
				}
				if (!selected.HasValue || IsHigherPriorityEncounter(candidate, selected.Value))
				{
					selected = candidate;
				}
			}
			if (!selected.HasValue)
			{
				return;
			}

			ScenarioDayEncounterRuleRuntime encounter = selected.Value;
			Tabletop.Cards.EnsureCanCreateCards(encounter.Count);
			List<ContentId> createdCardIds = new List<ContentId>(encounter.Count);
			List<Vector2> createdPositions = new List<Vector2>(encounter.Count);
			for (int i = 0; i < encounter.Count; i++)
			{
				TabletopCard created = Tabletop.CreateCardAtAuthoritativeRandomPosition(encounter.CardId);
				createdCardIds.Add(encounter.CardId);
				createdPositions.Add(created.Position);
			}
			m_dayCycle.RecordEncounter(encounter.CardId, encounter.Count, encounter.NotificationMessage);
			QuestLog.RecordFact(new CardsCreatedQuestTaskFact(createdCardIds));
			RefreshQuestState();
			if (encounter.OneTimeOnly && !m_completedDayEncounterKeys.Add(encounter.Key))
			{
				throw new InvalidOperationException($"一次性日终遭遇 {encounter.Key} 被重复提交。");
			}
			if (createdPositions.Count > 0)
			{
				Tabletop.RequestPresentationCue(TabletopPresentationCue.AtTablePosition(
					TabletopPresentationCueKind.CameraFocus,
					createdPositions[0]));
			}
			for (int i = 0; i < createdPositions.Count; i++)
			{
				Tabletop.RequestPresentationCue(TabletopPresentationCue.AtTablePosition(
					TabletopPresentationCueKind.CardSmoke,
					createdPositions[i]));
			}
		}

		private void FinishExcessCardResolution()
		{
			int excess = CalculateExcessCardCount();
			if (excess > 0)
			{
				throw new InvalidOperationException(
					$"第 {m_dayCycle.EndingDay} 天仍超出卡牌上限 {excess} 张，必须先处理超限卡牌。");
			}
			m_dayCycle.FinishExcessCardResolution();
			ResolveDayEncounter();
			PublishDayCycleChanged();
		}

		private static bool IsHigherPriorityEncounter(
			ScenarioDayEncounterRuleRuntime candidate,
			ScenarioDayEncounterRuleRuntime current)
		{
			if (candidate.Priority != current.Priority)
			{
				return candidate.Priority > current.Priority;
			}
			return candidate.Specificity < current.Specificity;
		}

		private bool IsBlockedByFriendlyMode(ScenarioDayEncounterRuleRuntime encounter)
		{
			if (!FriendlyMode)
			{
				return false;
			}
			if (!m_contentIndex.TryGet(encounter.CardId, out CardDefinition card))
			{
				throw new InvalidOperationException(
					$"日终遭遇 {encounter.Key} 引用的卡牌 {encounter.CardId} 不属于当前内容集合。");
			}
			for (int i = 0; i < card.TagCodes.Count; i++)
			{
				if (TagHelper.HasTag(card.TagCodes[i], XTag.Faction_Enemy))
				{
					return true;
				}
			}
			return false;
		}

		private int CountCharacters()
		{
			int count = 0;
			for (int regionIndex = 0; regionIndex < m_regionOrder.Count; regionIndex++)
			{
				IReadOnlyList<TabletopCardStack> stacks = m_regionOrder[regionIndex].Tabletop.Cards.Stacks;
				for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
				{
					IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
					for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
					{
						if (cards[cardIndex] is CharacterCard)
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		private int CalculateExcessCardCount()
		{
			if (!m_dayCycleRules.Enabled)
			{
				return 0;
			}
			int cardCount = 0;
			int cardLimitBonus = 0;
			for (int i = 0; i < m_regionOrder.Count; i++)
			{
				var tabletop = m_regionOrder[i].Tabletop;
				cardLimitBonus = checked(cardLimitBonus + tabletop.CardLimitBonus);
				IReadOnlyList<TabletopCardStack> stacks = tabletop.Cards.Stacks;
				for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
				{
					IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
					for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
					{
						TabletopCard card = cards[cardIndex];
						if (!m_contentIndex.TryGet(card.ContentId, out CardDefinition definition))
						{
							throw new InvalidOperationException(
								$"牌桌卡牌 {card.Id} 的内容 {card.ContentId} 已不在当前内容集合中。");
						}
						if (definition.CountsTowardCardLimit)
						{
							cardCount = checked(cardCount + 1);
						}
					}
				}
			}
			return Math.Max(
				0,
				checked(cardCount - (m_dayCycleRules.BaseCardLimit + cardLimitBonus)));
		}

		private void PublishDayCycleChanged()
		{
			EventKit.Type.Send(new ScenarioDayCycleChangedEvent(
				ScenarioId,
				m_dayCycle?.EndingDay ?? CurrentDay - 1,
				DayCyclePhase,
				ExcessCardCount));
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

		private void OnActionCompleted(
			ContentId actionId,
			ActionSettlementResult result)
		{
			if (result == null)
			{
				throw new ArgumentNullException(nameof(result));
			}
			int previousCompletedQuestCount = QuestLog.CompletedQuestCount;
			for (int i = 0; i < result.DiscoveredContentIds.Count; i++)
			{
				if (!DiscoverContent(result.DiscoveredContentIds[i]))
				{
					throw new InvalidOperationException(
						$"行动 {actionId} 结算出的研究内容 {result.DiscoveredContentIds[i]} 已被提前写入当前单局。");
				}
				previousCompletedQuestCount = QuestLog.CompletedQuestCount;
			}
			QuestLog.RecordFact(new ActionCompletedQuestTaskFact(actionId));
			if (result.CreatedCardIds.Count > 0)
			{
				QuestLog.RecordFact(new CardsCreatedQuestTaskFact(result.CreatedCardIds));
			}
			if (result.ExploredContentIds.Count > 0)
			{
				QuestLog.RecordFact(new CardsExploredQuestTaskFact(result.ExploredContentIds));
			}
			for (int i = 0; i < result.PurchasedPackIds.Count; i++)
			{
				QuestLog.RecordFact(new CardPackPurchasedQuestTaskFact(result.PurchasedPackIds[i]));
			}
			if (result.SoldContentIds.Count > 0)
			{
				QuestLog.RecordFact(new CardsSoldQuestTaskFact(result.SoldContentIds));
			}
			for (int i = 0; i < result.EquippedCardIds.Count; i++)
			{
				QuestLog.RecordFact(new CardEquippedQuestTaskFact(result.EquippedCardIds[i]));
			}
			RefreshQuestState(previousCompletedQuestCount);
			EventKit.Type.Send(new ActionCompletedEvent(ScenarioId, actionId));
			if (DayCyclePhase == ScenarioDayCyclePhase.AwaitingExcessCardResolution &&
				CalculateExcessCardCount() == 0)
			{
				FinishExcessCardResolution();
			}
		}

		private void OnCardsDefeated(IReadOnlyList<ContentId> defeatedCardIds)
		{
			if (defeatedCardIds == null || defeatedCardIds.Count == 0)
			{
				throw new ArgumentException("战斗击败事实必须包含至少一张被击败卡牌。", nameof(defeatedCardIds));
			}

			QuestLog.RecordFact(new CardsDefeatedQuestTaskFact(defeatedCardIds));
			RefreshQuestState();
		}

		private void RefreshQuestState()
		{
			RefreshQuestState(QuestLog.CompletedQuestCount);
		}

		private void RefreshQuestState(int previousCompletedQuestCount)
		{
			TabletopStateQuestTaskFact tabletopState = CreateTabletopStateQuestTaskFact();
			List<ContentId> discoveredContentIds = new List<ContentId>(m_discoveredContentIds);
			discoveredContentIds.Sort((left, right) =>
				string.CompareOrdinal(left.Value, right.Value));
			bool changed;
			do
			{
				changed = QuestLog.RecordFact(tabletopState);
				changed |= QuestLog.RecordFact(new DayReachedQuestTaskFact(CurrentDay));
				for (int i = 0; i < discoveredContentIds.Count; i++)
				{
					changed |= QuestLog.RecordFact(
						new ContentDiscoveredQuestTaskFact(discoveredContentIds[i]));
				}
			}
			while (changed);
			PresentPackVendorUnlocks(previousCompletedQuestCount, QuestLog.CompletedQuestCount);
		}

		private void PresentPackVendorUnlocks(
			int previousCompletedQuestCount,
			int currentCompletedQuestCount)
		{
			if (currentCompletedQuestCount <= previousCompletedQuestCount)
			{
				return;
			}

			IReadOnlyList<TabletopCardStack> stacks = Tabletop.Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					TabletopCard card = cards[cardIndex];
					if (card is not PackVendorCard vendorCard)
					{
						continue;
					}
					if (!m_contentIndex.TryGet(vendorCard.ContentId, out PackVendorDefinition vendorDefinition))
					{
						throw new InvalidOperationException(
							$"牌桌卡包商贩 {vendorCard.Id} 的内容定义 {vendorCard.ContentId} 不存在或类型错误。");
					}
					if (previousCompletedQuestCount >= vendorDefinition.MinimumCompletedQuests ||
						currentCompletedQuestCount < vendorDefinition.MinimumCompletedQuests)
					{
						continue;
					}

					Tabletop.RequestPresentationCue(TabletopPresentationCue.AtTablePosition(
						TabletopPresentationCueKind.CameraFocus,
						vendorCard.Position));
					Tabletop.RequestPresentationCue(TabletopPresentationCue.AtCard(
						TabletopPresentationCueKind.CardHighlight,
						vendorCard.Id));
				}
			}
		}

		private TabletopStateQuestTaskFact CreateTabletopStateQuestTaskFact()
		{
			List<ContentId> cardContentIds = new List<ContentId>();
			ScenarioTabletopStats stats = CreateTabletopStats(cardContentIds, out var currencyStocks);
			return new TabletopStateQuestTaskFact(
				cardContentIds,
				stats.TotalFoodNutrition,
				currencyStocks,
				stats.CardLimit);
		}

		private ScenarioTabletopStats CreateTabletopStats(
			List<ContentId> cardContentIds,
			out List<TabletopStateQuestTaskFact.CurrencyStock> currencyStocks)
		{
			Dictionary<ContentId, int> storedCurrencyByCard = cardContentIds != null
				? new Dictionary<ContentId, int>()
				: null;
			int totalFoodNutrition = 0;
			int cardLimitBonus = 0;
			int cardsOwned = 0;
			int currency = 0;
			int characterCount = 0;
			for (int regionIndex = 0; regionIndex < m_regionOrder.Count; regionIndex++)
			{
				var tabletop = m_regionOrder[regionIndex].Tabletop;
				cardLimitBonus = checked(cardLimitBonus + tabletop.CardLimitBonus);
				IReadOnlyList<TabletopCardStack> stacks = tabletop.Cards.Stacks;
				for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
				{
					IReadOnlyList<TabletopCard> cards = stacks[stackIndex].Cards;
					for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
					{
						TabletopCard card = cards[cardIndex];
						cardContentIds?.Add(card.ContentId);
						if (!m_contentIndex.TryGet(card.ContentId, out CardDefinition definition))
						{
							throw new InvalidOperationException(
								$"牌桌卡牌 {card.Id} 的内容 {card.ContentId} 已不在当前内容集合中。");
						}

						if (definition is FoodCardDefinition food)
						{
							totalFoodNutrition = checked(
								totalFoodNutrition + food.NutritionPerUse * card.RemainingUses);
						}
						if (definition is CharacterCardDefinition)
						{
							characterCount = checked(characterCount + 1);
						}
						if (definition.CountsTowardCardLimit)
						{
							cardsOwned = checked(cardsOwned + 1);
						}
						if (m_currencyCardIds.Contains(card.ContentId))
						{
							currency = checked(currency + 1);
						}
						if (card is ChestCard chest && chest.StoredCurrencyCount > 0)
						{
							if (!m_contentIndex.TryGet(card.ContentId, out ChestCardDefinition chestDefinition))
							{
								throw new InvalidOperationException($"箱子卡牌 {card.Id} 的内容 {card.ContentId} 类型不一致。");
							}
							if (storedCurrencyByCard != null)
							{
								if (storedCurrencyByCard.TryGetValue(chestDefinition.CurrencyCardId, out int currentAmount))
								{
									storedCurrencyByCard[chestDefinition.CurrencyCardId] =
										checked(currentAmount + chest.StoredCurrencyCount);
								}
								else
								{
									storedCurrencyByCard.Add(chestDefinition.CurrencyCardId, chest.StoredCurrencyCount);
								}
							}
							currency = checked(currency + chest.StoredCurrencyCount);
						}
					}
				}
			}

			if (storedCurrencyByCard == null)
			{
				currencyStocks = null;
			}
			else
			{
				currencyStocks = new List<TabletopStateQuestTaskFact.CurrencyStock>(storedCurrencyByCard.Count);
				foreach (KeyValuePair<ContentId, int> pair in storedCurrencyByCard)
				{
					currencyStocks.Add(new TabletopStateQuestTaskFact.CurrencyStock(pair.Key, pair.Value));
				}
				currencyStocks.Sort((left, right) =>
					string.CompareOrdinal(left.CurrencyCardId.Value, right.CurrencyCardId.Value));
			}

			int cardLimit = checked(m_dayCycleRules.BaseCardLimit + cardLimitBonus);
			int nutritionNeed = checked(characterCount * m_dayCycleRules.HungerPerCharacter);
			return new ScenarioTabletopStats(
				totalFoodNutrition,
				nutritionNeed,
				currency,
				cardsOwned,
				cardLimit);
		}

		private static HashSet<ContentId> BuildCurrencyCardIds(ContentIndex contentIndex)
		{
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}

			HashSet<ContentId> currencyCardIds = new HashSet<ContentId>();
			IReadOnlyList<ContentAsset> assets = contentIndex.AllAssets;
			for (int i = 0; i < assets.Count; i++)
			{
				ContentAsset asset = assets[i];
				switch (asset)
				{
					case ChestCardDefinition chest when chest.CurrencyCardId.IsValid:
						currencyCardIds.Add(chest.CurrencyCardId);
						break;
					case ActionDefinition action:
						AddCurrencyCardIds(action.ResultIntents, currencyCardIds);
						IReadOnlyList<ActionResultBranchDefinition> branches = action.ResultBranches;
						for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
						{
							ActionResultBranchDefinition branch = branches[branchIndex];
							if (branch != null)
							{
								AddCurrencyCardIds(branch.ResultIntents, currencyCardIds);
							}
						}
						break;
				}
			}
			return currencyCardIds;
		}

		private ContentAsset RequireJournalEntryContent(ContentId contentId)
		{
			if (!contentId.IsValid)
			{
				throw new ArgumentException($"日志条目必须引用有效的 Gameplay 内容 ID：{contentId}。", nameof(contentId));
			}
			if (!m_contentIndex.TryGet(contentId, out ContentAsset asset))
			{
				throw new InvalidOperationException($"日志条目 {contentId} 不属于当前单局内容集合。");
			}
			if (!IsJournalEntryContent(asset))
			{
				throw new InvalidOperationException($"内容 {contentId} 不是当前日志可查看的任务或配方 / 行动。");
			}
			return asset;
		}

		private static bool IsJournalEntryContent(ContentAsset asset)
		{
			return asset is QuestDefinition or ActionDefinition;
		}

		private static float ResolveSecondsPerTurn(
			ScenarioDefinition definition,
			ScenarioStartOptions startOptions)
		{
			if (startOptions.DayDurationSecondsOverride.HasValue)
			{
				return startOptions.DayDurationSecondsOverride.Value / definition.TurnsPerDay;
			}
			return definition.SecondsPerTurn;
		}

		private static void AddCurrencyCardIds(
			IReadOnlyList<ActionResultIntent> resultIntents,
			ISet<ContentId> currencyCardIds)
		{
			for (int intentIndex = 0; intentIndex < resultIntents.Count; intentIndex++)
			{
				if (resultIntents[intentIndex] is SellCardsResultIntent sellIntent &&
					sellIntent.CurrencyCardId.IsValid)
				{
					currencyCardIds.Add(sellIntent.CurrencyCardId);
				}
			}
		}

		private void RequireActive()
		{
			if (m_isEnded)
			{
				throw new InvalidOperationException($"剧本 {ScenarioId} 已经结束，不能再推进单局状态。");
			}
		}
	}

	public readonly struct ScenarioTabletopStats
	{
		public int TotalFoodNutrition { get; }

		public int NutritionNeed { get; }

		public int Currency { get; }

		public int CardsOwned { get; }

		public int CardLimit { get; }

		public ScenarioTabletopStats(
			int totalFoodNutrition,
			int nutritionNeed,
			int currency,
			int cardsOwned,
			int cardLimit)
		{
			if (totalFoodNutrition < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(totalFoodNutrition));
			}
			if (nutritionNeed < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(nutritionNeed));
			}
			if (currency < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(currency));
			}
			if (cardsOwned < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(cardsOwned));
			}
			if (cardLimit < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(cardLimit));
			}

			TotalFoodNutrition = totalFoodNutrition;
			NutritionNeed = nutritionNeed;
			Currency = currency;
			CardsOwned = cardsOwned;
			CardLimit = cardLimit;
		}
	}
}
