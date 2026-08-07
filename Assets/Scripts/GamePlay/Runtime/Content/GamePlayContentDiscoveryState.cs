using System;
using System.Collections.Generic;

namespace GamePlay
{
    /// <summary>
    /// 当前局内已经被玩家发现的内容身份集合。
    /// 它只记录唯一内容 ID，不保存配方副本、资源地址、UI 可见性或存档格式。
    /// </summary>
    public sealed class GamePlayContentDiscoveryState
    {
        private readonly HashSet<GamePlayContentId> m_discoveredContentIds = new();

        /// <summary>当前已发现的内容数量。</summary>
        public int Count => m_discoveredContentIds.Count;

        /// <summary>
        /// 查询指定内容是否已被当前局内发现。无效 ID 不会匹配成功。
        /// </summary>
        public bool IsDiscovered(GamePlayContentId contentId)
        {
            return contentId.IsValid && m_discoveredContentIds.Contains(contentId);
        }

        /// <summary>
        /// 把已进入当前内容索引的内容标记为发现。
        /// 未加载、无效或缺失的内容 ID 是作者源 / 存档输入问题，直接报错，不静默生成占位发现记录。
        /// </summary>
        public bool MarkDiscovered(GamePlayContentId contentId, GamePlayContentIndex contentIndex)
        {
            if (contentIndex == null)
            {
                throw new ArgumentNullException(nameof(contentIndex));
            }

            if (!contentId.IsValid)
            {
                throw new InvalidOperationException($"不能发现无效 GamePlay 内容 ID：{contentId}。");
            }

            if (!contentIndex.TryGet(contentId, out _))
            {
                throw new InvalidOperationException($"不能发现当前内容索引中不存在的 GamePlay 内容：{contentId}。");
            }

            return m_discoveredContentIds.Add(contentId);
        }

        /// <summary>清空当前局内发现集合；新局或重新加载快照时由正式流程 owner 调用。</summary>
        public void Clear()
        {
            m_discoveredContentIds.Clear();
        }
    }
}
