using System;
using System.Collections.Generic;
using GAS.General;
using GAS.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 角色卡作者源。卡面与内容身份继承普通卡牌，初始能力、属性和运行时标签只引用 EX-GAS ASC 预设。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/角色卡", fileName = "角色卡_")]
	public class CharacterCardDefinition : CardDefinition
	{
		[SerializeField]
		[ValueDropdown("@GetAbilitySystemPresetChoices()")]
		[LabelText("ASC 预设")]
		[Tooltip("引用 EX-GAS ASC 表中的正式预设。角色卡不会在 Gameplay 侧重复配置标签、属性集或基础技能。")]
		private int m_abilitySystemPresetId;

		public int AbilitySystemPresetId => m_abilitySystemPresetId;

		internal AbilitySystemCellConfig CreateAbilitySystemConfig()
		{
			if (m_abilitySystemPresetId <= 0)
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 没有配置有效的 EX-GAS ASC 预设。");
			}

			try
			{
				return XLuban.GetAscConfig(m_abilitySystemPresetId);
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 引用的 EX-GAS ASC 预设 {m_abilitySystemPresetId} 不存在或无法解析。",
					exception);
			}
		}

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (m_abilitySystemPresetId <= 0)
			{
				context.AddError(
					"CHARACTER_CARD_ASC_PRESET_INVALID",
					$"角色卡 {ContentId} 必须引用有效的 EX-GAS ASC 预设。",
					this);
			}
		}

		#if UNITY_EDITOR
		private static IEnumerable<ValueDropdownItem<int>> GetAbilitySystemPresetChoices()
		{
			GeneralGasChoiceHelper.LoadCache();
			List<int> presetIds = GasChoiceRawAccessor.GetLubanTableKeysToList("Tbasc");
			if (presetIds == null)
			{
				yield break;
			}
			for (int index = 0; index < presetIds.Count; index++)
			{
				int presetId = presetIds[index];
				string presetName = ReflectionPathHelper.GetRawMemberById<string>(
					"Tbasc",
					presetId,
					"Name",
					"GAS.Runtime.XLuban");
				yield return new ValueDropdownItem<int>($"[{presetId}]{presetName}", presetId);
			}
		}
		#endif
	}
}
