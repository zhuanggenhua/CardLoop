using System;
using GAS.Runtime;
using Gameplay.Content;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 角色卡是牌桌卡牌的正式派生类型，直接拥有该角色唯一的 EX-GAS 能力、属性、标签与效果状态。
	/// </summary>
	public sealed class CharacterCard : TabletopCard
	{
		public AbilitySystemCell AbilitySystem { get; }

		public float CurrentHealth => AbilitySystem.GetAttrCurrentValue(
			XAttrSet.FightUnit,
			XAttribute.Health);

		public float MaxHealth => AbilitySystem.GetAttrCurrentValue(
			XAttrSet.FightUnit,
			XAttribute.MaxHealth);

		internal CharacterCard(
			TabletopCardId id,
			ContentId contentId,
			AbilitySystemCellConfig abilitySystemConfig)
			: base(id, contentId)
		{
			AbilitySystem = new AbilitySystemCell();
			try
			{
				AbilitySystem.Init(
					abilitySystemConfig.BaseTags ?? Array.Empty<int>(),
					abilitySystemConfig.AttrSets ?? Array.Empty<AttrSetConfig>(),
					abilitySystemConfig.BaseAbilities ?? Array.Empty<AbilityConfig>(),
					abilitySystemConfig.Level);
			}
			catch
			{
				AbilitySystem.Dispose();
				throw;
			}
		}

		internal void Dispose()
		{
			AbilitySystem.Dispose();
		}
	}
}
