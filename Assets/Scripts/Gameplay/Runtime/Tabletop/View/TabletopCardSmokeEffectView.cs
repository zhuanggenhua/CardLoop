using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌卡牌烟雾粒子的纯表现组件；播放结束后只隐藏自身，由 TabletopView 释放资源句柄。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TabletopCardSmokeEffectView : MonoBehaviour
	{
		[SerializeField]
		[LabelText("粒子系统")]
		[Tooltip("用于播放对标参考模板的卡牌烟雾反馈 ParticleSystem；必须关闭循环。")]
		private ParticleSystem m_particleSystem;

		[SerializeField]
		[LabelText("粒子渲染器")]
		[Tooltip("卡牌烟雾粒子的 ParticleSystemRenderer；排序层级由牌桌视图设置统一写入。")]
		private ParticleSystemRenderer m_renderer;

		private float m_elapsedSeconds;
		private float m_durationSeconds;
		private bool m_isPlaying;

		/// <summary>当前卡牌烟雾粒子是否仍在播放。</summary>
		public bool IsPlaying => m_isPlaying;

		internal void Play(Vector2 tablePosition, int sortingOrder)
		{
			if (m_particleSystem == null)
			{
				throw new InvalidOperationException("卡牌烟雾粒子视图缺少 ParticleSystem。");
			}
			if (m_renderer == null)
			{
				throw new InvalidOperationException("卡牌烟雾粒子视图缺少 ParticleSystemRenderer。");
			}
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new ArgumentException("卡牌烟雾粒子位置必须是有限牌桌坐标。", nameof(tablePosition));
			}

			ParticleSystem.MainModule main = m_particleSystem.main;
			ParticleSystem.MinMaxCurve startLifetime = main.startLifetime;
			m_durationSeconds = Mathf.Max(0.01f, main.duration + startLifetime.constantMax);
			m_elapsedSeconds = 0f;
			transform.localPosition = TabletopCoordinateSpace.ToLocalPosition(tablePosition);
			m_renderer.sortingOrder = sortingOrder;
			gameObject.SetActive(true);
			m_particleSystem.Clear(withChildren: true);
			m_particleSystem.Play(withChildren: true);
			m_isPlaying = true;
		}

		private void Update()
		{
			if (!m_isPlaying)
			{
				return;
			}

			m_elapsedSeconds += Time.unscaledDeltaTime;
			if (m_elapsedSeconds >= m_durationSeconds)
			{
				m_isPlaying = false;
				gameObject.SetActive(false);
			}
		}
	}
}
