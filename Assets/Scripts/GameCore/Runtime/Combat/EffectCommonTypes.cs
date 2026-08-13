using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 效果冲击数据的解释方式。
    /// SourcePosition 表示来源位置，Velocity 表示方向/速度向量。
    /// </summary>
    public enum EEffectImpactDataType
    {
        SourcePosition,
        Velocity
    }

    /// <summary>
    /// 伤害命中后的推力策略。
    /// Default 走全局命中规则，Disabled 禁用，Override 使用效果自带参数。
    /// </summary>
    public enum EDamagePushMode
    {
        Default,
        Disabled,
        Override
    }

    /// <summary>
    /// 伤害命中后的动作表现参数。
    /// 它只控制击退和短暂无敌等受击手感，伤害数值、阵营和命中结果仍由 RPG 战斗规则决定。
    /// </summary>
    [Serializable]
    public struct DamageImpactSettings
    {
        public EDamagePushMode pushMode;
        public float pushIntensity;
        public float pushResistance;
        public float invincibilityDuration;

        public float sanitizedPushIntensity => Mathf.Max(0.0f, pushIntensity);
        public float sanitizedPushResistance => Mathf.Max(0.0f, pushResistance);
        public float sanitizedInvincibilityDuration => Mathf.Max(0.0f, invincibilityDuration);
    }

    /// <summary>
    /// 伤害与资源结算后的表现屏蔽标记。
    /// 结算本身仍由 EX-GAS 效果和 GameCore 伤害链负责。
    /// </summary>
    [System.Flags]
    public enum EEffectVisualFlags
    {
        None,
        NoFloatingText = 1 << 0,
        NoCameraShake = 1 << 1,
        NoScreenFlash = 1 << 2,
        [HideInInspector] All = ~None
    }
}
