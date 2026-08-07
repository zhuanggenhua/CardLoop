using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    public class UIAbility : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image m_icon = null;

        protected CharacterEquippedAbilitySlotView m_abilitySlot = default;
        protected int m_abilityIndex = -1;

        public virtual void SetAbility(CharacterEquippedAbilitySlotView slot, int index)
        {
            gameObject.SetActive(index < GameManager.Config.maxEquippableAbilities);

            m_abilitySlot = slot;
            m_abilityIndex = index;

            if (m_abilitySlot.HasDisplaySource)
            {
                m_icon.enabled = true;
                m_icon.sprite = m_abilitySlot.Icon;
            }
            else
            {
                m_icon.enabled = false;
                m_icon.sprite = null;
            }
        }
    }
}

