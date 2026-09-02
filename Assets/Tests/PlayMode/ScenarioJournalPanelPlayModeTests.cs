using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Gameplay.Tests.Support;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Tests
{
    /// <summary>验证统一地基场景通过正式 UIKit 查看当前任务和已发现配方 / 行动。</summary>
    public sealed class ScenarioJournalPanelPlayModeTests
    {
        private const string FoundationScenePath = "Assets/Scenes/地基测试.unity";
        private const string UnreadIndicatorGlyph = "●";
        private string m_saveDirectory;
        private int m_submitAudioRequestCount;

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

            ScenarioJournalPanel panel = null;
            yield return WaitForPanel(value => panel = value);
            TabletopCardInfoPanel infoPanel =
                UnityEngine.Object.FindAnyObjectByType<TabletopCardInfoPanel>();
            Assert.That(infoPanel, Is.Not.Null, "统一地基场景必须同时打开 StackCraft 式详情面板。");

            Assert.That(panel.DisplayedQuestCount, Is.EqualTo(1));
            StringAssert.Contains("基础 ▼", panel.DisplayedText);
            StringAssert.Contains("地基测试任务", panel.DisplayedText);
            StringAssert.Contains("• 地基测试任务", panel.DisplayedText);
            StringAssert.Contains(UnreadIndicatorGlyph, panel.DisplayedText);
            ScenarioJournalEntryButton questButton = FindEntryButton(panel, "地基测试任务");
            questButton.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.That(infoPanel.IsJournalEntryInfoActive, Is.True);
            Assert.That(infoPanel.DisplayedJournalEntryHeader, Is.EqualTo("地基测试任务"));
            StringAssert.Contains("进度：0 / 1", infoPanel.DisplayedJournalEntryBody);
            StringAssert.DoesNotContain(UnreadIndicatorGlyph, panel.DisplayedText);
            questButton.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.That(infoPanel.IsJournalEntryInfoActive, Is.False);

            FindButton(panel, "RecipesToggle").onClick.Invoke();
            yield return null;
            Assert.That(panel.DisplayedActionCount, Is.EqualTo(1));
            StringAssert.Contains("建造 ▼", panel.DisplayedText);
            StringAssert.Contains("测试行动", panel.DisplayedText);
            StringAssert.DoesNotContain("杂项", panel.DisplayedText);
            StringAssert.DoesNotContain("协同行动", panel.DisplayedText);
            StringAssert.Contains(UnreadIndicatorGlyph, panel.DisplayedText);

            ScenarioJournalEntryButton constructionHeader = FindEntryButton(panel, "建造");
            Button constructionToggle = constructionHeader.GetComponent<Button>();
            Assert.That(constructionToggle, Is.Not.Null, "日志分组头必须是可点击折叠按钮。");
            constructionToggle.onClick.Invoke();
            yield return null;
            StringAssert.Contains("建造 ►", panel.DisplayedText);
            StringAssert.DoesNotContain("测试行动", panel.DisplayedText);
            FindEntryButton(panel, "建造").GetComponent<Button>().onClick.Invoke();
            yield return null;
            StringAssert.Contains("建造 ▼", panel.DisplayedText);
            StringAssert.Contains("测试行动", panel.DisplayedText);

            ScenarioJournalEntryButton actionButton = FindEntryButton(panel, "测试行动");
            actionButton.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.That(infoPanel.IsJournalEntryInfoActive, Is.True);
            Assert.That(infoPanel.DisplayedJournalEntryHeader, Is.EqualTo("配方：测试行动"));
            Assert.That(infoPanel.DisplayedJournalEntryBody, Is.EqualTo("村民 ×2."));
            actionButton.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return null;

            harness.DiscoverActionPlanTestContent();
            yield return null;
            Assert.That(panel.DisplayedActionCount, Is.EqualTo(2));
            StringAssert.Contains("杂项 ▼", panel.DisplayedText);
            StringAssert.Contains("协同行动", panel.DisplayedText);
            StringAssert.Contains(UnreadIndicatorGlyph, panel.DisplayedText);

            FindButton(panel, "QuestsToggle").onClick.Invoke();
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

            StringAssert.Contains("√", panel.DisplayedText);

            FindButton(panel, "Close").onClick.Invoke();
            yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioJournalPanel>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator JournalSlidesClosedWhenDayCycleTakesOver()
        {
            FoundationTestSceneHarness harness =
                UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();

            ScenarioJournalPanel panel = null;
            yield return WaitForPanel(value => panel = value);
            Assert.That(panel.IsMenuOpen, Is.True);
            Assert.That(panel.MenuToggleText, Is.EqualTo(">>"));

            m_submitAudioRequestCount = 0;
            EventKit.Type.Register<AudioPlaybackRequestedEvent>(OnAudioPlaybackRequested);
            EventKit.Type.Send(new ScenarioDayCycleChangedEvent(
                director.ActiveScenarioId,
                endingDay: 1,
                ScenarioDayCyclePhase.AwaitingFeedingConfirmation,
                excessCardCount: 0));
            yield return new WaitForSecondsRealtime(0.6f);
            EventKit.Type.UnRegister<AudioPlaybackRequestedEvent>(OnAudioPlaybackRequested);

            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioJournalPanel>(), Is.SameAs(panel));
            Assert.That(panel.IsMenuOpen, Is.False);
            Assert.That(panel.MenuToggleText, Is.EqualTo("<<"));
            Assert.That(panel.MenuPanelAnchoredX, Is.EqualTo(400f).Within(0.01f));
            Assert.That(m_submitAudioRequestCount, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanel<ScenarioJournalPanel>();
            UIKit.ClearDialogQueue();
            EventKit.Type.UnRegister<AudioPlaybackRequestedEvent>(OnAudioPlaybackRequested);
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
            return root.GetComponentsInChildren<Button>(includeInactive: true)
                .First(button => button.gameObject.name == objectName);
        }

        private static ScenarioJournalEntryButton FindEntryButton(Component root, string text)
        {
            return root.GetComponentsInChildren<ScenarioJournalEntryButton>(includeInactive: true)
                .First(button => button.Text.Contains(text));
        }

        private void OnAudioPlaybackRequested(AudioPlaybackRequestedEvent audioEvent)
        {
            if (audioEvent.AudioClipResolver == GameManager.Config.submitSound)
            {
                m_submitAudioRequestCount++;
            }
        }
    }
}
