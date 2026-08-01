namespace FantasyWord.GameCore
{
    /// <summary>
    /// 背包转移失败原因，供命令层、UI 和日志把失败反馈映射成可读提示。
    /// </summary>
    public enum EInventoryTransferFailureReason
    {
        None,
        InvalidItem,
        InvalidQuantity,
        InvalidSourceOwner,
        InvalidDestinationOwner,
        ActorNotParticipant,
        ActorActionLocked,
        InsufficientQuantity
    }

    /// <summary>
    /// 库存结果型操作失败原因。
    /// 用于商店、制作和物品使用这类“多步写入”流程，把玩家可理解的失败和配置错误区分开。
    /// </summary>
    public enum EInventoryOperationFailureReason
    {
        None,
        InsufficientFunds,
        InsufficientQuantity,
        InsufficientIngredients,
        ItemNotSellable
    }

    /// <summary>
    /// 库存结果型操作执行结果。
    /// 成功表示所有库存写入已经完成；失败表示未写入任何库存状态。
    /// </summary>
    public readonly struct InventoryOperationResult
    {
        private InventoryOperationResult(bool succeeded, EInventoryOperationFailureReason failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public bool Succeeded { get; }
        public EInventoryOperationFailureReason FailureReason { get; }

        public static InventoryOperationResult Success()
        {
            return new InventoryOperationResult(true, EInventoryOperationFailureReason.None);
        }

        public static InventoryOperationResult Failed(EInventoryOperationFailureReason failureReason)
        {
            return new InventoryOperationResult(false, failureReason);
        }
    }

    /// <summary>
    /// 一次背包物品转移的不可变请求，包含参与者、来源、目标、物品、数量和转移语义。
    /// </summary>
    public readonly struct InventoryTransferRequest
    {
        /// <summary>
        /// 从角色入口创建转移请求；没有明确命令来源时会包装成未知上下文。
        /// </summary>
        public InventoryTransferRequest(
            CharacterBase actor,
            InventoryOwnerHandle sourceOwner,
            InventoryOwnerHandle destinationOwner,
            Item item,
            int quantity,
            EItemTransferType transferType)
            : this(
                GameCommandContext.Unknown(actor),
                sourceOwner,
                destinationOwner,
                item,
                quantity,
                transferType)
        {
        }

        /// <summary>
        /// 从完整命令上下文创建转移请求，适合需要校验参与者或追踪来源的交互。
        /// </summary>
        public InventoryTransferRequest(
            GameCommandContext commandContext,
            InventoryOwnerHandle sourceOwner,
            InventoryOwnerHandle destinationOwner,
            Item item,
            int quantity,
            EItemTransferType transferType)
        {
            CommandContext = commandContext;
            SourceOwner = sourceOwner;
            DestinationOwner = destinationOwner;
            Item = item;
            Quantity = quantity;
            TransferType = transferType;
        }

        /// <summary>
        /// 发起本次转移的命令上下文，包含执行者和来源信息。
        /// </summary>
        public GameCommandContext CommandContext { get; }

        /// <summary>
        /// 发起转移的角色；没有角色来源时可能为空。
        /// </summary>
        public CharacterBase Actor => CommandContext.Actor;

        /// <summary>
        /// 物品转出的背包所有者。
        /// </summary>
        public InventoryOwnerHandle SourceOwner { get; }

        /// <summary>
        /// 物品转入的背包所有者。
        /// </summary>
        public InventoryOwnerHandle DestinationOwner { get; }

        /// <summary>
        /// 要转移的物品资产。
        /// </summary>
        public Item Item { get; }

        /// <summary>
        /// 请求转移的物品数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 转移类型，用于区分拾取、丢弃、交易或内部移动等语义。
        /// </summary>
        public EItemTransferType TransferType { get; }
    }

    /// <summary>
    /// 背包转移执行结果，保留原始请求、成功状态、实际转移数量和失败原因。
    /// </summary>
    public readonly struct InventoryTransferResult
    {
        private InventoryTransferResult(
            InventoryTransferRequest request,
            bool succeeded,
            int transferredQuantity,
            EInventoryTransferFailureReason failureReason)
        {
            Request = request;
            Succeeded = succeeded;
            TransferredQuantity = transferredQuantity;
            FailureReason = failureReason;
        }

        /// <summary>
        /// 原始请求快照，便于 UI 或日志回放失败原因。
        /// </summary>
        public InventoryTransferRequest Request { get; }

        /// <summary>
        /// 本次转移是否成功。
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 实际转移的数量；失败时为 0。
        /// </summary>
        public int TransferredQuantity { get; }

        /// <summary>
        /// 失败原因；成功时固定为 None。
        /// </summary>
        public EInventoryTransferFailureReason FailureReason { get; }

        /// <summary>
        /// 创建成功结果，并记录实际完成的转移数量。
        /// </summary>
        public static InventoryTransferResult Success(InventoryTransferRequest request, int transferredQuantity)
        {
            return new InventoryTransferResult(
                request,
                true,
                transferredQuantity,
                EInventoryTransferFailureReason.None);
        }

        /// <summary>
        /// 创建失败结果；不会转移任何数量。
        /// </summary>
        public static InventoryTransferResult Failed(
            InventoryTransferRequest request,
            EInventoryTransferFailureReason failureReason)
        {
            return new InventoryTransferResult(request, false, 0, failureReason);
        }
    }
}
