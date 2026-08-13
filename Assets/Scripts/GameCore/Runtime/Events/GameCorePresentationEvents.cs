namespace GameCore
{
    /// <summary>
    /// 请求播放音频时发送的表现层事件。事件类型归 GameCore，派发机制统一使用 Yoki EventKit。
    /// </summary>
    public readonly struct AudioPlaybackRequestedEvent
    {
        public AudioPlaybackRequestedEvent(AudioClipResolver audioClipResolver)
        {
            AudioClipResolver = audioClipResolver;
        }

        public AudioClipResolver AudioClipResolver { get; }
    }

    /// <summary>
    /// 受击反馈播放后发送的纯表现事件。
    /// 它只给镜头、闪屏和浮字这类表现层消费，不承担伤害或死亡结算。
    /// </summary>
    internal readonly struct DamageTakenPresentationEvent
    {
        public DamageTakenPresentationEvent(DamageTakenFeedbackContext context)
        {
            Context = context;
        }

        public DamageTakenFeedbackContext Context { get; }
    }

    /// <summary>
    /// 生命恢复表现事件。它只描述需要展示的恢复结果，不改变正式属性真相。
    /// </summary>
    public readonly struct HealthRecoveredPresentationEvent
    {
        public HealthRecoveredPresentationEvent(CharacterValuePresentationContext context)
        {
            Context = context;
        }

        public CharacterValuePresentationContext Context { get; }
    }

    /// <summary>
    /// 法力消耗表现事件。它只服务表现监听者，不承载资源扣减规则。
    /// </summary>
    public readonly struct ManaConsumedPresentationEvent
    {
        public ManaConsumedPresentationEvent(CharacterValuePresentationContext context)
        {
            Context = context;
        }

        public CharacterValuePresentationContext Context { get; }
    }

    /// <summary>
    /// 法力恢复表现事件。它只服务表现监听者，不承载资源恢复规则。
    /// </summary>
    public readonly struct ManaRecoveredPresentationEvent
    {
        public ManaRecoveredPresentationEvent(CharacterValuePresentationContext context)
        {
            Context = context;
        }

        public CharacterValuePresentationContext Context { get; }
    }

    /// <summary>
    /// 死亡表现事件。它只服务表现层，不承担死亡逻辑推进。
    /// </summary>
    public readonly struct DeathPresentationEvent
    {
        public DeathPresentationEvent(DeathPresentationContext context)
        {
            Context = context;
        }

        public DeathPresentationContext Context { get; }
    }

    /// <summary>
    /// 交互表现事件。它只服务表现监听者，不承担交互命令执行真相。
    /// </summary>
    public readonly struct InteractionPresentationEvent
    {
        public InteractionPresentationEvent(InteractionPresentationContext context)
        {
            Context = context;
        }

        public InteractionPresentationContext Context { get; }
    }

    /// <summary>
    /// 玩家尝试释放能力失败时发送的表现事件。它只描述失败原因，不负责 UI 文案和具体能力结算。
    /// </summary>
    public readonly struct PlayerAbilityFireFailedEvent
    {
        public PlayerAbilityFireFailedEvent(int formalGasAbilityCode, EAbilityFireCheckResult reason)
        {
            FormalGasAbilityCode = System.Math.Max(0, formalGasAbilityCode);
            Reason = reason;
        }

        public int FormalGasAbilityCode { get; }
        public bool HasFormalGasAbility => FormalGasAbilityCode > 0;

        public EAbilityFireCheckResult Reason { get; }

    }

    /// <summary>
    /// 本地玩家命令被正式输入目标拒绝时发送的表现事件。
    /// 它只暴露裁决结果给 HUD，不改变命令执行或世界状态。
    /// </summary>
    public readonly struct LocalPlayerCommandFailedEvent
    {
        public LocalPlayerCommandFailedEvent(PlayerCommandResult result)
        {
            Result = result;
        }

        public PlayerCommandResult Result { get; }
    }

}
