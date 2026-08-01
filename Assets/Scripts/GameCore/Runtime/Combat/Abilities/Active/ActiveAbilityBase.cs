using System;
using GAS.Runtime;
using UnityEngine;
using UnityEngine.Events;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 主动能力启动检查结果。
    /// 用于输入层和 UI 区分冷却、资源不足、失能和未知配置错误。
    /// </summary>
    public enum EAbilityFireCheckResult
    {
        Valid,
        OnCooldown,
        NotEnoughMana,
        Incapacitated,
        Unknown
    }

    /// <summary>
    /// 可由角色主动触发的能力合同。
    /// 输入门、冷却和正式 EX-GAS 规则由实现类负责闭合。
    /// </summary>
    public interface ITriggerableAbility
    {
        public void Fire(
            GameCommandContext commandContext,
            AbilityActivationContext activationContext,
            UnityAction onAbilityEnded);
        public void StopFire();
        public EAbilityFireCheckResult CanFire();
        public bool abilityPermitted { get; }
        public void PermitAbility(bool abilityPermitted);
    }

    /// <summary>
    /// 主动能力基类。
    /// 它统一正式 EX-GAS 输入门、冷却、消耗提交和角色能力账本生命周期。
    /// </summary>
    public abstract class ActiveAbilityBase : AbilityBase, ITriggerableAbility
    {
        private UnityAction m_onAbilityEndedCallback = null;
        private FormalAbilityInputGateRuntime m_inputGate = null;
        private CharacterHandleWeapon m_characterHandleWeapon = null;
        private CharacterAbilitySet m_characterAbilitySet = null;
        private bool m_casting = false;
        private bool m_effectCostPaidForCurrentUse = false;
        private bool m_abilityPermitted = true;
        private GameCommandContext m_fireCommandContext = GameCommandContext.Script();
        private AbilityActivationContext m_fireActivationContext = null;

        public float remainingCooldown
        {
            get
            {
                GetCooldownState(out float remainingCooldownValue, out _);
                return remainingCooldownValue;
            }
        }

        public float cooldown
        {
            get
            {
                GetCooldownState(out _, out float cooldownDuration);
                return cooldownDuration;
            }
        }

        public bool abilityPermitted => m_abilityPermitted;
        public EFormalAbilityInputGateState inputGateState => m_inputGate?.state ?? EFormalAbilityInputGateState.Idle;

        protected GameCommandContext activeCommandContext => m_fireCommandContext;
        protected abstract void ExecuteAbilityUse();

        protected override void InitRuntime(CharacterBase character)
        {
            base.InitRuntime(character);
            m_characterAbilitySet = ResolveOwnedAbilitySet();
            m_fireCommandContext = GameCommandContext.ResolveForActor(character);
            InitializeFormalAbilityInputGate();
        }

        public void Fire(
            GameCommandContext commandContext,
            AbilityActivationContext activationContext,
            UnityAction onAbilityEnded)
        {
            m_onAbilityEndedCallback = onAbilityEnded;
            m_fireCommandContext = commandContext.HasActor
                ? commandContext
                : GameCommandContext.Recreate(commandContext.IssuerKind, m_character, commandContext.IssuerId);
            m_fireActivationContext = activationContext;
            m_inputGate.RequestUse();
        }

        public void StopFire()
        {
            m_inputGate?.ReleaseUse();
        }

        public void PermitAbility(bool abilityPermitted)
        {
            m_abilityPermitted = abilityPermitted;
        }

        internal void GetCooldownState(out float remainingCooldownValue, out float cooldownDuration)
        {
            if (TryGetFormalCooldownState(out remainingCooldownValue, out cooldownDuration))
            {
                return;
            }

            remainingCooldownValue = 0.0f;
            cooldownDuration = 0.0f;
        }

        private void InitializeFormalAbilityInputGate()
        {
            m_inputGate = new FormalAbilityInputGateRuntime(ResolveFormalAbilityInputGateSettings(), CanStartFormalAbilityInputGateSequence);
            m_inputGate.sequenceStarted += OnInputGateSequenceStarted;
            m_inputGate.usePerformed += OnInputGateUsePerformed;
            m_inputGate.sequenceStopped += OnInputGateSequenceStopped;
            m_inputGate.interrupted += OnInputGateInterrupted;
            m_inputGate.SetTimeScale(ResolveFormalAbilityInputGateSpeedMultiplier());
        }

        private void OnInputGateSequenceStarted()
        {
            OnInputGateSequenceStartedInternal();
            m_casting = true;
            m_effectCostPaidForCurrentUse = false;

            if (!usesFormalGasAbility)
            {
                Debug.LogError($"{GetAbilityDebugName()} 尚未绑定 EX-GAS Ability；主动能力无法启动。", this);
                m_inputGate?.Interrupt();
            }
        }

        private void OnInputGateUsePerformed()
        {
            if (usesFormalGasAbility)
            {
                if (!TryValidateAbilityUseAtFirePoint(out _))
                {
                    m_inputGate?.Interrupt();
                    return;
                }

                if (m_characterAbilitySet == null)
                {
                    m_inputGate?.Interrupt();
                    return;
                }

                if (!TryCreateActivationContext(out AbilityActivationContext activationContext))
                {
                    Debug.LogError($"{GetAbilityDebugName()} 无法激活：角色实例缺失，无法创建本次 GAS 激活上下文。", this);
                    m_inputGate?.Interrupt();
                    return;
                }

                if (!m_characterAbilitySet.BeginFormalGasAbilityRuleLifecycle(
                        formalGasAbilityCode,
                        activationContext))
                {
                    m_characterAbilitySet.CancelFormalGasAbilityRuleLifecycle(formalGasAbilityCode);
                    m_inputGate?.Interrupt();
                    return;
                }

                if (!TryCommitAbilityUse(out _))
                {
                    m_characterAbilitySet.CancelFormalGasAbilityRuleLifecycle(formalGasAbilityCode);
                    m_inputGate?.Interrupt();
                    return;
                }
            }
            else if (!TryCommitAbilityUse(out _))
            {
                m_inputGate?.Interrupt();
                return;
            }

            OnInputGateUsePerformedInternal();
            ExecuteAbilityUse();
        }

        private void OnInputGateSequenceStopped()
        {
            OnInputGateSequenceStoppedInternal();
            if (usesFormalGasAbility)
            {
                m_characterAbilitySet?.EndFormalGasAbilityRuleLifecycle(formalGasAbilityCode);
            }
            TerminateCasting();
        }

        private void OnInputGateInterrupted()
        {
            OnInputGateInterruptedInternal();
            if (usesFormalGasAbility)
            {
                m_characterAbilitySet?.CancelFormalGasAbilityRuleLifecycle(formalGasAbilityCode);
            }
        }

        public override void Reset()
        {
            base.Reset();
            if (m_character == null || !usesFormalGasAbility)
            {
                m_casting = false;
                m_effectCostPaidForCurrentUse = false;
                m_onAbilityEndedCallback = null;
                m_fireActivationContext = null;
                return;
            }

            m_characterAbilitySet = ResolveOwnedAbilitySet();
            if (usesFormalGasAbility)
            {
                m_characterAbilitySet?.CancelFormalGasAbilityRuleLifecycle(formalGasAbilityCode);
                m_characterAbilitySet?.ClearFormalGasAbilityRuleCooldown(formalGasAbilityCode);
            }
            m_fireCommandContext = GameCommandContext.ResolveForActor(m_character);
            m_fireActivationContext = null;
            InitializeFormalAbilityInputGate();
            m_casting = false;
            m_effectCostPaidForCurrentUse = false;
        }

        public override void Interrupt()
        {
            base.Interrupt();
            m_inputGate?.Interrupt();
        }

        public override void UpdateCooldowns(float deltaTime)
        {
            base.UpdateCooldowns(deltaTime);
            m_inputGate?.SetTimeScale(ResolveFormalAbilityInputGateSpeedMultiplier());

            m_inputGate?.Tick(deltaTime);
        }

        public virtual EAbilityFireCheckResult CanFire()
        {
            if (m_inputGate != null && m_inputGate.isBusy)
            {
                return EAbilityFireCheckResult.Valid;
            }

            return EvaluateAbilityStartRules();
        }

        private bool CanStartFormalAbilityInputGateSequence()
        {
            return EvaluateAbilityStartRules() == EAbilityFireCheckResult.Valid;
        }

        private EAbilityFireCheckResult EvaluateAbilityStartRules()
        {
            if (usesFormalGasAbility)
            {
                if (m_characterAbilitySet != null &&
                    m_characterAbilitySet.TryEvaluateFormalGasAbilityRuleActivation(
                        formalGasAbilityCode,
                        out EAbilityFireCheckResult formalGasResult,
                        out _))
                {
                    return formalGasResult;
                }

                Debug.LogError($"{GetAbilityDebugName()} 已绑定 EX-GAS Ability，但没有可用的正式规则绑定。", this);
                return EAbilityFireCheckResult.Unknown;
            }

            Debug.LogError($"{GetAbilityDebugName()} 尚未绑定 EX-GAS Ability；主动能力缺少启动规则。", this);
            return EAbilityFireCheckResult.Unknown;
        }

        private bool TryCommitAbilityUse(out EAbilityFireCheckResult result)
        {
            result = EAbilityFireCheckResult.Valid;
            if (m_effectCostPaidForCurrentUse)
            {
                return true;
            }

            if (usesFormalGasAbility)
            {
                if (m_characterAbilitySet != null &&
                    m_characterAbilitySet.TryCommitFormalGasAbilityRuleUse(formalGasAbilityCode, out result))
                {
                    if (result != EAbilityFireCheckResult.Valid)
                    {
                        return false;
                    }

                    m_effectCostPaidForCurrentUse = true;
                    return true;
                }

                result = EAbilityFireCheckResult.Unknown;
                Debug.LogError($"{GetAbilityDebugName()} 已绑定 EX-GAS Ability，但出手提交时没有可用的正式规则绑定。", this);
                return false;
            }

            result = EAbilityFireCheckResult.Unknown;
            Debug.LogError($"{GetAbilityDebugName()} 尚未绑定 EX-GAS Ability；主动能力无法提交消耗或冷却。", this);
            return false;
        }

        private bool TryValidateAbilityUseAtFirePoint(out EAbilityFireCheckResult result)
        {
            result = EAbilityFireCheckResult.Valid;
            if (m_effectCostPaidForCurrentUse)
            {
                return true;
            }

            if (usesFormalGasAbility)
            {
                if (m_characterAbilitySet != null &&
                    m_characterAbilitySet.TryValidateFormalGasAbilityRuleUseAtFirePoint(formalGasAbilityCode, out result))
                {
                    return result == EAbilityFireCheckResult.Valid;
                }

                result = EAbilityFireCheckResult.Unknown;
                Debug.LogError($"{GetAbilityDebugName()} 已绑定 EX-GAS Ability，但命中帧校验时没有可用的正式规则绑定。", this);
                return false;
            }

            result = EAbilityFireCheckResult.Unknown;
            Debug.LogError($"{GetAbilityDebugName()} 尚未绑定 EX-GAS Ability；主动能力无法执行命中帧校验。", this);
            return false;
        }

        protected void TerminateCasting()
        {
            m_fireActivationContext = null;
            if (!m_casting)
            {
                return;
            }

            m_casting = false;
            if (!usesFormalGasAbility)
            {
                Debug.LogError($"{GetAbilityDebugName()} 尚未绑定 EX-GAS Ability；主动能力无法执行施法结束动作恢复。", this);
            }
            m_onAbilityEndedCallback?.Invoke();
            m_onAbilityEndedCallback = null;
        }

        protected override void WriteFormalRuntimeState(CharacterAbilityRuntimeStateData runtimeState)
        {
            if (runtimeState == null)
            {
                return;
            }

            GetCooldownState(out float remainingCooldownValue, out _);
            runtimeState.remainingCooldownTimer = remainingCooldownValue;
        }

        protected override void ReadFormalRuntimeState(CharacterAbilityRuntimeStateData runtimeState)
        {
            if (runtimeState == null)
            {
                return;
            }

            float savedRemainingCooldown = runtimeState.remainingCooldownTimer;
            if (savedRemainingCooldown > 0.0f &&
                m_characterAbilitySet != null &&
                usesFormalGasAbility &&
                m_characterAbilitySet.TryApplyFormalGasAbilityRuleCooldown(formalGasAbilityCode, savedRemainingCooldown))
            {
                m_inputGate?.ResetTransientState();
                m_casting = false;
                m_effectCostPaidForCurrentUse = false;
                m_onAbilityEndedCallback = null;
                return;
            }

            if (usesFormalGasAbility)
            {
                m_characterAbilitySet?.ClearFormalGasAbilityRuleCooldown(formalGasAbilityCode);
            }

            m_inputGate?.ResetTransientState();
            m_casting = false;
            m_effectCostPaidForCurrentUse = false;
            m_onAbilityEndedCallback = null;
        }

        protected virtual void OnInputGateSequenceStartedInternal()
        {
        }

        protected virtual void OnInputGateUsePerformedInternal()
        {
        }

        protected virtual void OnInputGateSequenceStoppedInternal()
        {
        }

        protected virtual void OnInputGateInterruptedInternal()
        {
        }

        protected virtual float ResolveFormalAbilityInputGateSpeedMultiplier()
        {
            return 1.0f;
        }

        protected abstract FormalAbilityInputGateSettings ResolveFormalAbilityInputGateSettings();

        protected abstract GameplayFeedbackSet ResolveGameplayFeedbacks();

        private bool TryCreateActivationContext(out AbilityActivationContext activationContext)
        {
            activationContext = null;
            if (m_character == null)
            {
                return false;
            }

            activationContext = CreateFormalGasActivationContext();
            return activationContext != null;
        }

        protected virtual AbilityActivationContext CreateFormalGasActivationContext()
        {
            return m_fireActivationContext ??
                new AbilityActivationContext(m_character.transform.position);
        }

        internal bool ShouldUpdateLookAtDirectionOnFireForRuntime()
        {
            if (usesFormalGasAbility)
            {
                return TryGetFormalGasRuntimeConfig(out FormalGasAbilityRuntimeConfig config) &&
                    config.InputGate.updateLookAtDirectionOnFire;
            }

            return false;
        }

        internal bool ShouldLockTargetDirectionDuringInputGateForRuntime()
        {
            bool hasRequestedAimDirection = m_fireActivationContext != null &&
                m_fireActivationContext.TryGetAimDirection(out _);
            if (!ShouldUpdateLookAtDirectionOnFireForRuntime() && !hasRequestedAimDirection)
            {
                return false;
            }

            return inputGateState == EFormalAbilityInputGateState.Start ||
                inputGateState == EFormalAbilityInputGateState.DelayBeforeUse ||
                inputGateState == EFormalAbilityInputGateState.Use;
        }

        protected CharacterHandleWeapon ResolveCharacterHandleWeapon()
        {
            CharacterHandleWeapon handleWeapon = m_characterHandleWeapon;
            if (handleWeapon == null)
            {
                if (m_character != null)
                {
                    m_character.TryGetComponent(out handleWeapon);
                }

                m_characterHandleWeapon = handleWeapon;
            }

            return handleWeapon;
        }

        private bool TryGetFormalCooldownState(out float remainingCooldownValue, out float cooldownDuration)
        {
            remainingCooldownValue = 0.0f;
            cooldownDuration = 0.0f;
            return m_characterAbilitySet != null &&
                usesFormalGasAbility &&
                m_characterAbilitySet.TryGetFormalGasAbilityRuleCooldownState(
                    formalGasAbilityCode,
                    out remainingCooldownValue,
                    out cooldownDuration);
        }

        private CharacterAbilitySet ResolveOwnedAbilitySet()
        {
            if (m_character == null)
            {
                return null;
            }

            if (!m_character.TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return null;
            }

            return abilitySet;
        }

        private string GetAbilityDebugName()
        {
            return usesFormalGasAbility
                ? $"EX-GAS Ability {formalGasAbilityCode}"
                : name;
        }
    }
}

