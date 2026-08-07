namespace GameCore
{
    // Constants that you shouldn't tweak in most cases,
    // unless you know what you're doing.
    public static class Constants
    {
        public const int MaxEquipedAbilityCount = 5; // Changing only this value isn't sufficient, you'll also need to change the UI
        public const float CollisionOffset = 0.02f;
        public const float Epsilon = 0.01f; // Value used for float comparisons, that is considered "close enough"
        public const float AcceptableDistanceFromTarget = 0.1f; // Determines what is a "close enough" distance to consider the target reached (avoid waiting forever to reach the target)
        public const int MinLevel = 1; // The minimum level a character can have
        public const int MaxLevel = 20; // The maximum level a character can have (adjust this if you want to allow higher levels)
        public const string UniquePlayerIdentifier = "player"; // This can break save files if you don't know what you're doing! (used to identify the player in save files)
        public const float DefaultMasterVolume = 0.5f; // The default master volume, in case no value is found in PlayerPrefs. 0.5 is the middle value (0.0 to 1.0)
    }
}

