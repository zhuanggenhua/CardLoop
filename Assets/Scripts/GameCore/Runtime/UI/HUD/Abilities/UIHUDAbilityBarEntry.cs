using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIHUDAbilityBarEntry : UIAbility
    {
        [Header("References")]
        [SerializeField] private UIControllerButton m_controllerButton = null;
        [SerializeField] private Slider m_cooldownSlider = null;
        [SerializeField] private TextMeshProUGUI m_cooldownText = null;

        private CharacterBase m_boundCharacter = null;

        public override void SetAbility(CharacterEquippedAbilitySlotView slot, int index)
        {
            base.SetAbility(slot, index);

            gameObject.SetActive(slot.HasDisplaySource);

            if (slot.HasDisplaySource)
            {
                m_controllerButton.SetAction((UIControllerButtonManager.EAction)(index + (int)UIControllerButtonManager.EAction.FireAbility1));
            }
        }

        public void SetBoundCharacter(CharacterBase character)
        {
            m_boundCharacter = character;
            if (m_boundCharacter == null)
            {
                ClearCooldownUI();
            }
        }

        private void Update()
        {
            if (m_boundCharacter == null || !m_abilitySlot.HasDisplaySource)
            {
                ClearCooldownUI();
                return;
            }

            if (m_boundCharacter.TryGetActiveAbilityCooldownSnapshot(m_abilitySlot, out CharacterAbilityCooldownSnapshot cooldownSnapshot))
            {
                float remainingCooldown = cooldownSnapshot.RemainingCooldown;
                float cooldown = cooldownSnapshot.Cooldown;
                if (cooldown > 0.0f)
                {
                    float cooldownRatio = (cooldown - remainingCooldown) / cooldown;
                    m_cooldownSlider.value = 1.0f - cooldownRatio;
                }
                else
                {
                    m_cooldownSlider.value = 0.0f;
                }

                int seconds = Mathf.CeilToInt(remainingCooldown);
                m_cooldownText.text =
                    remainingCooldown > 0.0f ?
                    (remainingCooldown < 0.5f ? $"{remainingCooldown:0.0}" : $"{seconds}") :
                    string.Empty;
            }
            else
            {
                ClearCooldownUI();
            }
        }

        private void ClearCooldownUI()
        {
            m_cooldownSlider.value = 0.0f;
            m_cooldownText.text = string.Empty;
        }
    }
}

