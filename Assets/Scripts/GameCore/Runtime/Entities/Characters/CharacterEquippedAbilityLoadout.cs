using System;
using System.Collections.Generic;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 角色已装备主动技能槽实现。
    /// 这里只负责槽位占位、移槽和快照，不承担角色规则本身。
    /// </summary>
    internal sealed class CharacterEquippedAbilityLoadout
    {
        private readonly Entry[] m_slots;

        public CharacterEquippedAbilityLoadout(int slotCount)
        {
            m_slots = new Entry[slotCount];
        }

        public int SlotCount => m_slots.Length;

        public int GetFormalGasAbilityCode(int index)
        {
            return index >= 0 && index < m_slots.Length ? m_slots[index].formalGasAbilityCode : 0;
        }

        public CharacterEquippedAbilitySlotView[] CreateSlotViewSnapshot()
        {
            CharacterEquippedAbilitySlotView[] snapshot = new CharacterEquippedAbilitySlotView[m_slots.Length];
            for (int i = 0; i < m_slots.Length; ++i)
            {
                snapshot[i] = m_slots[i].CreateView(i);
            }

            return snapshot;
        }

        public CharacterAbilitySlotData[] CreateSlotDataSnapshot(DatabaseRegistry databaseRegistry)
        {
            CharacterAbilitySlotData[] slots = new CharacterAbilitySlotData[m_slots.Length];

            for (int i = 0; i < m_slots.Length; ++i)
            {
                slots[i] = new CharacterAbilitySlotData
                {
                    slotIndex = i,
                    formalGasAbilityCode = m_slots[i].formalGasAbilityCode
                };
            }

            return slots;
        }

        public bool ContainsFormalGasAbilityCode(int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0)
            {
                return false;
            }

            for (int i = 0; i < m_slots.Length; ++i)
            {
                if (m_slots[i].MatchesFormalGasAbilityCode(formalGasAbilityCode))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ClearSlot(int index)
        {
            if (index < 0 || index >= m_slots.Length || m_slots[index].IsEmpty)
            {
                return false;
            }

            m_slots[index] = default;
            return true;
        }

        public bool RemoveFormalGasAbilityCodeFromAllSlots(int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0)
            {
                return false;
            }

            bool changed = false;

            for (int i = 0; i < m_slots.Length; ++i)
            {
                if (m_slots[i].MatchesFormalGasAbilityCode(formalGasAbilityCode))
                {
                    m_slots[i] = default;
                    changed = true;
                }
            }

            return changed;
        }

        public bool TryAutoAssignFormalGasAbilityCode(int formalGasAbilityCode)
        {
            if (formalGasAbilityCode <= 0 || ContainsFormalGasAbilityCode(formalGasAbilityCode))
            {
                return false;
            }

            int index = -1;
            for (int i = 0; i < m_slots.Length; ++i)
            {
                if (m_slots[i].IsEmpty)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            m_slots[index] = Entry.FromFormalGasAbilityCode(formalGasAbilityCode);
            return true;
        }

        public bool TryAssignFormalGasAbilityCodeToSlot(int index, int formalGasAbilityCode)
        {
            if (index < 0 || index >= m_slots.Length || formalGasAbilityCode <= 0)
            {
                return false;
            }

            if (m_slots[index].MatchesFormalGasAbilityCode(formalGasAbilityCode))
            {
                return false;
            }

            for (int i = 0; i < m_slots.Length; ++i)
            {
                if (i != index && m_slots[i].MatchesFormalGasAbilityCode(formalGasAbilityCode))
                {
                    m_slots[i] = default;
                    break;
                }
            }

            m_slots[index] = Entry.FromFormalGasAbilityCode(formalGasAbilityCode);
            return true;
        }

        public bool RestoreFromSlotData(
            IEnumerable<CharacterAbilitySlotData> quickAbilitySlots)
        {
            CharacterAbilitySlotData[] slotDataSnapshot = quickAbilitySlots != null
                ? new List<CharacterAbilitySlotData>(quickAbilitySlots).ToArray()
                : Array.Empty<CharacterAbilitySlotData>();

            Array.Clear(m_slots, 0, m_slots.Length);

            foreach (CharacterAbilitySlotData slotData in slotDataSnapshot)
            {
                if (slotData == null ||
                    slotData.slotIndex < 0 ||
                    slotData.slotIndex >= SlotCount)
                {
                    continue;
                }

                if (slotData.formalGasAbilityCode > 0)
                {
                    m_slots[slotData.slotIndex] = Entry.FromFormalGasAbilityCode(slotData.formalGasAbilityCode);
                    continue;
                }
            }

            return true;
        }

        private readonly struct Entry
        {
            public readonly int formalGasAbilityCode;

            private Entry(int formalGasAbilityCode)
            {
                this.formalGasAbilityCode = formalGasAbilityCode;
            }

            public bool IsEmpty => formalGasAbilityCode <= 0;

            public static Entry FromFormalGasAbilityCode(int formalGasAbilityCode)
            {
                return new Entry(formalGasAbilityCode);
            }

            public bool MatchesFormalGasAbilityCode(int otherFormalGasAbilityCode)
            {
                return formalGasAbilityCode > 0 &&
                    otherFormalGasAbilityCode > 0 &&
                    formalGasAbilityCode == otherFormalGasAbilityCode;
            }

            public CharacterEquippedAbilitySlotView CreateView(int slotIndex)
            {
                return new CharacterEquippedAbilitySlotView(slotIndex, formalGasAbilityCode);
            }
        }
    }
}
