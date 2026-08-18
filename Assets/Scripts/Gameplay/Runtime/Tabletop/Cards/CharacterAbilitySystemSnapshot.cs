using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>角色跨非战斗存档延续的 EX-GAS 等级与基础属性事实。</summary>
	[Serializable]
	public sealed class CharacterAbilitySystemSnapshot : TabletopCardRuntimeStateSnapshot
	{
		[SerializeField]
		private int m_level;

		[SerializeField]
		private CharacterAttributeSetSnapshot[] m_attributeSets;

		[SerializeField]
		private EquippedCardSnapshot[] m_equippedCards;

		public int Level => m_level;
		public IReadOnlyList<CharacterAttributeSetSnapshot> AttributeSets => m_attributeSets;
		public IReadOnlyList<EquippedCardSnapshot> EquippedCards => m_equippedCards ?? Array.Empty<EquippedCardSnapshot>();

		internal CharacterAbilitySystemSnapshot(
			int level,
			CharacterAttributeSetSnapshot[] attributeSets,
			IReadOnlyList<EquippedCardSnapshot> equippedCards = null)
		{
			if (level < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(level), "角色 ASC 等级不能为负数。");
			}
			m_level = level;
			m_attributeSets = attributeSets ?? throw new ArgumentNullException(nameof(attributeSets));
			if (equippedCards == null)
			{
				m_equippedCards = Array.Empty<EquippedCardSnapshot>();
				return;
			}
			m_equippedCards = new EquippedCardSnapshot[equippedCards.Count];
			for (int i = 0; i < equippedCards.Count; i++)
			{
				m_equippedCards[i] = equippedCards[i] ?? throw new ArgumentException(
					$"角色装备快照第 {i + 1} 项为空。",
					nameof(equippedCards));
			}
		}
	}

	/// <summary>一个 EX-GAS 属性集在角色存档中的基础值。</summary>
	[Serializable]
	public sealed class CharacterAttributeSetSnapshot
	{
		[SerializeField]
		private int m_attributeSetCode;

		[SerializeField]
		private CharacterAttributeSnapshot[] m_attributes;

		public int AttributeSetCode => m_attributeSetCode;
		public IReadOnlyList<CharacterAttributeSnapshot> Attributes => m_attributes;

		internal CharacterAttributeSetSnapshot(int attributeSetCode, CharacterAttributeSnapshot[] attributes)
		{
			m_attributeSetCode = attributeSetCode;
			m_attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
		}
	}

	/// <summary>一个 EX-GAS 属性的持久基础值；当前计算值继续由 GameplayEffect 推导。</summary>
	[Serializable]
	public sealed class CharacterAttributeSnapshot
	{
		[SerializeField]
		private int m_attributeCode;

		[SerializeField]
		private float m_baseValue;

		public int AttributeCode => m_attributeCode;
		public float BaseValue => m_baseValue;

		internal CharacterAttributeSnapshot(int attributeCode, float baseValue)
		{
			if (!float.IsFinite(baseValue))
			{
				throw new ArgumentOutOfRangeException(nameof(baseValue), "角色属性基础值必须是有限数值。");
			}
			m_attributeCode = attributeCode;
			m_baseValue = baseValue;
		}
	}
}
