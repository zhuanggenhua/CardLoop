using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 能施加到世界地表上的元素类型。
    /// 该枚举描述规则语义，不等于 GAS 技能编号或视觉特效名称。
    /// </summary>
    public enum EWorldElementKind
    {
        None,
        Fire,
        Water,
        Electricity,
        Oil
    }

    /// <summary>
    /// 一次元素施加的空间范围。
    /// Cone 需要方向，Circle 和 Point 不依赖方向。
    /// </summary>
    public enum EElementAreaKind
    {
        Point,
        Circle,
        Cone
    }

    /// <summary>
    /// 地表上可随时间变化的运行时状态。
    /// 状态存在于地图实例中，不会改写作者规则 Tile 资产。
    /// </summary>
    public enum ETerrainElementStateKind
    {
        None,
        Wet,
        Burning,
        Oiled,
        Electrified
    }

    /// <summary>
    /// 同一种运行时状态重复施加时的合并策略。
    /// </summary>
    public enum ETerrainStateMergePolicy
    {
        RefreshDuration,
        KeepStronger,
        StackIntensity,
        Reject
    }

    /// <summary>
    /// 运行时地表变化是否需要进入后续持久化链路。
    /// 当前首期主要使用瞬时状态，持久化仍由世界地形变更流程收口。
    /// </summary>
    public enum ETerrainRuntimePersistencePolicy
    {
        Transient,
        Persistent
    }

    /// <summary>
    /// 元素反应规则的触发时机。
    /// </summary>
    public enum EElementReactionTrigger
    {
        OnElementApplied,
        OnStateExpired
    }

    /// <summary>
    /// 元素反应命中后允许执行的结果操作类型。
    /// </summary>
    public enum EElementReactionOperationKind
    {
        AddOrRefreshState,
        RemoveState,
        SetEffectiveSurface,
        ClearEffectiveSurface,
        EmitPresentationSignal,
        RemoveSurfaceCover,
        SetSurfaceCover,
        ClearSurfaceCoverOverride
    }

    /// <summary>
    /// 不改变规则状态的一次性表现信号。
    /// </summary>
    public enum EElementPresentationSignal
    {
        None,
        Steam
    }

    /// <summary>
    /// 元素施加范围的值对象。
    /// 它不持有场景引用，便于 GAS、测试和地图系统复用同一套规则输入。
    /// </summary>
    [Serializable]
    public readonly struct ElementArea
    {
        public ElementArea(EElementAreaKind kind, float radius, float coneHalfAngleDegrees)
        {
            Kind = kind;
            Radius = radius;
            ConeHalfAngleDegrees = coneHalfAngleDegrees;
        }

        public EElementAreaKind Kind { get; }
        public float Radius { get; }
        public float ConeHalfAngleDegrees { get; }

        public bool IsValid =>
            Kind == EElementAreaKind.Point ||
            Radius > 0.0f &&
            (Kind != EElementAreaKind.Cone ||
             ConeHalfAngleDegrees > 0.0f && ConeHalfAngleDegrees <= 180.0f);

        public static ElementArea Point() => new(EElementAreaKind.Point, 0.0f, 0.0f);

        public static ElementArea Circle(float radius) =>
            new(EElementAreaKind.Circle, radius, 0.0f);

        public static ElementArea Cone(float range, float halfAngleDegrees) =>
            new(EElementAreaKind.Cone, range, halfAngleDegrees);
    }

    /// <summary>
    /// 一次世界元素施加的不可变输入。它只描述来源与空间范围，不包含任何地表反应结果。
    /// </summary>
    public readonly struct ElementApplication
    {
        public ElementApplication(
            EWorldElementKind elementKind,
            float intensity,
            float exposureDuration,
            ElementArea area,
            Vector2 origin,
            Vector2 direction,
            UnityEngine.Object sourceEntity = null,
            int sourceAbilityCode = 0)
        {
            ElementKind = elementKind;
            Intensity = Mathf.Clamp01(intensity);
            ExposureDuration = Mathf.Max(0.0f, exposureDuration);
            Area = area;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.zero;
            SourceEntity = sourceEntity;
            SourceAbilityCode = sourceAbilityCode;
        }

        public EWorldElementKind ElementKind { get; }
        public float Intensity { get; }
        public float ExposureDuration { get; }
        public ElementArea Area { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public UnityEngine.Object SourceEntity { get; }
        public int SourceAbilityCode { get; }

        public bool IsValid =>
            ElementKind != EWorldElementKind.None &&
            Intensity > 0.0f &&
            Area.IsValid &&
            (Area.Kind != EElementAreaKind.Cone || Direction.sqrMagnitude > 0.0f);
    }

    /// <summary>
    /// 地表状态来源标识。
    /// 用于诊断和后续持久化归因，不参与反应规则排序。
    /// </summary>
    public readonly struct TerrainElementStateSource
    {
        public TerrainElementStateSource(UnityEngine.Object sourceEntity, int sourceAbilityCode)
        {
            SourceEntity = sourceEntity;
            SourceAbilityCode = sourceAbilityCode;
        }

        public UnityEngine.Object SourceEntity { get; }
        public int SourceAbilityCode { get; }
    }

    /// <summary>
    /// 地表状态枚举到位标记的转换入口。
    /// 集中放在这里，避免各运行时系统各自维护一份映射。
    /// </summary>
    public static class TerrainElementStateKindExtensions
    {
        public static ETerrainRuntimeSurfaceState ToRuntimeFlag(this ETerrainElementStateKind stateKind)
        {
            return stateKind switch
            {
                ETerrainElementStateKind.Wet => ETerrainRuntimeSurfaceState.Wet,
                ETerrainElementStateKind.Burning => ETerrainRuntimeSurfaceState.Burning,
                ETerrainElementStateKind.Oiled => ETerrainRuntimeSurfaceState.Oiled,
                ETerrainElementStateKind.Electrified => ETerrainRuntimeSurfaceState.Electrified,
                _ => ETerrainRuntimeSurfaceState.None
            };
        }
    }
}
