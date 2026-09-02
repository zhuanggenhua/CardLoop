using UnityEngine;
using System;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 卡牌视图的局部位置与排序结果。
	/// </summary>
	public readonly struct TabletopCardPose
	{
		public Vector3 LocalPosition { get; }

		public int SortingOrder { get; }

		public TabletopCardPose(Vector3 localPosition, int sortingOrder)
		{
			LocalPosition = localPosition;
			SortingOrder = sortingOrder;
		}
	}

	/// <summary>
	/// 卡牌堆在表现层中的偏移和排序参数。
	/// </summary>
	public readonly struct TabletopCardLayoutParameters
	{
		public Vector3 StackVisualStep { get; }

		public int BaseSortingOrder { get; }

		public TabletopCardLayoutParameters(Vector3 stackVisualStep, int baseSortingOrder)
		{
			if (!float.IsFinite(stackVisualStep.x) || !float.IsFinite(stackVisualStep.y) || !float.IsFinite(stackVisualStep.z))
			{
				throw new ArgumentException("牌堆视觉步进必须是有限坐标。", "stackVisualStep");
			}
			StackVisualStep = stackVisualStep;
			BaseSortingOrder = baseSortingOrder;
		}
	}

	/// <summary>
	/// 根据权威牌堆顺序计算卡牌视图姿态的纯布局规则。
	/// </summary>
	public static class TabletopCardLayout
	{
		public static TabletopCardPose Calculate(TabletopCardStack stack, int cardIndex, TabletopCardLayoutParameters parameters)
		{
			if (stack == null)
			{
				throw new ArgumentNullException("stack");
			}
			if (cardIndex < 0 || cardIndex >= stack.Cards.Count)
			{
				throw new ArgumentOutOfRangeException("cardIndex", "卡牌成员索引必须落在当前堆栈成员范围内。");
			}
			return Calculate(stack.Position, cardIndex, parameters);
		}

		public static TabletopCardPose Calculate(Vector2 stackPosition, int cardIndex, TabletopCardLayoutParameters parameters)
		{
			if (cardIndex < 0)
			{
				throw new ArgumentOutOfRangeException("cardIndex", "卡牌成员索引不能为负数。");
			}
			Vector3 stackBasePosition = TabletopCoordinateSpace.ToLocalPosition(stackPosition);
			// StackCraft 的卡牌 Prefab 不用堆内递增 SortingOrder 表达牌间遮挡；
			// 牌间层级由 Transform 高度和 StackStep 的 Z 偏移共同决定。
			return new TabletopCardPose(
				stackBasePosition + parameters.StackVisualStep * cardIndex,
				parameters.BaseSortingOrder);
		}
	}
}
