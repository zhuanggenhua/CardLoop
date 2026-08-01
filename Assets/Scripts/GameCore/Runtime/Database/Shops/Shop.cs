using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 商店交易方向，影响价格倍率。
    /// </summary>
    public enum ETransactionType
    {
        Buy,
        Sell
    }

    /// <summary>
    /// 商店资产，集中配置可交易物品和买入/卖出价格倍率。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Shops + nameof(Shop))]
    public class Shop : DatabaseEntry
    {
        [InspectorName("商品列表")]
        [Tooltip("商店可出售或回收的物品列表。")]
        [SerializeField] private Item[] m_items = null;

        [InspectorName("卖给商店倍率")]
        [Tooltip("玩家出售物品时使用的价格倍率。")]
        [SerializeField] private float m_sellingPriceMultiplier = 0.5f;

        [InspectorName("从商店购买倍率")]
        [Tooltip("玩家购买物品时使用的价格倍率。")]
        [SerializeField] private float m_buyingPriceMultiplier = 1.0f;

        public int itemCount => m_items?.Length ?? 0;
        public Item GetItemAt(int index) => index >= 0 && index < itemCount ? m_items[index] : null;
        /// <summary>
        /// 返回商品列表快照，避免外部直接修改商店资产数组。
        /// </summary>
        public Item[] GetItems() => m_items != null ? (Item[])m_items.Clone() : System.Array.Empty<Item>();

        /// <summary>
        /// 按交易方向计算取整后的价格。
        /// </summary>
        public int GetPrice(Item item, ETransactionType transaction)
        {
            float floatPrice = 0.0f;

            switch (transaction)
            {
                case ETransactionType.Buy:
                    floatPrice = item.price * m_buyingPriceMultiplier;
                    break;

                case ETransactionType.Sell:
                    floatPrice = item.price * m_sellingPriceMultiplier;
                    break;
            }

            return (int)math.ceil(floatPrice);
        }
    }
}
