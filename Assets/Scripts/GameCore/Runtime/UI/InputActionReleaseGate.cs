using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 把“按键还没松开时，下一层 UI 不得继续消费同一输入”收成单一正式规则。
    /// 用它替代时间阈值、Reset() 和一帧延迟这类临时补丁。
    /// </summary>
    internal sealed class InputActionReleaseGate
    {
        private readonly Dictionary<Guid, InputAction> m_blockedActions = new();
        private readonly List<Guid> m_staleActionIds = new();

        public bool HasBlockedActions
        {
            get
            {
                ReleaseActionsThatAreNoLongerPressed();
                return m_blockedActions.Count > 0;
            }
        }

        public void Clear()
        {
            m_blockedActions.Clear();
        }

        public void ArmIfPressed(params InputAction[] actions)
        {
            foreach (InputAction action in actions)
            {
                ArmIfPressed(action);
            }
        }

        public void ArmIfPressed(InputAction action)
        {
            if (action != null && action.IsPressed())
            {
                m_blockedActions[action.id] = action;
            }
        }

        public bool IsBlocked(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (!m_blockedActions.ContainsKey(action.id))
            {
                return false;
            }

            if (action.IsPressed())
            {
                return true;
            }

            // Unity 进入 PlayMode、切 action map 或 UI 层级变化时可能错过 released 回调。
            // 这里用当前按压状态兜底清理，避免防穿透门禁永久吞掉后续正式输入。
            m_blockedActions.Remove(action.id);
            return false;
        }

        public void NotifyReleased(InputAction action)
        {
            if (action != null)
            {
                m_blockedActions.Remove(action.id);
            }
        }

        private void ReleaseActionsThatAreNoLongerPressed()
        {
            m_staleActionIds.Clear();

            foreach (KeyValuePair<Guid, InputAction> pair in m_blockedActions)
            {
                if (pair.Value == null || !pair.Value.IsPressed())
                {
                    m_staleActionIds.Add(pair.Key);
                }
            }

            foreach (Guid actionId in m_staleActionIds)
            {
                m_blockedActions.Remove(actionId);
            }
        }
    }
}
