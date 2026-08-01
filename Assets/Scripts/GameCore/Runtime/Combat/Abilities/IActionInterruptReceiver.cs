namespace FantasyWord.GameCore
{
    /// <summary>
    /// 角色动作被打断时的正式接收接口。
    /// 当前主要用于能力实例收尾，不再通过 BroadcastMessage 用字符串扫整棵对象树。
    /// </summary>
    public interface IActionInterruptReceiver
    {
        void OnActionInterrupted();
    }
}
