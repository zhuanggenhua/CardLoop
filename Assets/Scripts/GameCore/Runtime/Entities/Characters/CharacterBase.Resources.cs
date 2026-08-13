using UnityEngine;
using Unity.Mathematics;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        public void SetInvincibleOnHit(bool invincibleOnHit) => m_invincibleOnHit = invincibleOnHit;

        /// <summary>
        /// 生命和法力是 EX-GAS 中成对的当前值与上限属性；角色只提供领域语义查询。
        /// </summary>
        public int GetMaxHealth() => Mathf.RoundToInt(GetAttributeBaseValue(CharacterAttributes.MaxHealth));
        public int GetCurrentHealth() => Mathf.RoundToInt(GetAttributeCurrentValue(CharacterAttributes.Health));
        public int GetMissingHealth() => math.max(0, GetMaxHealth() - GetCurrentHealth());
        public bool CanRecoverHealth() => GetMissingHealth() > 0;
        public int GetMaxMana() => Mathf.RoundToInt(GetAttributeBaseValue(CharacterAttributes.MaxMana));
        public int GetCurrentMana() => Mathf.RoundToInt(GetAttributeCurrentValue(CharacterAttributes.Mana));
        public int GetMissingMana() => math.max(0, GetMaxMana() - GetCurrentMana());
        public bool CanRecoverMana() => GetMissingMana() > 0;
        public bool HasEnoughMana(int amount) => GetCurrentMana() >= math.max(0, amount);

        /// <summary>
        /// 资源合法性判断由角色拥有者统一回答，外部不再自己拼“是否会死 / 是否会负蓝”的规则。
        /// </summary>
        public bool CanModifyCurrentHealth(int delta, int minimumValue = 0) => GetCurrentHealth() + delta >= minimumValue;
        public bool CanModifyCurrentMana(int delta, int minimumValue = 0) => GetCurrentMana() + delta >= minimumValue;

        /// <summary>
        /// 当前资源变更是否合法，由角色拥有者统一给出分类结果。
        /// 先判生命，再判法力，避免外部调用方自己拆 Health/Mana 条件后再映射业务结果。
        /// </summary>
        public EResourceValidationResult ValidateCurrentResourceDelta(int healthDelta, int manaDelta, int minimumHealth = 0, int minimumMana = 0)
        {
            if (!CanModifyCurrentHealth(healthDelta, minimumHealth))
            {
                return EResourceValidationResult.HealthBelowMinimum;
            }

            if (!CanModifyCurrentMana(manaDelta, minimumMana))
            {
                return EResourceValidationResult.ManaBelowMinimum;
            }

            return EResourceValidationResult.Valid;
        }

        /// <summary>
        /// 将资源改变量裁到当前角色允许的范围内。
        /// 这主要服务持续效果和装备预演，避免外部再去读当前值后手工写最小值裁剪。
        /// </summary>
        public int ClampCurrentHealthDelta(int delta, int minimumValue = 0)
        {
            int minimumAllowedDelta = minimumValue - GetCurrentHealth();
            return math.max(delta, minimumAllowedDelta);
        }

        public int ClampCurrentManaDelta(int delta, int minimumValue = 0)
        {
            int minimumAllowedDelta = minimumValue - GetCurrentMana();
            return math.max(delta, minimumAllowedDelta);
        }

        /// <summary>
        /// 当前生命值改动保留在拥有者内部完成，调用方只描述资源变化量和最低保底值。
        /// 正值允许临时超过上限，负值会按最低保底值截断；这样持续效果和伤害不需要自己再改底层数组。
        /// </summary>
        private void ModifyCurrentHealth(int delta, int minimumValue = 0)
        {
            int appliedDelta = ClampCurrentHealthDelta(delta, minimumValue);
            if (appliedDelta == 0)
            {
                return;
            }

            ApplyCurrentResourceDeltaViaFormalAbilitySystem(CharacterAttributes.Health, appliedDelta);
        }

        /// <summary>
        /// 当前法力值改动保留在拥有者内部完成，负值不会低于最低保底值。
        /// </summary>
        private void ModifyCurrentMana(int delta, int minimumValue = 0)
        {
            int appliedDelta = ClampCurrentManaDelta(delta, minimumValue);
            if (appliedDelta == 0)
            {
                return;
            }

            ApplyCurrentResourceDeltaViaFormalAbilitySystem(CharacterAttributes.Mana, appliedDelta);
        }

        public float GetAttackSpeedMultiplier(float baseline = 100.0f)
        {
            if (baseline <= 0.0f)
            {
                return 1.0f;
            }

            float currentAttackSpeed = math.max(
                0.0f,
                GetAttributeCurrentValue(CharacterAttributes.AttackSpeed));
            if (currentAttackSpeed <= 0.0f)
            {
                return 1.0f;
            }

            return math.max(0.05f, currentAttackSpeed / baseline);
        }

        /// <summary>
        /// 战斗层只取命中结算需要的 EX-GAS 属性值，不外借整个 ASC。
        /// </summary>
        internal CombatStatSnapshot CreateCombatStatSnapshot() => CreateFormalCombatStatSnapshot();

        internal bool Damage(DamageOutputDescriptor damageOutput, EEffectVisualFlags visualFlags = EEffectVisualFlags.None, Vector2? velocity = null, DamageImpactSettings damageImpact = default)
        {
            damageOutput.TryGetSourceCharacter(out CharacterBase sourceCharacter);

            bool isSelfTargeted = sourceCharacter == this;
            if (!CombatSolver.CanTarget(damageOutput, this))
            {
                return false;
            }

            DamageInputDescriptor damageInput = DamageSolver.SolveDamageInput(this, damageOutput);
            if (velocity.HasValue)
            {
                TryPush(damageInput, velocity.Value, damageImpact);
            }

            if (sourceCharacter != null)
            {
                m_provoked.Invoke(sourceCharacter);
            }

            if (damageInput.damage > 0)
            {
                SetLastEffectiveDamageSource(sourceCharacter);

                if (!damageInput.silent)
                {
                    RequestActionInterruptAfterFormalDamage();
                    TryPlayHitAnimation();
                }

                ApplyCurrentHealthLoss(damageInput.damage);

                characterSheet.feedbacks.PlayDamageTaken(transform.position, this, damageInput, visualFlags);
                if (characterSheet.hitAudio)
                {
                    YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(characterSheet.hitAudio));
                }

                if (!dead && !damageInput.silent && invincibleOnHit && !isSelfTargeted)
                {
                    m_animationStrategy?.PlayInvincibleAnimation();
                }

                if (!isSelfTargeted && !damageInput.silent && damageImpact.sanitizedInvincibilityDuration > 0.0f)
                {
                    // TopDown 的 DamageOnTouch 会把受击保护时间作为命中区参数；这里仅吸收保护时长，不接管 RPG 生命值真相。
                    ExtendTemporaryInvincibility(damageImpact.sanitizedInvincibilityDuration);
                }
            }

            return !damageInput.IsMissed;
        }

        internal void Heal(int value, EEffectVisualFlags visualFlags = EEffectVisualFlags.None)
        {
            int appliedValue = math.min(math.max(0, value), GetMissingHealth());
            ModifyCurrentHealth(appliedValue);
            YokiFrame.EventKit.Type.Send(new HealthRecoveredPresentationEvent(new CharacterValuePresentationContext(transform.position, this, appliedValue, visualFlags)));
        }

        internal void RecoverMana(int value, EEffectVisualFlags visualFlags = EEffectVisualFlags.None)
        {
            int appliedValue = math.min(math.max(0, value), GetMissingMana());
            ModifyCurrentMana(appliedValue);
            YokiFrame.EventKit.Type.Send(new ManaRecoveredPresentationEvent(new CharacterValuePresentationContext(transform.position, this, appliedValue, visualFlags)));
        }

        internal void ConsumeMana(int value, EEffectVisualFlags visualFlags = EEffectVisualFlags.None)
        {
            int appliedValue = math.min(math.max(0, value), GetCurrentMana());
            ModifyCurrentMana(-appliedValue);
            YokiFrame.EventKit.Type.Send(new ManaConsumedPresentationEvent(new CharacterValuePresentationContext(transform.position, this, appliedValue, visualFlags)));
        }

        public virtual void LevelUp(bool silentMode = false)
        {
            ++m_level;

            if (!silentMode)
            {
                if (m_restoreHealthOnLevelUp)
                {
                    Heal(GetMissingHealth());
                }

                if (m_restoreManaOnLevelUp)
                {
                    RecoverMana(GetMissingMana());
                }
            }

            UnlockFormalGasAbilitiesForLevel(characterSheet.GetFormalGasAbilitiesUnlockedAtLevel(m_level));
            m_levelUpped.Invoke(m_level);
        }

        private void ExtendTemporaryInvincibility(float duration)
        {
            m_temporaryInvincibilityTimer = Mathf.Max(m_temporaryInvincibilityTimer, duration);
        }

        private void ApplyCurrentResourceDeltaViaFormalAbilitySystem(
            int attributeCode,
            int delta)
        {
            float nextBaseValue = GetAttributeBaseValue(attributeCode) + delta;
            SetAttributeBaseValueAndRecalculate(attributeCode, nextBaseValue);
        }

        private void ApplyCurrentHealthLoss(int requestedDamage)
        {
            int appliedDamage = math.min(math.max(0, requestedDamage), GetCurrentHealth());
            if (appliedDamage <= 0)
            {
                return;
            }

            ApplyCurrentResourceDeltaViaFormalAbilitySystem(CharacterAttributes.Health, -appliedDamage);
        }

    }
}
