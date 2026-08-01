using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 对话系统自己拥有主对话通道，外部只通过系统方法协作，不再直接拿到底层 DialogueChannel。
    /// </summary>
    public class DialogueSystem : AGameSystem
    {
        [SerializeField] private DialogueChannel m_mainChannel = null;

        private DialogueChannel MainChannel => m_mainChannel;

        public void AddStartedListener(UnityAction<DialogueTree> listener) => MainChannel.AddStartedListener(listener);
        public void RemoveStartedListener(UnityAction<DialogueTree> listener) => MainChannel.RemoveStartedListener(listener);
        public void AddEndedListener(UnityAction<DialogueTree> listener) => MainChannel.AddEndedListener(listener);
        public void RemoveEndedListener(UnityAction<DialogueTree> listener) => MainChannel.RemoveEndedListener(listener);
        public void AddNodeChangedListener(UnityAction<DialogueNode> listener) => MainChannel.AddNodeChangedListener(listener);
        public void RemoveNodeChangedListener(UnityAction<DialogueNode> listener) => MainChannel.RemoveNodeChangedListener(listener);
        public void Interrupt() => MainChannel.Interrupt();
        public TaskCompletionSource<bool> AddToQueue(DialogueTree dialogue) => MainChannel.AddToQueue(dialogue);
        public Task PlayNow(string line, params object[] args) => MainChannel.PlayNow(line, args);
        public Task PlayNow(DialogueTree dialogue) => MainChannel.PlayNow(dialogue);
        public Task PlayQueue() => MainChannel.PlayQueue();
        public bool IsPlaying() => MainChannel.IsPlaying();
        public bool TryGetCurrentState(out DialogueTree dialogue, out DialogueNode node) => MainChannel.TryGetCurrentState(out dialogue, out node);
        public bool TrySkipping() => MainChannel.TrySkipping();
        public void Next(int option = 0) => MainChannel.Next(option);
    }
}
