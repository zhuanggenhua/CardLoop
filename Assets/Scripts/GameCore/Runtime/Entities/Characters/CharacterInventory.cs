using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 角色背包通道，决定查询主背包、武器背包还是快捷栏背包。
    /// </summary>
    public enum ECharacterInventoryChannel
    {
        Main,
        Weapon,
        Hotbar
    }

    /// <summary>
    /// 角色背包归属配置组件，决定各背包通道归角色独占还是落到队伍共享背包。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBase))]
    public sealed class CharacterInventory : MonoBehaviour
    {
        [Header("背包归属")]
        [InspectorName("角色引用")]
        [Tooltip("拥有独立背包时使用的角色引用；通常自动取同物体上的 CharacterBase。")]
        [SerializeField] private CharacterBase m_character = null;

        [InspectorName("主背包归角色所有")]
        [Tooltip("关闭后，主背包入口会解析到默认队伍背包。")]
        [SerializeField] private bool m_ownsMainInventory = true;

        [InspectorName("武器背包归角色所有")]
        [Tooltip("关闭后，武器背包入口会解析到默认队伍背包。")]
        [SerializeField] private bool m_ownsWeaponInventory = true;

        [InspectorName("快捷栏归角色所有")]
        [Tooltip("关闭后，快捷栏入口会解析到默认队伍背包。")]
        [SerializeField] private bool m_ownsHotbarInventory = true;

        public CharacterBase Character => m_character;
        public bool OwnsMainInventory => m_ownsMainInventory;
        public bool OwnsWeaponInventory => m_ownsWeaponInventory;
        public bool OwnsHotbarInventory => m_ownsHotbarInventory;

        /// <summary>
        /// 解析主背包的所有者句柄。
        /// </summary>
        public InventoryOwnerHandle ResolveMainInventoryOwner()
        {
            return ResolveOwner(m_ownsMainInventory);
        }

        /// <summary>
        /// 解析武器背包的所有者句柄。
        /// </summary>
        public InventoryOwnerHandle ResolveWeaponInventoryOwner()
        {
            return ResolveOwner(m_ownsWeaponInventory);
        }

        /// <summary>
        /// 解析快捷栏背包的所有者句柄。
        /// </summary>
        public InventoryOwnerHandle ResolveHotbarInventoryOwner()
        {
            return ResolveOwner(m_ownsHotbarInventory);
        }

        /// <summary>
        /// 按背包通道解析对应的所有者句柄。
        /// </summary>
        public InventoryOwnerHandle ResolveOwner(ECharacterInventoryChannel channel)
        {
            return channel switch
            {
                ECharacterInventoryChannel.Weapon => ResolveWeaponInventoryOwner(),
                ECharacterInventoryChannel.Hotbar => ResolveHotbarInventoryOwner(),
                _ => ResolveMainInventoryOwner()
            };
        }

        private InventoryOwnerHandle ResolveOwner(bool ownedByCharacter)
        {
            if (!ownedByCharacter)
            {
                return InventoryOwnerHandle.DefaultParty;
            }

            if (m_character == null)
            {
                throw new MissingComponentException(
                    $"[{nameof(CharacterInventory)}] Character-owned inventory requires {nameof(CharacterBase)} on the same GameObject.");
            }

            return InventoryOwnerHandle.ForCharacter(m_character);
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
