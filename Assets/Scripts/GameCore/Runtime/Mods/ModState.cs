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
    /// 单个 Mod 的本地状态记录，使用完整名称作为识别键。
    /// </summary>
    [Serializable]
    public class ModState
    {
        /// <summary>
        /// Mod 的完整名称或唯一标识。
        /// </summary>
        public string fullName;

        /// <summary>
        /// 本地期望状态。
        /// </summary>
        public ModStatus status;
    }
}
