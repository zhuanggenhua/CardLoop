using System;

namespace GameCore
{
    public interface IModValidator
    {
        bool ValidateMod(ModInfo modInfo);
    }

    /// <summary>
    /// 迁自 Chris 的 API 版本校验器。
    /// 当前只允许完全匹配，避免外部内容包在数据合同未稳定前静默加载。
    /// </summary>
    public sealed class APIValidator : IModValidator
    {
        private readonly Version m_apiVersion;

        public APIValidator(string apiVersion)
        {
            if (!Version.TryParse(apiVersion, out m_apiVersion))
            {
                throw new ArgumentException(
                    $"项目 Mod API 版本不是有效版本号：{apiVersion ?? "<null>"}。",
                    nameof(apiVersion));
            }
        }

        public bool ValidateMod(ModInfo modInfo)
        {
            return modInfo != null &&
                   Version.TryParse(modInfo.apiVersion, out Version modVersion) &&
                   modVersion.CompareTo(m_apiVersion) == 0;
        }
    }
}
