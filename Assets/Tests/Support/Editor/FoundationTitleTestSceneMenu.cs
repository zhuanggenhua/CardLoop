using System;
using System.Linq;
using System.Reflection;
using GameCore;
using Gameplay.Content;
using Gameplay.Scenarios;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gameplay.Tests.Support.Editor
{
	/// <summary>标题入口的地基验收资产生成切片；正式运行代码不依赖这里的测试剧本与临时视觉。</summary>
	public static partial class FoundationTestSceneMenu
	{
		internal const string TitleScenePath = "Assets/Scenes/地基标题测试.unity";
		private const string ScenarioTitlePanelPrefabPath = GameplayUiPrefabFolder + "/ScenarioTitlePanel.prefab";
		private const string SettingsPanelPrefabPath = GameplayUiPrefabFolder + "/UISettings.prefab";

		[MenuItem("Gameplay/地基/重建标题入口测试场景")]
		public static void RebuildTitleTestScene()
		{
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			EnsureTestScenarioAssets();
			EnsureTabletopTestAssets();
			EnsureScenarioTitlePanelPrefab();
			EnsureSettingsPanelPrefab();
			GameObject runtimeRootPrefab = EnsureRuntimeRootPrefab(EnsureConfigAsset());

			GameObject runtimeEntryObject = new("地基标题测试入口");
			SceneManager.MoveGameObjectToScene(runtimeEntryObject, scene);
			FoundationTestRuntimeEntry runtimeEntry = runtimeEntryObject.AddComponent<FoundationTestRuntimeEntry>();
			SerializedObject serializedEntry = new(runtimeEntry);
			SerializedProperty runtimeRootProperty = serializedEntry.FindProperty("m_runtimeRootPrefab") ??
				throw new MissingFieldException(
					typeof(FoundationTestRuntimeEntry).FullName,
					"m_runtimeRootPrefab");
			runtimeRootProperty.objectReferenceValue = runtimeRootPrefab;
			serializedEntry.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(runtimeEntry);

			if (new SerializedObject(runtimeEntry)
				    .FindProperty("m_runtimeRootPrefab")
				    ?.objectReferenceValue != runtimeRootPrefab)
			{
				throw new MissingReferenceException(
					$"标题测试场景没有写入唯一测试进程根预制体：{RuntimeRootPrefabPath}");
			}

			Camera camera = CreateMainCamera(scene);
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.025f, 0.032f, 0.04f, 1f);
			camera.transform.position = new Vector3(0f, 0f, -10f);

			GameObject titleObject = new("ScenarioTitleScreen");
			SceneManager.MoveGameObjectToScene(titleObject, scene);
			ScenarioTitleScreen titleScreen = titleObject.AddComponent<ScenarioTitleScreen>();
			SerializedObject serializedTitle = new(titleScreen);
			serializedTitle.FindProperty("m_defaultScenarioId").FindPropertyRelative("m_value").stringValue =
				FoundationTestSceneHarness.TestScenarioContentId;
			serializedTitle.ApplyModifiedPropertiesWithoutUndo();

			EditorSceneManager.MarkSceneDirty(scene);
			if (!EditorSceneManager.SaveScene(scene, TitleScenePath))
			{
				throw new MissingReferenceException($"无法保存标题入口测试场景：{TitleScenePath}");
			}

			ScenarioTitleScreen savedTitleScreen = VerifySavedTitleSceneConfig(runtimeRootPrefab);
			EnsureTestSceneCollector();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			RefreshEditorSimulateManifest();
			if (!Application.isBatchMode)
			{
				Selection.activeObject = savedTitleScreen;
			}
			Debug.Log($"标题入口测试场景已重建：{TitleScenePath}", savedTitleScreen);
		}

		private static ScenarioTitleScreen VerifySavedTitleSceneConfig(GameObject expectedRuntimeRootPrefab)
		{
			Scene savedScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
			if (savedScene.GetRootGameObjects()
				    .SelectMany(root => root.GetComponentsInChildren<GameManager>(true))
				    .Any())
			{
				throw new System.InvalidOperationException(
					$"标题入口场景不得重复保存进程级 {nameof(GameManager)}：{TitleScenePath}");
			}

			FoundationTestRuntimeEntry entry = savedScene.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<FoundationTestRuntimeEntry>(true))
				.SingleOrDefault();
			SerializedProperty runtimeRootProperty = entry == null
				? null
				: new SerializedObject(entry).FindProperty("m_runtimeRootPrefab");
			if (runtimeRootProperty?.objectReferenceValue != expectedRuntimeRootPrefab)
			{
				throw new MissingReferenceException(
					$"保存后的标题入口测试场景没有引用唯一测试进程根预制体：{TitleScenePath}");
			}

			ScenarioTitleScreen titleScreen = savedScene.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<ScenarioTitleScreen>(true))
				.SingleOrDefault();
			if (titleScreen == null)
			{
				throw new MissingReferenceException(
					$"保存后的标题入口测试场景没有唯一的 {nameof(ScenarioTitleScreen)}：{TitleScenePath}");
			}

			return titleScreen;
		}

		private static void AssignGameConfig(GameManager gameManager, GameConfig config)
		{
			FieldInfo field = typeof(GameManager).GetField(
				ConfigFieldName,
				BindingFlags.Instance | BindingFlags.NonPublic) ??
				throw new MissingFieldException(typeof(GameManager).FullName, ConfigFieldName);
			field.SetValue(gameManager, config);
			EditorUtility.SetDirty(gameManager);
		}

		private static void EnsureScenarioTitlePanelPrefab()
		{
			Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
			if (sprite == null || font == null)
			{
				throw new MissingReferenceException("缺少标题面板所需的 UI 图片或中文字体。");
			}

			GameObject root = new(
				"ScenarioTitlePanel",
				typeof(RectTransform),
				typeof(Image),
				typeof(ScenarioTitlePanel));
			try
			{
				RectTransform rootRect = root.GetComponent<RectTransform>();
				SetStretchRect(rootRect, 0f);
				root.GetComponent<Image>().color = new Color(0.018f, 0.025f, 0.032f, 1f);

				CreateDecorativeCard(rootRect, sprite, new Vector2(0.69f, 0.56f), new Vector2(420f, 590f), 10f,
					new Color(0.11f, 0.29f, 0.26f, 1f));
				CreateDecorativeCard(rootRect, sprite, new Vector2(0.79f, 0.52f), new Vector2(420f, 590f), -7f,
					new Color(0.31f, 0.16f, 0.14f, 1f));

				TextMeshProUGUI title = CreatePanelText("GameTitle", rootRect, font, "卡牌生存：无限", 112f);
				title.fontStyle = FontStyles.Bold;
				title.alignment = TextAlignmentOptions.Left;
				SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
					new Vector2(1200f, 150f), new Vector2(104f, -96f));

				TextMeshProUGUI subtitle = CreatePanelText(
					"Subtitle", rootRect, font, "CARD SURVIVAL · INFINITE WORLDS", 38f);
				subtitle.alignment = TextAlignmentOptions.Left;
				subtitle.color = new Color(0.64f, 0.75f, 0.7f, 1f);
				SetAnchoredRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
					new Vector2(1050f, 58f), new Vector2(108f, -236f));

				Slider dayDuration = CreateTitleDayDurationSlider(rootRect, sprite, font, out TextMeshProUGUI dayDurationLabel);
				Toggle friendlyMode = CreateTitleToggle("FriendlyMode", rootRect, sprite, font, "友好模式：不生成敌对遭遇");
				Button newGame = CreateTitleButton("NewGame", rootRect, sprite, font, "新游戏", 0);
				Button loadGame = CreateTitleButton("LoadGame", rootRect, sprite, font, "读取存档", 1);
				Button settings = CreateTitleButton("Settings", rootRect, sprite, font, "设置", 2);
				Button quit = CreateTitleButton("Quit", rootRect, sprite, font, "退出游戏", 3);

				ScenarioTitlePanel panel = root.GetComponent<ScenarioTitlePanel>();
				SerializedObject serializedPanel = new(panel);
				serializedPanel.FindProperty("m_newGameButton").objectReferenceValue = newGame;
				serializedPanel.FindProperty("m_loadGameButton").objectReferenceValue = loadGame;
				serializedPanel.FindProperty("m_settingsButton").objectReferenceValue = settings;
				serializedPanel.FindProperty("m_quitButton").objectReferenceValue = quit;
				serializedPanel.FindProperty("m_friendlyModeToggle").objectReferenceValue = friendlyMode;
				serializedPanel.FindProperty("m_dayDurationSlider").objectReferenceValue = dayDuration;
				serializedPanel.FindProperty("m_dayDurationLabel").objectReferenceValue = dayDurationLabel;
				serializedPanel.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

				if (PrefabUtility.SaveAsPrefabAsset(root, ScenarioTitlePanelPrefabPath) == null)
				{
					throw new MissingReferenceException($"无法保存标题面板：{ScenarioTitlePanelPrefabPath}");
				}
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(root);
			}
		}

		private static Button CreateTitleButton(
			string name,
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			string text,
			int index)
		{
			Button button = CreateSavePanelButton(
				name, parent, sprite, font, text,
				new Vector2(0f, 1f), new Vector2(0f, 1f),
				new Vector2(620f, 112f), new Vector2(108f, -500f - index * 138f),
				index == 0 ? new Color(0.13f, 0.43f, 0.34f, 1f) : new Color(0.10f, 0.13f, 0.15f, 1f),
				48f);
			button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
			return button;
		}

		private static Slider CreateTitleDayDurationSlider(
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			out TextMeshProUGUI label)
		{
			GameObject root = new("DayDuration", typeof(RectTransform), typeof(Slider), typeof(UINavigationTarget));
			root.transform.SetParent(parent, false);
			RectTransform rootRect = root.GetComponent<RectTransform>();
			SetAnchoredRect(rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
				new Vector2(760f, 104f), new Vector2(108f, -346f));

			label = CreatePanelText("Label", rootRect, font, "日长：120 秒", 32f);
			label.alignment = TextAlignmentOptions.Left;
			label.color = new Color(0.78f, 0.86f, 0.82f, 1f);
			SetAnchoredRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
				new Vector2(360f, 44f), new Vector2(0f, 0f));

			GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
			backgroundObject.transform.SetParent(rootRect, false);
			RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
			SetAnchoredRect(backgroundRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
				new Vector2(-22f, 18f), new Vector2(11f, 16f));
			Image background = backgroundObject.GetComponent<Image>();
			background.sprite = sprite;
			background.type = Image.Type.Sliced;
			background.color = new Color(0.10f, 0.13f, 0.15f, 1f);

			GameObject fillAreaObject = new("Fill Area", typeof(RectTransform));
			fillAreaObject.transform.SetParent(rootRect, false);
			RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
			SetAnchoredRect(fillArea, new Vector2(0f, 0f), new Vector2(1f, 0f),
				new Vector2(-34f, 18f), new Vector2(17f, 16f));

			GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
			fillObject.transform.SetParent(fillArea, false);
			RectTransform fillRect = fillObject.GetComponent<RectTransform>();
			SetStretchRect(fillRect, 0f);
			Image fill = fillObject.GetComponent<Image>();
			fill.sprite = sprite;
			fill.type = Image.Type.Sliced;
			fill.color = new Color(0.32f, 0.78f, 0.58f, 1f);

			GameObject handleAreaObject = new("Handle Slide Area", typeof(RectTransform));
			handleAreaObject.transform.SetParent(rootRect, false);
			RectTransform handleArea = handleAreaObject.GetComponent<RectTransform>();
			SetAnchoredRect(handleArea, new Vector2(0f, 0f), new Vector2(1f, 0f),
				new Vector2(-44f, 48f), new Vector2(22f, 16f));

			GameObject handleObject = new("Handle", typeof(RectTransform), typeof(Image));
			handleObject.transform.SetParent(handleArea, false);
			RectTransform handleRect = handleObject.GetComponent<RectTransform>();
			SetAnchoredRect(handleRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
				new Vector2(52f, 52f), Vector2.zero);
			Image handle = handleObject.GetComponent<Image>();
			handle.sprite = sprite;
			handle.type = Image.Type.Sliced;
			handle.color = new Color(0.87f, 0.95f, 0.89f, 1f);

			Slider slider = root.GetComponent<Slider>();
			slider.minValue = 60f;
			slider.maxValue = 180f;
			slider.wholeNumbers = true;
			slider.value = 120f;
			slider.targetGraphic = handle;
			slider.fillRect = fillRect;
			slider.handleRect = handleRect;
			return slider;
		}

		private static Toggle CreateTitleToggle(
			string name,
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			string text)
		{
			GameObject root = new(name, typeof(RectTransform), typeof(Toggle), typeof(UINavigationTarget));
			root.transform.SetParent(parent, false);
			RectTransform rootRect = root.GetComponent<RectTransform>();
			SetAnchoredRect(rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
				new Vector2(760f, 86f), new Vector2(108f, -438f));

			GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
			backgroundObject.transform.SetParent(rootRect, false);
			RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
			SetAnchoredRect(backgroundRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
				new Vector2(58f, 58f), new Vector2(0f, 0f));
			Image background = backgroundObject.GetComponent<Image>();
			background.sprite = sprite;
			background.type = Image.Type.Sliced;
			background.color = new Color(0.10f, 0.13f, 0.15f, 1f);

			GameObject checkmarkObject = new("Checkmark", typeof(RectTransform), typeof(Image));
			checkmarkObject.transform.SetParent(backgroundRect, false);
			SetStretchRect(checkmarkObject.GetComponent<RectTransform>(), 10f);
			Image checkmark = checkmarkObject.GetComponent<Image>();
			checkmark.sprite = sprite;
			checkmark.type = Image.Type.Sliced;
			checkmark.color = new Color(0.32f, 0.78f, 0.58f, 1f);

			TextMeshProUGUI label = CreatePanelText("Label", rootRect, font, text, 36f);
			label.alignment = TextAlignmentOptions.Left;
			label.color = new Color(0.78f, 0.86f, 0.82f, 1f);
			SetAnchoredRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
				new Vector2(660f, 70f), new Vector2(80f, 0f));

			Toggle toggle = root.GetComponent<Toggle>();
			toggle.targetGraphic = background;
			toggle.graphic = checkmark;
			toggle.isOn = false;
			return toggle;
		}
		private static void CreateDecorativeCard(
			RectTransform parent,
			Sprite sprite,
			Vector2 anchor,
			Vector2 size,
			float rotation,
			Color color)
		{
			GameObject card = new("Card", typeof(RectTransform), typeof(Image));
			card.transform.SetParent(parent, false);
			RectTransform rect = card.GetComponent<RectTransform>();
			SetAnchoredRect(rect, anchor, anchor, size, Vector2.zero);
			rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
			Image image = card.GetComponent<Image>();
			image.sprite = sprite;
			image.type = Image.Type.Sliced;
			image.color = color;
			image.raycastTarget = false;
		}

		private static void EnsureSettingsPanelPrefab()
		{
			Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestPanelFontPath);
			if (sprite == null || font == null)
			{
				throw new MissingReferenceException("缺少设置面板所需的 UI 图片或中文字体。");
			}

			GameObject root = new("UISettings", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(UISettings));
			try
			{
				RectTransform rootRect = root.GetComponent<RectTransform>();
				SetStretchRect(rootRect, 0f);
				root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

				GameObject windowObject = new("SettingsWindow", typeof(RectTransform), typeof(Image));
				windowObject.transform.SetParent(rootRect, false);
				RectTransform window = windowObject.GetComponent<RectTransform>();
				SetAnchoredRect(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
					new Vector2(1760f, 1080f), Vector2.zero);
				windowObject.GetComponent<Image>().color = new Color(0.04f, 0.055f, 0.065f, 1f);

				TextMeshProUGUI title = CreatePanelText("Title", window, font, "设置", 72f);
				SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
					new Vector2(1400f, 100f), new Vector2(0f, -72f));

				Button resolution = CreateSettingsOptionButton(
					"Resolution",
					window,
					sprite,
					font,
					"分辨率 1920x1080",
					300f,
					out TextMeshProUGUI resolutionLabel);
				Button fullscreen = CreateSettingsOptionButton(
					"Fullscreen",
					window,
					sprite,
					font,
					"全屏：开启",
					195f,
					out TextMeshProUGUI fullscreenLabel);
				Button vSync = CreateSettingsOptionButton(
					"VSync",
					window,
					sprite,
					font,
					"垂直同步：开启",
					90f,
					out TextMeshProUGUI vSyncLabel);
				Button frameRate = CreateSettingsOptionButton(
					"FrameRate",
					window,
					sprite,
					font,
					"帧率：无限制",
					-15f,
					out TextMeshProUGUI frameRateLabel);
				Button shadow = CreateSettingsOptionButton(
					"Shadow",
					window,
					sprite,
					font,
					"阴影：高",
					-120f,
					out TextMeshProUGUI shadowLabel);

				UISettingsMasterVolume masterVolume = CreateSettingsMasterVolumeRow(
					window,
					sprite,
					font,
					"主音量",
					-245f);
				UISettingsChannelVolume gameplaySoundVolume = CreateSettingsChannelVolumeRow(
					window,
					sprite,
					font,
					"玩法音效",
					EAudioChannel.GameplaySoundFX,
					-360f);
				UISettingsChannelVolume interfaceSoundVolume = CreateSettingsChannelVolumeRow(
					window,
					sprite,
					font,
					"界面音效",
					EAudioChannel.InterfaceSoundFX,
					-475f);
				UISettingsChannelVolume backgroundMusicVolume = CreateSettingsChannelVolumeRow(
					window,
					sprite,
					font,
					"背景音乐",
					EAudioChannel.BackgroundMusic,
					-590f);

				Button reset = CreateSavePanelButton("ResetSettings", window, sprite, font, "重置设置",
					new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(520f, 116f),
					new Vector2(-310f, 88f), new Color(0.34f, 0.13f, 0.13f, 1f), 48f);
				Button close = CreateSavePanelButton("Close", window, sprite, font, "关闭",
					new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(520f, 116f),
					new Vector2(310f, 88f), new Color(0.13f, 0.36f, 0.33f, 1f), 48f);

				UISettings settings = root.GetComponent<UISettings>();
				SerializedObject serializedSettings = new(settings);
				serializedSettings.FindProperty("m_masterVolume").objectReferenceValue = masterVolume;
				SerializedProperty channelVolumes = serializedSettings.FindProperty("m_channelVolumes");
				channelVolumes.arraySize = 3;
				channelVolumes.GetArrayElementAtIndex(0).objectReferenceValue = gameplaySoundVolume;
				channelVolumes.GetArrayElementAtIndex(1).objectReferenceValue = interfaceSoundVolume;
				channelVolumes.GetArrayElementAtIndex(2).objectReferenceValue = backgroundMusicVolume;
				serializedSettings.FindProperty("m_closeButton").objectReferenceValue = close;
				serializedSettings.FindProperty("m_resolutionButton").objectReferenceValue = resolution;
				serializedSettings.FindProperty("m_resolutionLabel").objectReferenceValue = resolutionLabel;
				serializedSettings.FindProperty("m_fullscreenButton").objectReferenceValue = fullscreen;
				serializedSettings.FindProperty("m_fullscreenLabel").objectReferenceValue = fullscreenLabel;
				serializedSettings.FindProperty("m_vSyncButton").objectReferenceValue = vSync;
				serializedSettings.FindProperty("m_vSyncLabel").objectReferenceValue = vSyncLabel;
				serializedSettings.FindProperty("m_frameRateButton").objectReferenceValue = frameRate;
				serializedSettings.FindProperty("m_frameRateLabel").objectReferenceValue = frameRateLabel;
				serializedSettings.FindProperty("m_shadowButton").objectReferenceValue = shadow;
				serializedSettings.FindProperty("m_shadowLabel").objectReferenceValue = shadowLabel;
				serializedSettings.FindProperty("m_resetSettingsButton").objectReferenceValue = reset;
				serializedSettings.ApplyModifiedPropertiesWithoutUndo();
				SetLayerRecursively(root, UnityUiLayer);

				if (PrefabUtility.SaveAsPrefabAsset(root, SettingsPanelPrefabPath) == null)
				{
					throw new MissingReferenceException($"无法保存设置面板：{SettingsPanelPrefabPath}");
				}
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(root);
			}
		}

		private static Button CreateSettingsOptionButton(
			string name,
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			string text,
			float y,
			out TextMeshProUGUI label)
		{
			Button button = CreateSavePanelButton(
				name,
				parent,
				sprite,
				font,
				text,
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new Vector2(960f, 84f),
				new Vector2(0f, y),
				new Color(0.10f, 0.13f, 0.15f, 1f),
				36f);
			label = button.GetComponentInChildren<TextMeshProUGUI>();
			label.alignment = TextAlignmentOptions.Center;
			return button;
		}

		private static UISettingsMasterVolume CreateSettingsMasterVolumeRow(
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			string labelText,
			float y)
		{
			GameObject rowObject = CreateSettingsVolumeRow(
				"MasterVolume",
				parent,
				sprite,
				font,
				labelText,
				y,
				out TextMeshProUGUI value,
				out Button decrease,
				out Button increase);
			UISettingsMasterVolume masterVolume = rowObject.AddComponent<UISettingsMasterVolume>();
			WriteSettingsVolumeReferences(masterVolume, value, decrease, increase);
			return masterVolume;
		}

		private static UISettingsChannelVolume CreateSettingsChannelVolumeRow(
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			string labelText,
			EAudioChannel channel,
			float y)
		{
			GameObject rowObject = CreateSettingsVolumeRow(
				channel + "Volume",
				parent,
				sprite,
				font,
				labelText,
				y,
				out TextMeshProUGUI value,
				out Button decrease,
				out Button increase);
			UISettingsChannelVolume channelVolume = rowObject.AddComponent<UISettingsChannelVolume>();
			SerializedObject serializedVolume = new(channelVolume);
			serializedVolume.FindProperty("m_audioChannel").enumValueIndex = (int)channel;
			serializedVolume.FindProperty("m_value").objectReferenceValue = value;
			serializedVolume.FindProperty("m_decreaseButton").objectReferenceValue = decrease;
			serializedVolume.FindProperty("m_increaseButton").objectReferenceValue = increase;
			serializedVolume.ApplyModifiedPropertiesWithoutUndo();
			return channelVolume;
		}

		private static GameObject CreateSettingsVolumeRow(
			string name,
			RectTransform parent,
			Sprite sprite,
			TMP_FontAsset font,
			string labelText,
			float y,
			out TextMeshProUGUI value,
			out Button decrease,
			out Button increase)
		{
			GameObject rowObject = new(name, typeof(RectTransform));
			rowObject.transform.SetParent(parent, false);
			RectTransform rowRect = rowObject.GetComponent<RectTransform>();
			SetAnchoredRect(
				rowRect,
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new Vector2(1280f, 96f),
				new Vector2(0f, y));
			TextMeshProUGUI label = CreatePanelText("Label", rowRect, font, labelText, 38f);
			label.alignment = TextAlignmentOptions.Left;
			SetAnchoredRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
				new Vector2(360f, 72f), new Vector2(0f, 0f));
			decrease = CreateSavePanelButton("Decrease", rowRect, sprite, font, "−",
				new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(90f, 78f),
				new Vector2(-430f, 0f), new Color(0.12f, 0.17f, 0.19f, 1f), 38f);
			value = CreatePanelText("Value", rowRect, font, "5 / 10", 38f);
			SetAnchoredRect(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
				new Vector2(250f, 72f), new Vector2(-240f, 0f));
			increase = CreateSavePanelButton("Increase", rowRect, sprite, font, "+",
				new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(90f, 78f),
				new Vector2(-60f, 0f), new Color(0.12f, 0.17f, 0.19f, 1f), 38f);
			return rowObject;
		}

		private static void WriteSettingsVolumeReferences(
			UISettingsVolume volume,
			TextMeshProUGUI value,
			Button decrease,
			Button increase)
		{
			SerializedObject serializedVolume = new(volume);
			serializedVolume.FindProperty("m_value").objectReferenceValue = value;
			serializedVolume.FindProperty("m_decreaseButton").objectReferenceValue = decrease;
			serializedVolume.FindProperty("m_increaseButton").objectReferenceValue = increase;
			serializedVolume.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
