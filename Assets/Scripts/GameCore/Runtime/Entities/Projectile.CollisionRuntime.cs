using UnityEngine;

namespace FantasyWord.GameCore
{
    public partial class Projectile
    {
        /// <summary>
        /// 投射物碰撞只负责命中判定和终止时机，不直接篡改爆炸和存档真相。
        /// </summary>
        private void OnCollision(CharacterBase primaryTarget = null)
        {
            GameRuntimeEvents.RequestAudioPlayback(m_collisionSound);
            Terminate(primaryTarget);
        }

        private void HandleCollision(GameObject target)
        {
            CharacterBase character = target.GetComponentInParent<CharacterBase>();

            if (character)
            {
                EffectImpactSettings impactSettings = new()
                {
                    impactDataType = EEffectImpactDataType.Velocity,
                    impactData = m_direction
                };
                bool applied = FormalGameplayEffectDamageHelper.TryApplyDamage(
                    m_source,
                    character,
                    new FormalDamageEffectPayload(
                        m_baseDamage.damageDescriptor,
                        m_baseDamage.visualFlags,
                        impactSettings.damageImpact,
                        impactSettings.impactDataType,
                        impactSettings.impactData));

                // 只要命中闭包里至少有一项不是“不适用”，就视为有效碰撞。
                if (applied)
                {
                    OnCollision(character);
                }
            }
            else
            {
                OnCollision();
            }
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
