using Sirenix.OdinInspector;
using Ami.BroAudio;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 多个 AudioClip 的选择策略。
    /// </summary>
    public enum EAudioClipResolvingAlgorithm
    {
        First,
        Random,
        Loop,
        PingPong
    }

    /// <summary>
    /// 音频解析资产，可同时提供 BroAudio SoundID 和传统 AudioClip 列表兜底。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.Audio + nameof(AudioClipResolver))]
    public class AudioClipResolver : DatabaseEntry
    {
        [LabelText("音频片段")]
        [Tooltip("传统 AudioClip 播放入口使用的候选片段列表。")]
        [SerializeField] private AudioClip[] m_audioClips = null;

        [LabelText("BroAudio 声音 ID")]
        [Tooltip("BroAudio 播放入口使用的 SoundID；有效时优先交给音频系统解析。")]
        [SerializeField] private SoundID m_soundId;

        [LabelText("目标音频通道")]
        [Tooltip("播放该音频时希望进入的项目音频通道。")]
        [SerializeField] private EAudioChannel m_targetChannel;

        [LabelText("片段选择策略")]
        [Tooltip("当使用 AudioClip 列表播放时，决定每次取哪一个片段。")]
        [SerializeField] private EAudioClipResolvingAlgorithm m_resolvingAlgorithm;

        private int m_currentIndex = 0;
        private int m_increment = 1;

        public EAudioChannel targetChannel => m_targetChannel;
        public SoundID soundId => m_soundId;

        /// <summary>
        /// 尝试取得有效的 BroAudio SoundID。
        /// </summary>
        public bool TryGetSoundId(out SoundID soundId)
        {
            soundId = m_soundId;
            return soundId.IsValid();
        }

        /// <summary>
        /// 按配置的选择策略返回一个 AudioClip；没有候选片段时返回 null。
        /// </summary>
        public AudioClip GetClip()
        {
            if (HasClips())
            {
                switch (m_resolvingAlgorithm)
                {
                    case EAudioClipResolvingAlgorithm.First: return GetClipFirst();
                    case EAudioClipResolvingAlgorithm.Random: return GetClipRandom();
                    case EAudioClipResolvingAlgorithm.Loop: return GetClipLoop();
                    case EAudioClipResolvingAlgorithm.PingPong: return GetClipPingPong();
                }
            }

            return null;
        }

        private bool HasClips() => m_audioClips != null && m_audioClips.Length > 0;

        private AudioClip GetClipFirst()
        {
            return m_audioClips[0];
        }

        private AudioClip GetClipRandom()
        {
            return m_audioClips[Random.Range(0, m_audioClips.Length)];
        }

        private AudioClip GetClipLoop()
        {
            AudioClip output = m_audioClips[m_currentIndex];

            ++m_currentIndex;

            if (m_currentIndex == m_audioClips.Length)
            {
                m_currentIndex = 0;
            }

            return output;
        }

        private AudioClip GetClipPingPong()
        {
            if (m_audioClips.Length == 1)
            {
                return m_audioClips[0];
            }

            AudioClip output = m_audioClips[m_currentIndex];
            m_currentIndex += m_increment;

            if (m_currentIndex >= m_audioClips.Length)
            {
                m_currentIndex = m_audioClips.Length - 2;
                m_increment = -1;
            }
            else if (m_currentIndex < 0)
            {
                m_currentIndex = 1;
                m_increment = 1;
            }

            return output;
        }
    }
}
