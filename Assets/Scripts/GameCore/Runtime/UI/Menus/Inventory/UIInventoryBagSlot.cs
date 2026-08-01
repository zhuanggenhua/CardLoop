using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIInventoryBagSlot : MonoBehaviour, IItemSlotHandler, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image m_image = null;
        [SerializeField] private TextMeshProUGUI m_quantity = null;
        [SerializeField] private Button m_button = null;

        private Item m_item = null;
        private bool m_selected = false;
        private IInventoryBagItemClickHandler m_clickHandler = null;

        public void Clear() => SetItem(null, 0);

        public Item GetItem()
        {
            return m_item;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_button.Select();
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_selected = true;
            GameRuntimeEvents.NotifyItemDetailsOpened(m_item);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_selected = false;
            GameRuntimeEvents.NotifyItemDetailsClosed();
        }

        public void SetItem(Item item, int quantity)
        {
            if (item != null)
            {
                m_item = item;
                m_quantity.text = quantity.ToString();
                m_image.enabled = true;
                m_image.sprite = item.icon;
            }
            else
            {
                m_image.enabled = false;
                m_quantity.text = string.Empty;
                m_item = null;
            }

            if (m_selected)
            {
                GameRuntimeEvents.NotifyItemDetailsOpened(m_item);
            }
        }

        private void Awake()
        {
            m_clickHandler = GetComponentInParent<IInventoryBagItemClickHandler>();
            Debug.Assert(m_clickHandler != null, $"{nameof(UIInventoryBagSlot)} requires a parent {nameof(IInventoryBagItemClickHandler)}.");
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
            if (m_item != null)
            {
                m_clickHandler?.HandleBagItemClicked(m_item);
            }
        }

        internal Selectable GetNavigationSelectable()
        {
            return m_button;
        }

    }
}


