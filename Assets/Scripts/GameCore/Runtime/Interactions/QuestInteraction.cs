using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class QuestInteraction : IInteraction
    {
        private async Task<bool> TryProgressingQuest(CharacterBase source, CharacterActor character)
        {
            TalkToCharacterTaskProgress taskProgress = GameManager.JournalSystem.GetTaskToComplete(character);
            if (taskProgress != null)
            {
                await character.Say(taskProgress.talkToCharacterTask.dialogue, source);
                taskProgress.MarkAsCompleted();
                return true;
            }

            return false;
        }

        private async Task<bool> TryCompletingQuest(CharacterBase source, CharacterActor character)
        {
            Quest quest = GameManager.JournalSystem.GetQuestToComplete(character);

            if (quest)
            {
                if (quest.questCompletedDialogue != null)
                {
                    await character.Say(quest.questCompletedDialogue, source);
                    await GameManager.JournalSystem.CompleteQuest(quest, ResolveQuestCompletionCommandContext(source));

                    return true;
                }
                else
                {
                    Debug.LogErrorFormat("No quest completed dialogue provided for [{0}]", quest.title);
                }
            }

            return false;
        }

        private static GameCommandContext ResolveQuestCompletionCommandContext(CharacterBase source)
        {
            return GameCommandContext.ResolveForActor(source);
        }

        private DialogueSequence FindQuestHintDialogue(Quest quest)
        {
            // Look for quest hint overrides (some tasks may have specific hints)
            if (quest.questHintDialogueOverrideCount > 0)
            {
                foreach (var activeQuest in GameManager.JournalSystem.GetActiveQuests())
                {
                    foreach (var task in activeQuest.GetCurrentTasks())
                    {
                        if (quest.TryGetQuestHintDialogueOverride(task.task, out DialogueSequence overrideDialogue))
                        {
                            return overrideDialogue;
                        }
                    }
                }
            }

            if (quest.questHintDialogue != null)
            {
                return quest.questHintDialogue;
            }

            return null;
        }

        private async Task<bool> TryGivingHint(CharacterBase source, CharacterActor character)
        {
            // Try to find a hint for a fullfilled quest (quest with no task, such as "Talk to X")
            Quest quest = GameManager.JournalSystem.GetFullfilledQuest(character);

            if (!quest)
            {
                // Try to find a hint for a started quest
                quest = GameManager.JournalSystem.GetStartedQuest(character);
            }

            if (quest != null)
            {
                DialogueSequence dialogue = FindQuestHintDialogue(quest);

                if (dialogue)
                {
                    await character.Say(dialogue, source);
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> TryOfferingQuest(CharacterBase source, CharacterActor character)
        {
            Quest quest = GameManager.JournalSystem.GetQuestToStart(character);

            if (quest)
            {
                if (quest.questOfferDialogue != null)
                {
                    await character.Say(quest.questOfferDialogue, source, (messages) =>
                    {
                        if (messages.Contains(EDialogueMessageType.Accept))
                        {
                            GameManager.JournalSystem.StartQuest(quest, ResolveQuestStartCommandContext(source));
                        }
                    });

                    return true;
                }
                else
                {
                    Debug.LogErrorFormat("No quest offer dialogue provided for [{0}]", quest.title);
                }
            }

            return false;
        }

        private static GameCommandContext ResolveQuestStartCommandContext(CharacterBase source)
        {
            return GameCommandContext.ResolveForActor(source);
        }

        public async Task<bool> TryExecute(CharacterBase source, IInteractionTarget target)
        {
            if (target is CharacterActor character)
            {
                if (!await TryCompletingQuest(source, character))
                {
                    if (!await TryProgressingQuest(source, character))
                    {
                        if (!await TryOfferingQuest(source, character))
                        {
                            if (!await TryGivingHint(source, character))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("QuestInteraction requires a CharacterActor target.");
                return false;
            }

            return true;
        }
    }
}

