using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameCore
{
    public class UIAbilityCategory : MonoBehaviour, ISelectHandler
    {
        [Header("Settings")]
        [SerializeField] private Sprite m_selectedSprite;
        [SerializeField] private Sprite m_unselectedSprite;

        [Header("References")]
        [SerializeField] private Button m_button = null;
        [SerializeField] private Image m_icon = null;
        [SerializeField] private TextMeshProUGUI m_text = null;

        private EAbilityType m_category;
        private IAbilityMenuEventReceiver m_receiver = null;

        private void Awake()
        {
            m_receiver = GetComponentInParent<IAbilityMenuEventReceiver>();
            Debug.Assert(m_receiver != null, $"{nameof(UIAbilityCategory)} 需要父级实现 {nameof(IAbilityMenuEventReceiver)}。");
        }

        public void SetCategory(EAbilityType category, int count)
        {
            m_category = category;
            m_icon.sprite = GameManager.Config.GetTermDefinition(m_category).icon;
            m_text.text = $"{GameManager.Config.GetTermDefinition(m_category).shortName} ({count})";
        }

        public void SetHighlight(bool value)
        {
            ((Image)m_button.targetGraphic).sprite = value ? m_selectedSprite : m_unselectedSprite;
        }

        public void SelectCategory()
        {
            m_receiver?.HandleAbilityCategorySelected(m_category);
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_receiver?.HandleAbilityCategoryHovered(m_category);
        }
    }
}

