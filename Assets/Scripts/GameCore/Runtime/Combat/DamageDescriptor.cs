using System;
using System.Collections.Generic;
using GAS.Runtime;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 一次伤害结算后的命中标记。
    /// Critical 只会出现在命中后的伤害结果；Miss 表示本次攻击没有造成伤害。
    /// </summary>
    [Flags]
    internal enum EDamageFlag
    {
        [HideInInspector] None = 0,
        Critical = 1 << 0,
        Miss = 1 << 1,
        [HideInInspector] All = ~None
    }

    /// <summary>
    /// 对默认暴击/闪避等随机解析行为的覆盖方式。
    /// </summary>
    internal enum EResolutionBehavior
    {
        Default,
        Always,
        Never
    }

    /// <summary>
    /// 一次伤害结算使用的权威掷值；调用方负责从对局或战斗的确定性随机上下文生成。
    /// </summary>
    internal readonly struct DamageResolutionRolls
    {
        internal DamageResolutionRolls(float criticalRollPercent, float hitRollPercent)
        {
            CriticalRollPercent = RequirePercent(criticalRollPercent, nameof(criticalRollPercent));
            HitRollPercent = RequirePercent(hitRollPercent, nameof(hitRollPercent));
        }

        public float CriticalRollPercent { get; }
        public float HitRollPercent { get; }

        private static float RequirePercent(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f || value >= 100.0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "伤害掷值必须位于 [0, 100) 区间。");
            }

            return value;
        }
    }

    /// <summary>
    /// 伤害类型。
    /// 当前只区分物理和魔法，后续抗性或元素伤害应在正式战斗规则中扩展。
    /// </summary>
    public enum EDamageType
    {
        None,
        Physical,
        Magical
    }

    /// <summary>
    /// 一次伤害在战斗语义匹配后的表现结果。
    /// 它只描述本次结算是否有优势 / 劣势反馈，不定义内容类型；内容语义仍由 EX-GAS 标签配置。
    /// </summary>
    public enum DamageMatchupResult
    {
        None = 0,
        Advantage = 1,
        Disadvantage = 2
    }

    /// <summary>
    /// FormalDamage 中声明的一条来源标签与目标标签倍率规则。
    /// 来源和目标都查询唯一 ASC 当前标签，不在战斗系统里复制 combat type。
    /// </summary>
    internal readonly struct DamageMatchupRule
    {
        internal DamageMatchupRule(
            int sourceRequiredTag,
            int targetRequiredTag,
            float multiplier,
            DamageMatchupResult result)
        {
            if (sourceRequiredTag <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceRequiredTag),
                    sourceRequiredTag,
                    "伤害克制规则的来源标签必须是有效 EX-GAS 标签码。");
            }
            if (targetRequiredTag <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetRequiredTag),
                    targetRequiredTag,
                    "伤害克制规则的目标标签必须是有效 EX-GAS 标签码。");
            }
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    multiplier,
                    "伤害克制倍率必须是非负有限数值。");
            }

            SourceRequiredTag = sourceRequiredTag;
            TargetRequiredTag = targetRequiredTag;
            Multiplier = multiplier;
            Result = result;
        }

        internal int SourceRequiredTag { get; }
        internal int TargetRequiredTag { get; }
        internal float Multiplier { get; }
        internal DamageMatchupResult Result { get; }
    }

    /// <summary>
    /// 一次伤害匹配到的克制倍率。
    /// </summary>
    internal readonly struct DamageMatchupEvaluation
    {
        internal DamageMatchupEvaluation(float multiplier, DamageMatchupResult result)
        {
            Multiplier = multiplier;
            Result = result;
            HasMatch = true;
        }

        internal bool HasMatch { get; }
        internal float Multiplier { get; }
        internal DamageMatchupResult Result { get; }
    }

    /// <summary>
    /// 伤害来源合同。
    /// 结算系统通过它读取攻击者实例和命中当刻的战斗属性快照，避免直接依赖具体角色字段。
    /// </summary>
    internal interface IDamageSource
    {
        public bool TryResolveCharacter(out CharacterBase character);
        public bool TryResolveAbilitySystem(out AbilitySystemCell abilitySystem);
        public bool TryGetCombatStatSnapshot(out CombatStatSnapshot snapshot);
    }

    /// <summary>
    /// 未知或环境伤害来源。
    /// 它明确表示无法解析攻击者，也没有可用于缩放的战斗属性快照。
    /// </summary>
    internal struct UnknownDamageSource : IDamageSource
    {
        public bool TryResolveCharacter(out CharacterBase character)
        {
            character = null;
            return false;
        }

        public bool TryResolveAbilitySystem(out AbilitySystemCell abilitySystem)
        {
            abilitySystem = null;
            return false;
        }

        public bool TryGetCombatStatSnapshot(out CombatStatSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
    }

    /// <summary>
    /// 没有场景角色表现对象的 ASC 伤害来源；只保留命中时的 GAS 战斗属性快照。
    /// </summary>
    internal readonly struct AbilitySystemDamageSource : IDamageSource
    {
        private readonly AbilitySystemCell m_abilitySystem;
        private readonly CombatStatSnapshot m_combatStats;

        internal AbilitySystemDamageSource(AbilitySystemCell abilitySystem)
        {
            m_abilitySystem = abilitySystem ?? throw new ArgumentNullException(nameof(abilitySystem));
            m_combatStats = DamageSolver.CreateCombatStatSnapshot(abilitySystem);
        }

        public bool TryResolveCharacter(out CharacterBase character)
        {
            character = null;
            return false;
        }

        public bool TryResolveAbilitySystem(out AbilitySystemCell abilitySystem)
        {
            abilitySystem = m_abilitySystem;
            return abilitySystem != null;
        }

        public bool TryGetCombatStatSnapshot(out CombatStatSnapshot snapshot)
        {
            snapshot = m_combatStats;
            return true;
        }
    }

    /// <summary>
    /// 来自角色的一次伤害来源。
    /// 它在创建时缓存战斗属性快照，避免命中延迟期间装备或状态变化污染本次结算。
    /// </summary>
    [Serializable]
    internal struct CharacterDamageSource : IDamageSource
    {
        internal static CharacterDamageSource Create(CharacterBase character)
        {
            CharacterDamageSource source = new();
            source.m_character = character;
            source.m_combatStats = character != null ? character.CreateCombatStatSnapshot() : default;
            return source;
        }

        // 攻击者引用用于仇恨、击退、自伤判定等需要真实角色实例的后续逻辑。
        [SerializeField] private PersistableReference<CharacterBase> m_character;

        // 我们继续保留“攻击发起那一刻”的已缓存战斗属性，
        // 只缓存命中结算实际需要的 EX-GAS 属性值，不保存另一份角色属性容器。
        [SerializeField] private CombatStatSnapshot m_combatStats;

        public bool TryResolveCharacter(out CharacterBase resolvedCharacter)
        {
            resolvedCharacter = m_character.ResolveOrNull();
            return resolvedCharacter != null;
        }

        public bool TryResolveAbilitySystem(out AbilitySystemCell abilitySystem)
        {
            abilitySystem = null;
            if (!TryResolveCharacter(out CharacterBase character) ||
                !character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent))
            {
                return false;
            }

            abilitySystem = abilitySystemComponent.Cell;
            return abilitySystem != null;
        }

        public bool TryGetCombatStatSnapshot(out CombatStatSnapshot snapshot)
        {
            snapshot = m_combatStats;
            return true;
        }
    }

    /// <summary>
    /// 技能或效果配置阶段的原始伤害描述。
    /// 它还没经过攻击者属性、暴击、闪避、防御等正式结算。
    /// </summary>
    internal readonly struct DamageDescriptor
    {
        internal DamageDescriptor(
            EDamageType damageType,
            int flatDamages,
            float scalingFactor,
            bool ignoreDefense,
            IReadOnlyList<DamageMatchupRule> matchupRules = null)
        {
            if (flatDamages < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flatDamages),
                    flatDamages,
                    "正式伤害的固定值不能为负数。");
            }

            if (float.IsNaN(scalingFactor) || float.IsInfinity(scalingFactor) || scalingFactor < 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scalingFactor),
                    scalingFactor,
                    "正式伤害的属性缩放必须是非负有限数值。");
            }

            DamageType = damageType;
            ScalingFactor = scalingFactor;
            FlatDamages = flatDamages;
            CriticalBehavior = EResolutionBehavior.Default;
            MissBehavior = EResolutionBehavior.Default;
            IgnoreDefense = ignoreDefense;
            Silent = false;
            MatchupRules = matchupRules == null
                ? Array.Empty<DamageMatchupRule>()
                : new List<DamageMatchupRule>(matchupRules).ToArray();
        }

        internal EDamageType DamageType { get; }
        internal float ScalingFactor { get; }
        internal int FlatDamages { get; }
        internal EResolutionBehavior CriticalBehavior { get; }
        internal EResolutionBehavior MissBehavior { get; }
        internal bool IgnoreDefense { get; }
        internal bool Silent { get; }
        internal IReadOnlyList<DamageMatchupRule> MatchupRules { get; }
    }

    /// <summary>
    /// 攻击者侧结算后的输出伤害。
    /// 这里已经包含攻击、暴击和命中标记，但还没经过目标防御减免。
    /// </summary>
    internal struct DamageOutputDescriptor
    {
        internal IDamageSource source;
        internal EDamageType type;
        internal int damage;
        internal EDamageFlag flags;
        internal DamageResolutionRolls rolls;
        internal EResolutionBehavior criticalBehavior;
        internal EResolutionBehavior missBehavior;
        internal IReadOnlyList<DamageMatchupRule> matchupRules;
        internal bool ignoreDefense;
        internal bool silent;

        internal bool TryGetSourceCharacter(out CharacterBase character)
        {
            character = null;
            return source != null && source.TryResolveCharacter(out character);
        }

        internal bool TryGetSourceCombatStatSnapshot(out CombatStatSnapshot snapshot)
        {
            snapshot = default;
            return source != null && source.TryGetCombatStatSnapshot(out snapshot);
        }
    }

    /// <summary>
    /// 目标侧减免后的输入伤害。
    /// 接收者用它播放受击、击退和飘字，同时保留来源以供反击或仇恨系统读取。
    /// </summary>
    internal struct DamageInputDescriptor
    {
        internal IDamageSource source;
        internal int damage;
        internal EDamageFlag flags;
        internal DamageMatchupResult matchupResult;
        internal bool silent;

        internal bool IsCriticalHit => flags.HasFlag(EDamageFlag.Critical);

        internal bool IsMissed => flags.HasFlag(EDamageFlag.Miss);

        internal bool IsRegularHit => flags == EDamageFlag.None;

        internal bool IsSilentAppliedHit => silent && !IsMissed;

        internal bool TryGetSourceCharacter(out CharacterBase character)
        {
            character = null;
            return source != null && source.TryResolveCharacter(out character);
        }
    }
}
