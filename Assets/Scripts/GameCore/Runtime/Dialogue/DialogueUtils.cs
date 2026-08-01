namespace FantasyWord.GameCore
{
    public static class DialogueUtils
    {
        public static DialogueTree CreateDialogueTree(DialogueSequence sequence, string speaker, params string[] args)
        {
            return new(CreateDialogueNodeRecursive(sequence, speaker, args));
        }

        public static DialogueTree CreateDialogueTree(DialogueSequence sequence, string speaker, GameCommandContext commandContext, params string[] args)
        {
            return new(CreateDialogueNodeRecursive(sequence, speaker, args), commandContext);
        }

        private static DialogueNode CreateDialogueNodeRecursive(DialogueSequence sequence, string speaker, params string[] args)
        {
            DialogueNode root = null;
            DialogueNode previous = null;

            for (int i = 0; i < sequence.lineCount; ++i)
            {
                DialogueNode current = new();

                current.SetContent(StringFormatter.Format(sequence.GetLineAt(i), args), speaker);

                if (i == sequence.lineCount - 1)
                {
                    DialogueNodeOption[] options = new DialogueNodeOption[sequence.optionCount];

                    for (int j = 0; j < options.Length; ++j)
                    {
                        DialogueSequenceOption option = sequence.GetOptionAt(j);
                        options[j] = new()
                        {
                            name = StringFormatter.Format(option.name),
                            node = option.sequence ? CreateDialogueNodeRecursive(option.sequence, speaker, args) : null,
                            message = option.message
                        };
                    }

                    current.SetOptions(options);
                    sequence.ApplyLifecycleCommands(current);
                }

                if (root == null)
                {
                    root = current;
                }

                if (previous != null)
                {
                    previous.SetOptions(new DialogueNodeOption[]
                    {
                        new() { node = current }
                    });
                }

                previous = current;
            }

            return root;
        }
    }
}
