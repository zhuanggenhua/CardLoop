using System;
using Cysharp.Threading.Tasks;
using GameCore;
using Gameplay.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Scenarios
{
	/// <summary>标题面板一次打开所需的正式剧本入口与默认剧本身份。</summary>
	public sealed class ScenarioTitlePanelData : IUIData
	{
		public ScenarioDirector Director { get; }
		public ContentId DefaultScenarioId { get; }

		public ScenarioTitlePanelData(ScenarioDirector director, ContentId defaultScenarioId)
		{
			Director = director ?? throw new ArgumentNullException(nameof(director));
			if (!defaultScenarioId.IsValid)
			{
				throw new ArgumentException("标题面板的默认剧本内容 ID 无效。", nameof(defaultScenarioId));
			}

			DefaultScenarioId = defaultScenarioId;
		}
	}

	/// <summary>
	/// 标题页玩家命令面板。它不保存单局、设置或存档状态，
	/// 只把新局、读档、设置和退出请求交给已有正式职责入口。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioTitlePanel : UIPanel
	{
		[Header("标题命令")]
		[SerializeField, Tooltip("使用标题场景配置的默认剧本开始新单局。")]
		private Button m_newGameButton;

		[SerializeField, Tooltip("打开现有动态存档列表，并由剧本导演恢复所选单局。")]
		private Button m_loadGameButton;

		[SerializeField, Tooltip("打开 GameCore 已有的音量设置面板。")]
		private Button m_settingsButton;

		[SerializeField, Tooltip("打开确认对话框；确认后退出应用。")]
		private Button m_quitButton;

		[SerializeField, Tooltip("开启后，新局会跳过带有敌对阵营标签的日终遭遇。为空时按关闭处理。")]
		private Toggle m_friendlyModeToggle;

		[SerializeField, Tooltip("玩家开局选择的一整天持续秒数；为空时使用剧本作者源默认时间。")]
		private Slider m_dayDurationSlider;

		[SerializeField, Tooltip("显示当前开局日长秒数；只读取滑条，不保存第二份时间状态。")]
		private TextMeshProUGUI m_dayDurationLabel;

		private ScenarioDirector m_director;
		private ContentId m_defaultScenarioId;
		private bool m_operationInProgress;

		public ContentId DefaultScenarioId => m_defaultScenarioId;

		protected override void OnInit(IUIData data = null)
		{
			if (m_newGameButton == null || m_loadGameButton == null ||
				m_settingsButton == null || m_quitButton == null)
			{
				throw new InvalidOperationException("标题面板预制体缺少必要按钮引用。");
			}

			m_newGameButton.onClick.AddListener(StartNewGame);
			m_loadGameButton.onClick.AddListener(OpenLoadPanel);
			m_settingsButton.onClick.AddListener(OpenSettings);
			m_quitButton.onClick.AddListener(ConfirmQuit);
			if (m_dayDurationSlider != null)
			{
				m_dayDurationSlider.onValueChanged.AddListener(UpdateDayDurationLabel);
				UpdateDayDurationLabel(m_dayDurationSlider.value);
			}
		}

		protected override void OnOpen(IUIData data = null)
		{
			if (m_director != null)
			{
				throw new InvalidOperationException("标题面板尚未关闭，不能覆盖上一打开请求。");
			}
			if (data is not ScenarioTitlePanelData panelData)
			{
				throw new ArgumentException("标题面板必须使用 ScenarioTitlePanelData 打开。", nameof(data));
			}

			m_director = panelData.Director;
			m_defaultScenarioId = panelData.DefaultScenarioId;
			SetCommandsInteractable(true);
		}

		protected override void OnClose()
		{
			m_director = null;
			m_defaultScenarioId = default;
			m_operationInProgress = false;
		}

		protected override void ClearUIComponents()
		{
			m_newGameButton?.onClick.RemoveListener(StartNewGame);
			m_loadGameButton?.onClick.RemoveListener(OpenLoadPanel);
			m_settingsButton?.onClick.RemoveListener(OpenSettings);
			m_quitButton?.onClick.RemoveListener(ConfirmQuit);
			if (m_dayDurationSlider != null)
			{
				m_dayDurationSlider.onValueChanged.RemoveListener(UpdateDayDurationLabel);
			}
			m_director = null;
		}

		private void StartNewGame()
		{
			RequireIdle();
			m_operationInProgress = true;
			SetCommandsInteractable(false);
			StartNewGameAsync().Forget(Debug.LogException);
		}

		private async UniTask StartNewGameAsync()
		{
			try
			{
				await RequireDirector().StartScenarioAsync(m_defaultScenarioId, CreateStartOptions());
				CloseSelf();
			}
			finally
			{
				m_operationInProgress = false;
				if (m_director != null)
				{
					SetCommandsInteractable(true);
				}
			}
		}

		private ScenarioStartOptions CreateStartOptions()
		{
			float? dayDurationSeconds = m_dayDurationSlider == null
				? null
				: Mathf.Max(0.001f, m_dayDurationSlider.value);
			return new ScenarioStartOptions(
				m_friendlyModeToggle != null && m_friendlyModeToggle.isOn,
				dayDurationSeconds);
		}

		private void UpdateDayDurationLabel(float value)
		{
			if (m_dayDurationLabel != null)
			{
				m_dayDurationLabel.text = $"日长：{Mathf.RoundToInt(value)} 秒";
			}
		}

		private void OpenLoadPanel()
		{
			RequireIdle();
			UIKit.OpenPanelAsync<ScenarioSavePanel>(
				callback: panel =>
				{
					if (panel == null)
					{
						throw new InvalidOperationException("UIKit 没有加载剧本存档窗口。");
					}
				},
				level: UILevel.Pop,
				data: new ScenarioSavePanelData(RequireDirector(), ScenarioSavePanelMode.Load));
		}

		private void OpenSettings()
		{
			RequireIdle();
			UIKit.OpenPanelAsync<UISettings>(
				callback: panel =>
				{
					if (panel == null)
					{
						throw new InvalidOperationException("UIKit 没有加载游戏设置窗口。");
					}
				},
				level: UILevel.Pop);
		}

		private void ConfirmQuit()
		{
			RequireIdle();
			DialogConfig config = DialogConfig.Confirm(
				"确定退出游戏吗？当前未保存的进度将会丢失。",
				"退出游戏");
			config.OKText = "退出";
			UIKit.ShowDialog<ConfirmationDialogPanel>(
				config,
				result =>
				{
					if (result.IsConfirmed)
					{
						Application.Quit();
					}
				});
		}

		private ScenarioDirector RequireDirector()
		{
			return m_director ??
				throw new InvalidOperationException("标题面板没有活动的剧本导演。");
		}

		private void RequireIdle()
		{
			RequireDirector();
			if (m_operationInProgress)
			{
				throw new InvalidOperationException("标题面板正在执行新局操作，不能同时提交另一命令。");
			}
		}

		private void SetCommandsInteractable(bool interactable)
		{
			m_newGameButton.interactable = interactable;
			m_loadGameButton.interactable = interactable;
			m_settingsButton.interactable = interactable;
			m_quitButton.interactable = interactable;
		}
	}
}
