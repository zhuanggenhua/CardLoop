namespace FantasyWord.GameCore
{
    /// <summary>
    /// 角色的单槽位装备变化描述。
    /// 预演和正式应用共用同一份变化数据，避免规则分散到多处。
    /// </summary>
    internal readonly struct CharacterEquipmentSlotChange
    {
        public CharacterEquipmentSlotChange(
            EEquipmentType slotType,
            Equipment previousEquipment,
            Equipment nextEquipment,
            Stats statDelta,
            int[] removedFormalGasAbilityCodes,
            int[] addedFormalGasAbilityCodes)
        {
            SlotType = slotType;
            PreviousEquipment = previousEquipment;
            NextEquipment = nextEquipment;
            StatDelta = statDelta;
            RemovedFormalGasAbilityCodes = removedFormalGasAbilityCodes;
            AddedFormalGasAbilityCodes = addedFormalGasAbilityCodes;
        }

        public EEquipmentType SlotType { get; }
        public Equipment PreviousEquipment { get; }
        public Equipment NextEquipment { get; }
        public Stats StatDelta { get; }
        public int[] RemovedFormalGasAbilityCodes { get; }
        public int[] AddedFormalGasAbilityCodes { get; }
    }
}
