using System.Threading.Tasks;

namespace GameCore
{
    public interface IInteraction
    {
        public Task<bool> TryExecute(CharacterBase source, IInteractionTarget target);
    }
}

