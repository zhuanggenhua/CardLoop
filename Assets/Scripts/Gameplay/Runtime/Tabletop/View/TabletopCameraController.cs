using DG.Tweening;
using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌镜头控制根，按 StackCraft 的透视相机、平移、滚轮缩放和事件聚焦参数驱动真实主相机。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TabletopCameraController : MonoBehaviour
	{
		[Header("牌桌镜头")]
		[SerializeField]
		[LabelText("牌桌视图")]
		[Tooltip("当前场景唯一牌桌视图。镜头只读取它绑定的牌桌和牌桌坐标，不创建或保存玩法状态。")]
		private TabletopView m_tabletopView;

		[SerializeField]
		[LabelText("相机 Transform")]
		[Tooltip("真正带 Camera 组件的子物体 Transform；这是唯一相机配置入口，Camera 组件由该对象自动读取。")]
		private Transform m_cameraTransform;

		private Camera m_camera;

		[SerializeField]
		[LabelText("平移速度")]
		[Tooltip("拖拽空白牌桌时每像素屏幕位移转换为世界位移的倍率；对齐 StackCraft panSpeed = 0.01。")]
		[Min(0.0001f)]
		private float m_panSpeed = 0.01f;

		[SerializeField]
		[LabelText("平滑时间")]
		[Tooltip("镜头向目标位置平滑移动的时间；对齐 StackCraft smoothTime = 0.15。")]
		[Min(0.001f)]
		private float m_smoothTime = 0.15f;

		[SerializeField]
		[LabelText("边界外余量")]
		[Tooltip("镜头中心允许越过牌桌规则边界的距离；对齐 StackCraft panPadding = 0.5。")]
		[Min(0f)]
		private float m_panPadding = 0.5f;

		[SerializeField]
		[LabelText("滚轮缩放速度")]
		[Tooltip("滚轮输入沿相机 forward 改变控制根位置的倍率；对齐 StackCraft zoomSpeed = 1。")]
		[Min(0.0001f)]
		private float m_zoomSpeed = 1f;

		[SerializeField]
		[LabelText("最近距离")]
		[Tooltip("沿相机 forward 计算的最近观察距离；对齐 StackCraft minDistance = 5。")]
		[Min(0.01f)]
		private float m_minDistance = 5f;

		[SerializeField]
		[LabelText("最远距离")]
		[Tooltip("沿相机 forward 计算的最远观察距离；对齐 StackCraft maxDistance = 20。")]
		[Min(0.01f)]
		private float m_maxDistance = 20f;

		[SerializeField]
		[LabelText("聚焦秒数")]
		[Tooltip("遭遇和商贩解锁聚焦到目标点的补间时长；对齐 StackCraft MoveTo 默认 0.5 秒。")]
		[Min(0f)]
		private float m_focusDurationSeconds = 0.5f;

		private Vector2 m_dragOriginScreenPosition;
		private Vector3 m_targetWorldPosition;
		private Vector3 m_moveVelocity;
		private bool m_isDragging;
		private Tabletop m_subscribedTabletop;
		private TabletopCardDragInput m_tabletopCardDragInput;
		private Tween m_focusTween;

		private void Awake()
		{
			RequireCameraBinding();
			ResetTargetToCurrentRoot();
		}

		private void OnEnable()
		{
			RequireCameraBinding();
			GameManager.RegisterMainCamera(m_camera, this);
			ResetTargetToCurrentRoot();
			SyncTabletopSubscription();
		}

		private void OnDisable()
		{
			GameManager.UnregisterMainCamera(m_camera, this);
			UnsubscribeFromTabletop();
			StopDragging();
			KillFocusTween();
		}

		private void LateUpdate()
		{
			if (m_tabletopView == null)
			{
				throw new MissingReferenceException("牌桌镜头控制器缺少唯一牌桌视图引用。");
			}

			SyncTabletopSubscription();
			if (TryGetInputSystem(out GameCore.InputSystem inputSystem))
			{
				if (inputSystem.IsGameplayInputLocked)
				{
					StopDragging();
				}
				else
				{
					Vector2 screenPosition = inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
					HandlePan(inputSystem, screenPosition);
					HandleZoom(inputSystem, screenPosition);
				}
			}
			ApplySmoothing();
		}

		private void RequireCameraBinding()
		{
			if (m_cameraTransform == null)
			{
				throw new MissingReferenceException("牌桌镜头控制器缺少相机 Transform 引用。");
			}
			m_camera = m_cameraTransform.GetComponent<Camera>();
			if (m_camera == null)
			{
				throw new MissingReferenceException("牌桌镜头控制器配置的相机 Transform 上缺少 Camera 组件。");
			}
			if (m_camera.orthographic)
			{
				throw new System.InvalidOperationException("StackCraft 镜头复刻必须使用透视相机，不能使用正交相机。");
			}
			if (m_tabletopView != null)
			{
				m_tabletopCardDragInput = m_tabletopView.GetComponent<TabletopCardDragInput>();
				if (m_tabletopCardDragInput == null)
				{
					throw new MissingReferenceException("牌桌镜头控制器必须和正式牌桌拖拽输入绑定同一张牌桌，避免拖卡时镜头同时平移。");
				}
			}
		}

		private static bool TryGetInputSystem(out GameCore.InputSystem inputSystem)
		{
			inputSystem = null;
			return GameManager.Exists() && GameManager.TryGetSystem(out inputSystem);
		}

		private void HandlePan(GameCore.InputSystem inputSystem, Vector2 screenPosition)
		{
			if (m_tabletopCardDragInput != null && m_tabletopCardDragInput.IsPointerSessionActive)
			{
				StopDragging();
				return;
			}

			bool middlePressed =
				inputSystem.IsGameplayActionPressed(EGameplayInputAction.MiddleClick) &&
				!inputSystem.IsGameplayActionBlocked(EGameplayInputAction.MiddleClick);
			bool leftPressed =
				inputSystem.IsGameplayActionPressed(EGameplayInputAction.Click) &&
				!inputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click);
			bool panPressed = middlePressed || leftPressed;
			if (!panPressed)
			{
				StopDragging();
				return;
			}

			if (!m_isDragging)
			{
				if (!middlePressed && IsPointerBlockedForPan(screenPosition))
				{
					return;
				}
				m_isDragging = true;
				m_dragOriginScreenPosition = screenPosition;
				KillFocusTween();
			}

			Vector2 delta = screenPosition - m_dragOriginScreenPosition;
			if (float.IsFinite(delta.x) && float.IsFinite(delta.y))
			{
				Vector3 tableDelta = new Vector3(-delta.x, 0f, -delta.y) *
					m_panSpeed *
					(transform.position.y / 10f);
				Vector3 worldDelta = m_tabletopView.transform.TransformVector(tableDelta);
				SetTargetWorldPosition(m_targetWorldPosition + worldDelta);
			}

			m_dragOriginScreenPosition = screenPosition;
		}

		private bool IsPointerBlockedForPan(Vector2 screenPosition)
		{
			if (UIPointerUtility.IsPositionOverUI(screenPosition))
			{
				return true;
			}

			Ray ray = m_camera.ScreenPointToRay(screenPosition);
			return Physics.Raycast(
					ray,
					out RaycastHit hit,
					Mathf.Infinity,
					Physics.DefaultRaycastLayers,
					QueryTriggerInteraction.Ignore) &&
				hit.collider.GetComponentInParent<TabletopCardView>() != null;
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

			Vector3 nextPosition = m_targetWorldPosition +
				m_cameraTransform.forward * (scrollDelta.y * m_zoomSpeed);
			float nextDistance = CalculateDistanceFromTable(nextPosition);
			if (nextDistance >= m_minDistance && nextDistance <= m_maxDistance)
			{
				KillFocusTween();
				SetTargetWorldPosition(nextPosition);
			}
		}

		private float CalculateDistanceFromTable(Vector3 rootWorldPosition)
		{
			float cosine = Mathf.Cos(Mathf.Deg2Rad * (90f - m_cameraTransform.eulerAngles.x));
			if (Mathf.Approximately(cosine, 0f))
			{
				throw new System.InvalidOperationException("牌桌镜头角度无法计算 StackCraft 缩放距离。");
			}
			return rootWorldPosition.y / cosine;
		}

		private void FocusOnTablePosition(Vector2 tablePosition)
		{
			if (!float.IsFinite(tablePosition.x) || !float.IsFinite(tablePosition.y))
			{
				throw new System.ArgumentException("牌桌镜头聚焦坐标必须是有限值。", nameof(tablePosition));
			}

			Vector3 targetWorldPosition = m_tabletopView.transform.TransformPoint(
				TabletopCoordinateSpace.ToLocalPosition(tablePosition));
			float desiredDistance = Mathf.Lerp(m_maxDistance, m_minDistance, 0.8f);
			Vector3 nextRootPosition = targetWorldPosition - m_cameraTransform.forward * desiredDistance;
			StopDragging();
			KillFocusTween();
			Tween focusTween = transform
				.DOMove(nextRootPosition, m_focusDurationSeconds)
				.SetUpdate(true)
				.SetTarget(this)
				.SetLink(gameObject, LinkBehaviour.KillOnDisable);
			m_focusTween = focusTween;
			focusTween.OnComplete(() =>
			{
				m_targetWorldPosition = nextRootPosition;
				m_moveVelocity = Vector3.zero;
			});
			focusTween.OnKill(() =>
			{
				if (ReferenceEquals(m_focusTween, focusTween))
				{
					m_focusTween = null;
				}
			});
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

		private void ApplySmoothing()
		{
			if (m_focusTween != null && m_focusTween.active)
			{
				return;
			}

			transform.position = Vector3.SmoothDamp(
				transform.position,
				m_targetWorldPosition,
				ref m_moveVelocity,
				m_smoothTime,
				Mathf.Infinity,
				Time.unscaledDeltaTime);
		}

		private void ResetTargetToCurrentRoot()
		{
			m_targetWorldPosition = transform.position;
			m_moveVelocity = Vector3.zero;
		}

		private void StopDragging()
		{
			m_isDragging = false;
		}

		private void KillFocusTween()
		{
			if (m_focusTween == null)
			{
				return;
			}

			m_focusTween.Kill();
			m_focusTween = null;
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
