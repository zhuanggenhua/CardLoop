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
        /// 当前正式玩法相机入口，只读取场景组合根显式注册的相机。
        /// </summary>
        public static Camera MainCamera => _mainCamera;
        public static GameConfig Config => _instance.m_config;
        public static DatabaseRegistry Database => _instance.m_config.GetDatabaseRegistry();
        public static GameManager Instance => _instance;
        public static GameManagerStartupState StartupState =>
            _instance != null ? _instance.m_startupState : GameManagerStartupState.NotStarted;
        public static Exception StartupException => _instance?.m_startupException;

        // System Access Shortcuts
        public static AudioSystem AudioSystem => GetSystem<AudioSystem>();
        public static DisplaySettingsSystem DisplaySettingsSystem => GetSystem<DisplaySettingsSystem>();
        public static GameFlagSystem GameFlagSystem => GetSystem<GameFlagSystem>();
        public static GameStateSystem GameStateSystem => GetSystem<GameStateSystem>();
        public static InputSystem InputSystem => GetSystem<InputSystem>();
        public static SaveSystem SaveSystem => GetSystem<SaveSystem>();
        public static MapSystem MapSystem => GetSystem<MapSystem>();
        public static SceneSystem SceneSystem => GetSystem<SceneSystem>();
        public static PlayerSystem PlayerSystem => GetSystem<PlayerSystem>();
        public static PersistenceSystem PersistenceSystem => GetSystem<PersistenceSystem>();
        public static TransitionSystem TransitionSystem => GetSystem<TransitionSystem>();
        public static UISystem UISystem => GetSystem<UISystem>();

        // Private Static Members
        private static GameManager _instance = null;
        private static Camera _mainCamera = null;
        private static UnityEngine.Object _mainCameraRegistrationSource = null;
        private Dictionary<Type, AGameSystem> m_systems = null;
        private bool m_startInvoked = false;
        private bool m_resourceRuntimeStarted = false;
        private bool m_modRuntimeStarted = false;
        private bool m_gasRuntimeStarted = false;
        private bool m_shutdownComplete = false;
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

                await ResourceSystem.InitializeAsync(cancellationToken: destroyCancellationToken);
                m_resourceRuntimeStarted = true;

                await ModAPI.Initialize(cancellationToken: destroyCancellationToken);
                if (!ModAPI.Initialized)
                {
                    throw new InvalidOperationException("ModAPI 初始化未完成，不能继续启动 GameCore 系统。");
                }
                m_modRuntimeStarted = true;

                if (this == null || _instance != this)
                {
                    return;
                }

                FormalAbilityRuntimeBootstrap.EnsureInitialized();
                m_gasRuntimeStarted = true;
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

            ShutdownOwnedRuntime(applicationQuit: false);
            ClearMainCameraRegistration();
            _instance = null;
        }

        private void OnApplicationQuit()
        {
            if (_instance == this)
            {
                ShutdownOwnedRuntime(applicationQuit: true);
            }
        }

        private void ShutdownOwnedRuntime(bool applicationQuit)
        {
            if (m_shutdownComplete)
            {
                return;
            }

            SetStartupState(GameManagerStartupState.ShuttingDown);
            ShutdownRuntime(applicationQuit);
            m_shutdownComplete = true;
        }

        public static bool Exists() => _instance;

        /// <summary>
        /// 注册当前场景的正式玩法相机。正式代码通过 <see cref="MainCamera"/> 读取，不使用 Unity 标签查找。
        /// </summary>
        public static void RegisterMainCamera(Camera camera, UnityEngine.Object registrationSource)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }
            if (registrationSource == null)
            {
                throw new ArgumentNullException(nameof(registrationSource));
            }

            if (_mainCamera != null && _mainCamera != camera)
            {
                throw new InvalidOperationException(
                    $"正式玩法相机已经由 {FormatUnityObject(_mainCameraRegistrationSource)} 注册，不能再由 {FormatUnityObject(registrationSource)} 注册另一台相机。");
            }
            if (_mainCameraRegistrationSource != null && _mainCameraRegistrationSource != registrationSource)
            {
                throw new InvalidOperationException(
                    $"正式玩法相机已经由 {FormatUnityObject(_mainCameraRegistrationSource)} 注册，不能再由 {FormatUnityObject(registrationSource)} 重复接管。");
            }

            _mainCamera = camera;
            _mainCameraRegistrationSource = registrationSource;
        }

        /// <summary>
        /// 注销当前场景的正式玩法相机。只允许注册入口清理自己提交的相机引用。
        /// </summary>
        public static void UnregisterMainCamera(Camera camera, UnityEngine.Object registrationSource)
        {
            if (camera == null || registrationSource == null)
            {
                return;
            }
            if (_mainCamera == camera && _mainCameraRegistrationSource == registrationSource)
            {
                ClearMainCameraRegistration();
            }
        }

        private static void ClearMainCameraRegistration()
        {
            _mainCamera = null;
            _mainCameraRegistrationSource = null;
        }

        private static string FormatUnityObject(UnityEngine.Object value)
        {
            return value == null
                ? "已销毁或未注册对象"
                : $"{value.GetType().Name}({value.name})";
        }

        private void SetStartupState(GameManagerStartupState state)
        {
            m_startupState = state;
        }

        private void HandleStartupFailure(Exception exception)
        {
            m_startupException = exception;
            ShutdownRuntime(applicationQuit: false);
            SetStartupState(GameManagerStartupState.Failed);
            Debug.LogException(new InvalidOperationException(
                "GameManager 启动失败：YooAsset、Mod、GAS 或 GameCore 系统未完成初始化。", exception),
                this);
            enabled = false;
        }

        private void ShutdownRuntime(bool applicationQuit)
        {
            ShutdownSystems();
            m_startInvoked = false;

            if (m_gasRuntimeStarted)
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
                    m_gasRuntimeStarted = false;
                }
            }

            if (m_modRuntimeStarted)
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
                    m_modRuntimeStarted = false;
                }
            }

            if (m_resourceRuntimeStarted)
            {
                try
                {
                    if (applicationQuit)
                    {
                        ResourceSystem.ShutdownForApplicationQuit();
                    }
                    else
                    {
                        ResourceSystem.Shutdown();
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException("资源系统关闭失败。", exception), this);
                }
                finally
                {
                    m_resourceRuntimeStarted = false;
                }
            }
        }
    }
}

