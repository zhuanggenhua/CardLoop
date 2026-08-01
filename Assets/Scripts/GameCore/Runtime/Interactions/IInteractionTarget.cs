using System.Threading.Tasks;
using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    public interface IInteractionTarget : IInteractionReceiver
    {
        public string GetSpeakerName();
        public Task Say(DialogueSequence sequence, UnityAction<DialogueMessageFeed> onDialogueEnded = null, params string[] args);
        public Task Say(DialogueSequence sequence, CharacterBase source, UnityAction<DialogueMessageFeed> onDialogueEnded = null, params string[] args);
    }
}

