using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 当前地图的场景表现配置。
    /// 地图名、检查点栈和存档仍归 MapSystem；这里只承载场景作者需要在 Inspector 中配置的出生、边界和相机目标。
    /// </summary>
    public class MapInfo : MonoBehaviour
    {
        [Header("出生点")]
        [Tooltip("正式进入该地图时的默认出生点。未配置时仍按存档里的检查点进入。")]
        [SerializeReference, SubclassSelector] private ICheckpoint m_initialSpawnCheckpoint = null;

        [Tooltip("仅用于编辑器/Playtest 直接进入地图时的出生点，不能写入正式存档语义。")]
        [SerializeReference, SubclassSelector] private ICheckpoint m_playtestCheckpoint = null;

        [Header("重生")]
        [Min(0f)]
        [Tooltip("玩家死亡后到传送回检查点前的等待秒数，吸收 TopDown LevelManager 的 RespawnDelay 思路。")]
        [SerializeField] private float m_respawnDelay = 0f;

        [Header("地图表现")]
        [Tooltip("是否把本地图的边界暴露给正式相机 Rig 使用。MapInfo 只提供配置，不直接驱动 Cinemachine。")]
        [SerializeField] private bool m_useLevelBounds = false;

        [Tooltip("地图边界碰撞体。正式玩家相机 Rig 会把该值写入 Cinemachine Confiner。")]
        [SerializeField] private Collider2D m_levelBounds = null;

        [Tooltip("本地图的固定相机目标。普通移动场景应留空，让正式玩家相机 Rig 跟随当前控制角色。")]
        [SerializeField] private Transform m_cameraTarget = null;

        [Header("地形导航")]
        [InspectorName("地形导航地图")]
        [Tooltip("本场景唯一的 Tilemap 地形规则入口。未配置时保留旧的直线点击移动，正式地形验收场景必须配置。")]
        [SerializeField] private TerrainNavigationMap m_terrainNavigationMap = null;

        public float respawnDelay => m_respawnDelay;

        /// <summary>
        /// MapInfo 是场景里的地图配置入口，不应由业务代码到处全局扫描。
        /// 这里把当前实例正式登记给 MapSystem，让地图系统自己维护 activeMapInfo 缓存。
        /// </summary>
        private void OnEnable()
        {
            TryRegisterToMapSystem();
        }

        /// <summary>
        /// 某些场景对象的 OnEnable 可能早于 GameManager 初始化。
        /// Start 再补一次正式注册，避免 MapSystem 需要回退到场景扫描。
        /// </summary>
        private void Start()
        {
            TryRegisterToMapSystem();
        }

        private void OnDisable()
        {
            if (GameManager.Exists() && GameManager.TryGetSystem<MapSystem>(out MapSystem mapSystem))
            {
                mapSystem.UnregisterActiveMapInfo(this);
            }
        }

        private void TryRegisterToMapSystem()
        {
            if (GameManager.Exists() && GameManager.TryGetSystem<MapSystem>(out MapSystem mapSystem))
            {
                mapSystem.RegisterActiveMapInfo(this);
            }
        }

        public bool TryGetInitialSpawnCheckpoint(out ICheckpoint checkpoint)
        {
            checkpoint = m_initialSpawnCheckpoint;
            return checkpoint != null && checkpoint.IsValid();
        }

        public bool TryGetPlaytestCheckpoint(out ICheckpoint checkpoint)
        {
            checkpoint = m_playtestCheckpoint;
            return checkpoint != null && checkpoint.IsValid();
        }

        public bool TryGetLevelBounds(out Collider2D levelBounds)
        {
            levelBounds = m_useLevelBounds ? m_levelBounds : null;
            return levelBounds != null;
        }

        public bool TryGetCameraTarget(out Transform cameraTarget)
        {
            cameraTarget = m_cameraTarget;
            return cameraTarget != null;
        }

        public bool TryGetTerrainNavigationMap(out TerrainNavigationMap terrainNavigationMap)
        {
            terrainNavigationMap = m_terrainNavigationMap;
            return terrainNavigationMap != null;
        }
    }
}
