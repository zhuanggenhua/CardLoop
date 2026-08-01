using UnityEngine;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    public sealed class CharacterHandleWeapon : MonoBehaviour
    {
        [Header("Weapon Handling")]
        [SerializeField] private CharacterBase m_character = null;
        [SerializeField] private Transform m_weaponAttachment = null;
        [SerializeField] private Transform m_projectileSpawn = null;

        public CharacterBase Character => m_character;
        public Transform WeaponAttachment => m_weaponAttachment ? m_weaponAttachment : transform;
        public Transform ProjectileSpawn => m_projectileSpawn ? m_projectileSpawn : WeaponAttachment;
        public Vector3 ProjectileSpawnPosition => ProjectileSpawn.position;

        public bool TryGetWeaponAttachment(out Transform attachment)
        {
            attachment = WeaponAttachment;
            return attachment != null;
        }

        public bool TryGetProjectileSpawn(out Transform spawn)
        {
            spawn = ProjectileSpawn;
            return spawn != null;
        }

        public Vector3 ResolveProjectileSpawnPosition()
        {
            return TryGetProjectileSpawn(out Transform spawn) ? spawn.position : transform.position;
        }

        private void Awake()
        {
            EnsureCharacterReference();
        }

        private void Reset()
        {
            EnsureCharacterReference();
        }

        private void OnValidate()
        {
            EnsureCharacterReference();
        }

        private void EnsureCharacterReference()
        {
            if (m_character == null)
            {
                TryGetComponent(out m_character);
            }
        }

    }
}
