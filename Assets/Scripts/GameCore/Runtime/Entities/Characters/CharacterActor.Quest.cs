using YokiFrame;

namespace FantasyWord.GameCore
{
    public partial class CharacterActor
    {
        private bool m_questStatusListening = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            StartQuestStatusListening();
            UpdateFloatingIcon();
        }

        protected override void OnDisable()
        {
            StopQuestStatusListening();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            StopQuestStatusListening();
            base.OnDestroy();
        }

        private void StartQuestStatusListening()
        {
            if (m_questStatusListening)
            {
                return;
            }

            m_questStatusListening = true;
            EventKit.Type.Register<QuestUnlockedEvent>(OnQuestUnlocked);
            EventKit.Type.Register<QuestAvailabilityChangedEvent>(OnQuestAvailabilityChanged);
            EventKit.Type.Register<QuestCompletedEvent>(OnQuestCompleted);
            EventKit.Type.Register<QuestFullfilledEvent>(OnQuestFullfilled);
            EventKit.Type.Register<QuestProgressionUpdatedEvent>(OnQuestProgressionUpdated);
            EventKit.Type.Register<QuestStartedEvent>(OnQuestStarted);
        }

        private void StopQuestStatusListening()
        {
            if (!m_questStatusListening)
            {
                return;
            }

            m_questStatusListening = false;
            EventKit.Type.UnRegister<QuestUnlockedEvent>(OnQuestUnlocked);
            EventKit.Type.UnRegister<QuestAvailabilityChangedEvent>(OnQuestAvailabilityChanged);
            EventKit.Type.UnRegister<QuestCompletedEvent>(OnQuestCompleted);
            EventKit.Type.UnRegister<QuestFullfilledEvent>(OnQuestFullfilled);
            EventKit.Type.UnRegister<QuestProgressionUpdatedEvent>(OnQuestProgressionUpdated);
            EventKit.Type.UnRegister<QuestStartedEvent>(OnQuestStarted);
        }

        private void OnQuestUnlocked(QuestUnlockedEvent questUnlockedEvent) => UpdateFloatingIcon();
        private void OnQuestAvailabilityChanged(QuestAvailabilityChangedEvent questAvailabilityChangedEvent) => UpdateFloatingIcon();
        private void OnQuestCompleted(QuestCompletedEvent questCompletedEvent) => UpdateFloatingIcon();
        private void OnQuestFullfilled(QuestFullfilledEvent questFullfilledEvent) => UpdateFloatingIcon();
        private void OnQuestProgressionUpdated(QuestProgressionUpdatedEvent questProgressionUpdatedEvent) => UpdateFloatingIcon();
        private void OnQuestStarted(QuestStartedEvent questStartedEvent) => UpdateFloatingIcon();

        private void UpdateFloatingIcon()
        {
            if (!GameManager.Exists() || !GameManager.HasSystem<JournalSystem>())
            {
                return;
            }

            if (GameManager.JournalSystem.GetQuestToComplete(this) != null)
            {
                SetFloatingIcon(EFloatingIcon.QuestCompleted);
            }
            else if (GameManager.JournalSystem.GetTaskToComplete(this) != null)
            {
                SetFloatingIcon(EFloatingIcon.QuestTalkTo);
            }
            else if (GameManager.JournalSystem.GetQuestToStart(this) != null)
            {
                SetFloatingIcon(EFloatingIcon.QuestAvailable);
            }
            else if (GameManager.JournalSystem.GetNonFullfilledQuestToReportTo(this) != null)
            {
                SetFloatingIcon(EFloatingIcon.QuestInProgress);
            }
            else
            {
                SetFloatingIcon(EFloatingIcon.None);
            }
        }
    }
}
