using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GAS.Runtime;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using GameCore;
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
		public void DayCycle_WaitsForEndOfDayAndNewDayConfirmationsBeforeAdvancingDate()
		{
			CharacterCardDefinition character = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.day-cycle.character\"},\"m_abilitySystemPresetId\":1001}",
				character);
			QuestDefinition quest = CreateDayQuest("test.day-cycle.day-two", requiredDay: 2);
			ScenarioDefinition scenario = CreateScenario(
				"test.day-cycle.scenario",
				turnsPerDay: 2,
				quest.ContentId.Value);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":10}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { character, quest, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.Tabletop.CreateCard(character.ContentId, Vector2.zero);

				run.ConfirmTurn();
				run.ConfirmTurn();

				Assert.That(run.CurrentDay, Is.EqualTo(1));
				Assert.That(run.ConfirmedTurnsInCurrentDay, Is.EqualTo(2));
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingFeedingConfirmation));
				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Active));
				Assert.Throws<InvalidOperationException>(() => run.ConfirmTurn());

				run.ContinueDayCycle();
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));

				run.ContinueDayCycle();

				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.Inactive));
				Assert.That(run.CurrentDay, Is.EqualTo(2));
				Assert.That(run.ConfirmedTurnsInCurrentDay, Is.Zero);
				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void DayCycle_FeedingConsumesNearestFoodAndKillsCharactersWhenFoodRunsOut()
		{
			CharacterCardDefinition fedCharacter = CreateCharacterCard("test.day-cycle.fed-character");
			CharacterCardDefinition hungryCharacter = CreateCharacterCard("test.day-cycle.hungry-character");
			FoodCardDefinition food = ScriptableObject.CreateInstance<FoodCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.day-cycle.food\"},\"m_initialUses\":1,\"m_nutritionPerUse\":1}",
				food);
			ScenarioDefinition scenario = CreateScenario("test.day-cycle.feeding", turnsPerDay: 1);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":1,\"m_baseCardLimit\":10}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					fedCharacter,
					hungryCharacter,
					food,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				TabletopCard fed = run.Tabletop.CreateCard(fedCharacter.ContentId, Vector2.zero);
				TabletopCard hungry = run.Tabletop.CreateCard(hungryCharacter.ContentId, new Vector2(10f, 0f));
				TabletopCard meal = run.Tabletop.CreateCard(food.ContentId, new Vector2(1f, 0f));

				run.ConfirmTurn();
				run.ContinueDayCycle();

				Assert.That(run.Tabletop.Cards.TryGetCard(fed.Id, out _), Is.True);
				Assert.That(run.Tabletop.Cards.TryGetCard(hungry.Id, out _), Is.False);
				Assert.That(run.Tabletop.Cards.TryGetCard(meal.Id, out _), Is.False);
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(fedCharacter);
				Object.DestroyImmediate(hungryCharacter);
				Object.DestroyImmediate(food);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void DayCycle_EntersGameOverWhenFeedingLeavesNoCharacters()
		{
			CharacterCardDefinition character = CreateCharacterCard("test.day-cycle.game-over.character");
			ScenarioDefinition scenario = CreateScenario("test.day-cycle.game-over", turnsPerDay: 1);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":1,\"m_baseCardLimit\":10}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { character, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				TabletopCard survivor = run.Tabletop.CreateCard(character.ContentId, Vector2.zero);

				run.ConfirmTurn();
				run.ContinueDayCycle();

				Assert.That(run.Tabletop.Cards.TryGetCard(survivor.Id, out _), Is.False);
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.GameOver));
				Assert.Throws<InvalidOperationException>(() => run.ContinueDayCycle());
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(scenario);
			}
		}
		[Test]
		public void DayCycle_RequiresActualTabletopCardsToBeReducedBelowTheConfiguredLimit()
		{
			CharacterCardDefinition character = CreateCharacterCard("test.day-cycle.limit-character");
			CardDefinition excessCard = CreateCard("test.day-cycle.excess-card");
			ScenarioDefinition scenario = CreateScenario("test.day-cycle.limit", turnsPerDay: 1);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":1}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { character, excessCard, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.Tabletop.CreateCard(character.ContentId, Vector2.zero);
				TabletopCard removable = run.Tabletop.CreateCard(excessCard.ContentId, Vector2.one);

				run.ConfirmTurn();
				run.ContinueDayCycle();

				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingExcessCardResolution));
				Assert.That(run.ExcessCardCount, Is.EqualTo(1));
				Assert.Throws<InvalidOperationException>(() => run.ContinueDayCycle());

				run.Tabletop.RemoveCard(removable.Id);
				run.ContinueDayCycle();

				Assert.That(run.ExcessCardCount, Is.Zero);
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(excessCard);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void DayCycle_CurrencyDoesNotUseCardLimitAndBoosterRaisesTheLimit()
		{
			CharacterCardDefinition character = CreateCharacterCard("test.day-cycle.limit-rules.character");
			CardDefinition currency = CreateCard("test.day-cycle.limit-rules.currency");
			CardDefinition booster = CreateCard("test.day-cycle.limit-rules.booster");
			CardDefinition normal = CreateCard("test.day-cycle.limit-rules.normal");
			JsonUtility.FromJsonOverwrite("{\"m_countsTowardCardLimit\":false}", currency);
			JsonUtility.FromJsonOverwrite("{\"m_cardLimitBonus\":2}", booster);
			ScenarioDefinition scenario = CreateScenario("test.day-cycle.limit-rules", turnsPerDay: 1);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":1}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					character,
					currency,
					booster,
					normal,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.Tabletop.CreateCard(character.ContentId, Vector2.zero);
				run.Tabletop.CreateCard(currency.ContentId, Vector2.right);
				run.Tabletop.CreateCard(booster.ContentId, Vector2.left);
				run.Tabletop.CreateCard(normal.ContentId, Vector2.up);

				run.ConfirmTurn();
				run.ContinueDayCycle();

				Assert.That(run.ExcessCardCount, Is.Zero);
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(currency);
				Object.DestroyImmediate(booster);
				Object.DestroyImmediate(normal);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void DayCycle_ExecutesAtMostOneEligibleEncounterAndRemembersOneTimeCompletion()
		{
			CharacterCardDefinition character = CreateCharacterCard("test.day-cycle.encounter-character");
			CardDefinition eventCard = CreateCard("test.day-cycle.encounter-card");
			ScenarioDefinition scenario = CreateScenario("test.day-cycle.encounter", turnsPerDay: 1);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":10," +
				"\"m_encounters\":[{\"m_key\":\"night-event\",\"m_cardId\":{\"m_value\":\"test.day-cycle.encounter-card\"}," +
				"\"m_notificationMessage\":\"夜里传来了陌生脚步声。\"," +
				"\"m_count\":1,\"m_oneTimeOnly\":true,\"m_minimumDay\":1,\"m_maximumDay\":99," +
				"\"m_interval\":0,\"m_priority\":10,\"m_chance\":1.0,\"m_maxCardsOnTabletop\":100}]}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { character, eventCard, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.Tabletop.CreateCard(character.ContentId, Vector2.zero);

				run.ConfirmTurn();
				run.ContinueDayCycle();
				Assert.That(CountCards(run, eventCard.ContentId), Is.EqualTo(1));
				Assert.That(run.DayEncounterResult.HasValue, Is.True);
				Assert.That(run.DayEncounterResult.Value.CardId, Is.EqualTo(eventCard.ContentId));
				Assert.That(run.DayEncounterResult.Value.Count, Is.EqualTo(1));
				Assert.That(run.DayEncounterResult.Value.NotificationMessage, Is.EqualTo("夜里传来了陌生脚步声。"));
				run.ContinueDayCycle();
				Assert.That(run.DayEncounterResult.HasValue, Is.False);

				run.ConfirmTurn();
				run.ContinueDayCycle();

				Assert.That(CountCards(run, eventCard.ContentId), Is.EqualTo(1));
				Assert.That(run.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(eventCard);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void DayCycle_CreatedEncounterCardsAdvanceCardCreationQuest()
		{
			CharacterCardDefinition character = CreateCharacterCard("test.day-cycle.obtain-character");
			CardDefinition eventCard = CreateCard("test.day-cycle.obtain-encounter-card");
			QuestDefinition quest = CreateCardCreationQuest(
				"test.day-cycle.obtain-quest",
				eventCard.ContentId.Value,
				requiredCreatedCount: 1);
			ScenarioDefinition scenario = CreateScenario(
				"test.day-cycle.obtain-scenario",
				turnsPerDay: 1,
				quest.ContentId.Value);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":10," +
				"\"m_encounters\":[{\"m_key\":\"obtain-event\",\"m_cardId\":{\"m_value\":\"test.day-cycle.obtain-encounter-card\"}," +
				"\"m_count\":1,\"m_oneTimeOnly\":true,\"m_minimumDay\":1,\"m_maximumDay\":99," +
				"\"m_interval\":0,\"m_priority\":10,\"m_chance\":1.0,\"m_maxCardsOnTabletop\":100}]}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					character,
					eventCard,
					quest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.Tabletop.CreateCard(character.ContentId, Vector2.zero);

				run.ConfirmTurn();
				run.ContinueDayCycle();

				QuestProgress progress = run.QuestLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(1));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(eventCard);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void DayCycle_FriendlyModeSkipsEnemyTaggedEncounterAndPersistsThroughSnapshot()
		{
			CharacterCardDefinition character = CreateCharacterCard("test.day-cycle.friendly-character");
			CardDefinition enemyEventCard = CreateCard("test.day-cycle.friendly-enemy-event");
			SetContentTags(enemyEventCard, XTag.Faction_Enemy);
			ScenarioDefinition scenario = CreateScenario("test.day-cycle.friendly-mode", turnsPerDay: 1);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":10," +
				"\"m_encounters\":[{\"m_key\":\"enemy-night-event\",\"m_cardId\":{\"m_value\":\"test.day-cycle.friendly-enemy-event\"}," +
				"\"m_count\":1,\"m_oneTimeOnly\":false,\"m_minimumDay\":1,\"m_maximumDay\":99," +
				"\"m_interval\":0,\"m_priority\":10,\"m_chance\":1.0,\"m_maxCardsOnTabletop\":100}]}}",
				scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { character, enemyEventCard, scenario });
				ScenarioRun friendlyRun = new ScenarioRun(
					scenario,
					contentIndex,
					12345u,
					new ScenarioStartOptions(friendlyMode: true));
				friendlyRun.Tabletop.CreateCard(character.ContentId, Vector2.zero);
				ScenarioRunSnapshot snapshot = JsonUtility.FromJson<ScenarioRunSnapshot>(
					JsonUtility.ToJson(friendlyRun.CreateSnapshot()));
				ScenarioRun restoredFriendlyRun = ScenarioRun.Restore(scenario, contentIndex, snapshot);

				Assert.That(restoredFriendlyRun.FriendlyMode, Is.True);
				restoredFriendlyRun.ConfirmTurn();
				restoredFriendlyRun.ContinueDayCycle();

				Assert.That(CountCards(restoredFriendlyRun, enemyEventCard.ContentId), Is.Zero);
				Assert.That(restoredFriendlyRun.DayEncounterResult.HasValue, Is.False);
				Assert.That(restoredFriendlyRun.DayCyclePhase, Is.EqualTo(ScenarioDayCyclePhase.AwaitingNewDayConfirmation));

				ScenarioRun normalRun = new ScenarioRun(scenario, contentIndex, 12345u);
				normalRun.Tabletop.CreateCard(character.ContentId, Vector2.zero);
				normalRun.ConfirmTurn();
				normalRun.ContinueDayCycle();

				Assert.That(normalRun.FriendlyMode, Is.False);
				Assert.That(CountCards(normalRun, enemyEventCard.ContentId), Is.EqualTo(1));
				Assert.That(normalRun.DayEncounterResult.HasValue, Is.True);
				Assert.That(normalRun.DayEncounterResult.Value.CardId, Is.EqualTo(enemyEventCard.ContentId));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(character);
				Object.DestroyImmediate(enemyEventCard);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void BattleDefeat_RemovedEnemyAdvancesDefeatQuest()
		{
			CharacterCardDefinition allyDefinition = CreateCharacterCard("test.defeat.ally");
			CharacterCardDefinition enemyDefinition = CreateCharacterCard("test.defeat.enemy");
			QuestDefinition quest = CreateDefeatQuest(
				"test.quest.defeat-enemy",
				enemyDefinition.ContentId.Value,
				requiredDefeatCount: 1);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario.defeat",
				turnsPerDay: 2,
				quest.ContentId.Value);
			ConfigureTwoSideBattleFormation(scenario);
			XLuban.LoadTablesForEditor();
			InvokeFormalGasBootstrap("EnsureInitialized");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					allyDefinition,
					enemyDefinition,
					quest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				CharacterCard ally = (CharacterCard)run.Tabletop.CreateCard(allyDefinition.ContentId, Vector2.zero);
				CharacterCard enemy = (CharacterCard)run.Tabletop.CreateCard(enemyDefinition.ContentId, Vector2.one);
				run.Tabletop.StartBattle(new[] { ally.Id }, new[] { enemy.Id });

				enemy.AbilitySystem.SetAttrBaseValue(XAttrSet.FightUnit, XAttribute.Health, 0f);
				RecalculateFightUnitHealth(enemy.AbilitySystem);
				Assert.That(enemy.CurrentHealth, Is.EqualTo(0f));
				run.AdvanceRealTime(0.1f);

				Assert.That(run.Tabletop.Cards.TryGetCard(enemy.Id, out _), Is.False);
				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				InvokeFormalGasBootstrap("Shutdown");
				Object.DestroyImmediate(allyDefinition);
				Object.DestroyImmediate(enemyDefinition);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void ActivateInitialQuests_EvaluatesCurrentTabletopStateTasks()
		{
			CardDefinition wood = CreateCard("test.state.quest.wood");
			FoodCardDefinition food = ScriptableObject.CreateInstance<FoodCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"test.state.quest.food\"},\"m_initialUses\":2,\"m_nutritionPerUse\":2}",
				food);
			CardDefinition coin = CreateCard("test.state.quest.coin");
			JsonUtility.FromJsonOverwrite("{\"m_countsTowardCardLimit\":false}", coin);
			CardDefinition booster = CreateCard("test.state.quest.booster");
			JsonUtility.FromJsonOverwrite("{\"m_cardLimitBonus\":2}", booster);
			QuestDefinition haveQuest = CreateCardPossessionQuest(
				"test.state.quest.have",
				wood.ContentId.Value,
				requiredCardCount: 2);
			QuestDefinition foodQuest = CreateFoodNutritionQuest(
				"test.state.quest.food-stock",
				requiredNutrition: 4);
			QuestDefinition coinsQuest = CreateCurrencyAmountQuest(
				"test.state.quest.coins",
				coin.ContentId.Value,
				requiredAmount: 2);
			QuestDefinition capacityQuest = CreateCardCapacityQuest(
				"test.state.quest.capacity",
				requiredCapacity: 5);
			ScenarioDefinition scenario = CreateScenario(
				"test.state.quest.scenario",
				turnsPerDay: 2,
				haveQuest.ContentId.Value,
				foodQuest.ContentId.Value,
				coinsQuest.ContentId.Value,
				capacityQuest.ContentId.Value);
			JsonUtility.FromJsonOverwrite(
				"{\"m_dayCycleRules\":{\"m_enabled\":true,\"m_hungerPerCharacter\":0,\"m_baseCardLimit\":3}}",
				scenario);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					wood,
					food,
					coin,
					booster,
					haveQuest,
					foodQuest,
					coinsQuest,
					capacityQuest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.Tabletop.CreateCardStack(wood.ContentId, 2, Vector2.zero);
				run.Tabletop.CreateCard(food.ContentId, Vector2.right);
				run.Tabletop.CreateCardStack(coin.ContentId, 2, Vector2.left);
				run.Tabletop.CreateCard(booster.ContentId, Vector2.up);

				run.ActivateInitialQuests();

				Assert.That(run.QuestLog.GetQuest(haveQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(run.QuestLog.GetQuest(foodQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(run.QuestLog.GetQuest(coinsQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(run.QuestLog.GetQuest(capacityQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Object.DestroyImmediate(wood);
				Object.DestroyImmediate(food);
				Object.DestroyImmediate(coin);
				Object.DestroyImmediate(booster);
				Object.DestroyImmediate(haveQuest);
				Object.DestroyImmediate(foodQuest);
				Object.DestroyImmediate(coinsQuest);
				Object.DestroyImmediate(capacityQuest);
				Object.DestroyImmediate(scenario);
			}
		}

		private static void InvokeFormalGasBootstrap(string methodName)
		{
			Type bootstrapType = typeof(GameManager).Assembly.GetType(
				"GameCore.FormalAbilityRuntimeBootstrap",
				throwOnError: true);
			MethodInfo method = bootstrapType.GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new InvalidOperationException($"找不到 FormalAbilityRuntimeBootstrap.{methodName}。");
			}
			method.Invoke(null, null);
		}

		private static void RecalculateFightUnitHealth(AbilitySystemCell abilitySystem)
		{
			Type helperType = typeof(AbilitySystemCell).Assembly.GetType(
				"GAS.Runtime.AttributeHelper",
				throwOnError: true);
			MethodInfo method = helperType.GetMethod(
				"RecalculateCurrentValue",
				BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				throw new InvalidOperationException("找不到 AttributeHelper.RecalculateCurrentValue。");
			}
			object entity = typeof(AbilitySystemCell)
				.GetProperty("Entity")
				.GetValue(abilitySystem);
			method.Invoke(null, new[] { entity, XAttrSet.FightUnit, XAttribute.Health });
		}

		private static CharacterCardDefinition CreateCharacterCard(string contentId)
		{
			CharacterCardDefinition definition = ScriptableObject.CreateInstance<CharacterCardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_abilitySystemPresetId\":1001}",
				definition);
			return definition;
		}

		private static QuestDefinition CreateDefeatQuest(
			string contentId,
			string defeatedCardId,
			int requiredDefeatCount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CardDefeatQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId")
				.FindPropertyRelative("m_value").stringValue = defeatedCardId;
			task.FindPropertyRelative("m_requiredDefeatCount").intValue = requiredDefeatCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static void ConfigureTwoSideBattleFormation(ScenarioDefinition scenario)
		{
			SerializedObject serializedScenario = new(scenario);
			SerializedProperty formation = serializedScenario.FindProperty("m_battleFormationRules");
			SerializedProperty layouts = formation.FindPropertyRelative("m_sideLayouts");
			layouts.arraySize = 2;
			SetSideLayout(layouts.GetArrayElementAtIndex(0), new Vector2(-1f, 0f), Vector2.right, Vector2.down, 2);
			SetSideLayout(layouts.GetArrayElementAtIndex(1), new Vector2(1f, 0f), Vector2.left, Vector2.up, 2);
			serializedScenario.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetSideLayout(
			SerializedProperty layout,
			Vector2 centerOffset,
			Vector2 columnStep,
			Vector2 rankStep,
			int columnsPerRank)
		{
			layout.FindPropertyRelative("m_centerOffset").vector2Value = centerOffset;
			layout.FindPropertyRelative("m_columnStep").vector2Value = columnStep;
			layout.FindPropertyRelative("m_rankStep").vector2Value = rankStep;
			layout.FindPropertyRelative("m_columnsPerRank").intValue = columnsPerRank;
		}

		private static int CountCards(ScenarioRun run, ContentId contentId)
		{
			int count = 0;
			for (int stackIndex = 0; stackIndex < run.Tabletop.Cards.Stacks.Count; stackIndex++)
			{
				IReadOnlyList<TabletopCard> cards = run.Tabletop.Cards.Stacks[stackIndex].Cards;
				for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
				{
					if (cards[cardIndex].ContentId == contentId)
					{
						count++;
					}
				}
			}
			return count;
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
		public void StartOptions_DayDurationOverrideDefinesPerTurnSeconds()
		{
			ScenarioDefinition scenario = CreateScenario("test.scenario.day-duration", turnsPerDay: 4);
			JsonUtility.FromJsonOverwrite("{\"m_secondsPerTurn\":1}", scenario);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { scenario });
				ScenarioRun run = new ScenarioRun(
					scenario,
					contentIndex,
					12345u,
					new ScenarioStartOptions(friendlyMode: false, dayDurationSecondsOverride: 20f));

				Assert.That(run.SecondsPerTurn, Is.EqualTo(5f).Within(0.0001f));
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
		public void CompletedAction_CreatedProductsAdvanceCardCreationQuest()
		{
			CardDefinition worker = CreateCard("test.scenario-create.worker");
			CardDefinition product = CreateCard("test.scenario-create.product");
			ActionDefinition action = CreateProductAction(
				"test.scenario-create.action",
				worker.ContentId.Value,
				product.ContentId.Value,
				2);
			QuestDefinition quest = CreateCardCreationQuest(
				"test.scenario-create.quest",
				product.ContentId.Value,
				requiredCreatedCount: 2);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario-create.scenario",
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					worker,
					product,
					action,
					quest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(action.ContentId);
				TabletopCard participant = run.Tabletop.CreateCard(worker.ContentId, Vector2.zero);
				ActionCandidate candidate = run.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						Vector2.zero,
						Vector2.one,
						Vector2.zero,
						isDrag: true,
						default))[0];

				run.StartAction(ActionRequest.FromCandidate(candidate));
				run.ConfirmTurn();

				QuestProgress progress = run.QuestLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
			}
			finally
			{
				Object.DestroyImmediate(worker);
				Object.DestroyImmediate(product);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void CompletedResearchAction_AdvancesContentDiscoveryQuest()
		{
			CardDefinition worker = CreateCard("test.scenario-discover.worker");
			CardDefinition recipeCard = CreateCard("test.scenario-discover.recipe-card");
			ActionDefinition unlockedAction = CreateAction(
				"test.scenario-discover.unlocked-action",
				worker.ContentId.Value);
			ActionDefinition researchAction = CreateResearchAction(
				"test.scenario-discover.research-action",
				worker.ContentId.Value,
				unlockedAction.ContentId.Value,
				recipeCard.ContentId.Value);
			QuestDefinition quest = CreateDiscoveryQuest(
				"test.scenario-discover.quest",
				unlockedAction.ContentId.Value);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario-discover.scenario",
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					worker,
					recipeCard,
					unlockedAction,
					researchAction,
					quest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(researchAction.ContentId);
				TabletopCard participant = run.Tabletop.CreateCard(worker.ContentId, Vector2.zero);
				ActionCandidate candidate = run.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						participant.Id,
						Vector2.zero,
						Vector2.one,
						Vector2.zero,
						isDrag: true,
						default))[0];

				run.StartAction(ActionRequest.FromCandidate(candidate));
				run.ConfirmTurn();

				QuestProgress progress = run.QuestLog.GetQuest(quest.ContentId);
				Assert.That(run.IsContentDiscovered(unlockedAction.ContentId), Is.True);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(1));
				Assert.That(CountCards(run, recipeCard.ContentId), Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(worker);
				Object.DestroyImmediate(recipeCard);
				Object.DestroyImmediate(unlockedAction);
				Object.DestroyImmediate(researchAction);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void CompletedSaleAction_AdvancesCardSaleQuest()
		{
			CardDefinition sellable = CreateCard("test.scenario-sell.sellable");
			CardDefinition coin = CreateCard("test.scenario-sell.coin");
			CardBuyerDefinition buyer = CreateBuyer("test.scenario-sell.buyer", coin.ContentId);
			SetSellValue(sellable, 2);
			ActionDefinition action = CreateSaleAction(
				"test.scenario-sell.action",
				sellable.ContentId.Value,
				buyer.ContentId.Value,
				coin.ContentId.Value);
			QuestDefinition quest = CreateCardSaleQuest(
				"test.scenario-sell.quest",
				sellable.ContentId.Value,
				requiredSoldCount: 2);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario-sell.scenario",
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					sellable,
					buyer,
					coin,
					action,
					quest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(action.ContentId);
				TabletopCard firstSoldCard = run.Tabletop.CreateCard(sellable.ContentId, Vector2.zero);
				TabletopCard secondSoldCard = run.Tabletop.CreateCard(sellable.ContentId, new Vector2(1f, 0f));
				TabletopCard buyerCard = run.Tabletop.CreateCard(buyer.ContentId, new Vector2(3f, 0f));
				run.Tabletop.MergeStackOnto(secondSoldCard.Id, firstSoldCard.Id);
				ActionCandidate candidate = run.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						firstSoldCard.Id,
						firstSoldCard.Stack.Position,
						buyerCard.Stack.Position,
						firstSoldCard.Stack.Position,
						isDrag: true,
						buyerCard.Id))[0];

				run.StartAction(ActionRequest.FromCandidate(candidate));
				run.ConfirmTurn();

				QuestProgress progress = run.QuestLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
				Assert.That(run.Tabletop.Cards.TryGetCard(firstSoldCard.Id, out _), Is.False);
				Assert.That(run.Tabletop.Cards.TryGetCard(secondSoldCard.Id, out _), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(sellable);
				Object.DestroyImmediate(buyer);
				Object.DestroyImmediate(coin);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void CompletedExplorationAction_AdvancesTargetAreaQuest()
		{
			CardDefinition forest = CreateCard("test.scenario-explore.forest");
			ActionDefinition action = CreateExplorationAction(
				"test.scenario-explore.action",
				forest.ContentId.Value);
			QuestDefinition quest = CreateCardExplorationQuest(
				"test.scenario-explore.quest",
				forest.ContentId.Value,
				requiredExplorationCount: 1);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario-explore.scenario",
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					forest,
					action,
					quest,
					scenario
				});
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();
				run.DiscoverContent(action.ContentId);
				TabletopCard area = run.Tabletop.CreateCard(forest.ContentId, Vector2.zero);
				ActionCandidate candidate = run.FindActionCandidates(
					new TabletopCardPointerReleaseIntent(
						area.Id,
						Vector2.zero,
						Vector2.one,
						Vector2.zero,
						isDrag: true,
						default))[0];

				run.StartAction(ActionRequest.FromCandidate(candidate));
				run.ConfirmTurn();

				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Object.DestroyImmediate(forest);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void ProgressionModeSwitch_AdvancesTargetModeQuest()
		{
			QuestDefinition quest = CreateProgressionModeQuest(
				"test.scenario-progress.quest",
				ActionProgressionMode.RealTime);
			ScenarioDefinition scenario = CreateScenario(
				"test.scenario-progress.scenario",
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { quest, scenario });
				ScenarioRun run = new ScenarioRun(scenario, contentIndex, 12345u);
				run.ActivateInitialQuests();

				run.UseRealTimeProgression();

				Assert.That(run.QuestLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void JournalEntrySeenState_PersistsThroughSnapshot()
		{
			CardDefinition worker = CreateCard("test.journal-seen.worker");
			ActionDefinition action = CreateAction(
				"test.journal-seen.action",
				worker.ContentId.Value);
			QuestDefinition quest = CreateActionQuest(
				"test.journal-seen.quest",
				action.ContentId.Value);
			ScenarioDefinition scenario = CreateScenario(
				"test.journal-seen.scenario",
				quest.ContentId.Value);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[]
				{
					worker,
					action,
					quest,
					scenario
				});
				ScenarioRun original = new ScenarioRun(scenario, contentIndex, 12345u);
				original.ActivateInitialQuests();
				original.DiscoverContent(action.ContentId);

				Assert.That(original.IsJournalEntrySeen(quest.ContentId), Is.False);
				Assert.That(original.IsJournalEntrySeen(action.ContentId), Is.False);
				Assert.That(original.MarkJournalEntrySeen(quest.ContentId), Is.True);
				Assert.That(original.MarkJournalEntrySeen(quest.ContentId), Is.False);
				Assert.That(original.MarkJournalEntrySeen(action.ContentId), Is.True);
				Assert.Throws<InvalidOperationException>(() =>
					original.MarkJournalEntrySeen(worker.ContentId));

				ScenarioRun restored = ScenarioRun.Restore(
					scenario,
					contentIndex,
					JsonUtility.FromJson<ScenarioRunSnapshot>(
						JsonUtility.ToJson(original.CreateSnapshot())));

				Assert.That(restored.IsJournalEntrySeen(quest.ContentId), Is.True);
				Assert.That(restored.IsJournalEntrySeen(action.ContentId), Is.True);
			}
			finally
			{
				Object.DestroyImmediate(worker);
				Object.DestroyImmediate(action);
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
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
		public void Snapshot_FreezesModPackageSetAndRejectsDifferentCurrentVersionFacts()
		{
			ScenarioDefinition scenario = CreateScenario("test.snapshot-mods.scenario");
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { scenario });
				var frozenMods = new ModPackageSetSnapshot(new[]
				{
					new ModPackageSnapshot("author.world", "1.0.0", "hash-world", "manifest-world"),
					new ModPackageSnapshot("author.core", "2.0.0", "hash-core", "manifest-core")
				});
				ScenarioRun original = new ScenarioRun(scenario, contentIndex, 12345u, frozenMods);
				ScenarioRunSnapshot snapshot = JsonUtility.FromJson<ScenarioRunSnapshot>(
					JsonUtility.ToJson(original.CreateSnapshot()));

				Assert.That(snapshot.ModPackages.Packages.Count, Is.EqualTo(2));
				Assert.That(snapshot.ModPackages.Packages[0].ModId, Is.EqualTo("author.core"));
				Assert.DoesNotThrow(() => ScenarioRun.Restore(scenario, contentIndex, frozenMods, snapshot));

				var changedMods = new ModPackageSetSnapshot(new[]
				{
					new ModPackageSnapshot("author.core", "2.1.0", "hash-core", "manifest-core"),
					new ModPackageSnapshot("author.world", "1.0.0", "hash-world", "manifest-world")
				});

				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
					() => ScenarioRun.Restore(scenario, contentIndex, changedMods, snapshot));
				StringAssert.Contains("author.core", exception.Message);
			}
			finally
			{
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void Snapshot_PersistsDayDurationOverrideAndRestoresRealtimeProgressAgainstIt()
		{
			ScenarioDefinition scenario = CreateScenario("test.snapshot-day-duration.scenario", turnsPerDay: 4);
			JsonUtility.FromJsonOverwrite("{\"m_secondsPerTurn\":1}", scenario);
			try
			{
				ContentIndex contentIndex = BuildContentIndex(new ContentAsset[] { scenario });
				ScenarioRun original = new ScenarioRun(
					scenario,
					contentIndex,
					12345u,
					new ScenarioStartOptions(friendlyMode: false, dayDurationSecondsOverride: 20f));
				original.UseRealTimeProgression();
				original.AdvanceRealTime(4.5f);
				ScenarioRunSnapshot snapshot = JsonUtility.FromJson<ScenarioRunSnapshot>(
					JsonUtility.ToJson(original.CreateSnapshot()));

				ScenarioRun restored = ScenarioRun.Restore(scenario, contentIndex, snapshot);

				Assert.That(restored.SecondsPerTurn, Is.EqualTo(5f).Within(0.0001f));
				Assert.That(restored.ProgressionMode, Is.EqualTo(ActionProgressionMode.RealTime));
				Assert.That(restored.ConfirmedTurnIndex, Is.Zero);
				Assert.That(restored.NormalizedDayProgress, Is.EqualTo(0.225f).Within(0.0001f));
			}
			finally
			{
				Object.DestroyImmediate(scenario);
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

		private static void SetContentTags(ContentAsset content, params int[] tagCodes)
		{
			SerializedObject serializedContent = new(content);
			SerializedProperty tags = serializedContent.FindProperty("m_tagCodes");
			tags.arraySize = tagCodes.Length;
			for (int i = 0; i < tagCodes.Length; i++)
			{
				tags.GetArrayElementAtIndex(i).intValue = tagCodes[i];
			}
			serializedContent.ApplyModifiedPropertiesWithoutUndo();
		}
		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			return definition;
		}

		private static CardBuyerDefinition CreateBuyer(string contentId, ContentId currencyCardId)
		{
			CardBuyerDefinition definition = ScriptableObject.CreateInstance<CardBuyerDefinition>();
			SerializedObject serialized = new(definition);
			serialized.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serialized.FindProperty("m_currencyCardId").FindPropertyRelative("m_value").stringValue = currencyCardId.Value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static void SetSellValue(CardDefinition card, int sellValue)
		{
			SerializedObject serializedCard = new(card);
			serializedCard.FindProperty("m_sellValue").intValue = sellValue;
			serializedCard.ApplyModifiedPropertiesWithoutUndo();
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

		private static ActionDefinition CreateProductAction(
			string contentId,
			string cardContentId,
			string productContentId,
			int productCount,
			int turnCost = 1)
		{
			ActionDefinition definition = CreateAction(contentId, cardContentId, turnCost);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty intents = serializedDefinition.FindProperty("m_resultIntents");
			intents.arraySize = 1;
			CreateCardsResultIntent intent = new();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + productContentId +
				"\"},\"m_count\":" + productCount +
				",\"m_anchorSlotKey\":\"slot-1\"}",
				intent);
			intents.GetArrayElementAtIndex(0).managedReferenceValue = intent;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static ActionDefinition CreateSaleAction(
			string contentId,
			string soldCardContentId,
			string buyerCardContentId,
			string currencyCardContentId)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId +
				"\"},\"m_turnCost\":1,\"m_participationSlots\":[{" +
				"\"m_key\":\"sold\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":2," +
				"\"m_allowedContentIds\":[{\"m_value\":\"" + soldCardContentId + "\"}]}," +
				"{\"m_key\":\"buyer\",\"m_minimumParticipants\":1,\"m_maximumParticipants\":1," +
				"\"m_allowedContentIds\":[{\"m_value\":\"" + buyerCardContentId + "\"}]}]}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty intents = serializedDefinition.FindProperty("m_resultIntents");
			intents.arraySize = 1;
			SellCardsResultIntent intent = new();
			JsonUtility.FromJsonOverwrite(
				"{\"m_soldSlotKey\":\"sold\"," +
				"\"m_currencyCardId\":{\"m_value\":\"" + currencyCardContentId + "\"}," +
				"\"m_anchorSlotKey\":\"buyer\"}",
				intent);
			intents.GetArrayElementAtIndex(0).managedReferenceValue = intent;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static ActionDefinition CreateResearchAction(
			string contentId,
			string participantCardContentId,
			string unlockedActionContentId,
			string recipeCardContentId)
		{
			ActionDefinition definition = CreateAction(contentId, participantCardContentId);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty intents = serializedDefinition.FindProperty("m_resultIntents");
			intents.arraySize = 1;
			ResearchDiscoveryResultIntent intent = new();
			JsonUtility.FromJsonOverwrite(
				"{\"m_entries\":[{\"m_actionId\":{\"m_value\":\"" + unlockedActionContentId +
				"\"},\"m_recipeCardId\":{\"m_value\":\"" + recipeCardContentId +
				"\"}}],\"m_anchorSlotKey\":\"slot-1\"}",
				intent);
			intents.GetArrayElementAtIndex(0).managedReferenceValue = intent;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static ActionDefinition CreateExplorationAction(
			string contentId,
			string areaCardContentId)
		{
			ActionDefinition definition = CreateAction(contentId, areaCardContentId);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty intents = serializedDefinition.FindProperty("m_resultIntents");
			intents.arraySize = 1;
			ExploreCardsResultIntent intent = new();
			JsonUtility.FromJsonOverwrite(
				"{\"m_exploredSlotKey\":\"slot-1\"}",
				intent);
			intents.GetArrayElementAtIndex(0).managedReferenceValue = intent;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardExplorationQuest(
			string contentId,
			string exploredCardId,
			int requiredExplorationCount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CardExplorationQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId")
				.FindPropertyRelative("m_value").stringValue = exploredCardId;
			task.FindPropertyRelative("m_requiredExplorationCount").intValue = requiredExplorationCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardSaleQuest(
			string contentId,
			string soldCardId,
			int requiredSoldCount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CardSaleQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId")
				.FindPropertyRelative("m_value").stringValue = soldCardId;
			task.FindPropertyRelative("m_requiredSoldCount").intValue = requiredSoldCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateProgressionModeQuest(
			string contentId,
			ActionProgressionMode targetMode)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new ProgressionModeQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_targetMode").intValue = (int)targetMode;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardCreationQuest(
			string contentId,
			string createdCardId,
			int requiredCreatedCount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CardCreationQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId")
				.FindPropertyRelative("m_value").stringValue = createdCardId;
			task.FindPropertyRelative("m_requiredCreatedCount").intValue = requiredCreatedCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardPossessionQuest(
			string contentId,
			string cardId,
			int requiredCardCount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CardPossessionQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId")
				.FindPropertyRelative("m_value").stringValue = cardId;
			task.FindPropertyRelative("m_requiredCardCount").intValue = requiredCardCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateFoodNutritionQuest(
			string contentId,
			int requiredNutrition)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new FoodNutritionQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_requiredNutrition").intValue = requiredNutrition;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCurrencyAmountQuest(
			string contentId,
			string currencyCardId,
			int requiredAmount)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CurrencyAmountQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_currencyCardId")
				.FindPropertyRelative("m_value").stringValue = currencyCardId;
			task.FindPropertyRelative("m_requiredAmount").intValue = requiredAmount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardCapacityQuest(
			string contentId,
			int requiredCapacity)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				definition);
			SerializedObject serializedDefinition = new(definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue =
				new CardCapacityQuestTaskDefinition();
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			serializedDefinition.Update();
			SerializedProperty task = serializedDefinition
				.FindProperty("m_tasks")
				.GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_requiredCapacity").intValue = requiredCapacity;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
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
