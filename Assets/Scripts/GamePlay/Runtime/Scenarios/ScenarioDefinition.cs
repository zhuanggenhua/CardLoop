using System;
using System.Collections.Generic;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Scenarios
{
    /// <summary>
    /// 声明一个可进入剧本当前已经确定的子模块组合。
    /// 现阶段只组合正式任务定义；地图、事件、世界规则和初始内容必须等各自职责成立后再加入。
    /// </summary>
    [CreateAssetMenu(fileName = "剧本_", menuName = "Gameplay/内容/剧本")]
    public class ScenarioDefinition : ContentAsset
    {
        [SerializeField, InspectorName("剧本任务 ID"), Tooltip("进入本剧本时由剧本导演统一交给任务系统的任务定义。前置任务也必须包含在同一列表中。允许为空，表示本剧本当前不配置正式任务。")]
        private ContentId[] m_questIds = Array.Empty<ContentId>();

        /// <summary>本剧本开始时交给任务子模块的全部任务唯一内容 ID。</summary>
        public IReadOnlyList<ContentId> QuestIds =>
            m_questIds ?? Array.Empty<ContentId>();
    }
}
