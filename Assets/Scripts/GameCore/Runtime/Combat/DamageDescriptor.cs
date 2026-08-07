using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 一次伤害结算后的命中标记。
    /// Critical 和 Miss 可以同时作为计算过程证据保留，最终表现由接收端解释。
    /// </summary>
    [Flags]
    public enum EDamageFlag
    {
        [HideInInspector] None = 0,
        Critical = 1 << 0,
        Miss = 1 << 1,
        [HideInInspector] All = ~None
    }

    /// <summary>
    /// 对默认暴击/闪避等随机解析行为的覆盖方式。
    /// </summary>
    public enum EResolutionBehavior
    {
        Default,
        Always,
        Never
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
    /// 伤害来源合同。
    /// 结算系统通过它读取攻击者实例和命中当刻的战斗属性快照，避免直接依赖具体角色字段。
    /// </summary>
    public interface IDamageSource
    {
        public bool TryResolveCharacter(out CharacterBase character);
        public bool TryGetCombatStatSnapshot(out CombatStatSnapshot snapshot);
    }

    /// <summary>
    /// 未知或环境伤害来源。
    /// 它明确表示无法解析攻击者，也没有可用于缩放的战斗属性快照。
    /// </summary>
    public struct UnknownDamageSource : IDamageSource
    {
        public bool TryResolveCharacter(out CharacterBase character)
        {
            character = null;
            return false;
        }

        public bool TryGetCombatStatSnapshot(out CombatStatSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
    }

    /// <summary>
    /// 来自角色的一次伤害来源。
    /// 它在创建时缓存战斗属性快照，避免命中延迟期间装备或状态变化污染本次结算。
    /// </summary>
    [Serializable]
    public struct CharacterDamageSource : IDamageSource
    {
        public static CharacterDamageSource Create(CharacterBase character)
        {
            CharacterDamageSource source = new();
            source.m_character = character;
            source.m_combatStats = character != null ? character.CreateCombatStatSnapshot() : default;
            return source;
        }

        // 攻击者引用用于仇恨、击退、自伤判定等需要真实角色实例的后续逻辑。
        [SerializeField] private PersistableReference<CharacterBase> m_character;

        // 我们继续保留“攻击发起那一刻”的已缓存战斗属性，
        // 但不再把整份 Stats 都塞进伤害来源，只缓存正式命中结算实际需要的最小集合。
        [SerializeField] private CombatStatSnapshot m_combatStats;

        public bool TryResolveCharacter(out CharacterBase resolvedCharacter)
        {
            resolvedCharacter = m_character.ResolveOrNull();
            return resolvedCharacter != null;
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
    [Serializable]
    public struct DamageDescriptor
    {
        public EDamageType damageType;
        [Min(0)] public float scalingFactor;
        [Min(0)] public int flatDamages;
        public EResolutionBehavior criticalBehavior;
        public EResolutionBehavior missBehavior;
        public bool ignoreDefense;
        public bool silent;
    }

    /// <summary>
    /// 攻击者侧结算后的输出伤害。
    /// 这里已经包含攻击、暴击和命中标记，但还没经过目标防御减免。
    /// </summary>
    [Serializable]
    public struct DamageOutputDescriptor
    {
        [SerializeReference] public IDamageSource source;
        public EDamageType type;
        public int damage;
        public EDamageFlag flags;
        public EResolutionBehavior missBehavior;
        public bool ignoreDefense;
        public bool silent;

        public bool TryGetSourceCharacter(out CharacterBase character)
        {
            character = null;
            return source != null && source.TryResolveCharacter(out character);
        }

        public bool TryGetSourceCombatStatSnapshot(out CombatStatSnapshot snapshot)
        {
            snapshot = default;
            return source != null && source.TryGetCombatStatSnapshot(out snapshot);
        }
    }

    /// <summary>
    /// 目标侧减免后的输入伤害。
    /// 接收者用它播放受击、击退和飘字，同时保留来源以供反击或仇恨系统读取。
    /// </summary>
    public struct DamageInputDescriptor
    {
        [SerializeReference] public IDamageSource source;
        public int damage;
        public EDamageFlag flags;
        public bool silent;

        public bool IsCriticalHit => flags.HasFlag(EDamageFlag.Critical);

        public bool IsMissed => flags.HasFlag(EDamageFlag.Miss);

        public bool IsRegularHit => flags == EDamageFlag.None;

        public bool IsSilentAppliedHit => silent && !IsMissed;

        public bool TryGetSourceCharacter(out CharacterBase character)
        {
            character = null;
            return source != null && source.TryResolveCharacter(out character);
        }
    }
}
