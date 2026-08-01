using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    public class UISettingsMasterVolume : UISettingsVolume
    {
        private UnityAction m_decreaseCallback;
        private UnityAction m_increaseCallback;

        public void RegisterCallbacks(UnityAction decrease, UnityAction increase)
        {
            UnregisterCallbacks();
            m_decreaseCallback = decrease;
            m_increaseCallback = increase;
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
    }
}
