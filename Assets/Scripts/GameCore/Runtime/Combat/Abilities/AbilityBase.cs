namespace GameCore
{
    public abstract class AbilityBase : UnityEngine.MonoBehaviour
    {
        private int m_formalGasAbilityCode = 0;
        private int m_formalGasAbilityInitializationCode = 0;
        private FormalGasAbilityRuntimeConfig m_formalGasRuntimeConfig = default;
        private bool m_hasFormalGasRuntimeConfig = false;

        protected CharacterBase m_character = null;
        protected bool usesFormalGasAbility => m_formalGasAbilityCode > 0;
        protected int formalGasAbilityCode => m_formalGasAbilityCode;
        internal bool usesFormalGasAbilityForRuntime => usesFormalGasAbility;

        protected virtual void InitRuntime(CharacterBase character)
        {
            m_character = character;
            ConfigureFormalGasContext(m_formalGasAbilityInitializationCode);
        }

        public void InitFormalGasAbility(
            CharacterBase character,
            int formalGasAbilityCode)
        {
            m_character = character;
            m_formalGasAbilityInitializationCode = System.Math.Max(0, formalGasAbilityCode);
            try
            {
                ConfigureFormalGasContext(m_formalGasAbilityInitializationCode);
                InitFormalGasAbilityCore();
                ConfigureFormalGasContext(m_formalGasAbilityInitializationCode);
            }
            finally
            {
                m_formalGasAbilityInitializationCode = 0;
            }
        }

        protected virtual void InitFormalGasAbilityCore()
        {
            InitRuntime(m_character);
            ConfigureFormalGasContext(formalGasAbilityCode);
        }

        protected bool TryGetFormalGasRuntimeConfig(out FormalGasAbilityRuntimeConfig config)
        {
            config = m_formalGasRuntimeConfig;
            return m_hasFormalGasRuntimeConfig;
        }

        protected void ConfigureFormalGasContext(int formalGasAbilityCode)
        {
            m_formalGasAbilityCode = System.Math.Max(0, formalGasAbilityCode);
            m_hasFormalGasRuntimeConfig = m_formalGasAbilityCode > 0 &&
                FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(
                    m_formalGasAbilityCode,
                    out m_formalGasRuntimeConfig);
            if (!m_hasFormalGasRuntimeConfig)
            {
                m_formalGasRuntimeConfig = default;
            }
        }

        public virtual void UpdateCooldowns()
        {
            UpdateCooldowns(UnityEngine.Time.deltaTime);
        }

        public virtual void UpdateCooldowns(float deltaTime) { }

        /// <summary>
        /// 能力自己的动画状态更新触点。吸收 TopDown CharacterAbility.UpdateAnimator 的调用位置，但不接管 Animator 参数系统。
        /// </summary>
        public virtual void UpdateAnimationState() { }
        public virtual void Reset() { }
        public virtual void Interrupt() { }
        public virtual void Destroy()
        {
            UnityEngine.Object.Destroy(gameObject);
        }

        internal bool UsesAutomaticRuntimeStateManagement()
        {
            return usesFormalGasAbility;
        }

        internal CharacterAbilityRuntimeStateData CreateFormalRuntimeState()
        {
            if (!usesFormalGasAbility)
            {
                return null;
            }

            CharacterAbilityRuntimeStateData runtimeState = new()
            {
                formalGasAbilityCode = formalGasAbilityCode,
                state = CaptureAbilityState()
            };

            WriteFormalRuntimeState(runtimeState);

            if (this is IAbilityRuntimeExtraStateCarrier runtimeExtraStateCarrier &&
                runtimeExtraStateCarrier.TryCaptureRuntimeExtraState(out AbilityRuntimeExtraState extraRuntimeState))
            {
                runtimeState.extraRuntimeState = extraRuntimeState;
            }

            return runtimeState;
        }

        internal void RestoreFormalRuntimeState(CharacterAbilityRuntimeStateData runtimeState)
        {
            if (runtimeState == null || !ApplyAbilityState(runtimeState.state))
            {
                return;
            }

            ReadFormalRuntimeState(runtimeState);

            if (runtimeState.extraRuntimeState != null &&
                this is IAbilityRuntimeExtraStateCarrier runtimeExtraStateCarrier)
            {
                runtimeExtraStateCarrier.RestoreRuntimeExtraState(runtimeState.extraRuntimeState);
            }
            else if (runtimeState.extraRuntimeState != null)
            {
                UnityEngine.Debug.LogWarning($"EX-GAS Ability [{formalGasAbilityCode}] carries formal extra runtime state, but runtime instance [{GetType().Name}] does not implement {nameof(IAbilityRuntimeExtraStateCarrier)}.");
            }
        }

        protected virtual void WriteFormalRuntimeState(CharacterAbilityRuntimeStateData runtimeState)
        {
        }

        protected virtual void ReadFormalRuntimeState(CharacterAbilityRuntimeStateData runtimeState)
        {
        }

        private EPersistableObjectState CaptureAbilityState()
        {
            if (UsesAutomaticRuntimeStateManagement())
            {
                return EPersistableObjectState.Inactive;
            }

            return gameObject.activeInHierarchy
                ? EPersistableObjectState.Active
                : EPersistableObjectState.Inactive;
        }

        private bool ApplyAbilityState(EPersistableObjectState state)
        {
            switch (state)
            {
                case EPersistableObjectState.Active:
                    gameObject.SetActive(true);
                    return true;
                case EPersistableObjectState.Inactive:
                    gameObject.SetActive(false);
                    return true;
                case EPersistableObjectState.Destroyed:
                    UnityEngine.Object.Destroy(gameObject);
                    return false;
                default:
                    return true;
            }
        }
    }
}
