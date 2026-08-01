using UnityEngine;
using UnityEngine.InputSystem;
using YokiFrame;

namespace FantasyWord.GameCore
{
    public sealed partial class UIManager
    {
        private readonly InputActionReleaseGate m_cancelReleaseGate = new();

        /// <summary>
        /// 只负责把正式输入和正式事件路由到当前菜单运行时。
        /// 不承担菜单注册重建，也不承担面板栈和 close task 编排。
        /// </summary>
        private void OnCancelReleased(InputAction.CallbackContext context)
        {
            m_cancelReleaseGate.NotifyReleased(context.action);
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (m_cancelReleaseGate.IsBlocked(context.action) ||
                GameManager.DialogueSystem.IsPlaying())
            {
                return;
            }

            UIKitMenuPanelBase currentPanel = UIKit.PeekPanel(GetStackName()) as UIKitMenuPanelBase;
            if (currentPanel == null)
            {
                return;
            }

            if (currentPanel.TryHandleBackRequest())
            {
                return;
            }

            if (!currentPanel.AllowsStackClose())
            {
                return;
            }

            PopCurrentPanel();
        }

        private void OnMenuRequested(MenuRequestedEvent menuRequestedEvent)
        {
            if (!m_menuRegistrations.TryGetValue(menuRequestedEvent.Menu, out UIKitMenuRegistration registration))
            {
                Debug.LogError($"[{nameof(UIManager)}] 菜单 {menuRequestedEvent.Menu} 已路由到当前正式菜单运行时，但没有对应的 UIKit 面板注册。", this);
                menuRequestedEvent.MenuClosedTask?.TrySetResult(false);
                return;
            }

            OpenRegisteredPanel(registration, menuRequestedEvent.MenuClosedTask);
        }

        private void OnShopRequested(ShopRequestedEvent shopRequestedEvent)
        {
            if (m_shopRegistration == null)
            {
                Debug.LogError($"[{nameof(UIManager)}] 商店菜单已路由到当前正式菜单运行时，但没有对应的 UIKit 面板注册。", this);
                shopRequestedEvent.MenuClosedTask?.TrySetResult(false);
                return;
            }

            OpenRegisteredPanel(m_shopRegistration, shopRequestedEvent.MenuClosedTask, shopRequestedEvent.Shop, shopRequestedEvent.CommandContext);
        }

        private void OnInventoryMenuRequested(InventoryMenuRequestedEvent inventoryMenuRequestedEvent)
        {
            if (!m_menuRegistrations.TryGetValue(EMenu.Inventory, out UIKitMenuRegistration registration))
            {
                Debug.LogError($"[{nameof(UIManager)}] 库存菜单已路由到当前正式菜单运行时，但没有对应的 UIKit 面板注册。", this);
                inventoryMenuRequestedEvent.MenuClosedTask?.TrySetResult(false);
                return;
            }

            OpenRegisteredPanel(registration, inventoryMenuRequestedEvent.MenuClosedTask, inventoryMenuRequestedEvent.Context);
        }

        private void OnCharacterMenuRequested(CharacterMenuRequestedEvent characterMenuRequestedEvent)
        {
            if (!m_menuRegistrations.TryGetValue(EMenu.Character, out UIKitMenuRegistration registration))
            {
                Debug.LogError($"[{nameof(UIManager)}] 角色菜单已路由到当前正式菜单运行时，但没有对应的 UIKit 面板注册。", this);
                characterMenuRequestedEvent.MenuClosedTask?.TrySetResult(false);
                return;
            }

            OpenRegisteredPanel(registration, characterMenuRequestedEvent.MenuClosedTask, characterMenuRequestedEvent.Context);
        }

        private void OnAbilitiesMenuRequested(AbilitiesMenuRequestedEvent abilitiesMenuRequestedEvent)
        {
            if (!m_menuRegistrations.TryGetValue(EMenu.Abilities, out UIKitMenuRegistration registration))
            {
                Debug.LogError($"[{nameof(UIManager)}] 能力菜单已路由到当前正式菜单运行时，但没有对应的 UIKit 面板注册。", this);
                abilitiesMenuRequestedEvent.MenuClosedTask?.TrySetResult(false);
                return;
            }

            OpenRegisteredPanel(registration, abilitiesMenuRequestedEvent.MenuClosedTask, abilitiesMenuRequestedEvent.Context);
        }

        private void OnCraftRequested(CraftRequestedEvent craftRequestedEvent)
        {
            if (m_craftRegistration == null)
            {
                Debug.LogError($"[{nameof(UIManager)}] 制作菜单已路由到当前正式菜单运行时，但没有对应的 UIKit 面板注册。", this);
                craftRequestedEvent.MenuClosedTask?.TrySetResult(false);
                return;
            }

            OpenRegisteredPanel(m_craftRegistration, craftRequestedEvent.MenuClosedTask, craftRequestedEvent.CraftingStation, craftRequestedEvent.CommandContext);
        }

        private void OnCloseAllMenusRequested(CloseAllMenusRequestedEvent _)
        {
            while (PopCurrentPanel())
            {
            }
        }
    }
}
