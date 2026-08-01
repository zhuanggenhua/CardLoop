using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 装备槽位类型，决定物品会挂到角色哪一个装备位置和表现层插槽。
    /// </summary>
    public enum EEquipmentType
    {
        Weapon,
        Head,
        Torso,
        Hands,
        Feet,
        Mount
    }

    /// <summary>
    /// 装备操作方向，用于区分穿戴和卸下时的背包、属性和表现更新。
    /// </summary>
    public enum EOperationType
    {
        Equip,
        Unequip
    }

    /// <summary>
    /// 可被角色穿戴的物品资产，负责提供装备槽位、属性加成、外观和附加 Formal GAS 技能。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Items + nameof(Equipment))]
    public class Equipment : Item
    {
        [Header("装备")]
        [InspectorName("装备槽位")]
        [Tooltip("决定该装备占用角色的哪个装备位置，也用于表现层选择对应外观槽。")]
        [SerializeField] private EEquipmentType m_type;

        [InspectorName("属性加成")]
        [Tooltip("装备穿戴后提供的属性增量；对外读取时会返回快照，避免外部直接改写资产数据。")]
        [SerializeField] private Stats m_bonusStats;

        [InspectorName("装备外观")]
        [Tooltip("角色穿戴该装备时使用的外观资源。为空时只提供数值或技能效果，不改变外观。")]
        [SerializeField] private EquipmentVisualAsset m_visual;

        [InspectorName("附加技能编码")]
        [Tooltip("装备穿戴后额外授予角色的 Formal GAS 技能编码；小于等于 0 的值会被过滤。")]
        [SerializeField] private int[] m_formalGasAbilityCodes;

        public EEquipmentType type => m_type;
        public EquipmentVisualAsset visual => m_visual;
        public int bonusAbilityCount => GetBonusFormalGasAbilityCodes().Length;

        /// <summary>
        /// 只有需要整份加成快照做聚合运算时才复制整组属性。
        /// 简单 UI 或单值查询优先走标量入口，避免把整份 Stats 真相借出去。
        /// </summary>
        public Stats CreateBonusStatsSnapshot() => m_bonusStats?.Clone() ?? new Stats();

        /// <summary>
        /// 查询指定属性的装备加成；未配置属性表时按 0 处理。
        /// </summary>
        public int GetBonusStatValue(EStat stat) => m_bonusStats != null ? m_bonusStats[stat] : 0;

        /// <summary>
        /// 按正式属性定义查询装备加成，供 UI 和 Formal GAS 绑定层复用同一入口。
        /// </summary>
        public int GetBonusStatValue(FormalAttributeDefinition definition) => GetBonusStatValue(definition.Stat);

        /// <summary>
        /// 返回去重且有效的附加技能编码，避免装备资产中的空值或重复值泄漏到角色技能集合。
        /// </summary>
        public int[] GetBonusFormalGasAbilityCodes()
        {
            HashSet<int> codes = new();
            if (m_formalGasAbilityCodes != null)
            {
                foreach (int code in m_formalGasAbilityCodes)
                {
                    if (code > 0)
                    {
                        codes.Add(code);
                    }
                }
            }

            return codes.ToArray();
        }

    }
}
