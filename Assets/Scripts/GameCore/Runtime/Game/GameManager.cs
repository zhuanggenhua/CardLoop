using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameCore
{
    public enum GameManagerStartupState
    {
        NotStarted = 0,
        Initializing = 10,
        Ready = 20,
        Failed = 30,
        ShuttingDown = 40
    }

    [DefaultExecutionOrder(-10000)]
    public partial class GameManager : MonoBehaviour
    {
        // Inspector Settings
        [Header("Global Settings")]
        [SerializeField] private GameConfig m_config = null;

        // Public Static Members
        /// <summary>
        /// 项目侧正式 UI 输入入口。
        /// 除唯一节点诊断和第三方 UIKit 内部实现外，其它 GameCore 代码不再直接读取 EventSystem.current。
        /// </summary>
        public static EventSystem EventSystem => EventSystem.current;
        /// <summary>
        /// 当前正式玩法相机入口。
        /// 现阶段仍跟随 Unity 主相机语义，后续若切到模式相机或多相机入口，只改这里。
        /// </summary>
        public static Camera MainCamera => Camera.main;
        public static GameConfig Config => _instance.m_config;
        public static DatabaseRegistry Database => _instance.m_config.GetDatabaseRegistry();
        public static GameManager Instance => _instance;
        public static GameManagerStartupState StartupState =>
            _instance != null ? _instance.m_startupState : GameManagerStartupState.NotStarted;
        public static Exception StartupException => _instance?.m_startupException;

        // System Access Shortcuts
        public static AudioSystem AudioSystem => GetSystem<AudioSystem>();
        public static GameFlagSystem GameFlagSystem => GetSystem<GameFlagSystem>();
        public static GameStateSystem GameStateSystem => GetSystem<GameStateSystem>();
        public static InputSystem InputSystem => GetSystem<InputSystem>();
        public static SaveSystem SaveSystem => GetSystem<SaveSystem>();
        public static MapSystem MapSystem => GetSystem<MapSystem>();
        public static PlayerSystem PlayerSystem => GetSystem<PlayerSystem>();
        public static PersistenceSystem PersistenceSystem => GetSystem<PersistenceSystem>();
        public static TransitionSystem TransitionSystem => GetSystem<TransitionSystem>();
        public static UISystem UISystem => GetSystem<UISystem>();

        // Private Static Members
        private static GameManager _instance = null;
        private Dictionary<Type, AGameSystem> m_systems = null;
        private bool m_startInvoked = false;
        private bool m_resourceInitializationEntered = false;
        private bool m_modInitializationEntered = false;
        private bool m_gasInitializationEntered = false;
        private GameManagerStartupState m_startupState = GameManagerStartupState.NotStarted;
        private Exception m_startupException = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError(
                    $"检测到重复的 {nameof(GameManager)}。当前场景中的实例将被销毁，正式入口只能有一个。",
                    this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            FindSystems();
        }

        private void OnEnable()
        {
            if (m_startInvoked)
            {
                try
                {
                    StartSystems();
                }
                catch (Exception exception)
                {
                    HandleStartupFailure(exception);
                }
            }
        }

        private async void Start()
        {
            if (_instance != this)
            {
                return;
            }

            SetStartupState(GameManagerStartupState.Initializing);

            try
            {
                if (m_config == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(GameManager)} 缺少 {nameof(GameConfig)}。测试场景和正式入口必须显式配置它。");
                }

                m_resourceInitializationEntered = true;
                await ResourceSystem.InitializeAsync(cancellationToken: destroyCancellationToken);

                m_modInitializationEntered = true;
                await ModAPI.Initialize();
                if (!ModAPI.Initialized)
                {
                    throw new InvalidOperationException("ModAPI 初始化未完成，不能继续启动 GameCore 系统。");
                }

                if (this == null || _instance != this)
                {
                    return;
                }

                m_gasInitializationEntered = true;
                FormalAbilityRuntimeBootstrap.EnsureInitialized();
                InitializeSystems();

                m_startInvoked = true;
                if (isActiveAndEnabled)
                {
                    StartSystems();
                }

                SetStartupState(GameManagerStartupState.Ready);
            }
            catch (OperationCanceledException) when (this == null)
            {
            }
            catch (Exception exception)
            {
                HandleStartupFailure(exception);
            }
        }

        private void OnDisable()
        {
            if (m_startInvoked)
            {
                StopSystems();
            }

        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            SetStartupState(GameManagerStartupState.ShuttingDown);
            ShutdownRuntime();
            _instance = null;
        }

        public static bool Exists() => _instance;

        private void SetStartupState(GameManagerStartupState state)
        {
            m_startupState = state;
        }

        private void HandleStartupFailure(Exception exception)
        {
            m_startupException = exception;
            ShutdownRuntime();
            SetStartupState(GameManagerStartupState.Failed);
            Debug.LogException(new InvalidOperationException(
                "GameManager 启动失败：YooAsset、Mod、GAS 或 GameCore 系统未完成初始化。", exception),
                this);
            enabled = false;
        }

        private void ShutdownRuntime()
        {
            ShutdownSystems();
            m_startInvoked = false;

            if (m_gasInitializationEntered)
            {
                try
                {
                    FormalAbilityRuntimeBootstrap.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException("GAS 关闭失败。", exception), this);
                }
                finally
                {
                    m_gasInitializationEntered = false;
                }
            }

            if (m_modInitializationEntered)
            {
                try
                {
                    ModAPI.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException("ModAPI 关闭失败。", exception), this);
                }
                finally
                {
                    m_modInitializationEntered = false;
                }
            }

            if (m_resourceInitializationEntered)
            {
                try
                {
                    ResourceSystem.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException("资源系统关闭失败。", exception), this);
                }
                finally
                {
                    m_resourceInitializationEntered = false;
                }
            }
        }
    }
}

