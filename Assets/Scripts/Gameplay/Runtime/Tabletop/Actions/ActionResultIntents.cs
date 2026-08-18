using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>声明行动完成后，把指定参与槽位中的卡牌记为已探索区域或地点。</summary>
	[Serializable]
	public sealed class ExploreCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ActionSlotReference]
		[LabelText("探索槽位")]
		[Tooltip("行动成功完成后，该槽位绑定的卡牌内容会作为已探索区域或地点提交给当前单局任务日志。")]
		private string m_exploredSlotKey;

		public string ExploredSlotKey => m_exploredSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(
				ExploredSlotKey,
				"ACTION_RESULT_EXPLORE_SLOT_UNKNOWN");
		}
	}
	/// <summary>声明把一张装备卡挂到角色槽位；同槽旧装备由角色卡替换并回到牌桌。</summary>
	[Serializable]
	public sealed class EquipCardResultIntent : ActionResultIntent
	{
		[SerializeField, ActionSlotReference, LabelText("装备槽位")]
		private string m_equipmentSlotKey;

		[SerializeField, ActionSlotReference, LabelText("角色槽位")]
		private string m_characterSlotKey;

		public string EquipmentSlotKey => m_equipmentSlotKey ?? string.Empty;

		public string CharacterSlotKey => m_characterSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(EquipmentSlotKey, "ACTION_RESULT_EQUIP_EQUIPMENT_SLOT_UNKNOWN");
			context.ValidateSlotReference(CharacterSlotKey, "ACTION_RESULT_EQUIP_CHARACTER_SLOT_UNKNOWN");
			if (context.Action.TurnCost != 0)
			{
				context.AddError(
					"ACTION_RESULT_EQUIP_MUST_BE_IMMEDIATE",
					$"装备行动 {context.Action.ContentId} 必须是 0 回合即时行动，避免离桌装备计划跨回合持有可变牌桌状态。");
			}
		}
	}

	/// <summary>声明从角色指定槽位卸下一张装备，并把原装备卡恢复回牌桌。</summary>
	[Serializable]
	public sealed class UnequipCardResultIntent : ActionResultIntent
	{
		[SerializeField, ActionSlotReference, LabelText("角色槽位")]
		private string m_characterSlotKey;

		[SerializeField]
		[ContentIdReference(typeof(EquipmentSlotDefinition))]
		[LabelText("装备槽位")]
		[Tooltip("要从角色身上卸下的装备槽位。槽位是正式内容 ID，不使用枚举。")]
		private ContentId m_equipmentSlotId;

		public string CharacterSlotKey => m_characterSlotKey ?? string.Empty;

		public ContentId EquipmentSlotId => m_equipmentSlotId;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(CharacterSlotKey, "ACTION_RESULT_UNEQUIP_CHARACTER_SLOT_UNKNOWN");
			if (!EquipmentSlotId.IsValid ||
				!context.Content.TryGet(EquipmentSlotId, out EquipmentSlotDefinition _))
			{
				context.AddError(
					"ACTION_RESULT_UNEQUIP_SLOT_INVALID",
					$"卸装行动 {context.Action.ContentId} 缺少有效装备槽位 {EquipmentSlotId}。");
			}
			if (context.Action.TurnCost != 0)
			{
				context.AddError(
					"ACTION_RESULT_UNEQUIP_MUST_BE_IMMEDIATE",
					$"卸装行动 {context.Action.ContentId} 必须是 0 回合即时行动，避免离桌装备计划跨回合持有可变角色状态。");
			}
		}
	}

	/// <summary>声明把付款槽位中的卡牌投入卡包商贩；满价时生成商贩配置的卡包。</summary>
	[Serializable]
	public sealed class PurchaseCardPackResultIntent : ActionResultIntent
	{
		[SerializeField, ActionSlotReference, LabelText("商贩槽位")]
		private string m_vendorSlotKey;

		[SerializeField, ActionSlotReference, LabelText("付款槽位")]
		private string m_paymentSlotKey;

		public string VendorSlotKey => m_vendorSlotKey ?? string.Empty;

		public string PaymentSlotKey => m_paymentSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(VendorSlotKey, "ACTION_RESULT_PURCHASE_VENDOR_SLOT_UNKNOWN");
			context.ValidateSlotReference(PaymentSlotKey, "ACTION_RESULT_PURCHASE_PAYMENT_SLOT_UNKNOWN");
			if (context.Action.TurnCost != 0)
			{
				context.AddError(
					"ACTION_RESULT_PURCHASE_MUST_BE_IMMEDIATE",
					$"卡包购买行动 {context.Action.ContentId} 必须是 0 回合即时行动，避免付款计划跨回合占用货币和商贩状态。");
			}
		}
	}

	/// <summary>声明使用一张卡包卡的当前槽位，并由牌桌权威随机生成普通卡或未发现配方卡。</summary>
	[Serializable]
	public sealed class OpenCardPackResultIntent : ActionResultIntent
	{
		[SerializeField, ActionSlotReference, LabelText("卡包槽位")]
		private string m_packSlotKey;

		public string PackSlotKey => m_packSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(PackSlotKey, "ACTION_RESULT_PACK_SLOT_UNKNOWN");
			if (context.Action.TurnCost != 0)
			{
				context.AddError("ACTION_RESULT_PACK_MUST_BE_IMMEDIATE", $"打开卡包行动 {context.Action.ContentId} 必须是 0 回合即时行动。");
			}

			ActionSlotDefinition packSlot = null;
			for (int i = 0; i < context.Action.ParticipationSlots.Count; i++)
			{
				ActionSlotDefinition candidate = context.Action.ParticipationSlots[i];
				if (candidate != null &&
					(string.Equals(candidate.Key, PackSlotKey, StringComparison.Ordinal) ||
					 (string.IsNullOrWhiteSpace(PackSlotKey) && context.Action.ParticipationSlots.Count == 1)))
				{
					packSlot = candidate;
					break;
				}
			}
			if (packSlot != null && (packSlot.MinimumParticipants != 1 || packSlot.MaximumParticipants != 1))
			{
				context.AddError("ACTION_RESULT_PACK_PARTICIPANT_COUNT_INVALID", $"打开卡包行动 {context.Action.ContentId} 的卡包槽位必须且只能绑定一张卡。");
			}
		}
	}

	/// <summary>研究结果池中的一个可解锁行动及其对应配方卡。</summary>
	[Serializable]
	public sealed class ResearchDiscoveryEntry
	{
		[SerializeField]
		[ContentIdReference(typeof(ActionDefinition))]
		[LabelText("解锁行动")]
		[Tooltip("研究成功后写入当前单局发现集合的行动。")]
		private ContentId m_actionId;

		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("配方卡")]
		[Tooltip("解锁该行动时在牌桌上生成的对应配方卡。")]
		private ContentId m_recipeCardId;

		public ContentId ActionId => m_actionId;

		public ContentId RecipeCardId => m_recipeCardId;
	}

	/// <summary>
	/// 声明研究完成后从当前尚未发现的候选中权威随机选择一项，生成对应配方卡并解锁行动。
	/// </summary>
	[Serializable]
	public sealed class ResearchDiscoveryResultIntent : ActionResultIntent
	{
		[SerializeField]
		[LabelText("研究候选")]
		[Tooltip("研究只会从当前单局尚未发现的候选中选择；全部发现后本结果不再生成卡牌。")]
		private ResearchDiscoveryEntry[] m_entries = Array.Empty<ResearchDiscoveryEntry>();

		[SerializeField]
		[ActionSlotReference]
		[LabelText("配方卡生成位置")]
		[Tooltip("选择配方卡生成位置所依据的参与槽位。单槽位行动可以自动推导。")]
		private string m_anchorSlotKey;

		public IReadOnlyList<ResearchDiscoveryEntry> Entries =>
			m_entries ?? Array.Empty<ResearchDiscoveryEntry>();

		public string AnchorSlotKey => m_anchorSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			if (Entries.Count == 0)
			{
				context.AddError(
					"ACTION_RESULT_RESEARCH_POOL_EMPTY",
					$"行动 {context.Action.ContentId} 的研究结果没有配置任何候选。");
			}

			HashSet<ContentId> actionIds = new HashSet<ContentId>();
			for (int i = 0; i < Entries.Count; i++)
			{
				ResearchDiscoveryEntry entry = Entries[i];
				if (entry == null)
				{
					context.AddError(
						"ACTION_RESULT_RESEARCH_ENTRY_NULL",
						$"行动 {context.Action.ContentId} 的第 {i + 1} 个研究候选为空。");
					continue;
				}
				if (!entry.ActionId.IsValid ||
					!context.Content.TryGet(entry.ActionId, out ActionDefinition _))
				{
					context.AddError(
						"ACTION_RESULT_RESEARCH_ACTION_INVALID",
						$"行动 {context.Action.ContentId} 的研究候选引用了不存在或类型错误的行动 {entry.ActionId}。");
				}
				else if (!actionIds.Add(entry.ActionId))
				{
					context.AddError(
						"ACTION_RESULT_RESEARCH_ACTION_DUPLICATE",
						$"行动 {context.Action.ContentId} 重复配置研究候选 {entry.ActionId}。");
				}
				if (!entry.RecipeCardId.IsValid ||
					!context.Content.TryGet(entry.RecipeCardId, out CardDefinition _))
				{
					context.AddError(
						"ACTION_RESULT_RESEARCH_CARD_INVALID",
						$"行动 {context.Action.ContentId} 的研究候选 {entry.ActionId} 缺少有效配方卡 {entry.RecipeCardId}。");
				}
			}

			context.ValidateSlotReference(
				AnchorSlotKey,
				"ACTION_RESULT_RESEARCH_ANCHOR_SLOT_UNKNOWN");
		}
	}

	/// <summary>
	/// 声明行动完成后移除某个参与槽位中的牌桌卡牌。
	/// </summary>
	[Serializable]
	public sealed class RemoveCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ActionSlotReference]
		[LabelText("移除槽位")]
		[Tooltip("选择行动完成后要移除卡牌的参与槽位。单槽位行动可以保留自动推导；多槽位行动必须明确选择。")]
		private string m_slotKey;

		public string SlotKey => m_slotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(SlotKey, "ACTION_RESULT_REMOVE_SLOT_UNKNOWN");
		}
	}

	/// <summary>
	/// 声明行动完成后使用某个参与槽位中的卡牌一次；剩余次数降到零时由牌桌移除。
	/// </summary>
	[Serializable]
	public sealed class UseCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ActionSlotReference]
		[LabelText("使用槽位")]
		[Tooltip("选择行动完成后消耗一次使用次数的参与槽位。单槽位行动可以保留自动推导；多槽位行动必须明确选择。")]
		private string m_slotKey;

		public string SlotKey => m_slotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(SlotKey, "ACTION_RESULT_USE_SLOT_UNKNOWN");
		}
	}

	/// <summary>声明把货币卡存入箱子；存入数量受箱子当前剩余容量限制。</summary>
	[Serializable]
	public sealed class DepositCurrencyIntoChestResultIntent : ActionResultIntent
	{
		[SerializeField, ActionSlotReference, LabelText("箱子槽位")]
		private string m_chestSlotKey;

		[SerializeField, ActionSlotReference, LabelText("货币槽位")]
		private string m_currencySlotKey;

		public string ChestSlotKey => m_chestSlotKey ?? string.Empty;

		public string CurrencySlotKey => m_currencySlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(ChestSlotKey, "ACTION_RESULT_CHEST_DEPOSIT_CHEST_SLOT_UNKNOWN");
			context.ValidateSlotReference(CurrencySlotKey, "ACTION_RESULT_CHEST_DEPOSIT_CURRENCY_SLOT_UNKNOWN");
			if (context.Action.TurnCost != 0)
			{
				context.AddError(
					"ACTION_RESULT_CHEST_DEPOSIT_MUST_BE_IMMEDIATE",
					$"箱子存币行动 {context.Action.ContentId} 必须是 0 回合即时行动，避免存币计划跨回合占用货币和箱子状态。");
			}
		}
	}

	/// <summary>声明从箱子取出一枚货币卡。</summary>
	[Serializable]
	public sealed class WithdrawCurrencyFromChestResultIntent : ActionResultIntent
	{
		[SerializeField, ActionSlotReference, LabelText("箱子槽位")]
		private string m_chestSlotKey;

		public string ChestSlotKey => m_chestSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(ChestSlotKey, "ACTION_RESULT_CHEST_WITHDRAW_SLOT_UNKNOWN");
			if (context.Action.TurnCost != 0)
			{
				context.AddError(
					"ACTION_RESULT_CHEST_WITHDRAW_MUST_BE_IMMEDIATE",
					$"箱子取币行动 {context.Action.ContentId} 必须是 0 回合即时行动。");
			}
		}
	}
	/// <summary>声明行动完成后移除可出售卡牌，并按其出售价值生成货币卡。</summary>
	[Serializable]
	public sealed class SellCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ActionSlotReference]
		[LabelText("出售槽位")]
		[Tooltip("行动完成后被出售并从牌桌移除的卡牌槽位。")]
		private string m_soldSlotKey;

		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("货币卡牌")]
		[Tooltip("按被售卡牌出售价值生成的货币卡。")]
		private ContentId m_currencyCardId;

		[SerializeField]
		[ActionSlotReference]
		[LabelText("出售目标槽位")]
		[Tooltip("用于确认本次出售拖到的收购点等交互目标存在；货币生成在被售卡牌释放出的牌堆位置。")]
		private string m_anchorSlotKey;

		public string SoldSlotKey => m_soldSlotKey ?? string.Empty;

		public ContentId CurrencyCardId => m_currencyCardId;

		public string AnchorSlotKey => m_anchorSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			context.ValidateSlotReference(SoldSlotKey, "ACTION_RESULT_SELL_SLOT_UNKNOWN");
			context.ValidateSlotReference(AnchorSlotKey, "ACTION_RESULT_SELL_ANCHOR_SLOT_UNKNOWN");
			if (!CurrencyCardId.IsValid ||
				!context.Content.TryGet(CurrencyCardId, out CardDefinition _))
			{
				context.AddError(
					"ACTION_RESULT_SELL_CURRENCY_INVALID",
					$"行动 {context.Action.ContentId} 的售卡结果缺少有效货币卡 {CurrencyCardId}。");
			}
		}
	}

	/// <summary>
	/// 声明行动完成后在参与卡牌位置生成指定内容卡牌。
	/// </summary>
	[Serializable]
	public sealed class CreateCardsResultIntent : ActionResultIntent
	{
		[SerializeField]
		[ContentIdReference(typeof(CardDefinition))]
		[LabelText("产物卡牌")]
		[Tooltip("行动完成后生成的卡牌定义。编辑器自动保存其唯一内容 ID，不需要手填。")]
		private ContentId m_contentId;

		[SerializeField]
		[Min(1f)]
		[LabelText("生成数量")]
		[Tooltip("行动完成后生成的卡牌数量，必须大于 0。")]
		private int m_count = 1;

		[SerializeField]
		[ActionSlotReference]
		[LabelText("生成位置")]
		[Tooltip("选择产物生成位置所依据的参与槽位。单槽位行动可以保留自动推导；多槽位行动必须明确选择。")]
		private string m_anchorSlotKey;

		public ContentId ContentId => m_contentId;

		public int Count => m_count;

		public string AnchorSlotKey => m_anchorSlotKey ?? string.Empty;

		protected override void ValidateResult(ActionResultValidationContext context)
		{
			if (!ContentId.IsValid)
			{
				context.AddError(
					"ACTION_RESULT_CREATE_CONTENT_INVALID",
					$"行动 {context.Action.ContentId} 的产物内容 ID 无效：{ContentId}。");
			}
			else if (!context.Content.TryGet(ContentId, out ContentAsset _))
			{
				context.AddError(
					"ACTION_RESULT_CREATE_CONTENT_UNKNOWN",
					$"行动 {context.Action.ContentId} 的产物内容 {ContentId} 没有进入当前内容作者源集合。");
			}

			if (Count <= 0)
			{
				context.AddError(
					"ACTION_RESULT_CREATE_COUNT_INVALID",
					$"行动 {context.Action.ContentId} 的产物生成数量必须大于 0，当前值为 {Count}。");
			}
			context.ValidateSlotReference(
				AnchorSlotKey,
				"ACTION_RESULT_CREATE_ANCHOR_SLOT_UNKNOWN");
		}
	}
}
