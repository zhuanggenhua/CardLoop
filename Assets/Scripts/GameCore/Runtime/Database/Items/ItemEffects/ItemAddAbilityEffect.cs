using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ItemAddAbilityEffect : AItemEffect
    {
        [SerializeField] private int m_formalGasAbilityCode = 0;

        protected override ItemUsageResult OnUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            if (m_formalGasAbilityCode > 0)
            {
                if (!TryCreateItemUseAbilitySource(item, out CharacterAbilitySourceKey source))
                {
                    return new ItemUsageResult { success = false };
                }

                if (!target.HasFormalGasAbility(m_formalGasAbilityCode) &&
                    target.AddBonusFormalGasAbility(m_formalGasAbilityCode, source))
                {
                    return new()
                    {
                        success = true,
                        message = $"You learned EX-GAS Ability {m_formalGasAbilityCode}"
                    };
                }

                return new ItemUsageResult { success = false };
            }

            return new ItemUsageResult { success = false };
        }

        private static bool TryCreateItemUseAbilitySource(Item item, out CharacterAbilitySourceKey source)
        {
            source = default;
            if (!item)
            {
                return false;
            }

            if (!GameManager.Database.TryCreateReference(item, out DatabaseEntryReference<Item> reference))
            {
                Debug.LogError($"[{nameof(ItemAddAbilityEffect)}] 物品 {item.name} 未登记到 DatabaseRegistry，不能作为能力来源。", item);
                return false;
            }

            source = new CharacterAbilitySourceKey(ECharacterAbilitySourceKind.ItemUse, reference.guid);
            return true;
        }
    }
}

