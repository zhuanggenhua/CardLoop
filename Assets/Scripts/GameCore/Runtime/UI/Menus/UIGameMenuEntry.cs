using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 游戏主菜单中的单个条目，负责在选中时更新焦点表现并在点击时请求对应菜单。
    /// </summary>
    public class UIGameMenuEntry : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        /// <summary>
        /// 游戏主菜单条目可触发的动作。
        /// </summary>
        public enum EGameMenuAction
        {
            None,
            OpenInventory,
            OpenJournal,
            OpenSaveMenu,
            OpenAbilities,
            OpenCharacter,
            OpenCraft,
            OpenSettings,
            GoToMainMenu
        }

        [Header("设置")]
        [InspectorName("菜单动作")]
        [Tooltip("点击该条目时执行的菜单动作。")]
        [SerializeField] private EGameMenuAction m_action = EGameMenuAction.None;

        [Header("引用")]
        [InspectorName("按钮")]
        [Tooltip("接收点击和焦点的按钮。")]
        [SerializeField] private Button m_button = null;

        [InspectorName("文本")]
        [Tooltip("条目选中时显示的文本提示。")]
        [SerializeField] private TextMeshProUGUI m_text = null;

        private UIGameMenu m_menu = null;

        private void Awake()
        {
            m_menu = GetComponentInParent<UIGameMenu>();
            Debug.Assert(m_menu != null, $"{nameof(UIGameMenuEntry)} requires a parent {nameof(UIGameMenu)}.");
            m_button.onClick.AddListener(OnButtonClicked);
            m_text.enabled = false;

            // Disable this menu entry if no "On The Go" CraftingStation has been provided
            if (m_action == EGameMenuAction.OpenCraft && GameManager.Config.onTheGoCraftingStation == null)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (m_button)
            {
                m_button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_text.enabled = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_text.enabled = true;
            m_menu.HandleGameMenuEntrySelected(this);
        }

        internal GameObject GetFocusTarget() => m_button != null ? m_button.gameObject : gameObject;

        private void OnButtonClicked()
        {
            switch (m_action)
            {
                case EGameMenuAction.OpenJournal:
                    GameRuntimeEvents.RequestMenu(EMenu.Journal);
                    break;

                case EGameMenuAction.OpenCharacter:
                    CharacterBase characterMenuActor = GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance();
                    GameRuntimeEvents.RequestCharacterMenu(CharacterMenuContext.ViewCharacter(characterMenuActor));
                    break;

                case EGameMenuAction.OpenCraft:
                    Debug.Assert(GameManager.Config.onTheGoCraftingStation != null, "Cannot open the craft menu with a default recipe book defined in the game config!");
                    CharacterBase craftingActor = GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance();
                    GameRuntimeEvents.RequestCraft(
                        GameManager.Config.onTheGoCraftingStation,
                        GameCommandContext.ResolveForActor(craftingActor));
                    break;

                case EGameMenuAction.OpenSaveMenu:
                    GameRuntimeEvents.RequestMenu(EMenu.Save);
                    break;

                case EGameMenuAction.OpenInventory:
                    CharacterBase inventoryActor = GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance();
                    GameRuntimeEvents.RequestInventory(InventoryMenuContext.ViewCharacter(inventoryActor));
                    break;

                case EGameMenuAction.OpenAbilities:
                    CharacterBase abilitiesMenuActor = GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance();
                    GameRuntimeEvents.RequestAbilitiesMenu(CharacterMenuContext.ViewCharacter(abilitiesMenuActor));
                    break;

                case EGameMenuAction.OpenSettings:
                    GameRuntimeEvents.RequestMenu(EMenu.Settings);
                    break;

                case EGameMenuAction.GoToMainMenu:
                    GameRuntimeEvents.RequestReturnToMainMenu();
                    break;
            }
        }
    }
}


