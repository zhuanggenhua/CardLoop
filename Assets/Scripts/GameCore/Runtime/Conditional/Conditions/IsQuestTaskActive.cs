using System;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class IsQuestTaskActive : ABaseCondition
    {
        [SerializeField] private QuestTask m_task = null;

        public override bool Evaluate() => GameManager.JournalSystem.IsTaskActive(m_task);

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
