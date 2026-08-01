using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ItemHealEffect : AItemEffect
    {
        [SerializeField] private int m_healthToRestore = 1;

        protected override ItemUsageResult OnUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            if (target.CanRecoverHealth())
            {
                int previousHealth = target.GetCurrentHealth();
                target.Heal(m_healthToRestore, EEffectVisualFlags.NoFloatingText);
                int currentHealth = target.GetCurrentHealth();
                int diff = currentHealth - previousHealth;

                return new()
                {
                    success = true,
                    message = $"You recover {diff} <health>"
                };
            }

            return new() { success = false };
        }
    }
}

