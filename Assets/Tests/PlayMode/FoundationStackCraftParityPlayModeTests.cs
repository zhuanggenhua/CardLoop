using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Gameplay.Tests.Support;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using InputSystemApi = UnityEngine.InputSystem.InputSystem;
using YokiFrame;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证 StackCraft 同态场景只通过 Gameplay 正式链路生成模板默认 Starter 卡包初始画面。
	/// </summary>
	public sealed class FoundationStackCraftParityPlayModeTests : InputTestFixture
	{
		private const string StackCraftParityScenePath = "Assets/Scenes/模板同态测试.unity";
		private const string ScreenshotFileName = "stackcraft-parity-current-ready.png";
		private const string ExpectedStarterPackDisplayName = "初始卡包";
		private const string ExpectedStarterPackArtworkName = "初始卡包";

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
			Assert.That(controller.IsTabletopReady, Is.True);
			Assert.That(controller.IsReady, Is.True, "StackCraft 同态截图必须等待顶部 HUD、右侧日志和卡牌详情 HUD 全部打开。");
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
			Assert.That(packView.DisplayedTitleText, Is.EqualTo(ExpectedStarterPackDisplayName));
			Assert.That(packView.DisplaysArtwork, Is.True);
			Assert.That(packView.DisplayedArtwork.name, Is.EqualTo(ExpectedStarterPackArtworkName));
			yield return WaitUntilStarterPackSurface(packView);

			yield return CaptureParityScreenshot(ScreenshotFileName);
		}

		[UnityTest]
		public IEnumerator StackCraftParityScene_DrawnResourceCardHasCompleteFaceOnFirstViewFrame()
		{
			yield return LoadStackCraftParityTabletop();

			FoundationTestSceneHarness controller =
				UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			Assert.That(controller, Is.Not.Null);
			OpenStarterPack(controller);
			OpenStarterPack(controller);

			// 这是资源卡的第一个可观察帧，不能依赖下一帧异步回填默认角色卡面。
			yield return null;
			TabletopCard drawnCard = controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Single(card => card.ContentId == new ContentId(
					FoundationTestSceneHarness.TestCardPackFirstRewardContentId));
			TabletopCardView drawnView = UnityEngine.Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
				.SingleOrDefault(view => view.CardId == drawnCard.Id);
			Assert.That(drawnView, Is.Not.Null, "卡包抽出的资源卡必须在首个可观察帧创建正式视图。");
			Assert.That(drawnView.DisplayedSurfaceMaterial, Is.Not.Null);
			Assert.That(drawnView.DisplayedSurfaceMaterial.name, Is.EqualTo("卡牌表面_资源"));
			Assert.That(drawnView.DisplayedArtwork, Is.Not.Null);
			Assert.That(drawnView.DisplayedArtwork.name, Is.EqualTo("浆果丛"));
		}

		[UnityTest]
		public IEnumerator StackCraftParityScene_ConsecutivePackOpensAnimateFromLiftedPackStack()
		{
			Mouse mouse = InputSystemApi.AddDevice<Mouse>("StackCraftParityPackMouse");
			Keyboard keyboard = InputSystemApi.AddDevice<Keyboard>("StackCraftParityPackKeyboard");
			yield return LoadStackCraftParityTabletop();

			FoundationTestSceneHarness controller =
				UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			TabletopView tabletopView =
				UnityEngine.Object.FindAnyObjectByType<TabletopView>();
			PlayerInput playerInput =
				UnityEngine.Object.FindAnyObjectByType<PlayerInput>();
			TabletopCardDragInput dragInput =
				UnityEngine.Object.FindAnyObjectByType<TabletopCardDragInput>();
			Assert.That(controller, Is.Not.Null);
			Assert.That(tabletopView, Is.Not.Null);
			Assert.That(playerInput, Is.Not.Null);
			Assert.That(dragInput, Is.Not.Null);
			Assert.That(playerInput.SwitchCurrentControlScheme(keyboard, mouse), Is.True);
			GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);

			for (int openIndex = 0; openIndex < 2; openIndex++)
			{
				TabletopCardView packView = WaitForSingleStarterPackView(controller.CardPackId);
				TabletopCardId[] beforeCardIds = SnapshotCardIds(controller);
				Camera camera = GameManager.MainCamera;
				Assert.That(camera, Is.Not.Null, "StackCraft 同态开包测试必须通过正式主相机点击卡包。");
				BoxCollider packCollider = packView.GetComponent<BoxCollider>();
				Assert.That(packCollider, Is.Not.Null, "Starter 卡包视图必须保留根碰撞盒，测试才能走真实玩家点击。");
				Vector2 pressScreenPosition =
					FindScreenPointThatHitsCard(camera, packCollider, controller.CardPackId);
				Vector2 pressTablePosition =
					TabletopCoordinateSpace.ToTablePosition(packView.transform.localPosition);
				Vector2 releaseTablePosition = pressTablePosition +
					Vector2.right * (tabletopView.CardClickThreshold * 0.5f);
				Vector2 releaseScreenPosition =
					TableToScreenPoint(camera, packView, releaseTablePosition);

				Move(mouse.position, pressScreenPosition);
				yield return null;
				Press(mouse.leftButton);
				yield return null;
				Assert.That(
					dragInput.IsPointerSessionActive,
					Is.True,
					$"第 {openIndex + 1} 次开包按下卡包后没有建立 StackCraft 式拖拽会话。");
				Move(mouse.position, releaseScreenPosition);
				yield return null;
				Assert.That(
					dragInput.IsDragging,
					Is.False,
					$"第 {openIndex + 1} 次开包低于点击阈值的小移动不能被解释成普通拖拽。");
				Vector3 liftedPackLocalPosition = packView.transform.localPosition;
				Vector2 liftedPackTablePosition =
					TabletopCoordinateSpace.ToTablePosition(liftedPackLocalPosition);
				Assert.That(
					Vector2.Distance(liftedPackTablePosition, releaseTablePosition),
					Is.LessThan(0.01f),
					$"第 {openIndex + 1} 次开包释放前，卡包必须已经跟随指针到当前点击位置。");
				Assert.That(
					liftedPackLocalPosition.y,
					Is.EqualTo(tabletopView.CardDragHeight).Within(0.001f),
					$"第 {openIndex + 1} 次开包释放前，卡包必须已经处于 StackCraft 拖拽高度。");

				Release(mouse.leftButton);
				TabletopCardId createdCardId =
					FindSingleCreatedCardId(controller, beforeCardIds);
				TabletopCardView createdView = null;
				float viewTimeoutAt = Time.realtimeSinceStartup + 5f;
				while (createdView == null)
				{
					createdView = FindSingleCardViewOrNull(createdCardId);
					Assert.Less(
						Time.realtimeSinceStartup,
						viewTimeoutAt,
						$"第 {openIndex + 1} 次开包已经创建卡牌 {createdCardId}，但没有生成牌桌视图。");
					yield return null;
				}

				Vector3 firstObservedLocalPosition = createdView.transform.localPosition;
				Vector2 firstObservedTablePosition =
					TabletopCoordinateSpace.ToTablePosition(firstObservedLocalPosition);
				Assert.That(
					Vector2.Distance(firstObservedTablePosition, liftedPackTablePosition),
					Is.LessThan(0.01f),
					$"第 {openIndex + 1} 次开包的新卡首个可观察位置必须从当前抬起的卡包位置推出，而不是原始地面位置。");
				Assert.That(
					firstObservedLocalPosition.y,
					Is.EqualTo(liftedPackLocalPosition.y + 0.1f).Within(0.001f),
					$"第 {openIndex + 1} 次开包的新卡必须从卡包当前拖拽高度再上抬 0.1 出生。");

				yield return WaitUntilCardViewSettlesAtStackPose(
					controller,
					createdView,
					$"第 {openIndex + 1} 次开包的新卡没有按 StackCraft 0.1 秒补间归入权威牌堆。");
			}
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

			FoundationTestSceneHarness controller;
			while (true)
			{
				controller = UnityEngine.Object.FindAnyObjectByType<FoundationTestSceneHarness>();
				if (controller != null &&
					controller.IsReady &&
					IsStarterPackViewReady(controller.CardPackId))
				{
					break;
				}

				Assert.Less(
					Time.realtimeSinceStartup,
					timeoutAt,
					BuildStarterPackReadyDiagnostic(controller));
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
				view.DisplayedArtwork.name == ExpectedStarterPackArtworkName &&
				view.DisplayedTitleText == ExpectedStarterPackDisplayName;
		}

		private static TabletopCardId[] SnapshotCardIds(FoundationTestSceneHarness controller)
		{
			return controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Select(card => card.Id)
				.ToArray();
		}

		private static void OpenStarterPack(FoundationTestSceneHarness controller)
		{
			TabletopCardStack packStack = controller.Cards.GetStackContaining(controller.CardPackId);
			TabletopCardPointerReleaseIntent click = new TabletopCardPointerReleaseIntent(
				controller.CardPackId,
				packStack.Position,
				packStack.Position,
				packStack.Position,
				isDrag: false);
			ActionCandidate[] candidates = controller.ScenarioRun.FindActionCandidates(click);
			Assert.That(candidates, Has.Length.EqualTo(1));
			Assert.That(
				candidates[0].Action.ContentId,
				Is.EqualTo(new ContentId(FoundationTestSceneHarness.TestOpenCardPackActionContentId)));
			controller.ScenarioRun.StartAction(ActionRequest.FromCandidate(candidates[0]));
		}

		private static TabletopCardId FindSingleCreatedCardId(
			FoundationTestSceneHarness controller,
			TabletopCardId[] beforeCardIds)
		{
			TabletopCardId[] createdCardIds = controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Select(card => card.Id)
				.Where(cardId => !beforeCardIds.Contains(cardId))
				.ToArray();
			Assert.That(createdCardIds, Has.Length.EqualTo(1), "每次真实点击 Starter 卡包必须只创建一个新卡牌实例。");
			return createdCardIds[0];
		}

		private static TabletopCardView FindSingleCardViewOrNull(TabletopCardId cardId)
		{
			TabletopCardView[] views = UnityEngine.Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
				.Where(view => view.CardId == cardId)
				.ToArray();
			Assert.That(views.Length, Is.LessThanOrEqualTo(1), $"局内卡牌 {cardId} 不能拥有多个正式牌桌视图。");
			return views.Length == 0 ? null : views[0];
		}

		private static IEnumerator WaitUntilCardViewSettlesAtStackPose(
			FoundationTestSceneHarness controller,
			TabletopCardView view,
			string timeoutMessage)
		{
			float timeoutAt = Time.realtimeSinceStartup + 1f;
			while (Vector3.Distance(
					view.transform.localPosition,
					GetExpectedStackLocalPosition(controller, view.CardId)) > 0.01f)
			{
				Assert.Less(Time.realtimeSinceStartup, timeoutAt, timeoutMessage);
				yield return null;
			}
		}

		private static Vector3 GetExpectedStackLocalPosition(
			FoundationTestSceneHarness controller,
			TabletopCardId cardId)
		{
			TabletopCardStack stack = controller.Cards.GetStackContaining(cardId);
			int cardIndex = stack.IndexOf(cardId);
			Assert.That(cardIndex, Is.GreaterThanOrEqualTo(0), $"牌堆中找不到局内卡牌 {cardId}。");
			Vector2 tablePosition = stack.Position + controller.PlacementRules.Geometry.StackStep * cardIndex;
			return TabletopCoordinateSpace.ToLocalPosition(tablePosition);
		}

		private static Vector2 TableToScreenPoint(
			Camera camera,
			TabletopCardView view,
			Vector2 tablePosition)
		{
			Vector3 localPosition = TabletopCoordinateSpace.ToLocalPosition(tablePosition);
			Vector3 worldPosition = view.transform.parent.TransformPoint(localPosition);
			return camera.WorldToScreenPoint(worldPosition);
		}

		private static Vector2 FindScreenPointThatHitsCard(
			Camera camera,
			BoxCollider collider,
			TabletopCardId expectedCardId)
		{
			const int sampleSteps = 12;
			for (int zIndex = 0; zIndex <= sampleSteps; zIndex++)
			{
				float normalizedZ = Mathf.Lerp(0.98f, -0.98f, zIndex / (float)sampleSteps);
				for (int xIndex = 0; xIndex <= sampleSteps; xIndex++)
				{
					float normalizedX = Mathf.Lerp(-0.98f, 0.98f, xIndex / (float)sampleSteps);
					Vector3 localPoint = collider.center + new Vector3(
						collider.size.x * 0.5f * normalizedX,
						0f,
						collider.size.z * 0.5f * normalizedZ);
					Vector2 screenPoint = camera.WorldToScreenPoint(
						collider.transform.TransformPoint(localPoint));
					if (TryFindCardHitAt(camera, screenPoint, out TabletopCardView hitView) &&
						hitView.CardId == expectedCardId &&
						!UIPointerUtility.IsPositionOverUI(screenPoint))
					{
						return screenPoint;
					}
				}
			}

			Assert.Fail(
				$"当前相机与 Collider 下找不到可直接命中 Starter 卡包 {expectedCardId}、且不被 UI 挡住的外露点，无法验证真实开包输入。");
			return default;
		}

		private static bool TryFindCardHitAt(
			Camera camera,
			Vector2 screenPosition,
			out TabletopCardView view)
		{
			Ray ray = camera.ScreenPointToRay(screenPosition);
			RaycastHit[] hits = Physics.RaycastAll(
				ray,
				float.PositiveInfinity,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore);
			TabletopCardView bestView = null;
			float bestDistance = float.PositiveInfinity;
			for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
			{
				RaycastHit hit = hits[hitIndex];
				TabletopCardView candidate = hit.collider.GetComponentInParent<TabletopCardView>();
				if (candidate == null || !candidate.CardId.IsValid)
				{
					continue;
				}
				if (bestView == null || hit.distance < bestDistance - 0.0001f)
				{
					bestView = candidate;
					bestDistance = hit.distance;
				}
			}

			view = bestView;
			return view != null;
		}

		private static string BuildStarterPackReadyDiagnostic(FoundationTestSceneHarness controller)
		{
			StringBuilder builder = new StringBuilder("StackCraft 同态牌桌初始画面实例化超时。");
			builder.Append(" GameManager=").Append(GameManager.StartupState);
			if (controller == null)
			{
				builder.Append("；场景装配器=未生成。");
				return builder.ToString();
			}

			builder.Append("；牌桌核心就绪=").Append(controller.IsTabletopReady)
				.Append("；HUD就绪=").Append(controller.IsReady)
				.Append("；卡包ID=").Append(controller.CardPackId);
			if (controller.Cards == null)
			{
				builder.Append("；牌桌卡牌集合=空。");
			}
			else if (controller.CardPackId.IsValid &&
				controller.Cards.TryGetCard(controller.CardPackId, out TabletopCard pack))
			{
				builder.Append("；卡包内容=").Append(pack.ContentId.Value)
					.Append("；剩余次数=").Append(pack.RemainingUses);
			}
			else
			{
				builder.Append("；卡包=未在牌桌查回。");
			}

			TabletopCardView[] views = UnityEngine.Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
				.Where(view => !controller.CardPackId.IsValid || view.CardId == controller.CardPackId)
				.ToArray();
			builder.Append("；匹配视图数=").Append(views.Length);
			for (int index = 0; index < views.Length && index < 3; index++)
			{
				TabletopCardView view = views[index];
				builder.Append("；视图").Append(index + 1)
					.Append(" 标题=").Append(view.DisplayedTitleText)
					.Append(" 卡图=").Append(view.DisplayedArtwork == null ? "(空)" : view.DisplayedArtwork.name)
					.Append(" 材质=").Append(view.DisplayedSurfaceMaterial == null ? "(空)" : view.DisplayedSurfaceMaterial.name)
					.Append(" 尺寸=").Append(view.AppliedCardSize.ToString("F3"));
			}

			return builder.ToString();
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
