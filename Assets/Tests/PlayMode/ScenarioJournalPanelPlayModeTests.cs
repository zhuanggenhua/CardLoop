using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using Gameplay.Tests.Support;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Tests
{
    /// <summary>验证统一地基场景通过正式 UIKit 查看当前任务和已发现配方 / 行动。</summary>
    public sealed class ScenarioJournalPanelPlayModeTests
    {
        private const string FoundationScenePath = "Assets/Scenes/FoundationTest.unity";
        private const string UnreadIndicatorGlyph = "●";
        private string m_saveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_saveDirectory = Path.Combine(
                Application.temporaryCachePath,
                "Gameplay-ScenarioJournalPanelTests",
                Guid.NewGuid().ToString("N"));
            SaveSystem.ResetSaveKitConfigurationForTests();
            SaveSystem.ConfigureSaveKit(m_saveDirectory);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            float timeoutAt = Time.realtimeSinceStartup + 20f;
            FoundationTestSceneHarness harness = null;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or
                   GameManagerStartupState.Initializing || harness == null || !harness.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "统一地基场景启动超时。");
                harness = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator JournalProjectsQuestProgressAndOnlyDiscoveredActions()
        {
            FoundationTestSceneHarness harness =
                UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();

            harness.OpenJournalPanel();
            ScenarioJournalPanel panel = null;
            yield return WaitForPanel(value => panel = value);

            Assert.That(panel.DisplayedQuestCount, Is.EqualTo(1));
            StringAssert.Contains("地基测试任务", panel.DisplayedText);
            StringAssert.Contains("进度 1: 0 / 1", panel.DisplayedText);
            StringAssert.Contains(UnreadIndicatorGlyph, panel.DisplayedText);

            FindButton(panel, "ActionsTab").onClick.Invoke();
            yield return null;
            Assert.That(panel.DisplayedActionCount, Is.EqualTo(1));
            StringAssert.Contains("Test Action", panel.DisplayedText);
            StringAssert.DoesNotContain("协同行动", panel.DisplayedText);
            StringAssert.Contains(UnreadIndicatorGlyph, panel.DisplayedText);

            harness.DiscoverActionPlanTestContent();
            yield return null;
            Assert.That(panel.DisplayedActionCount, Is.EqualTo(2));
            StringAssert.Contains("协同行动", panel.DisplayedText);
            StringAssert.Contains(UnreadIndicatorGlyph, panel.DisplayedText);

            FindButton(panel, "QuestsTab").onClick.Invoke();
            yield return null;
            StringAssert.DoesNotContain(UnreadIndicatorGlyph, panel.DisplayedText);
            IReadOnlyList<ActionCandidate> candidates = harness.QueryTestActionCandidates(
                harness.BottomCardId,
                harness.TargetCardId);
            ActionCandidate candidate = candidates.Single(value =>
                value.Action.ContentId == new ContentId(FoundationTestSceneHarness.TestActionContentId));
            ActionInstance action = harness.StartSelectedAction(candidate.Action.ContentId);
            Assert.That(action.State, Is.EqualTo(ActionInstanceState.Running));
            director.ConfirmTurn();
            director.ConfirmTurn();
            yield return null;

            StringAssert.Contains("[已完成]", panel.DisplayedText);
            StringAssert.Contains("进度 1: 1 / 1", panel.DisplayedText);

            FindButton(panel, "Close").onClick.Invoke();
            yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioJournalPanel>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator JournalClosesWhenDayCycleTakesOver()
        {
            FoundationTestSceneHarness harness =
                UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();

            harness.OpenJournalPanel();
            yield return WaitForPanel(_ => { });

            EventKit.Type.Send(new ScenarioDayCycleChangedEvent(
                director.ActiveScenarioId,
                endingDay: 1,
                ScenarioDayCyclePhase.AwaitingFeedingConfirmation,
                excessCardCount: 0));
            yield return null;

            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioJournalPanel>(), Is.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanel<ScenarioJournalPanel>();
            UIKit.ClearDialogQueue();
            if (GameManager.Exists())
            {
                UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
                yield return null;
            }

            SaveSystem.ResetSaveKitConfigurationForTests();
            if (!string.IsNullOrWhiteSpace(m_saveDirectory) && Directory.Exists(m_saveDirectory))
            {
                Directory.Delete(m_saveDirectory, true);
            }
            m_saveDirectory = null;
        }

        private static IEnumerator WaitForPanel(Action<ScenarioJournalPanel> assign)
        {
            ScenarioJournalPanel panel = null;
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            while (panel == null || !panel.gameObject.activeInHierarchy)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "UIKit 没有打开剧本日志。");
                panel = UnityEngine.Object.FindAnyObjectByType<ScenarioJournalPanel>();
                yield return null;
            }
            assign(panel);
        }

        private static Button FindButton(Component root, string objectName)
        {
            return root.GetComponentsInChildren<Button>(false)
                .First(button => button.gameObject.name == objectName);
        }
    }
}
