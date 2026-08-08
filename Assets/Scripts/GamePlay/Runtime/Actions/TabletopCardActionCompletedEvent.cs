using Gameplay.Content;

namespace Gameplay.Actions
{
    /// <summary>
    /// 一次普通牌桌行动已经完成并成功提交结果的领域事实。
    /// 载荷只保留稳定行动身份；作业、牌桌状态和结果对象仍由各自正式 owner 持有。
    /// </summary>
    public readonly struct TabletopCardActionCompletedEvent
    {
        public TabletopCardActionCompletedEvent(ContentId actionId)
        {
            ActionId = actionId;
        }

        /// <summary>本次已完成行动的唯一内容 ID。</summary>
        public ContentId ActionId { get; }
    }
}
