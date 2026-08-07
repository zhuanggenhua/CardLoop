namespace GameCore
{
    /// <summary>
    /// 效果链中断策略。
    /// 用于决定某个效果失败或成功后，后续效果是否继续执行。
    /// </summary>
    public enum EEffectInterruptionPolicy
    {
        AfterFail,
        AfterSuccess,
        Never
    }

    /// <summary>
    /// 效果描述文本。
    /// name 用于标题或短标签，details 用于数值和规则说明。
    /// </summary>
    public struct EffectDescription
    {
        public string name;
        public string details;
    }

    /// <summary>
    /// 战斗效果合同。
    /// 调用方按 Init -> IsApplicable -> Apply -> Deinit 生命周期使用，不直接读取具体效果私有字段。
    /// </summary>
    public interface IEffect
    {
        public bool initialized { get; }
        public EEffectInterruptionPolicy interruptionPolicy { get; }
        public EEffectVisualFlags visualFlags { get; }

        public void Init(CharacterBase source);
        public bool IsApplicable(CharacterBase target);
        public bool Apply(CharacterBase target, EffectImpactSettings? impactSettings = null);
        public void Deinit();

        public EffectDescription GenerateDescription();
    }
}

