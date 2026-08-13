using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
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
