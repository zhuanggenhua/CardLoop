using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public class UIJournalQuestDescription : MonoBehaviour
    {
        // Component References
        [SerializeField] private TextMeshProUGUI m_description = null;
        [SerializeField] private TextMeshProUGUI m_currentTasks = null;
        [SerializeField] private TextMeshProUGUI m_completedTasks = null;

        // Private Members
        private Quest m_targetQuest = null;
        private QuestProgress m_targetQuestProgress = null;

        private void Awake()
        {
            Clear();
        }

        private void OnEnable()
        {
            Clear();
        }

        public void SetTargetQuest(Quest quest, QuestProgress progress = null)
        {
            m_targetQuest = quest;
            m_targetQuestProgress = progress;

            UpdateDescription();
        }

        public void Clear()
        {
            SetTargetQuest(null);
        }

        private string FormatQuestTasks(IEnumerable<IQuestTaskProgress> tasks, bool completed)
        {
            string result = string.Empty;

            foreach (IQuestTaskProgress task in tasks)
            {
                if (result.Length > 0)
                {
                    result += "\n";
                }

                result += completed ? task.task.GetCompletedTitle() : task.task.GetInProgressTitle(task);
            }

            return result;
        }

        private string FormatQuestTasks(IEnumerable<QuestTask> tasks)
        {
            string result = string.Empty;

            foreach (QuestTask task in tasks)
            {
                if (result.Length > 0)
                {
                    result += "\n";
                }

                result += task.GetCompletedTitle();
            }

            return result;
        }

        private void UpdateDescription()
        {
            if (m_targetQuest)
            {
                m_description.text = m_targetQuest.description;

                if (m_targetQuestProgress != null)
                {
                    // Active quests
                    m_currentTasks.text = FormatQuestTasks(m_targetQuestProgress.GetCurrentTasks(), false);
                    m_completedTasks.text = FormatQuestTasks(m_targetQuestProgress.GetCompletedTasks(), true);
                }
                else
                {
                    // Fullfilled quests
                    m_currentTasks.text = StringFormatter.Format("Talk to {0}", m_targetQuest.reportTo.displayName);
                    m_completedTasks.text = FormatQuestTasks(m_targetQuest.GetTasks());
                }
            }
            else
            {
                m_description.text = string.Empty;
                m_currentTasks.text = string.Empty;
                m_completedTasks.text = string.Empty;
            }
        }

        public void UpdateUI()
        {
            UpdateDescription();
        }
    }
}

