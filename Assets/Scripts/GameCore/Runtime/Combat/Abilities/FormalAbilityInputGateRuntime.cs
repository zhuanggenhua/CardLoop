using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 正式 GAS 能力的本地输入门控状态机。
    /// 它只负责把本地按键、缓冲、按住释放和本地节奏转换成 EX-GAS Ability 激活请求；
    /// 命中、伤害、效果和表现不在这里结算。
    /// </summary>
    public sealed class FormalAbilityInputGateRuntime
    {
        private readonly FormalAbilityInputGateSettings m_settings;
        private readonly Func<bool> m_canStartUseSequence;

        private EFormalAbilityInputGateState m_state = EFormalAbilityInputGateState.Idle;
        private float m_stateTimer = 0.0f;
        private bool m_triggerHeld = false;
        private bool m_triggerReleasedSinceLastUse = true;
        private bool m_bufferedInput = false;
        private float m_bufferTimer = 0.0f;
        private float m_timeScale = 1.0f;

        public EFormalAbilityInputGateState state => m_state;
        public bool isBusy => m_state != EFormalAbilityInputGateState.Idle &&
            m_state != EFormalAbilityInputGateState.Stop &&
            m_state != EFormalAbilityInputGateState.Interrupted;

        public event Action<EFormalAbilityInputGateState> stateChanged;
        public event Action sequenceStarted;
        public event Action usePerformed;
        public event Action sequenceStopped;
        public event Action interrupted;

        public FormalAbilityInputGateRuntime(FormalAbilityInputGateSettings settings, Func<bool> canStartUseSequence = null)
        {
            m_settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_canStartUseSequence = canStartUseSequence;
        }

        public void SetTimeScale(float timeScale)
        {
            m_timeScale = Mathf.Max(0.05f, timeScale);
        }

        /// <summary>
        /// 处理按下攻击输入。返回 false 表示当前状态不接受这次请求。
        /// </summary>
        public bool RequestUse()
        {
            m_triggerHeld = true;

            if (m_settings.triggerMode == EFormalAbilityInputTriggerMode.HoldRelease)
            {
                if (m_state == EFormalAbilityInputGateState.Idle || m_state == EFormalAbilityInputGateState.Stop)
                {
                    return StartUseSequence();
                }

                return false;
            }

            if (m_settings.triggerMode == EFormalAbilityInputTriggerMode.SemiAuto && !m_triggerReleasedSinceLastUse)
            {
                TryBufferInput();
                return false;
            }

            if (m_state == EFormalAbilityInputGateState.Idle || m_state == EFormalAbilityInputGateState.Stop)
            {
                return StartUseSequence();
            }

            TryBufferInput();
            return m_bufferedInput;
        }

        /// <summary>
        /// 处理松开攻击输入。半自动武器依靠这个信号允许下一次开火。
        /// </summary>
        public void ReleaseUse()
        {
            m_triggerHeld = false;
            m_triggerReleasedSinceLastUse = true;

            if (m_state == EFormalAbilityInputGateState.Charging)
            {
                EnterUse();
                return;
            }

            if (m_state == EFormalAbilityInputGateState.DelayBeforeUse && m_settings.delayBeforeUseReleaseInterruption)
            {
                Interrupt();
                return;
            }

            if (m_state == EFormalAbilityInputGateState.DelayBetweenUses && m_settings.timeBetweenUsesReleaseInterruption)
            {
                StopUseSequence();
            }
        }

        /// <summary>
        /// 由拥有者逐帧推进，保证状态机可测试且不依赖协程生命周期。
        /// </summary>
        public void Tick(float deltaTime)
        {
            deltaTime = Mathf.Max(0.0f, deltaTime) * m_timeScale;
            TickBuffer(deltaTime);

            switch (m_state)
            {
                case EFormalAbilityInputGateState.DelayBeforeUse:
                    TickTimedState(deltaTime, EnterUse);
                    break;
                case EFormalAbilityInputGateState.DelayBetweenUses:
                    TickDelayBetweenUses(deltaTime);
                    break;
                case EFormalAbilityInputGateState.Stop:
                case EFormalAbilityInputGateState.Interrupted:
                    ChangeState(EFormalAbilityInputGateState.Idle);
                    TryConsumeBufferedInput();
                    break;
            }
        }

        public void Interrupt()
        {
            m_bufferedInput = false;
            m_bufferTimer = 0.0f;
            ChangeState(EFormalAbilityInputGateState.Interrupted);
            interrupted?.Invoke();
            sequenceStopped?.Invoke();
        }

        /// <summary>
        /// 读档和重置时只清掉本地瞬时输入门状态。
        /// 若未来要恢复中途施法，必须先把 GAS active lifecycle、动作锁和回调一并定义成可恢复协议。
        /// </summary>
        public void ResetTransientState()
        {
            m_state = EFormalAbilityInputGateState.Idle;
            m_stateTimer = 0.0f;
            m_triggerHeld = false;
            m_triggerReleasedSinceLastUse = true;
            m_bufferedInput = false;
            m_bufferTimer = 0.0f;
        }

        private bool StartUseSequence()
        {
            if (!CanStartUseSequence())
            {
                return false;
            }

            m_bufferedInput = false;
            m_bufferTimer = 0.0f;
            m_triggerReleasedSinceLastUse = false;
            sequenceStarted?.Invoke();
            ChangeState(EFormalAbilityInputGateState.Start);

            if (m_settings.triggerMode == EFormalAbilityInputTriggerMode.HoldRelease)
            {
                ChangeState(EFormalAbilityInputGateState.Charging);
                return true;
            }

            if (m_settings.delayBeforeUse > 0.0f)
            {
                ChangeTimedState(EFormalAbilityInputGateState.DelayBeforeUse, m_settings.delayBeforeUse);
            }
            else
            {
                EnterUse();
            }

            return true;
        }

        private void EnterUse()
        {
            ChangeState(EFormalAbilityInputGateState.Use);
            usePerformed?.Invoke();
            if (m_state != EFormalAbilityInputGateState.Use)
            {
                return;
            }

            ChangeTimedState(EFormalAbilityInputGateState.DelayBetweenUses, m_settings.timeBetweenUses);
        }

        private void TickDelayBetweenUses(float deltaTime)
        {
            TickTimedState(deltaTime, () =>
            {
                if (m_settings.triggerMode == EFormalAbilityInputTriggerMode.Auto && m_triggerHeld)
                {
                    if (StartUseSequence())
                    {
                        return;
                    }
                }

                StopUseSequence();
            });
        }

        private void StopUseSequence()
        {
            if (m_state != EFormalAbilityInputGateState.Idle && m_state != EFormalAbilityInputGateState.Stop)
            {
                ChangeState(EFormalAbilityInputGateState.Stop);
                sequenceStopped?.Invoke();
            }
        }

        private void TryBufferInput()
        {
            if (!m_settings.bufferInput || m_settings.triggerMode == EFormalAbilityInputTriggerMode.HoldRelease)
            {
                return;
            }

            if (!m_bufferedInput || m_settings.newInputExtendsBuffer)
            {
                m_bufferedInput = true;
                m_bufferTimer = m_settings.maximumBufferDuration;
            }
        }

        private void TickBuffer(float deltaTime)
        {
            if (!m_bufferedInput)
            {
                return;
            }

            m_bufferTimer -= deltaTime;
            if (m_bufferTimer <= 0.0f)
            {
                m_bufferedInput = false;
                m_bufferTimer = 0.0f;
            }
        }

        private void TryConsumeBufferedInput()
        {
            if (!m_bufferedInput)
            {
                return;
            }

            if (m_settings.triggerMode == EFormalAbilityInputTriggerMode.HoldRelease)
            {
                m_bufferedInput = false;
                return;
            }

            if (m_settings.triggerMode == EFormalAbilityInputTriggerMode.SemiAuto && !m_triggerReleasedSinceLastUse)
            {
                return;
            }

            m_bufferedInput = false;
            StartUseSequence();
        }

        private bool CanStartUseSequence()
        {
            return m_canStartUseSequence?.Invoke() ?? true;
        }

        private void ChangeTimedState(EFormalAbilityInputGateState nextState, float duration)
        {
            m_stateTimer = Mathf.Max(0.0f, duration);
            ChangeState(nextState);
        }

        private void TickTimedState(float deltaTime, Action onCompleted)
        {
            m_stateTimer -= deltaTime;
            if (m_stateTimer <= 0.0f)
            {
                m_stateTimer = 0.0f;
                onCompleted?.Invoke();
            }
        }

        private void ChangeState(EFormalAbilityInputGateState nextState)
        {
            if (m_state == nextState)
            {
                return;
            }

            m_state = nextState;
            stateChanged?.Invoke(m_state);
        }
    }
}

