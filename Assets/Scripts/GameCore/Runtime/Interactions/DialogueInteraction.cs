using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class DialogueInteraction : IInteraction
    {
        [SerializeField] private DialogueSequence m_sequence = null;

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (m_sequence != null)
            {
                await target.Say(m_sequence, source);
                return true;
            }

            return false;
        }
    }
}

