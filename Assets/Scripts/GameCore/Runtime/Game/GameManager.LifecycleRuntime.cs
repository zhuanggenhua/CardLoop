namespace FantasyWord.GameCore
{
    public partial class GameManager
    {
        private void OnMapLoading()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnMapLoading();
            }

            GameRuntimeEvents.PublishMapLoading();
        }

        private void OnMapLoaded()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnMapLoaded();
            }

            GameRuntimeEvents.PublishMapLoaded();
        }

        private void OnMapUnloading()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnMapUnloading();
            }

            GameRuntimeEvents.PublishMapUnloading();
        }

        private void OnMapUnloaded()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnMapUnloaded();
            }

            GameRuntimeEvents.PublishMapUnloaded();
        }

        private void OnSaveFileLoaded()
        {
            foreach (AGameSystem system in m_systems.Values)
            {
                system.OnSaveFileLoaded();
            }

            GameRuntimeEvents.PublishSaveFileLoaded();
        }

        // GameManager 只保留系统生命周期分发职责；正式事件触发入口统一走 GameRuntimeEvents。
        internal static void DispatchMapLoadingLifecycle()
        {
            if (_instance != null && _instance.m_lifecycleEventsEnabled)
            {
                _instance.OnMapLoading();
            }
        }

        internal static void DispatchMapLoadedLifecycle()
        {
            if (_instance != null && _instance.m_lifecycleEventsEnabled)
            {
                _instance.OnMapLoaded();
            }
        }

        internal static void DispatchMapUnloadingLifecycle()
        {
            if (_instance != null && _instance.m_lifecycleEventsEnabled)
            {
                _instance.OnMapUnloading();
            }
        }

        internal static void DispatchMapUnloadedLifecycle()
        {
            if (_instance != null && _instance.m_lifecycleEventsEnabled)
            {
                _instance.OnMapUnloaded();
            }
        }

        internal static void DispatchSaveFileLoadedLifecycle()
        {
            if (_instance != null && _instance.m_lifecycleEventsEnabled)
            {
                _instance.OnSaveFileLoaded();
            }
        }
    }
}
