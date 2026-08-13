using Unity.Cinemachine;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 玩家 3C 相机正式接线点。
    /// 角色和输入真相来自 PlayerSystem，地图相机覆盖和边界来自 MapInfo，实际镜头运动交给 Cinemachine。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [Header("Cinemachine")]
        [Tooltip("正式玩家虚拟相机。该相机必须由 Main Camera 上的 CinemachineBrain 输出。")]
        [SerializeField] private CinemachineCamera m_virtualCamera = null;

        [Tooltip("可选的 2D 相机边界组件。MapInfo 配置了地图边界时会自动写入。")]
        [SerializeField] private CinemachineConfiner2D m_confiner2D = null;

        [Min(0)]
        [Tooltip("玩家相机的默认优先级。当前只有一个正式玩家相机时保持大于 0 即可。")]
        [SerializeField] private int m_livePriority = 10;

        [Tooltip("允许 MapInfo 的相机目标覆盖当前控制角色。用于固定镜头、房间镜头或特殊演出；普通移动测试应保持 MapInfo 为空，从而跟随当前控制角色。")]
        [SerializeField] private bool m_allowMapCameraTargetOverride = true;

        private CharacterBase m_boundControlledCharacter;
        private Transform m_currentTrackingTarget;

        private void Reset()
        {
            m_virtualCamera = GetComponent<CinemachineCamera>();
            m_confiner2D = GetComponent<CinemachineConfiner2D>();
        }

        private void Awake()
        {
            Debug.Assert(m_virtualCamera != null, "PlayerCameraRig requires a CinemachineCamera.");
        }

        private void OnEnable()
        {
            EventKit.Type.Register<SceneLoadedEvent>(OnSceneLoaded);
            EventKit.Type.Register<SceneUnloadedEvent>(OnSceneUnloaded);
            TryBindPlayerSystem();
            RefreshCameraBinding();
        }

        private void Start()
        {
            TryBindPlayerSystem();
            RefreshCameraBinding();
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<SceneLoadedEvent>(OnSceneLoaded);
            EventKit.Type.UnRegister<SceneUnloadedEvent>(OnSceneUnloaded);
            UnbindPlayerSystem();
            ClearCameraBinding();
        }

        private void OnSceneLoaded(SceneLoadedEvent _)
        {
            RefreshCameraBinding();
        }

        private void OnSceneUnloaded(SceneUnloadedEvent _)
        {
            RefreshCameraBinding();
        }

        private void OnControlledCharacterChanged(CharacterBase character)
        {
            BindControlledCharacter(character);
            RefreshCameraBinding();
        }

        private void OnControlledCharacterTeleported()
        {
            if (m_virtualCamera != null)
            {
                m_virtualCamera.PreviousStateIsValid = false;
            }
        }

        private void TryBindPlayerSystem()
        {
            if (!GameManager.Exists() || !GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                return;
            }

            playerSystem.RemoveCurrentControlledCharacterChangedListener(OnControlledCharacterChanged);
            playerSystem.AddCurrentControlledCharacterChangedListener(OnControlledCharacterChanged);
            BindControlledCharacter(playerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        private void UnbindPlayerSystem()
        {
            if (GameManager.Exists() && GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                playerSystem.RemoveCurrentControlledCharacterChangedListener(OnControlledCharacterChanged);
            }

            BindControlledCharacter(null);
        }

        private void BindControlledCharacter(CharacterBase character)
        {
            if (m_boundControlledCharacter == character)
            {
                return;
            }

            if (m_boundControlledCharacter != null)
            {
                m_boundControlledCharacter.RemoveTeleportedListener(OnControlledCharacterTeleported);
            }

            m_boundControlledCharacter = character;

            if (m_boundControlledCharacter != null)
            {
                m_boundControlledCharacter.AddTeleportedListener(OnControlledCharacterTeleported);
            }
        }

        private void RefreshCameraBinding()
        {
            if (m_virtualCamera == null)
            {
                return;
            }

            Transform trackingTarget = ResolveTrackingTarget();
            if (m_currentTrackingTarget != trackingTarget)
            {
                m_currentTrackingTarget = trackingTarget;
                m_virtualCamera.Follow = trackingTarget;
                m_virtualCamera.PreviousStateIsValid = false;
            }

            m_virtualCamera.Priority.Value = m_livePriority;
            RefreshConfiner();
        }

        private Transform ResolveTrackingTarget()
        {
            MapInfo mapInfo = ResolveActiveMapInfo();
            if (m_allowMapCameraTargetOverride && mapInfo != null && mapInfo.TryGetCameraTarget(out Transform mapCameraTarget))
            {
                return mapCameraTarget;
            }

            if (m_boundControlledCharacter != null)
            {
                return m_boundControlledCharacter.transform;
            }

            if (GameManager.Exists() && GameManager.TryGetSystem(out PlayerSystem playerSystem))
            {
                return playerSystem.GetCurrentControlledCharacterOrPlayerInstance()?.transform;
            }

            return null;
        }

        private MapInfo ResolveActiveMapInfo()
        {
            return GameManager.Exists() && GameManager.TryGetSystem(out MapSystem mapSystem)
                ? mapSystem.ResolveActiveMapInfo()
                : null;
        }

        private void RefreshConfiner()
        {
            if (m_confiner2D == null)
            {
                return;
            }

            MapInfo mapInfo = ResolveActiveMapInfo();
            Collider2D levelBounds = mapInfo != null && mapInfo.TryGetLevelBounds(out Collider2D bounds)
                ? bounds
                : null;

            if (m_confiner2D.BoundingShape2D == levelBounds)
            {
                return;
            }

            m_confiner2D.BoundingShape2D = levelBounds;
            m_confiner2D.InvalidateBoundingShapeCache();
        }

        private void ClearCameraBinding()
        {
            if (m_virtualCamera != null)
            {
                m_virtualCamera.Follow = null;
            }

            if (m_confiner2D != null)
            {
                m_confiner2D.BoundingShape2D = null;
                m_confiner2D.InvalidateBoundingShapeCache();
            }

            m_currentTrackingTarget = null;
        }
    }
}
