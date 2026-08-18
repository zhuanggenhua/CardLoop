using System;
using System.Collections.Generic;
using System.Text;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Scenarios
{
	/// <summary>打开剧本日志所需的活动单局；面板不保存任务或发现状态。</summary>
	public sealed class ScenarioJournalPanelData : IUIData
	{
		public ScenarioRun Run { get; }

		public ScenarioJournalPanelData(ScenarioRun run)
		{
			Run = run ?? throw new ArgumentNullException(nameof(run));
		}
	}

	/// <summary>投影当前单局任务和已发现配方 / 行动的只读剧本日志。</summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioJournalPanel : UIPanel
	{
		private const string UnreadIndicator = " <color=red>●</color>";
		private const string QuestTabText = "任务";
		private const string ActionTabText = "已发现配方 / 行动";

		[SerializeField, LabelText("任务标签按钮")]
		[Tooltip("切换到当前任务列表。")]
		private Button m_questsTabButton;

		[SerializeField, LabelText("配方行动标签按钮")]
		[Tooltip("切换到本局已发现配方 / 行动列表。")]
		private Button m_actionsTabButton;

		[SerializeField, LabelText("标题文本")]
		[Tooltip("显示当前选中日志分类。")]
		private TMP_Text m_titleLabel;

		[SerializeField, LabelText("内容文本")]
		[Tooltip("显示当前任务或已发现配方 / 行动。")]
		private TMP_Text m_contentLabel;

		[SerializeField, LabelText("关闭按钮")]
		[Tooltip("关闭剧本日志。")]
		private Button m_closeButton;

		private ScenarioRun m_run;
		private bool m_showQuests = true;
		private bool m_isSubscribed;
		private TMP_Text m_questsTabLabel;
		private TMP_Text m_actionsTabLabel;

		public int DisplayedQuestCount { get; private set; }

		public int DisplayedActionCount { get; private set; }

		public string DisplayedText => m_contentLabel == null ? string.Empty : m_contentLabel.text;

		protected override void OnInit(IUIData data = null)
		{
			if (m_questsTabButton == null || m_actionsTabButton == null ||
				m_titleLabel == null || m_contentLabel == null || m_closeButton == null)
			{
				throw new InvalidOperationException("剧本日志预制体缺少必要 UI 引用。");
			}

			m_questsTabButton.onClick.AddListener(ShowQuests);
			m_actionsTabButton.onClick.AddListener(ShowActions);
			m_closeButton.onClick.AddListener(CloseSelf);
			m_questsTabLabel = GetRequiredButtonLabel(m_questsTabButton, "任务标签按钮");
			m_actionsTabLabel = GetRequiredButtonLabel(m_actionsTabButton, "配方行动标签按钮");
		}

		protected override void OnOpen(IUIData data = null)
		{
			if (m_run != null || m_isSubscribed)
			{
				throw new InvalidOperationException("剧本日志尚未关闭，不能覆盖上一局剧本。");
			}
			if (data is not ScenarioJournalPanelData panelData || panelData.Run.IsEnded)
			{
				throw new ArgumentException("剧本日志必须使用仍在进行的 ScenarioRun 打开。", nameof(data));
			}

			m_run = panelData.Run;
			m_showQuests = true;
			EventKit.Type.Register<QuestProgressChangedEvent>(OnQuestProgressChanged);
			EventKit.Type.Register<QuestStatusChangedEvent>(OnQuestStatusChanged);
			EventKit.Type.Register<ContentDiscoveredEvent>(OnContentDiscovered);
			EventKit.Type.Register<ScenarioDayCycleChangedEvent>(OnScenarioDayCycleChanged);
			m_isSubscribed = true;
			Refresh();
		}

		protected override void OnClose()
		{
			Unbind();
		}

		protected override void ClearUIComponents()
		{
			m_questsTabButton?.onClick.RemoveListener(ShowQuests);
			m_actionsTabButton?.onClick.RemoveListener(ShowActions);
			m_closeButton?.onClick.RemoveListener(CloseSelf);
			Unbind();
		}

		private void ShowQuests()
		{
			m_showQuests = true;
			Refresh();
		}

		private void ShowActions()
		{
			m_showQuests = false;
			Refresh();
		}

		private void Refresh()
		{
			if (m_run == null)
			{
				return;
			}

			DisplayedQuestCount = m_run.QuestLog.Quests.Count;
			ActionDefinition[] actions = m_run.GetDiscoveredActions();
			DisplayedActionCount = actions.Length;
			bool questsHaveUnreadEntries = HasUnreadVisibleQuests(m_run);
			bool actionsHaveUnreadEntries = HasUnreadActions(m_run, actions);
			List<ContentId> visibleEntryIds = new List<ContentId>();
			m_titleLabel.text = m_showQuests ? "当前任务" : "已发现配方 / 行动";
			m_contentLabel.text = m_showQuests
				? BuildQuestText(m_run, visibleEntryIds)
				: BuildActionText(m_run, actions, visibleEntryIds);
			m_questsTabLabel.text = BuildTabText(QuestTabText, questsHaveUnreadEntries);
			m_actionsTabLabel.text = BuildTabText(ActionTabText, actionsHaveUnreadEntries);
			m_questsTabButton.interactable = !m_showQuests;
			m_actionsTabButton.interactable = m_showQuests;
			MarkVisibleEntriesSeen(visibleEntryIds);
		}

		private static string BuildQuestText(ScenarioRun run, ICollection<ContentId> visibleEntryIds)
		{
			StringBuilder text = new StringBuilder();
			QuestLog questLog = run.QuestLog;
			for (int i = 0; i < questLog.Quests.Count; i++)
			{
				QuestProgress quest = questLog.Quests[i];
				if (quest.Status == QuestStatus.Locked)
				{
					continue;
				}
				ContentId questId = quest.Definition.ContentId;
				visibleEntryIds.Add(questId);
				if (text.Length > 0)
				{
					text.Append("\n\n");
				}
				text.Append(quest.Status == QuestStatus.Completed ? "[已完成] " : "[进行中] ");
				text.Append(quest.Definition.DisplayName);
				AppendUnreadIndicator(text, run, questId);
				if (!string.IsNullOrWhiteSpace(quest.Definition.Description))
				{
					text.Append('\n').Append(quest.Definition.Description);
				}
				for (int taskIndex = 0; taskIndex < quest.Tasks.Count; taskIndex++)
				{
					QuestTaskProgressSnapshot progress = quest.Tasks[taskIndex].Progress;
					text.Append("\n进度 ").Append(taskIndex + 1).Append(": ")
						.Append(progress.CurrentAmount).Append(" / ").Append(progress.RequiredAmount);
				}
			}
			return text.Length == 0 ? "暂无可见任务" : text.ToString();
		}

		private static string BuildActionText(
			ScenarioRun run,
			ActionDefinition[] actions,
			ICollection<ContentId> visibleEntryIds)
		{
			if (actions.Length == 0)
			{
				return "尚未发现配方 / 行动";
			}

			StringBuilder text = new StringBuilder();
			for (int i = 0; i < actions.Length; i++)
			{
				if (i > 0)
				{
					text.Append("\n\n");
				}
				ActionDefinition action = actions[i];
				ContentId actionId = action.ContentId;
				visibleEntryIds.Add(actionId);
				text.Append(action.DisplayName);
				AppendUnreadIndicator(text, run, actionId);
				if (!string.IsNullOrWhiteSpace(action.Description))
				{
					text.Append('\n').Append(action.Description);
				}
			}
			return text.ToString();
		}

		private static string BuildTabText(string text, bool hasUnreadEntries)
		{
			return hasUnreadEntries ? text + UnreadIndicator : text;
		}

		private static void AppendUnreadIndicator(StringBuilder text, ScenarioRun run, ContentId entryId)
		{
			if (!run.IsJournalEntrySeen(entryId))
			{
				text.Append(UnreadIndicator);
			}
		}

		private static bool HasUnreadVisibleQuests(ScenarioRun run)
		{
			IReadOnlyList<QuestProgress> quests = run.QuestLog.Quests;
			for (int i = 0; i < quests.Count; i++)
			{
				QuestProgress quest = quests[i];
				if (quest.Status != QuestStatus.Locked &&
					!run.IsJournalEntrySeen(quest.Definition.ContentId))
				{
					return true;
				}
			}
			return false;
		}

		private static bool HasUnreadActions(ScenarioRun run, IReadOnlyList<ActionDefinition> actions)
		{
			for (int i = 0; i < actions.Count; i++)
			{
				if (!run.IsJournalEntrySeen(actions[i].ContentId))
				{
					return true;
				}
			}
			return false;
		}

		private void MarkVisibleEntriesSeen(IReadOnlyList<ContentId> visibleEntryIds)
		{
			for (int i = 0; i < visibleEntryIds.Count; i++)
			{
				m_run.MarkJournalEntrySeen(visibleEntryIds[i]);
			}
		}

		private static TMP_Text GetRequiredButtonLabel(Button button, string fieldName)
		{
			TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
			if (label == null)
			{
				throw new InvalidOperationException($"剧本日志的{fieldName}缺少文字标签。");
			}
			return label;
		}

		private void OnQuestProgressChanged(QuestProgressChangedEvent changedEvent)
		{
			if (m_run != null && changedEvent.ScenarioId == m_run.ScenarioId)
			{
				Refresh();
			}
		}

		private void OnQuestStatusChanged(QuestStatusChangedEvent changedEvent)
		{
			if (m_run != null && changedEvent.ScenarioId == m_run.ScenarioId)
			{
				Refresh();
			}
		}

		private void OnContentDiscovered(ContentDiscoveredEvent discoveredEvent)
		{
			if (m_run != null && discoveredEvent.ScenarioId == m_run.ScenarioId)
			{
				Refresh();
			}
		}

		private void OnScenarioDayCycleChanged(ScenarioDayCycleChangedEvent changedEvent)
		{
			if (m_run == null || changedEvent.ScenarioId != m_run.ScenarioId ||
				changedEvent.Phase == ScenarioDayCyclePhase.Inactive)
			{
				return;
			}

			CloseSelf();
		}

		private void Unbind()
		{
			if (m_isSubscribed)
			{
				EventKit.Type.UnRegister<QuestProgressChangedEvent>(OnQuestProgressChanged);
				EventKit.Type.UnRegister<QuestStatusChangedEvent>(OnQuestStatusChanged);
				EventKit.Type.UnRegister<ContentDiscoveredEvent>(OnContentDiscovered);
				EventKit.Type.UnRegister<ScenarioDayCycleChangedEvent>(OnScenarioDayCycleChanged);
				m_isSubscribed = false;
			}
			m_run = null;
		}
	}
}
