using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 菜单语义运行时入口。
    /// 这里只负责把项目菜单请求接到 UIKit 原生 panel 机制，不复制第二套路由或菜单栈真相。
    /// </summary>
    public sealed partial class UIManager
    {
        private const string DefaultStackName = "game-menu";
        private bool m_menuRuntimeStarted;

        private void StartMenuRuntime()
        {
            if (m_menuRuntimeStarted)
            {
                return;
            }

            RebuildRegistrations();
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Performed, OnCancel);
            GameManager.InputSystem.AddUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnCancelReleased);
            m_menuRuntimeStarted = true;
        }

        private void StopMenuRuntime()
        {
            if (!m_menuRuntimeStarted)
            {
                return;
            }

            if (GameManager.Exists() && GameManager.HasSystem<InputSystem>())
            {
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Performed, OnCancel);
                GameManager.InputSystem.RemoveUIActionListener(EUIInputAction.Cancel, EInputActionPhase.Canceled, OnCancelReleased);
            }

            ResolveAllCloseTasks();
            m_menuRuntimeStarted = false;
        }
    }
}
