using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YokiFrame;
using Object = UnityEngine.Object;

using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证剧本是任务集合的父级生命周期入口，任务系统不再由外部随意开始和结束。
    /// </summary>
    public sealed class ScenarioDirectorEditModeTests
    {
        [Test]
        public void StartAndEndScenario_OwnsActiveScenarioAndQuestSetLifecycle()
        {
            QuestDefinition quest = CreateQuest("test.quest.root");
            ScenarioDefinition scenario = CreateScenario(
                "test.scenario",
                quest.ContentId.Value);
            GameObject systemObject = new("ScenarioDirectorTests");
            ScenarioTurnSystem scenarioTurnSystem =
                systemObject.AddComponent<ScenarioTurnSystem>();
            QuestSystem questSystem = systemObject.AddComponent<QuestSystem>();
            ScenarioDirector scenarioDirector = systemObject.AddComponent<ScenarioDirector>();
            SerializedObject serializedScenarioDirector = new(scenarioDirector);
            serializedScenarioDirector.FindProperty("m_questSystem").objectReferenceValue =
                questSystem;
            serializedScenarioDirector.FindProperty("m_turnSystem").objectReferenceValue =
                scenarioTurnSystem;
            serializedScenarioDirector.ApplyModifiedPropertiesWithoutUndo();
            bool observedCommittedScenario = false;

            void OnQuestStatusChanged(QuestStatusChangedEvent _)
            {
                Assert.That(
                    scenarioDirector.ActiveScenarioId,
                    Is.EqualTo(scenario.ContentId),
                    "任务激活事实发布时，剧本父级身份必须已经提交。");
                observedCommittedScenario = true;
            }

            EventKit.Type.Register<QuestStatusChangedEvent>(OnQuestStatusChanged);
            try
            {
                ContentIndex contentIndex = ContentIndex.Build(
                    new ContentAsset[] { quest, scenario });
                scenarioTurnSystem.OnSystemStart();
                questSystem.OnSystemStart();
                scenarioDirector.OnSystemStart();

                scenarioDirector.StartScenario(scenario.ContentId, contentIndex);

                Assert.That(scenarioDirector.HasActiveScenario, Is.True);
                Assert.That(scenarioDirector.ActiveScenarioId, Is.EqualTo(scenario.ContentId));
                Assert.That(questSystem.HasQuestSet, Is.True);
                Assert.That(
                    questSystem.GetStatus(quest.ContentId),
                    Is.EqualTo(QuestStatus.Active));
                Assert.That(observedCommittedScenario, Is.True);
                Assert.That(scenarioDirector.ConfirmTurn(), Is.EqualTo(1));
                Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.EqualTo(1));

                scenarioDirector.EndScenario();

                Assert.That(scenarioDirector.HasActiveScenario, Is.False);
                Assert.That(questSystem.HasQuestSet, Is.False);
                Assert.That(scenarioTurnSystem.ConfirmedTurnIndex, Is.Zero);
                StringAssert.Contains(
                    "没有活动剧本",
                    Assert.Throws<System.InvalidOperationException>(
                        () => scenarioDirector.ConfirmTurn()).Message);
            }
            finally
            {
                EventKit.Type.UnRegister<QuestStatusChangedEvent>(OnQuestStatusChanged);
                scenarioDirector.OnSystemStop();
                questSystem.OnSystemStop();
                scenarioTurnSystem.OnSystemStop();
                Object.DestroyImmediate(systemObject);
                Destroy(quest, scenario);
            }
        }

        [Test]
        public void ContentValidator_RejectsInvalidScenarioQuestComposition()
        {
            CardDefinition card = CreateCard("test.card");
            QuestDefinition root = CreateQuest("test.quest.root");
            QuestDefinition child = CreateQuest(
                "test.quest.child",
                "test.quest.root");
            ScenarioDefinition scenario = CreateScenario(
                "test.scenario.invalid",
                child.ContentId.Value,
                child.ContentId.Value,
                card.ContentId.Value,
                "test.quest.missing");

            try
            {
                ContentValidationReport report = ContentValidator.ValidateContentAssets(
                    new ContentAsset[] { card, root, child, scenario });

                Assert.That(report.HasErrors, Is.True);
                AssertIssue(report, "SCENARIO_QUEST_DUPLICATE");
                AssertIssue(report, "SCENARIO_QUEST_TYPE_INVALID");
                AssertIssue(report, "SCENARIO_QUEST_UNKNOWN");
                AssertIssue(report, "SCENARIO_QUEST_PREREQUISITE_MISSING");
            }
            finally
            {
                Destroy(card, root, child, scenario);
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
                System.Array.ConvertAll(
                    prerequisiteQuestIds,
                    prerequisiteId => $"{{\"m_value\":\"{prerequisiteId}\"}}"));
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
                $"\"m_prerequisiteQuestIds\":[{prerequisitesJson}]" +
                "}",
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

        private static ScenarioDefinition CreateScenario(
            string contentId,
            params string[] questIds)
        {
            ScenarioDefinition definition =
                ScriptableObject.CreateInstance<ScenarioDefinition>();
            string questJson = string.Join(
                ",",
                System.Array.ConvertAll(
                    questIds,
                    questId => $"{{\"m_value\":\"{questId}\"}}"));
            JsonUtility.FromJsonOverwrite(
                "{" +
                $"\"m_contentId\":{{\"m_value\":\"{contentId}\"}}," +
                $"\"m_questIds\":[{questJson}]" +
                "}",
                definition);
            return definition;
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

        private static void AssertIssue(ContentValidationReport report, string code)
        {
            Assert.That(
                report.Issues.Any(issue => issue.Code == code),
                Is.True,
                $"校验报告缺少问题码：{code}");
        }
    }
}

