using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.Editor.Automation
{
	/// <summary>
	/// 只用于采集 StackCraft 参考模板的干净开局画面，不参与正式 Gameplay 链路。
	/// </summary>
	[InitializeOnLoad]
	internal static class StackCraftReferenceCaptureMenu
	{
		private const string MenuPath = "Gameplay/Automation/Capture StackCraft Reference Main";
		private const string SessionPrefix = "Gameplay.StackCraftReferenceCapture.";
		private const string SessionStepKey = SessionPrefix + "Step";
		private const string SessionScreenshotPathKey = SessionPrefix + "ScreenshotPath";
		private const string SessionDeadlineKey = SessionPrefix + "Deadline";
		private const string SessionCaptureFrameKey = SessionPrefix + "CaptureFrame";
		private const string SessionCaptureRequestedKey = SessionPrefix + "CaptureRequested";
		private const string StackCraftTitleScenePath = "Assets/StackCraft/Scenes/Title.unity";
		private const string StackCraftMainScenePath = "Assets/StackCraft/Scenes/Main.unity";
		private const string GameDirectorTypeName = "CryingSnow.StackCraft.GameDirector";
		private const string GameplayPrefsTypeName = "CryingSnow.StackCraft.GameplayPrefs";
		private const string GameDataTypeName = "CryingSnow.StackCraft.GameData";
		private const string CardManagerTypeName = "CryingSnow.StackCraft.CardManager";
		private const string TimeManagerTypeName = "CryingSnow.StackCraft.TimeManager";
		private const string PackInstanceTypeName = "CryingSnow.StackCraft.PackInstance";
		private const string ReferenceScreenshotFileName = "stackcraft-main-reference-clean.png";
		private const string ReferenceMetadataFileName = "stackcraft-main-reference-clean.json";
		private const double PlayModeTimeoutSeconds = 90d;
		private const double ScreenshotTimeoutSeconds = 5d;

		private static CaptureStep s_step;
		private static AsyncOperation s_sceneLoadOperation;
		private static string s_screenshotPath;
		private static double s_deadline;
		private static int s_captureFrame;
		private static bool s_captureRequested;

		static StackCraftReferenceCaptureMenu()
		{
			RestoreState();
			EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
			EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

			if (s_step != CaptureStep.None)
			{
				RegisterTick();
				Debug.Log($"恢复 StackCraft 参考截图采集流程，当前步骤：{s_step}。");
			}
		}

		private enum CaptureStep
		{
			None,
			EnteringPlayMode,
			LoadingMain,
			WaitingMainReady,
			Capturing,
		}

		[MenuItem(MenuPath)]
		private static void Capture()
		{
			if (s_step != CaptureStep.None)
			{
				Debug.LogWarning("StackCraft 参考截图采集已经在运行。");
				return;
			}

			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				Debug.LogError("Unity 正在编译或导入，拒绝开始 StackCraft 参考截图采集。");
				return;
			}

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				Debug.LogError("当前已经在切换或运行 PlayMode，拒绝叠加 StackCraft 参考截图采集。");
				return;
			}

			if (HasDirtyLoadedScene())
			{
				Debug.LogError("当前有未保存场景，拒绝自动切换到 StackCraft 参考场景。请先保存或处理当前场景。");
				return;
			}

			try
			{
				string screenshotDirectory = Path.GetFullPath(
					Path.Combine(Application.dataPath, "..", "Assets", "Screenshots", "StackCraftReference"));
				Directory.CreateDirectory(screenshotDirectory);
				s_screenshotPath = Path.Combine(screenshotDirectory, ReferenceScreenshotFileName);
				string metadataPath = Path.Combine(screenshotDirectory, ReferenceMetadataFileName);
				DeleteGeneratedFileAndMeta(s_screenshotPath);
				DeleteGeneratedFileAndMeta(metadataPath);

				EditorSceneManager.OpenScene(StackCraftTitleScenePath, OpenSceneMode.Single);
				s_step = CaptureStep.EnteringPlayMode;
				s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
				SaveState();
				RegisterTick();
				EditorApplication.isPlaying = true;
				Debug.Log("开始采集 StackCraft 干净 Main 开局参考截图。");
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
					Debug.LogWarning($"StackCraft 参考截图采集在 PlayMode 外恢复，清理未完成步骤：{s_step}。");
					CleanupTick();
					return;
				}

				if (EditorApplication.timeSinceStartup > s_deadline)
				{
					throw new TimeoutException(
						$"StackCraft 参考截图采集超时，当前步骤：{s_step}，等待点：{DescribeCurrentWaitPoint()}。");
				}

				switch (s_step)
				{
					case CaptureStep.EnteringPlayMode:
						TickEnteringPlayMode();
						break;
					case CaptureStep.LoadingMain:
						TickLoadingMain();
						break;
					case CaptureStep.WaitingMainReady:
						TickWaitingMainReady();
						break;
					case CaptureStep.Capturing:
						TickCapturing();
						break;
					case CaptureStep.None:
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(s_step), s_step, "未知 StackCraft 截图采集步骤。");
				}
			}
			catch (Exception exception)
			{
				Fail(exception);
			}
		}

		private static void RegisterTick()
		{
			EditorApplication.update -= Tick;
			EditorApplication.update += Tick;
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

			Debug.LogWarning($"StackCraft 参考截图采集未完成就退出 PlayMode，清理未完成步骤：{s_step}。");
			CleanupTick();
		}

		private static void TickEnteringPlayMode()
		{
			if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != StackCraftTitleScenePath)
			{
				return;
			}

			MonoBehaviour director = FindBehaviour(GameDirectorTypeName);
			if (director == null)
			{
				return;
			}

			SetCleanStackCraftGameData(director);
			s_sceneLoadOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(
				StackCraftMainScenePath,
				new LoadSceneParameters(LoadSceneMode.Single));
			if (s_sceneLoadOperation == null)
			{
				throw new InvalidOperationException($"无法按路径加载 StackCraft Main 场景：{StackCraftMainScenePath}");
			}

			s_step = CaptureStep.LoadingMain;
			s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
			SaveState();
		}

		private static string DescribeCurrentWaitPoint()
		{
			string activeScenePath = SceneManager.GetActiveScene().path;
			return s_step switch
			{
				CaptureStep.EnteringPlayMode when !EditorApplication.isPlaying => "Unity 尚未进入 PlayMode",
				CaptureStep.EnteringPlayMode when activeScenePath != StackCraftTitleScenePath =>
					$"当前场景不是 StackCraft Title，实际为 {activeScenePath}",
				CaptureStep.EnteringPlayMode when FindBehaviour(GameDirectorTypeName) == null =>
					"StackCraft GameDirector 尚未生成",
				CaptureStep.EnteringPlayMode => "等待注入干净 GameData 并加载 Main",
				CaptureStep.LoadingMain when activeScenePath != StackCraftMainScenePath =>
					$"等待 StackCraft Main 场景加载完成，当前场景为 {activeScenePath}",
				CaptureStep.LoadingMain => "等待 Main 场景加载操作完成",
				CaptureStep.WaitingMainReady when !IsReferenceMainReady(out string pendingReason) =>
					pendingReason ?? "等待 Main 初始化完成",
				CaptureStep.WaitingMainReady => "等待截图帧",
				CaptureStep.Capturing when string.IsNullOrWhiteSpace(s_screenshotPath) =>
					"截图路径为空",
				CaptureStep.Capturing when !File.Exists(s_screenshotPath) =>
					$"等待截图文件生成：{s_screenshotPath}",
				CaptureStep.Capturing => $"等待截图文件写入非空：{s_screenshotPath}",
				_ => "无等待点",
			};
		}

		private static void TickLoadingMain()
		{
			if (s_sceneLoadOperation != null && !s_sceneLoadOperation.isDone)
			{
				return;
			}

			if (SceneManager.GetActiveScene().path != StackCraftMainScenePath)
			{
				if (s_sceneLoadOperation == null)
				{
					throw new InvalidOperationException("StackCraft Main 加载操作在脚本域重载后丢失，且当前未进入 Main 场景。");
				}

				return;
			}

			s_step = CaptureStep.WaitingMainReady;
			s_deadline = EditorApplication.timeSinceStartup + PlayModeTimeoutSeconds;
			SaveState();
		}

		private static void TickWaitingMainReady()
		{
			if (!IsReferenceMainReady(out string pendingReason))
			{
				if (!string.IsNullOrWhiteSpace(pendingReason))
				{
					Debug.Log($"等待 StackCraft 参考 Main 初始化：{pendingReason}");
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

			Complete();
		}

		private static bool IsReferenceMainReady(out string pendingReason)
		{
			pendingReason = null;
			if (FindBehaviour(CardManagerTypeName) == null)
			{
				pendingReason = "CardManager 尚未生成。";
				return false;
			}

			MonoBehaviour timeManager = FindBehaviour(TimeManagerTypeName);
			if (timeManager == null)
			{
				pendingReason = "TimeManager 尚未生成。";
				return false;
			}

			MonoBehaviour[] packs = FindBehaviours(PackInstanceTypeName);
			if (packs.Length == 0)
			{
				pendingReason = "Starter 卡包尚未生成。";
				return false;
			}

			if (packs.Length != 1)
			{
				throw new InvalidOperationException($"StackCraft 干净 Main 开局必须只生成一个 Starter 卡包，当前 {packs.Length} 个。");
			}

			string displayName = ReadPackDisplayName(packs[0]);
			if (!string.Equals(displayName, "Starter", StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"StackCraft 干净 Main 开局卡包显示名错误：{displayName}");
			}

			int usesLeft = ReadIntProperty(packs[0], "UsesLeft");
			if (usesLeft != 4)
			{
				throw new InvalidOperationException($"StackCraft Starter 卡包剩余次数错误：{usesLeft}");
			}

			int currentDay = ReadIntProperty(timeManager, "CurrentDay");
			float normalizedTime = ReadFloatProperty(timeManager, "NormalizedTime");
			if (currentDay != 1 || normalizedTime >= 0.15f)
			{
				throw new InvalidOperationException($"StackCraft Main 不是干净初始时间：Day={currentDay}, NormalizedTime={normalizedTime}");
			}

			if (TryFindTextContaining("End of Day", out string offendingText))
			{
				throw new InvalidOperationException($"StackCraft 参考初始帧仍被日终面板污染：{offendingText}");
			}

			return true;
		}

		private static void Complete()
		{
			WriteReferenceMetadata();
			ClearStackCraftGameData();
			CleanupTick();
			Debug.Log($"StackCraft 干净 Main 开局参考截图已写入：{s_screenshotPath}");
			EditorApplication.isPlaying = false;
		}

		private static void WriteReferenceMetadata()
		{
			MonoBehaviour[] packs = FindBehaviours(PackInstanceTypeName);
			if (packs.Length != 1)
			{
				throw new InvalidOperationException($"无法写入 StackCraft 参考元数据，Starter 卡包数量为 {packs.Length}。");
			}

			MonoBehaviour pack = packs[0];
			Vector3 targetPosition = pack.transform.position;
			object stack = ReadProperty(pack, "Stack");
			if (stack != null)
			{
				targetPosition = (Vector3)ReadProperty(stack, "TargetPosition");
			}

			string directory = Path.GetDirectoryName(s_screenshotPath);
			if (string.IsNullOrWhiteSpace(directory))
			{
				throw new InvalidOperationException("StackCraft 参考截图路径缺少目录，无法写入元数据。");
			}

			ReferenceCaptureMetadata metadata = new()
			{
				scenePath = SceneManager.GetActiveScene().path,
				screenshotPath = s_screenshotPath,
				packDisplayName = ReadPackDisplayName(pack),
				usesLeft = ReadIntProperty(pack, "UsesLeft"),
				worldPosition = SerializableVector3.From(pack.transform.position),
				localPosition = SerializableVector3.From(pack.transform.localPosition),
				stackTargetPosition = SerializableVector3.From(targetPosition),
				frameCount = Time.frameCount,
				captureTimeSeconds = Time.time,
			};
			string metadataPath = Path.Combine(directory, ReferenceMetadataFileName);
			File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, prettyPrint: true));
			Debug.Log($"StackCraft 干净 Main 开局参考元数据已写入：{metadataPath}");
		}

		private static void Fail(Exception exception)
		{
			Debug.LogError($"StackCraft 参考截图采集失败：{exception}");
			ClearStackCraftGameData();
			CleanupTick();
			if (EditorApplication.isPlaying)
			{
				EditorApplication.isPlaying = false;
			}
		}

		private static void CleanupTick()
		{
			EditorApplication.update -= Tick;
			s_step = CaptureStep.None;
			s_sceneLoadOperation = null;
			s_captureRequested = false;
			s_captureFrame = 0;
			s_deadline = 0d;
			ClearSessionState();
		}

		private static void SaveState()
		{
			SessionState.SetInt(SessionStepKey, (int)s_step);
			SessionState.SetString(SessionScreenshotPathKey, s_screenshotPath ?? string.Empty);
			SessionState.SetFloat(SessionDeadlineKey, (float)s_deadline);
			SessionState.SetInt(SessionCaptureFrameKey, s_captureFrame);
			SessionState.SetBool(SessionCaptureRequestedKey, s_captureRequested);
		}

		private static void RestoreState()
		{
			s_step = (CaptureStep)SessionState.GetInt(SessionStepKey, (int)CaptureStep.None);
			s_screenshotPath = SessionState.GetString(SessionScreenshotPathKey, string.Empty);
			s_deadline = SessionState.GetFloat(SessionDeadlineKey, 0f);
			s_captureFrame = SessionState.GetInt(SessionCaptureFrameKey, 0);
			s_captureRequested = SessionState.GetBool(SessionCaptureRequestedKey, false);
		}

		private static void ClearSessionState()
		{
			SessionState.EraseInt(SessionStepKey);
			SessionState.EraseString(SessionScreenshotPathKey);
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

		private static MonoBehaviour FindBehaviour(string fullTypeName)
		{
			return FindBehaviours(fullTypeName).FirstOrDefault();
		}

		private static MonoBehaviour[] FindBehaviours(string fullTypeName)
		{
			return UnityEngine.Object
				.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
				.Where(candidate => candidate != null && candidate.GetType().FullName == fullTypeName)
				.ToArray();
		}

		private static void SetCleanStackCraftGameData(MonoBehaviour director)
		{
			Type prefsType = RequireType(GameplayPrefsTypeName);
			Type gameDataType = RequireType(GameDataTypeName);
			object prefs = Activator.CreateInstance(prefsType, 120, true);
			object gameData = Activator.CreateInstance(gameDataType, 9001, prefs);
			SetPropertyValue(director, "GameData", gameData);
		}

		private static void ClearStackCraftGameData()
		{
			MonoBehaviour director = FindBehaviour(GameDirectorTypeName);
			if (director != null)
			{
				SetPropertyValue(director, "GameData", null);
			}
		}

		private static Type RequireType(string fullTypeName)
		{
			Type type = AppDomain.CurrentDomain
				.GetAssemblies()
				.Select(assembly => assembly.GetType(fullTypeName, throwOnError: false))
				.FirstOrDefault(candidate => candidate != null);
			if (type == null)
			{
				throw new InvalidOperationException($"当前 AppDomain 没有加载参考类型：{fullTypeName}");
			}

			return type;
		}

		private static void SetPropertyValue(object target, string propertyName, object value)
		{
			PropertyInfo property = FindInstanceProperty(target.GetType(), propertyName);
			MethodInfo setter = property?.GetSetMethod(nonPublic: true);
			if (setter == null)
			{
				throw new InvalidOperationException($"{target.GetType().FullName}.{propertyName} 缺少可反射调用的 setter。");
			}

			setter.Invoke(target, new[] { value });
		}

		private static string ReadPackDisplayName(MonoBehaviour pack)
		{
			object definition = ReadProperty(pack, "Definition");
			if (definition == null)
			{
				throw new InvalidOperationException("StackCraft Starter 卡包缺少定义对象。");
			}

			return (string)ReadProperty(definition, "DisplayName");
		}

		private static int ReadIntProperty(object target, string propertyName)
		{
			return Convert.ToInt32(ReadProperty(target, propertyName));
		}

		private static float ReadFloatProperty(object target, string propertyName)
		{
			return Convert.ToSingle(ReadProperty(target, propertyName));
		}

		private static object ReadProperty(object target, string propertyName)
		{
			PropertyInfo property = FindInstanceProperty(target.GetType(), propertyName);
			if (property == null)
			{
				throw new InvalidOperationException($"{target.GetType().FullName} 缺少属性 {propertyName}。");
			}

			return property.GetValue(target);
		}

		private static PropertyInfo FindInstanceProperty(Type type, string propertyName)
		{
			for (Type current = type; current != null; current = current.BaseType)
			{
				PropertyInfo property = current.GetProperty(
					propertyName,
					BindingFlags.Instance |
					BindingFlags.Public |
					BindingFlags.NonPublic |
					BindingFlags.DeclaredOnly);
				if (property != null)
				{
					return property;
				}
			}

			return null;
		}

		private static bool TryFindTextContaining(string text, out string foundText)
		{
			foreach (Component component in UnityEngine.Object.FindObjectsByType<Component>(
				FindObjectsInactive.Include))
			{
				if (component == null || !IsTypeOrBase(component.GetType(), "TMPro.TMP_Text"))
				{
					continue;
				}

				object value = ReadProperty(component, "text");
				string componentText = value as string;
				if (!string.IsNullOrWhiteSpace(componentText) &&
					componentText.Contains(text, StringComparison.OrdinalIgnoreCase))
				{
					foundText = componentText;
					return true;
				}
			}

			foundText = null;
			return false;
		}

		private static bool IsTypeOrBase(Type type, string fullTypeName)
		{
			for (Type current = type; current != null; current = current.BaseType)
			{
				if (current.FullName == fullTypeName)
				{
					return true;
				}
			}

			return false;
		}

		[Serializable]
		private struct ReferenceCaptureMetadata
		{
			public string scenePath;
			public string screenshotPath;
			public string packDisplayName;
			public int usesLeft;
			public SerializableVector3 worldPosition;
			public SerializableVector3 localPosition;
			public SerializableVector3 stackTargetPosition;
			public int frameCount;
			public float captureTimeSeconds;
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
