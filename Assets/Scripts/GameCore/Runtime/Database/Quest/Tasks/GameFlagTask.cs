using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using azixMcAze.SerializableDictionary;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 游戏标记任务的进度存档块；实际完成度在读档后通过当前 GameFlagSystem 重新计算。
    /// </summary>
    [Serializable]
    public class GameFlagTaskProgressDataBlock : QuestTaskProgressDataBlock
    {
        public override IQuestTaskProgress CreateInstance() => new GameFlagTaskProgress(this);
    }

    /// <summary>
    /// 监听游戏标记变化并统计已满足条件数量的任务进度。
    /// </summary>
    public class GameFlagTaskProgress : QuestTaskProgress<GameFlagTaskProgressDataBlock>
    {
        private GameFlagTask m_gameFlagTask => (GameFlagTask)m_task;

        public GameFlagTaskProgress(GameFlagTask task) : base(task) { }

        public GameFlagTaskProgress(GameFlagTaskProgressDataBlock block) : base(block) { }

        protected override void OnProgressTrackingStarted()
        {
            EventKit.Type.Register<GameFlagChangedEvent>(OnGameFlagChanged);
        }

        protected override void OnProgressTrackingStopped()
        {
            EventKit.Type.UnRegister<GameFlagChangedEvent>(OnGameFlagChanged);
        }

        /// <summary>
        /// 返回当前已满足的游戏标记条件数量。
        /// </summary>
        public int CountCompleted()
        {
            int count = 0;

            foreach (var flag in m_gameFlagTask.GetRequiredFlags())
            {
                if (GameManager.GameFlagSystem.Get(flag.Key) == flag.Value)
                {
                    count++;
                }
            }

            return count;
        }

        public override bool IsCompleted()
        {
            return CountCompleted() == m_gameFlagTask.requiredFlagCount;
        }

        private void OnGameFlagChanged(GameFlagChangedEvent gameFlagChangedEvent) => UpdateProgression();
    }

    /// <summary>
    /// 要求一组游戏标记达到指定布尔值的任务资产。
    /// </summary>
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Quests_Tasks + nameof(GameFlagTask))]
    public class GameFlagTask : QuestTask
    {
        [InspectorName("目标游戏标记")]
        [Tooltip("任务完成所需的游戏标记和值；所有条目都满足时任务完成。")]
        [SerializeField] private SerializableDictionary<string, bool> m_gameFlags = new();

        public GameFlagTask()
        {
            m_title = "{0}/{1} conditions are met";
        }

        /// <summary>
        /// 返回任务要求的游戏标记快照，避免外部直接修改资产内的字典。
        /// </summary>
        public KeyValuePair<string, bool>[] GetRequiredFlags() => m_gameFlags != null ? m_gameFlags.ToArray() : Array.Empty<KeyValuePair<string, bool>>();
        public int requiredFlagCount => m_gameFlags?.Count ?? 0;

        public override IQuestTaskProgress CreateTaskProgress() => new GameFlagTaskProgress(this);

        public override string GetCompletedTitle()
        {
            return StringFormatter.Format(m_title, requiredFlagCount, requiredFlagCount);
        }

        public override string GetInProgressTitle(IQuestTaskProgress progress)
        {
            return StringFormatter.Format(m_title, (progress as GameFlagTaskProgress).CountCompleted(), requiredFlagCount);
        }
    }
}

