using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ItemRestoreManaEffect : AItemEffect
    {
        [SerializeField] private int m_manaToRestore = 1;

        protected override ItemUsageResult OnUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            if (target.CanRecoverMana())
            {
                int previousMana = target.GetCurrentMana();
                target.RecoverMana(m_manaToRestore, EEffectVisualFlags.NoFloatingText);
                int currentMana = target.GetCurrentMana();
                int diff = currentMana - previousMana;

                return new()
                {
                    success = true,
                    message = $"You recover {diff} <mana>"
                };
            }

            return new() { success = false };
        }
    }
}

