using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gameplay.Tests.Support.Editor
{
	/// <summary>
	/// 通过正式 Gameplay 场景采集 StackCraft 同态当前画面，避开 Unity Test Runner 的 PlayMode 回连缺陷。
	/// </summary>
	[InitializeOnLoad]
	internal static class StackCraftParityCurrentCaptureMenu
	{
		private const string MenuPath = "Gameplay/Automation/Capture StackCraft Parity Current";
		private const string SessionPrefix = "Gameplay.StackCraftParityCurrentCapture.";
		private const string SessionStepKey = SessionPrefix + "Step";
		private const string SessionScreenshotPathKey = SessionPrefix + "ScreenshotPath";
		private const string SessionResultPathKey = SessionPrefix + "ResultPath";
		private const string SessionSaveDirectoryKey = SessionPrefix + "SaveDirectory";
		private const string SessionDeadlineKey = SessionPrefix + "Deadline";
		private const string SessionCaptureFrameKey = SessionPrefix + "CaptureFrame";
		private const string SessionCaptureRequestedKey = SessionPrefix + "CaptureRequested";
		private const string ScreenshotFileName = "stackcraft-parity-current-ready.png";
		private const string ResultFileName = "stackcraft-parity-current-ready.json";
		private const string ExpectedStarterPackArtworkName = "初始卡包";
		private const double PlayModeTimeoutSeconds = 90d;
		private const double ScreenshotTimeoutSeconds = 5d;

		private static CaptureStep s_step;
		private static string s_screenshotPath;
		private static string s_resultPath;
		private static string s_saveDirectory;
		private static double s_deadline;
		private static int s_captureFrame;
		private static bool s_captureRequested;

		static StackCraftParityCurrentCaptureMenu()
		{
			RestoreState();
			EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
			EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

			if (s_step != CaptureStep.None)
			{
				RegisterTick();
				Debug.Log($"恢复 StackCraft 同态当前截图流程，当前步骤：{s_step}。");
			}
		}

		private enum CaptureStep
		{
			None,
			EnteringPlayMode,
			WaitingReady,
			Capturing,
		}

		[MenuItem(MenuPath)]
		private static void Capture()
		{
			if (s_step != CaptureStep.None)
			{
				Debug.LogWarning("StackCraft 同态当前截图流程已经在运行。");
				return;
			}

			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				Debug.LogError("Unity 正在编译或导入，拒绝开始 StackCraft 同态当前截图。");
				return;
			}

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				Debug.LogError("当前已经在切换或运行 PlayMode，拒绝叠加 StackCraft 同态当前截图。");
				return;
			}

			if (HasDirtyLoadedScene())
			{
				Debug.LogError("当前有未保存场景，拒绝自动切换到 StackCraft 同态测试场景。请先保存或处理当前场景。");
				return;
			}

			if (!FoundationTestSceneHarness.TryReadStackCraftReferenceStarterPackPosition(
				out _,
				out string referenceFailure))
			{
				Debug.LogError(referenceFailure);
				return;
			}

			try
			{
				string screenshotDirectory = Path.GetFullPath(
					Path.Combine(Application.dataPath, "..", "Assets", "Screenshots", "StackCraftParity"));
				Directory.CreateDirectory(screenshotDirectory);
				s_screenshotPath = Path.Combine(screenshotDirectory, ScreenshotFileName);
				s_resultPath = Path.Combine(screenshotDirectory, ResultFileName);
				s_saveDirectory = Path.Combine(
					Application.temporaryCachePath,
					"Gameplay-StackCraftParityCurrentCapture",
					Guid.NewGuid().ToString("N"));
				DeleteGeneratedFileAndMeta(s_screenshotPath);
				DeleteGeneratedFileAndMeta(s_resultPath);

				EditorSceneManager.OpenScene(FoundationTestSceneMenu.StackCraftParityScenePath, OpenSceneMode.Single);
				s_step = CaptureStep.EnteringPlayMode;
				s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
				s_captureRequested = false;
				SaveState();
				RegisterTick();
				EditorApplication.isPlaying = true;
				Debug.Log("开始采集当前项目正式 Gameplay 的 StackCraft 同态当前截图。");
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
					Debug.LogWarning($"StackCraft 同态当前截图在 PlayMode 外恢复，清理未完成步骤：{s_step}。");
					CleanupTick();
					return;
				}

				if (EditorApplication.timeSinceStartup > s_deadline)
				{
					throw new TimeoutException(
						$"StackCraft 同态当前截图超时，当前步骤：{s_step}，等待点：{DescribeCurrentWaitPoint()}。");
				}

				switch (s_step)
				{
					case CaptureStep.EnteringPlayMode:
						TickEnteringPlayMode();
						break;
					case CaptureStep.WaitingReady:
						TickWaitingReady();
						break;
					case CaptureStep.Capturing:
						TickCapturing();
						break;
					case CaptureStep.None:
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(s_step), s_step, "未知 StackCraft 同态当前截图步骤。");
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
				SceneManager.GetActiveScene().path != FoundationTestSceneMenu.StackCraftParityScenePath)
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
			if (!TryValidateReadyState(out CaptureResult result, out string pendingReason))
			{
				if (!string.IsNullOrWhiteSpace(pendingReason))
				{
					Debug.Log($"等待 StackCraft 同态当前场景就绪：{pendingReason}");
				}

				return;
			}

			s_captureFrame = Time.frameCount + 2;
			s_captureRequested = false;
			s_step = CaptureStep.Capturing;
			s_deadline = EditorApplication.timeSinceStartup + ScreenshotTimeoutSeconds;
			SaveState();
		}

		private static void TickCapturing()
		{
			if (Time.frameCount < s_captureFrame)
			{
				return;
			}

			if (!s_captureRequested)
			{
				ScreenCapture.CaptureScreenshot(s_screenshotPath);
				s_captureRequested = true;
				SaveState();
				return;
			}

			if (!File.Exists(s_screenshotPath) || new FileInfo(s_screenshotPath).Length == 0)
			{
				return;
			}

			TryValidateReadyState(out CaptureResult result, out _);
			result.status = "pass";
			result.screenshotPath = s_screenshotPath;
			result.screenshotBytes = new FileInfo(s_screenshotPath).Length;
			result.frameCount = Time.frameCount;
			result.captureTimeSeconds = Time.time;
			WriteResult(result);
			CleanupAndExitPlayMode();
			Debug.Log($"StackCraft 同态当前截图已写入：{s_screenshotPath}");
		}

		private static bool TryValidateReadyState(out CaptureResult result, out string pendingReason)
		{
			result = new CaptureResult
			{
				status = "pending",
				scenePath = SceneManager.GetActiveScene().path,
			};
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
				throw new InvalidOperationException("StackCraft 同态场景必须由剧本导演启动，不能恢复模板旧场景 Manager 链路。");
			}

			FoundationTestSceneHarness controller = Object.FindAnyObjectByType<FoundationTestSceneHarness>();
			if (controller == null)
			{
				pendingReason = "FoundationTestSceneHarness 尚未生成。";
				return false;
			}

			if (!controller.IsReady)
			{
				pendingReason = "FoundationTestSceneHarness 尚未完成内容、牌桌、输入和必需 HUD 绑定。";
				return false;
			}

			if (!controller.CardPackId.IsValid)
			{
				throw new InvalidOperationException("StackCraft 同态初始画面没有生成 Starter 卡包实例。");
			}

			if (!controller.Cards.TryGetCard(controller.CardPackId, out TabletopCard pack))
			{
				throw new InvalidOperationException("StackCraft 同态 Starter 卡包 ID 无法在当前牌桌查回。");
			}

			ContentId expectedPackId = new(FoundationTestSceneHarness.TestCardPackContentId);
			if (pack.ContentId != expectedPackId)
			{
				throw new InvalidOperationException($"Starter 卡包内容 ID 错误：{pack.ContentId}，预期 {expectedPackId}。");
			}

			if (pack.RemainingUses != 4)
			{
				throw new InvalidOperationException($"Starter 卡包剩余次数错误：{pack.RemainingUses}。");
			}

			TabletopCard[] allCards = controller.Cards.Stacks
				.SelectMany(stack => stack.Cards)
				.ToArray();
			if (allCards.Length != 1)
			{
				throw new InvalidOperationException($"StackCraft 同态初始画面必须只有 Starter 卡包，当前卡牌数 {allCards.Length}。");
			}

			if (!FoundationTestSceneHarness.TryReadStackCraftReferenceStarterPackPosition(
				out Vector2 referencePackPosition,
				out string referencePositionFailure))
			{
				throw new InvalidOperationException(referencePositionFailure);
			}

			TabletopCardStack packStack = controller.Cards.GetStackContaining(controller.CardPackId);
			if (Mathf.Abs(packStack.Position.x - referencePackPosition.x) > 0.001f ||
				Mathf.Abs(packStack.Position.y - referencePackPosition.y) > 0.001f)
			{
				throw new InvalidOperationException(
					$"Starter 卡包坐标未对齐参考采集结果：当前 {packStack.Position}，参考 {referencePackPosition}。");
			}

			TabletopCardView packView = Object
				.FindObjectsByType<TabletopCardView>(FindObjectsInactive.Exclude)
				.SingleOrDefault(candidate => candidate.CardId == controller.CardPackId);
			if (packView == null)
			{
				pendingReason = "Starter 卡包正式视图尚未生成。";
				return false;
			}

			if (packView.ContentId != expectedPackId)
			{
				throw new InvalidOperationException($"Starter 卡包视图内容 ID 错误：{packView.ContentId}。");
			}

			if (packView.DisplayedTitleText != "初始卡包")
			{
				throw new InvalidOperationException($"Starter 卡包标题错误：{packView.DisplayedTitleText}。");
			}

			if (!packView.DisplaysArtwork || packView.DisplayedArtwork.name != ExpectedStarterPackArtworkName)
			{
				pendingReason =
					$"Starter 卡包插画尚未完成 YooAsset 投影，当前={packView.DisplayedArtwork?.name ?? "(空)"}。";
				return false;
			}

			if (packView.DisplayedSurfaceMaterial == null ||
				packView.DisplayedSurfaceMaterial.name != "卡牌表面_卡包")
			{
				pendingReason = $"Starter 卡包表面材质尚未切换，当前={packView.DisplayedSurfaceMaterial?.name ?? "(空)"}。";
				return false;
			}

			Vector2 expectedSize = new(0.9f, 1.3000002f);
			if (Vector2.Distance(packView.AppliedCardSize, expectedSize) > 0.001f)
			{
				pendingReason = $"Starter 卡包尺寸尚未对齐，当前={packView.AppliedCardSize}。";
				return false;
			}

			BoxCollider collider = packView.GetComponent<BoxCollider>();
			if (collider == null ||
				Mathf.Abs(collider.size.x - expectedSize.x) > 0.001f ||
				Mathf.Abs(collider.size.z - expectedSize.y) > 0.001f ||
				Mathf.Abs(collider.size.y) > 0.001f)
			{
				throw new InvalidOperationException(
					$"Starter 卡包碰撞盒未对齐 StackCraft PackInstance：{collider?.size.ToString() ?? "(缺少碰撞盒)"}。");
			}

			ScenarioDirector scenarioDirector = GameManager.GetSystem<ScenarioDirector>();
			result.scenarioId = scenarioDirector.ActiveScenarioId.Value;
			result.confirmedTurnIndex = scenarioDirector.ActiveRun.ConfirmedTurnIndex;
			result.cardContentId = pack.ContentId.Value;
			result.cardDisplayName = packView.DisplayedTitleText;
			result.remainingUses = pack.RemainingUses;
			result.cardCount = allCards.Length;
			result.artworkName = packView.DisplayedArtwork.name;
			result.surfaceMaterialName = packView.DisplayedSurfaceMaterial.name;
			result.stackPosition = SerializableVector2.From(packStack.Position);
			result.referenceStackPosition = SerializableVector2.From(referencePackPosition);
			result.appliedCardSize = SerializableVector2.From(packView.AppliedCardSize);
			result.colliderSize = SerializableVector3.From(collider.size);
			return true;
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

			Debug.LogWarning($"StackCraft 同态当前截图未完成就退出 PlayMode，清理未完成步骤：{s_step}。");
			CleanupTick();
		}

		private static string DescribeCurrentWaitPoint()
		{
			string activeScenePath = SceneManager.GetActiveScene().path;
			return s_step switch
			{
				CaptureStep.EnteringPlayMode when !EditorApplication.isPlaying => "Unity 尚未进入 PlayMode",
				CaptureStep.EnteringPlayMode when activeScenePath != FoundationTestSceneMenu.StackCraftParityScenePath =>
					$"当前场景不是 StackCraft 同态场景，实际为 {activeScenePath}",
				CaptureStep.EnteringPlayMode => "等待配置测试存档目录",
				CaptureStep.WaitingReady when !TryValidateReadyState(out _, out string pendingReason) =>
					pendingReason ?? "等待同态场景初始化完成",
				CaptureStep.WaitingReady => "等待截图帧",
				CaptureStep.Capturing when string.IsNullOrWhiteSpace(s_screenshotPath) =>
					"截图路径为空",
				CaptureStep.Capturing when !File.Exists(s_screenshotPath) =>
					$"等待截图文件生成：{s_screenshotPath}",
				CaptureStep.Capturing => $"等待截图文件写入非空：{s_screenshotPath}",
				_ => "无等待点",
			};
		}

		private static void Fail(Exception exception)
		{
			Debug.LogError($"StackCraft 同态当前截图失败：{exception}");
			WriteResult(new CaptureResult
			{
				status = "fail",
				scenePath = SceneManager.GetActiveScene().path,
				error = exception.ToString(),
				screenshotPath = s_screenshotPath,
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
			s_deadline = 0d;
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
			SessionState.SetString(SessionScreenshotPathKey, s_screenshotPath ?? string.Empty);
			SessionState.SetString(SessionResultPathKey, s_resultPath ?? string.Empty);
			SessionState.SetString(SessionSaveDirectoryKey, s_saveDirectory ?? string.Empty);
			SessionState.SetFloat(SessionDeadlineKey, (float)s_deadline);
			SessionState.SetInt(SessionCaptureFrameKey, s_captureFrame);
			SessionState.SetBool(SessionCaptureRequestedKey, s_captureRequested);
		}

		private static void RestoreState()
		{
			s_step = (CaptureStep)SessionState.GetInt(SessionStepKey, (int)CaptureStep.None);
			s_screenshotPath = SessionState.GetString(SessionScreenshotPathKey, string.Empty);
			s_resultPath = SessionState.GetString(SessionResultPathKey, string.Empty);
			s_saveDirectory = SessionState.GetString(SessionSaveDirectoryKey, string.Empty);
			s_deadline = SessionState.GetFloat(SessionDeadlineKey, 0f);
			s_captureFrame = SessionState.GetInt(SessionCaptureFrameKey, 0);
			s_captureRequested = SessionState.GetBool(SessionCaptureRequestedKey, false);
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
			public string cardContentId;
			public string cardDisplayName;
			public int remainingUses;
			public int cardCount;
			public string artworkName;
			public string surfaceMaterialName;
			public SerializableVector2 stackPosition;
			public SerializableVector2 referenceStackPosition;
			public SerializableVector2 appliedCardSize;
			public SerializableVector3 colliderSize;
			public string screenshotPath;
			public long screenshotBytes;
			public int frameCount;
			public float captureTimeSeconds;
			public string error;
		}

		[Serializable]
		private struct SerializableVector2
		{
			public float x;
			public float y;

			public static SerializableVector2 From(Vector2 value)
			{
				return new SerializableVector2
				{
					x = value.x,
					y = value.y,
				};
			}
		}

		[Serializable]
		private struct SerializableVector3
		{
			public float x;
			public float y;
			public float z;

			public static SerializableVector3 From(Vector3 value)
			{
				return new SerializableVector3
				{
					x = value.x,
					y = value.y,
					z = value.z,
				};
			}
		}
	}
}
