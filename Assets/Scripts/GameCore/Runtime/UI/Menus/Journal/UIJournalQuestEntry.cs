using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 任务日志条目选中时传给详情面板的数据。
    /// </summary>
    public struct JournalQuestEntry
    {
        /// <summary>
        /// 被选中的任务资产。
        /// </summary>
        public Quest quest;

        /// <summary>
        /// 任务进行中时对应的进度实例；未进行中时为空。
        /// </summary>
        public QuestProgress instance;
    }

    /// <summary>
    /// 任务日志中的单个任务条目，负责显示标题并在选中时通知父级日志面板。
    /// </summary>
    public class UIJournalQuestEntry : MonoBehaviour, ISelectHandler
    {
        [Header("引用")]
        [InspectorName("任务标题文本")]
        [Tooltip("显示任务推荐等级和标题的文本控件。")]
        [SerializeField] private TextMeshProUGUI m_text = null;

        private Quest m_targetQuest = null;
        private QuestProgress m_targetQuestProgress = null;
        private UIJournal m_journalMenu = null;

        /// <summary>
        /// 绑定任务资产和可选进度实例，并刷新条目显示。
        /// </summary>
        public void SetTargetQuest(Quest quest, QuestProgress progress = null)
        {
            m_journalMenu = GetComponentInParent<UIJournal>();
            Debug.Assert(m_journalMenu != null, $"{nameof(UIJournalQuestEntry)} 需要父级 {nameof(UIJournal)} 作为任务日志菜单。");
            m_targetQuest = quest;
            m_targetQuestProgress = progress;

            if (quest)
            {
                m_text.text = StringFormatter.Format("[Lvl. {0}] {1}", quest.recommendedLevel, quest.title);
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            SetTargetQuest(null);
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_journalMenu.HandleQuestDescriptionUpdate(new JournalQuestEntry
            {
                quest = m_targetQuest,
                instance = m_targetQuestProgress
            });
        }
    }
}

