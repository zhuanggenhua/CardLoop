using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ChestInteraction : IInteraction
    {
        [SerializeField] private Chest m_chest = null;

        public Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            return m_chest.TryOpen(source);
        }
    }
}

