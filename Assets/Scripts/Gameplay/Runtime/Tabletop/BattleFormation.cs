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

		public IReadOnlyList<BattleSideFormationRules> SideLayouts =>
			m_sideLayouts ?? Array.Empty<BattleSideFormationRules>();

		public bool IsConfigured => SideLayouts.Count > 0;

		public BattleFormationRules()
		{
		}

		public BattleFormationRules(params BattleSideFormationRules[] sideLayouts)
		{
			m_sideLayouts = sideLayouts ?? throw new ArgumentNullException(nameof(sideLayouts));
		}

		internal BattleFormation CreateRuntime()
		{
			return IsConfigured ? new BattleFormation(SideLayouts) : null;
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

		internal BattleFormation(IReadOnlyList<BattleSideFormationRules> sideLayouts)
		{
			if (sideLayouts == null)
			{
				throw new ArgumentNullException(nameof(sideLayouts));
			}
			if (sideLayouts.Count < 2)
			{
				throw new InvalidOperationException("战斗阵型至少需要两个战斗方队列。");
			}

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
			Vector2 position = CalculateBattleAnchor(battle.Sides, cards) +
				group.CenterOffset +
				group.ColumnStep * centeredColumnIndex +
				group.RankStep * rankIndex;
			pose = new TabletopCardPose(
				new Vector3(position.x, position.y, 0f),
				checked(baseSortingOrder + CalculateParticipantOrder(battle.Sides, sideIndex, indexInSide)));
			return true;
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

		private static Vector2 CalculateBattleAnchor(
			IReadOnlyList<BattleSide> sides,
			TabletopCards cards)
		{
			Vector2 totalPosition = Vector2.zero;
			int participantCount = 0;
			for (int sideIndex = 0; sideIndex < sides.Count; sideIndex++)
			{
				IReadOnlyList<TabletopCardId> cardIds = sides[sideIndex].CardIds;
				for (int cardIndex = 0; cardIndex < cardIds.Count; cardIndex++)
				{
					totalPosition += cards.GetStackContaining(cardIds[cardIndex]).Position;
					participantCount++;
				}
			}
			return totalPosition / participantCount;
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
	}
}
