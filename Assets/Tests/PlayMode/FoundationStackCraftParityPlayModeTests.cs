using System;
using System.Collections;
using System.IO;
using System.Linq;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tests.Support;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using YokiFrame;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证 StackCraft 同态场景只通过 Gameplay 正式链路生成模板默认 Starter 卡包初始画面。
	/// </summary>
	public sealed class FoundationStackCraftParityPlayModeTests
	{
		private const string StackCraftParityScenePath = "Assets/Scenes/FoundationStackCraftParityTest.unity";
		private const string ScreenshotFileName = "stackcraft-parity-current-ready.png";

		private string m_saveDirectory;
		private bool m_previousRunInBackground;

		[UnitySetUp]
		public IEnumerator SetUp()
		{
			m_previousRunInBackground = Application.runInBackground;
			Application.runInBackground = true;
			m_saveDirectory = Path.Combine(
				Application.temporaryCachePath,
				"Gameplay-StackCraftParityTests",
				Guid.NewGuid().ToString("N"));
			SaveSystem.ResetSaveKitConfigurationForTests();
			SaveSystem.ConfigureSaveKit(m_saveDirectory);
			yield return null;
		}

		[UnityTest]
		public IEnumerator StackCraftParityScene_CapturesStarterPackInitialFrame()
		{
			Assert.That(
				FoundationTestSceneHarness.TryReadStackCraftReferenceStarterPackPosition(
					out Vector2 referencePackPosition,
					out string referencePositionFailure),
				Is.True,
				referencePositionFailure);
			yield return LoadStackCraftParityTabletop();

			FoundationTestSceneHarness controller =
				UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			Assert.That(controller, Is.Not.Null);
			Assert.That(controller.IsReady, Is.True);
			Assert.That(controller.CardPackId.IsValid, Is.True);
			Assert.That(controller.Cards.TryGetCard(controller.CardPackId, out TabletopCard pack), Is.True);
			Assert.That(
				pack.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestCardPackContentId)));
			Assert.That(pack.RemainingUses, Is.EqualTo(4));

			TabletopCard[] allCards = controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.ToArray();
			Assert.That(
				allCards.Length,
				Is.EqualTo(1),
				"StackCraft 同态初始画面必须只生成 Starter 卡包，不能混入 Villager 规则测试卡。");
			TabletopCardStack packStack = controller.Cards.GetStackContaining(controller.CardPackId);
			Assert.That(packStack.Position.x, Is.EqualTo(referencePackPosition.x).Within(0.001f));
			Assert.That(packStack.Position.y, Is.EqualTo(referencePackPosition.y).Within(0.001f));

			TabletopCardView packView = WaitForSingleStarterPackView(controller.CardPackId);
			Assert.That(packView.DisplayedTitleText, Is.EqualTo("初始卡包"));
			Assert.That(packView.DisplaysArtwork, Is.True);
			Assert.That(packView.DisplayedArtwork.name, Is.EqualTo("Starter"));
			yield return WaitUntilStarterPackSurface(packView);

			yield return CaptureParityScreenshot(ScreenshotFileName);
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

			Application.runInBackground = m_previousRunInBackground;
			SaveSystem.ResetSaveKitConfigurationForTests();
			if (!string.IsNullOrWhiteSpace(m_saveDirectory) && Directory.Exists(m_saveDirectory))
			{
				Directory.Delete(m_saveDirectory, true);
			}

			yield return null;
		}

		private static IEnumerator LoadStackCraftParityTabletop()
		{
			yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
				StackCraftParityScenePath,
				new LoadSceneParameters(LoadSceneMode.Single));

			float timeoutAt = Time.realtimeSinceStartup + 20f;
			while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, "GameManager 启动超时。");
				yield return null;
			}

			Assert.That(
				GameManager.StartupState,
				Is.EqualTo(GameManagerStartupState.Ready),
				GameManager.StartupException?.ToString());
			Assert.That(
				GameManager.HasSystem<ScenarioDirector>(),
				Is.True,
				"StackCraft 同态场景必须由剧本导演启动，不能恢复模板旧场景 Manager 链路。");

			PlayerInput playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInput>();
			Assert.That(playerInput, Is.Not.Null, "StackCraft 同态场景必须保留正式新输入系统入口。");
			playerInput.neverAutoSwitchControlSchemes = true;

			FoundationTestSceneHarness controller = null;
			while (controller == null || !controller.IsReady || !IsStarterPackViewReady(controller.CardPackId))
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, "StackCraft 同态牌桌初始画面实例化超时。");
				controller = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
				yield return null;
			}

			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			Assert.That(scenarioDirector.HasActiveScenario, Is.True);
			Assert.That(
				scenarioDirector.ActiveScenarioId.Value,
				Is.EqualTo(FoundationTestSceneHarness.TestStackCraftParityScenarioContentId));
			Assert.That(scenarioDirector.ActiveRun.ConfirmedTurnIndex, Is.Zero);
		}

		private static bool IsStarterPackViewReady(TabletopCardId cardId)
		{
			if (!cardId.IsValid)
			{
				return false;
			}

			TabletopCardView view = UnityEngine.Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
				.SingleOrDefault(candidate => candidate.CardId == cardId);
			return view != null &&
				view.ContentId == new ContentId(FoundationTestSceneHarness.TestCardPackContentId) &&
				view.DisplaysArtwork &&
				view.DisplayedArtwork.name == "Starter" &&
				view.DisplayedTitleText == "初始卡包";
		}

		private static TabletopCardView WaitForSingleStarterPackView(TabletopCardId cardId)
		{
			TabletopCardView[] views = UnityEngine.Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
				.Where(view => view.CardId == cardId)
				.ToArray();
			Assert.That(views.Length, Is.EqualTo(1), "Starter 卡包必须只有一个正式牌桌视图。");
			return views[0];
		}

		private static IEnumerator WaitUntilStarterPackSurface(TabletopCardView view)
		{
			Vector2 expectedSize = new Vector2(0.9f, 1.3000002f);
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (view.DisplayedSurfaceMaterial == null ||
				view.DisplayedSurfaceMaterial.name != "卡牌表面_卡包" ||
				Vector2.Distance(view.AppliedCardSize, expectedSize) > 0.001f)
			{
				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					"StackCraft 同态截图不能早于 PackInstance 表面完成。" +
					$" 当前材质={view.DisplayedSurfaceMaterial?.name ?? "(空)"}，尺寸={view.AppliedCardSize:F3}。");
				yield return null;
			}

			BoxCollider collider = view.GetComponent<BoxCollider>();
			Assert.That(collider, Is.Not.Null, "Starter 卡包视图必须保留 StackCraft PackInstance 根碰撞盒。");
			Assert.That(collider.size.x, Is.EqualTo(expectedSize.x).Within(0.001f));
			Assert.That(collider.size.z, Is.EqualTo(expectedSize.y).Within(0.001f));
			Assert.That(collider.size.y, Is.EqualTo(0f).Within(0.001f));
		}

		private static IEnumerator CaptureParityScreenshot(string fileName)
		{
			string screenshotDirectory = Path.GetFullPath(
				Path.Combine(Application.dataPath, "..", "Assets", "Screenshots", "StackCraftParity"));
			Directory.CreateDirectory(screenshotDirectory);
			string screenshotPath = Path.Combine(screenshotDirectory, fileName);
			if (File.Exists(screenshotPath))
			{
				File.Delete(screenshotPath);
			}

			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(screenshotPath);
			float timeoutAt = Time.realtimeSinceStartup + 5f;
			while (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, $"同态截图没有写入：{screenshotPath}");
				yield return null;
			}

			yield return null;
		}
	}
}
