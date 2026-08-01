using System;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 移动当前上下文角色的命令，没有上下文角色时退回主玩家角色。
    /// </summary>
    [Serializable]
    public class MovePlayer : MoveCharacterBase
    {
        protected override CharacterBase targetCharacter => GameManager.PlayerSystem.GetPrimaryPlayerCharacter();

        protected override CharacterBase ResolveTargetCharacter(GameCommandContext context)
        {
            return context.ResolveActorOrCurrentControlledCharacter();
        }
    }
}

