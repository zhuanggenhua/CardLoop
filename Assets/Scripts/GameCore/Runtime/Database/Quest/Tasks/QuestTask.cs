using UnityEngine;

namespace FantasyWord.GameCore
{
    public abstract class QuestTask : DatabaseEntry
    {
        [SerializeField] protected string m_title = string.Empty;
        [SerializeField] protected bool m_requirePreviousTasksCompletion = false;

        public bool requirePreviousTaskCompletion => m_requirePreviousTasksCompletion;

        public abstract IQuestTaskProgress CreateTaskProgress();
        public abstract string GetCompletedTitle();
        public abstract string GetInProgressTitle(IQuestTaskProgress progress);
    }
}
