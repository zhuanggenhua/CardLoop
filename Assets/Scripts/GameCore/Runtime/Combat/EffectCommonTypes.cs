using System;
using System.Linq;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单个效果与目标交互后的结果。
    /// 用于聚合多目标反馈，而不是直接代表整个技能成功或失败。
    /// </summary>
    public enum EEffectInteractionResult
    {
        NotApplicable,
        ApplyFailed,
        ApplySucceeded,
        Consumed
    }

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
    /// 通用效果冲击参数。
    /// 它把冲击向量解释方式和伤害受击手感配置打包，供不同效果复用。
    /// </summary>
    [Serializable]
    public struct EffectImpactSettings
    {
        public EEffectImpactDataType impactDataType;
        public Vector2 impactData;
        public DamageImpactSettings damageImpact;
    }

    /// <summary>
    /// 一次效果应用的聚合结果。
    /// 保存交互结果快照和实际受影响目标，避免外部修改内部数组。
    /// </summary>
    public readonly struct EffectApplicationResult
    {
        private readonly EEffectInteractionResult[] m_feed;
        private readonly CharacterBase[] m_affectedTargets;

        public int AffectedTargetCount => m_affectedTargets.Length;

        public EffectApplicationResult(EEffectInteractionResult[] feedSnapshot, CharacterBase[] affectedTargetSnapshot)
        {
            m_feed = feedSnapshot != null ? (EEffectInteractionResult[])feedSnapshot.Clone() : Array.Empty<EEffectInteractionResult>();
            m_affectedTargets = affectedTargetSnapshot != null ? affectedTargetSnapshot.Where(target => target != null).ToArray() : Array.Empty<CharacterBase>();
        }

        public bool HasAnyInteractionBeyond(EEffectInteractionResult interactionResult)
        {
            return Array.Exists(m_feed, item => item != interactionResult);
        }

        public EEffectInteractionResult[] CreateFeedSnapshot()
        {
            return (EEffectInteractionResult[])m_feed.Clone();
        }

        public CharacterBase[] CreateAffectedTargetsSnapshot()
        {
            return (CharacterBase[])m_affectedTargets.Clone();
        }
    }
}
