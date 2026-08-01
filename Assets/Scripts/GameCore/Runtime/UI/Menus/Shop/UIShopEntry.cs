using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIShopEntry : MonoBehaviour, IItemSlotHandler, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [Header("References")]
        [SerializeField] private Image m_image = null;
        [SerializeField] private TextMeshProUGUI m_name = null;
        [SerializeField] private TextMeshProUGUI m_price = null;
        [SerializeField] private Button m_button = null;

        private Item m_target = null;
        private UIShop m_shopMenu = null;

        private void Awake()
        {
            m_button.onClick.AddListener(OnSlotClicked);
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnSlotClicked);
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            m_button.Select();
        }

        public void OnSelect(BaseEventData eventData)
        {
            GameRuntimeEvents.NotifyItemDetailsOpened(m_target);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            GameRuntimeEvents.NotifyItemDetailsClosed();
        }

        public void OnSlotClicked()
        {
            if (m_target != null)
            {
                m_shopMenu.HandleShopSlotClicked(m_target);
            }
        }

        public void Initialize(Item item)
        {
            m_shopMenu = GetComponentInParent<UIShop>();
            Debug.Assert(m_shopMenu != null, $"{nameof(UIShopEntry)} 需要父级 {nameof(UIShop)} 作为商店菜单。");
            m_target = item;
            m_name.text = item.displayName;
            m_price.text = item.price.ToString();
            m_image.sprite = item.icon;
        }

        public Item GetItem()
        {
            return m_target;
        }

        internal GameObject GetFocusTarget()
        {
            return m_button != null ? m_button.gameObject : gameObject;
        }

        internal void ConfigureNavigation(UIShopEntry previous, UIShopEntry next, Selectable rightTarget)
        {
            m_button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = previous?.m_button,
                selectOnDown = next?.m_button,
                selectOnRight = rightTarget
            };
        }
    }
}


