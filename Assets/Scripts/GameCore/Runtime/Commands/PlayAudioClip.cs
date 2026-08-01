using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 通过全局音频事件播放一个音频解析器。
    /// </summary>
    [Serializable]
    public class PlayAudioClip : IContextualCommand
    {
        [InspectorName("音频")]
        [Tooltip("要播放的音频解析器。")]
        [SerializeField] private AudioClipResolver m_audioClip = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            GameRuntimeEvents.RequestAudioPlayback(m_audioClip);
            return Task.CompletedTask;
        }
    }
}

