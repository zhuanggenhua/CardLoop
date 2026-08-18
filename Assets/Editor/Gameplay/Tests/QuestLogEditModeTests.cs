using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证单局任务日志、前置解锁和行动完成进度的 EditMode 行为合同。
	/// </summary>
	public sealed class QuestLogEditModeTests
	{
		[Test]
		public void StartQuestSet_ActivatesRootsAndUnlocksDependentsAfterCompletion()
		{
			ActionDefinition action = CreateAction("test.quest.unlock-action");
			QuestDefinition root = CreateActionQuest("test.quest.root", action.ContentId.Value, 1);
			QuestDefinition child = CreateQuest("test.quest.child", "test.quest.root");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { action, root, child });
				QuestLog questLog = new QuestLog(new ContentId("test.scenario.quest-initial"), new ContentId[2] { root.ContentId, child.ContentId }, contentIndex);
				questLog.ActivateInitialQuests();
				Assert.That<QuestStatus>(questLog.GetQuest(root.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Active));
				Assert.That<QuestStatus>(questLog.GetQuest(child.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Locked));
				questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId));
				Assert.That<QuestStatus>(questLog.GetQuest(root.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Completed));
				Assert.That<QuestStatus>(questLog.GetQuest(child.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Active));
			}
			finally
			{
				Destroy((Object)action, (Object)root, (Object)child);
			}
		}

		[Test]
		public void Quests_PreserveScenarioAuthoringOrderAsReadOnlyRuntimeObjects()
		{
			QuestDefinition first = CreateQuest("test.quest.order-first");
			QuestDefinition second = CreateQuest("test.quest.order-second");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { first, second });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.quest-order"),
					new[] { second.ContentId, first.ContentId },
					contentIndex);

				Assert.That(
					questLog.Quests.Select(quest => quest.Definition.ContentId),
					Is.EqualTo(new[] { second.ContentId, first.ContentId }));
			}
			finally
			{
				Destroy(first, second);
			}
		}

		[Test]
		public void QuestLifecycle_PublishesCommittedStatusChangesInCausalOrder()
		{
			ActionDefinition action = CreateAction("test.quest.lifecycle-action");
			QuestDefinition root = CreateActionQuest("test.quest.root", action.ContentId.Value, 1);
			QuestDefinition child = CreateQuest("test.quest.child", "test.quest.root");
			ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { action, root, child });
			QuestLog questLog = new QuestLog(new ContentId("test.scenario.quest-lifecycle"), new ContentId[2] { root.ContentId, child.ContentId }, contentIndex);
			List<QuestStatusChangedEvent> receivedEvents = new List<QuestStatusChangedEvent>();
			EventKit.Type.Register<QuestStatusChangedEvent>(OnQuestStatusChanged);
			try
			{
				questLog.ActivateInitialQuests();
				questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId));
				Assert.That<List<QuestStatusChangedEvent>>(receivedEvents, (IResolveConstraint)(object)((ConstraintExpression)Has.Count).EqualTo((object)3));
				AssertStatusChange(receivedEvents[0], root.ContentId, QuestStatus.Locked, QuestStatus.Active);
				AssertStatusChange(receivedEvents[1], root.ContentId, QuestStatus.Active, QuestStatus.Completed);
				AssertStatusChange(receivedEvents[2], child.ContentId, QuestStatus.Locked, QuestStatus.Active);
			}
			finally
			{
				EventKit.Type.UnRegister<QuestStatusChangedEvent>(OnQuestStatusChanged);
				Destroy((Object)action, (Object)root, (Object)child);
			}
			void OnQuestStatusChanged(QuestStatusChangedEvent statusChangedEvent)
			{
				Assert.That(statusChangedEvent.ScenarioId.Value, Is.EqualTo("test.scenario.quest-lifecycle"));
				Assert.That<QuestStatus>(questLog.GetQuest(statusChangedEvent.QuestId).Status, (IResolveConstraint)(object)Is.EqualTo((object)statusChangedEvent.CurrentStatus), "订阅者收到任务状态事实时，系统状态必须已经提交。", Array.Empty<object>());
				receivedEvents.Add(statusChangedEvent);
			}
		}

		[Test]
		public void RecordFact_PublishesCommittedProgressBeforeQuestCompletes()
		{
			ActionDefinition action = CreateAction("test.quest.progress-action");
			QuestDefinition quest = CreateActionQuest("test.quest.progress", action.ContentId.Value, 2);
			ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { action, quest });
			QuestLog questLog = new QuestLog(
				new ContentId("test.scenario.quest-progress"),
				new[] { quest.ContentId },
				contentIndex);
			QuestProgressChangedEvent received = default;
			int eventCount = 0;
			EventKit.Type.Register<QuestProgressChangedEvent>(OnProgressChanged);
			try
			{
				questLog.ActivateInitialQuests();

				Assert.That(
					questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId)),
					Is.True);

				Assert.That(eventCount, Is.EqualTo(1));
				Assert.That(received.ScenarioId.Value, Is.EqualTo("test.scenario.quest-progress"));
				Assert.That(received.QuestId, Is.EqualTo(quest.ContentId));
				Assert.That(questLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Active));
				Assert.That(questLog.GetQuest(quest.ContentId).Tasks[0].Progress.CurrentAmount, Is.EqualTo(1));
			}
			finally
			{
				EventKit.Type.UnRegister<QuestProgressChangedEvent>(OnProgressChanged);
				Destroy(action, quest);
			}

			void OnProgressChanged(QuestProgressChangedEvent changedEvent)
			{
				received = changedEvent;
				eventCount++;
			}
		}

		[Test]
		public void GetQuest_RejectsUnknownQuest()
		{
			QuestDefinition root = CreateQuest("test.quest.root");
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { root });
				QuestLog questLog = new QuestLog(new ContentId("test.scenario.quest-query"), new[] { root.ContentId }, contentIndex);
				questLog.ActivateInitialQuests();
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>((TestDelegate)delegate
				{
					questLog.GetQuest(new ContentId("test.quest.missing"));
				});
				StringAssert.Contains("不属于当前单局任务日志", exception.Message);
			}
			finally
			{
				Destroy((Object)root);
			}
		}

		[Test]
		public void RecordedActions_AdvanceOnlyQuestsThatWereAlreadyActive()
		{
			ActionDefinition action = CreateAction("test.action.explore");
			QuestDefinition root = CreateActionQuest("test.quest.explore-twice", action.ContentId.Value, 2);
			QuestDefinition child = CreateActionQuest("test.quest.explore-again", action.ContentId.Value, 1, root.ContentId.Value);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[3] { action, root, child });
				QuestLog questLog = new QuestLog(new ContentId("test.scenario.quest-action"), new ContentId[2] { root.ContentId, child.ContentId }, contentIndex);
				questLog.ActivateInitialQuests();
				questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId));
				Assert.That<QuestStatus>(questLog.GetQuest(root.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Active));
				QuestTaskProgressSnapshot progress = questLog.GetQuest(root.ContentId).Tasks[0].Progress;
				Assert.That(progress.CurrentAmount, Is.EqualTo(1));
				Assert.That(progress.RequiredAmount, Is.EqualTo(2));
				Assert.That(progress.IsCompleted, Is.False);
				questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId));
				Assert.That<QuestStatus>(questLog.GetQuest(root.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Completed));
				Assert.That<QuestStatus>(questLog.GetQuest(child.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Active), "同一次行动不能继续推进刚刚解锁的后继任务。", Array.Empty<object>());
				questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId));
				Assert.That<QuestStatus>(questLog.GetQuest(child.ContentId).Status, (IResolveConstraint)(object)Is.EqualTo((object)QuestStatus.Completed));
			}
			finally
			{
				Destroy((Object)action, (Object)root, (Object)child);
			}
		}

		[Test]
		public void ContentValidator_RejectsUnknownWrongTypeAndCyclicQuestPrerequisites()
		{
			CardDefinition card = CreateCard("test.card");
			QuestDefinition unknownReference = CreateQuest("test.quest.unknown-reference", "test.quest.missing");
			QuestDefinition wrongTypeReference = CreateQuest("test.quest.wrong-type", "test.card");
			QuestDefinition cycleA = CreateQuest("test.quest.cycle-a", "test.quest.cycle-b");
			QuestDefinition cycleB = CreateQuest("test.quest.cycle-b", "test.quest.cycle-a");
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[5] { card, unknownReference, wrongTypeReference, cycleA, cycleB });
				Assert.That<bool>(report.HasErrors, (IResolveConstraint)(object)Is.True);
				AssertIssue(report, "QUEST_PREREQUISITE_UNKNOWN");
				AssertIssue(report, "QUEST_PREREQUISITE_TYPE_INVALID");
				AssertIssue(report, "QUEST_PREREQUISITE_CYCLE");
			}
			finally
			{
				Destroy((Object)card, (Object)unknownReference, (Object)wrongTypeReference, (Object)cycleA, (Object)cycleB);
			}
		}

		[Test]
		public void ContentValidator_RejectsInvalidActionCompletionQuestTasks()
		{
			CardDefinition card = CreateCard("test.card");
			ActionDefinition action = CreateAction("test.action");
			QuestDefinition invalidActionId = CreateActionQuest("test.quest.invalid-action", string.Empty, 1);
			QuestDefinition unknownAction = CreateActionQuest("test.quest.unknown-action", "test.action.missing", 1);
			QuestDefinition wrongType = CreateActionQuest("test.quest.wrong-type-action", card.ContentId.Value, 1);
			QuestDefinition invalidCount = CreateActionQuest("test.quest.invalid-count", action.ContentId.Value, 0);
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(new ContentAsset[6] { card, action, invalidActionId, unknownAction, wrongType, invalidCount });
				Assert.That<bool>(report.HasErrors, (IResolveConstraint)(object)Is.True);
				AssertIssue(report, "QUEST_ACTION_TASK_ACTION_INVALID");
				AssertIssue(report, "QUEST_ACTION_TASK_ACTION_UNKNOWN");
				AssertIssue(report, "QUEST_ACTION_TASK_ACTION_TYPE_INVALID");
				AssertIssue(report, "QUEST_ACTION_TASK_COUNT_INVALID");
			}
			finally
			{
				Destroy((Object)card, (Object)action, (Object)invalidActionId, (Object)unknownAction, (Object)wrongType, (Object)invalidCount);
			}
		}

		[Test]
		public void RecordActionFact_AllowsModDerivedTaskWithoutQuestLogTypeRegistration()
		{
			ActionDefinition action = CreateAction("test.action.mod-derived-task");
			QuestDefinition quest = CreateQuest("test.quest.mod-derived-task");
			try
			{
				SetTask(quest, new ModDerivedQuestTaskDefinition());
				SetModDerivedTaskActionId(quest, action.ContentId.Value);
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { action, quest });
				QuestLog questLog = new QuestLog(new ContentId("test.scenario.quest-mod"), new[] { quest.ContentId }, contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new ActionCompletedQuestTaskFact(action.ContentId));

				Assert.That(questLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Destroy(action, quest);
			}
		}

		[Test]
		public void CardSaleQuestTask_CountsSpecificSoldCardsFromCommittedSaleFact()
		{
			CardDefinition sellable = CreateCard("test.card.sellable");
			CardDefinition other = CreateCard("test.card.other");
			QuestDefinition quest = CreateCardSaleQuest(
				"test.quest.sell-two",
				sellable.ContentId.Value,
				2);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { sellable, other, quest });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.sell-quest"),
					new[] { quest.ContentId },
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new CardsSoldQuestTaskFact(new[] { other.ContentId, sellable.ContentId }));

				QuestProgress progress = questLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Active));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(1));

				questLog.RecordFact(new CardsSoldQuestTaskFact(new[] { sellable.ContentId }));

				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
			}
			finally
			{
				Destroy(sellable, other, quest);
			}
		}

		[Test]
		public void CardCreationQuestTask_CountsMatchingCreatedCardsFromCommittedActionFact()
		{
			CardDefinition product = CreateCard("test.card.created-product");
			CardDefinition other = CreateCard("test.card.created-other");
			QuestDefinition quest = CreateCardCreationQuest(
				"test.quest.create-two",
				product.ContentId.Value,
				2);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { product, other, quest });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.create-quest"),
					new[] { quest.ContentId },
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new CardsCreatedQuestTaskFact(new[]
				{
					other.ContentId,
					product.ContentId,
					product.ContentId
				}));

				QuestProgress progress = questLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
			}
			finally
			{
				Destroy(product, other, quest);
			}
		}

		[Test]
		public void CardDefeatQuestTask_CountsOnlyMatchingDefeatedCardsFromBattleFact()
		{
			CardDefinition enemy = CreateCard("test.card.defeated-enemy");
			CardDefinition other = CreateCard("test.card.defeated-other");
			QuestDefinition quest = CreateCardDefeatQuest(
				"test.quest.defeat-two",
				enemy.ContentId.Value,
				2);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { enemy, other, quest });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.defeat-quest"),
					new[] { quest.ContentId },
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new CardsDefeatedQuestTaskFact(new[]
				{
					other.ContentId,
					enemy.ContentId,
					enemy.ContentId
				}));

				QuestProgress progress = questLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
			}
			finally
			{
				Destroy(enemy, other, quest);
			}
		}

		[Test]
		public void CardExplorationQuestTask_CountsOnlyMatchingExploredCardsFromCommittedActionFact()
		{
			CardDefinition forest = CreateCard("test.card.explore-forest");
			CardDefinition beach = CreateCard("test.card.explore-beach");
			QuestDefinition quest = CreateCardExplorationQuest(
				"test.quest.explore-forest",
				forest.ContentId.Value,
				2);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { forest, beach, quest });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.explore-quest"),
					new[] { quest.ContentId },
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new CardsExploredQuestTaskFact(new[]
				{
					beach.ContentId,
					forest.ContentId,
					forest.ContentId
				}));

				QuestProgress progress = questLog.GetQuest(quest.ContentId);
				Assert.That(progress.Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(progress.Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
			}
			finally
			{
				Destroy(forest, beach, quest);
			}
		}

		[Test]
		public void ProgressionModeQuestTask_CompletesWhenTargetModeFactIsRecorded()
		{
			QuestDefinition quest = CreateProgressionModeQuest(
				"test.quest.progression-real-time",
				ActionProgressionMode.RealTime);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { quest });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.progression-quest"),
					new[] { quest.ContentId },
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new ProgressionModeChangedQuestTaskFact(ActionProgressionMode.RealTime));

				Assert.That(questLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Destroy(quest);
			}
		}

		[Test]
		public void CardSaleQuestTask_EmptyTargetCountsAnySoldCard()
		{
			CardDefinition first = CreateCard("test.card.first");
			CardDefinition second = CreateCard("test.card.second");
			QuestDefinition quest = CreateCardSaleQuest(
				"test.quest.sell-any",
				string.Empty,
				2);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[] { first, second, quest });
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.sell-any-quest"),
					new[] { quest.ContentId },
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new CardsSoldQuestTaskFact(new[] { first.ContentId, second.ContentId }));

				Assert.That(questLog.GetQuest(quest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(questLog.GetQuest(quest.ContentId).Tasks[0].Progress.CurrentAmount, Is.EqualTo(2));
			}
			finally
			{
				Destroy(first, second, quest);
			}
		}

		[Test]
		public void TabletopStateQuestTasks_SetProgressFromCurrentStateFact()
		{
			CardDefinition wood = CreateCard("test.card.state.wood");
			CardDefinition coin = CreateCard("test.card.state.coin");
			QuestDefinition haveQuest = CreateCardPossessionQuest(
				"test.quest.have-wood",
				wood.ContentId.Value,
				2);
			QuestDefinition foodQuest = CreateFoodNutritionQuest("test.quest.food-stock", 3);
			QuestDefinition coinsQuest = CreateCurrencyAmountQuest(
				"test.quest.coins-stock",
				coin.ContentId.Value,
				4);
			QuestDefinition capacityQuest = CreateCardCapacityQuest("test.quest.card-capacity", 6);
			try
			{
				ContentIndex contentIndex = ContentIndex.Build(new ContentAsset[]
				{
					wood,
					coin,
					haveQuest,
					foodQuest,
					coinsQuest,
					capacityQuest
				});
				QuestLog questLog = new QuestLog(
					new ContentId("test.scenario.tabletop-state-quest"),
					new[]
					{
						haveQuest.ContentId,
						foodQuest.ContentId,
						coinsQuest.ContentId,
						capacityQuest.ContentId
					},
					contentIndex);
				questLog.ActivateInitialQuests();

				questLog.RecordFact(new TabletopStateQuestTaskFact(
					new[]
					{
						wood.ContentId,
						wood.ContentId,
						coin.ContentId
					},
					totalFoodNutrition: 3,
					new[]
					{
						new TabletopStateQuestTaskFact.CurrencyStock(coin.ContentId, 4)
					},
					cardCapacity: 6));

				Assert.That(questLog.GetQuest(haveQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(questLog.GetQuest(foodQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(questLog.GetQuest(coinsQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
				Assert.That(questLog.GetQuest(capacityQuest.ContentId).Status, Is.EqualTo(QuestStatus.Completed));
			}
			finally
			{
				Destroy(wood, coin, haveQuest, foodQuest, coinsQuest, capacityQuest);
			}
		}

		private static QuestDefinition CreateQuest(string contentId, params string[] prerequisiteQuestIds)
		{
			QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
			string prerequisitesJson = string.Join(",", prerequisiteQuestIds.Select((string id) => "{\"m_value\":\"" + id + "\"}"));
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"},\"m_prerequisiteQuestIds\":[" + prerequisitesJson + "]}", (object)definition);
			return definition;
		}

		private static QuestDefinition CreateActionQuest(string contentId, string actionId, int requiredCompletionCount, params string[] prerequisiteQuestIds)
		{
			QuestDefinition definition = CreateQuest(contentId, prerequisiteQuestIds);
			SetTask(definition, new ActionCompletionQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject((Object)(object)definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_actionId").FindPropertyRelative("m_value").stringValue = actionId;
			task.FindPropertyRelative("m_requiredCompletionCount").intValue = requiredCompletionCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardSaleQuest(
			string contentId,
			string soldCardId,
			int requiredSoldCount)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CardSaleQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId").FindPropertyRelative("m_value").stringValue = soldCardId;
			task.FindPropertyRelative("m_requiredSoldCount").intValue = requiredSoldCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardDefeatQuest(
			string contentId,
			string defeatedCardId,
			int requiredDefeatCount)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CardDefeatQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId").FindPropertyRelative("m_value").stringValue = defeatedCardId;
			task.FindPropertyRelative("m_requiredDefeatCount").intValue = requiredDefeatCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardExplorationQuest(
			string contentId,
			string exploredCardId,
			int requiredExplorationCount)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CardExplorationQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId").FindPropertyRelative("m_value").stringValue = exploredCardId;
			task.FindPropertyRelative("m_requiredExplorationCount").intValue = requiredExplorationCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateProgressionModeQuest(
			string contentId,
			ActionProgressionMode targetMode)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new ProgressionModeQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_targetMode").intValue = (int)targetMode;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardCreationQuest(
			string contentId,
			string createdCardId,
			int requiredCreatedCount)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CardCreationQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId").FindPropertyRelative("m_value").stringValue = createdCardId;
			task.FindPropertyRelative("m_requiredCreatedCount").intValue = requiredCreatedCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardPossessionQuest(
			string contentId,
			string cardId,
			int requiredCardCount)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CardPossessionQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_cardId").FindPropertyRelative("m_value").stringValue = cardId;
			task.FindPropertyRelative("m_requiredCardCount").intValue = requiredCardCount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateFoodNutritionQuest(string contentId, int requiredNutrition)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new FoodNutritionQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_requiredNutrition").intValue = requiredNutrition;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCurrencyAmountQuest(
			string contentId,
			string currencyCardId,
			int requiredAmount)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CurrencyAmountQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_currencyCardId").FindPropertyRelative("m_value").stringValue = currencyCardId;
			task.FindPropertyRelative("m_requiredAmount").intValue = requiredAmount;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static QuestDefinition CreateCardCapacityQuest(string contentId, int requiredCapacity)
		{
			QuestDefinition definition = CreateQuest(contentId);
			SetTask(definition, new CardCapacityQuestTaskDefinition());
			SerializedObject serializedDefinition = new SerializedObject(definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_requiredCapacity").intValue = requiredCapacity;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		private static void SetTask(QuestDefinition definition, QuestTaskDefinition taskDefinition)
		{
			SerializedObject serializedDefinition = new SerializedObject((Object)(object)definition);
			SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
			tasks.arraySize = 1;
			tasks.GetArrayElementAtIndex(0).managedReferenceValue = taskDefinition;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetModDerivedTaskActionId(QuestDefinition definition, string actionId)
		{
			SerializedObject serializedDefinition = new SerializedObject((Object)(object)definition);
			SerializedProperty task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
			task.FindPropertyRelative("m_actionId").FindPropertyRelative("m_value").stringValue = actionId;
			serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
		}

		[Serializable]
		private sealed class ModDerivedQuestTaskDefinition : QuestTaskDefinition
		{
			[SerializeField]
			private ContentId m_actionId;

			protected override QuestTaskRuntimeState CreateRuntimeState()
			{
				return new ModDerivedQuestTaskRuntimeState(m_actionId);
			}
		}

		private sealed class ModDerivedQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly ContentId m_actionId;

			private bool m_isCompleted;

			internal ModDerivedQuestTaskRuntimeState(ContentId actionId)
			{
				m_actionId = actionId;
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_isCompleted ? 1 : 0,
				1);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (m_isCompleted || fact is not ActionCompletedQuestTaskFact actionCompleted ||
					actionCompleted.ActionId != m_actionId)
				{
					return false;
				}

				m_isCompleted = true;
				return true;
			}
		}

		private static ActionDefinition CreateAction(string contentId)
		{
			ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", (object)definition);
			return definition;
		}

		private static CardDefinition CreateCard(string contentId)
		{
			CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite("{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}", (object)definition);
			return definition;
		}

		private static void AssertIssue(ContentValidationReport report, string code)
		{
			Assert.That<bool>(report.Issues.Any((ContentValidationIssue issue) => issue.Code == code), (IResolveConstraint)(object)Is.True, "校验报告缺少问题码：" + code, Array.Empty<object>());
		}

		private static void AssertStatusChange(QuestStatusChangedEvent statusChangedEvent, ContentId expectedQuestId, QuestStatus expectedPreviousStatus, QuestStatus expectedCurrentStatus)
		{
			Assert.That<ContentId>(statusChangedEvent.QuestId, (IResolveConstraint)(object)Is.EqualTo((object)expectedQuestId));
			Assert.That<QuestStatus>(statusChangedEvent.PreviousStatus, (IResolveConstraint)(object)Is.EqualTo((object)expectedPreviousStatus));
			Assert.That<QuestStatus>(statusChangedEvent.CurrentStatus, (IResolveConstraint)(object)Is.EqualTo((object)expectedCurrentStatus));
		}

		private static void Destroy(params Object[] objects)
		{
			for (int i = 0; i < objects.Length; i++)
			{
				if (objects[i] != (Object)null)
				{
					Object.DestroyImmediate(objects[i]);
				}
			}
		}
	}
}
