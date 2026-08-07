using System;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 一次主指针按下到释放产生的卡牌意图。
    /// 它只记录卡牌和二维位置，不执行拆堆、移动、合堆或业务行动。
    /// </summary>
    public readonly struct TabletopCardPointerReleaseIntent
    {
        /// <summary>
        /// 创建一次指针释放意图。目标卡牌可以无效，表示本次释放没有命中其它卡牌候选。
        /// 该值只描述输入结果，不代表规则层已经接受点击、移动、拆堆或合堆请求。
        /// </summary>
        public TabletopCardPointerReleaseIntent(
            TabletopCardId cardId,
            Vector2 pressPosition,
            Vector2 releasePosition,
            bool isDrag,
            TabletopCardId targetCardId = default)
        {
            CardId = cardId;
            PressPosition = pressPosition;
            ReleasePosition = releasePosition;
            IsDrag = isDrag;
            TargetCardId = targetCardId;
        }

        /// <summary>本次意图针对的局内卡牌。</summary>
        public TabletopCardId CardId { get; }

        /// <summary>主指针按下时的牌桌二维位置。</summary>
        public Vector2 PressPosition { get; }

        /// <summary>主指针释放时的牌桌二维位置。</summary>
        public Vector2 ReleasePosition { get; }

        /// <summary>是否曾跨过拖拽阈值；为 false 时应按点击意图解释。</summary>
        public bool IsDrag { get; }

        /// <summary>释放时命中的空间候选卡牌；没有候选时无效。</summary>
        public TabletopCardId TargetCardId { get; }
    }

    /// <summary>
    /// 与 Unity 输入设备无关的拖拽判定状态机。
    /// 它把点击阈值和指针轨迹收敛为一次释放意图，便于鼠标、触屏、回放和联机命令复用同一规则。
    /// </summary>
    public sealed class TabletopCardDragSession
    {
        private readonly float m_dragStartDistanceSquared;

        /// <summary>
        /// 创建拖拽会话。阈值使用牌桌二维坐标，不能为负数或非有限值。
        /// </summary>
        public TabletopCardDragSession(float dragStartDistance)
        {
            if (!float.IsFinite(dragStartDistance) || dragStartDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dragStartDistance),
                    "拖拽起始距离不能为负数或非有限值。");
            }

            m_dragStartDistanceSquared = dragStartDistance * dragStartDistance;
        }

        /// <summary>当前是否正在跟踪一次未释放的主指针交互。</summary>
        public bool IsActive { get; private set; }

        /// <summary>当前会话是否已经跨过拖拽阈值。</summary>
        public bool IsDragging { get; private set; }

        /// <summary>当前会话命中的局内卡牌。</summary>
        public TabletopCardId CardId { get; private set; }

        /// <summary>当前会话的按下位置。</summary>
        public Vector2 PressPosition { get; private set; }

        /// <summary>最近一次更新到的指针位置。</summary>
        public Vector2 CurrentPosition { get; private set; }

        /// <summary>
        /// 开始跟踪一张牌桌卡牌。已有会话未结束时拒绝覆盖，避免一次指针控制两张卡牌。
        /// </summary>
        public void Begin(TabletopCardId cardId, Vector2 tablePosition)
        {
            if (IsActive)
            {
                throw new InvalidOperationException("当前已有未结束的牌桌拖拽会话。");
            }

            if (!cardId.IsValid)
            {
                throw new ArgumentException("拖拽会话必须引用有效的局内卡牌。", nameof(cardId));
            }

            EnsureFinite(tablePosition, nameof(tablePosition));
            IsActive = true;
            IsDragging = false;
            CardId = cardId;
            PressPosition = tablePosition;
            CurrentPosition = tablePosition;
        }

        /// <summary>
        /// 更新当前指针位置，并返回本次更新后是否已经跨过拖拽阈值。
        /// 一旦进入拖拽态，本次会话在释放前不会退回点击态。
        /// </summary>
        public bool Update(Vector2 tablePosition)
        {
            EnsureActive();
            EnsureFinite(tablePosition, nameof(tablePosition));
            CurrentPosition = tablePosition;

            if (!IsDragging && (CurrentPosition - PressPosition).sqrMagnitude >= m_dragStartDistanceSquared)
            {
                IsDragging = true;
            }

            return IsDragging;
        }

        /// <summary>
        /// 结束会话并返回点击或拖拽释放意图。返回后会话恢复为空闲状态。
        /// </summary>
        public TabletopCardPointerReleaseIntent End(
            Vector2 tablePosition,
            TabletopCardId targetCardId = default)
        {
            Update(tablePosition);
            var result = new TabletopCardPointerReleaseIntent(
                CardId,
                PressPosition,
                CurrentPosition,
                IsDragging,
                targetCardId);
            Reset();
            return result;
        }

        /// <summary>
        /// 取消当前会话，不产生释放意图。
        /// </summary>
        public void Cancel()
        {
            Reset();
        }

        private void EnsureActive()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("当前没有可更新或结束的牌桌拖拽会话。");
            }
        }

        private static void EnsureFinite(Vector2 position, string parameterName)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
            {
                throw new ArgumentException("牌桌指针位置必须是有限坐标。", parameterName);
            }
        }

        private void Reset()
        {
            IsActive = false;
            IsDragging = false;
            CardId = default;
            PressPosition = default;
            CurrentPosition = default;
        }
    }
}
