using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIInventoryEquipmentSlot : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private EEquipmentType m_equipmentType = EEquipmentType.Head;
        [SerializeField] private Image m_placeholder = null;
        [SerializeField] private Image m_content = null;
        [SerializeField] private Button m_button = null;

        public EEquipmentType equipmentType => m_equipmentType;

        private Equipment m_equipment = null;
        private bool m_selected = false;
        private UIInventory m_inventoryMenu = null;

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_button.Select();
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_selected = true;
            GameRuntimeEvents.NotifyItemDetailsOpened(m_equipment);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_selected = false;
            GameRuntimeEvents.NotifyItemDetailsClosed();
        }

        public void SetEquipment(Equipment equipment)
        {
            m_equipment = equipment;

            if (equipment)
            {
                Debug.Assert(equipment.type == m_equipmentType, "Equipment type mismatch");

                m_placeholder.enabled = false;
                m_content.enabled = true;
                m_content.sprite = equipment.icon;
            }
            else
            {
                m_placeholder.enabled = true;
                m_content.enabled = false;
                m_content.sprite = null;
            }

            if (m_selected)
            {
                GameRuntimeEvents.NotifyItemDetailsOpened(m_equipment);
            }
        }

        private void Awake()
        {
            m_inventoryMenu = GetComponentInParent<UIInventory>();
            Debug.Assert(m_inventoryMenu != null, $"{nameof(UIInventoryEquipmentSlot)} requires a parent {nameof(UIInventory)}.");
            m_button.onClick.AddListener(OnSlotClicked);
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnSlotClicked);
            }
        }
        private void OnSlotClicked()
        {
            if (m_equipment != null)
            {
                m_inventoryMenu.HandleEquipmentItemClicked(m_equipment);
            }
        }
    }
}


