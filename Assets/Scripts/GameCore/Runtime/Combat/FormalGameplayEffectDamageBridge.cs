using System;
using GAS.Runtime;
using Unity.Entities;
using UnityEngine;
using UEntity = Unity.Entities.Entity;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 正式伤害组件的条件类型。
    /// 当前只定义背刺，后续新增条件应继续保持可数据化而不是写死在技能脚本中。
    /// </summary>
    public enum EFormalDamageConditionKind
    {
        None = 0,
        Backstab = 1
    }

    /// <summary>
    /// EX-GAS GameplayEffect 中承载的正式伤害载荷。
    /// 这里是 GAS 配置到 GameCore 伤害系统之间的序列化边界。
    /// </summary>
    [Serializable]
    public struct FormalDamageEffectPayload
    {
        [InspectorName("伤害描述")]
        [Tooltip("正式伤害的类型、固定值和属性缩放。")]
        [SerializeField] private DamageDescriptor m_damageDescriptor;

        [InspectorName("表现标记")]
        [Tooltip("命中后允许触发的视觉/反馈类型。")]
        [SerializeField] private EEffectVisualFlags m_visualFlags;

        [InspectorName("打击参数")]
        [Tooltip("命中停顿、击退或其它打击感参数。")]
        [SerializeField] private DamageImpactSettings m_damageImpact;

        [InspectorName("冲击数据类型")]
        [Tooltip("说明 impactData 的解释方式，例如方向或世界坐标。")]
        [SerializeField] private EEffectImpactDataType m_impactDataType;

        [InspectorName("冲击数据")]
        [Tooltip("与冲击数据类型配套使用的二维参数。")]
        [SerializeField] private Vector2 m_impactData;

        public FormalDamageEffectPayload(
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

        public DamageDescriptor damageDescriptor => m_damageDescriptor;
        public EEffectVisualFlags visualFlags => m_visualFlags;
        public DamageImpactSettings damageImpact => m_damageImpact;
        public EEffectImpactDataType impactDataType => m_impactDataType;
        public Vector2 impactData => m_impactData;
        public bool isConfigured => m_damageDescriptor.flatDamages > 0 || Mathf.Abs(m_damageDescriptor.scalingFactor) > 0.0001f;

        public bool TryGenerateDescription(out EffectDescription description)
        {
            description = default;
            if (!isConfigured)
            {
                return false;
            }

            string flatDamage = m_damageDescriptor.flatDamages != 0
                ? $"{m_damageDescriptor.flatDamages:0.#} {GameConfig.GetSafeTermDefinition("flat_damage").shortName}"
                : string.Empty;
            string scaledDamage = m_damageDescriptor.scalingFactor != 0.0f
                ? $"{m_damageDescriptor.scalingFactor:0.#} {GameConfig.GetSafeTermDefinition("scaled_damage").shortName}"
                : string.Empty;

            description = new EffectDescription
            {
                name = GameConfig.GetSafeTermDefinition("remove_health").shortName,
                details = $"{flatDamage}{(string.IsNullOrEmpty(flatDamage) || string.IsNullOrEmpty(scaledDamage) ? string.Empty : "+")}{scaledDamage} {GameConfig.GetSafeTermDefinition(m_damageDescriptor.damageType).shortName}"
            };
            return true;
        }
    }

    /// <summary>
    /// EX-GAS 编辑器配置组件：把正式伤害载荷写入 GameplayEffect 实体。
    /// 该类是插件扩展点的项目侧桥接，不承担实际扣血逻辑。
    /// </summary>
    public sealed class MCConfFormalDamageEffect : GameplayEffectComponentConfig
    {
        public FormalDamageEffectPayload payload;

        public override void LoadToGameplayEffectEntity(UEntity ge)
        {
            EntityHelper.AddManagedComponent<MCFormalDamageEffect>(ge);
            EntityHelper.SetManagedComponent(ge, new MCFormalDamageEffect(payload));
        }
    }

    /// <summary>
    /// GameplayEffect 实体上的正式伤害组件数据。
    /// 运行时系统读取 Payload 后再进入 GameCore 的伤害结算链路。
    /// </summary>
    public sealed class MCFormalDamageEffect : IComponentData
    {
        public MCFormalDamageEffect()
        {
            Payload = default;
        }

        public MCFormalDamageEffect(FormalDamageEffectPayload payload)
        {
            Payload = payload;
        }

        public FormalDamageEffectPayload Payload;
    }

    /// <summary>
    /// 条件伤害的命中前置条件。
    /// 例如背刺使用 facing dot 阈值判断攻击方向与目标朝向关系。
    /// </summary>
    [Serializable]
    public readonly struct FormalDamageConditionPayload
    {
        public FormalDamageConditionPayload(EFormalDamageConditionKind kind, float facingDotThreshold)
        {
            Kind = kind;
            FacingDotThreshold = Mathf.Clamp(facingDotThreshold, -1.0f, 1.0f);
        }

        public EFormalDamageConditionKind Kind { get; }
        public float FacingDotThreshold { get; }
        public bool requiresCondition => Kind != EFormalDamageConditionKind.None;
    }

    /// <summary>
    /// 条件伤害载荷。
    /// 它把条件和伤害本体绑定在同一个 GameplayEffect 组件里，方便 GAS 配置表读取。
    /// </summary>
    [Serializable]
    public readonly struct FormalConditionalDamageEffectPayload
    {
        public FormalConditionalDamageEffectPayload(
            FormalDamageConditionPayload condition,
            FormalDamageEffectPayload damage)
        {
            Condition = condition;
            Damage = damage;
        }

        public FormalDamageConditionPayload Condition { get; }
        public FormalDamageEffectPayload Damage { get; }
        public bool isConfigured => Damage.isConfigured && Condition.requiresCondition;
    }

    /// <summary>
    /// EX-GAS 编辑器配置组件：写入条件伤害组件。
    /// 条件判断仍由 GameCore 战斗系统执行，不在配置组件里即时结算。
    /// </summary>
    public sealed class MCConfFormalConditionalDamageEffect : GameplayEffectComponentConfig
    {
        public FormalConditionalDamageEffectPayload payload;

        public override void LoadToGameplayEffectEntity(UEntity ge)
        {
            EntityHelper.AddManagedComponent<MCFormalConditionalDamageEffect>(ge);
            EntityHelper.SetManagedComponent(ge, new MCFormalConditionalDamageEffect(payload));
        }
    }

    /// <summary>
    /// GameplayEffect 实体上的条件伤害组件数据。
    /// </summary>
    public sealed class MCFormalConditionalDamageEffect : IComponentData
    {
        public MCFormalConditionalDamageEffect()
        {
            Payload = default;
        }

        public MCFormalConditionalDamageEffect(FormalConditionalDamageEffectPayload payload)
        {
            Payload = payload;
        }

        public FormalConditionalDamageEffectPayload Payload;
    }
}
