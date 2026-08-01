using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UICharacterInfo : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI m_nameText = null;
        [SerializeField] private Slider m_healthSlider = null;
        [SerializeField] private Slider m_manaSlider = null;
        [SerializeField] private CharacterBase m_target = null;

        private string m_nameAndLevelFormat = string.Empty;
        private bool m_targetListening = false;

        private void Awake()
        {
            CacheNameAndLevelFormat();
        }

        private void OnEnable()
        {
            StartTargetListeningIfReady();
        }

        private void Start()
        {
            StartTargetListeningIfReady();
        }

        private void OnDisable()
        {
            StopTargetListening();
        }

        private void OnDestroy()
        {
            StopTargetListening();
        }

        public void UpdateResourceBars()
        {
            if (m_target == null)
            {
                return;
            }

            if (m_healthSlider?.isActiveAndEnabled ?? false)
            {
                m_healthSlider.minValue = 0;
                m_healthSlider.maxValue = m_target.GetMaxHealth();
                m_healthSlider.value = m_target.GetCurrentHealth();
            }

            if (m_manaSlider?.isActiveAndEnabled ?? false)
            {
                m_manaSlider.minValue = 0;
                m_manaSlider.maxValue = m_target.GetMaxMana();
                m_manaSlider.value = m_target.GetCurrentMana();
            }
        }

        public void UpdateNameAndLevel()
        {
            if (m_target != null && (m_nameText?.isActiveAndEnabled ?? false))
            {
                m_nameText.text = StringFormatter.Format(m_nameAndLevelFormat).Replace("{name}", m_target.characterSheet.displayName).Replace("{level}", m_target.level.ToString());
            }
        }

        private void OnStatsChanged(Stats previous) => UpdateResourceBars();

        private void OnLevelUpped(int level) => UpdateNameAndLevel();

        private void StartTargetListeningIfReady()
        {
            if (m_targetListening || m_target == null)
            {
                return;
            }

            m_targetListening = true;
            m_target.AddStatsChangedListener(OnStatsChanged);
            m_target.AddCurrentStatsChangedListener(OnStatsChanged);
            m_target.AddLevelUppedListener(OnLevelUpped);

            UpdateResourceBars();
            UpdateNameAndLevel();
        }

        private void StopTargetListening()
        {
            if (!m_targetListening)
            {
                return;
            }

            m_targetListening = false;
            if (m_target == null)
            {
                return;
            }

            m_target.RemoveStatsChangedListener(OnStatsChanged);
            m_target.RemoveCurrentStatsChangedListener(OnStatsChanged);
            m_target.RemoveLevelUppedListener(OnLevelUpped);
        }

        private void CacheNameAndLevelFormat()
        {
            m_nameAndLevelFormat = m_nameText != null ? m_nameText.text : string.Empty;
        }
    }
}
