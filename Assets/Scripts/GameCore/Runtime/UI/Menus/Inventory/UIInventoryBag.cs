using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    public class UIInventoryBag : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SerializableDictionary<EItemCategory, UIInventoryBagCategory> m_categories = null;

        private UIInventoryBagSlot[] m_slots = null;
        private EItemCategory m_category = 0;
        private InventoryOwnerHandle m_currentOwner = default;

        public void Init()
        {
            m_slots = GetComponentsInChildren<UIInventoryBagSlot>();

            // Because we display slots from bottom right to top left, we need to reverse them here to make sure we fill
            // them from top left to bottom right.
            Array.Reverse(m_slots);

            foreach (var category in m_categories)
            {
                category.Value.SetCategory(category.Key);
            }
        }

        // Always reset to the first category when shown
        private void OnEnable() => SetCategory(0);

        public void UpdateSlots(CharacterBase owner)
        {
            UpdateSlots(GameManager.InventorySystem.GetOwner(owner));
        }

        public void UpdateSlots(InventoryOwnerHandle owner)
        {
            m_currentOwner = owner;
            if (m_slots == null)
            {
                return;
            }

            ClearSlots();
            if (!owner.IsValid || !GameManager.TryGetSystem(out InventorySystem inventorySystem))
            {
                return;
            }

            FillSlots(owner, inventorySystem);
        }

        private void ClearSlots()
        {
            foreach (UIInventoryBagSlot slot in m_slots)
            {
                slot.Clear();
            }
        }

        private void FillSlots(InventoryOwnerHandle owner, InventorySystem inventorySystem)
        {
            int usedSlots = 0;

            foreach (KeyValuePair<Item, int> entry in inventorySystem.GetBagEntries(owner))
            {
                if (entry.Key.category == m_category)
                {
                    if (usedSlots >= m_slots.Length)
                    {
                        Debug.LogWarning($"Inventory bag UI does not have enough slots to display all {m_category} items for {owner}.", this);
                        break;
                    }

                    UIInventoryBagSlot slot = m_slots[usedSlots++];
                    slot.SetItem(entry.Key, entry.Value);
                }
            }
        }

        internal Selectable GetFirstSlotSelectable()
        {
            return m_slots.Length > 0 ? m_slots[0].GetNavigationSelectable() : null;
        }

        public UINavigationCursorTarget FindNavigationTarget()
        {
            if (m_slots.Length > 0)
            {
                return m_slots[0].gameObject.GetComponentInChildren<UINavigationCursorTarget>();
            }

            return null;
        }

        public void SetCategory(EItemCategory category)
        {
            // Make sure this category is available in the bag
            if (!m_categories.ContainsKey(category))
            {
                Debug.LogWarning($"Category {category} not found in the bag");
                return;
            }

            foreach (var entry in m_categories)
            {
                entry.Value.SetHighlight(false);
            }

            m_category = category;
            m_categories[m_category].SetHighlight(true);

            UpdateSlots(m_currentOwner);
        }

        public void HandleBagCategorySelected(EItemCategory category) => SetCategory(category);
    }
}

