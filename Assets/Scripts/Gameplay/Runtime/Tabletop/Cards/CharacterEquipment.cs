using System;
using System.Collections.Generic;
using GAS.Runtime;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>角色当前装备的一张离桌卡牌及其持续 GameplayEffect 事实。</summary>
	public sealed class EquippedCardState
	{
		public ContentId SlotId { get; }

		public TabletopCardSnapshot CardSnapshot { get; }

		public int OnEquippedGameplayEffectId { get; }

		internal GameplayEffectSpec ActiveEffect { get; }

		internal EquippedCardState(
			ContentId slotId,
			TabletopCardSnapshot cardSnapshot,
			int onEquippedGameplayEffectId,
			GameplayEffectSpec activeEffect)
		{
			if (!slotId.IsValid)
			{
				throw new ArgumentException("装备状态必须引用有效装备槽位。", nameof(slotId));
			}
			SlotId = slotId;
			CardSnapshot = cardSnapshot ?? throw new ArgumentNullException(nameof(cardSnapshot));
			OnEquippedGameplayEffectId = onEquippedGameplayEffectId > 0
				? onEquippedGameplayEffectId
				: throw new ArgumentOutOfRangeException(nameof(onEquippedGameplayEffectId));
			ActiveEffect = activeEffect ?? throw new ArgumentNullException(nameof(activeEffect));
		}

		internal EquippedCardSnapshot CreateSnapshot()
		{
			return new EquippedCardSnapshot(SlotId, CardSnapshot, OnEquippedGameplayEffectId);
		}
	}

	/// <summary>角色装备状态的可序列化事实；不保存运行时 GameplayEffectSpec 实例。</summary>
	[Serializable]
	public sealed class EquippedCardSnapshot
	{
		[SerializeField]
		private ContentId m_slotId;

		[SerializeField]
		private TabletopCardSnapshot m_cardSnapshot;

		[SerializeField]
		private int m_onEquippedGameplayEffectId;

		public ContentId SlotId => m_slotId;

		public TabletopCardSnapshot CardSnapshot => m_cardSnapshot;

		public int OnEquippedGameplayEffectId => m_onEquippedGameplayEffectId;

		internal EquippedCardSnapshot(
			ContentId slotId,
			TabletopCardSnapshot cardSnapshot,
			int onEquippedGameplayEffectId)
		{
			if (!slotId.IsValid)
			{
				throw new ArgumentException("装备快照必须引用有效装备槽位。", nameof(slotId));
			}
			m_slotId = slotId;
			m_cardSnapshot = cardSnapshot ?? throw new ArgumentNullException(nameof(cardSnapshot));
			m_onEquippedGameplayEffectId = onEquippedGameplayEffectId > 0
				? onEquippedGameplayEffectId
				: throw new ArgumentOutOfRangeException(nameof(onEquippedGameplayEffectId));
		}

		internal EquippedCardState Restore(CharacterCard character)
		{
			if (character == null)
			{
				throw new ArgumentNullException(nameof(character));
			}
			if (!SlotId.IsValid || CardSnapshot == null || OnEquippedGameplayEffectId <= 0)
			{
				throw new InvalidOperationException($"角色卡 {character.Id} 的装备快照无效。");
			}
			return new EquippedCardState(
				SlotId,
				CardSnapshot,
				OnEquippedGameplayEffectId,
				CharacterEquipmentEffects.Apply(character, OnEquippedGameplayEffectId));
		}
	}

	internal static class CharacterEquipmentEffects
	{
		internal static GameplayEffectSpec Apply(CharacterCard character, int gameplayEffectId)
		{
			if (character == null)
			{
				throw new ArgumentNullException(nameof(character));
			}
			GameplayEffectConfig effect = GameplayEffectHelper.GetConfigByID(gameplayEffectId)
				?? throw new InvalidOperationException(
					$"角色卡 {character.Id} 要施加的装备 GameplayEffect {gameplayEffectId} 不存在。");
			GameplayEffectSpec spec = new GameplayEffectSpec(effect.ComponentConfigs);
			spec.ApplyToSelf(character.AbilitySystem);
			return spec;
		}

		internal static void Remove(GameplayEffectSpec spec)
		{
			if (spec == null)
			{
				throw new ArgumentNullException(nameof(spec));
			}
			spec.Remove();
		}
	}
}
