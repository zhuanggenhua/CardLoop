using System.Collections.Generic;
using System;
using UnityEngine;
using YokiFrame;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 项目音频通道，用于把音效路由到不同音量控制和播放策略。
    /// </summary>
    public enum EAudioChannel
    {
        BackgroundMusic,
        BackgroundSound,
        InterfaceSoundFX,
        GameplaySoundFX,
        Miscellaneous
    }

    /// <summary>
    /// 项目音频系统，监听播放请求、管理通道音量并把 AudioClipResolver 派发到目标通道。
    /// </summary>
    public class AudioSystem : AGameSystem
    {
        [InspectorName("音频通道")]
        [Tooltip("每个项目音频通道对应的实际 AudioChannel 实例。")]
        [SerializeField] private SerializableDictionary<EAudioChannel, AudioChannel> m_audioChannels;

        const string kVolumePlayerPrefsKey = "M2D_AudioSystem_Volume_";
        const string kChannelVolumePlayerPrefsKey = kVolumePlayerPrefsKey + "Channel_";
        const string kMasterVolumePlayerPrefsKey = kVolumePlayerPrefsKey + "Master";

        private float m_masterVolume = Constants.DefaultMasterVolume;

        public override void OnSystemStart()
        {
            ValidateAudioChannels();
            LoadSettings();
            EventKit.Type.Register<AudioPlaybackRequestedEvent>(DispatchAudioPlaybackRequest);
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<AudioPlaybackRequestedEvent>(DispatchAudioPlaybackRequest);
            SaveSettings();
        }

        /// <summary>
        /// 设置主音量并同步到 Unity AudioListener。
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            m_masterVolume = volume;
            AudioListener.volume = volume;
        }

        public float GetMasterVolume() => m_masterVolume;

        private void LoadSettings()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(kMasterVolumePlayerPrefsKey, m_masterVolume));

            if (m_audioChannels == null)
            {
                return;
            }

            foreach (KeyValuePair<EAudioChannel, AudioChannel> channel in m_audioChannels)
            {
                if (channel.Value == null)
                {
                    continue;
                }

                channel.Value.SetVolumeScale(PlayerPrefs.GetFloat($"{kChannelVolumePlayerPrefsKey}{channel.Key}", channel.Value.GetVolumeScale()));
            }
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(kMasterVolumePlayerPrefsKey, m_masterVolume);

            if (m_audioChannels != null)
            {
                foreach (KeyValuePair<EAudioChannel, AudioChannel> channel in m_audioChannels)
                {
                    if (channel.Value == null)
                    {
                        continue;
                    }

                    PlayerPrefs.SetFloat($"{kChannelVolumePlayerPrefsKey}{channel.Key}", channel.Value.GetVolumeScale());
                }
            }

            PlayerPrefs.Save();
        }

        private void DispatchAudioPlaybackRequest(AudioPlaybackRequestedEvent audioPlaybackRequestedEvent)
        {
            Play(audioPlaybackRequestedEvent.AudioClipResolver);
        }

        /// <summary>
        /// 在解析器目标通道上播放一次音频。
        /// </summary>
        public void Play(AudioClipResolver audioClipResolver, Action onCompleted = null)
        {
            if (TryGetChannel(audioClipResolver, out AudioChannel channel))
            {
                channel.Play(audioClipResolver, onCompleted);
            }
        }

        /// <summary>
        /// 在指定世界位置播放音频。
        /// </summary>
        public void PlayAt(AudioClipResolver audioClipResolver, Vector3 position, Action onCompleted = null)
        {
            if (TryGetChannel(audioClipResolver, out AudioChannel channel))
            {
                channel.PlayAt(audioClipResolver, position, onCompleted);
            }
        }

        /// <summary>
        /// 把音频挂到目标 Transform 上播放。
        /// </summary>
        public void PlayAttached(AudioClipResolver audioClipResolver, Transform target, Action onCompleted = null)
        {
            if (TryGetChannel(audioClipResolver, out AudioChannel channel))
            {
                channel.PlayAttached(audioClipResolver, target, onCompleted);
            }
        }

        /// <summary>
        /// 返回指定通道最近一次播放的音频解析器，主要用于验证和调试。
        /// </summary>
        public AudioClipResolver GetLastPlayedAudioClipResolver(EAudioChannel channel)
        {
            if (TryGetConfiguredChannel(channel, nameof(GetLastPlayedAudioClipResolver), out AudioChannel channelInstance))
            {
                return channelInstance.GetLastPlayedAudioClipResolver();
            }

            return null;
        }

        public void SetChannelVolumeScale(EAudioChannel channel, float volume)
        {
            if (TryGetConfiguredChannel(channel, nameof(SetChannelVolumeScale), out AudioChannel channelInstance))
            {
                channelInstance.SetVolumeScale(volume);
            }
        }

        public float GetChannelVolumeScale(EAudioChannel channel)
        {
            if (TryGetConfiguredChannel(channel, nameof(GetChannelVolumeScale), out AudioChannel channelInstance))
            {
                return channelInstance.GetVolumeScale();
            }

            return 0.0f;
        }

        public void StopChannel(EAudioChannel channel)
        {
            if (TryGetConfiguredChannel(channel, nameof(StopChannel), out AudioChannel channelInstance))
            {
                channelInstance.Stop();
            }
        }

        public void PauseChannel(EAudioChannel channel)
        {
            if (TryGetConfiguredChannel(channel, nameof(PauseChannel), out AudioChannel channelInstance))
            {
                channelInstance.Pause();
            }
        }

        public void ResumeChannel(EAudioChannel channel)
        {
            if (TryGetConfiguredChannel(channel, nameof(ResumeChannel), out AudioChannel channelInstance))
            {
                channelInstance.Resume();
            }
        }

        private bool TryGetChannel(AudioClipResolver audioClipResolver, out AudioChannel channel)
        {
            channel = null;
            if (!audioClipResolver)
            {
                return false;
            }

            return TryGetConfiguredChannel(audioClipResolver.targetChannel, audioClipResolver.name, out channel);
        }

        private bool TryGetConfiguredChannel(EAudioChannel channelKey, string requestSource, out AudioChannel channel)
        {
            channel = null;
            if (m_audioChannels == null)
            {
                Debug.LogError($"[{nameof(AudioSystem)}] 音频请求 {requestSource} 需要通道 {channelKey}，但 AudioSystem 未配置任何音频通道。", this);
                return false;
            }

            if (!m_audioChannels.TryGetValue(channelKey, out channel) || channel == null)
            {
                Debug.LogError($"[{nameof(AudioSystem)}] 音频请求 {requestSource} 需要通道 {channelKey}，但该通道未在 AudioSystem 中配置。", this);
                return false;
            }

            return true;
        }

        private void ValidateAudioChannels()
        {
            if (m_audioChannels == null)
            {
                Debug.LogError($"[{nameof(AudioSystem)}] AudioSystem 缺少音频通道配置，所有 AudioClipResolver 播放请求都会失败。", this);
                return;
            }

            foreach (KeyValuePair<EAudioChannel, AudioChannel> channel in m_audioChannels)
            {
                if (channel.Value == null)
                {
                    Debug.LogError($"[{nameof(AudioSystem)}] 音频通道 {channel.Key} 没有绑定 AudioChannel。", this);
                }
            }
        }
    }
}
