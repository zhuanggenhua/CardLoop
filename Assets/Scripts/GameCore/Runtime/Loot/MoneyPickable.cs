using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 场景内可直接拾取的金钱入口。
    /// 当前队伍资金仍是共享钱包，不随角色物品背包一起拆成每人一份。
    /// </summary>
    public sealed class MoneyPickable : PickableItem
    {
        [Header("Money Pickup")]
        [Min(1)]
        [SerializeField] private int m_amount = 1;

        protected override bool TryPick(CharacterBase pickerCharacter)
        {
            if (m_amount <= 0)
            {
                return false;
            }

            GameManager.InventorySystem.AddMoney(m_amount);
            return true;
        }
    }
}
