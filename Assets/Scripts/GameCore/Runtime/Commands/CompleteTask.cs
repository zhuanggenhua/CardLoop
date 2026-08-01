using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 强制完成所有进行中任务里匹配的指定子任务。
    /// </summary>
    [Serializable]
    public class CompleteTask : IContextualCommand
    {
        [InspectorName("目标子任务")]
        [Tooltip("要在当前进行中任务里强制标记完成的子任务资产。")]
        [SerializeField] private QuestTask m_task = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            foreach (QuestProgress progress in GameManager.JournalSystem.GetActiveQuests())
            {
                progress.CompleteTask(m_task);
            }

            return Task.CompletedTask;
        }
    }
}

