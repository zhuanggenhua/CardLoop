using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 单条宝箱物品掉落配置。
    /// </summary>
    [Serializable]
    public struct ChestLootEntry
    {
        [InspectorName("物品")]
        [Tooltip("宝箱首次打开时放入容器背包的物品。")]
        public Item item;

        [InspectorName("数量")]
        [Tooltip("放入容器背包的物品数量；必须大于 0，否则宝箱首次开启会报配置错误。")]
        public int quantity;
    }

    /// <summary>
    /// 宝箱掉落配置，支持物品列表和金钱，并能提供掉落图标快照。
    /// </summary>
    [Serializable]
    public struct ChestLoot
    {
        [InspectorName("物品掉落")]
        [Tooltip("宝箱首次打开时生成到容器背包里的物品列表。")]
        [SerializeField] private ChestLootEntry[] m_entries;

        [InspectorName("金钱")]
        [Tooltip("宝箱首次打开时直接给予玩家的金钱数量；必须大于等于 0。")]
        public int money;

        public int entryCount => m_entries?.Length ?? 0;
        public bool HasMoney() => money > 0;
        public bool HasItems() => entryCount > 0;
        public bool IsEmpty() => !(HasItems() || HasMoney());
        /// <summary>
        /// 返回掉落条目快照，避免外部直接改写配置数组。
        /// </summary>
        public ChestLootEntry[] GetEntries() => m_entries != null ? (ChestLootEntry[])m_entries.Clone() : Array.Empty<ChestLootEntry>();

        /// <summary>
        /// 收集掉落中可展示的物品和金钱图标，用于宝箱内容揭示动画。
        /// </summary>
        public Sprite[] GetLootSprites()
        {
            List<Sprite> sprites = new();

            foreach (ChestLootEntry entry in GetEntries())
            {
                if (entry.item)
                {
                    sprites.Add(entry.item.icon);
                }
            }

            if (HasMoney())
            {
                sprites.Add(GameManager.Config.GetTermDefinition("currency").icon);
            }

            sprites.RemoveAll((sprite) => sprite == null);

            return sprites.ToArray();
        }
    }
}
