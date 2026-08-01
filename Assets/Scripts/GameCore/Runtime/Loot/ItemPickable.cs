using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 场景内可直接拾取的物品入口。
    /// 只负责把掉落转交给 InventorySystem 的执行角色 owner，不引入第二套物品真相。
    /// </summary>
    public sealed class ItemPickable : PickableItem
    {
        [Header("Item Pickup")]
        [SerializeField] private Item m_item = null;
        [Min(1)]
        [SerializeField] private int m_quantity = 1;
        [SerializeField] private EItemTransferType m_transferType = EItemTransferType.Unknown;

        protected override bool TryPick(CharacterBase pickerCharacter)
        {
            if (m_item == null || m_quantity <= 0)
            {
                return false;
            }

            InventoryOwnerHandle ownerHandle = GameManager.InventorySystem.GetOwner(pickerCharacter);
            GameManager.InventorySystem.AddToBag(ownerHandle, m_item, m_quantity, m_transferType);
            return true;
        }
    }
}
