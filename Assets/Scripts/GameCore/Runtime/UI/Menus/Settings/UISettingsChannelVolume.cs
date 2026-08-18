using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace GameCore
{
    public class UISettingsChannelVolume : UISettingsVolume
    {
        [Header("音频通道")]
        [LabelText("音频通道")]
        [Tooltip("这个控件要调整的 GameCore 音频通道。")]
        [SerializeField] private EAudioChannel m_audioChannel;

        public EAudioChannel audioChannel => m_audioChannel;

        private UnityAction m_decreaseCallback;
        private UnityAction m_increaseCallback;

        public void RegisterCallbacks(UnityAction<EAudioChannel> decrease, UnityAction<EAudioChannel> increase)
        {
            UnregisterCallbacks();
            m_decreaseCallback = () => decrease(m_audioChannel);
            m_increaseCallback = () => increase(m_audioChannel);
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
