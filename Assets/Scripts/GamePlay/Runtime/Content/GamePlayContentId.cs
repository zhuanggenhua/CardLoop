using System;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// GamePlay 内容唯一身份。它只包一段作者维护的字符串，不派生 Unity GUID 或资源地址。
    /// </summary>
    [Serializable]
    public struct GamePlayContentId : IEquatable<GamePlayContentId>
    {
        [SerializeField, InspectorName("内容 ID"), Tooltip("GamePlay 内容的唯一作者 ID。不要填写 Unity GUID、YooAsset 地址、文件路径或运行时实例号。")]
        private string m_value;

        /// <summary>
        /// 内容 ID 的原始作者值。比较使用区分大小写的序数规则，不进行大小写或 Unicode 归一化。
        /// </summary>
        public string Value => m_value ?? string.Empty;

        /// <summary>
        /// 当前值是否符合内容 ID 的基础字符约束；不代表它已经进入内容索引。
        /// </summary>
        public bool IsValid => GamePlayContentKeyUtility.IsValidKey(Value);

        /// <summary>
        /// 包装作者提供的内容 ID。构造过程不改写输入，完整唯一性在建立内容索引时校验。
        /// </summary>
        public GamePlayContentId(string value)
        {
            m_value = value;
        }

        public bool Equals(GamePlayContentId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GamePlayContentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static implicit operator string(GamePlayContentId id)
        {
            return id.Value;
        }

        public static implicit operator GamePlayContentId(string value)
        {
            return new GamePlayContentId(value);
        }
    }

    /// <summary>
    /// GamePlay 内容 ID 的基础格式规则。
    /// 这里只检查可持久化键的形状，不负责分配 ID、检查唯一性或解析 EX-GAS 标签。
    /// </summary>
    public static class GamePlayContentKeyUtility
    {
        /// <summary>
        /// 检查内容 ID 是否非空、没有首尾空白，且只包含字母、数字、点、下划线或连字符。
        /// 字母和数字允许 Unicode，以支持 Mod 作者使用本地语言命名空间。
        /// </summary>
        public static bool IsValidKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}

