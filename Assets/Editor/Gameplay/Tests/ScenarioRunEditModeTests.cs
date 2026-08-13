using System;
using System.Collections;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证剧本单局回合事实与生命周期边界的 EditMode 行为合同。
	/// </summary>
	public sealed class ScenarioRunEditModeTests
	{
		private static readonly Dictionary<ScenarioDefinition, ScenarioRegionDefinition> ScenarioRegions =
			new Dictionary<ScenarioDefinition, ScenarioRegionDefinition>();

		[TearDown]
		public void DestroyScenarioRegions()
		{
			foreach (ScenarioRegionDefinition region in ScenarioRegions.Values)
			{
				Object.DestroyImmediate(region);
			}
			ScenarioRegions.Clear();
		}

		[Test]
		public void ConfirmTurn_IncrementsRunIndexAndPublishesTheSameFact()
		{
			ScenarioDefinition definition = CreateScenario("test.scenario.turns");
			ScenarioRun run = CreateRun(definition);
			List<int> receivedTurnIndices = new List<int>();
			EventKit.Type.Register<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
			try
			{
				Assert.That<int>(run.ConfirmedTurnIndex, (IResolveConstraint)(object)Is.Zero);
				Assert.That<int>(run.ConfirmTurn(), (IResolveConstraint)(object)Is.EqualTo((object)1));
				Assert.That<int>(run.ConfirmTurn(), (IResolveConstraint)(object)Is.EqualTo((object)2));
				Assert.That<int>(run.ConfirmedTurnIndex, (IResolveConstraint)(object)Is.EqualTo((object)2));
				CollectionAssert.AreEqual((IEnumerable)new int[2] { 1, 2 }, (IEnumerable)receivedTurnIndices);
			}
			finally
			{
				EventKit.Type.UnRegister<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
				Object.DestroyImmediate((Object)(object)definition);
			}
			void OnScenarioTurnConfirmed(ScenarioTurnConfirmedEvent confirmedEvent)
			{
				Assert.That(confirmedEvent.ScenarioId, Is.EqualTo(run.ScenarioId));
				Assert.That<int>(run.ConfirmedTurnIndex, (IResolveConstraint)(object)Is.EqualTo((object)confirmedEvent.ConfirmedTurnIndex), "订阅者收到回合事实时，单局状态必须已经提交。", Array.Empty<object>());
				receivedTurnIndices.Add(confirmedEvent.ConfirmedTurnIndex);
			}
		}

		[Test]
		public void SeparateRuns_DoNotShareConfirmedTurnState()
		{
			ScenarioDefinition firstDefinition = CreateScenario("test.scenario.first");
			ScenarioDefinition secondDefinition = CreateScenario("test.scenario.second");
			try
			{
				ScenarioRun firstRun = CreateRun(firstDefinition);
				ScenarioRun secondRun = CreateRun(secondDefinition);
				firstRun.ConfirmTurn();
				Assert.That<int>(firstRun.ConfirmedTurnIndex, (IResolveConstraint)(object)Is.EqualTo((object)1));
				Assert.That<int>(secondRun.ConfirmedTurnIndex, (IResolveConstraint)(object)Is.Zero);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)firstDefinition);
				Object.DestroyImmediate((Object)(object)secondDefinition);
			}
		}

		[Test]
		public void CreateRun_RejectsMissingAuthoritativeRandomSeed()
		{
			ScenarioDefinition definition = CreateScenario("test.scenario.random-seed");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { definition });
				Assert.Throws<ArgumentOutOfRangeException>(() =>
					new ScenarioRun(definition, contentIndex, 0u));
			}
			finally
			{
				Object.DestroyImmediate(definition);
			}
		}

		[Test]
		public void ConfirmTurn_DerivesDayAndCompletesDayReachedQuestAtBoundary()
		{
			QuestDefinition quest = CreateDayQuest("test.scenario.day-quest", requiredDay: 2);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.day-boundary",
				turnsPerDay: 2,
				quest.ContentId.Value);
			List<ScenarioTurnConfirmedEvent> receivedEvents = new List<ScenarioTurnConfirmedEvent>();
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { quest, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				EventKit.Type.Register<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
				try
				{
					run.ActivateInitialQuests();
					Assert.That(run.CurrentDay, Is.EqualTo(1));
					Assert.That(run.ConfirmedTurnsInCurrentDay, Is.Zero);
					Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Active));

					run.ConfirmTurn();
					Assert.That(run.CurrentDay, Is.EqualTo(1));
					Assert.That(run.ConfirmedTurnsInCurrentDay, Is.EqualTo(1));
					Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Active));

					run.ConfirmTurn();
					Assert.That(run.CurrentDay, Is.EqualTo(2));
					Assert.That(run.ConfirmedTurnsInCurrentDay, Is.Zero);
					Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
					Assert.That(receivedEvents, Has.Count.EqualTo(2));
					Assert.That(receivedEvents[0].CurrentDay, Is.EqualTo(1));
					Assert.That(receivedEvents[0].ConfirmedTurnsInCurrentDay, Is.EqualTo(1));
					Assert.That(receivedEvents[1].CurrentDay, Is.EqualTo(2));
					Assert.That(receivedEvents[1].ConfirmedTurnsInCurrentDay, Is.Zero);
				}
				finally
				{
					EventKit.Type.UnRegister<ScenarioTurnConfirmedEvent>(OnScenarioTurnConfirmed);
				}

				void OnScenarioTurnConfirmed(ScenarioTurnConfirmedEvent confirmedEvent)
				{
					Assert.That(confirmedEvent.ScenarioId, Is.EqualTo(run.ScenarioId));
					Assert.That(run.CurrentDay, Is.EqualTo(confirmedEvent.CurrentDay));
					Assert.That(
						run.ConfirmedTurnsInCurrentDay,
						Is.EqualTo(confirmedEvent.ConfirmedTurnsInCurrentDay));
					if (confirmedEvent.CurrentDay == 2)
					{
						Assert.That(
							run.QuestLog.GetQuest(quest.ContentId).Status,
							Is.EqualTo(QuestStatus.Completed),
							"订阅者收到跨日回合事实时，按天数任务必须已经提交完成状态。");
					}
					receivedEvents.Add(confirmedEvent);
				}
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)quest);
				Object.DestroyImmediate((Object)(object)scenario);
			}
		}

		[Test]
		public void ScenarioDefinition_RejectsNonPositiveTurnsPerDay()
		{
			ScenarioDefinition scenario = CreateScenario("test.scenario.invalid-day", turnsPerDay: 0);
			try
			{
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
					BuildContentIndex(new ContentAsset[] { scenario }));

				StringAssert.Contains("SCENARIO_TURNS_PER_DAY_INVALID", exception.Message);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)scenario);
			}
		}

		[Test]
		public void ScenarioDefinition_RejectsNonPositiveSecondsPerTurn()
		{
			ScenarioDefinition scenario = CreateScenario("test.scenario.invalid-seconds", turnsPerDay: 2);
			JsonUtility.FromJsonOverwrite("{\"m_secondsPerTurn\":0}", scenario);
			try
			{
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
					BuildContentIndex(new ContentAsset[] { scenario }));

				StringAssert.Contains("SCENARIO_SECONDS_PER_TURN_INVALID", exception.Message);
			}
			finally
			{
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void RealTimeProgression_AdvancesTheSameWorldTurnTimeline()
		{
			ScenarioDefinition scenario = CreateScenario("test.scenario.real-time-turns", turnsPerDay: 2);
			JsonUtility.FromJsonOverwrite("{\"m_secondsPerTurn\":0.5}", scenario);
			List<ScenarioTurnConfirmedEvent> receivedEvents = new List<ScenarioTurnConfirmedEvent>();
			try
			{
				ScenarioRun run = new ScenarioRun(scenario, BuildContentIndex(new ContentAsset[] { scenario }), 12345u);
				EventKit.Type.Register<ScenarioTurnConfirmedEvent>(OnTurnConfirmed);
				run.UseRealTimeProgression();

				run.AdvanceRealTime(0.25f);
				Assert.That(run.ConfirmedTurnIndex, Is.Zero);
				run.AdvanceRealTime(0.25f);
				Assert.That(run.ConfirmedTurnIndex, Is.EqualTo(1));
				run.AdvanceRealTime(1f);

				Assert.That(run.ConfirmedTurnIndex, Is.EqualTo(3));
				Assert.That(run.CurrentDay, Is.EqualTo(2));
				Assert.That(receivedEvents, Has.Count.EqualTo(3));
				Assert.That(receivedEvents[0].ConfirmedTurnIndex, Is.EqualTo(1));
				Assert.That(receivedEvents[1].ConfirmedTurnIndex, Is.EqualTo(2));
				Assert.That(receivedEvents[2].ConfirmedTurnIndex, Is.EqualTo(3));
				Assert.Throws<InvalidOperationException>(() => run.ConfirmTurn());
			}
			finally
			{
				EventKit.Type.UnRegister<ScenarioTurnConfirmedEvent>(OnTurnConfirmed);
				Object.DestroyImmediate(scenario);
			}

			void OnTurnConfirmed(ScenarioTurnConfirmedEvent confirmedEvent)
			{
				receivedEvents.Add(confirmedEvent);
			}
		}

		[Test]
		public void ActivateInitialQuests_ReplaysDiscoveredContentToNewlyUnlockedDiscoveryQuest()
		{
			ActionDefinition discoveredAction = CreateAction("test.scenario.discovery.action", "test.scenario.discovery.card");
			CardDefinition card = CreateCard("test.scenario.discovery.card");
			QuestDefinition initialDayQuest = CreateDayQuest("test.scenario.discovery.day", requiredDay: 1);
			QuestDefinition discoveryQuest = CreateDiscoveryQuest(
				"test.scenario.discovery.quest",
				discoveredAction.ContentId.Value,
				initialDayQuest.ContentId.Value);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.discovery",
				initialDayQuest.ContentId.Value,
				discoveryQuest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					card,
					discoveredAction,
					initialDayQuest,
					discoveryQuest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);

				Assert.That(run.DiscoverContent(discoveredAction.ContentId), Is.True);
				run.ActivateInitialQuests();

				Assert.That(run.QuestLog.GetQuest(initialDayQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(run.QuestLog.GetQuest(discoveryQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)card);
				Object.DestroyImmediate((Object)(object)discoveredAction);
				Object.DestroyImmediate((Object)(object)initialDayQuest);
				Object.DestroyImmediate((Object)(object)discoveryQuest);
				Object.DestroyImmediate((Object)(object)scenario);
			}
		}

		[Test]
		public void CompletedAction_RefreshesCurrentDayForNewlyUnlockedQuest()
		{
			ActionDefinition action = CreateAction(
				"test.scenario.day-refresh.action",
				"test.scenario.day-refresh.card");
			CardDefinition card = CreateCard("test.scenario.day-refresh.card");
			QuestDefinition actionQuest = CreateActionQuest(
				"test.scenario.day-refresh.action-quest",
				action.ContentId.Value);
			QuestDefinition dayQuest = CreateDayQuest(
				"test.scenario.day-refresh.day-quest",
				requiredDay: 1);
			SerializedObject dayQuestObject = new SerializedObject(dayQuest);
			SerializedProperty prerequisites = dayQuestObject.FindProperty("m_prerequisiteQuestIds");
			prerequisites.arraySize = 1;
			prerequisites.GetArrayElementAtIndex(0).FindPropertyRelative("m_value").stringValue =
				actionQuest.ContentId.Value;
			dayQuestObject.ApplyModifiedPropertiesWithoutUndo();
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.day-refresh",
				actionQuest.ContentId.Value,
				dayQuest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					card,
					action,
					actionQuest,
					dayQuest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(action.ContentId);
				TabletopCard participant = run.Tabletop.CreateCard(card.ContentId, Vector2.zero);
				ActionCandidate[] candidates = run.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						Vector2.zero,
						Vector2.one,
						Vector2.zero,
						isDrag: true,
						default));

				run.StartAction(ActionRequest.FromCandidate(candidates[0]));
				run.ConfirmTurn();

				Assert.That(run.CurrentDay, Is.EqualTo(1));
				Assert.That(run.QuestLog.GetQuest(actionQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(
					run.QuestLog.GetQuest(dayQuest.ContentId).Status,
					Is.EqualTo(QuestStatus.Completed),
					"后置解锁的状态型任务必须立即读取当前已经成立的日期事实。");
			}
			finally
			{
				Object.DestroyImmediate(card);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(actionQuest);
				Object.DestroyImmediate(dayQuest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void EndRun_RejectsMutationThroughPreviouslyCapturedTabletop()
		{
			CardDefinition card = CreateCard("test.scenario-ended.card");
			ScenarioDefinition scenario = CreateScenario("test.scenario-ended");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(
					new ContentAsset[] { card, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				Gameplay.Tabletop.Tabletop capturedTabletop = run.Tabletop;

				run.End();

				Assert.Throws<InvalidOperationException>(() =>
					capturedTabletop.CreateCard(card.ContentId, Vector2.zero));
			}
			finally
			{
				Object.DestroyImmediate(card);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void CompletedAction_UpdatesOwningQuestLogBeforePublishingFact()
		{
			CardDefinition card = CreateCard("test.scenario-run.card");
			ActionDefinition action = CreateAction(
				"test.scenario-run.action",
				card.ContentId.Value);
			QuestDefinition quest = CreateActionQuest(
				"test.scenario-run.quest",
				action.ContentId.Value);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario-run.action-fact",
				quest.ContentId.Value);
			ActionCompletedEvent? receivedEvent = null;
			ScenarioRun run = null;
			try
			{
				ContentIndex contentIndex = BuildContentIndex(
					new ContentAsset[] { card, action, quest, scenario });
				run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(action.ContentId);
				TabletopCard participant = run.Tabletop.CreateCard(card.ContentId, Vector2.zero);
				ActionCandidate[] candidates = run.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						Vector2.zero,
						Vector2.one,
						Vector2.zero,
						isDrag: true,
						default));
				Assert.That(candidates, Has.Length.EqualTo(1));

				EventKit.Type.Register<ActionCompletedEvent>(OnActionCompleted);
				run.StartAction(ActionRequest.FromCandidate(candidates[0]));
				run.ConfirmTurn();

				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(receivedEvent?.ActionId, Is.EqualTo(action.ContentId));
			}
			finally
			{
				EventKit.Type.UnRegister<ActionCompletedEvent>(OnActionCompleted);
				Object.DestroyImmediate(card);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}

			void OnActionCompleted(ActionCompletedEvent completedEvent)
			{
				Assert.That(completedEvent.ScenarioId, Is.EqualTo(run.ScenarioId));
				Assert.That(
					run.QuestLog.GetQuest(quest.ContentId).Status,
					Is.EqualTo(QuestStatus.Completed),
					"对外发布行动完成事实前，所属单局任务日志必须已经提交。");
				receivedEvent = completedEvent;
			}
		}

		[Test]
		public void Snapshot_RestoresTheWholeRunAfterJsonRoundTrip()
		{
			CardDefinition worker = CreateCard("test.snapshot.worker");
			ActionDefinition action = CreateAction(
				"test.snapshot.action",
				worker.ContentId.Value,
				turnCost: 2);
			QuestDefinition quest = CreateActionQuest(
				"test.snapshot.quest",
				action.ContentId.Value,
				requiredCompletionCount: 2);
			ScenarioDefinition scenario = CreateTwoRegionScenario(
				"test.snapshot.scenario",
				out ScenarioRegionDefinition secondRegion,
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					worker,
					action,
					quest,
					scenario,
					secondRegion
				});
				ScenarioRun original = new ScenarioRun(scenario, contentIndex, 12345u);
				original.ActivateInitialQuests();
				original.DiscoverContent(action.ContentId);

				TabletopCard participant = original.Tabletop.CreateCard(worker.ContentId, new Vector2(-1f, 0f));
				TabletopCard traveler = original.Tabletop.CreateCard(worker.ContentId, new Vector2(1f, 0f));
				ActionCandidate[] candidates = original.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						participant.Stack.Position,
						Vector2.one,
						participant.Stack.Position,
						isDrag: true,
						default));
				original.StartAction(ActionRequest.FromCandidate(candidates[0]));
				original.ConfirmTurn();
				original.ConfirmTurn();
				candidates = original.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						participant.Stack.Position,
						Vector2.one,
						participant.Stack.Position,
						isDrag: true,
						default));
				original.StartAction(ActionRequest.FromCandidate(candidates[0]));
				original.ConfirmTurn();
				ScenarioTravelPlan travel = original.BeginTravel(
					secondRegion.ContentId,
					new[] { traveler.Id });
				original.CommitTravel(travel);
				original.UseRealTimeProgression();
				original.AdvanceRealTime(original.SecondsPerTurn * 0.25f);

				string json = JsonUtility.ToJson(original.CreateSnapshot());
				ScenarioRunSnapshot serialized = JsonUtility.FromJson<ScenarioRunSnapshot>(json);
				ScenarioRun restored = ScenarioRun.Restore(scenario, contentIndex, serialized);

				Assert.That(restored.ActiveRegion.Id, Is.EqualTo(secondRegion.ContentId));
				Assert.That(restored.ConfirmedTurnIndex, Is.EqualTo(3));
				Assert.That(restored.ProgressionMode, Is.EqualTo(ActionProgressionMode.RealTime));
				Assert.That(restored.NormalizedDayProgress, Is.EqualTo(0.625f).Within(0.0001f));
				Assert.That(restored.IsContentDiscovered(action.ContentId), Is.True);
				Assert.That(restored.QuestLog.GetQuest(quest.ContentId).Tasks[0].Progress.CurrentAmount, Is.EqualTo(1));
				Assert.That(restored.GetRegion(ScenarioRegions[scenario].ContentId).Tabletop.ActiveActions, Has.Count.EqualTo(1));
				Assert.That(restored.GetRegion(ScenarioRegions[scenario].ContentId).Tabletop.ActiveActions[0].ProgressedTurns, Is.EqualTo(1.25f));
				Assert.That(restored.ActiveRegion.Tabletop.Cards.TryGetCard(traveler.Id, out _), Is.True);
				for (int i = 0; i < original.Regions.Count; i++)
				{
					Assert.That(
						restored.GetRegion(original.Regions[i].Id).Tabletop.AuthoritativeRandomState,
						Is.EqualTo(original.Regions[i].Tabletop.AuthoritativeRandomState));
				}

				TabletopCard nextCard = restored.ActiveRegion.Tabletop.CreateCard(worker.ContentId, new Vector2(2f, 0f));
				Assert.That(nextCard.Id.Value, Is.EqualTo(3uL), "跨地区恢复后必须继续使用同一份单局卡牌编号序列。");
			}
			finally
			{
				Object.DestroyImmediate(worker);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
				Object.DestroyImmediate(secondRegion);
			}
		}

		[Test]
		public void Snapshot_DoesNotPersistUncommittedActionPlans()
		{
			CardDefinition worker = CreateCard("test.snapshot-plan.worker");
			ActionDefinition action = CreateAction(
				"test.snapshot-plan.action",
				worker.ContentId.Value);
			ScenarioDefinition scenario = CreateScenario("test.snapshot-plan.scenario");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { worker, action, scenario });
				ScenarioRun original = new ScenarioRun(scenario, contentIndex, 12345u);
				original.ActivateInitialQuests();
				original.DiscoverContent(action.ContentId);
				TabletopCard participant = original.Tabletop.CreateCard(worker.ContentId, Vector2.zero);
				ActionCandidate candidate = original.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						Vector2.zero,
						Vector2.one,
						Vector2.zero,
						isDrag: true,
						default))[0];
				original.CreateActionPlan(candidate);

				ScenarioRun restored = ScenarioRun.Restore(
					scenario,
					contentIndex,
					JsonUtility.FromJson<ScenarioRunSnapshot>(JsonUtility.ToJson(original.CreateSnapshot())));

				Assert.That(original.Tabletop.ActionPlans, Has.Count.EqualTo(1));
				Assert.That(restored.Tabletop.ActionPlans, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate(worker);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(scenario);
			}
		}

		private static ScenarioRun CreateRun(ScenarioDefinition definition)
		{
			ContentIndex contentIndex = BuildContentIndex(new ContentAsset[1] { definition });
			return new ScenarioRun(definition, contentIndex, 12345u);
		}

		private static ScenarioDefinition CreateScenario(
			string contentId,
			params string[] questIds)
		{
			return CreateScenario(contentId, turnsPerDay: 2, questIds);
		}

		private static ScenarioDefinition CreateTwoRegionScenario(
			string contentId,
			out ScenarioRegionDefinition secondRegion,
			params string[] questIds)
		{
			ScenarioDefinition definition = CreateScenario(contentId, questIds);
			ScenarioRegionDefinition initialRegion = ScenarioRegions[definition];
			secondRegion = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			string secondRegionId = contentId + ".region-2";
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + secondRegionId + "\"}}",
				secondRegion);
			string questJson = string.Join(",", System.Array.ConvertAll(
				questIds,
				questId => "{\"m_value\":\"" + questId + "\"}"));
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
					"\"},\"m_turnsPerDay\":2,\"m_initialRegionId\":{\"m_value\":\"" + initialRegion.ContentId.Value +
					"\"},\"m_regionIds\":[{\"m_value\":\"" + initialRegion.ContentId.Value +
					"\"},{\"m_value\":\"" + secondRegionId + "\"}],\"m_questIds\":[" + questJson + "]}",
				definition);
			return definition;
		}

		private static ScenarioDefinition CreateScenario(
			string contentId,
			int turnsPerDay,
			params string[] questIds)
		{
			ScenarioDefinition definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
			ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
			string regionId = contentId + ".region";
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + regionId + "\"}}",
				region);
			string questJson = string.Join(",", System.Array.ConvertAll(
				questIds,
				questId => "{\"m_value\":\"" + questId + "\"}"));
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
					"\"},\"m_turnsPerDay\":" + turnsPerDay +
					",\"m_initialRegionId\":{\"m_value\":\"" + regionId +
					"\"},\"m_regionIds\":[{\"m_value\":\"" + regionId + "\"}]" +
					",\"m_questIds\":[" + questJson + "]}",
				definition);
			ScenarioRegions.Add(definition, region);
			return definition;
		}

		private static ContentIndex BuildContentIndex(IEnumerable<ContentAsset> assets)
		{
			List<ContentAsset> content = new List<ContentAsset>(assets);
			for (int i = 0; i < content.Count; i++)
			{
				if (content[i] is ScenarioDefinition scenario &&
					ScenarioRegions.TryGetValue(scenario, out ScenarioRegionDefinition region) &&
					!content.Contains(region))
				{
					content.Add(region);
				}
			}
			return ContentIndex.Build(content);
		}

		private static QuestDefinition CreateDayQuest(string contentId, int requiredDay)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new DayReachedQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_requiredDay").intValue = requiredDay;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateDiscoveryQuest(
			string contentId,
			string discoveredContentId,
			params string[] prerequisiteQuestIds)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			string prerequisitesJson = string.Join(",", System.Array.ConvertAll(
				prerequisiteQuestIds,
				prerequisiteId => "{\"m_value\":\"" + prerequisiteId + "\"}"));
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_prerequisiteQuestIds\":[" + prerequisitesJson + "]}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new ContentDiscoveryQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_discoveredContentId")
				.FindPropertyRelative("m_value").stringValue = discoveredContentId;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			return definition;
		}

		private static ActionDefinition CreateAction(
			string contentId,
			string cardContentId,
			int turnCost = 1)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_turnCost\":" + turnCost + ",\"m_participationSlots\":[{" +
				"\"m_key\":\"slot-1\",\"m_minimumParticipants\":1," +
				"\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{" +
				"\"m_value\":\"" + cardContentId + "\"}]}]}",
				definition);
			return definition;
		}

		private static QuestDefinition CreateActionQuest(
			string contentId,
			string actionId,
			int requiredCompletionCount = 1)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new ActionCompletionQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_actionId")
				.FindPropertyRelative("m_value").stringValue = actionId;
			task.FindPropertyRelative("m_requiredCompletionCount").intValue = requiredCompletionCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}
	}
}
