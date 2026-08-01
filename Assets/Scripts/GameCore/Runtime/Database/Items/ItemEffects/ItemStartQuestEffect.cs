using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class ItemStartQuestEffect : AItemEffect
    {
        [SerializeField] private string m_dialogueLine;
        [SerializeField] private Quest m_questToStart;

        protected override ItemUsageResult OnUse(Item item, CharacterBase sourceOwner, CharacterBase target, EItemLocation location)
        {
            bool canPlayQuest =
                !GameManager.JournalSystem.IsQuestActive(m_questToStart) &&
                !GameManager.JournalSystem.IsQuestFullfilled(m_questToStart) &&
                (!GameManager.JournalSystem.IsQuestCompleted(m_questToStart) || m_questToStart.repeatable);

            if (canPlayQuest)
            {
                GameManager.JournalSystem.StartQuest(m_questToStart, ResolveQuestStartCommandContext(sourceOwner, target));

                return new()
                {
                    success = true,
                    message = m_dialogueLine
                };
            }

            return new() { success = false };
        }

        private static GameCommandContext ResolveQuestStartCommandContext(CharacterBase sourceOwner, CharacterBase target)
        {
            CharacterBase actor = sourceOwner ? sourceOwner : target;
            if (!actor)
            {
                return GameCommandContext.Unknown();
            }

            return GameCommandContext.ResolveForActor(actor);
        }
    }
}

