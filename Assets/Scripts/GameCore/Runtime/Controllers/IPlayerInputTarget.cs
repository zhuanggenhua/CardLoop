namespace GameCore
{
    /// <summary>
    /// 当前玩家输入的正式落点。
    /// 当前由角色上的 CharacterPlayerControl 或控制组实现，后续编队也应实现同一接口，而不是再复制一套 InputAction 订阅逻辑。
    /// </summary>
    public interface IPlayerInputTarget
    {
        bool TryGetControlledCharacter(out CharacterBase character);

        CharacterBase[] CreateControlledCharacterSnapshot();

        PlayerOrderResult SubmitPlayerOrder(PlayerOrderRequest orderRequest);
    }
}
