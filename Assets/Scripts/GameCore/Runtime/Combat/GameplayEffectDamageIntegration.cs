using GAS.Runtime;
using Unity.Entities;
using UnityEngine;
using UEntity = Unity.Entities.Entity;

namespace GameCore
{
    /// <summary>
    /// 正式伤害组件的条件类型。
    /// 当前只定义背刺，后续新增条件应继续保持可数据化而不是写死在技能脚本中。
    /// </summary>
    internal enum EDamageConditionKind
    {
        None = 0,
        Backstab = 1
    }

    /// <summary>
    /// EX-GAS 表格转换到 GameCore 伤害系统的内部执行数据。
    /// 它不是内容作者入口，作者只维护 EX-GAS 的正式效果配置。
    /// </summary>
    internal readonly struct GameplayEffectDamagePayload
    {
        private readonly DamageDescriptor m_damageDescriptor;
        private readonly EEffectVisualFlags m_visualFlags;
        private readonly DamageImpactSettings m_damageImpact;
        private readonly EEffectImpactDataType m_impactDataType;
        private readonly Vector2 m_impactData;

        internal GameplayEffectDamagePayload(
            DamageDescriptor damageDescriptor,
            EEffectVisualFlags visualFlags,
            DamageImpactSettings damageImpact,
            EEffectImpactDataType impactDataType,
            Vector2 impactData)
        {
            m_damageDescriptor = damageDescriptor;
            m_visualFlags = visualFlags;
            m_damageImpact = damageImpact;
            m_impactDataType = impactDataType;
            m_impactData = impactData;
        }

        internal DamageDescriptor damageDescriptor => m_damageDescriptor;
        internal EEffectVisualFlags visualFlags => m_visualFlags;
        internal DamageImpactSettings damageImpact => m_damageImpact;
        internal EEffectImpactDataType impactDataType => m_impactDataType;
        internal Vector2 impactData => m_impactData;
        internal bool isConfigured => m_damageDescriptor.FlatDamages > 0 || Mathf.Abs(m_damageDescriptor.ScalingFactor) > 0.0001f;

        internal bool TryGenerateDescription(out AbilityDescriptionLine description)
        {
            description = default;
            if (!isConfigured)
            {
                return false;
            }

            string flatDamage = m_damageDescriptor.FlatDamages != 0
                ? $"{m_damageDescriptor.FlatDamages:0.#} {GameConfig.GetSafeTermDefinition("flat_damage").shortName}"
                : string.Empty;
            string scaledDamage = m_damageDescriptor.ScalingFactor != 0.0f
                ? $"{m_damageDescriptor.ScalingFactor:0.#} {GameConfig.GetSafeTermDefinition("scaled_damage").shortName}"
                : string.Empty;

            description = new AbilityDescriptionLine
            {
                header = GameConfig.GetSafeTermDefinition("remove_health").shortName,
                content = $"{flatDamage}{(string.IsNullOrEmpty(flatDamage) || string.IsNullOrEmpty(scaledDamage) ? string.Empty : "+")}{scaledDamage} {GameConfig.GetSafeTermDefinition(m_damageDescriptor.DamageType).shortName}"
            };
            return true;
        }
    }

    /// <summary>
    /// EX-GAS 编辑器配置组件：把正式伤害载荷写入 GameplayEffect 实体。
    /// 该组件只负责把配置载荷装入 GE 实体，不承担实际扣血逻辑。
    /// </summary>
    internal sealed class MCConfGameplayEffectDamage : GameplayEffectComponentConfig
    {
        private readonly GameplayEffectDamagePayload m_payload;

        internal MCConfGameplayEffectDamage(GameplayEffectDamagePayload payload)
        {
            m_payload = payload;
        }

        public override void LoadToGameplayEffectEntity(UEntity ge)
        {
            EntityHelper.AddManagedComponent<MCGameplayEffectDamage>(ge);
            EntityHelper.SetManagedComponent(ge, new MCGameplayEffectDamage(m_payload));
        }
    }

    /// <summary>
    /// GameplayEffect 实体上的正式伤害组件数据。
    /// 运行时系统读取 Payload 后再进入 GameCore 的伤害结算链路。
    /// </summary>
    internal sealed class MCGameplayEffectDamage : IComponentData
    {
        public MCGameplayEffectDamage()
        {
            Payload = default;
        }

        internal MCGameplayEffectDamage(GameplayEffectDamagePayload payload)
        {
            Payload = payload;
        }

        internal GameplayEffectDamagePayload Payload;
    }

    /// <summary>
    /// 单次 GameplayEffect 应用的动态冲击数据。
    /// 它只描述本次命中的方向或来源位置，不复制表格中的伤害与受击参数。
    /// </summary>
    internal sealed class MCGameplayEffectImpactOverride : IComponentData
    {
        public MCGameplayEffectImpactOverride()
        {
        }

        internal MCGameplayEffectImpactOverride(
            EEffectImpactDataType impactDataType,
            Vector2 impactData)
        {
            ImpactDataType = impactDataType;
            ImpactData = impactData;
        }

        internal EEffectImpactDataType ImpactDataType { get; }
        internal Vector2 ImpactData { get; }
    }

    /// <summary>
    /// 条件伤害的命中前置条件。
    /// 例如背刺使用 facing dot 阈值判断攻击方向与目标朝向关系。
    /// </summary>
    internal readonly struct GameplayEffectDamageCondition
    {
        internal GameplayEffectDamageCondition(EDamageConditionKind kind, float facingDotThreshold)
        {
            Kind = kind;
            FacingDotThreshold = Mathf.Clamp(facingDotThreshold, -1.0f, 1.0f);
        }

        internal EDamageConditionKind Kind { get; }
        internal float FacingDotThreshold { get; }
        internal bool requiresCondition => Kind != EDamageConditionKind.None;
    }

    /// <summary>
    /// 条件伤害载荷。
    /// 它把条件和伤害本体绑定在同一个 GameplayEffect 组件里，方便 GAS 配置表读取。
    /// </summary>
    internal readonly struct GameplayEffectConditionalDamagePayload
    {
        internal GameplayEffectConditionalDamagePayload(
            GameplayEffectDamageCondition condition,
            GameplayEffectDamagePayload damage)
        {
            Condition = condition;
            Damage = damage;
        }

        internal GameplayEffectDamageCondition Condition { get; }
        internal GameplayEffectDamagePayload Damage { get; }
        internal bool isConfigured => Damage.isConfigured && Condition.requiresCondition;
    }

    /// <summary>
    /// EX-GAS 编辑器配置组件：写入条件伤害组件。
    /// 条件判断仍由 GameCore 战斗系统执行，不在配置组件里即时结算。
    /// </summary>
    internal sealed class MCConfGameplayEffectConditionalDamage : GameplayEffectComponentConfig
    {
        private readonly GameplayEffectConditionalDamagePayload m_payload;

        internal MCConfGameplayEffectConditionalDamage(GameplayEffectConditionalDamagePayload payload)
        {
            m_payload = payload;
        }

        public override void LoadToGameplayEffectEntity(UEntity ge)
        {
            EntityHelper.AddManagedComponent<MCGameplayEffectConditionalDamage>(ge);
            EntityHelper.SetManagedComponent(ge, new MCGameplayEffectConditionalDamage(m_payload));
        }
    }

    /// <summary>
    /// GameplayEffect 实体上的条件伤害组件数据。
    /// </summary>
    internal sealed class MCGameplayEffectConditionalDamage : IComponentData
    {
        public MCGameplayEffectConditionalDamage()
        {
            Payload = default;
        }

        internal MCGameplayEffectConditionalDamage(GameplayEffectConditionalDamagePayload payload)
        {
            Payload = payload;
        }

        internal GameplayEffectConditionalDamagePayload Payload;
    }
}
