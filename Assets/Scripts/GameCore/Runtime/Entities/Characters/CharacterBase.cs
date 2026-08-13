using System;
using GAS.Runtime;
using UnityEngine;

namespace GameCore
{
    [RequireComponent(typeof(AbilitySystemComponent))]
    [RequireComponent(typeof(CharacterAbilitySet))]
    public abstract partial class CharacterBase : Movable
    {
        [Header("Character Base Settings")]
        [Range(Constants.MinLevel, Constants.MaxLevel)]
        [SerializeField] protected int m_level = Constants.MinLevel;
        [SerializeField] private bool m_invincibleOnHit = false;
        [SerializeField] private bool m_invincibleOnRevive = false;
        [SerializeField] private bool m_invincible = false;
        [SerializeField] private bool m_restoreHealthOnLevelUp = true;
        [SerializeField] private bool m_restoreManaOnLevelUp = true;

        public abstract CharacterSheet characterSheet { get; }
        public bool dead => GetCurrentHealth() == 0;
        public int level => m_level;
        public bool invincibleOnHit => m_invincibleOnHit;
        public EAlignment currentAlignment =>
            TryResolveAlterationAlignmentOverride(out EAlignment alterationAlignment)
                ? alterationAlignment
                : m_alignmentOverride ?? characterSheet.alignment;
        public bool invincible => m_invincible
            || m_temporaryInvincibilityTimer > 0.0f
            || dead
            || (m_animationStrategy?.IsInvincibleAnimationPlaying() ?? false);

        private readonly CharacterActionStateRuntime m_actionRuntime = new();
        private EAlignment? m_alignmentOverride = null;
        private bool m_isDeadAndDestroyed = false;
        private float m_temporaryInvincibilityTimer = 0.0f;

        protected bool m_isSummoned = false;
        private CharacterBase m_lastEffectiveDamageSource = null;
        private bool m_hasDeathCommandContextOverride = false;
        private GameCommandContext m_deathCommandContextOverride;
        private bool m_pendingDeathAfterFormalCurrentValueMutation = false;
        private bool m_pendingActionInterruptAfterFormalDamage = false;

        protected override void Awake()
        {
            base.Awake();

            InitializeAbilities();
            InitializeFormalAbilitySystemFromCharacterSheet();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureFormalAbilitySystemInitializedAfterAwake();
            RegisterFormalAttributeEvents();
        }
        
        protected override void Update()
        {
            ProcessPendingActionInterruptAfterFormalDamage();
            ProcessPendingDeathAfterFormalCurrentValueMutation();
            if (IsMarkedAsDestroyed())
            {
                return;
            }

            base.Update();
            AdvanceCharacterRuntime(Time.deltaTime);
        }

        internal void AdvanceCharacterRuntime(float deltaTime)
        {
            ProcessPendingActionInterruptAfterFormalDamage();
            ProcessPendingDeathAfterFormalCurrentValueMutation();
            if (IsMarkedAsDestroyed())
            {
                return;
            }

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            m_temporaryInvincibilityTimer = Mathf.Max(0.0f, m_temporaryInvincibilityTimer - safeDeltaTime);
            AbilityRuntime.UpdateRuntime(safeDeltaTime);
        }

        private void EnsureFormalAbilitySystemInitializedAfterAwake()
        {
            if (TryGetInitializedFormalAttributes(out _))
            {
                return;
            }

            InitializeFormalAbilitySystemFromCharacterSheet();
        }

        /// <summary>
        /// 角色禁用既可能来自对象池回收，也可能来自场景切换或读档重建。
        /// EX-GAS 的 ASC 会在 OnDisable 清掉能力、GameplayEffect 和 Tag；项目侧只清理仍由角色直接持有的瞬态状态。
        /// </summary>
        protected override void OnDisable()
        {
            UnregisterFormalAttributeEvents();
            CleanupOwnedTransientRuntimeState();
            base.OnDisable();
        }

        protected override void OnDeathAnimationEnd()
        {
            // 死亡动画结束只能驱动一次正式销毁流程，避免 Animator 启停后重复发送结束消息。
            if (!m_isDeadAndDestroyed)
            {
                base.OnDeathAnimationEnd();
            }
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            m_isDeadAndDestroyed = true;
        }

        protected override GameCommandContext ResolveDeathCommandContext()
        {
            if (TryConsumeDeathCommandContextOverride(out GameCommandContext overrideContext))
            {
                return overrideContext;
            }

            if (!TryGetLastEffectiveDamageSource(out CharacterBase source))
            {
                return GameCommandContext.Script();
            }

            return GameCommandContext.ResolveForActor(source);
        }

        protected override AudioClipResolver GetDeathAudio() => characterSheet.deathAudio;

        public override void Revive()
        {
            Heal(int.MaxValue, EEffectVisualFlags.NoFloatingText);
            RecoverMana(int.MaxValue, EEffectVisualFlags.NoFloatingText);
            AbilityRuntime.ResetInstances();

            base.Revive();
            m_isDeadAndDestroyed = false;
            NotifyPlayerSystemAboutRevive();

            if (m_invincibleOnRevive)
            {
                m_animationStrategy?.PlayInvincibleAnimation();
            }
        }

        public override void OnInteract(CharacterBase sender)
        {
            LookAtTarget(sender.transform);
            base.OnInteract(sender);
        }

        public override bool CanUpdateTargetDirection()
        {
            return base.CanUpdateTargetDirection() &&
                Can(EActionFlags.UpdateTargetDirection) &&
                (!TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet) ||
                 !abilitySet.ShouldLockTargetDirectionForInputGate());
        }

        protected virtual bool TryPlayHitAnimation()
        {
            return m_animationStrategy?.PlayHitAnimation() ?? false;
        }

        public override bool CanMove() => base.CanMove() && Can(EActionFlags.Move);

        protected override float CalculateMoveSpeed()
        {
            float speed = base.CalculateMoveSpeed();

            foreach (float factor in m_actionRuntime.CreateMoveSpeedFactorSnapshot())
            {
                speed *= factor;
            }

            return speed;
        }

        public override void Kill()
        {
            if (IsMarkedAsDestroyed())
            {
                return;
            }

            m_pendingDeathAfterFormalCurrentValueMutation = false;
            characterSheet.feedbacks.PlayDeath(transform.position);
            YokiFrame.EventKit.Type.Send(new DeathPresentationEvent(new DeathPresentationContext(transform.position, this, m_lastEffectiveDamageSource)));
            base.Kill();
            NotifyPlayerSystemAboutDeath();
            AbilityRuntime.InterruptInstances();
        }

        private void RequestDeathAfterFormalCurrentValueMutation()
        {
            m_pendingDeathAfterFormalCurrentValueMutation = true;
        }

        private void RequestActionInterruptAfterFormalDamage()
        {
            m_pendingActionInterruptAfterFormalDamage = true;
        }

        private void ProcessPendingActionInterruptAfterFormalDamage()
        {
            if (!m_pendingActionInterruptAfterFormalDamage ||
                IsMarkedAsDestroyed())
            {
                return;
            }

            m_pendingActionInterruptAfterFormalDamage = false;
            InterruptActions();
        }

        private void ProcessPendingDeathAfterFormalCurrentValueMutation()
        {
            if (!m_pendingDeathAfterFormalCurrentValueMutation ||
                IsMarkedAsDestroyed() ||
                !dead)
            {
                return;
            }

            Kill();
        }

        public void Kill(GameCommandContext context)
        {
            if (IsMarkedAsDestroyed())
            {
                return;
            }

            m_hasDeathCommandContextOverride = true;
            m_deathCommandContextOverride = context;
            Kill();
        }

        public bool TryGetLastEffectiveDamageSource(out CharacterBase source)
        {
            source = m_lastEffectiveDamageSource;
            return source != null;
        }

        protected void SetLastEffectiveDamageSource(CharacterBase source)
        {
            if (source != null && source != this)
            {
                m_lastEffectiveDamageSource = source;
            }
        }

        private bool TryConsumeDeathCommandContextOverride(out GameCommandContext context)
        {
            context = m_deathCommandContextOverride;
            if (!m_hasDeathCommandContextOverride)
            {
                return false;
            }

            m_hasDeathCommandContextOverride = false;
            m_deathCommandContextOverride = default;
            return true;
        }

        private void CleanupOwnedTransientRuntimeState()
        {
            m_temporaryInvincibilityTimer = 0.0f;
            m_pendingActionInterruptAfterFormalDamage = false;
            m_animationStrategy?.OnInvincibleAnimationStop();
            AbilityRuntime.InterruptInstances();
            ClearOwnedCharacterTransientState();
        }

        private CharacterAbilitySetRuntime AbilityRuntime =>
            TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet)
                ? abilitySet.Runtime
                : throw new InvalidOperationException(
                    $"[{nameof(CharacterBase)}] requires {nameof(CharacterAbilitySet)} as the formal ability runtime owner.");

        private bool TryGetAbilitySet(out CharacterAbilitySet abilitySet)
        {
            if (TryGetComponent(out abilitySet) && abilitySet != null)
            {
                return true;
            }

            abilitySet = null;
            return false;
        }

        public bool TryResolvePlayerInputTarget(out IPlayerInputTarget inputTarget)
        {
            inputTarget = null;

            if (!CanBePlayerControlled() ||
                !HasConfiguredPlayerInputTarget(out CharacterPlayerControl playerControl))
            {
                return false;
            }

            inputTarget = playerControl;
            return true;
        }

        public bool HasConfiguredPlayerInputTarget(out CharacterPlayerControl playerControl)
        {
            if (!TryGetComponent(out playerControl) ||
                playerControl == null ||
                !playerControl.AcceptsPlayerInput ||
                !playerControl.isActiveAndEnabled)
            {
                playerControl = null;
                return false;
            }

            return true;
        }

        internal bool TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet)
        {
            return TryGetAbilitySet(out abilitySet) &&
                abilitySet.OwnsAbilityComposition;
        }

        private void NotifyPlayerSystemAboutDeath()
        {
            GameManager.PlayerSystem.NotifyCharacterDied(this);
        }

        private void NotifyPlayerSystemAboutRevive()
        {
            GameManager.PlayerSystem.NotifyCharacterRevived(this);
        }

        /// <summary>
        /// 这些字段只描述当前实体实例的局部运行时状态，
        /// 禁用/回收后不能泄漏到下一次复用。
        /// </summary>
        private void ClearOwnedCharacterTransientState()
        {
            m_isDeadAndDestroyed = false;
            m_isSummoned = false;
            m_alignmentOverride = null;
            m_lastEffectiveDamageSource = null;
            m_hasDeathCommandContextOverride = false;
            m_deathCommandContextOverride = default;
        }
    }
}
