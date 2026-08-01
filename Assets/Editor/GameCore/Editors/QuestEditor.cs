using UnityEditor;

namespace FantasyWord.GameCore
{
    [CustomEditor(typeof(Quest))]
    public class QuestEditor : DatabaseEntryEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Quest quest = target as Quest;

            if (!IsQuestOfferDialogueValid(quest.questOfferDialogue))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(string.Format("The quest offer DialogueSequence has no option sending the expected [{0}] message. The quest may have no way to get accepted by the player.", EDialogueMessageType.Accept), MessageType.Warning);
            }
        }

        private bool IsQuestOfferDialogueValid(DialogueSequence sequence)
        {
            return HasQuestOfferMessage(sequence);
        }

        private bool HasQuestOfferMessage(DialogueSequence sequence)
        {
            if (sequence)
            {
                foreach (DialogueSequenceOption option in sequence.GetOptions())
                {
                    if (option.message.Equals(EDialogueMessageType.Accept))
                    {
                        return true;
                    }
                    else if (option.sequence)
                    {
                        return HasQuestOfferMessage(option.sequence);
                    }
                }
            }

            return false;
        }
    }
}

