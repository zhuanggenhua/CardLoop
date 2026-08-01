using System;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// UIKit 菜单入口传给面板的最小打开上下文。
    /// 这里只承载打开参数，不承担任何玩法真相。
    /// </summary>
    public sealed class UIKitMenuOpenData : IUIData
    {
        public static readonly UIKitMenuOpenData Empty = new(Array.Empty<object>());

        private readonly object[] m_arguments;

        public UIKitMenuOpenData(params object[] arguments)
        {
            m_arguments = arguments != null ? (object[])arguments.Clone() : Array.Empty<object>();
        }

        public object[] CreateArgumentSnapshot() => (object[])m_arguments.Clone();

        public int ArgumentCount => m_arguments.Length;

        public bool TryGetArgument<T>(int index, out T value)
        {
            if (index >= 0 && index < m_arguments.Length && m_arguments[index] is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }
    }
}
