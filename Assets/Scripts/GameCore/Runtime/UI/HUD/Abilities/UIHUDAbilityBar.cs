using Unity.Mathematics;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIHUDAbilityBar : MonoBehaviour
    {
        protected UIHUDAbilityBarEntry[] m_abilities = null;
        private CharacterBase m_currentCharacter = null;
        private bool m_isInitialized = false;
        private bool m_currentControlledCharacterListening = false;

        private void Awake()
        {
            InitializeEntriesIfNeeded();
        }

        private void OnEnable()
        {
            InitializeEntriesIfNeeded();
            StartCurrentControlledCharacterListeningIfReady();
        }

        private void Start()
        {
            StartCurrentControlledCharacterListeningIfReady();
        }

        private void OnDisable()
        {
            StopCurrentControlledCharacterListening();
            BindCharacter(null);
        }

        private void OnDestroy()
        {
            StopCurrentControlledCharacterListening();
            BindCharacter(null);
        }

        private void InitializeEntriesIfNeeded()
        {
            if (m_isInitialized)
            {
                return;
            }

            m_abilities = GetComponentsInChildren<UIHUDAbilityBarEntry>();

            for (int i = 0; i < m_abilities.Length; ++i)
            {
                m_abilities[i].SetAbility(default(CharacterEquippedAbilitySlotView), i);
            }

            m_isInitialized = true;
        }

        private void StartCurrentControlledCharacterListeningIfReady()
        {
            if (m_currentControlledCharacterListening)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<PlayerSystem>())
            {
                return;
            }

            m_currentControlledCharacterListening = true;
            GameManager.PlayerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            OnCurrentControlledCharacterChanged(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        private void StopCurrentControlledCharacterListening()
        {
            if (!m_currentControlledCharacterListening)
            {
                return;
            }

            m_currentControlledCharacterListening = false;
            if (GameManager.Exists() && GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character) => BindCharacter(character);

        private void BindCharacter(CharacterBase character)
        {
            if (ReferenceEquals(m_currentCharacter, character))
            {
                return;
            }

            if (m_currentCharacter != null)
            {
                m_currentCharacter.RemoveEquippedAbilitiesChangedListener(OnEquippedAbilitiesChanged);
            }

            m_currentCharacter = character;
            foreach (UIHUDAbilityBarEntry abilityEntry in m_abilities ?? System.Array.Empty<UIHUDAbilityBarEntry>())
            {
                abilityEntry.SetBoundCharacter(m_currentCharacter);
            }

            if (m_currentCharacter != null)
            {
                m_currentCharacter.AddEquippedAbilitiesChangedListener(OnEquippedAbilitiesChanged);
                OnEquippedAbilitiesChanged(m_currentCharacter.GetEquippedAbilitySlotViewSnapshots());
            }
            else
            {
                OnEquippedAbilitiesChanged(System.Array.Empty<CharacterEquippedAbilitySlotView>());
            }
        }

        private void OnEquippedAbilitiesChanged(CharacterEquippedAbilitySlotView[] abilities)
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
