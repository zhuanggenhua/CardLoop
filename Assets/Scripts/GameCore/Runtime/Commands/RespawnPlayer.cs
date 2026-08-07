using System;
using System.Threading.Tasks;

namespace GameCore
{
    [Serializable]
    public class RespawnPlayer : IContextualCommand
    {
        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            GameManager.MapSystem.RespawnPlayer();
            return Task.CompletedTask;
        }
    }
}

