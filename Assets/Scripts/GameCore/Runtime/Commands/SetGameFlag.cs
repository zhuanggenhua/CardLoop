using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameCore
{
    [Serializable]
    public class SetGameFlag : IContextualCommand
    {
        [SerializeField] private string m_flagID = string.Empty;
        [SerializeField] private bool m_state = true;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            GameManager.GameFlagSystem.Set(m_flagID, m_state);
            return Task.CompletedTask;
        }
    }
}

