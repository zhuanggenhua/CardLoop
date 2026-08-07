using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 战斗结算使用的最小正式属性快照。
    /// 它只保留当前伤害系统真正用到的攻击、防御、敏捷和幸运，
    /// 不再让战斗侧为了一次结算拿整份 Stats 真相。
    /// </summary>
    [Serializable]
    public struct CombatStatSnapshot
    {
        [SerializeField] private int m_physicalAttack;
        [SerializeField] private int m_magicalAttack;
        [SerializeField] private int m_physicalDefense;
        [SerializeField] private int m_magicalDefense;
        [SerializeField] private int m_agility;
        [SerializeField] private int m_luck;

        public CombatStatSnapshot(
            int physicalAttack,
            int magicalAttack,
            int physicalDefense,
            int magicalDefense,
            int agility,
            int luck)
        {
            m_physicalAttack = physicalAttack;
            m_magicalAttack = magicalAttack;
            m_physicalDefense = physicalDefense;
            m_magicalDefense = magicalDefense;
            m_agility = agility;
            m_luck = luck;
        }

        public int Agility => m_agility;

        public int Luck => m_luck;

        public int GetOffensiveStat(EDamageType type)
        {
            switch (type)
            {
                default:
                case EDamageType.None: return 0;
                case EDamageType.Physical: return m_physicalAttack;
                case EDamageType.Magical: return m_magicalAttack;
            }
        }

        public int GetDefensiveStat(EDamageType type)
        {
            switch (type)
            {
                default:
                case EDamageType.None: return 0;
                case EDamageType.Physical: return m_physicalDefense;
                case EDamageType.Magical: return m_magicalDefense;
            }
        }
    }
}
