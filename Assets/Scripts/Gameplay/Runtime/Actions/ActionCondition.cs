using System;
using System.Collections.Generic;
using Gameplay.Content;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Actions
{
	/// <summary>一次行动候选在当前单局中判断可用性所需的只读事实。</summary>
	public sealed class ActionConditionContext
	{
		public ActionDefinition Action { get; }

		public IReadOnlyList<ActionSlotBinding> Bindings { get; }

		public ContentIndex Content { get; }

		public TabletopCards Cards { get; }

		public int CompletedQuestCount { get; }

		internal ActionConditionContext(
			ActionDefinition action,
			IReadOnlyList<ActionSlotBinding> bindings,
			ContentIndex content,
			TabletopCards cards,
			int completedQuestCount)
		{
			Action = action ?? throw new ArgumentNullException(nameof(action));
			Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
			Content = content ?? throw new ArgumentNullException(nameof(content));
			Cards = cards ?? throw new ArgumentNullException(nameof(cards));
			if (completedQuestCount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(completedQuestCount));
			}
			CompletedQuestCount = completedQuestCount;
		}

		public ActionSlotBinding GetBinding(string slotKey)
		{
			for (int i = 0; i < Bindings.Count; i++)
			{
				if (string.Equals(Bindings[i].Slot.Key, slotKey, StringComparison.Ordinal))
				{
					return Bindings[i];
				}
			}
			throw new InvalidOperationException($"行动 {Action.ContentId} 缺少条件所需槽位 {slotKey}。");
		}
	}

	/// <summary>行动作者源声明的可用条件；只读取当前事实，不拥有运行状态。</summary>
	[Serializable]
	public abstract class ActionCondition
	{
		internal bool IsMet(ActionConditionContext context)
		{
			return Evaluate(context ?? throw new ArgumentNullException(nameof(context)));
		}

		internal void Validate(ActionResultValidationContext context)
		{
			ValidateCondition(context ?? throw new ArgumentNullException(nameof(context)));
		}

		protected abstract bool Evaluate(ActionConditionContext context);

		protected virtual void ValidateCondition(ActionResultValidationContext context)
		{
		}
	}

	/// <summary>要求指定槽位中的卡包商贩已经达到其任务完成数门槛。</summary>
	[Serializable]
	public sealed class PackVendorUnlockedCondition : ActionCondition
	{
		[SerializeField, ActionSlotReference, LabelText("商贩槽位")]
		private string m_vendorSlotKey;

		public string VendorSlotKey => m_vendorSlotKey ?? string.Empty;

		protected override bool Evaluate(ActionConditionContext context)
		{
			ActionSlotBinding binding = context.GetBinding(VendorSlotKey);
			if (binding.CardIds.Count == 0)
			{
				return false;
			}
			if (binding.CardIds.Count != 1)
			{
				return false;
			}
			if (!context.Cards.TryGetCard(binding.CardIds[0], out TabletopCard card))
			{
				throw new InvalidOperationException(
					$"行动 {context.Action.ContentId} 的商贩解锁条件引用了不存在的牌桌卡牌 {binding.CardIds[0]}。");
			}
			if (card is not PackVendorCard ||
				!context.Content.TryGet(card.ContentId, out PackVendorDefinition vendor))
			{
				return false;
			}
			return vendor.IsUnlocked(context.CompletedQuestCount);
		}

		protected override void ValidateCondition(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(VendorSlotKey, "ACTION_CONDITION_PACK_VENDOR_SLOT_UNKNOWN");
		}
	}
	/// <summary>要求指定槽位中的箱子仍有容量，避免满箱继续出现存币候选。</summary>
	[Serializable]
	public sealed class ChestHasCapacityCondition : ActionCondition
	{
		[SerializeField, ActionSlotReference, LabelText("箱子槽位")]
		private string m_chestSlotKey;

		public string ChestSlotKey => m_chestSlotKey ?? string.Empty;

		protected override bool Evaluate(ActionConditionContext context)
		{
			ActionSlotBinding binding = context.GetBinding(ChestSlotKey);
			if (binding.CardIds.Count != 1)
			{
				return false;
			}
			if (!context.Cards.TryGetCard(binding.CardIds[0], out TabletopCard card))
			{
				throw new InvalidOperationException(
					$"行动 {context.Action.ContentId} 的箱子容量条件引用了不存在的牌桌卡牌 {binding.CardIds[0]}。");
			}
			return card is ChestCard chest && chest.RemainingCapacity > 0;
		}

		protected override void ValidateCondition(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(ChestSlotKey, "ACTION_CONDITION_CHEST_CAPACITY_SLOT_UNKNOWN");
		}
	}

	/// <summary>要求指定槽位中的箱子单独成堆且至少存有一个货币单位。</summary>
	[Serializable]
	public sealed class ChestHasStoredCurrencyCondition : ActionCondition
	{
		[SerializeField, ActionSlotReference, LabelText("箱子槽位")]
		private string m_chestSlotKey;

		public string ChestSlotKey => m_chestSlotKey ?? string.Empty;

		protected override bool Evaluate(ActionConditionContext context)
		{
			ActionSlotBinding binding = context.GetBinding(ChestSlotKey);
			if (binding.CardIds.Count != 1)
			{
				return false;
			}
			if (!context.Cards.TryGetCard(binding.CardIds[0], out TabletopCard card))
			{
				throw new InvalidOperationException(
					$"行动 {context.Action.ContentId} 的箱子存币条件引用了不存在的牌桌卡牌 {binding.CardIds[0]}。");
			}
			if (card is not ChestCard chest)
			{
				return false;
			}

			TabletopCardStack stack = context.Cards.GetStackContaining(chest.Id);
			return chest.StoredCurrencyCount > 0 && stack.Cards.Count == 1;
		}

		protected override void ValidateCondition(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(ChestSlotKey, "ACTION_CONDITION_CHEST_STORED_SLOT_UNKNOWN");
		}
	}

	/// <summary>要求付款槽位里的每个来源都是当前内容集合声明的货币卡，或是至少存有一个货币单位的箱子。</summary>
	[Serializable]
	public sealed class CardPaymentSourceAvailableCondition : ActionCondition
	{
		[SerializeField, ActionSlotReference, LabelText("付款槽位")]
		private string m_paymentSlotKey;

		public string PaymentSlotKey => m_paymentSlotKey ?? string.Empty;

		protected override bool Evaluate(ActionConditionContext context)
		{
			ActionSlotBinding binding = context.GetBinding(PaymentSlotKey);
			if (binding.CardIds.Count == 0)
			{
				return false;
			}

			for (int i = 0; i < binding.CardIds.Count; i++)
			{
				if (!context.Cards.TryGetCard(binding.CardIds[i], out TabletopCard card))
				{
					throw new InvalidOperationException($"行动 {context.Action.ContentId} 的付款槽位引用了不存在的牌桌卡牌 {binding.CardIds[i]}。");
				}
				if (card is ChestCard chest && chest.StoredCurrencyCount <= 0)
				{
					return false;
				}
				if (card is not ChestCard &&
					!CurrencyCardQuery.IsCurrencyCard(context.Content, card.ContentId))
				{
					return false;
				}
			}
			return true;
		}

		protected override void ValidateCondition(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(PaymentSlotKey, "ACTION_CONDITION_PAYMENT_SLOT_UNKNOWN");
		}
	}

	/// <summary>要求出售槽位里的每张牌都可出售；箱子必须为空才能进入收购点。</summary>
	[Serializable]
	public sealed class CardSaleSourceAvailableCondition : ActionCondition
	{
		[SerializeField, ActionSlotReference, LabelText("出售槽位")]
		private string m_soldSlotKey;

		public string SoldSlotKey => m_soldSlotKey ?? string.Empty;

		protected override bool Evaluate(ActionConditionContext context)
		{
			ActionSlotBinding binding = context.GetBinding(SoldSlotKey);
			if (binding.CardIds.Count == 0)
			{
				return false;
			}

			for (int i = 0; i < binding.CardIds.Count; i++)
			{
				if (!context.Cards.TryGetCard(binding.CardIds[i], out TabletopCard card))
				{
					throw new InvalidOperationException($"行动 {context.Action.ContentId} 的出售槽位引用了不存在的牌桌卡牌 {binding.CardIds[i]}。");
				}
				if (!context.Content.TryGet(card.ContentId, out CardDefinition definition))
				{
					throw new InvalidOperationException($"行动 {context.Action.ContentId} 的出售槽位引用了非卡牌内容 {card.ContentId}。");
				}
				if (card is ChestCard chest && chest.StoredCurrencyCount > 0)
				{
					return false;
				}
				if (definition.SellValue <= 0)
				{
					return false;
				}
			}
			return true;
		}

		protected override void ValidateCondition(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(SoldSlotKey, "ACTION_CONDITION_SELL_SLOT_UNKNOWN");
		}
	}

	internal static class ChestConditionUtility
	{
		internal static ChestCard RequireSingleChest(
			ActionConditionContext context,
			string slotKey,
			string conditionName)
		{
			ActionSlotBinding binding = context.GetBinding(slotKey);
			if (binding.CardIds.Count != 1 ||
				!context.Cards.TryGetCard(binding.CardIds[0], out TabletopCard card) ||
				card is not ChestCard chest)
			{
				throw new InvalidOperationException(
					$"行动 {context.Action.ContentId} 的箱子{conditionName}必须绑定一张有效箱子卡。");
			}
			return chest;
		}
	}
}
