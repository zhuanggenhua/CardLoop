namespace GameCore
{
    public readonly struct CharacterAbilityAddedEvent
    {
        public CharacterAbilityAddedEvent(CharacterBase character, int formalGasAbilityCode)
        {
            Character = character;
            FormalGasAbilityCode = System.Math.Max(0, formalGasAbilityCode);
        }

        public CharacterBase Character { get; }
        public int FormalGasAbilityCode { get; }

        public string DisplayName => FormalGasAbilityCode > 0 &&
            FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                FormalGasAbilityCode,
                out FormalGasAbilityIdentity identity) &&
                !string.IsNullOrWhiteSpace(identity.DisplayName)
                    ? identity.DisplayName
                    : FormalGasAbilityCode > 0 ? $"EX-GAS Ability {FormalGasAbilityCode}" : string.Empty;
    }

    public readonly struct CharacterAbilityRemovedEvent
    {
        public CharacterAbilityRemovedEvent(CharacterBase character, int formalGasAbilityCode)
        {
            Character = character;
            FormalGasAbilityCode = System.Math.Max(0, formalGasAbilityCode);
        }

        public CharacterBase Character { get; }
        public int FormalGasAbilityCode { get; }

        public string DisplayName => FormalGasAbilityCode > 0 &&
            FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                FormalGasAbilityCode,
                out FormalGasAbilityIdentity identity) &&
                !string.IsNullOrWhiteSpace(identity.DisplayName)
                    ? identity.DisplayName
                    : FormalGasAbilityCode > 0 ? $"EX-GAS Ability {FormalGasAbilityCode}" : string.Empty;
    }

}
