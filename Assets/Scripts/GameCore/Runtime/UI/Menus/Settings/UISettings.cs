using Unity.Mathematics;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UISettings : UIKitMenuPanelBase
    {
        [Header("References")]
        [SerializeField] private UISettingsMasterVolume m_masterVolume = null;
        [SerializeField] private UISettingsChannelVolume[] m_channelVolumes = null;

        [Header("Settings")]
        [SerializeField] private float m_maxVolume = 10.0f;
        [SerializeField] private string m_volumeSuffix = " / 10";
        [SerializeField] private float m_volumeStep = 0.1f;

        protected override void OnPanelInit()
        {
            m_masterVolume.RegisterCallbacks(OnMasterVolumeDecreased, OnMasterVolumeIncreased);

            foreach (UISettingsChannelVolume channelVolume in m_channelVolumes)
            {
                channelVolume.RegisterCallbacks(OnChannelVolumeDecreased, OnChannelVolumeIncreased);
            }
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            UpdateUI();
        }

        private void OnDestroy()
        {
            m_masterVolume.UnregisterCallbacks();

            foreach (UISettingsChannelVolume channelVolume in m_channelVolumes)
            {
                channelVolume.UnregisterCallbacks();
            }
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            return m_masterVolume.GetDefaultFocusTarget();
        }

        private float ComputeVolumeChange(float volume, float stepScale)
        {
            float step = m_volumeStep * stepScale;
            return math.saturate(math.round((volume + step) * (1.0f / step)) * step);
        }

        private float ComputeVolumeIncrement(float volume) => ComputeVolumeChange(volume, +1.0f);

        private float ComputeVolumeDecrement(float volume) => ComputeVolumeChange(volume, -1.0f);

        private void OnMasterVolumeIncreased()
        {
            GameManager.AudioSystem.SetMasterVolume(
                ComputeVolumeIncrement(
                    GameManager.AudioSystem.GetMasterVolume()
                )
            );

            UpdateUI();
        }

        private void OnMasterVolumeDecreased()
        {
            GameManager.AudioSystem.SetMasterVolume(
                ComputeVolumeDecrement(
                    GameManager.AudioSystem.GetMasterVolume()
                )
            );

            UpdateUI();
        }

        private void OnChannelVolumeIncreased(EAudioChannel channel)
        {
            AudioSystem audioSystem = GameManager.AudioSystem;
            float targetVolumeScale = ComputeVolumeIncrement(audioSystem.GetChannelVolumeScale(channel));
            audioSystem.SetChannelVolumeScale(channel, targetVolumeScale);
            UpdateUI();
        }

        private void OnChannelVolumeDecreased(EAudioChannel channel)
        {
            AudioSystem audioSystem = GameManager.AudioSystem;
            float targetVolumeScale = ComputeVolumeDecrement(audioSystem.GetChannelVolumeScale(channel));
            audioSystem.SetChannelVolumeScale(channel, targetVolumeScale);
            UpdateUI();
        }

        private void UpdateUI()
        {
            m_masterVolume.UpdateUI((int)math.round(GameManager.AudioSystem.GetMasterVolume() * m_maxVolume), m_volumeSuffix);

            foreach (UISettingsChannelVolume channelVolume in m_channelVolumes)
            {
                float volumeScale = GameManager.AudioSystem.GetChannelVolumeScale(channelVolume.audioChannel) * m_maxVolume;
                channelVolume.UpdateUI((int)math.round(volumeScale), m_volumeSuffix);
            }
        }
    }
}


