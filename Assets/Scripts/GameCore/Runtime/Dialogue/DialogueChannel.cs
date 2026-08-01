using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 队列中的可等待对话树，保存播放对象和调用方等待的完成信号。
    /// </summary>
    public class AwaitableDialogueTree
    {
        public DialogueTree dialogue { get; set; }
        public TaskCompletionSource<bool> task { get; set; }
    }

    /// <summary>
    /// 对话播放通道，串行消费对话队列并向 UI/系统广播开始、结束和节点变化事件。
    /// </summary>
    public class DialogueChannel : MonoBehaviour
    {
        private readonly UnityEvent<DialogueTree> m_dialogueStarted = new();
        private readonly UnityEvent<DialogueTree> m_dialogueEnded = new();
        private readonly UnityEvent<DialogueNode> m_dialogueNodeChanged = new();
        private AwaitableDialogueTree m_currentTree = null;
        private DialogueNode m_currentNode = null;
        private readonly Queue<AwaitableDialogueTree> m_dialogueQueue = new();

        public void AddStartedListener(UnityAction<DialogueTree> listener) => m_dialogueStarted.AddListener(listener);
        public void RemoveStartedListener(UnityAction<DialogueTree> listener) => m_dialogueStarted.RemoveListener(listener);
        public void AddEndedListener(UnityAction<DialogueTree> listener) => m_dialogueEnded.AddListener(listener);
        public void RemoveEndedListener(UnityAction<DialogueTree> listener) => m_dialogueEnded.RemoveListener(listener);
        public void AddNodeChangedListener(UnityAction<DialogueNode> listener) => m_dialogueNodeChanged.AddListener(listener);
        public void RemoveNodeChangedListener(UnityAction<DialogueNode> listener) => m_dialogueNodeChanged.RemoveListener(listener);

        /// <summary>
        /// 中断当前对话并清空等待队列；当前对话会按完成流程通知监听者。
        /// </summary>
        public void Interrupt()
        {
            ClearQueue();

            if (m_currentTree != null)
            {
                OnDialogueCompleted();
            }
        }

        private void OnDisable()
        {
            CancelCurrentDialogue(false);
            ClearQueue();
        }

        private void OnDestroy()
        {
            CancelCurrentDialogue(false);
            ClearQueue();
        }

        /// <summary>
        /// 把对话树加入播放队列，并返回调用方可等待的完成信号。
        /// </summary>
        public TaskCompletionSource<bool> AddToQueue(DialogueTree dialogue)
        {
            var task = new TaskCompletionSource<bool>();
            if (dialogue == null)
            {
                Debug.LogError("Cannot enqueue a null dialogue tree.", this);
                task.TrySetResult(false);
                return task;
            }

            m_dialogueQueue.Enqueue(new()
            {
                dialogue = dialogue,
                task = task
            });

            return task;
        }

        /// <summary>
        /// 清空尚未播放的队列，并把等待者标记为未播放完成。
        /// </summary>
        public void ClearQueue()
        {
            foreach (var dialogue in m_dialogueQueue)
            {
                CompleteDialogueTask(dialogue, false);
            }

            m_dialogueQueue.Clear();
        }

        /// <summary>
        /// 立即排队播放一行格式化文本；如果通道空闲会马上开始消费队列。
        /// </summary>
        public async Task PlayNow(string line, params object[] args)
        {
            await PlayNow(new DialogueTree(new DialogueNode(StringFormatter.Format(line, args))));
        }

        /// <summary>
        /// 立即排队播放对话树；已有对话播放时会等待该树轮到并结束。
        /// </summary>
        public async Task PlayNow(DialogueTree dialogue)
        {
            var task = AddToQueue(dialogue);

            if (!IsPlaying())
            {
                await PlayQueue();
            }
            else
            {
                await task.Task;
            }
        }

        /// <summary>
        /// 在通道空闲时按顺序播放队列中的所有对话。
        /// </summary>
        public async Task PlayQueue()
        {
            if (!IsPlaying())
            {
                while (m_dialogueQueue.Count > 0)
                {
                    var current = m_dialogueQueue.Dequeue();
                    Play(current);
                    await current.task.Task;
                }
            }
        }

        /// <summary>
        /// 尝试跳过当前节点；多选节点不会自动选择，避免误触选项。
        /// </summary>
        public bool TrySkipping()
        {
            if (m_currentNode != null && m_currentNode.optionCount < 2)
            {
                Next();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 进入当前节点的下一个节点，并执行当前节点完成命令。
        /// </summary>
        public void Next(int option = 0)
        {
            if (m_currentTree == null || m_currentNode == null)
            {
                return;
            }

            m_currentTree.dialogue.OnNodeExecuted(m_currentNode, option);
            m_currentNode.ExecuteCompletionCommand(m_currentTree.dialogue.CommandContext);
            SetCurrentNode(m_currentNode.GetNext(option));
        }

        public bool IsPlaying() => m_currentTree != null;

        public bool TryGetCurrentState(out DialogueTree dialogue, out DialogueNode node)
        {
            if (m_currentTree == null)
            {
                dialogue = null;
                node = null;
                return false;
            }

            dialogue = m_currentTree.dialogue;
            node = m_currentNode;
            return true;
        }

        private void Play(AwaitableDialogueTree tree)
        {
            if (tree == null || tree.dialogue == null)
            {
                CompleteDialogueTask(tree, false);
                return;
            }

            if (tree.dialogue.HasEntryPoint())
            {
                m_currentTree = tree;
                m_dialogueStarted.Invoke(tree.dialogue);
                tree.dialogue.NotifyStarted();
                SetCurrentNode(tree.dialogue.GetEntryPoint());
            }
            else
            {
                Debug.LogError("Cannot start a dialogue with a null entry point node.");
                CompleteDialogueTask(tree, false);
            }
        }

        private void SetCurrentNode(DialogueNode node)
        {
            m_currentNode = node;
            m_dialogueNodeChanged.Invoke(m_currentNode);

            if (m_currentNode == null)
            {
                OnDialogueCompleted();
            }
            else
            {
                m_currentNode.ExecuteStartCommand(m_currentTree.dialogue.CommandContext);
            }
        }

        private void OnDialogueCompleted()
        {
            if (IsPlaying())
            {
                m_currentNode = null;
                m_dialogueEnded.Invoke(m_currentTree.dialogue);
                m_currentTree.dialogue.NotifyEnded();
                var task = m_currentTree.task;
                m_currentTree = null;
                task.TrySetResult(true);
            }
        }

        private void CancelCurrentDialogue(bool notifyEnded)
        {
            if (m_currentTree == null)
            {
                m_currentNode = null;
                return;
            }

            DialogueTree dialogue = m_currentTree.dialogue;
            TaskCompletionSource<bool> task = m_currentTree.task;
            m_currentTree = null;
            m_currentNode = null;

            if (notifyEnded && dialogue != null)
            {
                m_dialogueEnded.Invoke(dialogue);
                dialogue.NotifyEnded();
            }

            task?.TrySetResult(false);
        }

        private static void CompleteDialogueTask(AwaitableDialogueTree tree, bool completed)
        {
            tree?.task?.TrySetResult(completed);
        }
    }
}
