using System;
using Ami.BroAudio;
using UnityEngine;

namespace GameCore
{
    public enum EAudioChannelMode
    {
        Multiple,
        Exclusive
    }

    /// <summary>
    /// 正式音频通道入口。
    /// 这里只负责选择播放路径、维护通道级状态与对外 API；具体播放执行和 fallback 池化都收进内部运行时模块。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class AudioChannel : MonoBehaviour
    {
        private static Func<SoundID, IAudioPlayer> sBroAudioPlay = soundId => BroAudio.Play(soundId);
        private static Func<SoundID, Vector3, IAudioPlayer> sBroAudioPlayAt = (soundId, position) => BroAudio.Play(soundId, position);
        private static Func<SoundID, Transform, IAudioPlayer> sBroAudioPlayAttached = (soundId, target) => BroAudio.Play(soundId, target);

        [Header("General Settings")]
        [SerializeField] private EAudioChannelMode m_audioChannelMode;
        [SerializeField] private AudioSource m_audioSource = null;
        [SerializeField] private float m_volumeScale = 0.5f;

        [Header("Exclusive Mode Settings")]
        [SerializeField] private float m_fadeOutDuration = 0.5f;
        [SerializeField] private float m_fadeInDuration = 0.25f;
        [SerializeField] private int m_multipleModePrewarmCount = 4;
        [SerializeField] private int m_multipleModeMaxPlayers = -1;

        private AudioClipResolver m_lastPlayedClip = null;
        private bool m_isPaused = false;
        private FallbackPoolRuntime m_fallbackPoolRuntime = null;
        private PlaybackRuntime m_playbackRuntime = null;

        private FallbackPoolRuntime fallbackPoolRuntime => m_fallbackPoolRuntime ??= new FallbackPoolRuntime(this);
        private PlaybackRuntime playbackRuntime => m_playbackRuntime ??= new PlaybackRuntime(this);

        private void Awake()
        {
            if (!EnsureAudioSource())
            {
                enabled = false;
                return;
            }

            fallbackPoolRuntime.Initialize();
        }

        internal AudioClipResolver GetLastPlayedAudioClipResolver()
        {
            return m_lastPlayedClip;
        }

        private void OnDestroy()
        {
            m_playbackRuntime?.Stop();
            m_fallbackPoolRuntime?.Dispose();
        }

        private void OnDisable()
        {
            m_isPaused = false;
            m_playbackRuntime?.Stop();
        }

        public void Play(AudioClipResolver audioClipResolver, Action onCompleted = null)
        {
            PlayInternal(audioClipResolver, null, null, onCompleted);
        }

        public void PlayAt(AudioClipResolver audioClipResolver, Vector3 position, Action onCompleted = null)
        {
            PlayInternal(audioClipResolver, position, null, onCompleted);
        }

        public void PlayAttached(AudioClipResolver audioClipResolver, Transform target, Action onCompleted = null)
        {
            PlayInternal(audioClipResolver, null, target, onCompleted);
        }

        public void SetVolumeScale(float scale)
        {
            m_volumeScale = scale;
            if (m_audioSource != null)
            {
                m_audioSource.volume = m_volumeScale;
            }

            fallbackPoolRuntime.SetVolumeScale(m_volumeScale);
            playbackRuntime.SetVolumeScale(m_volumeScale);
        }

        public float GetVolumeScale()
        {
            return m_volumeScale;
        }

        public void Stop()
        {
            m_isPaused = false;
            playbackRuntime.Stop();
        }

        public void Pause()
        {
            m_isPaused = true;
            playbackRuntime.Pause();
        }

        public void Resume()
        {
            m_isPaused = false;
            playbackRuntime.Resume();
        }

        private void PlayInternal(
            AudioClipResolver audioClipResolver,
            Vector3? position,
            Transform followTarget,
            Action onCompleted)
        {
            if (audioClipResolver == null)
            {
                return;
            }

            m_lastPlayedClip = audioClipResolver;

            if (audioClipResolver.TryGetSoundId(out SoundID soundId))
            {
                playbackRuntime.PlayBroAudio(soundId, position, followTarget, onCompleted);
                return;
            }

            AudioClip audioClip = audioClipResolver.GetClip();
            if (audioClip == null)
            {
                return;
            }

            if (ShouldUseExclusiveClipPlayback(position, followTarget))
            {
                playbackRuntime.PlayExclusiveClip(audioClip, onCompleted);
                return;
            }

            playbackRuntime.PlayFallbackClip(audioClip, position, followTarget, onCompleted);
        }

        private bool ShouldUseExclusiveClipPlayback(Vector3? position, Transform followTarget)
        {
            return m_audioChannelMode == EAudioChannelMode.Exclusive && !position.HasValue && followTarget == null;
        }

        private bool EnsureAudioSource()
        {
            m_audioSource ??= GetComponent<AudioSource>();
            if (m_audioSource == null)
            {
                Debug.LogError($"[{nameof(AudioChannel)}] 音频通道缺少必需的 AudioSource，无法作为正式播放通道。", this);
                return false;
            }

            m_audioSource.volume = m_volumeScale;
            return true;
        }

        private void DestroyOwnedObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
