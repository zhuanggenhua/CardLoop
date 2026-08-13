using System;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 在 EX-GAS World 关闭前释放仍由 ECS 组件持有的 Persistent 原生容器。
    /// 正常运行中的组件增删仍由各自系统负责，本入口只承担 World 最终析构。
    /// </summary>
    internal static class NativeContainerCleanup
    {
        internal static void DisposeAll(EntityManager entityManager)
        {
            DisposeArrayComponents<CAbilityActivationOwnedTags, int>(entityManager, component => component.tags);
            DisposeArrayComponents<CAbilityAssetTags, int>(entityManager, component => component.tags);
            DisposeArrayComponents<CBlockAbilityWithTags, int>(entityManager, component => component.tags);
            DisposeArrayComponents<CCancelAbilityWithTags, int>(entityManager, component => component.tags);
            DisposeArrayComponents<CAbilityCooldown, int>(entityManager, component => component.CooldownTags);
            DisposeRequirements<CAbilityActivationBlockedTags>(entityManager, component => component.requirement);
            DisposeRequirements<CAbilityActivationRequiredTags>(entityManager, component => component.requirement);

            DisposeArrayComponents<CApplicationCondition, int>(entityManager, component => component.conditions);
            DisposeArrayComponents<CEffectAssetTags, int>(entityManager, component => component.tags);
            DisposeArrayComponents<CEffectGrantedTags, int>(entityManager, component => component.tags);
            DisposeArrayComponents<CPeriod, Entity>(entityManager, component => component.GameplayEffects);
            DisposeArrayComponents<CStacking, Entity>(entityManager, component => component.overflowEffects);
            DisposeRequirements<CApplicationRequiredTags>(entityManager, component => component.requirement);
            DisposeRequirements<CEffectImmunityTags>(entityManager, component => component.requirement);
            DisposeRequirements<COngoingRequiredTags>(entityManager, component => component.requirement);
            DisposeRequirements<CRemoveEffectWithTags>(entityManager, component => component.requirement);

            DisposeArrayComponents<CCueOnApply, Entity>(entityManager, component => component.cues);
            DisposeArrayComponents<CCueOnAdd, Entity>(entityManager, component => component.cues);
            DisposeArrayComponents<CCueOnRemove, Entity>(entityManager, component => component.cues);
            DisposeArrayComponents<CCueOnActivate, Entity>(entityManager, component => component.cues);
            DisposeArrayComponents<CCueOnDeactivate, Entity>(entityManager, component => component.cues);
            DisposeArrayComponents<CCueOnTick, Entity>(entityManager, component => component.cues);
            DisposeRequirements<CPlayImmunitedTags>(entityManager, component => component.requirement);
            DisposeRequirements<CPlayRequiredTags>(entityManager, component => component.requirement);
        }

        /// <summary>释放一个即将销毁的 GameplayEffect 实体直接拥有的原生容器。</summary>
        internal static void DisposeGameplayEffect(EntityManager entityManager, Entity gameplayEffect)
        {
            DisposeArrayComponent<CApplicationCondition, int>(
                entityManager, gameplayEffect, component => component.conditions);
            DisposeArrayComponent<CEffectAssetTags, int>(
                entityManager, gameplayEffect, component => component.tags);
            DisposeArrayComponent<CEffectGrantedTags, int>(
                entityManager, gameplayEffect, component => component.tags);
            DisposeArrayComponent<CPeriod, Entity>(
                entityManager, gameplayEffect, component => component.GameplayEffects);
            DisposeArrayComponent<CStacking, Entity>(
                entityManager, gameplayEffect, component => component.overflowEffects);
            DisposeRequirement<CApplicationRequiredTags>(
                entityManager, gameplayEffect, component => component.requirement);
            DisposeRequirement<CEffectImmunityTags>(
                entityManager, gameplayEffect, component => component.requirement);
            DisposeRequirement<COngoingRequiredTags>(
                entityManager, gameplayEffect, component => component.requirement);
            DisposeRequirement<CRemoveEffectWithTags>(
                entityManager, gameplayEffect, component => component.requirement);
            DisposeArrayComponent<CCueOnApply, Entity>(
                entityManager, gameplayEffect, component => component.cues);
            DisposeArrayComponent<CCueOnAdd, Entity>(
                entityManager, gameplayEffect, component => component.cues);
            DisposeArrayComponent<CCueOnRemove, Entity>(
                entityManager, gameplayEffect, component => component.cues);
            DisposeArrayComponent<CCueOnActivate, Entity>(
                entityManager, gameplayEffect, component => component.cues);
            DisposeArrayComponent<CCueOnDeactivate, Entity>(
                entityManager, gameplayEffect, component => component.cues);
            DisposeArrayComponent<CCueOnTick, Entity>(
                entityManager, gameplayEffect, component => component.cues);
        }

        private static void DisposeArrayComponents<TComponent, TValue>(
            EntityManager entityManager,
            Func<TComponent, NativeArray<TValue>> selector)
            where TComponent : unmanaged, IComponentData
            where TValue : unmanaged
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TComponent>());
            using NativeArray<TComponent> components =
                query.ToComponentDataArray<TComponent>(Allocator.Temp);
            for (int index = 0; index < components.Length; index++)
            {
                NativeArray<TValue> values = selector(components[index]);
                if (values.IsCreated)
                {
                    values.Dispose();
                }
            }
        }

        private static void DisposeRequirements<TComponent>(
            EntityManager entityManager,
            Func<TComponent, TagRequirementData> selector)
            where TComponent : unmanaged, IComponentData
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TComponent>());
            using NativeArray<TComponent> components =
                query.ToComponentDataArray<TComponent>(Allocator.Temp);
            for (int index = 0; index < components.Length; index++)
            {
                TagRequirementData requirement = selector(components[index]);
                if (requirement.all.IsCreated)
                {
                    requirement.all.Dispose();
                }
                if (requirement.any.IsCreated)
                {
                    requirement.any.Dispose();
                }
                if (requirement.none.IsCreated)
                {
                    requirement.none.Dispose();
                }
            }
        }

        private static void DisposeArrayComponent<TComponent, TValue>(
            EntityManager entityManager,
            Entity entity,
            Func<TComponent, NativeArray<TValue>> selector)
            where TComponent : unmanaged, IComponentData
            where TValue : unmanaged
        {
            if (!entityManager.HasComponent<TComponent>(entity))
            {
                return;
            }

            NativeArray<TValue> values = selector(entityManager.GetComponentData<TComponent>(entity));
            if (values.IsCreated)
            {
                values.Dispose();
            }
        }

        private static void DisposeRequirement<TComponent>(
            EntityManager entityManager,
            Entity entity,
            Func<TComponent, TagRequirementData> selector)
            where TComponent : unmanaged, IComponentData
        {
            if (!entityManager.HasComponent<TComponent>(entity))
            {
                return;
            }

            TagRequirementData requirement = selector(entityManager.GetComponentData<TComponent>(entity));
            if (requirement.all.IsCreated)
            {
                requirement.all.Dispose();
            }
            if (requirement.any.IsCreated)
            {
                requirement.any.Dispose();
            }
            if (requirement.none.IsCreated)
            {
                requirement.none.Dispose();
            }
        }
    }
}
