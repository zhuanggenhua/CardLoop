using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 播放指定对话序列的命令。
    /// </summary>
    [Serializable]
    public class PlayDialogueSequence : IContextualCommand
    {
        [InspectorName("对话序列")]
        [Tooltip("要播放的对话序列资产。缺失时会暴露配置错误。")]
        [SerializeField] private DialogueSequence m_dialogueSequence = null;

        [InspectorName("说话者")]
        [Tooltip("播放该对话序列时使用的说话者名称。")]
        [SerializeField] private string m_speaker = string.Empty;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            if (m_dialogueSequence == null)
            {
                throw new InvalidOperationException($"{nameof(PlayDialogueSequence)} 缺少要播放的对话序列。");
            }

            return GameManager.DialogueSystem.PlayNow(m_dialogueSequence.ToDialogueTree(m_speaker, context));
        }
    }
}

