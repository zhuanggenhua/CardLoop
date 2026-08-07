using System.Collections.Generic;
using GAS.Runtime;
using UnityEngine;

namespace GameCore
{
    public abstract partial class CharacterBase
    {
        [Header("GAS")]
        [Tooltip("实体级正式 AbilitySystemComponent。当前阶段只允许角色自己持有，不允许升格为 GameManager 级系统。")]
        [SerializeField] private AbilitySystemComponent m_abilitySystemComponent = null;

        private bool m_isFormalAbilitySystemReady = false;
        private bool m_formalAttributeEventsRegistered = false;
        private bool m_isApplyingFormalCurrentValueMutation = false;
        private readonly Dictionary<EStat, System.Action<float, float>> m_formalCurrentValueChangedHandlers = new();

        public bool TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent)
        {
            if (!m_abilitySystemComponent)
            {
                m_abilitySystemComponent = GetComponent<AbilitySystemComponent>();
            }

            abilitySystemComponent = m_abilitySystemComponent;
            return abilitySystemComponent != null;
        }

        protected void InitializeFormalAbilitySystemFromCurrentAttributes()
        {
            Stats initialBaseStats = CreateStatsSnapshot();
            Stats initialCurrentStats = CreateCurrentStatsSnapshot();

            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            AbilitySystemCellConfig config = new(
                baseTags: System.Array.Empty<int>(),
                attrSets: new[] { FormalGameplayAttributeSet.CreateConfig(initialBaseStats) },
                baseAbilities: System.Array.Empty<AbilityConfig>(),
                level: m_level);

            abilitySystemComponent.Init(config);
            ApplyStatsSnapshotToFormalAbilitySystem(
                abilitySystemComponent,
                initialBaseStats,
                initialCurrentStats,
                initialBaseStats,
                initialCurrentStats);
            m_isFormalAbilitySystemReady = true;
            SyncFormalAbilityRuleRosterFromRuntime();
        }

        protected void ApplyResolvedBaseStatsToFormalAbilitySystem(
            Stats resolvedBaseStats,
            Stats previousBaseStats,
            Stats previousCurrentStats)
        {
            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            Stats nextCurrentStats = CreateAdjustedCurrentStatsSnapshot(previousBaseStats, previousCurrentStats, resolvedBaseStats);
            ApplyStatsSnapshotToFormalAbilitySystem(
                abilitySystemComponent,
                resolvedBaseStats ?? new Stats(),
                nextCurrentStats,
                previousBaseStats,
                previousCurrentStats);
        }

        protected void ApplySavedCurrentStatsToOwnedAttributeTruth(Stats currentStats)
        {
            if (currentStats == null)
            {
                return;
            }

            Stats previousBaseStats = CreateStatsSnapshot();
            Stats previousCurrentStats = CreateCurrentStatsSnapshot();

            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                Debug.LogError($"[{nameof(CharacterBase)}] 读档当前属性必须命中正式 ASC，无法为 {name} 恢复当前值。", this);
                return;
            }

            ApplyStatsSnapshotToFormalAbilitySystem(
                abilitySystemComponent,
                CreateStatsSnapshot(),
                currentStats,
                previousBaseStats,
                previousCurrentStats);
        }

        private bool TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent)
        {
            abilitySystemComponent = null;
            if (!m_isFormalAbilitySystemReady)
            {
                return false;
            }

            if (!TryGetFormalAbilitySystem(out abilitySystemComponent) || abilitySystemComponent == null)
            {
                return false;
            }

            return true;
        }

        private bool TryGetFormalBaseStat(EStat stat, out int value)
        {
            value = 0;
            if (!TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent))
            {
                return false;
            }

            value = Mathf.RoundToInt(abilitySystemComponent.GetAttrBaseValue(
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.GetAttributeCode(stat)));
            return true;
        }

        private bool TryGetFormalCurrentStat(EStat stat, out int value)
        {
            value = 0;
            if (!TryGetInitializedFormalAttributes(out AbilitySystemComponent abilitySystemComponent))
            {
                return false;
            }

            value = Mathf.RoundToInt(abilitySystemComponent.GetAttrCurrentValue(
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.GetAttributeCode(stat)));
            return true;
        }

        private Stats CreateFormalBaseStatsSnapshot()
        {
            Stats snapshot = new();
            foreach (FormalAttributeDefinition definition in FormalAttributeCatalog.Definitions)
            {
                snapshot[definition.Stat] = ReadFormalBaseStatOrReportFailure(definition.Stat);
            }

            return snapshot;
        }

        private Stats CreateFormalCurrentStatsSnapshot()
        {
            Stats snapshot = new();
            foreach (FormalAttributeDefinition definition in FormalAttributeCatalog.Definitions)
            {
                snapshot[definition.Stat] = ReadFormalCurrentStatOrReportFailure(definition.Stat);
            }

            return snapshot;
        }

        private CombatStatSnapshot CreateFormalCombatStatSnapshot()
        {
            Stats currentStats = CreateFormalCurrentStatsSnapshot();
            return new CombatStatSnapshot(
                currentStats[EStat.PhysicalAttack],
                currentStats[EStat.MagicalAttack],
                currentStats[EStat.PhysicalDefense],
                currentStats[EStat.MagicalDefense],
                currentStats[EStat.Agility],
                currentStats[EStat.Luck]);
        }

        private bool TrySetFormalCurrentStat(EStat stat, int value)
        {
            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return false;
            }

            int attributeCode = FormalGameplayAttributeSet.GetAttributeCode(stat);
            ExecuteFormalCurrentValueMutation(() =>
            {
                abilitySystemComponent.SetAttrCurrentValue(FormalGameplayAttributeSet.SetCode, attributeCode, value);
            });
            return abilitySystemComponent.GetAttrCurrentValue(FormalGameplayAttributeSet.SetCode, attributeCode) == value;
        }

        protected void RegisterFormalAttributeEvents()
        {
            if (m_formalAttributeEventsRegistered ||
                !TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            foreach (FormalAttributeDefinition definition in FormalAttributeCatalog.Definitions)
            {
                EStat stat = definition.Stat;
                int attributeCode = FormalGameplayAttributeSet.GetAttributeCode(stat);

                System.Action<float, float> currentHandler = (oldValue, _) => OnFormalCurrentValueChanged(stat, oldValue);
                m_formalCurrentValueChangedHandlers[stat] = currentHandler;

                GASEventCenter.RegisterOnAttrCurrentValueChangeAfter(
                    abilitySystemComponent.Cell,
                    FormalGameplayAttributeSet.SetCode,
                    attributeCode,
                    currentHandler);
            }

            m_formalAttributeEventsRegistered = true;
        }

        protected void UnregisterFormalAttributeEvents()
        {
            if (!m_formalAttributeEventsRegistered ||
                !TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return;
            }

            foreach (FormalAttributeDefinition definition in FormalAttributeCatalog.Definitions)
            {
                EStat stat = definition.Stat;
                int attributeCode = FormalGameplayAttributeSet.GetAttributeCode(stat);

                if (m_formalCurrentValueChangedHandlers.TryGetValue(stat, out System.Action<float, float> currentHandler))
                {
                    GASEventCenter.UnRegisterOnAttrCurrentValueChangeAfter(
                        abilitySystemComponent.Cell,
                        FormalGameplayAttributeSet.SetCode,
                        attributeCode,
                        currentHandler);
                }
            }

            m_formalCurrentValueChangedHandlers.Clear();
            m_formalAttributeEventsRegistered = false;
        }

        protected bool TryRecalculateFormalCurrentAttributesImmediately()
        {
            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return false;
            }

            return abilitySystemComponent.Cell.Entity.TryRecalculateAttributeCurrentValue();
        }

        protected bool TryRecalculateFormalCurrentAttributeImmediately(EStat stat)
        {
            if (!TryGetFormalAbilitySystem(out AbilitySystemComponent abilitySystemComponent) ||
                abilitySystemComponent == null)
            {
                return false;
            }

            AttributeHelper.RecalculateCurrentValue(
                abilitySystemComponent.Cell.Entity,
                FormalGameplayAttributeSet.SetCode,
                FormalGameplayAttributeSet.GetAttributeCode(stat));
            return true;
        }

        protected void ExecuteFormalCurrentValueMutation(System.Action mutation)
        {
            if (mutation == null)
            {
                return;
            }

            bool previousMutationState = m_isApplyingFormalCurrentValueMutation;
            m_isApplyingFormalCurrentValueMutation = true;
            try
            {
                mutation();
            }
            finally
            {
                m_isApplyingFormalCurrentValueMutation = previousMutationState;
            }
        }

        private void OnFormalCurrentValueChanged(EStat stat, float oldValue)
        {
            if (!m_isApplyingFormalCurrentValueMutation)
            {
                return;
            }

            Stats previousCurrentStats = CreateCurrentStatsSnapshot();
            previousCurrentStats[stat] = Mathf.RoundToInt(oldValue);
            NotifyCurrentStatsChanged(previousCurrentStats);
        }

        private int ReadFormalBaseStatOrReportFailure(EStat stat)
        {
            if (TryGetFormalBaseStat(stat, out int value))
            {
                return value;
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 正式基础属性快照缺失字段 {stat}，当前角色无法从正式属性真相读取该字段。", this);
            return 0;
        }

        private int ReadFormalCurrentStatOrReportFailure(EStat stat)
        {
            if (TryGetFormalCurrentStat(stat, out int value))
            {
                return value;
            }

            Debug.LogError($"[{nameof(CharacterBase)}] 正式当前属性快照缺失字段 {stat}，当前角色无法从正式属性真相读取该字段。", this);
            return 0;
        }

        private void ApplyStatsSnapshotToFormalAbilitySystem(
            AbilitySystemComponent abilitySystemComponent,
            Stats baseStats,
            Stats currentStats,
            Stats previousBaseStats,
            Stats previousCurrentStats)
        {
            if (abilitySystemComponent == null)
            {
                return;
            }

            Stats safeBaseStats = baseStats ?? new Stats();
            Stats safeCurrentStats = currentStats ?? safeBaseStats;

            foreach (FormalAttributeDefinition definition in FormalAttributeCatalog.Definitions)
            {
                int attributeCode = FormalGameplayAttributeSet.GetAttributeCode(definition.Stat);
                abilitySystemComponent.SetAttrValues(
                    FormalGameplayAttributeSet.SetCode,
                    attributeCode,
                    safeBaseStats[definition.Stat],
                    safeCurrentStats[definition.Stat]);
            }

            if (IsAttributeBootstrapReadWindowOpen())
            {
                m_attributeBootstrapBuffer.MirrorFromFormalSnapshots(safeBaseStats, safeCurrentStats);
            }

            PublishStatChanges(previousBaseStats, previousCurrentStats);
        }

        private static Stats CreateAdjustedCurrentStatsSnapshot(Stats previousBaseStats, Stats previousCurrentStats, Stats nextBaseStats)
        {
            Stats safePreviousBaseStats = previousBaseStats ?? new Stats();
            Stats safePreviousCurrentStats = previousCurrentStats ?? new Stats();
            Stats safeNextBaseStats = nextBaseStats ?? new Stats();
            return safePreviousCurrentStats + (safeNextBaseStats - safePreviousBaseStats);
        }

        private void SyncFormalAbilityRuleRosterFromRuntime()
        {
            if (TryGetOwnedAbilitySet(out CharacterAbilitySet abilitySet))
            {
                foreach (int formalGasAbilityCode in CreateOwnedFormalGasAbilityCodeSnapshot())
                {
                    abilitySet.RegisterFormalGasAbilityRule(formalGasAbilityCode);
                }

            }
        }
    }
}
