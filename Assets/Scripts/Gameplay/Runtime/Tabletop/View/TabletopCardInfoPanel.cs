using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Gameplay.Actions;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using YokiFrame;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;

namespace Gameplay.Tabletop
{
    /// <summary>打开牌桌卡牌详情面板所需的当前牌桌表现对象。</summary>
    public sealed class TabletopCardInfoPanelData : IUIData
    {
        public TabletopView TabletopView { get; }

		public ScenarioRun ScenarioRun { get; }

        public TabletopCardInfoPanelData(TabletopView tabletopView, ScenarioRun scenarioRun)
        {
            TabletopView = tabletopView ?? throw new ArgumentNullException(nameof(tabletopView));
			ScenarioRun = scenarioRun ?? throw new ArgumentNullException(nameof(scenarioRun));
			if (!ReferenceEquals(tabletopView.BoundTabletop, scenarioRun.Tabletop))
			{
				throw new InvalidOperationException("卡牌详情面板的牌桌视图和剧本单局不属于同一当前牌桌。");
			}
        }
    }

    /// <summary>
    /// 常驻牌桌卡牌详情投影。它只读取当前可读卡牌，不保存卡牌或规则的第二份状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabletopCardInfoPanel : UIPanel
    {
		private const string SectionBullet = "• ";

        [Header("面板组件")]
		[SerializeField]
		[LabelText("内容根节点")]
		[Tooltip("没有可读卡牌时隐藏的详情内容根节点。")]
		private GameObject m_contentRoot;

		[SerializeField]
		[FormerlySerializedAs("m_titleLabel")]
		[LabelText("信息文本")]
		[Tooltip("按 StackCraft InfoPanel 格式显示标题和正文。")]
		private TMP_Text m_infoLabel;

		[SerializeField]
		[LabelText("标题字号")]
		[Tooltip("对齐 StackCraft InfoPanel 的 headerSize。")]
		private int m_headerSize = 34;

		[SerializeField]
		[LabelText("正文字号")]
		[Tooltip("对齐 StackCraft InfoPanel 的 bodySize。")]
		private int m_bodySize = 30;

        private TabletopView m_tabletopView;

		private ScenarioRun m_scenarioRun;

		private bool m_isSubscribed;

		private bool m_sequenceMessageActive;

		private string m_sequenceHeader = string.Empty;

		private string m_sequenceBody = string.Empty;

		private float m_sequenceMessageExpiresAt;

		private bool m_journalEntryInfoActive;

		private ContentId m_journalEntryInfoId;

		private string m_journalEntryHeader = string.Empty;

		private string m_journalEntryBody = string.Empty;

		private string m_displayedTitle = string.Empty;

		private string m_displayedDescription = string.Empty;

        /// <summary>当前显示的局内卡牌 ID；空值表示面板没有可读对象。</summary>
        public TabletopCardId DisplayedCardId { get; private set; }

		/// <summary>当前是否由剧本流程提示覆盖卡牌悬浮信息。</summary>
		public bool IsSequenceMessageActive => m_sequenceMessageActive;

		/// <summary>当前显示的流程提示标题；空值表示没有流程提示。</summary>
		public string DisplayedSequenceHeader => m_sequenceMessageActive ? m_sequenceHeader : string.Empty;

		/// <summary>当前显示的流程提示正文；空值表示没有流程提示。</summary>
		public string DisplayedSequenceBody => m_sequenceMessageActive ? m_sequenceBody : string.Empty;

		/// <summary>当前是否由剧本日志条目悬浮信息覆盖卡牌详情。</summary>
		public bool IsJournalEntryInfoActive => m_journalEntryInfoActive;

		/// <summary>当前显示的剧本日志条目标题；空值表示没有日志条目信息。</summary>
		public string DisplayedJournalEntryHeader =>
			m_journalEntryInfoActive ? m_journalEntryHeader : string.Empty;

		/// <summary>当前显示的剧本日志条目正文；空值表示没有日志条目信息。</summary>
		public string DisplayedJournalEntryBody =>
			m_journalEntryInfoActive ? m_journalEntryBody : string.Empty;

        /// <summary>当前实际显示的标题文本。</summary>
        public string DisplayedTitle => m_displayedTitle;

        /// <summary>当前实际显示的描述文本。</summary>
        public string DisplayedDescription => m_displayedDescription;

		/// <summary>当前按 StackCraft InfoPanel rich text 格式生成的完整显示文本。</summary>
		public string DisplayedInfoText => m_infoLabel == null ? string.Empty : m_infoLabel.text;

        protected override void OnInit(IUIData data = null)
        {
            if (m_contentRoot == null || m_infoLabel == null)
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
			m_scenarioRun = panelData.ScenarioRun;
            m_tabletopView.ReadableCardChanged += Refresh;
			EventKit.Type.Register<ScenarioSequenceMessageEvent>(OnScenarioSequenceMessage);
			EventKit.Type.Register<ScenarioJournalEntryInfoEvent>(OnScenarioJournalEntryInfo);
			m_isSubscribed = true;
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
			if (m_sequenceMessageActive)
			{
				DisplaySequenceMessage();
				return;
			}
			if (m_journalEntryInfoActive)
			{
				DisplayJournalEntryInfo();
				return;
			}

            if (m_tabletopView == null ||
                !m_tabletopView.TryGetReadableCard(out TabletopCard card, out var definition))
            {
                DisplayedCardId = default;
				ClearDisplayedInfo();
                m_contentRoot.SetActive(false);
                return;
            }

			DisplayedCardId = card.Id;
			ApplyDisplayedInfo(definition.DisplayName, BuildDescription(card, definition));
			m_contentRoot.SetActive(true);
		}

		private void LateUpdate()
		{
			if (m_sequenceMessageActive)
			{
				if (Time.realtimeSinceStartup >= m_sequenceMessageExpiresAt)
				{
					ClearSequenceMessage();
					Refresh();
				}
				return;
			}

			if (m_tabletopView != null && DisplayedCardId.IsValid)
			{
				Refresh();
			}
		}

		private string BuildDescription(TabletopCard card, CardDefinition definition)
		{
			StringBuilder text = new StringBuilder(BuildBaseDescription(card, definition));
			AppendActiveActionSection(text, card);
			AppendStackSummarySection(text, card);
			return text.ToString();
		}

		private string BuildBaseDescription(TabletopCard card, CardDefinition definition)
		{
			if (card is CharacterCard characterCard)
			{
				return BuildCharacterDescription(characterCard, definition);
			}
			if (card is ChestCard chestCard && definition is ChestCardDefinition)
			{
				StringBuilder chestText = new StringBuilder(definition.Description);
				AppendSectionBreak(chestText);
				chestText.Append("存币：")
					.Append(chestCard.StoredCurrencyCount)
					.Append('/')
					.Append(chestCard.Capacity);
				return chestText.ToString();
			}

			if (card is not PackVendorCard vendorCard || definition is not PackVendorDefinition vendor)
			{
				return definition.Description;
			}

			StringBuilder text = new StringBuilder(definition.Description);
			if (!vendor.IsUnlocked(m_scenarioRun.QuestLog.CompletedQuestCount))
			{
				AppendSectionBreak(text);
				text.Append("完成任务后解锁：")
					.Append(m_scenarioRun.QuestLog.CompletedQuestCount)
					.Append('/')
					.Append(vendor.MinimumCompletedQuests);
				return text.ToString();
			}

			CardPackDefinition pack = m_scenarioRun.ContentIndex.TryGet(
				vendor.OfferedPackId,
				out CardPackDefinition offeredPack)
				? offeredPack
				: throw new InvalidOperationException($"卡包商贩 {vendor.ContentId} 引用的卡包 {vendor.OfferedPackId} 不在当前单局内容集合中。");
			CardPackCollectionProgress progress = pack.GetCollectionProgress(m_scenarioRun.IsContentDiscovered);
			AppendSectionBreak(text);
			text.Append("出售：").Append(pack.DisplayName);
			text.AppendLine();
			text.Append("剩余价格：").Append(vendorCard.RemainingPrice);
			text.AppendLine();
			text.Append("收藏进度：").Append(progress.DiscoveredCount).Append('/').Append(progress.TotalCount);
			return text.ToString();
		}

		private string BuildCharacterDescription(CharacterCard characterCard, CardDefinition definition)
		{
			if (characterCard.EquippedCardCount == 0)
			{
				return definition.Description;
			}

			List<string> equipmentLines = new List<string>(characterCard.EquippedCardCount);
			foreach (EquippedCardState equipped in characterCard.EquippedCards)
			{
				EquipmentSlotDefinition slot = m_scenarioRun.ContentIndex.TryGet(
					equipped.SlotId,
					out EquipmentSlotDefinition resolvedSlot)
					? resolvedSlot
					: throw new InvalidOperationException(
						$"角色卡 {characterCard.Id} 已装备的槽位 {equipped.SlotId} 不在当前单局内容集合中。");
				EquipmentCardDefinition equipment = m_scenarioRun.ContentIndex.TryGet(
					equipped.CardSnapshot.ContentId,
					out EquipmentCardDefinition resolvedEquipment)
					? resolvedEquipment
					: throw new InvalidOperationException(
						$"角色卡 {characterCard.Id} 已装备的卡牌 {equipped.CardSnapshot.ContentId} 不在当前单局内容集合中。");
				equipmentLines.Add("• " + slot.DisplayName + "：" + equipment.DisplayName);
			}
			equipmentLines.Sort(StringComparer.Ordinal);

			StringBuilder text = new StringBuilder(definition.Description);
			if (text.Length > 0)
			{
				text.AppendLine();
			}
			text.Append("已装备：");
			for (int i = 0; i < equipmentLines.Count; i++)
			{
				text.AppendLine();
				text.Append(equipmentLines[i]);
			}
			return text.ToString();
		}

		private void AppendActiveActionSection(StringBuilder text, TabletopCard card)
		{
			TabletopCardStack stack = card.Stack;
			if (stack == null)
			{
				return;
			}

			List<string> actionLines = new List<string>();
			IReadOnlyList<ActionInstance> activeActions = m_scenarioRun.Tabletop.ActiveActions;
			for (int actionIndex = 0; actionIndex < activeActions.Count; actionIndex++)
			{
				ActionInstance action = activeActions[actionIndex];
				if (!IsActionBoundToStack(action, stack))
				{
					continue;
				}
				ActionDefinition actionDefinition = m_scenarioRun.ContentIndex.TryGet(
					action.ActionId,
					out ActionDefinition resolvedAction)
					? resolvedAction
					: throw new InvalidOperationException($"活动行动 {action.ActionId} 不在当前单局内容集合中。");
				actionLines.Add(BuildActionLine(action, actionDefinition));
			}

			if (actionLines.Count == 0)
			{
				return;
			}
			actionLines.Sort(StringComparer.Ordinal);
			AppendSectionBreak(text);
			text.Append("进行中行动：");
			for (int lineIndex = 0; lineIndex < actionLines.Count; lineIndex++)
			{
				text.AppendLine();
				text.Append(SectionBullet).Append(actionLines[lineIndex]);
			}
		}

		private void AppendStackSummarySection(StringBuilder text, TabletopCard card)
		{
			TabletopCardStack stack = card.Stack;
			if (stack == null || stack.Cards.Count <= 1)
			{
				return;
			}

			Dictionary<ContentId, int> counts = new Dictionary<ContentId, int>();
			Dictionary<ContentId, string> displayNames = new Dictionary<ContentId, string>();
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				TabletopCard stackCard = stack.Cards[cardIndex];
				CardDefinition cardDefinition = m_scenarioRun.ContentIndex.TryGet(
					stackCard.ContentId,
					out CardDefinition resolvedDefinition)
					? resolvedDefinition
					: throw new InvalidOperationException($"牌堆卡牌 {stackCard.Id} 引用了当前单局内容集合中不存在的卡牌 {stackCard.ContentId}。");
				counts.TryGetValue(stackCard.ContentId, out int currentCount);
				counts[stackCard.ContentId] = currentCount + 1;
				displayNames[stackCard.ContentId] = cardDefinition.DisplayName;
			}

			List<string> summaryLines = new List<string>(counts.Count);
			foreach (KeyValuePair<ContentId, int> pair in counts)
			{
				summaryLines.Add(displayNames[pair.Key] + " ×" + pair.Value.ToString(CultureInfo.InvariantCulture));
			}
			summaryLines.Sort(StringComparer.Ordinal);

			AppendSectionBreak(text);
			text.Append("牌堆：");
			for (int lineIndex = 0; lineIndex < summaryLines.Count; lineIndex++)
			{
				text.AppendLine();
				text.Append(SectionBullet).Append(summaryLines[lineIndex]);
			}
		}

		private bool IsActionBoundToStack(ActionInstance action, TabletopCardStack stack)
		{
			for (int cardIndex = 0; cardIndex < stack.Cards.Count; cardIndex++)
			{
				if (action.ContainsParticipant(stack.Cards[cardIndex].Id))
				{
					return true;
				}
			}
			return false;
		}

		private string BuildActionLine(ActionInstance action, ActionDefinition actionDefinition)
		{
			StringBuilder line = new StringBuilder(actionDefinition.DisplayName);
			if (action.State == ActionInstanceState.Paused)
			{
				line.Append("（暂停）");
			}
			line.Append("，剩余 ")
				.Append(FormatTurns(action.RemainingTurns))
				.Append(" 回合");
			float remainingSeconds = action.RemainingTurns * m_scenarioRun.SecondsPerTurn;
			if (remainingSeconds > 0f)
			{
				line.Append(" / 约 ")
					.Append(FormatSeconds(remainingSeconds))
					.Append(" 秒");
			}
			return line.ToString();
		}

		private static void AppendSectionBreak(StringBuilder text)
		{
			if (text.Length > 0)
			{
				text.AppendLine();
			}
		}

		private static string FormatTurns(float value)
		{
			return FormatNumber(value);
		}

		private static string FormatSeconds(float value)
		{
			return FormatNumber(value);
		}

		private static string FormatNumber(float value)
		{
			if (!float.IsFinite(value))
			{
				throw new ArgumentOutOfRangeException(nameof(value), value, "卡牌详情面板只能显示有限数值。");
			}
			if (Mathf.Approximately(value, Mathf.Round(value)))
			{
				return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
			}
			return value.ToString("0.#", CultureInfo.InvariantCulture);
		}

		private void OnScenarioSequenceMessage(ScenarioSequenceMessageEvent messageEvent)
		{
			if (m_scenarioRun == null ||
				!messageEvent.ScenarioId.Equals(m_scenarioRun.ScenarioId))
			{
				return;
			}

			m_sequenceMessageActive = true;
			m_sequenceHeader = messageEvent.Header;
			m_sequenceBody = messageEvent.Body;
			m_sequenceMessageExpiresAt = Time.realtimeSinceStartup + messageEvent.DurationSeconds;
			Refresh();
		}

		private void OnScenarioJournalEntryInfo(ScenarioJournalEntryInfoEvent infoEvent)
		{
			if (m_scenarioRun == null ||
				!infoEvent.ScenarioId.Equals(m_scenarioRun.ScenarioId))
			{
				return;
			}

			if (infoEvent.IsVisible)
			{
				m_journalEntryInfoActive = true;
				m_journalEntryInfoId = infoEvent.EntryId;
				m_journalEntryHeader = infoEvent.Header;
				m_journalEntryBody = infoEvent.Body;
				Refresh();
				return;
			}

			if (m_journalEntryInfoActive && m_journalEntryInfoId.Equals(infoEvent.EntryId))
			{
				ClearJournalEntryInfo();
				Refresh();
			}
		}

		private void DisplaySequenceMessage()
		{
			DisplayedCardId = default;
			ApplyDisplayedInfo(m_sequenceHeader, m_sequenceBody);
			m_contentRoot.SetActive(true);
		}

		private void DisplayJournalEntryInfo()
		{
			DisplayedCardId = default;
			ApplyDisplayedInfo(m_journalEntryHeader, m_journalEntryBody);
			m_contentRoot.SetActive(true);
		}

		private void ApplyDisplayedInfo(string header, string body)
		{
			m_displayedTitle = header ?? string.Empty;
			m_displayedDescription = body ?? string.Empty;
			m_infoLabel.text = FormatStackCraftInfoText(m_displayedTitle, m_displayedDescription);
		}

		private void ClearDisplayedInfo()
		{
			m_displayedTitle = string.Empty;
			m_displayedDescription = string.Empty;
			if (m_infoLabel != null)
			{
				m_infoLabel.text = string.Empty;
			}
		}

		private string FormatStackCraftInfoText(string header, string body)
		{
			StringBuilder text = new();
			if (!string.IsNullOrEmpty(header))
			{
				text.Append("<size=")
					.Append(m_headerSize)
					.Append('>')
					.Append("[")
					.Append(header)
					.Append("]\n");
			}
			if (!string.IsNullOrEmpty(body))
			{
				text.Append("<size=")
					.Append(m_bodySize)
					.Append('>')
					.Append(body);
			}
			return text.ToString();
		}

		private void ClearSequenceMessage()
		{
			m_sequenceMessageActive = false;
			m_sequenceHeader = string.Empty;
			m_sequenceBody = string.Empty;
			m_sequenceMessageExpiresAt = 0f;
		}

		private void ClearJournalEntryInfo()
		{
			m_journalEntryInfoActive = false;
			m_journalEntryInfoId = default;
			m_journalEntryHeader = string.Empty;
			m_journalEntryBody = string.Empty;
		}

        private void Unbind()
        {
			if (m_isSubscribed)
			{
				EventKit.Type.UnRegister<ScenarioSequenceMessageEvent>(OnScenarioSequenceMessage);
				EventKit.Type.UnRegister<ScenarioJournalEntryInfoEvent>(OnScenarioJournalEntryInfo);
				m_isSubscribed = false;
			}
            if (m_tabletopView != null)
            {
                m_tabletopView.ReadableCardChanged -= Refresh;
                m_tabletopView = null;
            }
			m_scenarioRun = null;
			ClearSequenceMessage();
			ClearJournalEntryInfo();

            DisplayedCardId = default;
			if (m_contentRoot != null)
			{
				m_contentRoot.SetActive(false);
			}
			ClearDisplayedInfo();
        }
    }
}
