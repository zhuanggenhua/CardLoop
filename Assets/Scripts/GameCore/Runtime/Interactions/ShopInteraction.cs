using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ShopInteraction : IInteraction
    {
        [Header("Dialogues")]
        [SerializeField] private DialogueSequence m_dialogue = null;

        [Header("References")]
        [SerializeField] private Shop m_shop = null;

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (m_shop != null)
            {
                await target.Say(m_dialogue, source, async (messages) =>
                {
                    if (messages.Contains(EDialogueMessageType.Accept))
                    {
                        var onMenuClosed = new TaskCompletionSource<bool>();
                        GameRuntimeEvents.RequestShop(m_shop, GameCommandContext.ResolveForActor(source), onMenuClosed);
                        await onMenuClosed.Task;
                    }
                });

                return true;
            }

            return false;
        }
    }
}

