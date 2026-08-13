using System;
using TMPro;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Tabletop
{
    /// <summary>打开牌桌卡牌详情面板所需的当前牌桌表现对象。</summary>
    public sealed class TabletopCardInfoPanelData : IUIData
    {
        public TabletopView TabletopView { get; }

        public TabletopCardInfoPanelData(TabletopView tabletopView)
        {
            TabletopView = tabletopView ?? throw new ArgumentNullException(nameof(tabletopView));
        }
    }

    /// <summary>
    /// 常驻牌桌卡牌详情投影。它只读取当前可读卡牌，不保存卡牌或规则的第二份状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopCardInfoPanel : UIPanel
    {
        [Header("面板组件")]
        [SerializeField]
        [Tooltip("没有可读卡牌时隐藏的详情内容根节点。")]
        private GameObject m_contentRoot;

        [SerializeField]
        [Tooltip("显示卡牌作者源名称。")]
        private TMP_Text m_titleLabel;

        [SerializeField]
        [Tooltip("显示卡牌作者源描述。")]
        private TMP_Text m_descriptionLabel;

        private TabletopView m_tabletopView;

        /// <summary>当前显示的局内卡牌 ID；空值表示面板没有可读对象。</summary>
        public TabletopCardId DisplayedCardId { get; private set; }

        /// <summary>当前实际显示的标题文本。</summary>
        public string DisplayedTitle => m_titleLabel == null ? string.Empty : m_titleLabel.text;

        /// <summary>当前实际显示的描述文本。</summary>
        public string DisplayedDescription =>
            m_descriptionLabel == null ? string.Empty : m_descriptionLabel.text;

        protected override void OnInit(IUIData data = null)
        {
            if (m_contentRoot == null || m_titleLabel == null || m_descriptionLabel == null)
            {
                throw new InvalidOperationException("牌桌卡牌详情面板预制体缺少必要 UI 引用。");
            }
        }

        protected override void OnOpen(IUIData data = null)
        {
            if (m_tabletopView != null)
            {
                throw new InvalidOperationException("牌桌卡牌详情面板尚未关闭，不能覆盖上一张牌桌。");
            }
            if (data is not TabletopCardInfoPanelData panelData)
            {
                throw new ArgumentException(
                    "牌桌卡牌详情面板必须使用 TabletopCardInfoPanelData 打开。",
                    nameof(data));
            }

            m_tabletopView = panelData.TabletopView;
            m_tabletopView.ReadableCardChanged += Refresh;
            Refresh();
        }

        protected override void OnClose()
        {
            Unbind();
        }

        protected override void ClearUIComponents()
        {
            Unbind();
        }

        private void Refresh()
        {
            if (m_tabletopView == null ||
                !m_tabletopView.TryGetReadableCard(out TabletopCard card, out var definition))
            {
                DisplayedCardId = default;
                m_titleLabel.text = string.Empty;
                m_descriptionLabel.text = string.Empty;
                m_contentRoot.SetActive(false);
                return;
            }

            DisplayedCardId = card.Id;
            m_titleLabel.text = definition.DisplayName;
            m_descriptionLabel.text = definition.Description;
            m_contentRoot.SetActive(true);
        }

        private void Unbind()
        {
            if (m_tabletopView != null)
            {
                m_tabletopView.ReadableCardChanged -= Refresh;
                m_tabletopView = null;
            }

            DisplayedCardId = default;
            if (m_contentRoot != null)
            {
                m_contentRoot.SetActive(false);
            }
            if (m_titleLabel != null)
            {
                m_titleLabel.text = string.Empty;
            }
            if (m_descriptionLabel != null)
            {
                m_descriptionLabel.text = string.Empty;
            }
        }
    }
}
