using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 把新输入系统的指针操作转换为牌桌释放意图；拖拽过程只通过牌桌聚合提交拆堆和位置。
	/// </summary>
	public sealed class TabletopCardDragInput : MonoBehaviour
	{
		private GameCore.InputSystem m_inputSystem;

		private Tabletop m_tabletop;

		private TabletopView m_tabletopView;

		private Func<TabletopCardPointerReleaseIntent, bool> m_releaseHandler;

		private Func<TabletopCardPointerReleaseIntent, bool> m_dropTargetHighlightPredicate;

		private TabletopCardDragSession m_session;

		private TabletopCardId m_currentTargetCardId;

		private readonly List<TabletopCardId> m_dropTargetHighlightCardIds = new List<TabletopCardId>();

		private readonly List<TabletopCardId> m_attachTargetCardIds = new List<TabletopCardId>();

		private readonly List<RaycastResult> m_pointerRaycastResults = new List<RaycastResult>();

		private PointerEventData m_pointerEventData;

		private bool m_isSubscribed;

		public bool IsPointerSessionActive => m_session?.IsActive ?? false;

		public bool IsDragging => m_session?.IsDragging ?? false;

		public void Bind(
			GameCore.InputSystem inputSystem,
			Tabletop tabletop,
			TabletopView tabletopView,
			Func<TabletopCardPointerReleaseIntent, bool> releaseHandler,
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
			m_pointerEventData = new PointerEventData(eventSystem);
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
			m_attachTargetCardIds.Clear();
			m_pointerRaycastResults.Clear();
			m_pointerEventData = null;
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
				UpdateDragSessionWithStackCraftBounds(session, screenPosition, tablePosition);
				m_tabletopView.SetLocalDraggedStack(GetLocalDraggedStackAnchorCardId(session.CardId));
				UpdateDropTarget();
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
				if (!CanBeginPointerDragFromCard(cardView))
				{
					m_tabletopView.SelectCard(cardView.CardId);
					return;
				}

				TabletopCardId localDraggedStackAnchorCardId;
				Vector2 dragAnchor;
				if (m_tabletop.TryGetBattlePose(cardView.CardId, 0, out TabletopCardPose battlePose))
				{
					localDraggedStackAnchorCardId = cardView.CardId;
					dragAnchor = TabletopCoordinateSpace.ToTablePosition(battlePose.LocalPosition);
				}
				else
				{
					Vector2 visibleCardTablePosition =
						TabletopCoordinateSpace.ToTablePosition(cardView.transform.localPosition);
					TabletopCardStack draggedStack = m_tabletop.BeginLocalStackDrag(
						cardView.CardId,
						visibleCardTablePosition);
					localDraggedStackAnchorCardId = draggedStack.TopCard.Id;
					dragAnchor = draggedStack.Position;
				}
				m_session.Begin(cardView.CardId, screenPosition, tablePosition, dragAnchor);
				m_tabletop.HoldAutomaticBehaviorForLocalInput(cardView.CardId);
				UpdateDragSessionWithStackCraftBounds(m_session, screenPosition, tablePosition);
				m_tabletopView.SetLocalDraggedStack(localDraggedStackAnchorCardId);
				UpdateDropTarget();
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

		private bool CanBeginPointerDragFromCard(TabletopCardView cardView)
		{
			if (cardView == null)
			{
				throw new ArgumentNullException(nameof(cardView));
			}
			if (!m_tabletop.ContentIndex.TryGet(cardView.ContentId, out CardDefinition definition))
			{
				throw new InvalidOperationException(
					$"牌桌输入命中的卡牌 {cardView.CardId} 缺少内容作者源：{cardView.ContentId}。");
			}

			// StackCraft 的商贩和收购点是 TradeZone 释放目标，不是 CardController 拖拽来源。
			return definition is not PackVendorDefinition and not CardBuyerDefinition;
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
				UpdateDragSessionWithStackCraftBounds(session, screenPosition, releaseTablePosition);
				m_tabletopView.SetLocalDraggedStack(GetLocalDraggedStackAnchorCardId(session.CardId));
				UpdateDropTarget();
				Vector2 releasedCardTablePosition =
					m_tabletopView.GetCardTablePosition(session.CardId);
				TabletopCardPointerReleaseIntent intent = session.End(
					screenPosition,
					releaseTablePosition,
					releasedCardTablePosition,
					m_currentTargetCardId);
				m_tabletopView.ClearDropTargetHighlights();
				m_dropTargetHighlightCardIds.Clear();
				m_tabletopView.PlayPresentationCue(TabletopPresentationCueKind.CardDrop);
				bool keepReleasedPlacement = intent.IsDrag;
				try
				{
					if (!intent.IsDrag)
					{
						m_tabletopView.SelectCard(intent.CardId);
					}
					keepReleasedPlacement = m_releaseHandler(intent);
				}
				finally
				{
					m_tabletop.FinishLocalStackDragIfActive(intent.CardId, keepReleasedPlacement);
					m_tabletop.ReleaseAutomaticBehaviorForLocalInput(intent.CardId);
					m_tabletopView.ClearLocalDraggedStack();
					m_currentTargetCardId = default(TabletopCardId);
				}
			}
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

		private void UpdateDragSessionWithStackCraftBounds(
			TabletopCardDragSession session,
			Vector2 screenPosition,
			Vector2 tablePosition)
		{
			Vector2 requestedStackPosition = session.CalculateStackPosition(tablePosition);
			Vector2 stackPosition = m_tabletop.TryGetBattlePose(session.CardId, 0, out _)
				? requestedStackPosition
				: m_tabletop.MoveLocalDraggedStackToBounds(
					session.CardId,
					requestedStackPosition);
			session.Update(screenPosition, tablePosition, stackPosition);
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
			EventSystem eventSystem = GameManager.EventSystem;
			if (GameManager.MainCamera == null || eventSystem == null || m_tabletopView == null)
			{
				cardView = null;
				return false;
			}

			SyncPhysicsTransformsForStackCraftPointerQuery();
			PointerEventData pointerEventData = m_pointerEventData ??= new PointerEventData(eventSystem);
			pointerEventData.Reset();
			pointerEventData.position = screenPosition;
			pointerEventData.pointerId = -1;
			eventSystem.RaycastAll(pointerEventData, m_pointerRaycastResults);
			try
			{
				for (int resultIndex = 0; resultIndex < m_pointerRaycastResults.Count; resultIndex++)
				{
					RaycastResult result = m_pointerRaycastResults[resultIndex];
					TabletopCardView candidateView =
						result.gameObject == null
							? null
							: result.gameObject.GetComponentInParent<TabletopCardView>();
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

					cardView = candidateView;
					return true;
				}
			}
			finally
			{
				m_pointerRaycastResults.Clear();
			}

			cardView = null;
			return false;
		}

		private static void SyncPhysicsTransformsForStackCraftPointerQuery()
		{
			// 当前项目关闭了 Physics auto sync；按下命中必须读取刚投影到画面的卡牌碰撞体。
			Physics.SyncTransforms();
		}

		private void UpdateDropTarget()
		{
			TabletopCardDragSession session = m_session;
			if (session == null || !session.IsActive || m_tabletop == null ||
				!m_tabletop.Cards.TryGetStackContaining(session.CardId, out var sourceStack))
			{
				ClearDropTarget();
				return;
			}

			RefreshDropTargetHighlights(session, sourceStack);
			if (TryFindAttachRadiusTarget(session, sourceStack, out var targetView))
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
			m_attachTargetCardIds.Clear();
			IReadOnlyList<TabletopCardStack> stacks = m_tabletop.Cards.Stacks;
			for (int stackIndex = 0; stackIndex < stacks.Count; stackIndex++)
			{
				TabletopCardStack candidateStack = stacks[stackIndex];
				if (ReferenceEquals(candidateStack, sourceStack))
				{
					continue;
				}
				TabletopCardId bottomCardId = candidateStack.BottomCard.Id;
				if (m_tabletop.CanStackOnto(session.CardId, bottomCardId))
				{
					m_attachTargetCardIds.Add(bottomCardId);
				}
			}

			if (!m_tabletopView.TryFindNearestCardViewWithinAttachRadius(
					session.CardId,
					sourceStack,
					m_attachTargetCardIds,
					out targetView))
			{
				return false;
			}

			return true;
		}

		private bool TrySetDropTarget(TabletopCardDragSession session, TabletopCardId targetCardId)
		{
			if (!m_tabletop.CanStackOnto(session.CardId, targetCardId))
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

		private TabletopCardId GetLocalDraggedStackAnchorCardId(TabletopCardId cardId)
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
				m_tabletop?.FinishLocalStackDragIfActive(cardId, keepReleasedPlacement: false);
				m_tabletop?.ReleaseAutomaticBehaviorForLocalInput(cardId);
			}
			m_tabletopView?.ClearLocalDraggedStack();
			m_tabletopView?.ClearDropTargetHighlights();
			m_tabletopView?.SetHoveredCard(default(TabletopCardId));
			m_currentTargetCardId = default(TabletopCardId);
			m_dropTargetHighlightCardIds.Clear();
			m_attachTargetCardIds.Clear();
			m_pointerRaycastResults.Clear();
		}
	}
}
