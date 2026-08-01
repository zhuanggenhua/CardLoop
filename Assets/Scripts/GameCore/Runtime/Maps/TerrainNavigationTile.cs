using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 地图作者在规则 Tilemap 上绘制的单格玩法数据。
    /// 它不负责显示地形，只提供可行走、地形层级、坡道和基础地表真相。
    /// </summary>
    [CreateAssetMenu(fileName = "地形规则-", menuName = "GameCore/地图/地形导航瓦片")]
    public sealed class TerrainNavigationTile : Tile
    {
        [Header("移动规则")]
        [InspectorName("可行走")]
        [Tooltip("关闭后该格不会进入路径数据。悬崖正面、深水和实体阻挡应使用不可行走格。")]
        [SerializeField] private bool m_walkable = true;

        [InspectorName("地形层级")]
        [Min(0)]
        [Tooltip("低地通常为 0，高台依次增加。它只参与玩法查询，不改变角色的连续世界坐标。")]
        [SerializeField] private int m_elevation = 0;

        [InspectorName("过渡类型")]
        [Tooltip("不同地形层级之间只有坡道格允许连接；阻挡格即使误开可行走也会被拒绝。")]
        [SerializeField] private ETerrainTransitionKind m_transitionKind = ETerrainTransitionKind.Ground;

        [InspectorName("坡道上坡方向")]
        [Tooltip("从低层指向高层的视觉方向。坡道未配置方向时不能承担跨层连接，普通地面会忽略该值。")]
        [SerializeField] private ETerrainRampDirection m_rampDirection = ETerrainRampDirection.None;

        [Header("地表规则")]
        [InspectorName("基础地表")]
        [Tooltip("后续移动代价、脚步声和元素地表反应都从这里查询，不从视觉 Sprite 名称推断。")]
        [SerializeField] private ETerrainSurfaceKind m_surfaceKind = ETerrainSurfaceKind.Grass;

        [InspectorName("通行代价")]
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
    /// 元素反应、脚步声和通行代价从这里读取，不从视觉 Tile 名称推断。
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
    /// 覆盖在基础地表上的作者/表现层语义。
    /// 例如草、雪和花可以被元素反应移除或改变，但不会改写基础地表。
    /// </summary>
    public enum ETerrainSurfaceCoverKind
    {
        None,
        Grass,
        Snow,
        Moss,
        Leaves,
        Flowers,
        RoadCover
    }

    /// <summary>
    /// 一个 Tilemap 作者层在地表系统中的职责。
    /// Role 只描述语义来源，不代表 Unity SortingLayer 或物理层。
    /// </summary>
    public enum ETerrainSurfaceLayerRole
    {
        None,
        BaseGround,
        SurfaceCover,
        Decoration,
        Water,
        Blocking,
        Shadow,
        RuntimeTemporaryEffect,
        RuntimeResultOverride
    }

    /// <summary>
    /// 上层地表覆盖的可反应属性。
    /// 元素规则用这些标记判断是否可燃、可销毁或可再生。
    /// </summary>
    [Flags]
    public enum ETerrainSurfaceCoverTraits
    {
        None = 0,
        Flammable = 1 << 0,
        Destructible = 1 << 1,
        Regrowable = 1 << 2
    }

    /// <summary>
    /// 上层地表覆盖的运行时生命周期。
    /// 例如草从 Alive 进入 Burning，再变成 Removed 后由表现层隐藏原覆盖 Tile。
    /// </summary>
    public enum ETerrainSurfaceCoverLifecycle
    {
        None,
        Alive,
        Burning,
        Removed,
        Regrowing
    }

    /// <summary>
    /// 作者/表现 Tile 到上层地表语义的显式映射。
    /// 未列入映射的 Tile 只保留视觉职责，不参与元素反应。
    /// </summary>
    [Serializable]
    public sealed class TerrainSurfaceCoverTileMapping
    {
        [InspectorName("来源 Tile")]
        [Tooltip("作者/表现 Tilemap 上的具体 Tile。只有显式列在这里的 Tile 才会产生上层地表语义。")]
        [SerializeField] private TileBase m_tile = null;

        [InspectorName("覆盖类型")]
        [Tooltip("该 Tile 对应的上层地表覆盖类型，例如 Grass/Snow/Moss。")]
        [SerializeField] private ETerrainSurfaceCoverKind m_coverKind =
            ETerrainSurfaceCoverKind.None;

        [InspectorName("覆盖属性")]
        [Tooltip("元素反应可读取的属性，例如可燃、可销毁或可再生。")]
        [SerializeField] private ETerrainSurfaceCoverTraits m_traits =
            ETerrainSurfaceCoverTraits.None;

        public TileBase Tile => m_tile;
        public ETerrainSurfaceCoverKind CoverKind => m_coverKind;
        public ETerrainSurfaceCoverTraits Traits => m_traits;
        public bool IsValid => m_tile != null && m_coverKind != ETerrainSurfaceCoverKind.None;
    }

    /// <summary>
    /// 上层地表覆盖来源的稳定引用。
    /// SourceId 区分不同作者层，Role 说明该层承担的 Tilemap 职责。
    /// </summary>
    public readonly struct TerrainSurfaceCoverSourceReference
    {
        public const int DefaultSurfaceLayerSourceId = 0;
        public const int LegacySurfaceCoverSourceId = -1000;

        public TerrainSurfaceCoverSourceReference(
            int sourceId,
            ETerrainSurfaceLayerRole role)
        {
            SourceId = sourceId;
            Role = role;
        }

        public int SourceId { get; }
        public ETerrainSurfaceLayerRole Role { get; }
        public bool IsValid => Role != ETerrainSurfaceLayerRole.None;

        public static TerrainSurfaceCoverSourceReference None =>
            new(-1, ETerrainSurfaceLayerRole.None);

        public static TerrainSurfaceCoverSourceReference LegacySurfaceCover =>
            new(LegacySurfaceCoverSourceId, ETerrainSurfaceLayerRole.SurfaceCover);
    }

    /// <summary>
    /// 地形格当前活动状态的位标记快照。
    /// 它用于快速匹配规则条件，详细来源仍在 TerrainElementStateSnapshot 中。
    /// </summary>
    [Flags]
    public enum ETerrainRuntimeSurfaceState
    {
        None = 0,
        Wet = 1 << 0,
        Burning = 1 << 1,
        Oiled = 1 << 2,
        Electrified = 1 << 3
    }

    /// <summary>
    /// 世界规则查询得到的地形快照。
    /// 基础地表来自规则 Tile 资产，运行时状态来自地图实例，二者不会互相改写。
    /// </summary>
    public readonly struct TerrainSurfaceSample
    {
        public TerrainSurfaceSample(
            Vector3Int cell,
            int elevation,
            ETerrainSurfaceKind baseSurface,
            ETerrainSurfaceKind effectiveSurface,
            ETerrainSurfaceCoverKind baseSurfaceCover,
            ETerrainSurfaceCoverKind effectiveSurfaceCover,
            ETerrainSurfaceCoverTraits surfaceCoverTraits,
            TerrainSurfaceCoverSourceReference surfaceCoverSource,
            ETerrainSurfaceCoverLifecycle surfaceCoverLifecycle,
            float baseTraversalCost,
            float effectiveTraversalCost,
            in TerrainCellRuntimeStateSnapshot runtimeStateSnapshot)
            : this(
                TerrainNodeKey.Default(cell),
                elevation,
                baseSurface,
                effectiveSurface,
                baseSurfaceCover,
                effectiveSurfaceCover,
                surfaceCoverTraits,
                surfaceCoverSource,
                surfaceCoverLifecycle,
                baseTraversalCost,
                effectiveTraversalCost,
                runtimeStateSnapshot)
        {
        }

        public TerrainSurfaceSample(
            in TerrainNodeKey nodeKey,
            int elevation,
            ETerrainSurfaceKind baseSurface,
            ETerrainSurfaceKind effectiveSurface,
            ETerrainSurfaceCoverKind baseSurfaceCover,
            ETerrainSurfaceCoverKind effectiveSurfaceCover,
            ETerrainSurfaceCoverTraits surfaceCoverTraits,
            TerrainSurfaceCoverSourceReference surfaceCoverSource,
            ETerrainSurfaceCoverLifecycle surfaceCoverLifecycle,
            float baseTraversalCost,
            float effectiveTraversalCost,
            in TerrainCellRuntimeStateSnapshot runtimeStateSnapshot)
        {
            NodeKey = nodeKey;
            Elevation = elevation;
            BaseSurface = baseSurface;
            EffectiveSurface = effectiveSurface;
            BaseSurfaceCover = baseSurfaceCover;
            EffectiveSurfaceCover = effectiveSurfaceCover;
            SurfaceCoverTraits = surfaceCoverTraits;
            SurfaceCoverSource = surfaceCoverSource;
            SurfaceCoverLifecycle = surfaceCoverLifecycle;
            BaseTraversalCost = baseTraversalCost;
            EffectiveTraversalCost = effectiveTraversalCost;
            RuntimeStateSnapshot = runtimeStateSnapshot;
        }

        public TerrainNodeKey NodeKey { get; }
        public Vector3Int Cell => NodeKey.Cell;
        public int Elevation { get; }
        public ETerrainSurfaceKind BaseSurface { get; }
        public ETerrainSurfaceKind EffectiveSurface { get; }
        public ETerrainSurfaceCoverKind BaseSurfaceCover { get; }
        public ETerrainSurfaceCoverKind EffectiveSurfaceCover { get; }
        public ETerrainSurfaceCoverTraits SurfaceCoverTraits { get; }
        public TerrainSurfaceCoverSourceReference SurfaceCoverSource { get; }
        public ETerrainSurfaceCoverLifecycle SurfaceCoverLifecycle { get; }
        public float BaseTraversalCost { get; }
        public float EffectiveTraversalCost { get; }
        public TerrainCellRuntimeStateSnapshot RuntimeStateSnapshot { get; }

        public float TraversalCost => EffectiveTraversalCost;
        public bool HasSurfaceCover =>
            EffectiveSurfaceCover != ETerrainSurfaceCoverKind.None;
        public bool IsSurfaceCoverFlammable =>
            (SurfaceCoverTraits & ETerrainSurfaceCoverTraits.Flammable) != 0;
        public ETerrainRuntimeSurfaceState RuntimeState => RuntimeStateSnapshot.RuntimeStateFlags;
        public IReadOnlyList<TerrainElementStateSnapshot> ActiveStates =>
            RuntimeStateSnapshot.ActiveStates;
    }
}
