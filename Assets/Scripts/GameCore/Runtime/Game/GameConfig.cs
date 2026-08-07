using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.Game + nameof(GameConfig))]
    public partial class GameConfig : DatabaseEntry
    {
        [Header("General Settings")]
        [SerializeField, FormerlySerializedAs("databaseRegistry")]
        private DatabaseRegistry m_databaseRegistry = null;

        [Header("Physics Settings")]
        [SerializeField, FormerlySerializedAs("interactionLayer")]
        private string m_interactionLayer = "Interaction";

        [SerializeField, FormerlySerializedAs("hitboxLayer")]
        private string m_hitboxLayer = "Hitbox";

        [Min(0.0f)]
        [SerializeField, FormerlySerializedAs("maxTeleportDistanceWhenStuckInWall")]
        private float m_maxTeleportDistanceWhenStuckInWall = 5.0f;

        [SerializeField, FormerlySerializedAs("collisionContactFilter")]
        private ContactFilter2D m_collisionContactFilter;

        [SerializeField, FormerlySerializedAs("visibilityContactFilter")]
        private ContactFilter2D m_visibilityContactFilter;

        [Tooltip("ContextSteering2D 用于识别其他角色和可选目标的过滤条件。必须与墙体/地形阻挡过滤分开配置。")]
        [SerializeField] private ContactFilter2D m_steeringNeighbourContactFilter;

        [Header("Visual Settings")]
        [SerializeField, FormerlySerializedAs("cameraShakeSources")]
        private ECameraShakeSources m_cameraShakeSources = ECameraShakeSources.None;

        [Header("Combat Settings")]
        [Range(1, Constants.MaxEquipedAbilityCount)]
        [SerializeField, FormerlySerializedAs("maxEquippableAbilities")]
        private int m_maxEquippableAbilities = 5;

        [SerializeField, FormerlySerializedAs("canCriticalHit")]
        private bool m_canCriticalHit = true;

        [SerializeField, FormerlySerializedAs("canMissHit")]
        private bool m_canMissHit = true;

        [SerializeField, FormerlySerializedAs("allowPushOnRegularHit")]
        private bool m_allowPushOnRegularHit = true;

        [SerializeField, FormerlySerializedAs("allowPushOnCriticalHit")]
        private bool m_allowPushOnCriticalHit = true;

        [SerializeField, FormerlySerializedAs("allowPushOnMissedHit")]
        private bool m_allowPushOnMissedHit = true;

        [SerializeField, FormerlySerializedAs("allowPushOnSilentHit")]
        private bool m_allowPushOnSilentHit = false;

        [Header("UI Settings")]
        [SerializeField, FormerlySerializedAs("navigationSelectSound")]
        private AudioClipResolver m_navigationSelectSound = null;

        [SerializeField, FormerlySerializedAs("pointerSelectSound")]
        private AudioClipResolver m_pointerSelectSound = null;

        [SerializeField, FormerlySerializedAs("submitSound")]
        private AudioClipResolver m_submitSound = null;

        public string interactionLayer => m_interactionLayer;
        public string hitboxLayer => m_hitboxLayer;
        public float maxTeleportDistanceWhenStuckInWall => m_maxTeleportDistanceWhenStuckInWall;
        public ContactFilter2D collisionContactFilter => m_collisionContactFilter;
        public ContactFilter2D visibilityContactFilter => m_visibilityContactFilter;
        public ContactFilter2D steeringNeighbourContactFilter => m_steeringNeighbourContactFilter;
        public ECameraShakeSources cameraShakeSources => m_cameraShakeSources;
        public int maxEquippableAbilities => m_maxEquippableAbilities;
        public bool canCriticalHit => m_canCriticalHit;
        public bool canMissHit => m_canMissHit;
        public bool allowPushOnRegularHit => m_allowPushOnRegularHit;
        public bool allowPushOnCriticalHit => m_allowPushOnCriticalHit;
        public bool allowPushOnMissedHit => m_allowPushOnMissedHit;
        public bool allowPushOnSilentHit => m_allowPushOnSilentHit;
        public AudioClipResolver navigationSelectSound => m_navigationSelectSound;
        public AudioClipResolver pointerSelectSound => m_pointerSelectSound;
        public AudioClipResolver submitSound => m_submitSound;

        /// <summary>
        /// 数据库注册表的正式外部入口是 GameManager.Database。
        /// 这里只给 GameManager 留同程序集读取口，避免再把同一真相同时挂在 Config 和 GameManager 两边。
        /// </summary>
        internal DatabaseRegistry GetDatabaseRegistry()
        {
            return m_databaseRegistry;
        }
    }
}
