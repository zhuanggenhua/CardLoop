using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>牌桌上的一项卡包售卖；售价和解锁门槛属于售卖关系，不污染卡包商品定义。</summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡包商贩", fileName = "卡包商贩_")]
	public sealed class PackVendorDefinition : CardDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardPackDefinition)), LabelText("出售卡包")]
		private ContentId m_offeredPackId;

		[SerializeField, Min(1), LabelText("售价")]
		private int m_price = 1;

		[SerializeField, Min(0), LabelText("解锁所需完成任务数")]
		private int m_minimumCompletedQuests;

		[SerializeField]
		[LabelText("卡包生成偏移")]
		[Tooltip("购买完成后卡包相对商贩牌桌位置的生成偏移；StackCraft 模板为商贩下方 1.4。")]
		private Vector2 m_packSpawnOffset = new Vector2(0f, -1.4f);

		public ContentId OfferedPackId => m_offeredPackId;

		public int Price => m_price;

		public int MinimumCompletedQuests => m_minimumCompletedQuests;

		public Vector2 PackSpawnOffset => m_packSpawnOffset;

		public bool IsUnlocked(int completedQuestCount)
		{
			return completedQuestCount >= MinimumCompletedQuests;
		}

		protected internal override TabletopCard CreateRuntimeCard(TabletopCardId id)
		{
			return new PackVendorCard(id, ContentId, Price);
		}

		protected internal override TabletopCard RestoreRuntimeCard(TabletopCardSnapshot snapshot)
		{
			return new PackVendorCard(snapshot.CardId, ContentId, Price, snapshot.RuntimeState);
		}

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (!OfferedPackId.IsValid || !context.TryGet(OfferedPackId, out CardPackDefinition _))
			{
				context.AddError("PACK_VENDOR_PACK_INVALID", $"卡包商贩 {ContentId} 缺少有效的出售卡包 {OfferedPackId}。", this);
			}
			if (Price <= 0)
			{
				context.AddError("PACK_VENDOR_PRICE_INVALID", $"卡包商贩 {ContentId} 的售价必须大于 0。", this);
			}
			if (MinimumCompletedQuests < 0)
			{
				context.AddError("PACK_VENDOR_QUEST_COUNT_INVALID", $"卡包商贩 {ContentId} 的解锁任务数不能为负数。", this);
			}
			if (float.IsNaN(PackSpawnOffset.x) || float.IsNaN(PackSpawnOffset.y) ||
				float.IsInfinity(PackSpawnOffset.x) || float.IsInfinity(PackSpawnOffset.y))
			{
				context.AddError("PACK_VENDOR_SPAWN_OFFSET_INVALID", $"卡包商贩 {ContentId} 的卡包生成偏移必须是有限数值。", this);
			}
		}
	}
}
