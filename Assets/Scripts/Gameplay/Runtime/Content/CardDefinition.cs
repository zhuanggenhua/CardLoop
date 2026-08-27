using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;
using Gameplay.Tabletop;

namespace Gameplay.Content
{
	/// <summary>
	/// 可实例化到牌桌的卡牌内容作者源，补充卡面图片等卡牌专属数据。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡牌", fileName = "卡牌_")]
	public class CardDefinition : DisplayableContentAsset
	{
		[Header("卡牌实例")]
		[SerializeField]
		[Min(1)]
		[HideIf(nameof(HasDerivedInitialUses))]
		[LabelText("初始使用次数")]
		[Tooltip("每个新卡牌实例可被行动使用的次数；降到零时由牌桌移除。普通一次性材料保持 1。")]
		private int m_initialUses = 1;

		[SerializeField]
		[Min(0)]
		[LabelText("出售价值")]
		[Tooltip("把这张卡牌出售时生成的货币卡数量；0 表示不可出售。")]
		private int m_sellValue;

		[SerializeField]
		[HideIf(nameof(HasDerivedCardLimitCounting))]
		[LabelText("计入卡牌上限")]
		[Tooltip("关闭后，这张卡仍存在于牌桌，但不计入日终卡牌数量；货币和固定交互节点通常关闭。")]
		private bool m_countsTowardCardLimit = true;

		[SerializeField]
		[Min(0)]
		[LabelText("卡牌上限加成")]
		[Tooltip("这张卡存在于牌桌时，为所属剧本的日终卡牌上限提供的额外数量。")]
		private int m_cardLimitBonus;

		[Header("周期产出")]
		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("周期产出卡牌")]
		[Tooltip("这张卡在牌桌真实秒推进时周期生成的卡牌；为空则不产出。")]
		private ContentId m_periodicProductionCardId;

		[SerializeField]
		[Min(0f)]
		[LabelText("产出间隔秒数")]
		[Tooltip("两次周期产出之间的真实秒数；配置了周期产出卡牌时必须大于 0。")]
		private float m_periodicProductionIntervalSeconds;

		[Header("自动移动")]
		[SerializeField]
		[Min(0f)]
		[LabelText("自动移动间隔秒数")]
		[Tooltip("这张卡每隔多少真实秒尝试自动随机移动一次；0 表示不会自动移动。")]
		private float m_automaticMovementIntervalSeconds;

		[SerializeField]
		[Min(0f)]
		[LabelText("自动移动半径")]
		[Tooltip("单次自动随机移动的最大距离；启用自动移动时必须大于 0。")]
		private float m_automaticMovementRadius;

		[SerializeField]
		[Min(0)]
		[LabelText("自动移动尝试次数")]
		[Tooltip("每次触发自动移动时最多尝试多少个随机目标点；启用自动移动时必须大于 0。")]
		private int m_automaticMovementMaxAttempts;

		[SerializeField]
		[Min(0)]
		[LabelText("自动移动留存容量")]
		[Tooltip("这张卡位于牌堆底部时，可让上方多少张非敌对自动移动卡留在当前牌堆；0 表示不提供该约束。")]
		private int m_automaticMovementRetentionCapacity;

		[Header("卡牌表现")]
		[SerializeField]
		[LabelText("卡面美术")]
		[Tooltip("卡牌正面使用的图片地址。它只负责表现，不替代内容 ID。")]
		private SoftAssetReference<Sprite> m_cardArt;

		[SerializeField]
		[LabelText("卡牌表面")]
		[Tooltip("卡牌类别表面材质地址。StackCraft 卡面由材质同时承载底板、颜色、覆盖图比例和覆盖图偏移，不用 SpriteRenderer 另画一层。")]
		private SoftAssetReference<Material> m_cardSurface;

		[SerializeField]
		[LabelText("覆盖牌桌可见尺寸")]
		[Tooltip("开启后，这张卡在牌桌上的 Mesh、文字、图标和碰撞范围使用作者源尺寸；用于承接 StackCraft 卡包、交易区等非普通卡牌尺寸。")]
		private bool m_overrideViewSize;

		[SerializeField]
		[LabelText("牌桌可见尺寸")]
		[Tooltip("覆盖后的牌桌可见宽高，单位是牌桌坐标；只有开启覆盖时生效。")]
		private Vector2 m_viewSize = Vector2.one;

		public SoftAssetReference<Sprite> CardArt => m_cardArt;

		public SoftAssetReference<Material> CardSurface => m_cardSurface;

		public SoftAssetReference<Sprite> Artwork => CardArt != null && CardArt.IsValid() ? CardArt : base.Icon;

		public virtual int InitialUses => m_initialUses;

		/// <summary>派生卡牌可隐藏由自身作者数据计算的使用次数，避免维护第二份值。</summary>
		protected virtual bool HasDerivedInitialUses => false;

		/// <summary>派生卡牌可固定容量统计规则，避免作者源维护无效开关。</summary>
		protected virtual bool HasDerivedCardLimitCounting => false;

		/// <summary>派生卡牌校验历史资产里隐藏字段是否仍保存了错误值。</summary>
		protected bool AuthoringCountsTowardCardLimit => m_countsTowardCardLimit;

		/// <summary>返回这张卡牌在牌桌上的可见尺寸；尺寸只读作者源，不允许派生类再维护第二套显示真相。</summary>
		public Vector2 GetViewSize(Vector2 defaultCardSize)
		{
			return m_overrideViewSize ? m_viewSize : defaultCardSize;
		}

		public int SellValue => m_sellValue;

		public virtual bool CountsTowardCardLimit => m_countsTowardCardLimit;

		public int CardLimitBonus => m_cardLimitBonus;

		public ContentId PeriodicProductionCardId => m_periodicProductionCardId;

		public float PeriodicProductionIntervalSeconds => m_periodicProductionIntervalSeconds;

		public bool HasPeriodicProduction =>
			PeriodicProductionCardId.IsValid || PeriodicProductionIntervalSeconds > 0f;

		public float AutomaticMovementIntervalSeconds => m_automaticMovementIntervalSeconds;

		public float AutomaticMovementRadius => m_automaticMovementRadius;

		public int AutomaticMovementMaxAttempts => m_automaticMovementMaxAttempts;

		public int AutomaticMovementRetentionCapacity => m_automaticMovementRetentionCapacity;

		public bool HasAutomaticMovement =>
			AutomaticMovementIntervalSeconds > 0f ||
			AutomaticMovementRadius > 0f ||
			AutomaticMovementMaxAttempts > 0;

		/// <summary>创建这个卡牌定义对应的局内对象；派生卡牌在这里返回自己的运行时类型。</summary>
		protected internal virtual TabletopCard CreateRuntimeCard(TabletopCardId id)
		{
			return new TabletopCard(id, ContentId, InitialUses);
		}

		/// <summary>从牌桌快照恢复这个卡牌定义对应的局内对象。</summary>
		protected internal virtual TabletopCard RestoreRuntimeCard(TabletopCardSnapshot snapshot)
		{
			if (snapshot.RuntimeState != null)
			{
				throw new System.InvalidOperationException(
					$"普通卡牌 {snapshot.CardId} 不能恢复派生卡牌运行状态 {snapshot.RuntimeState.GetType().FullName}。");
			}
			return new TabletopCard(
				snapshot.CardId,
				ContentId,
				snapshot.RemainingUses,
				snapshot.PeriodicProductionElapsedSeconds,
				snapshot.AutomaticMovementElapsedSeconds);
		}

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (InitialUses <= 0)
			{
				context.AddError(
					"CARD_INITIAL_USES_INVALID",
					$"卡牌 {ContentId} 的初始使用次数必须大于 0，当前值为 {InitialUses}。",
					this);
			}
			if (SellValue < 0)
			{
				context.AddError(
					"CARD_SELL_VALUE_INVALID",
					$"卡牌 {ContentId} 的出售价值不能为负数，当前值为 {SellValue}。",
					this);
			}
			if (CardLimitBonus < 0)
			{
				context.AddError(
					"CARD_LIMIT_BONUS_INVALID",
					$"卡牌 {ContentId} 的卡牌上限加成不能为负数，当前值为 {CardLimitBonus}。",
					this);
			}
			if (AutomaticMovementRetentionCapacity < 0)
			{
				context.AddError(
					"CARD_AUTOMATIC_MOVEMENT_RETENTION_CAPACITY_INVALID",
					$"卡牌 {ContentId} 的自动移动留存容量不能为负数，当前值为 {AutomaticMovementRetentionCapacity}。",
					this);
			}
			if (m_overrideViewSize &&
				(!float.IsFinite(m_viewSize.x) || !float.IsFinite(m_viewSize.y) ||
				 m_viewSize.x <= 0f || m_viewSize.y <= 0f))
			{
				context.AddError(
					"CARD_VIEW_SIZE_INVALID",
					$"卡牌 {ContentId} 的牌桌可见尺寸必须为正数，当前值为 {m_viewSize}。",
					this);
			}
			if (HasPeriodicProduction)
			{
				if (!PeriodicProductionCardId.IsValid ||
					!context.TryGet(PeriodicProductionCardId, out CardDefinition _))
				{
					context.AddError(
						"CARD_PERIODIC_PRODUCTION_CARD_INVALID",
						$"卡牌 {ContentId} 的周期产出卡牌无效：{PeriodicProductionCardId}。",
						this);
				}
				if (!float.IsFinite(PeriodicProductionIntervalSeconds) ||
					PeriodicProductionIntervalSeconds <= 0f)
				{
					context.AddError(
						"CARD_PERIODIC_PRODUCTION_INTERVAL_INVALID",
						$"卡牌 {ContentId} 的周期产出间隔必须大于 0 秒，当前值为 {PeriodicProductionIntervalSeconds}。",
						this);
				}
			}
			if (HasAutomaticMovement)
			{
				if (!float.IsFinite(AutomaticMovementIntervalSeconds) ||
					AutomaticMovementIntervalSeconds <= 0f)
				{
					context.AddError(
						"CARD_AUTOMATIC_MOVEMENT_INTERVAL_INVALID",
						$"卡牌 {ContentId} 的自动移动间隔必须大于 0 秒，当前值为 {AutomaticMovementIntervalSeconds}。",
						this);
				}
				if (!float.IsFinite(AutomaticMovementRadius) ||
					AutomaticMovementRadius <= 0f)
				{
					context.AddError(
						"CARD_AUTOMATIC_MOVEMENT_RADIUS_INVALID",
						$"卡牌 {ContentId} 的自动移动半径必须大于 0，当前值为 {AutomaticMovementRadius}。",
						this);
				}
				if (AutomaticMovementMaxAttempts <= 0)
				{
					context.AddError(
						"CARD_AUTOMATIC_MOVEMENT_ATTEMPTS_INVALID",
						$"卡牌 {ContentId} 的自动移动尝试次数必须大于 0，当前值为 {AutomaticMovementMaxAttempts}。",
						this);
				}
			}
		}
	}
}
