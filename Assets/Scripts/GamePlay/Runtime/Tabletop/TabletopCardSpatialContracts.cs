using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 可堆叠卡牌的放置边界和禁放区域。全部坐标使用牌桌二维坐标，不依赖场景 Transform。
    /// </summary>
    public sealed class TabletopCardPlacementArea
    {
        private readonly ReadOnlyCollection<Rect> m_restrictedAreas;

        /// <summary>
        /// 创建一个卡牌二维放置区域，并复制禁放矩形作为不可变配置。
        /// 边界或禁放区域包含非有限坐标、零宽或零高时抛出异常。
        /// </summary>
        public TabletopCardPlacementArea(Rect bounds, IReadOnlyList<Rect> restrictedAreas = null)
        {
            if (!IsFinite(bounds) || bounds.width <= 0f || bounds.height <= 0f)
            {
                throw new ArgumentException("牌桌可放置边界必须具有正数宽高。", nameof(bounds));
            }

            Bounds = bounds;
            var copiedRestrictedAreas = new List<Rect>(restrictedAreas ?? Array.Empty<Rect>());
            for (int i = 0; i < copiedRestrictedAreas.Count; i++)
            {
                Rect restrictedArea = copiedRestrictedAreas[i];
                if (!IsFinite(restrictedArea) || restrictedArea.width <= 0f || restrictedArea.height <= 0f)
                {
                    throw new ArgumentException("禁放区域必须具有有限坐标和正数宽高。", nameof(restrictedAreas));
                }
            }

            m_restrictedAreas = copiedRestrictedAreas.AsReadOnly();
        }

        /// <summary>允许卡牌中心和完整占地进入的牌桌总边界。</summary>
        public Rect Bounds { get; }

        /// <summary>卡牌完整占地不得重叠的只读禁放区域。</summary>
        public IReadOnlyList<Rect> RestrictedAreas => m_restrictedAreas;

        private static bool IsFinite(Rect rect)
        {
            return
                float.IsFinite(rect.x) &&
                float.IsFinite(rect.y) &&
                float.IsFinite(rect.width) &&
                float.IsFinite(rect.height);
        }
    }

    /// <summary>
    /// 参与牌桌空间解算的只读卡牌占地。
    /// </summary>
    public readonly struct TabletopCardSpatialBody
    {
        /// <summary>
        /// 创建一次解算使用的卡牌占地快照。局内卡牌 ID、位置和尺寸必须有效；
        /// 锁定卡牌参与阻挡和收敛检查，但解算器不会移动它。
        /// </summary>
        public TabletopCardSpatialBody(
            TabletopCardId cardId,
            Vector2 position,
            Vector2 size,
            bool isLocked)
        {
            if (!cardId.IsValid)
            {
                throw new ArgumentException("空间卡牌必须使用有效的局内卡牌 ID。", nameof(cardId));
            }

            if (!IsFinite(position) || !IsFinite(size) || size.x <= 0f || size.y <= 0f)
            {
                throw new ArgumentException("空间卡牌必须具有有限坐标和正数占地。", nameof(size));
            }

            CardId = cardId;
            Position = position;
            Size = size;
            IsLocked = isLocked;
        }

        /// <summary>空间占地对应的局内卡牌身份。</summary>
        public TabletopCardId CardId { get; }

        /// <summary>卡牌完整占地的中心点，使用牌桌二维坐标。</summary>
        public Vector2 Position { get; }

        /// <summary>卡牌完整占地的宽高，不是半径或半尺寸。</summary>
        public Vector2 Size { get; }

        /// <summary>是否作为不可移动约束参与本次解算。</summary>
        public bool IsLocked { get; }

        /// <summary>
        /// 复制当前占地并替换位置，供纯解算过程生成新快照；不会修改原值。
        /// </summary>
        internal TabletopCardSpatialBody WithPosition(Vector2 position)
        {
            return new TabletopCardSpatialBody(CardId, position, Size, IsLocked);
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }

    /// <summary>
    /// 一次空间解算的不可变结果。
    /// </summary>
    public sealed class TabletopCardSpatialResult
    {
        private readonly ReadOnlyCollection<TabletopCardSpatialBody> m_bodies;

        /// <summary>
        /// 保存一次解算的快照。输入集合会被复制，后续调用方修改原列表不会改变结果。
        /// </summary>
        internal TabletopCardSpatialResult(
            IReadOnlyList<TabletopCardSpatialBody> bodies,
            int iterations,
            bool converged)
        {
            m_bodies = new List<TabletopCardSpatialBody>(bodies).AsReadOnly();
            Iterations = iterations;
            Converged = converged;
        }

        /// <summary>按解算器稳定顺序保存的最终卡牌占地。</summary>
        public IReadOnlyList<TabletopCardSpatialBody> Bodies => m_bodies;

        /// <summary>本次解算实际执行的迭代轮数。</summary>
        public int Iterations { get; }

        /// <summary>所有卡牌是否已经满足边界、禁放区和互不重叠约束。</summary>
        public bool Converged { get; }

        /// <summary>
        /// 返回指定局内卡牌的解算后位置。输入结果中没有该卡牌时抛出异常，
        /// 防止调用方把缺失结果误当成坐标原点写回卡牌状态。
        /// </summary>
        public Vector2 GetPosition(TabletopCardId cardId)
        {
            for (int i = 0; i < m_bodies.Count; i++)
            {
                if (m_bodies[i].CardId == cardId)
                {
                    return m_bodies[i].Position;
                }
            }

            throw new KeyNotFoundException($"空间解算结果中不存在局内卡牌 {cardId}。");
        }
    }
}
