using System;
using System.Collections.Generic;
using GAS.Runtime;
using Gameplay.Content;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 角色卡是牌桌卡牌的正式派生类型，直接拥有该角色唯一的 EX-GAS 能力、属性、标签与效果状态。
	/// </summary>
	public sealed class CharacterCard : TabletopCard
	{
		private readonly AbilitySystemCellConfig m_abilitySystemConfig;
		private readonly Dictionary<ContentId, EquippedCardState> m_equippedCards =
			new Dictionary<ContentId, EquippedCardState>();

		public AbilitySystemCell AbilitySystem { get; }

		/// <summary>角色进入即时战斗后由战斗聚合自动请求的 EX-GAS Ability；0 表示不自动行动。</summary>
		public int AutomaticBattleAbilityCode { get; }

		public float CurrentHealth => AbilitySystem.GetAttrCurrentValue(
			XAttrSet.FightUnit,
			XAttribute.Health);

		public float MaxHealth => AbilitySystem.GetAttrCurrentValue(
			XAttrSet.FightUnit,
			XAttribute.MaxHealth);

		/// <summary>当前已装备卡牌数量；装备事实只能由角色装备入口修改。</summary>
		public int EquippedCardCount => m_equippedCards.Count;

		/// <summary>枚举角色当前装备事实；调用方只能读取，不能绕过装备 / 卸装入口修改。</summary>
		public IEnumerable<EquippedCardState> EquippedCards => m_equippedCards.Values;

		internal CharacterCard(
			TabletopCardId id,
			ContentId contentId,
			AbilitySystemCellConfig abilitySystemConfig,
			int automaticBattleAbilityCode,
			int remainingUses = 1,
			float periodicProductionElapsedSeconds = 0f,
			float automaticMovementElapsedSeconds = 0f)
			: base(id, contentId, remainingUses, periodicProductionElapsedSeconds, automaticMovementElapsedSeconds)
		{
			if (automaticBattleAbilityCode < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(automaticBattleAbilityCode),
					automaticBattleAbilityCode,
					"角色卡的自动战斗 Ability 不能小于 0。");
			}
			m_abilitySystemConfig = abilitySystemConfig;
			AutomaticBattleAbilityCode = automaticBattleAbilityCode;
			AbilitySystem = new AbilitySystemCell();
			try
			{
				AbilitySystem.Init(
					abilitySystemConfig.BaseTags ?? Array.Empty<int>(),
					abilitySystemConfig.AttrSets ?? Array.Empty<AttrSetConfig>(),
					abilitySystemConfig.BaseAbilities ?? Array.Empty<AbilityConfig>(),
					abilitySystemConfig.Level);
				if (automaticBattleAbilityCode > 0 &&
					AbilitySystem.GetAbilitySpec(automaticBattleAbilityCode) == null)
				{
					throw new InvalidOperationException(
						$"角色卡 {id} 的 ASC 没有自动战斗 Ability {automaticBattleAbilityCode}。");
				}
			}
			catch
			{
				AbilitySystem.Dispose();
				throw;
			}
		}

		internal CharacterCard(
			TabletopCardId id,
			ContentId contentId,
			AbilitySystemCellConfig abilitySystemConfig,
			int automaticBattleAbilityCode,
			CharacterAbilitySystemSnapshot snapshot,
			int remainingUses = 1,
			float periodicProductionElapsedSeconds = 0f,
			float automaticMovementElapsedSeconds = 0f)
			: this(
				id,
				contentId,
				abilitySystemConfig,
				automaticBattleAbilityCode,
				remainingUses,
				periodicProductionElapsedSeconds,
				automaticMovementElapsedSeconds)
		{
			try
			{
				RestoreAbilitySystem(snapshot);
			}
			catch
			{
				AbilitySystem.Dispose();
				throw;
			}
		}

		protected internal override TabletopCardRuntimeStateSnapshot CreateRuntimeStateSnapshot()
		{
			AttrSetConfig[] attributeSets = m_abilitySystemConfig.AttrSets ?? Array.Empty<AttrSetConfig>();
			CharacterAttributeSetSnapshot[] setSnapshots = new CharacterAttributeSetSnapshot[attributeSets.Length];
			for (int setIndex = 0; setIndex < attributeSets.Length; setIndex++)
			{
				AttrSetConfig set = attributeSets[setIndex];
				AttributeBaseSetting[] settings = set.Settings ?? Array.Empty<AttributeBaseSetting>();
				CharacterAttributeSnapshot[] attributes = new CharacterAttributeSnapshot[settings.Length];
				for (int attributeIndex = 0; attributeIndex < settings.Length; attributeIndex++)
				{
					attributes[attributeIndex] = new CharacterAttributeSnapshot(
						settings[attributeIndex].Code,
						AbilitySystem.GetAttrBaseValue(set.Code, settings[attributeIndex].Code));
				}
				setSnapshots[setIndex] = new CharacterAttributeSetSnapshot(set.Code, attributes);
			}
			List<EquippedCardSnapshot> equipmentSnapshots = new List<EquippedCardSnapshot>(m_equippedCards.Count);
			foreach (EquippedCardState equipped in m_equippedCards.Values)
			{
				equipmentSnapshots.Add(equipped.CreateSnapshot());
			}
			equipmentSnapshots.Sort((left, right) =>
				string.CompareOrdinal(left.SlotId.Value, right.SlotId.Value));
			return new CharacterAbilitySystemSnapshot(
				AbilitySystem.GetLevel(),
				setSnapshots,
				equipmentSnapshots);
		}

		/// <summary>查询角色当前槽位上的装备；结果只读，修改必须走装备 / 卸装入口。</summary>
		public bool TryGetEquippedCard(ContentId slotId, out EquippedCardState state)
		{
			if (!slotId.IsValid)
			{
				state = null;
				return false;
			}
			return m_equippedCards.TryGetValue(slotId, out state);
		}

		/// <summary>把桌面装备卡挂到角色指定槽位；返回被替换下来的旧装备快照，供牌桌恢复回桌。</summary>
		internal EquippedCardSnapshot Equip(
			EquipmentCardDefinition definition,
			TabletopCard equipmentCard)
		{
			if (definition == null)
			{
				throw new ArgumentNullException(nameof(definition));
			}
			if (equipmentCard == null)
			{
				throw new ArgumentNullException(nameof(equipmentCard));
			}
			if (equipmentCard.ContentId != definition.ContentId)
			{
				throw new InvalidOperationException(
					$"角色卡 {Id} 要装备的卡牌 {equipmentCard.Id} 与装备作者源 {definition.ContentId} 不一致。");
			}
			if (!definition.SlotId.IsValid)
			{
				throw new InvalidOperationException($"装备卡 {definition.ContentId} 缺少有效装备槽位。");
			}

			GameplayEffectSpec newEffect = CharacterEquipmentEffects.Apply(
				this,
				definition.OnEquippedGameplayEffectId);
			EquippedCardState newState = new EquippedCardState(
				definition.SlotId,
				equipmentCard.CreateSnapshot(),
				definition.OnEquippedGameplayEffectId,
				newEffect);
			if (!m_equippedCards.Remove(definition.SlotId, out EquippedCardState oldState))
			{
				m_equippedCards.Add(definition.SlotId, newState);
				return null;
			}

			CharacterEquipmentEffects.Remove(oldState.ActiveEffect);
			m_equippedCards.Add(definition.SlotId, newState);
			return oldState.CreateSnapshot();
		}

		/// <summary>卸下指定槽位装备；返回旧装备快照，供牌桌恢复回桌。</summary>
		internal EquippedCardSnapshot Unequip(ContentId slotId)
		{
			if (!slotId.IsValid)
			{
				throw new ArgumentException("卸装必须引用有效装备槽位。", nameof(slotId));
			}
			if (!m_equippedCards.Remove(slotId, out EquippedCardState state))
			{
				throw new InvalidOperationException($"角色卡 {Id} 的槽位 {slotId} 没有可卸下装备。");
			}

			CharacterEquipmentEffects.Remove(state.ActiveEffect);
			return state.CreateSnapshot();
		}

		private void RestoreAbilitySystem(CharacterAbilitySystemSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new InvalidOperationException($"角色卡 {Id} 的快照缺少 EX-GAS 长期状态。");
			}
			if (snapshot.Level < 0)
			{
				throw new InvalidOperationException($"角色卡 {Id} 的 ASC 存档等级不能为负数。");
			}
			AttrSetConfig[] configuredSets = m_abilitySystemConfig.AttrSets ?? Array.Empty<AttrSetConfig>();
			IReadOnlyList<CharacterAttributeSetSnapshot> savedSets = snapshot.AttributeSets
				?? throw new InvalidOperationException($"角色卡 {Id} 的快照缺少属性集集合。");
			if (savedSets.Count != configuredSets.Length)
			{
				throw new InvalidOperationException($"角色卡 {Id} 的 ASC 预设属性集结构已与存档不兼容。");
			}

			Dictionary<int, CharacterAttributeSetSnapshot> savedBySet = new Dictionary<int, CharacterAttributeSetSnapshot>(savedSets.Count);
			for (int i = 0; i < savedSets.Count; i++)
			{
				CharacterAttributeSetSnapshot savedSet = savedSets[i]
					?? throw new InvalidOperationException($"角色卡 {Id} 的第 {i} 个属性集快照为空。");
				if (!savedBySet.TryAdd(savedSet.AttributeSetCode, savedSet))
				{
					throw new InvalidOperationException($"角色卡 {Id} 的快照重复保存属性集 {savedSet.AttributeSetCode}。");
				}
			}

			for (int setIndex = 0; setIndex < configuredSets.Length; setIndex++)
			{
				AttrSetConfig configuredSet = configuredSets[setIndex];
				if (!savedBySet.TryGetValue(configuredSet.Code, out CharacterAttributeSetSnapshot savedSet))
				{
					throw new InvalidOperationException($"角色卡 {Id} 的快照缺少属性集 {configuredSet.Code}。");
				}
				RestoreAttributeSet(configuredSet, savedSet);
			}
			AbilitySystem.SetLevel(snapshot.Level);
			RestoreEquipment(snapshot);
		}

		private void RestoreEquipment(CharacterAbilitySystemSnapshot snapshot)
		{
			IReadOnlyList<EquippedCardSnapshot> equipment = snapshot.EquippedCards;
			for (int i = 0; i < equipment.Count; i++)
			{
				EquippedCardSnapshot equippedSnapshot = equipment[i]
					?? throw new InvalidOperationException($"角色卡 {Id} 的第 {i + 1} 个装备快照为空。");
				EquippedCardState restored = equippedSnapshot.Restore(this);
				if (!m_equippedCards.TryAdd(restored.SlotId, restored))
				{
					CharacterEquipmentEffects.Remove(restored.ActiveEffect);
					throw new InvalidOperationException(
						$"角色卡 {Id} 的快照重复保存装备槽位 {restored.SlotId}。");
				}
			}
		}

		private void RestoreAttributeSet(AttrSetConfig configuredSet, CharacterAttributeSetSnapshot savedSet)
		{
			AttributeBaseSetting[] settings = configuredSet.Settings ?? Array.Empty<AttributeBaseSetting>();
			IReadOnlyList<CharacterAttributeSnapshot> savedAttributes = savedSet.Attributes
				?? throw new InvalidOperationException($"角色卡 {Id} 的属性集 {configuredSet.Code} 缺少属性集合。");
			if (savedAttributes.Count != settings.Length)
			{
				throw new InvalidOperationException($"角色卡 {Id} 的属性集 {configuredSet.Code} 已与存档结构不兼容。");
			}

			Dictionary<int, float> savedValues = new Dictionary<int, float>(savedAttributes.Count);
			for (int i = 0; i < savedAttributes.Count; i++)
			{
				CharacterAttributeSnapshot savedAttribute = savedAttributes[i]
					?? throw new InvalidOperationException($"角色卡 {Id} 的属性集 {configuredSet.Code} 包含空属性快照。");
				if (!float.IsFinite(savedAttribute.BaseValue) || !savedValues.TryAdd(savedAttribute.AttributeCode, savedAttribute.BaseValue))
				{
					throw new InvalidOperationException($"角色卡 {Id} 的属性集 {configuredSet.Code} 包含重复或非法属性 {savedAttribute.AttributeCode}。");
				}
			}

			for (int i = 0; i < settings.Length; i++)
			{
				int attributeCode = settings[i].Code;
				if (!savedValues.TryGetValue(attributeCode, out float baseValue))
				{
					throw new InvalidOperationException($"角色卡 {Id} 的属性集 {configuredSet.Code} 缺少属性 {attributeCode}。");
				}
				AbilitySystem.SetAttrBaseValue(configuredSet.Code, attributeCode, baseValue);
			}
		}

		internal void Dispose()
		{
			foreach (EquippedCardState equipped in m_equippedCards.Values)
			{
				CharacterEquipmentEffects.Remove(equipped.ActiveEffect);
			}
			m_equippedCards.Clear();
			AbilitySystem.Dispose();
		}
	}
}
