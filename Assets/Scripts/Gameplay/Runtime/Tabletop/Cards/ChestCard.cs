using System;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>牌桌中的箱子卡实例，唯一拥有本次单局的存币数量。</summary>
	public sealed class ChestCard : TabletopCard
	{
		public int Capacity { get; }

		public int StoredCurrencyCount { get; private set; }

		public int RemainingCapacity => Capacity - StoredCurrencyCount;

		internal ChestCard(TabletopCardId id, ContentId contentId, int capacity)
			: base(id, contentId)
		{
			if (capacity <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(capacity), "箱子容量必须大于 0。");
			}
			Capacity = capacity;
		}

		internal ChestCard(
			TabletopCardId id,
			ContentId contentId,
			int capacity,
			TabletopCardRuntimeStateSnapshot runtimeState)
			: this(id, contentId, capacity)
		{
			if (runtimeState is not ChestCardRuntimeStateSnapshot chestState ||
				chestState.StoredCurrencyCount < 0 ||
				chestState.StoredCurrencyCount > Capacity)
			{
				throw new InvalidOperationException($"箱子卡牌 {id} 的存币快照无效。");
			}
			StoredCurrencyCount = chestState.StoredCurrencyCount;
		}

		internal void DepositCurrency(int amount)
		{
			if (amount <= 0 || amount > RemainingCapacity)
			{
				throw new ArgumentOutOfRangeException(nameof(amount), amount, $"箱子 {Id} 的存入数量超出剩余容量。");
			}
			StoredCurrencyCount = checked(StoredCurrencyCount + amount);
		}

		internal void WithdrawCurrency(int amount)
		{
			if (amount <= 0 || amount > StoredCurrencyCount)
			{
				throw new ArgumentOutOfRangeException(nameof(amount), amount, $"箱子 {Id} 的取出数量超出当前存币。");
			}
			StoredCurrencyCount -= amount;
		}

		internal void ApplyCurrencyChange(int expectedStoredCurrencyCount, int delta)
		{
			if (StoredCurrencyCount != expectedStoredCurrencyCount)
			{
				throw new InvalidOperationException($"箱子 {Id} 的当前存币数量已不符合冻结行动计划。");
			}
			if (delta > 0)
			{
				DepositCurrency(delta);
			}
			else if (delta < 0)
			{
				WithdrawCurrency(-delta);
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(delta), delta, "箱子存币变化量不能为 0。");
			}
		}

		protected internal override TabletopCardRuntimeStateSnapshot CreateRuntimeStateSnapshot()
		{
			return new ChestCardRuntimeStateSnapshot(StoredCurrencyCount);
		}
	}

	[Serializable]
	public sealed class ChestCardRuntimeStateSnapshot : TabletopCardRuntimeStateSnapshot
	{
		[SerializeField]
		private int m_storedCurrencyCount;

		public int StoredCurrencyCount => m_storedCurrencyCount;

		internal ChestCardRuntimeStateSnapshot(int storedCurrencyCount)
		{
			m_storedCurrencyCount = storedCurrencyCount;
		}
	}
}
