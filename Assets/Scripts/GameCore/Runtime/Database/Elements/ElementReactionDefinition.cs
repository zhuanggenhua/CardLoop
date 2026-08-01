using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 元素反应命中后要执行的一条结果操作。
    /// 它只描述规则结果，真正写入地表状态由运行时系统统一处理。
    /// </summary>
    [Serializable]
    public sealed class ElementReactionOperation
    {
        [InspectorName("操作类型")]
        [SerializeField] private EElementReactionOperationKind m_kind =
            EElementReactionOperationKind.AddOrRefreshState;

        [InspectorName("目标状态")]
        [Tooltip("添加、刷新或移除的地表运行时状态。仅状态类操作会读取。")]
        [SerializeField] private ETerrainElementStateKind m_stateKind =
            ETerrainElementStateKind.None;

        [InspectorName("强度倍率")]
        [Tooltip("施加元素强度会乘以该值后写入状态，低于 0 会被运行时夹到 0。")]
        [Min(0.0f)]
        [SerializeField] private float m_intensityMultiplier = 1.0f;

        [InspectorName("持续时间覆盖")]
        [Tooltip("小于等于 0 时使用状态定义的默认持续时间。")]
        [SerializeField] private float m_durationOverride = 0.0f;

        [InspectorName("目标基础地表")]
        [Tooltip("用于改写有效地表的操作；不会改动作者规则 Tile。")]
        [SerializeField] private ETerrainSurfaceKind m_surfaceKind =
            ETerrainSurfaceKind.None;

        [InspectorName("目标上层覆盖")]
        [Tooltip("用于移除、设置或清除上层地表覆盖覆盖值。")]
        [SerializeField] private ETerrainSurfaceCoverKind m_surfaceCoverKind =
            ETerrainSurfaceCoverKind.None;

        [InspectorName("表现信号")]
        [Tooltip("用于触发蒸汽等瞬时表现，不直接写入持久地形数据。")]
        [SerializeField] private EElementPresentationSignal m_presentationSignal =
            EElementPresentationSignal.None;

        public EElementReactionOperationKind Kind => m_kind;
        public ETerrainElementStateKind StateKind => m_stateKind;
        public float IntensityMultiplier => Mathf.Max(0.0f, m_intensityMultiplier);
        public float DurationOverride => m_durationOverride;
        public ETerrainSurfaceKind SurfaceKind => m_surfaceKind;
        public ETerrainSurfaceCoverKind SurfaceCoverKind => m_surfaceCoverKind;
        public EElementPresentationSignal PresentationSignal => m_presentationSignal;
    }

    /// <summary>
    /// 一条可数据化的地表元素反应规则。
    /// 它把“什么元素/状态命中什么地表条件”映射成结果操作，不直接负责施法、Tilemap 显示或存档。
    /// </summary>
    [CreateAssetMenu(
        fileName = "元素反应-",
        menuName = "GameCore/元素/元素反应定义")]
    public sealed class ElementReactionDefinition : DatabaseEntry
    {
        [Header("触发")]
        [InspectorName("触发时机")]
        [Tooltip("元素刚施加时触发，或运行时状态到期时触发。")]
        [SerializeField] private EElementReactionTrigger m_trigger =
            EElementReactionTrigger.OnElementApplied;

        [InspectorName("施加元素")]
        [Tooltip("OnElementApplied 规则需要匹配的世界元素。")]
        [SerializeField] private EWorldElementKind m_elementKind = EWorldElementKind.None;

        [InspectorName("到期状态")]
        [Tooltip("OnStateExpired 规则需要匹配的到期运行时状态。")]
        [SerializeField] private ETerrainElementStateKind m_expiredStateKind =
            ETerrainElementStateKind.None;

        [InspectorName("最低强度")]
        [Tooltip("元素施加强度低于该值时不触发规则。")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float m_minimumIntensity = 0.0f;

        [Header("地表条件")]
        [InspectorName("要求基础地表")]
        [SerializeField] private bool m_requireBaseSurface = false;

        [InspectorName("基础地表")]
        [Tooltip("规则 Tile 资产提供的作者地表类型。")]
        [SerializeField] private ETerrainSurfaceKind m_baseSurface = ETerrainSurfaceKind.None;

        [InspectorName("要求有效地表")]
        [SerializeField] private bool m_requireEffectiveSurface = false;

        [InspectorName("有效地表")]
        [Tooltip("基础地表叠加运行时覆盖后的当前地表类型。")]
        [SerializeField] private ETerrainSurfaceKind m_effectiveSurface = ETerrainSurfaceKind.None;

        [Header("上层地表条件")]
        [InspectorName("要求上层覆盖")]
        [SerializeField] private bool m_requireSurfaceCover = false;

        [InspectorName("上层覆盖")]
        [Tooltip("草、雪、苔藓等作者/表现层映射出来的上层地表覆盖。")]
        [SerializeField] private ETerrainSurfaceCoverKind m_surfaceCover =
            ETerrainSurfaceCoverKind.None;

        [InspectorName("必须具备的覆盖属性")]
        [Tooltip("例如 Flammable/Destructible。全部命中才触发。")]
        [SerializeField] private ETerrainSurfaceCoverTraits m_requiredSurfaceCoverTraits =
            ETerrainSurfaceCoverTraits.None;

        [InspectorName("禁止具备的覆盖属性")]
        [Tooltip("命中任意一个禁止属性时不触发。")]
        [SerializeField] private ETerrainSurfaceCoverTraits m_forbiddenSurfaceCoverTraits =
            ETerrainSurfaceCoverTraits.None;

        [Header("状态条件")]
        [InspectorName("必须具备的运行时状态")]
        [SerializeField] private ETerrainRuntimeSurfaceState m_requiredStates =
            ETerrainRuntimeSurfaceState.None;

        [InspectorName("禁止具备的运行时状态")]
        [SerializeField] private ETerrainRuntimeSurfaceState m_forbiddenStates =
            ETerrainRuntimeSurfaceState.None;

        [Header("结果")]
        [InspectorName("优先级")]
        [Tooltip("同一上下文命中多条规则时，优先级高的先执行；同优先级按稳定 ID 排序。")]
        [SerializeField] private int m_priority = 0;

        [InspectorName("结果操作")]
        [Tooltip("命中规则后按顺序执行的状态/地表/表现信号操作。至少需要一条。")]
        [SerializeField] private ElementReactionOperation[] m_operations =
            Array.Empty<ElementReactionOperation>();

        public EElementReactionTrigger Trigger => m_trigger;
        public EWorldElementKind ElementKind => m_elementKind;
        public ETerrainElementStateKind ExpiredStateKind => m_expiredStateKind;
        public float MinimumIntensity => m_minimumIntensity;
        public int Priority => m_priority;
        public IReadOnlyList<ElementReactionOperation> Operations => m_operations;

        public bool Matches(in ElementReactionContext context)
        {
            if (m_trigger != context.Trigger)
            {
                return false;
            }

            if (m_trigger == EElementReactionTrigger.OnElementApplied &&
                (m_elementKind != context.Application.ElementKind ||
                 context.Application.Intensity < m_minimumIntensity))
            {
                return false;
            }

            if (m_trigger == EElementReactionTrigger.OnStateExpired &&
                m_expiredStateKind != context.ExpiredStateKind)
            {
                return false;
            }

            if (m_requireBaseSurface && m_baseSurface != context.BaseSurface ||
                m_requireEffectiveSurface && m_effectiveSurface != context.EffectiveSurface)
            {
                return false;
            }

            if (m_requireSurfaceCover && m_surfaceCover != context.SurfaceCover ||
                (context.SurfaceCoverTraits & m_requiredSurfaceCoverTraits) !=
                m_requiredSurfaceCoverTraits ||
                (context.SurfaceCoverTraits & m_forbiddenSurfaceCoverTraits) != 0)
            {
                return false;
            }

            if ((context.RuntimeStates & m_requiredStates) != m_requiredStates ||
                (context.RuntimeStates & m_forbiddenStates) != 0)
            {
                return false;
            }

            return true;
        }

        public bool TryValidate(out string error)
        {
            if (m_trigger == EElementReactionTrigger.OnElementApplied &&
                m_elementKind == EWorldElementKind.None)
            {
                error = "元素施加规则必须配置元素类型。";
                return false;
            }

            if (m_trigger == EElementReactionTrigger.OnStateExpired &&
                m_expiredStateKind == ETerrainElementStateKind.None)
            {
                error = "状态到期规则必须配置到期状态类型。";
                return false;
            }

            if ((m_requiredStates & m_forbiddenStates) != 0)
            {
                error = "同一种运行时状态不能同时出现在 Required 和 Forbidden 条件中。";
                return false;
            }

            if ((m_requiredSurfaceCoverTraits & m_forbiddenSurfaceCoverTraits) != 0)
            {
                error = "同一种上层地表属性不能同时出现在 Required 和 Forbidden 条件中。";
                return false;
            }

            if (m_operations == null || m_operations.Length == 0)
            {
                error = "反应规则必须至少包含一个结果操作。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 元素反应匹配时使用的只读上下文。
    /// 它把元素输入、基础地表、运行时地表和上层覆盖打包，避免规则直接读取 Tilemap 或场景对象。
    /// </summary>
    public readonly struct ElementReactionContext
    {
        public ElementReactionContext(
            EElementReactionTrigger trigger,
            in ElementApplication application,
            ETerrainElementStateKind expiredStateKind,
            ETerrainSurfaceKind baseSurface,
            ETerrainSurfaceKind effectiveSurface,
            ETerrainSurfaceCoverKind surfaceCover,
            ETerrainSurfaceCoverTraits surfaceCoverTraits,
            ETerrainRuntimeSurfaceState runtimeStates)
        {
            Trigger = trigger;
            Application = application;
            ExpiredStateKind = expiredStateKind;
            BaseSurface = baseSurface;
            EffectiveSurface = effectiveSurface;
            SurfaceCover = surfaceCover;
            SurfaceCoverTraits = surfaceCoverTraits;
            RuntimeStates = runtimeStates;
        }

        public ElementReactionContext(
            EElementReactionTrigger trigger,
            in ElementApplication application,
            ETerrainElementStateKind expiredStateKind,
            ETerrainSurfaceKind baseSurface,
            ETerrainSurfaceKind effectiveSurface,
            ETerrainRuntimeSurfaceState runtimeStates)
            : this(
                trigger,
                application,
                expiredStateKind,
                baseSurface,
                effectiveSurface,
                ETerrainSurfaceCoverKind.None,
                ETerrainSurfaceCoverTraits.None,
                runtimeStates)
        {
        }

        public EElementReactionTrigger Trigger { get; }
        public ElementApplication Application { get; }
        public ETerrainElementStateKind ExpiredStateKind { get; }
        public ETerrainSurfaceKind BaseSurface { get; }
        public ETerrainSurfaceKind EffectiveSurface { get; }
        public ETerrainSurfaceCoverKind SurfaceCover { get; }
        public ETerrainSurfaceCoverTraits SurfaceCoverTraits { get; }
        public ETerrainRuntimeSurfaceState RuntimeStates { get; }
    }
}
