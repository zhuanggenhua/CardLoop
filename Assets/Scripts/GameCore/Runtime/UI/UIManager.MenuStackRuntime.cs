using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    public sealed partial class UIManager
    {
        private readonly Dictionary<int, TaskCompletionSource<bool>> m_closeTasks = new();

        /// <summary>
        /// 只负责运行时会话、菜单栈和关闭任务。
        /// 这样菜单声明、请求路由和栈会话可以各自演进，而不会继续挤在一个主文件里。
        /// </summary>
        private void OpenRegisteredPanel(UIKitMenuRegistration registration, TaskCompletionSource<bool> menuClosedTask, params object[] arguments)
        {
            string stackName = GetStackName();
            int previousDepth = UIKit.GetStackDepth(stackName);

            if (UIRoot.Instance == null)
            {
                Debug.LogError($"[{nameof(UIManager)}] 缺少可用的 UIKit 根节点，无法打开正式菜单面板。", this);
                menuClosedTask?.TrySetResult(false);
                return;
            }

            UIKit.OpenPanelAsync(registration.PanelType, registration.Level, new UIKitMenuOpenData(arguments), panel =>
            {
                if (panel is not UIKitMenuPanelBase menuPanel)
                {
                    Debug.LogError($"[{nameof(UIManager)}] 打开的 UIKit 面板不是 {nameof(UIKitMenuPanelBase)}：{registration.PanelType.FullName}", this);
                    AbortPendingRuntimeSession(previousDepth, menuClosedTask, panel);
                    return;
                }

                UIKitMenuPanelBase previousTop = UIKit.PeekPanel(stackName) as UIKitMenuPanelBase;
                previousTop?.ApplyMenuStackInteractions(false);

                if (previousDepth == 0)
                {
                    GameManager.GameStateSystem.AddLayer(EGameState.Menu);
                }

                BindCloseTask(menuPanel, menuClosedTask);
                UIKit.PushPanel(menuPanel, stackName, true);
                menuPanel.NotifyPushedToMenuStack();
                menuPanel.ApplyMenuStackInteractions(true);
                GameManager.InputSystem.PrepareUIReleaseGate(m_cancelReleaseGate, EUIInputAction.Cancel);
                menuPanel.TryFocusDefaultTarget();
            });
        }

        private bool PopCurrentPanel()
        {
            string stackName = GetStackName();
            UIKitMenuPanelBase currentPanel = UIKit.PeekPanel(stackName) as UIKitMenuPanelBase;
            if (currentPanel == null)
            {
                return false;
            }

            IPanel popped = UIKit.PopPanel(stackName, true, true);
            if (popped is not UIKitMenuPanelBase poppedPanel)
            {
                return false;
            }

            poppedPanel.NotifyPoppedFromMenuStack();
            ResolveCloseTask(poppedPanel);
            GameRuntimeEvents.NotifyItemDetailsClosed();

            UIKitMenuPanelBase nextPanel = UIKit.PeekPanel(stackName) as UIKitMenuPanelBase;
            if (nextPanel != null)
            {
                nextPanel.ApplyMenuStackInteractions(true);
                nextPanel.TryFocusDefaultTarget();
            }
            else
            {
                GameManager.GameStateSystem.RemoveLayer(EGameState.Menu);
            }

            return true;
        }

        private void BindCloseTask(UIKitMenuPanelBase panel, TaskCompletionSource<bool> menuClosedTask)
        {
            if (panel == null || menuClosedTask == null)
            {
                return;
            }

            int panelId = GetPanelObjectId(panel);
            m_closeTasks[panelId] = menuClosedTask;
            panel.OnClosed(() => ResolveCloseTask(panel));
        }

        private void ResolveCloseTask(UIKitMenuPanelBase panel)
        {
            if (panel == null)
            {
                return;
            }

            int panelId = GetPanelObjectId(panel);
            if (m_closeTasks.TryGetValue(panelId, out TaskCompletionSource<bool> closeTask))
            {
                m_closeTasks.Remove(panelId);
                closeTask.TrySetResult(true);
            }
        }

        private void ResolveAllCloseTasks()
        {
            foreach (TaskCompletionSource<bool> closeTask in m_closeTasks.Values)
            {
                closeTask.TrySetResult(false);
            }

            m_closeTasks.Clear();
        }

        private string GetStackName()
        {
            return string.IsNullOrWhiteSpace(m_stackName) ? DefaultStackName : m_stackName.Trim();
        }

        private static int GetPanelObjectId(UnityEngine.Object panel)
        {
#if UNITY_6000_0_OR_NEWER
            return panel != null ? panel.GetEntityId().GetHashCode() : 0;
#else
            return panel != null ? panel.GetInstanceID() : 0;
#endif
        }

        private void AbortPendingRuntimeSession(int previousDepth, TaskCompletionSource<bool> menuClosedTask, IPanel openedPanel = null)
        {
            string stackName = GetStackName();

            while (UIKit.GetStackDepth(stackName) > previousDepth)
            {
                UIKit.PopPanel(stackName, true, true);
            }

            openedPanel?.Close();
            menuClosedTask?.TrySetResult(false);
        }
    }
}
