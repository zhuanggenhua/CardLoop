using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 玩家输入被归一化后的命令类型。
    /// 输入系统只负责生成这些请求，实际执行由玩家控制系统判定。
    /// </summary>
    public enum EPlayerCommandKind
    {
        Interact,
        OpenGameMenu,
        Move,
        StopMove,
        ClickMove,
        ToggleMovementControlMode,
        FireAbility,
        StopFireAbility
    }

    /// <summary>
    /// 玩家命令执行失败原因。
    /// 用于诊断和 UI 提示，避免只返回 false 丢失是哪道门禁拒绝。
    /// </summary>
    public enum EPlayerCommandFailureReason
    {
        None,
        InvalidCommand,
        MissingInputTarget,
        InvalidControlledCharacter,
        InvalidTarget,
        ActorMismatch,
        NotRunning,
        ControlLocked,
        InteractionLocked,
        BlockedByState,
        MissingAbility,
        AbilityRejected
    }

    /// <summary>
    /// 一次玩家命令请求。
    /// 它把动作类型、方向、点击位置、技能槽和交互目标打包，执行层再统一校验 Actor。
    /// </summary>
    public readonly struct PlayerCommandRequest
    {
        public PlayerCommandRequest(
            GameCommandContext commandContext,
            EPlayerCommandKind kind,
            Vector2 direction = default,
            Vector2? worldPosition = null,
            int abilityIndex = -1,
            CharacterBase targetCharacter = null,
            GameObject interactionTarget = null)
        {
            CommandContext = commandContext;
            Kind = kind;
            Direction = direction;
            WorldPosition = worldPosition;
            AbilityIndex = abilityIndex;
            TargetCharacter = targetCharacter;
            InteractionTarget = interactionTarget;
        }

        public GameCommandContext CommandContext { get; }
        public CharacterBase Actor => CommandContext.Actor;
        public EPlayerCommandKind Kind { get; }
        public Vector2 Direction { get; }
        public bool HasWorldPosition => WorldPosition.HasValue;
        public Vector2? WorldPosition { get; }
        public int AbilityIndex { get; }
        public CharacterBase TargetCharacter { get; }
        public GameObject InteractionTarget { get; }
    }

    /// <summary>
    /// 玩家命令执行结果。
    /// 成功时保留原请求，失败时额外给出失败原因。
    /// </summary>
    public readonly struct PlayerCommandResult
    {
        private PlayerCommandResult(
            PlayerCommandRequest request,
            bool succeeded,
            EPlayerCommandFailureReason failureReason)
        {
            Request = request;
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public PlayerCommandRequest Request { get; }
        public bool Succeeded { get; }
        public EPlayerCommandFailureReason FailureReason { get; }

        public static PlayerCommandResult Success(PlayerCommandRequest request)
        {
            return new PlayerCommandResult(request, true, EPlayerCommandFailureReason.None);
        }

        public static PlayerCommandResult Failed(
            PlayerCommandRequest request,
            EPlayerCommandFailureReason failureReason)
        {
            return new PlayerCommandResult(request, false, failureReason);
        }
    }
}
