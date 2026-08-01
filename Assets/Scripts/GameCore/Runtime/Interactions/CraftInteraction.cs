using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class CraftInteraction : IInteraction
    {
        [Header("Dialogues")]
        [SerializeField] private DialogueSequence m_dialogue = null;

        [Header("References")]
        [SerializeField] private CraftingStation m_craftingStation = null;

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (m_craftingStation != null)
            {
                await target.Say(m_dialogue, source, async (messages) =>
                {
                    if (messages.Contains(EDialogueMessageType.Accept))
                    {
                        var result = new TaskCompletionSource<bool>();
                        GameRuntimeEvents.RequestCraft(m_craftingStation, GameCommandContext.ResolveForActor(source), result);
                        await result.Task;
                    }
                });

                return true;
            }

            return false;
        }
    }
}

