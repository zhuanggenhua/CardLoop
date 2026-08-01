using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;
using UnityEngine.Serialization;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 对话选项配置，包含选项显示名、跳转序列和选项消息。
    /// </summary>
    [System.Serializable]
    public struct DialogueSequenceOption
    {
        [FormerlySerializedAs("name")]
        [InspectorName("选项名称")]
        [Tooltip("玩家在选项列表中看到的短文本。")]
        [SerializeField]
        private string m_name;

        [FormerlySerializedAs("sequence")]
        [InspectorName("跳转对话")]
        [Tooltip("选择该选项后进入的对话序列。")]
        [SerializeField]
        private DialogueSequence m_sequence;

        [FormerlySerializedAs("message")]
        [InspectorName("选项消息")]
        [Tooltip("选择该选项时使用的完整对话消息配置。")]
        [SerializeField]
        private DialogueMessage m_message;

        public string name => m_name;
        public DialogueSequence sequence => m_sequence;
        public DialogueMessage message => m_message;
    }

    /// <summary>
    /// 数据库中的线性对话序列资产，可配置台词、选项和开始/完成时执行的命令。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Dialogues + nameof(DialogueSequence))]
    public class DialogueSequence : DatabaseEntry
    {
        [InspectorName("台词")]
        [Tooltip("该对话序列按顺序播放的文本行。")]
        [SerializeField] private string[] m_lines = null;

        [InspectorName("对话选项")]
        [Tooltip("序列结束或节点需要分支时展示的选项列表。")]
        [SerializeField] private DialogueSequenceOption[] m_options = null;

        [InspectorName("开始命令")]
        [Tooltip("对话序列开始时执行的命令，可用于触发任务、状态或表现。")]
        [SerializeReference, SubclassSelector] private ICommand m_toExecuteOnStart = null;

        [InspectorName("完成命令")]
        [Tooltip("对话序列完成时执行的命令，可用于推进任务或解锁后续内容。")]
        [SerializeReference, SubclassSelector] private ICommand m_toExecuteOnCompletion = null;

        public int lineCount => m_lines?.Length ?? 0;
        public int optionCount => m_options?.Length ?? 0;
        public string GetLineAt(int index) => index >= 0 && index < lineCount ? m_lines[index] : string.Empty;
        public DialogueSequenceOption GetOptionAt(int index) => index >= 0 && index < optionCount ? m_options[index] : default;
        /// <summary>
        /// 返回对话选项快照，避免外部直接修改资产内数组。
        /// </summary>
        public DialogueSequenceOption[] GetOptions() => m_options != null ? (DialogueSequenceOption[])m_options.Clone() : System.Array.Empty<DialogueSequenceOption>();

        internal void ApplyLifecycleCommands(DialogueNode node)
        {
            if (node == null)
            {
                return;
            }

            node.ApplyLifecycleCommands(m_toExecuteOnStart, m_toExecuteOnCompletion);
        }

        /// <summary>
        /// 使用说话者名称和格式参数构建可播放的对话树。
        /// </summary>
        public DialogueTree ToDialogueTree(string speaker, params string[] args)
        {
            return DialogueUtils.CreateDialogueTree(this, speaker, args);
        }

        /// <summary>
        /// 使用指定命令上下文构建对话树，让对话节点命令能保留真实发起者。
        /// </summary>
        public DialogueTree ToDialogueTree(string speaker, GameCommandContext commandContext, params string[] args)
        {
            return DialogueUtils.CreateDialogueTree(this, speaker, commandContext, args);
        }
    }
}
