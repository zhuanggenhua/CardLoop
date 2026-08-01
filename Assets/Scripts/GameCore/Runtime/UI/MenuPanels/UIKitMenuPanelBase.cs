using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// UIKit 正式菜单面板基类。
    /// 它只承接历史 AUIMenu 体系里仍然属于菜单面板的那部分语义，
    /// 不负责菜单请求入口、菜单栈拥有权、GameState 层切换或任何玩法真相。
    /// </summary>
    public abstract class UIKitMenuPanelBase : UIPanel
    {
        private UIKitMenuOpenData m_openData = UIKitMenuOpenData.Empty;

        protected sealed override void Awake()
        {
            base.Awake();

            EnsureDefaultSelectable();
            OnPanelAwake();
        }

        protected virtual void OnPanelAwake()
        {
        }

        protected sealed override void OnInit(IUIData data = null)
        {
            OnPanelInit();
        }

        protected sealed override void OnOpen(IUIData data = null)
        {
            m_openData = data as UIKitMenuOpenData ?? UIKitMenuOpenData.Empty;
            OnPanelOpened(m_openData);
        }

        protected sealed override void OnDidShow()
        {
            OnPanelShown(m_openData);
        }

        protected sealed override void OnDidHide()
        {
            OnPanelHidden();
        }

        /// <summary>
        /// 面板只初始化一次的入口，对齐旧 AUIMenu.OnInit 语义。
        /// </summary>
        protected virtual void OnPanelInit()
        {
        }

        /// <summary>
        /// 面板收到一次新的打开请求时调用。
        /// </summary>
        protected virtual void OnPanelOpened(UIKitMenuOpenData openData)
        {
        }

        /// <summary>
        /// 面板真正显示完成后调用。
        /// </summary>
        protected virtual void OnPanelShown(UIKitMenuOpenData openData)
        {
        }

        /// <summary>
        /// 面板真正隐藏完成后调用。
        /// </summary>
        protected virtual void OnPanelHidden()
        {
        }

        /// <summary>
        /// 对齐旧 AUIMenu.OnMenuPushed 语义。
        /// </summary>
        protected virtual void OnPushedToMenuStack()
        {
        }

        /// <summary>
        /// 对齐旧 AUIMenu.OnMenuPopped 语义。
        /// </summary>
        protected virtual void OnPoppedFromMenuStack()
        {
        }

        /// <summary>
        /// 对齐旧 AUIMenu.OnCancel 语义。返回 true 表示当前面板已消费返回请求。
        /// </summary>
        protected virtual bool HandleBackRequested()
        {
            return false;
        }

        /// <summary>
        /// 对齐旧 AUIMenu.CanPop 语义。返回 false 表示菜单栈不能直接把它弹掉。
        /// </summary>
        protected virtual bool CanCloseFromMenuStack()
        {
            return true;
        }

        /// <summary>
        /// 对齐旧 AUIMenu.FindSomethingToSelect 语义。
        /// 若未重写，则优先用默认 Selectable。
        /// </summary>
        protected virtual GameObject ResolveDefaultFocusTarget()
        {
            return GetDefaultSelectable() ? GetDefaultSelectable().gameObject : null;
        }

        /// <summary>
        /// 菜单栈控制交互开关时的统一入口。
        /// 默认仍只操作 CanvasGroup，不把玩法逻辑混进来。
        /// </summary>
        protected virtual void SetPanelInteractions(bool enable)
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup)
            {
                canvasGroup.interactable = enable;
            }
        }

        /// <summary>
        /// Unity UI 按钮只能接同步回调；菜单异步流程必须经过这里报告异常，不能使用异步 void 回调。
        /// </summary>
        protected void RunPanelTaskAndReport(Task task, string operationName)
        {
            _ = RunPanelTaskAndReportAsync(task, operationName);
        }

        private async Task RunPanelTaskAndReportAsync(Task task, string operationName)
        {
            try
            {
                if (task != null)
                {
                    await task;
                }
            }
            catch (Exception exception)
            {
                string operation = string.IsNullOrWhiteSpace(operationName) ? "菜单异步操作" : operationName;
                Debug.LogException(
                    new InvalidOperationException($"[{GetType().Name}] {operation} 执行失败。", exception),
                    this);
            }
        }

        public bool TryFocusDefaultTarget()
        {
            EnsureDefaultSelectable();
            RefreshPanelLayout();

            GameObject target = ResolveDefaultFocusTarget();
            if (target == null)
            {
                return false;
            }

            UIKit.SetFocus(target);
            return UIKit.GetCurrentFocus() == target;
        }

        internal void NotifyPushedToMenuStack()
        {
            OnPushedToMenuStack();
        }

        internal void NotifyPoppedFromMenuStack()
        {
            OnPoppedFromMenuStack();
        }

        internal bool TryHandleBackRequest()
        {
            return HandleBackRequested();
        }

        internal bool AllowsStackClose()
        {
            return CanCloseFromMenuStack();
        }

        internal void ApplyMenuStackInteractions(bool enable)
        {
            SetPanelInteractions(enable);
        }

        private void EnsureDefaultSelectable()
        {
            if (GetDefaultSelectable() != null)
            {
                return;
            }

            Selectable fallbackSelectable = GetComponentInChildren<Selectable>(true);
            if (fallbackSelectable != null)
            {
                SetDefaultSelectable(fallbackSelectable);
            }
        }

        private void RefreshPanelLayout()
        {
            if (transform is not RectTransform panelRoot)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            foreach (RectTransform rectTransform in panelRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rectTransform.GetComponent<LayoutGroup>() != null ||
                    rectTransform.GetComponent<ContentSizeFitter>() != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            Canvas.ForceUpdateCanvases();
        }
    }
}
