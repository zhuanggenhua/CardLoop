using System;
using System.Collections.Generic;
using System.Linq;
using GAS.Runtime;
using UnityEngine;
using UnityEngine.Events;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        private readonly UnityEvent<CharacterBase> m_provoked = new();
        private readonly UnityEvent<CharacterAttributeValueChange> m_attributeBaseValueChanged = new();
        private readonly UnityEvent<CharacterAttributeValueChange> m_attributeCurrentValueChanged = new();
        private readonly UnityEvent<int> m_levelUpped = new();
        private readonly Dictionary<CharacterAbilitySourceKey, CharacterAlignmentOverrideRuntimeEntry> m_alterationAlignmentOverrides = new();
        private readonly Dictionary<CharacterAbilitySourceKey, int> m_alterationPlayerControlLocks = new();
        private readonly Dictionary<CharacterAbilitySourceKey, int> m_alterationAIControlOverrides = new();

        private readonly struct CharacterAlignmentOverrideRuntimeEntry
        {
            public CharacterAlignmentOverrideRuntimeEntry(EAlignment alignment, int priority, int stackCount)
            {
                Alignment = alignment;
                Priority = priority;
                StackCount = stackCount;
            }

            public EAlignment Alignment { get; }
            public int Priority { get; }
            public int StackCount { get; }
        }

        public void AddProvokedListener(UnityAction<CharacterBase> listener)
        {
            m_provoked.AddListener(listener);
        }

        public void RemoveProvokedListener(UnityAction<CharacterBase> listener)
        {
            m_provoked.RemoveListener(listener);
        }

        public void AddAttributeBaseValueChangedListener(UnityAction<CharacterAttributeValueChange> listener)
        {
            m_attributeBaseValueChanged.AddListener(listener);
        }

        public void RemoveAttributeBaseValueChangedListener(UnityAction<CharacterAttributeValueChange> listener)
        {
            m_attributeBaseValueChanged.RemoveListener(listener);
        }

        public void AddAttributeCurrentValueChangedListener(UnityAction<CharacterAttributeValueChange> listener)
        {
            m_attributeCurrentValueChanged.AddListener(listener);
        }

        public void RemoveAttributeCurrentValueChangedListener(UnityAction<CharacterAttributeValueChange> listener)
        {
            m_attributeCurrentValueChanged.RemoveListener(listener);
        }

        public void AddLevelUppedListener(UnityAction<int> listener)
        {
            m_levelUpped.AddListener(listener);
        }

        public void RemoveLevelUppedListener(UnityAction<int> listener)
        {
            m_levelUpped.RemoveListener(listener);
        }

        public string ApplyMoveSpeedFactor(float factor)
        {
            return m_actionRuntime.ApplyMoveSpeedFactor(factor);
        }

        public void UpdateMoveSpeedFactor(string key, float factor)
        {
            try
            {
                m_actionRuntime.UpdateMoveSpeedFactor(key, factor);
            }
            catch (InvalidOperationException exception)
            {
                Debug.Assert(false, exception.Message);
            }
        }

        public void RemoveMoveSpeedFactor(string key)
        {
            try
            {
                m_actionRuntime.RemoveMoveSpeedFactor(key);
            }
            catch (InvalidOperationException exception)
            {
                Debug.Assert(false, exception.Message);
            }
        }

        public string LockActions(EActionFlags actions)
        {
            return m_actionRuntime.LockActions(actions);
        }

        public void ApplyAlterationActionLockRule(CharacterAbilitySourceKey source, EActionFlags actions)
        {
            m_actionRuntime.ApplyAlterationRuleActionLock(source, actions);
        }

        public void RemoveAlterationActionLockRuleStack(CharacterAbilitySourceKey source)
        {
            m_actionRuntime.RemoveAlterationRuleActionLockStack(source);
        }

        public void RemoveAllAlterationActionLockRules(CharacterAbilitySourceKey source)
        {
            m_actionRuntime.RemoveAllAlterationRuleActionLocks(source);
        }

        internal void ClearAlterationActionLockRules()
        {
            m_actionRuntime.ClearAlterationRuleActionLocks();
        }

        public void ApplyAlterationPlayerControlLockRule(CharacterAbilitySourceKey source)
        {
            m_alterationPlayerControlLocks.TryGetValue(source, out int currentStackCount);
            m_alterationPlayerControlLocks[source] = currentStackCount + 1;
        }

        public void RemoveAlterationPlayerControlLockRuleStack(CharacterAbilitySourceKey source)
        {
            if (!m_alterationPlayerControlLocks.TryGetValue(source, out int currentStackCount))
            {
                return;
            }

            int nextStackCount = currentStackCount - 1;
            if (nextStackCount <= 0)
            {
                m_alterationPlayerControlLocks.Remove(source);
                return;
            }

            m_alterationPlayerControlLocks[source] = nextStackCount;
        }

        public void RemoveAllAlterationPlayerControlLockRules(CharacterAbilitySourceKey source)
        {
            m_alterationPlayerControlLocks.Remove(source);
        }

        internal void ClearAlterationPlayerControlLockRules()
        {
            m_alterationPlayerControlLocks.Clear();
        }

        public void ApplyAlterationAIControlRule(CharacterAbilitySourceKey source)
        {
            m_alterationAIControlOverrides.TryGetValue(source, out int currentStackCount);
            m_alterationAIControlOverrides[source] = currentStackCount + 1;
            RefreshAlterationControllerOverride();
        }

        public void RemoveAlterationAIControlRuleStack(CharacterAbilitySourceKey source)
        {
            if (!m_alterationAIControlOverrides.TryGetValue(source, out int currentStackCount))
            {
                return;
            }

            int nextStackCount = currentStackCount - 1;
            if (nextStackCount <= 0)
            {
                m_alterationAIControlOverrides.Remove(source);
            }
            else
            {
                m_alterationAIControlOverrides[source] = nextStackCount;
            }

            RefreshAlterationControllerOverride();
        }

        public void RemoveAllAlterationAIControlRules(CharacterAbilitySourceKey source)
        {
            m_alterationAIControlOverrides.Remove(source);
            RefreshAlterationControllerOverride();
        }

        internal void ClearAlterationAIControlRules()
        {
            m_alterationAIControlOverrides.Clear();
            RefreshAlterationControllerOverride();
        }

        public bool CanBePlayerControlled()
        {
            return !dead && !HasAlterationPlayerControlLock();
        }

        private bool HasAlterationPlayerControlLock()
        {
            foreach (int stackCount in m_alterationPlayerControlLocks.Values)
            {
                if (stackCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAlterationAIControlOverride()
        {
            foreach (int stackCount in m_alterationAIControlOverrides.Values)
            {
                if (stackCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAlterationControllerOverride()
        {
            if (HasAlterationAIControlOverride())
            {
                TryActivateController<AIController>();
                return;
            }

            ClearControllerOverride<AIController>();
        }

        public void ApplyAlterationAlignmentRule(CharacterAbilitySourceKey source, EAlignment alignment, int priority)
        {
            m_alterationAlignmentOverrides.TryGetValue(source, out CharacterAlignmentOverrideRuntimeEntry currentEntry);
            m_alterationAlignmentOverrides[source] = new CharacterAlignmentOverrideRuntimeEntry(
                alignment,
                priority,
                currentEntry.StackCount + 1);
        }

        public void RemoveAlterationAlignmentRuleStack(CharacterAbilitySourceKey source)
        {
            if (!m_alterationAlignmentOverrides.TryGetValue(source, out CharacterAlignmentOverrideRuntimeEntry currentEntry))
            {
                return;
            }

            int nextStackCount = currentEntry.StackCount - 1;
            if (nextStackCount <= 0)
            {
                m_alterationAlignmentOverrides.Remove(source);
                return;
            }

            m_alterationAlignmentOverrides[source] = new CharacterAlignmentOverrideRuntimeEntry(
                currentEntry.Alignment,
                currentEntry.Priority,
                nextStackCount);
        }

        public void RemoveAllAlterationAlignmentRules(CharacterAbilitySourceKey source)
        {
            m_alterationAlignmentOverrides.Remove(source);
        }

        internal void ClearAlterationAlignmentRules()
        {
            m_alterationAlignmentOverrides.Clear();
        }

        private bool TryResolveAlterationAlignmentOverride(out EAlignment alignment)
        {
            alignment = default;
            bool hasResolvedAlignment = false;
            CharacterAbilitySourceKey resolvedSource = default;
            int resolvedPriority = int.MinValue;

            foreach ((CharacterAbilitySourceKey source, CharacterAlignmentOverrideRuntimeEntry entry) in m_alterationAlignmentOverrides)
            {
                if (entry.StackCount <= 0)
                {
                    continue;
                }

                if (!hasResolvedAlignment ||
                    entry.Priority > resolvedPriority ||
                    (entry.Priority == resolvedPriority && CompareAlignmentOverrideSource(source, resolvedSource) < 0))
                {
                    alignment = entry.Alignment;
                    resolvedPriority = entry.Priority;
                    resolvedSource = source;
                    hasResolvedAlignment = true;
                }
            }

            return hasResolvedAlignment;
        }

        private static int CompareAlignmentOverrideSource(CharacterAbilitySourceKey a, CharacterAbilitySourceKey b)
        {
            int kindComparison = ((int)a.Kind).CompareTo((int)b.Kind);
            return kindComparison != 0
                ? kindComparison
                : string.Compare(a.SourceId, b.SourceId, StringComparison.Ordinal);
        }

        public void UnlockActions(string key)
        {
            try
            {
                m_actionRuntime.UnlockActions(key);
            }
            catch (InvalidOperationException exception)
            {
                Debug.Assert(false, exception.Message);
            }
        }

        public bool IsActionLocked(EActionFlags actions)
        {
            return m_actionRuntime.IsActionLocked(actions) || HasFormalActionLock(actions);
        }

        public void EnableActions(EActionFlags actions)
        {
            m_actionRuntime.EnableActions(actions);
        }

        public void DisableActions(EActionFlags actions)
        {
            m_actionRuntime.DisableActions(actions);
        }

        public bool Can(EActionFlags actions)
        {
            return m_actionRuntime.Can(actions) && !HasFormalActionLock(actions);
        }

        private bool HasFormalActionLock(EActionFlags actions)
        {
            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return false;
            }

            if ((actions.HasFlag(EActionFlags.Move) ||
                 actions.HasFlag(EActionFlags.UseAbility) ||
                 actions.HasFlag(EActionFlags.UpdateTargetDirection)) &&
                HasFormalGameplayTag(abilitySystemComponent, FormalGameplayTagCatalog.AttackingEvent))
            {
                return true;
            }

            // 控制效果的禁用语义现在优先看 formal GameplayTag，而不是再让执行壳长期镜像一份动作锁。
            if (actions.HasFlag(EActionFlags.Move) &&
                (HasFormalGameplayTag(abilitySystemComponent, FormalGameplayTagCatalog.StunControlEffect) ||
                 HasFormalGameplayTag(abilitySystemComponent, FormalGameplayTagCatalog.RootControlEffect)))
            {
                return true;
            }

            if (actions.HasFlag(EActionFlags.UseAbility) &&
                (HasFormalGameplayTag(abilitySystemComponent, FormalGameplayTagCatalog.StunControlEffect) ||
                 HasFormalGameplayTag(abilitySystemComponent, FormalGameplayTagCatalog.SilenceControlEffect)))
            {
                return true;
            }

            if (actions.HasFlag(EActionFlags.UpdateTargetDirection) &&
                HasFormalGameplayTag(abilitySystemComponent, FormalGameplayTagCatalog.StunControlEffect))
            {
                return true;
            }

            return false;
        }

        private static bool HasFormalGameplayTag(
            AbilitySystemComponent abilitySystemComponent,
            FormalGameplayTagDefinition tagDefinition)
        {
            return abilitySystemComponent != null &&
                   tagDefinition.TagCode > 0 &&
                   abilitySystemComponent.HasTag(tagDefinition.TagCode);
        }

        public void FlagAsSummoned()
        {
            m_isSummoned = true;
        }

        public void SetAlignmentOverride(EAlignment? alignment)
        {
            m_alignmentOverride = alignment;
        }

    }
}
