using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIInventoryBagCategory : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Sprite m_selectedSprite;
        [SerializeField] private Sprite m_unselectedSprite;

        [Header("References")]
        [SerializeField] private Button m_button = null;
        [SerializeField] private Image m_icon = null;
        [SerializeField] private TextMeshProUGUI m_text = null;

        private EItemCategory m_category;
        private UIInventoryBag m_inventoryBag = null;

        private void Awake()
        {
            m_inventoryBag = GetComponentInParent<UIInventoryBag>();
            Debug.Assert(m_inventoryBag != null, $"{nameof(UIInventoryBagCategory)} requires a parent {nameof(UIInventoryBag)}.");
        }

        public void SetCategory(EItemCategory category)
        {
            m_category = category;
            m_icon.sprite = GameManager.Config.GetTermDefinition(m_category).icon;
            m_text.text = GameManager.Config.GetTermDefinition(m_category).shortName;
        }

        public void SetHighlight(bool value)
        {
            ((Image)m_button.targetGraphic).sprite = value ? m_selectedSprite : m_unselectedSprite;
        }

        public void SelectCategory()
        {
            m_inventoryBag.HandleBagCategorySelected(m_category);
        }
    }
}

