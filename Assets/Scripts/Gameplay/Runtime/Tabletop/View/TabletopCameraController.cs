using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌主相机控制组件，吸收 StackCraft 的中键平移、滚轮缩放和事件聚焦能力。
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
	public sealed class TabletopCameraController : MonoBehaviour
	{
		[Header("牌桌镜头")]
		[SerializeField]
		[LabelText("牌桌视图")]
		[Tooltip("当前场景唯一牌桌视图。镜头只读取它绑定的牌桌和牌桌坐标，不创建或保存玩法状态。")]
		private TabletopView m_tabletopView;

		[SerializeField]
		[LabelText("平移倍率")]
		[Tooltip("中键拖拽时屏幕位移转换为牌桌平移的倍率。1 表示指针下的牌桌点跟随鼠标。")]
		[Min(0.01f)]
		private float m_panMultiplier = 1f;

		[SerializeField]
		[LabelText("平滑时间")]
		[Tooltip("镜头向目标位置和目标缩放平滑移动的时间，单位秒。")]
		[Min(0.001f)]
		private float m_smoothTime = 0.12f;

		[SerializeField]
		[LabelText("边界外余量")]
		[Tooltip("镜头中心允许越过牌桌规则边界的距离，用于保留边缘卡牌观察空间。")]
		[Min(0f)]
		private float m_panPadding = 0.5f;

		[Header("缩放")]
		[SerializeField]
		[LabelText("滚轮缩放倍率")]
		[Tooltip("鼠标滚轮每单位输入改变的正交尺寸。新输入系统鼠标滚轮通常每格约为 120。")]
		[Min(0.0001f)]
		private float m_zoomPerScrollUnit = 0.01f;

		[SerializeField]
		[LabelText("最小正交尺寸")]
		[Tooltip("镜头允许缩到最近时的正交尺寸。")]
		[Min(0.01f)]
		private float m_minOrthographicSize = 2.5f;

		[SerializeField]
		[LabelText("最大正交尺寸")]
		[Tooltip("镜头允许拉远时的正交尺寸。")]
		[Min(0.01f)]
		private float m_maxOrthographicSize = 8f;

		[SerializeField]
		[LabelText("聚焦正交尺寸")]
		[Tooltip("遭遇、解锁等牌桌表现要求聚焦时使用的目标正交尺寸。")]
		[Min(0.01f)]
		private float m_focusOrthographicSize = 3.5f;

		private Camera m_camera;
		private Vector3 m_targetWorldPosition;
		private Vector3 m_moveVelocity;
		private float m_targetOrthographicSize;
		private float m_zoomVelocity;
		private bool m_isDragging;
		private bool m_hasPreviousDragTablePosition;
		private Vector2 m_previousDragTablePosition;
		private bool m_hasLastPointerScreenPosition;
		private Vector2 m_lastPointerScreenPosition;
		private Tabletop m_subscribedTabletop;

		private void Awake()
		{
			m_camera = GetComponent<Camera>();
			if (!m_camera.orthographic)
			{
				throw new System.InvalidOperationException("牌桌镜头控制器只支持正交主相机。");
			}
			ResetTargetsToCurrentCamera();
		}

		private void OnEnable()
		{
			ResetTargetsToCurrentCamera();
			SyncTabletopSubscription();
		}

		private void OnDisable()
		{
			UnsubscribeFromTabletop();
			StopDragging();
		}

		private void LateUpdate()
		{
			if (m_tabletopView == null)
			{
				throw new MissingReferenceException("牌桌镜头控制器缺少唯一牌桌视图引用。");
			}

			SyncTabletopSubscription();
			if (!TryGetInputSystem(out GameCore.InputSystem inputSystem))
			{
				ApplySmoothing();
				return;
			}

			Vector2 screenPosition = inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
			HandlePan(inputSystem, screenPosition);
			HandleZoom(inputSystem, screenPosition);
			ApplySmoothing();
			RememberPointerScreenPosition(screenPosition);
		}

		private static bool TryGetInputSystem(out GameCore.InputSystem inputSystem)
		{
			inputSystem = null;
			return GameManager.Exists() && GameManager.TryGetSystem(out inputSystem);
		}

		private void HandlePan(GameCore.InputSystem inputSystem, Vector2 screenPosition)
		{
			bool middlePressed =
				inputSystem.IsGameplayActionPressed(EGameplayInputAction.MiddleClick) &&
				!inputSystem.IsGameplayActionBlocked(EGameplayInputAction.MiddleClick);
			if (!middlePressed)
			{
				StopDragging();
				return;
			}

			if (!m_isDragging)
			{
				if (UIPointerUtility.IsPositionOverUI(screenPosition))
				{
					return;
				}
				m_isDragging = true;
				Vector2 dragStartScreenPosition = m_hasLastPointerScreenPosition
					? m_lastPointerScreenPosition
					: screenPosition;
				m_hasPreviousDragTablePosition = TryProjectToTable(
					dragStartScreenPosition,
					out m_previousDragTablePosition);
			}

			if (!TryProjectToTable(screenPosition, out Vector2 tablePosition))
			{
				m_hasPreviousDragTablePosition = false;
				return;
			}
			if (m_hasPreviousDragTablePosition)
			{
				Vector2 delta = tablePosition - m_previousDragTablePosition;
				if (float.IsFinite(delta.x) && float.IsFinite(delta.y))
				{
					Vector3 worldDelta = m_tabletopView.transform.TransformVector(
						TabletopCoordinateSpace.ToLocalDelta(delta));
					Vector3 nextTarget = m_targetWorldPosition -
						worldDelta * m_panMultiplier;
					SetTargetWorldPosition(nextTarget);
				}
			}
			m_previousDragTablePosition = tablePosition;
			m_hasPreviousDragTablePosition = true;
		}

		private void HandleZoom(GameCore.InputSystem inputSystem, Vector2 screenPosition)
		{
			if (UIPointerUtility.IsPositionOverUI(screenPosition))
			{
				return;
			}

			Vector2 scrollDelta = inputSystem.ReadGameplayVector2(EGameplayInputAction.ScrollWheel);
			if (Mathf.Abs(scrollDelta.y) <= 0.01f)
			{
				return;
			}

			float nextSize = Mathf.Clamp(
				m_targetOrthographicSize - scrollDelta.y * m_zoomPerScrollUnit,
				m_minOrthographicSize,
				m_maxOrthographicSize);
			if (!Mathf.Approximately(nextSize, m_targetOrthographicSize))
			{
				m_targetOrthographicSize = nextSize;
			}
		}

		private void FocusOnTablePosition(Vector2 tablePosition)
		{
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new System.ArgumentException("牌桌镜头聚焦坐标必须是有限值。", nameof(tablePosition));
			}

			Vector3 targetWorldPosition = m_tabletopView.transform.TransformPoint(
				new Vector3(
					tablePosition.x,
					m_tabletopView.transform.InverseTransformPoint(transform.position).y,
					tablePosition.y));
			SetTargetWorldPosition(targetWorldPosition);
			m_targetOrthographicSize = Mathf.Clamp(
				m_focusOrthographicSize,
				m_minOrthographicSize,
				m_maxOrthographicSize);
			StopDragging();
		}

		private void SetTargetWorldPosition(Vector3 targetWorldPosition)
		{
			m_targetWorldPosition = ClampWorldPositionToTableBounds(targetWorldPosition);
		}

		private Vector3 ClampWorldPositionToTableBounds(Vector3 worldPosition)
		{
			Tabletop tabletop = m_tabletopView != null ? m_tabletopView.BoundTabletop : null;
			if (tabletop == null)
			{
				return worldPosition;
			}

			Rect bounds = tabletop.PlacementRules.Area.Bounds;
			Vector3 tableLocalPosition = m_tabletopView.transform.InverseTransformPoint(worldPosition);
			tableLocalPosition.x = Mathf.Clamp(
				tableLocalPosition.x,
				bounds.xMin - m_panPadding,
				bounds.xMax + m_panPadding);
			tableLocalPosition.z = Mathf.Clamp(
				tableLocalPosition.z,
				bounds.yMin - m_panPadding,
				bounds.yMax + m_panPadding);
			return m_tabletopView.transform.TransformPoint(tableLocalPosition);
		}

		private bool TryProjectToTable(Vector2 screenPosition, out Vector2 tablePosition)
		{
			tablePosition = default;
			if (m_tabletopView == null)
			{
				return false;
			}

			Ray ray = m_camera.ScreenPointToRay(screenPosition);
			Transform tablePlane = m_tabletopView.transform;
			if (!TabletopCoordinateSpace.CreateTablePlane(tablePlane).Raycast(ray, out float distance))
			{
				return false;
			}

			Vector3 localPoint = tablePlane.InverseTransformPoint(ray.GetPoint(distance));
			tablePosition = TabletopCoordinateSpace.ToTablePosition(localPoint);
			return true;
		}

		private void ApplySmoothing()
		{
			transform.position = Vector3.SmoothDamp(
				transform.position,
				m_targetWorldPosition,
				ref m_moveVelocity,
				m_smoothTime,
				Mathf.Infinity,
				Time.unscaledDeltaTime);
			m_camera.orthographicSize = Mathf.SmoothDamp(
				m_camera.orthographicSize,
				m_targetOrthographicSize,
				ref m_zoomVelocity,
				m_smoothTime,
				Mathf.Infinity,
				Time.unscaledDeltaTime);
		}

		private void ResetTargetsToCurrentCamera()
		{
			m_targetWorldPosition = transform.position;
			m_targetOrthographicSize = m_camera != null ? m_camera.orthographicSize : 1f;
			m_moveVelocity = Vector3.zero;
			m_zoomVelocity = 0f;
		}

		private void StopDragging()
		{
			m_isDragging = false;
			m_hasPreviousDragTablePosition = false;
		}

		private void RememberPointerScreenPosition(Vector2 screenPosition)
		{
			if (!float.IsFinite(screenPosition.x) || !float.IsFinite(screenPosition.y))
			{
				m_hasLastPointerScreenPosition = false;
				return;
			}

			m_lastPointerScreenPosition = screenPosition;
			m_hasLastPointerScreenPosition = true;
		}

		private void SyncTabletopSubscription()
		{
			Tabletop nextTabletop = m_tabletopView != null ? m_tabletopView.BoundTabletop : null;
			if (ReferenceEquals(nextTabletop, m_subscribedTabletop))
			{
				return;
			}

			UnsubscribeFromTabletop();
			if (nextTabletop != null)
			{
				m_subscribedTabletop = nextTabletop;
				m_subscribedTabletop.PresentationCueRequested += OnPresentationCueRequested;
				SetTargetWorldPosition(m_targetWorldPosition);
			}
		}

		private void UnsubscribeFromTabletop()
		{
			if (m_subscribedTabletop != null)
			{
				m_subscribedTabletop.PresentationCueRequested -= OnPresentationCueRequested;
				m_subscribedTabletop = null;
			}
		}

		private void OnPresentationCueRequested(TabletopPresentationCue cue)
		{
			if (cue.Kind != TabletopPresentationCueKind.CameraFocus)
			{
				return;
			}
			if (!cue.HasTablePosition)
			{
				throw new System.InvalidOperationException("牌桌镜头聚焦反馈必须带有牌桌坐标。");
			}
			FocusOnTablePosition(cue.TablePosition);
		}
	}
}
