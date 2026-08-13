using System;
using System.Collections.Generic;
using GameCore;
using Gameplay.Tabletop.Actions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 一次牌桌拖拽释放产生的短暂行动选择数据。
    /// 它只保存本次可见候选、当前牌桌和屏幕锚点，不拥有行动状态或候选解析职责。
    /// </summary>
    public sealed class TabletopActionChoicePanelData : IUIData
    {
        private readonly ActionCandidate[] m_candidates;

        public IReadOnlyList<ActionCandidate> Candidates => m_candidates;

        public Vector2 ScreenAnchor { get; }

		public Action<ActionCandidate> CandidateSelected { get; }

        public TabletopActionChoicePanelData(
            IReadOnlyList<ActionCandidate> candidates,
            Vector2 screenAnchor,
			Action<ActionCandidate> candidateSelected)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }
            if (candidates.Count == 0)
            {
                throw new ArgumentException("行动选择面板不能打开为空候选集合。", nameof(candidates));
            }

            m_candidates = new ActionCandidate[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                m_candidates[i] = candidates[i] ??
                    throw new ArgumentException($"行动选择候选第 {i + 1} 项为空。", nameof(candidates));
            }
            ScreenAnchor = screenAnchor;
			CandidateSelected = candidateSelected ??
				throw new ArgumentNullException(nameof(candidateSelected));
        }
    }

    /// <summary>
    /// UIKit 的牌桌行动选择面板。
    /// 点击就绪候选时只向当前牌桌提交既有 <see cref="ActionRequest"/>，不保存第二份行动或输入状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopActionChoicePanel : UIPanel, ICancelHandler
    {
        [Header("面板组件")]
        [SerializeField]
        [Tooltip("承载候选按钮的弹窗窗口。")]
        private RectTransform m_window;

        [SerializeField]
        [Tooltip("弹窗标题文本。")]
        private TMP_Text m_titleLabel;

        [SerializeField]
        [Tooltip("动态候选按钮的父节点。")]
        private RectTransform m_choiceRoot;

        [SerializeField]
        [Tooltip("候选按钮模板。模板本身必须保持隐藏。")]
        private Button m_choiceTemplate;

        [SerializeField]
        [Tooltip("关闭当前候选选择而不创建行动实例的按钮。")]
        private Button m_cancelButton;

        private readonly List<Button> m_choiceButtons = new();

        private Action<ActionCandidate> m_candidateSelected;
        private bool m_hasDialogueLayer;

        /// <summary>当前面板实际显示的行动候选数量，只反映 UI 临时状态。</summary>
        public int ChoiceCount => m_choiceButtons.Count;

        protected override void OnInit(IUIData data = null)
        {
            if (m_window == null || m_titleLabel == null || m_choiceRoot == null ||
                m_choiceTemplate == null || m_cancelButton == null)
            {
                throw new InvalidOperationException("行动选择面板预制体缺少必要 UI 引用。");
            }

            m_choiceTemplate.gameObject.SetActive(false);
            m_cancelButton.onClick.AddListener(CloseSelf);
        }

        protected override void OnOpen(IUIData data = null)
        {
            if (m_candidateSelected != null || m_hasDialogueLayer)
            {
                throw new InvalidOperationException("行动选择面板尚未关闭，不能覆盖上一轮候选。");
            }
            if (data is not TabletopActionChoicePanelData choiceData)
            {
                throw new ArgumentException(
                    "行动选择面板必须使用 TabletopActionChoicePanelData 打开。",
                    nameof(data));
            }

            m_candidateSelected = choiceData.CandidateSelected;
            m_titleLabel.text = "可用行动";
            for (int i = 0; i < choiceData.Candidates.Count; i++)
            {
                AddChoice(choiceData.Candidates[i]);
            }
            PositionWindow(choiceData.ScreenAnchor);

            GameManager.GameStateSystem.AddLayer(EGameState.Dialogue);
            m_hasDialogueLayer = true;
        }

        protected override void OnClose()
        {
            if (!m_hasDialogueLayer)
            {
                throw new InvalidOperationException("行动选择面板关闭时缺少自身创建的 UI 输入层。 ");
            }
            if (GameManager.GameStateSystem.currentState != EGameState.Dialogue)
            {
                throw new InvalidOperationException("行动选择面板只能移除自己位于栈顶的 UI 输入层。 ");
            }

            GameManager.GameStateSystem.RemoveLayer(EGameState.Dialogue);
            m_hasDialogueLayer = false;
            m_candidateSelected = null;
            ClearChoiceButtons();
        }

        protected override void ClearUIComponents()
        {
            if (m_cancelButton != null)
            {
                m_cancelButton.onClick.RemoveListener(CloseSelf);
            }
            ClearChoiceButtons();
        }

        public void OnCancel(BaseEventData eventData)
        {
            CloseSelf();
        }

        private void AddChoice(ActionCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            Button choiceButton = Instantiate(m_choiceTemplate, m_choiceRoot);
            choiceButton.gameObject.name = $"ActionChoice_{m_choiceButtons.Count}";
            TMP_Text choiceLabel = choiceButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (choiceLabel == null)
            {
                throw new InvalidOperationException("行动选择按钮模板缺少 TMP 文本组件。");
            }

            choiceLabel.text = candidate.IsReady
                ? candidate.Action.DisplayName
                : $"{candidate.Action.DisplayName}  (还需 {candidate.MissingParticipantCount})";
            choiceButton.onClick.AddListener(delegate { SelectCandidate(candidate); });
            choiceButton.gameObject.SetActive(true);
            m_choiceButtons.Add(choiceButton);
        }

        private void SelectCandidate(ActionCandidate candidate)
        {
            if (m_candidateSelected == null)
            {
                throw new InvalidOperationException("行动选择面板没有绑定玩家选择回调。");
            }
            Action<ActionCandidate> candidateSelected = m_candidateSelected;
            CloseSelf();
            candidateSelected(candidate);
        }

        private void PositionWindow(Vector2 screenAnchor)
        {
            RectTransform parent = m_window.parent as RectTransform;
            if (parent == null)
            {
                throw new InvalidOperationException("行动选择面板窗口必须位于 RectTransform 父节点下。");
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenAnchor,
                    eventCamera,
                    out Vector2 localAnchor))
            {
                throw new InvalidOperationException("无法把行动释放位置转换为候选面板坐标。");
            }

            Vector2 halfWindowSize = m_window.rect.size * 0.5f;
            Rect parentRect = parent.rect;

            // 选择面板承载释放目标的快捷动作，默认放在目标侧边，不能覆盖目标本体。
            const float sideMargin = 160f;
            float rightPosition = localAnchor.x + halfWindowSize.x + sideMargin;
            float leftPosition = localAnchor.x - halfWindowSize.x - sideMargin;
            if (rightPosition + halfWindowSize.x <= parentRect.xMax)
            {
                localAnchor.x = rightPosition;
            }
            else if (leftPosition - halfWindowSize.x >= parentRect.xMin)
            {
                localAnchor.x = leftPosition;
            }

            localAnchor.x = Mathf.Clamp(
                localAnchor.x,
                parentRect.xMin + halfWindowSize.x,
                parentRect.xMax - halfWindowSize.x);
            localAnchor.y = Mathf.Clamp(
                localAnchor.y,
                parentRect.yMin + halfWindowSize.y,
                parentRect.yMax - halfWindowSize.y);
            m_window.anchoredPosition = localAnchor;
        }

        private void ClearChoiceButtons()
        {
            for (int i = 0; i < m_choiceButtons.Count; i++)
            {
                Button choiceButton = m_choiceButtons[i];
                if (choiceButton != null)
                {
                    Destroy(choiceButton.gameObject);
                }
            }
            m_choiceButtons.Clear();
        }
    }
}
