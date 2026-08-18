using System;
using Gameplay.Content;

namespace Gameplay.Tabletop
{
	/// <summary>卡包商贩在当前牌桌中的实例，唯一拥有这一次售卖的付款进度。</summary>
	public sealed class PackVendorCard : TabletopCard
	{
		public int Price { get; }

		public int PaidAmount { get; private set; }

		public int RemainingPrice => Price - PaidAmount;

		internal PackVendorCard(TabletopCardId id, ContentId contentId, int price)
			: base(id, contentId)
		{
			if (price <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(price));
			}
			Price = price;
		}

		internal PackVendorCard(
			TabletopCardId id,
			ContentId contentId,
			int price,
			TabletopCardRuntimeStateSnapshot runtimeState)
			: this(id, contentId, price)
		{
			if (runtimeState is not PackVendorRuntimeStateSnapshot vendorState ||
				vendorState.PaidAmount < 0 || vendorState.PaidAmount >= Price)
			{
				throw new InvalidOperationException($"卡包商贩 {id} 的付款快照无效。");
			}
			PaidAmount = vendorState.PaidAmount;
		}

		internal bool Pay(int amount)
		{
			if (amount <= 0 || amount > RemainingPrice)
			{
				throw new ArgumentOutOfRangeException(nameof(amount));
			}
			PaidAmount = checked(PaidAmount + amount);
			return PaidAmount == Price;
		}

		internal void CompletePurchase()
		{
			if (PaidAmount != Price)
			{
				throw new InvalidOperationException($"卡包商贩 {Id} 尚未收足款，不能完成购买。");
			}
			PaidAmount = 0;
		}

		protected internal override TabletopCardRuntimeStateSnapshot CreateRuntimeStateSnapshot()
		{
			return new PackVendorRuntimeStateSnapshot(PaidAmount);
		}
	}

	[Serializable]
	public sealed class PackVendorRuntimeStateSnapshot : TabletopCardRuntimeStateSnapshot
	{
		[UnityEngine.SerializeField]
		private int m_paidAmount;

		public int PaidAmount => m_paidAmount;

		internal PackVendorRuntimeStateSnapshot(int paidAmount)
		{
			m_paidAmount = paidAmount;
		}
	}
}
