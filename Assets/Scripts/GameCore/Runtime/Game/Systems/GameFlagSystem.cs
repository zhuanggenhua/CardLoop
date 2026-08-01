using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 游戏标记系统的存档块，只保存当前为 true 的标记名集合。
    /// </summary>
    [Serializable]
    public class GameFlagsDataBlock : DataBlock
    {
        /// <summary>
        /// 当前已置为 true 的游戏标记名。
        /// </summary>
        public string[] flags;
    }

    /// <summary>
    /// 布尔游戏标记系统，缺省值为 false，设置变化时广播 GameFlagChangedEvent。
    /// </summary>
    public class GameFlagSystem : AGameSystem, IDataBlockHandler<GameFlagsDataBlock>
    {
        private HashSet<string> m_flags = new();

        /// <summary>
        /// 查询指定标记当前是否为 true。
        /// </summary>
        public bool Get(string variableName)
        {
            return m_flags.Contains(variableName);
        }

        /// <summary>
        /// 设置指定标记，并在值写入后通知运行时事件。
        /// </summary>
        public void Set(string variableName, bool value)
        {
            if (value)
            {
                m_flags.Add(variableName);
            }
            else
            {
                m_flags.Remove(variableName);
            }

            GameRuntimeEvents.NotifyGameFlagChanged(variableName, value);
        }

        public void LoadDataBlock(GameFlagsDataBlock block)
        {
            m_flags = block.flags.ToHashSet();
        }

        public GameFlagsDataBlock CreateDataBlock()
        {
            return new GameFlagsDataBlock
            {
                flags = m_flags.ToArray(),
            };
        }
    }
}

