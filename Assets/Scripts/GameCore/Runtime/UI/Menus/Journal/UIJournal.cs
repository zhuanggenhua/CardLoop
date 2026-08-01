using UnityEngine;

using YokiFrame;

namespace FantasyWord.GameCore
{
    public class UIJournal : UIKitMenuPanelBase
    {
        [SerializeField] private UIJournalQuestDescription m_questDescription = null;
        [SerializeField] private Transform m_questListRoot = null;
        [SerializeField] private GameObject m_questEntryPrefab = null;
        [SerializeField] private int m_questEntryPoolSize = 100;

        private UIJournalQuestEntry[] m_quests = System.Array.Empty<UIJournalQuestEntry>();

        protected override void OnPanelInit()
        {
            ConfigureQuestEntryPool();
            m_quests = new UIJournalQuestEntry[m_questEntryPoolSize];

            for (int i = 0; i < m_questEntryPoolSize; ++i)
            {
                GameObject instance = GameObjectPoolService.Rent(m_questEntryPrefab, m_questListRoot);
                if (instance == null)
                {
                    Debug.LogWarning("没有可用的任务日志条目实例，请检查任务日志对象池容量。", this);
                    continue;
                }

                if (!instance.TryGetComponent(out UIJournalQuestEntry entry))
                {
                    Debug.LogError($"任务日志条目预制体缺少 {nameof(UIJournalQuestEntry)} 组件。", instance);
                    GameObjectPoolService.Return(instance);
                    continue;
                }

                m_quests[i] = entry;
                entry.Hide();
            }
        }

        private void OnDestroy()
        {
            ReturnQuestEntries();
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            UpdateUI();
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            foreach (UIJournalQuestEntry entry in m_quests)
            {
                if (entry != null && entry.gameObject.activeInHierarchy)
                {
                    return entry.gameObject;
                }
            }

            return null;
        }

        private void UpdateUI()
        {
            int entryCount = m_quests.Length;
            int usedQuestEntries = 0;

            foreach (Quest quest in GameManager.JournalSystem.GetFullfilledQuests())
            {
                if (usedQuestEntries < entryCount)
                {
                    UIJournalQuestEntry availableText = m_quests[usedQuestEntries++];
                    availableText.SetTargetQuest(quest);
                }
                else
                {
                    Debug.LogError("Not enough quest texts allocated");
                }
            }

            foreach (QuestProgress progress in GameManager.JournalSystem.GetActiveQuests())
            {
                if (usedQuestEntries < entryCount)
                {
                    UIJournalQuestEntry availableText = m_quests[usedQuestEntries++];
                    availableText.SetTargetQuest(progress.quest, progress);
                }
                else
                {
                    Debug.LogError("Not enough quest texts allocated");
                }
            }

            // Disable remaining texts
            for (int i = usedQuestEntries; i < entryCount; ++i)
            {
                m_quests[i]?.Hide();
            }

            m_questDescription.UpdateUI();
        }

        public void HandleQuestDescriptionUpdate(JournalQuestEntry entry)
        {
            m_questDescription.SetTargetQuest(entry.quest, entry.instance);
            UpdateUI();
        }

        private void ConfigureQuestEntryPool()
        {
            if (m_questEntryPrefab == null)
            {
                return;
            }

            GameObjectPoolService.SetMaxCapacity(m_questEntryPrefab, m_questEntryPoolSize);
            GameObjectPoolService.Prewarm(m_questEntryPrefab, m_questEntryPoolSize);
        }

        private void ReturnQuestEntries()
        {
            foreach (UIJournalQuestEntry entry in m_quests)
            {
                if (entry)
                {
                    GameObjectPoolService.Return(entry.gameObject);
                }
            }
        }
    }
}

