using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 播放单行临时对话的命令。
    /// </summary>
    [Serializable]
    public class PlayDialogueLine : IContextualCommand
    {
        [InspectorName("说话者")]
        [Tooltip("该行对话显示的说话者名称。")]
        [SerializeField] private string m_speaker = string.Empty;

        [InspectorName("台词")]
        [Tooltip("要播放的单行对话文本。")]
        [TextArea][SerializeField] private string m_line = string.Empty;

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            await GameManager.DialogueSystem.PlayNow(new DialogueTree(new DialogueNode(StringFormatter.Format(m_line), m_speaker), context));
        }
    }
}

