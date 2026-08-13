using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 把新输入系统的指针操作转换为牌桌释放意图；它只读权威状态，不直接修改牌桌。
	/// </summary>
	public sealed class TabletopCardDragInput : MonoBehaviour
	{
		private GameCore.InputSystem m_inputSystem;

		private Tabletop m_tabletop;

		private TabletopView m_tabletopView;

		private Action<TabletopCardPointerReleaseIntent> m_releaseHandler;

		private TabletopCardDragSession m_session;

		private TabletopCardId m_currentTargetCardId;

		private bool m_isSubscribed;

		public bool IsPointerSessionActive => m_session?.IsActive ?? false;

		public bool IsDragging => m_session?.IsDragging ?? false;

		public void Bind(
			GameCore.InputSystem inputSystem,
			Tabletop tabletop,
			TabletopView tabletopView,
			Action<TabletopCardPointerReleaseIntent> releaseHandler)
		{
			if (inputSystem == null)
			{
				throw new ArgumentNullException("inputSystem");
			}
			if (tabletop == null)
			{
				throw new ArgumentNullException(nameof(tabletop));
			}
			if (tabletopView == null)
			{
				throw new ArgumentNullException(nameof(tabletopView));
			}
			if (!ReferenceEquals(tabletopView.BoundTabletop, tabletop))
			{
				throw new InvalidOperationException("牌桌拖拽输入与卡牌视图投影必须绑定同一个牌桌。");
			}
			if (releaseHandler == null)
			{
				throw new ArgumentNullException("releaseHandler");
			}
			EventSystem eventSystem = GameManager.EventSystem;
			if (eventSystem == null)
			{
				throw new InvalidOperationException(
					"牌桌拖拽输入需要 GameManager 提供正式 EventSystem，以复用项目唯一的像素拖拽阈值。");
			}
			Unsubscribe();
			CancelCurrentInteraction();
			m_inputSystem = inputSystem;
			m_tabletop = tabletop;
			m_tabletopView = tabletopView;
			m_releaseHandler = releaseHandler;
			m_session = new TabletopCardDragSession(eventSystem.pixelDragThreshold);
			SubscribeIfPossible();
		}

		public void Unbind()
		{
			Unsubscribe();
			CancelCurrentInteraction();
			m_inputSystem = null;
			m_tabletop = null;
			m_tabletopView = null;
			m_releaseHandler = null;
			m_session = null;
		}

		private void OnEnable()
		{
			SubscribeIfPossible();
		}

		private void OnDisable()
		{
			Unsubscribe();
			CancelCurrentInteraction();
		}

		private void Update()
		{
			TabletopCardDragSession session = m_session;
			if (session == null || !session.IsActive)
			{
				UpdateHoveredCard();
			}
			if (session != null && session.IsActive &&
				TryReadPointerPositions(out var screenPosition, out var tablePosition) &&
				session.Update(screenPosition, tablePosition))
			{
				m_tabletopView.SetDragPreview(session.CardId, session.CurrentStackPosition);
				UpdateDropTarget(screenPosition);
			}
		}

		private void SubscribeIfPossible()
		{
			if (base.isActiveAndEnabled && !m_isSubscribed && !(m_inputSystem == null))
			{
				m_inputSystem.AddGameplayActionListener(EGameplayInputAction.Click, EInputActionPhase.Started, OnPrimaryPointerStarted);
				m_inputSystem.AddGameplayActionListener(EGameplayInputAction.Click, EInputActionPhase.Canceled, OnPrimaryPointerCanceled);
				m_isSubscribed = true;
			}
		}

		private void Unsubscribe()
		{
			if (m_isSubscribed)
			{
				GameCore.InputSystem inputSystem = m_inputSystem;
				m_isSubscribed = false;
				if (!(inputSystem == null))
				{
					inputSystem.RemoveGameplayActionListener(EGameplayInputAction.Click, EInputActionPhase.Started, OnPrimaryPointerStarted);
					inputSystem.RemoveGameplayActionListener(EGameplayInputAction.Click, EInputActionPhase.Canceled, OnPrimaryPointerCanceled);
				}
			}
		}

		private void OnPrimaryPointerStarted(InputAction.CallbackContext _)
		{
			if (m_inputSystem == null)
			{
				return;
			}

			Vector2 screenPosition = m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
			if (IsPointerOverUi(screenPosition))
			{
				return;
			}

			if (!m_inputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click) &&
				m_session != null &&
				!m_session.IsActive &&
				TryProjectToTable(screenPosition, out var tablePosition) &&
				TryHitCardView(screenPosition, out var cardView))
			{
				TabletopCardStack sourceStack = m_tabletop.Cards.GetStackContaining(cardView.CardId);
				m_session.Begin(cardView.CardId, screenPosition, tablePosition, sourceStack.Position);
			}
		}

		private bool IsPointerOverUi(Vector2 screenPosition)
		{
			EventSystem eventSystem = GameManager.EventSystem;
			if (eventSystem == null)
			{
				throw new InvalidOperationException(
					"牌桌拖拽输入已绑定，但 GameManager 的正式 EventSystem 已丢失。");
			}

			// InputAction 回调可能早于 EventSystem 刷新上一帧的鼠标命中状态；直接用当前坐标射线检测。
			PointerEventData pointerEventData = new(eventSystem)
			{
				position = screenPosition
			};
			List<RaycastResult> results = new();
			eventSystem.RaycastAll(pointerEventData, results);
			return results.Count > 0;
		}

		private void OnPrimaryPointerCanceled(InputAction.CallbackContext _)
		{
			TabletopCardDragSession session = m_session;
			if (session != null && session.IsActive)
			{
				Vector2 screenPosition = m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
				Vector2 releaseTablePosition = TryProjectToTable(screenPosition, out var tablePosition)
					? tablePosition
					: session.CurrentPointerTablePosition;
				UpdateDropTarget(screenPosition);
				TabletopCardPointerReleaseIntent intent = session.End(
					screenPosition,
					releaseTablePosition,
					m_currentTargetCardId);
				m_tabletopView.ClearDragPreview();
				m_tabletopView.SetDropTargetHighlight(default(TabletopCardId));
				m_currentTargetCardId = default(TabletopCardId);
				if (intent.IsDrag && TryGetCardDropTarget(screenPosition, out var dropTarget))
				{
					dropTarget.AcceptCard(intent.CardId);
					return;
				}
				if (!intent.IsDrag)
				{
					m_tabletopView.SelectCard(intent.CardId);
				}
				m_releaseHandler(intent);
			}
		}

		private bool TryGetCardDropTarget(
			Vector2 screenPosition,
			out ITabletopCardDropTarget dropTarget)
		{
			PointerEventData pointerEventData = new(GameManager.EventSystem)
			{
				position = screenPosition
			};
			List<RaycastResult> results = new();
			GameManager.EventSystem.RaycastAll(pointerEventData, results);
			for (int i = 0; i < results.Count; i++)
			{
				dropTarget = results[i].gameObject.GetComponentInParent<ITabletopCardDropTarget>();
				if (dropTarget != null)
				{
					return true;
				}
			}
			dropTarget = null;
			return false;
		}

		private void UpdateHoveredCard()
		{
			if (m_inputSystem == null || m_tabletopView == null)
			{
				return;
			}

			Vector2 screenPosition =
				m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
			TabletopCardId hoveredCardId = !IsPointerOverUi(screenPosition) &&
				TryHitCardView(screenPosition, out TabletopCardView cardView)
					? cardView.CardId
					: default;
			m_tabletopView.SetHoveredCard(hoveredCardId);
		}

		private bool TryReadPointerPositions(out Vector2 screenPosition, out Vector2 tablePosition)
		{
			if (m_inputSystem == null)
			{
				screenPosition = default;
				tablePosition = default;
				return false;
			}

			screenPosition = m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
			return TryProjectToTable(screenPosition, out tablePosition);
		}

		private bool TryProjectToTable(Vector2 screenPosition, out Vector2 tablePosition)
		{
			Camera camera = GameManager.MainCamera;
			Transform tablePlane = transform;
			if (camera == null)
			{
				tablePosition = default(Vector2);
				return false;
			}
			Ray ray = camera.ScreenPointToRay(screenPosition);
			if (!new Plane(tablePlane.forward, tablePlane.position).Raycast(ray, out var distance))
			{
				tablePosition = default(Vector2);
				return false;
			}
			Vector3 localPoint = tablePlane.InverseTransformPoint(ray.GetPoint(distance));
			tablePosition = new Vector2(localPoint.x, localPoint.y);
			return true;
		}

		private bool TryHitCardView(Vector2 screenPosition, out TabletopCardView cardView)
		{
			return TryHitCardView(screenPosition, null, out cardView);
		}

		private bool TryHitCardView(
			Vector2 screenPosition,
			TabletopCardStack excludedStack,
			out TabletopCardView cardView)
		{
			Camera camera = GameManager.MainCamera;
			if (camera == null)
			{
				cardView = null;
				return false;
			}
			Ray ray = camera.ScreenPointToRay(screenPosition);
			RaycastHit[] hits = Physics.RaycastAll(
				ray,
				camera.farClipPlane,
				Physics.AllLayers,
				QueryTriggerInteraction.Collide);
			Array.Sort(hits, (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance));
			for (int i = 0; i < hits.Length; i++)
			{
				TabletopCardView found = hits[i].collider.GetComponentInParent<TabletopCardView>();
				if (!(found == null) && found.CardId.IsValid &&
					(excludedStack == null ||
					 !m_tabletop.Cards.TryGetStackContaining(found.CardId, out var foundStack) ||
					 excludedStack != foundStack))
				{
					cardView = found;
					return true;
				}
			}
			cardView = null;
			return false;
		}

		private void UpdateDropTarget(Vector2 screenPosition)
		{
			TabletopCardDragSession session = m_session;
			if (session == null || !session.IsDragging || m_tabletop == null ||
				!m_tabletop.Cards.TryGetStackContaining(session.CardId, out var sourceStack) ||
				!TryHitCardView(screenPosition, sourceStack, out var targetView))
			{
				m_currentTargetCardId = default(TabletopCardId);
				m_tabletopView.SetDropTargetHighlight(default(TabletopCardId));
			}
			else
			{
				m_currentTargetCardId = targetView.CardId;
				m_tabletopView.SetDropTargetHighlight(m_currentTargetCardId);
			}
		}

		private void CancelCurrentInteraction()
		{
			TabletopCardDragSession session = m_session;
			if (session != null && session.IsActive)
			{
				session.Cancel();
			}
			m_tabletopView?.ClearDragPreview();
			m_tabletopView?.SetDropTargetHighlight(default(TabletopCardId));
			m_tabletopView?.SetHoveredCard(default(TabletopCardId));
			m_currentTargetCardId = default(TabletopCardId);
		}
	}
}
