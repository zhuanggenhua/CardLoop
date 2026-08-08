using System;
using GameCore;
using UnityEngine;
using UnityEngine.InputSystem;
using CoreInputSystem = GameCore.InputSystem;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 将项目正式输入系统的主指针动作转换为可堆叠卡牌的点击/拖拽意图。
    /// 它只负责卡牌命中、坐标投影和预览，不修改权威卡牌状态，也不解释配方、装备、交易或战斗。
    /// </summary>
    public sealed class TabletopCardDragInput : MonoBehaviour
    {
        [Header("场景引用")]
        [SerializeField, InspectorName("牌桌相机")]
        [Tooltip("把正式 Point 动作的屏幕坐标投影到牌桌平面的相机。")]
        private Camera m_tabletopCamera;

        [SerializeField, InspectorName("牌桌平面")]
        [Tooltip("其局部 XY 坐标作为 TabletopCardState 的二维坐标，局部 Z 轴作为射线投影平面法线。")]
        private Transform m_tablePlane;

        [Header("命中与手感")]
        [SerializeField, InspectorName("卡牌命中层")]
        [Tooltip("只在这些物理层中寻找带 TabletopCardView 的卡牌视图。")]
        private LayerMask m_cardHitMask = ~0;

        [SerializeField, InspectorName("最大命中距离")]
        [Tooltip("相机射线寻找卡牌视图的最大世界距离。")]
        [Min(0.01f)]
        private float m_maxHitDistance = 1000f;

        [SerializeField, InspectorName("拖拽起始距离")]
        [Tooltip("指针在牌桌二维坐标中移动达到该距离后才进入拖拽；小于该值的释放仍是点击。")]
        [Min(0f)]
        private float m_dragStartDistance = 0.05f;

        private CoreInputSystem m_inputSystem;
        private TabletopCardState m_state;
        private TabletopCardViewProjector m_viewProjector;
        private Action<TabletopCardPointerReleaseIntent> m_releaseHandler;
        private TabletopCardDragSession m_session;
        private TabletopCardId m_currentTargetCardId;
        private bool m_isSubscribed;

        /// <summary>当前是否正在跟踪一次尚未释放的主指针交互。</summary>
        public bool IsPointerSessionActive => m_session?.IsActive == true;

        /// <summary>当前主指针交互是否已经跨过拖拽阈值。</summary>
        public bool IsDragging => m_session?.IsDragging == true;

        /// <summary>
        /// 绑定正式输入 owner、卡牌视图投影和真实意图消费者。
        /// 输入、卡牌状态、视图投影和消费者缺一时拒绝启用，避免生成空事件或第二输入链路。
        /// </summary>
        public void Bind(
            CoreInputSystem inputSystem,
            TabletopCardState state,
            TabletopCardViewProjector viewProjector,
            Action<TabletopCardPointerReleaseIntent> releaseHandler)
        {
            if (inputSystem == null)
            {
                throw new ArgumentNullException(nameof(inputSystem));
            }

            if (viewProjector == null)
            {
                throw new ArgumentNullException(nameof(viewProjector));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (releaseHandler == null)
            {
                throw new ArgumentNullException(nameof(releaseHandler));
            }

            Unsubscribe();
            CancelCurrentInteraction();
            m_inputSystem = inputSystem;
            m_state = state;
            m_viewProjector = viewProjector;
            m_releaseHandler = releaseHandler;
            m_session = new TabletopCardDragSession(m_dragStartDistance);
            SubscribeIfPossible();
        }

        /// <summary>
        /// 解除输入监听并取消当前预览，不产生释放意图。
        /// </summary>
        public void Unbind()
        {
            Unsubscribe();
            CancelCurrentInteraction();
            m_inputSystem = null;
            m_state = null;
            m_viewProjector = null;
            m_releaseHandler = null;
            m_session = null;
        }

        private void OnEnable()
        {
            // Bind 可能发生在组件启用前或启用后，两条生命周期路径都汇入同一幂等订阅入口。
            SubscribeIfPossible();
        }

        private void OnDisable()
        {
            // 禁用期间不能保留输入监听或视觉预览，否则重新启用会重复发送释放意图。
            Unsubscribe();
            CancelCurrentInteraction();
        }

        private void Update()
        {
            if (m_session?.IsActive != true || !TryReadTablePosition(out Vector2 tablePosition))
            {
                return;
            }

            if (m_session.Update(tablePosition))
            {
                m_viewProjector.SetDragPreview(m_session.CardId, tablePosition);
                UpdateDropTarget();
            }
        }

        private void SubscribeIfPossible()
        {
            if (!isActiveAndEnabled || m_isSubscribed || m_inputSystem == null)
            {
                return;
            }

            m_inputSystem.AddGameplayActionListener(
                EGameplayInputAction.Click,
                EInputActionPhase.Started,
                OnPrimaryPointerStarted);
            m_inputSystem.AddGameplayActionListener(
                EGameplayInputAction.Click,
                EInputActionPhase.Canceled,
                OnPrimaryPointerCanceled);
            m_isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_isSubscribed)
            {
                return;
            }

            CoreInputSystem inputSystem = m_inputSystem;
            m_isSubscribed = false;
            if (inputSystem == null)
            {
                return;
            }

            inputSystem.RemoveGameplayActionListener(
                EGameplayInputAction.Click,
                EInputActionPhase.Started,
                OnPrimaryPointerStarted);
            inputSystem.RemoveGameplayActionListener(
                EGameplayInputAction.Click,
                EInputActionPhase.Canceled,
                OnPrimaryPointerCanceled);
        }

        private void OnPrimaryPointerStarted(InputAction.CallbackContext _)
        {
            if (m_inputSystem == null ||
                m_inputSystem.IsGameplayActionBlocked(EGameplayInputAction.Click) ||
                m_session == null || m_session.IsActive ||
                !TryReadTablePosition(out Vector2 tablePosition) ||
                !TryHitCardView(out TabletopCardView cardView))
            {
                return;
            }

            m_session.Begin(cardView.CardId, tablePosition);
        }

        private void OnPrimaryPointerCanceled(InputAction.CallbackContext _)
        {
            if (m_session?.IsActive != true)
            {
                return;
            }

            Vector2 releasePosition = TryReadTablePosition(out Vector2 tablePosition)
                ? tablePosition
                : m_session.CurrentPosition;
            UpdateDropTarget();
            TabletopCardPointerReleaseIntent intent = m_session.End(
                releasePosition,
                m_currentTargetCardId);
            m_viewProjector.ClearDragPreview();
            m_viewProjector.SetDropTargetHighlight(default);
            m_currentTargetCardId = default;
            m_releaseHandler.Invoke(intent);
        }

        private bool TryReadTablePosition(out Vector2 tablePosition)
        {
            Camera camera = m_tabletopCamera != null ? m_tabletopCamera : Camera.main;
            Transform tablePlane = m_tablePlane != null ? m_tablePlane : transform;
            if (camera == null || m_inputSystem == null)
            {
                tablePosition = default;
                return false;
            }

            Vector2 screenPosition = m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
            Ray ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(tablePlane.forward, tablePlane.position);
            if (!plane.Raycast(ray, out float distance))
            {
                tablePosition = default;
                return false;
            }

            Vector3 localPoint = tablePlane.InverseTransformPoint(ray.GetPoint(distance));
            tablePosition = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        private bool TryHitCardView(out TabletopCardView cardView)
        {
            return TryHitCardView(excludedStack: null, out cardView);
        }

        private bool TryHitCardView(TabletopCardStack excludedStack, out TabletopCardView cardView)
        {
            Camera camera = m_tabletopCamera != null ? m_tabletopCamera : Camera.main;
            if (camera == null || m_inputSystem == null)
            {
                cardView = null;
                return false;
            }

            Vector2 screenPosition = m_inputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
            Ray ray = camera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Max(0.01f, m_maxHitDistance),
                m_cardHitMask,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                TabletopCardView found = hits[i].collider.GetComponentInParent<TabletopCardView>();
                if (found == null || !found.CardId.IsValid)
                {
                    continue;
                }

                if (excludedStack != null &&
                    m_state.TryGetStackContaining(found.CardId, out TabletopCardStack foundStack) &&
                    ReferenceEquals(excludedStack, foundStack))
                {
                    continue;
                }

                cardView = found;
                return true;
            }

            cardView = null;
            return false;
        }

        private void UpdateDropTarget()
        {
            if (m_session?.IsDragging != true ||
                m_state == null ||
                !m_state.TryGetStackContaining(m_session.CardId, out TabletopCardStack sourceStack) ||
                !TryHitCardView(sourceStack, out TabletopCardView targetView))
            {
                m_currentTargetCardId = default;
                m_viewProjector.SetDropTargetHighlight(default);
                return;
            }

            m_currentTargetCardId = targetView.CardId;
            m_viewProjector.SetDropTargetHighlight(m_currentTargetCardId);
        }

        private void CancelCurrentInteraction()
        {
            if (m_session?.IsActive == true)
            {
                m_session.Cancel();
            }

            m_viewProjector?.ClearDragPreview();
            m_viewProjector?.SetDropTargetHighlight(default);
            m_currentTargetCardId = default;
        }
    }
}
