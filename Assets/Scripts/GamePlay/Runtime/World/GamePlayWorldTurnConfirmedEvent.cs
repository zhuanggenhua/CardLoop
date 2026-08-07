namespace GamePlay
{
    /// <summary>
    /// 世界流程确认了一次普通回合的事实。
    /// 这是 YokiFrame EventKit 的直接事件载荷，不承担日结、遭遇或行动结果。
    /// </summary>
    public readonly struct GamePlayWorldTurnConfirmedEvent
    {
        /// <summary>从一开始连续递增的已确认世界回合编号。</summary>
        public int ConfirmedTurnIndex { get; }

        public GamePlayWorldTurnConfirmedEvent(int confirmedTurnIndex)
        {
            ConfirmedTurnIndex = confirmedTurnIndex;
        }
    }
}
