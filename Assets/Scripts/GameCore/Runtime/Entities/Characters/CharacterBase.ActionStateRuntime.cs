using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        /// <summary>
        /// 角色动作状态内部容器。
        /// 这里只服务 CharacterBase 自己的动作启用位、动作锁和移动速度修饰，
        /// 不对外升格成独立角色宿主或第二套动作系统。
        /// </summary>
        private sealed class CharacterActionStateRuntime
        {
            private EActionFlags m_actionFlags = EActionFlags.All;
            private readonly Dictionary<string, EActionFlags> m_lockedActions = new();
            private readonly Dictionary<CharacterAbilitySourceKey, CharacterActionLockRuntimeEntry> m_alterationRuleActionLocks = new();
            private readonly Dictionary<string, float> m_moveSpeedFactors = new();

            private readonly struct CharacterActionLockRuntimeEntry
            {
                public CharacterActionLockRuntimeEntry(EActionFlags actions, int stackCount)
                {
                    Actions = actions;
                    StackCount = stackCount;
                }

                public EActionFlags Actions { get; }
                public int StackCount { get; }
            }

            public float[] CreateMoveSpeedFactorSnapshot()
            {
                return m_moveSpeedFactors.Values.ToArray();
            }

            public string ApplyMoveSpeedFactor(float factor)
            {
                string key = Guid.NewGuid().ToString();
                m_moveSpeedFactors[key] = factor;
                return key;
            }

            public void UpdateMoveSpeedFactor(string key, float factor)
            {
                if (!m_moveSpeedFactors.ContainsKey(key))
                {
                    throw new InvalidOperationException("Invalid key, no move speed factor is applied with this key.");
                }

                m_moveSpeedFactors[key] = factor;
            }

            public void RemoveMoveSpeedFactor(string key)
            {
                if (!m_moveSpeedFactors.ContainsKey(key))
                {
                    throw new InvalidOperationException("Invalid key, no move speed factor is applied with this key.");
                }

                m_moveSpeedFactors.Remove(key);
            }

            public string LockActions(EActionFlags actions)
            {
                string key = Guid.NewGuid().ToString();
                m_lockedActions[key] = actions;
                return key;
            }

            public void UnlockActions(string key)
            {
                if (!m_lockedActions.ContainsKey(key))
                {
                    throw new InvalidOperationException("Invalid key, no actions are locked with this key.");
                }

                m_lockedActions.Remove(key);
            }

            public void ApplyAlterationRuleActionLock(CharacterAbilitySourceKey source, EActionFlags actions)
            {
                if (actions == EActionFlags.None)
                {
                    return;
                }

                m_alterationRuleActionLocks.TryGetValue(source, out CharacterActionLockRuntimeEntry currentEntry);
                EActionFlags nextActions = currentEntry.Actions | actions;
                int nextStackCount = currentEntry.StackCount + 1;
                m_alterationRuleActionLocks[source] = new CharacterActionLockRuntimeEntry(nextActions, nextStackCount);
            }

            public void RemoveAlterationRuleActionLockStack(CharacterAbilitySourceKey source)
            {
                if (!m_alterationRuleActionLocks.TryGetValue(source, out CharacterActionLockRuntimeEntry currentEntry))
                {
                    return;
                }

                int nextStackCount = currentEntry.StackCount - 1;
                if (nextStackCount <= 0)
                {
                    m_alterationRuleActionLocks.Remove(source);
                    return;
                }

                m_alterationRuleActionLocks[source] = new CharacterActionLockRuntimeEntry(currentEntry.Actions, nextStackCount);
            }

            public void RemoveAllAlterationRuleActionLocks(CharacterAbilitySourceKey source)
            {
                m_alterationRuleActionLocks.Remove(source);
            }

            public void ClearAlterationRuleActionLocks()
            {
                m_alterationRuleActionLocks.Clear();
            }

            public bool IsActionLocked(EActionFlags actions)
            {
                foreach (EActionFlags lockedActions in m_lockedActions.Values)
                {
                    if (lockedActions.HasFlag(actions))
                    {
                        return true;
                    }
                }

                foreach (CharacterActionLockRuntimeEntry entry in m_alterationRuleActionLocks.Values)
                {
                    if (entry.Actions.HasFlag(actions))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void EnableActions(EActionFlags actions)
            {
                m_actionFlags |= actions;
            }

            public void DisableActions(EActionFlags actions)
            {
                m_actionFlags &= ~actions;
            }

            public bool Can(EActionFlags actions)
            {
                return m_actionFlags.HasFlag(actions) && !IsActionLocked(actions);
            }

        }
    }
}
