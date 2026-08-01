using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Quests + nameof(Quest))]
    public class Quest : DatabaseEntry
    {
        [Header("Details")]
        [SerializeField] private string m_title = string.Empty;
        [SerializeField][TextArea] private string m_description = string.Empty;
        [SerializeField][Range(Constants.MinLevel, Constants.MaxLevel)] private int m_recommendedLevel = 1;
        [SerializeField][Range(Constants.MinLevel, Constants.MaxLevel)] private int m_requiredLevel = 1;
        [SerializeField] private bool m_repeatable = false;
        [SerializeField] private QuestTask[] m_tasks = null;

        [Header("Completion")]
        [SerializeReference, SubclassSelector] private ICommand m_toExecuteOnQuestCompletion = null;

        [Header("Related Characters")]
        [SerializeField] private CharacterSheet m_offeredBy = null;
        [SerializeField] private CharacterSheet m_reportTo = null;

        [Header("Dialogues")]
        [SerializeField] private DialogueSequence m_questOfferDialogue = null;
        [SerializeField] private DialogueSequence m_questHintDialogue = null;
        [SerializeField] private DialogueSequence m_questCompletedDialogue = null;
        [SerializeField] private SerializableDictionary<QuestTask, DialogueSequence> m_questHintDialogueOverrides = new();

        public string title => m_title;
        public string description => m_description;
        public int recommendedLevel => m_recommendedLevel;
        public int requiredLevel => m_requiredLevel;
        public bool repeatable => m_repeatable;
        public CharacterSheet offeredBy => m_offeredBy;
        public CharacterSheet reportTo => m_reportTo;
        public DialogueSequence questOfferDialogue => m_questOfferDialogue;
        public DialogueSequence questHintDialogue => m_questHintDialogue;
        public DialogueSequence questCompletedDialogue => m_questCompletedDialogue;
        public int taskCount => m_tasks?.Length ?? 0;
        public QuestTask GetTaskAt(int index) => index >= 0 && index < taskCount ? m_tasks[index] : null;
        public QuestTask[] GetTasks() => m_tasks != null ? (QuestTask[])m_tasks.Clone() : Array.Empty<QuestTask>();
        public int questHintDialogueOverrideCount => m_questHintDialogueOverrides?.Count ?? 0;

        public bool TryGetQuestHintDialogueOverride(QuestTask task, out DialogueSequence dialogue)
        {
            dialogue = null;
            return task != null
                && m_questHintDialogueOverrides != null
                && m_questHintDialogueOverrides.TryGetValue(task, out dialogue);
        }

        public Task ExecuteOnQuestCompletion() => ExecuteOnQuestCompletion(GameCommandContext.Script());

        public Task ExecuteOnQuestCompletion(GameCommandContext context) => m_toExecuteOnQuestCompletion.Execute(context);
    }
}
