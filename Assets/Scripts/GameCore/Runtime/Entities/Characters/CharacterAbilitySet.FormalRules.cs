using System;
using System.Collections.Generic;
using GAS.General;
using GAS.Runtime;
using UnityEngine;
using UEntity = Unity.Entities.Entity;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// Formal 规则专用的 EX-GAS 空逻辑代理，只让 AbilitySpec 能承载成本、冷却和标签规则。
    /// </summary>
    [Serializable]
    public sealed class FormalAbilityRuleProxyLogic : AbilityLogicBase<XParamNone>
    {
        public FormalAbilityRuleProxyLogic(UEntity ability) : base(ability)
        {
        }

        public override void ActivateAbility(GlobalTimer timer)
        {
        }

        public override void CancelAbility(GlobalTimer timer)
        {
        }

        public override void EndAbility(GlobalTimer timer)
        {
        }

        public override void AbilityTick(GlobalTimer timer)
        {
        }
    }

    /// <summary>
    /// CharacterAbilitySet 的 Formal GAS 规则桥接部分，负责注册能力规则、评估成本/冷却并同步项目侧冷却状态。
    /// </summary>
    public sealed partial class CharacterAbilitySet
    {
        private readonly Dictionary<int, FormalAbilityRuleBinding> m_formalRuleBindings = new();

        private sealed class FormalAbilityRuleBinding
        {
            public int AbilityCode;
            public int CooldownTagCode;
            public AbilitySpec AbilitySpec;
            public bool UsesFormalCost;
            public bool TracksCooldownViaGas;
            public bool UsesGeneratedGasAbilityConfig;
            public bool CommitsFormalCostInProject;
            public bool CommitsFormalCooldownInProject;
            public UEntity CooldownEffectEntity;
        }

        internal static void EnsureFormalAbilityRuleLogicRegistered()
        {
            try
            {
                AbilityHelper.GetAbilityLogicType(FormalRuleLogicTypeName);
            }
            catch (Exception)
            {
                AbilityHelper.RegisterAbilityLogic(
                    FormalRuleLogicTypeName,
                    typeof(FormalAbilityRuleProxyLogic),
                    typeof(XParamNone));
            }

            EnsureFormalRuleSupportTypesRegistered();
        }

        internal static void EnsureFormalRuleSupportTypesRegistered()
        {
            if (MmcHelper.GetMmcType(nameof(MMCNone)) == null)
            {
                MmcHelper.RegisterMmc(nameof(MMCNone), typeof(MMCNone), typeof(XParamNone));
            }
        }

        internal void RegisterFormalGasAbilityRule(int formalGasAbilityCode)
        {
            if (!m_ownsAbilityComposition || formalGasAbilityCode <= 0 || m_character == null)
            {
                return;
            }

            if (!m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            FormalAbilityRuntimeBootstrap.EnsureInitialized();
            EnsureFormalAbilityRuleLogicRegistered();

            FormalAbilityRuleBinding binding = CreateGeneratedFormalRuleBinding(formalGasAbilityCode);
            if (m_formalRuleBindings.TryGetValue(binding.AbilityCode, out FormalAbilityRuleBinding existingBinding))
            {
                AbilitySpec existingSpec = abilitySystemComponent.Cell.GetAbilitySpec(existingBinding.AbilityCode);
                if (existingSpec != null && existingSpec.IsValid)
                {
                    existingBinding.AbilitySpec = existingSpec;
                    return;
                }

                m_formalRuleBindings.Remove(binding.AbilityCode);
            }

            abilitySystemComponent.Cell.GrantAbility(CreateGeneratedFormalAbilityConfig(binding));

            AbilitySpec abilitySpec = abilitySystemComponent.Cell.GetAbilitySpec(binding.AbilityCode);
            if (abilitySpec == null || !abilitySpec.IsValid)
            {
                Debug.LogError($"[{nameof(CharacterAbilitySet)}] 无法为 EX-GAS Ability [{formalGasAbilityCode}] 建立正式规则 AbilitySpec。", this);
                return;
            }

            RefreshFormalRuleBindingFromSpec(binding, abilitySpec);
            binding.AbilitySpec = abilitySpec;
            m_formalRuleBindings[binding.AbilityCode] = binding;
        }

        internal void UnregisterFormalGasAbilityRule(int formalGasAbilityCode)
        {
            if (!m_ownsAbilityComposition ||
                formalGasAbilityCode <= 0 ||
                !m_formalRuleBindings.TryGetValue(formalGasAbilityCode, out FormalAbilityRuleBinding binding))
            {
                return;
            }

            if (m_character != null &&
                m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) &&
                abilitySystemComponent != null)
            {
                ClearFormalCooldown(binding);
                abilitySystemComponent.Cell.RemoveAbility(binding.AbilityCode);
            }

            m_formalRuleBindings.Remove(formalGasAbilityCode);
        }

        private bool TryEvaluateFormalAbilityActivation(
            int abilityCode,
            out EAbilityFireCheckResult result,
            out bool usesFormalCost)
        {
            result = EAbilityFireCheckResult.Unknown;
            usesFormalCost = false;

            if (!TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out FormalAbilityRuleBinding binding))
            {
                return false;
            }

            usesFormalCost = binding.UsesFormalCost;
            AbilityActivationResult activationResult = abilitySpec.CheckActivation();
            if (activationResult == AbilityActivationResult.Success && HasExactBlockedActivationTag(abilitySpec, binding))
            {
                activationResult = AbilityActivationResult.FailTagRequirement;
            }
            else if (activationResult == AbilityActivationResult.Success && HasExactCooldownTag(binding))
            {
                activationResult = AbilityActivationResult.FailCooldown;
            }

            result = activationResult switch
            {
                AbilityActivationResult.Success => EAbilityFireCheckResult.Valid,
                AbilityActivationResult.FailCost => EAbilityFireCheckResult.NotEnoughMana,
                AbilityActivationResult.FailCooldown => EAbilityFireCheckResult.OnCooldown,
                AbilityActivationResult.FailHasActivated => EAbilityFireCheckResult.Incapacitated,
                AbilityActivationResult.FailTagRequirement => EAbilityFireCheckResult.Incapacitated,
                _ => EAbilityFireCheckResult.Incapacitated
            };

            return true;
        }

        private bool TryCommitFormalAbilityUse(int abilityCode, bool commitFormalCosts, out EAbilityFireCheckResult result)
        {
            result = EAbilityFireCheckResult.Unknown;
            if (!TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out FormalAbilityRuleBinding binding))
            {
                return false;
            }

            AbilityActivationResult activationResult = abilitySpec.CheckActivation();
            if (activationResult == AbilityActivationResult.Success && HasExactBlockedActivationTag(abilitySpec, binding))
            {
                activationResult = AbilityActivationResult.FailTagRequirement;
            }
            else if (activationResult == AbilityActivationResult.Success && HasExactCooldownTag(binding))
            {
                activationResult = AbilityActivationResult.FailCooldown;
            }

            if (activationResult == AbilityActivationResult.FailTagRequirement)
            {
                result = EAbilityFireCheckResult.Incapacitated;
                return true;
            }

            if (activationResult == AbilityActivationResult.FailCost)
            {
                result = EAbilityFireCheckResult.NotEnoughMana;
                return true;
            }

            if (activationResult == AbilityActivationResult.FailCooldown ||
                activationResult == AbilityActivationResult.FailHasActivated)
            {
                result = activationResult == AbilityActivationResult.FailCooldown
                    ? EAbilityFireCheckResult.OnCooldown
                    : EAbilityFireCheckResult.Incapacitated;
                return true;
            }

            if (commitFormalCosts && binding.CommitsFormalCostInProject)
            {
                abilitySpec.DoCost();
            }

            if (commitFormalCosts && binding.CommitsFormalCooldownInProject)
            {
                if (!TryApplyFormalAbilityCooldown(abilityCode))
                {
                    result = EAbilityFireCheckResult.OnCooldown;
                    return true;
                }
            }

            result = EAbilityFireCheckResult.Valid;
            return true;
        }

        private bool TryValidateFormalAbilityUseAtFirePoint(int abilityCode, out EAbilityFireCheckResult result)
        {
            return TryCommitFormalAbilityUse(abilityCode, false, out result);
        }

        private bool BeginFormalAbilityRuleLifecycle(
            int abilityCode,
            AbilityActivationContext activationContext)
        {
            if (!TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out _))
            {
                return false;
            }

            abilitySpec.TryActivate(activationContext);
            return true;
        }

        private void EndFormalAbilityRuleLifecycle(int abilityCode)
        {
            if (TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out _))
            {
                abilitySpec.TryEnd();
            }
        }

        private void CancelFormalAbilityRuleLifecycle(int abilityCode)
        {
            if (TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out _))
            {
                abilitySpec.TryCancel();
            }
        }

        private bool TryGetFormalAbilitySpec(
            int abilityCode,
            out AbilitySpec abilitySpec,
            out FormalAbilityRuleBinding binding)
        {
            abilitySpec = null;
            binding = null;

            if (abilityCode <= 0 || !m_formalRuleBindings.TryGetValue(abilityCode, out binding))
            {
                return false;
            }

            AbilitySpec resolvedSpec = binding.AbilitySpec;
            if ((resolvedSpec == null || !resolvedSpec.IsValid) &&
                m_character != null &&
                m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) &&
                abilitySystemComponent != null)
            {
                resolvedSpec = abilitySystemComponent.Cell.GetAbilitySpec(binding.AbilityCode);
                binding.AbilitySpec = resolvedSpec;
            }

            if (resolvedSpec == null || !resolvedSpec.IsValid)
            {
                return false;
            }

            abilitySpec = resolvedSpec;
            return true;
        }

        private bool TryGetFormalAbilityCooldownState(
            int abilityCode,
            out float remainingCooldown,
            out float cooldownDuration)
        {
            remainingCooldown = 0.0f;
            cooldownDuration = 0.0f;

            if (!TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out FormalAbilityRuleBinding binding))
            {
                return false;
            }

            cooldownDuration = abilitySpec.CheckCooldownExist()
                ? ResolveCooldownSeconds(abilitySpec.GetCooldown())
                : 0.0f;
            if (!binding.TracksCooldownViaGas)
            {
                return false;
            }

            UEntity effectEntity = ResolveActiveCooldownEffectEntity(binding);
            if (effectEntity == UEntity.Null)
            {
                return false;
            }

            if (!TryCalculateRemainingDuration(effectEntity, out remainingCooldown))
            {
                return false;
            }

            if (remainingCooldown <= 0.0f)
            {
                binding.CooldownEffectEntity = UEntity.Null;
                remainingCooldown = 0.0f;
            }

            return true;
        }

        private bool TryApplyFormalAbilityCooldown(
            int abilityCode,
            float durationOverride = 0.0f,
            float defaultCooldownSeconds = 0.0f)
        {
            if (!TryGetFormalAbilitySpec(abilityCode, out AbilitySpec abilitySpec, out FormalAbilityRuleBinding binding))
            {
                return false;
            }

            float cooldownDuration = Mathf.Max(0.0f, durationOverride > 0.0f ? durationOverride : defaultCooldownSeconds);
            if (cooldownDuration <= 0.0f)
            {
                ClearFormalCooldown(binding);
                return true;
            }

            if (!binding.TracksCooldownViaGas)
            {
                return false;
            }

            ClearFormalCooldown(binding);
            if (!abilitySpec.CheckCooldownExist())
            {
                return false;
            }

            int defaultCooldownFrames = ResolveCooldownFrames(defaultCooldownSeconds);
            int overrideCooldownFrames = ResolveCooldownFrames(cooldownDuration);
            int previousCooldownFrames = abilitySpec.GetCooldown();
            abilitySpec.SetCooldown(overrideCooldownFrames);
            abilitySpec.DoCooldown();
            ApplyProjectCooldownTagImmediately(binding);

            if (overrideCooldownFrames != defaultCooldownFrames)
            {
                abilitySpec.SetCooldown(defaultCooldownFrames);
            }
            else if (previousCooldownFrames != defaultCooldownFrames)
            {
                abilitySpec.SetCooldown(defaultCooldownFrames);
            }

            binding.CooldownEffectEntity = ResolveActiveCooldownEffectEntity(binding);
            return true;
        }

        private void ApplyProjectCooldownTagImmediately(FormalAbilityRuleBinding binding)
        {
            if (binding == null ||
                m_character == null ||
                !m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            abilitySystemComponent.Cell.AddFixedTag(binding.CooldownTagCode);
        }

        private bool HasExactCooldownTag(FormalAbilityRuleBinding binding)
        {
            return binding != null &&
                   binding.TracksCooldownViaGas &&
                   HasExactOwnerFixedTag(binding.CooldownTagCode);
        }

        private bool HasExactBlockedActivationTag(AbilitySpec abilitySpec, FormalAbilityRuleBinding binding)
        {
            if (abilitySpec == null ||
                binding == null ||
                !abilitySpec.CheckActivationBlockedTagsExist())
            {
                return false;
            }

            int[] blockedTags = abilitySpec.GetActivationBlockedTags();
            if (blockedTags == null || blockedTags.Length == 0)
            {
                return false;
            }

            foreach (int blockedTag in blockedTags)
            {
                if (HasExactOwnerFixedTag(blockedTag))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasExactOwnerFixedTag(int tag)
        {
            if (m_character == null ||
                !m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return false;
            }

            var fixedTags = ASCHelper.GetDynamicBufferFixedTags(abilitySystemComponent.Cell.Entity);
            foreach (BFixedTag fixedTag in fixedTags)
            {
                if (fixedTag.tag == tag)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearFormalAbilityCooldown(int abilityCode)
        {
            if (abilityCode <= 0 || !m_formalRuleBindings.TryGetValue(abilityCode, out FormalAbilityRuleBinding binding))
            {
                return;
            }

            ClearFormalCooldown(binding);
        }

        private FormalAbilityRuleBinding CreateGeneratedFormalRuleBinding(int formalGasAbilityCode)
        {
            return new FormalAbilityRuleBinding
            {
                AbilityCode = formalGasAbilityCode,
                CooldownTagCode = BuildStablePositiveCode(FormalCooldownTagSeed, $"exgas:{formalGasAbilityCode}:cooldown"),
                UsesFormalCost = false,
                TracksCooldownViaGas = false,
                UsesGeneratedGasAbilityConfig = true,
                CommitsFormalCostInProject = false,
                CommitsFormalCooldownInProject = false,
                CooldownEffectEntity = UEntity.Null
            };
        }

        private AbilityConfig CreateGeneratedFormalAbilityConfig(FormalAbilityRuleBinding binding)
        {
            AbilityConfig generatedConfig = ReflectionHelper.InvokeStaticMethod(
                    "GAS.Runtime.XLuban",
                    "GetAbilityConfig",
                    binding.AbilityCode) as AbilityConfig;
            List<AbilityComponentConfig> configs = generatedConfig?.ComponentConfigs != null
                ? new List<AbilityComponentConfig>(generatedConfig.ComponentConfigs)
                : new List<AbilityComponentConfig>();

            return new AbilityConfig(configs.ToArray());
        }

        private static bool HasComponentConfig<T>(IEnumerable<AbilityComponentConfig> configs)
            where T : AbilityComponentConfig
        {
            if (configs == null)
            {
                return false;
            }

            foreach (AbilityComponentConfig config in configs)
            {
                if (config is T)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshFormalRuleBindingFromSpec(
            FormalAbilityRuleBinding binding,
            AbilitySpec abilitySpec)
        {
            if (binding == null || abilitySpec == null || !abilitySpec.IsValid)
            {
                return;
            }

            binding.UsesFormalCost = abilitySpec.CheckCostExist();
            binding.TracksCooldownViaGas = abilitySpec.CheckCooldownExist();

            if (!binding.UsesGeneratedGasAbilityConfig)
            {
                binding.CommitsFormalCostInProject = binding.UsesFormalCost;
                binding.CommitsFormalCooldownInProject = binding.TracksCooldownViaGas;
                return;
            }

            binding.CommitsFormalCostInProject = false;
            binding.CommitsFormalCooldownInProject = binding.TracksCooldownViaGas && binding.CommitsFormalCooldownInProject;
        }

        private GameplayEffectComponentConfig[] CreateCooldownEffectConfig(FormalAbilityRuleBinding binding)
        {
            return new GameplayEffectComponentConfig[]
            {
                new ConfDuration
                {
                    duration = 1,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = true,
                    StopTickWhenDeactivated = false
                },
                new ConfEffectGrantedTags
                {
                    tags = new[] { binding.CooldownTagCode }
                }
            };
        }

        private void ClearFormalCooldown(FormalAbilityRuleBinding binding)
        {
            if (m_character == null ||
                !m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            foreach (UEntity effectEntity in EnumerateActiveCooldownEffectEntities(abilitySystemComponent.Cell, binding.CooldownTagCode))
            {
                if (effectEntity != UEntity.Null && GASManager.EntityManager.Exists(effectEntity))
                {
                    GameplayEffectHelper.RemoveGameplayEffect(effectEntity);
                }
            }

            abilitySystemComponent.Cell.KillFixedTag(binding.CooldownTagCode);
            binding.CooldownEffectEntity = UEntity.Null;
        }

        private bool TryCalculateRemainingDuration(UEntity effectEntity, out float remainingDurationSeconds)
        {
            remainingDurationSeconds = 0.0f;
            if (effectEntity == UEntity.Null ||
                !GASManager.IsInitialized ||
                !GASManager.EntityManager.Exists(effectEntity) ||
                !GASManager.EntityManager.HasComponent<CDuration>(effectEntity))
            {
                return false;
            }

            CDuration duration = GASManager.EntityManager.GetComponentData<CDuration>(effectEntity);
            int totalFrames = duration.duration;
            if (totalFrames <= 0)
            {
                return false;
            }

            int startFrame = duration.activeTime;
            int currentFrame = GASManager.CurrentFrame;
            int elapsedFrames = Mathf.Max(0, currentFrame - startFrame);
            int remainingFrames = Mathf.Max(0, totalFrames - elapsedFrames);
            remainingDurationSeconds = remainingFrames * Time.fixedDeltaTime;
            return true;
        }

        private UEntity ResolveActiveCooldownEffectEntity(FormalAbilityRuleBinding binding)
        {
            if (binding == null || m_character == null)
            {
                return UEntity.Null;
            }

            UEntity cachedEntity = binding.CooldownEffectEntity;
            if (IsMatchingCooldownEffectEntity(cachedEntity, binding.CooldownTagCode))
            {
                return cachedEntity;
            }

            if (!m_character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                binding.CooldownEffectEntity = UEntity.Null;
                return UEntity.Null;
            }

            foreach (UEntity effectEntity in EnumerateActiveCooldownEffectEntities(abilitySystemComponent.Cell, binding.CooldownTagCode))
            {
                binding.CooldownEffectEntity = effectEntity;
                return effectEntity;
            }

            binding.CooldownEffectEntity = UEntity.Null;
            return UEntity.Null;
        }

        private static IEnumerable<UEntity> EnumerateActiveCooldownEffectEntities(AbilitySystemCell abilitySystemCell, int cooldownTagCode)
        {
            if (abilitySystemCell == null || !GASManager.IsInitialized)
            {
                yield break;
            }

            var entityManager = GASManager.EntityManager;
            var gameplayEffects = entityManager.GetBuffer<BGameplayEffect>(abilitySystemCell.Entity);
            foreach (BGameplayEffect gameplayEffect in gameplayEffects)
            {
                UEntity effectEntity = gameplayEffect.GameplayEffect;
                if (IsMatchingCooldownEffectEntity(effectEntity, cooldownTagCode))
                {
                    yield return effectEntity;
                }
            }
        }

        private static bool IsMatchingCooldownEffectEntity(UEntity effectEntity, int cooldownTagCode)
        {
            if (effectEntity == UEntity.Null || !GASManager.IsInitialized)
            {
                return false;
            }

            var entityManager = GASManager.EntityManager;
            if (!entityManager.Exists(effectEntity) ||
                !entityManager.HasComponent<CEffectGrantedTags>(effectEntity) ||
                entityManager.HasComponent<CEffectDestroy>(effectEntity))
            {
                return false;
            }

            var grantedTags = entityManager.GetComponentData<CEffectGrantedTags>(effectEntity).tags;
            foreach (int grantedTag in grantedTags)
            {
                if (grantedTag == cooldownTagCode || TagHelper.HasTag(grantedTag, cooldownTagCode))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveCooldownFrames(float cooldownSeconds)
        {
            if (cooldownSeconds <= 0.0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(cooldownSeconds / Mathf.Max(Time.fixedDeltaTime, 0.0001f));
        }

        private static float ResolveCooldownSeconds(int cooldownFrames)
        {
            return Mathf.Max(0, cooldownFrames) * Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        }

        private static int BuildStablePositiveCode(int seed, string key)
        {
            unchecked
            {
                int hash = seed;
                for (int i = 0; i < key.Length; i++)
                {
                    hash = (hash * 31) + key[i];
                }

                if (hash == int.MinValue)
                {
                    hash = int.MaxValue;
                }

                hash = Math.Abs(hash);
                return hash == 0 ? seed : hash;
            }
        }
    }
}
