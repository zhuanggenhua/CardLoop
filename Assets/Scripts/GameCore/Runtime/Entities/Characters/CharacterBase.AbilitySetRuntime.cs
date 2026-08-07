using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色能力集合运行时容器。
    /// 当前由 CharacterAbilitySet 持有，CharacterBase 不再保留第二套能力实例仓库。
    /// </summary>
    internal sealed class CharacterAbilitySetRuntime
    {
        private readonly HashSet<RuntimeAbilityKey> m_unlockedAbilities = new();
        private readonly Dictionary<RuntimeAbilityKey, Dictionary<CharacterAbilitySourceKey, int>> m_bonusAbilitySources = new();
        private readonly Dictionary<RuntimeAbilityKey, Dictionary<CharacterAbilitySourceKey, int>> m_suppressedAbilitySources = new();
        private readonly Dictionary<RuntimeAbilityKey, AbilityBase> m_instances = new();

        public bool TryAddUnlockedFormalGasAbility(
            int formalGasAbilityCode,
            Func<int, AbilityBase> createInstance,
            Action<int> onAdded = null)
        {
            if (formalGasAbilityCode <= 0 || createInstance == null)
            {
                return false;
            }

            RuntimeAbilityKey key = RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode);
            bool requiresNewInstance = !m_instances.ContainsKey(key);
            bool wasAdded = m_unlockedAbilities.Add(key);

            if (wasAdded && requiresNewInstance)
            {
                AbilityBase instance = createInstance(formalGasAbilityCode);
                if (instance == null)
                {
                    m_unlockedAbilities.Remove(key);
                    return false;
                }

                m_instances[key] = instance;
                onAdded?.Invoke(formalGasAbilityCode);
            }

            return wasAdded;
        }

        public bool TryRegisterBonusFormalGasAbility(
            int formalGasAbilityCode,
            CharacterAbilitySourceKey source,
            Func<int, AbilityBase> createInstance,
            int count = 1,
            Action<int> onAdded = null)
        {
            if (formalGasAbilityCode <= 0 || count <= 0 || createInstance == null)
            {
                return false;
            }

            RuntimeAbilityKey key = RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode);
            bool requiresNewInstance = !m_instances.ContainsKey(key);

            if (!m_bonusAbilitySources.TryGetValue(key, out Dictionary<CharacterAbilitySourceKey, int> sources))
            {
                sources = new Dictionary<CharacterAbilitySourceKey, int>();
                m_bonusAbilitySources.Add(key, sources);
            }

            sources.TryGetValue(source, out int currentCount);
            sources[source] = currentCount + count;

            if (requiresNewInstance)
            {
                AbilityBase instance = createInstance(formalGasAbilityCode);
                if (instance == null)
                {
                    sources[source] = currentCount;
                    if (sources.Count == 1 && currentCount <= 0)
                    {
                        m_bonusAbilitySources.Remove(key);
                    }

                    return false;
                }

                m_instances[key] = instance;
                onAdded?.Invoke(formalGasAbilityCode);
            }

            return requiresNewInstance;
        }

        public bool TryUnregisterBonusFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 ||
                count <= 0 ||
                !m_bonusAbilitySources.TryGetValue(RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode), out Dictionary<CharacterAbilitySourceKey, int> sources) ||
                !sources.TryGetValue(source, out int currentCount))
            {
                return false;
            }

            RuntimeAbilityKey key = RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode);

            int nextCount = currentCount - count;
            if (nextCount > 0)
            {
                sources[source] = nextCount;
                return false;
            }

            sources.Remove(source);

            if (sources.Count > 0)
            {
                return false;
            }

            m_bonusAbilitySources.Remove(key);
            return !m_unlockedAbilities.Contains(key);
        }

        public CharacterAbilitySourceRuntimeEntry[] CreateBonusAbilitySourceEntrySnapshot(CharacterAbilitySourceKey source)
        {
            return m_bonusAbilitySources
                .SelectMany(abilityEntry => abilityEntry.Value
                    .Where(sourceEntry => sourceEntry.Key.Equals(source))
                    .Select(sourceEntry =>
                        CreateSourceRuntimeEntry(abilityEntry.Key, sourceEntry.Key, sourceEntry.Value)))
                .Where(entry => entry.StackCount > 0 && entry.HasFormalGasAbility)
                .ToArray();
        }

        public bool TryUnregisterBonusFormalGasAbility(
            int formalGasAbilityCode,
            CharacterAbilitySourceKey source,
            int count,
            Action<AbilityBase> releaseInstance,
            Action<int> onRemoved = null)
        {
            bool shouldRemoveInstance = TryUnregisterBonusFormalGasAbility(formalGasAbilityCode, source, count);
            RuntimeAbilityKey key = RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode);

            if (shouldRemoveInstance &&
                m_instances.TryGetValue(key, out AbilityBase instance))
            {
                m_instances.Remove(key);
                m_suppressedAbilitySources.Remove(key);

                releaseInstance?.Invoke(instance);
                onRemoved?.Invoke(formalGasAbilityCode);
            }

            return shouldRemoveInstance;
        }

        public bool TrySuppressFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 || count <= 0)
            {
                return false;
            }

            RuntimeAbilityKey key = RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode);
            bool wasSuppressed = IsAbilitySuppressed(key);

            if (!m_suppressedAbilitySources.TryGetValue(key, out Dictionary<CharacterAbilitySourceKey, int> sources))
            {
                sources = new Dictionary<CharacterAbilitySourceKey, int>();
                m_suppressedAbilitySources.Add(key, sources);
            }

            sources.TryGetValue(source, out int currentCount);
            sources[source] = currentCount + count;
            return !wasSuppressed;
        }

        public bool TryUnsuppressFormalGasAbility(int formalGasAbilityCode, CharacterAbilitySourceKey source, int count = 1)
        {
            if (formalGasAbilityCode <= 0 ||
                count <= 0 ||
                !m_suppressedAbilitySources.TryGetValue(RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode), out Dictionary<CharacterAbilitySourceKey, int> sources) ||
                !sources.TryGetValue(source, out int currentCount))
            {
                return false;
            }

            RuntimeAbilityKey key = RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode);

            int nextCount = currentCount - count;
            if (nextCount > 0)
            {
                sources[source] = nextCount;
                return false;
            }

            sources.Remove(source);

            if (sources.Count > 0)
            {
                return false;
            }

            m_suppressedAbilitySources.Remove(key);
            return true;
        }

        public bool IsFormalGasAbilitySuppressed(int formalGasAbilityCode)
        {
            return formalGasAbilityCode > 0 &&
                IsAbilitySuppressed(RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode));
        }

        public CharacterAbilitySourceRuntimeEntry[] CreateSuppressedAbilitySourceEntrySnapshot()
        {
            return m_suppressedAbilitySources
                .SelectMany(abilityEntry => abilityEntry.Value.Select(sourceEntry =>
                    CreateSourceRuntimeEntry(abilityEntry.Key, sourceEntry.Key, sourceEntry.Value)))
                .Where(entry => entry.StackCount > 0 && entry.HasFormalGasAbility)
                .ToArray();
        }

        public CharacterAbilitySourceRuntimeEntry[] CreateSuppressedAbilitySourceEntrySnapshot(CharacterAbilitySourceKey source)
        {
            return m_suppressedAbilitySources
                .SelectMany(abilityEntry => abilityEntry.Value
                    .Where(sourceEntry => sourceEntry.Key.Equals(source))
                    .Select(sourceEntry =>
                        CreateSourceRuntimeEntry(abilityEntry.Key, sourceEntry.Key, sourceEntry.Value)))
                .Where(entry => entry.StackCount > 0 && entry.HasFormalGasAbility)
                .ToArray();
        }

        public CharacterAbilitySourceRuntimeEntry[] CreateBonusAbilitySourceEntrySnapshot()
        {
            return m_bonusAbilitySources
                .SelectMany(abilityEntry => abilityEntry.Value.Select(sourceEntry =>
                    CreateSourceRuntimeEntry(abilityEntry.Key, sourceEntry.Key, sourceEntry.Value)))
                .Where(entry => entry.StackCount > 0 && entry.HasFormalGasAbility)
                .ToArray();
        }

        public void UpdateRuntime()
        {
            UpdateRuntime(Time.deltaTime);
        }

        public void UpdateRuntime(float deltaTime)
        {
            foreach (AbilityBase ability in m_instances.Values)
            {
                ability.UpdateCooldowns(deltaTime);
                ability.UpdateAnimationState();
            }
        }

        public void ResetInstances()
        {
            foreach (AbilityBase ability in m_instances.Values)
            {
                ability.Reset();
            }
        }

        public void InterruptInstances()
        {
            foreach (AbilityBase ability in m_instances.Values)
            {
                ability.Interrupt();
            }
        }

        /// <summary>
        /// 这里只通知显式声明了动作中断合同的能力实例，
        /// 不再让 CharacterBase 自己遍历实例集合并判断具体实现类型。
        /// </summary>
        public void NotifyActionInterrupted()
        {
            foreach (AbilityBase ability in m_instances.Values)
            {
                if (ability is IActionInterruptReceiver interruptReceiver)
                {
                    interruptReceiver.OnActionInterrupted();
                }
            }
        }

        public bool HasFormalGasAbility(int formalGasAbilityCode)
        {
            return formalGasAbilityCode > 0 &&
                m_instances.ContainsKey(RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode));
        }

        public bool TryGetFormalGasAbilityInstance(int formalGasAbilityCode, out AbilityBase instance)
        {
            instance = null;

            if (formalGasAbilityCode <= 0)
            {
                return false;
            }

            return m_instances.TryGetValue(RuntimeAbilityKey.FromFormalGasAbilityCode(formalGasAbilityCode), out instance) &&
                instance != null;
        }

        public KeyValuePair<int, AbilityBase>[] GetFormalGasAbilityInstanceEntriesSnapshot()
        {
            return m_instances
                .Where(entry => entry.Key.HasFormalGasAbility)
                .Select(entry => new KeyValuePair<int, AbilityBase>(entry.Key.FormalGasAbilityCode, entry.Value))
                .Where(entry => entry.Value != null)
                .ToArray();
        }

        public int[] GetFormalGasAbilityCodeSnapshots()
        {
            return m_instances.Keys
                .Where(key => key.HasFormalGasAbility)
                .Select(key => key.FormalGasAbilityCode)
                .Distinct()
                .ToArray();
        }

        private bool IsAbilitySuppressed(RuntimeAbilityKey key)
        {
            return m_suppressedAbilitySources.TryGetValue(key, out Dictionary<CharacterAbilitySourceKey, int> sources) &&
                sources.Values.Sum() > 0;
        }

        private CharacterAbilitySourceRuntimeEntry CreateSourceRuntimeEntry(
            RuntimeAbilityKey key,
            CharacterAbilitySourceKey source,
            int stackCount)
        {
            return new CharacterAbilitySourceRuntimeEntry(
                key.FormalGasAbilityCode,
                source,
                stackCount);
        }

        private readonly struct RuntimeAbilityKey : IEquatable<RuntimeAbilityKey>
        {
            private RuntimeAbilityKey(int formalGasAbilityCode)
            {
                FormalGasAbilityCode = Math.Max(0, formalGasAbilityCode);
            }

            public int FormalGasAbilityCode { get; }
            public bool HasFormalGasAbility => FormalGasAbilityCode > 0;

            public static RuntimeAbilityKey FromFormalGasAbilityCode(int formalGasAbilityCode)
            {
                return new RuntimeAbilityKey(formalGasAbilityCode);
            }

            public bool Equals(RuntimeAbilityKey other)
            {
                return FormalGasAbilityCode == other.FormalGasAbilityCode;
            }

            public override bool Equals(object obj)
            {
                return obj is RuntimeAbilityKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(nameof(RuntimeAbilityKey), FormalGasAbilityCode);
            }
        }
    }
}
