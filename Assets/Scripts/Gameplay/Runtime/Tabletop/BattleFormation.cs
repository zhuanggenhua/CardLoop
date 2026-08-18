using System;
using System.Collections.Generic;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 由剧本持有的战斗方阵型作者规则，只定义本场各方的表现队列。
	/// </summary>
	[Serializable]
	public sealed class BattleFormationRules
	{
		[SerializeField]
		[ListDrawerSettings]
		[LabelText("战斗方队列")]
		[Tooltip("按战斗方顺序定义表现队列；角色的 GAS 阵营与敌我关系不复制到阵型配置。")]
		private BattleSideFormationRules[] m_sideLayouts = Array.Empty<BattleSideFormationRules>();

		[SerializeField]
		[LabelText("战斗区域边距")]
		[Tooltip("战斗区域在卡牌阵列之外保留的二维边距，也用于判断相邻战斗区域是否重叠。")]
		private Vector2 m_areaMargin = new Vector2(0.1f, 0.1f);

		public IReadOnlyList<BattleSideFormationRules> SideLayouts =>
			m_sideLayouts ?? Array.Empty<BattleSideFormationRules>();

		public bool IsConfigured => SideLayouts.Count > 0;

		public Vector2 AreaMargin => m_areaMargin;

		public BattleFormationRules()
		{
		}

		public BattleFormationRules(params BattleSideFormationRules[] sideLayouts)
		{
			m_sideLayouts = sideLayouts ?? throw new ArgumentNullException(nameof(sideLayouts));
		}

		internal BattleFormation CreateRuntime()
		{
			return IsConfigured ? new BattleFormation(SideLayouts, AreaMargin) : null;
		}

		internal void ValidateContent(ContentValidationContext context, ContentAsset source)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}
			if (!IsConfigured)
			{
				return;
			}
			if (SideLayouts.Count < 2)
			{
				context.AddError(
					"BATTLE_FORMATION_SIDE_COUNT_INVALID",
					"战斗阵型至少需要两个战斗方队列。",
					source);
			}
			if (!IsFinite(AreaMargin) || AreaMargin.x < 0f || AreaMargin.y < 0f)
			{
				context.AddError(
					"BATTLE_AREA_MARGIN_INVALID",
					"战斗区域边距必须是大于或等于 0 的有限二维数值。",
					source);
			}

			for (int index = 0; index < SideLayouts.Count; index++)
			{
				BattleSideFormationRules layout = SideLayouts[index];
				if (layout == null)
				{
					context.AddError(
						"BATTLE_FORMATION_LAYOUT_NULL",
						$"战斗阵型的第 {index + 1} 个战斗方队列为空。",
						source);
					continue;
				}

				string error = layout.GetValidationError();
				if (!string.IsNullOrEmpty(error))
				{
					context.AddError(
						"BATTLE_FORMATION_LAYOUT_INVALID",
						$"战斗阵型的第 {index + 1} 个战斗方队列无效：{error}",
						source);
				}
			}
		}

		private static bool IsFinite(Vector2 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y);
		}
	}

	/// <summary>一个战斗方在阵型中的自动排布规则。</summary>
	[Serializable]
	public sealed class BattleSideFormationRules
	{
		[SerializeField]
		[LabelText("队列中心偏移")]
		[Tooltip("相对于本次参战卡牌平均桌面位置的队列中心偏移。")]
		private Vector2 m_centerOffset;

		[SerializeField]
		[LabelText("同排步进")]
		[Tooltip("同一排相邻卡牌的二维偏移。每排容量大于 1 时不能为零。")]
		private Vector2 m_columnStep = Vector2.right;

		[SerializeField]
		[LabelText("下一排步进")]
		[Tooltip("队列超出每排容量后进入下一排的二维偏移。")]
		private Vector2 m_rankStep = Vector2.up;

		[SerializeField]
		[Min(1f)]
		[LabelText("每排容量")]
		[Tooltip("一排最多容纳的参与者数量，超过后按下一排步进自动扩展。")]
		private int m_columnsPerRank = 1;

		public Vector2 CenterOffset => m_centerOffset;

		public Vector2 ColumnStep => m_columnStep;

		public Vector2 RankStep => m_rankStep;

		public int ColumnsPerRank => m_columnsPerRank;

		public BattleSideFormationRules()
		{
		}

		public BattleSideFormationRules(
			Vector2 centerOffset,
			Vector2 columnStep,
			Vector2 rankStep,
			int columnsPerRank)
		{
			m_centerOffset = centerOffset;
			m_columnStep = columnStep;
			m_rankStep = rankStep;
			m_columnsPerRank = columnsPerRank;
		}

		internal string GetValidationError()
		{
			if (!IsFinite(CenterOffset) || !IsFinite(ColumnStep) || !IsFinite(RankStep))
			{
				return "队列中心偏移和排布步进必须是有限二维坐标。";
			}
			if (ColumnsPerRank <= 0)
			{
				return "每排容量必须大于 0。";
			}
			if (ColumnsPerRank > 1 && ColumnStep.sqrMagnitude <= 0.000001f)
			{
				return "每排容量大于 1 时，同排步进不能为零。";
			}
			if (RankStep.sqrMagnitude <= 0.000001f)
			{
				return "下一排步进不能为零。";
			}
			return null;
		}

		private static bool IsFinite(Vector2 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y);
		}
	}

	/// <summary>牌桌内部的不可变阵型，只从战斗方和现有卡牌位置派生表现姿态。</summary>
	internal sealed class BattleFormation
	{
		private readonly Group[] m_groups;
		private readonly Vector2 m_areaMargin;

		internal BattleFormation(
			IReadOnlyList<BattleSideFormationRules> sideLayouts,
			Vector2 areaMargin)
		{
			if (sideLayouts == null)
			{
				throw new ArgumentNullException(nameof(sideLayouts));
			}
			if (sideLayouts.Count < 2)
			{
				throw new InvalidOperationException("战斗阵型至少需要两个战斗方队列。");
			}
			if (!IsFinite(areaMargin) || areaMargin.x < 0f || areaMargin.y < 0f)
			{
				throw new InvalidOperationException("战斗区域边距必须是大于或等于 0 的有限二维数值。");
			}

			m_areaMargin = areaMargin;
			m_groups = new Group[sideLayouts.Count];
			for (int index = 0; index < sideLayouts.Count; index++)
			{
				BattleSideFormationRules layout = sideLayouts[index];
				if (layout == null)
				{
					throw new InvalidOperationException($"战斗阵型的第 {index + 1} 个战斗方队列为空。");
				}
				string error = layout.GetValidationError();
				if (!string.IsNullOrEmpty(error))
				{
					throw new InvalidOperationException(
						$"战斗阵型的第 {index + 1} 个战斗方队列无效：{error}");
				}
				m_groups[index] = new Group(layout);
			}
		}

		internal void ValidateBattle(Battle battle)
		{
			if (battle == null)
			{
				throw new ArgumentNullException(nameof(battle));
			}
			if (battle.SideCount > m_groups.Length)
			{
				throw new InvalidOperationException(
					$"当前战斗包含 {battle.SideCount} 个战斗方，但剧本阵型只配置了 {m_groups.Length} 个队列。");
			}
		}

		internal bool TryCalculatePose(
			Battle battle,
			TabletopCards cards,
			TabletopCardId cardId,
			int baseSortingOrder,
			out TabletopCardPose pose)
		{
			if (battle == null)
			{
				throw new ArgumentNullException(nameof(battle));
			}
			if (cards == null)
			{
				throw new ArgumentNullException(nameof(cards));
			}

			int sideIndex = FindSideIndex(battle.Sides, cardId, out int indexInSide);
			if (sideIndex < 0)
			{
				pose = default;
				return false;
			}
			if (sideIndex >= m_groups.Length)
			{
				throw new InvalidOperationException($"战斗方 {sideIndex + 1} 没有对应的剧本阵型队列。");
			}

			BattleSide side = battle.Sides[sideIndex];
			Group group = m_groups[sideIndex];
			int rankIndex = indexInSide / group.ColumnsPerRank;
			int firstIndexInRank = rankIndex * group.ColumnsPerRank;
			int countInRank = Math.Min(group.ColumnsPerRank, side.ParticipantCount - firstIndexInRank);
			int columnIndex = indexInSide - firstIndexInRank;
			float centeredColumnIndex = columnIndex - (countInRank - 1) * 0.5f;
			Vector2 position = battle.AreaCenter +
				group.CenterOffset +
				group.ColumnStep * centeredColumnIndex +
				group.RankStep * rankIndex;
			pose = new TabletopCardPose(
				TabletopCoordinateSpace.ToLocalPosition(position),
				checked(baseSortingOrder + CalculateParticipantOrder(battle.Sides, sideIndex, indexInSide)));
			return true;
		}

		internal Rect CalculateArea(
			Battle battle,
			Vector2 cardSize,
			int additionalParticipantSideIndex = -1)
		{
			ValidateBattle(battle);
			if (!IsFinite(cardSize) || cardSize.x <= 0f || cardSize.y <= 0f)
			{
				throw new ArgumentException("战斗区域需要有效的卡牌尺寸。", nameof(cardSize));
			}
			if (additionalParticipantSideIndex < -1 ||
				additionalParticipantSideIndex >= battle.SideCount)
			{
				throw new ArgumentOutOfRangeException(nameof(additionalParticipantSideIndex));
			}

			int largestSideCount = 0;
			for (int sideIndex = 0; sideIndex < battle.SideCount; sideIndex++)
			{
				int participantCount = battle.Sides[sideIndex].ParticipantCount;
				if (sideIndex == additionalParticipantSideIndex)
				{
					participantCount++;
				}
				largestSideCount = Math.Max(largestSideCount, participantCount);
			}

			Vector2 cellSize = cardSize + m_areaMargin;
			Vector2 areaSize = new Vector2(
				cellSize.x * largestSideCount,
				cellSize.y * battle.SideCount) + m_areaMargin * 2f;
			return new Rect(battle.AreaCenter - areaSize * 0.5f, areaSize);
		}

		private static int FindSideIndex(
			IReadOnlyList<BattleSide> sides,
			TabletopCardId cardId,
			out int indexInSide)
		{
			for (int sideIndex = 0; sideIndex < sides.Count; sideIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = sides[sideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					if (cardIds[cardIndex] == cardId)
					{
						indexInSide = cardIndex;
						return sideIndex;
					}
				}
			}
			indexInSide = -1;
			return -1;
		}

		private static int CalculateParticipantOrder(
			IReadOnlyList<BattleSide> sides,
			int sideIndex,
			int indexInSide)
		{
			int order = indexInSide;
			for (int index = 0; index < sideIndex; index++)
			{
				order += sides[index].ParticipantCount;
			}
			return order;
		}

		private readonly struct Group
		{
			internal Vector2 CenterOffset { get; }
			internal Vector2 ColumnStep { get; }
			internal Vector2 RankStep { get; }
			internal int ColumnsPerRank { get; }

			internal Group(BattleSideFormationRules rules)
			{
				CenterOffset = rules.CenterOffset;
				ColumnStep = rules.ColumnStep;
				RankStep = rules.RankStep;
				ColumnsPerRank = rules.ColumnsPerRank;
			}
		}

		private static bool IsFinite(Vector2 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y);
		}
	}
}
