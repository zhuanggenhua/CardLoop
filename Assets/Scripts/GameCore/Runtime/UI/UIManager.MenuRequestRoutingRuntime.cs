using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YokiFrame;

namespace GameCore
{
    public sealed partial class UIManager
    {
        private readonly InputActionReleaseGate m_cancelReleaseGate = new();

        /// <summary>
        /// 只负责把正式输入和公开菜单请求路由到当前菜单运行时。
        /// 不承担菜单注册重建，也不承担面板栈和 close task 编排。
        /// </summary>
        private void OnCancelReleased(InputAction.CallbackContext context)
        {
            m_cancelReleaseGate.NotifyReleased(context.action);
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (m_cancelReleaseGate.IsBlocked(context.action))
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

        internal Task<bool> OpenMenuAsync(EMenu menu)
        {
            StartMenuRuntime();

            var menuClosedTask = new TaskCompletionSource<bool>();
            if (!m_menuRegistrations.TryGetValue(menu, out UIKitMenuRegistration registration))
            {
                Debug.LogError($"[{nameof(UIManager)}] 菜单 {menu} 没有对应的 UIKit 面板注册。", this);
                menuClosedTask.TrySetResult(false);
                return menuClosedTask.Task;
            }

            OpenRegisteredPanel(registration, menuClosedTask);
            return menuClosedTask.Task;
        }

        internal void CloseAllMenus()
        {
            StartMenuRuntime();

            while (PopCurrentPanel())
            {
            }
        }

        internal bool CloseCurrentMenu()
        {
            StartMenuRuntime();
            return PopCurrentPanel();
        }
    }
}
