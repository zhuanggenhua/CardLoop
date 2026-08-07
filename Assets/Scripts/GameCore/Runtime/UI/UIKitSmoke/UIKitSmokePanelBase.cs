using System;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// UIKit 面板栈 smoke 的最小生命周期快照。
    /// 这里只记录面板合同，不承载任何玩法状态。
    /// </summary>
    [Serializable]
    public struct UIKitSmokePanelSnapshot
    {
        public int InitCount;
        public int OpenCount;
        public int WillShowCount;
        public int DidShowCount;
        public int WillHideCount;
        public int DidHideCount;
        public int CloseCount;
        public int FocusCount;
        public int BlurCount;
        public int ResumeCount;
        public bool IsActive;
        public bool DefaultSelectableAssigned;
        public string StateName;
    }

    /// <summary>
    /// UIKit 面板栈 smoke 专用面板基类。
    /// 它的唯一职责是把 UIKit 生命周期回调压成可审计快照，供编辑器 smoke 验证读取。
    /// </summary>
    public abstract class UIKitSmokePanelBase : UIPanel
    {
        private UIKitSmokePanelSnapshot m_snapshot;

        protected override void Awake()
        {
            base.Awake();

            if (GetDefaultSelectable() == null)
            {
                Selectable fallbackSelectable = GetComponentInChildren<Selectable>(true);
                if (fallbackSelectable != null)
                {
                    SetDefaultSelectable(fallbackSelectable);
                }
            }

            SetAutoFocusOnShow(true);
        }

        public UIKitSmokePanelSnapshot CreateSnapshot()
        {
            RefreshSnapshotState();
            m_snapshot.DefaultSelectableAssigned = GetDefaultSelectable() != null;
            return m_snapshot;
        }

        /// <summary>
        /// smoke 关闭后必须立即销毁，避免把测试面板留在缓存里污染正式 UI 栈。
        /// </summary>
        public void ForceTemporaryCacheMode()
        {
            if (Handler != null)
            {
                Handler.CacheMode = PanelCacheMode.Temporary;
            }
        }

        protected override void OnInit(IUIData data = null)
        {
            m_snapshot.InitCount++;
            RefreshSnapshotState();
        }

        protected override void OnOpen(IUIData data = null)
        {
            m_snapshot.OpenCount++;
            RefreshSnapshotState();
        }

        protected override void OnWillShow()
        {
            m_snapshot.WillShowCount++;
            RefreshSnapshotState();
        }

        protected override void OnDidShow()
        {
            m_snapshot.DidShowCount++;
            RefreshSnapshotState();
        }

        protected override void OnWillHide()
        {
            m_snapshot.WillHideCount++;
            RefreshSnapshotState();
        }

        protected override void OnDidHide()
        {
            m_snapshot.DidHideCount++;
            RefreshSnapshotState();
        }

        protected override void OnClose()
        {
            m_snapshot.CloseCount++;
            RefreshSnapshotState();
        }

        protected override void OnFocus()
        {
            base.OnFocus();
            m_snapshot.FocusCount++;
            RefreshSnapshotState();
        }

        protected override void OnBlur()
        {
            base.OnBlur();
            m_snapshot.BlurCount++;
            RefreshSnapshotState();
        }

        protected override void OnResume()
        {
            base.OnResume();
            m_snapshot.ResumeCount++;
            RefreshSnapshotState();
        }

        private void RefreshSnapshotState()
        {
            m_snapshot.IsActive = gameObject.activeSelf;
            m_snapshot.StateName = State.ToString();
        }
    }
}
