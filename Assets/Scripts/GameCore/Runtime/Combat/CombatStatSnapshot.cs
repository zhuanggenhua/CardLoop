using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单次命中结算使用的最小 EX-GAS 属性快照。
    /// 战斗侧不持有 ASC，也不建立另一份角色属性目录。
    /// </summary>
    [Serializable]
    internal struct CombatStatSnapshot
    {
        [SerializeField] private float m_attack;
        [SerializeField] private float m_defense;
        [SerializeField] private float m_accuracy;
        [SerializeField] private float m_dodge;
        [SerializeField] private float m_criticalChance;
        [SerializeField] private float m_criticalMultiplier;

        internal CombatStatSnapshot(
            float attack,
            float defense,
            float accuracy,
            float dodge,
            float criticalChance,
            float criticalMultiplier)
        {
            m_attack = attack;
            m_defense = defense;
            m_accuracy = accuracy;
            m_dodge = dodge;
            m_criticalChance = criticalChance;
            m_criticalMultiplier = criticalMultiplier;
        }

        internal float Accuracy => m_accuracy;
        internal float Dodge => m_dodge;
        internal float CriticalChance => m_criticalChance;
        internal float CriticalMultiplier => m_criticalMultiplier;

        internal float GetOffensiveStat(EDamageType type) =>
            type == EDamageType.None ? 0.0f : m_attack;

        internal float GetDefensiveStat(EDamageType type) =>
            type == EDamageType.None ? 0.0f : m_defense;
    }
}
