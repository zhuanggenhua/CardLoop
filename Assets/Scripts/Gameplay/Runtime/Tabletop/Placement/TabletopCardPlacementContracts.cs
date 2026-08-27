using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GAS.Runtime;
using Gameplay.Content;
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
		[Tooltip("牌桌中允许卡牌完整占据的二维边界。默认值对齐 StackCraft Board.fbx 在 BlendShape 权重 0 时的 BakeMesh 边界。")]
		private Rect m_bounds = new Rect(-6f, -4f, 12f, 8f);

		[SerializeField]
		[LabelText("禁放区域")]
		[Tooltip("牌桌边界内禁止卡牌占据的区域，例如 StackCraft Board 顶部 1.5 单位页眉区域。")]
		private Rect[] m_restrictedAreas = new[] { new Rect(-6f, 2.5f, 12f, 1.5f) };

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
		[LabelText("重叠解算迭代次数")]
		[Tooltip("整堆重叠解算每次最多迭代次数；对齐 StackCraft Default_Card_Settings.maxIterations = 8。")]
		[Min(1)]
		private int m_overlapResolveMaxIterations = TabletopCardPlacementRules.DefaultOverlapResolveMaxIterations;

		[SerializeField]
		[LabelText("出生吸附半径")]
		[Tooltip("运行时产物出生后寻找附近同内容牌堆的半径；对齐 StackCraft Default_Card_Settings.spawnAttachRadius = 1。固定场地摆放不会自动使用该吸附。")]
		[Min(0f)]
		private float m_spawnAttachRadius = TabletopCardPlacementRules.DefaultSpawnAttachRadius;

		[SerializeField]
		[InlineProperty]
		[LabelText("合堆规则")]
		[Tooltip("玩家把卡牌拖到其它牌堆时使用的基础合堆规则；使用 EX-GAS 内容标签表达 StackCraft 的类别矩阵，不维护第二套卡牌类型枚举。")]
		private TabletopStackingRulesDefinition m_stacking = TabletopStackingRulesDefinition.CreateStackCraftDefault();

		[SerializeField]
		[LabelText("上限加成扩展")]
		[Tooltip("牌桌上每 1 点卡牌上限加成会让可放置边界向左右和上下各扩展的距离。对齐 StackCraft Board BlendShape：100 点从 12×8 扩到 24×16。")]
		private Vector2 m_cardLimitBonusExpansionPerPoint = new Vector2(0.06f, 0.04f);

		public TabletopCardPlacementRules CreateRuntime()
		{
			return new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(m_bounds, m_restrictedAreas),
				new TabletopCardStackGeometry(m_cardSize, m_stackStep, m_cardMargin),
				m_overlapResolveMaxIterations,
				m_cardLimitBonusExpansionPerPoint,
				m_spawnAttachRadius,
				m_stacking?.CreateRuntime() ?? TabletopStackingRules.Empty);
		}
	}

	/// <summary>
	/// 一条基础合堆作者规则；来源和目标都用 EX-GAS 内容标签匹配，不引入 StackCraft 的 CardCategory 枚举。
	/// </summary>
	[Serializable]
	public sealed class TabletopStackingRuleDefinition
	{
		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("拖动卡必须具有")]
		[Tooltip("拖动卡内容必须全部满足的 EX-GAS 标签；用于表达 StackCraft 矩阵左侧类别。")]
		private int[] m_requiredSourceContentTagCodes = Array.Empty<int>();

		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("目标堆底牌必须具有")]
		[Tooltip("目标牌堆底牌内容必须全部满足的 EX-GAS 标签；用于表达 StackCraft 矩阵上方类别。")]
		private int[] m_requiredTargetBottomContentTagCodes = Array.Empty<int>();

		[SerializeField]
		[LabelText("必须同内容")]
		[Tooltip("开启后，只有拖动卡和目标堆底牌引用同一内容 ID 时才允许合堆；对齐 StackCraft 的 SameDefinition。")]
		private bool m_requiresSameContent;

		public IReadOnlyList<int> RequiredSourceContentTagCodes =>
			m_requiredSourceContentTagCodes ?? Array.Empty<int>();

		public IReadOnlyList<int> RequiredTargetBottomContentTagCodes =>
			m_requiredTargetBottomContentTagCodes ?? Array.Empty<int>();

		public bool RequiresSameContent => m_requiresSameContent;

		internal TabletopStackingRuleDefinition()
		{
		}

		internal TabletopStackingRuleDefinition(
			int sourceTagCode,
			int targetBottomTagCode,
			bool requiresSameContent = false)
		{
			m_requiredSourceContentTagCodes = new[] { sourceTagCode };
			m_requiredTargetBottomContentTagCodes = new[] { targetBottomTagCode };
			m_requiresSameContent = requiresSameContent;
		}

		internal TabletopStackingRule CreateRuntime()
		{
			return new TabletopStackingRule(
				RequiredSourceContentTagCodes,
				RequiredTargetBottomContentTagCodes,
				RequiresSameContent);
		}
	}

	/// <summary>
	/// 地区牌桌的基础合堆作者规则集合；默认规则对齐 StackCraft 的 SRM_Default.asset。
	/// </summary>
	[Serializable]
	public sealed class TabletopStackingRulesDefinition
	{
		[SerializeField]
		[ListDrawerSettings]
		[LabelText("规则")]
		[Tooltip("按顺序匹配；任意一条满足即允许合堆。未匹配表示不能合堆。")]
		private TabletopStackingRuleDefinition[] m_rules = Array.Empty<TabletopStackingRuleDefinition>();

		public IReadOnlyList<TabletopStackingRuleDefinition> Rules =>
			m_rules ?? Array.Empty<TabletopStackingRuleDefinition>();

		public TabletopStackingRules CreateRuntime()
		{
			List<TabletopStackingRule> rules = new List<TabletopStackingRule>(Rules.Count);
			for (int i = 0; i < Rules.Count; i++)
			{
				TabletopStackingRuleDefinition rule = Rules[i];
				if (rule == null)
				{
					throw new InvalidOperationException($"牌桌合堆规则的第 {i + 1} 项为空。");
				}
				rules.Add(rule.CreateRuntime());
			}
			return new TabletopStackingRules(rules);
		}

		internal static TabletopStackingRulesDefinition CreateStackCraftDefault()
		{
			return new TabletopStackingRulesDefinition
			{
				m_rules = new[]
				{
					Rule(XTag.Card_Category_Resource, XTag.Card_Category_Resource, requiresSameContent: true),
					Rule(XTag.Card_Category_Character, XTag.Card_Category_Resource),
					Rule(XTag.Card_Category_Character, XTag.Card_Category_Character),
					Rule(XTag.Card_Category_Character, XTag.Card_Category_Material),
					Rule(XTag.Card_Category_Character, XTag.Card_Category_Structure),
					Rule(XTag.Card_Category_Character, XTag.Card_Category_Area),
					Rule(XTag.Card_Category_Consumable, XTag.Card_Category_Resource),
					Rule(XTag.Card_Category_Consumable, XTag.Card_Category_Consumable),
					Rule(XTag.Card_Category_Consumable, XTag.Card_Category_Structure),
					Rule(XTag.Card_Category_Material, XTag.Card_Category_Resource),
					Rule(XTag.Card_Category_Material, XTag.Card_Category_Material),
					Rule(XTag.Card_Category_Material, XTag.Card_Category_Structure),
					Rule(XTag.Card_Category_Equipment, XTag.Card_Category_Character),
					Rule(XTag.Card_Category_Equipment, XTag.Card_Category_Equipment),
					Rule(XTag.Card_Category_Structure, XTag.Card_Category_Structure),
					Rule(XTag.Card_Category_Currency, XTag.Card_Category_Material),
					Rule(XTag.Card_Category_Currency, XTag.Card_Category_Structure),
					Rule(XTag.Card_Category_Currency, XTag.Card_Category_Currency),
					Rule(XTag.Card_Category_Recipe, XTag.Card_Category_Recipe),
					Rule(XTag.Card_Category_Mob, XTag.Card_Category_Consumable),
					Rule(XTag.Card_Category_Mob, XTag.Card_Category_Structure),
					Rule(XTag.Card_Category_Mob, XTag.Card_Category_Mob),
					Rule(XTag.Card_Category_Area, XTag.Card_Category_Area),
					Rule(XTag.Card_Category_Valuable, XTag.Card_Category_Valuable),
				}
			};
		}

		private static TabletopStackingRuleDefinition Rule(
			int sourceTagCode,
			int targetBottomTagCode,
			bool requiresSameContent = false)
		{
			return new TabletopStackingRuleDefinition(
				sourceTagCode,
				targetBottomTagCode,
				requiresSameContent);
		}
	}

	/// <summary>
	/// 运行时只读合堆规则集合；它消费内容资产上的 EX-GAS 标签，不保存卡牌类别第二真相。
	/// </summary>
	public sealed class TabletopStackingRules
	{
		public static readonly TabletopStackingRules Empty =
			new TabletopStackingRules(Array.Empty<TabletopStackingRule>());

		private readonly ReadOnlyCollection<TabletopStackingRule> m_rules;

		public IReadOnlyList<TabletopStackingRule> Rules => m_rules;

		internal TabletopStackingRules(IReadOnlyList<TabletopStackingRule> rules)
		{
			m_rules = new List<TabletopStackingRule>(rules ?? Array.Empty<TabletopStackingRule>()).AsReadOnly();
		}

		public bool CanStack(ContentAsset source, ContentAsset targetBottom)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}
			if (targetBottom == null)
			{
				throw new ArgumentNullException(nameof(targetBottom));
			}
			for (int i = 0; i < m_rules.Count; i++)
			{
				if (m_rules[i].Matches(source, targetBottom))
				{
					return true;
				}
			}
			return false;
		}
	}

	/// <summary>
	/// 单条只读合堆规则；标签父子判断委托给 EX-GAS <see cref="TagHelper"/>。
	/// </summary>
	public sealed class TabletopStackingRule
	{
		private readonly ReadOnlyCollection<int> m_requiredSourceContentTagCodes;
		private readonly ReadOnlyCollection<int> m_requiredTargetBottomContentTagCodes;

		public IReadOnlyList<int> RequiredSourceContentTagCodes => m_requiredSourceContentTagCodes;

		public IReadOnlyList<int> RequiredTargetBottomContentTagCodes => m_requiredTargetBottomContentTagCodes;

		public bool RequiresSameContent { get; }

		internal TabletopStackingRule(
			IReadOnlyList<int> requiredSourceContentTagCodes,
			IReadOnlyList<int> requiredTargetBottomContentTagCodes,
			bool requiresSameContent)
		{
			m_requiredSourceContentTagCodes = new List<int>(
				requiredSourceContentTagCodes ?? Array.Empty<int>()).AsReadOnly();
			m_requiredTargetBottomContentTagCodes = new List<int>(
				requiredTargetBottomContentTagCodes ?? Array.Empty<int>()).AsReadOnly();
			RequiresSameContent = requiresSameContent;
		}

		internal bool Matches(ContentAsset source, ContentAsset targetBottom)
		{
			if (RequiresSameContent && !source.ContentId.Equals(targetBottom.ContentId))
			{
				return false;
			}
			return MatchesAllTags(source.TagCodes, RequiredSourceContentTagCodes) &&
				MatchesAllTags(targetBottom.TagCodes, RequiredTargetBottomContentTagCodes);
		}

		private static bool MatchesAllTags(
			IReadOnlyList<int> actualTags,
			IReadOnlyList<int> requiredTags)
		{
			for (int requiredIndex = 0; requiredIndex < requiredTags.Count; requiredIndex++)
			{
				if (!MatchesAtLeastOneActualTag(actualTags, requiredTags[requiredIndex]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool MatchesAtLeastOneActualTag(
			IReadOnlyList<int> actualTags,
			int requiredTag)
		{
			for (int actualIndex = 0; actualIndex < actualTags.Count; actualIndex++)
			{
				if (TagHelper.HasTag(actualTags[actualIndex], requiredTag))
				{
					return true;
				}
			}
			return false;
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

		internal bool TryGetFullWidthTopRestrictedBand(out Rect band)
		{
			for (int i = 0; i < RestrictedAreas.Count; i++)
			{
				Rect restrictedArea = RestrictedAreas[i];
				if (IsFullWidthTopRestrictedBand(Bounds, restrictedArea))
				{
					band = restrictedArea;
					return true;
				}
			}

			band = default;
			return false;
		}

		internal static bool IsFullWidthTopRestrictedBand(Rect bounds, Rect restrictedArea)
		{
			return Mathf.Approximately(restrictedArea.xMin, bounds.xMin) &&
				Mathf.Approximately(restrictedArea.xMax, bounds.xMax) &&
				Mathf.Approximately(restrictedArea.yMax, bounds.yMax);
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

		internal Rect CalculateFootprint(Vector2 stackPosition, int cardCount)
		{
			return CalculateFootprint(stackPosition, cardCount, CardSize);
		}

		internal Rect CalculateFootprint(Vector2 stackPosition, int cardCount, Vector2 cardSize)
		{
			if (!IsFinite(stackPosition))
			{
				throw new ArgumentException("堆栈锚点必须是有限二维坐标。", "stackPosition");
			}
			if (cardCount <= 0)
			{
				throw new ArgumentOutOfRangeException("cardCount", "空间占地至少需要一张卡牌。");
			}
			if (!IsFinite(cardSize) || cardSize.x <= 0f || cardSize.y <= 0f)
			{
				throw new ArgumentException("空间占地使用的卡牌尺寸必须具有有限坐标和正数宽高。", nameof(cardSize));
			}
			Vector2 span = StackStep * (cardCount - 1);
			Vector2 center = stackPosition + span * 0.5f;
			Vector2 size = cardSize + CardMargin + new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));
			return new Rect(center - size * 0.5f, size);
		}

		internal TabletopCardStackSpatialBody CreateSpatialBody(TabletopCardId bottomCardId, Vector2 stackPosition, int cardCount, bool isLocked)
		{
			Rect footprint = CalculateFootprint(stackPosition, cardCount);
			return new TabletopCardStackSpatialBody(bottomCardId, stackPosition, footprint.center - stackPosition, footprint.size, isLocked);
		}

		internal TabletopCardStackSpatialBody CreateSpatialBody(
			TabletopCardId bottomCardId,
			Vector2 stackPosition,
			int cardCount,
			bool isLocked,
			Vector2 cardSize)
		{
			Rect footprint = CalculateFootprint(stackPosition, cardCount, cardSize);
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
		internal const int DefaultOverlapResolveMaxIterations = 8;
		internal const float DefaultSpawnAttachRadius = 1f;

		private const int MaxCardLimitBonusPlacementExpansion = 100;

		public TabletopCardPlacementArea Area { get; }

		public TabletopCardStackGeometry Geometry { get; }

		internal int OverlapResolveMaxIterations { get; }

		public Vector2 CardLimitBonusExpansionPerPoint { get; }

		public float SpawnAttachRadius { get; }

		public TabletopStackingRules StackingRules { get; }

		public TabletopCardPlacementRules(
			TabletopCardPlacementArea area,
			TabletopCardStackGeometry geometry,
			int overlapResolveMaxIterations = DefaultOverlapResolveMaxIterations,
			Vector2 cardLimitBonusExpansionPerPoint = default,
			float spawnAttachRadius = DefaultSpawnAttachRadius,
			TabletopStackingRules stackingRules = null)
		{
			Area = area ?? throw new ArgumentNullException("area");
			if (!geometry.IsValid)
			{
				throw new ArgumentException("牌桌放置规则缺少有效的堆栈几何。", "geometry");
			}
			if (overlapResolveMaxIterations <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(overlapResolveMaxIterations),
					overlapResolveMaxIterations,
					"牌桌重叠解算迭代次数必须大于 0。");
			}
			if (!IsFinite(cardLimitBonusExpansionPerPoint) ||
				cardLimitBonusExpansionPerPoint.x < 0f ||
				cardLimitBonusExpansionPerPoint.y < 0f)
			{
				throw new ArgumentException(
					"卡牌上限加成的牌桌扩展必须是有限且大于或等于 0 的二维数值。",
					nameof(cardLimitBonusExpansionPerPoint));
			}
			if (!float.IsFinite(spawnAttachRadius) || spawnAttachRadius < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(spawnAttachRadius),
					spawnAttachRadius,
					"牌桌出生吸附半径必须是大于或等于 0 的有限值。");
			}
			Geometry = geometry;
			OverlapResolveMaxIterations = overlapResolveMaxIterations;
			CardLimitBonusExpansionPerPoint = cardLimitBonusExpansionPerPoint;
			SpawnAttachRadius = spawnAttachRadius;
			StackingRules = stackingRules ?? TabletopStackingRules.Empty;
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

			int placementExpansionBonus = Math.Min(cardLimitBonus, MaxCardLimitBonusPlacementExpansion);
			Vector2 expansion = CardLimitBonusExpansionPerPoint * placementExpansionBonus;
			Rect bounds = Area.Bounds;
			Rect expandedBounds = new Rect(
				bounds.xMin - expansion.x,
				bounds.yMin - expansion.y,
				bounds.width + expansion.x * 2f,
				bounds.height + expansion.y * 2f);
			IReadOnlyList<Rect> expandedRestrictedAreas = CreateExpandedRestrictedAreas(bounds, Area.RestrictedAreas, expansion);
			return new TabletopCardPlacementRules(
				new TabletopCardPlacementArea(expandedBounds, expandedRestrictedAreas),
				Geometry,
				OverlapResolveMaxIterations,
				CardLimitBonusExpansionPerPoint,
				SpawnAttachRadius,
				StackingRules);
		}

		/// <summary>随 StackCraft Board 的边界扩展同步移动顶部页眉禁放区，其它内部禁放区保持原世界位置。</summary>
		private static IReadOnlyList<Rect> CreateExpandedRestrictedAreas(
			Rect originalBounds,
			IReadOnlyList<Rect> restrictedAreas,
			Vector2 expansion)
		{
			if (restrictedAreas == null || restrictedAreas.Count == 0)
			{
				return restrictedAreas;
			}

			List<Rect> expandedRestrictedAreas = new List<Rect>(restrictedAreas.Count);
			for (int i = 0; i < restrictedAreas.Count; i++)
			{
				Rect restrictedArea = restrictedAreas[i];
				if (TabletopCardPlacementArea.IsFullWidthTopRestrictedBand(originalBounds, restrictedArea))
				{
					expandedRestrictedAreas.Add(new Rect(
						restrictedArea.xMin - expansion.x,
						restrictedArea.yMin + expansion.y,
						restrictedArea.width + expansion.x * 2f,
						restrictedArea.height));
					continue;
				}

				expandedRestrictedAreas.Add(restrictedArea);
			}
			return expandedRestrictedAreas;
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
