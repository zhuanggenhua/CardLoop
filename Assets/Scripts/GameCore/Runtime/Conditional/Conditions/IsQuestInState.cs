using System;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 任务链中可被条件系统查询的任务状态。
    /// </summary>
    public enum EQuestState
    {
        Unlocked,
        Available,
        Active,
        Fullfilled,
        Completed
    }

    /// <summary>
    /// 判断指定任务是否处于目标状态，并按状态订阅对应的任务事件刷新条件。
    /// </summary>
    [Serializable]
    public class IsQuestInState : ABaseCondition
    {
        [InspectorName("目标任务")]
        [Tooltip("要查询状态的任务资产。")]
        [SerializeField] private Quest m_quest = null;

        [InspectorName("目标状态")]
        [Tooltip("条件期望任务达到的状态。")]
        [SerializeField] private EQuestState m_state = EQuestState.Unlocked;

        public override bool Evaluate()
        {
            switch (m_state)
            {
                case EQuestState.Unlocked: return GameManager.JournalSystem.IsQuestUnlocked(m_quest);
                case EQuestState.Available: return GameManager.JournalSystem.IsQuestAvailable(m_quest);
                case EQuestState.Active: return GameManager.JournalSystem.IsQuestActive(m_quest);
                case EQuestState.Fullfilled: return GameManager.JournalSystem.IsQuestFullfilled(m_quest);
                case EQuestState.Completed: return GameManager.JournalSystem.IsQuestCompleted(m_quest);
            }

            return false;
        }

        protected override void OnStartListening()
        {
            switch (m_state)
            {
                case EQuestState.Unlocked:
                    EventKit.Type.Register<QuestUnlockedEvent>(OnQuestUnlocked);
                    EventKit.Type.Register<QuestStartedEvent>(OnQuestStarted);
                    break;

                case EQuestState.Available:
                    EventKit.Type.Register<QuestAvailabilityChangedEvent>(OnQuestAvailabilityChanged);
                    EventKit.Type.Register<QuestStartedEvent>(OnQuestStarted);
                    break;

                case EQuestState.Active:
                    EventKit.Type.Register<QuestStartedEvent>(OnQuestStarted);
                    EventKit.Type.Register<QuestFullfilledEvent>(OnQuestFullfilled);
                    break;

                case EQuestState.Fullfilled:
                    EventKit.Type.Register<QuestFullfilledEvent>(OnQuestFullfilled);
                    EventKit.Type.Register<QuestCompletedEvent>(OnQuestCompleted);
                    break;

                case EQuestState.Completed:
                    EventKit.Type.Register<QuestCompletedEvent>(OnQuestCompleted);
                    break;
            }
        }

        protected override void OnStopListening()
        {
            switch (m_state)
            {
                case EQuestState.Unlocked:
                    EventKit.Type.UnRegister<QuestUnlockedEvent>(OnQuestUnlocked);
                    EventKit.Type.UnRegister<QuestStartedEvent>(OnQuestStarted);
                    break;

                case EQuestState.Available:
                    EventKit.Type.UnRegister<QuestAvailabilityChangedEvent>(OnQuestAvailabilityChanged);
                    EventKit.Type.UnRegister<QuestStartedEvent>(OnQuestStarted);
                    break;

                case EQuestState.Active:
                    EventKit.Type.UnRegister<QuestStartedEvent>(OnQuestStarted);
                    EventKit.Type.UnRegister<QuestFullfilledEvent>(OnQuestFullfilled);
                    break;

                case EQuestState.Fullfilled:
                    EventKit.Type.UnRegister<QuestFullfilledEvent>(OnQuestFullfilled);
                    EventKit.Type.UnRegister<QuestCompletedEvent>(OnQuestCompleted);
                    break;

                case EQuestState.Completed:
                    EventKit.Type.UnRegister<QuestCompletedEvent>(OnQuestCompleted);
                    break;
            }
        }

        private void OnQuestUnlocked(QuestUnlockedEvent questUnlockedEvent) => NotifyStateChange();
        private void OnQuestAvailabilityChanged(QuestAvailabilityChangedEvent questAvailabilityChangedEvent) => NotifyStateChange();
        private void OnQuestStarted(QuestStartedEvent questStartedEvent) => NotifyStateChange();
        private void OnQuestFullfilled(QuestFullfilledEvent questFullfilledEvent) => NotifyStateChange();
        private void OnQuestCompleted(QuestCompletedEvent questCompletedEvent) => NotifyStateChange();
    }
}
