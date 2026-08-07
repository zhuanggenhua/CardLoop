namespace GameCore
{
    /// <summary>
    /// 地图开始加载时发送的领域事件。事件类型归 GameCore 所有，派发机制统一走 Yoki EventKit。
    /// </summary>
    public readonly struct MapLoadingEvent
    {
    }

    /// <summary>
    /// 地图完成加载时发送的领域事件。监听者应只依赖已稳定的地图生命周期结果。
    /// </summary>
    public readonly struct MapLoadedEvent
    {
    }

    /// <summary>
    /// 地图开始卸载时发送的领域事件。地图相关系统和场景组件统一直接订阅该事件。
    /// </summary>
    public readonly struct MapUnloadingEvent
    {
    }

    /// <summary>
    /// 地图完成卸载时发送的领域事件。后续新事件继续留在 GameCore 强类型事件定义层。
    /// </summary>
    public readonly struct MapUnloadedEvent
    {
    }

    /// <summary>
    /// 存档文件完成载入时发送的领域事件。发送方是 SaveSystem，监听者统一直接使用 EventKit。
    /// </summary>
    public readonly struct SaveFileLoadedEvent
    {
    }

    /// <summary>
    /// 轻量世界标记变化时发送的事件。它只描述标记真相变化，不承载任务内容或 UI 表现。
    /// </summary>
    public readonly struct GameFlagChangedEvent
    {
        public GameFlagChangedEvent(string variableName, bool value)
        {
            VariableName = variableName;
            Value = value;
        }

        public string VariableName { get; }

        public bool Value { get; }
    }

    /// <summary>
    /// 地图切换流程开始时发送的领域事件。它用于输入锁定等框架级响应，不承载具体地图业务。
    /// </summary>
    public readonly struct MapTransitionStartedEvent
    {
    }

    /// <summary>
    /// 地图切换流程完成时发送的领域事件。它用于恢复输入等框架级响应。
    /// </summary>
    public readonly struct MapTransitionCompletedEvent
    {
    }

}
