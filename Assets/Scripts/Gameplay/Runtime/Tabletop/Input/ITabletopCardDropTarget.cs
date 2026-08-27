namespace Gameplay.Tabletop
{
    /// <summary>接收牌桌卡牌拖拽的 UI 目标；成功接收后原牌堆保持原位。</summary>
    public interface ITabletopCardDropTarget
    {
        bool TryAcceptCard(TabletopCardId cardId);
    }
}
