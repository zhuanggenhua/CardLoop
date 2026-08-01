using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 解锁指定任务，使其进入任务日志系统的可用性检查流程。
    /// </summary>
    [Serializable]
    public class UnlockQuest : IContextualCommand
    {
        [InspectorName("目标任务")]
        [Tooltip("要解锁的任务资产。")]
        [SerializeField] private Quest m_quest = null;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            Debug.Assert(m_quest != null, "Missing Quest reference!");
            GameManager.JournalSystem.UnlockQuest(m_quest);
            return Task.CompletedTask;
        }
    }
}

