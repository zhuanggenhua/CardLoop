namespace FantasyWord.GameCore
{
    /// <summary>
    /// 显式声明“这个组件可以接收玩家交互”的正式合同。
    /// 用它替代旧玩家控制器上对 OnInteract 的字符串 SendMessageUpwards 分发，
    /// 让交互入口回到类型系统，而不是继续依赖层级扫描和方法名拼写。
    /// </summary>
    public interface IInteractionReceiver
    {
        void OnInteract(CharacterBase source);
    }
}
