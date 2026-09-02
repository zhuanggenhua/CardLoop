using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameCore;
using Gameplay.Scenarios;
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
    /// <summary>验证 StackCraft 标题入口由正式剧本导演和 UIKit 组合提供等价玩家流程。</summary>
    public sealed class ScenarioTitleScreenPlayModeTests
    {
        private const string TitleScenePath = "Assets/Scenes/地基标题测试.unity";
        private string m_saveDirectory;
        private bool m_previousRunInBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            ResetSceneKitStaticStateForTests();
            m_saveDirectory = Path.Combine(
                Application.temporaryCachePath,
                "Gameplay-ScenarioTitleTests",
                Guid.NewGuid().ToString("N"));
            SaveSystem.ResetSaveKitConfigurationForTests();
            SaveSystem.ConfigureSaveKit(m_saveDirectory);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                TitleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForTitlePanel();
        }

        [UnityTest]
        public IEnumerator TitlePanel_ExposesTemplateCommandsThroughUIKit()
        {
            ScenarioTitlePanel panel = FindTitlePanel();
            Assert.That(panel.DefaultScenarioId.IsValid, Is.True);
            Assert.That(FindButton(panel, "NewGame").interactable, Is.True);
            Assert.That(FindButton(panel, "LoadGame").interactable, Is.True);
            Assert.That(FindButton(panel, "Settings").interactable, Is.True);
            Assert.That(FindButton(panel, "Quit").interactable, Is.True);

            FindButton(panel, "LoadGame").onClick.Invoke();
            ScenarioSavePanel savePanel = null;
            yield return WaitFor<ScenarioSavePanel>(value => savePanel = value);
            Assert.That(savePanel, Is.Not.Null);
            Assert.That(savePanel.DisplayedSlotCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TitlePanel_SettingsAndQuitUseExistingUIKitPanels()
        {
            ScenarioTitlePanel panel = FindTitlePanel();
            FindButton(panel, "Settings").onClick.Invoke();
            UISettings settings = null;
            yield return WaitFor<UISettings>(value => settings = value);
            Assert.That(settings, Is.Not.Null);
            Assert.That(GameManager.HasSystem<DisplaySettingsSystem>(), Is.True);
            Assert.That(FindButton(settings, "Resolution").interactable, Is.True);
            Assert.That(FindButton(settings, "Fullscreen").interactable, Is.True);
            Assert.That(FindButton(settings, "VSync").interactable, Is.True);
            Assert.That(FindButton(settings, "FrameRate").interactable, Is.True);
            Assert.That(FindButton(settings, "Shadow").interactable, Is.True);

            FindButton(settings, "ResetSettings").onClick.Invoke();
            ConfirmationDialogPanel resetDialog = null;
            yield return WaitFor<ConfirmationDialogPanel>(value => resetDialog = value);
            Assert.That(resetDialog, Is.Not.Null);
            FindButton(resetDialog, "Cancel").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ConfirmationDialogPanel>(), Is.Null);

            FindButton(settings, "Close").onClick.Invoke();
            yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<UISettings>(), Is.Null);

            FindButton(panel, "Quit").onClick.Invoke();
            ConfirmationDialogPanel dialog = null;
            yield return WaitFor<ConfirmationDialogPanel>(value => dialog = value);
            Assert.That(dialog, Is.Not.Null);
            Assert.That(FindButton(dialog, "Confirm").interactable, Is.True);
            Assert.That(FindButton(dialog, "Cancel").interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator TitlePanel_NewGameStartsScenarioAndLeavesTitle()
        {
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
            ScenarioTitlePanel panel = FindTitlePanel();
            FindButton(panel, "NewGame").onClick.Invoke();

            float timeoutAt = Time.realtimeSinceStartup + 15f;
            FoundationTestSceneHarness harness = null;
            while (director.ActiveRun == null ||
                   (harness = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>()) == null ||
                   !harness.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "标题页新游戏没有进入地基测试场景。");
                yield return null;
            }

            Assert.That(director.ActiveScenarioId.IsValid, Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioTitlePanel>(), Is.Null);
            Assert.That(harness, Is.Not.Null);
            Assert.That(harness.IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator TitlePanel_FriendlyModeStartsScenarioWithFriendlyOption()
        {
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
            ScenarioTitlePanel panel = FindTitlePanel();
            FindToggle(panel, "FriendlyMode").isOn = true;
            FindButton(panel, "NewGame").onClick.Invoke();

            float timeoutAt = Time.realtimeSinceStartup + 15f;
            FoundationTestSceneHarness harness = null;
            while (director.ActiveRun == null ||
                   (harness = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>()) == null ||
                   !harness.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "标题页友好模式新游戏没有进入地基测试场景。");
                yield return null;
            }

            Assert.That(director.ActiveRun.FriendlyMode, Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioTitlePanel>(), Is.Null);
            Assert.That(harness, Is.Not.Null);
            Assert.That(harness.IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator TitlePanel_DayDurationSliderStartsScenarioWithSelectedDayLength()
        {
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
            ScenarioTitlePanel panel = FindTitlePanel();
            FindSlider(panel, "DayDuration").value = 90f;
            FindButton(panel, "NewGame").onClick.Invoke();

            float timeoutAt = Time.realtimeSinceStartup + 15f;
            FoundationTestSceneHarness harness = null;
            while (director.ActiveRun == null ||
                   (harness = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>()) == null ||
                   !harness.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "标题页日长滑条新游戏没有进入地基测试场景。");
                yield return null;
            }

            Assert.That(
                director.ActiveRun.SecondsPerTurn,
                Is.EqualTo(90f / director.ActiveRun.TurnsPerDay).Within(0.0001f));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioTitlePanel>(), Is.Null);
            Assert.That(harness, Is.Not.Null);
            Assert.That(harness.IsReady, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Exception teardownFailure = null;
            UIKit.ClearDialogQueue();
            if (GameManager.Exists())
            {
                if (GameManager.TryGetSystem(out ScenarioDirector director) &&
                    director.HasActiveScenario &&
                    !director.IsChangingScenario)
                {
                    Exception endFailure = null;
                    yield return director.EndScenarioAsync().ToCoroutine(exception => endFailure = exception);
                    if (endFailure != null)
                    {
                        teardownFailure = new InvalidOperationException(
                            "标题入口测试结束后必须通过正式剧本导演退出单局。",
                            endFailure);
                    }
                }
                UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
                yield return null;
            }

            Application.runInBackground = m_previousRunInBackground;
            SaveSystem.ResetSaveKitConfigurationForTests();
            if (!string.IsNullOrWhiteSpace(m_saveDirectory) && Directory.Exists(m_saveDirectory))
            {
                Directory.Delete(m_saveDirectory, true);
            }
            ResetSceneKitStaticStateForTests();
            yield return null;
            Assert.That(teardownFailure, Is.Null);
        }

        private static ScenarioTitlePanel FindTitlePanel()
        {
            return UnityEngine.Object.FindAnyObjectByType<ScenarioTitlePanel>() ??
                throw new AssertionException("标题场景没有打开 ScenarioTitlePanel。");
        }

        private static IEnumerator WaitForTitlePanel()
        {
            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (UnityEngine.Object.FindAnyObjectByType<ScenarioTitlePanel>() == null)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "标题场景没有通过 UIKit 打开标题面板。");
                yield return null;
            }
        }

        private static IEnumerator WaitFor<T>(Action<T> assign) where T : Component
        {
            T component = null;
            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (component == null || !component.gameObject.activeInHierarchy)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, $"UIKit 没有打开 {typeof(T).Name}。");
                component = UnityEngine.Object.FindAnyObjectByType<T>();
                yield return null;
            }
            assign(component);
        }

        private static Button FindButton(Component root, string objectName)
        {
            return root.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.name == objectName);
        }

        private static Toggle FindToggle(Component root, string objectName)
        {
            return root.GetComponentsInChildren<Toggle>(true)
                .Single(toggle => toggle.gameObject.name == objectName);
        }

        private static Slider FindSlider(Component root, string objectName)
        {
            return root.GetComponentsInChildren<Slider>(true)
                .Single(slider => slider.gameObject.name == objectName);
        }

        private static void ResetSceneKitStaticStateForTests()
        {
            foreach (SceneHandler handler in SceneKit.GetLoadedScenes().ToArray())
            {
                handler?.OnRecycled();
            }

            GetRequiredSceneKitFieldValue<IDictionary>("sSceneCache").Clear();
            GetRequiredSceneKitFieldValue<IList>("sLoadedScenesList").Clear();
            GetRequiredSceneKitField("sActiveSceneHandler").SetValue(null, null);
            GetRequiredSceneKitField("sIsTransitioning").SetValue(null, false);
        }

        private static T GetRequiredSceneKitFieldValue<T>(string fieldName)
        {
            object value = GetRequiredSceneKitField(fieldName).GetValue(null);
            return value is T typed
                ? typed
                : throw new InvalidCastException(
                    $"测试隔离需要 {typeof(T).Name}，但 SceneKit 字段实际是 {value?.GetType().Name ?? "<null>"}。");
        }

        private static FieldInfo GetRequiredSceneKitField(string fieldName)
        {
            return typeof(SceneKit).GetField(
                    fieldName,
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(SceneKit).FullName, fieldName);
        }
    }
}
