using System;

namespace GameCore
{
    /// <summary>
    /// 游戏命令的发起方类型，用于权限判断、交互参与者校验和日志归因。
    /// </summary>
    public enum EGameCommandIssuerKind
    {
        Unknown,
        LocalPlayer,
        AI,
        Script,
        RemotePlayer
    }

    /// <summary>
    /// 一次游戏命令的调用上下文，集中记录发起方、发起方标识和实际执行角色。
    /// </summary>
    public readonly struct GameCommandContext
    {
        private GameCommandContext(EGameCommandIssuerKind issuerKind, string issuerId, CharacterBase actor)
        {
            IssuerKind = issuerKind;
            IssuerId = string.IsNullOrWhiteSpace(issuerId) ? string.Empty : issuerId;
            Actor = actor;
        }

        /// <summary>
        /// 发起方类型。
        /// </summary>
        public EGameCommandIssuerKind IssuerKind { get; }

        /// <summary>
        /// 发起方标识；本地玩家和 AI 使用固定标识，远端或脚本可传入自定义值。
        /// </summary>
        public string IssuerId { get; }

        /// <summary>
        /// 命令实际作用的角色，部分脚本命令允许为空。
        /// </summary>
        public CharacterBase Actor { get; }

        /// <summary>
        /// 当前上下文是否带有明确角色。
        /// </summary>
        public bool HasActor => Actor != null;

        /// <summary>
        /// 当前命令是否由本地玩家发起。
        /// </summary>
        public bool IsLocalPlayer => IssuerKind == EGameCommandIssuerKind.LocalPlayer;

        /// <summary>
        /// 创建未知来源上下文，适合兼容旧入口或无法归因的自动动作。
        /// </summary>
        public static GameCommandContext Unknown(CharacterBase actor = null)
        {
            return new GameCommandContext(EGameCommandIssuerKind.Unknown, string.Empty, actor);
        }

        /// <summary>
        /// 创建本地玩家上下文。
        /// </summary>
        public static GameCommandContext LocalPlayer(CharacterBase actor)
        {
            return new GameCommandContext(EGameCommandIssuerKind.LocalPlayer, "local", actor);
        }

        /// <summary>
        /// 创建 AI 控制器上下文。
        /// </summary>
        public static GameCommandContext AI(CharacterBase actor)
        {
            return new GameCommandContext(EGameCommandIssuerKind.AI, "ai", actor);
        }

        /// <summary>
        /// 创建脚本上下文，允许传入脚本来源标识。
        /// </summary>
        public static GameCommandContext Script(CharacterBase actor = null, string issuerId = null)
        {
            return new GameCommandContext(EGameCommandIssuerKind.Script, issuerId, actor);
        }

        /// <summary>
        /// 创建远端玩家上下文；当前项目仍单机优先，该入口只保留有限合作的语义边界。
        /// </summary>
        public static GameCommandContext RemotePlayer(CharacterBase actor, string issuerId)
        {
            return new GameCommandContext(EGameCommandIssuerKind.RemotePlayer, issuerId, actor);
        }

        /// <summary>
        /// 根据已保存的来源类型重建上下文，未知类型会降级为脚本上下文。
        /// </summary>
        public static GameCommandContext Recreate(EGameCommandIssuerKind issuerKind, CharacterBase actor = null, string issuerId = null)
        {
            return issuerKind switch
            {
                EGameCommandIssuerKind.LocalPlayer => LocalPlayer(actor),
                EGameCommandIssuerKind.AI => AI(actor),
                EGameCommandIssuerKind.RemotePlayer => RemotePlayer(actor, issuerId),
                EGameCommandIssuerKind.Script => Script(actor, issuerId),
                EGameCommandIssuerKind.Unknown => Unknown(actor),
                _ => Script(actor, issuerId)
            };
        }

        /// <summary>
        /// 根据角色当前控制器状态推断命令上下文，优先识别本地玩家，其次识别 AI。
        /// </summary>
        public static GameCommandContext ResolveForActor(CharacterBase actor)
        {
            if (actor == null)
            {
                return Unknown();
            }

            if (TryGetPlayerSystem(out PlayerSystem playerSystem) &&
                playerSystem.IsCurrentControlledMember(actor))
            {
                return LocalPlayer(actor);
            }

            if (actor.IsControllerActive<AIController>())
            {
                return AI(actor);
            }

            return Unknown(actor);
        }

        /// <summary>
        /// 返回上下文角色；若为空则退回当前控制角色，适合旧命令入口兼容。
        /// </summary>
        public CharacterBase ResolveActorOrCurrentControlledCharacter()
        {
            if (Actor != null)
            {
                return Actor;
            }

            return TryGetPlayerSystem(out PlayerSystem playerSystem)
                ? playerSystem.GetCurrentControlledCharacterOrPlayerInstance()
                : null;
        }

        /// <summary>
        /// 返回命令作用角色；缺少角色时暴露正式命令配置错误，而不是吞掉奖励、治疗、复活等结果。
        /// </summary>
        public CharacterBase ResolveRequiredActorOrCurrentControlledCharacter(string commandName)
        {
            if (Actor != null)
            {
                return Actor;
            }

            CharacterBase currentControlledCharacter =
                GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            if (currentControlledCharacter != null)
            {
                return currentControlledCharacter;
            }

            string owner = string.IsNullOrWhiteSpace(commandName)
                ? nameof(IContextualCommand)
                : commandName;
            throw new InvalidOperationException(
                $"{owner} 需要命令作用角色，但上下文没有角色，且 PlayerSystem 没有当前受控角色。");
        }

        private static bool TryGetPlayerSystem(out PlayerSystem playerSystem)
        {
            playerSystem = null;
            return GameManager.Exists() && GameManager.TryGetSystem(out playerSystem);
        }
    }
}
