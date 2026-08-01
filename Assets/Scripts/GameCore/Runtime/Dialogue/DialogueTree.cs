using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    public class DialogueTree
    {
        private readonly UnityEvent m_dialogueStarted = new();
        private readonly UnityEvent<DialogueMessageFeed> m_dialogueEnded = new();
        private readonly DialogueNode m_root;
        private readonly DialogueMessageFeed m_messages = new();

        public DialogueTree(DialogueNode root)
            : this(root, GameCommandContext.Script())
        {
        }

        public DialogueTree(DialogueNode root, GameCommandContext commandContext)
        {
            m_root = root;
            CommandContext = commandContext;
        }

        internal GameCommandContext CommandContext { get; }

        public void OnNodeExecuted(DialogueNode node, int option)
        {
            if (node != null && node.TryGetOption(option, out DialogueNodeOption selectedOption))
            {
                DialogueMessage message = selectedOption.message;

                if (!string.IsNullOrWhiteSpace(message.ToString()))
                {
                    m_messages.Add(message);
                }
            }
        }

        public void AddStartedListener(UnityAction listener) => m_dialogueStarted.AddListener(listener);
        public void RemoveStartedListener(UnityAction listener) => m_dialogueStarted.RemoveListener(listener);
        public void AddEndedListener(UnityAction<DialogueMessageFeed> listener) => m_dialogueEnded.AddListener(listener);
        public void RemoveEndedListener(UnityAction<DialogueMessageFeed> listener) => m_dialogueEnded.RemoveListener(listener);
        internal bool HasEntryPoint() => m_root != null;
        internal DialogueNode GetEntryPoint() => m_root;

        internal void NotifyStarted() => m_dialogueStarted.Invoke();
        internal void NotifyEnded() => m_dialogueEnded.Invoke(m_messages);
    }
}
