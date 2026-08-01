using System.Collections.Generic;
using System.Linq;
using azixMcAze.SerializableDictionary;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 角色的已装备物品槽实现。
    /// 这里只负责物品槽位与装备统计聚合，不承担装备规则编排。
    /// </summary>
    internal sealed class CharacterEquippedItemLoadout
    {
        private readonly SerializableDictionary<EEquipmentType, Equipment> m_slots = new();

        public bool TryGet(EEquipmentType type, out Equipment equipment)
        {
            return m_slots.TryGetValue(type, out equipment);
        }

        public void Set(EEquipmentType type, Equipment equipment)
        {
            if (equipment)
            {
                m_slots[type] = equipment;
            }
            else
            {
                m_slots.Remove(type);
            }
        }

        public void Clear()
        {
            m_slots.Clear();
        }

        public Equipment[] SnapshotItems()
        {
            return m_slots.Values.ToArray();
        }

        public Stats BuildStatContribution()
        {
            Stats equipmentStats = new();

            foreach (Equipment piece in m_slots.Values)
            {
                if (piece)
                {
                    equipmentStats += piece.CreateBonusStatsSnapshot();
                }
            }

            return equipmentStats;
        }

        public CharacterEquipmentSlotData[] CreateSlotDataSnapshot(DatabaseRegistry databaseRegistry)
        {
            if (databaseRegistry == null)
            {
                throw new System.InvalidOperationException(
                    $"[{nameof(CharacterEquippedItemLoadout)}] 保存已装备物品需要有效 DatabaseRegistry。");
            }

            List<CharacterEquipmentSlotData> slots = new();
            foreach ((EEquipmentType slotType, Equipment equipment) in m_slots.Where(kvp => kvp.Value))
            {
                slots.Add(new CharacterEquipmentSlotData
                {
                    slotType = slotType,
                    equipment = databaseRegistry.CreateReference(equipment)
                });
            }

            return slots.ToArray();
        }

        public bool RestoreFromSlotData(
            System.Collections.Generic.IEnumerable<CharacterEquipmentSlotData> equipmentSlots,
            System.Func<DatabaseEntryReference<Equipment>, Equipment> resolveEquipment,
            System.Action<Equipment> applyEquipment)
        {
            CharacterEquipmentSlotData[] slotDataSnapshot = equipmentSlots?.ToArray()
                ?? System.Array.Empty<CharacterEquipmentSlotData>();

            Clear();

            foreach (CharacterEquipmentSlotData slotData in slotDataSnapshot)
            {
                if (slotData?.equipment == null || string.IsNullOrWhiteSpace(slotData.equipment.guid))
                {
                    continue;
                }

                Equipment equipment = resolveEquipment(slotData.equipment);

                if (!equipment)
                {
                    Debug.LogWarning($"A saved equipment slot [{slotData.slotType}] reference could not be restored and was skipped.");
                    continue;
                }

                if (equipment.type != slotData.slotType)
                {
                    Debug.LogWarning($"A saved equipment slot [{slotData.slotType}] contains equipment [{equipment.name}] of type [{equipment.type}] and was skipped.");
                    continue;
                }

                applyEquipment(equipment);
            }

            return true;
        }

    }
}
