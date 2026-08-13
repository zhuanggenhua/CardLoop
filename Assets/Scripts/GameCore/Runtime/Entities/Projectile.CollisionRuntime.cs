using System;
using GAS.Runtime;
using Unity.Entities;
using UnityEngine;
using UEntity = Unity.Entities.Entity;

namespace GameCore
{
    public partial class Projectile
    {
        /// <summary>
        /// 投射物碰撞只负责命中判定和终止时机，不直接篡改爆炸和存档真相。
        /// </summary>
        private void OnCollision(CharacterBase primaryTarget = null)
        {
            if (m_collisionSound)
            {
                YokiFrame.EventKit.Type.Send(new AudioPlaybackRequestedEvent(m_collisionSound));
            }

            Terminate(primaryTarget);
        }

        private void HandleCollision(GameObject target)
        {
            CharacterBase character = target.GetComponentInParent<CharacterBase>();

            if (character)
            {
                ApplyImpactGameplayEffect(
                    character,
                    EEffectImpactDataType.Velocity,
                    m_direction);
                OnCollision(character);
            }
            else
            {
                OnCollision();
            }
        }

        private void ApplyImpactGameplayEffect(
            CharacterBase target,
            EEffectImpactDataType impactDataType,
            Vector2 impactData)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            int gameplayEffectId = ProjectileLaunchParameters.RequireGameplayEffectId(
                m_impactGameplayEffectId);
            GameplayEffectConfig effectConfig = GameplayEffectHelper.GetConfigByID(gameplayEffectId)
                ?? throw new InvalidOperationException(
                    $"投射物 {name} 引用的 EX-GAS GameplayEffect {gameplayEffectId} 不存在。");

            UEntity targetAscEntity = RequireAbilitySystemEntity(target);
            UEntity sourceAscEntity = m_source != null
                ? RequireAbilitySystemEntity(m_source)
                : UEntity.Null;
            UEntity gameplayEffect = effectConfig.CreateGameplayEffectEntity();
            EntityHelper.AddManagedComponent<MCGameplayEffectImpactOverride>(gameplayEffect);
            EntityHelper.SetManagedComponent(
                gameplayEffect,
                new MCGameplayEffectImpactOverride(impactDataType, impactData));
            GameplayEffectHelper.ApplyGameplayEffectTo(
                gameplayEffect,
                targetAscEntity,
                sourceAscEntity);
        }

        private static UEntity RequireAbilitySystemEntity(CharacterBase character)
        {
            if (!character.TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystem) ||
                abilitySystem?.Cell == null)
            {
                throw new InvalidOperationException(
                    $"角色 {character.name} 的正式 AbilitySystemComponent 尚未初始化，无法施加投射物命中效果。");
            }

            return abilitySystem.Cell.Entity;
        }

        private bool TryColliding(GameObject target)
        {
            if (target.layer == LayerMask.NameToLayer(GameManager.Config.hitboxLayer))
            {
                if (m_operating && target != gameObject)
                {
                    HandleCollision(target);
                    return true;
                }
            }

            return false;
        }

        private bool IsProperCollider(int layer)
        {
            int layermask = GameManager.Config.collisionContactFilter.layerMask;
            return layermask == (layermask | (1 << layer));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!TryColliding(collision.gameObject) && IsProperCollider(collision.gameObject.layer))
            {
                OnCollision();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            TryColliding(collision.gameObject);
        }
    }
}
