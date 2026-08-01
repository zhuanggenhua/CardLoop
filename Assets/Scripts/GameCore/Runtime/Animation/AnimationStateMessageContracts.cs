namespace FantasyWord.GameCore
{
    /// <summary>
    /// 当前项目里已经正式登记的状态机动画消息名。
    /// 这些消息不再只是“某个字符串刚好被某个组件收到了”，而是有固定业务语义的正式合同。
    /// </summary>
    public static class AnimationStateMessageNames
    {
        public const string InvincibleAnimationStart = "OnInvincibleAnimationStart";
        public const string InvincibleAnimationStop = "OnInvincibleAnimationStop";
        public const string DeathAnimationStart = "OnDeathAnimationStart";
        public const string DeathAnimationStop = "OnDeathAnimationStop";
        public const string FadeInCompleted = "OnFadeInCompleted";
        public const string FadeOutCompleted = "OnFadeOutCompleted";
        public const string FloatingTextAnimationEnd = "OnFloatingTextAnimationEnd";
    }

    /// <summary>
    /// 角色动画状态机回调的正式合同。
    /// 当前只承载无敌与死亡状态的开始/结束，不让 StateMessageDispatcher 再靠字符串解释角色语义。
    /// </summary>
    public interface ICharacterAnimationStateReceiver
    {
        void OnInvincibleAnimationStart();
        void OnInvincibleAnimationStop();
        void OnDeathAnimationStart();
        void OnDeathAnimationStop();
    }

    /// <summary>
    /// 场景淡入淡出状态机回调的正式合同。
    /// 这样过场系统不再依赖 SendMessageUpwards 去碰运气命中私有方法。
    /// </summary>
    public interface ITransitionAnimationStateReceiver
    {
        void OnFadeInCompleted();
        void OnFadeOutCompleted();
    }

    /// <summary>
    /// 浮字动画生命周期回调的正式合同。
    /// 当前只负责在动画结束时归还或停用实例。
    /// </summary>
    public interface IFloatingTextAnimationStateReceiver
    {
        void OnFloatingTextAnimationEnd();
    }
}
