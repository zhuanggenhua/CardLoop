using System;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Quests
{
    /// <summary>
    /// 任务内部子项的作者声明。
    /// 子项是 Quest 的组成部分，不是桌面行动作业，也不是战斗 Task。
    /// </summary>
    [Serializable]
    public abstract class QuestTaskDefinition
    {
    }

    /// <summary>要求指定普通牌桌行动成功完成一定次数。</summary>
    [Serializable]
    public sealed class ActionCompletionQuestTaskDefinition : QuestTaskDefinition
    {
        [SerializeField, InspectorName("行动内容 ID"), Tooltip("需要成功完成的具体行动唯一内容 ID；它不是行动类型枚举，也不替代行动自身的 EX-GAS 标签。")]
        private ContentId m_actionId;

        [SerializeField, Min(1), InspectorName("完成次数"), Tooltip("本任务子项需要累计的成功行动次数。必须大于 0。")]
        private int m_requiredCompletionCount = 1;

        /// <summary>需要统计的具体行动唯一内容 ID。</summary>
        public ContentId ActionId => m_actionId;

        /// <summary>本子项完成前需要收到的成功行动次数。</summary>
        public int RequiredCompletionCount => m_requiredCompletionCount;
    }
}
