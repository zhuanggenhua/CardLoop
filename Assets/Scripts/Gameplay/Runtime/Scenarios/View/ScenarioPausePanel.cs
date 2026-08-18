using System;
using System.Threading.Tasks;
using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本暂停菜单面板；继续、设置和保存返回标题都通过正式菜单栈与剧本导演完成。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioPausePanel : UIKitMenuPanelBase
	{
		[Header("暂停菜单组件")]
		[LabelText("继续按钮")]
		[Tooltip("关闭暂停菜单并恢复当前剧本。")]
		[SerializeField]
		private Button m_continueButton;

		[LabelText("设置按钮")]
		[Tooltip("通过正式菜单栈打开设置面板，关闭设置后返回暂停菜单。")]
		[SerializeField]
		private Button m_settingsButton;

		[LabelText("保存并退出按钮")]
		[Tooltip("保存当前单局到活动槽位，然后结束剧本并返回进入单局前的场景。")]
		[SerializeField]
		private Button m_saveAndExitButton;

		protected override void OnPanelInit()
		{
			if (m_continueButton == null || m_settingsButton == null || m_saveAndExitButton == null)
			{
				throw new InvalidOperationException("剧本暂停菜单预制体缺少必要按钮引用。");
			}

			m_continueButton.onClick.AddListener(ContinueScenario);
			m_settingsButton.onClick.AddListener(OpenSettings);
			m_saveAndExitButton.onClick.AddListener(SaveAndExit);
		}

		private void OnDestroy()
		{
			m_continueButton?.onClick.RemoveListener(ContinueScenario);
			m_settingsButton?.onClick.RemoveListener(OpenSettings);
			m_saveAndExitButton?.onClick.RemoveListener(SaveAndExit);
		}

		protected override GameObject ResolveDefaultFocusTarget()
		{
			return m_continueButton != null ? m_continueButton.gameObject : base.ResolveDefaultFocusTarget();
		}

		private void ContinueScenario()
		{
			CloseFromMenuStackOrSelf();
		}

		private void OpenSettings()
		{
			RunPanelTaskAndReport(GameManager.UISystem.OpenMenuAsync(EMenu.Settings), "打开设置菜单");
		}

		private void SaveAndExit()
		{
			RunPanelTaskAndReport(SaveAndExitAsync(), "保存并返回标题");
		}

		private async Task SaveAndExitAsync()
		{
			ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
			if (!director.HasActiveScenario)
			{
				throw new InvalidOperationException("当前没有活动剧本，不能保存并返回标题。");
			}
			int slotId = director.ActiveSaveSlotId ??
				throw new InvalidOperationException("当前剧本没有活动存档槽位，不能保存并返回标题。");

			SetControlsInteractable(false);
			try
			{
				if (!director.SaveActiveRunToSlot(slotId))
				{
					throw new InvalidOperationException($"当前单局写入槽位 {slotId} 失败。");
				}

				await director.EndScenarioAsync();
				if (GameManager.Exists() && GameManager.HasSystem<UISystem>())
				{
					GameManager.UISystem.CloseAllMenus();
				}
			}
			finally
			{
				if (this != null && isActiveAndEnabled)
				{
					SetControlsInteractable(true);
				}
			}
		}

		private void SetControlsInteractable(bool interactable)
		{
			m_continueButton.interactable = interactable;
			m_settingsButton.interactable = interactable;
			m_saveAndExitButton.interactable = interactable;
		}
	}
}
