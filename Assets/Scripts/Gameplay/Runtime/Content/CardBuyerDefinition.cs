using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>牌桌上的固定收购点；出售规则由行动结算执行，这里只声明收购点的货币表现来源。</summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/收购点", fileName = "收购点_")]
	public sealed class CardBuyerDefinition : CardDefinition
	{
		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("支付货币")]
		[Tooltip("StackCraft CardBuyer 在收购点表面显示的货币图标；出售行动仍负责真正生成货币卡。")]
		private ContentId m_currencyCardId;

		[SerializeField]
		[LabelText("货币生成偏移")]
		[Tooltip("出售成功后，货币相对收购点牌桌位置的生成偏移；StackCraft TradeZone 模板为收购点下方 1.4。")]
		private Vector2 m_currencySpawnOffset = new Vector2(0f, -1.4f);

		public ContentId CurrencyCardId => m_currencyCardId;

		public Vector2 CurrencySpawnOffset => m_currencySpawnOffset;

		protected internal override TabletopCard CreateRuntimeCard(TabletopCardId id)
		{
			return new TabletopCard(id, ContentId, InitialUses);
		}

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (!CurrencyCardId.IsValid || !context.TryGet(CurrencyCardId, out CardDefinition _))
			{
				context.AddError(
					"CARD_BUYER_CURRENCY_INVALID",
					$"收购点 {ContentId} 缺少有效支付货币 {CurrencyCardId}。",
					this);
			}
			if (float.IsNaN(CurrencySpawnOffset.x) || float.IsNaN(CurrencySpawnOffset.y) ||
				float.IsInfinity(CurrencySpawnOffset.x) || float.IsInfinity(CurrencySpawnOffset.y))
			{
				context.AddError(
					"CARD_BUYER_SPAWN_OFFSET_INVALID",
					$"收购点 {ContentId} 的货币生成偏移必须是有限数值。",
					this);
			}
		}
	}
}
