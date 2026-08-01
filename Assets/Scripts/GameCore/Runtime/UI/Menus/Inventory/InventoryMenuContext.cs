namespace FantasyWord.GameCore
{
    /// <summary>
    /// 背包菜单打开模式，决定是使用当前所有者物品还是转移到目标背包。
    /// </summary>
    public enum EInventoryMenuMode
    {
        UseOwnerItems,
        TransferToDestination
    }

    /// <summary>
    /// 背包菜单上下文，集中保存显示背包、目标背包、命令来源和转移类型。
    /// </summary>
    public readonly struct InventoryMenuContext
    {
        private InventoryMenuContext(
            GameCommandContext commandContext,
            InventoryOwnerHandle displayOwner,
            InventoryOwnerHandle destinationOwner,
            EInventoryMenuMode mode,
            EItemTransferType transferType)
        {
            CommandContext = commandContext;
            DisplayOwner = displayOwner;
            DestinationOwner = destinationOwner;
            Mode = mode;
            TransferType = transferType;
        }

        public GameCommandContext CommandContext { get; }
        public CharacterBase Actor => CommandContext.Actor;
        public InventoryOwnerHandle DisplayOwner { get; }
        public InventoryOwnerHandle DestinationOwner { get; }
        public EInventoryMenuMode Mode { get; }
        public EItemTransferType TransferType { get; }

        /// <summary>
        /// 创建跟随当前控制角色的默认背包菜单上下文。
        /// </summary>
        public static InventoryMenuContext CurrentControlledCharacter()
        {
            return new InventoryMenuContext(
                GameCommandContext.Unknown(),
                default,
                default,
                EInventoryMenuMode.UseOwnerItems,
                EItemTransferType.Use);
        }

        /// <summary>
        /// 创建查看指定角色背包的上下文。
        /// </summary>
        public static InventoryMenuContext ViewCharacter(CharacterBase actor)
        {
            if (actor == null)
            {
                return CurrentControlledCharacter();
            }

            InventoryOwnerHandle owner = ResolveInventoryOwner(actor);
            return new InventoryMenuContext(
                ResolveCommandContextForActor(actor),
                owner,
                owner,
                EInventoryMenuMode.UseOwnerItems,
                EItemTransferType.Use);
        }

        /// <summary>
        /// 创建从来源背包向目标角色转移物品的上下文。
        /// </summary>
        public static InventoryMenuContext TransferToCharacter(
            CharacterBase destination,
            InventoryOwnerHandle sourceOwner,
            EItemTransferType transferType)
        {
            return TransferToCharacter(
                ResolveCommandContextForActor(destination),
                destination,
                sourceOwner,
                transferType);
        }

        /// <summary>
        /// 用完整命令上下文创建从来源背包向目标角色转移物品的上下文。
        /// </summary>
        public static InventoryMenuContext TransferToCharacter(
            GameCommandContext commandContext,
            CharacterBase destination,
            InventoryOwnerHandle sourceOwner,
            EItemTransferType transferType)
        {
            InventoryOwnerHandle destinationOwner = ResolveInventoryOwner(destination);
            return new InventoryMenuContext(
                commandContext,
                sourceOwner,
                destinationOwner,
                EInventoryMenuMode.TransferToDestination,
                transferType);
        }

        /// <summary>
        /// 是否跟随当前控制角色动态解析背包所有者。
        /// </summary>
        public bool FollowsCurrentControlledCharacter =>
            Actor == null && !DisplayOwner.IsValid && !DestinationOwner.IsValid;

        /// <summary>
        /// 解析菜单当前作用的角色。
        /// </summary>
        public CharacterBase ResolveActor()
        {
            if (Actor)
            {
                return Actor;
            }

            return GameManager.TryGetSystem(out PlayerSystem playerSystem)
                ? playerSystem.GetCurrentControlledCharacterOrPlayerInstance()
                : null;
        }

        /// <summary>
        /// 解析菜单应展示的背包所有者。
        /// </summary>
        public InventoryOwnerHandle ResolveDisplayOwner()
        {
            if (DisplayOwner.IsValid)
            {
                return DisplayOwner;
            }

            return ResolveInventoryOwner(ResolveActor());
        }

        /// <summary>
        /// 解析物品转移的目标背包所有者。
        /// </summary>
        public InventoryOwnerHandle ResolveDestinationOwner()
        {
            if (DestinationOwner.IsValid)
            {
                return DestinationOwner;
            }

            return ResolveInventoryOwner(ResolveActor());
        }

        /// <summary>
        /// 按当前菜单上下文创建物品转移请求。
        /// </summary>
        public InventoryTransferRequest CreateTransferRequest(Item item, int quantity)
        {
            return new InventoryTransferRequest(
                ResolveCommandContext(),
                ResolveDisplayOwner(),
                ResolveDestinationOwner(),
                item,
                quantity,
                TransferType);
        }

        private GameCommandContext ResolveCommandContext()
        {
            if (CommandContext.HasActor || CommandContext.IssuerKind != EGameCommandIssuerKind.Unknown)
            {
                return CommandContext;
            }

            return ResolveCommandContextForActor(ResolveActor());
        }

        private static GameCommandContext ResolveCommandContextForActor(CharacterBase actor)
        {
            return GameCommandContext.ResolveForActor(actor);
        }

        private static InventoryOwnerHandle ResolveInventoryOwner(CharacterBase actor)
        {
            if (actor == null || !GameManager.TryGetSystem(out InventorySystem inventorySystem))
            {
                return default;
            }

            return inventorySystem.GetOwner(actor);
        }
    }
}
