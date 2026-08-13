using System;
using System.Collections.Generic;
using GAS.Runtime;
using UnityEngine;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        [Header("GAS")]
        [Tooltip("实体级正式 AbilitySystemComponent。角色属性、标签、效果和能力只使用这一份 ASC。")]
        [SerializeField] private AbilitySystemComponent m_abilitySystemComponent;

        private bool m_isFormalAbilitySystemReady;
        private bool m_formalAttributeEventsRegistered;
        private readonly Dictionary<int, Action<float, float>> m_formalBaseValueChangedHandlers = new();
        private readonly Dictionary<int, Action<float, float>> m_formalCurrentValueChangedHandlers = new();

        public bool TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent)
        {
            if (!m_abilitySystemComponent)
            {
                m_abilitySystemComponent = GetComponent<AbilitySystemComponent>();
            }

            abilitySystemComponent = m_abilitySystemComponent;
            return abilitySystemComponent != null;
        }

        /// <summary>
        /// 用角色配置直接创建唯一 ASC 属性集；角色配置只覆盖 EX-GAS 表中的基础值。
        /// </summary>
        protected void InitializeFormalAbilitySystemFromCharacterSheet()
        {
            if (m_isFormalAbilitySystemReady)
            {
                return;
            }

            AbilitySystemComponent abilitySystemComponent = GetRequiredFormalAbilitySystem();
            if (abilitySystemComponent.Cell == null)
            {
                throw new InvalidOperationException(
                    $"角色 {name} 的 AbilitySystemComponent 尚未完成 Awake，不能初始化角色属性。");
            }

            AbilitySystemCellConfig config = new(
                baseTags: Array.Empty<int>(),
                attrSets: new[] { characterSheet.CreateAttributeSetConfig() },
                baseAbilities: Array.Empty<AbilityConfig>(),
                level: m_level);

            abilitySystemComponent.Init(config);
            m_isFormalAbilitySystemReady = true;
            SyncFormalAbilityRuleRosterFromRuntime();
        }

        private bool TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent)
        {
            abilitySystemComponent = null;
            return m_isFormalAbilitySystemReady &&
                TryGetFormalAbilitySystem(out abilitySystemComponent) &&
                abilitySystemComponent != null;
        }

        private AbilitySystemComponent GetRequiredFormalAbilitySystem()
        {
            if (TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) &&
                abilitySystemComponent != null)
            {
                return abilitySystemComponent;
            }

            throw new InvalidOperationException(
                $"角色 {name} 缺少正式 AbilitySystemComponent，无法访问 EX-GAS 角色状态。");
        }

        private AbilitySystemComponent GetRequiredInitializedFormalAbilitySystem()
        {
            if (TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent))
            {
                return abilitySystemComponent;
            }

            throw new InvalidOperationException(
                $"角色 {name} 的正式 ASC 尚未初始化，无法访问 EX-GAS 角色属性。");
        }

        private bool TryGetFormalBaseAttribute(int attributeCode, out float value)
        {
            value = 0.0f;
            if (!TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent))
            {
                return false;
            }

            value = abilitySystemComponent.GetAttrBaseValue(CharacterAttributes.SetCode, attributeCode);
            return true;
        }

        private bool TryGetFormalCurrentAttribute(int attributeCode, out float value)
        {
            value = 0.0f;
            if (!TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent))
            {
                return false;
            }

            value = abilitySystemComponent.GetAttrCurrentValue(CharacterAttributes.SetCode, attributeCode);
            return true;
        }

        internal CharacterAttributeSnapshot CaptureAttributeSnapshot()
        {
            AbilitySystemComponent abilitySystemComponent = GetRequiredInitializedFormalAbilitySystem();
            int[] attributeCodes = CharacterAttributes.GetKnownAttributeCodes();
            CharacterAttributeSnapshotEntry[] entries = new CharacterAttributeSnapshotEntry[attributeCodes.Length];
            for (int i = 0; i < attributeCodes.Length; i++)
            {
                int attributeCode = attributeCodes[i];
                entries[i] = new CharacterAttributeSnapshotEntry(
                    attributeCode,
                    abilitySystemComponent.GetAttrBaseValue(CharacterAttributes.SetCode, attributeCode));
            }

            return new CharacterAttributeSnapshot(entries);
        }

        internal void RestoreAttributeSnapshot(CharacterAttributeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            AbilitySystemComponent abilitySystemComponent = GetRequiredInitializedFormalAbilitySystem();
            HashSet<int> restoredCodes = new();
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                CharacterAttributeSnapshotEntry entry = snapshot.Entries[i];
                CharacterAttributes.RequireKnownAttributeCode(entry.AttributeCode);
                if (!restoredCodes.Add(entry.AttributeCode))
                {
                    throw new InvalidOperationException(
                        $"角色 {name} 的属性快照重复包含属性码 {entry.AttributeCode}。");
                }

                abilitySystemComponent.SetAttrBaseValue(
                    CharacterAttributes.SetCode,
                    entry.AttributeCode,
                    entry.BaseValue);
            }

            foreach (int attributeCode in restoredCodes)
            {
                AttributeHelper.RecalculateCurrentValue(
                    abilitySystemComponent.Cell.Entity,
                    CharacterAttributes.SetCode,
                    attributeCode);
            }
        }

        internal void SetAttributeBaseValueAndRecalculate(int attributeCode, float value)
        {
            CharacterAttributes.RequireKnownAttributeCode(attributeCode);
            AbilitySystemComponent abilitySystemComponent = GetRequiredInitializedFormalAbilitySystem();
            abilitySystemComponent.SetAttrBaseValue(CharacterAttributes.SetCode, attributeCode, value);
            AttributeHelper.RecalculateCurrentValue(
                abilitySystemComponent.Cell.Entity,
                CharacterAttributes.SetCode,
                attributeCode);
        }

        protected void RegisterFormalAttributeEvents()
        {
            if (m_formalAttributeEventsRegistered)
            {
                return;
            }

            AbilitySystemComponent abilitySystemComponent = GetRequiredInitializedFormalAbilitySystem();
            foreach (int attributeCode in CharacterAttributes.GetKnownAttributeCodes())
            {
                int capturedAttributeCode = attributeCode;
                Action<float, float> baseHandler = (oldValue, newValue) =>
                    OnFormalBaseValueChanged(capturedAttributeCode, oldValue, newValue);
                m_formalBaseValueChangedHandlers.Add(capturedAttributeCode, baseHandler);
                GASEventCenter.RegisterOnBaseValueChangeAfter(
                    abilitySystemComponent.Cell,
                    CharacterAttributes.SetCode,
                    capturedAttributeCode,
                    baseHandler);

                Action<float, float> currentHandler = (oldValue, newValue) =>
                    OnFormalCurrentValueChanged(capturedAttributeCode, oldValue, newValue);
                m_formalCurrentValueChangedHandlers.Add(capturedAttributeCode, currentHandler);
                GASEventCenter.RegisterOnAttrCurrentValueChangeAfter(
                    abilitySystemComponent.Cell,
                    CharacterAttributes.SetCode,
                    capturedAttributeCode,
                    currentHandler);
            }

            m_formalAttributeEventsRegistered = true;
        }

        protected void UnregisterFormalAttributeEvents()
        {
            if (!m_formalAttributeEventsRegistered)
            {
                return;
            }

            AbilitySystemComponent abilitySystemComponent = GetRequiredFormalAbilitySystem();
            foreach ((int attributeCode, Action<float, float> baseHandler) in m_formalBaseValueChangedHandlers)
            {
                GASEventCenter.UnRegisterOnBaseValueChangeAfter(
                    abilitySystemComponent.Cell,
                    CharacterAttributes.SetCode,
                    attributeCode,
                    baseHandler);
            }

            foreach ((int attributeCode, Action<float, float> currentHandler) in m_formalCurrentValueChangedHandlers)
            {
                GASEventCenter.UnRegisterOnAttrCurrentValueChangeAfter(
                    abilitySystemComponent.Cell,
                    CharacterAttributes.SetCode,
                    attributeCode,
                    currentHandler);
            }

            m_formalBaseValueChangedHandlers.Clear();
            m_formalCurrentValueChangedHandlers.Clear();
            m_formalAttributeEventsRegistered = false;
        }

        private void OnFormalBaseValueChanged(int attributeCode, float oldValue, float newValue)
        {
            m_attributeBaseValueChanged.Invoke(
                new CharacterAttributeValueChange(attributeCode, oldValue, newValue));
        }

        private void OnFormalCurrentValueChanged(int attributeCode, float oldValue, float newValue)
        {
            m_attributeCurrentValueChanged.Invoke(
                new CharacterAttributeValueChange(attributeCode, oldValue, newValue));

            if (attributeCode == CharacterAttributes.Health && oldValue > 0.0f && newValue <= 0.0f)
            {
                RequestDeathAfterFormalCurrentValueMutation();
            }
        }

        /// <summary>
        /// 按 EX-GAS 属性码读取角色基础值；属性身份和钳制规则均来自正式属性表。
        /// </summary>
        public float GetAttributeBaseValue(int attributeCode)
        {
            CharacterAttributes.RequireKnownAttributeCode(attributeCode);
            if (TryGetFormalBaseAttribute(attributeCode, out float value))
            {
                return value;
            }

            throw new InvalidOperationException(
                $"角色 {name} 的正式 ASC 尚未初始化，无法读取 EX-GAS 基础属性 {attributeCode}。");
        }

        /// <summary>
        /// 按 EX-GAS 属性码读取角色当前值，包含当前生效 GameplayEffect 的重算结果。
        /// </summary>
        public float GetAttributeCurrentValue(int attributeCode)
        {
            CharacterAttributes.RequireKnownAttributeCode(attributeCode);
            if (TryGetFormalCurrentAttribute(attributeCode, out float value))
            {
                return value;
            }

            throw new InvalidOperationException(
                $"角色 {name} 的正式 ASC 尚未初始化，无法读取 EX-GAS 当前属性 {attributeCode}。");
        }

        private CombatStatSnapshot CreateFormalCombatStatSnapshot()
        {
            return new CombatStatSnapshot(
                GetAttributeCurrentValue(CharacterAttributes.Attack),
                GetAttributeCurrentValue(CharacterAttributes.Defense),
                GetAttributeCurrentValue(CharacterAttributes.Accuracy),
                GetAttributeCurrentValue(CharacterAttributes.Dodge),
                GetAttributeCurrentValue(CharacterAttributes.CriticalChance),
                GetAttributeCurrentValue(CharacterAttributes.CriticalMultiplier));
        }

        private void SyncFormalAbilityRuleRosterFromRuntime()
        {
            if (!TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                return;
            }

            foreach (int formalGasAbilityCode in CreateOwnedFormalGasAbilityCodeSnapshot())
            {
                abilitySet.RegisterFormalGasAbilityRule(formalGasAbilityCode);
            }
        }
    }
}
