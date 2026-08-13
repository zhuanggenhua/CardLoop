using System;
using System.Linq;
using UnityEngine;

namespace GameCore
{
    public partial class Projectile
    {
        private static CharacterBase[] CreateExplosionTargetSnapshot(
            CharacterBase[] characters,
            CharacterBase primaryTarget,
            bool ignorePrimaryTarget)
        {
            if (characters == null || characters.Length == 0)
            {
                return Array.Empty<CharacterBase>();
            }

            return characters
                .Where(character => character != null && (!ignorePrimaryTarget || character != primaryTarget))
                .Distinct()
                .ToArray();
        }

        private void ApplyExplosionImpactEffect(Vector2 explosionOrigin, CharacterBase[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return;
            }

            foreach (CharacterBase target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                ApplyImpactGameplayEffect(
                    target,
                    EEffectImpactDataType.Velocity,
                    (Vector2)target.transform.position - explosionOrigin);
            }
        }

        private void HandleExplosion(CharacterBase primaryTarget)
        {
            if (m_explosionRadius <= 0.0f)
            {
                return;
            }

            if (!m_hasDestroyAnimation)
            {
                Debug.LogWarning("This projectile has an explosion radius but no destroy animation. The explosion may not be visible.");
            }

            Vector2 explosionOrigin = transform.position;
            CharacterBase[] characters = Physics2D.OverlapCircleAll(explosionOrigin, m_explosionRadius)
                .Select(collider => collider.GetComponentInParent<CharacterBase>())
                .Where(character => character != null)
                .Distinct()
                .ToArray();

            if (m_explosionApplyImpactEffect)
            {
                ApplyExplosionImpactEffect(
                    explosionOrigin,
                    CreateExplosionTargetSnapshot(characters, primaryTarget, m_explosionImpactEffectIgnorePrimaryTarget));
            }
        }
    }
}
