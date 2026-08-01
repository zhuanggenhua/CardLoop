using System;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 长期背包金钱增加时发送的事件。它只描述背包金额变化，不决定 UI 展示或奖励来源。
    /// </summary>
    public readonly struct InventoryMoneyAddedEvent
    {
        public InventoryMoneyAddedEvent(int amount)
            : this(InventoryOwnerHandle.DefaultParty, amount)
        {
        }

        public InventoryMoneyAddedEvent(InventoryOwnerHandle owner, int amount)
        {
            Owner = owner;
            Amount = amount;
        }

        public InventoryOwnerHandle Owner { get; }

        public int Amount { get; }
    }

    /// <summary>
    /// 长期背包金钱减少时发送的事件。它只描述背包金额变化，不决定消耗原因或菜单反馈。
    /// </summary>
    public readonly struct InventoryMoneyRemovedEvent
    {
        public InventoryMoneyRemovedEvent(int amount)
            : this(InventoryOwnerHandle.DefaultParty, amount)
        {
        }

        public InventoryMoneyRemovedEvent(InventoryOwnerHandle owner, int amount)
        {
            Owner = owner;
            Amount = amount;
        }

        public InventoryOwnerHandle Owner { get; }

        public int Amount { get; }
    }

    /// <summary>
    /// 背包新增物品时发送的事件。它只描述物品真相变化，不承载拾取、商店或制作流程。
    /// </summary>
    public readonly struct InventoryItemAddedEvent
    {
        public InventoryItemAddedEvent(Item item, int quantity, EItemTransferType transferType)
            : this(InventoryOwnerHandle.DefaultParty, item, quantity, transferType)
        {
        }

        public InventoryItemAddedEvent(InventoryOwnerHandle owner, Item item, int quantity, EItemTransferType transferType)
        {
            Owner = owner;
            Item = item;
            Quantity = quantity;
            TransferType = transferType;
        }

        public InventoryOwnerHandle Owner { get; }

        public Item Item { get; }

        public int Quantity { get; }

        public EItemTransferType TransferType { get; }
    }

    /// <summary>
    /// 背包移除物品时发送的事件。它只描述物品真相变化，不决定消耗、装备或交易语义。
    /// </summary>
    public readonly struct InventoryItemRemovedEvent
    {
        public InventoryItemRemovedEvent(Item item, int quantity, EItemTransferType transferType)
            : this(InventoryOwnerHandle.DefaultParty, item, quantity, transferType)
        {
        }

        public InventoryItemRemovedEvent(InventoryOwnerHandle owner, Item item, int quantity, EItemTransferType transferType)
        {
            Owner = owner;
            Item = item;
            Quantity = quantity;
            TransferType = transferType;
        }

        public InventoryOwnerHandle Owner { get; }

        public Item Item { get; }

        public int Quantity { get; }

        public EItemTransferType TransferType { get; }
    }

    /// <summary>
    /// 角色获得能力时发送的事件。它只描述能力列表变化，不决定 UI 或装备槽行为。
    /// </summary>
    public readonly struct CharacterAbilityAddedEvent
    {
        public CharacterAbilityAddedEvent(CharacterBase character, int formalGasAbilityCode)
        {
            Character = character;
            FormalGasAbilityCode = Math.Max(0, formalGasAbilityCode);
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

    /// <summary>
    /// 角色失去能力时发送的事件。它只描述能力列表变化，不决定 UI 或装备槽行为。
    /// </summary>
    public readonly struct CharacterAbilityRemovedEvent
    {
        public CharacterAbilityRemovedEvent(CharacterBase character, int formalGasAbilityCode)
        {
            Character = character;
            FormalGasAbilityCode = Math.Max(0, formalGasAbilityCode);
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

    public static partial class GameRuntimeEvents
    {
        public static void NotifyInventoryMoneyAdded(int amount)
        {
            NotifyInventoryMoneyAdded(InventoryOwnerHandle.DefaultParty, amount);
        }

        public static void NotifyInventoryMoneyAdded(InventoryOwnerHandle owner, int amount)
        {
            Publish(new InventoryMoneyAddedEvent(owner, amount));
        }

        public static void NotifyInventoryMoneyRemoved(int amount)
        {
            NotifyInventoryMoneyRemoved(InventoryOwnerHandle.DefaultParty, amount);
        }

        public static void NotifyInventoryMoneyRemoved(InventoryOwnerHandle owner, int amount)
        {
            Publish(new InventoryMoneyRemovedEvent(owner, amount));
        }

        public static void NotifyInventoryItemAdded(Item item, int quantity, EItemTransferType transferType)
        {
            NotifyInventoryItemAdded(InventoryOwnerHandle.DefaultParty, item, quantity, transferType);
        }

        public static void NotifyInventoryItemAdded(InventoryOwnerHandle owner, Item item, int quantity, EItemTransferType transferType)
        {
            if (!item)
            {
                return;
            }

            Publish(new InventoryItemAddedEvent(owner, item, quantity, transferType));
        }

        public static void NotifyInventoryItemRemoved(Item item, int quantity, EItemTransferType transferType)
        {
            NotifyInventoryItemRemoved(InventoryOwnerHandle.DefaultParty, item, quantity, transferType);
        }

        public static void NotifyInventoryItemRemoved(InventoryOwnerHandle owner, Item item, int quantity, EItemTransferType transferType)
        {
            if (!item)
            {
                return;
            }

            Publish(new InventoryItemRemovedEvent(owner, item, quantity, transferType));
        }

        public static void NotifyCharacterFormalGasAbilityAdded(CharacterBase character, int formalGasAbilityCode)
        {
            if (!character || formalGasAbilityCode <= 0)
            {
                return;
            }

            Publish(new CharacterAbilityAddedEvent(character, formalGasAbilityCode));
        }

        public static void NotifyCharacterFormalGasAbilityRemoved(CharacterBase character, int formalGasAbilityCode)
        {
            if (!character || formalGasAbilityCode <= 0)
            {
                return;
            }

            Publish(new CharacterAbilityRemovedEvent(character, formalGasAbilityCode));
        }
    }
}
