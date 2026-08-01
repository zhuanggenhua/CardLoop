using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UICharacterStat : UIStat
    {
        [Header("References")]
        [SerializeField] private Button m_decreaseButton;
        [SerializeField] private Button m_increaseButton;

        private UnityAction m_decreaseCallback;
        private UnityAction m_increaseCallback;

        public void RegisterCallbacks(UnityAction<EStat> decrease, UnityAction<EStat> increase)
        {
            UnregisterCallbacks();
            m_decreaseCallback = () => decrease(m_stat);
            m_increaseCallback = () => increase(m_stat);
            m_decreaseButton.onClick.AddListener(m_decreaseCallback);
            m_increaseButton.onClick.AddListener(m_increaseCallback);
        }

        public void UnregisterCallbacks()
        {
            if (m_decreaseCallback != null)
            {
                m_decreaseButton.onClick.RemoveListener(m_decreaseCallback);
                m_decreaseCallback = null;
            }

            if (m_increaseCallback != null)
            {
                m_increaseButton.onClick.RemoveListener(m_increaseCallback);
                m_increaseCallback = null;
            }
        }

        public void UpdateUI(CharacterBase target, Stats tempStats)
        {
            int baseValue = target != null ? target.GetStatValue(definition) : 0;
            int pendingValue = tempStats != null ? tempStats[m_stat] : 0;

            if (pendingValue > 0)
            {
                m_value.text = string.Format("{0} (+{1})", baseValue, pendingValue);
            }
            else
            {
                m_value.text = string.Format("{0}", baseValue);
            }
        }

        // 只回答默认焦点对象，不把内部 Button 直接外借给外层菜单。
        public GameObject GetDefaultFocusTarget() => m_decreaseButton != null ? m_decreaseButton.gameObject : gameObject;
    }
}


