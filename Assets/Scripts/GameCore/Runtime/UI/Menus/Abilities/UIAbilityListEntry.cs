using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIAbilityListEntry : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [Header("References")]
        [SerializeField] private Image m_image = null;
        [SerializeField] private TextMeshProUGUI m_name = null;
        [SerializeField] private Button m_button = null;

        private CharacterAbilityMenuEntry m_target = default;
        private EAbilityType m_type;
        private IAbilityMenuEventReceiver m_receiver = null;

        public void Initialize(CharacterAbilityMenuEntry ability, EAbilityType type)
        {
            ResolveReceiver();
            m_target = ability;
            m_name.text = ability.DisplayName;
            m_image.sprite = ability.Icon;
            m_type = type;
        }

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
        private void ResolveReceiver()
        {
            m_receiver = GetComponentInParent<IAbilityMenuEventReceiver>();
            Debug.Assert(m_receiver != null, $"{nameof(UIAbilityListEntry)} 需要父级实现 {nameof(IAbilityMenuEventReceiver)}。");
        }

        public CharacterAbilityMenuEntry GetTarget()
        {
            return m_target;
        }

        public void ForceSelection()
        {
            m_button.Select();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_button.IsInteractable())
            {
                m_button.Select();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_receiver?.HandleAbilityHovered(m_target);
        }

        public void OnSlotClicked()
        {
            if (m_target.CanEquipToActiveSlot && m_type == EAbilityType.Active)
            {
                m_receiver?.HandleAbilitySelectedFromList(this);
            }
        }
    }
}


