using System;
using System.Collections.Generic;
using GAS.General;
using GAS.Runtime;
using GameCore;
using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 角色卡作者源。卡面与内容身份继承普通卡牌，能力和运行时标签引用 EX-GAS ASC 预设，属性差异只覆盖正式 FightUnit 属性集。
	/// </summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/角色卡", fileName = "角色卡_")]
	public class CharacterCardDefinition : CardDefinition
	{
		[SerializeField]
		[ValueDropdown("@GetAbilitySystemPresetChoices()")]
		[LabelText("ASC 预设")]
		[Tooltip("引用 EX-GAS ASC 表中的正式预设。角色卡不会在 Gameplay 侧重复配置标签、属性集或基础技能。")]
		private int m_abilitySystemPresetId;

		[Header("属性覆盖")]
		[SerializeField]
		[LabelText("角色基础值覆盖")]
		[Tooltip("只填写该角色不同于 EX-GAS FightUnit 默认值的属性；属性身份和钳制规则仍由 EX-GAS 表维护。")]
		private CharacterAttributeOverride[] m_attributeOverrides = Array.Empty<CharacterAttributeOverride>();

		[SerializeField]
		[ValueDropdown("@GetAbilityChoices()")]
		[LabelText("自动战斗能力")]
		[Tooltip("角色参与即时战斗时自动使用的 EX-GAS Ability。0 表示该角色不会自动行动，但仍可作为战斗目标。")]
		private int m_automaticBattleAbilityCode;

		[Header("自动敌对行为")]
		[SerializeField]
		[Min(0f)]
		[LabelText("索敌半径")]
		[Tooltip("拥有 Faction.Enemy 标签的角色卡在自动移动触发时，会在这个牌桌距离内寻找玩家角色或玩家参与的战斗。0 表示不启用敌对 AI。")]
		private float m_automaticAggroRadius;

		[SerializeField]
		[Min(0f)]
		[LabelText("攻击半径")]
		[Tooltip("敌对角色与目标距离小于等于该值时，会加入战斗或发起战斗；必须小于等于索敌半径。")]
		private float m_automaticAttackRadius;

		public int AbilitySystemPresetId => m_abilitySystemPresetId;

		public int AutomaticBattleAbilityCode => m_automaticBattleAbilityCode;

		public float AutomaticAggroRadius => m_automaticAggroRadius;

		public float AutomaticAttackRadius => m_automaticAttackRadius;

		public bool HasAutomaticHostileBehavior =>
			AutomaticAggroRadius > 0f || AutomaticAttackRadius > 0f;

		public override bool UsesAutomaticMovement =>
			base.UsesAutomaticMovement || HasAutomaticHostileBehavior;

		internal AbilitySystemCellConfig CreateAbilitySystemConfig()
		{
			if (m_abilitySystemPresetId <= 0)
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 没有配置有效的 EX-GAS ASC 预设。");
			}

			AbilitySystemCellConfig config;
			try
			{
				config = XLuban.GetAscConfig(m_abilitySystemPresetId);
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 引用的 EX-GAS ASC 预设 {m_abilitySystemPresetId} 不存在或无法解析。",
					exception);
			}

			if (IsEmptyAbilitySystemFallback(config))
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 引用的 EX-GAS ASC 预设 {m_abilitySystemPresetId} 没有生成任何标签、属性集、能力或等级。"
					+ "这通常表示预设 ID 不存在或配置表尚未正确生成。");
			}

			return ApplyAttributeOverrides(config);
		}

		private static bool IsEmptyAbilitySystemFallback(AbilitySystemCellConfig config)
		{
			return config.Level == 0 &&
				(config.BaseTags == null || config.BaseTags.Length == 0) &&
				(config.AttrSets == null || config.AttrSets.Length == 0) &&
				(config.BaseAbilities == null || config.BaseAbilities.Length == 0);
		}

		private AbilitySystemCellConfig ApplyAttributeOverrides(AbilitySystemCellConfig config)
		{
			if (m_attributeOverrides == null || m_attributeOverrides.Length == 0)
			{
				return config;
			}

			AttrSetConfig[] sourceSets = config.AttrSets ?? Array.Empty<AttrSetConfig>();
			if (sourceSets.Length == 0)
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 配置了属性覆盖，但 ASC 预设 {m_abilitySystemPresetId} 没有 FightUnit 属性集。");
			}

			AttrSetConfig overrideSet = CharacterAttributes.CreateConfig(m_attributeOverrides);
			AttrSetConfig[] attrSets = new AttrSetConfig[sourceSets.Length];
			bool replaced = false;
			for (int setIndex = 0; setIndex < sourceSets.Length; setIndex++)
			{
				if (sourceSets[setIndex].Code == CharacterAttributes.SetCode)
				{
					attrSets[setIndex] = overrideSet;
					replaced = true;
				}
				else
				{
					attrSets[setIndex] = sourceSets[setIndex];
				}
			}

			if (!replaced)
			{
				throw new InvalidOperationException(
					$"角色卡 {ContentId} 配置了属性覆盖，但 ASC 预设 {m_abilitySystemPresetId} 不包含 EX-GAS FightUnit 属性集 {CharacterAttributes.SetCode}。");
			}

			config.SetAttrSets(attrSets);
			return config;
		}

		protected internal override TabletopCard CreateRuntimeCard(TabletopCardId id)
		{
			return new CharacterCard(
				id,
				ContentId,
				CreateAbilitySystemConfig(),
				AutomaticBattleAbilityCode,
				InitialUses);
		}

		protected internal override TabletopCard RestoreRuntimeCard(TabletopCardSnapshot snapshot)
		{
			if (snapshot.RuntimeState is not CharacterAbilitySystemSnapshot abilitySystem)
			{
				throw new System.InvalidOperationException(
					$"角色卡 {snapshot.CardId} 的快照缺少角色能力状态。");
			}
			return new CharacterCard(
				snapshot.CardId,
				ContentId,
				CreateAbilitySystemConfig(),
				AutomaticBattleAbilityCode,
				abilitySystem,
				snapshot.RemainingUses,
				snapshot.PeriodicProductionElapsedSeconds,
				snapshot.AutomaticMovementElapsedSeconds);
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
				return;
			}

			AbilitySystemCellConfig config;
			try
			{
				config = CreateAbilitySystemConfig();
			}
			catch (Exception exception)
			{
				context.AddError(
					"CHARACTER_CARD_ASC_CONFIG_INVALID",
					$"角色卡 {ContentId} 的 EX-GAS ASC 配置无效：{exception.Message}",
					this);
				return;
			}

			if (m_automaticBattleAbilityCode < 0)
			{
				context.AddError(
					"CHARACTER_CARD_AUTOMATIC_BATTLE_ABILITY_INVALID",
					$"角色卡 {ContentId} 的自动战斗 Ability 不能小于 0。",
					this);
			}
			else if (m_automaticBattleAbilityCode > 0 &&
				!ContainsAbility(config, m_automaticBattleAbilityCode))
			{
				context.AddError(
					"CHARACTER_CARD_AUTOMATIC_BATTLE_ABILITY_MISSING",
					$"角色卡 {ContentId} 的 ASC 预设 {m_abilitySystemPresetId} 不包含自动战斗 Ability {m_automaticBattleAbilityCode}。",
					this);
			}
			ValidateAutomaticHostileBehavior(context, config);
		}

		private static bool ContainsAbility(AbilitySystemCellConfig config, int abilityCode)
		{
			AbilityConfig[] abilities = config.BaseAbilities ?? Array.Empty<AbilityConfig>();
			for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
			{
				AbilityComponentConfig[] components = abilities[abilityIndex].ComponentConfigs
					?? Array.Empty<AbilityComponentConfig>();
				for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
				{
					if (components[componentIndex] is ConfAbilityBaseInfo baseInfo &&
						baseInfo.Code == abilityCode)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void ValidateAutomaticHostileBehavior(
			ContentValidationContext context,
			AbilitySystemCellConfig config)
		{
			if (!HasAutomaticHostileBehavior)
			{
				return;
			}

			if (!float.IsFinite(AutomaticAggroRadius) || AutomaticAggroRadius <= 0f)
			{
				context.AddError(
					"CHARACTER_CARD_AUTOMATIC_AGGRO_RADIUS_INVALID",
					$"角色卡 {ContentId} 启用自动敌对行为时，索敌半径必须大于 0。",
					this);
			}
			if (!float.IsFinite(AutomaticAttackRadius) || AutomaticAttackRadius <= 0f)
			{
				context.AddError(
					"CHARACTER_CARD_AUTOMATIC_ATTACK_RADIUS_INVALID",
					$"角色卡 {ContentId} 启用自动敌对行为时，攻击半径必须大于 0。",
					this);
			}
			if (float.IsFinite(AutomaticAggroRadius) &&
				float.IsFinite(AutomaticAttackRadius) &&
				AutomaticAggroRadius > 0f &&
				AutomaticAttackRadius > AutomaticAggroRadius)
			{
				context.AddError(
					"CHARACTER_CARD_AUTOMATIC_ATTACK_RADIUS_EXCEEDS_AGGRO",
					$"角色卡 {ContentId} 的攻击半径不能大于索敌半径。",
					this);
			}
			if (!ConfigHasTag(config, XTag.Faction_Enemy))
			{
				context.AddError(
					"CHARACTER_CARD_AUTOMATIC_HOSTILITY_REQUIRES_ENEMY_TAG",
					$"角色卡 {ContentId} 配置了自动敌对行为，但 ASC 预设 {m_abilitySystemPresetId} 没有 Faction.Enemy 标签。",
					this);
			}
			if (HasPeriodicProduction)
			{
				context.AddError(
					"CHARACTER_CARD_ENEMY_CANNOT_PRODUCE_PERIODICALLY",
					$"角色卡 {ContentId} 配置了自动敌对行为，不能同时配置周期产出。",
					this);
			}
		}

		private static bool ConfigHasTag(AbilitySystemCellConfig config, int tagCode)
		{
			int[] tags = config.BaseTags ?? Array.Empty<int>();
			for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
			{
				if (tags[tagIndex] == tagCode)
				{
					return true;
				}
			}
			return false;
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

		private static IEnumerable<ValueDropdownItem<int>> GetAbilityChoices()
		{
			GeneralGasChoiceHelper.LoadCache();
			yield return new ValueDropdownItem<int>("[0]不自动行动", 0);
			List<int> abilityIds = GasChoiceRawAccessor.GetLubanTableKeysToList("Tbability");
			if (abilityIds == null)
			{
				yield break;
			}
			for (int index = 0; index < abilityIds.Count; index++)
			{
				int abilityId = abilityIds[index];
				string abilityName = ReflectionPathHelper.GetRawMemberById<string>(
					"Tbability",
					abilityId,
					"Name",
					"GAS.Runtime.XLuban");
				yield return new ValueDropdownItem<int>($"[{abilityId}]{abilityName}", abilityId);
			}
		}
		#endif
	}
}
