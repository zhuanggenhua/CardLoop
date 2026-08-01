namespace FantasyWord.GameCore
{
    public abstract partial class CharacterBase
    {
        /// <summary>
        /// 角色属性启动快照缓冲。
        /// 这里只服务 CharacterBase 自己在启动窗口里的 bootstrap 临时快照，
        /// 不再承担 Awake 结束后的正式 ASC 长期镜像真相。
        /// </summary>
        private sealed class AttributeBootstrapBuffer
        {
            private Stats m_baseStats = new();
            private Stats m_currentStats = new();

            public void Clear()
            {
                m_baseStats = new Stats();
                m_currentStats = new Stats();
            }

            public void ReplaceBaseStats(Stats stats)
            {
                Stats nextBaseStats = stats?.Clone() ?? new Stats();
                Stats difference = nextBaseStats - m_baseStats;
                m_baseStats = nextBaseStats;
                m_currentStats += difference;
            }

            public int GetBaseStat(EStat stat) => m_baseStats?[stat] ?? 0;

            public int GetCurrentStat(EStat stat) => m_currentStats?[stat] ?? 0;

            public Stats CreateBaseStatsSnapshot() => m_baseStats?.Clone() ?? new Stats();

            public Stats CreateCurrentStatsSnapshot() => m_currentStats?.Clone() ?? new Stats();

            /// <summary>
            /// 只在启动窗口仍允许 bootstrap 读取时，临时把 ASC 快照回填进旧缓冲。
            /// 这里统一接收一整份基础/当前快照，避免 CharacterBase 外层再散落逐项同步顺序。
            /// </summary>
            public void MirrorFromFormalSnapshots(Stats baseStats, Stats currentStats)
            {
                Stats safeBaseStats = baseStats?.Clone() ?? new Stats();
                Stats safeCurrentStats = currentStats?.Clone() ?? safeBaseStats.Clone();
                if (AreStatsEqual(m_baseStats, safeBaseStats) && AreStatsEqual(m_currentStats, safeCurrentStats))
                {
                    return;
                }

                m_baseStats = safeBaseStats;
                m_currentStats = safeCurrentStats;
            }
        }
    }
}
