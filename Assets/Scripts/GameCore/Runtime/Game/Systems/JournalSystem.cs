using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 任务日志系统的存档块，分开保存未接、进行中、已达成和已完成任务。
    /// </summary>
    [Serializable]
    public class JournalDataBlock : DataBlock
    {
        /// <summary>
        /// 已解锁但尚未开始的任务。
        /// </summary>
        public DatabaseEntryReference<Quest>[] unlockedQuests;

        /// <summary>
        /// 正在进行中的任务进度。
        /// </summary>
        public QuestProgressDataBlock[] activeQuests;

        /// <summary>
        /// 已满足条件但尚未交付完成的任务。
        /// </summary>
        public DatabaseEntryReference<Quest>[] fullfilledQuests;

        /// <summary>
        /// 已完成任务。
        /// </summary>
        public DatabaseEntryReference<Quest>[] completedQuests;
    }

    /// <summary>
    /// 任务日志系统，负责任务解锁、开始、达成、完成、可接取刷新和任务进度存档。
    /// </summary>
    public class JournalSystem : AGameSystem, IDataBlockHandler<JournalDataBlock>
    {
        [Header("音频")]
        [InspectorName("任务开始音效")]
        [Tooltip("任务开始时播放的音效。")]
        [SerializeField] private AudioClipResolver m_questStartedSound;

        [InspectorName("任务完成音效")]
        [Tooltip("任务完成时播放的音效。")]
        [SerializeField] private AudioClipResolver m_questCompletedSound;

        private readonly List<Quest> m_unlockedQuests = new();
        private readonly List<Quest> m_availableQuests = new();
        private readonly List<QuestProgress> m_activeQuests = new();
        private readonly List<Quest> m_fullfilledQuests = new();
        private readonly List<Quest> m_completedQuests = new();

        // 对外只暴露快照，避免 UI 或交互层把系统内部列表当成自己的可变真相。
        public Quest[] GetUnlockedQuests() => m_unlockedQuests.ToArray();
        public Quest[] GetAvailableQuests() => m_availableQuests.ToArray();
        public QuestProgress[] GetActiveQuests() => m_activeQuests.ToArray();
        public Quest[] GetFullfilledQuests() => m_fullfilledQuests.ToArray();
        public Quest[] GetCompletedQuests() => m_completedQuests.ToArray();

        public bool IsQuestUnlocked(Quest quest) => m_unlockedQuests.Contains(quest);
        public bool IsQuestAvailable(Quest quest) => m_availableQuests.Contains(quest);
        public bool IsQuestActive(Quest quest) => m_activeQuests.Find(progress => progress.quest == quest) != null;
        public bool IsQuestFullfilled(Quest quest) => m_fullfilledQuests.Contains(quest);
        public bool IsQuestCompleted(Quest quest) => m_completedQuests.Contains(quest);

        public bool IsTaskActive(QuestTask task)
        {
            foreach (QuestProgress progress in m_activeQuests)
            {
                if (progress.HasCurrentTask(task))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTaskCompleted(QuestTask task)
        {
            foreach (QuestProgress progress in m_activeQuests)
            {
                if (progress.HasCompletedTask(task))
                {
                    return true;
                }
            }

            foreach (Quest quest in m_fullfilledQuests.Union(m_completedQuests))
            {
                if (quest.GetTasks().Contains(task))
                {
                    return true;
                }
            }

            return false;
        }

        public void StartQuest(Quest quest)
        {
            StartQuest(quest, GameCommandContext.Script());
        }

        public void StartQuest(Quest quest, GameCommandContext context)
        {
            EnsureValidQuest(quest, nameof(StartQuest));

            QuestProgress instance = new(quest, OnQuestFullfilled);
            m_unlockedQuests.Remove(quest);
            m_availableQuests.Remove(quest);
            m_activeQuests.Add(instance);
            instance.Initialize();
            GameRuntimeEvents.NotifyQuestStarted(quest, context);
            GameRuntimeEvents.RequestAudioPlayback(m_questStartedSound);
        }

        private void OnQuestFullfilled(QuestProgress instance)
        {
            if (instance == null || !instance.IsValid())
            {
                Debug.LogError($"[{nameof(JournalSystem)}] 收到缺失任务进度的达成回调，已忽略。");
                return;
            }

            instance.StopTracking();
            if (!m_fullfilledQuests.Contains(instance.quest))
            {
                m_fullfilledQuests.Add(instance.quest);
            }

            m_activeQuests.Remove(instance);
            GameRuntimeEvents.NotifyQuestFullfilled(instance.quest);
        }

        public Task CompleteQuest(Quest quest)
        {
            return CompleteQuest(quest, GameCommandContext.Script());
        }

        public async Task CompleteQuest(Quest quest, GameCommandContext context)
        {
            EnsureValidQuest(quest, nameof(CompleteQuest));

            m_fullfilledQuests.Remove(quest);
            m_completedQuests.Add(quest);

            GameRuntimeEvents.NotifyQuestCompleted(quest);
            GameRuntimeEvents.RequestAudioPlayback(m_questCompletedSound);

            if (quest.repeatable)
            {
                UnlockQuest(quest);
            }

            await quest.ExecuteOnQuestCompletion(context);
        }

        public void UnlockQuest(Quest quest)
        {
            EnsureValidQuest(quest, nameof(UnlockQuest));

            m_unlockedQuests.Add(quest);

            if (!m_availableQuests.Contains(quest) && CheckIfQuestRequirementsAreMet(quest))
            {
                m_availableQuests.Add(quest);
                GameRuntimeEvents.NotifyQuestAvailabilityChanged(quest, true);
            }

            GameRuntimeEvents.NotifyQuestUnlocked(quest);
        }

        public QuestProgress GetNonFullfilledQuestToReportTo(CharacterActor character)
        {
            return m_activeQuests.Find(quest => quest.quest.reportTo == character.characterSheet);
        }

        public Quest GetQuestToComplete(CharacterActor character)
        {
            return m_fullfilledQuests.Find(quest => quest.reportTo == character.characterSheet);
        }

        public TalkToCharacterTaskProgress GetTaskToComplete(CharacterActor character)
        {
            foreach (QuestProgress quest in m_activeQuests)
            {
                foreach (IQuestTaskProgress task in quest.GetCurrentTasks())
                {
                    if (task is TalkToCharacterTaskProgress progress && progress.talkToCharacterTask.target == character.characterSheet)
                    {
                        return progress;
                    }
                }
            }

            return null;
        }

        public Quest GetQuestToStart(CharacterActor character)
        {
            return m_availableQuests.Find(quest => quest.offeredBy == character.characterSheet);
        }

        public Quest GetStartedQuest(CharacterActor character)
        {
            List<QuestProgress> results = m_activeQuests.FindAll(quest => quest.quest.offeredBy == character.characterSheet);
            return results.Count > 0 ? results[0].quest : null;
        }

        public Quest GetFullfilledQuest(CharacterActor character)
        {
            List<Quest> results = m_fullfilledQuests.FindAll(quest => quest.offeredBy == character.characterSheet);
            return results.Count > 0 ? results[0] : null;
        }

        private void UpdateQuestsAvailability()
        {
            foreach (Quest quest in m_unlockedQuests)
            {
                bool requirementsMet = CheckIfQuestRequirementsAreMet(quest);

                if (!m_availableQuests.Contains(quest) && requirementsMet)
                {
                    m_availableQuests.Add(quest);
                    GameRuntimeEvents.NotifyQuestAvailabilityChanged(quest, true);
                }
                else if (m_availableQuests.Contains(quest) && !requirementsMet)
                {
                    m_availableQuests.Remove(quest);
                    GameRuntimeEvents.NotifyQuestAvailabilityChanged(quest, false);
                }
            }
        }

        public override void OnSystemStart()
        {
            EventKit.Type.Register<CharacterLevelUpEvent>(OnCharacterLevelUp);
        }

        public override void OnSystemStop()
        {
            StopActiveQuestTracking();
            EventKit.Type.UnRegister<CharacterLevelUpEvent>(OnCharacterLevelUp);
        }

        public override void OnSaveFileLoaded()
        {
            UpdateQuestsAvailability();
        }

        private void OnCharacterLevelUp(CharacterLevelUpEvent characterLevelUpEvent)
        {
            UpdateQuestsAvailability();
        }

        public void LoadDataBlock(JournalDataBlock block)
        {
            DatabaseRegistry database = GameManager.Database;

            StopActiveQuestTracking();
            m_unlockedQuests.Clear();
            m_availableQuests.Clear();
            m_activeQuests.Clear();
            m_fullfilledQuests.Clear();
            m_completedQuests.Clear();

            if (block == null)
            {
                UpdateQuestsAvailability();
                return;
            }

            if (block.unlockedQuests != null)
            {
                m_unlockedQuests.AddRange(LoadQuestReferences(database, block.unlockedQuests, "已解锁任务"));
            }

            if (block.fullfilledQuests != null)
            {
                m_fullfilledQuests.AddRange(LoadQuestReferences(database, block.fullfilledQuests, "已达成任务"));
            }

            if (block.completedQuests != null)
            {
                m_completedQuests.AddRange(LoadQuestReferences(database, block.completedQuests, "已完成任务"));
            }

            QuestProgressDataBlock[] activeQuestBlocks = block.activeQuests ?? Array.Empty<QuestProgressDataBlock>();
            foreach (QuestProgressDataBlock progressDataBlock in activeQuestBlocks)
            {
                if (progressDataBlock == null)
                {
                    continue;
                }

                QuestProgress progress = new(progressDataBlock, OnQuestFullfilled);
                if (!progress.IsValid())
                {
                    continue;
                }

                m_activeQuests.Add(progress);
                progress.CheckFullfillment();
            }

            UpdateQuestsAvailability();
        }

        public JournalDataBlock CreateDataBlock()
        {
            DatabaseRegistry database = GameManager.Database;
            return new JournalDataBlock
            {
                unlockedQuests = CreateQuestReferences(database, m_unlockedQuests, "已解锁任务"),
                activeQuests = CreateActiveQuestDataBlocks(m_activeQuests),
                fullfilledQuests = CreateQuestReferences(database, m_fullfilledQuests, "已达成任务"),
                completedQuests = CreateQuestReferences(database, m_completedQuests, "已完成任务")
            };
        }

        private void StopActiveQuestTracking()
        {
            foreach (QuestProgress progress in m_activeQuests)
            {
                progress?.StopTracking();
            }
        }

        private static IEnumerable<Quest> LoadQuestReferences(
            DatabaseRegistry database,
            IEnumerable<DatabaseEntryReference<Quest>> questReferences,
            string listName)
        {
            foreach (DatabaseEntryReference<Quest> questReference in questReferences)
            {
                Quest quest = database.LoadFromReference(questReference);
                if (quest)
                {
                    yield return quest;
                    continue;
                }

                Debug.LogError($"[{nameof(JournalSystem)}] 存档中的{listName} GUID 无法解析，已跳过：{questReference?.guid}");
            }
        }

        private static DatabaseEntryReference<Quest>[] CreateQuestReferences(
            DatabaseRegistry database,
            IEnumerable<Quest> quests,
            string listName)
        {
            if (quests == null)
            {
                return Array.Empty<DatabaseEntryReference<Quest>>();
            }

            List<DatabaseEntryReference<Quest>> references = new();
            foreach (Quest quest in quests)
            {
                if (!database.TryCreateReference(quest, out DatabaseEntryReference<Quest> reference))
                {
                    Debug.LogError($"[{nameof(JournalSystem)}] {listName}包含未登记任务资产，已跳过：{quest?.name ?? "<null>"}", quest);
                    continue;
                }

                references.Add(reference);
            }

            return references.ToArray();
        }

        private static QuestProgressDataBlock[] CreateActiveQuestDataBlocks(IEnumerable<QuestProgress> progresses)
        {
            if (progresses == null)
            {
                return Array.Empty<QuestProgressDataBlock>();
            }

            List<QuestProgressDataBlock> blocks = new();
            foreach (QuestProgress progress in progresses)
            {
                if (progress == null || !progress.IsValid())
                {
                    throw new InvalidOperationException(
                        $"[{nameof(JournalSystem)}] 进行中任务列表包含无效任务进度，不能把当前任务状态保存成部分存档。");
                }

                QuestProgressDataBlock block = progress.CreateDataBlock();
                if (block?.quest == null || string.IsNullOrWhiteSpace(block.quest.guid))
                {
                    throw new InvalidOperationException(
                        $"[{nameof(JournalSystem)}] 进行中任务 {progress.quest.name} 不能创建稳定数据库引用，不能写入存档。");
                }

                blocks.Add(block);
            }

            return blocks.ToArray();
        }

        private bool CheckIfQuestRequirementsAreMet(Quest quest)
        {
            return quest.requiredLevel <= GameManager.PlayerSystem.GetPrimaryPlayerCharacter().level;
        }

        private static void EnsureValidQuest(Quest quest, string operationName)
        {
            if (!quest)
            {
                throw new InvalidOperationException(
                    $"[{nameof(JournalSystem)}] {operationName} requires a valid quest asset and cannot silently skip the journal result.");
            }
        }
    }
}
