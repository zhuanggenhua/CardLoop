using System.Threading.Tasks;

namespace FantasyWord.GameCore
{
    public interface IInteraction
    {
        public Task<bool> TryExecute(CharacterBase source, IInteractionTarget target);
    }
}

