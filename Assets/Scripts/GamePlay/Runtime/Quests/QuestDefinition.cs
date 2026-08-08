using System;
using System.Collections.Generic;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Quests
{
    /// <summary>
    /// 玩家可见任务的作者源。
    /// 任务是主线、教程、危机和秘密目标的父级；具体进度由内部任务子项解释，不再把子目标提升成顶级系统。
    /// </summary>
    [CreateAssetMenu(fileName = "任务_", menuName = "Gameplay/内容/任务")]
    public sealed class QuestDefinition : ContentAsset
    {
        [SerializeField, InspectorName("前置任务 ID"), Tooltip("本任务激活前必须全部完成的任务。只维护这一份单向关系，不再额外维护完成后解锁列表。")]
        private ContentId[] m_prerequisiteQuestIds = Array.Empty<ContentId>();

        [SerializeReference, InspectorName("任务子项"), Tooltip("任务内部需要完成的子项或步骤。子项只声明作者数据，运行进度由 QuestSystem 创建并持有。")]
        private QuestTaskDefinition[] m_tasks = Array.Empty<QuestTaskDefinition>();

        /// <summary>激活本任务前必须全部完成的任务唯一内容 ID。</summary>
        public IReadOnlyList<ContentId> PrerequisiteQuestIds =>
            m_prerequisiteQuestIds ?? Array.Empty<ContentId>();

        /// <summary>任务内部的作者子项。当前子项顺序就是最小顺序，不另建 Step 层。</summary>
        public IReadOnlyList<QuestTaskDefinition> Tasks =>
            m_tasks ?? Array.Empty<QuestTaskDefinition>();
    }
}
