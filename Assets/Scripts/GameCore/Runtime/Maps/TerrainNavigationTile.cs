using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameCore
{
    /// <summary>
    /// 地图作者在规则 Tilemap 上绘制的单格导航数据。
    /// 它只提供可行走、高低差、坡道和基础地表，不承载具体玩法状态。
    /// </summary>
    [CreateAssetMenu(fileName = "地形规则-", menuName = "GameCore/地图/地形导航瓦片")]
    public sealed class TerrainNavigationTile : Tile
    {
        [Header("移动规则")]
        [LabelText("可行走")]
        [Tooltip("关闭后该格不会进入路径数据。悬崖正面、深水和实体阻挡应使用不可行走格。")]
        [SerializeField] private bool m_walkable = true;

        [LabelText("地形层级")]
        [Min(0)]
        [Tooltip("低地通常为 0，高台依次增加。它只参与玩法查询，不改变角色的连续世界坐标。")]
        [SerializeField] private int m_elevation = 0;

        [LabelText("过渡类型")]
        [Tooltip("不同地形层级之间只有坡道格允许连接；阻挡格即使误开可行走也会被拒绝。")]
        [SerializeField] private ETerrainTransitionKind m_transitionKind = ETerrainTransitionKind.Ground;

        [LabelText("坡道上坡方向")]
        [Tooltip("从低层指向高层的视觉方向。坡道未配置方向时不能承担跨层连接，普通地面会忽略该值。")]
        [SerializeField] private ETerrainRampDirection m_rampDirection = ETerrainRampDirection.None;

        [Header("地表规则")]
        [LabelText("基础地表")]
        [Tooltip("基础地表只用于通用查询、脚步声或后续项目扩展，不在 GameCore 默认壳内触发元素反应。")]
        [SerializeField] private ETerrainSurfaceKind m_surfaceKind = ETerrainSurfaceKind.Grass;

        [LabelText("通行代价")]
        [Min(0.01f)]
        [Tooltip("A* 进入该格的相对代价。1 为普通地面，更高的值会让单位优先绕行。")]
        [SerializeField] private float m_traversalCost = 1.0f;

        public bool Walkable => m_walkable && m_transitionKind != ETerrainTransitionKind.Blocked;
        public int Elevation => Mathf.Max(0, m_elevation);
        public ETerrainTransitionKind TransitionKind => m_transitionKind;
        public ETerrainRampDirection RampDirection =>
            m_transitionKind == ETerrainTransitionKind.Ramp
                ? m_rampDirection
                : ETerrainRampDirection.None;
        public ETerrainSurfaceKind SurfaceKind => m_surfaceKind;
        public float TraversalCost => Mathf.Max(0.01f, m_traversalCost);
    }

    /// <summary>
    /// 单格地形的基础过渡语义。
    /// 它决定同层移动和坡道连接是否允许，不自动生成物理碰撞。
    /// </summary>
    public enum ETerrainTransitionKind
    {
        Ground,
        Ramp,
        Blocked
    }

    /// <summary>
    /// 坡道从低层到高层的视觉方向。
    /// 该方向同时约束跨层连接，并用于把正交格路径投影为连续坡道中心线。
    /// </summary>
    public enum ETerrainRampDirection
    {
        None = 0,
        NorthEast = 1,
        NorthWest = 2,
        SouthEast = 3,
        SouthWest = 4
    }

    /// <summary>
    /// 规则 Tile 提供的基础地表类型。
    /// GameCore 默认壳只保存基础语义，不在这里实现具体元素反应。
    /// </summary>
    public enum ETerrainSurfaceKind
    {
        None,
        Grass,
        Dirt,
        Stone,
        ShallowWater,
        Mud,
        ScorchedDirt
    }

    /// <summary>
    /// 世界规则查询得到的地形快照。
    /// 它只表达规则 Tilemap 的基础导航事实，具体游戏状态应由项目业务层另行定义。
    /// </summary>
    public readonly struct TerrainSurfaceSample
    {
        public TerrainSurfaceSample(
            Vector3Int cell,
            int elevation,
            ETerrainSurfaceKind surfaceKind,
            float traversalCost)
            : this(TerrainNodeKey.Default(cell), elevation, surfaceKind, traversalCost)
        {
        }

        public TerrainSurfaceSample(
            in TerrainNodeKey nodeKey,
            int elevation,
            ETerrainSurfaceKind surfaceKind,
            float traversalCost)
        {
            NodeKey = nodeKey;
            Elevation = elevation;
            SurfaceKind = surfaceKind;
            TraversalCost = Mathf.Max(0.01f, traversalCost);
        }

        public TerrainNodeKey NodeKey { get; }
        public Vector3Int Cell => NodeKey.Cell;
        public int Elevation { get; }
        public ETerrainSurfaceKind SurfaceKind { get; }
        public float TraversalCost { get; }
    }
}
