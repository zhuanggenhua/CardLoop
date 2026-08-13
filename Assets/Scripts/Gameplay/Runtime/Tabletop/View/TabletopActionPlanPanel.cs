using System;
using System.Collections.Generic;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Tabletop
{
    /// <summary>UIKit 填槽面板的打开数据，只引用牌桌拥有的正式行动计划。</summary>
    public sealed class TabletopActionPlanPanelData : IUIData
    {
        public ScenarioRun ScenarioRun { get; }

        public ActionPlan Plan { get; }

        public TabletopActionPlanPanelData(ScenarioRun scenarioRun, ActionPlan plan)
        {
            ScenarioRun = scenarioRun ?? throw new ArgumentNullException(nameof(scenarioRun));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }
    }

    /// <summary>
    /// 把一项牌桌行动计划投影为可填槽面板；卡牌绑定、取消和提交仍写入所属牌桌。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopActionPlanPanel : UIPanel
    {
        [SerializeField]
        [Tooltip("显示当前计划的行动名称。")]
        private TMP_Text m_titleLabel;

        [SerializeField]
        [Tooltip("承载行动槽位行。")]
        private RectTransform m_slotRoot;

        [SerializeField]
        [Tooltip("行动槽位行模板；模板本身必须保持隐藏。")]
        private TabletopActionPlanSlotView m_slotTemplate;

		[SerializeField]
		[Tooltip("显示当前计划在牌桌待计划集合中的位置。")]
		private TMP_Text m_planIndexLabel;

		[SerializeField]
		[Tooltip("切换到上一项待计划。")]
		private Button m_previousPlanButton;

		[SerializeField]
		[Tooltip("切换到下一项待计划。")]
		private Button m_nextPlanButton;

        [SerializeField]
        [Tooltip("计划完整后提交行动。")]
        private Button m_submitButton;

        [SerializeField]
        [Tooltip("取消当前计划并关闭面板。")]
        private Button m_cancelButton;

        private readonly List<TabletopActionPlanSlotView> m_slotViews = new();
        private ScenarioRun m_scenarioRun;
        private ActionPlan m_plan;

        public ActionPlan Plan => m_plan;

        public int SlotCount => m_slotViews.Count;

        protected override void OnInit(IUIData data = null)
        {
			if (m_titleLabel == null || m_slotRoot == null || m_slotTemplate == null ||
				m_planIndexLabel == null || m_previousPlanButton == null || m_nextPlanButton == null ||
                m_submitButton == null || m_cancelButton == null)
            {
                throw new InvalidOperationException("行动计划面板预制体缺少必要 UI 引用。");
            }

            m_slotTemplate.gameObject.SetActive(false);
			m_previousPlanButton.onClick.AddListener(ShowPreviousPlan);
			m_nextPlanButton.onClick.AddListener(ShowNextPlan);
            m_submitButton.onClick.AddListener(SubmitPlan);
            m_cancelButton.onClick.AddListener(CancelPlan);
        }

        protected override void OnOpen(IUIData data = null)
        {
            if (data is not TabletopActionPlanPanelData planData)
            {
                throw new ArgumentException(
                    "行动计划面板必须使用 TabletopActionPlanPanelData 打开。",
                    nameof(data));
            }
			if (m_scenarioRun != null && !ReferenceEquals(m_scenarioRun, planData.ScenarioRun))
			{
				throw new InvalidOperationException("行动计划面板不能同时投影两个剧本单局。");
			}

			m_scenarioRun = planData.ScenarioRun;
            m_plan = planData.Plan;
			RebuildSelectedPlan();
        }

        protected override void OnClose()
        {
            ClearSlots();
            m_scenarioRun = null;
            m_plan = null;
        }

        protected override void ClearUIComponents()
        {
            if (m_submitButton != null)
            {
                m_submitButton.onClick.RemoveListener(SubmitPlan);
            }
			if (m_previousPlanButton != null)
			{
				m_previousPlanButton.onClick.RemoveListener(ShowPreviousPlan);
			}
			if (m_nextPlanButton != null)
			{
				m_nextPlanButton.onClick.RemoveListener(ShowNextPlan);
			}
            if (m_cancelButton != null)
            {
                m_cancelButton.onClick.RemoveListener(CancelPlan);
            }
            ClearSlots();
        }

        internal void AddCard(ActionPlanBinding binding, TabletopCardId cardId)
        {
            RequireOpen();
            m_scenarioRun.Tabletop.AddCardToActionPlan(m_plan, binding.Slot.Key, cardId);
            Refresh();
        }

        internal void RemoveLastCard(ActionPlanBinding binding)
        {
            RequireOpen();
            if (binding.CardIds.Count == 0)
            {
                throw new InvalidOperationException($"行动槽位 {binding.Slot.Key} 当前没有可移除卡牌。");
            }
            m_scenarioRun.Tabletop.RemoveCardFromActionPlan(
                m_plan,
                binding.Slot.Key,
                binding.CardIds[binding.CardIds.Count - 1]);
            Refresh();
        }

        private void SubmitPlan()
        {
            RequireOpen();
            m_scenarioRun.SubmitActionPlan(m_plan);
			ShowRemainingPlanOrClose();
        }

        private void CancelPlan()
        {
            RequireOpen();
            m_scenarioRun.Tabletop.CancelActionPlan(m_plan);
			ShowRemainingPlanOrClose();
        }

        private void Refresh()
        {
            for (int i = 0; i < m_slotViews.Count; i++)
            {
                m_slotViews[i].Refresh();
            }
            m_submitButton.interactable = m_plan.IsReady;
			IReadOnlyList<ActionPlan> plans = m_scenarioRun.Tabletop.ActionPlans;
			int selectedIndex = IndexOfPlan(plans, m_plan);
			if (selectedIndex < 0)
			{
				throw new InvalidOperationException($"当前行动计划 {m_plan.ActionId} 已不属于牌桌。");
			}
			m_planIndexLabel.text = $"{selectedIndex + 1}/{plans.Count}";
			bool hasMultiplePlans = plans.Count > 1;
			m_previousPlanButton.interactable = hasMultiplePlans;
			m_nextPlanButton.interactable = hasMultiplePlans;
        }

		private void ShowPreviousPlan()
		{
			ShowRelativePlan(-1);
		}

		private void ShowNextPlan()
		{
			ShowRelativePlan(1);
		}

		private void ShowRelativePlan(int offset)
		{
			RequireOpen();
			IReadOnlyList<ActionPlan> plans = m_scenarioRun.Tabletop.ActionPlans;
			int selectedIndex = IndexOfPlan(plans, m_plan);
			if (selectedIndex < 0 || plans.Count == 0)
			{
				throw new InvalidOperationException("当前行动计划不再属于牌桌。");
			}
			int nextIndex = (selectedIndex + offset + plans.Count) % plans.Count;
			m_plan = plans[nextIndex];
			RebuildSelectedPlan();
		}

		private void ShowRemainingPlanOrClose()
		{
			IReadOnlyList<ActionPlan> plans = m_scenarioRun.Tabletop.ActionPlans;
			if (plans.Count == 0)
			{
				CloseSelf();
				return;
			}
			m_plan = plans[0];
			RebuildSelectedPlan();
		}

		private void RebuildSelectedPlan()
		{
			ClearSlots();
			m_titleLabel.text = m_plan.Action.DisplayName;
			for (int i = 0; i < m_plan.Bindings.Count; i++)
			{
				TabletopActionPlanSlotView slotView = Instantiate(m_slotTemplate, m_slotRoot);
				slotView.gameObject.name = $"ActionPlanSlot_{i}";
				slotView.Bind(this, m_plan.Bindings[i]);
				slotView.gameObject.SetActive(true);
				m_slotViews.Add(slotView);
			}
			Refresh();
		}

		private static int IndexOfPlan(IReadOnlyList<ActionPlan> plans, ActionPlan plan)
		{
			for (int i = 0; i < plans.Count; i++)
			{
				if (ReferenceEquals(plans[i], plan))
				{
					return i;
				}
			}
			return -1;
		}

        private void RequireOpen()
        {
            if (m_scenarioRun == null || m_plan == null)
            {
                throw new InvalidOperationException("行动计划面板没有绑定当前牌桌计划。");
            }
        }

        private void ClearSlots()
        {
            for (int i = 0; i < m_slotViews.Count; i++)
            {
                if (m_slotViews[i] != null)
                {
                    Destroy(m_slotViews[i].gameObject);
                }
            }
            m_slotViews.Clear();
        }
    }

}
