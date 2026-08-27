using System;
using System.Collections;
using System.Threading.Tasks;
using GameCore;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreInputSystem = GameCore.InputSystem;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本场景里的暂停输入转接器。它只把正式 Gameplay 输入转成菜单请求，不拥有暂停状态或 UI 生命周期。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioPauseInput : MonoBehaviour
	{
		private bool m_subscribed;

		private IEnumerator Start()
		{
			while (GameManager.StartupState is GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
			{
				yield return null;
			}

			if (GameManager.StartupState != GameManagerStartupState.Ready)
			{
				Debug.LogError(
					$"剧本暂停输入无法启动：GameManager 状态为 {GameManager.StartupState}。\n{GameManager.StartupException}",
					this);
				yield break;
			}

			Subscribe();
		}

		private void OnEnable()
		{
			if (GameManager.StartupState == GameManagerStartupState.Ready)
			{
				Subscribe();
			}
		}

		private void OnDisable()
		{
			Unsubscribe();
		}

		private void Subscribe()
		{
			if (m_subscribed)
			{
				return;
			}

			CoreInputSystem inputSystem = GameManager.InputSystem;
			inputSystem.AddGameplayActionListener(
				EGameplayInputAction.OpenGameMenu,
				EInputActionPhase.Performed,
				OnOpenGameMenu);
			m_subscribed = true;
		}

		private void Unsubscribe()
		{
			if (!m_subscribed)
			{
				return;
			}

			m_subscribed = false;
			if (GameManager.StartupState != GameManagerStartupState.Ready)
			{
				return;
			}

			if (GameManager.Exists() && GameManager.HasSystem<CoreInputSystem>())
			{
				GameManager.InputSystem.RemoveGameplayActionListener(
					EGameplayInputAction.OpenGameMenu,
					EInputActionPhase.Performed,
					OnOpenGameMenu);
			}
		}

		private void OnOpenGameMenu(InputAction.CallbackContext context)
		{
			if (GameManager.InputSystem.IsGameplayActionBlocked(EGameplayInputAction.OpenGameMenu))
			{
				return;
			}
			if (!GameManager.TryGetSystem(out ScenarioDirector director) ||
				!director.HasActiveScenario ||
				director.IsChangingScenario)
			{
				return;
			}

			_ = OpenPauseMenuAsync();
		}

		private async Task OpenPauseMenuAsync()
		{
			try
			{
				await GameManager.UISystem.OpenMenuAsync(EMenu.Pause);
			}
			catch (Exception exception)
			{
				Debug.LogException(new InvalidOperationException("打开剧本暂停菜单失败。", exception), this);
			}
		}
	}
}