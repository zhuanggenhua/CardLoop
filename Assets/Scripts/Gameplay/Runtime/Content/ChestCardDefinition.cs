using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>可实例化为存储货币的箱子卡；它不是通用库存，只承接当前牌桌存币箱效果。</summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡牌/箱子", fileName = "箱子_")]
	public sealed class ChestCardDefinition : CardDefinition
	{
		[SerializeField]
		[Min(1f)]
		[LabelText("容量")]
		[Tooltip("这张箱子卡最多能存储多少个货币单位。")]
		private int m_capacity = 50;

		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("存储货币")]
		[Tooltip("箱子存入和取出时对应的货币卡牌内容。运行时只保存数量，不保存第二份货币资产引用。")]
		private ContentId m_currencyCardId;

		public int Capacity => m_capacity;

		public ContentId CurrencyCardId => m_currencyCardId;

		protected internal override TabletopCard CreateRuntimeCard(TabletopCardId id)
		{
			return new ChestCard(id, ContentId, Capacity);
		}

		protected internal override TabletopCard RestoreRuntimeCard(TabletopCardSnapshot snapshot)
		{
			return new ChestCard(snapshot.CardId, ContentId, Capacity, snapshot.RuntimeState);
		}

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (Capacity <= 0)
			{
				context.AddError("CHEST_CAPACITY_INVALID", $"箱子 {ContentId} 的容量必须大于 0。", this);
			}
			if (!CurrencyCardId.IsValid || !context.TryGet(CurrencyCardId, out CardDefinition _))
			{
				context.AddError("CHEST_CURRENCY_INVALID", $"箱子 {ContentId} 缺少有效的存储货币 {CurrencyCardId}。", this);
			}
		}
	}
}
