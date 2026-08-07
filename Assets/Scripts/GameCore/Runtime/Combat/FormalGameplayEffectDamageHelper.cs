using GAS.Runtime;
using Unity.Entities;
using UnityEngine;
using UEntity = Unity.Entities.Entity;

namespace GameCore
{
    public static class FormalGameplayEffectDamageHelper
    {
        public static bool TryApplyDamage(
            CharacterBase source,
            CharacterBase target,
            FormalDamageEffectPayload payload)
        {
            if (target == null || payload.isConfigured == false)
            {
                return false;
            }

            if (!TryResolveAbilitySystem(target, out AbilitySystemComponent targetAsc))
            {
                return false;
            }

            AbilitySystemComponent sourceAsc = null;
            if (source != null && !TryResolveAbilitySystem(source, out sourceAsc))
            {
                sourceAsc = null;
            }

            UEntity sourceAscEntity = sourceAsc != null
                ? sourceAsc.Cell.Entity
                : UEntity.Null;

            UEntity gameplayEffect = GameplayEffectHelper.CreateGameplayEffectEntity(
                new GameplayEffectComponentConfig[]
                {
                    new MCConfFormalDamageEffect
                    {
                        payload = payload
                    }
                });

            GameplayEffectHelper.ApplyGameplayEffectTo(gameplayEffect, targetAsc.Cell.Entity, sourceAscEntity);
            return true;
        }

        public static Vector2 ResolveImpactVector(
            CharacterBase source,
            CharacterBase target,
            EEffectImpactDataType impactDataType,
            Vector2 impactData)
        {
            if (target == null)
            {
                return Vector2.zero;
            }

            return impactDataType switch
            {
                EEffectImpactDataType.Velocity => impactData,
                EEffectImpactDataType.SourcePosition => (Vector2)target.transform.position -
                                                       (source != null ? (Vector2)source.transform.position : impactData),
                _ => Vector2.zero
            };
        }

        private static bool TryResolveAbilitySystem(CharacterBase character, out AbilitySystemComponent abilitySystem)
        {
            abilitySystem = null;
            return character != null &&
                   character.TryGetFormalAbilitySystem(out abilitySystem) &&
                   abilitySystem != null;
        }
    }
}
