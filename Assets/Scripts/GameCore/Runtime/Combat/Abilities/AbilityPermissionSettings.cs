using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 能力触发时可选的移动阻断条件。
    /// 这里只记录 GameCore 已经拥有的正式状态，不复制 TopDown 的完整状态机。
    /// </summary>
    [Flags]
    public enum EAbilityMovementBlockers
    {
        None = 0,
        Pushed = 1 << 0,
        MovementForbidden = 1 << 1,
        MoveOrder = 1 << 2
    }

    /// <summary>
    /// 能力触发时可选的角色阻断条件。
    /// RPG 生命、存档和角色身份仍由 CharacterBase 负责。
    /// </summary>
    [Flags]
    public enum EAbilityConditionBlockers
    {
        None = 0,
        Dead = 1 << 0
    }

    /// <summary>
    /// GameCore 正式能力权限配置。
    /// 承接本项目能力许可、移动阻断、条件阻断和本地输入门控阻断；不引入第二套角色生命周期。
    /// </summary>
    [Serializable]
    public sealed class AbilityPermissionSettings
    {
        [SerializeField]
        [Tooltip("关闭后该能力实例不能触发，适合剧情、装备或技能树临时禁用能力。")]
        private bool m_permittedByDefault = true;

        [SerializeField]
        [Tooltip("触发能力前必须允许的角色动作。默认要求 UseAbility，仍使用 GameCore 的动作锁作为正式真相。")]
        private EActionFlags m_requiredActions = EActionFlags.UseAbility;

        [SerializeField]
        [Tooltip("触发能力前会阻断的角色状态。默认死亡时不能触发。")]
        private EAbilityConditionBlockers m_blockingConditionStates = EAbilityConditionBlockers.Dead;

        [SerializeField]
        [Tooltip("触发能力前会阻断的移动状态，例如被击退中、普通移动被禁止或正在执行 MoveTo。")]
        private EAbilityMovementBlockers m_blockingMovementStates = EAbilityMovementBlockers.None;

        [SerializeField]
        [Tooltip("其它能力处于这些本地输入门控状态时，本能力不能触发。默认避免多个能力同时进入出手段。")]
        private EFormalAbilityInputGateState[] m_blockingOtherInputGateStates =
        {
            EFormalAbilityInputGateState.Start,
            EFormalAbilityInputGateState.DelayBeforeUse,
            EFormalAbilityInputGateState.Use,
            EFormalAbilityInputGateState.DelayBetweenUses
        };

        public bool permittedByDefault => m_permittedByDefault;
        public EActionFlags requiredActions => m_requiredActions;
        public EAbilityConditionBlockers blockingConditionStates => m_blockingConditionStates;
        public EAbilityMovementBlockers blockingMovementStates => m_blockingMovementStates;
        public int blockingOtherInputGateStateCount => m_blockingOtherInputGateStates?.Length ?? 0;
        public EFormalAbilityInputGateState[] GetBlockingOtherInputGateStates() => m_blockingOtherInputGateStates != null ? (EFormalAbilityInputGateState[])m_blockingOtherInputGateStates.Clone() : Array.Empty<EFormalAbilityInputGateState>();

        /// <summary>
        /// 统一判断能力是否允许触发。失败统一映射到 Incapacitated，避免旧 HUD 资产必须立即补所有新文案。
        /// </summary>
        public EAbilityFireCheckResult Evaluate(CharacterBase character, ActiveAbilityBase ability)
        {
            if (character == null || ability == null)
            {
                return EAbilityFireCheckResult.Unknown;
            }

            if (!m_permittedByDefault || !ability.abilityPermitted)
            {
                return EAbilityFireCheckResult.Incapacitated;
            }

            if (m_requiredActions != EActionFlags.None && !character.Can(m_requiredActions))
            {
                return EAbilityFireCheckResult.Incapacitated;
            }

            if (m_blockingConditionStates.HasFlag(EAbilityConditionBlockers.Dead) && character.dead)
            {
                return EAbilityFireCheckResult.Incapacitated;
            }

            if (IsMovementBlocked(character))
            {
                return EAbilityFireCheckResult.Incapacitated;
            }

            if (character.HasOtherAbilityInInputGateState(ability, m_blockingOtherInputGateStates))
            {
                return EAbilityFireCheckResult.Incapacitated;
            }

            return EAbilityFireCheckResult.Valid;
        }

        private bool IsMovementBlocked(CharacterBase character)
        {
            return
                m_blockingMovementStates.HasFlag(EAbilityMovementBlockers.Pushed) && character.IsPushed() ||
                m_blockingMovementStates.HasFlag(EAbilityMovementBlockers.MovementForbidden) && character.IsMovementForbidden() ||
                m_blockingMovementStates.HasFlag(EAbilityMovementBlockers.MoveOrder) && character.HasMoveOrder();
        }
    }
}

