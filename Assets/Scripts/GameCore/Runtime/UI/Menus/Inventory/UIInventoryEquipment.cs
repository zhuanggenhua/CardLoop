using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIInventoryEquipment : MonoBehaviour
    {
        [SerializeField] private UIInventoryEquipmentSlot[] m_slots = null;

        public void UpdateSlots(CharacterBase targetCharacter)
        {
            UpdateSlots(
                targetCharacter != null &&
                targetCharacter.TryGetComponent(out CharacterEquipment targetEquipment) &&
                targetEquipment != null
                    ? targetEquipment
                    : null);
        }

        public void UpdateSlots(CharacterEquipment targetEquipment)
        {
            InventorySystem inventorySystem = GameManager.GetSystem<InventorySystem>();

            foreach (UIInventoryEquipmentSlot slot in m_slots)
            {
                slot.SetEquipment(inventorySystem.GetEquipment(targetEquipment, slot.equipmentType));
            }
        }

        public UINavigationCursorTarget FindNavigationTarget()
        {
            if (m_slots != null && m_slots.Length > 0)
            {
                return m_slots[0].gameObject.GetComponentInChildren<UINavigationCursorTarget>();
            }

            return null;
        }
    }
}

