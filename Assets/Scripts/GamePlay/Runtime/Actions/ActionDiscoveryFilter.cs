using System;
using System.Collections.Generic;
using Gameplay.Content;

namespace Gameplay.Actions
{
    /// <summary>
    /// 根据当前局内发现状态过滤可提供给牌桌、节点或后续 UI 的行动作者源。
    /// 该入口不负责研究随机、蓝图生成、列表 UI 或行动执行。
    /// </summary>
    public static class ActionDiscoveryFilter
    {
        /// <summary>
        /// 返回已被发现的行动集合，顺序保持调用方传入的当前可用行动顺序。
        /// 调用方仍然负责提供当前位置 / 世界规则下可用的行动候选，本方法只处理发现门槛。
        /// </summary>
        public static ActionDefinition[] FilterDiscoveredActions(
            IReadOnlyList<ActionDefinition> availableActions,
            ContentDiscoveryState discoveryState)
        {
            if (availableActions == null)
            {
                throw new ArgumentNullException(nameof(availableActions));
            }

            if (discoveryState == null)
            {
                throw new ArgumentNullException(nameof(discoveryState));
            }

            var discovered = new List<ActionDefinition>();
            for (int i = 0; i < availableActions.Count; i++)
            {
                ActionDefinition action = availableActions[i];
                if (action == null)
                {
                    throw new InvalidOperationException("可用行动集合包含空行动作者源。");
                }

                if (!action.ContentId.IsValid)
                {
                    throw new InvalidOperationException($"可用行动集合包含无效行动内容 ID：{action.ContentId}。");
                }

                if (discoveryState.IsDiscovered(action.ContentId))
                {
                    discovered.Add(action);
                }
            }

            return discovered.ToArray();
        }
    }
}
