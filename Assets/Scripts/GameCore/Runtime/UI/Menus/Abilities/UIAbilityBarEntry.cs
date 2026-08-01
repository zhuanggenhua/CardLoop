using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIAbilityBarEntry : UIAbility, ISelectHandler, IPointerEnterHandler
    {
        [Header("References")]
        [SerializeField] private Button m_button = null;

        private IAbilityMenuEventReceiver m_receiver = null;

        private void Awake()
        {
            m_receiver = GetComponentInParent<IAbilityMenuEventReceiver>();
            Debug.Assert(m_receiver != null, $"{nameof(UIAbilityBarEntry)} 需要父级实现 {nameof(IAbilityMenuEventReceiver)}。");

            if (m_button)
            {
                m_button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnClick);
            }
        }
        private void OnClick()
        {
            m_receiver?.HandleAbilitySlotClicked(m_abilityIndex);
        }

        public void ForceSelection()
        {
            m_button.Select();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (m_button.interactable)
            {
                if (m_button.navigation.mode != Navigation.Mode.None)
                {
                    if (m_abilitySlot.HasDisplaySource)
                    {
                        m_receiver?.HandleAbilityHovered(m_abilitySlot);
                    }
                    else
                    {
                        m_receiver?.HandleNullAbilityHovered();
                    }
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ForceSelection();
        }
    }
}


