using System;

namespace GameCore
{
    /// <summary>
    /// Mod 在本地配置中的启用状态。
    /// </summary>
    public enum ModStatus
    {
        Enabled,
        Disabled,
        Delete
    }

    /// <summary>
    /// 单个 Mod 的本地状态记录，版本升级不会改变其稳定身份。
    /// </summary>
    [Serializable]
    public class ModState
    {
        /// <summary>
        /// Mod 清单声明的稳定身份。
        /// </summary>
        public string modId;

        /// <summary>
        /// 本地期望状态。
        /// </summary>
        public ModStatus status;
    }
}
