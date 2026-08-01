using System;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 能力运行时额外恢复状态的正式基类。
    /// 这里只承载通用冷却/本地输入门控状态之外、确实无法从 EX-GAS 配置直接重建的最小剩余数据。
    /// </summary>
    [Serializable]
    public abstract class AbilityRuntimeExtraState
    {
    }

    /// <summary>
    /// 需要额外恢复状态的能力，由能力自己声明和恢复最小 extra state。
    /// CharacterBase 只负责汇总，不再持有每种能力的私有数据细节。
    /// </summary>
    public interface IAbilityRuntimeExtraStateCarrier
    {
        bool TryCaptureRuntimeExtraState(out AbilityRuntimeExtraState extraState);

        void RestoreRuntimeExtraState(AbilityRuntimeExtraState extraState);
    }
}
