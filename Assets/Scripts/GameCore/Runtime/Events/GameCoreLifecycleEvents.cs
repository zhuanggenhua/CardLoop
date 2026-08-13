namespace GameCore
{
    /// <summary>
    /// 技术场景开始加载时发送的生命周期事件。事件类型归 GameCore 所有，派发机制统一走 Yoki EventKit。
    /// </summary>
    public readonly struct SceneLoadingEvent
    {
    }

    /// <summary>
    /// 技术场景完成加载时发送的生命周期事件。监听者应只依赖已稳定的场景结果。
    /// </summary>
    public readonly struct SceneLoadedEvent
    {
    }

    /// <summary>
    /// 技术场景开始卸载时发送的生命周期事件。
    /// </summary>
    public readonly struct SceneUnloadingEvent
    {
    }

    /// <summary>
    /// 技术场景完成卸载时发送的生命周期事件。
    /// </summary>
    public readonly struct SceneUnloadedEvent
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
    /// 场景切换流程开始时发送的生命周期事件。它用于输入锁定等框架级响应，不承载具体场景业务。
    /// </summary>
    public readonly struct SceneTransitionStartedEvent
    {
    }

    /// <summary>
    /// 场景成功完成淡入、加载和淡入后发送的生命周期事件。
    /// </summary>
    public readonly struct SceneTransitionCompletedEvent
    {
    }

    /// <summary>
    /// 场景切换流程结束时发送的生命周期事件。它覆盖成功、失败和取消，
    /// 输入解锁等 finally 清理职责必须订阅此事件，而不是把“成功完成”误当作“流程已结束”。
    /// </summary>
    public readonly struct SceneTransitionEndedEvent
    {
    }

}
