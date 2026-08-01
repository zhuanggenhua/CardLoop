namespace FantasyWord.GameCore
{
    /// <summary>
    /// 非召唤角色死亡时发送的领域事件。它只描述被击杀的角色配置，不处理奖励、任务内容或 UI 日志。
    /// </summary>
    public readonly struct CharacterKilledEvent
    {
        public CharacterKilledEvent(CharacterSheet character)
        {
            Character = character;
        }

        public CharacterSheet Character { get; }
    }

    /// <summary>
    /// 任意角色获得经验时发送的成长事件。
    /// </summary>
    public readonly struct CharacterExperienceGainedEvent
    {
        public CharacterExperienceGainedEvent(CharacterActor character, int amount)
        {
            Character = character;
            Amount = amount;
        }

        public CharacterActor Character { get; }
        public int Amount { get; }
    }

    /// <summary>
    /// 任意角色升级时发送的成长事件。
    /// </summary>
    public readonly struct CharacterLevelUpEvent
    {
        public CharacterLevelUpEvent(CharacterActor character, int level)
        {
            Character = character;
            Level = level;
        }

        public CharacterActor Character { get; }
        public int Level { get; }
    }

    public static partial class GameRuntimeEvents
    {
        public static void NotifyCharacterKilled(CharacterSheet character)
        {
            if (!character)
            {
                return;
            }

            Publish(new CharacterKilledEvent(character));
        }

        public static void NotifyCharacterExperienceGained(CharacterActor character, int amount)
        {
            Publish(new CharacterExperienceGainedEvent(character, amount));
        }

        public static void NotifyCharacterLevelUp(CharacterActor character, int level)
        {
            Publish(new CharacterLevelUpEvent(character, level));
        }
    }
}
