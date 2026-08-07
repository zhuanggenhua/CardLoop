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
    internal sealed partial class SExecuteFormalDamageEffectsManaged : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<CEffectInstance>();
            RequireForUpdate<CEffectInUsage>();
            RequireForUpdate<WipApplyEffect>();
        }

        protected override void OnUpdate()
        {
            foreach (var (_, formalDamage, inUsage) in SystemAPI
                         .Query<RefRO<CEffectInstance>, MCFormalDamageEffect, RefRO<CEffectInUsage>>()
                         .WithNone<CDuration>()
                         .WithAll<WipApplyEffect>())
            {
                if (!TryResolveCharacters(inUsage.ValueRO, out CharacterBase sourceCharacter, out CharacterBase targetCharacter))
                {
                    continue;
                }

                FormalDamageEffectPayload payload = formalDamage.Payload;
                if (!payload.isConfigured || targetCharacter == null)
                {
                    continue;
                }

                DamageOutputDescriptor output = DamageSolver.SolveDamageOutput(sourceCharacter, payload.damageDescriptor);
                Vector2? impactVelocity = ResolveImpactVelocity(payload, sourceCharacter, targetCharacter);
                targetCharacter.Damage(output, payload.visualFlags, impactVelocity, payload.damageImpact);
            }

            foreach (var (_, conditionalDamage, inUsage) in SystemAPI
                         .Query<RefRO<CEffectInstance>, MCFormalConditionalDamageEffect, RefRO<CEffectInUsage>>()
                         .WithNone<CDuration>()
                         .WithAll<WipApplyEffect>())
            {
                if (!TryResolveCharacters(inUsage.ValueRO, out CharacterBase sourceCharacter, out CharacterBase targetCharacter))
                {
                    continue;
                }

                FormalConditionalDamageEffectPayload payload = conditionalDamage.Payload;
                if (!payload.isConfigured || targetCharacter == null)
                {
                    continue;
                }

                if (!IsConditionMatched(payload.Condition, sourceCharacter, targetCharacter))
                {
                    continue;
                }

                DamageOutputDescriptor output = DamageSolver.SolveDamageOutput(sourceCharacter, payload.Damage.damageDescriptor);
                Vector2? impactVelocity = ResolveImpactVelocity(payload.Damage, sourceCharacter, targetCharacter);
                targetCharacter.Damage(output, payload.Damage.visualFlags, impactVelocity, payload.Damage.damageImpact);
            }
        }

        private static bool TryResolveCharacters(
            CEffectInUsage inUsage,
            out CharacterBase sourceCharacter,
            out CharacterBase targetCharacter)
        {
            sourceCharacter = ResolveCharacter(inUsage.Source);
            targetCharacter = ResolveCharacter(inUsage.Target);
            return targetCharacter != null;
        }

        private static CharacterBase ResolveCharacter(UEntity ascEntity)
        {
            AbilitySystemCell abilitySystemCell = GASManager.GetAscFromEntity(ascEntity);
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

        private static bool IsConditionMatched(
            FormalDamageConditionPayload condition,
            CharacterBase sourceCharacter,
            CharacterBase targetCharacter)
        {
            return condition.Kind switch
            {
                EFormalDamageConditionKind.None => true,
                EFormalDamageConditionKind.Backstab => IsBackstab(sourceCharacter, targetCharacter, condition.FacingDotThreshold),
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

        private static Vector2? ResolveImpactVelocity(
            FormalDamageEffectPayload payload,
            CharacterBase sourceCharacter,
            CharacterBase targetCharacter)
        {
            return payload.impactDataType switch
            {
                EEffectImpactDataType.Velocity => payload.impactData,
                EEffectImpactDataType.SourcePosition => (Vector2)targetCharacter.transform.position -
                                                       (sourceCharacter != null ? (Vector2)sourceCharacter.transform.position : payload.impactData),
                _ => null
            };
        }
    }
}
