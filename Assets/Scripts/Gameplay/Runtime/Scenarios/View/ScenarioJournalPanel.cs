using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using GameCore;
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
		private const string ActionTabText = "配方";
		private const string DefaultQuestGroupText = "任务";
		private const string DefaultActionGroupText = "配方";
		private const string SymbolCollapsed = "►";
		private const string SymbolExpanded = "▼";
		private const string SymbolBullet = "•";
		private const string SymbolCompleted = "√";
		private const string MenuOpenLabel = ">>";
		private const string MenuClosedLabel = "<<";
		private const float HeaderFontSize = 35f;
		private const float ItemFontSize = 30f;
		private const float MenuSlideSeconds = 0.5f;
		private static readonly Color HeaderColor = new(0.3f, 0.8f, 1f, 1f);

		[SerializeField, LabelText("菜单容器")]
		[Tooltip("承接 StackCraft MenuToggle 滑动的右侧菜单根节点。")]
		private RectTransform m_menuPanel;

		[SerializeField, LabelText("任务标签 Toggle")]
		[Tooltip("承接 StackCraft QuestsToggle 的选中状态，用于切换到当前任务列表。")]
		private Toggle m_questsTabToggle;

		[SerializeField, LabelText("配方行动标签 Toggle")]
		[Tooltip("承接 StackCraft RecipesToggle 的选中状态，用于切换到本局已发现配方 / 行动列表。")]
		private Toggle m_actionsTabToggle;

		[SerializeField, LabelText("菜单折叠按钮")]
		[Tooltip("折叠或展开右侧剧本日志主体。")]
		private Button m_menuToggleButton;

		[SerializeField, LabelText("菜单折叠文字")]
		[Tooltip("显示 StackCraft MenuToggle 的 >> / << 状态文字。")]
		private TMP_Text m_menuToggleLabel;

		[SerializeField, LabelText("页眉显隐")]
		[Tooltip("承接 StackCraft 右侧菜单页眉的显示状态。")]
		private CanvasGroup m_headerGroup;

		[SerializeField, LabelText("任务视图显隐")]
		[Tooltip("承接 StackCraft QuestsView 的显示状态。")]
		private CanvasGroup m_questsViewGroup;

		[SerializeField, LabelText("配方视图显隐")]
		[Tooltip("承接 StackCraft RecipesView 的显示状态。")]
		private CanvasGroup m_actionsViewGroup;

		[SerializeField, LabelText("任务内容文本")]
		[Tooltip("显示当前任务列表。运行时作为 StackCraft 式条目列表的 Content 根。")]
		private TMP_Text m_questsContentLabel;

		[SerializeField, LabelText("配方行动内容文本")]
		[Tooltip("显示本局已发现配方 / 行动。运行时作为 StackCraft 式条目列表的 Content 根。")]
		private TMP_Text m_actionsContentLabel;

		[SerializeField, LabelText("关闭按钮")]
		[Tooltip("关闭剧本日志。")]
		private Button m_closeButton;

		private readonly List<ScenarioJournalEntryButton> m_questButtons = new();
		private readonly List<ScenarioJournalEntryButton> m_actionButtons = new();
		private readonly Dictionary<string, bool> m_questGroupExpandedByName =
			new Dictionary<string, bool>(StringComparer.Ordinal);
		private readonly Dictionary<string, bool> m_actionGroupExpandedByName =
			new Dictionary<string, bool>(StringComparer.Ordinal);
		private ScenarioRun m_run;
		private bool m_showQuests = true;
		private bool m_isMenuVisible = true;
		private bool m_isMenuAnimating;
		private bool m_isSubscribed;
		private bool m_hasHoveredEntryInfo;
		private ContentId m_hoveredEntryId;
		private TMP_Text m_questsTabLabel;
		private TMP_Text m_actionsTabLabel;
		private string m_displayedTextCache = string.Empty;
		private Tween m_menuTween;

		public int DisplayedQuestCount { get; private set; }

		public int DisplayedActionCount { get; private set; }

		public string DisplayedText => m_displayedTextCache;

		/// <summary>右侧剧本日志菜单是否处于展开状态；收起时面板本体仍保留，按钮继续可点。</summary>
		public bool IsMenuOpen => m_isMenuVisible;

		/// <summary>右侧菜单当前滑动位置；用于验证 StackCraft 同款收起距离。</summary>
		public float MenuPanelAnchoredX => m_menuPanel == null ? 0f : m_menuPanel.anchoredPosition.x;

		/// <summary>右侧菜单折叠按钮当前文案。</summary>
		public string MenuToggleText => m_menuToggleLabel == null ? string.Empty : m_menuToggleLabel.text;

		private TMP_Text ActiveContentLabel => m_showQuests ? m_questsContentLabel : m_actionsContentLabel;

		protected override void OnInit(IUIData data = null)
		{
			if (m_menuPanel == null || m_questsTabToggle == null || m_actionsTabToggle == null ||
				m_menuToggleButton == null || m_menuToggleLabel == null || m_headerGroup == null ||
				m_questsViewGroup == null || m_actionsViewGroup == null ||
				m_questsContentLabel == null || m_actionsContentLabel == null ||
				m_closeButton == null)
			{
				throw new InvalidOperationException("剧本日志预制体缺少必要 UI 引用。");
			}

			EnsureContentListRoot(m_questsContentLabel);
			EnsureContentListRoot(m_actionsContentLabel);
			m_questsTabToggle.onValueChanged.AddListener(OnQuestsTabChanged);
			m_actionsTabToggle.onValueChanged.AddListener(OnActionsTabChanged);
			m_menuToggleButton.onClick.AddListener(ToggleMenuVisibility);
			m_closeButton.onClick.AddListener(CloseSelf);
			m_questsTabLabel = GetRequiredToggleLabel(m_questsTabToggle, "任务标签 Toggle");
			m_actionsTabLabel = GetRequiredToggleLabel(m_actionsTabToggle, "配方行动标签 Toggle");
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
			m_isMenuVisible = true;
			SnapMenuVisibility(isOpen: true);
			m_questGroupExpandedByName.Clear();
			m_actionGroupExpandedByName.Clear();
			m_hasHoveredEntryInfo = false;
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
			m_questsTabToggle?.onValueChanged.RemoveListener(OnQuestsTabChanged);
			m_actionsTabToggle?.onValueChanged.RemoveListener(OnActionsTabChanged);
			m_menuToggleButton?.onClick.RemoveListener(ToggleMenuVisibility);
			m_closeButton?.onClick.RemoveListener(CloseSelf);
			Unbind();
		}

		private void OnQuestsTabChanged(bool isOn)
		{
			if (isOn)
			{
				ShowQuests();
			}
		}

		private void OnActionsTabChanged(bool isOn)
		{
			if (isOn)
			{
				ShowActions();
			}
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

		private void ToggleMenuVisibility()
		{
			SetMenuVisibility(!m_isMenuVisible, animated: true, playSound: false);
		}

		private void Refresh()
		{
			if (m_run == null)
			{
				return;
			}

			List<JournalEntry> questEntries = BuildQuestEntries(m_run);
			ActionDefinition[] actions = m_run.GetDiscoveredActions();
			List<JournalEntry> actionEntries = BuildActionEntries(m_run, actions);
			DisplayedQuestCount = questEntries.Count;
			DisplayedActionCount = actionEntries.Count;
			PopulateList(
				m_questsContentLabel,
				m_questButtons,
				questEntries,
				m_questGroupExpandedByName,
				DefaultQuestGroupText,
				"暂无任务");
			PopulateList(
				m_actionsContentLabel,
				m_actionButtons,
				actionEntries,
				m_actionGroupExpandedByName,
				DefaultActionGroupText,
				"暂无已发现配方");
			RefreshTabLabels();
			ApplyViewVisibility();
			RefreshDisplayedTextCache();
		}

		private void ToggleQuestGroup(string groupName)
		{
			ToggleGroup(m_questGroupExpandedByName, groupName);
		}

		private void ToggleActionGroup(string groupName)
		{
			ToggleGroup(m_actionGroupExpandedByName, groupName);
		}

		private void ToggleGroup(Dictionary<string, bool> expandedByName, string groupName)
		{
			if (!expandedByName.TryGetValue(groupName, out bool isExpanded))
			{
				throw new InvalidOperationException($"剧本日志没有找到可折叠分组：{groupName}。");
			}
			expandedByName[groupName] = !isExpanded;
			Refresh();
		}

		private void PopulateList(
			TMP_Text contentRoot,
			List<ScenarioJournalEntryButton> buttonCache,
			IReadOnlyList<JournalEntry> entries,
			Dictionary<string, bool> expandedByName,
			string defaultGroupName,
			string emptyText)
		{
			ClearButtons(buttonCache);
			contentRoot.text = entries.Count == 0 ? emptyText : string.Empty;
			contentRoot.raycastTarget = false;
			if (entries.Count == 0)
			{
				return;
			}

			List<JournalGroup> groups = BuildJournalGroups(entries, expandedByName, defaultGroupName);
			for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
			{
				JournalGroup group = groups[groupIndex];
				string groupName = group.Name;
				bool isExpanded = group.IsExpanded;
				ScenarioJournalEntryButton headerButton = CreateJournalButton(
					contentRoot,
					$"{groupName} {(isExpanded ? SymbolExpanded : SymbolCollapsed)}",
					HeaderFontSize,
					HeaderColor,
					hoverChanged: null,
					onClick: () =>
					{
						if (ReferenceEquals(expandedByName, m_questGroupExpandedByName))
						{
							ToggleQuestGroup(groupName);
						}
						else
						{
							ToggleActionGroup(groupName);
						}
					});
				buttonCache.Add(headerButton);

				for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
				{
					JournalEntry entry = group.Entries[entryIndex];
					ScenarioJournalEntryButton entryButton = null;
					entryButton = CreateJournalButton(
						contentRoot,
						entry.ListText,
						ItemFontSize,
						Color.white,
						enter => OnEntryHoverChanged(enter, entry, entryButton),
						onClick: null);
					entryButton.gameObject.SetActive(isExpanded);
					buttonCache.Add(entryButton);
				}
			}
		}

		private static List<JournalGroup> BuildJournalGroups(
			IReadOnlyList<JournalEntry> entries,
			Dictionary<string, bool> expandedByName,
			string defaultGroupName)
		{
			List<JournalGroup> groups = new();
			for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
			{
				JournalEntry entry = entries[entryIndex];
				string groupName = ResolveJournalGroupName(entry.GroupName, defaultGroupName);
				if (!expandedByName.ContainsKey(groupName))
				{
					expandedByName.Add(groupName, true);
				}

				JournalGroup group = FindJournalGroup(groups, groupName);
				if (group == null)
				{
					group = new JournalGroup(groupName);
					groups.Add(group);
				}
				group.IsExpanded = expandedByName[groupName];
				group.Entries.Add(entry);
			}
			return groups;
		}

		private static JournalGroup FindJournalGroup(IReadOnlyList<JournalGroup> groups, string groupName)
		{
			for (int i = 0; i < groups.Count; i++)
			{
				if (string.Equals(groups[i].Name, groupName, StringComparison.Ordinal))
				{
					return groups[i];
				}
			}
			return null;
		}

		private static string ResolveJournalGroupName(string configuredGroupName, string defaultGroupName)
		{
			return string.IsNullOrWhiteSpace(configuredGroupName)
				? defaultGroupName
				: configuredGroupName.Trim();
		}

		private ScenarioJournalEntryButton CreateJournalButton(
			TMP_Text template,
			string text,
			float fontSize,
			Color color,
			Action<bool> hoverChanged,
			Action onClick)
		{
			GameObject buttonObject = new(
				"ItemButton (" + StripRichText(text) + ")",
				typeof(RectTransform),
				typeof(TextMeshProUGUI),
				typeof(LayoutElement),
				typeof(ScenarioJournalEntryButton));
			buttonObject.transform.SetParent(template.rectTransform, false);
			RectTransform rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = Vector2.zero;
			rect.sizeDelta = new Vector2(0f, fontSize + 10f);

			TextMeshProUGUI label = buttonObject.GetComponent<TextMeshProUGUI>();
			CopyTextStyle(template, label);
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.overflowMode = TextOverflowModes.Overflow;
			label.horizontalAlignment = HorizontalAlignmentOptions.Left;
			label.verticalAlignment = VerticalAlignmentOptions.Top;

			LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
			layout.minHeight = fontSize + 10f;
			layout.preferredHeight = fontSize + 10f;
			layout.flexibleWidth = 1f;

			if (onClick != null)
			{
				Button button = buttonObject.AddComponent<Button>();
				button.targetGraphic = label;
				button.transition = Selectable.Transition.None;
				button.onClick.AddListener(() => onClick());
				buttonObject.AddComponent<UINavigationTarget>();
			}

			ScenarioJournalEntryButton itemButton = buttonObject.GetComponent<ScenarioJournalEntryButton>();
			itemButton.Initialize(text, fontSize, color, hoverChanged);
			return itemButton;
		}

		private void OnEntryHoverChanged(
			bool enter,
			JournalEntry entry,
			ScenarioJournalEntryButton button)
		{
			if (m_run == null)
			{
				return;
			}

			if (enter)
			{
				EventKit.Type.Send(ScenarioJournalEntryInfoEvent.Show(
					m_run.ScenarioId,
					entry.Id,
					entry.InfoHeader,
					entry.InfoBody));
				m_hasHoveredEntryInfo = true;
				m_hoveredEntryId = entry.Id;
				if (m_run.MarkJournalEntrySeen(entry.Id))
				{
					button.SetText(RemoveUnreadIndicator(button.Text));
					RefreshTabLabels();
					RefreshDisplayedTextCache();
				}
				return;
			}

			if (m_hasHoveredEntryInfo && m_hoveredEntryId.Equals(entry.Id))
			{
				ClearHoveredEntryInfo();
			}
		}

		private void RefreshTabLabels()
		{
			if (m_run == null)
			{
				return;
			}

			m_questsTabLabel.text = QuestTabText;
			m_actionsTabLabel.text = ActionTabText;
			m_questsTabToggle.SetIsOnWithoutNotify(m_showQuests);
			m_actionsTabToggle.SetIsOnWithoutNotify(!m_showQuests);
			m_questsTabToggle.interactable = true;
			m_actionsTabToggle.interactable = true;
		}

		private void ApplyViewVisibility()
		{
			SetCanvasGroupVisible(m_headerGroup, true);
			SetCanvasGroupVisible(m_questsViewGroup, m_showQuests);
			SetCanvasGroupVisible(m_actionsViewGroup, !m_showQuests);
			RefreshDisplayedTextCache();
		}

		private void SetMenuVisibility(bool isOpen, bool animated, bool playSound)
		{
			if (m_isMenuAnimating || m_menuPanel == null)
			{
				return;
			}

			m_isMenuVisible = isOpen;
			float targetX = GetMenuTargetX(isOpen);
			if (!animated)
			{
				SnapMenuVisibility(isOpen);
				if (playSound)
				{
					PlayMenuToggleSound();
				}
				return;
			}

			m_isMenuAnimating = true;
			m_menuTween?.Kill();
			m_menuTween = m_menuPanel
				.DOAnchorPosX(targetX, MenuSlideSeconds)
				.SetUpdate(true)
				.OnComplete(() =>
				{
					m_isMenuAnimating = false;
					m_menuToggleLabel.text = isOpen ? MenuOpenLabel : MenuClosedLabel;
					m_menuTween = null;
				});
			if (playSound)
			{
				PlayMenuToggleSound();
			}
		}

		private void SnapMenuVisibility(bool isOpen)
		{
			m_menuTween?.Kill();
			m_menuTween = null;
			m_isMenuAnimating = false;
			m_isMenuVisible = isOpen;
			if (m_menuPanel != null)
			{
				Vector2 position = m_menuPanel.anchoredPosition;
				position.x = GetMenuTargetX(isOpen);
				m_menuPanel.anchoredPosition = position;
			}
			if (m_menuToggleLabel != null)
			{
				m_menuToggleLabel.text = isOpen ? MenuOpenLabel : MenuClosedLabel;
			}
		}

		private float GetMenuTargetX(bool isOpen)
		{
			return isOpen ? 0f : m_menuPanel.sizeDelta.x;
		}

		private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
		{
			group.alpha = visible ? 1f : 0f;
			group.blocksRaycasts = visible;
		}

		private void RefreshDisplayedTextCache()
		{
			List<ScenarioJournalEntryButton> buttons = m_showQuests ? m_questButtons : m_actionButtons;
			TMP_Text fallback = ActiveContentLabel;
			if (buttons.Count == 0)
			{
				m_displayedTextCache = fallback == null ? string.Empty : fallback.text;
				return;
			}

			StringBuilder text = new();
			for (int i = 0; i < buttons.Count; i++)
			{
				ScenarioJournalEntryButton button = buttons[i];
				if (button == null || !button.gameObject.activeSelf)
				{
					continue;
				}
				if (text.Length > 0)
				{
					text.AppendLine();
				}
				text.Append(button.Text);
			}
			m_displayedTextCache = text.ToString();
		}

		private static List<JournalEntry> BuildQuestEntries(ScenarioRun run)
		{
			List<JournalEntry> entries = new();
			QuestLog questLog = run.QuestLog;
			for (int i = 0; i < questLog.Quests.Count; i++)
			{
				QuestProgress quest = questLog.Quests[i];
				if (quest.Status == QuestStatus.Locked)
				{
					continue;
				}

				ContentId questId = quest.Definition.ContentId;
				StringBuilder listText = new();
				listText.Append(SymbolBullet).Append(' ').Append(quest.Definition.DisplayName);
				if (quest.Status == QuestStatus.Completed)
				{
					listText.Append(' ').Append(SymbolCompleted);
				}
				AppendUnreadIndicator(listText, run, questId);

				entries.Add(new JournalEntry(
					questId,
					quest.Definition.JournalGroupName,
					listText.ToString(),
					quest.Definition.DisplayName,
					BuildQuestInfoBody(quest)));
			}
			return entries;
		}

		private static string BuildQuestInfoBody(QuestProgress quest)
		{
			StringBuilder body = new(quest.Definition.Description);
			if (quest.Status == QuestStatus.Completed)
			{
				return body.ToString();
			}
			for (int taskIndex = 0; taskIndex < quest.Tasks.Count; taskIndex++)
			{
				QuestTaskProgressSnapshot progress = quest.Tasks[taskIndex].Progress;
				if (body.Length > 0)
				{
					body.AppendLine().AppendLine();
				}
				body.Append("进度：")
					.Append(progress.CurrentAmount)
					.Append(" / ")
					.Append(progress.RequiredAmount);
			}
			return body.ToString();
		}

		private static List<JournalEntry> BuildActionEntries(
			ScenarioRun run,
			IReadOnlyList<ActionDefinition> actions)
		{
			List<JournalEntry> entries = new(actions.Count);
			for (int i = 0; i < actions.Count; i++)
			{
				ActionDefinition action = actions[i];
				ContentId actionId = action.ContentId;
				StringBuilder listText = new();
				listText.Append(SymbolBullet).Append(' ').Append(action.DisplayName);
				AppendUnreadIndicator(listText, run, actionId);
				entries.Add(new JournalEntry(
					actionId,
					action.JournalGroupName,
					listText.ToString(),
					"配方：" + action.DisplayName,
					BuildActionInfoBody(run.ContentIndex, action)));
			}
			return entries;
		}

		private static string BuildActionInfoBody(ContentIndex contentIndex, ActionDefinition action)
		{
			string ingredientSummary = BuildActionIngredientSummary(contentIndex, action);
			return string.IsNullOrEmpty(ingredientSummary)
				? action.Description
				: ingredientSummary;
		}

		private static string BuildActionIngredientSummary(ContentIndex contentIndex, ActionDefinition action)
		{
			StringBuilder text = new();
			IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
			for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
			{
				string line = BuildActionSlotIngredientText(contentIndex, action, slots[slotIndex]);
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}
				if (text.Length > 0)
				{
					text.Append(", ");
				}
				text.Append(line);
			}
			if (text.Length > 0)
			{
				text.Append('.');
			}
			return text.ToString();
		}

		private static string BuildActionSlotIngredientText(
			ContentIndex contentIndex,
			ActionDefinition action,
			ActionSlotDefinition slot)
		{
			if (slot == null)
			{
				throw new InvalidOperationException($"行动 {action.ContentId} 的日志条目包含空参与槽位。");
			}

			StringBuilder text = new();
			if (slot.AllowedContentIds.Count > 0)
			{
				for (int contentIndexInSlot = 0; contentIndexInSlot < slot.AllowedContentIds.Count; contentIndexInSlot++)
				{
					ContentId contentId = slot.AllowedContentIds[contentIndexInSlot];
					if (!contentIndex.TryGet(contentId, out ContentAsset contentAsset))
					{
						throw new InvalidOperationException(
							$"行动 {action.ContentId} 的日志条目引用了当前内容集合中不存在的材料 {contentId}。");
					}
					if (text.Length > 0)
					{
						text.Append(" / ");
					}
					text.Append(GetDisplayName(contentAsset));
				}
			}
			else
			{
				text.Append(slot.DisplayName);
			}

			if (text.Length == 0)
			{
				return string.Empty;
			}
			text.Append(BuildParticipantCountText(slot));
			return text.ToString();
		}

		private static string GetDisplayName(ContentAsset asset)
		{
			return asset is DisplayableContentAsset displayable
				? displayable.DisplayName
				: asset.ContentId.Value;
		}

		private static string BuildParticipantCountText(ActionSlotDefinition slot)
		{
			if (slot.MinimumParticipants == slot.MaximumParticipants && slot.MinimumParticipants > 0)
			{
				return " ×" + slot.MinimumParticipants;
			}
			if (slot.MinimumParticipants > 0 && slot.MaximumParticipants == 0)
			{
				return " ×" + slot.MinimumParticipants + "+";
			}
			if (slot.MinimumParticipants > 0 && slot.MaximumParticipants > slot.MinimumParticipants)
			{
				return " ×" + slot.MinimumParticipants + "-" + slot.MaximumParticipants;
			}
			if (slot.MinimumParticipants == 0 && slot.MaximumParticipants > 0)
			{
				return " ×0-" + slot.MaximumParticipants;
			}
			return string.Empty;
		}

		private static void AppendUnreadIndicator(StringBuilder text, ScenarioRun run, ContentId entryId)
		{
			if (!run.IsJournalEntrySeen(entryId))
			{
				text.Append(UnreadIndicator);
			}
		}

		private static string RemoveUnreadIndicator(string text)
		{
			return text.Replace(UnreadIndicator, string.Empty);
		}

		private static void EnsureContentListRoot(TMP_Text contentRoot)
		{
			contentRoot.raycastTarget = false;
			RectTransform rect = contentRoot.rectTransform;
			VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>() ??
				contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
			layout.padding.left = 20;
			layout.padding.right = 20;
			layout.padding.top = 10;
			layout.padding.bottom = 10;
			layout.childAlignment = TextAnchor.UpperLeft;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;
			layout.spacing = 10f;
			ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>() ??
				contentRoot.gameObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y);
		}

		private static void CopyTextStyle(TMP_Text source, TMP_Text target)
		{
			target.font = source.font;
			target.fontSharedMaterial = source.fontSharedMaterial;
			target.enableAutoSizing = source.enableAutoSizing;
			target.fontSizeMin = source.fontSizeMin;
			target.fontSizeMax = source.fontSizeMax;
			target.lineSpacing = source.lineSpacing;
			target.margin = source.margin;
		}

		private static string StripRichText(string text)
		{
			return text.Replace(UnreadIndicator, string.Empty);
		}

		private static TMP_Text GetRequiredToggleLabel(Toggle toggle, string fieldName)
		{
			TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
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

			if (m_isMenuVisible)
			{
				SetMenuVisibility(isOpen: false, animated: true, playSound: true);
			}
		}

		private static void PlayMenuToggleSound()
		{
			if (!GameManager.Exists() || GameManager.Config == null || !GameManager.Config.submitSound)
			{
				return;
			}

			EventKit.Type.Send(new AudioPlaybackRequestedEvent(GameManager.Config.submitSound));
		}

		private void ClearHoveredEntryInfo()
		{
			if (!m_hasHoveredEntryInfo || m_run == null)
			{
				m_hasHoveredEntryInfo = false;
				return;
			}

			EventKit.Type.Send(ScenarioJournalEntryInfoEvent.Clear(
				m_run.ScenarioId,
				m_hoveredEntryId));
			m_hasHoveredEntryInfo = false;
			m_hoveredEntryId = default;
		}

		private void ClearButtons(List<ScenarioJournalEntryButton> buttons)
		{
			for (int i = 0; i < buttons.Count; i++)
			{
				if (buttons[i] != null)
				{
					Destroy(buttons[i].gameObject);
				}
			}
			buttons.Clear();
		}

		private void Unbind()
		{
			m_menuTween?.Kill();
			m_menuTween = null;
			m_isMenuAnimating = false;
			ClearHoveredEntryInfo();
			ClearButtons(m_questButtons);
			ClearButtons(m_actionButtons);
			m_questGroupExpandedByName.Clear();
			m_actionGroupExpandedByName.Clear();
			m_displayedTextCache = string.Empty;
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

		private readonly struct JournalEntry
		{
			public ContentId Id { get; }

			public string GroupName { get; }

			public string ListText { get; }

			public string InfoHeader { get; }

			public string InfoBody { get; }

			public JournalEntry(
				ContentId id,
				string groupName,
				string listText,
				string infoHeader,
				string infoBody)
			{
				Id = id;
				GroupName = groupName ?? string.Empty;
				ListText = listText ?? string.Empty;
				InfoHeader = infoHeader ?? string.Empty;
				InfoBody = infoBody ?? string.Empty;
			}
		}

		private sealed class JournalGroup
		{
			public string Name { get; }

			public bool IsExpanded { get; set; }

			public List<JournalEntry> Entries { get; } = new();

			public JournalGroup(string name)
			{
				Name = name ?? throw new ArgumentNullException(nameof(name));
				IsExpanded = true;
			}
		}
	}
}
