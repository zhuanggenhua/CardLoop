using System;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 单个任务子目标的状态掩码，允许同时匹配进行中和已完成。
    /// </summary>
    [Flags]
    public enum EQuestTaskStateFlags
    {
        [HideInInspector] None = 0,
        InProgress = 1 << 0,
        Completed = 1 << 1,
        [HideInInspector] All = ~None
    }

    /// <summary>
    /// 判断指定任务子目标是否处于目标状态集合中。
    /// </summary>
    [Serializable]
    public class IsQuestTaskInState : ABaseCondition
    {
        [InspectorName("目标子任务")]
        [Tooltip("要查询状态的任务子目标资产。")]
        [SerializeField] private QuestTask m_task = null;

        [InspectorName("目标状态")]
        [Tooltip("条件可接受的子任务状态集合。")]
        [SerializeField] private EQuestTaskStateFlags m_stateFlags = EQuestTaskStateFlags.None;

        public override bool Evaluate()
        {
            return
                (m_stateFlags.HasFlag(EQuestTaskStateFlags.InProgress) && GameManager.JournalSystem.IsTaskActive(m_task)) ||
                (m_stateFlags.HasFlag(EQuestTaskStateFlags.Completed) && GameManager.JournalSystem.IsTaskCompleted(m_task));
        }

        protected override void OnStartListening()
        {
            EventKit.Type.Register<QuestStartedEvent>(OnQuestStarted);
            EventKit.Type.Register<QuestProgressionUpdatedEvent>(OnQuestProgressionUpdated);
        }

        protected override void OnStopListening()
        {
            EventKit.Type.UnRegister<QuestStartedEvent>(OnQuestStarted);
            EventKit.Type.UnRegister<QuestProgressionUpdatedEvent>(OnQuestProgressionUpdated);
        }

        private void OnQuestStarted(QuestStartedEvent questStartedEvent) => NotifyStateChange();
        private void OnQuestProgressionUpdated(QuestProgressionUpdatedEvent questProgressionUpdatedEvent) => NotifyStateChange();
    }
}
