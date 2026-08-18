using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌自动战斗投射物的纯表现组件；移动完成只隐藏自身，不提交伤害或规则。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TabletopProjectileView : MonoBehaviour
	{
		[SerializeField]
		[LabelText("渲染器")]
		[Tooltip("投射物使用的 SpriteRenderer；排序层级由牌桌视图设置。")]
		private SpriteRenderer m_renderer;

		[SerializeField]
		[LabelText("箭矢图片")]
		[Tooltip("远程攻击投射物图片，对应 StackCraft Projectile_Arrow 玩家反馈。")]
		private Sprite m_rangedSprite;

		[SerializeField]
		[LabelText("魔法图片")]
		[Tooltip("魔法攻击投射物图片，对应 StackCraft Projectile_Magic 玩家反馈。")]
		private Sprite m_magicSprite;

		private Vector3 m_start;
		private Vector3 m_end;
		private float m_durationSeconds;
		private float m_elapsedSeconds;
		private bool m_isPlaying;

		/// <summary>当前投射物是否仍在播放飞行表现。</summary>
		public bool IsPlaying => m_isPlaying;

		internal void Play(
			Vector3 start,
			Vector3 end,
			float durationSeconds,
			int sortingOrder,
			int presentationTagCode)
		{
			if (m_renderer == null)
			{
				throw new InvalidOperationException("投射物视图缺少 SpriteRenderer。");
			}
			if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(durationSeconds),
					durationSeconds,
					"投射物飞行秒数必须是有限正数。");
			}

			m_renderer.sprite = ResolveProjectileSprite(presentationTagCode);
			m_start = start;
			m_end = end;
			m_durationSeconds = durationSeconds;
			m_elapsedSeconds = 0f;
			transform.localPosition = start;
			ApplyRotation(start, end);
			m_renderer.sortingOrder = sortingOrder;
			gameObject.SetActive(true);
			m_isPlaying = true;
		}

		private Sprite ResolveProjectileSprite(int presentationTagCode)
		{
			if (presentationTagCode == global::GAS.Runtime.XTag.Combat_Ranged)
			{
				if (m_rangedSprite == null)
				{
					throw new InvalidOperationException("远程攻击投射物缺少箭矢图片。");
				}
				return m_rangedSprite;
			}
			if (presentationTagCode == global::GAS.Runtime.XTag.Combat_Magic)
			{
				if (m_magicSprite == null)
				{
					throw new InvalidOperationException("魔法攻击投射物缺少魔法图片。");
				}
				return m_magicSprite;
			}
			throw new ArgumentOutOfRangeException(
				nameof(presentationTagCode),
				presentationTagCode,
				"只有远程和魔法攻击应该播放投射物。");
		}

		private void Update()
		{
			if (!m_isPlaying)
			{
				return;
			}

			m_elapsedSeconds += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(m_elapsedSeconds / m_durationSeconds);
			transform.localPosition = Vector3.Lerp(m_start, m_end, progress);
			if (progress >= 1f)
			{
				m_isPlaying = false;
				gameObject.SetActive(false);
			}
		}

		private void ApplyRotation(Vector3 start, Vector3 end)
		{
			Vector3 direction = end - start;
			direction.y = 0f;
			if (direction.sqrMagnitude <= 0.000001f)
			{
				return;
			}

			transform.localRotation =
				Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
		}
	}
}
