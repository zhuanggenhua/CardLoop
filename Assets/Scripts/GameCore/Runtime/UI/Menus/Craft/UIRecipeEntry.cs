using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    public class UIRecipeEntry : MonoBehaviour, IItemSlotHandler, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [Header("References")]
        [SerializeField] private Image m_icon = null;
        [SerializeField] private TextMeshProUGUI m_name = null;
        [SerializeField] private Image m_status = null;
        [SerializeField] private Button m_button = null;

        [Header("Settings")]
        [SerializeField] private Sprite m_canCraftStatusIcon = null;
        [SerializeField] private Sprite m_cannotCraftStatusIcon = null;

        private Recipe m_recipe = null;
        private CraftingStation m_craftingStation = null;
        private UICraft m_craftMenu = null;

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
            GameRuntimeEvents.NotifyItemDetailsOpened(m_recipe.item);
            m_craftMenu.HandleRecipeEntrySelected(m_recipe);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            GameRuntimeEvents.NotifyItemDetailsClosed();
            m_craftMenu.HandleRecipeEntryDeselected(m_recipe);
        }

        public void OnSlotClicked()
        {
            if (m_recipe != null)
            {
                m_craftMenu.HandleRecipeEntryClicked(m_recipe);
            }
        }

        public void Initialize(Recipe recipe, CraftingStation craftingStation)
        {
            m_craftMenu = GetComponentInParent<UICraft>();
            Debug.Assert(m_craftMenu != null, $"{nameof(UIRecipeEntry)} 需要父级 {nameof(UICraft)} 作为制作菜单。");
            m_recipe = recipe;
            m_craftingStation = craftingStation;
            m_name.text = recipe.displayName;
            m_icon.sprite = recipe.icon;

            UpdateUI();
        }

        public void UpdateUI()
        {
            UpdateUI(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        public void UpdateUI(CharacterBase owner)
        {
            bool canCraft = m_craftingStation.CanCraft(owner, m_recipe, out bool hasMoney, out bool hasIngredients);
            m_status.sprite = canCraft ? m_canCraftStatusIcon : m_cannotCraftStatusIcon;
        }

        public Item GetItem()
        {
            return m_recipe?.item;
        }

        internal GameObject GetFocusTarget()
        {
            return m_button != null ? m_button.gameObject : gameObject;
        }

        internal void ConfigureNavigation(UIRecipeEntry previous, UIRecipeEntry next, Selectable rightTarget)
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


