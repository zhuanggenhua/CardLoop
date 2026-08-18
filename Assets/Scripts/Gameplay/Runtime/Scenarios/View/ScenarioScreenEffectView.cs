using System;
using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本屏幕效果视图，吸收 StackCraft 的暂停灰阶和跨日暗角反馈。
	/// 该组件只投影现有状态，不保存剧本、时间或菜单真相。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioScreenEffectView : MonoBehaviour
	{
		[Header("屏幕效果组件")]
		[SerializeField]
		[LabelText("后处理 Volume")]
		[Tooltip("承载暂停灰阶和日终暗角的全局 Volume；Profile 必须包含 ColorAdjustments 与 Vignette。")]
		private Volume m_volume;

		[Header("模板反馈参数")]
		[SerializeField]
		[Min(0.01f)]
		[LabelText("暂停灰阶淡入秒数")]
		[Tooltip("进入暂停菜单时把画面转为灰阶所需的真实秒数，对应 StackCraft 暂停灰阶反馈。")]
		private float m_pauseGrayscaleFadeSeconds = 0.3f;

		[SerializeField]
		[Range(0f, 1f)]
		[LabelText("暂停灰阶目标")]
		[Tooltip("暂停菜单打开时的灰阶强度；1 表示完全灰阶。")]
		private float m_pauseGrayscaleTarget = 1f;

		[SerializeField]
		[Min(0.01f)]
		[LabelText("日终暗角淡入秒数")]
		[Tooltip("进入日终等待阶段时增加暗角所需的真实秒数，对应 StackCraft 跨日暗角反馈。")]
		private float m_dayVignetteFadeSeconds = 0.5f;

		[SerializeField]
		[Range(0f, 1f)]
		[LabelText("日终暗角目标")]
		[Tooltip("日终处理期间的 URP Vignette 强度。")]
		private float m_dayVignetteTarget = 0.45f;

		private ColorAdjustments m_colorAdjustments;
		private Vignette m_vignette;
		private float m_grayscaleAmount;
		private float m_vignetteIntensity;

		public float DisplayedGrayscaleAmount => m_grayscaleAmount;

		public float DisplayedVignetteIntensity => m_vignetteIntensity;

		private void Awake()
		{
			ResolveVolumeOverrides();
			ApplyEffects();
		}

		private void OnEnable()
		{
			ResolveVolumeOverrides();
			ApplyEffects();
		}

		private void Update()
		{
			float targetGrayscale = ShouldShowPauseGrayscale() ? m_pauseGrayscaleTarget : 0f;
			float targetVignette = ShouldShowDayVignette() ? m_dayVignetteTarget : 0f;
			float nextGrayscale = MoveToward(m_grayscaleAmount, targetGrayscale, m_pauseGrayscaleFadeSeconds);
			float nextVignette = MoveToward(m_vignetteIntensity, targetVignette, m_dayVignetteFadeSeconds);
			if (Mathf.Approximately(nextGrayscale, m_grayscaleAmount) &&
				Mathf.Approximately(nextVignette, m_vignetteIntensity))
			{
				return;
			}

			m_grayscaleAmount = nextGrayscale;
			m_vignetteIntensity = nextVignette;
			ApplyEffects();
		}

		private bool ShouldShowPauseGrayscale()
		{
			return GameManager.Exists() &&
				GameManager.HasSystem<GameStateSystem>() &&
				GameManager.GameStateSystem.currentState == EGameState.Menu;
		}

		private bool ShouldShowDayVignette()
		{
			if (!GameManager.Exists() ||
				!GameManager.TryGetSystem(out ScenarioDirector director) ||
				!director.HasActiveScenario)
			{
				return false;
			}

			return director.ActiveRun.DayCyclePhase != ScenarioDayCyclePhase.Inactive;
		}

		private void ResolveVolumeOverrides()
		{
			if (m_volume == null)
			{
				throw new InvalidOperationException("剧本屏幕效果缺少后处理 Volume。");
			}

			VolumeProfile profile = m_volume.profile;
			if (profile == null)
			{
				throw new InvalidOperationException("剧本屏幕效果的 Volume 缺少 Profile。");
			}
			if (!profile.TryGet(out m_colorAdjustments) || m_colorAdjustments == null)
			{
				throw new InvalidOperationException("剧本屏幕效果 Profile 缺少 ColorAdjustments。");
			}
			if (!profile.TryGet(out m_vignette) || m_vignette == null)
			{
				throw new InvalidOperationException("剧本屏幕效果 Profile 缺少 Vignette。");
			}
		}

		private void ApplyEffects()
		{
			m_colorAdjustments.saturation.overrideState = true;
			m_colorAdjustments.saturation.value = Mathf.Lerp(0f, -100f, m_grayscaleAmount);
			m_vignette.intensity.overrideState = true;
			m_vignette.intensity.value = m_vignetteIntensity;
		}

		private static float MoveToward(float current, float target, float duration)
		{
			float safeDuration = Mathf.Max(0.01f, duration);
			return Mathf.MoveTowards(current, target, Time.unscaledDeltaTime / safeDuration);
		}
	}
}
