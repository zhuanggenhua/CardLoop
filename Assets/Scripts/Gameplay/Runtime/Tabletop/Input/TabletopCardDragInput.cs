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

		private Func<TabletopCardPointerReleaseIntent, bool> m_dropTargetHighlightPredicate;

		private TabletopCardDragSession m_session;

		private TabletopCardId m_currentTargetCardId;

		private readonly List<TabletopCardId> m_dropTargetHighlightCardIds = new List<TabletopCardId>();

		private bool m_isSubscribed;

		public bool IsPointerSessionActive => m_session?.IsActive ?? false;

		public bool IsDragging => m_session?.IsDragging ?? false;

		public void Bind(
			GameCore.InputSystem inputSystem,
			Tabletop tabletop,
			TabletopView tabletopView,
			Action<TabletopCardPointerReleaseIntent> releaseHandler,
			Func<TabletopCardPointerReleaseIntent, bool> dropTargetHighlightPredicate)
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
			if (dropTargetHighlightPredicate == null)
			{
				throw new ArgumentNullException(nameof(dropTargetHighlightPredicate));
			}
			EventSystem eventSystem = GameManager.EventSystem;
			if (eventSystem == null)
			{
				throw new InvalidOperationException(
					"牌桌拖拽输入需要 GameManager 提供正式 EventSystem，用于 UI 命中和释放目标射线。");
			}
			Unsubscribe();
			CancelCurrentInteraction();
			m_inputSystem = inputSystem;
			m_tabletop = tabletop;
			m_tabletopView = tabletopView;
			m_releaseHandler = releaseHandler;
			m_dropTargetHighlightPredicate = dropTargetHighlightPredicate;
			m_session = new TabletopCardDragSession(tabletopView.CardClickThreshold);
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
			m_dropTargetHighlightPredicate = null;
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
			if (m_inputSystem != null && m_inputSystem.IsGameplayInputLocked)
			{
				CancelCurrentInteraction();
				return;
			}

			TabletopCardDragSession session = m_session;
			if (session == null || !session.IsActive)
			{
				UpdateHoveredCard();
			}
			if (session != null && session.IsActive &&
				TryReadPointerPositions(out var screenPosition, out var tablePosition))
			{
				session.Update(screenPosition, tablePosition);
				m_tabletopView.SetDragPreview(
					GetDragPreviewAnchorCardId(session.CardId),
					session.CurrentStackPosition);
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
				if (!(inputSystem == null) && GameManager.StartupState == GameManagerStartupState.Ready)
				{
					inputSystem.RemoveGameplayActionListener(EGameplayInputAction.Click, EInputActionPhase.Started, OnPrimaryPointerStarted);
					inputSystem.RemoveGameplayActionListener(EGameplayInputAction.Click, EInputActionPhase.Canceled, OnPrimaryPointerCanceled);
				}
			}
		}

		private void OnPrimaryPointerStarted(InputAction.CallbackContext _)
		{
			if (m_inputSystem == null ||
				m_inputSystem.IsGameplayInputLocked)
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
				int draggedSegmentStartIndex = sourceStack.GetDraggedSegmentStartIndex(cardView.CardId);
				TabletopCardId previewAnchorCardId = sourceStack.Cards[draggedSegmentStartIndex].Id;
				Vector2 dragAnchor = m_tabletop.Cards.GetCardTablePosition(
					previewAnchorCardId,
					m_tabletop.PlacementRules.Geometry);
				if (m_tabletop.TryGetBattlePose(cardView.CardId, 0, out TabletopCardPose battlePose))
				{
					previewAnchorCardId = cardView.CardId;
					dragAnchor = TabletopCoordinateSpace.ToTablePosition(battlePose.LocalPosition);
				}
				m_session.Begin(cardView.CardId, screenPosition, tablePosition, dragAnchor);
				m_tabletop.HoldAutomaticBehaviorForLocalInput(cardView.CardId);
				m_tabletopView.SetDragPreview(previewAnchorCardId, m_session.CurrentStackPosition);
				UpdateDropTarget(screenPosition);
				m_tabletopView.PlayPresentationCue(TabletopPresentationCueKind.CardPick);
			}
		}

		private bool IsPointerOverUi(Vector2 screenPosition)
		{
			if (GameManager.EventSystem == null)
			{
				throw new InvalidOperationException(
					"牌桌拖拽输入已绑定，但 GameManager 的正式 EventSystem 已丢失。");
			}

			return UIPointerUtility.IsPositionOverUI(screenPosition);
		}

		private void OnPrimaryPointerCanceled(InputAction.CallbackContext _)
		{
			if (m_inputSystem == null ||
				m_inputSystem.IsGameplayInputLocked)
			{
				CancelCurrentInteraction();
				return;
			}

			TabletopCardDragSession session = m_session;
			if (session != null && session.IsActive)
			{
				Vector2 screenPosition = m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
				Vector2 releaseTablePosition = TryProjectToTable(screenPosition, out var tablePosition)
					? tablePosition
					: session.CurrentPointerTablePosition;
				session.Update(screenPosition, releaseTablePosition);
				UpdateDropTarget(screenPosition);
				TabletopCardPointerReleaseIntent intent = session.End(
					screenPosition,
					releaseTablePosition,
					m_currentTargetCardId);
				m_tabletop.ReleaseAutomaticBehaviorForLocalInput(intent.CardId);
				m_tabletopView.ClearDragPreview();
				m_tabletopView.ClearDropTargetHighlights();
				m_currentTargetCardId = default(TabletopCardId);
				m_dropTargetHighlightCardIds.Clear();
				m_tabletopView.PlayPresentationCue(TabletopPresentationCueKind.CardDrop);
				if (intent.IsDrag && TryGetCardDropTarget(screenPosition, out var dropTarget))
				{
					dropTarget.TryAcceptCard(intent.CardId);
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
			if (m_inputSystem.IsGameplayInputLocked)
			{
				m_tabletopView.SetHoveredCard(default(TabletopCardId));
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
			if (!TabletopCoordinateSpace.CreateTablePlane(tablePlane).Raycast(ray, out var distance))
			{
				tablePosition = default(Vector2);
				return false;
			}
			Vector3 localPoint = tablePlane.InverseTransformPoint(ray.GetPoint(distance));
			tablePosition = TabletopCoordinateSpace.ToTablePosition(localPoint);
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
			if (camera == null || m_tabletopView == null)
			{
				cardView = null;
				return false;
			}

			TabletopCardView bestView = null;
			float bestDistance = float.PositiveInfinity;
			int bestSortingOrder = int.MinValue;
			Ray ray = camera.ScreenPointToRay(screenPosition);
			RaycastHit[] hits = Physics.RaycastAll(
				ray,
				float.PositiveInfinity,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore);
			for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
			{
				RaycastHit hit = hits[hitIndex];
				TabletopCardView candidateView = hit.collider.GetComponentInParent<TabletopCardView>();
				if (candidateView == null ||
					!candidateView.CardId.IsValid ||
					!m_tabletopView.TryGetCardView(candidateView.CardId, out TabletopCardView registeredView) ||
					!ReferenceEquals(candidateView, registeredView))
				{
					continue;
				}
				if (excludedStack != null &&
					ReferenceEquals(candidateView.TabletopCard?.Stack, excludedStack))
				{
					continue;
				}
				if (bestView == null ||
					candidateView.SortingOrder > bestSortingOrder ||
					(candidateView.SortingOrder == bestSortingOrder &&
					 hit.distance < bestDistance - 0.0001f))
				{
					bestView = candidateView;
					bestDistance = hit.distance;
					bestSortingOrder = candidateView.SortingOrder;
				}
			}

			cardView = bestView;
			return cardView != null;
		}

		private void UpdateDropTarget(Vector2 screenPosition)
		{
			TabletopCardDragSession session = m_session;
			if (session == null || !session.IsActive || m_tabletop == null ||
				!m_tabletop.Cards.TryGetStackContaining(session.CardId, out var sourceStack))
			{
				ClearDropTarget();
				return;
			}

			RefreshDropTargetHighlights(session, sourceStack);
			if (TryHitCardView(screenPosition, sourceStack, out var targetView))
			{
				if (TrySetDropTarget(session, targetView.CardId))
				{
					return;
				}
			}
			if (TryFindAttachRadiusTarget(session, sourceStack, out targetView))
			{
				TrySetDropTarget(session, targetView.CardId);
			}
			else
			{
				ClearCurrentDropTarget();
			}
		}

		private bool TryFindAttachRadiusTarget(
			TabletopCardDragSession session,
			TabletopCardStack sourceStack,
			out TabletopCardView targetView)
		{
			if (!m_tabletopView.TryFindNearestCardViewWithinAttachRadius(
					session.CurrentStackPosition,
					sourceStack,
					m_dropTargetHighlightCardIds,
					out targetView))
			{
				return false;
			}

			return true;
		}

		private bool TrySetDropTarget(TabletopCardDragSession session, TabletopCardId targetCardId)
		{
			if (!CanHighlightDropTarget(session, targetCardId))
			{
				m_currentTargetCardId = default(TabletopCardId);
				return false;
			}

			m_currentTargetCardId = targetCardId;
			return true;
		}

		private void RefreshDropTargetHighlights(
			TabletopCardDragSession session,
			TabletopCardStack sourceStack)
		{
			m_dropTargetHighlightCardIds.Clear();
			IReadOnlyList<TabletopCardStack> stacks = m_tabletop.Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				TabletopCardStack candidateStack = stacks[stackIndex];
				if (ReferenceEquals(candidateStack, sourceStack))
				{
					continue;
				}
				TabletopCardId bottomCardId = candidateStack.BottomCard.Id;
				if (CanHighlightDropTarget(session, bottomCardId))
				{
					m_dropTargetHighlightCardIds.Add(bottomCardId);
				}
			}

			m_tabletopView.SetDropTargetHighlights(m_dropTargetHighlightCardIds);
		}

		private bool CanHighlightDropTarget(TabletopCardDragSession session, TabletopCardId targetCardId)
		{
			TabletopCardPointerReleaseIntent intent = new TabletopCardPointerReleaseIntent(
				session.CardId,
				session.PressPointerTablePosition,
				session.CurrentPointerTablePosition,
				session.CurrentStackPosition,
				isDrag: true,
				targetCardId);
			return m_dropTargetHighlightPredicate(intent);
		}

		private void ClearDropTarget()
		{
			ClearCurrentDropTarget();
			m_dropTargetHighlightCardIds.Clear();
			m_tabletopView.ClearDropTargetHighlights();
		}

		private void ClearCurrentDropTarget()
		{
			m_currentTargetCardId = default(TabletopCardId);
		}

		private TabletopCardId GetDragPreviewAnchorCardId(TabletopCardId cardId)
		{
			if (m_tabletop.TryGetBattlePose(cardId, 0, out _))
			{
				return cardId;
			}

			TabletopCardStack sourceStack = m_tabletop.Cards.GetStackContaining(cardId);
			int draggedSegmentStartIndex = sourceStack.GetDraggedSegmentStartIndex(cardId);
			return sourceStack.Cards[draggedSegmentStartIndex].Id;
		}

		private void CancelCurrentInteraction()
		{
			TabletopCardDragSession session = m_session;
			if (session != null && session.IsActive)
			{
				TabletopCardId cardId = session.CardId;
				session.Cancel();
				m_tabletop?.ReleaseAutomaticBehaviorForLocalInput(cardId);
			}
			m_tabletopView?.ClearDragPreview();
			m_tabletopView?.ClearDropTargetHighlights();
			m_tabletopView?.SetHoveredCard(default(TabletopCardId));
			m_currentTargetCardId = default(TabletopCardId);
			m_dropTargetHighlightCardIds.Clear();
		}
	}
}
