namespace FantasyWord.GameCore
{
    /// <summary>
    /// Movable 碰撞通知的正式接收接口。
    /// 只让真正关心角色碰撞的组件显式实现，而不是继续依赖 SendMessage 字符串分发。
    /// </summary>
    public interface IMovableCollisionReceiver
    {
        void OnMovableCollision(Movable movable);
    }
}
