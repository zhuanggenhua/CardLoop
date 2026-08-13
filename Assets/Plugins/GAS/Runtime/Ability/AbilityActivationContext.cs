using System;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// 单次 Ability 激活的运行时上下文。
    /// 它与作者配置参数分离，避免激活时临时数据覆盖 AbilityLogic 的配置参数。
    /// 其中的瞄准方向表示激活请求的输入意图，不等同于技能任务执行时的最终姿态。
    /// </summary>
    public sealed class AbilityActivationContext
    {
        private const float DirectionEpsilon = 0.0001f;
        private readonly Vector3 _aimDirection;

        public Vector3 AimOrigin { get; }
        public bool HasAimDirection { get; }
        public AbilitySystemCell MainTarget { get; }
        public uint AuthoritativeRandomSeed { get; }
        public bool HasAuthoritativeRandomSeed => AuthoritativeRandomSeed != 0u;

        public AbilityActivationContext(
            Vector3 aimOrigin,
            AbilitySystemCell mainTarget = null,
            uint authoritativeRandomSeed = 0u)
        {
            AimOrigin = aimOrigin;
            MainTarget = mainTarget;
            AuthoritativeRandomSeed = authoritativeRandomSeed;
        }

        public AbilityActivationContext(
            Vector3 aimOrigin,
            Vector3 aimDirection,
            AbilitySystemCell mainTarget = null,
            uint authoritativeRandomSeed = 0u)
            : this(aimOrigin, mainTarget, authoritativeRandomSeed)
        {
            if (aimDirection.sqrMagnitude <= DirectionEpsilon)
            {
                throw new ArgumentException("Ability activation aim direction must be non-zero.", nameof(aimDirection));
            }

            _aimDirection = aimDirection.normalized;
            HasAimDirection = true;
        }

        public bool TryGetAimDirection(out Vector3 aimDirection)
        {
            aimDirection = _aimDirection;
            return HasAimDirection;
        }
    }
}
