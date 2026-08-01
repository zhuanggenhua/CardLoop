namespace FantasyWord.GameCore
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
    /// 地图开始卸载时发送的领域事件，用于让非 AGameSystem 组件同步释放地图相关状态。
    /// </summary>
    public readonly struct MapUnloadingEvent
    {
    }

    /// <summary>
    /// 地图完成卸载时发送的领域事件。后续新事件继续留在 GameCore 强类型事件层。
    /// </summary>
    public readonly struct MapUnloadedEvent
    {
    }

    /// <summary>
    /// 存档文件完成载入时发送的领域事件。存档生命周期不再依赖旧通知中心。
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

    /// <summary>
    /// 地图过渡委托被请求时发送的事件。它只在 MapSystem 与 TransitionSystem 间传递过场流程，不承载地图业务。
    /// </summary>
    public readonly struct MapTransitionDelegationRequestedEvent
    {
        public MapTransitionDelegationRequestedEvent(MapLoadingDelegationParams delegationParams)
        {
            DelegationParams = delegationParams;
        }

        public MapLoadingDelegationParams DelegationParams { get; }
    }

    /// <summary>
    /// 请求退出当前游戏会话并返回主菜单。
    /// UI 只发布这个语义请求，真正加载哪个场景由正式游戏状态流程决定。
    /// </summary>
    public readonly struct ReturnToMainMenuRequestedEvent
    {
    }

    public static partial class GameRuntimeEvents
    {
        public static void NotifyMapLoading()
        {
            GameManager.DispatchMapLoadingLifecycle();
        }

        public static void NotifyMapLoaded()
        {
            GameManager.DispatchMapLoadedLifecycle();
        }

        public static void NotifyMapUnloading()
        {
            GameManager.DispatchMapUnloadingLifecycle();
        }

        public static void NotifyMapUnloaded()
        {
            GameManager.DispatchMapUnloadedLifecycle();
        }

        public static void NotifySaveFileLoaded()
        {
            GameManager.DispatchSaveFileLoadedLifecycle();
        }

        internal static void PublishMapLoading()
        {
            Publish(new MapLoadingEvent());
        }

        internal static void PublishMapLoaded()
        {
            Publish(new MapLoadedEvent());
        }

        internal static void PublishMapUnloading()
        {
            Publish(new MapUnloadingEvent());
        }

        internal static void PublishMapUnloaded()
        {
            Publish(new MapUnloadedEvent());
        }

        internal static void PublishSaveFileLoaded()
        {
            Publish(new SaveFileLoadedEvent());
        }

        public static void NotifyMapTransitionStarted()
        {
            Publish(new MapTransitionStartedEvent());
        }

        public static void NotifyMapTransitionCompleted()
        {
            Publish(new MapTransitionCompletedEvent());
        }

        public static void NotifyMapTransitionDelegationRequested(MapLoadingDelegationParams delegationParams)
        {
            if (delegationParams == null)
            {
                return;
            }

            Publish(new MapTransitionDelegationRequestedEvent(delegationParams));
        }

        public static void RequestReturnToMainMenu()
        {
            Publish(new ReturnToMainMenuRequestedEvent());
        }

        public static void NotifyGameFlagChanged(string variableName, bool value)
        {
            Publish(new GameFlagChangedEvent(variableName, value));
        }
    }
}
