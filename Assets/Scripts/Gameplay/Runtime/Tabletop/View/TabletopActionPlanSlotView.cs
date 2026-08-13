using System;
using Gameplay.Tabletop.Actions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Tabletop
{
    /// <summary>一个行动槽位的 UI 投影，也是牌桌卡牌的明确释放目标。</summary>
    [DisallowMultipleComponent]
    public sealed class TabletopActionPlanSlotView : MonoBehaviour, ITabletopCardDropTarget
    {
        [SerializeField]
        [Tooltip("槽位名称与已填数量。")]
        private TMP_Text m_label;

        [SerializeField]
        [Tooltip("移除当前槽位最后一张卡。")]
        private Button m_removeButton;

        private TabletopActionPlanPanel m_panel;
        private ActionPlanBinding m_binding;

        private void Awake()
        {
            if (m_label == null || m_removeButton == null)
            {
                throw new InvalidOperationException("行动计划槽位模板缺少文本或移除按钮。");
            }
            m_removeButton.onClick.AddListener(RemoveLastCard);
        }

        internal void Bind(TabletopActionPlanPanel panel, ActionPlanBinding binding)
        {
            if (m_panel != null || m_binding != null)
            {
                throw new InvalidOperationException("行动计划槽位视图已经绑定。");
            }
            m_panel = panel ?? throw new ArgumentNullException(nameof(panel));
            m_binding = binding ?? throw new ArgumentNullException(nameof(binding));
        }

        public void AcceptCard(TabletopCardId cardId)
        {
            m_panel.AddCard(m_binding, cardId);
        }

        internal void Refresh()
        {
            int maximum = m_binding.Slot.MaximumParticipants;
            string maximumText = maximum == 0 ? "不限" : maximum.ToString();
            m_label.text = $"{m_binding.Slot.DisplayName}  {m_binding.CardIds.Count}/{maximumText}";
            m_removeButton.interactable = m_binding.CardIds.Count > 0;
        }

        private void RemoveLastCard()
        {
            m_panel.RemoveLastCard(m_binding);
        }

        private void OnDestroy()
        {
            if (m_removeButton != null)
            {
                m_removeButton.onClick.RemoveListener(RemoveLastCard);
            }
        }
    }
}
