using System;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 单个活动行动的牌桌进度表现，只显示牌桌行动已有的进度和暂停状态。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TabletopActionProgressView : MonoBehaviour
	{
		[Header("表现组件")]
		[SerializeField]
		[Tooltip("进度条底板。")]
		private SpriteRenderer m_backgroundRenderer;

		[SerializeField]
		[Tooltip("可水平缩放的进度填充条。")]
		private SpriteRenderer m_fillRenderer;

		[Header("布局")]
		[SerializeField]
		[Tooltip("相对行动锚点卡牌的本地位置。")]
		private Vector3 m_anchorOffset = new Vector3(0f, -0.62f, -0.08f);

		[SerializeField]
		[Min(0f)]
		[Tooltip("多个行动锚定到同一张卡牌时，每条进度条向上错开的距离。")]
		private float m_stackedOffset = 0.14f;

		[Header("状态颜色")]
		[SerializeField]
		private Color m_runningColor = new Color(0.24f, 0.86f, 0.94f, 1f);

		[SerializeField]
		private Color m_pausedColor = new Color(1f, 0.72f, 0.24f, 1f);

		private bool m_initialized;
		private Vector3 m_fillBaseLocalPosition;
		private Vector3 m_fillBaseLocalScale;

		/// <summary>当前显示的归一化行动进度；它是视图缓存，不是行动真相。</summary>
		public float NormalizedProgress { get; private set; }

		/// <summary>当前显示是否对应暂停中的行动。</summary>
		public bool IsPaused { get; private set; }

		/// <summary>当前相对于同锚点其它行动的视觉层级。</summary>
		public int StackedIndex { get; private set; }

		private void Awake()
		{
			EnsureInitialized();
		}

		/// <summary>投影本帧的行动状态和锚点排序；不会修改行动实例。</summary>
		public void Show(float normalizedProgress, bool paused, int stackedIndex, int sortingOrder)
		{
			if (!float.IsFinite(normalizedProgress))
			{
				throw new ArgumentOutOfRangeException(
					nameof(normalizedProgress),
					normalizedProgress,
					"行动进度视图只能显示有限数值。");
			}
			if (stackedIndex < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(stackedIndex),
					stackedIndex,
					"行动进度视图的同锚点层级不能小于 0。");
			}

			EnsureInitialized();
			NormalizedProgress = Mathf.Clamp01(normalizedProgress);
			IsPaused = paused;
			StackedIndex = stackedIndex;
			base.gameObject.SetActive(true);
			base.transform.localPosition = m_anchorOffset + Vector3.up * (m_stackedOffset * stackedIndex);
			m_fillRenderer.transform.localScale = new Vector3(
				m_fillBaseLocalScale.x * NormalizedProgress,
				m_fillBaseLocalScale.y,
				m_fillBaseLocalScale.z);
			m_fillRenderer.transform.localPosition = m_fillBaseLocalPosition +
				Vector3.right * ((NormalizedProgress - 1f) * m_fillBaseLocalScale.x * 0.5f);
			m_fillRenderer.color = paused ? m_pausedColor : m_runningColor;
			m_backgroundRenderer.sortingOrder = sortingOrder;
			m_fillRenderer.sortingOrder = sortingOrder + 1;
		}

		/// <summary>隐藏已经没有对应活动行动的进度表现。</summary>
		public void Hide()
		{
			NormalizedProgress = 0f;
			IsPaused = false;
			StackedIndex = 0;
			base.gameObject.SetActive(false);
		}

		private void EnsureInitialized()
		{
			if (m_initialized)
			{
				return;
			}
			if (m_backgroundRenderer == null || m_fillRenderer == null)
			{
				throw new InvalidOperationException(
					"行动进度视图缺少底板或填充渲染器，不能创建不完整投影。");
			}

			m_fillBaseLocalPosition = m_fillRenderer.transform.localPosition;
			m_fillBaseLocalScale = m_fillRenderer.transform.localScale;
			if (m_fillBaseLocalScale.x <= 0f ||
				m_fillBaseLocalScale.y <= 0f ||
				m_fillBaseLocalScale.z <= 0f)
			{
				throw new InvalidOperationException("行动进度视图填充条的初始缩放必须全部大于 0。");
			}
			m_initialized = true;
		}
	}
}
