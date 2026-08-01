using TMPro;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UICharacter : UIKitMenuPanelBase
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI m_class = null;
        [SerializeField] private TextMeshProUGUI m_level = null;
        [SerializeField] private TextMeshProUGUI m_experience = null;
        [SerializeField] private TextMeshProUGUI m_skillPoints = null;
        [SerializeField] private TextMeshProUGUI m_currency = null;
        [SerializeField] private UICharacterStat[] m_stats = null;
        [SerializeField] private TextMeshProUGUI m_applyButtonText = null;

        private CharacterActor m_currentCharacter = null;
        private CharacterMenuContext m_context = CharacterMenuContext.CurrentControlledCharacter();
        private bool m_currentControlledCharacterListening = false;

        private Stats m_tempStats;
        private int m_availablePoints = 0;
        private int m_totalAvailablePoints = 0;

        protected override void OnPanelInit()
        {
            foreach (UICharacterStat stat in m_stats)
            {
                stat.RegisterCallbacks(OnRemoveButtonPressed, OnAddButtonPressed);
            }
        }

        private void OnDestroy()
        {
            StopCurrentControlledCharacterListening();
            foreach (UICharacterStat stat in m_stats)
            {
                stat.UnregisterCallbacks();
            }
        }

        protected override void OnPanelOpened(UIKitMenuOpenData openData)
        {
            m_context = TryResolveCharacterMenuContext(openData, out CharacterMenuContext context)
                ? context
                : CharacterMenuContext.CurrentControlledCharacter();
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            BindCurrentControlledCharacterListenerForContext();
            BindCharacter(m_context.ResolveActor() as CharacterActor);
            m_tempStats = new();
            m_availablePoints = m_currentCharacter != null ? m_currentCharacter.availablePoints : 0;
            m_totalAvailablePoints = m_availablePoints;
            UpdateUI();
        }

        protected override void OnPanelHidden()
        {
            StopCurrentControlledCharacterListening();
            ClearPanelState();
        }

        public void Apply()
        {
            if (m_currentCharacter != null && m_tempStats.GetTotal() > 0)
            {
                m_currentCharacter.LogUsedPoints(m_totalAvailablePoints - m_availablePoints);
                m_totalAvailablePoints = m_availablePoints;
                m_currentCharacter.AddCustomStats(m_tempStats);
                m_tempStats = new();

                UpdateUI();
            }
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            return m_stats.Length > 0 ? m_stats[0].GetDefaultFocusTarget() : base.ResolveDefaultFocusTarget();
        }

        private void UpdateUI()
        {
            UpdateInfoSection();
            UpdateStatsSection();
            m_applyButtonText.text = $"Apply {m_tempStats.GetTotal()} points";
        }

        private void UpdateInfoSection()
        {
            if (m_currentCharacter == null)
            {
                m_class.text = string.Empty;
                m_level.text = string.Empty;
                m_experience.text = string.Empty;
                m_skillPoints.text = "0";
                m_currency.text = StringFormatter.Format("{0}", GameManager.InventorySystem.money.ToString());
                return;
            }

            m_class.text = m_currentCharacter.characterSheet.displayName;
            m_level.text = m_currentCharacter.level.ToString();
            m_experience.text = StringFormatter.Format("{0}", m_currentCharacter.nextLevelExperience - m_currentCharacter.experience);
            m_skillPoints.text = m_availablePoints.ToString();
            m_currency.text = StringFormatter.Format("{0}", GameManager.InventorySystem.money.ToString());
        }

        private void UpdateStatsSection()
        {
            foreach (UICharacterStat stat in m_stats)
            {
                stat.UpdateUI(m_currentCharacter, m_tempStats);
            }
        }

        public void OnAddButtonPressed(EStat stat)
        {
            if (m_currentCharacter != null && m_availablePoints > 0)
            {
                m_tempStats[stat] += 1;
                --m_availablePoints;
                UpdateUI();
            }
        }

        public void OnRemoveButtonPressed(EStat stat)
        {
            if (m_currentCharacter != null && m_tempStats[stat] > 0)
            {
                m_tempStats[stat] -= 1;
                ++m_availablePoints;
                UpdateUI();
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character)
        {
            if (m_context.FollowsCurrentControlledCharacter)
            {
                BindCharacter(m_context.ResolveActor() as CharacterActor);
            }
        }

        private void BindCurrentControlledCharacterListenerForContext()
        {
            if (m_context.FollowsCurrentControlledCharacter)
            {
                StartCurrentControlledCharacterListeningIfReady();
            }
            else
            {
                StopCurrentControlledCharacterListening();
            }
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

        private void BindCharacter(CharacterActor character)
        {
            if (ReferenceEquals(m_currentCharacter, character))
            {
                return;
            }

            m_currentCharacter = character;

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            m_tempStats ??= new Stats();
            m_tempStats = new Stats();
            m_availablePoints = m_currentCharacter != null ? m_currentCharacter.availablePoints : 0;
            m_totalAvailablePoints = m_availablePoints;
            UpdateUI();
        }

        private void ClearPanelState()
        {
            m_currentCharacter = null;
            m_tempStats = new Stats();
            m_availablePoints = 0;
            m_totalAvailablePoints = 0;
        }

        private static bool TryResolveCharacterMenuContext(UIKitMenuOpenData openData, out CharacterMenuContext context)
        {
            context = CharacterMenuContext.CurrentControlledCharacter();
            if (openData == null || openData.ArgumentCount != 1)
            {
                return false;
            }

            return openData.TryGetArgument(0, out context);
        }
    }
}


