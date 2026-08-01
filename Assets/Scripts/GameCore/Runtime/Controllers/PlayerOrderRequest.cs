using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 正式玩家订单的目标范围。
    /// 它表达“订单应该只作用于主控成员，还是分发给当前控制组全部可批准成员”，
    /// 不再让每个调用方自己猜某个命令是不是群发。
    /// </summary>
    public enum EPlayerOrderTargetScope
    {
        PrimaryMemberOnly,
        ControlledGroup
    }

    /// <summary>
    /// 正式玩家订单的排队模式。
    /// 当前第一阶段先把替换、停止和最小可排队移动语义显式化；后续 RTS 队列可继续沿同一合同扩到更多订单族。
    /// </summary>
    public enum EPlayerOrderQueueMode
    {
        ReplaceCurrent,
        Append,
        StopCurrent
    }

    /// <summary>
    /// 批量空间订单的正式落点策略。
    /// 当前先收口“所有成员都挤到同一点”之外的最小正式语义，后续若引入更复杂编队，继续沿这条合同扩展。
    /// </summary>
    public enum EPlayerOrderSpatialPolicy
    {
        None,
        DistributedRing
    }

    /// <summary>
    /// 批量空间订单的落点合同。
    /// 这里只描述运行时如何分配成员目标点，不让 UI 或调用方各自硬编码偏移量。
    /// </summary>
    public readonly struct PlayerOrderSpatialContract
    {
        public PlayerOrderSpatialContract(
            EPlayerOrderSpatialPolicy policy,
            float spacing)
        {
            Policy = policy;
            Spacing = Mathf.Max(0.05f, spacing);
        }

        public EPlayerOrderSpatialPolicy Policy { get; }
        public float Spacing { get; }
        public bool UsesDistributedWorldPositions => Policy != EPlayerOrderSpatialPolicy.None;

        public static PlayerOrderSpatialContract None => default;
    }

    /// <summary>
    /// 从玩家输入请求投影出来的正式订单对象。
    /// 它只描述“命令是什么、发给谁、以什么排队语义进入运行时”，
    /// 不直接承接 UI 细节。
    /// </summary>
    public readonly struct PlayerOrderRequest
    {
        public PlayerOrderRequest(
            PlayerCommandRequest commandRequest,
            EPlayerOrderTargetScope targetScope,
            EPlayerOrderQueueMode queueMode,
            PlayerOrderSpatialContract spatialContract = default)
        {
            CommandRequest = commandRequest;
            TargetScope = targetScope;
            QueueMode = queueMode;
            SpatialContract = spatialContract;
        }

        public PlayerCommandRequest CommandRequest { get; }
        public EPlayerCommandKind Kind => CommandRequest.Kind;
        public GameCommandContext CommandContext => CommandRequest.CommandContext;
        public CharacterBase Actor => CommandRequest.Actor;
        public Vector2 Direction => CommandRequest.Direction;
        public bool HasWorldPosition => CommandRequest.HasWorldPosition;
        public Vector2? WorldPosition => CommandRequest.WorldPosition;
        public int AbilityIndex => CommandRequest.AbilityIndex;
        public CharacterBase TargetCharacter => CommandRequest.TargetCharacter;
        public GameObject InteractionTarget => CommandRequest.InteractionTarget;
        public EPlayerOrderTargetScope TargetScope { get; }
        public EPlayerOrderQueueMode QueueMode { get; }
        public PlayerOrderSpatialContract SpatialContract { get; }
        public bool IsQueueableMovementOrder => Kind == EPlayerCommandKind.ClickMove;
        public bool IsStopOrder => Kind == EPlayerCommandKind.StopMove;
        public bool UsesDistributedWorldPositions =>
            TargetScope == EPlayerOrderTargetScope.ControlledGroup &&
            HasWorldPosition &&
            SpatialContract.UsesDistributedWorldPositions;

        public static PlayerOrderRequest FromCommandRequest(PlayerCommandRequest commandRequest)
        {
            return new PlayerOrderRequest(
                commandRequest,
                ResolveTargetScope(commandRequest.Kind),
                ResolveQueueMode(commandRequest.Kind),
                ResolveSpatialContract(commandRequest.Kind));
        }

        public PlayerOrderRequest WithTargetScope(EPlayerOrderTargetScope targetScope)
        {
            return new PlayerOrderRequest(CommandRequest, targetScope, QueueMode, SpatialContract);
        }

        public PlayerOrderRequest WithQueueMode(EPlayerOrderQueueMode queueMode)
        {
            return new PlayerOrderRequest(CommandRequest, TargetScope, queueMode, SpatialContract);
        }

        public PlayerOrderRequest WithSpatialContract(PlayerOrderSpatialContract spatialContract)
        {
            return new PlayerOrderRequest(CommandRequest, TargetScope, QueueMode, spatialContract);
        }

        private static EPlayerOrderTargetScope ResolveTargetScope(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.Move => EPlayerOrderTargetScope.ControlledGroup,
                EPlayerCommandKind.StopMove => EPlayerOrderTargetScope.ControlledGroup,
                EPlayerCommandKind.ClickMove => EPlayerOrderTargetScope.ControlledGroup,
                EPlayerCommandKind.ToggleMovementControlMode => EPlayerOrderTargetScope.ControlledGroup,
                _ => EPlayerOrderTargetScope.PrimaryMemberOnly
            };
        }

        private static EPlayerOrderQueueMode ResolveQueueMode(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.StopMove => EPlayerOrderQueueMode.StopCurrent,
                _ => EPlayerOrderQueueMode.ReplaceCurrent
            };
        }

        private static PlayerOrderSpatialContract ResolveSpatialContract(EPlayerCommandKind kind)
        {
            return kind switch
            {
                EPlayerCommandKind.ClickMove => new PlayerOrderSpatialContract(
                    EPlayerOrderSpatialPolicy.DistributedRing,
                    0.65f),
                _ => PlayerOrderSpatialContract.None
            };
        }
    }

    /// <summary>
    /// 正式订单分发结果。
    /// 当前阶段先记录分发到多少成员，以及最后一次成员执行结果；后续队列/编队阶段可继续扩展。
    /// </summary>
    public readonly struct PlayerOrderResult
    {
        private PlayerOrderResult(
            PlayerOrderRequest orderRequest,
            int dispatchedMemberCount,
            bool succeeded,
            bool wasQueued,
            int queuedOrderCount,
            PlayerCommandResult lastCommandResult)
        {
            OrderRequest = orderRequest;
            DispatchedMemberCount = dispatchedMemberCount;
            Succeeded = succeeded;
            WasQueued = wasQueued;
            QueuedOrderCount = queuedOrderCount;
            LastCommandResult = lastCommandResult;
        }

        public PlayerOrderRequest OrderRequest { get; }
        public int DispatchedMemberCount { get; }
        public bool Succeeded { get; }
        public bool WasQueued { get; }
        public int QueuedOrderCount { get; }
        public PlayerCommandResult LastCommandResult { get; }

        public static PlayerOrderResult Success(
            PlayerOrderRequest orderRequest,
            int dispatchedMemberCount,
            PlayerCommandResult lastCommandResult)
        {
            return new PlayerOrderResult(orderRequest, dispatchedMemberCount, true, false, 0, lastCommandResult);
        }

        public static PlayerOrderResult Failed(
            PlayerOrderRequest orderRequest,
            int dispatchedMemberCount,
            PlayerCommandResult lastCommandResult)
        {
            return new PlayerOrderResult(orderRequest, dispatchedMemberCount, false, false, 0, lastCommandResult);
        }

        public static PlayerOrderResult Queued(
            PlayerOrderRequest orderRequest,
            int queuedOrderCount)
        {
            return new PlayerOrderResult(
                orderRequest,
                0,
                true,
                true,
                queuedOrderCount,
                PlayerCommandResult.Success(orderRequest.CommandRequest));
        }
    }
}
