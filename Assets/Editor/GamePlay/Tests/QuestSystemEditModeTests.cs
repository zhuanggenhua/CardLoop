using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证任务生命周期只维护单向前置关系，并由当前单局任务系统持有激活、完成和任务子项进度。
    /// </summary>
    public sealed class QuestSystemEditModeTests
    {
        [Test]
        public void StartQuestSet_ActivatesRootsAndUnlocksDependentsAfterCompletion()
        {
            QuestDefinition root = CreateQuest("test.quest.root");
            QuestDefinition child = CreateQuest(
                "test.quest.child",
                "test.quest.root");
            GameObject systemObject = new("QuestSystemTests");
            QuestSystem questSystem = systemObject.AddComponent<QuestSystem>();

            try
            {
                ContentIndex contentIndex = ContentIndex.Build(
                    new ContentAsset[] { root, child });
                questSystem.OnSystemStart();
                questSystem.StartQuestSet(
                    new[] { root.ContentId, child.ContentId },
                    contentIndex);

                Assert.That(
                    questSystem.GetStatus(root.ContentId),
                    Is.EqualTo(QuestStatus.Active));
                Assert.That(
                    questSystem.GetStatus(child.ContentId),
                    Is.EqualTo(QuestStatus.Locked));

                questSystem.CompleteQuest(root.ContentId);

                Assert.That(
                    questSystem.GetStatus(root.ContentId),
                    Is.EqualTo(QuestStatus.Completed));
                Assert.That(
                    questSystem.GetStatus(child.ContentId),
                    Is.EqualTo(QuestStatus.Active));
            }
            finally
            {
                questSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                Destroy(root, child);
            }
        }

        [Test]
        public void QuestLifecycle_PublishesCommittedStatusChangesInCausalOrder()
        {
            QuestDefinition root = CreateQuest("test.quest.root");
            QuestDefinition child = CreateQuest(
                "test.quest.child",
                "test.quest.root");
            GameObject systemObject = new("QuestSystemTests");
            QuestSystem questSystem = systemObject.AddComponent<QuestSystem>();
            var receivedEvents = new List<QuestStatusChangedEvent>();

            void OnQuestStatusChanged(QuestStatusChangedEvent statusChangedEvent)
            {
                Assert.That(
                    questSystem.GetStatus(statusChangedEvent.QuestId),
                    Is.EqualTo(statusChangedEvent.CurrentStatus),
                    "订阅者收到任务状态事实时，系统状态必须已经提交。");
                receivedEvents.Add(statusChangedEvent);
            }

            EventKit.Type.Register<QuestStatusChangedEvent>(OnQuestStatusChanged);
            try
            {
                ContentIndex contentIndex = ContentIndex.Build(
                    new ContentAsset[] { root, child });
                questSystem.OnSystemStart();
                questSystem.StartQuestSet(
                    new[] { root.ContentId, child.ContentId },
                    contentIndex);
                questSystem.CompleteQuest(root.ContentId);

                Assert.That(receivedEvents, Has.Count.EqualTo(3));
                AssertStatusChange(
                    receivedEvents[0],
                    root.ContentId,
                    QuestStatus.Locked,
                    QuestStatus.Active);
                AssertStatusChange(
                    receivedEvents[1],
                    root.ContentId,
                    QuestStatus.Active,
                    QuestStatus.Completed);
                AssertStatusChange(
                    receivedEvents[2],
                    child.ContentId,
                    QuestStatus.Locked,
                    QuestStatus.Active);
            }
            finally
            {
                EventKit.Type.UnRegister<QuestStatusChangedEvent>(OnQuestStatusChanged);
                questSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                Destroy(root, child);
            }
        }

        [Test]
        public void CompleteQuest_RejectsLockedAndAlreadyCompletedQuests()
        {
            QuestDefinition root = CreateQuest("test.quest.root");
            QuestDefinition child = CreateQuest(
                "test.quest.child",
                "test.quest.root");
            GameObject systemObject = new("QuestSystemTests");
            QuestSystem questSystem = systemObject.AddComponent<QuestSystem>();

            try
            {
                ContentIndex contentIndex = ContentIndex.Build(
                    new ContentAsset[] { root, child });
                questSystem.OnSystemStart();
                questSystem.StartQuestSet(
                    new[] { root.ContentId, child.ContentId },
                    contentIndex);

                InvalidOperationException lockedException = Assert.Throws<InvalidOperationException>(
                    () => questSystem.CompleteQuest(child.ContentId));
                StringAssert.Contains("尚未激活", lockedException.Message);

                questSystem.CompleteQuest(root.ContentId);
                InvalidOperationException completedException = Assert.Throws<InvalidOperationException>(
                    () => questSystem.CompleteQuest(root.ContentId));
                StringAssert.Contains("已经完成", completedException.Message);
            }
            finally
            {
                questSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                Destroy(root, child);
            }
        }

        [Test]
        public void CompletedActions_AdvanceOnlyQuestsThatWereAlreadyActive()
        {
            ActionDefinition action = CreateAction("test.action.explore");
            QuestDefinition root = CreateActionQuest(
                "test.quest.explore-twice",
                action.ContentId.Value,
                requiredCompletionCount: 2);
            QuestDefinition child = CreateActionQuest(
                "test.quest.explore-again",
                action.ContentId.Value,
                requiredCompletionCount: 1,
                root.ContentId.Value);
            GameObject systemObject = new("QuestSystemTests");
            QuestSystem questSystem = systemObject.AddComponent<QuestSystem>();

            try
            {
                ContentIndex contentIndex = ContentIndex.Build(
                    new ContentAsset[] { action, root, child });
                questSystem.OnSystemStart();
                questSystem.StartQuestSet(
                    new[] { root.ContentId, child.ContentId },
                    contentIndex);

                EventKit.Type.Send(new TabletopCardActionCompletedEvent(action.ContentId));
                Assert.That(
                    questSystem.GetStatus(root.ContentId),
                    Is.EqualTo(QuestStatus.Active));

                EventKit.Type.Send(new TabletopCardActionCompletedEvent(action.ContentId));
                Assert.That(
                    questSystem.GetStatus(root.ContentId),
                    Is.EqualTo(QuestStatus.Completed));
                Assert.That(
                    questSystem.GetStatus(child.ContentId),
                    Is.EqualTo(QuestStatus.Active),
                    "同一次行动不能继续推进刚刚解锁的后继任务。");

                EventKit.Type.Send(new TabletopCardActionCompletedEvent(action.ContentId));
                Assert.That(
                    questSystem.GetStatus(child.ContentId),
                    Is.EqualTo(QuestStatus.Completed));
            }
            finally
            {
                questSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                Destroy(action, root, child);
            }
        }

        [Test]
        public void ContentValidator_RejectsUnknownWrongTypeAndCyclicQuestPrerequisites()
        {
            CardDefinition card = CreateCard("test.card");
            QuestDefinition unknownReference = CreateQuest(
                "test.quest.unknown-reference",
                "test.quest.missing");
            QuestDefinition wrongTypeReference = CreateQuest(
                "test.quest.wrong-type",
                "test.card");
            QuestDefinition cycleA = CreateQuest(
                "test.quest.cycle-a",
                "test.quest.cycle-b");
            QuestDefinition cycleB = CreateQuest(
                "test.quest.cycle-b",
                "test.quest.cycle-a");

            try
            {
                ContentValidationReport report = ContentValidator.ValidateContentAssets(
                    new ContentAsset[]
                    {
                        card,
                        unknownReference,
                        wrongTypeReference,
                        cycleA,
                        cycleB
                    });

                Assert.That(report.HasErrors, Is.True);
                AssertIssue(report, "QUEST_PREREQUISITE_UNKNOWN");
                AssertIssue(report, "QUEST_PREREQUISITE_TYPE_INVALID");
                AssertIssue(report, "QUEST_PREREQUISITE_CYCLE");
            }
            finally
            {
                Destroy(card, unknownReference, wrongTypeReference, cycleA, cycleB);
            }
        }

        [Test]
        public void ContentValidator_RejectsInvalidActionCompletionQuestTasks()
        {
            CardDefinition card = CreateCard("test.card");
            ActionDefinition action = CreateAction("test.action");
            QuestDefinition invalidActionId = CreateActionQuest(
                "test.quest.invalid-action",
                string.Empty,
                requiredCompletionCount: 1);
            QuestDefinition unknownAction = CreateActionQuest(
                "test.quest.unknown-action",
                "test.action.missing",
                requiredCompletionCount: 1);
            QuestDefinition wrongType = CreateActionQuest(
                "test.quest.wrong-type-action",
                card.ContentId.Value,
                requiredCompletionCount: 1);
            QuestDefinition invalidCount = CreateActionQuest(
                "test.quest.invalid-count",
                action.ContentId.Value,
                requiredCompletionCount: 0);

            try
            {
                ContentValidationReport report = ContentValidator.ValidateContentAssets(
                    new ContentAsset[]
                    {
                        card,
                        action,
                        invalidActionId,
                        unknownAction,
                        wrongType,
                        invalidCount
                    });

                Assert.That(report.HasErrors, Is.True);
                AssertIssue(report, "QUEST_ACTION_TASK_ACTION_INVALID");
                AssertIssue(report, "QUEST_ACTION_TASK_ACTION_UNKNOWN");
                AssertIssue(report, "QUEST_ACTION_TASK_ACTION_TYPE_INVALID");
                AssertIssue(report, "QUEST_ACTION_TASK_COUNT_INVALID");
            }
            finally
            {
                Destroy(
                    card,
                    action,
                    invalidActionId,
                    unknownAction,
                    wrongType,
                    invalidCount);
            }
        }

        private static QuestDefinition CreateQuest(
            string contentId,
            params string[] prerequisiteQuestIds)
        {
            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            string prerequisitesJson = string.Join(
                ",",
                prerequisiteQuestIds.Select(id => $"{{\"m_value\":\"{id}\"}}"));
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
                $"\"m_prerequisiteQuestIds\":[{prerequisitesJson}]" +
                "}",
                definition);
            return definition;
        }

        private static QuestDefinition CreateActionQuest(
            string contentId,
            string actionId,
            int requiredCompletionCount,
            params string[] prerequisiteQuestIds)
        {
            QuestDefinition definition = CreateQuest(
                contentId,
                prerequisiteQuestIds);
            var serializedDefinition = new SerializedObject(definition);
            SerializedProperty tasks = serializedDefinition.FindProperty("m_tasks");
            tasks.arraySize = 1;
            SerializedProperty task = tasks.GetArrayElementAtIndex(0);
            task.managedReferenceValue = new ActionCompletionQuestTaskDefinition();
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            serializedDefinition.Update();
            task = serializedDefinition.FindProperty("m_tasks").GetArrayElementAtIndex(0);
            task.FindPropertyRelative("m_actionId")
                .FindPropertyRelative("m_value").stringValue = actionId;
            task.FindPropertyRelative("m_requiredCompletionCount").intValue =
                requiredCompletionCount;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static ActionDefinition CreateAction(string contentId)
        {
            ActionDefinition definition = ScriptableObject.CreateInstance<ActionDefinition>();
            JsonUtility.FromJsonOverwrite(
                $"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
                definition);
            return definition;
        }

        private static CardDefinition CreateCard(string contentId)
        {
            CardDefinition definition = ScriptableObject.CreateInstance<CardDefinition>();
            JsonUtility.FromJsonOverwrite(
                $"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
                definition);
            return definition;
        }

        private static void AssertIssue(ContentValidationReport report, string code)
        {
            Assert.That(
                report.Issues.Any(issue => issue.Code == code),
                Is.True,
                $"校验报告缺少问题码：{code}");
        }

        private static void AssertStatusChange(
            QuestStatusChangedEvent statusChangedEvent,
            ContentId expectedQuestId,
            QuestStatus expectedPreviousStatus,
            QuestStatus expectedCurrentStatus)
        {
            Assert.That(statusChangedEvent.QuestId, Is.EqualTo(expectedQuestId));
            Assert.That(statusChangedEvent.PreviousStatus, Is.EqualTo(expectedPreviousStatus));
            Assert.That(statusChangedEvent.CurrentStatus, Is.EqualTo(expectedCurrentStatus));
        }

        private static void Destroy(params Object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }
    }
}

