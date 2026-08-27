using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 单个活动行动的牌桌进度表现，按 StackCraft ProgressUI 的 Image.fillAmount 语义显示进度。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TabletopActionProgressView : MonoBehaviour
	{
		[Header("表现组件")]
		[SerializeField]
		[Tooltip("进度条底板 Image，对齐 StackCraft ProgressUI 根 Image。")]
		private Image m_backgroundImage;

		[SerializeField]
		[Tooltip("进度填充 Image，对齐 StackCraft ProgressUI.progressFill。")]
		private Image m_progressFill;

		[Header("布局")]
		[SerializeField]
		[Tooltip("相对行动锚点卡牌的本地位置；StackCraft ProgressUI.displayOffset 为 {0,0,0.55}。")]
		private Vector3 m_displayOffset = new Vector3(0f, 0f, 0.55f);

		[Header("状态颜色")]
		[SerializeField]
		private Color m_runningColor = new Color(1f, 0.7974138f, 0f, 1f);

		private bool m_initialized;
		/// <summary>当前显示的归一化行动进度；它是视图缓存，不是行动真相。</summary>
		public float NormalizedProgress { get; private set; }

		/// <summary>当前显示是否对应暂停中的行动。</summary>
		public bool IsPaused { get; private set; }

		private void Awake()
		{
			EnsureInitialized();
		}

		/// <summary>投影本帧的行动状态和排序；不会修改行动实例。</summary>
		public void Show(float normalizedProgress, bool paused, int sortingOrder)
		{
			if (!float.IsFinite(normalizedProgress))
			{
				throw new ArgumentOutOfRangeException(
					nameof(normalizedProgress),
					normalizedProgress,
					"行动进度视图只能显示有限数值。");
			}

			EnsureInitialized();
			NormalizedProgress = Mathf.Clamp01(normalizedProgress);
			IsPaused = paused;
			base.gameObject.SetActive(true);
			base.transform.localPosition = m_displayOffset;
			m_progressFill.fillAmount = NormalizedProgress;
			m_progressFill.color = m_runningColor;
			ApplyCanvasSortingOrder(sortingOrder);
		}

		/// <summary>隐藏已经没有对应活动行动的进度表现。</summary>
		public void Hide()
		{
			NormalizedProgress = 0f;
			IsPaused = false;
			base.gameObject.SetActive(false);
		}

		private void EnsureInitialized()
		{
			if (m_initialized)
			{
				return;
			}
			if (m_backgroundImage == null || m_progressFill == null)
			{
				throw new InvalidOperationException(
					"行动进度视图缺少底板 Image 或填充 Image，不能创建不完整投影。");
			}

			if (m_progressFill.type != Image.Type.Filled)
			{
				throw new InvalidOperationException("行动进度填充 Image 必须使用 Filled 类型，才能对齐 StackCraft ProgressUI.fillAmount。");
			}
			m_initialized = true;
		}

		private void ApplyCanvasSortingOrder(int sortingOrder)
		{
			if (!TryGetComponent(out Canvas canvas))
			{
				throw new InvalidOperationException("行动进度视图根对象缺少 WorldSpace Canvas，UGUI 进度条无法在牌桌上显示。");
			}

			canvas.renderMode = RenderMode.WorldSpace;
			canvas.overrideSorting = true;
			canvas.sortingOrder = sortingOrder;
		}
	}
}
