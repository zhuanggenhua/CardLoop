using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 剧本内嵌的牌桌放置作者规则；每次单局开始时据此创建一份不可变运行时规则。
	/// </summary>
	[Serializable]
	public sealed class TabletopCardPlacementDefinition
	{
		[SerializeField]
		[LabelText("牌桌边界")]
		[Tooltip("牌桌中允许卡牌完整占据的二维边界。")]
		private Rect m_bounds = new Rect(-5f, -3f, 10f, 6f);

		[SerializeField]
		[LabelText("禁放区域")]
		[Tooltip("牌桌边界内禁止卡牌占据的区域，例如固定 HUD 或特殊工位保留区。")]
		private Rect[] m_restrictedAreas = Array.Empty<Rect>();

		[SerializeField]
		[LabelText("卡牌尺寸")]
		[Tooltip("单张卡牌在牌桌上的可见宽高。StackCraft 角色卡原始可见尺寸是 0.8 × 1.0。")]
		private Vector2 m_cardSize = new Vector2(0.8f, 1f);

		[SerializeField]
		[LabelText("卡牌占地边距")]
		[Tooltip("只参与放置解算的额外占地，不拉伸卡面表现。StackCraft 默认 margin 是 0.1 × 0.1。")]
		private Vector2 m_cardMargin = new Vector2(0.1f, 0.1f);

		[SerializeField]
		[LabelText("堆叠步进")]
		[Tooltip("同一牌堆每增加一张卡牌时，规则占地在 XY 平面的偏移。当前 2D 牌桌用 Y 轴承接 StackCraft 的 Z 轴 -0.18 露出。")]
		private Vector2 m_stackStep = new Vector2(0f, -0.18f);

		[SerializeField]
		[LabelText("上限加成扩展")]
		[Tooltip("牌桌上每 1 点卡牌上限加成会让可放置边界向左右和上下各扩展的距离。0 表示该地区不随上限加成扩展牌桌。")]
		private Vector2 m_cardLimitBonusExpansionPerPoint = new Vector2(0.05f, 0.05f);

		public TabletopCardPlacementRules CreateRuntime()
		{
			return new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(m_bounds, m_restrictedAreas),
				new TabletopCardStackGeometry(m_cardSize, m_stackStep, m_cardMargin),
				m_cardLimitBonusExpansionPerPoint);
		}
	}

	/// <summary>
	/// 牌桌可放置边界及禁放区域的运行时规则值。
	/// </summary>
	public sealed class TabletopCardPlacementArea
	{
		private readonly ReadOnlyCollection<Rect> m_restrictedAreas;

		public Rect Bounds { get; }

		public IReadOnlyList<Rect> RestrictedAreas => m_restrictedAreas;

		public TabletopCardPlacementArea(Rect bounds, IReadOnlyList<Rect> restrictedAreas = null)
		{
			if (!IsFinite(bounds) || bounds.width <= 0f || bounds.height <= 0f)
			{
				throw new ArgumentException("牌桌可放置边界必须具有有限坐标和正数宽高。", "bounds");
			}
			Bounds = bounds;
			List<Rect> copiedRestrictedAreas = new List<Rect>(restrictedAreas ?? Array.Empty<Rect>());
			for (int i = 0; i < copiedRestrictedAreas.Count; i++)
			{
				Rect restrictedArea = copiedRestrictedAreas[i];
				if (!IsFinite(restrictedArea) || restrictedArea.width <= 0f || restrictedArea.height <= 0f)
				{
					throw new ArgumentException("禁放区域必须具有有限坐标和正数宽高。", "restrictedAreas");
				}
			}
			m_restrictedAreas = copiedRestrictedAreas.AsReadOnly();
		}

		private static bool IsFinite(Rect rect)
		{
			return float.IsFinite(rect.x) && float.IsFinite(rect.y) && float.IsFinite(rect.width) && float.IsFinite(rect.height);
		}
	}

	/// <summary>
	/// 整堆空间解算使用的卡牌尺寸与堆叠步进。
	/// </summary>
	public readonly struct TabletopCardStackGeometry
	{
		public Vector2 CardSize { get; }

		public Vector2 CardMargin { get; }

		public Vector2 FootprintSize { get; }

		public Vector2 StackStep { get; }

		internal bool IsValid =>
			CardSize.x > 0f &&
			CardSize.y > 0f &&
			CardMargin.x >= 0f &&
			CardMargin.y >= 0f &&
			IsFinite(CardSize) &&
			IsFinite(CardMargin) &&
			IsFinite(StackStep);

		public TabletopCardStackGeometry(Vector2 cardSize, Vector2 stackStep)
			: this(cardSize, stackStep, Vector2.zero)
		{
		}

		public TabletopCardStackGeometry(Vector2 cardSize, Vector2 stackStep, Vector2 cardMargin)
		{
			if (!IsFinite(cardSize) || cardSize.x <= 0f || cardSize.y <= 0f)
			{
				throw new ArgumentException("牌桌卡牌尺寸必须具有有限坐标和正数宽高。", "cardSize");
			}
			if (!IsFinite(cardMargin) || cardMargin.x < 0f || cardMargin.y < 0f)
			{
				throw new ArgumentException("牌桌卡牌占地边距必须具有有限坐标且不能为负数。", "cardMargin");
			}
			if (!IsFinite(stackStep))
			{
				throw new ArgumentException("牌桌堆叠步进必须是有限二维坐标。", "stackStep");
			}
			CardSize = cardSize;
			CardMargin = cardMargin;
			FootprintSize = cardSize + cardMargin;
			StackStep = stackStep;
		}

		public Rect CalculateFootprint(Vector2 stackPosition, int cardCount)
		{
			if (!IsFinite(stackPosition))
			{
				throw new ArgumentException("堆栈锚点必须是有限二维坐标。", "stackPosition");
			}
			if (cardCount <= 0)
			{
				throw new ArgumentOutOfRangeException("cardCount", "空间占地至少需要一张卡牌。");
			}
			Vector2 span = StackStep * (cardCount - 1);
			Vector2 center = stackPosition + span * 0.5f;
			Vector2 size = FootprintSize + new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));
			return new Rect(center - size * 0.5f, size);
		}

		internal TabletopCardStackSpatialBody CreateSpatialBody(TabletopCardId bottomCardId, Vector2 stackPosition, int cardCount, bool isLocked)
		{
			Rect footprint = CalculateFootprint(stackPosition, cardCount);
			return new TabletopCardStackSpatialBody(bottomCardId, stackPosition, footprint.center - stackPosition, footprint.size, isLocked);
		}

		private static bool IsFinite(Vector2 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y);
		}
	}

	/// <summary>
	/// 一次牌桌运行使用的边界和整堆几何规则。
	/// </summary>
	public sealed class TabletopCardPlacementRules
	{
		public TabletopCardPlacementArea Area { get; }

		public TabletopCardStackGeometry Geometry { get; }

		public Vector2 CardLimitBonusExpansionPerPoint { get; }

		public TabletopCardPlacementRules(
			TabletopCardPlacementArea area,
			TabletopCardStackGeometry geometry,
			Vector2 cardLimitBonusExpansionPerPoint = default)
		{
			Area = area ?? throw new ArgumentNullException("area");
			if (!geometry.IsValid)
			{
				throw new ArgumentException("牌桌放置规则缺少有效的堆栈几何。", "geometry");
			}
			if (!IsFinite(cardLimitBonusExpansionPerPoint) ||
				cardLimitBonusExpansionPerPoint.x < 0f ||
				cardLimitBonusExpansionPerPoint.y < 0f)
			{
				throw new ArgumentException(
					"卡牌上限加成的牌桌扩展必须是有限且大于或等于 0 的二维数值。",
					nameof(cardLimitBonusExpansionPerPoint));
			}
			Geometry = geometry;
			CardLimitBonusExpansionPerPoint = cardLimitBonusExpansionPerPoint;
		}

		public TabletopCardPlacementRules CreateForCardLimitBonus(int cardLimitBonus)
		{
			if (cardLimitBonus < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(cardLimitBonus),
					cardLimitBonus,
					"牌桌卡牌上限加成不能为负数。");
			}
			if (cardLimitBonus == 0 ||
				(CardLimitBonusExpansionPerPoint.x == 0f && CardLimitBonusExpansionPerPoint.y == 0f))
			{
				return this;
			}

			Vector2 expansion = CardLimitBonusExpansionPerPoint * cardLimitBonus;
			Rect bounds = Area.Bounds;
			Rect expandedBounds = new Rect(
				bounds.xMin - expansion.x,
				bounds.yMin - expansion.y,
				bounds.width + expansion.x * 2f,
				bounds.height + expansion.y * 2f);
			return new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(expandedBounds, Area.RestrictedAreas),
				Geometry,
				CardLimitBonusExpansionPerPoint);
		}

		private static bool IsFinite(Vector2 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y);
		}
	}

	/// <summary>
	/// 整堆放置解算器使用的内部空间体，不作为作者源或第二状态。
	/// </summary>
	internal readonly struct TabletopCardStackSpatialBody
	{
		public TabletopCardId BottomCardId { get; }

		public Vector2 Position { get; }

		public Vector2 FootprintCenterOffset { get; }

		public Vector2 Size { get; }

		public bool IsLocked { get; }

		internal Vector2 FootprintCenter => Position + FootprintCenterOffset;

		internal TabletopCardStackSpatialBody(TabletopCardId bottomCardId, Vector2 position, Vector2 footprintCenterOffset, Vector2 size, bool isLocked)
		{
			if (!bottomCardId.IsValid)
			{
				throw new ArgumentException("空间堆栈必须使用有效的底牌局内 ID。", "bottomCardId");
			}
			if (!IsFinite(position) || !IsFinite(footprintCenterOffset) || !IsFinite(size) || size.x <= 0f || size.y <= 0f)
			{
				throw new ArgumentException("空间堆栈必须具有有限锚点、中心偏移和正数占地。", "size");
			}
			BottomCardId = bottomCardId;
			Position = position;
			FootprintCenterOffset = footprintCenterOffset;
			Size = size;
			IsLocked = isLocked;
		}

		internal TabletopCardStackSpatialBody WithPosition(Vector2 position)
		{
			return new TabletopCardStackSpatialBody(BottomCardId, position, FootprintCenterOffset, Size, IsLocked);
		}

		private static bool IsFinite(Vector2 value)
		{
			return float.IsFinite(value.x) && float.IsFinite(value.y);
		}
	}

	/// <summary>
	/// 整堆放置解算得到的候选位置集合。
	/// </summary>
	internal sealed class TabletopCardStackSpatialResult
	{
		private readonly ReadOnlyCollection<TabletopCardStackSpatialBody> m_bodies;

		public IReadOnlyList<TabletopCardStackSpatialBody> Bodies => m_bodies;

		public int Iterations { get; }

		public bool Converged { get; }

		internal TabletopCardStackSpatialResult(IReadOnlyList<TabletopCardStackSpatialBody> bodies, int iterations, bool converged)
		{
			m_bodies = new List<TabletopCardStackSpatialBody>(bodies).AsReadOnly();
			Iterations = iterations;
			Converged = converged;
		}

		public Vector2 GetPosition(TabletopCardId bottomCardId)
		{
			for (int i = 0; i < m_bodies.Count; i++)
			{
				if (m_bodies[i].BottomCardId == bottomCardId)
				{
					return m_bodies[i].Position;
				}
			}
			throw new KeyNotFoundException($"空间解算结果中不存在底牌为 {bottomCardId} 的堆栈。");
		}
	}
}
