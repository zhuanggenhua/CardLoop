using GAS.General;
using GAS.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 装备卡作者源。装备后的数值、标签和持续效果只引用 EX-GAS GameplayEffect。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡牌/装备", fileName = "装备_")]
	public sealed class EquipmentCardDefinition : CardDefinition
	{
		[SerializeField]
		[ContentIdReference(typeof(EquipmentSlotDefinition))]
		[LabelText("装备槽位")]
		[Tooltip("这张装备占用的正式槽位。槽位使用内容 ID，允许 Mod 新增槽位。")]
		private ContentId m_slotId;

		[SerializeField]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.GameplayEffects()")]
		[LabelText("装备时 GE")]
		[Tooltip("装备后施加到角色 ASC 的 EX-GAS GameplayEffect。卸下或替换时通过 GE 正式移除入口撤销。")]
		private int m_onEquippedGameplayEffectId;

		public ContentId SlotId => m_slotId;

		public int OnEquippedGameplayEffectId => m_onEquippedGameplayEffectId;

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (!SlotId.IsValid || !context.TryGet(SlotId, out EquipmentSlotDefinition _))
			{
				context.AddError(
					"EQUIPMENT_SLOT_INVALID",
					$"装备卡 {ContentId} 缺少有效装备槽位 {SlotId}。",
					this);
			}
			if (OnEquippedGameplayEffectId <= 0 ||
				GameplayEffectHelper.GetConfigByID(OnEquippedGameplayEffectId) == null)
			{
				context.AddError(
					"EQUIPMENT_EFFECT_INVALID",
					$"装备卡 {ContentId} 引用的 EX-GAS GameplayEffect {OnEquippedGameplayEffectId} 不存在。",
					this);
			}
		}

	}
}

