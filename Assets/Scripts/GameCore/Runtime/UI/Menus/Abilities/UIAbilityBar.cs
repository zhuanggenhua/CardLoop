using Unity.Mathematics;
using UnityEngine;

namespace GameCore
{
    public class UIAbilityBar : MonoBehaviour
    {
        protected UIAbilityBarEntry[] m_abilities = null;
        private CharacterBase m_currentCharacter = null;
        private bool m_isInitialized = false;

        public void Init()
        {
            if (m_isInitialized)
            {
                return;
            }

            m_abilities = GetComponentsInChildren<UIAbilityBarEntry>();

            for (int i = 0; i < m_abilities.Length; ++i)
            {
                m_abilities[i].SetAbility(default(CharacterEquippedAbilitySlotView), i);
            }

            m_isInitialized = true;
        }

        private void OnDestroy()
        {
            BindCharacter(null);
        }

        public void SelectFirstElement()
        {
            Init();
            if (m_abilities.Length > 0)
            {
                m_abilities[0].ForceSelection();
            }
        }

        public void UpdateUI()
        {
            Init();
            if (m_currentCharacter != null)
            {
                FillAbilityBar(m_currentCharacter.GetEquippedAbilitySlotViewSnapshots());
            }
            else
            {
                FillAbilityBar(System.Array.Empty<CharacterEquippedAbilitySlotView>());
            }
        }

        public void PresentCharacter(CharacterBase character)
        {
            Init();
            BindCharacter(character);
        }

        private void BindCharacter(CharacterBase character)
        {
            if (ReferenceEquals(m_currentCharacter, character))
            {
                return;
            }

            if (m_currentCharacter != null)
            {
                m_currentCharacter.RemoveEquippedAbilitiesChangedListener(FillAbilityBar);
            }

            m_currentCharacter = character;

            if (m_currentCharacter != null)
            {
                m_currentCharacter.AddEquippedAbilitiesChangedListener(FillAbilityBar);
                FillAbilityBar(m_currentCharacter.GetEquippedAbilitySlotViewSnapshots());
            }
            else
            {
                FillAbilityBar(System.Array.Empty<CharacterEquippedAbilitySlotView>());
            }
        }

        private void FillAbilityBar(CharacterEquippedAbilitySlotView[] abilities)
        {
            if (m_abilities == null)
            {
                return;
            }

            int maxEquippableAbilities = GameManager.Exists()
                ? GameManager.Config.maxEquippableAbilities
                : m_abilities.Length;
            for (int i = 0; i < math.min(m_abilities.Length, maxEquippableAbilities); ++i)
            {
                if (abilities.Length > i)
                {
                    m_abilities[i].SetAbility(abilities[i], i);
                }
                else
                {
                    m_abilities[i].SetAbility(default(CharacterEquippedAbilitySlotView), i);
                }
            }
        }
    }
}
