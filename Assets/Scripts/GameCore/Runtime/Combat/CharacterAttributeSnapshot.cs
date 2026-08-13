using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单个 EX-GAS 角色属性的可持久化基础值。
    /// 属性身份仍由 EX-GAS 属性表提供，本结构不保存显示名、顺序或另一套编号。
    /// </summary>
    [Serializable]
    public struct CharacterAttributeSnapshotEntry
    {
        public CharacterAttributeSnapshotEntry(int attributeCode, float baseValue)
        {
            if (attributeCode <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attributeCode),
                    attributeCode,
                    "EX-GAS 属性码必须是正整数。");
            }

            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseValue),
                    baseValue,
                    "角色属性基础值必须是有限数值。");
            }

            m_attributeCode = attributeCode;
            m_baseValue = baseValue;
        }

        [SerializeField] private int m_attributeCode;
        [SerializeField] private float m_baseValue;

        public int AttributeCode => m_attributeCode;
        public float BaseValue => m_baseValue;
    }

    /// <summary>
    /// 角色 ASC 的可持久化基础值快照。
    /// 它只在存档和局部运行态恢复时存在，不参与属性查询、效果重算或作者配置。
    /// </summary>
    [Serializable]
    public sealed class CharacterAttributeSnapshot
    {
        [SerializeField] private CharacterAttributeSnapshotEntry[] m_entries =
            Array.Empty<CharacterAttributeSnapshotEntry>();

        public CharacterAttributeSnapshot(IReadOnlyList<CharacterAttributeSnapshotEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            m_entries = new CharacterAttributeSnapshotEntry[entries.Count];
            HashSet<int> attributeCodes = new();
            for (int i = 0; i < entries.Count; i++)
            {
                CharacterAttributeSnapshotEntry entry = entries[i];
                if (!attributeCodes.Add(entry.AttributeCode))
                {
                    throw new InvalidOperationException(
                        $"角色属性快照重复包含属性码 {entry.AttributeCode}。");
                }

                m_entries[i] = entry;
            }

            Array.Sort(
                m_entries,
                static (left, right) => left.AttributeCode.CompareTo(right.AttributeCode));
        }

        public IReadOnlyList<CharacterAttributeSnapshotEntry> Entries =>
            m_entries ?? Array.Empty<CharacterAttributeSnapshotEntry>();
    }
}
