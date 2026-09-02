using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Gameplay.Tests.Support.Editor
{
	/// <summary>
	/// 通过正式 Gameplay 地基场景采集 StackCraft 吸收过程链证据，规避 Test Runner PlayMode 回连缺陷。
	/// </summary>
	[InitializeOnLoad]
	internal static class FoundationE2ECurrentCaptureMenu
	{
		private const string MenuPath = "Gameplay/Automation/Capture Foundation E2E Current";
		private const string SessionPrefix = "Gameplay.FoundationE2ECurrentCapture.";
		private const string SessionStepKey = SessionPrefix + "Step";
		private const string SessionScreenshotPathKey = SessionPrefix + "ScreenshotPath";
		private const string SessionResultPathKey = SessionPrefix + "ResultPath";
		private const string SessionSaveDirectoryKey = SessionPrefix + "SaveDirectory";
		private const string SessionDeadlineKey = SessionPrefix + "Deadline";
		private const string SessionCaptureFrameKey = SessionPrefix + "CaptureFrame";
		private const string SessionCaptureRequestedKey = SessionPrefix + "CaptureRequested";
		private const string SessionCaptureFileNameKey = SessionPrefix + "CaptureFileName";
		private const string ContactSheetFileName = "_contactsheet-foundation-e2e-sequence-latest.png";
		private const string ResultFileName = "foundation-e2e-current-result.json";
		private const double PlayModeTimeoutSeconds = 90d;
		private const double StepTimeoutSeconds = 10d;
		private const double ScreenshotTimeoutSeconds = 5d;

		private static readonly string[] ScreenshotFileNames =
		{
			"foundation-e2e-sequence-01-ready.png",
			"foundation-e2e-sequence-02-damage-feedback.png",
			"foundation-e2e-sequence-03-action-choice.png",
			"foundation-e2e-sequence-04-action-progress-start.png",
			"foundation-e2e-sequence-05-action-progress-half.png",
			"foundation-e2e-sequence-06-action-completed-product.png",
		};

		private static CaptureStep s_step;
		private static string s_screenshotDirectory;
		private static string s_resultPath;
		private static string s_saveDirectory;
		private static string s_captureFileName;
		private static double s_deadline;
		private static int s_captureFrame;
		private static bool s_captureRequested;
		private static ContentId s_selectedActionId;
		private static ActionInstance s_startedAction;

		static FoundationE2ECurrentCaptureMenu()
		{
			RestoreState();
			EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
			EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

			if (s_step != CaptureStep.None)
			{
				RegisterTick();
				Debug.Log($"恢复 Foundation E2E 当前过程采集，当前步骤：{s_step}。");
			}
		}

		private enum CaptureStep
		{
			None,
			EnteringPlayMode,
			WaitingReady,
			CaptureReadyWait,
			PresentDamage,
			CaptureDamageWait,
			PresentActionChoice,
			WaitingActionChoice,
			CaptureActionChoiceWait,
			SelectAction,
			WaitingActionStarted,
			CaptureProgressStartWait,
			ConfirmFirstTurn,
			CaptureProgressHalfWait,
			ConfirmSecondTurn,
			WaitingCompletedProduct,
			CaptureCompletedProductWait,
			CreatingContactSheet,
		}

		[MenuItem(MenuPath)]
		private static void Capture()
		{
			if (s_step != CaptureStep.None)
			{
				Debug.LogWarning("Foundation E2E 当前过程采集已经在运行。");
				return;
			}

			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				Debug.LogError("Unity 正在编译或导入，拒绝开始 Foundation E2E 当前过程采集。");
				return;
			}

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				Debug.LogError("当前已经在切换或运行 PlayMode，拒绝叠加 Foundation E2E 当前过程采集。");
				return;
			}

			if (HasDirtyLoadedScene())
			{
				Debug.LogError("当前有未保存场景，拒绝自动切换到 FoundationTest 场景。请先保存或处理当前场景。");
				return;
			}

			try
			{
				s_screenshotDirectory = Path.GetFullPath(
					Path.Combine(Application.dataPath, "..", "Assets", "Screenshots", "FoundationE2E"));
				Directory.CreateDirectory(s_screenshotDirectory);
				s_resultPath = Path.Combine(s_screenshotDirectory, ResultFileName);
				s_saveDirectory = Path.Combine(
					Application.temporaryCachePath,
					"Gameplay-FoundationE2ECurrentCapture",
					Guid.NewGuid().ToString("N"));
				DeleteGeneratedEvidence();

				EditorSceneManager.OpenScene(FoundationTestSceneMenu.ScenePath, OpenSceneMode.Single);
				s_step = CaptureStep.EnteringPlayMode;
				s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
				s_captureRequested = false;
				s_captureFileName = string.Empty;
				s_startedAction = null;
				s_selectedActionId = default;
				SaveState();
				RegisterTick();
				EditorApplication.isPlaying = true;
				Debug.Log("开始采集当前项目正式 Gameplay 的 Foundation E2E 过程图。");
			}
			catch (Exception exception)
			{
				Fail(exception);
			}
		}

		private static void Tick()
		{
			try
			{
				if (s_step != CaptureStep.None &&
					!EditorApplication.isPlaying &&
					!EditorApplication.isPlayingOrWillChangePlaymode)
				{
					Debug.LogWarning($"Foundation E2E 当前过程采集在 PlayMode 外恢复，清理未完成步骤：{s_step}。");
					CleanupTick();
					return;
				}

				if (EditorApplication.timeSinceStartup > s_deadline)
				{
					throw new TimeoutException(
						$"Foundation E2E 当前过程采集超时，当前步骤：{s_step}，等待点：{DescribeCurrentWaitPoint()}。");
				}

				switch (s_step)
				{
					case CaptureStep.EnteringPlayMode:
						TickEnteringPlayMode();
						break;
					case CaptureStep.WaitingReady:
						TickWaitingReady();
						break;
					case CaptureStep.CaptureReadyWait:
						TickCaptureWait(CaptureStep.PresentDamage);
						break;
					case CaptureStep.PresentDamage:
						TickPresentDamage();
						break;
					case CaptureStep.CaptureDamageWait:
						TickCaptureWait(CaptureStep.PresentActionChoice);
						break;
					case CaptureStep.PresentActionChoice:
						TickPresentActionChoice();
						break;
					case CaptureStep.WaitingActionChoice:
						TickWaitingActionChoice();
						break;
					case CaptureStep.CaptureActionChoiceWait:
						TickCaptureWait(CaptureStep.SelectAction);
						break;
					case CaptureStep.SelectAction:
						TickSelectAction();
						break;
					case CaptureStep.WaitingActionStarted:
						TickWaitingActionStarted();
						break;
					case CaptureStep.CaptureProgressStartWait:
						TickCaptureWait(CaptureStep.ConfirmFirstTurn);
						break;
					case CaptureStep.ConfirmFirstTurn:
						TickConfirmFirstTurn();
						break;
					case CaptureStep.CaptureProgressHalfWait:
						TickCaptureWait(CaptureStep.ConfirmSecondTurn);
						break;
					case CaptureStep.ConfirmSecondTurn:
						TickConfirmSecondTurn();
						break;
					case CaptureStep.WaitingCompletedProduct:
						TickWaitingCompletedProduct();
						break;
					case CaptureStep.CaptureCompletedProductWait:
						TickCaptureWait(CaptureStep.CreatingContactSheet);
						break;
					case CaptureStep.CreatingContactSheet:
						TickCreatingContactSheet();
						break;
					case CaptureStep.None:
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(s_step), s_step, "未知 Foundation E2E 当前过程采集步骤。");
				}
			}
			catch (Exception exception)
			{
				Fail(exception);
			}
		}

		private static void TickEnteringPlayMode()
		{
			if (!EditorApplication.isPlaying ||
				SceneManager.GetActiveScene().path != FoundationTestSceneMenu.ScenePath)
			{
				return;
			}

			InvokeSaveSystemMethod("ResetSaveKitConfigurationForTests");
			InvokeSaveSystemMethod("ConfigureSaveKit", s_saveDirectory);
			s_step = CaptureStep.WaitingReady;
			s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
			SaveState();
		}

		private static void TickWaitingReady()
		{
			if (!TryValidateReadyState(out string pendingReason))
			{
				if (!string.IsNullOrWhiteSpace(pendingReason))
				{
					Debug.Log($"等待 Foundation E2E 场景就绪：{pendingReason}");
				}

				return;
			}

			StartScreenshot(ScreenshotFileNames[0], CaptureStep.CaptureReadyWait);
		}

		private static void TickPresentDamage()
		{
			FoundationTestSceneHarness controller = RequireController();
			if (!controller.Cards.TryGetCard(controller.TargetCardId, out TabletopCard tabletopCard))
			{
				throw new InvalidOperationException("Foundation E2E 目标卡牌不存在，不能投放伤害反馈。");
			}
			if (tabletopCard is not CharacterCard character)
			{
				throw new InvalidOperationException("Foundation E2E 目标卡牌必须是角色卡，才能承载 GAS 伤害反馈。");
			}

			EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
				character.AbilitySystem,
				28,
				isMissed: false,
				isCriticalHit: true,
				isSilent: false,
				damageType: EDamageType.Physical,
				visualFlags: EEffectVisualFlags.None,
				matchupResult: DamageMatchupResult.Advantage));
			StartScreenshot(ScreenshotFileNames[1], CaptureStep.CaptureDamageWait);
		}

		private static void TickPresentActionChoice()
		{
			FoundationTestSceneHarness controller = RequireController();
			TabletopInteraction interaction = Object.FindAnyObjectByType<TabletopInteraction>();
			if (interaction == null || !interaction.IsBound)
			{
				throw new InvalidOperationException("Foundation E2E 场景缺少已绑定的正式牌桌交互组件。");
			}

			Vector2 sourcePosition = controller.Cards.GetStackContaining(controller.MiddleCardId).Position;
			Vector2 targetPosition = controller.Cards.GetStackContaining(controller.TargetCardId).Position;
			TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
				controller.MiddleCardId,
				sourcePosition,
				targetPosition,
				sourcePosition,
				isDrag: true,
				controller.TargetCardId);
			bool keepReleasedPlacement = interaction.HandleRelease(intent, out ActionCandidate[] candidates);
			if (!keepReleasedPlacement)
			{
				throw new InvalidOperationException(
					"Foundation E2E 拖拽释放不应进入 UI 填槽恢复路径。");
			}
			if (candidates.Length != 1)
			{
				throw new InvalidOperationException(
					$"Foundation E2E 拖拽释放必须得到 1 个行动候选，当前 {candidates.Length} 个。");
			}
			if (candidates[0].Action.ContentId.Value != FoundationTestSceneHarness.TestActionContentId)
			{
				throw new InvalidOperationException(
					$"Foundation E2E 行动候选错误：{candidates[0].Action.ContentId}。");
			}

			s_selectedActionId = candidates[0].Action.ContentId;
			s_step = CaptureStep.WaitingActionChoice;
			s_deadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
			SaveState();
		}

		private static void TickWaitingActionChoice()
		{
			TabletopActionChoicePanel panel = Object.FindAnyObjectByType<TabletopActionChoicePanel>();
			if (panel == null || !panel.gameObject.activeInHierarchy)
			{
				return;
			}
			if (panel.ChoiceCount != 1)
			{
				throw new InvalidOperationException($"Foundation E2E 行动选择面板候选数量错误：{panel.ChoiceCount}。");
			}
			if (GameManager.GameStateSystem.currentState != EGameState.Dialogue)
			{
				throw new InvalidOperationException("Foundation E2E 行动选择面板打开后没有进入 UI 输入层。");
			}

			StartScreenshot(ScreenshotFileNames[2], CaptureStep.CaptureActionChoiceWait);
		}

		private static void TickSelectAction()
		{
			TabletopActionChoicePanel panel = Object.FindAnyObjectByType<TabletopActionChoicePanel>();
			if (panel == null || !panel.gameObject.activeInHierarchy)
			{
				throw new InvalidOperationException("Foundation E2E 选择行动前，行动选择面板已经关闭。");
			}

			Button choiceButton = panel.GetComponentsInChildren<Button>(includeInactive: true)
				.SingleOrDefault(button => button.gameObject.name == "ActionChoice_0");
			if (choiceButton == null)
			{
				throw new InvalidOperationException("Foundation E2E 行动选择面板缺少第一个候选按钮。");
			}

			choiceButton.onClick.Invoke();
			s_step = CaptureStep.WaitingActionStarted;
			s_deadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
			SaveState();
		}

		private static void TickWaitingActionStarted()
		{
			FoundationTestSceneHarness controller = RequireController();
			IReadOnlyList<ActionInstance> activeActions = controller.ScenarioRun.Tabletop.ActiveActions;
			if (activeActions.Count == 0)
			{
				return;
			}
			if (activeActions.Count != 1)
			{
				throw new InvalidOperationException($"Foundation E2E 行动提交后活动行动数量错误：{activeActions.Count}。");
			}
			s_startedAction = activeActions[0];
			if (s_startedAction.ActionId != s_selectedActionId)
			{
				throw new InvalidOperationException(
					$"Foundation E2E 活动行动 ID 错误：{s_startedAction.ActionId}。");
			}

			StartScreenshot(ScreenshotFileNames[3], CaptureStep.CaptureProgressStartWait);
		}

		private static void TickConfirmFirstTurn()
		{
			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			int confirmedTurn = scenarioDirector.ConfirmTurn();
			if (confirmedTurn != 1)
			{
				throw new InvalidOperationException($"Foundation E2E 第一次推进回合返回 {confirmedTurn}，预期 1。");
			}

			ActionInstance action = RequireSingleActiveAction();
			if (!Mathf.Approximately(action.Progress, 0.5f))
			{
				throw new InvalidOperationException($"Foundation E2E 行动半程进度错误：{action.Progress}。");
			}

			StartScreenshot(ScreenshotFileNames[4], CaptureStep.CaptureProgressHalfWait);
		}

		private static void TickConfirmSecondTurn()
		{
			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			int confirmedTurn = scenarioDirector.ConfirmTurn();
			if (confirmedTurn != 2)
			{
				throw new InvalidOperationException($"Foundation E2E 第二次推进回合返回 {confirmedTurn}，预期 2。");
			}

			s_step = CaptureStep.WaitingCompletedProduct;
			s_deadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
			SaveState();
		}

		private static void TickWaitingCompletedProduct()
		{
			FoundationTestSceneHarness controller = RequireController();
			if (controller.ScenarioRun.Tabletop.ActiveActions.Count > 0)
			{
				return;
			}
			if (CountCards(controller, FoundationTestSceneHarness.TestProductContentId) <= 0)
			{
				return;
			}
			if (!DoViewsDisplayArtwork(FoundationTestSceneHarness.TestProductContentId))
			{
				return;
			}

			StartScreenshot(ScreenshotFileNames[5], CaptureStep.CaptureCompletedProductWait);
		}

		private static void TickCreatingContactSheet()
		{
			CreateVisualEvidenceContactSheet();
			WriteResult(CreatePassResult());
			CleanupAndExitPlayMode();
			Debug.Log($"Foundation E2E 当前过程图已写入：{s_screenshotDirectory}");
		}

		private static void StartScreenshot(string fileName, CaptureStep waitStep)
		{
			s_captureFileName = fileName;
			s_captureRequested = false;
			s_captureFrame = Time.frameCount + 2;
			s_step = waitStep;
			s_deadline = EditorApplication.timeSinceStartup + ScreenshotTimeoutSeconds;
			SaveState();
		}

		private static void TickCaptureWait(CaptureStep nextStep)
		{
			if (Time.frameCount < s_captureFrame)
			{
				return;
			}

			string screenshotPath = Path.Combine(s_screenshotDirectory, s_captureFileName);
			if (!s_captureRequested)
			{
				DeleteGeneratedFileAndMeta(screenshotPath);
				ScreenCapture.CaptureScreenshot(screenshotPath);
				s_captureRequested = true;
				SaveState();
				return;
			}

			if (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
			{
				return;
			}

			s_step = nextStep;
			s_captureRequested = false;
			s_captureFileName = string.Empty;
			s_deadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
			SaveState();
		}

		private static bool TryValidateReadyState(out string pendingReason)
		{
			pendingReason = null;
			if (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
			{
				pendingReason = $"GameManager 启动中：{GameManager.StartupState}";
				return false;
			}

			if (GameManager.StartupState != GameManagerStartupState.Ready)
			{
				throw new InvalidOperationException(
					$"GameManager 启动失败：{GameManager.StartupState}\n{GameManager.StartupException}");
			}
			if (!GameManager.HasSystem<ScenarioDirector>())
			{
				throw new InvalidOperationException("Foundation E2E 场景必须由剧本导演启动。");
			}

			FoundationTestSceneHarness controller = Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			if (controller == null)
			{
				pendingReason = "FoundationTestSceneHarness 尚未生成。";
				return false;
			}
			if (!controller.IsReady)
			{
				pendingReason = "FoundationTestSceneHarness 尚未完成内容、牌桌和 HUD 绑定。";
				return false;
			}
			if (!AreAllInitialViewsReady())
			{
				pendingReason = "初始牌桌卡牌视图尚未全部完成插画投影。";
				return false;
			}

			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			if (!scenarioDirector.HasActiveScenario ||
				scenarioDirector.ActiveScenarioId.Value != FoundationTestSceneHarness.TestScenarioContentId)
			{
				throw new InvalidOperationException(
					$"Foundation E2E 活动剧本错误：{scenarioDirector.ActiveScenarioId}。");
			}
			if (scenarioDirector.ActiveRun.ConfirmedTurnIndex != 0)
			{
				throw new InvalidOperationException(
					$"Foundation E2E 初始已确认回合错误：{scenarioDirector.ActiveRun.ConfirmedTurnIndex}。");
			}

			return true;
		}

		private static bool AreAllInitialViewsReady()
		{
			TabletopCardView[] views = Object.FindObjectsByType<TabletopCardView>(
				FindObjectsInactive.Exclude);
			return views.Length >= 4 && views.All(view => view.DisplaysArtwork);
		}

		private static FoundationTestSceneHarness RequireController()
		{
			FoundationTestSceneHarness controller = Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			if (controller == null || !controller.IsReady)
			{
				throw new InvalidOperationException("Foundation E2E 测试场景尚未就绪。");
			}
			return controller;
		}

		private static ActionInstance RequireSingleActiveAction()
		{
			FoundationTestSceneHarness controller = RequireController();
			IReadOnlyList<ActionInstance> activeActions = controller.ScenarioRun.Tabletop.ActiveActions;
			if (activeActions.Count != 1)
			{
				throw new InvalidOperationException($"Foundation E2E 当前活动行动数量错误：{activeActions.Count}。");
			}
			return activeActions[0];
		}

		private static int CountCards(FoundationTestSceneHarness controller, string contentId)
		{
			ContentId expected = new ContentId(contentId);
			return controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.Count(card => card.ContentId == expected);
		}

		private static bool DoViewsDisplayArtwork(string contentId)
		{
			ContentId expected = new ContentId(contentId);
			TabletopCardView[] matchingViews = Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude)
				.Where(view => view.ContentId == expected)
				.ToArray();
			return matchingViews.Length > 0 &&
				matchingViews.All(view =>
					view.DisplaysArtwork &&
					view.DisplayedArtwork != null &&
					view.DisplayedArtwork.name != "卡牌占位图");
		}

		private static void CreateVisualEvidenceContactSheet()
		{
			var textures = new List<Texture2D>(ScreenshotFileNames.Length);
			try
			{
				foreach (string fileName in ScreenshotFileNames)
				{
					string path = Path.Combine(s_screenshotDirectory, fileName);
					if (!File.Exists(path))
					{
						throw new FileNotFoundException($"端到端过程图缺失，无法生成拼图：{path}", path);
					}

					var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
					if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
					{
						throw new InvalidOperationException($"端到端过程图无法解码，无法生成拼图：{path}");
					}
					textures.Add(texture);
				}

				int columns = Mathf.Min(3, textures.Count);
				int rows = Mathf.CeilToInt(textures.Count / (float)columns);
				int cellWidth = textures.Max(texture => texture.width);
				int cellHeight = textures.Max(texture => texture.height);
				var contactSheet = new Texture2D(
					cellWidth * columns,
					cellHeight * rows,
					TextureFormat.RGBA32,
					false);
				try
				{
					Color32[] pixels = Enumerable
						.Repeat(new Color32(15, 15, 15, 255), contactSheet.width * contactSheet.height)
						.ToArray();
					contactSheet.SetPixels32(pixels);

					for (int index = 0; index < textures.Count; index++)
					{
						Texture2D texture = textures[index];
						int column = index % columns;
						int row = index / columns;
						int xOffset = column * cellWidth;
						int yOffset = (rows - 1 - row) * cellHeight;
						contactSheet.SetPixels32(
							xOffset,
							yOffset,
							texture.width,
							texture.height,
							texture.GetPixels32());
					}

					contactSheet.Apply();
					string outputPath = Path.Combine(s_screenshotDirectory, ContactSheetFileName);
					DeleteGeneratedFileAndMeta(outputPath);
					File.WriteAllBytes(outputPath, ImageConversion.EncodeToPNG(contactSheet));
				}
				finally
				{
					Object.Destroy(contactSheet);
				}
			}
			finally
			{
				foreach (Texture2D texture in textures)
				{
					Object.Destroy(texture);
				}
			}
		}

		private static CaptureResult CreatePassResult()
		{
			FoundationTestSceneHarness controller = RequireController();
			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			return new CaptureResult
			{
				status = "pass",
				scenePath = SceneManager.GetActiveScene().path,
				scenarioId = scenarioDirector.ActiveScenarioId.Value,
				confirmedTurnIndex = scenarioDirector.ActiveRun.ConfirmedTurnIndex,
				actionId = FoundationTestSceneHarness.TestActionContentId,
				productContentId = FoundationTestSceneHarness.TestProductContentId,
				productCount = CountCards(controller, FoundationTestSceneHarness.TestProductContentId),
				screenshotDirectory = s_screenshotDirectory,
				artifacts = BuildArtifacts(),
			};
		}

		private static ScreenshotArtifact[] BuildArtifacts()
		{
			List<ScreenshotArtifact> artifacts = new List<ScreenshotArtifact>();
			foreach (string fileName in ScreenshotFileNames.Concat(new[] { ContactSheetFileName }))
			{
				string path = Path.Combine(s_screenshotDirectory, fileName);
				artifacts.Add(ReadArtifact(path));
			}
			return artifacts.ToArray();
		}

		private static ScreenshotArtifact ReadArtifact(string path)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"Foundation E2E 证据文件缺失：{path}", path);
			}

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			try
			{
				byte[] bytes = File.ReadAllBytes(path);
				if (!ImageConversion.LoadImage(texture, bytes))
				{
					throw new InvalidOperationException($"Foundation E2E 证据文件无法解码：{path}");
				}

				return new ScreenshotArtifact
				{
					path = path,
					fileName = Path.GetFileName(path),
					width = texture.width,
					height = texture.height,
					bytes = bytes.LongLength,
					sha256 = ComputeSha256(bytes),
				};
			}
			finally
			{
				Object.Destroy(texture);
			}
		}

		private static string ComputeSha256(byte[] bytes)
		{
			using SHA256 sha256 = SHA256.Create();
			return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
		}

		private static void HandlePlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredPlayMode && s_step == CaptureStep.EnteringPlayMode)
			{
				s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
				SaveState();
				return;
			}

			if (state != PlayModeStateChange.EnteredEditMode || s_step == CaptureStep.None)
			{
				return;
			}

			Debug.LogWarning($"Foundation E2E 当前过程采集未完成就退出 PlayMode，清理未完成步骤：{s_step}。");
			CleanupTick();
		}

		private static string DescribeCurrentWaitPoint()
		{
			string activeScenePath = SceneManager.GetActiveScene().path;
			return s_step switch
			{
				CaptureStep.EnteringPlayMode when !EditorApplication.isPlaying => "Unity 尚未进入 PlayMode",
				CaptureStep.EnteringPlayMode when activeScenePath != FoundationTestSceneMenu.ScenePath =>
					$"当前场景不是 FoundationTest，实际为 {activeScenePath}",
				CaptureStep.EnteringPlayMode => "等待配置测试存档目录",
				CaptureStep.WaitingReady when !TryValidateReadyState(out string pendingReason) =>
					pendingReason ?? "等待 FoundationTest 初始化完成",
				CaptureStep.WaitingActionChoice => "等待行动选择面板打开",
				CaptureStep.WaitingActionStarted => "等待行动选择按钮提交活动行动",
				CaptureStep.WaitingCompletedProduct => "等待第二次推进回合后生成产物并完成插画投影",
				_ when s_step.ToString().Contains("Capture", StringComparison.Ordinal) =>
					$"等待截图文件写入：{s_captureFileName}",
				_ => "无等待点",
			};
		}

		private static void Fail(Exception exception)
		{
			Debug.LogError($"Foundation E2E 当前过程采集失败：{exception}");
			WriteResult(new CaptureResult
			{
				status = "fail",
				scenePath = SceneManager.GetActiveScene().path,
				error = exception.ToString(),
				screenshotDirectory = s_screenshotDirectory,
			});
			CleanupAndExitPlayMode();
		}

		private static void CleanupAndExitPlayMode()
		{
			InvokeSaveSystemMethod("ResetSaveKitConfigurationForTests");
			if (!string.IsNullOrWhiteSpace(s_saveDirectory) && Directory.Exists(s_saveDirectory))
			{
				Directory.Delete(s_saveDirectory, true);
			}

			CleanupTick();
			if (EditorApplication.isPlaying)
			{
				EditorApplication.isPlaying = false;
			}
		}

		private static void RegisterTick()
		{
			EditorApplication.update -= Tick;
			EditorApplication.update += Tick;
		}

		private static void CleanupTick()
		{
			EditorApplication.update -= Tick;
			s_step = CaptureStep.None;
			s_captureRequested = false;
			s_captureFrame = 0;
			s_captureFileName = string.Empty;
			s_deadline = 0d;
			s_startedAction = null;
			s_selectedActionId = default;
			ClearSessionState();
		}

		private static void WriteResult(CaptureResult result)
		{
			if (string.IsNullOrWhiteSpace(s_resultPath))
			{
				return;
			}

			string directory = Path.GetDirectoryName(s_resultPath);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}
			File.WriteAllText(s_resultPath, JsonUtility.ToJson(result, prettyPrint: true));
		}

		private static void SaveState()
		{
			SessionState.SetInt(SessionStepKey, (int)s_step);
			SessionState.SetString(SessionScreenshotPathKey, s_screenshotDirectory ?? string.Empty);
			SessionState.SetString(SessionResultPathKey, s_resultPath ?? string.Empty);
			SessionState.SetString(SessionSaveDirectoryKey, s_saveDirectory ?? string.Empty);
			SessionState.SetFloat(SessionDeadlineKey, (float)s_deadline);
			SessionState.SetInt(SessionCaptureFrameKey, s_captureFrame);
			SessionState.SetBool(SessionCaptureRequestedKey, s_captureRequested);
			SessionState.SetString(SessionCaptureFileNameKey, s_captureFileName ?? string.Empty);
		}

		private static void RestoreState()
		{
			s_step = (CaptureStep)SessionState.GetInt(SessionStepKey, (int)CaptureStep.None);
			s_screenshotDirectory = SessionState.GetString(SessionScreenshotPathKey, string.Empty);
			s_resultPath = SessionState.GetString(SessionResultPathKey, string.Empty);
			s_saveDirectory = SessionState.GetString(SessionSaveDirectoryKey, string.Empty);
			s_deadline = SessionState.GetFloat(SessionDeadlineKey, 0f);
			s_captureFrame = SessionState.GetInt(SessionCaptureFrameKey, 0);
			s_captureRequested = SessionState.GetBool(SessionCaptureRequestedKey, false);
			s_captureFileName = SessionState.GetString(SessionCaptureFileNameKey, string.Empty);
		}

		private static void ClearSessionState()
		{
			SessionState.EraseInt(SessionStepKey);
			SessionState.EraseString(SessionScreenshotPathKey);
			SessionState.EraseString(SessionResultPathKey);
			SessionState.EraseString(SessionSaveDirectoryKey);
			SessionState.EraseFloat(SessionDeadlineKey);
			SessionState.EraseInt(SessionCaptureFrameKey);
			SessionState.EraseBool(SessionCaptureRequestedKey);
			SessionState.EraseString(SessionCaptureFileNameKey);
		}

		private static void DeleteGeneratedEvidence()
		{
			foreach (string fileName in ScreenshotFileNames.Concat(new[] { ContactSheetFileName, ResultFileName }))
			{
				DeleteGeneratedFileAndMeta(Path.Combine(s_screenshotDirectory, fileName));
			}
		}

		private static void DeleteGeneratedFileAndMeta(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
			{
				return;
			}

			if (File.Exists(assetPath))
			{
				File.Delete(assetPath);
			}

			string metaPath = assetPath + ".meta";
			if (File.Exists(metaPath))
			{
				File.Delete(metaPath);
			}
		}

		private static bool HasDirtyLoadedScene()
		{
			for (int index = 0; index < SceneManager.sceneCount; index++)
			{
				Scene scene = SceneManager.GetSceneAt(index);
				if (scene.isDirty)
				{
					return true;
				}
			}

			return false;
		}

		private static void InvokeSaveSystemMethod(string methodName, params object[] parameters)
		{
			MethodInfo method = typeof(SaveSystem).GetMethod(
				methodName,
				BindingFlags.Static | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new MissingMethodException(typeof(SaveSystem).FullName, methodName);
			}

			method.Invoke(null, parameters);
		}

		[Serializable]
		private struct CaptureResult
		{
			public string status;
			public string scenePath;
			public string scenarioId;
			public int confirmedTurnIndex;
			public string actionId;
			public string productContentId;
			public int productCount;
			public string screenshotDirectory;
			public ScreenshotArtifact[] artifacts;
			public string error;
		}

		[Serializable]
		private struct ScreenshotArtifact
		{
			public string path;
			public string fileName;
			public int width;
			public int height;
			public long bytes;
			public string sha256;
		}
	}
}
