using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 对话消息类型。
    /// 固定类型用于常见选择，自定义消息用于剧情脚本之间传递更细的状态。
    /// </summary>
    public enum EDialogueMessageType
    {
        None,
        Custom,
        Accept,
        Decline
    }

    /// <summary>
    /// 对话过程中已经产生的消息集合。
    /// 条件和命令可用它判断玩家是否选择过某个选项。
    /// </summary>
    [System.Serializable]
    public class DialogueMessageFeed
    {
        private readonly HashSet<DialogueMessage> m_messages = new();

        internal void Add(DialogueMessage message)
        {
            m_messages.Add(message);
        }

        public bool Contains(string message)
        {
            return Contains(new DialogueMessage
            {
                type = EDialogueMessageType.Custom,
                customMessage = message
            });
        }

        public bool Contains(EDialogueMessageType type)
        {
            return Contains(new DialogueMessage
            {
                type = type,
                customMessage = string.Empty
            });
        }

        public bool Contains(DialogueMessage message)
        {
            return m_messages.Contains(message);
        }
    }

    /// <summary>
    /// 对话选项或节点产生的一条消息。
    /// ToString 统一成小写字符串，便于 HashSet 去重和条件匹配。
    /// </summary>
    [System.Serializable]
    public struct DialogueMessage
    {
        public EDialogueMessageType type;
        public string customMessage;

        public override string ToString()
        {
            switch (type)
            {
                case EDialogueMessageType.None: return string.Empty;
                case EDialogueMessageType.Custom: return customMessage.ToLower();
            }

            return type.ToString().ToLower();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ToString().Equals(obj.ToString());
        }

        public bool Equals(EDialogueMessageType type)
        {
            return Equals(new DialogueMessage { type = type });
        }
    }

    /// <summary>
    /// 对话节点上的一个可选分支。
    /// name 是玩家看到的选项名，node 是下一节点，message 是选择后写入的消息。
    /// </summary>
    [System.Serializable]
    public struct DialogueNodeOption
    {
        public string name;
        public DialogueNode node;
        public DialogueMessage message;
    }

    /// <summary>
    /// 单个对话节点。
    /// 节点只保存文本、说话人、选项和生命周期命令，不直接持有 UI 面板。
    /// </summary>
    public class DialogueNode
    {
        [InspectorName("开始命令")]
        [Tooltip("进入该节点时执行的命令。为空时不执行额外逻辑。")]
        [SerializeReference, SubclassSelector]
        private ICommand m_toExecuteOnStart;

        [InspectorName("完成命令")]
        [Tooltip("离开该节点或完成该节点时执行的命令。为空时不执行额外逻辑。")]
        [SerializeReference, SubclassSelector]
        private ICommand m_toExecuteOnCompletion;

        private string m_text;
        private string m_speaker;
        private DialogueNodeOption[] m_options = Array.Empty<DialogueNodeOption>();

        public string text => m_text;
        public string speaker => m_speaker;
        public int optionCount => m_options?.Length ?? 0;

        public DialogueNode(
            string text = "",
            string speaker = "",
            DialogueNodeOption[] options = null,
            ICommand toExecuteOnStart = null,
            ICommand toExecuteOnCompletion = null)
        {
            SetContent(text, speaker);
            SetOptions(options);
            ApplyLifecycleCommands(toExecuteOnStart, toExecuteOnCompletion);
        }

        public DialogueNode GetNext(int option)
        {
            return TryGetOption(option, out DialogueNodeOption selectedOption)
                ? selectedOption.node
                : null;
        }

        public DialogueNodeOption[] GetOptions()
        {
            return m_options != null
                ? (DialogueNodeOption[])m_options.Clone()
                : Array.Empty<DialogueNodeOption>();
        }

        public bool TryGetOption(int index, out DialogueNodeOption option)
        {
            if (m_options != null && index >= 0 && index < m_options.Length)
            {
                option = m_options[index];
                return true;
            }

            option = default;
            return false;
        }

        internal void SetContent(string text, string speaker)
        {
            m_text = text;
            m_speaker = speaker;
        }

        internal void SetOptions(DialogueNodeOption[] options)
        {
            m_options = options != null
                ? (DialogueNodeOption[])options.Clone()
                : Array.Empty<DialogueNodeOption>();
        }

        internal void ApplyLifecycleCommands(ICommand onStart, ICommand onCompletion)
        {
            m_toExecuteOnStart = onStart;
            m_toExecuteOnCompletion = onCompletion;
        }

        internal void ExecuteStartCommand()
        {
            ExecuteStartCommand(GameCommandContext.Script());
        }

        internal void ExecuteStartCommand(GameCommandContext context)
        {
            m_toExecuteOnStart.ExecuteFireAndReport(context, nameof(DialogueNode));
        }

        internal void ExecuteCompletionCommand()
        {
            ExecuteCompletionCommand(GameCommandContext.Script());
        }

        internal void ExecuteCompletionCommand(GameCommandContext context)
        {
            m_toExecuteOnCompletion.ExecuteFireAndReport(context, nameof(DialogueNode));
        }
    }
}
