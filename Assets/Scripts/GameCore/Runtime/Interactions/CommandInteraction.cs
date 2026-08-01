using System;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class CommandInteraction : IInteraction
    {
        [SerializeReference, SubclassSelector] private ICommand m_command = null;

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (m_command != null)
            {
                await m_command.Execute(ResolveCommandContext(source));
                return true;
            }

            return false;
        }

        private static GameCommandContext ResolveCommandContext(CharacterBase source)
        {
            return GameCommandContext.ResolveForActor(source);
        }
    }
}

