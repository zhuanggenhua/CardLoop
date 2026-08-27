using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using GameCore;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 当前牌桌的玩家交互组件，把指针释放解释为空白放置或行动选择。
    /// 权威牌桌状态与行动实例仍由 <see cref="ScenarioRun"/> 提交。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopInteraction : MonoBehaviour
    {
        private ScenarioRun m_scenarioRun;
        private TabletopActionChoicePanel m_openChoicePanel;
		private TabletopActionPlanPanel m_openPlanPanel;

        public bool IsBound => m_scenarioRun != null;

        public void Bind(ScenarioRun scenarioRun)
        {
            if (scenarioRun == null)
            {
                throw new ArgumentNullException(nameof(scenarioRun));
            }
            if (m_scenarioRun != null)
            {
                throw new InvalidOperationException("牌桌交互尚未解绑，不能覆盖上一局剧本。");
            }
            if (!ReferenceEquals(scenarioRun.Tabletop, GetComponent<TabletopView>()?.BoundTabletop))
            {
                throw new InvalidOperationException("牌桌交互、牌桌视图和当前剧本必须指向同一张活动牌桌。");
            }

            m_scenarioRun = scenarioRun;
        }

        public void Unbind()
        {
            if (m_scenarioRun == null)
            {
                return;
            }

            if (m_openChoicePanel != null)
            {
                UIKit.ClosePanel(m_openChoicePanel);
            }
			if (m_openPlanPanel != null)
			{
				UIKit.ClosePanel(m_openPlanPanel);
			}
            m_scenarioRun = null;
        }

        /// <summary>
        /// 处理一次正式指针释放，并返回本次得到的候选快照供直接调用方观察。
        /// </summary>
        public ActionCandidate[] HandleRelease(TabletopCardPointerReleaseIntent intent)
        {
            ScenarioRun scenarioRun = m_scenarioRun ??
                throw new InvalidOperationException("牌桌交互尚未绑定活动剧本。");
            if (!intent.IsDrag)
            {
				ActionCandidate[] clickCandidates = scenarioRun.FindActionCandidates(intent);
				if (TryStartStackCraftClickableAction(scenarioRun, intent, clickCandidates))
				{
					return clickCandidates;
				}
				return PresentActionCandidates(clickCandidates);
            }
			if (scenarioRun.Tabletop.TryDropBattleParticipant(
					intent.CardId,
					intent.ReleasePointerPosition,
					intent.RequestedStackPosition,
					out _,
					out _))
			{
				return Array.Empty<ActionCandidate>();
			}
            if (!intent.TargetCardId.IsValid)
            {
                scenarioRun.Tabletop.TryPlaceStack(
                    intent.CardId,
                    intent.RequestedStackPosition,
                    out _);
                return Array.Empty<ActionCandidate>();
            }

			if (scenarioRun.Tabletop.TryDropStackOnto(intent.CardId, intent.TargetCardId, out _))
			{
				return Array.Empty<ActionCandidate>();
			}
			ActionCandidate[] candidates = scenarioRun.FindActionCandidates(intent);
			if (candidates.Length > 0)
			{
				return PresentActionCandidates(candidates);
			}
			scenarioRun.Tabletop.TryPlaceStack(
				intent.CardId,
				intent.RequestedStackPosition,
				out _);
			return Array.Empty<ActionCandidate>();
		}

		/// <summary>
		/// 只读判断一次拖拽目标是否可交互；用于拖拽中的目标高亮，不打开 UI。
		/// </summary>
		public bool CanShowDropTargetHighlight(TabletopCardPointerReleaseIntent intent)
		{
			ScenarioRun scenarioRun = m_scenarioRun ??
				throw new InvalidOperationException("牌桌交互尚未绑定活动剧本。");
			ActionCandidate[] candidates = scenarioRun.FindActionCandidates(intent);
			if (candidates.Length > 0)
			{
				return true;
			}
			return intent.TargetCardId.IsValid &&
				scenarioRun.Tabletop.CanStackOnto(intent.CardId, intent.TargetCardId);
		}

		private static bool TryStartStackCraftClickableAction(
			ScenarioRun scenarioRun,
			TabletopCardPointerReleaseIntent intent,
			IReadOnlyList<ActionCandidate> candidates)
		{
			if (scenarioRun == null)
			{
				throw new ArgumentNullException(nameof(scenarioRun));
			}
			if (candidates == null)
			{
				throw new ArgumentNullException(nameof(candidates));
			}
			if (!IsStackCraftClickableCard(scenarioRun, intent.CardId) ||
				candidates.Count != 1)
			{
				return false;
			}

			ActionCandidate candidate = candidates[0];
			if (!candidate.IsReady ||
				candidate.Action.TurnCost != 0 ||
				!HasStackCraftClickableResult(candidate.Action))
			{
				return false;
			}

			scenarioRun.StartAction(ActionRequest.FromCandidate(candidate));
			return true;
		}

		private static bool IsStackCraftClickableCard(ScenarioRun scenarioRun, TabletopCardId cardId)
		{
			if (!scenarioRun.Tabletop.Cards.TryGetCard(cardId, out TabletopCard card) ||
				!scenarioRun.ContentIndex.TryGet(card.ContentId, out ContentAsset content))
			{
				return false;
			}

			return content is CardPackDefinition or ChestCardDefinition;
		}

		private static bool HasStackCraftClickableResult(ActionDefinition action)
		{
			for (int intentIndex = 0; intentIndex < action.ResultIntents.Count; intentIndex++)
			{
				if (action.ResultIntents[intentIndex] is OpenCardPackResultIntent or
					WithdrawCurrencyFromChestResultIntent)
				{
					return true;
				}
			}
			return false;
		}

		private ActionCandidate[] PresentActionCandidates(ActionCandidate[] candidates)
		{
            if (candidates.Length == 0)
            {
                return candidates;
            }

            Vector2 screenAnchor =
                GameManager.InputSystem.ReadPointerScreenPosition(EActionMap.Gameplay);
            UIKit.OpenPanelAsync<TabletopActionChoicePanel>(
                callback: panel =>
                {
                    if (panel is not TabletopActionChoicePanel choicePanel)
                    {
                        throw new InvalidOperationException(
                            "牌桌行动候选已经产生，但 UIKit 没有加载行动选择面板。");
                    }

                    m_openChoicePanel = choicePanel;
                    choicePanel.OnClosed(() =>
                    {
                        if (ReferenceEquals(m_openChoicePanel, choicePanel))
                        {
                            m_openChoicePanel = null;
                        }
                    });
                },
                level: UILevel.Pop,
                data: new TabletopActionChoicePanelData(
                    candidates,
                    screenAnchor,
					SelectCandidate));
            return candidates;
        }

		private void SelectCandidate(ActionCandidate candidate)
		{
			ScenarioRun scenarioRun = m_scenarioRun ??
				throw new InvalidOperationException("牌桌交互尚未绑定活动剧本。");
			if (candidate.IsReady)
			{
				scenarioRun.StartAction(ActionRequest.FromCandidate(candidate));
				return;
			}

			ActionPlan plan = scenarioRun.CreateActionPlan(candidate);
			UIKit.OpenPanelAsync<TabletopActionPlanPanel>(
				callback: panel =>
				{
					if (panel is not TabletopActionPlanPanel planPanel)
					{
						throw new InvalidOperationException(
							"牌桌已创建行动计划，但 UIKit 没有加载填槽面板。");
					}
					m_openPlanPanel = planPanel;
					planPanel.OnClosed(() =>
					{
						if (ReferenceEquals(m_openPlanPanel, planPanel))
						{
							m_openPlanPanel = null;
						}
					});
				},
				level: UILevel.Pop,
				data: new TabletopActionPlanPanelData(scenarioRun, plan));
		}

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
