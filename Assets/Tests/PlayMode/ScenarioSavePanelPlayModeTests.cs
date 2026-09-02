using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    /// <summary>验证统一地基场景通过正式 UIKit 和 SaveKit 完成模板等价的存档操作。</summary>
    public sealed class ScenarioSavePanelPlayModeTests
    {
        private const string FoundationScenePath = "Assets/Scenes/地基测试.unity";
        private string m_saveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_saveDirectory = Path.Combine(
                Application.temporaryCachePath,
                "Gameplay-ScenarioSavePanelTests",
                Guid.NewGuid().ToString("N"));
            SaveSystem.ResetSaveKitConfigurationForTests();
            SaveSystem.ConfigureSaveKit(m_saveDirectory);
            yield return LoadFoundationTabletop();
        }

        [UnityTest]
        public IEnumerator SaveOverwriteAndLoad_UsesOneDynamicSlotList()
        {
            FoundationTestSceneHarness harness =
                UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
            ScenarioRun originalRun = director.ActiveRun;

            harness.OpenSavePanel();
            ScenarioSavePanel panel = null;
            yield return WaitForPanel(value => panel = value);
            Assert.That(panel.DisplayedSlotCount, Is.Zero);

            FindButton(panel, "CreateSave").onClick.Invoke();
            yield return null;
            Assert.That(panel.DisplayedSlotCount, Is.EqualTo(1));
            Assert.That(SaveSystem.GetAllSaveMetadata().Select(meta => meta.SlotId), Is.EqualTo(new[] { 0 }));

            FindButton(panel, "Primary").onClick.Invoke();
            yield return null;
            Assert.That(panel.DisplayedSlotCount, Is.EqualTo(1), "覆盖同一槽位不能创建第二份存档事实。");

            UIKit.ClosePanel<ScenarioSavePanel>();
            director.ConfirmTurn();
            harness.OpenSavePanel(ScenarioSavePanelMode.Load);
            yield return WaitForPanel(value => panel = value);
            FindButton(panel, "Primary").onClick.Invoke();

            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (ReferenceEquals(director.ActiveRun, originalRun))
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "点击读取后没有替换当前剧本单局。");
                yield return null;
            }

            Assert.That(originalRun.IsEnded, Is.True);
            Assert.That(director.ActiveRun.ConfirmedTurnIndex, Is.Zero);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioSavePanel>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator DeleteConfirmClearAndSaveExit_CompleteThroughUIKit()
        {
            FoundationTestSceneHarness harness =
                UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
            ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
            Assert.That(director.SaveActiveRunToSlot(0), Is.True);
            Assert.That(director.SaveActiveRunToSlot(1), Is.True);

            harness.OpenSavePanel();
            ScenarioSavePanel panel = null;
            yield return WaitForPanel(value => panel = value);
            Assert.That(panel.DisplayedSlotCount, Is.EqualTo(2));

            FindButton(panel, "Delete").onClick.Invoke();
            ConfirmationDialogPanel dialog = null;
            yield return WaitForDialog(value => dialog = value);
            FindButton(dialog, "Confirm").onClick.Invoke();
            yield return null;
            Assert.That(panel.DisplayedSlotCount, Is.EqualTo(1));

            FindButton(panel, "ClearAll").onClick.Invoke();
            yield return WaitForDialog(value => dialog = value);
            FindButton(dialog, "Confirm").onClick.Invoke();
            yield return null;
            Assert.That(panel.DisplayedSlotCount, Is.Zero);
            Assert.That(SaveSystem.GetAllSaveMetadata(), Is.Empty);

            FindButton(panel, "SaveAndExit").onClick.Invoke();
            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (director.HasActiveScenario)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "保存并退出后活动剧本没有结束。");
                yield return null;
            }

            Assert.That(SaveSystem.GetAllSaveMetadata().Count, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ScenarioSavePanel>(), Is.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            yield return null;
        }

        private static IEnumerator LoadFoundationTabletop()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                FoundationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            float timeoutAt = Time.realtimeSinceStartup + 20f;
            FoundationTestSceneHarness harness = null;
            while (GameManager.StartupState is GameManagerStartupState.NotStarted or
                   GameManagerStartupState.Initializing ||
                   harness == null || !harness.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "统一地基场景启动超时。");
                harness = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
                yield return null;
            }

            Assert.That(GameManager.StartupState, Is.EqualTo(GameManagerStartupState.Ready),
                GameManager.StartupException?.ToString());
        }

        private static IEnumerator WaitForPanel(Action<ScenarioSavePanel> assign)
        {
            ScenarioSavePanel panel = null;
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            while (panel == null || !panel.gameObject.activeInHierarchy)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "UIKit 没有打开剧本存档窗口。");
                panel = UnityEngine.Object.FindAnyObjectByType<ScenarioSavePanel>();
                yield return null;
            }
            assign(panel);
        }

        private static IEnumerator WaitForDialog(Action<ConfirmationDialogPanel> assign)
        {
            ConfirmationDialogPanel dialog = null;
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            while (dialog == null || !dialog.gameObject.activeInHierarchy)
            {
                Assert.Less(Time.realtimeSinceStartup, timeoutAt, "UIKit 没有打开通用确认框。");
                dialog = UnityEngine.Object.FindAnyObjectByType<ConfirmationDialogPanel>();
                yield return null;
            }
            assign(dialog);
        }

        private static Button FindButton(Component root, string objectName)
        {
            return root.GetComponentsInChildren<Button>(false)
                .First(button => button.gameObject.name == objectName);
        }

    }
}
