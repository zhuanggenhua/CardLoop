using System;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    [Serializable]
    public class SaveCheckpoint : IContextualCommand
    {
        [SerializeReference, SubclassSelector] private ICheckpoint m_checkpoint;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            GameManager.MapSystem.SaveCheckpoint(m_checkpoint);
            return Task.CompletedTask;
        }
    }
}

