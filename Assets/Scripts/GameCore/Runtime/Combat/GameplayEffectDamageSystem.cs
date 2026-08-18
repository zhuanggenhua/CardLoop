using System.Collections.Generic;
using GAS.Runtime;
using Unity.Entities;
using UnityEngine;
using UEntity = Unity.Entities.Entity;

namespace GameCore
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SGInstantEffect))]
    [UpdateAfter(typeof(SExecuteInstantEffectModifiers))]
    [UpdateBefore(typeof(SExecuteInstantEffectEnd))]
    internal sealed partial class SExecuteGameplayEffectDamageManaged : SystemBase
    {
        private readonly List<PendingDamageApplication> m_pendingApplications = new();

        private readonly struct PendingDamageApplication
        {
            public PendingDamageApplication(
                AbilitySystemCell targetAbilitySystem,
                CharacterBase target,
                DamageOutputDescriptor output,
                EEffectVisualFlags visualFlags,
                Vector2? impactVelocity,
                DamageImpactSettings impactSettings)
            {
                TargetAbilitySystem = targetAbilitySystem;
                Target = target;
                Output = output;
                VisualFlags = visualFlags;
                ImpactVelocity = impactVelocity;
                ImpactSettings = impactSettings;
            }

            public AbilitySystemCell TargetAbilitySystem { get; }
            public CharacterBase Target { get; }
            public DamageOutputDescriptor Output { get; }
            public EEffectVisualFlags VisualFlags { get; }
            public Vector2? ImpactVelocity { get; }
            public DamageImpactSettings ImpactSettings { get; }
        }

        protected override void OnCreate()
        {
            RequireForUpdate<CEffectInstance>();
            RequireForUpdate<CEffectInUsage>();
            RequireForUpdate<WipApplyEffect>();
        }

        protected override void OnUpdate()
        {
            m_pendingApplications.Clear();

            foreach (var (_, formalDamage, inUsage, effectEntity) in SystemAPI
                         .Query<RefRO<CEffectInstance>, MCGameplayEffectDamage, RefRO<CEffectInUsage>>()
                         .WithNone<CDuration>()
                         .WithAll<WipApplyEffect>()
                         .WithEntityAccess())
            {
                if (!TryResolveAbilitySystems(
                        inUsage.ValueRO,
                        out AbilitySystemCell sourceAbilitySystem,
                        out AbilitySystemCell targetAbilitySystem))
                {
                    continue;
                }

                CharacterBase sourceCharacter = ResolveCharacter(sourceAbilitySystem);
                CharacterBase targetCharacter = ResolveCharacter(targetAbilitySystem);

                GameplayEffectDamagePayload payload = formalDamage.Payload;
                if (!payload.isConfigured)
                {
                    continue;
                }

                DamageResolutionRolls rolls = CreateResolutionRolls(
                    effectEntity,
                    inUsage.ValueRO.Source,
                    inUsage.ValueRO.Target,
                    0x6A09E667u);
                DamageOutputDescriptor output = sourceCharacter != null
                    ? DamageSolver.SolveDamageOutput(sourceCharacter, payload.damageDescriptor, rolls)
                    : DamageSolver.SolveDamageOutput(sourceAbilitySystem, payload.damageDescriptor, rolls);
                Vector2? impactVelocity = ResolveImpactVelocity(
                    effectEntity,
                    payload,
                    sourceCharacter,
                    targetCharacter);
                m_pendingApplications.Add(new PendingDamageApplication(
                    targetAbilitySystem,
                    targetCharacter,
                    output,
                    payload.visualFlags,
                    impactVelocity,
                    payload.damageImpact));
            }

            foreach (var (_, conditionalDamage, inUsage, effectEntity) in SystemAPI
                         .Query<RefRO<CEffectInstance>, MCGameplayEffectConditionalDamage, RefRO<CEffectInUsage>>()
                         .WithNone<CDuration>()
                         .WithAll<WipApplyEffect>()
                         .WithEntityAccess())
            {
                if (!TryResolveAbilitySystems(
                        inUsage.ValueRO,
                        out AbilitySystemCell sourceAbilitySystem,
                        out AbilitySystemCell targetAbilitySystem))
                {
                    continue;
                }

                CharacterBase sourceCharacter = ResolveCharacter(sourceAbilitySystem);
                CharacterBase targetCharacter = ResolveCharacter(targetAbilitySystem);

                GameplayEffectConditionalDamagePayload payload = conditionalDamage.Payload;
                if (!payload.isConfigured)
                {
                    continue;
                }

                if (!IsConditionMatched(payload.Condition, sourceCharacter, targetCharacter))
                {
                    continue;
                }

                DamageResolutionRolls rolls = CreateResolutionRolls(
                    effectEntity,
                    inUsage.ValueRO.Source,
                    inUsage.ValueRO.Target,
                    0xBB67AE85u);
                DamageOutputDescriptor output = sourceCharacter != null
                    ? DamageSolver.SolveDamageOutput(sourceCharacter, payload.Damage.damageDescriptor, rolls)
                    : DamageSolver.SolveDamageOutput(sourceAbilitySystem, payload.Damage.damageDescriptor, rolls);
                Vector2? impactVelocity = ResolveImpactVelocity(
                    effectEntity,
                    payload.Damage,
                    sourceCharacter,
                    targetCharacter);
                m_pendingApplications.Add(new PendingDamageApplication(
                    targetAbilitySystem,
                    targetCharacter,
                    output,
                    payload.Damage.visualFlags,
                    impactVelocity,
                    payload.Damage.damageImpact));
            }

            foreach (PendingDamageApplication application in m_pendingApplications)
            {
                if (application.Target != null)
                {
                    application.Target.Damage(
                        application.Output,
                        application.VisualFlags,
                        application.ImpactVelocity,
                        application.ImpactSettings);
                }
                else
                {
                    ApplyDamageToAbilitySystem(
                        application.TargetAbilitySystem,
                        application.Output,
                        application.VisualFlags);
                }
            }
        }

        private static bool TryResolveAbilitySystems(
            CEffectInUsage inUsage,
            out AbilitySystemCell sourceAbilitySystem,
            out AbilitySystemCell targetAbilitySystem)
        {
            sourceAbilitySystem = GASManager.GetAscFromEntity(inUsage.Source);
            targetAbilitySystem = GASManager.GetAscFromEntity(inUsage.Target);
            return targetAbilitySystem != null;
        }

        private static CharacterBase ResolveCharacter(AbilitySystemCell abilitySystemCell)
        {
            if (abilitySystemCell == null)
            {
                return null;
            }

            GameObject owner = abilitySystemCell.GameObject;
            if (owner == null)
            {
                return null;
            }

            return owner.GetComponent<CharacterBase>();
        }

        private static void ApplyDamageToAbilitySystem(
            AbilitySystemCell targetAbilitySystem,
            DamageOutputDescriptor output,
            EEffectVisualFlags visualFlags)
        {
            DamageInputDescriptor input = DamageSolver.SolveDamageInput(targetAbilitySystem, output);
            int currentHealth = Mathf.RoundToInt(targetAbilitySystem.GetAttrCurrentValue(
                CharacterAttributes.SetCode,
                CharacterAttributes.Health));
            int appliedDamage = Mathf.Min(Mathf.Max(0, input.damage), currentHealth);
            if (appliedDamage <= 0 && !input.IsMissed)
            {
                return;
            }

            if (appliedDamage > 0)
            {
                float baseHealth = targetAbilitySystem.GetAttrBaseValue(
                    CharacterAttributes.SetCode,
                    CharacterAttributes.Health);
                targetAbilitySystem.SetAttrBaseValue(
                    CharacterAttributes.SetCode,
                    CharacterAttributes.Health,
                    baseHealth - appliedDamage);
                AttributeHelper.RecalculateCurrentValue(
                    targetAbilitySystem.Entity,
                    CharacterAttributes.SetCode,
                    CharacterAttributes.Health);
            }

            YokiFrame.EventKit.Type.Send(new AbilitySystemDamageResolvedPresentationEvent(
                targetAbilitySystem,
                appliedDamage,
                input.IsMissed,
                input.IsCriticalHit,
                input.silent,
                output.type,
                visualFlags,
                input.matchupResult));
        }

        private static bool IsConditionMatched(
            GameplayEffectDamageCondition condition,
            CharacterBase sourceCharacter,
            CharacterBase targetCharacter)
        {
            return condition.Kind switch
            {
                EDamageConditionKind.None => true,
                EDamageConditionKind.Backstab => IsBackstab(sourceCharacter, targetCharacter, condition.FacingDotThreshold),
                _ => false
            };
        }

        private static bool IsBackstab(
            CharacterBase sourceCharacter,
            CharacterBase targetCharacter,
            float facingDotThreshold)
        {
            if (sourceCharacter == null || targetCharacter == null)
            {
                return false;
            }

            Vector2 targetFacing = targetCharacter.GetTargetDirection();
            if (targetFacing.sqrMagnitude <= 0.0001f)
            {
                targetFacing = Vector2.right;
            }

            Vector2 targetToAttacker = (Vector2)(sourceCharacter.transform.position - targetCharacter.transform.position);
            if (targetToAttacker.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            targetFacing.Normalize();
            targetToAttacker.Normalize();
            return Vector2.Dot(targetFacing, targetToAttacker) <= facingDotThreshold;
        }

        private static DamageResolutionRolls CreateResolutionRolls(
            UEntity effectEntity,
            UEntity sourceEntity,
            UEntity targetEntity,
            uint salt)
        {
			uint seed;
			if (GASManager.EntityManager.HasComponent<CEffectAuthoritativeRandomSeed>(effectEntity))
			{
				uint effectSeed = GASManager.EntityManager
					.GetComponentData<CEffectAuthoritativeRandomSeed>(effectEntity)
					.Value;
				seed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(effectSeed, salt));
			}
			else
			{
				// 尚未迁入牌桌聚合的 2D 场景能力仍走旧本地种子；牌桌战斗不会进入此分支。
				seed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint4(
					unchecked((uint)effectEntity.Index),
					unchecked((uint)effectEntity.Version),
					unchecked((uint)(sourceEntity.Index ^ targetEntity.Index)),
					salt));
			}
            Unity.Mathematics.Random random = new(seed == 0u ? 1u : seed);
            return new DamageResolutionRolls(
                random.NextFloat(0.0f, 100.0f),
                random.NextFloat(0.0f, 100.0f));
        }

        private Vector2? ResolveImpactVelocity(
            UEntity effectEntity,
            GameplayEffectDamagePayload payload,
            CharacterBase sourceCharacter,
            CharacterBase targetCharacter)
        {
            if (EntityManager.HasComponent<MCGameplayEffectImpactOverride>(effectEntity))
            {
                MCGameplayEffectImpactOverride impactOverride =
                    EntityHelper.GetManagedComponentData<MCGameplayEffectImpactOverride>(effectEntity);
                return ResolveImpactData(
                    impactOverride.ImpactDataType,
                    impactOverride.ImpactData,
                    sourceCharacter,
                    targetCharacter);
            }

            return ResolveImpactData(
                payload.impactDataType,
                payload.impactData,
                sourceCharacter,
                targetCharacter);
        }

        private static Vector2? ResolveImpactData(
            EEffectImpactDataType impactDataType,
            Vector2 impactData,
            CharacterBase sourceCharacter,
            CharacterBase targetCharacter)
        {
            return impactDataType switch
            {
                EEffectImpactDataType.Velocity => impactData,
                EEffectImpactDataType.SourcePosition when targetCharacter != null =>
                    (Vector2)targetCharacter.transform.position -
                    (sourceCharacter != null ? (Vector2)sourceCharacter.transform.position : impactData),
                _ => null
            };
        }
    }
}
