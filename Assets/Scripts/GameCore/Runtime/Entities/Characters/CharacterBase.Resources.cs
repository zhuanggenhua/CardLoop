using UnityEngine;
using UnityEngine.Events;
using Unity.Mathematics;
using GAS.Runtime;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        public void SetInvincibleOnHit(bool invincibleOnHit) => m_invincibleOnHit = invincibleOnHit;

        /// <summary>
        /// 通用当前属性改动只保留给非资源属性使用。
        /// 若传入生命或法力，则自动转到专用资源写入口，避免外部继续把资源修改当普通属性增减来用。
        /// </summary>
        public void ModifyCurrentStat(EStat stat, int delta)
        {
            switch (stat)
            {
                case EStat.Health:
                    ModifyCurrentHealth(delta);
                    return;
                case EStat.Mana:
                    ModifyCurrentMana(delta);
                    return;
                default:
                    int newValue = GetCurrentStatValue(stat) + delta;
                    SetFormalCurrentStatOrReportFailure(stat, newValue);

                    return;
            }
        }

        /// <summary>
        /// 生命和法力不是普通属性，它们带有“当前值 / 上限 / 缺口 / 能否恢复 / 能否支付”的资源语义。
        /// 后续若由 GAS 接管，这一层是优先替换入口，外部不应继续自己拼 Health/Mana 规则。
        /// </summary>
        public int GetMaxHealth() => GetFormalBaseStatOrBootstrapBuffer(EStat.Health);
        public int GetCurrentHealth() => GetFormalCurrentStatOrBootstrapBuffer(EStat.Health);
        public int GetMissingHealth() => math.max(0, GetMaxHealth() - GetCurrentHealth());
        public bool CanRecoverHealth() => GetMissingHealth() > 0;
        public int GetMaxMana() => GetFormalBaseStatOrBootstrapBuffer(EStat.Mana);
        public int GetCurrentMana() => GetFormalCurrentStatOrBootstrapBuffer(EStat.Mana);
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

        public EResourceValidationResult ValidateCurrentResourceDelta(Stats statDelta, int minimumHealth = 0, int minimumMana = 0)
        {
            if (statDelta == null)
            {
                return ValidateCurrentResourceDelta(0, 0, minimumHealth, minimumMana);
            }

            return ValidateCurrentResourceDelta(statDelta[EStat.Health], statDelta[EStat.Mana], minimumHealth, minimumMana);
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
        public void ModifyCurrentHealth(int delta, int minimumValue = 0)
        {
            int appliedDelta = ClampCurrentHealthDelta(delta, minimumValue);
            if (appliedDelta == 0)
            {
                return;
            }

            ApplyCurrentResourceDeltaViaFormalAbilitySystem(EStat.Health, appliedDelta);
        }

        /// <summary>
        /// 当前法力值改动保留在拥有者内部完成，负值不会低于最低保底值。
        /// </summary>
        public void ModifyCurrentMana(int delta, int minimumValue = 0)
        {
            int appliedDelta = ClampCurrentManaDelta(delta, minimumValue);
            if (appliedDelta == 0)
            {
                return;
            }

            ApplyCurrentResourceDeltaViaFormalAbilitySystem(EStat.Mana, appliedDelta);
        }

        /// <summary>
        /// 读取角色当前生效的正式属性值。
        /// 调用方只想知道某一个数值时，应走这个标量入口，而不是先拿整份属性快照。
        /// </summary>
        public int GetStatValue(EStat stat) => GetFormalBaseStatOrBootstrapBuffer(stat);

        public int GetStatValue(FormalAttributeDefinition definition) => GetStatValue(definition.Stat);

        public float GetAttackSpeedMultiplier(float baseline = 100.0f)
        {
            if (baseline <= 0.0f)
            {
                return 1.0f;
            }

            float currentAttackSpeed = math.max(0.0f, GetCurrentStatValue(EStat.AttackSpeed));
            if (currentAttackSpeed <= 0.0f)
            {
                return 1.0f;
            }

            return math.max(0.05f, currentAttackSpeed / baseline);
        }

        /// <summary>
        /// 读取角色当前运行时属性值。
        /// 这里的“当前”包含生命、法力以及临时效果改动后的实时结果，后续若切 GAS，优先在这里收查询真相。
        /// </summary>
        public int GetCurrentStatValue(EStat stat) => GetFormalCurrentStatOrBootstrapBuffer(stat);

        public int GetCurrentStatValue(FormalAttributeDefinition definition) => GetCurrentStatValue(definition.Stat);

        /// <summary>
        /// 只有确实需要整份属性快照做批量计算或存档时，才暴露完整快照。
        /// 外部 UI 和简单规则读取优先使用标量查询入口，避免把整份属性真相到处外借。
        /// </summary>
        public Stats CreateStatsSnapshot() => TryGetInitializedFormalAttributes(out _)
            ? CreateFormalBaseStatsSnapshot()
            : CreateBootstrapBaseStatsSnapshotOrReportFailure();

        /// <summary>
        /// 创建当前运行时属性快照。
        /// 这个入口主要服务存档、批量计算或需要复制整组状态的调用点，不作为日常单值读取路径。
        /// </summary>
        public Stats CreateCurrentStatsSnapshot() => TryGetInitializedFormalAttributes(out _)
            ? CreateFormalCurrentStatsSnapshot()
            : CreateBootstrapCurrentStatsSnapshotOrReportFailure();

        /// <summary>
        /// 战斗层只取当前结算真正需要的最小属性快照。
        /// 后续若切 GAS，优先在这里切掉伤害系统对整份 Stats 的依赖。
        /// </summary>
        public CombatStatSnapshot CreateCombatStatSnapshot() => TryGetInitializedFormalAttributes(out _)
            ? CreateFormalCombatStatSnapshot()
            : CreateCombatStatSnapshotFromCurrentStats(CreateBootstrapCurrentStatsSnapshotOrReportFailure());

        public void AddStatsChangedListener(UnityAction<Stats> listener)
        {
            m_statsChanged.AddListener(listener);
        }

        public void RemoveStatsChangedListener(UnityAction<Stats> listener)
        {
            m_statsChanged.RemoveListener(listener);
        }

        public void AddCurrentStatsChangedListener(UnityAction<Stats> listener)
        {
            m_currentStatsChanged.AddListener(listener);
        }

        public void RemoveCurrentStatsChangedListener(UnityAction<Stats> listener)
        {
            m_currentStatsChanged.RemoveListener(listener);
        }

        public bool Damage(DamageOutputDescriptor damageOutput, EEffectVisualFlags visualFlags = EEffectVisualFlags.None, Vector2? velocity = null, DamageImpactSettings damageImpact = default)
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

                ApplyCurrentHealthLossViaFormalGameplayEffect(damageInput.damage, sourceCharacter);

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

        public void Heal(int value, EEffectVisualFlags visualFlags = EEffectVisualFlags.None)
        {
            int appliedValue = math.min(math.max(0, value), GetMissingHealth());
            ModifyCurrentHealth(appliedValue);
            YokiFrame.EventKit.Type.Send(new HealthRecoveredPresentationEvent(new CharacterValuePresentationContext(transform.position, this, appliedValue, visualFlags)));
        }

        public void RecoverMana(int value, EEffectVisualFlags visualFlags = EEffectVisualFlags.None)
        {
            int appliedValue = math.min(math.max(0, value), GetMissingMana());
            ModifyCurrentMana(appliedValue);
            YokiFrame.EventKit.Type.Send(new ManaRecoveredPresentationEvent(new CharacterValuePresentationContext(transform.position, this, appliedValue, visualFlags)));
        }

        public void ConsumeMana(int value, EEffectVisualFlags visualFlags = EEffectVisualFlags.None)
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

        /// <summary>
        /// 正式角色 prefab 现已强制挂 ASC，运行时当前值写入口不再允许退回启动缓冲。
        /// 若这里失败，说明正式属性宿主或字段接线已经回归损坏，应直接报错而不是默默双轨。
        /// </summary>
        private void SetFormalCurrentStatOrReportFailure(EStat stat, int value)
        {
            if (TrySetFormalCurrentStat(stat, value))
            {
                return;
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 运行时当前属性写入必须命中正式 ASC，无法为 {name} 写入 {stat}={value}。", this);
        }

        private void ApplyCurrentResourceDeltaViaFormalAbilitySystem(
            EStat stat,
            int delta)
        {
            SetFormalCurrentStatOrReportFailure(stat, GetCurrentStatValue(stat) + delta);
        }

        private void ApplyCurrentHealthLossViaFormalGameplayEffect(int requestedDamage, CharacterBase sourceCharacter)
        {
            int appliedDamage = math.min(math.max(0, requestedDamage), GetCurrentHealth());
            if (appliedDamage <= 0)
            {
                return;
            }

            ApplyCurrentResourceDeltaViaFormalAbilitySystem(EStat.Health, -appliedDamage);
        }

        /// <summary>
        /// 标量属性读取默认只认正式 ASC。
        /// 仅当角色正式属性系统尚未完成启动初始化时，才允许退回启动快照缓冲。
        /// </summary>
        private int GetFormalBaseStatOrBootstrapBuffer(EStat stat)
        {
            if (TryGetFormalBaseStat(stat, out int value))
            {
                return value;
            }

            return ReadBootstrapBaseStatOrReportFailure(stat);
        }

        private int GetFormalCurrentStatOrBootstrapBuffer(EStat stat)
        {
            if (TryGetFormalCurrentStat(stat, out int value))
            {
                return value;
            }

            return ReadBootstrapCurrentStatOrReportFailure(stat);
        }

        private int ReadBootstrapBaseStatOrReportFailure(EStat stat)
        {
            if (IsAttributeBootstrapReadWindowOpen())
            {
                return m_attributeBootstrapBuffer.GetBaseStat(stat);
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 运行时基础属性读取必须命中正式 ASC，无法为 {name} 读取 {stat}。", this);
            return 0;
        }

        private int ReadBootstrapCurrentStatOrReportFailure(EStat stat)
        {
            if (IsAttributeBootstrapReadWindowOpen())
            {
                return m_attributeBootstrapBuffer.GetCurrentStat(stat);
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 运行时当前属性读取必须命中正式 ASC，无法为 {name} 读取 {stat}。", this);
            return 0;
        }

        private Stats CreateBootstrapBaseStatsSnapshotOrReportFailure()
        {
            if (IsAttributeBootstrapReadWindowOpen())
            {
                return m_attributeBootstrapBuffer.CreateBaseStatsSnapshot();
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 基础属性快照必须命中正式 ASC，当前角色无法继续依赖启动期基础快照。", this);
            return new Stats();
        }

        private Stats CreateBootstrapCurrentStatsSnapshotOrReportFailure()
        {
            if (IsAttributeBootstrapReadWindowOpen())
            {
                return m_attributeBootstrapBuffer.CreateCurrentStatsSnapshot();
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 当前属性快照必须命中正式 ASC，当前角色无法继续依赖启动期当前快照。", this);
            return new Stats();
        }

        private bool IsAttributeBootstrapReadWindowOpen() => m_isAttributeBootstrapReadWindowOpen;

        private static CombatStatSnapshot CreateCombatStatSnapshotFromCurrentStats(Stats currentStats)
        {
            Stats safeCurrentStats = currentStats ?? new Stats();
            return new CombatStatSnapshot(
                safeCurrentStats[EStat.PhysicalAttack],
                safeCurrentStats[EStat.MagicalAttack],
                safeCurrentStats[EStat.PhysicalDefense],
                safeCurrentStats[EStat.MagicalDefense],
                safeCurrentStats[EStat.Agility],
                safeCurrentStats[EStat.Luck]);
        }
    }
}
