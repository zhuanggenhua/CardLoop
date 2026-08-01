using System.Threading.Tasks;

namespace FantasyWord.GameCore
{
    public interface IItemEffect
    {
        public Task<bool> TryUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location);
    }
}

