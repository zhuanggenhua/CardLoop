namespace GameCore
{
    /// <summary>
    /// 能力菜单内部条目与父级菜单之间的正式 UI 合同。
    /// 当前只服务 `UIAbilities` 菜单闭包，用来替代同树内的 SendMessageUpwards 字符串消息。
    /// </summary>
    public interface IAbilityMenuEventReceiver
    {
        void HandleAbilitySlotClicked(int abilityIndex);
        void HandleAbilityHovered(CharacterEquippedAbilitySlotView slot);
        void HandleAbilityHovered(CharacterAbilityMenuEntry entry);
        void HandleNullAbilityHovered();
        void HandleAbilitySelectedFromList(UIAbilityListEntry ability);
        void HandleAbilityCategorySelected(EAbilityType type);
        void HandleAbilityCategoryHovered(EAbilityType type);
    }
}
