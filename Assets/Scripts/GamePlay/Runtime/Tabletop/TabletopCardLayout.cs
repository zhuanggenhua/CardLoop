using System;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 单张牌在牌桌视图中的局部姿态。
    /// 位置和层级由权威卡牌状态与堆栈顺序计算，视图不反向保存这些数据。
    /// </summary>
    public readonly struct TabletopCardPose
    {
        /// <summary>
        /// 创建一张卡牌的表现层姿态。该值不会写回牌桌运行时状态。
        /// </summary>
        public TabletopCardPose(Vector3 localPosition, int sortingOrder)
        {
            LocalPosition = localPosition;
            SortingOrder = sortingOrder;
        }

        /// <summary>相对于牌桌视图根节点的局部位置。</summary>
        public Vector3 LocalPosition { get; }

        /// <summary>应用到卡牌 SpriteRenderer 的渲染排序值。</summary>
        public int SortingOrder { get; }
    }

    /// <summary>
    /// 牌桌卡牌布局的不可变参数。
    /// 它只描述表现层的错位和层级，不参与碰撞、合堆或玩法规则计算。
    /// </summary>
    public readonly struct TabletopCardLayoutParameters
    {
        /// <summary>
        /// 创建布局参数。三维步进允许按相机方向配置正负深度，但所有分量都必须是有限值。
        /// </summary>
        public TabletopCardLayoutParameters(
            Vector3 stackVisualStep,
            int baseSortingOrder)
        {
            if (!float.IsFinite(stackVisualStep.x) ||
                !float.IsFinite(stackVisualStep.y) ||
                !float.IsFinite(stackVisualStep.z))
            {
                throw new ArgumentException("牌堆视觉步进必须是有限坐标。", nameof(stackVisualStep));
            }

            StackVisualStep = stackVisualStep;
            BaseSortingOrder = baseSortingOrder;
        }

        /// <summary>
        /// 同一堆栈中每向顶部一张卡牌增加的局部三维偏移。
        /// </summary>
        public Vector3 StackVisualStep { get; }

        /// <summary>
        /// 堆栈底部卡牌使用的渲染排序起点。
        /// </summary>
        public int BaseSortingOrder { get; }
    }

    /// <summary>
    /// 将牌桌二维状态转换为卡牌视图姿态的纯计算入口。
    /// </summary>
    public static class TabletopCardLayout
    {
        /// <summary>
        /// 计算指定堆栈成员的局部位置和渲染顺序。
        /// 成员索引从底部开始为 0；越靠顶部，视觉错位和排序值越大。
        /// </summary>
        public static TabletopCardPose Calculate(
            TabletopCardStack stack,
            int cardIndex,
            TabletopCardLayoutParameters parameters)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            if (cardIndex < 0 || cardIndex >= stack.Cards.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cardIndex),
                    "卡牌成员索引必须落在当前堆栈成员范围内。");
            }

            return Calculate(stack.Position, cardIndex, parameters);
        }

        /// <summary>
        /// 以给定牌桌二维位置作为临时底座计算视图姿态。
        /// 该入口供拖拽预览使用，不会创建或修改正式堆栈。
        /// </summary>
        public static TabletopCardPose Calculate(
            Vector2 stackPosition,
            int cardIndex,
            TabletopCardLayoutParameters parameters)
        {
            if (cardIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cardIndex), "卡牌成员索引不能为负数。");
            }

            Vector3 stackBasePosition = new(stackPosition.x, stackPosition.y, 0f);
            return new TabletopCardPose(
                stackBasePosition + parameters.StackVisualStep * cardIndex,
                checked(parameters.BaseSortingOrder + cardIndex));
        }
    }
}
