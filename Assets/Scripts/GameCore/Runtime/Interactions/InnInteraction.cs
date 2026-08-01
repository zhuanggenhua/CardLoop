using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class InnInteraction : IInteraction
    {
        [Header("Dialogues")]
        [SerializeField] private DialogueSequence m_dialogueIfCanPay = null;
        [SerializeField] private DialogueSequence m_dialogueIfCannotPay = null;

        [Header("References")]
        [SerializeField] private Inn m_inn = null;

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (m_inn != null)
            {
                CharacterActor targetCharacter = source as CharacterActor;

                if (GameManager.InventorySystem.HasSufficientFunds(m_inn.price))
                {
                    await target.Say(m_dialogueIfCanPay, source, (messages) =>
                    {
                        if (messages.Contains(EDialogueMessageType.Accept) && targetCharacter != null)
                        {
                            GameRuntimeEvents.RequestAudioPlayback(m_inn.healingSound);
                            GameManager.InventorySystem.RemoveMoney(m_inn.price);
                            targetCharacter.Heal(m_inn.healAmount);
                            targetCharacter.RecoverMana(m_inn.manaRecoveredAmount);
                        }
                    }, m_inn.price.ToString());
                }
                else
                {
                    await target.Say(m_dialogueIfCannotPay, source);
                }

                return true;
            }

            return false;
        }
    }
}

